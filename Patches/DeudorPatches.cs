using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Mira;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting.Voting;
using TownOfPlus.Modules;
using TownOfPlus.Networking;

namespace TownOfPlus.Patches;

/// <summary>Ciclo de vida del estado del Deudor (partida, rondas y reuniones).</summary>
public static class DeudorCicloVidaEvents
{
    [RegisterEvent]
    public static void AlEmpezarPartida(IntroBeginEvent _)
    {
        try { DeudorManager.ReiniciarPartida(); } catch { }
    }

    /// <summary>
    ///     Tras CADA reunión: marcas, tintes y deudas se limpian para que el
    ///     Deudor vuelva a marcar desde cero. También como respaldo al empezar
    ///     una ronda que no es la introductoria.
    /// </summary>
    [RegisterEvent]
    public static void AlEmpezarRonda(RoundStartEvent @event)
    {
        try
        {
            if (!@event.TriggeredByIntro)
            {
                DeudorManager.LimpiarReunion();
            }
        }
        catch { }
    }

    [RegisterEvent]
    public static void AlTerminarPartida(GameEndEvent _)
    {
        try { DeudorManager.ReiniciarPartida(); } catch { }
    }
}

/// <summary>
///     Detección de uso de habilidades de los jugadores marcados.
///     Cuenta: kill vanilla (BeforeMurderEvent) y botones MiraAPI
///     (TOU Mira, Town of Plus, soft-compat Divani).
///     NO cuenta: ventilar/ductos (esos no pasan por estos eventos).
///     El host es quien otorga el favor y lo difunde.
/// </summary>
public static class DeudorHabilidadEvents
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

            if (!DeudorManager.EstaMarcadoPorAlguien(local.PlayerId))
            {
                return;
            }

            // El aviso viaja al host; él valida y difunde el favor.
            DeudorRpc.EnviarAvisoUso(local);
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

            if (!DeudorManager.EstaMarcadoPorAlguien(origen.PlayerId))
            {
                return;
            }

            DeudorRpc.EnviarAvisoUso(origen);
        }
        catch { }
    }
}

/// <summary>
///     Votos del Deudor: los deudores votan al mismo destino que su Deudor
///     (jugador o skip). Solo el host muta la lista; MiraAPI recalcula la
///     expulsión al detectar los votos modificados.
/// </summary>
public static class DeudorVotosEvents
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

            if (@event?.Votes is not { Count: > 0 } votos)
            {
                return;
            }

            DeudorManager.RedirigirVotos(votos);
        }
        catch { }
    }
}

/// <summary>Al cerrarse la reunión se limpian marcas/tintes/deudas.</summary>
[HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
public static class DeudorWrapUpPatch
{
    public static void Postfix()
    {
        try { DeudorManager.LimpiarReunion(); } catch { }
    }
}

/// <summary>Motor del Deudor: tinte #A18262 solo en el cliente del Deudor.</summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class DeudorMotorPatch
{
    public static void Postfix()
    {
        try { DeudorManager.AplicarTinteNombres(); } catch { }
    }
}
