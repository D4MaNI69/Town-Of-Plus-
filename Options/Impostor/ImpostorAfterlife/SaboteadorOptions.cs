using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using TownOfPlus.Roles.Impostor.ImpostorAfterlife;
using UnityEngine;

namespace TownOfPlus.Options.Impostor.ImpostorAfterlife;

public sealed class SaboteadorOptions : AbstractRoleOptionGroup<SaboteadorRole>
{
    public override string GroupName => "Saboteador";

    /// <summary>Cooldown del botón Luces Fuera.</summary>
    public ModdedNumberOption CooldownLucesFuera { get; } = new(
        "Cooldown de Luces Fuera", 50f, 40f, 70f, 2.5f, MiraNumberSuffixes.Seconds);

    /// <summary>Duración del apagón (es la X del mensaje y el efecto real).</summary>
    public ModdedNumberOption TiempoLucesFuera { get; } = new(
        "Tiempo de Luces Fuera", 15f, 15f, 30f, 2.5f, MiraNumberSuffixes.Seconds);
}