using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using TownOfPlus.Roles.Impostor.ImpostorPower;
using UnityEngine;

namespace TownOfPlus.Options.Impostor.ImpostorPower;

public sealed class DictadorOptions : AbstractOptionGroup<DictadorRole>
{
    public override string GroupName => "Dictador";
    public override Color GroupColor => DictadorRole.DictadorColor;

    /// <summary>
    ///     Votos EN CONTRA acumulados (entre todas las reuniones) necesarios
    ///     para poder revelarse como Dictador.
    /// </summary>
    public ModdedNumberOption VotosParaRevelar { get; } = new(
        "Votos en contra para revelarse", 5, 3, 15, 1, MiraNumberSuffixes.None);
}