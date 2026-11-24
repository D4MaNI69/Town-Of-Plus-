using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Mira;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting.Voting;
using TownOfPlus.Modules;
using TownOfPlus.Networking;
using TownOfPlus.Roles.Crewmate.CrewmateSupport;

namespace TownOfPlus.Patches;

/// <summary>Ciclo de vida del estado del Colega (partida, rondas y reuniones).</summary>
public static class ColegaCicloVidaEvents
{
    [RegisterEvent]
    public static void AlEmpezarPartida(IntroBeginEvent _)
    {
        try { ColegaManager.ReiniciarPartida(); } catch { }
    }

    /// <summary>
    ///     Tras CADA reunión se limpian las Esperanzas (la marca permanente
    ///     del Colega no se toca). También como respaldo al empezar una ronda
    ///     que no es la introductoria.
    /// </summary>
    [RegisterEvent]
    public static void AlEmpezarRonda(RoundStartEvent @event)
    {
        try
        {
            if (!@event.TriggeredByIntro)
            {
                ColegaManager.LimpiarReunion();
            }
        }
        catch { }
    }

    [RegisterEvent]
    public static void AlTerminarPartida(GameEndEvent _)
    {
        try { ColegaManager.ReiniciarPartida(); } catch { }
    }

    // Los votos extra NO se inflan al empezar la reunión: se aplican al emitir
    // el voto (ver ColegaVotosEvents.AlEmitirVoto), como el Alcalde, para que
    // el Colega vote una sola vez.
}

/// <summary>
///     Detección de uso de habilidades del marcado para ocultar la flecha
///     3 segundos. Cuenta: kill vanilla (BeforeMurderEvent) y botones MiraAPI
///     (TOU Mira, Town of Plus, soft-compat Divani).
///     NO cuenta: ventilar/ductos (esos no pasan por estos eventos).
/// </summary>
public static class ColegaHabilidadEvents
{
    [RegisterEvent]
    public static void AlUsarBotonMira(MiraButtonClickEvent @event)
    {
        try
        {
            var local = PlayerControl.LocalPlayer;
            if (local?.Data == null || local.Data.IsDead)
            {
                return;
            }

            if (!ColegaManager.EstaMarcadoPorColega(local.PlayerId))
            {
                return;
            }

            ColegaRpc.EnviarAvisoUso(local);
        }
        catch { }
    }

    [RegisterEvent]
    public static void AlAsesinar(BeforeMurderEvent @event)
    {
        try
        {
            // BeforeMurderEvent ya se lanza en el host, pero se blinda igual.
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            {
                return;
            }

            var origen = @event?.Source;
            if (origen?.Data == null || origen.Data.IsDead)
            {
                return;
            }

            if (!ColegaManager.EstaMarcadoPorColega(origen.PlayerId))
            {
                return;
            }

            ColegaRpc.EnviarAvisoUso(origen);
        }
        catch { }
    }
}

/// <summary>
///     Esperanza: al calcular los votos (host) se intercambian los votos de
///     la pareja; al mostrar los resultados se deshace el intercambio de
///     datos porque la animación ya lo muestra visualmente (igual que el
///     Swapper de TOU Mira).
/// </summary>
public static class ColegaVotosEvents
{
    /// <summary>
    ///     Igual que el Alcalde: cuando el acompañado del Colega está muerto,
    ///     su ÚNICO click aplica sus 2 votos al mismo objetivo (jugador o skip)
    ///     sin obligarlo a votar varias veces.
    /// </summary>
    [RegisterEvent]
    public static void AlEmitirVoto(HandleVoteEvent @event)
    {
        try
        {
            if (@event == null)
            {
                return;
            }

            var datos = @event.VoteData;
            var votante = datos?.Owner;
            if (datos == null || votante?.Data == null || votante.Data.IsDead)
            {
                return;
            }

            if (votante.Data.Role is not ColegaRole)
            {
                return;
            }

            if (!ColegaManager.VotoCuentaDoble(votante.PlayerId))
            {
                return;
            }

            // Un solo click: 2 votos al mismo objetivo (incluido skip = 253).
            datos.SetRemainingVotes(0);
            for (var i = 0; i < 2; i++)
            {
                datos.VoteForPlayer(@event.TargetId);
            }

            @event.Cancel();
        }
        catch { }
    }

    [RegisterEvent]
    public static void AlProcesarVotos(ProcessVotesEvent @event)
    {
        try
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            {
                return;
            }

            if (@event?.Votes is not { Count: > 0 } votos)
            {
                return;
            }

            ColegaManager.IntercambiarVotos(votos);
        }
        catch { }
    }

    [RegisterEvent]
    public static void AlMostrarResultados(PopulateResultsEvent @event)
    {
        try
        {
            if (@event?.Votes is not { Count: > 0 } votos)
            {
                return;
            }

            ColegaManager.DeshacerVotosParaMostrar(votos);
        }
        catch { }
    }

    [RegisterEvent]
    public static void AlCompletarVotacion(VotingCompleteEvent _)
    {
        try { ColegaManager.IniciarAnimacionEsperanza(); } catch { }
    }
}

/// <summary>Al cerrarse la reunión se limpian las Esperanzas y la flecha.</summary>
[HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
public static class ColegaWrapUpPatch
{
    public static void Postfix()
    {
        try { ColegaManager.LimpiarReunion(); } catch { }
    }
}

/// <summary>La flecha no debe verse durante la reunión.</summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
public static class ColegaMeetingStartPatch
{
    public static void Postfix()
    {
        try { ColegaManager.LimpiarFlecha(); } catch { }
    }
}

/// <summary>Motor del Colega: tinte #3F51B5 y flecha, solo en su cliente.</summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class ColegaMotorPatch
{
    public static void Postfix()
    {
        try { ColegaManager.Actualizar(); } catch { }
    }
}
