using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.Hud;
using TownOfPlus.Modules;
using TownOfPlus.Roles.Crewmate.CrewmateSupport;
using UnityEngine;

namespace TownOfPlus.Patches;

/// <summary>
///     Inmunidad del escudo del Aislador: cancela cualquier intento de muerte
///     contra él mientras su burbuja de escudo esté activa (copiado del
///     escudo/barrier de TOU Mira, vía BeforeMurderEvent como el Huésped).
/// </summary>
public static class AisladorInmunidadEvents
{
    [RegisterEvent]
    public static void BeforeMurderHandler(BeforeMurderEvent @event)
    {
        if (AisladorManager.EscudoActivo && @event.Target != null &&
            @event.Target.Data?.Role is AisladorRole)
        {
            @event.Cancel();
        }
    }

    /// <summary>
    ///     Feedback al asesino bloqueado: su pantalla parpadea gris
    ///     transparente y su botón de kill entra en cooldown de 5 s.
    /// </summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer),
        new[] { typeof(PlayerControl), typeof(MurderResultFlags) })]
    public static class AisladorFeedbackEscudoPatch
    {
        public static void Prefix(PlayerControl __instance, PlayerControl target)
        {
            try
            {
                if (AisladorManager.EscudoActivo && target != null &&
                    target.Data?.Role is AisladorRole)
                {
                    // Cooldown de 5 segundos en el botón de kill.
                    try { __instance.SetKillTimer(5f); } catch { }

                    ParpadearGris();
                }
            }
            catch
            {
                // Nunca romper un asesinato por el feedback.
            }
        }

        /// <summary>
        ///     Parpadeo gris transparente — réplica exacta de MiscUtils.CoFlash
        ///     de TOU Mira (el mismo que usa el Medic): clona el FullScreen vanilla
        ///     del HUD, lo escala ×10, muestra el color 1 s y lo desactiva.
        /// </summary>
        private static void ParpadearGris()
        {
            try
            {
                var hud = HudManager.Instance;
                if (hud != null && hud.FullScreen != null)
                {
                    Reactor.Utilities.Coroutines.Start(CoFlashGris());
                }
            }
            catch
            {
                // Un fallo del flash nunca debe romper el asesinato.
            }
        }

        private static SpriteRenderer? _flashRenderer;

        private static System.Collections.IEnumerator CoFlashGris()
        {
            // Gris transparente, misma alpha (0.3) que el flash del Medic.
            var color = new Color(0.45f, 0.45f, 0.45f, 0.3f);

            var hud = HudManager.Instance;
            if (hud == null || hud.FullScreen == null)
            {
                yield break;
            }

            if (_flashRenderer == null)
            {
                _flashRenderer = UnityEngine.Object.Instantiate(hud.FullScreen, hud.FullScreen.transform.parent);
                _flashRenderer.transform.localScale *= 10f;
            }

            _flashRenderer.enabled = true;
            _flashRenderer.gameObject.SetActive(true);
            _flashRenderer.color = color;

            yield return new WaitForSeconds(1f);

            if (_flashRenderer != null && _flashRenderer.color.Equals(color))
            {
                _flashRenderer.enabled = false;
            }
        }
    }
}

/// <summary>
///     Congela a los atrapados en la burbuja grupal: se anula su velocidad
///     cada tick (mismo camino que el bloqueo del poseído del Huésped, pero
///     con velocidad cero).
/// </summary>
[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
public static class AisladorBurbujaFreezePatch
{
    public static void Postfix(PlayerPhysics __instance)
    {
        if (__instance?.body == null || !AisladorManager.BurbujaActiva)
        {
            return;
        }

        try
        {
            var miJugador = __instance.myPlayer;
            var local = PlayerControl.LocalPlayer;
            if (local == null || miJugador == null || miJugador.PlayerId != local.PlayerId)
            {
                return;
            }

            if (AisladorManager.SoyObjetivoBurbuja)
            {
                try { __instance.HandleAnimation(miJugador.Data.IsDead); } catch { }
                try { __instance.SetNormalizedVelocity(Vector2.zero); } catch { }
            }
        }
        catch
        {
            // Un tick de burbuja nunca debe romper la física.
        }
    }
}

/// <summary>
///     Bloqueo de botones para los atrapados en la burbuja: el mismo
///     comportamiento que el poseído del Huésped (BotonesPoseidoPatch):
///     botones custom de TOU Mira/ToP ocultos + vanilla (kill/uso/reporte/
///     ductos/sabotaje/mascota) OFF cada frame hasta que acabe la burbuja.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class AisladorBloqueoBotonesPatch
{
    private static bool _aplicado;

    public static void Postfix(HudManager __instance)
    {
        if (__instance == null)
        {
            return;
        }

        var bloquear = AisladorManager.SoyObjetivoBurbuja;
        var local = PlayerControl.LocalPlayer;

        if (!bloquear)
        {
            if (_aplicado)
            {
                _aplicado = false;
                try { BotonesVanilla(__instance, true); } catch { }
            }

            return;
        }

        _aplicado = true;

        try
        {
            // 1) Botones custom del local: ocultos durante la burbuja.
            var rol = local != null && local.Data != null ? local.Data.Role : null;
            if (rol != null)
            {
                var botones = CustomButtonManager.Buttons;
                if (botones != null)
                {
                    foreach (var boton in botones)
                    {
                        if (boton != null)
                        {
                            boton.SetActive(false, rol);
                        }
                    }
                }
            }

            // 2) Botones vanilla: kill/uso/reporte/ductos/sabotaje/mascota OFF.
            BotonesVanilla(__instance, false);
        }
        catch
        {
            // Un fallo visual nunca debe romper el HUD.
        }
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

/// <summary>
///     Reloj del rol: burbujas (fin de la burbuja grupal), escudo (fin +
///     burbuja que sigue al Aislador) y tinte de nombres. Se engancha al
///     PlayerControl.FixedUpdate (mismo camino que Cerrajero/Amalgama) y
///     solo corre UNA vez por frame (jugador local).
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class AisladorTickPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        var local = PlayerControl.LocalPlayer;
        if (__instance == null || local == null || __instance.PlayerId != local.PlayerId)
        {
            return;
        }

        try
        {
            // Un solo tick global (el estado del Aislador es estático).
            AisladorManager.Actualizar();
        }
        catch
        {
            // Un tick del rol nunca debe romper la física.
        }
    }
}

/// <summary>
///     Al empezar una reunión: burbujas, escudo y marcas se limpian.
/// </summary>
public static class AisladorReunionEvents
{
    [RegisterEvent]
    public static void AlIniciarReunion(StartMeetingEvent _)
    {
        try
        {
            AisladorManager.ReiniciarPartida();
        }
        catch
        {
            // Nunca romper la reunión por un error del rol.
        }
    }
}
