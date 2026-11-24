using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Networking;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Networking;
using TownOfPlus.Options.Crewmate.CrewmateAfterlife;
using TownOfPlus.Roles.Crewmate.CrewmateAfterlife;
using TownOfUs.Buttons;
using TownOfUs.Networking;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Buttons.Crewmate.CrewmateAfterlife;

/// <summary>
///     Botón POSEER del Huésped (afterlife crew): resalta con el contorno
///     #109FB9 al jugador VIVO más cercano dentro del rango corto fijo y, al
///     pulsarlo, pide al host iniciar la posesión temporal de ese jugador.
///
///     Como el Huésped es un rol afterlife (muerto), se sobreescribe SetActive
///     y UsableInDeath para que el botón se muestre y funcione tras morir
///     (mismo FIX que el Saboteador).
/// </summary>
public sealed class HuespedPoseerBoton : TownOfUsTargetButton<PlayerControl>
{
    /// <summary>Rango corto fijo para poder poseer (unidades del mapa).</summary>
    private const float RangoPoseer = 1.2f;

    public override string Name => "Poseer";
    public override Color TextOutlineColor => HuespedRole.HuespedColor;
    public override ButtonLocation Location { get; set; } = ButtonLocation.BottomRight;
    public override LoadableAsset<Sprite> Sprite => TownofplusAssets.PoseerButton;
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;
    public override float Distance => RangoPoseer;
    public override float EffectDuration => 0f;

    /// <summary>El Huésped es un afterlife (muerto): el botón funciona en muerte.</summary>
    public override bool UsableInDeath => true;

    public override float Cooldown =>
        OptionGroupSingleton<HuespedOptions>.Instance.CooldownPoseer.Value;

    public override bool Enabled(RoleBehaviour? role) =>
        role is HuespedRole && !HuespedManager.Activa && !HuespedManager.SoyObjetivo;

    /// <summary>FIX: sin el gate de !HasDied de TOU (los afterlife SÍ usan su botón).</summary>
    public override void SetActive(bool visible, RoleBehaviour role)
    {
        Button?.ToggleVisible(visible && Enabled(role));
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
            new Il2CppSystem.Nullable<Color>(HuespedRole.HuespedColor));
    }

    public override bool IsTargetValid(PlayerControl? target)
    {
        if (target == null || target.Data == null || target.Data.IsDead || target.Data.Disconnected)
        {
            return false;
        }

        return target != PlayerControl.LocalPlayer;
    }

    public override bool CanUse()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null || local.Data == null || local.Data.Role is not HuespedRole)
        {
            return false;
        }

        // El poseído no puede usar botones custom mientras es controlado.
        if (HuespedManager.SoyObjetivo)
        {
            return false;
        }

        // Solo se puede poseer si no hay otra posesión en curso.
        if (HuespedManager.Activa)
        {
            return false;
        }

        // Comms saboteadas: los afterlife tripulantes no pueden usar sus
        // botones hasta que alguien arregle las comunicaciones.
        if (CommsSabotageManager.CommsSabotadas)
        {
            return false;
        }

        return base.CanUse();
    }

    protected override void OnClick()
    {
        // Bloqueo DURO por comms saboteadas (además de CanUse): garantiza que
        // no se pueda ejecutar la acción aunque el botón quede usable.
        if (CommsSabotageManager.CommsSabotadas)
        {
            Timer = Cooldown;
            return;
        }

        var local = PlayerControl.LocalPlayer;
        if (local == null || Target == null)
        {
            return;
        }

        // Pide al host iniciar la posesión del target (host-authoritative).
        HuespedRpc.PedirPosesion(local, Target);
        ResetTarget();
        Timer = Cooldown;
    }
}