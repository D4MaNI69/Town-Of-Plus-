using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Options.Crewmate.CrewmateInvestigative;
using TownOfPlus.Roles.Crewmate.CrewmateInvestigative;
using TownOfPlus.Utilities;
using TownOfUs.Buttons;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Buttons.Crewmate.CrewmateInvestigative;

/// <summary>
///     Botón Ritual: marca al jugador más cercano. Los marcados muestran su
///     nombre en color crema (solo para el Ritualista). Al completar todas
///     las marcas, la revelación llega en la próxima reunión.
/// </summary>
public sealed class RitualBoton : TownOfUsTargetButton<PlayerControl>
{
    public override string Name => "Ritual";
    public override float Cooldown =>
        OptionGroupSingleton<RitualistaOptions>.Instance.RitualCooldown.Value;
    public override float EffectDuration => 0f;
    public override LoadableAsset<Sprite> Sprite => SpritesRitual.BotonRitualAsset;
    public override float Distance => 2f;
    public override ButtonLocation Location { get; set; } = ButtonLocation.BottomRight;
    public override Color TextOutlineColor => RitualistaRole.RitualistaColor;
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;

    public override bool Enabled(RoleBehaviour? role) =>
        role is RitualistaRole && RitualistaManager.Fase == FaseRitual.Marcando;

    public override PlayerControl? GetTarget() =>
        PlayerControl.LocalPlayer.GetClosestPlayer(true, Distance, false);

    public override void SetOutline(bool active)
    {
        if (Target == null)
        {
            return;
        }

        Target.cosmetics.SetOutline(active,
            new Il2CppSystem.Nullable<Color>(RitualistaRole.RitualistaColor));
    }

    public override bool IsTargetValid(PlayerControl? target)
    {
        if (target == null || target.Data == null || target.Data.IsDead || target.Data.Disconnected)
        {
            return false;
        }

        if (target == PlayerControl.LocalPlayer)
        {
            return false;
        }

        return !RitualistaManager.EstaMarcado(target.PlayerId);
    }

    public override bool CanUse()
    {
        var player = PlayerControl.LocalPlayer;
        if (player == null || player.Data == null || player.Data.IsDead)
        {
            return false;
        }

        if (player.Data.Role is not RitualistaRole)
        {
            return false;
        }

        if (RitualistaManager.Fase != FaseRitual.Marcando || RitualistaManager.MarcasCompletas)
        {
            return false;
        }

        return base.CanUse();
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        // Progreso de marcas visible en el botón: Ritual (2/3).
        OverrideName($"Ritual ({RitualistaManager.CantidadMarcados}/{RitualistaManager.MaxMarcas})");
        base.FixedUpdate(playerControl);
    }

    protected override void OnClick()
    {
        var player = PlayerControl.LocalPlayer;
        if (player == null || Target == null || player.Data?.Role is not RitualistaRole)
        {
            return;
        }

        if (RitualistaManager.Marcar(Target))
        {
            ResetTarget();
        }
    }
}