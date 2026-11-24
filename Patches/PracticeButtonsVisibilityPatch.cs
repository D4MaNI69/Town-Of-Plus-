using HarmonyLib;
using MiraAPI.Hud;
using TownOfUs.Modules;
using TownOfUs.Roles;
using UnityEngine;

namespace TownOfPlus.Patches;

/// <summary>
///     Garantiza que los botones de práctica/freeplay de TOU Mira
///     (espacio de nombres TownOfUs.Buttons.BaseFreeplay) SOLO se muestren
///     cuando el modo práctica/freeplay está realmente activo.
///
///     Condición de modo práctica:
///     - TutorialManager.InstanceExists (modo tutorial/un jugador)
///
///     Si no se cumple, los botones se ocultan y se desactivan, evitando que
///     aparezcan en partidas normales (lobby → juego sin práctica).
///
///     También incluye una red de seguridad: si por algún bug un jugador VIVO
///     termina con un rol afterlife (IGhostRole), se registra una advertencia.
///     Los guards de IsDead en todos los Convertir* de AfterlifeManager ya
///     impiden que un vivo reciba afterlife.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class PracticeButtonsVisibilityPatch
{
    private static float _ultimoCheck = -999f;
    private const float IntervaloCheck = 0.5f;

    public static void Postfix()
    {
        try
        {
            // Throttle: no comprobar cada frame (evita GC y sobrecarga).
            if (Time.time - _ultimoCheck < IntervaloCheck)
                return;
            _ultimoCheck = Time.time;

            var modoPractica = TutorialManager.InstanceExists;

            if (!modoPractica)
            {
                // FUERA de práctica: ocultar y desactivar todos los botones BaseFreeplay.
                foreach (var button in CustomButtonManager.Buttons)
                {
                    if (button == null)
                        continue;

                    if (button.GetType().Namespace != "TownOfUs.Buttons.BaseFreeplay")
                        continue;

                    try
                    {
                        button.SetActive(false, PlayerControl.LocalPlayer?.Data?.Role!);
                    }
                    catch
                    {
                        // Ignorado: nunca romper el HUD por un botón de práctica.
                    }
                }
            }

            // RED DE SEGURIDAD: si un jugador VIVO tiene un rol afterlife,
            // registrar advertencia (los Convertir* ya impiden esto con IsDead).
            RevertirAfterlifeEnVivos();
        }
        catch
        {
            // Nunca romper el juego por la visibilidad de botones de práctica.
        }
    }

    /// <summary>
    ///     Busca jugadores VIVOS que tengan un rol afterlife (IGhostRole de
    ///     Town of plus+) y lanza una advertencia. Los guards de IsDead en
    ///     todos los Convertir* de AfterlifeManager ya impiden que un vivo
    ///     reciba afterlife; este método es solo una red de seguridad de registro.
    /// </summary>
    private static void RevertirAfterlifeEnVivos()
    {
        try
        {
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p == null || p.Data == null || p.Data.IsDead)
                    continue;

                var rol = p.Data.Role;
                if (rol is not IGhostRole ghostRole || !ghostRole.GhostActive)
                    continue;

                TownOfPlusPlugin.Instance?.Log.LogWarning(
                    $"[Afterlife] SEGURIDAD: {p.Data.PlayerName} está VIVO con rol afterlife " +
                    $"({rol.GetType().Name}). Esto no debería ocurrir: los Convertir* " +
                    "exigen Data.IsDead == true.");
            }
        }
        catch
        {
            // Ignorado.
        }
    }
}
