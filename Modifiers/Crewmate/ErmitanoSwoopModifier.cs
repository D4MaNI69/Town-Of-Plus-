using TownOfUs.Utilities;
using MiraAPI.Modifiers;
using TownOfPlus.Modules;
using TownOfPlus.Roles.Crewmate.CrewmateInvestigative;
using TownOfUs.Assets;
using TownOfUs.Modifiers;
using TownOfUs.Utilities.Appearances;
using TownOfPlus.Utilities;
using UnityEngine;

namespace TownOfPlus.Modifiers.Crewmate;

/// <summary>
///     Invisibilidad del Ermitaño copiada del Swooper de TOU Mira (HnsChameleonSwoopModifier):
///     <see cref="ConcealedModifier"/> + <see cref="IVisualAppearance"/> con RendererColor
///     semitransparente. Con <see cref="ConcealedModifier.VisibleToOthers"/> en false el
///     motor de apariencias de TOU Mira aplica esta apariencia SOLO en el cliente dueño:
///     los demás jugadores lo ven completamente invisible (RenderersEnabled = false) y el
///     propio Ermitaño se ve a sí mismo semitransparente (10% de opacidad).
/// </summary>
public sealed class ErmitanoSwoopModifier : ConcealedModifier, IVisualAppearance
{
    public override string ModifierName => "ErmitañoOculto";
    public override float Duration => ErmitanoManager.DuracionInvisibilidad;
    public override bool HideOnUi => true;
    public override bool AutoStart => true;
    public override bool VisibleToOthers => false;
    public override bool CarriesIntoMeetings => false;
    public bool VisualPriority => true;

    public VisualAppearance GetVisualAppearance()
    {
        // Igual que el Chameleon/Swooper: negro semitransparente para el dueño.
        var playerColor = PlayerControl.LocalPlayer.IsCrewmate()
            ? new Color(0f, 0f, 0f, 0.1f)
            : Color.clear;

        return new VisualAppearance(Player.GetDefaultModifiedAppearance(), TownOfUsAppearances.Swooper)
        {
            HatId = "hat_NoHat",
            SkinId = "skin_None",
            VisorId = "visor_EmptyVisor",
            PlayerName = string.Empty,
            PetId = "pet_EmptyPet",
            RendererColor = playerColor,
            NameColor = Color.clear,
            ColorBlindTextColor = Color.clear
        };
    }

    public override void OnDeath(DeathReason reason)
    {
        Player.RemoveModifier(this);
    }

    public override void OnMeetingStart()
    {
        Player.RemoveModifier(this);
    }

    public override void OnActivate()
    {
        if (Player.AmOwner)
        {
            AudioTop.Sonar(TouAudio.SwooperActivateSound);
        }

        Player.RawSetAppearance(this);
        Player.cosmetics.ToggleNameVisible(false);
    }

    public override void OnDeactivate()
    {
        Player.ResetAppearance();
        Player.cosmetics.ToggleNameVisible(true);
    }
}