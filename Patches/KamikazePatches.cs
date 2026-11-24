using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.Events.Vanilla.Meeting.Voting;
using MiraAPI.GameOptions;
using MiraAPI.Networking;
using MiraAPI.Utilities;
using TownOfPlus.Modules;
using TownOfPlus.Networking;
using TownOfPlus.Options.Impostor.ImpostorConcealing;
using TownOfPlus.Roles.Impostor.ImpostorConcealing;
using UnityEngine;

namespace TownOfPlus.Patches;

/// <summary>
///     Eventos de reunión del Kamikaze: contador de reuniones, bloqueo de
///     votos de sus aliados contra él y detonación al ser expulsado en la
///     reunión de detonación (reunión N).
/// </summary>
public static class KamikazeMeetingEvents
{
    /// <summary>Al iniciar una reunión: cuenta la reunión y limpia estados.</summary>
    [RegisterEvent]
    public static void AlIniciarReunion(StartMeetingEvent _)
    {
        try
        {
            KamikazeManager.AlIniciarReunion();
        }
        catch
        {
            // Nunca romper la reunión por un error del rol.
        }
    }

    /// <summary>
    ///     El Kamikaze y sus aliados (impostores + Alliance vinculados) NO
    ///     pueden votarlo: su voto se convierte en saltar (skip).
    /// </summary>
    [RegisterEvent]
    public static void AntesDeVotar(BeforeVoteEvent @event)
    {
        try
        {
            var kamikaze = KamikazeManager.ObtenerKamikaze();
            if (kamikaze?.Player == null || kamikaze.Player.Data.IsDead)
            {
                return;
            }

            // ¿El voto va dirigido al Kamikaze?
            if (@event.VoteArea == null ||
                @event.VoteArea.PlayerId != kamikaze.Player.PlayerId)
            {
                return;
            }

            // ¿El votante es aliado (impostor, Alliance impostor o el propio Kamikaze)?
            if (!KamikazeManager.EsAliado(@event.Player))
            {
                return;
            }

            @event.Cancel();

            if (MeetingHud.Instance == null)
            {
                return;
            }

            // El voto aliado se convierte en un voto de "saltar".
            @event.VoteArea.SetVote(252);
            MeetingHud.Instance.Confirm(252);
        }
        catch
        {
            // Nunca romper la votación por un error del rol.
        }
    }

    /// <summary>
    ///     CORAZÓN DE LA VICTORIA ESPECIAL: si el Kamikaze es expulsado en la
    ///     reunión de detonación (reunión N), los impostores ganan. Si muere
    ///     de cualquier otra forma, no se activa nada.
    /// </summary>
    [RegisterEvent]
    public static void AlExpulsar(EjectionEvent @event)
    {
        try
        {
            var exiliado = @event.ExileController?.initData?.networkedPlayer?.Object;
            if (exiliado == null || exiliado.Data == null ||
                exiliado.Data.Role is not KamikazeRole)
            {
                return;
            }

            var reunionObjetivo = (int)OptionGroupSingleton<KamikazeOptions>
                .Instance.ReunionDetonacion.Value;

            if (KamikazeManager.ContadorReuniones != reunionObjetivo)
            {
                return;
            }

            // Sincroniza la detonación a todos los clientes (RPC).
            KamikazeRpc.EnviarDetonacion(exiliado);

            // Solo el host dispara la victoria automática de impostores.
            if (PlayerControl.LocalPlayer.IsHost())
            {
                KamikazeManager.ProgramarVictoria();
            }
        }
        catch
        {
            // Nunca romper la expulsión por un error del rol.
        }
    }
}

/// <summary>
///     Motor del Kamikaze: corre SIEMPRE desde HudManager.Update
///     (invisibilidad, reversión del shapeshift, arrastres).
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class KamikazeMotorPatch
{
    public static void Postfix()
    {
        try
        {
            KamikazeManager.Actualizar();
        }
        catch
        {
            // Nunca romper el HUD por un error del rol.
        }
    }
}