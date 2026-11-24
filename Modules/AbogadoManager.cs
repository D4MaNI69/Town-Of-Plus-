using System.Collections.Generic;
using System.Linq;
using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using Reactor.Utilities;
using TownOfPlus.Networking;
using TownOfPlus.Options.Impostor.ImpostorSupport;
using TownOfPlus.Roles.Impostor.ImpostorSupport;
using TownOfPlus.Utilities;
using TownOfUs.Modifiers.Game.Alliance;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Modules;

/// <summary>
///     ESTADO DEL ABOGADO (impostor support).
///     <para>COOLDOWN POR REUNIONES: Terminar disponible en la 1ª
///     reunión; al usarlo, entra en espera N reuniones (ajuste
///     ReunionesEntreUsos, default 2, rango 1–4). El host es la única verdad
///     (contador global) y la difunde con AbogadoRpc.RpcEstado.</para>
///     <para>CHANTAJE: PlayerId del marcado (byte.MaxValue = nadie). Se limpia
///     al terminar la reunión y al empezar la ronda (NO al empezar la reunión:
///     la marca debe sobrevivir para poder redirigir el voto). Siempre usable
///     en partida (sin cooldown por reuniones). El chantajeado vota lo que
///     quiera, pero el HOST redirige su voto a SKIP en el cálculo.</para>
///     <para>TERMINAR: fuerza skip de TODOS (prioridad absoluta sobre el
///     cálculo de votos: ProcessVotesEvent + RpcVotingComplete).</para>
/// </summary>
public static class AbogadoManager
{
    /// <summary>Voto de SKIP en el sistema de votos de MiraAPI.</summary>
    public const byte VotoSkip = 253;

    /// <summary>Sin marca de chantaje.</summary>
    public const byte SinMarca = byte.MaxValue;

    // ── COOLDOWN POR REUNIONES (verdad del host, sincronizada) ────────────

    /// <summary>Nº de reuniones celebradas en la partida (empieza en 0).</summary>
    public static int ReunionesContadas { get; private set; }

    /// <summary>Reunión en la que se usó Terminar (0 = nunca).</summary>
    public static int UltimaReunionUso { get; private set; }

    private static byte _chantajeado = SinMarca;

    /// <summary>La reunión actual tiene Terminar activo (skip forzado).</summary>
    public static bool TerminarActivo { get; private set; }

    // ── FASE: solo en VOTACIÓN tiene sentido Terminar ────────────────────────

