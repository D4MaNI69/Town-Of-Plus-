using System;
using Il2CppInterop.Runtime.Attributes;
using MiraAPI.Patches.Stubs;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Utilities;
using TownOfUs.Modules;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Roles.Crewmate.CrewmatePower;

/// <summary>
///     El Deudor (Crewmate Power, color #A18262) hace tratos con jugadores
///     vivos: al usar una habilidad (kill vanilla o botón MiraAPI) le deberán
///     un favor. Los que deben favor votan obligatoriamente al mismo destino
///     que el Deudor (jugador o skip), como el Dictador. Después de cada
///     reunión se limpian marcas, tintes y deudas.
/// </summary>
public sealed class DeudorRole(IntPtr cppPtr)
    : CrewmateRole(cppPtr), ITownOfUsRole, IWikiDiscoverable
{
    /// <summary>Color propio del Deudor (#A18262).</summary>
    public static readonly Color32 DeudorColor = new(0xA1, 0x82, 0x62, 255);

    public string LocaleKey => "Deudor";
    public string RoleName => "Deudor";
    public string RoleDescription => "Haz tratos y cobra favores";
    public string RoleLongDescription =>
        "Marca jugadores vivos.\n" +
        "Si uno de ellos usa una habilidad, te debera un favor\n" +
        "y su voto irá a donde vaya el tuyo.\n" +
        "las deudas se limpian tras cada reunión.";

    public string GetAdvancedDescription() =>
        RoleLongDescription + MiscUtils.AppendOptionsText(GetType());

    public Color RoleColor => DeudorColor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;
    public RoleAlignment RoleAlignment => RoleAlignment.CrewmatePower;

    [HideFromIl2Cpp] public List<CustomButtonWikiDescription> Abilities { get; } =
    [
        new("Trato",
            "Marca a un jugador vivo cercano. Si usa una habilidad, te deberá un favor y su voto re redirijira al tuyo.",
            TownofplusAssets.DeudorTratoButton),
    ];

    public CustomRoleConfiguration Configuration => new(this)
    {
        UseVanillaKillButton = false,

        IconTmp = MiraAPI.Utilities.Assets.TmpSpriteUtils.CreateSpriteAsset(
            SpritesDeudor.IconoRolAsset.LoadAsset(), "TownOfPlus.Role.Crewmate.Deudor", 1.45f),

        Icon = SpritesDeudor.IconoRolAsset,

        // Banner de la pagina de ajustes del rol.
        OptionsScreenshot = BannersTop.Deudor,

        // CANTIDAD: 0–15 (0 = desactivado).
        DefaultRoleCount = 1,
        MaxRoleCount = 15,
        DefaultChance = 100,
        CanModifyChance = true,

        HideSettings = false,
        ShowInFreeplay = true,
    };

    public override void Initialize(PlayerControl player)
    {
        RoleBehaviourStubs.Initialize(this, player);
        DeudorManager.RegistrarDeudor(player);
    }

    public override void Deinitialize(PlayerControl targetPlayer)
    {
        RoleBehaviourStubs.Deinitialize(this, targetPlayer);
        DeudorManager.QuitarJugador(targetPlayer.PlayerId);
    }
}
