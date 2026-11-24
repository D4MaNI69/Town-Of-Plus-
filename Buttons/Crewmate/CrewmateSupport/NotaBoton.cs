using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Options.Crewmate.CrewmateSupport;
using TownOfPlus.Roles.Crewmate.CrewmateSupport;
using TownOfUs.Buttons;
using UnityEngine;

namespace TownOfPlus.Buttons.Crewmate.CrewmateSupport;

/// <summary>
///     Botón NOTA de la Víctima: abre el chat temporal de nota (un mensaje
///     por uso). El cooldown se reanuda al CERRAR el chat (enviado o no),
///     vía VictimaManager.ReiniciarCooldownNota.
/// </summary>
public sealed class NotaBoton : TownOfUsButton
{
    public override string Name => "Nota";
    public override Color TextOutlineColor => VictimaRole.VictimaColor;
    public override ButtonLocation Location => ButtonLocation.BottomRight;
    public override LoadableAsset<Sprite> Sprite => TownofplusAssets.NotaButton;

    public override float Cooldown =>
        OptionGroupSingleton<VictimaOptions>.Instance.CooldownNota.Value;

    public override bool Enabled(RoleBehaviour? role) => role is VictimaRole;

    public override bool CanUse()
    {
        if (!VictimaManager.EsVictima(PlayerControl.LocalPlayer))
        {
            return false;
        }

        // No usar Nota si el chat ya está abierto o no queda cupo de notas.
        return !VictimaManager.ModoNotaActivo &&
               VictimaManager.TieneCupo(PlayerControl.LocalPlayer);
    }

    protected override void OnClick()
    {
        if (VictimaManager.AbrirChatNota())
        {
            VictimaManager.BotonNotaLocal = this;
        }
    }
}