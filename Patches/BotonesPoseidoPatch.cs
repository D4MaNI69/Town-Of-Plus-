using System.Collections.Generic;
using HarmonyLib;
using MiraAPI.Hud;
using TownOfPlus.Modules;

namespace TownOfPlus.Patches;

/// <summary>
///     BLOQUEO de botones por POSEÍDO y por COMMS saboteadas:
///     <para>· POSEÍDO (Huésped controlando al local): bloqueo TOTAL — botones
///     custom del jugador local ocultos + botones vanilla (kill, uso, reporte,
///     ductos, sabotaje, mascota) OFF cada frame hasta soltar la posesión.</para>
///     <para>· COMMS SABOTEADAS sobre AFTERLIFE: los afterlife CREW/NEUTRAL
///     pierden SOLO sus habilidades propias (botones custom), pero CONSERVAN el
///     botón Usar (tareas) y los ductos/vents. Los afterlife IMPOSTOR no se
///     bloquean en absoluto (sin limitaciones por Comms).</para>
///     Nada se destruye; al soltar la posesión / arreglar Comms todo vuelve solo
///     (el juego reactiva sus botones). El Huésped controlador y el resto no
///     resultan afectados.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class HuespedBotonesPoseidoPatch
{
    /// <summary>¿Este patch dejó los botones VANILLA ocultos en el frame anterior?</summary>
    private static bool _vanillaOculto;

    public static void Postfix(HudManager __instance)
    {
        if (__instance == null)
        {
            return;
        }

        var local = PlayerControl.LocalPlayer;
        var poseido = HuespedManager.Activa && local != null &&
                      local.PlayerId == HuespedManager.ObjetivoId;

        // COMMS saboteadas: SOLO afecta a los afterlife CREW/NEUTRAL (pierden sus
        // habilidades propias). Los afterlife IMPOSTOR quedan exentos del todo.
        var commsAfterlife = CommsSabotageManager.BloqueaHabilidadesAfterlife(local);

        // Poseído = bloqueo total; Comms = bloqueo SOLO de las habilidades custom
        // (Usar y ductos/vents se quedan para el afterlife crew/neutral).
        var ocultarCustom = poseido || commsAfterlife;
        var ocultarVanilla = poseido;

        try
        {
            // 1) Botones custom del local: ocultos mientras es el poseído o
            //    mientras Comms bloquea las habilidades del afterlife.
            if (ocultarCustom)
            {
                var rol = local != null && local.Data != null ? local.Data.Role : null;
                if (rol != null)
                {
                    foreach (var boton in BotonesCustom())
                    {
                        if (boton != null)
                        {
                            boton.SetActive(false, rol);
                        }
                    }
                }
            }

            // 2) Botones vanilla (kill/uso/reporte/ductos/sabotaje/mascota): SOLO
            //    el poseído los pierde. Comms YA NO los oculta: el afterlife
            //    crew/neutral conserva Usar (tareas) y ductos/vents.
            if (ocultarVanilla)
            {
                if (!_vanillaOculto)
                {
                    _vanillaOculto = true;
                }

                BotonesVanilla(__instance, false);
            }
            else if (_vanillaOculto)
            {
                _vanillaOculto = false;
                BotonesVanilla(__instance, true);
            }
        }
        catch
        {
            // Un fallo visual nunca debe romper el HUD.
        }
    }

    private static List<MiraAPI.Hud.CustomActionButton> BotonesCustom()
    {
        var lista = new List<MiraAPI.Hud.CustomActionButton>();
        try
        {
            var botones = CustomButtonManager.Buttons;
            if (botones == null)
            {
                return lista;
            }

            foreach (var b in botones)
            {
                if (b != null)
                {
                    lista.Add(b);
                }
            }
        }
        catch
        {
            // Sin lista de botones no hay nada que ocultar.
        }

        return lista;
    }

    private static void BotonesVanilla(HudManager hud, bool activar)
    {
        if (hud.KillButton != null)
        {
            hud.KillButton.gameObject.SetActive(activar);
        }

        if (hud.UseButton != null)
        {
            hud.UseButton.gameObject.SetActive(activar);
        }

        if (hud.ReportButton != null)
        {
            hud.ReportButton.gameObject.SetActive(activar);
        }

        if (hud.ImpostorVentButton != null)
        {
            hud.ImpostorVentButton.gameObject.SetActive(activar);
        }

        if (hud.SabotageButton != null)
        {
            hud.SabotageButton.gameObject.SetActive(activar);
        }

        if (hud.PetButton != null)
        {
            hud.PetButton.gameObject.SetActive(activar);
        }
    }
}
