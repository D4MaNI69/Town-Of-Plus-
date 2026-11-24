using MiraAPI.Utilities;
using TownOfPlus.Assets;
using TownOfPlus.Roles.Crewmate.CrewmateSupport;
using TownOfPlus.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TownOfPlus.Modules;

/// <summary>
///     Estado del Aislador:
///     <para>· Soplar: marca jugadores vivos. El nombre se tiñe de #109FB9
///     SOLO en el cliente del Aislador. Un marcado no puede volver a
///     marcarse hasta que la Burbuja limpie las marcas.</para>
///     <para>· Burbuja: encierra a todos los marcados 3 segundos (sin
///     movimiento, sin botones), con el PNG de burbuja encima de cada uno.
///     Al acabar, libres y sin marcas.</para>
///     <para>· Escudo: burbuja que sigue al Aislador en tiempo real; durante
///     la duración fija (4 s) es inmune a kills pero no puede usar Soplar
///     ni Burbuja.</para>
/// </summary>
public static class AisladorManager
{
    /// <summary>Duración de la burbuja grupal: fija por diseño (3 s).</summary>
    public const float DuracionBurbuja = 3f;

    /// <summary>
    ///     Duración del escudo: fija por diseño (4 s, rango sugerido 3–5 s).
    ///     No hay option a propósito para mantener la UI mínima.
    /// </summary>
    public const float DuracionEscudo = 4f;

    // ── ESTADO COMPARTIDO (sincronizado por RPC) ────────────────────────────
    /// <summary>IDs de jugadores marcados con Soplar.</summary>
    public static readonly System.Collections.Generic.HashSet<byte> Marcados = new();

    /// <summary>IDs atrapados en la burbuja grupal actual (vacío si no activa).</summary>
    public static readonly System.Collections.Generic.HashSet<byte> EnBurbuja = new();

    /// <summary>¿El escudo del Aislador está activo?</summary>
    public static bool EscudoActivo { get; private set; }

    // ── TEMPORIZADORES (por cliente, arrancan al recibir el RPC) ────────────
    private static float _finBurbuja;
    private static float _finEscudo;

    /// <summary>¿La burbuja grupal sigue activa en este cliente?</summary>
    public static bool BurbujaActiva => EnBurbuja.Count > 0;

    /// <summary>¿El jugador local está atrapado en la burbuja?</summary>
    public static bool SoyObjetivoBurbuja =>
        PlayerControl.LocalPlayer != null && EnBurbuja.Contains(PlayerControl.LocalPlayer.PlayerId);

    /// <summary>¿El jugador local es un Aislador vivo?</summary>
    public static bool EsLocalAislador
    {
        get
        {
            var local = PlayerControl.LocalPlayer;
            return local != null && local.Data != null && !local.Data.IsDead &&
                   local.Data.Role is AisladorRole;
        }
    }

    /// <summary>¿Está bloqueado el uso de Soplar/Burbuja para el local? (escudo)</summary>
    public static bool HabilidadesBloqueadas => EscudoActivo;

    /// <summary>Limpia todo al empezar una partida nueva.</summary>
    public static void ReiniciarPartida()
    {
        Marcados.Clear();
        EnBurbuja.Clear();
        EscudoActivo = false;
        _finBurbuja = 0f;
        _finEscudo = 0f;
        DestruirBurbujas();
        DestruirBurbujaEscudo();
    }

    /// <summary>Limpia los estados puramente locales (muerte/reunión del dueño).</summary>
    public static void LimpiarEstadosLocales()
    {
        DestruirBurbujas();
        DestruirBurbujaEscudo();
    }

    // ── SOPLAR ──────────────────────────────────────────────────────────────

    public static bool EstaMarcado(PlayerControl? jugador) =>
        jugador != null && Marcados.Contains(jugador.PlayerId);

    /// <summary>Marca a un jugador vivo (llamado por el RPC en todos los clientes).</summary>
    public static void Marcar(PlayerControl aislador, PlayerControl objetivo)
    {
        if (objetivo == null || objetivo.Data == null || objetivo.Data.IsDead)
        {
            return;
        }

        // Un marcado no puede volver a marcarse.
        Marcados.Add(objetivo.PlayerId);

        if (EsLocalAislador)
        {
            try
            {
                Helpers.CreateAndShowNotification(
                    $"<b>{objetivo.Data.PlayerName}</b> marcado", AisladorRole.AisladorColor,
                    new Vector3(0f, 2f, -20f));
            }
            catch
            {
                // Sin notificación no pasa nada.
            }
        }
    }

