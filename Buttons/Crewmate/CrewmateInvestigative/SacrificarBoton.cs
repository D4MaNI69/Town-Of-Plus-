using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Networking;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Options.Crewmate.CrewmateInvestigative;
using TownOfPlus.Roles.Crewmate.CrewmateInvestigative;
using TownOfPlus.Utilities;
using TownOfUs.Buttons;
using TownOfUs.Networking;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Buttons.Crewmate.CrewmateInvestigative;

/// <summary>
///     Botón Sacrificar: aparece tras la revelación, con tantos tiros como
///     malos (impostores + neutrales) haya entre las marcas. Sacrificar a un
///     malo lo elimina a distancia; sacrificar a un tripulante inocente
///     mata al propio Ritualista.
/// </summary>
public sealed class SacrificarBoton : TownOfUsTargetButton<PlayerControl>
{
    public override string Name => "Sacrificar";
    public override float Cooldown =>
        OptionGroupSingleton<RitualistaOptions>.Instance.SacrificarCooldown.Value;
    public override float EffectDuration => 0f;
    public override LoadableAsset<Sprite> Sprite => SpritesRitual.BotonSacrificarAsset;
    public override float Distance => 2f;
    public override ButtonLocation Location { get; set; } = ButtonLocation.BottomRight;
    public override Color TextOutlineColor => RitualistaRole.RitualistaColor;
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;

    public override bool Enabled(RoleBehaviour? role) =>
        role is RitualistaRole &&
        RitualistaManager.Fase == FaseRitual.Sacrificando &&
        RitualistaManager.TirosRestantes > 0;

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

        // Solo se puede sacrificar a los jugadores marcados.
        return RitualistaManager.EstaMarcado(target.PlayerId);
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

        if (RitualistaManager.Fase != FaseRitual.Sacrificando ||
            RitualistaManager.TirosRestantes <= 0)
        {
            return false;
        }

        return base.CanUse();
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        // Tiros restantes visibles en el botón: Sacrificar (2).
        OverrideName($"Sacrificar ({RitualistaManager.TirosRestantes})");
        base.FixedUpdate(playerControl);
    }

    protected override void OnClick()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null || Target == null || local.Data?.Role is not RitualistaRole)
        {
            return;
        }

        if (RitualistaManager.EsImpostorONeutral(Target))
        {
            // Acierto: el malo muere a distancia, sin mover al Ritualista.
            // Consume 1 cargo y marca al objetivo para que el chequeo de
            // muertes NO reste otro tiro por la misma víctima.
            local.RpcSpecialMurder(Target, MeetingCheck.OutsideMeeting, teleportMurderer: false);
            RitualistaManager.ConsumirSacrificio(Target.PlayerId);
        }
        else
        {
            // Sacrificó a un tripulante inocente: el Ritualista muere en su lugar.
            local.RpcSpecialMurder(local, MeetingCheck.OutsideMeeting, teleportMurderer: false);
            RitualistaManager.Salir();
        }

        ResetTarget();
    }
}