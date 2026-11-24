using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using TownOfPlus.Roles.Crewmate.CrewmateAfterlife;
using UnityEngine;

namespace TownOfPlus.Options.Crewmate.CrewmateAfterlife;

public sealed class HuespedOptions : AbstractRoleOptionGroup<HuespedRole>
{
    public override string GroupName => "Huésped";

    /// <summary>Cooldown del botón Poseer (default 35s, rango 30–50s).</summary>
    public ModdedNumberOption CooldownPoseer { get; } = new(
        "Cooldown de Poseer", 35f, 30f, 50f, 2.5f, MiraNumberSuffixes.Seconds);

    /// <summary>Tiempo durante el que el Huésped controla al poseído (default 10s, rango 10–20s).</summary>
    public ModdedNumberOption TiempoPosesion { get; } = new(
        "Tiempo de Posesión", 10f, 10f, 20f, 2.5f, MiraNumberSuffixes.Seconds);
}
