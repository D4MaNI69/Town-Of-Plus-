using HarmonyLib;
using MiraAPI.Modifiers;
using TownOfPlus.Modifiers.Crewmate;
using TownOfUs.Modifiers.Game.Assailant;

namespace TownOfPlus.Patches;

/// <summary>
///     ADIVINABLE FORZOSO del modificador ANCLA: el Assassin (impostor) puede
///     adivinarlo en reunión SIEMPRE, aunque la opción «adivinar modificadores»
///     de TOU Mira esté desactivada.
///     <para>TOU Mira filtra el menú de adivinación con
///     <see cref="AssassinModifier.IsModifierGuessable"/> (solo deja pasar
///     modificadores de TOU según sus opciones). Este Postfix fuerza el
///     resultado a true cuando el modificador es <see cref="AnclaModifier"/>,
///     así que se lista en el menú y la adivinación funciona siempre.</para>
/// </summary>
[HarmonyPatch(typeof(AssassinModifier), nameof(AssassinModifier.IsModifierGuessable))]
public static class AnclaSiempreAdivinablePatch
{
    public static void Postfix(BaseModifier baseModifier, ref bool __result)
    {
        if (baseModifier is AnclaModifier)
        {
            __result = true;
        }
    }
}
