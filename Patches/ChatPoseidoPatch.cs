using HarmonyLib;
using TownOfPlus.Modules;

namespace TownOfPlus.Patches;

/// <summary>
///     El POSEÍDO no puede hablar por el chat de texto mientras es controlado:
///     se bloquean TODOS los caminos de envío (chat libre, submit, quick-chat)
///     solo cuando el jugador local es el poseído. El Huésped y el resto hablan
///     normal. Al soltar la posesión todo vuelve solo. La voz (Perfect Comms)
///     se mutea por su regla en PerfectCommsHuespedBridge.
/// </summary>
public static class HuespedChatPoseidoBlock
{
    private static bool EsPoseidoLocal()
    {
        var local = PlayerControl.LocalPlayer;
        return HuespedManager.Activa && local != null &&
               local.PlayerId == HuespedManager.ObjetivoId;
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.OnFreeChatSubmit))]
    public static class HuespedOnFreeChatSubmitPatch
    {
        public static bool Prefix()
        {
            return !EsPoseidoLocal();
        }
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.SendFreeChat))]
    public static class HuespedSendFreeChatPatch
    {
        public static bool Prefix()
        {
            return !EsPoseidoLocal();
        }
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
    public static class HuespedSendChatPatch
    {
        public static bool Prefix()
        {
            return !EsPoseidoLocal();
        }
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.OnQuickChatSubmit))]
    public static class HuespedOnQuickChatSubmitPatch
    {
        public static bool Prefix()
        {
            return !EsPoseidoLocal();
        }
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.SendQuickChat))]
    public static class HuespedSendQuickChatPatch
    {
        public static bool Prefix()
        {
            return !EsPoseidoLocal();
        }
    }
}
