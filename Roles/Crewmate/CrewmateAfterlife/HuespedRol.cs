using Il2CppInterop.Runtime.Attributes;
using MiraAPI.Patches.Stubs;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Options.Crewmate.CrewmateAfterlife;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using TownOfPlus.Utilities;
using UnityEngine;

namespace TownOfPlus.Roles.Crewmate.CrewmateAfterlife;

/// <summary>
///     HUÉSPED — afterlife (post-mortem) de tripulante, gemelo del Cerrajero
///     pero con la habilidad POSEER: un muerto tripulante que adquiere control
///     temporal de un jugador VIVO cercano para alejarlo de peligros o impedir
///     kills a su alrededor.
///
///     Hermano del Cerrajero (CrewmateAfterlife/CerrajeroRol): mismo patrón
///     completo de conversión post-reunión por RPC del host describiado en
///     AfterlifeManager, mismo aspecto fantasma (invisible para vivos, atraviesa
///     paredes con la capa Ghost y visión ilimitada), y con el color #109FB9.
/// </summary>
public sealed class HuespedRole(IntPtr cppPtr)
    : CrewmateRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IGhostRole
{
    /// <summary>Color del Huésped — #109FB9 (RGB 16,159,185).</summary>
    public static readonly Color32 HuespedColor = new(16, 159, 185, 255);

    public bool Setup { get; set; }
    public bool Caught { get; set; }
    public bool Faded { get; set; }
    public bool CanBeClicked { get; set; }
    public bool GhostActive => Setup && !Caught;

    public void Spawn() { }
    public void FadeUpdate() { }
    public void Clicked() { }
    public bool CanCatch() => false;

    public string LocaleKey => "Huesped";
    public string RoleName => "Huésped";
    public string RoleDescription => "Posee a un jugador vivo desde el más allá";
    public string RoleLongDescription =>
        "Aprovecha tu estado fantasmal para tomar el control\\n" +
        "de un jugador durante unos segundos y alejarlo del peligro";

    public string GetAdvancedDescription() =>
        RoleLongDescription + MiscUtils.AppendOptionsText(GetType());

    /// <summary>
    ///     NUNCA se asigna como rol vivo al empezar la partida (estilo
    ///     Haunter/Spectre de TOU Mira): AfterlifeGhostRolePatch lo excluye del
    ///     pool inicial y solo el host lo otorga por RPC al final de una reunión
    ///     (ExileController.WrapUp → AfterlifeManager). Tampoco aparece en los
    ///     menús de cambio de rol (Profesor/Ambassador/etc.) por ser IGhostRole.
    /// </summary>
    public bool IsDraftable => false;

    public Color RoleColor => HuespedColor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;
    public RoleAlignment RoleAlignment => RoleAlignment.CrewmateAfterlife;

    [HideFromIl2Cpp]
    public List<CustomButtonWikiDescription> Abilities { get; } =
    [
        new("Poseer",
            "Posee temporalmente a un jugador vivo cercano para moverlo",
            TownofplusAssets.PoseerButton),
    ];

    public CustomRoleConfiguration Configuration => new(this)
    {
        UseVanillaKillButton = false,

        // Ícono PNG en lobby, chat y ajustes (como los roles de TOU Mira).
        IconTmp = MiraAPI.Utilities.Assets.TmpSpriteUtils.CreateSpriteAsset(
            TownofplusAssets.HuespedIcon.LoadAsset(), "TownOfPlus.Role.Crewmate.Huesped", 1.45f),

        Icon = TownofplusAssets.HuespedIcon,

        

        // Banner de la pagina de ajustes del rol.
        OptionsScreenshot = BannersTop.Huesped,

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
        player.gameObject.layer = LayerMask.NameToLayer("Ghost");
        player.shouldAppearInvisible = true;
        player.cosmetics.ToggleNameVisible(false);

        // Aviso corto de conversión (solo para el jugador convertido).
        if (player.AmOwner)
        {
            try
            {
                Helpers.CreateAndShowNotification(
                    "<b>¡Ahora eres el Huésped del más allá!</b>\n" +
                    "Acércate a un vivo para controlarlo.",
                    Color.white, new Vector3(0f, 1f, -20f),
                    spr: TownofplusAssets.HuespedIcon.LoadAsset());
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

        // Si este jugador era el controlador o el objetivo de una posesión,
        // se cancela la posesión en curso (host difunde el fin).
        if (targetPlayer.PlayerId == HuespedManager.ControladorId ||
            targetPlayer.PlayerId == HuespedManager.ObjetivoId)
        {
            if (AmongUsClient.Instance.AmHost)
            {
                Networking.HuespedRpc.FinalizarPosesionGlobal();
            }
            HuespedManager.FinalizarPosesion();
        }

        targetPlayer.gameObject.layer = LayerMask.NameToLayer("Players");
        targetPlayer.shouldAppearInvisible = false;
        targetPlayer.cosmetics.ToggleNameVisible(true);
    }
}
