using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using TownOfPlus.Modules;

namespace TownOfPlus.Events;

/// <summary>
///     Conversión EXCLUSIVA del Eco (afterlife crew, SOLO muerte en reunión):
///     El host convierte al exiliado/adivinado de ESTA reunión (candidatos que
///     EcoManager vigiló) y le asigna 1 ancla viva aleatoria por RPC.
///     El rol respeta sus sliders vanilla (# DE ROL y PROBABILIDAD %) igual que
///     el Cerrajero/Huésped: sin contador o con el contador ya cubierto, no se
///     convierte a nadie.
///     Los Ecos ya existentes se reúnen junto a su ancla tras la reunión (esto
///     ocurre SIEMPRE, aunque hoy no se asigne ningún Eco nuevo).
///     Corre en el WrapUp DESPUÉS del pipeline normal de AfterlifeManager.
/// </summary>
[HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
public static class EcoWrapUpPatch
{
    public static void Postfix()
    {
        try
        {
            var soyHost = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
            if (soyHost && EcoManager.PuedeAsignarseEco())
            {
                foreach (var candidato in EcoManager.CandidatosEco())
                {
                    var ancla = EcoManager.ElegirAnclaAleatoria();
                    if (ancla == null)
                    {
                        continue;
                    }

                    EcoManager.ConvertirConAncla(candidato, ancla);
                    break;
                }
            }
        }
        catch
        {
            // Nunca romper el WrapUp por el Eco.
        }

        try
        {
            EcoManager.ReunirTrasReunion();
        }
        catch
        {
            // Nunca romper el WrapUp por el Eco.
        }
    }
}
