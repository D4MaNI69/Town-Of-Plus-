using Il2CppInterop.Runtime.Attributes;
using MiraAPI.Patches.Stubs;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Modifiers.Crewmate;
using TownOfPlus.Options.Crewmate.CrewmateAfterlife;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using TownOfPlus.Utilities;
using UnityEngine;

namespace TownOfPlus.Roles.Crewmate.CrewmateAfterlife;

public sealed class CerrajeroRole(IntPtr cppPtr)
    : CrewmateRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IGhostRole
{
    public static readonly Color32 CerrajeroColor = new(194, 143, 49, 255);

    public bool Setup { get; set; }
    public bool Caught { get; set; }
    public bool Faded { get; set; }
    public bool CanBeClicked { get; set; }
    public bool GhostActive => Setup && !Caught;

    public void Spawn() { }
    public void FadeUpdate() { }
    public void Clicked() { }
    public bool CanCatch() => false;

    public string LocaleKey => "Cerrajero";
    public string RoleName => "Cerrajero";
    public string RoleDescription => "Abre y cierra puertas desde el más allá";
    public string RoleLongDescription =>
        "Aprovecha tu habilidad de cerrar y abrir puertas\n" +
        "para ayudar a la tripulación o dificultar a los traidores";

    public string GetAdvancedDescription() =>
        RoleLongDescription + MiscUtils.AppendOptionsText(GetType());

    /// <summary>
    ///     NUNCA se asigna como rol vivo al empezar la partida (estilo
    ///     Haunter/Spectre de TOU Mira): AfterlifeGhostRolePatch lo excluye del
    ///     pool inicial y solo el host lo otorga por RPC al final de una reunión
    ///     (ExileController.WrapUp → AfterlifeManager).
    /// </summary>
    public bool IsDraftable => false;

    public Color RoleColor => CerrajeroColor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;
    public RoleAlignment RoleAlignment => RoleAlignment.CrewmateAfterlife;

    [HideFromIl2Cpp]
    public List<CustomButtonWikiDescription> Abilities { get; } =
    [
        new("Abrir",
            "Abre la puerta que tengas cerca",
            TownofplusAssets.AbrirButton),
        new("Cerrar",
            "Cierra la puerta que tengas cerca",
            TownofplusAssets.CerrarButton),
    ];

    public CustomRoleConfiguration Configuration => new(this)
    {
        UseVanillaKillButton = false,

        // Ícono PNG en lobby, chat y ajustes (como los roles de TOU Mira).
        IconTmp = MiraAPI.Utilities.Assets.TmpSpriteUtils.CreateSpriteAsset(
            TownofplusAssets.CerrajeroIcon.LoadAsset(), "TownOfPlus.Role.Crewmate.Cerrajero", 1.45f),

        // Logo de la lista de ajustes (RoleIconSolid/RoleIconWhite): MISMO
        // asset que IconTmp (patrón Divani: IconTmp + Icon juntos).
        Icon = TownofplusAssets.CerrajeroIcon,

        IntroSound = TownOfPlus.Utilities.SonidosTop.CerrajeroIntro,
        

        // Banner de la pagina de ajustes del rol.
        OptionsScreenshot = BannersTop.Cerrajero,

        // El contador y la probabilidad SI se muestran como sliders vanilla
        // (# DE ROL y PROBABILIDAD %) y los lee AfterlifeManager vía GetAssignData.
        // AfterlifeGhostRolePatch evita que se asigne vivo al inicio.
        DefaultRoleCount = 1,
        MaxRoleCount = 1,
        DefaultChance = 50,
        CanModifyChance = true,

        HideSettings = false,
        ShowInFreeplay = true,
    };

    public override void Initialize(PlayerControl player)
    {
        RoleBehaviourStubs.Initialize(this, player);
        CerrajeroManager.Reiniciar(player);

        player.gameObject.layer = LayerMask.NameToLayer("Ghost");
        player.shouldAppearInvisible = true;
        player.cosmetics.ToggleNameVisible(false);

        // Aviso corto de conversión (solo para el jugador convertido).
        if (player.AmOwner)
        {
            try
            {
                Helpers.CreateAndShowNotification(
                    "<b>¡Ahora eres el Cerrajero del más allá!</b>\n" +
                    "Abre y cierra puertas para ayudar a tu equipo.",
                    Color.white, new Vector3(0f, 1f, -20f),
                    spr: TownofplusAssets.CerrajeroIcon.LoadAsset());
            }
            catch
            {
                // Sin notificación no pasa nada.
            }
        }
    }

    public override void Deinitialize(PlayerControl targetPlayer)
    {
        RoleBehaviourStubs.Deinitialize(this, targetPlayer);
        CerrajeroManager.Salir(targetPlayer);

        targetPlayer.gameObject.layer = LayerMask.NameToLayer("Players");
        targetPlayer.shouldAppearInvisible = false;
        targetPlayer.cosmetics.ToggleNameVisible(true);
    }
}
