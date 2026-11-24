using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Meeting;
using TownOfPlus.Modules;

namespace TownOfPlus.Patches;

/// <summary>
///     Al empezar una reunión: se cierra cualquier canal de Escape del Ermitaño
///     (quita el PNG del cursor y deja el intento sin
///     cooldown, porque la interrupción no es culpa del jugador).
/// </summary>
public static class ErmitanoReunionEvents
{
    [RegisterEvent]
    public static void AlIniciarReunion(StartMeetingEvent _)
    {
        try
        {
            ErmitanoManager.LimpiarEstadosLocales();
        }
        catch
        {
            // Nunca romper la reunión por un error del rol.
        }
    }
}

/// <summary>
///     Motor del Ermitaño: el canal del Escape (PNG del cursor siguiendo al
///     raton, click izquierdo y timeout de 5 s) corre
///     SIEMPRE desde HudManager.Update, exista o no el botón en pantalla: así el
///     canal nunca se queda colgado ni bloquea el movimiento (mismo aprendizaje
///     que el motor del Francotirador).
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class ErmitanoMotorPatch
{
    public static void Postfix()
    {
        try
        {
            ErmitanoManager.Actualizar();
        }
        catch
        {
            // Nunca romper el HUD por un error del rol.
        }
    }
}