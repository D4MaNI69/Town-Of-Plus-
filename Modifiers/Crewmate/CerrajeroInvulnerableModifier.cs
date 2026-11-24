using MiraAPI.Modifiers;
using TownOfPlus.Modules;
using TownOfPlus.Roles.Crewmate.CrewmateAfterlife;

namespace TownOfPlus.Modifiers.Crewmate;

public sealed class CerrajeroInvulnerableModifier : BaseModifier
{
    public override string ModifierName => "CerrajeroInvulnerable";
    public override bool HideOnUi => true;
    public override bool Unique => true;
}
