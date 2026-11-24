using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MiraAPI.Modifiers;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using Reactor.Utilities;
using TownOfPlus.Assets;
using TownOfPlus.Networking;
using TownOfPlus.Roles.Impostor.ImpostorConcealing;
using TownOfPlus.Options.Impostor.ImpostorConcealing;
using TownOfUs.Assets;
using TownOfUs.Modifiers.Impostor;
using TownOfUs.Modifiers.Game;
using TownOfPlus.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace TownOfPlus.Modules;

/// <summary>Habilidades del ciclo del botón Engaño.</summary>
public enum HabilidadKamikaze
{
    /// <summary>Shapeshift vanilla hacia un jugador vivo aleatorio.</summary>
    Cambiaformas = 0,

    /// <summary>Invisible exactamente 1 segundo.</summary>
    Camuflaje = 1,

    /// <summary>Ducto falso (solo visual) que desaparece a los 3 segundos.</summary>
    DuctoFalso = 2,

    /// <summary>Arrastrar un cuerpo durante un máximo de 3 segundos.</summary>
    Arrastre = 3,
}

/// <summary>
///     Estado y motor del Kamikaze: ciclo de habilidades del botón Engaño,
///     invisibilidad temporal, ductos falsos, arrastre de cuerpos y la
///     condición de victoria especial por expulsión en la reunión de detonación.
/// </summary>
public static class KamikazeManager
{
    /// <summary>Duración del Camuflaje (Swoop, invisible): fija por diseño.</summary>
    public const float DuracionCamuflaje = 3f;

    /// <summary>Duración del ducto falso en pantalla: fija por diseño.</summary>
    public const float DuracionDucto = 3f;

    /// <summary>Duración máxima del arrastre de cuerpo: fija por diseño.</summary>
    public const float DuracionArrastre = 3f;

    /// <summary>Duración del Cambiaformas antes de revertirse solo.</summary>
    public const float DuracionCambiaformas = 10f;

    /// <summary>Distancia máxima para enganchar un cuerpo al arrastrar.</summary>
    public const float DistanciaArrastre = 2f;

    private static GameManager? _juegoActual;
    private static float _finCambiaformas;
    private static float _finCamuflaje;
    private static float _finArrastre;
    private static bool _victoriaProgramada;

    /// <summary>Número de reunión actual (1 = primera reunión de la partida).</summary>
    public static int ContadorReuniones { get; private set; }

    /// <summary>Índice de la próxima habilidad del ciclo (0-3).</summary>
    public static int IndiceCiclo { get; internal set; }

    /// <summary>¿Se activó la detonación? (expulsado en la reunión correcta).</summary>
    public static bool DetonacionActivada { get; private set; }

    /// <summary>¿El jugador local es un Kamikaze vivo?</summary>
    public static bool EsLocalKamikaze
    {
        get
        {
            var local = PlayerControl.LocalPlayer;
            return local != null && local.Data != null && !local.Data.IsDead &&
                   local.Data.Role is KamikazeRole;
        }
    }

    /// <summary>Devuelve el primer Kamikaze activo de la partida (o null).</summary>
    public static KamikazeRole? ObtenerKamikaze() =>
        CustomRoleUtils.GetActiveRolesOfType<KamikazeRole>().FirstOrDefault();

    /// <summary>¿Es este jugador aliado del Kamikaze (impostor o Alliance impostor)?</summary>
    public static bool EsAliado(PlayerControl? jugador)
    {
        if (jugador == null || jugador.Data == null || jugador.Data.Disconnected)
        {
            return false;
        }

        try
        {
            if (jugador.Data.Role.IsImpostor)
            {
                return true;
            }

            // Alliance vinculados a los impostores (mod TOU Mira), si aplica.
            foreach (var mod in jugador.GetModifiers<AllianceGameModifier>())
            {
                if (mod.TrueFactionType == AlliedFaction.Impostor)
                {
                    return true;
                }
            }
        }
        catch
        {
            // Sin el modificador de Alliance: solo vale IsImpostor.
        }

        return false;
    }

    /// <summary>Habilidad que se ejecutará con el próximo uso del botón Engaño.</summary>
    public static HabilidadKamikaze HabilidadActual =>
        (HabilidadKamikaze)(IndiceCiclo % 4);

    /// <summary>Avanza el ciclo de habilidades 1 → 2 → 3 → 4 → 1…</summary>
    public static void AvanzarCiclo() => IndiceCiclo = (IndiceCiclo + 1) % 4;

