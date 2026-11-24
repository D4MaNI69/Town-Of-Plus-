using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Networking;
using TownOfPlus.Options.Impostor.ImpostorSupport;
using TownOfPlus.Roles.Impostor.ImpostorSupport;
using TownOfUs.Buttons;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Buttons.Impostor.ImpostorSupport;

/// <summary>
///     Botón "Chantajear" del Abogado: marca a 1 jugador vivo NO aliado
///     (impostores y alliances del mismo equipo quedan fuera). En la reunión
///     el marcado puede votar a quien quiera, pero su voto se REDIRIGE a SKIP
///     (lo hace el host en el cálculo). Cooldown en los ajustes (35s/30–50s).
///     Máximo 1 marcado a la vez (la marca se reemplaza al chantajear a otro).
/// </summary>
public sealed class ChantajeBoton : TownOfUsTargetButton<PlayerControl>
{
    private const float RangoChantaje = 2.5f;

    public override string Name => "Chantajear";
    public override Color TextOutlineColor => AbogadoRole.AbogadoColor;
    public override ButtonLocation Location => ButtonLocation.BottomRight;
    public override LoadableAsset<Sprite> Sprite => TownofplusAssets.AbogadoChantajeAsset;
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;
    public override float Distance => RangoChantaje;
    public override float EffectDuration => 0f;

    public override float Cooldown =>
        OptionGroupSingleton<AbogadoOptions>.Instance.CooldownChantaje.Value;

    public override bool Enabled(RoleBehaviour? role) => role is AbogadoRole;

    public override PlayerControl? GetTarget() =>
        PlayerControl.LocalPlayer.GetClosestPlayer(true, Distance, false);

    public override void SetOutline(bool active)
    {
        if (Target == null)
        {
            return;
        }

        Target.cosmetics.SetOutline(active,
            new Il2CppSystem.Nullable<Color>(AbogadoRole.AbogadoColor));
    }

    public override bool IsTargetValid(PlayerControl? target)
    {
        if (target == null || target.Data == null || target.Data.IsDead || target.Data.Disconnected)
        {
            return false;
        }

        // Ni a sí mismo, ni aliados impostores, ni alliances del mismo equipo.
        if (target == PlayerControl.LocalPlayer || AbogadoManager.EsAliado(target))
        {
            return false;
        }

        return true;
    }

    protected override void OnClick()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null || Target == null)
        {
            return;
        }

        AbogadoRpc.EnviarChantaje(local, Target);
        ResetTarget();
        Timer = Cooldown;
    }
}
