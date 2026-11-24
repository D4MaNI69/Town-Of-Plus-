using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using TownOfPlus.Roles.Crewmate.CrewmatePower;
using UnityEngine;

namespace TownOfPlus.Options.Crewmate.CrewmatePower;

public sealed class ProfesorOptions : AbstractRoleOptionGroup<ProfesorRole>
{
    public override string GroupName => "Profesor";
    public override Color GroupColor => ProfesorRole.ProfesorColor;

    /// <summary>Tope total de reentrenos en la partida (default 2, rango 1–15).</summary>
    public ModdedNumberOption MaxReentrenos { get; } = new(
        "Max de reentrenar", 2f, 1f, 15f, 1f, MiraNumberSuffixes.None);

    /// <summary>Tareas completadas por cada carga de reentrenar (default 2, rango 2–4).</summary>
    public ModdedNumberOption TareasParaReentrenar { get; } = new(
        "Tareas necesarias para reentrenar", 2f, 2f, 4f, 1f, MiraNumberSuffixes.None);

    /// <summary>Rondas de espera entre un reentreno y el siguiente (default 2, rango 0–3).</summary>
    public ModdedNumberOption RondasCooldown { get; } = new(
        "Rondas necesarias para volver a reentrenar", 2f, 0f, 3f, 1f, MiraNumberSuffixes.None);
}
