using HarmonyLib;
using Reactor.Patches;

namespace TownOfPlus.Patches;

/// <summary>
///     Añade la línea "Town Of Plus (versión)" a la lista de versiones/mods
///     del menú (la que construye Reactor con Reactor/BepInEx/Mods: N).
///     La versión sale de TownOfPlusPlugin.Version, así que se actualiza sola
///     en cada actualización del mod.
/// </summary>
[HarmonyPatch(typeof(ReactorVersionShower), nameof(ReactorVersionShower.UpdateText))]
public static class ModsLinePatch
{
    public static void Postfix()
    {
        try
        {
            if (ReactorVersionShower.Text == null)
            {
                return;
            }

            var text = ReactorVersionShower.Text;
            if (text.text.Contains("Town Of Plus"))
            {
                return;
            }

            text.text += $"\n<color=#FF5E00>Town Of Plus v{TownOfPlusPlugin.Version}</color>";
        }
        catch
        {
            // Nunca romper el menú por un error del patch.
        }
    }
}