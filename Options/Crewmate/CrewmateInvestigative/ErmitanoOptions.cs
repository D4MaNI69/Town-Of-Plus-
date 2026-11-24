using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using TownOfPlus.Roles.Crewmate.CrewmateInvestigative;
using UnityEngine;

namespace TownOfPlus.Options.Crewmate.CrewmateInvestigative;

/// <summary>
///     Opciones del Ermitaño tras el rework a ESCAPE: solo queda el cooldown
///     largo de la habilidad (Marcar, Volver y la marca fija se eliminaron).
/// </summary>
public sealed class ErmitanoOptions : AbstractRoleOptionGroup<ErmitanoRole>
{
    public override string GroupName => "Ermitaño";
    public override Color GroupColor => ErmitanoRole.ErmitanoColor;

    /// <summary>
    ///     Cooldown LARGO del botón Escape entre usos (30-60 s, 40 s por defecto).
    ///     Se aplica al teletransportarse o al agotarse/cancelarse el canal de
    ///     transporte (5 s). Un click en una posición inválida NO lo consume.
    /// </summary>
    public ModdedNumberOption CooldownEscape { get; } = new(
        "Cooldown de Escape", 40f, 30f, 60f, 2.5f, MiraNumberSuffixes.Seconds);
}
