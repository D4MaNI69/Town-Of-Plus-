using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using TownOfPlus.Roles.Impostor.ImpostorKilling;
using UnityEngine;

namespace TownOfPlus.Options.Impostor.ImpostorKilling;

public sealed class FrancotiradorOptions : AbstractOptionGroup<FrancotiradorRole>
{
    public override string GroupName => "Francotirador";
    public override Color GroupColor => FrancotiradorRole.FrancotiradorColor;

    /// <summary>Segundos que tarda el rifle en recargarse.</summary>
    public ModdedNumberOption TiempoRecarga { get; } = new(
        "Tiempo de Recarga", 30f, 20f, 40f, 2.5f, MiraNumberSuffixes.Seconds);

    /// <summary>Cuánto se aleja la cámara al apuntar (tamaño ortográfico).</summary>
    public ModdedNumberOption NivelZoom { get; } = new(
        "Nivel de Zoom Out", 10f, 6f, 12f, 0.5f, MiraNumberSuffixes.None);

    /// <summary>Duración máxima del zoom out antes de volver solo.</summary>
    public ModdedNumberOption DuracionZoom { get; } = new(
        "Duración del Zoom Out", 3f, 2f, 5f, 0.5f, MiraNumberSuffixes.Seconds);

}