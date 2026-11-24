using HarmonyLib;
using TownOfPlus.Modules;
using TownOfPlus.Roles.Neutral.NeutralAfterlife;

namespace TownOfPlus.Patches;

/// <summary>
///     Motor per-frame del estado afterlife de la Amalgama (patrón TOU Mira):
///     TOU Mira ejecuta los ghost roles mediante sus propios patches sobre
///     IGhostRole; este postfix delega en AmalgamaRole.FixedUpdate(), que
///     ejecuta FadeUpdate() → Player.GhostFade() (semitransparencia mientras
///     el fantasma está activo, tanto quieta como en movimiento).
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class AmalgamaFixedUpdatePatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (__instance == null || __instance.Data == null || __instance.Data.Role is not AmalgamaRole amalgama)
        {
            return;
        }

        try
        {
            amalgama.FixedUpdate();
        }
        catch
        {
            // Ante cualquier error en el fade, no romper el juego.
        }
    }
}

/// <summary>
///     FIN DE PARTIDA: al volver al lobby se restauran la apariencia y el
///     nombre ORIGINALES de cada Amalgama que revivió copiando la apariencia
///     de un cuerpo (la copia SOLO dura esa partida). También se cortan los
///     efectos de canalización (overlay/sonido) si quedaran activos.
/// </summary>
[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
public static class AmalgamaGameEndPatch
{
    public static void Postfix()
    {
        try
        {
            AmalgamaManager.RestaurarAparienciasOriginales();
        }
        catch
        {
            // La restauración es una red de seguridad: si falla, el juego
            // re-sincroniza el outfit de todos al empezar la siguiente.
        }

        try
        {
            AmalgamaManager.LimpiarAparienciasOriginales();
        }
        catch
        {
            // Nada crítico.
        }

        try
        {
            AmalgamaManager.PararEfectosRevive();
        }
        catch
        {
            // Nada crítico.
        }

        try
        {
            AmalgamaSenales.Limpiar();
        }
        catch
        {
        }
    }
}

/// <summary>
///     Motor por frame de las señales de cuerpos (flecha gris + parpadeo gris)
///     exclusivas para la Amalgama local, estilo Amnesiac de TOU Mira.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class AmalgamaSenalesHudUpdatePatch
{
    public static void Postfix()
    {
        try
        {
            AmalgamaSenales.Actualizar();
        }
        catch
        {
        }
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
public static class AmalgamaLimpiarSenalesEnReunionPatch
{
    public static void Postfix()
    {
        try
        {
            AmalgamaSenales.Limpiar();
        }
        catch
        {
        }
    }
}
