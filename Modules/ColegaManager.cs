using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MiraAPI.Utilities;
using MiraAPI.Voting;
using Reactor.Utilities;
using TownOfPlus.Networking;
using TownOfPlus.Roles.Crewmate.CrewmateSupport;
using TownOfPlus.Utilities;
using TownOfUs.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TownOfPlus.Modules;

/// <summary>
///     ESTADO DEL COLEGA (crewmate support).
///     <para>· ACOMPAÑAR: el Colega marca a UN jugador VIVO cercano (rango 1.5).
///     La marca es única y PERMANENTE durante toda la partida; después de
///     usarla, el botón desaparece para siempre. El nombre del marcado se tiñe
///     de #3F51B5 SOLO en el cliente del Colega.</para>
///     <para>· FLECHA: solo el Colega la ve. Apunta siempre a su marcado y se
///     oculta 3 s cuando el marcado usa una habilidad (kill o botón MiraAPI);
///     ventilar/ductos NO cuentan. Si el marcado muere, la flecha desaparece
///     para siempre.</para>
///     <para>· VOTOS: si el marcado muere, el voto del Colega CUENTA DOBLE
///     (2 votos) en TODAS las reuniones siguientes, aplicados de una sola vez
///     al mismo objetivo (como el Alcalde). Si sigue vivo, vota normal (1).</para>
///     <para>· ESPERANZA: botón de reunión que intercambia TODOS los votos que
///     van al Colega ↔ los que van a su marcado (mecánica y animación del
///     Swapper de TOU Mira, limitada a esa pareja). Solo funciona mientras el
///     marcado esté vivo.</para>
/// </summary>
public static class ColegaManager
{
    /// <summary>Marcado de cada Colega (colegaId → marcadoId). Permanente.</summary>
    private static readonly Dictionary<byte, byte> MarcaPorColega = new();

    /// <summary>Colegas cuyo marcado murió (la flecha ya no vuelve nunca).</summary>
    private static readonly HashSet<byte> MarcaMuerta = new();

    /// <summary>Parejas de Esperanza activas en la reunión (colegaId → marcadoId).</summary>
    private static readonly Dictionary<byte, byte> EsperanzaActiva = new();

    /// <summary>Colegas que ya usaron Esperanza en esta reunión.</summary>
    private static readonly HashSet<byte> EsperanzaUsada = new();

    private static ArrowBehaviour? _flecha;
    private static byte? _marcaFlecha;
    private static float _flechaOcultaHasta;

    // ── CONSULTAS ───────────────────────────────────────────────────────────

    /// <summary>¿El Colega ya usó Acompañar (tiene marca permanente)?</summary>
    public static bool TieneMarca(byte colegaId) => MarcaPorColega.ContainsKey(colegaId);

    /// <summary>ID del marcado de un Colega, o 255 si no tiene.</summary>
    public static byte MarcaDe(byte colegaId) =>
        MarcaPorColega.TryGetValue(colegaId, out var marcaId) ? marcaId : byte.MaxValue;

    /// <summary>¿Algún Colega tiene marcado a este jugador?</summary>
    public static bool EstaMarcadoPorColega(byte jugadorId) => MarcaPorColega.ContainsValue(jugadorId);

    /// <summary>¿El Colega local ya presionó Esperanza en esta reunión?</summary>
    public static bool YaUsoEsperanza(byte colegaId) => EsperanzaUsada.Contains(colegaId);

    /// <summary>¿El marcado murió o se desconectó?</summary>
    public static bool MarcaEstaMuerta(byte marcaId)
    {
        var marca = MiscUtils.PlayerById(marcaId);
        return marca?.Data == null || marca.Data.IsDead || marca.Data.Disconnected;
    }

    /// <summary>
    ///     ¿El Colega local puede usar Esperanza ahora? Requiere estar vivo,
    ///     tener una marca viva y no haber usado ya Esperanza esta reunión.
    /// </summary>
    public static bool PuedeUsarEsperanzaLocal()
    {
        var local = PlayerControl.LocalPlayer;
        if (local?.Data == null || local.Data.IsDead || local.Data.Role is not ColegaRole)
        {
            return false;
        }

        if (!MarcaPorColega.TryGetValue(local.PlayerId, out var marcaId) ||
            EsperanzaUsada.Contains(local.PlayerId))
        {
            return false;
        }

        return !MarcaEstaMuerta(marcaId);
    }

