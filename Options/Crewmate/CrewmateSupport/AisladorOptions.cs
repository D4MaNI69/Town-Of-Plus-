using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using TownOfPlus.Roles.Crewmate.CrewmateSupport;
using UnityEngine;

namespace TownOfPlus.Options.Crewmate.CrewmateSupport;

public sealed class AisladorOptions : AbstractRoleOptionGroup<AisladorRole>
{
    public override string GroupName => "Aislador";
    public override Color GroupColor => AisladorRole.AisladorColor;

    /// <summary>Cooldown del botón Soplar entre usos (15–35 s).</summary>
    public ModdedNumberOption CooldownSoplar { get; } = new(
        "Cooldown de Soplar", 20f, 15f, 35f, 2.5f, MiraNumberSuffixes.Seconds);

    /// <summary>Cooldown del botón Escudo (20–50 s).</summary>
    public ModdedNumberOption CooldownEscudo { get; } = new(
        "Cooldown de Escudo", 30f, 20f, 50f, 2.5f, MiraNumberSuffixes.Seconds);

    // NOTA: Burbuja NO tiene option de cooldown por diseño: siempre está
    // disponible si hay al menos un jugador marcado.
    // La duración del escudo también es fija (AisladorManager.DuracionEscudo
    // = 4 s) para no añadir options extra.
}