    /// <summary>Reinicia el estado del rol al empezar una partida.</summary>
    public static void ReiniciarPartida()
    {
        _juegoActual = GameManager.Instance;
        ContadorReuniones = 0;
        IndiceCiclo = 0;
        DetonacionActivada = false;
        _victoriaProgramada = false;
        _finCambiaformas = 0f;
        _finCamuflaje = 0f;
        _finArrastre = 0f;
    }

    /// <summary>Limpia efectos visuales cuando el kamikaze local pierde el rol.</summary>
    public static void LimpiarEstadosLocales()
    {
        _finCambiaformas = 0f;
        _finCamuflaje = 0f;
        _finArrastre = 0f;
    }

    /// <summary>
    ///     Al empezar una reunión: cuenta la reunión (reinicio seguro por
    ///     partida) y limpia los temporizadores locales. Los modifiers de TOU
    ///     Mira (SwoopModifier, DragModifier, MorphlingMorphModifier) se
    ///     quitan SOLOS al empezar la reunión (OnMeetingStart), así no queda
    ///     ningún efecto colgado.
    /// </summary>
    public static void AlIniciarReunion()
    {
        // Nueva partida detectada: reiniciar el contador de reuniones.
        if (_juegoActual != GameManager.Instance)
        {
            ReiniciarPartida();
        }

        ContadorReuniones++;

        _finCambiaformas = 0f;
        _finCamuflaje = 0f;
        _finArrastre = 0f;
    }

    /// <summary>Ejecuta una habilidad del Engaño en este cliente (llamado por RPCs).</summary>
    public static void EjecutarHabilidad(PlayerControl kamikaze, int habilidad)
    {
        if (kamikaze == null || kamikaze.Data == null)
        {
            return;
        }

        switch ((HabilidadKamikaze)(habilidad % 4))
        {
            case HabilidadKamikaze.Camuflaje:
                Camuflaje(kamikaze);
                break;
            case HabilidadKamikaze.DuctoFalso:
                DuctoFalso(kamikaze, kamikaze.GetTruePosition());
                break;
        }
    }

    /// <summary>Elegir un jugador vivo aleatorio (nunca elegible a mano).</summary>
    public static PlayerControl? ElegirObjetivoCambiaformas(PlayerControl kamikaze)
    {
        var candidatos = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p != null && p != kamikaze && p.Data != null &&
                        !p.Data.IsDead && !p.Data.Disconnected)
            .ToList();

