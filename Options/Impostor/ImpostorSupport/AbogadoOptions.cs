using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using TownOfPlus.Roles.Impostor.ImpostorSupport;

namespace TownOfPlus.Options.Impostor.ImpostorSupport;

public sealed class AbogadoOptions : AbstractRoleOptionGroup<AbogadoRole>
{
    public override string GroupName => "Abogado";

    /// <summary>Cooldown del botón Chantaje (default 35s, rango 30–50s).</summary>
    public ModdedNumberOption CooldownChantaje { get; } = new(
        "Cooldown de Chantaje", 35f, 30f, 50f, 2.5f, MiraNumberSuffixes.Seconds);

    /// <summary>Cada cuántas reuniones recupera Terminar
    /// (default 2, rango 1–4). La 1ª reunión siempre disponible.</summary>
    public ModdedNumberOption ReunionesEntreUsos { get; } = new(
        "Reuniones para recuperar Terminar", 2f, 1f, 4f, 1f, MiraNumberSuffixes.None);
}
