using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.Events.Vanilla.Meeting.Voting;
using MiraAPI.Networking;
using MiraAPI.Patches.Stubs;
using MiraAPI.Roles;
using TownOfPlus.Roles.Crewmate.CrewmateAfterlife;
using TownOfUs.Roles;
using TownOfUs.Modules.Wiki;
using UnityEngine;

namespace TownOfPlus.Patches;

/// <summary>
///     Inmunidad total del Cerrajero: cancela cualquier intento de muerte
///     (vanilla o custom) contra él.
/// </summary>
public static class CerrajeroImmunityEvents
{
    [RegisterEvent]
    public static void BeforeMurderHandler(BeforeMurderEvent @event)
    {
        if (@event.Target?.Data?.Role is CerrajeroRole)
        {
            @event.Cancel();
        }
    }
}

/// <summary>
///     Aspecto del Cerrajero fantasma, copiando la regla VANILLA de Among Us:
///     un observador muerto ve a los fantasmas, un vivo no. Por tanto el
///     Cerrajero se ve a sí mismo, a otros muertos/fantasmas y a roles
///     afterlife, pero NUNCA a los jugadores vivos. Usa el cuerpo vanilla del
///     jugador (como el Saboteador), sin depender de sprites propios.
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class CerrajeroVisualPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (__instance == null || __instance.Data == null || __instance.Data.Role == null)
        {
            return;
        }

        if (__instance.Data.Role is not CerrajeroRole)
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
///     Replica fielmente el setter vanilla de PlayerControl.Visible para el
///     Cerrajero: fuerza que sea visible solo cuando el observador esté muerto,
///     sea un afterlife o sea él mismo. Esto es exactamente lo que hace el
///     juego con los fantasmas (visible para muertos, oculto para vivos).
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Visible), MethodType.Setter)]
public static class CerrajeroVisibleVanillaPatch
{
    public static void Prefix(PlayerControl __instance, ref bool value)
    {
        if (__instance == null || __instance.Data == null || __instance.Data.Role == null)
        {
            return;
        }

        if (__instance.Data.Role is not CerrajeroRole)
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

/// <summary>
///     Nadie puede proteger al Cerrajero con el Escudo del Ángel Guardián.
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ProtectPlayer))]
public static class CerrajeroProtectBlockPatch
{
    public static bool Prefix(PlayerControl target)
    {
        return target?.Data?.Role is not CerrajeroRole;
    }
}

/// <summary>
///     Bloquea targeting de roles custom sobre el Cerrajero.
/// </summary>
[HarmonyPatch(typeof(RoleBehaviourStubs), nameof(RoleBehaviourStubs.IsValidTarget))]
public static class CerrajeroValidTargetPatch
{
    public static bool Prefix(RoleBehaviour __instance, NetworkedPlayerInfo target, ref bool __result)
    {
        if (target?.Object?.Data?.Role is CerrajeroRole)
        {
            __result = false;
            return false;
        }

        return true;
    }
}

/// <summary>
///     Filtra al Cerrajero de los resultados de FindClosestTarget.
/// </summary>
[HarmonyPatch(typeof(RoleBehaviourStubs), nameof(RoleBehaviourStubs.FindClosestTarget))]
public static class CerrajeroFindClosestTargetPatch
{
    public static void Postfix(ref PlayerControl __result)
    {
        if (__result?.Data?.Role is CerrajeroRole)
        {
            __result = null!;
        }
    }
}

/// <summary>
///     El Cerrajero no puede reportar cuerpos ni usar reuniones de emergencia.
/// </summary>
public static class CerrajeroMeetingEvents
{
    [RegisterEvent]
    public static void OnReportBody(ReportBodyEvent @event)
    {
        if (@event.Reporter?.Data?.Role is CerrajeroRole)
        {
            @event.Cancel();
        }
    }

    [RegisterEvent]
    public static void OnBeforeVote(BeforeVoteEvent @event)
    {
        if (@event.Player?.Data?.Role is not CerrajeroRole)
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

[HarmonyPatch(typeof(RoleBehaviour), nameof(RoleBehaviour.TeamColor), MethodType.Getter)]
public static class CerrajeroTeamColorPatch
{
    public static bool Prefix(RoleBehaviour __instance, ref Color __result)
    {
        if (__instance is CerrajeroRole)
        {
            __result = CerrajeroRole.CerrajeroColor;
            return false;
        }

        return true;
    }
}
