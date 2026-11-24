using HarmonyLib;
using TownOfPlus.Modules;
using TownOfPlus.Utilities;
using UnityEngine;

namespace TownOfPlus.Patches;

/// <summary>
///     Al empezar una reunión, si el Ritualista completó sus marcas, el chat
///     le revela las facciones de sus objetivos (mensaje visible solo para él).
/// </summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
public static class RitualistaReunionPatch
{
    public static void Postfix()
    {
        RitualistaManager.AlIniciarReunion();
    }
}

/// <summary>
///     Los jugadores marcados por el Ritualista muestran su nombre en color
///     crema. Solo afecta al cliente del Ritualista.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class RitualistaNombresPatch
{
    public static void Postfix()
    {
        try
        {
            if (!RitualistaManager.EsLocalRitualista)
            {
                return;
            }

            // Avisar (y restar tiros) si mataron o expulsaron a un objetivo.
            RitualistaManager.ComprobarObjetivosMuertos();

            var crema = (Color)SpritesRitual.Crema;

            // En REUNIÓN: teñir los nombres de los objetivos en la lista de votos.
            if (MeetingHud.Instance != null)
            {
                foreach (var area in MeetingHud.Instance.playerStates)
                {
                    if (area != null &&
                        RitualistaManager.EstaMarcado(area.PlayerId) &&
                        area.NameText != null)
                    {
                        area.NameText.color = crema;
                    }
                }

                return;
            }

            // Fuera de reunión: teñir los nombres sobre los jugadores.
            for (var i = 0; i < PlayerControl.AllPlayerControls.Count; i++)
            {
                var p = PlayerControl.AllPlayerControls[i];
                if (p == null || p.cosmetics == null || p.cosmetics.nameText == null)
                {
                    continue;
                }

                if (RitualistaManager.EstaMarcado(p.PlayerId))
                {
                    p.cosmetics.nameText.color = crema;
                }
            }
        }
        catch
        {
            // Nunca romper el HUD por un error cosmético.
        }
    }
}

// NOTA: el patch "RitualistaBurbujaChatPatch" (tinte crema de la burbuja de
// chat) fue eliminado: apuntaba a ChatController.AddChat(PlayerControl,string),
// firma que ya no existe en la versión actual del juego, y su fallo abortaba
// TODO el PatchAll. Si se quiere recuperar, usar TargetMethod() con búsqueda por reflexión.
