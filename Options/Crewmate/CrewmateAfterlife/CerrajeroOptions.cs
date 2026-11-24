using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using TownOfPlus.Roles.Crewmate.CrewmateAfterlife;
using UnityEngine;

namespace TownOfPlus.Options.Crewmate.CrewmateAfterlife;

public sealed class CerrajeroOptions : AbstractRoleOptionGroup<CerrajeroRole>
{
    public override string GroupName => "Cerrajero";

    public ModdedNumberOption AbrirCooldown { get; } = new(
        "Tiempo para Abrir Puertas", 15f, 10f, 30f, 2.5f, MiraNumberSuffixes.Seconds);

    public ModdedNumberOption CerrarCooldown { get; } = new(
        "Tiempo para Cerrar Puertas", 15f, 10f, 30f, 2.5f, MiraNumberSuffixes.Seconds);

    public ModdedNumberOption PuertaCerradaTiempo { get; } = new(
        "Tiempo de Puerta Cerrada", 7.5f, 5f, 20f, 2.5f, MiraNumberSuffixes.Seconds);

    public ModdedNumberOption SearchRadius { get; } = new(
        "Radio de Alcance", 2f, 1f, 5f, 0.5f, MiraNumberSuffixes.None);
}
