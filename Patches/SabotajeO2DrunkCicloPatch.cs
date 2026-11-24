using HarmonyLib;
using TownOfPlus.Modules;

namespace TownOfPlus.Patches;

/// <summary>
///     El Drunk de "Mejor Sabotaje de O2" debe existir SOLO mientras el
///     sabotaje está activo. Estos parches lo quitan en el instante exacto en
///     que el O2 se repara (MarkClean) o en que el sistema deja de estar
///     activo (Deteriorate / fin de la cuenta atrás), sin esperar al Update.
/// </summary>
[HarmonyPatch(typeof(LifeSuppSystemType), nameof(LifeSuppSystemType.MarkClean))]
public static class LifeSuppReparadoDrunkPatch
{
    public static void Postfix()
    {
        try { SabotajesMejoradosManager.ForzarLimpiarDrunkO2(); }
        catch { }
    }
}

[HarmonyPatch(typeof(LifeSuppSystemType), nameof(LifeSuppSystemType.Deteriorate))]
public static class LifeSuppDeteriorateDrunkPatch
{
    public static void Postfix(LifeSuppSystemType __instance)
    {
        try
        {
            if (__instance == null) return;
            if (!__instance.IsActive || __instance.Countdown <= 0f)
            {
                SabotajesMejoradosManager.ForzarLimpiarDrunkO2();
            }
        }
        catch { }
    }
}