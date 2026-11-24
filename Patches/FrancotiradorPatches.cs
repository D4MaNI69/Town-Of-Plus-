using HarmonyLib;
using TownOfPlus.Modules;
using UnityEngine;

namespace TownOfPlus.Patches;

/// <summary>
///     Visión libre para el Francotirador durante su zoom out: ve todo el mapa
///     a través de las paredes. Solo afecta a su propio cliente.
/// </summary>
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CalculateLightRadius))]
public static class FrancotiradorVisionPatch
{
    public static void Postfix(NetworkedPlayerInfo player, ref float __result)
    {
        if (player == null || player.Object == null || !player.Object.AmOwner)
        {
            return;
        }

        if (FrancotiradorManager.EstaApuntando)
        {
            __result = Mathf.Max(__result, 999f);
        }
    }
}

/// <summary>
///     Al empezar una reunión se cancela cualquier estado del rifle
///     (zoom, nubes, puntos rojos, congelación) y vuelve la recarga.
/// </summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
public static class FrancotiradorMeetingResetPatch
{
    public static void Postfix()
    {
        FrancotiradorManager.ReiniciarCiclo();
    }
}

/// <summary>
///     CORAZÓN DEL ROL: corre el ciclo del rifle SIEMPRE, esté el botón
///     visible o no. Esto arregla el bug donde el zoom out no terminaba
///     nunca y dejaba al jugador congelado sin poder moverse ni usar botones.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class FrancotiradorMotorPatch
{
    public static void Postfix()
    {
        try
        {
            FrancotiradorManager.Actualizar();
        FlechasDisparo.Actualizar();
        }
        catch
        {
            // Nunca romper el HUD por un error del rol.
        }
    }
}