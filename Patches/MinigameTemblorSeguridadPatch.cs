using HarmonyLib;
using TownOfPlus.Modules;

namespace TownOfPlus.Patches;

/// <summary>
///     Válvula de seguridad del temblor de tareas del sabotaje de Reactor.
///     Cuando el juego ABRE una tarea (<see cref="Minigame.Begin" />) o la
///     CIERRA (<see cref="Minigame.Close" />), el dueño de la posición del
///     minigame vuelve a ser el juego: aquí reiniciamos nuestro estado para que
///     el "deshacer offset" no pueda aplicarse nunca a una posición que no
///     hayamos escrito nosotros (eso dejaba la interfaz de la tarea fuera de
///     pantalla y el jugador atrapado sin poder moverse).
/// </summary>
[HarmonyPatch(typeof(Minigame), nameof(Minigame.Begin))]
public static class MinigameBeginTemblorPatch
{
    public static void Postfix()
    {
        try { SabotajesMejoradosManager.OlvidarTemblorTarea(); }
        catch { }
    }
}

[HarmonyPatch(typeof(Minigame), nameof(Minigame.Close))]
public static class MinigameCloseTemblorPatch
{
    public static void Postfix()
    {
        // Al cerrar SÍ se deshace el offset (fue lo último que escribimos),
        // para devolver el minigame exactamente a su posición original.
        try { SabotajesMejoradosManager.RestaurarTemblorTarea(); }
        catch { }
    }
}