    // ── CICLO DE VIDA ───────────────────────────────────────────────────────

    /// <summary>Limpia TODO (partida nueva / fin de partida).</summary>
    public static void ReiniciarPartida()
    {
        MarcaPorColega.Clear();
        MarcaMuerta.Clear();
        EsperanzaActiva.Clear();
        EsperanzaUsada.Clear();
        LimpiarFlecha();
    }

    /// <summary>
    ///     Tras CADA reunión se limpian las Esperanzas (la marca permanente
    ///     del Colega NO se limpia: solo desaparece cuando el marcado muere).
    /// </summary>
    public static void LimpiarReunion()
    {
        EsperanzaActiva.Clear();
        EsperanzaUsada.Clear();
        LimpiarFlecha();
    }

    /// <summary>Registra a un Colega (inicialización del rol).</summary>
    public static void RegistrarColega(PlayerControl jugador)
    {
        _ = jugador;
    }

    /// <summary>Quita el estado de un jugador que dejó de ser Colega.</summary>
    public static void QuitarJugador(byte playerId)
    {
        MarcaPorColega.Remove(playerId);
        MarcaMuerta.Remove(playerId);
        EsperanzaActiva.Remove(playerId);
        EsperanzaUsada.Remove(playerId);

        var eraMarca = MarcaPorColega
            .Where(par => par.Value == playerId)
            .Select(par => par.Key)
            .ToList();
        foreach (var colegaId in eraMarca)
        {
            MarcaPorColega.Remove(colegaId);
            EsperanzaActiva.Remove(colegaId);
        }

        var local = PlayerControl.LocalPlayer;
        if (local != null && local.PlayerId == playerId)
        {
            LimpiarFlecha();
        }
    }

    // ── ACOMPAÑAR ───────────────────────────────────────────────────────────

    /// <summary>Marca a un jugador vivo (llamado por el RPC en todos los clientes).</summary>
    public static void Marcar(PlayerControl colega, PlayerControl objetivo)
    {
        if (colega?.Data == null || objetivo?.Data == null)
        {
            return;
        }

        var colegaId = colega.PlayerId;
        var objetivoId = objetivo.PlayerId;

        if (colegaId == objetivoId || objetivo.Data.IsDead || objetivo.Data.Disconnected)
        {
            return;
        }

        // La marca es única y permanente: si ya tiene una, no se cambia.
        if (MarcaPorColega.ContainsKey(colegaId))
        {
            return;
        }

        MarcaPorColega[colegaId] = objetivoId;
        MarcaMuerta.Remove(colegaId);

        var local = PlayerControl.LocalPlayer;
        if (local != null && local.PlayerId == colegaId)
        {
            MostrarNotificacion($"<b>Ahora acompañas a {objetivo.Data.PlayerName}</b>");
        }
    }

    // ── ESPERANZA (host autoritativo) ───────────────────────────────────────

    /// <summary>
    ///     HOST: valida y difunde el uso de Esperanza. Solo un Colega vivo con
    ///     una marca viva y sin haber usado Esperanza esta reunión.
    /// </summary>
    public static void HostProcesarEsperanza(PlayerControl colega)
    {
        if (colega?.Data == null || colega.Data.IsDead || colega.Data.Disconnected)
        {
            return;
        }

        if (colega.Data.Role is not ColegaRole)
        {
            return;
        }

        if (!MarcaPorColega.TryGetValue(colega.PlayerId, out var marcaId) ||
            EsperanzaUsada.Contains(colega.PlayerId) || MarcaEstaMuerta(marcaId))
        {
            return;
        }

        ColegaRpc.EnviarEsperanzaConfirmada(colega, marcaId);
    }

