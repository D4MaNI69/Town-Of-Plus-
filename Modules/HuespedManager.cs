using System.Collections;
using System.Linq;
using MiraAPI.GameOptions;
using MiraAPI.Utilities;
using Reactor.Utilities;
using Reactor.Networking.Rpc;
using TownOfPlus.Assets;
using TownOfPlus.Networking;
using TownOfPlus.Options.Crewmate.CrewmateAfterlife;
using TownOfPlus.Roles.Crewmate.CrewmateAfterlife;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Modules;

/// <summary>
///     Estado y efectos del rol Huésped (afterlife crew).
///
///     POSEER — control temporal de un jugador VIVO cercano:
///     - El Huésped (muerto) presiona Poseer sobre un vivo en rango corto; su
///       cliente pide la posesión al host por RPC.
///     - El HOST es la fuente de verdad del inicio/fin: valida, difunde el
///       inicio a todos los clientes y corta la posesión al acabar el tiempo
///       configurado. El MOVIMIENTO lo ejecuta el propio cliente del poseído
///       (cliente-autoritativo, como el juego vanilla): su teclado queda
///       anulado y se le inyecta la dirección del Huésped por
///       SetNormalizedVelocity, así usa las animaciones vanilla, respeta
///       colisiones/muros y su NetTransform lo replica a todos.
///     - Mientras dura la posesión, el Huésped queda anclado sobre el poseído
///       (el fantasma lo sigue; la cámara no se pierde) y el poseído queda
///       bloqueado: sin botones, sin interacciones, sin chat y sin voz
///       (Perfect Comms con soft-dependency).
///     - Al acabar el tiempo, al morir/desconectarse el poseído o empezar una
///       reunión, se difunde el fin y todo vuelve a la normalidad (sin romper
///       meetings ni reconnects: si un cliente se reconecta, la posesión
///       transitoria simplemente se pierde; el rol en sí sigue persistido).
/// </summary>
public static class HuespedManager
{
    /// <summary>Intervalo entre envíos continuos de input del controlador.</summary>
    private const float Tick = 0.033f;

    /// <summary>¿Hay una posesión en curso?</summary>
    public static bool Activa { get; private set; }

    /// <summary>PlayerId del Huésped que controla.</summary>
    public static byte ControladorId { get; private set; }

    /// <summary>PlayerId del jugador poseído (el que mueve el host).</summary>
    public static byte ObjetivoId { get; private set; }

    // Estado solo del HOST (fuente de verdad del inicio/fin).
    private static IEnumerator? _corrutinaPosesion;

    // Última dirección inyectada al poseído (la consume su propio cliente).
    private static Vector2 _movDir;
    private static float _movDirStamp = -999f;

    // Throttle del envío de input del controlador local.
    private static float _ultimoInput = -1f;

    // Última dirección enviada: se usa para enviar AL INSTANTE cuando el
    // controlador cambia de dirección (giros), y solo throttlear mientras
    // mantiene la misma dirección (movimiento continuo).
    private static Vector2 _ultimoEnviado = new Vector2(2f, 2f);

    // Dirección leída del teclado EN EL CLIENTE DEL CONTROLADOR, sin pasar por
    // la red: la consume la PREDICCIÓN local (el controlador mueve la copia
    // local del poseído al mismo tiempo que envía el input), así ambos ven el
    // movimiento en tiempo real y sincronizado.
    private static Vector2 _direccionLocal;
    private static float _dirLocalStamp = -999f;

    /// <summary>¿El jugador local es un Huésped?</summary>
    public static bool EsHuespedLocal =>
        PlayerControl.LocalPlayer?.Data?.Role is HuespedRole;

    /// <summary>¿Este cliente es el Huésped que está controlador en este momento?</summary>
    public static bool SoyControlador =>
        Activa && PlayerControl.LocalPlayer != null &&
        PlayerControl.LocalPlayer.PlayerId == ControladorId;

    /// <summary>¿Este cliente es el jugador poseído en este momento?</summary>
    public static bool SoyObjetivo =>
        Activa && PlayerControl.LocalPlayer != null &&
        PlayerControl.LocalPlayer.PlayerId == ObjetivoId;

    /// <summary>
    ///     El Huésped NO se mueve durante la posesión: queda anclado sobre el
    ///     poseído (el fantasma lo sigue). Se usa en el patch de física.
    /// </summary>
    public static bool FantasmaAnclado => SoyControlador;

    /// <summary>
    ///     Dirección actual del Huésped para el poseído (la consume el propio
    ///     cliente del poseído). Caduca a los 0,4 s sin paquetes: el poseído se
    ///     detiene en vez de seguir andando solo, pero sin 'cortes' con la
    ///     latencia normal de red (el controlador refresca continuamente).
    /// </summary>
    public static Vector2 DireccionActual =>
        Time.time - _movDirStamp < 0.4f ? _movDir : Vector2.zero;

