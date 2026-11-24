using System;
using Il2CppInterop.Runtime.Attributes;
using MiraAPI.Patches.Stubs;
using MiraAPI.Roles;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Utilities;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Roles.Impostor.ImpostorConcealing;

/// <summary>
///     El Kamikaze es un impostor ocultador (ImpostorConcealing) que NO puede
///     matar: su botón de kill vanilla está deshabilitado por completo.
///     Su poder es el engaño (un botón único que cicla 4 habilidades) y su
///     condición de victoria especial: si la tripulación lo expulsa en la
///     reunión de detonación (reunión N configurable), los impostores ganan
///     automáticamente. Si muere de cualquier otra forma, la victoria no se activa.
/// </summary>
public sealed class KamikazeRole(IntPtr cppPtr)
    : ImpostorRole(cppPtr), ITownOfUsRole, IWikiDiscoverable
{
    /// <summary>Rojo impostor del Kamikaze: mismo color que su equipo en TODO
    /// (nombre en lobby/chat, rol, botón, opciones y victoria).</summary>
    public static readonly Color32 KamikazeColor = Palette.ImpostorRed;

    public string LocaleKey => "Kamikaze";
    public string RoleName => "Kamikaze";
    public string RoleDescription => "Engaña con tu ciclo de habilidades y detona al ser expulsado";
    public string RoleLongDescription =>
        "Tu misión es ser expulsado para poder ganar.\n" +
        "Lastimosamente no puedes matar, pero en vez de eso\n" +
        "puedes intentar engañar a la tripulación";

    public string GetAdvancedDescription() =>
        RoleLongDescription + MiscUtils.AppendOptionsText(GetType());

    public Color RoleColor => KamikazeColor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Impostor;
    public RoleAlignment RoleAlignment => RoleAlignment.ImpostorConcealing;

    /// <summary>Habilidades del Kamikaze mostradas en la wiki del juego.</summary>
    [HideFromIl2Cpp] public List<CustomButtonWikiDescription> Abilities { get; } =
    [
        new("Transformar",
            "Transformate en un jugador aleatorio vivo durante unos segundos",
            TownofplusAssets.EnganoCambiaformasButton),
        new("Desvanecer",
            "Vuelvete invisible durante unos segundos.",
            TownofplusAssets.EnganoCamuflajeButton),
        new("Excavar",
            "Coloca un ducto falso que luego desaparece.",
            TownofplusAssets.EnganoDuctoButton),
        new("Arrastrar",
            "Arrastra un cuerpo cercano durante unos segundos.",
            TownofplusAssets.EnganoArrastreButton),
    ];

    public CustomRoleConfiguration Configuration => new(this)
    {
        // El Kamikaze NO tiene botón de kill vanilla: solo engaña.
        UseVanillaKillButton = false,

        // Ícono PNG en lobby, chat y ajustes (como los roles de TOU Mira).
        IconTmp = MiraAPI.Utilities.Assets.TmpSpriteUtils.CreateSpriteAsset(
            TownofplusAssets.KamikazeIcon.LoadAsset(), "TownOfPlus.Role.Impostor.Kamikaze", 1.45f),

        Icon = TownofplusAssets.KamikazeIcon,

        // Sonido de la pantalla de rol: Resources/Sound/KamikazeIntro.wav
        IntroSound = SonidosTop.KamikazeIntro,

        

        // Banner de la pagina de ajustes del rol.
        OptionsScreenshot = BannersTop.Kamikaze,
        DefaultRoleCount = 1,
        MaxRoleCount = 15,
        HideSettings = false,
        ShowInFreeplay = true,
    };

    public override void Initialize(PlayerControl player)
    {
        RoleBehaviourStubs.Initialize(this, player);
        KamikazeManager.ReiniciarPartida();
    }

    public override void Deinitialize(PlayerControl targetPlayer)
    {
        RoleBehaviourStubs.Deinitialize(this, targetPlayer);
        if (targetPlayer.AmOwner)
        {
            KamikazeManager.LimpiarEstadosLocales();
        }
    }
}