    /// <summary>
    ///     ¿La reunión actual está en fase de VOTACIÓN (NotVoted/Voted)?
    ///     Durante la DISCUSIÓN los botones de reunión del Abogado están
    ///     deshabilitados y sus clicks no hacen nada.
    /// </summary>
    public static bool EnVotacion
    {
        get
        {
            var reunion = MeetingHud.Instance;
            if (reunion == null)
            {
                return false;
            }

            try
            {
                var estado = reunion.state;
                return estado != MeetingHud.MeetingStates.Discussion &&
                       estado != MeetingHud.MeetingStates.Animating &&
                       estado != MeetingHud.MeetingStates.Results &&
                       estado != MeetingHud.MeetingStates.Proceeding;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>¿Hay al menos 1 Abogado vivo en la partida?</summary>
    public static bool EsAbogadoPresente()
    {
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p != null && p.Data != null &&
                p.Data.Role is AbogadoRole &&
                !p.Data.IsDead && !p.Data.Disconnected)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Temporizador del watchdog de modifiers (0.5 s).</summary>
    private static float _tiempoWatchdog;

    // CONSULTAS

    public static bool EsAbogado(PlayerControl? jugador) =>
        jugador?.Data?.Role is AbogadoRole;

    public static bool EsAbogadoLocal =>
        PlayerControl.LocalPlayer?.Data?.Role is AbogadoRole;

    /// <summary>Cada cuántas reuniones recupera Terminar (option, default 2).</summary>
    public static int ReunionesEntreUsos
    {
        get
        {
            try
            {
                return (int)OptionGroupSingleton<AbogadoOptions>.Instance.ReunionesEntreUsos.Value;
            }
            catch
            {
                return 2;
            }
        }
    }

    /// <summary>
    ///     ¿Terminar disponible en la reunión actual?
    ///     1ª reunión (contador 1) siempre SÍ. Después: solo si pasaron N
    ///     reuniones desde el último uso (N = ajuste).
    /// </summary>
    public static bool HabilidadesDisponibles =>
        UltimaReunionUso <= 0 || ReunionesContadas - UltimaReunionUso >= ReunionesEntreUsos;

    /// <summary>Reuniones que faltan para recuperar (0 = disponible).</summary>
    public static int ReunionesRestantes
    {
        get
        {
            if (UltimaReunionUso <= 0)
            {
                return 0;
            }

            var resto = ReunionesEntreUsos - (ReunionesContadas - UltimaReunionUso);
            return resto < 0 ? 0 : resto;
        }
    }

    public static byte Chantajeado => _chantajeado;

    public static bool HayChantaje => _chantajeado != SinMarca;

    /// <summary>
    ///     ¿Es aliado del Abogado? (impostores del equipo, incluido el propio
    ///     Abogado, y Alliance vinculada a la facción impostor). Misma regla
    ///     que DictadorManager.EsAliado (se reutiliza para no duplicar).
    /// </summary>
    public static bool EsAliado(PlayerControl? p) => DictadorManager.EsAliado(p);

    /// <summary>
    ///     ¿El local PUEDE ver el tinte de chantaje? (Abogado, impostores y
    ///     alliances del mismo equipo).
    /// </summary>
    public static bool LocalVeTinte()
    {
        var local = PlayerControl.LocalPlayer;
        return local != null && (EsAbogado(local) || EsAliado(local));
    }

    // CICLO DE VIDA

    public static void ReiniciarPartida()
    {
        ReunionesContadas = 0;
        UltimaReunionUso = 0;
        _chantajeado = SinMarca;
        TerminarActivo = false;
        _tiempoWatchdog = 0f;
    }

    public static void ReiniciarJugador(PlayerControl jugador)
    {
        // Sin estado por jugador: el cooldown es global por partida.
    }

    public static void SalirDelRol(PlayerControl jugador)
    {
        // Sin estado por jugador que limpiar.
    }

    /// <summary>
    ///     Se limpia solo la marca (al EMPEZAR la ronda siguiente): la marca
    ///     debe sobrevivir a la reunión para redirigir el voto del marcado.
    ///     Terminar gobierna la reunión en curso, por eso NO se toca aquí.
    /// </summary>
    public static void QuitarSoloMarca()
    {
        if (HayChantaje)
        {
            _chantajeado = SinMarca;
            AplicarTinteNombres();
        }
    }

    /// <summary>Se limpia la marca al TERMINAR la reunión (WrapUp) y resetea
    /// Terminar para la siguiente.</summary>
    public static void LimpiarReunion()
    {
        if (HayChantaje)
        {
            Networking.AbogadoRpc.QuitarChantajeGlobal();
        }

        TerminarActivo = false;
        AplicarTinteNombres();
    }

    // ESTADO DE COOLDOWN (host = verdad, difunde estado)

    /// <summary>
    ///     Escribe la verdad del host en todos los clientes (reuniones +
    ///     último uso).
    /// </summary>
    public static void AplicarEstado(int reuniones, int ultimoUso)
    {
        ReunionesContadas = reuniones;
        UltimaReunionUso = ultimoUso;
    }

    /// <summary>HOST: avanza el contador al empezar cada reunión.</summary>
    public static void ContarReunion()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        ReunionesContadas++;
        Networking.AbogadoRpc.DifundirEstado(ReunionesContadas, UltimaReunionUso);
    }

    /// <summary>
    ///     HOST: Terminar disponible solo con cooldown cumplido. Marca el uso
    ///     y difunde. False = en espera.
    /// </summary>
    public static bool GastarTerminar(byte abogadoId)
    {
        if (!HabilidadesDisponibles)
        {
            return false;
        }

        TerminarActivo = true;
        UltimaReunionUso = ReunionesContadas;

        // Si hay reunión activa, el host fuerza el skip ya (por si era la
        // última votación antes del cálculo).
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
        {
            ForzarSkipInmediato();
            Networking.AbogadoRpc.DifundirEstado(ReunionesContadas, UltimaReunionUso);
        }

        return true;
    }

    // TERMINAR (skip forzado de TODOS)

    /// <summary>
    ///     Marca el flag (corre en TODOS los clientes por RPC) y, si hay
    ///     reunión activa, pone visualmente todos los votos en skip.
    /// </summary>
    public static void AplicarTerminar()
    {
        TerminarActivo = true;

        var meeting = MeetingHud.Instance;
        if (meeting == null || meeting.playerStates == null)
        {
            return;
        }

        try
        {
            foreach (var area in meeting.playerStates)
            {
                if (area == null || area.AmDead)
                {
                    continue;
                }

                area.UnsetVote();
                area.ClearButtons();
                area.SetVote(VotoSkip);
                area.SetHasVoted();
                area.VoteComplete = true;
            }

            if (meeting.SkipVoteButton != null)
            {
                meeting.SkipVoteButton.VoteComplete = true;
            }
        }
        catch
        {
            // Un fallo visual no rompe la reunión.
        }
    }

    /// <summary>
    ///     HOST, en el pre-cálculo de votos (ProcessVotesEvent): reescribe
    ///     la lista completa a skip (un voto por votante vivo) y borra el
    ///     exiliado calculado/extra. Así no expulsa a nadie: da igual Mayor,
    ///     Prosecutor, Dictador, votes extra o overrule.
    /// </summary>
    public static void ForzarSkipEnCalculo(System.Collections.Generic.List<MiraAPI.Voting.CustomVote> votos)
    {
        votos.Clear();
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p?.Data == null || p.Data.IsDead || p.Data.Disconnected)
            {
                continue;
            }

            votos.Add(new MiraAPI.Voting.CustomVote(p.PlayerId, VotoSkip));
        }
    }