        return candidatos.Count == 0
            ? null
            : candidatos[Random.Range(0, candidatos.Count)];
    }

    /// <summary>
    ///     Cambiaformas (MORPH): usa el Morphling de TOU Mira — el modifier
    ///     <see cref="MorphlingMorphModifier"/> cambia el aspecto mediante el
    ///     sistema de apariencias de TOU Mira (mismas animaciones/visual que
    ///     el Morphling). La sincronización la hace MiraAPI (RPC de modifiers).
    /// </summary>
    public static void AplicarShift(PlayerControl kamikaze, PlayerControl objetivo)
    {
        if (objetivo == null)
        {
            return;
        }

        _finCambiaformas = Time.time + DuracionCambiaformas;

        try
        {
            // Evitar morphs apilados: quitar uno activo antes de añadir el nuevo.
            if (kamikaze.HasModifier<MorphlingMorphModifier>())
            {
                kamikaze.RpcRemoveModifier<MorphlingMorphModifier>();
            }

            kamikaze.RpcAddModifier<MorphlingMorphModifier>(objetivo);
        }
        catch (System.Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Kamikaze] Morph (Morphling) falló: {ex.Message}");
        }
    }

    /// <summary>
    ///     Camuflaje (SWOOP): usa el Swooper de TOU Mira —
    ///     <see cref="SwoopModifier"/> aplica la apariencia invisible del
    ///     Swooper y reproduce sus sonidos (SwooperActivateSound al
    ///     desaparecer y SwooperDeactivateSound al reaparecer, los mismos que
    ///     el Swooper). Duración: DuracionCamuflaje (3 segundos).
    /// </summary>
    public static void Camuflaje(PlayerControl kamikaze)
    {
        try
        {
            if (kamikaze.HasModifier<SwoopModifier>())
            {
                kamikaze.RpcRemoveModifier<SwoopModifier>();
            }

            kamikaze.RpcAddModifier<SwoopModifier>();
        }
        catch (System.Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Kamikaze] Swoop falló: {ex.Message}");
        }

        _finCamuflaje = Time.time + DuracionCamuflaje;
    }

    /// <summary>
    ///     Ducto falso (MINE): repite la colocación de vent del Miner de TOU
    ///     Mira (vent real del prefab vanilla, capa/sorting de vents → queda
    ///     DEBAJO de los jugadores) con su MISMO sonido (TouAudio.MineSound).
    ///     El ducto NO se puede usar: collider deshabilitado y fuera de
    ///     ShipStatus.AllVents. Desaparece a los 3 segundos (DuracionDucto).
    /// </summary>
    public static void DuctoFalso(PlayerControl kamikaze, Vector2 posicion)
    {
        try
        {
            AudioTop.Sonar(TouAudio.MineSound);
        }
        catch
        {
            // Sin el audio de TOU Mira no pasa nada.
        }

        GameObject? ducto = null;

        try
        {
            var prefab = ShipStatus.Instance.AllVents[0];
            var vent = Object.Instantiate(prefab, prefab.transform.parent);
            vent.name = "TOP_KamikazeDuctoFalso";
            vent.transform.position = new Vector3(
                posicion.x, posicion.y, posicion.y / 1000f);

            // Mismo setup que MinerRole.RpcPlaceVent de TOU Mira.
            vent.EnterVentAnim = null!;
            vent.ExitVentAnim = null!;
            if (vent.myAnim)
            {
                vent.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                vent.Offset = new Vector3(0f, 0.15f, 0f);
            }

            // El ducto NO se puede usar: sin collider y fuera de AllVents.
            var col = vent.GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.enabled = false;
            }

            if (vent.myRend != null)
            {
                vent.myRend.enabled = true;
            }

            ducto = vent.gameObject;
        }
        catch
        {
            // Fallback: sprite visual simple del proyecto (nunca usable).
            ducto = DuctoFalsoSprite(posicion);
        }

        if (ducto != null)
        {
            Coroutines.Start(CoDestruirDucto(ducto));
        }
    }

    private static GameObject? DuctoFalsoSprite(Vector2 posicion)
    {
        try
        {
            var sprite = TownofplusAssets.DuctoFalsoSprite.LoadAsset();
            if (sprite == null)
            {
                return null;
            }

            var ducto = new GameObject("TOP_KamikazeDuctoFalso");
            ducto.transform.position = new Vector3(
                posicion.x, posicion.y, (posicion.y / 1000f) - 0.001f);

            var rend = ducto.AddComponent<SpriteRenderer>();
            rend.sprite = sprite;
            rend.sortingOrder = 0;
            return ducto;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerator CoDestruirDucto(GameObject ducto)
    {
        yield return new WaitForSeconds(DuracionDucto);

        if (ducto != null)
        {
            Object.Destroy(ducto);
        }
    }

    /// <summary>
    ///     Arrastre (DRAG): usa el Undertaker de TOU Mira —
    ///     <see cref="DragModifier"/> mueve el cuerpo siguiendo al jugador
    ///     (lerp idéntico al Undertaker), lo suelta limpio (z incluida) al
    ///     quitarse y se autolimpia en reunión/muerte. Duración máxima:
    ///     DuracionArrastre (3 segundos).
    /// </summary>
    public static bool IniciarArrastre(PlayerControl kamikaze, byte bodyId)
    {
        var cuerpo = ObtenerCuerpo(bodyId);
        if (cuerpo == null || _finArrastre > 0f)
        {
            return false;
        }

        try
        {
            if (kamikaze.HasModifier<DragModifier>())
            {
                kamikaze.RpcRemoveModifier<DragModifier>();
            }

            kamikaze.RpcAddModifier<DragModifier>(bodyId);
        }
        catch (System.Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Kamikaze] Drag (Undertaker) falló: {ex.Message}");
            return false;
        }

        _finArrastre = Time.time + DuracionArrastre;
        return true;
    }

    /// <summary>Suelta el cuerpo arrastrado por el kamikaze (si lo hay).</summary>
    public static void SoltarCuerpo(PlayerControl kamikaze)
    {
        _finArrastre = 0f;

        try
        {
            if (kamikaze != null && kamikaze.HasModifier<DragModifier>())
            {
                kamikaze.RpcRemoveModifier<DragModifier>();
            }
        }
        catch
        {
            // Si falla, la reunión o la muerte del modifier lo limpian.
        }
    }

    /// <summary>Busca el cuerpo sin reportar más cercano dentro de una distancia.</summary>
    public static DeadBody? CuerpoCercano(PlayerControl jugador, float distancia)
    {
        DeadBody? mejor = null;
        var mejorDist = distancia;

        foreach (var cuerpo in Object.FindObjectsOfType<DeadBody>())
        {
            if (cuerpo == null || cuerpo.Reported)
            {
                continue;
            }

            var dist = Vector2.Distance(jugador.GetTruePosition(), cuerpo.TruePosition);
            if (!(dist < mejorDist))
            {
                continue;
            }

            mejorDist = dist;
            mejor = cuerpo;
        }

        return mejor;
    }

    private static DeadBody? ObtenerCuerpo(byte parentId)
    {
        return Object.FindObjectsOfType<DeadBody>()
            .FirstOrDefault(c => c != null && c.ParentId == parentId);
    }

    /// <summary>
    ///     Motor del rol (HudManager.Update): expira en su plazo el Morph, el
    ///     Swoop y el Drag de TOU Mira (solo desde el cliente del kamikaze,
    ///     que es el dueño legítimo de sus modifiers). Los modifiers mueven el
    ///     cuerpo, aplican la apariencia y se limpian solos en reunión/muerte.
    /// </summary>
    public static void Actualizar()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null || local.Data == null || local.Data.IsDead ||
            local.Data.Disconnected || local.Data.Role is not KamikazeRole)
        {
            return;
        }

        try
        {
            // Cambiaformas: unmorph (Morphling) al cumplirse la duración.
            if (_finCambiaformas > 0f && Time.time >= _finCambiaformas)
            {
                _finCambiaformas = 0f;
                if (local.HasModifier<MorphlingMorphModifier>())
                {
                    local.RpcRemoveModifier<MorphlingMorphModifier>();
                }
            }

            // Camuflaje: unswoop (Swooper) al cumplirse la duración.
            if (_finCamuflaje > 0f && Time.time >= _finCamuflaje)
            {
                _finCamuflaje = 0f;
                if (local.HasModifier<SwoopModifier>())
                {
                    local.RpcRemoveModifier<SwoopModifier>();
                }
            }

            // Arrastre: soltar el cuerpo (Undertaker) al cumplirse el máximo.
            if (_finArrastre > 0f && Time.time >= _finArrastre)
            {
                SoltarCuerpo(local);
            }
        }
        catch
        {
            // Nunca romper el HUD por un error del rol.
        }
    }

    /// <summary>Notifica a todos los clientes la detonación del Kamikaze (RPC).</summary>
    public static void AplicarDetonacion(PlayerControl kamikaze)
    {
        if (DetonacionActivada)
        {
            return;
        }

        DetonacionActivada = true;

        try
        {
            var notif = Helpers.CreateAndShowNotification(
                "<b>¡El Kamikaze ha detonado!</b>",
                Color.white, new Vector3(0f, 2f, -20f));
        }
        catch
        {
            // Sin notificación no pasa nada.
        }
    }

    /// <summary>
    ///     HOST: programa la victoria de impostores tras la expulsión del
    ///     Kamikaze en la reunión de detonación. Espera a que termine la
    ///     reunión y dispara el game over de impostores.
    /// </summary>
    public static void ProgramarVictoria()
    {
        if (_victoriaProgramada)
        {
            return;
        }

        // Solo el host puede disparar el game over (RpcEndGame es un RPC de host).
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        _victoriaProgramada = true;
        Coroutines.Start(CoVictoriaImpostores());
    }

    private static IEnumerator CoVictoriaImpostores()
    {
        // Esperar a que la reunión y su animación de expulsión terminen.
        var espera = 0f;
        while (MeetingHud.Instance != null && espera < 20f)
        {
            espera += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        if (GameManager.Instance == null)
        {
            yield break;
        }

        try
        {
            // PANTALLA DE VICTORIA IMPOSTOR VANILLA: se usa el game over nativo
            // de impostores (ImpostorByVote, porque la victoria llega al ser
            // expulsado). NO es una pantalla personalizada: es la de impostores
            // en sí (título, color de fondo, retratos y "Victoria/Derrota" los
            // pinta el EndGameManager vanilla), y los roles/modifiers con
            // condiciones de victoria propias (Sobreviviente, Hada, etc.) se
            // añaden automáticamente por el sistema estándar de TOU/Mira.
            GameManager.Instance.RpcEndGame(GameOverReason.ImpostorsByVote, false);
        }
        catch (System.Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Kamikaze] No se pudo disparar la victoria: {ex.Message}");
        }
    }
}