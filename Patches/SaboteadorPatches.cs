using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.Events.Vanilla.Meeting.Voting;
using MiraAPI.Patches.Stubs;
using MiraAPI.Roles;
using TownOfPlus.Modules;
using TownOfPlus.Roles.Impostor.ImpostorAfterlife;
using TownOfUs.Roles;
using UnityEngine;

namespace TownOfPlus.Patches;

/// <summary>
///     Inmunidad total del Saboteador: cancela cualquier intento de muerte
///     (vanilla o custom) contra él, igual que el Cerrajero.
/// </summary>
public static class SaboteadorImmunityEvents
{
    [RegisterEvent]
    public static void BeforeMurderHandler(BeforeMurderEvent @event)
    {
        if (@event.Target?.Data?.Role is SaboteadorRole)
        {
            @event.Cancel();
        }
    }
}

/// <summary>
///     Visibilidad del Saboteador (COPIA del sistema del Cerrajero): regla
///     VANILLA de Among Us — un observador vivo NO ve fantasmas; uno muerto,
///     afterlife (IGhostRole) o el propio jugador SÍ. No muestra sprite
///     fantasma especial: solo muestra/oculta el jugador.
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class SaboteadorVisualPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (__instance == null || __instance.Data == null || __instance.Data.Role == null ||
            __instance.Data.Role is not SaboteadorRole)
        {
            return;
        }

        var local = PlayerControl.LocalPlayer;
        if (local == null || local.Data == null)
        {
            return;
        }

        // Regla vanilla: los vivos no ven fantasmas; los muertos/afterlife sí.
        var deberiaVerse = local.Data.IsDead ||
                           local.Data.Role is IGhostRole ||
                           local.PlayerId == __instance.PlayerId;

        if (deberiaVerse)
        {
            Mostrar(__instance);
        }
        else
        {
            Ocultar(__instance);
        }
    }

    private static void Mostrar(PlayerControl player)
    {
        player.shouldAppearInvisible = false;
        try { player.gameObject.SetActive(true); } catch { }

        var cosmetics = player.cosmetics;
        if (cosmetics == null)
        {
            return;
        }

        var body = cosmetics.currentBodySprite?.BodySprite;
        if (body != null)
        {
            body.color = body.color.SetAlpha(1f);
        }

        try { cosmetics.ToggleNameVisible(true); } catch { }
        cosmetics.nameText.color = cosmetics.nameText.color.SetAlpha(1f);

        try
        {
            player.SetHatAndVisorAlpha(1f);
            if (cosmetics.currentPet != null && cosmetics.currentPet.gameObject != null)
            {
                cosmetics.currentPet.gameObject.SetActive(true);
            }

            if (cosmetics.skin != null && cosmetics.skin.gameObject != null)
            {
                cosmetics.skin.gameObject.SetActive(true);
            }
            if (cosmetics.hat != null && cosmetics.hat.gameObject != null)
            {
                cosmetics.hat.gameObject.SetActive(true);
            }

            if (cosmetics.visor != null && cosmetics.visor.gameObject != null)
            {
                cosmetics.visor.gameObject.SetActive(true);
            }
        }
        catch
        {
            // Cosmeticos opcionales.
        }

        foreach (var rend in player.transform.GetChild(2)
                     .GetComponentsInChildren<SpriteRenderer>())
        {
            rend.color = rend.color.SetAlpha(1f);
        }
    }

    private static void Ocultar(PlayerControl player)
    {
        player.shouldAppearInvisible = true;

        var cosmetics = player.cosmetics;
        if (cosmetics != null)
        {
            var body = cosmetics.currentBodySprite?.BodySprite;
            if (body != null)
            {
                body.color = body.color.SetAlpha(0f);
            }

            try { cosmetics.ToggleNameVisible(false); } catch { }
            cosmetics.nameText.color = cosmetics.nameText.color.SetAlpha(0f);

            foreach (var rend in player.transform.GetChild(2)
                         .GetComponentsInChildren<SpriteRenderer>())
            {
                rend.color = rend.color.SetAlpha(0f);
            }
        }
    }
}

