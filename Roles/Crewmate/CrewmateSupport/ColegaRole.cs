using System;
using Il2CppInterop.Runtime.Attributes;
using MiraAPI.Patches.Stubs;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Utilities;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Roles.Crewmate.CrewmateSupport;

/// <summary>
///     El Colega (Crewmate Support, color #3F51B5) elige a UN jugador vivo y
///     lo acompaña para siempre: siempre ve una flecha hacia él y su nombre
///     aparece teñido solo para el Colega. Si el acompañado muere, el voto del
///     Colega cuenta doble (2 votos). En reunión, Esperanza intercambia todos los
///     votos que van a uno con los que van al otro mientras el acompañado siga
///     vivo.
/// </summary>
public sealed class ColegaRole(IntPtr cppPtr)
    : CrewmateRole(cppPtr), ITownOfUsRole, IWikiDiscoverable
{
    /// <summary>Color propio del Colega (#3F51B5).</summary>
    public static readonly Color32 ColegaColor = new(0x3F, 0x51, 0xB5, 255);

    public string LocaleKey => "Colega";
    public string RoleName => "Colega";
    public string RoleDescription => "Acompaña y protege a un jugador";
    public string RoleLongDescription =>
        "Marca a un jugador vivo con Acompañar. Siempre verás una flecha hacia él.\n" +
        "Si usa una habilidad, la flecha se oculta 3 segundos. Si muere, tu voto cuenta doble.\n" +
        "En reunión, Esperanza intercambia todos los votos entre tú y tu acompañado.\n";

    public string GetAdvancedDescription() =>
        RoleLongDescription + MiscUtils.AppendOptionsText(GetType());

    public Color RoleColor => ColegaColor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;
    public RoleAlignment RoleAlignment => RoleAlignment.CrewmateSupport;

    [HideFromIl2Cpp] public List<CustomButtonWikiDescription> Abilities { get; } =
    [
        new("Acompañar",
            "Marca a un jugador vivo cercano (1.5). La marca es única y permanente: " +
            "siempre verás una flecha hacia él y su nombre teñido solo para ti.",
            TownofplusAssets.ColegaAcompanarButton),
        new("Esperanza",
            "En reunión: intercambia todos los votos que van a ti con los que van a tu " +
            "acompañado. Solo funciona mientras él siga vivo.",
            TownofplusAssets.ColegaEsperanzaButton),
    ];

    public CustomRoleConfiguration Configuration => new(this)
    {
        UseVanillaKillButton = false,

        IconTmp = MiraAPI.Utilities.Assets.TmpSpriteUtils.CreateSpriteAsset(
            TownofplusAssets.ColegaIcon.LoadAsset(), "TownOfPlus.Role.Crewmate.Colega", 1.45f),

        Icon = TownofplusAssets.ColegaIcon,

        // Banner de la pagina de ajustes del rol.
        OptionsScreenshot = BannersTop.Colega,

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
        ColegaManager.RegistrarColega(player);
    }

    public override void Deinitialize(PlayerControl targetPlayer)
    {
        RoleBehaviourStubs.Deinitialize(this, targetPlayer);
        ColegaManager.QuitarJugador(targetPlayer.PlayerId);
    }
}
