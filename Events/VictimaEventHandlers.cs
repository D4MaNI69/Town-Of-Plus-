using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.Events.Vanilla.Player;
using TownOfPlus.Modules;

namespace TownOfPlus.Events;

/// <summary>
///     Eventos del rol Víctima:
///     - PlayerDeathEvent (kill en ronda): marca la revelación como pendiente
///       para la PRÓXIMA reunión, y si la Víctima local muere desbloquea su
///       chat para que pueda escribir con los fantasmas.
///     - StartMeetingEvent: se revelan las notas pendientes por kill, y se
///       re-habilita el chat libre de la reunión.
///     - GameEndEvent: al terminar la partida el chat vuelve a estar libre
///       (no se deja el bloqueo de Nota pegado entre partidas).
///     - La EXPULSIÓN se revela al FINALIZAR la animación (ExileController.
///       WrapUp → VictimaExilioWrapUpPatch → VictimaManager.AlTerminarExilio),
///       sin esperar a la próxima reunión.
/// </summary>
public static class VictimaEventHandlers
{
    [RegisterEvent]
    public static void AlMorir(PlayerDeathEvent ev)
    {
        try
        {
            // La expulsión la maneja el WrapUp (reveal al fin de la animación).
            if (ev.DeathReason == DeathReason.Exile)
            {
                return;
            }

            // MUERTE DENTRO DE UNA REUNIÓN: un jugador puede morir en la reunión
            // sin ser expulsado (adivinado/acusado correctamente por un rol de
            // adivinación, EJECUTADO por un rol que mata en la reunión, o
            // asesinado por cualquier habilidad de reunión). En ese caso se
            // revelan YA sus notas + el aviso de que era la Víctima.
            if (MeetingHud.Instance != null)
            {
                VictimaManager.MuerteEnReunion(ev.Player);
            }
            else
            {
                // Kill en ronda: revelación pendiente para la próxima reunión.
                VictimaManager.RegistrarMuerte(ev.Player);
            }

            // Si la Víctima LOCAL murió (kill en ronda) → desbloquear su chat
            // (fantasma) para que pueda escribir con los fantasmas.
            var local = PlayerControl.LocalPlayer;
            if (ev.Player != null && local != null && ev.Player.PlayerId == local.PlayerId)
            {
                VictimaManager.RestablecerChat();
            }
        }
        catch
        {
            // Nunca romper la muerte por un error del rol.
        }
    }

    [RegisterEvent]
    public static void AlComenzarReunion(StartMeetingEvent ev)
    {
        try
        {
            VictimaManager.AlComenzarReunion();
        }
        catch
        {
            // Nunca romper el inicio de reunión por un error del rol.
        }
    }

    [RegisterEvent]
    public static void AlEmpezarPartida(IntroBeginEvent ev)
    {
        try
        {
            // Nueva partida: reset TOTAL (notas incluidas). Es el ÚNICO punto
            // donde se limpian las notas junto con GameEndEvent.
            VictimaManager.ReiniciarPartida();
        }
        catch
        {
            // Nunca romper el inicio de partida por un error del rol.
        }
    }

    [RegisterEvent]
    public static void AlTerminarPartida(GameEndEvent ev)
    {
        try
        {
            // Al terminar la partida (pantalla de game over / vuelta al lobby)
            // el estado se resetea completo: no dejar el bloqueo de Nota pegado
            // entre partidas ni un chat externo concedido.
            VictimaManager.ReiniciarPartida();
        }
        catch
        {
            // Nunca romper el fin de partida por un error del rol.
        }
    }
}