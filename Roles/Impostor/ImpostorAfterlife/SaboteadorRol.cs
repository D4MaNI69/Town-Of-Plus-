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

namespace TownOfPlus.Roles.Impostor.ImpostorAfterlife;

public sealed class SaboteadorRole(IntPtr cppPtr)
    : ImpostorRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IGhostRole
{
    /// <summary>
    ///     Color del Saboteador: usa el color IMPOSTOR (rojo vanilla), igual
    ///     que el resto de roles impostor de TOU Mira (Kamikaze, Dictador,
    ///     ...). Se aplica a nombre, intro, UI, wiki y botones.
    /// </summary>
    public static readonly Color32 SaboteadorColor = Palette.ImpostorRed;

    public bool Setup { get; set; }
    public bool Caught { get; set; }
    public bool Faded { get; set; }
    public bool CanBeClicked { get; set; }
    public bool GhostActive => Setup && !Caught;

    public void Spawn() { }
    public void FadeUpdate() { }
    public void Clicked() { }
    public bool CanCatch() => false;

    public string LocaleKey => "Saboteador";
    public string RoleName => "Saboteador";
    public string RoleDescription => "Apaga las luces desde el más allá";
    public string RoleLongDescription =>
        "Puedes sabotear electricidad durante unos segundos\n" +
        "Incluso si tus aliados ya sabotearon otra cosa";

    public string GetAdvancedDescription() =>
        RoleLongDescription + MiscUtils.AppendOptionsText(GetType());

    /// <summary>
    ///     NUNCA se asigna como rol vivo al empezar la partida (estilo
    ///     Haunter/Spectre de TOU Mira): AfterlifeGhostRolePatch lo excluye del
    ///     pool inicial y solo el host lo otorga por RPC al final de una reunión
    ///     (ExileController.WrapUp → AfterlifeManager).
    /// </summary>
    public bool IsDraftable => false;

    public Color RoleColor => SaboteadorColor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Impostor;
    public RoleAlignment RoleAlignment => RoleAlignment.ImpostorAfterlife;

    [HideFromIl2Cpp]
    public List<CustomButtonWikiDescription> Abilities { get; } =
    [
        new("Luces Fuera",
            "Apaga las luces para ayudar a tus aliados, ellos sabrán que apagaste las luces",
            TownofplusAssets.LucesFueraButton),
    ];

    public CustomRoleConfiguration Configuration => new(this)
    {
        UseVanillaKillButton = false,

        // Ícono PNG en lobby, chat y ajustes (como los roles de TOU Mira).
        IconTmp = MiraAPI.Utilities.Assets.TmpSpriteUtils.CreateSpriteAsset(
            TownofplusAssets.SaboteadorIcon.LoadAsset(), "TownOfPlus.Role.Impostor.Saboteador", 1.45f),

        Icon = TownofplusAssets.SaboteadorIcon,

        

        // Banner de la pagina de ajustes del rol.
        OptionsScreenshot = BannersTop.Saboteador,

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

    public void FantasmaVacio(){}

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
                    "<b>¡Ahora eres el Saboteador del más allá!</b>\n" +
                    "Apaga las luces para cegar a la tripulación.",
                    Color.white, new Vector3(0f, 1f, -20f),
                    spr: TownofplusAssets.SaboteadorIcon.LoadAsset());
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
        SaboteadorManager.LimpiarEstadosLocales();

        targetPlayer.gameObject.layer = LayerMask.NameToLayer("Players");
        targetPlayer.shouldAppearInvisible = false;
        targetPlayer.cosmetics.ToggleNameVisible(true);
    }
}
