using MiraAPI.Modifiers;
using MiraAPI.Modifiers.Types;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Roles.Crewmate.CrewmateAfterlife;
using TownOfPlus.Utilities;
using TownOfUs.Interfaces;
using UnityEngine;

namespace TownOfPlus.Modifiers.Crewmate;

/// <summary>
///     ANCLA del Eco: modificador SOLO para el jugador vivo ENLAZADO a un Eco.
///     <para>· NUNCA se asigna al inicio ni aleatoriamente: solo lo pone/quita
///     <see cref="EcoManager"/> cuando el Eco tiene ancla enlazada.</para>
///     <para>· Sin ajustes: no crea opciones (no aparece en los ajustes de
///     modificadores) y su cantidad/probabilidad son 0, así que el sorteo del
///     inicio de partida jamás lo reparte.</para>
///     <para>· ADIVINABLE FORZOSO: <c>AnclaSiempreAdivinablePatch</c> fuerza que
///     el Assassin (impostor) pueda adivinarlo en reunión SIEMPRE, aunque la
///     opción «adivinar modificadores» esté desactivada.</para>
///     <para>· Descripción: «Estas atado a {Eco}».</para>
/// </summary>
public sealed class AnclaModifier : GameModifier, IColoredModifier
{
    public override string ModifierName => "Ancla";

    /// <summary>Visible en el HUD del ancla (muestra a quién está atado).</summary>
    public override bool HideOnUi => false;

    /// <summary>NUNCA en el menú de freeplay (no es un modificador repartible).</summary>
    public override bool ShowInFreeplay => false;

    public override bool Unique => true;

    public override LoadableAsset<Sprite>? ModifierIcon => SpritesAncla.IconoModAsset;

    /// <summary>Color del modificador en la UI (mismo amarillo pálido del Eco).</summary>
    public Color ModifierColor => EcoRole.EcoColor;

    /// <summary>Jamás se reparte en el sorteo inicial de modificadores.</summary>
    public override int GetAssignmentChance() => 0;

    /// <summary>Jamás se reparte en el sorteo inicial de modificadores.</summary>
    public override int GetAmountPerGame() => 0;

    /// <summary>No es válido en ningún rol para el reparto aleatorio.</summary>
    public override bool IsModifierValidOn(RoleBehaviour role) => false;

    /// <summary>«Estas atado a (Eco)» — nombre del Eco enlazado al ancla.</summary>
    public override string GetDescription()
    {
        var ecoId = EcoManager.EcoDe(Player?.PlayerId ?? byte.MaxValue);
        var nombre = ecoId.HasValue
            ? EcoManager.NombreDelEco(ecoId.Value, Player!.PlayerId)
            : "?";

        return $"Estas atado a ({nombre})";
    }
}

