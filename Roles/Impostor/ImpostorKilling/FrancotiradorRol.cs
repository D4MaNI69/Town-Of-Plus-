using Il2CppInterop.Runtime.Attributes;
using MiraAPI.Roles;
using TownOfPlus.Assets;
using TownOfPlus.Utilities;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Roles.Impostor.ImpostorKilling;

/// <summary>
///     El Francotirador es un impostor de eliminación sin botón de kill normal:
///     debe recargar su rifle (Recargando… → Recargar → Preparando → Disparar),
///     quedarse quieto mientras prepara y usar el zoom out para ver todo el mapa
///     a través de las paredes con puntos rojos sobre sus objetivos, a quienes
///     mata desde la lejanía con un clic.
/// </summary>
public sealed class FrancotiradorRole(IntPtr cppPtr)
    : ImpostorRole(cppPtr), ITownOfUsRole, IWikiDiscoverable
{
    /// <summary>Rojo carmesí del Francotirador.</summary>
    public static readonly Color32 FrancotiradorColor = new(220, 40, 40, 255);

    public string LocaleKey => "Francotirador";
    public string RoleName => "Francotirador";
    public string RoleDescription => "Recarga tu rifle y elimina desde la lejanía";
    public string RoleLongDescription =>
        "Espera a recargar, cuando lo hagas quedate quieto\n" +
        "Luego siéntete libre de matar a la distancia.\n" +
        "Pero cuidado con que escuchen tus disparos";

    public string GetAdvancedDescription() =>
        RoleLongDescription + MiscUtils.AppendOptionsText(GetType());

    public Color RoleColor => FrancotiradorColor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Impostor;
    public RoleAlignment RoleAlignment => RoleAlignment.ImpostorKilling;

    /// <summary>Botones del Francotirador mostrados en la wiki del juego.</summary>
    [HideFromIl2Cpp] public List<CustomButtonWikiDescription> Abilities { get; } =
    [
        new("Recargar",
            "Comienza la recarga del rifle. Debes quedarte quieto mientras se prepara antes de poder disparar.",
            TownofplusAssets.RecargarButton),
        new("Disparar",
            "verás casi todo el mapa con un punto rojo sobre cada objetivo. Pulsa una mira para eliminar a ese jugador desde la distancia.",
            TownofplusAssets.DispararButton),
    ];

    public CustomRoleConfiguration Configuration => new(this)
    {
        // El Francotirador NO tiene botón de kill vanilla: solo su rifle.
        UseVanillaKillButton = false,

        // Ícono PNG en lobby, chat y ajustes (como los roles de TOU Mira).
        IconTmp = MiraAPI.Utilities.Assets.TmpSpriteUtils.CreateSpriteAsset(
            TownofplusAssets.FrancotiradorIcon.LoadAsset(), "TownOfPlus.Role.Impostor.Francotirador", 1.45f),

        Icon = TownofplusAssets.FrancotiradorIcon,

        // Sonido de la pantalla de rol: plugins/TownOfPlus/Sonidos/Francotirador.wav
        IntroSound = SonidosTop.FrancotiradorIntro,

        

        // Banner de la pagina de ajustes del rol.
        OptionsScreenshot = BannersTop.Francotirador,
        DefaultRoleCount = 1,
        MaxRoleCount = 15,
        HideSettings = false,
        ShowInFreeplay = true,
    };
}
