using MiraAPI.MeetingAbilities;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Networking;
using TownOfPlus.Roles.Crewmate.CrewmateSupport;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Buttons.Crewmate.CrewmateSupport;

/// <summary>
///     ESPERANZA (botón de reunión, junto a la fila de habilidades): mientras
///     el marcado del Colega siga vivo, permite intercambiar TODOS los votos
///     que van al Colega con los que van a su marcado (y viceversa), como el
///     Swapper de TOU Mira pero limitado a esa pareja. Se puede usar una vez
///     por reunión; si el marcado muere, desaparece para siempre.
/// </summary>
public sealed class EsperanzaBoton : MeetingActionButton
{
    public override string Name => "Esperanza";

    public override Color TextOutlineColor => ColegaRole.ColegaColor;

    public override LoadableAsset<Sprite> Sprite => TownofplusAssets.ColegaEsperanzaButton;

    // Sin cooldown real: la disponibilidad la deciden los usos por reunión.
    public override float Cooldown => 0.0001f;
    public override float InitialCooldown => 0.0001f;

    // Una vez por reunión.
    public override int MaxUses => 1;
    public override MeetingButtonUsesMode UsesMode => MeetingButtonUsesMode.PerMeeting;

    public override bool DisableUponUse => true;
    public override bool HideUponWrapUp => true;

    public override bool Enabled(RoleBehaviour? role)
    {
        var local = PlayerControl.LocalPlayer;
        if (local?.Data == null || role is not ColegaRole || local.Data.IsDead)
        {
            return false;
        }

        // Solo mientras el marcado siga vivo.
        if (!ColegaManager.TieneMarca(local.PlayerId))
        {
            return false;
        }

        return !ColegaManager.MarcaEstaMuerta(ColegaManager.MarcaDe(local.PlayerId));
    }

    public override bool CanUse()
    {
        return base.CanUse() && ColegaManager.PuedeUsarEsperanzaLocal();
    }

    protected override void OnClick()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null || !ColegaManager.PuedeUsarEsperanzaLocal())
        {
            return;
        }

        ColegaRpc.EnviarEsperanza(local);
    }
}