    /// <summary>
    ///     Dirección del teclado leída EN EL CLIENTE DEL CONTROLADOR (sin
    ///     red). Caduca a los 0,25 s si Actualizar deja de correr. La usa el
    ///     patch de predicción para mover la copia local del poseído al mismo
    ///     tiempo que el poseído real se mueve en su propio cliente.
    /// </summary>
    public static Vector2 DireccionLocal =>
        Time.time - _dirLocalStamp < 0.25f ? _direccionLocal : Vector2.zero;

    /// <summary>
    ///     Establece la posesión en ESTE cliente (llega por el RPC de inicio del
    ///     host en todos los clientes). El host arranca además la corrutina que
    ///     corta la posesión al acabar el tiempo.
    public static void EstablecerPosesion(byte controladorId, byte objetivoId, float duracion)
    {
        // Si ya hay una posesión distinta activa, se ignora (una a la vez).
        if (Activa)
        {
            return;
        }

        Activa = true;
        ControladorId = controladorId;
        ObjetivoId = objetivoId;
        _movDir = Vector2.zero;
        _movDirStamp = -999f;
        _ultimoInput = -1f;
        _ultimoEnviado = new Vector2(2f, 2f);
        _direccionLocal = Vector2.zero;
        _dirLocalStamp = -999f;

        Avisar(controladorId, "<b>¡Poseyendo!</b>\nPuedes mover a tu objetivo.");

        // Congela solo al jugador POSEÍDO (su teclado queda anulado; su
        // propio cliente lo mueve con DireccionActual). El Huésped NO se
        // congela con moveable: queda anclado sobre el poseído por el patch.
        CongelarLocal();

        // El HOST solo corta la posesión al acabar el tiempo; el movimiento lo
        // ejecuta el cliente del poseído (animaciones vanilla + colisiones).
        if (AmongUsClient.Instance.AmHost && duracion > 0f)
        {
            if (_corrutinaPosesion != null)
            {
                Coroutines.Stop(_corrutinaPosesion);
                _corrutinaPosesion = null;
            }

            _corrutinaPosesion = Coroutines.Start(CoPoseer(duracion));
        }
    }

    /// <summary>
    ///     Finaliza la posesión localmente en ESTE cliente (limpia flags, detiene
    ///     la corrutina del host). Idempotente: se llama por el RPC de fin y por
    ///     limpiezas locales de seguridad.
    /// </summary>
    public static void FinalizarPosesion()
    {
        if (!Activa)
        {
            return;
        }

        Activa = false;
        ControladorId = 255;
        ObjetivoId = 255;
        _movDir = Vector2.zero;
        _ultimoInput = -1f;
        _direccionLocal = Vector2.zero;
        _dirLocalStamp = -999f;

        // El jugador local recupera el control de su movimiento.
        DescongelarLocal();

        if (_corrutinaPosesion != null)
        {
            try
            {
                Coroutines.Stop(_corrutinaPosesion);
            }
            catch
            {
                // Sin problema si ya terminó.
            }

            _corrutinaPosesion = null;
        }
    }

    /// <summary>
    ///     Actualización por frame (HudManager.Update) del cliente local: el
    ///     Huésped que controla lee su teclado (WASD) y envía la dirección al
    ///     host con throttling, para no saturar la red.
    ///
    ///     El throttling SOLO se aplica mientras se mantiene la MISMA dirección
    ///     (movimiento continuo). Al cambiar de dirección (girar/jugar con el
    ///     WASD) el nuevo vector se envía al instante, así el poseído reacciona
    ///     al mismo tiempo que el Huésped pulsa la tecla y no con retraso.
    /// </summary>
    public static void Actualizar()
    {
        if (!SoyControlador)
        {
            return;
        }

        var dir = LeerInput();

        // Se registra SIEMPRE (antes del throttle): la predicción local del
        // controlador necesita la dirección de ESTE frame, no la del último
        // paquete enviado.
        _direccionLocal = dir;
        _dirLocalStamp = Time.time;

        // ¿La dirección cambió respecto a la última enviada? Si sí → ENVÍO YA
        // (giros instantáneos). Si no → se throttlea el envío continuo.
        var cambio = Mathf.Abs(dir.x - _ultimoEnviado.x) > 0.0001f ||
                     Mathf.Abs(dir.y - _ultimoEnviado.y) > 0.0001f;

        if (!cambio && Time.time - _ultimoInput < Tick)
        {
            return;
        }

        _ultimoInput = Time.time;
        _ultimoEnviado = dir;

        // Canal estilo Puppeteer (TOU Mira): UN solo RPC UNRELIABLE con
        // RpcLocalHandling.Before. El host NO lo procesa y reenvía: Reactor
        // lo difunde a todos los clientes en el MISMO paquete de red (incluido
        // el del poseído y el del propio controlador, que se aplica localmente
        // al instante por Before). Sin relay extra y sin ACK reliable: la
        // dirección llega con el primer datagrama, sin demora añadida.
        Rpc<HuespedInputUnreliableRpc>.Instance.Send(
            PlayerControl.LocalPlayer,
            new HuespedInputPacket(ObjetivoId, dir));
    }

