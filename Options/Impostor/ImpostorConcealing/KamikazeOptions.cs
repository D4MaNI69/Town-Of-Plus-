using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using TownOfPlus.Roles.Impostor.ImpostorConcealing;
using UnityEngine;

namespace TownOfPlus.Options.Impostor.ImpostorConcealing;

public sealed class KamikazeOptions : AbstractOptionGroup<KamikazeRole>
{
    public override string GroupName => "Kamikaze";
    public override Color GroupColor => KamikazeRole.KamikazeColor;

    /// <summary>
    ///     Reunión en la que debe ser expulsado el Kamikaze para que los
    ///     impostores ganen automáticamente (1 = primera reunión, 3 = tercera).
    /// </summary>
    public ModdedNumberOption ReunionDetonacion { get; } = new(
        "Reunión de Detonación", 3, 1, 3, 1f, MiraNumberSuffixes.None);

    /// <summary>Cooldown del botón Engaño entre usos.</summary>
    public ModdedNumberOption CooldownEngano { get; } = new(
        "Cooldown del Engaño", 30f, 20f, 50f, 2.5f, MiraNumberSuffixes.Seconds);
}