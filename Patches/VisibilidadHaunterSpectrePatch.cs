using HarmonyLib;
using MiraAPI.GameOptions;
using TownOfPlus.Options.General;
using TownOfPlus.Modules;

using TownOfUs.Roles;
using TownOfUs.Roles.Crewmate;
using TownOfUs.Roles.Neutral;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Patches;

public static class VisibilidadHaunterSpectrePatch
{
    private static readonly HashSet<int> _ocultos = new();

    private static bool LocalDebeNoVer()
    {
        try
        {
            var opt = OptionGroupSingleton<VisibilidadAfterlifeOptions>.Instance;
            if (opt == null || !opt.SoloImpsNeutrales.Value)
            {
                return false;
            }

            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null || local.Data.Role == null)
            {
                return false;
            }

            if (local.HasDied())
            {
                return false;
            }

            if (local.IsImpostor() || local.IsNeutral())
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool EsFantasmaOcultable(PlayerControl target)
    {
        if (target == null || target.Data?.Role == null)
        {
            return false;
        }

        if (target.Data.Role is not IGhostRole ghost || !ghost.GhostActive)
        {
            return false;
        }

        return target.Data.Role is HaunterRole or SpectreRole;
    }
    private static void Ocultar(PlayerControl target)
    {
        try
        {
            target.Visible = false;
            var sprite = target.cosmetics?.currentBodySprite?.BodySprite;
            if (sprite != null)
            {
                var cc = sprite.color;
                cc.a = 0f;
                sprite.color = cc;
            }
            var boton = target.gameObject?.GetComponent<PassiveButton>();
            if (boton != null) boton.enabled = false;
            var collider = target.gameObject?.GetComponent<BoxCollider2D>();
            if (collider != null) collider.enabled = false;
            _ocultos.Add(target.PlayerId);
        }
        catch { }
    }

    private static void Restaurar(PlayerControl target)
    {
        try
        {
            var b2 = target.gameObject?.GetComponent<PassiveButton>();
            if (b2 != null) b2.enabled = true;
            var c2 = target.gameObject?.GetComponent<BoxCollider2D>();
            if (c2 != null) c2.enabled = true;
        }
        catch { }
        finally { _ocultos.Remove(target.PlayerId); }
    }

    private static void AplicarTodos()
    {
        bool modoOculto;
        try { modoOculto = LocalDebeNoVer(); }
        catch { return; }
        var todos = PlayerControl.AllPlayerControls;
        if (todos == null) return;
        foreach (var p in todos)
        {
            if (p == null) continue;
            try
            {
                if (modoOculto && EsFantasmaOcultable(p)) Ocultar(p);
                else if (_ocultos.Contains(p.PlayerId)) Restaurar(p);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(TownOfUs.Utilities.Extensions), nameof(TownOfUs.Utilities.Extensions.GhostFade))]
    public static class GhostFadeVisibilidadPatch
    {
        public static void Postfix(PlayerControl player)
        {
            try
            {
                if (player == null) return;
                if (LocalDebeNoVer() && EsFantasmaOcultable(player)) Ocultar(player);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.OnClick))]
    public static class ClickFantasmaBloqueoPatch
    {
        public static bool Prefix(PlayerControl __instance)
        {
            try
            {
                if (__instance != null && LocalDebeNoVer() && EsFantasmaOcultable(__instance))
                    return false;
            }
            catch { }
            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class VisibilidadFixedUpdatePatch
    {
        public static void Postfix()
        {
            try
            {
                if (MeetingHud.Instance || ExileController.Instance || LobbyBehaviour.Instance) return;
                AplicarTodos();
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    public static class VisibilidadHudUpdatePatch
    {
        public static void Postfix()
        {
            // El manager se actualiza SIEMPRE (también en reunión/lobby): es
            // el que internamente detecta el estado y llama a
            // PausarEnReunion()/ResetearTodo() para quitar el Drunk del O2 y
            // los temblores. Antes el early-return lo saltaba y los efectos
            // se quedaban pegados.
            try { SabotajesMejoradosManager.Actualizar(); }
            catch { }

            try
            {
                if (MeetingHud.Instance || ExileController.Instance || LobbyBehaviour.Instance) return;
                AplicarTodos();
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(HaunterRole), nameof(HaunterRole.FadeUpdate))]
    public static class HaunterVisibilidadPatch
    {
        public static void Postfix(HaunterRole __instance)
        {
            try
            {
                if (__instance == null || MeetingHud.Instance) return;
                AplicarTodos();
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(SpectreRole), nameof(SpectreRole.FadeUpdate))]
    public static class SpectreVisibilidadPatch
    {
        public static void Postfix(SpectreRole __instance)
        {
            try
            {
                if (__instance == null || MeetingHud.Instance) return;
                AplicarTodos();
            }
            catch { }
        }
    }

}