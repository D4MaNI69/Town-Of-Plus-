using HarmonyLib;
using MiraAPI.GameOptions;
using TownOfPlus.Options.General;
using TownOfUs.Options.Modifiers;

namespace TownOfPlus.Patches;

/// <summary>
///     Cuando "Mejor Sabotaje de O2" estÃ¡ ACTIVADO, el modificador Drunk de
///     TOU Mira se usa SOLO de forma temporal durante el sabotaje de O2 (lo
///     aplica SabotajesMejoradosManager). Por eso se fuerzan sus opciones de
///     probabilidad (DrunkChance) y cantidad (DrunkAmount) a 0 mientras se
///     asignan los modificadores al inicio de la partida, para que NADIE salga
///     Drunk por asignaciÃ³n aleatoria.
///     Si la opciÃ³n estÃ¡ DESACTIVADA no se toca nada y el Drunk de TOU Mira
///     funciona con normalidad.
/// </summary>
[HarmonyPatch(typeof(MiraAPI.Modifiers.ModifierManager),
    nameof(MiraAPI.Modifiers.ModifierManager.AssignModifiers))]
public static class SabotajeDrunkCeroPatch
{
    private static float _chancePrevio = -1f;
    private static float _amountPrevio = -1f;

    private static bool DebeForzar()
    {
        try
        {
            return OptionGroupSingleton<VisibilidadAfterlifeOptions>.Instance
                ?.MejorSabotajeO2.Value == true;
        }
        catch
        {
            return false;
        }
    }

    public static void Prefix()
    {
        if (!DebeForzar())
        {
            return;
        }

        try
        {
            var opts = OptionGroupSingleton<UniversalModifierOptions>.Instance;
            if (opts == null)
            {
                return;
            }

            _chancePrevio = opts.DrunkChance.Value;
            _amountPrevio = opts.DrunkAmount.Value;

            opts.DrunkChance.SetValue(0f, false);
            opts.DrunkAmount.SetValue(0f, false);
        }
        catch
        {
            _chancePrevio = -1f;
            _amountPrevio = -1f;
        }
    }

    public static void Postfix()
    {
        if (_chancePrevio < 0f && _amountPrevio < 0f)
        {
            return;
        }

        try
        {
            var opts = OptionGroupSingleton<UniversalModifierOptions>.Instance;
            if (opts != null)
            {
                if (_chancePrevio >= 0f)
                {
                    opts.DrunkChance.SetValue(_chancePrevio, false);
                }

                if (_amountPrevio >= 0f)
                {
                    opts.DrunkAmount.SetValue(_amountPrevio, false);
                }
            }
        }
        catch
        {
            // Si falla la restauraciÃ³n, se deja el valor actual.
        }

        _chancePrevio = -1f;
        _amountPrevio = -1f;
    }
}
