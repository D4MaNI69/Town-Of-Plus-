using System;
using Il2CppInterop.Runtime.Attributes;
using MiraAPI.Patches.Stubs;
using MiraAPI.Roles;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Utilities;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Roles.Crewmate.CrewmateInvestigative;

/// <summary>
///     El Ermitaño es un tripulante investigativo solitario: con el botón ESCAPE
///     abre un canal de transporte (aparece un PNG de cursor sobre el ratón al
///     70% de opacidad y se queda quieto unos segundos); al hacer click
///     izquierdo se teletransporta a ese punto y desaparece un instante
///     (invisibilidad tipo Swoop). Si el cursor está sobre una pared o una zona
///     no caminable, el Escape se rechaza con el aviso "No puedes moverte aquí"
///     y el intento se puede repetir sin gastar el cooldown largo.
/// </summary>
public sealed class ErmitanoRole(IntPtr cppPtr)
    : CrewmateRole(cppPtr), ITownOfUsRole, IWikiDiscoverable
{
    /// <summary>Color celeste del Ermitaño (#70C6FB).</summary>
    public static readonly Color32 ErmitanoColor = new(112, 198, 251, 255);

    public string LocaleKey => "Ermitano";
    public string RoleName => "Ermitaño";
    public string RoleDescription => "Escapa al punto que marques con el cursor";
    public string RoleLongDescription =>
        "Abre un canal de Escape sobre tu cursor.\n" +
        "Haz click donde puedas y desaparécete hasta allí\n" +
        "volviéndote invisible unos segundos";

    public string GetAdvancedDescription() =>
        RoleLongDescription + MiscUtils.AppendOptionsText(GetType());

    public Color RoleColor => ErmitanoColor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;
    public RoleAlignment RoleAlignment => RoleAlignment.CrewmateInvestigative;

    /// <summary>Habilidades del Ermitaño mostradas en la wiki del juego.</summary>
    [HideFromIl2Cpp] public List<CustomButtonWikiDescription> Abilities { get; } =
    [
        new("Escape",
            "Abre el canal de Escape y teletranspórtate al punto del cursor " +
            "(quedas invisible unos segundos). No funciona en paredes.",
            SpritesErmitano.BotonEscapeAsset),
    ];

    public CustomRoleConfiguration Configuration => new(this)
    {
        MaxRoleCount = 15,
        DefaultRoleCount = 1,
        DefaultChance = 50,
        CanModifyChance = true,
        HideSettings = false,
        ShowInFreeplay = true,

        // Ícono PNG en lobby, chat y ajustes (o procedural si falta el PNG).
        IconTmp = MiraAPI.Utilities.Assets.TmpSpriteUtils.CreateSpriteAsset(
            SpritesErmitano.IconoRolAsset.LoadAsset(), "TownOfPlus.Role.Crewmate.Ermitano", 1.45f),

        IntroSound = SonidosTop.ErmitanoIntro,

        // Banner de la pagina de ajustes del rol.
        OptionsScreenshot = BannersTop.Ermitano,

        Icon = SpritesErmitano.IconoRolAsset,
    };

    public override void Initialize(PlayerControl player)
    {
        RoleBehaviourStubs.Initialize(this, player);
        ErmitanoManager.ReiniciarPartida();
    }

    public override void Deinitialize(PlayerControl targetPlayer)
    {
        RoleBehaviourStubs.Deinitialize(this, targetPlayer);
        if (targetPlayer.AmOwner)
        {
            ErmitanoManager.LimpiarEstadosLocales();
        }
    }
}