    /// <summary>
    ///     Recibe la dirección del controlador y la guarda con sello de tiempo
    ///     para que la consuma el cliente del poseído.
    ///     SOLO el host la reenvía a todos (host-authoritative); la valida.
    /// </summary>
    public static void SetMovimiento(byte controladorId, float x, float y)
    {
        if (!AmongUsClient.Instance.AmHost || !Activa || controladorId != ControladorId)
        {
            return;
        }

        var dir = new Vector2(x, y);
        if (dir.sqrMagnitude > 1f)
        {
            dir.Normalize();
        }

        HuespedRpc.DifundirDireccion(controladorId, dir.x, dir.y);
    }

    /// <summary>
    ///     Aplica la dirección difundida por el host (corre en TODOS los
    ///     clientes). Solo el cliente del poseído la usa para moverse.
    /// </summary>
    public static void AplicarDireccion(Vector2 dir)
    {
        if (!Activa)
        {
            return;
        }

        _movDir = dir.sqrMagnitude > 1f ? dir.normalized : dir;
        _movDirStamp = Time.time;
    }

    /// <summary>Lee el input WASD del teclado (para el Huésped que controla).</summary>
    private static Vector2 LeerInput()
    {
        var x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        var y = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
        if (x != 0f || y != 0f)
        {
            return new Vector2(x, y).normalized;
        }

        return Vector2.zero;
    }

    /// <summary>
    ///     Congela SOLO al jugador POSEÍDO: su teclado queda anulado en su
    ///     propio cliente y su movimiento lo inyecta el patch con la dirección
    ///     del Huésped (animaciones vanilla intactas).
    ///     El Huésped NO se congela con moveable: queda anclado sobre el
    ///     poseído mediante el patch de física (el fantasma lo sigue).
    /// </summary>
    private static void CongelarLocal()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null || !SoyObjetivo)
        {
            return;
        }

        try
        {
            local.moveable = false;
            local.MyPhysics?.ResetMoveState();
        }
        catch
        {
            // Sin problema si no se puede congelar aquí.
        }
    }

    /// <summary>Restaura el movimiento del jugador local al final de la posesión.</summary>
    private static void DescongelarLocal()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null)
        {
            return;
        }

        try
        {
            local.moveable = true;
        }
        catch
        {
            // Sin problema.
        }
    }

    /// <summary>
    ///     HOST: corta la posesión al cumplirse el tiempo. Además vigila que el
    ///     poseído siga vivo/conectado; si muere o se va, la posesión termina.
    ///     El MOVIMIENTO lo ejecuta el propio cliente del poseído (cliente-
    ///     autoritativo como en vanilla): así usa animaciones normales, respeta
    ///     colisiones/muros y su NetTransform lo replica a todos.
    /// </summary>
    private static IEnumerator CoPoseer(float duracion)
    {
        var fin = Time.time + duracion;

        while (Activa && Time.time < fin)
        {
            var objetivo = ResolverPorId(ObjetivoId);
            if (objetivo == null || objetivo.Data == null || objetivo.Data.IsDead ||
                objetivo.Data.Disconnected)
            {
                // El poseído murió/desapareció: se corta la posesión.
                if (AmongUsClient.Instance.AmHost)
                {
                    HuespedRpc.FinalizarPosesionGlobal();
                }

                yield break;
            }

            yield return new WaitForSeconds(Tick);
        }

        // Tiempo de posesión acabado: se libera el control.
        if (Activa && AmongUsClient.Instance.AmHost)
        {
            HuespedRpc.FinalizarPosesionGlobal();
        }
    }

    /// <summary>Resuelve un PlayerControl por su PlayerId (robusto a reconexiones).</summary>
    private static PlayerControl? ResolverPorId(byte id)
    {
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p != null && p.PlayerId == id)
            {
                return p;
            }
        }

        return null;
    }

    /// <summary>Notificación corta a un jugador concreto (solo en su propio cliente).</summary>
    private static void Avisar(byte playerId, string texto)
    {
        var local = PlayerControl.LocalPlayer;
        if (local != null && local.PlayerId == playerId)
        {
            try
            {
                Helpers.CreateAndShowNotification(
                    texto, Color.white, new Vector3(0f, 1f, -20f),
                    spr: TownofplusAssets.HuespedIcon.LoadAsset());
            }
            catch
            {
                // Sin notificación no pasa nada.
            }
        }
    }
}
