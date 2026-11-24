using Il2CppInterop.Runtime.Attributes;
using MiraAPI.GameOptions;
using MiraAPI.Patches.Stubs;
using MiraAPI.Roles;
using MiraAPI.Translation;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Options.Impostor.ImpostorSupport;
using TownOfPlus.Utilities;
using TownOfUs.Modules;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Roles.Impostor.ImpostorSupport;

/// <summary>
///     ABOGADO — impostor support.
///     Ronda: Chantaje (marca 1 jugador: vote a quien vote, su voto se
///     REDIRIGE a skip; su nombre sale en gris #535b61 solo para el Abogado,
///     impostores aliados y alliances aliadas). Siempre usable en partida.
///     Reunión: Terminar (fuerza skip de TODOS; botón de habilidad de reunión
///     estilo MAYOR de TOU Mira, fila inferior). Disponible en la 1ª reunión y
///     luego cada N reuniones (ajuste, default 2).
///     NUNCA puede llevar Saboteur de TOU Mira ni nada que acelere sabotajes.
/// </summary>
public sealed class AbogadoRole(IntPtr cppPtr)
    : ImpostorRole(cppPtr), ITownOfUsRole, IWikiDiscoverable
{
    public static readonly Color32 AbogadoColor = Palette.ImpostorRed;

    public string LocaleKey => "Abogado";
    public string RoleName => "Abogado";
    public string RoleDescription => "Chantajea siempre; Terminar cada N reuniones";

    public string RoleLongDescription
    {
        get
        {
            var texto =
                "Chantajea a 1 jugador: vote a quien vote, su voto se\n" +
                "redirige a skip (lo ves en gris). Chantaje siempre usable.\n" +
                "En reunión (votación): TERMINAR fuerza skip de TODOS.\n" +
                "Disponible en la 1ª reunión y luego cada " +
                AbogadoManager.ReunionesEntreUsos +
                " reuniones.";
            var resto = AbogadoManager.ReunionesRestantes;
            if (resto > 0)
            {
                texto += "\nVuelve en " + resto + " reunión(es).";
            }

            return texto;
        }
    }

    public string GetAdvancedDescription() =>
        RoleLongDescription + MiraLocaleManager.Get("TownOfUsMira.Role.AbogadoRole.TabDescription", string.Empty) +
        TownOfUs.Utilities.MiscUtils.AppendOptionsText(GetType());

    public Color RoleColor => AbogadoColor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Impostor;
    public RoleAlignment RoleAlignment => RoleAlignment.ImpostorSupport;

    [HideFromIl2Cpp] public List<CustomButtonWikiDescription> Abilities { get; } =
    [
        new("Chantaje",
            "Marca a 1 jugador: su voto se redirige a SKIP",
            TownofplusAssets.AbogadoChantajeAsset),
        new("Terminar",
            "Fuerza skip de TODOS en la reunión (cada N reuniones)",
            TownofplusAssets.AbogadoTerminarAsset),
    ];

    public CustomRoleConfiguration Configuration => new(this)
    {
        UseVanillaKillButton = true,
        CanUseVent = true,
        CanUseSabotage = true,

        // Ícono PNG en lobby, chat y ajustes (como el resto de roles del mod).
        IconTmp = MiraAPI.Utilities.Assets.TmpSpriteUtils.CreateSpriteAsset(
            TownofplusAssets.AbogadoIcon.LoadAsset(), "TownOfPlus.Role.Impostor.Abogado", 1.45f),
        Icon = TownofplusAssets.AbogadoIcon,
        OptionsScreenshot = BannersTop.Abogado,

        // CANTIDAD: 0–15 (slider vanilla; 0 = desactivado).
        DefaultRoleCount = 1,
        MaxRoleCount = 15,
        DefaultChance = 100,
        CanModifyChance = true,

        HideSettings = false,
        ShowInFreeplay = true,
    };

    public override void Initialize(PlayerControl player)
    {
        RoleBehaviourStubs.Initialize(this, player);
        AbogadoManager.ReiniciarJugador(player);
    }

    public override void OnMeetingStart()
    {
        RoleBehaviourStubs.OnMeetingStart(this);
    }

    public override void OnVotingComplete()
    {
        RoleBehaviourStubs.OnVotingComplete(this);
    }

    public override void Deinitialize(PlayerControl targetPlayer)
    {
        RoleBehaviourStubs.Deinitialize(this, targetPlayer);
        AbogadoManager.SalirDelRol(targetPlayer);
    }
}

