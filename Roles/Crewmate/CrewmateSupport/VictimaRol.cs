using Il2CppInterop.Runtime.Attributes;
using MiraAPI.Patches.Stubs;
using MiraAPI.Roles;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Options.Crewmate.CrewmateSupport;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using TownOfPlus.Utilities;
using UnityEngine;

namespace TownOfPlus.Roles.Crewmate.CrewmateSupport;

/// <summary>
///     La Víctima es un tripulante Support. Con su botón NOTA abre un chat
///     temporal solo para ella (estilo Telecom/Journalist pero propio de
///     Town Of Plus) y guarda mensajes en secreto. Cuando muere o es
///     expulsada, TODAS sus notas se revelan a todos los jugadores en la
///     PRÓXIMA reunión (alerta tipo afterlife + burbuja negra estilo Dictador).
/// </summary>
public sealed class VictimaRole(IntPtr cppPtr)
    : CrewmateRole(cppPtr), ITownOfUsRole, IWikiDiscoverable
{
    /// <summary>Gris apagado de la Víctima (#646464).</summary>
    public static readonly Color32 VictimaColor = new(100, 100, 100, 255);

    public string LocaleKey => "Victima";
    public string RoleName => "Víctima";
    public string RoleDescription => "Guarda notas secretas que se revelan al morir";
    public string RoleLongDescription =>
        "Puedes escribir notas que duran hasta la siguiente reunión\n" +
        "Solo cuando te maten tus notas serán reveladas";

    public string GetAdvancedDescription() =>
        RoleLongDescription + MiscUtils.AppendOptionsText(GetType());

    public Color RoleColor => VictimaColor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;
    public RoleAlignment RoleAlignment => RoleAlignment.CrewmateSupport;

    [HideFromIl2Cpp]
    public List<CustomButtonWikiDescription> Abilities { get; } =
    [
        new("Nota",
            "Escribe notas que se revelaran en la siguiente reunión luego de morir",
            TownofplusAssets.NotaButton),
    ];

    public CustomRoleConfiguration Configuration => new(this)
    {
        UseVanillaKillButton = false,

        // Ícono PNG en lobby, chat y ajustes (como los roles de TOU Mira).
        IconTmp = TmpSpriteUtils.CreateSpriteAsset(
            TownofplusAssets.VictimaIcon.LoadAsset(), "TownOfPlus.Role.Crewmate.Victima", 1.45f),

        Icon = TownofplusAssets.VictimaIcon,

        

        // Banner de la pagina de ajustes del rol.
        OptionsScreenshot = BannersTop.Victima,
        DefaultRoleCount = 1,
        MaxRoleCount = 15,
        DefaultChance = 100,
        HideSettings = false,
        ShowInFreeplay = true,
    };

    public override void Initialize(PlayerControl player)
    {
        RoleBehaviourStubs.Initialize(this, player);

        if (player.AmOwner)
        {
            TownOfPlusPlugin.Instance?.Log.LogInfo("[Victima] Eres la Víctima: usa NOTA para guardar tus notas.");
        }
    }

    public override void Deinitialize(PlayerControl targetPlayer)
    {
        RoleBehaviourStubs.Deinitialize(this, targetPlayer);
        if (targetPlayer.AmOwner)
        {
            VictimaManager.BotonNotaLocal = null;
        }
    }
}
