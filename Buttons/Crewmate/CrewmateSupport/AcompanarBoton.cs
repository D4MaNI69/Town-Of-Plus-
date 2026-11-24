using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Networking;
using TownOfPlus.Options.Crewmate.CrewmateSupport;
using TownOfPlus.Roles.Crewmate.CrewmateSupport;
using TownOfUs.Buttons;
using UnityEngine;

namespace TownOfPlus.Buttons.Crewmate.CrewmateSupport;

/// <summary>
///     Acompañar: el Colega marca a UN jugador VIVO cercano (rango 1.5). La
///     marca es única y permanente: al usarla, este botón desaparece para
///     siempre. El marcado ve su nombre en #3F51B5 solo en la pantalla del
///     Colega, que además siempre tiene una flecha hacia él.
///     Si el ajuste de primera ronda está desactivado, el botón queda
///     bloqueado en la ronda 1 igual que el Sheriff de TOU Mira.
/// </summary>
public sealed class AcompanarBoton : TownOfUsTargetButton<PlayerControl>
{
    /// <summary>Rango fijo de Acompañar (unidades del mapa).</summary>
    private const float RangoAcompanar = 1.5f;

    public override string Name => "Acompañar";
    public override Color TextOutlineColor => ColegaRole.ColegaColor;
    public override ButtonLocation Location => ButtonLocation.BottomRight;
    public override LoadableAsset<Sprite> Sprite => TownofplusAssets.ColegaAcompanarButton;
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;
    public override float Distance => RangoAcompanar;
    public override float EffectDuration => 0f;

    /// <summary>
    ///     Igual que el Sheriff: si el ajuste está apagado, TOU Mira bloquea
    ///     la ronda 1 y dibuja el candado sobre el botón.
    /// </summary>
    public override bool UsableFirstRound =>
        OptionGroupSingleton<ColegaOptions>.Instance.UsarPrimeraRonda.Value;

    // El botón desaparece al usarse: sin cooldown real.
    public override float Cooldown => 0f;
    public override float InitialCooldown => 0f;

    public override bool Enabled(RoleBehaviour? role)
    {
        var local = PlayerControl.LocalPlayer;
        if (role is not ColegaRole || local?.Data == null || local.Data.IsDead)
        {
            return false;
        }

        // Marca única y permanente: el botón no vuelve tras el primer uso.
        return !ColegaManager.TieneMarca(local.PlayerId);
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
            new Il2CppSystem.Nullable<Color>(ColegaRole.ColegaColor));
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

        return target != local && !ColegaManager.TieneMarca(local.PlayerId);
    }

    public override bool CanUse()
    {
        var local = PlayerControl.LocalPlayer;
        if (local?.Data == null || ColegaManager.TieneMarca(local.PlayerId))
        {
            return false;
        }

        return base.CanUse();
    }

    protected override void OnClick()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null || Target == null)
        {
            return;
        }

        ColegaRpc.EnviarAcompanar(local, Target);
        ResetTarget();
    }
}