/// <summary>
///     Réplica del setter vanilla de PlayerControl.Visible para el Saboteador:
///     visible solo para muertos, afterlife y él mismo (mismo hook que Cerrajero).
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Visible), MethodType.Setter)]
public static class SaboteadorVisibleVanillaPatch
{
    public static void Prefix(PlayerControl __instance, ref bool value)
    {
        if (__instance == null || __instance.Data == null || __instance.Data.Role == null ||
            __instance.Data.Role is not SaboteadorRole)
        {
            return;
        }

        var local = PlayerControl.LocalPlayer;
        if (local == null || local.Data == null)
        {
            return;
        }

        // Regla vanilla de fantasmas.
        var deberiaVerse = local.Data.IsDead ||
                           local.Data.Role is IGhostRole ||
                           local.PlayerId == __instance.PlayerId;
        value = deberiaVerse;
    }
}

// La visión de "Luces Fuera" la gestiona ahora el SABOTAJE REAL de luces del
// juego (ShipStatus.CalculateLightRadius vanilla con el SwitchSystem activo):
// ya NO se parchea la visión a mano. Al acabar el tiempo, SaboteadorManager
// resuelve el sabotaje (ActualSwitches = ExpectedSwitches, patrón Engineer).

/// <summary>
///     El Saboteador fantasma no puede ser protegido, votado, reportar ni
///     ser objetivo de habilidades (mismo trato que el Cerrajero).
/// </summary>
public static class SaboteadorMeetingEvents
{
    /// <summary>
    ///     Al empezar una reunión: corta el apagón y restaura la visión al
    ///     instante para que NUNCA quede visión rota tras la reunión.
    /// </summary>
    [RegisterEvent]
    public static void OnStartMeeting(StartMeetingEvent _)
    {
        try
        {
            SaboteadorManager.LimpiarEstadosLocales();
        }
        catch
        {
            // Nunca romper la reunión por un error del rol.
        }
    }

    [RegisterEvent]
    public static void OnReportBody(ReportBodyEvent @event)
    {
        if (@event.Reporter?.Data?.Role is SaboteadorRole)
        {
            @event.Cancel();
        }
    }

    [RegisterEvent]
    public static void OnBeforeVote(BeforeVoteEvent @event)
    {
        if (@event.Player?.Data?.Role is not SaboteadorRole)
        {
            return;
        }

        @event.Cancel();

        if (MeetingHud.Instance == null || @event.VoteArea == null)
        {
            return;
        }

        @event.VoteArea.SetVote(252);
        MeetingHud.Instance.Confirm(252);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ProtectPlayer))]
public static class SaboteadorProtectBlockPatch
{
    public static bool Prefix(PlayerControl target)
    {
        return target?.Data?.Role is not SaboteadorRole;
    }
}

[HarmonyPatch(typeof(RoleBehaviourStubs), nameof(RoleBehaviourStubs.IsValidTarget))]
public static class SaboteadorValidTargetPatch
{
    public static bool Prefix(RoleBehaviour __instance, NetworkedPlayerInfo target, ref bool __result)
    {
        if (target?.Object?.Data?.Role is SaboteadorRole)
        {
            __result = false;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(RoleBehaviourStubs), nameof(RoleBehaviourStubs.FindClosestTarget))]
public static class SaboteadorFindClosestTargetPatch
{
    public static void Postfix(ref PlayerControl __result)
    {
        if (__result?.Data?.Role is SaboteadorRole)
        {
            __result = null!;
        }
    }
}

[HarmonyPatch(typeof(RoleBehaviour), nameof(RoleBehaviour.TeamColor), MethodType.Getter)]
public static class SaboteadorTeamColorPatch
{
    public static bool Prefix(RoleBehaviour __instance, ref Color __result)
    {
        if (__instance is SaboteadorRole)
        {
            __result = SaboteadorRole.SaboteadorColor;
            return false;
        }

        return true;
    }
}