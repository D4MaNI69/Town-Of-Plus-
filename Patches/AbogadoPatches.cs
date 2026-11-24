using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.Events.Vanilla.Meeting.Voting;
using TownOfPlus.Modules;

namespace TownOfPlus.Patches;

/// <summary>Ciclo de vida del estado del Abogado (partida y rondas).</summary>
public static class AbogadoCicloVidaEvents
{
    [RegisterEvent]
    public static void AlEmpezarPartida(IntroBeginEvent _)
    {
        try { AbogadoManager.ReiniciarPartida(); } catch { }
    }

    [RegisterEvent]
    public static void AlEmpezarRonda(RoundStartEvent @event)
    {
        try
        {
            if (!@event.TriggeredByIntro)
            {
                AbogadoManager.QuitarSoloMarca();
            }
        }
        catch { }
    }

    [RegisterEvent]
    public static void AlTerminarPartida(GameEndEvent _)
    {
        try { AbogadoManager.ReiniciarPartida(); } catch { }
    }
}

/// Votos del Abogado: Terminar y Chantaje (redirigido a skip por el host).
public static class AbogadoVotosEvents
{
    [RegisterEvent]
    public static void AlProcesarVotos(ProcessVotesEvent @event)
    {
        try
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            {
                return;
            }

            var votos = @event?.Votes;
            if (votos == null)
            {
                return;
            }

            if (AbogadoManager.TerminarActivo)
            {
                AbogadoManager.ForzarSkipEnCalculo(votos);
                @event!.ExiledPlayer = null;
                @event.OverruledVote = false;
                return;
            }

            // CHANTAJE: el voto del marcado se redirige a SKIP (aunque haya
            // votado a un jugador). Si su voto decidía la expulsión, se
            // recalcula con la lista ya corregida para que no cuente.
            if (AbogadoManager.RedirigirChantajeEnCalculo(votos))
            {
                var exiliado = MiraAPI.Voting.VotingUtils.GetExiled(votos, out var empate);
                @event!.ExiledPlayer = exiliado;
                @event.OverruledVote = empate;
            }
        }
        catch { }
    }
}

/// Motor del Abogado: tick, tinte y Terminar.
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class AbogadoMotorPatch
{
    public static void Postfix()
    {
        try { AbogadoManager.Actualizar(); } catch { }
        try { AbogadoManager.AplicarTinteNombres(); } catch { }

        if (MeetingHud.Instance == null)
        {
            return;
        }

        try
        {
            AbogadoManager.SostenerTerminar();
        }
        catch { }
    }
}
/// Reuniones del Abogado: contador de cooldown y limpieza.
public static class AbogadoReunionEvents
{
    [RegisterEvent]
    public static void AlEmpezarReunion(StartMeetingEvent _)
    {
        try
        {
            // La marca de Chantaje DEBE sobrevivir a la reunión: es lo que
            // redirige su voto a skip en el cálculo del host. Se limpia al
            // terminar la reunión (WrapUp) y al empezar la siguiente ronda.
            AbogadoManager.ContarReunion();
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
public static class AbogadoWrapUpPatch
{
    public static void Postfix()
    {
        try { AbogadoManager.LimpiarReunion(); } catch { }
    }
}

