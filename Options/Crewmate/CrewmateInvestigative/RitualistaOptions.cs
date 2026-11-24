using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using TownOfPlus.Roles.Crewmate.CrewmateInvestigative;
using UnityEngine;

namespace TownOfPlus.Options.Crewmate.CrewmateInvestigative;

public sealed class RitualistaOptions : AbstractRoleOptionGroup<RitualistaRole>
{
    public override string GroupName => "Ritualista";
    public override Color GroupColor => RitualistaRole.RitualistaColor;

    /// <summary>Jugadores que debe marcar antes de la revelación (2 a 4).</summary>
    public ModdedNumberOption JugadoresAMarcar { get; } = new(
        "Jugadores a Marcar", 3f, 2f, 4f, 1f, MiraNumberSuffixes.None);

    /// <summary>Cooldown del botón Ritual.</summary>
    public ModdedNumberOption RitualCooldown { get; } = new(
        "Cooldown de Ritual", 15f, 5f, 30f, 2.5f, MiraNumberSuffixes.Seconds);

    /// <summary>Cooldown del botón Sacrificar.</summary>
    public ModdedNumberOption SacrificarCooldown { get; } = new(
        "Cooldown de Sacrificar", 20f, 5f, 40f, 2.5f, MiraNumberSuffixes.Seconds);
}