    /// <summary>Aplica la Esperanza en todos los clientes.</summary>
    public static void AplicarEsperanza(PlayerControl colega, byte marcaId)
    {
        if (colega?.Data == null || colega.Data.IsDead)
        {
            return;
        }

        if (colega.Data.Role is not ColegaRole || MarcaEstaMuerta(marcaId))
        {
            return;
        }

        var colegaId = colega.PlayerId;
        if (EsperanzaActiva.ContainsKey(colegaId))
        {
            return;
        }

        // La marca debe coincidir con la marca real del Colega.
        if (!MarcaPorColega.TryGetValue(colegaId, out var marcaReal) || marcaReal != marcaId)
        {
            return;
        }

        EsperanzaActiva[colegaId] = marcaId;
        EsperanzaUsada.Add(colegaId);

        var local = PlayerControl.LocalPlayer;
        if (local != null && local.PlayerId == colegaId)
        {
            MostrarNotificacion("<b>Los votos se intercambiarán con tu Colega</b>");
        }
    }

    /// <summary>
    ///     HOST, al calcular los votos: intercambia TODOS los votos que van al
    ///     Colega con los que van a su marcado (y viceversa). Al mutar la
    ///     lista, MiraAPI recalcula la expulsión.
    /// </summary>
    public static void IntercambiarVotos(List<CustomVote> votos)
    {
        if (votos == null || votos.Count == 0 || EsperanzaActiva.Count == 0)
        {
            return;
        }

        foreach (var (colegaId, marcaId) in EsperanzaActiva)
        {
            if (MarcaEstaMuerta(marcaId))
            {
                continue;
            }

            var cambiados = 0;
            for (var i = 0; i < votos.Count; i++)
            {
                var voto = votos[i];
                if (voto.Suspect == colegaId)
                {
                    voto.Suspect = marcaId;
                    votos[i] = voto;
                    cambiados++;
                }
                else if (voto.Suspect == marcaId)
                {
                    voto.Suspect = colegaId;
                    votos[i] = voto;
                    cambiados++;
                }
            }

            if (cambiados > 0)
            {
                TownOfPlusPlugin.Instance?.Log.LogInfo(
                    $"[Colega] Esperanza: {cambiados} voto(s) intercambiado(s) " +
                    $"entre {colegaId} y {marcaId}.");
            }
        }
    }

    /// <summary>
    ///     En los resultados se deshace el intercambio de datos (igual que el
    ///     Swapper) porque la animación ya muestra visualmente el cambio.
    /// </summary>
    public static void DeshacerVotosParaMostrar(List<CustomVote> votos)
    {
        IntercambiarVotos(votos);
    }

    // ── VOTO DOBLE (si el marcado murió) ────────────────────────────────────

    /// <summary>
    ///     ¿El voto de este Colega cuenta doble? Solo si tiene una marca y ese
    ///     marcado ya murió. Se aplica al EMITIR el voto (como el Alcalde): un
    ///     solo click reparte sus 2 votos al mismo objetivo o al skip.
    /// </summary>
    public static bool VotoCuentaDoble(byte colegaId) =>
        MarcaPorColega.TryGetValue(colegaId, out var marcaId) && MarcaEstaMuerta(marcaId);

    // ── MOTOR VISUAL (tinte + flecha) ───────────────────────────────────────

    /// <summary>Llamado cada frame desde HudManager.Update.</summary>
    public static void Actualizar()
    {
        AplicarTinteNombre();
        ActualizarFlecha();
    }

    /// <summary>
    ///     Tiñe de #3F51B5 el nombre del marcado del Colega LOCAL. Los demás
    ///     clientes no ven ningún cambio.
    /// </summary>
    private static void AplicarTinteNombre()
    {
        try
        {
            var local = PlayerControl.LocalPlayer;
            if (local?.Data == null || local.Data.IsDead || local.Data.Role is not ColegaRole)
            {
                RestaurarNombres();
                return;
            }

            byte? marcaId = MarcaPorColega.TryGetValue(local.PlayerId, out var id) ? id : null;

            foreach (var jugador in PlayerControl.AllPlayerControls)
            {
                if (jugador?.cosmetics?.nameText == null || jugador.Data == null)
                {
                    continue;
                }

                if (marcaId.HasValue && jugador.PlayerId == marcaId.Value && !jugador.Data.IsDead)
                {
                    jugador.cosmetics.nameText.color = ColegaRole.ColegaColor;
                }
                else if (jugador.cosmetics.nameText.color == ColegaRole.ColegaColor)
                {
                    jugador.cosmetics.nameText.color = Color.white;
                }
            }
        }
        catch
        {
            // El tinte nunca debe romper el HUD.
        }
    }

