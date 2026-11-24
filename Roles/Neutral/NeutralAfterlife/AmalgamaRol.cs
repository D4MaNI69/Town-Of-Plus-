using System;
using Il2CppInterop.Runtime.Attributes;
using MiraAPI.Modifiers;
using MiraAPI.Patches.Stubs;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using Reactor.Utilities;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Options.Neutral.NeutralAfterlife;
using TownOfUs.Events;
using TownOfUs.Modifiers;
using TownOfUs.Modules.Wiki;
using TownOfUs.Options;
using TownOfUs.Patches;
using TownOfUs.Roles;
using TownOfUs.Roles.Neutral;
using TownOfUs.Utilities;
using TownOfUs.Utilities.Appearances;
using TownOfPlus.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace TownOfPlus.Roles.Neutral.NeutralAfterlife;

/// <summary>
///     La Amalgama es un rol Neutral Afterlife (post-mortem): nunca se asigna
///     vivo; el host la convierte tras una muerte (patrón Haunter/Spectre /
///     afterlife del proyecto). Su estado afterlife es UNA COPIA del patrón
///     de Haunter/Spectre de TOU Mira (cuerpo en capa "Players" con colisión,
///     spawn/vent en alcantarilla aleatoria según GhostwalkerVentSpawn y
///     SEMITRANSPARENCIA permanente vía Player.GhostFade(), tanto quieta como
///     en movimiento). Con su habilidad REVIVIR canaliza sobre un cuerpo para
///     volver a la vida con el color del cadáver y un rol aleatorio del pool
///     de roles habilitados en la sala.
/// </summary>
public sealed class AmalgamaRole(IntPtr cppPtr)
    : NeutralRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IGhostRole
{
    /// <summary>Rosa espectral de la Amalgama (#FF84CD).</summary>
    public static readonly Color32 AmalgamaColor = new(255, 132, 205, 255);

    public bool Setup { get; set; }
    public bool Caught { get; set; }
    public bool Faded { get; set; }
    public bool CanBeClicked { get; set; }
    public bool GhostActive => Setup && !Caught;

    public string LocaleKey => "Amalgama";
    public string RoleName => "Amalgama";
    public string RoleDescription => "Resucita en un cuerpo con un rol nuevo";
    public string RoleLongDescription =>
        "Tienes una segunda oportunidad de volver a la vida.\n" +
        "Pero tu destino es incierto, dime que es peor?\n" +
        "traicionar a tu tripulación o masacrar a tus propios compañeros.\n" +
        "¿Qué es peor que elegir entre dos males?";

    public string GetAdvancedDescription() =>
        RoleLongDescription + MiscUtils.AppendOptionsText(GetType());

    public Color RoleColor => AmalgamaColor;
    // ModdedRoleTeams solo tiene Crewmate/Impostor/Custom; para neutrales se usa Custom.
    public ModdedRoleTeams Team => ModdedRoleTeams.Custom;
    public RoleAlignment RoleAlignment => RoleAlignment.NeutralAfterlife;

    /// <summary>NUNCA se asigna vivo al inicio (afterlife): lo excluye AfterlifeGhostRolePatch.</summary>
    public bool IsDraftable => false;

    [HideFromIl2Cpp]
    public List<CustomButtonWikiDescription> Abilities { get; } =
    [
        new("Revivir",
            "Canaliza y revive sobre un cuerpo, cuando revivas tendrás un rol aleatorio",
            TownofplusAssets.RevivirButton),
    ];

    public CustomRoleConfiguration Configuration => new(this)
    {
        UseVanillaKillButton = false,

        // Ícono PNG en lobby, chat y ajustes.
        IconTmp = TmpSpriteUtils.CreateSpriteAsset(
            TownofplusAssets.AmalgamaIcon.LoadAsset(), "TownOfPlus.Role.Neutral.Amalgama", 1.45f),

        Icon = TownofplusAssets.AmalgamaIcon,

        

        // Banner de la pagina de ajustes del rol.
        OptionsScreenshot = BannersTop.Amalgama,

        // # DE ROL y PROBABILIDAD % como sliders vanilla (los lee AfterlifeManager).
        DefaultRoleCount = 1,
        MaxRoleCount = 1,
        DefaultChance = 100,
        CanModifyChance = true,

        HideSettings = false,
        ShowInFreeplay = true,
    };

    // ── PATRÓN SPECTRE / HAUNTER (TOU Mira) ─────────────────────────────────

    /// <summary>
    ///     Spawn copiado de SpectreRole/HaunterRole de TOU Mira: cuerpo en la
    ///     capa "Players" (con colisión y click), y en el dueño local un
    ///     spawn/vent en alcantarilla aleatoria según la opción global
    ///     GhostwalkerVentSpawn (la Amalgama es evil-aligned como Spectre).
    /// </summary>
    public void Spawn()
    {
        Setup = true;

        if (HudManagerPatches.CamouflageCommsEnabled)
        {
            Player.SetCamouflage(false);
        }

        Player.gameObject.layer = LayerMask.NameToLayer("Players");

        Player.gameObject.GetComponent<PassiveButton>().OnClick = new Button.ButtonClickedEvent();
        Player.gameObject.GetComponent<PassiveButton>().OnClick.AddListener((Action)(() => Player.OnClick()));
        Player.gameObject.GetComponent<BoxCollider2D>().enabled = true;

        if (Player.AmOwner)
        {
            // TOU Mira 1.7.x: aparición en una alcantarilla aleatoria
            // (SpawnAtRandomVent, el mismo camino de Haunter/Spectre).
            Player.SpawnAtRandomVent();
            Player.MyPhysics.ResetMoveState();

            HudManager.Instance.SetHudActive(false);
            HudManager.Instance.SetHudActive(true);
            HudManagerPatches.ResetZoom();
        }
    }

    public void Clicked() { }
    public bool CanCatch() => false;

    /// <summary>
    ///     FadeUpdate copiado de SpectreRole/HaunterRole: mientras el ghost
    ///     está activo, SEMITRANSPARENCIA (Player.GhostFade) tanto quieta como
    ///     en movimiento. Si dejara de estar activa, restaura apariencia.
    /// </summary>
    public void FadeUpdate()
    {
        if (!Caught && Setup)
        {
            Player.GhostFade();
            Faded = true;
        }
        else if (Faded)
        {
            Player.ResetAppearance();
            Player.cosmetics.ToggleNameVisible(true);

            Player.cosmetics.currentBodySprite.BodySprite.color = Color.white;
            Player.gameObject.layer = LayerMask.NameToLayer("Ghost");
            Player.MyPhysics.ResetMoveState();

            Faded = false;
        }
    }

    /// <summary>FixedUpdate copiado de SpectreRole (sin tareas de ghost).</summary>
    public void FixedUpdate()
    {
        if (!Player || Player.Data.Role is not AmalgamaRole || MeetingHud.Instance)
        {
            return;
        }

        FadeUpdate();
    }

    public override void Initialize(PlayerControl player)
    {
        RoleBehaviourStubs.Initialize(this, player);

        // Patrón Haunter (TOU Mira): marca que ya fue ghost para que TOU Mira
        // no le asigne sus propios afterlife encima; y ajusta sus tareas.
        if (!Player.HasModifier<BasicGhostModifier>())
        {
            Player.AddModifier<BasicGhostModifier>();
        }
        MiscUtils.AdjustGhostTasks(player);

        // En el momento de la conversión (WrapUp) ponemos el estado espectral
        // en marcha de inmediato; TOU Mira además llamará a Spawn() en cada
        // RoundStart (GhostRoleEvents), igual que hace con Haunter/Spectre.
        try
        {
            Spawn();
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Amalgama] Spawn inicial falló: {ex.Message}");
        }

        if (player.AmOwner)
        {
            try
            {
                Coroutines.Start(MiscUtils.CoFlash(Color.white));
                Helpers.CreateAndShowNotification(
                    "<b>¡Eres el Amalgama del más allá!</b>\n" +
                    "Canaliza sobre un cuerpo para resucitar con un rol nuevo.",
                    Color.white, new Vector3(0f, 1f, -20f),
                    spr: TownofplusAssets.AmalgamaIcon.LoadAsset());
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
        AmalgamaManager.LimpiarEstadosLocales();

        // Revive / fin del rol: restaurar apariencia de jugador vivo.
        Caught = true;
        Setup = false;

        try
        {
            targetPlayer.ResetAppearance();
            targetPlayer.cosmetics.currentBodySprite.BodySprite.color = Color.white;
            targetPlayer.MyPhysics.ResetMoveState();
        }
        catch
        {
            // ResetAppearance puede no estar disponible: sin fallo crítico.
        }

        targetPlayer.gameObject.layer = LayerMask.NameToLayer("Players");
        targetPlayer.shouldAppearInvisible = false;
        targetPlayer.cosmetics.ToggleNameVisible(true);

        // Sin esto el jugador revive atravesando paredes: restaurar la
        // física/colisión de jugador VIVO.
        AmalgamaManager.RestaurarColisionViva(targetPlayer);
    }
}
