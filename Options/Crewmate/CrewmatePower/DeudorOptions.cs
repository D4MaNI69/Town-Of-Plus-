using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using TownOfPlus.Roles.Crewmate.CrewmatePower;
using UnityEngine;

namespace TownOfPlus.Options.Crewmate.CrewmatePower;

public sealed class DeudorOptions : AbstractRoleOptionGroup<DeudorRole>
{
    public override string GroupName => "Deudor";
    public override Color GroupColor => DeudorRole.DeudorColor;

    /// <summary>
    ///     Único ajuste del Deudor: cooldown entre tratos (default 30 s,
    ///     rango 20–60 s). El rango de Trato (1.5) es fijo por diseño.
    /// </summary>
    public ModdedNumberOption CooldownTrato { get; } = new(
        "Cooldown de Trato", 30f, 20f, 60f, 1f, MiraNumberSuffixes.Seconds);
}