    /// <summary>Restaura los nombres que este mod pudo teñir.</summary>
    private static void RestaurarNombres()
    {
        try
        {
            foreach (var jugador in PlayerControl.AllPlayerControls)
            {
                if (jugador?.cosmetics?.nameText == null)
                {
                    continue;
                }

                if (jugador.cosmetics.nameText.color == ColegaRole.ColegaColor)
                {
                    jugador.cosmetics.nameText.color = Color.white;
                }
            }
        }
        catch
        {
            // Ignorar.
        }
    }

    /// <summary>
    ///     Crea/actualiza la flecha #3F51B5 que solo ve el Colega y que
    ///     apunta siempre a su marcado. Se oculta 3 s tras una habilidad.
    /// </summary>
    private static void ActualizarFlecha()
    {
        try
        {
            var local = PlayerControl.LocalPlayer;
            if (local?.Data == null || local.Data.IsDead || local.Data.Role is not ColegaRole)
            {
                LimpiarFlecha();
                return;
            }

            if (!MarcaPorColega.TryGetValue(local.PlayerId, out var marcaId))
            {
                LimpiarFlecha();
                return;
            }

            var marca = MiscUtils.PlayerById(marcaId);
            if (marca?.Data == null || marca.Data.IsDead || marca.Data.Disconnected)
            {
                if (MarcaMuerta.Add(local.PlayerId))
                {
                    MostrarNotificacion("<b>Tu acompañante murió: tu voto cuenta doble</b>");
                }

                LimpiarFlecha();
                return;
            }

            if (MeetingHud.Instance != null || LobbyBehaviour.Instance != null)
            {
                LimpiarFlecha();
                return;
            }

            if (_flecha == null || _marcaFlecha != marcaId)
            {
                LimpiarFlecha();

                if (Camera.main == null)
                {
                    return;
                }

                _flecha = Helpers.CreateArrow(Camera.main.transform, ColegaRole.ColegaColor);
                _marcaFlecha = marcaId;
                _flechaOcultaHasta = 0f;

                // Flecha propia #3F51B5 (PNG o procedural). El color blanco
                // evita teñir dos veces el sprite.
                try
                {
                    var sprite = SpritesColega.FlechaAsset.LoadAsset();
                    if (sprite != null && _flecha != null)
                    {
                        _flecha.image.sprite = sprite;
                        _flecha.image.color = Color.white;
                    }
                }
                catch
                {
                    // Si falla el sprite propio, queda la flecha nativa teñida.
                }
            }

            if (_flecha == null)
            {
                return;
            }

            var oculta = Time.time < _flechaOcultaHasta;
            if (_flecha.gameObject.activeSelf != !oculta)
            {
                _flecha.gameObject.SetActive(!oculta);
            }

            if (oculta)
            {
                return;
            }

            _flecha.target = marca.GetTruePosition();
            _flecha.Update();
        }
        catch
        {
            // La flecha es cosmética: jamás rompe el HUD.
        }
    }

    /// <summary>
    ///     Oculta 3 segundos la flecha cuando el marcado usa una habilidad.
    ///     Solo actúa en el cliente del Colega dueño de esa marca.
    /// </summary>
    public static void OcultarFlecha3s(byte marcaId)
    {
        try
        {
            var local = PlayerControl.LocalPlayer;
            if (local?.Data == null || local.Data.Role is not ColegaRole)
            {
                return;
            }

            if (!MarcaPorColega.TryGetValue(local.PlayerId, out var id) || id != marcaId)
            {
                return;
            }

            _flechaOcultaHasta = Time.time + 3f;
        }
        catch
        {
            // Cosmético.
        }
    }

