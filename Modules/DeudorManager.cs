using System.Collections.Generic;
using System.Linq;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using MiraAPI.Voting;
using TownOfPlus.Networking;
using TownOfPlus.Roles.Crewmate.CrewmatePower;
using TownOfPlus.Utilities;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Modules;

/// <summary>
///     ESTADO DEL DEUDOR (crewmate power).
///     <para>· TRATO: el Deudor marca jugadores VIVOS cercanos (rango 1.5).
///     Puede marcar varios por ronda (cooldown entre marcas) y no puede
///     repetir a alguien ya marcado esa ronda. El nombre del marcado se tiñe
///     de #A18262 SOLO en el cliente del Deudor.</para>
///     <para>· DEUDA: si un marcado usa una habilidad (kill vanilla o botón
///     MiraAPI), pasa a deber un favor. El aviso "Le debes un favor al Deudor"
///     lo ve SOLO el marcado. Ventilar/ductos NO cuentan.</para>
///     <para>· VOTO: al calcular la votación, el voto de cada deudor se fuerza
///     al mismo destino que el voto del Deudor (jugador o skip), como el
///     Dictador. Solo el host hace la redirección.</para>
///     <para>· RESET: al terminar CADA reunión se limpian marcas, tintes y
///     deudas, para que el Deudor vuelva a marcar desde cero.</para>
/// </summary>
public static class DeudorManager
{
    /// <summary>ID del voto de "skip" en el sistema de votos de MiraAPI.</summary>
    private const byte VotoSkip = 253;

    // ── ESTADO COMPARTIDO (sincronizado por RPC) ────────────────────────────

    /// <summary>Marcados de cada Deudor (deudorId → ids marcados esta ronda).</summary>
    private static readonly Dictionary<byte, HashSet<byte>> MarcadosPorDeudor = new();

    /// <summary>Deudores que marcaron a cada jugador, en orden de marcado.</summary>
    private static readonly Dictionary<byte, List<byte>> DeudoresDeMarcado = new();

    /// <summary>Favores pendientes: idDeudor(dominado) → idDeudor(acreedor).</summary>
    private static readonly Dictionary<byte, byte> Favores = new();

    /// <summary>¿El jugador local es un Deudor vivo?</summary>
    public static bool EsLocalDeudor
    {
        get
        {
            var local = PlayerControl.LocalPlayer;
            return local?.Data != null && !local.Data.IsDead && local.Data.Role is DeudorRole;
        }
    }

    /// <summary>¿Este jugador ya fue marcado por este Deudor en la ronda?</summary>
    public static bool EstaMarcado(byte deudorId, byte objetivoId) =>
        MarcadosPorDeudor.TryGetValue(deudorId, out var set) && set.Contains(objetivoId);

    /// <summary>¿Algún Deudor marcó a este jugador en la ronda?</summary>
    public static bool EstaMarcadoPorAlguien(byte objetivoId) =>
        DeudoresDeMarcado.TryGetValue(objetivoId, out var lista) && lista.Count > 0;

    /// <summary>¿Este jugador ya debe un favor?</summary>
    public static bool DebeFavor(byte objetivoId) => Favores.ContainsKey(objetivoId);

    /// <summary>Limpia TODO (partida nueva).</summary>
    public static void ReiniciarPartida()
    {
        MarcadosPorDeudor.Clear();
        DeudoresDeMarcado.Clear();
        Favores.Clear();
    }

    /// <summary>
    ///     Limpia marcas, tintes y deudas al terminar una reunión. Las deudas
    ///     se mantienen hasta aquí para poder redirigir los votos.
    /// </summary>
    public static void LimpiarReunion()
    {
        MarcadosPorDeudor.Clear();
        DeudoresDeMarcado.Clear();
        Favores.Clear();
    }

    /// <summary>Registra a un Deudor al inicializar su rol.</summary>
    public static void RegistrarDeudor(PlayerControl jugador)
    {
        if (jugador?.Data == null)
        {
            return;
        }

        if (!MarcadosPorDeudor.ContainsKey(jugador.PlayerId))
        {
            MarcadosPorDeudor[jugador.PlayerId] = new HashSet<byte>();
        }
    }

