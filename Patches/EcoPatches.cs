using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.Events.Vanilla.Meeting.Voting;
using MiraAPI.Roles;
using TownOfPlus.Modules;
using TownOfPlus.Roles.Crewmate.CrewmateAfterlife;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Patches;

/// <summary>
///     Inmunidad del Eco: cancela cualquier intento de muerte (vanilla o
///     custom) contra este afterlife (igual que el Huésped).
/// </summary>
public static class EcoImmunityEvents
{
    [RegisterEvent]
    public static void BeforeMurderHandler(BeforeMurderEvent @event)
    {
        if (@event.Target?.Data?.Role is EcoRole)
            @event.Cancel();
    }
}

/// <summary>
///     Visibilidad del Eco fantasma (regla por cliente):
///     · Su ANCLA viva lo ve al 80% de opacidad, con cosméticos propios.
///     · Fantasmas y otros afterlife SÍ lo ven (80% opacidad).
///     · El resto de vivos NO lo ve (invisible total).
///     · El propio Eco siempre se ve a sí mismo (80%).
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class EcoVisualPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (__instance == null || __instance.Data == null || __instance.Data.Role == null)
            return;
        if (__instance.Data.Role is not EcoRole)
            return;
        // En reunión la visibilidad la decide el chat (filtro de burbujas):
        // no tocar render aquí para no romper los paneles de votación.
        if (MeetingHud.Instance != null)
            return;
        var local = PlayerControl.LocalPlayer;
        if (local == null || local.Data == null)
            return;
        try
        {
            bool localEsAncla = EcoManager.AnclaDe(__instance.PlayerId) == local.PlayerId;
            bool localMuerto = local.Data.IsDead;
            bool localAfterlife = local.Data.Role is IGhostRole;
            bool localEsElEco = local.PlayerId == __instance.PlayerId;
            bool debeVerse = localEsAncla || localMuerto || localAfterlife || localEsElEco;
            if (debeVerse)
                Mostrar(__instance);
            else
                Ocultar(__instance);
        }
        catch { }
    }

    private static void Mostrar(PlayerControl player)
    {
        // shouldAppearInvisible = motor vanilla de invisibilidad: true lo
        // oculta a quien no deba verlo; Visible decide el render final.
        // Se combinan ambos para que el resto de vivos no lo vea y el ancla /
        // fantasmas sí (al 80% de opacidad).
        player.shouldAppearInvisible = false;
        player.Visible = true;
        try { player.gameObject.SetActive(true); } catch { }
        var cosmetics = player.cosmetics;
        if (cosmetics == null)
            return;
        var body = cosmetics.currentBodySprite?.BodySprite;
        if (body != null)
        {
            var c = body.color;
            c.a = EcoManager.Opacidad;
            body.color = c;
        }
        try { player.SetHatAndVisorAlpha(EcoManager.Opacidad); } catch { }
        if (cosmetics.currentPet != null && cosmetics.currentPet.gameObject != null)
        {
            try { cosmetics.currentPet.gameObject.SetActive(true); } catch { }
        }
        try { cosmetics.nameText.color = cosmetics.nameText.color.SetAlpha(EcoManager.Opacidad); } catch { }
    }

    private static void Ocultar(PlayerControl player)
    {
        player.shouldAppearInvisible = true;
        player.Visible = false;
        var sprite = player.cosmetics?.currentBodySprite?.BodySprite;
        if (sprite != null)
        {
            var cc = sprite.color;
            cc.a = 0f;
            sprite.color = cc;
        }
        var boton = player.gameObject?.GetComponent<PassiveButton>();
        if (boton != null) boton.enabled = false;
        var collider = player.gameObject?.GetComponent<BoxCollider2D>();
        if (collider != null) collider.enabled = false;
    }
}

/// <summary>Motor del Eco: watchdog en reunion + atado al ancla.</summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class EcoMotorPatch
{
    public static void Postfix()
    {
        try { EcoManager.ActualizarReunion(); } catch { }
        try { EcoManager.Actualizar(); } catch { }
    }
}

/// <summary>Al empezar la reunion: armar vigilancia de muertes EN reunion.</summary>
public static class EcoReunionEvents
{
    [RegisterEvent]
    public static void AlEmpezar(StartMeetingEvent @event)
    {
        try { EcoManager.AlComenzarReunion(); } catch { }
    }
}

/// <summary>El Eco NO vota como vivo: su voto se abstiene (skip).</summary>
public static class EcoVotoEvents
{
    [RegisterEvent]
    public static void AntesDeVotar(BeforeVoteEvent @event)
    {
        try
        {
            if (@event.Player?.Data?.Role is EcoRole)
            {
                @event.Cancel();
                if (MeetingHud.Instance == null || @event.VoteArea == null)
                    return;
                @event.VoteArea.SetVote(252);
                MeetingHud.Instance.Confirm(252);
            }
        }
        catch { }
    }

    [RegisterEvent]
    public static void AlEmpezarPartida(IntroBeginEvent ev)
    {
        try { EcoManager.AlEmpezarPartida(); } catch { }
    }

