using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Networking;
using TownOfPlus.Options.Crewmate.CrewmatePower;
using TownOfPlus.Roles.Crewmate.CrewmatePower;
using TownOfUs.Buttons;
using UnityEngine;

namespace TownOfPlus.Buttons.Crewmate.CrewmatePower;

/// <summary>
///     Trato: el Deudor marca a un jugador VIVO cercano (rango 1.5). Se puede
///     marcar a varios por ronda (limitado por el cooldown), pero nunca dos
///     veces al mismo jugador ni a sí mismo. El marcado aparece con el nombre
///     en #A18262 solo para el Deudor.
/// </summary>
public sealed class TratoBoton : TownOfUsTargetButton<PlayerControl>
{
    /// <summary>Rango fijo del trato (unidades del mapa).</summary>
    private const float RangoTrato = 1.5f;

    public override string Name => "Trato";
    public override Color TextOutlineColor => DeudorRole.DeudorColor;
    public override ButtonLocation Location => ButtonLocation.BottomRight;
    public override LoadableAsset<Sprite> Sprite => TownofplusAssets.DeudorTratoButton;
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;
    public override float Distance => RangoTrato;
    public override float EffectDuration => 0f;

    public override float Cooldown =>
        OptionGroupSingleton<DeudorOptions>.Instance.CooldownTrato.Value;

    public override bool Enabled(RoleBehaviour? role)
    {
        var local = PlayerControl.LocalPlayer;
        return role is DeudorRole && local?.Data != null && !local.Data.IsDead;
    }

    public override PlayerControl? GetTarget() =>
        PlayerControl.LocalPlayer.GetClosestPlayer(true, Distance, false);

    public override void SetOutline(bool active)
    {
        if (Target == null)
        {
            return;
        }

        Target.cosmetics.SetOutline(active,
            new Il2CppSystem.Nullable<Color>(DeudorRole.DeudorColor));
    }

    public override bool IsTargetValid(PlayerControl? target)
    {
        if (target?.Data == null || target.Data.IsDead || target.Data.Disconnected)
        {
            return false;
        }

        var local = PlayerControl.LocalPlayer;
        if (local?.Data == null)
        {
            return false;
        }

        // No puede tratarse a sí mismo ni repetir con alguien ya marcado.
        return target != local && !DeudorManager.EstaMarcado(local.PlayerId, target.PlayerId);
    }

    protected override void OnClick()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null || Target == null)
        {
            return;
        }

        DeudorRpc.EnviarTrato(local, Target);
        ResetTarget();
        Timer = Cooldown;
    }
}