    // ── BURBUJA GRUPAL ──────────────────────────────────────────────────────

    /// <summary>
    ///     Aplica la burbuja a todos los marcados durante 3 s. Al terminar se
    ///     limpian las marcas y el tinte en todos los clientes.
    /// </summary>
    public static void AplicarBurbuja(PlayerControl aislador)
    {
        if (Marcados.Count == 0)
        {
            return;
        }

        EnBurbuja.Clear();
        foreach (var id in Marcados)
        {
            EnBurbuja.Add(id);
        }

        _finBurbuja = Time.time + DuracionBurbuja;
        CrearBurbujas();
    }

    private static void TerminarBurbuja()
    {
        // POP: solo lo oye el dueño de su propia burbuja (un marcado). El
        // Aislador NO oye los POP de los demás, solo el de su escudo.
        if (SoyObjetivoBurbuja)
        {
            AisladorSonidos.ReproducirPop();
        }

        EnBurbuja.Clear();
        Marcados.Clear(); // Tras Burbuja: libres + sin marcas/tinte.
        DestruirBurbujas();
    }

    // ── ESCUDO ──────────────────────────────────────────────────────────────

    public static void AplicarEscudo(PlayerControl aislador)
    {
        EscudoActivo = true;
        _finEscudo = Time.time + DuracionEscudo;
        CrearBurbujaEscudo();
    }

    private static void TerminarEscudo()
    {
        // POP del escudo: solo el propio Aislador lo oye.
        if (EsLocalAislador)
        {
            AisladorSonidos.ReproducirPop();
        }

        EscudoActivo = false;
        DestruirBurbujaEscudo();
    }

    // ── RELOJ (llamado desde AisladorRole.FixedUpdate) ──────────────────────

    public static void Actualizar()
    {
        if (BurbujaActiva && Time.time >= _finBurbuja)
        {
            TerminarBurbuja();
        }

        if (EscudoActivo && Time.time >= _finEscudo)
        {
            TerminarEscudo();
        }

        SeguirBurbujaEscudo();
        RefrescarBurbujas();
        AplicarTinteNombres();
    }

    // ── TINTE DE NOMBRES (solo visible para el Aislador) ────────────────────

    private static void AplicarTinteNombres()
    {
        if (!EsLocalAislador)
        {
            return;
        }

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p?.cosmetics?.nameText == null || p.Data == null)
            {
                continue;
            }