    /// <summary>Destruye la flecha local (reunión, muerte, fin de partida).</summary>
    public static void LimpiarFlecha()
    {
        try
        {
            if (_flecha != null)
            {
                Object.Destroy(_flecha.gameObject);
            }
        }
        catch
        {
            // Ignorar.
        }

        _flecha = null;
        _marcaFlecha = null;
        _flechaOcultaHasta = 0f;
    }

    // ── ANIMACIÓN DEL SWAP (igual que el Swapper de TOU Mira) ───────────────

    /// <summary>Arranca la animación de intercambio de las parejas activas.</summary>
    public static void IniciarAnimacionEsperanza()
    {
        if (EsperanzaActiva.Count == 0 || MeetingHud.Instance == null)
        {
            return;
        }

        var pares = EsperanzaActiva.ToList();
        try
        {
            Coroutines.Start(AnimarPares(pares));
        }
        catch
        {
            // La animación es cosmética.
        }
    }

    private static IEnumerator AnimarPares(List<KeyValuePair<byte, byte>> pares)
    {
        var duracion = 4f / (pares.Count + 1);

        foreach (var (colegaId, marcaId) in pares)
        {
            var area1 = BuscarArea(colegaId);
            var area2 = BuscarArea(marcaId);
            if (area1 == null || area2 == null)
            {
                continue;
            }

            var elementos1 = GetUIElements(area1);
            var elementos2 = GetUIElements(area2);
            var votos1 = GetVoteTransforms(area1);
            var votos2 = GetVoteTransforms(area2);

            foreach (var voto in votos2)
            {
                var render = voto.GetComponent<SpriteRenderer>();
                if (render != null)
                {
                    render.material.SetInt(PlayerMaterial.MaskLayer, area1.MaskLayer);
                }
            }

            foreach (var voto in votos1)
            {
                var render = voto.GetComponent<SpriteRenderer>();
                if (render != null)
                {
                    render.material.SetInt(PlayerMaterial.MaskLayer, area2.MaskLayer);
                }
            }

            for (var i = 0; i < elementos1.Length; i++)
            {
                Coroutines.Start(Slide2D(elementos1[i], elementos1[i].position, elementos2[i].position, duracion));
                Coroutines.Start(Slide2D(elementos2[i], elementos2[i].position, elementos1[i].position, duracion));
            }

            yield return new WaitForSeconds(duracion);
        }
    }

    private static PlayerVoteArea? BuscarArea(byte playerId) =>
        MeetingHud.Instance?.playerStates?.FirstOrDefault(area => area.PlayerId == playerId);

    private static Transform[] GetUIElements(PlayerVoteArea voteArea) =>
    [
        voteArea.PlayerIcon.transform,
        voteArea.NameText.transform,
        voteArea.Background.transform,
        voteArea.MaskArea.transform,
        voteArea.PlayerButton.transform,
        voteArea.LevelNumberText.transform,
        voteArea.ColorBlindName.transform,
        voteArea.Overlay.transform,
        voteArea.Megaphone.transform,
    ];

    private static List<Transform> GetVoteTransforms(PlayerVoteArea voteArea)
    {
        var votos = new List<Transform>();
        for (var i = 0; i < voteArea.transform.childCount; i++)
        {
            var hijo = voteArea.transform.GetChild(i);
            if (hijo.name == "playerVote(Clone)")
            {
                votos.Add(hijo);
            }
        }

        return votos;
    }

    private static IEnumerator Slide2D(Transform objetivo, Vector3 origen, Vector3 destino, float duracion) =>
        MiscUtils.PerformTimedAction(duracion, p => objetivo.position = Vector3.Lerp(origen, destino, p));

    // ── UTILIDADES ──────────────────────────────────────────────────────────

    private static void MostrarNotificacion(string texto)
    {
        try
        {
            Helpers.CreateAndShowNotification(
                texto, ColegaRole.ColegaColor, new Vector3(0f, 1f, -20f),
                spr: SpritesColega.IconoRolAsset.LoadAsset());
        }
        catch
        {
            // Sin notificación no pasa nada.
        }
    }
}
