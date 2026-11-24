using HarmonyLib;
using MiraAPI.Events.Vanilla.Meeting;
using TownOfPlus.Modules;

namespace TownOfPlus.Events;

/// <summary>
///     Conversión a Afterlife estilo Haunter/Spectre de TOU Mira:
///     ExileController.WrapUp (fin de reunión) → solo el HOST elige un muerto
///     elegible y lo convierte por RPC (AfterlifeManager).
///     Los roles afterlife NO se asignan al inicio ni al morir directamente.
///     TOU Mira engancha además el EjectionEvent (expulsión): se añade como
///     segundo disparador para no perder ninguna oportunidad de conversión.
/// </summary>
[HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
public static class AfterlifeWrapUpPatch
{
    public static void Postfix()
    {
        try
        {
            AfterlifeManager.AlWrapUp();
        }
        catch
        {
            // Nunca romper el WrapUp por un error del rol.
        }
    }
}

/// <summary>
///     Segundo disparador al estilo EXACTO de TOU Mira
///     (GhostRoleEvents.EjectionEventHandler): la expulsión arranca la
///     conversión con 1 segundo de margen. Los guards del manager (activos
///     vs. # DE ROL) evitan conversiones duplicadas con el WrapUp.
/// </summary>
public static class AfterlifeEjectionEvents
{
    [MiraAPI.Events.RegisterEvent]
    public static void AlEyeccion(EjectionEvent _)
    {
        try
        {
            AfterlifeManager.AlWrapUp();
        }
        catch
        {
            // Nunca romper la expulsión por un error del rol.
        }
    }
}
