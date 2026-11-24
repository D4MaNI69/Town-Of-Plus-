using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using TownOfPlus.Roles.Neutral.NeutralAfterlife;

namespace TownOfPlus.Options.Neutral.NeutralAfterlife;

public sealed class AmalgamaOptions : AbstractRoleOptionGroup<AmalgamaRole>
{
    public override string GroupName => "Amalgama";

    /// <summary>Segundos de canalización para revivir (por defecto 3s, rango 0–5).</summary>
    public ModdedNumberOption SegundosParaRevivir { get; } = new(
        "Segundos para Revivir", 3f, 0f, 5f, 0.5f, MiraNumberSuffixes.Seconds);
}