            if (Marcados.Contains(p.PlayerId) && !p.Data.IsDead)
            {
                p.cosmetics.nameText.color = AisladorRole.AisladorColor;
            }
            else if (p.cosmetics.nameText.color == AisladorRole.AisladorColor)
            {
                p.cosmetics.nameText.color = Color.white;
            }
        }
    }

    // ── SPRITES DE BURBUJA ──────────────────────────────────────────────────

    private static readonly System.Collections.Generic.Dictionary<byte, GameObject> _burbujas = new();
    private static GameObject? _burbujaEscudo;

    private static void CrearBurbujas()
    {
        DestruirBurbujas();
        // Las burbujas son visibles SOLO para: el Aislador (las de todos,
        // filtradas por campo de visión) y cada marcado (únicamente la suya).
        if (!EsLocalAislador)
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || !EnBurbuja.Contains(local.PlayerId))
            {
                return;
            }

            // Solo la burbuja propia del marcado.
            var go = new GameObject("AisladorBurbuja");
            Object.DontDestroyOnLoad(go);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpritesAislador.EfectoBurbujaAsset.LoadAsset();
            renderer.sortingOrder = 15;
            _burbujas[local.PlayerId] = go;
            return;
        }

        foreach (var id in EnBurbuja)
        {
            var go = new GameObject("AisladorBurbuja");
            Object.DontDestroyOnLoad(go);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpritesAislador.EfectoBurbujaAsset.LoadAsset();
            renderer.sortingOrder = 15;
            _burbujas[id] = go;
        }
    }

    private static void RefrescarBurbujas()
    {
        var esAislador = EsLocalAislador;
        var local = PlayerControl.LocalPlayer;
        if (!esAislador && (local == null || !_burbujas.ContainsKey(local.PlayerId)))
        {
            return;
        }

        foreach (var par in _burbujas)
        {
            // Un marcado SOLO refresca su propia burbuja.
            if (!esAislador && par.Key != local!.PlayerId)
            {
                continue;
            }

            var jugador = Resolver(par.Key);
            if (jugador == null || par.Value == null)
            {
                continue;
            }

            var pos = jugador.GetTruePosition();
            par.Value.transform.position = new Vector3(pos.x, pos.y + 0.35f, -5f);
            par.Value.transform.localScale = Vector3.one * 0.6f;

            // Solo para el Aislador: dentro de su campo de visión (distancia a
            // la luz + línea vista sin paredes). El marcado siempre ve la suya.
            par.Value.SetActive(!esAislador || EstaEnCampoDeVision(pos));
        }
    }

    /// <summary>¿Está la posición dentro del campo de visión del jugador local?</summary>
    private static bool EstaEnCampoDeVision(Vector2 posObjetivo)
    {
        try
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null)
            {
                return false;
            }

            var origen = (Vector2)local.GetTruePosition();
            var distancia = Vector2.Distance(origen, posObjetivo);

            // Radio de luz real (cambia con luz/sabotaje de luces).
            // En esta versión del juego la sobrecarga recibe los datos del jugador local.
            var radio = ShipStatus.Instance != null
                ? ShipStatus.Instance.CalculateLightRadius(local.Data)
                : 0f;
            if (radio <= 0f)
            {
                radio = 4f;
            }

            if (distancia > radio)
            {
                return false;
            }

            // Línea de visión: si algo sólido (paredes/obstáculos) hay en medio,
            // la burbuja no se ve aunque la distancia cuadre.
            var golpe = Physics2D.Linecast(
                origen, posObjetivo, Constants.ShipAndObjectsMask);
            return golpe.collider == null;
        }
        catch
        {
            return true; // ante duda, mostrar (nunca ocultar por un error).
        }
    }

    private static void DestruirBurbujas()
    {
        foreach (var go in _burbujas.Values)
        {
            if (go != null)
            {
                try { Object.Destroy(go); } catch { }
            }
        }

        _burbujas.Clear();
    }

    private static void CrearBurbujaEscudo()
    {
        DestruirBurbujaEscudo();
        try
        {
            var aislador = ObtenerAislador();
            var jugadorAislador = aislador?.Player;
            if (jugadorAislador == null)
            {
                return;
            }

            // Solo el propio Aislador ve su burbuja de escudo.
            if (!EsLocalAislador)
            {
                return;
            }

            _burbujaEscudo = new GameObject("AisladorBurbujaEscudo");
            var renderer = _burbujaEscudo.AddComponent<SpriteRenderer>();
            renderer.sprite = SpritesAislador.EfectoBurbujaAsset.LoadAsset();
            renderer.sortingOrder = 16;

            // Se hace HIJA del transform del jugador: el movimiento es suave
            // (sincronizado con el propio renderizado del jugador) y no
            // "tira delante/detrás" como cuando se reposicionaba por tick.
            _burbujaEscudo.transform.SetParent(jugadorAislador.transform, false);
            var z = jugadorAislador.transform.position.z;
            _burbujaEscudo.transform.localPosition = new Vector3(0f, 0.15f, -5f - z);
            // Más abajo y más grande que las burbujas de las marcas.
            _burbujaEscudo.transform.localScale = Vector3.one * 0.75f;
        }
        catch (System.Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Aislador] No se pudo crear la burbuja de escudo: {ex.Message}");
        }
    }

    /// <summary>
    ///     La burbuja del escudo es hija del Aislador, así que sigue su
    ///     movimiento automáticamente. Se mantiene la MISMA escala que las
    ///     burbujas de las marcas (0.6) para que sean idénticas.
    /// </summary>
    private static void SeguirBurbujaEscudo()
    {
        if (_burbujaEscudo != null)
        {
            _burbujaEscudo.transform.localScale = Vector3.one * 0.75f;
        }
    }

    private static void DestruirBurbujaEscudo()
    {
        if (_burbujaEscudo != null)
        {
            try { Object.Destroy(_burbujaEscudo); } catch { }
            _burbujaEscudo = null;
        }
    }

    // ── UTILIDADES ──────────────────────────────────────────────────────────

    public static PlayerControl? Resolver(byte id)
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

    /// <summary>Devuelve el primer Aislador activo de la partida (o null).</summary>
    public static AisladorRole? ObtenerAislador() =>
        MiraAPI.Roles.CustomRoleUtils.GetActiveRolesOfType<AisladorRole>().FirstOrDefault();
}
