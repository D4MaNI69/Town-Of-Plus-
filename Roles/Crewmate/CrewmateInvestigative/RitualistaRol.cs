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
///     El Ritualista es un tripulante investigativo: marca a varios jugadores
///     con su botón Ritual, y cuando completó todas las marcas, en la próxima
///     reunión el chat le revela las facciones de sus objetivos (Impostor /
///     Neutral / Tripulante, en orden aleatorio y sin decir cuál es cuál).
///     Después recibe tiros de Sacrificar iguales a la cantidad de malos que
///     marcó: si sacrifica a un tripulante inocente, ÉL muere en su lugar.
/// </summary>
public sealed class RitualistaRole(IntPtr cppPtr)
    : CrewmateRole(cppPtr), ITownOfUsRole, IWikiDiscoverable
{
    /// <summary>Color crema del Ritualista (#FFFDD0).</summary>
    public static readonly Color32 RitualistaColor = new(255, 253, 208, 255);

    public string LocaleKey => "Ritualista";
    public string RoleName => "Ritualista";
    public string RoleDescription => "Marca objetivos y sacrifica a los malos";
    public string RoleLongDescription =>
        "Marca una cantidad de jugadores establecidos y\n" +
        "en una reunión te serán reveladas sus facciones.\n" +
        "Aprovecha esa información y mata a los traidores";

    public string GetAdvancedDescription() =>
        RoleLongDescription + MiscUtils.AppendOptionsText(GetType());

    public Color RoleColor => RitualistaColor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;
    public RoleAlignment RoleAlignment => RoleAlignment.CrewmateInvestigative;

    /// <summary>Botones del Ritualista mostrados en la wiki del juego.</summary>
    [HideFromIl2Cpp] public List<CustomButtonWikiDescription> Abilities { get; } =
    [
        new("Ritual",
            "Marca al jugador más cercano. Completa todas las marcas para que la próxima reunión te revele las facciones de tus objetivos.",
            SpritesRitual.BotonRitualAsset),
        new("Sacrificar",
            "Sacrifica a un jugador marcado. Si es un impostor o neutral, muere a distancia; si es un tripulante inocente, mueres TÚ.",
            SpritesRitual.BotonSacrificarAsset),
    ];

    public CustomRoleConfiguration Configuration => new(this)
    {
        MaxRoleCount = 15,
        DefaultRoleCount = 1,
        DefaultChance = 50,
        CanModifyChance = true,
        HideSettings = false,
        ShowInFreeplay = true,

        // Sonido de la pantalla de rol: Resources/Sound/RitualistaIntro.wav
        IntroSound = SonidosTop.RitualistaIntro,

        // Ícono PNG en lobby, chat y ajustes (como los roles de TOU Mira).
        IconTmp = MiraAPI.Utilities.Assets.TmpSpriteUtils.CreateSpriteAsset(
            SpritesRitual.IconoRolAsset.LoadAsset(), "TownOfPlus.Role.Crewmate.Ritualista", 1.45f),

        // PNG en Resources/Icons/ritualista.png (o procedural si falta).
        Icon = SpritesRitual.IconoRolAsset,
        

        // Banner de la pagina de ajustes del rol.
        OptionsScreenshot = BannersTop.Ritualista,
    };

    public override void Initialize(PlayerControl player)
    {
        RoleBehaviourStubs.Initialize(this, player);
        RitualistaManager.Reiniciar();
    }

    public override void Deinitialize(PlayerControl targetPlayer)
    {
        RoleBehaviourStubs.Deinitialize(this, targetPlayer);
        RitualistaManager.Salir();
    }
}