    private static void ForzarSkipInmediato()
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            return;
        }
    }

    // CHANTAJE (marca en ronda + bloqueo de voto en reunión)

    /// <summary>
    ///     HOST: marca a un jugador (máximo 1 a la vez; la marca anterior se
    ///     reemplaza). El marcado no puede ser aliado impostor/alliance y debe
    ///     estar vivo. Devuelve false si no se puede marcar.
    /// </summary>
    public static bool MarcarChantaje(byte marcadoId)
    {
        var marcado = ResolverPorId(marcadoId);
        if (marcado?.Data == null || marcado.Data.IsDead || marcado.Data.Disconnected)
        {
            return false;
        }

        if (EsAliado(marcado))
        {
            return false;
        }

        AplicarChantaje(marcadoId);
        return true;
    }

    /// <summary>
    ///     Marca el chantajeado (corre en TODOS los clientes por RPC) y aplica
    ///     el tinte de nombre en el acto para quien deba verlo.
    /// </summary>
    public static void AplicarChantaje(byte marcadoId)
    {
        _chantajeado = marcadoId;
        AplicarTinteNombres();
    }

    /// <summary>Quita la marca (corre en TODOS los clientes por RPC).</summary>
    public static void QuitarChantaje()
    {
        _chantajeado = SinMarca;
        AplicarTinteNombres();
    }

    /// <summary>
    ///     HOST, en el pre-cálculo de votos (ProcessVotesEvent): el voto del
    ///     chantajeado se REDIRIGE a SKIP aunque haya votado a un jugador.
    ///     Devuelve true si cambió algo (hay que recalcular la expulsión).
    ///     Sin marca no toca nada.
    /// </summary>
    public static bool RedirigirChantajeEnCalculo(System.Collections.Generic.List<MiraAPI.Voting.CustomVote> votos)
    {
        if (!HayChantaje || votos == null || votos.Count == 0)
        {
            return false;
        }

        var cambiado = false;
        for (var i = 0; i < votos.Count; i++)
        {
            var voto = votos[i];
            if (voto.Voter == _chantajeado && voto.Suspect != VotoSkip)
            {
                voto.Suspect = VotoSkip;
                votos[i] = voto;
                cambiado = true;
            }
        }

        return cambiado;
    }

    // DETECCIÓN DE SABOTAJES: eliminada (ya no dan cargas).

    /// <summary>
    ///     Tick del Abogado (corre en el cliente del local): solo el watchdog
    ///     anti-Saboteur. Sin detección de sabotajes (ya no dan cargas).
    /// </summary>
    public static void Actualizar()
    {
        var local = PlayerControl.LocalPlayer;
        if (local?.Data == null || !EsAbogado(local) || local.Data.IsDead)
        {
            return;
        }

        try
        {
            _tiempoWatchdog += Time.deltaTime;

            if (_tiempoWatchdog >= 0.5f)
            {
                _tiempoWatchdog = 0f;
                QuitarSaboteurDelAbogado(local);
            }
        }
        catch
        {
            // Un tick del rol nunca debe romper la partida.
        }
    }

    // TINTE DEL CHANTajeado + WATCHDOG ANTI-SABOTEUR

    /// <summary>
    ///     Nombre en #535b61 para el marcado, SOLO en los clientes que deben
    ///     verlo (Abogado, impostores aliados y alliances aliadas). El resto
    ///     ve el nombre normal. Se aplica cada frame del local (barato).
    /// </summary>
    public static void AplicarTinteNombres()
    {
        if (!LocalVeTinte())
        {
            return;
        }

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p?.cosmetics?.nameText == null || p.Data == null)
            {
                continue;
            }

            if (HayChantaje && p.PlayerId == _chantajeado && !p.Data.IsDead)
            {
                p.cosmetics.nameText.color = SpritesAbogado.ColorChantaje;
            }
            else if (p.cosmetics.nameText.color == SpritesAbogado.ColorChantaje)
            {
                p.cosmetics.nameText.color = Color.white;
            }
        }
    }

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

    /// <summary>
    ///     El Abogado NUNCA puede llevar Saboteur de TOU Mira ni ningún
    ///     modifier que acelere sabotajes o permita más de 1 sabotaje a la
    ///     vez: se detecta por tipo exacto Y por nombre ("sabot", case-
    ///     insensitive) y se REMUEVE en el acto. Corre en el cliente del
    ///     Abogado y en el host para todos los Abogados (red de seguridad
    ///     si se asignó por bug).
    /// </summary>
    public static void QuitarSaboteurDelAbogado(PlayerControl local)
    {
        try
        {
            QuitarSaboteurDe(local);

            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            {
                foreach (var p in PlayerControl.AllPlayerControls)
                {
                    if (p != null && p != local && EsAbogado(p))
                    {
                        QuitarSaboteurDe(p);
                    }
                }
            }
        }
        catch
        {
            // Nunca romper la partida por un modifier.
        }
    }

    private static void QuitarSaboteurDe(PlayerControl? jugador)
    {
        if (jugador?.Data == null || !EsAbogado(jugador))
        {
            return;
        }

        var componente = jugador.GetModifierComponent();
        if (componente == null)
        {
            return;
        }

        foreach (var modificador in componente.ActiveModifiers.ToArray())
        {
            if (modificador == null)
            {
                continue;
            }

            var tipo = modificador.GetType();
            var nombre = tipo.Name;
            var completo = tipo.FullName ?? string.Empty;

            // Tipo exacto de TOU Mira + red por nombre para cualquier variante
            // ("sabot...": SaboteurModifier y futuros que aceleren sabotajes).
            if (tipo == typeof(TownOfUs.Modifiers.Game.Impostor.SaboteurModifier) ||
                nombre.ToLowerInvariant().Contains("sabot") ||
                completo.ToLowerInvariant().Contains("sabot"))
            {
                try
                {
                    componente.RemoveModifier(modificador);
                }
                catch
                {
                    // Si falla la remoción, el parche del RPC lo bloquea.
                }
            }
        }
    }

    private static float _ultimoCierreTerminar;

    /// <summary>
    ///     TERMINAR (host, cada frame): UI+datos de VIVOS en SKIP con 0 votos
    ///     restantes (los fantasmas NO votan) e invocación de CheckForEndVoting
    ///     para cerrar la reunión YA sin agotar el temporizador.
    /// </summary>
    public static void SostenerTerminar()
    {
        // ── AUTO-TERMINAR en la 1ª reunión al entrar en votación ──
        // Solo la primera reunión (ReunionesContadas == 1) y nunca usado antes
        // (UltimaReunionUso == 0). Se activa sin que el Abogado pulse el botón.
        if (ReunionesContadas == 1 && UltimaReunionUso <= 0 && !TerminarActivo &&
            MeetingHud.Instance != null && EnVotacion && EsAbogadoPresente())
        {
            GastarTerminar(byte.MaxValue); // byte.MaxValue = activación automática del sistema
        }

        if (!TerminarActivo || MeetingHud.Instance == null || !EnVotacion)
        {
            return;
        }

        if (UnityEngine.Time.realtimeSinceStartup - _ultimoCierreTerminar < 0.25f)
        {
            return;
        }

        _ultimoCierreTerminar = UnityEngine.Time.realtimeSinceStartup;

        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        try
        {
            var meeting = MeetingHud.Instance;
            if (meeting.playerStates != null)
            {
                foreach (var area in meeting.playerStates)
                {
                    if (area == null || area.AmDead)
                    {
                        continue;
                    }

                    try
                    {
                        area.SetVote(VotoSkip);
                        area.VoteComplete = true;
                    }
                    catch
                    {
                        // Un fallo por área no detiene el cierre.
                    }
                }
            }

            foreach (var jugador in PlayerControl.AllPlayerControls)
            {
                if (jugador?.Data == null || jugador.Data.IsDead ||
                    jugador.Data.Disconnected)
                {
                    continue;
                }

                try
                {
                    var datos = jugador.GetVoteData();
                    if (datos == null)
                    {
                        continue;
                    }

                    if (!datos.VotedFor(VotoSkip))
                    {
                        datos.Votes.Clear();
                        datos.VoteForPlayer(VotoSkip);
                    }

                    if (datos.VotesRemaining != 0)
                    {
                        datos.SetRemainingVotes(0);
                    }
                }
                catch
                {
                    // Un dato roto no detiene el cierre.
                }
            }

            var cierre = HarmonyLib.AccessTools.Method(
                typeof(MeetingHud), "CheckForEndVoting");
            cierre?.Invoke(meeting, null);
        }
        catch
        {
            // Nunca romper Terminar por un cierre fallido.
        }

    }
}