    [RegisterEvent]
    public static void AlTerminarPartida(GameEndEvent ev)
    {
        try { EcoManager.ReiniciarPartida(); } catch { }
    }
}

/// <summary>
///     Mantenedor del fantasma común devuelto: si ESTE cliente era el Eco y su
///     ancla murió/se desconectó, se le conserva cada frame el aspecto de
///     fantasma normal (capa Ghost, visible para sí mismo y para los muertos,
///     sprites al 100%) porque el motor vanilla lo vuelve a marcar como
///     invisible en cuanto cambia el rol (bug: "no se veía su propio
///     personaje").
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class EcoFantasmaComunMantenerPatch
{
    public static void Postfix()
    {
        try { EcoManager.MantenerFantasmaComunVisible(); } catch { }
    }
}

/// <summary>
///     Chat en reunión del Eco:
///     · SOLO su ancla lee sus mensajes: al recibir un chat del Eco, si el
///       local NO es su ancla (ni el propio Eco), se oculta la burbuja.
///     · El Eco NO ve ni lee el chat de fantasmas: todo mensaje de un muerto
///       que NO sea su ancla se oculta para el Eco.
/// </summary>
[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
public static class EcoChatFiltroPatch
{
    public static void Postfix(ChatController __instance, PlayerControl sourcePlayer)
    {
        try
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || sourcePlayer?.Data == null)
                return;
            if (MeetingHud.Instance == null)
                return;
            if (sourcePlayer.Data.Role is EcoRole)
            {
                var ancla = EcoManager.AnclaDe(sourcePlayer.PlayerId);
                bool localPuedeLeer = local.PlayerId == sourcePlayer.PlayerId ||
                    (ancla.HasValue && local.PlayerId == ancla.Value);
                if (!localPuedeLeer)
                    QuitarUltimaBurbuja(__instance);
                return;
            }
            if (local.Data.Role is EcoRole)
            {
                if (sourcePlayer.Data == null || !sourcePlayer.Data.IsDead)
                    return;
                var miAncla = EcoManager.AnclaDe(local.PlayerId);
                bool esMiAncla = miAncla.HasValue && sourcePlayer.PlayerId == miAncla.Value;
                if (!esMiAncla)
                    QuitarUltimaBurbuja(__instance);
            }
        }
        catch { }
    }

    private static void QuitarUltimaBurbuja(ChatController chat)
    {
        try
        {
            // ChatController guarda las burbujas en chatBubblePool (ListPool).
            var campo = typeof(ChatController).GetField(
                "chatBubblePool",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            var pool = campo?.GetValue(chat);
            var lista = pool?.GetType().GetProperty("ActiveChildren")
                ?.GetValue(pool) as System.Collections.IList;
            if (lista == null || lista.Count == 0)
                return;
            var ultima = lista[lista.Count - 1] as ChatBubble;
            if (ultima != null && ultima.gameObject != null)
                ultima.gameObject.SetActive(false);
        }
        catch { }
    }
}

/// <summary>
///     Visión de TRIPULANTE para el Eco (mismo ajuste de la sala): en vez del
///     radio máximo que la fórmula vanilla da a los muertos/fantasmas, el Eco
///     ve con la MISMA luz que tendría un crewmate vivo parado ahí
///     (ShipStatus.CalculateLightRadius con los datos VIVOS del Eco +
///     CrewLightMod, limitado al MaxLightRadius del mapa). Solo afecta al
///     cliente del Eco.
/// </summary>
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CalculateLightRadius))]
public static class EcoVisionPatch
{
    public static void Postfix(NetworkedPlayerInfo player, ref float __result)
    {
        try
        {
            var eco = player?.Object;
            if (eco == null || !eco.AmOwner)
                return;
            if (eco.Data?.Role is not EcoRole)
                return;
            if (ShipStatus.Instance == null)
                return;

            // MISMA fórmula que un crewmate: el ajuste de la sala
            // (Visión de tripulantes = CrewLightMod) sobre el radio máximo del
            // mapa. Es el patrón exacto que usa TOU Mira para su visión de
            // tripulante, y reemplaza el radio MÁXIMO que la fórmula vanilla da
            // a los muertos/fantasmas (por eso el Eco veía todo el mapa).
            float crew = 1f;
            try
            {
                crew = GameOptionsManager.Instance.currentNormalGameOptions.CrewLightMod;
            }
            catch { }

            __result = ShipStatus.Instance.MaxLightRadius * crew;
        }
        catch { }
    }
}

/// <summary>Color del equipo en la UI para el Eco.</summary>
[HarmonyPatch(typeof(RoleBehaviour), nameof(RoleBehaviour.TeamColor), MethodType.Getter)]
public static class EcoTeamColorPatch
{
    public static bool Prefix(RoleBehaviour __instance, ref Color __result)
    {
        if (__instance is EcoRole)
        {
            __result = EcoRole.EcoColor;
            return false;
        }
        return true;
    }
}
