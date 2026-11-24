using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Options.Neutral.NeutralAfterlife;
using TownOfPlus.Roles.Neutral.NeutralAfterlife;
using TownOfUs.Buttons;
using UnityEngine;

namespace TownOfPlus.Buttons.Neutral.NeutralAfterlife;

/// <summary>
///     Botón REVIVIR de la Amalgama (afterlife neutral, muerta): busca el
///     cuerpo no reportado más cercano y canaliza "Segundos para Revivir".
///     Al completar, resucita en ese cuerpo con un rol aleatorio del pool de
///     roles habilitados. Si alguien reporta o llama reunión, FALLA.
///     FIX de botón roto en muerte (como SaboteadorBoton): UsableInDeath +
///     SetActive para que el afterlife pueda usar su botón.
/// </summary>
public sealed class RevivirBoton : TownOfUsButton
{
    public override string Name => "Revivir";
    public override Color TextOutlineColor => AmalgamaRole.AmalgamaColor;
    public override ButtonLocation Location => ButtonLocation.BottomRight;
    public override LoadableAsset<Sprite> Sprite => TownofplusAssets.RevivirButton;
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;

    /// <summary>La Amalgama es un rol afterlife (muerto): el botón funciona en muerte.</summary>
    public override bool UsableInDeath => true;

    public override float Cooldown =>
        OptionGroupSingleton<AmalgamaOptions>.Instance.SegundosParaRevivir.Value + 1f;

    public override bool Enabled(RoleBehaviour? role) => role is AmalgamaRole;

    /// <summary>Sin el gate de !HasDied de TOU (los afterlife SÍ pueden usar su botón).</summary>
    public override void SetActive(bool visible, RoleBehaviour role)
    {
        Button?.ToggleVisible(visible && Enabled(role));
    }

    public override bool CanUse()
    {
        if (!base.CanUse() || !AmalgamaManager.EsAmalgamaLocal)
        {
            return false;
        }

        // Comms saboteadas: los afterlife NEUTROS no pueden usar sus botones
        // hasta que alguien arregle las comunicaciones.
        if (CommsSabotageManager.CommsSabotadas)
        {
            return false;
        }

        // No canalizar si ya está canalizando o no hay cuerpo cercano libre.
        return !AmalgamaManager.Canalizando &&
               AmalgamaManager.BuscarCuerpoCercano() != null;
    }

    protected override void OnClick()
    {
        // Bloqueo DURO por comms saboteadas (además de CanUse).
        if (CommsSabotageManager.CommsSabotadas)
        {
            Timer = Cooldown;
            return;
        }

        var cuerpo = AmalgamaManager.BuscarCuerpoCercano();
        if (cuerpo == null)
        {
            return;
        }

        AmalgamaManager.IniciarRevive(cuerpo.ParentId);
    }
}