    /// <summary>Quita el estado de un jugador que dejó de ser Deudor.</summary>
    public static void QuitarJugador(byte playerId)
    {
        MarcadosPorDeudor.Remove(playerId);

        foreach (var set in MarcadosPorDeudor.Values)
        {
            set.Remove(playerId);
        }

        foreach (var lista in DeudoresDeMarcado.Values)
        {
            lista.Remove(playerId);
        }

        DeudoresDeMarcado.Remove(playerId);
        Favores.Remove(playerId);

        var acreedores = Favores
            .Where(par => par.Value == playerId)
            .Select(par => par.Key)
            .ToList();
        foreach (var id in acreedores)
        {
            Favores.Remove(id);
        }
    }

    // ── TRATO ───────────────────────────────────────────────────────────────

    /// <summary>Marca a un jugador vivo (llamado por el RPC en todos los clientes).</summary>
    public static void Marcar(PlayerControl deudor, PlayerControl objetivo)
    {
        if (deudor?.Data == null || objetivo?.Data == null)
        {
            return;
        }

        var deudorId = deudor.PlayerId;
        var objetivoId = objetivo.PlayerId;

        if (deudorId == objetivoId || objetivo.Data.IsDead || objetivo.Data.Disconnected)
        {
            return;
        }

        if (!MarcadosPorDeudor.TryGetValue(deudorId, out var marcados))
        {
            marcados = new HashSet<byte>();
            MarcadosPorDeudor[deudorId] = marcados;
        }

        // Ya marcado por este Deudor esta ronda.
        if (!marcados.Add(objetivoId))
        {
            return;
        }

        if (!DeudoresDeMarcado.TryGetValue(objetivoId, out var deudores))
        {
            deudores = new List<byte>();
            DeudoresDeMarcado[objetivoId] = deudores;
        }

        if (!deudores.Contains(deudorId))
        {
            deudores.Add(deudorId);
        }

        // Aviso SOLO para el Deudor que hizo el trato.
        var local = PlayerControl.LocalPlayer;
        if (local != null && local.PlayerId == deudorId)
        {
            MostrarNotificacion($"<b>Trataste con {objetivo.Data.PlayerName}</b>");
        }
    }

    /// <summary>
    ///     HOST: un jugador marcado avisó que usó una habilidad. Se otorga el
    ///     favor solo si sigue marcado y todavía no debía nada.
    /// </summary>
    public static void HostProcesarUso(PlayerControl objetivo)
    {
        if (objetivo?.Data == null || objetivo.Data.IsDead)
        {
            return;
        }

        var objetivoId = objetivo.PlayerId;
        if (Favores.ContainsKey(objetivoId))
        {
            return;
        }

        if (AcreedorDe(objetivoId) is not { } acreedor)
        {
            return;
        }

        // El host difunde el favor (y a quién se le debe); los clientes lo aplican.
        DeudorRpc.EnviarFavor(objetivo, acreedor);
    }

    /// <summary>
    ///     Aplica el favor en todos los clientes. El aviso "Le debes un favor
    ///     al Deudor" lo ve SOLO el jugador marcado.
    /// </summary>
    public static void OtorgarFavor(PlayerControl objetivo, byte acreedorId)
    {
        if (objetivo?.Data == null)
        {
            return;
        }

        var objetivoId = objetivo.PlayerId;
        if (Favores.ContainsKey(objetivoId))
        {
            return;
        }

        Favores[objetivoId] = acreedorId;

        var local = PlayerControl.LocalPlayer;
        if (local != null && local.PlayerId == objetivoId)
        {
            MostrarNotificacion("<b>Le debes un favor al Deudor</b>");
        }
    }

