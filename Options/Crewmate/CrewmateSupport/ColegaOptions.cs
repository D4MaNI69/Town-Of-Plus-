using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using TownOfPlus.Roles.Crewmate.CrewmateSupport;
using UnityEngine;

namespace TownOfPlus.Options.Crewmate.CrewmateSupport;

public sealed class ColegaOptions : AbstractRoleOptionGroup<ColegaRole>
{
    public override string GroupName => "Colega";
    public override Color GroupColor => ColegaRole.ColegaColor;

    /// <summary>
    ///     Único ajuste del Colega: si puede usar Acompañar en la primera
    ///     ronda. Por defecto SÍ. Si se desactiva, el botón queda bloqueado
    ///     en la ronda 1 exactamente igual que el Sheriff de TOU Mira
    ///     (UsableFirstRound + candado sobre el botón).
    /// </summary>
    public ModdedToggleOption UsarPrimeraRonda { get; } =
        new("Puede usar Acompañar en primera ronda", true);
}
