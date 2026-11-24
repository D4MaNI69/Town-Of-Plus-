using System;
using System.Collections.Generic;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;

namespace TownOfPlus.Options.General;

/// <summary>
///     Opciones de soundpack de Town Of Plus.
/// </summary>
public sealed class ConspiranoicoSoundpackOptions : AbstractOptionGroup
{
    public override string GroupName => "Ajustes Generales TOP";
    public override uint GroupPriority => 51;

    public ModdedToggleOption UsarConspiranoicoSoundpack { get; } =
        new("Usar hay un conspiranoico soundpack", false)
        {
            ChangedEvent = OnSoundpackToggled
        };

    private static void OnSoundpackToggled(bool enabled)
    {
        // Sin efecto sobre otros mods: TOP no toca absolutamente nada de Divani Mods.
    }
}