    /// <summary>
    ///     Deudor al que un marcado le debe el favor: el primero que lo marcó
    ///     y siga vivo (si ninguno vive, el primero que lo marcó).
    /// </summary>
    private static byte? AcreedorDe(byte objetivoId)
    {
        if (!DeudoresDeMarcado.TryGetValue(objetivoId, out var deudores) || deudores.Count == 0)
        {
            return null;
        }

        foreach (var id in deudores)
        {
            var jugador = MiscUtils.PlayerById(id);
            if (jugador?.Data != null && !jugador.Data.IsDead && jugador.Data.Role is DeudorRole)
            {
                return id;
            }
        }

        return deudores[0];
    }

    // ── TINTE DE NOMBRES (solo visible para el Deudor) ──────────────────────

    /// <summary>
    ///     Tiñe de #A18262 los nombres de los marcados por el Deudor LOCAL.
    ///     Los demás clientes no ven ningún cambio. Se restaura el blanco al
    ///     limpiar las marcas (fin de reunión / ronda).
    /// </summary>
    public static void AplicarTinteNombres()
    {
        try
        {
            var local = PlayerControl.LocalPlayer;
            HashSet<byte>? misMarcados = null;
            if (local?.Data != null && local.Data.Role is DeudorRole &&
                MarcadosPorDeudor.TryGetValue(local.PlayerId, out var encontrados))
            {
                misMarcados = encontrados;
            }

            foreach (var jugador in PlayerControl.AllPlayerControls)
            {
                if (jugador?.cosmetics?.nameText == null || jugador.Data == null)
                {
                    continue;
                }

                if (misMarcados != null && misMarcados.Contains(jugador.PlayerId) && !jugador.Data.IsDead)
                {
                    jugador.cosmetics.nameText.color = DeudorRole.DeudorColor;
                }
                else if (jugador.cosmetics.nameText.color == DeudorRole.DeudorColor)
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

    // ── VOTOS (host) ────────────────────────────────────────────────────────

    /// <summary>
    ///     HOST, en el cálculo de los votos: cada jugador que debe un favor
    ///     vota al MISMO destino que su Deudor (jugador o skip). Si el Deudor
    ///     no votó, no hay destino y no se redirige nada.
    /// </summary>
    public static void RedirigirVotos(List<CustomVote> votos)
    {
        if (votos == null || votos.Count == 0 || Favores.Count == 0)
        {
            return;
        }

        var deudores = CustomRoleUtils.GetActiveRolesOfType<DeudorRole>()
            .Where(d => d.Player?.Data != null && !d.Player.Data.IsDead)
            .ToList();
        if (deudores.Count == 0)
        {
            return;
        }

        foreach (var deudor in deudores)
        {
            var deudorId = deudor.Player.PlayerId;

            // Destino = el voto propio del Deudor (skip incluido). Sin voto
            // propio no se puede saber el destino: no se redirige.
            var indicePropio = votos.FindIndex(v => v.Voter == deudorId);
            if (indicePropio < 0)
            {
                continue;
            }

            var objetivo = votos[indicePropio].Suspect;
            var redirigidos = 0;

            for (var i = 0; i < votos.Count; i++)
            {
                var voto = votos[i];
                if (voto.Voter == deudorId)
                {
                    continue;
                }

                if (!Favores.TryGetValue(voto.Voter, out var acreedor) || acreedor != deudorId)
                {
                    continue;
                }

                voto.Suspect = objetivo;
                votos[i] = voto;
                redirigidos++;
            }

            if (redirigidos > 0)
            {
                TownOfPlusPlugin.Instance?.Log.LogInfo(
                    $"[Deudor] {redirigidos} voto(s) redirigido(s) a " +
                    (objetivo == VotoSkip ? "skip." : $"jugador {objetivo}."));
            }
        }
    }

    // ── UTILIDADES ──────────────────────────────────────────────────────────

    private static void MostrarNotificacion(string texto)
    {
        try
        {
            Helpers.CreateAndShowNotification(
                texto, DeudorRole.DeudorColor, new Vector3(0f, 1f, -20f),
                spr: SpritesDeudor.IconoRolAsset.LoadAsset());
        }
        catch
        {
            // Sin notificación no pasa nada.
        }
    }
}
