using Il2CppInterop.Runtime.Attributes;
using MiraAPI.Patches.Stubs;
using MiraAPI.Roles;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Networking;
using TownOfPlus.Options.Crewmate.CrewmateSupport;
using TownOfPlus.Utilities;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Roles.Crewmate.CrewmateSupport;

/// <summary>
///     El Aislador (Crewmate Support) marca jugadores vivos con Soplar y los
///     encierra en una burbuja con Burbuja (3 s sin moverse ni usar botones).
///     Con Escudo se protege a sí mismo: burbuja que lo sigue, inmune a kills
///     durante unos segundos (copiado del escudo/barrier de TOU Mira), aunque
///     mientras dura no puede usar Soplar ni Burbuja.
///     Su color propio es #109FB9.
/// </summary>
public sealed class AisladorRole(IntPtr cppPtr)
    : CrewmateRole(cppPtr), ITownOfUsRole, IWikiDiscoverable
{
    /// <summary>Color propio del Aislador (#109FB9).</summary>
    public static readonly Color32 AisladorColor = new(16, 159, 185, 255);

    public string LocaleKey => "Aislador";
    public string RoleName => "Aislador";
    public string RoleDescription => "Aísla a otros con burbujas";
    public string RoleLongDescription =>
        "Marca jugadores con Soplar y enciérralos a todos\n" +
        "con Burbuja: quedan 3 segundos sin moverse ni usar\n" +
        "habilidades. También puedes protegerte con Escudo:\n" +
        "una burbuja te sigue y eres inmune a asesinatos.";

    public string GetAdvancedDescription() =>
        RoleLongDescription + MiscUtils.AppendOptionsText(GetType());

    public Color RoleColor => AisladorColor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;
    public RoleAlignment RoleAlignment => RoleAlignment.CrewmateSupport;

    [HideFromIl2Cpp] public List<CustomButtonWikiDescription> Abilities { get; } =
    [
        new("Soplar",
            "Marca a un jugador vivo con tu color (solo tú lo ves).",
            TownofplusAssets.AisladorSoplarButton),
        new("Burbuja",
            "Encierra a TODOS los marcados 3 segundos: sin moverse ni usar botones.",
            TownofplusAssets.AisladorBurbujaButton),
        new("Escudo",
            "Una burbuja te sigue y eres inmune a kills mientras dure.",
            TownofplusAssets.AisladorEscudoButton),
    ];

    public CustomRoleConfiguration Configuration => new(this)
    {
        IconTmp = MiraAPI.Utilities.Assets.TmpSpriteUtils.CreateSpriteAsset(
            TownofplusAssets.AisladorIcon.LoadAsset(), "TownOfPlus.Role.Crewmate.Aislador", 1.45f),

        Icon = TownofplusAssets.AisladorIcon,

        // Sonido de la pantalla de rol (igual que Kamikaze/Francotirador):
        // suena SOLO durante la intro que muestra tu rol.
        IntroSound = SonidosTop.AisladorIntro,

        // Banner de la pagina de ajustes del rol.
        OptionsScreenshot = BannersTop.Aislador,
        DefaultRoleCount = 0,
        MaxRoleCount = 1,
        HideSettings = false,
        ShowInFreeplay = true,
    };

    public override void Initialize(PlayerControl player)
    {
        RoleBehaviourStubs.Initialize(this, player);
        AisladorManager.ReiniciarPartida();
    }

    public override void Deinitialize(PlayerControl targetPlayer)
    {
        RoleBehaviourStubs.Deinitialize(this, targetPlayer);
        AisladorManager.LimpiarEstadosLocales();
    }

    /// <summary>
    ///     Reloj del rol: burbujas, escudo y tinte de nombres. Se ejecuta desde
    ///     AisladorTickPatch (PlayerControl.FixedUpdate) — este rol NO es un
    ///     MonoBehaviour, por lo que un FixedUpdate privado nunca se llamaría.
    /// </summary>
    public void AisladorTick()
    {
        try
        {
            AisladorManager.Actualizar();
        }
        catch
        {
            // Un tick del rol nunca debe romper la partida.
        }
    }
}
