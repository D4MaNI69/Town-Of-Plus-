using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using TownOfPlus.Roles.Crewmate.CrewmateAfterlife;

namespace TownOfPlus.Options.Crewmate.CrewmateAfterlife;

/// <summary>
///     Ajustes del Eco (afterlife crew). El botón/rol solo tiene UNA mecánica
///     automática (el ancla), así que aquí solo se configura el rango del atado:
///     si el Eco se aleja más de ese radio de su ancla, vuelve a ella.
///     La opacidad NO es configurable: es fija al 80% (diseño del rol).
/// </summary>
public sealed class EcoOptions : AbstractRoleOptionGroup<EcoRole>
{
    public override string GroupName => "Eco";

    /// <summary>
    ///     Distancia máxima a la que puede alejarse el Eco de su ancla antes de
    ///     volver a su lado (default 4f, rango 2–10).
    /// </summary>
    public ModdedNumberOption RangoAtado { get; } = new(
        "Rango del ancla", 4f, 2f, 10f, 0.5f, MiraNumberSuffixes.None, "0.0");

    /// <summary>
    ///     Tiempo máximo (en segundos) que puede estar el Eco separado del ancla
    ///     tras una reunión antes de ser devuelto a su lado (default 0 = vuelve
    ///     de inmediato; rango 0–5).
    /// </summary>
    public ModdedNumberOption MargenTrasReunion { get; } = new(
        "Margen tras reunión", 0f, 0f, 5f, 0.5f, MiraNumberSuffixes.Seconds);
}
