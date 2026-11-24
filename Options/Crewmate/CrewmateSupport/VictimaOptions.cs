using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using TownOfPlus.Roles.Crewmate.CrewmateSupport;

namespace TownOfPlus.Options.Crewmate.CrewmateSupport;

public sealed class VictimaOptions : AbstractRoleOptionGroup<VictimaRole>
{
    public override string GroupName => "Víctima";

    /// <summary>Cooldown del botón Nota (por defecto 15s, rango 10–40).</summary>
    public ModdedNumberOption CooldownNota { get; } = new(
        "Cooldown Nota", 15f, 10f, 40f, 2.5f, MiraNumberSuffixes.Seconds);

    /// <summary>Tiempo que la Víctima tiene para escribir la nota (5–20s).</summary>
    public ModdedNumberOption TiempoEscritura { get; } = new(
        "Tiempo para Escribir Nota", 10f, 5f, 20f, 1f, MiraNumberSuffixes.Seconds);

    /// <summary>
    ///     Cantidad de notas guardables. 0 = INFINITO (el slider usa
    ///     zeroInfinity para mostrar "Infinito" en 0). Por diseño de MiraAPI
    ///     el rango es continuo 0–20; el comportamiento del rol trata
    ///     cualquier valor en 1–20 como límite estricto.
    /// </summary>
    public ModdedNumberOption CantidadMensajes { get; } = new(
        "Cantidad de Mensajes", 0f, 0f, 20f, 1f, MiraNumberSuffixes.None,
        zeroInfinity: true);
}