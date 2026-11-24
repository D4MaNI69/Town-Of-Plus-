using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;

namespace TownOfPlus.Options.General;

/// <summary>
///     Ajustes generales de Town Of Plus (visibilidad afterlife y mejoras de sabotaje).
/// </summary>
public sealed class VisibilidadAfterlifeOptions : AbstractOptionGroup
{
    public override string GroupName => "Ajustes Generales TOP";
    public override uint GroupPriority => 50;

    /// <summary>
    ///     True = Haunter y Spectre de TOU Mira INVISIBLES para tripulantes
    ///     vivos (solo los ven Impostores y Neutrales vivos; los muertos /
    ///     afterlife los ven como siempre). False (default) = comportamiento
    ///     normal de TOU Mira.
    /// </summary>
    public ModdedToggleOption SoloImpsNeutrales { get; } =
        new("Haunter/Spectre solo visibles para Imp y Neutral", false);

    /// <summary>
    ///     A) Mejor Sabotaje de Luces:
    ///     Si ON y luces saboteadas: NO impostores ven overlay negro ~70% opacidad,
    ///     con fade de entrada en ~7s al iniciar el sabotaje. Al reparar → normal.
    /// </summary>
    public ModdedToggleOption MejorSabotajeLuces { get; } =
        new("Mejor Sabotaje de Luces", false);

    /// <summary>
    ///     B) Mejor Sabotaje de Reactor:
    ///     Si ON y reactor/lab activo: pantalla de NO impostores tiembla leve (~35%),
    ///     y TODOS (imps incluidos) cambian su velocidad cada 4–7s (-30% o +20%).
    ///     Al terminar → cámara y velocidad normales.
    /// </summary>
    public ModdedToggleOption MejorSabotajeReactor { get; } =
        new("Mejor Sabotaje de Reactor", false);

    /// <summary>
    ///     C) Mejor Sabotaje de O2:
    ///     Si ON y O2 activo: todos -20% velocidad y efecto Drunk (controles invertidos).
    ///     Al reparar O2 → normal.
    /// </summary>
    public ModdedToggleOption MejorSabotajeO2 { get; } =
        new("Mejor Sabotaje de O2", false);
}

