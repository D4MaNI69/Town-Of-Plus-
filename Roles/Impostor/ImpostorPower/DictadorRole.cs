using Il2CppInterop.Runtime.Attributes;
using AmongUs.GameOptions;
using MiraAPI.GameOptions;
using MiraAPI.Patches.Stubs;
using MiraAPI.Roles;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Networking;
using TownOfPlus.Options.Impostor.ImpostorPower;
using TownOfPlus.Utilities;
using TownOfUs.Extensions;
using TownOfUs.Modules;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Roles.Crewmate;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Roles.Impostor.ImpostorPower;

/// <summary>
///     El Dictador (Candidato → Dictador) es un impostor de poder que empieza
///     como un impostor normal. En cada reunión acumula los votos EN CONTRA
///     (que no se resetean). Al alcanzar el umbral puede REVELARSE en reunión
///     con la animación del Mayor (de TOU Mira): todos creen que es el Mayor,
///     pero en realidad es el Dictador (impostor). Ya revelado, redirige como
///     máximo 2 votos AJENOS hacia el jugador que él votó (primero los votos
///     en su contra y, si queda hueco, 1 de los que van contra un compinche
///     impostor); no es adivinable.
///     REGLA MAYOR + DICTADOR: si el MAYOR de verdad se revela, el Dictador
///     puede revelarse SIN cumplir el requisito de votos en contra (en esa
///     reunión o en cualquier posterior).
/// </summary>
public sealed class DictadorRole(IntPtr cppPtr)
    : ImpostorRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IUnguessable
{
    /// <summary>Rojo impostor del Dictador.</summary>
    public static readonly Color32 DictadorColor = Palette.ImpostorRed;

    /// <summary>¿Ya se reveló como Dictador?</summary>
    public bool Revealed { get; set; }

    /// <summary>Votos EN CONTRA acumulados (entre reuniones).</summary>
    public int VotosEnContra { get; set; }

    private MeetingMenu? _meetingMenu;

    /// <summary>
    ///     ¿Puede revelarse ya? Cumple el umbral de votos en contra O BIEN el
    ///     MAYOR (TOU Mira) se reveló en esta partida: en ese caso el Dictador
    ///     puede revelarse sin cumplir el requisito, en esa misma reunión o en
    ///     cualquier reunión posterior. El resto de su mecánica no cambia.
    /// </summary>
    public bool PuedeRevelarse =>
        !Revealed &&
        (DictadorManager.MayorRevelado ||
         VotosEnContra >= OptionGroupSingleton<DictadorOptions>.Instance.VotosParaRevelar.Value);

    // ── IUnguessable (como Mayor revelado): no se puede adivinar ─────────────
    public bool IsGuessable => !Revealed;
    public RoleBehaviour AppearAs =>
        Revealed
            ? RoleManager.Instance.GetRole((RoleTypes)RoleId.Get<MayorRole>())
            : this;

    public string LocaleKey => "Dictador";
    public string RoleName => "Dictador";
    public string RoleDescription => "Acumula votos en contra y se revela como un falso Mayor";
    public string RoleLongDescription =>
        "Tu objetivo es conseguir votos en contra sin ser\n" +
        "expulsado en el proceso, cuando lo consigas podrás\n" +
        "revelarte como dictador, pero todos pensaran que eres Alcalde.\n" +
        "Ya revelado, se redirigen como máximo 2 votos ajenos hacia el\n" +
        "jugador que tú votes: primero los votos en tu contra y, si aún\n" +
        "queda hueco, 1 voto de los que van contra un compinche impostor.";

    public string GetAdvancedDescription() =>
        RoleLongDescription + MiscUtils.AppendOptionsText(GetType());

    public Color RoleColor => DictadorColor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Impostor;
    public RoleAlignment RoleAlignment => RoleAlignment.ImpostorPower;

    [HideFromIl2Cpp] public List<CustomButtonWikiDescription> Abilities { get; } =
    [
        new("Revelarse",
            "Revelate y haz creer a todos que eres bueno",
            TownofplusAssets.DictadorRevealButton),
    ];

    public CustomRoleConfiguration Configuration => new(this)
    {
        // Impostor normal en fase Candidato: kill vanilla activo.
        UseVanillaKillButton = true,

        // Sonido de intro: el MISMO del Politician de Town of Us Mira
        // (solo el sonido; sin cambiar otras mecánicas del rol).
        IntroSound = TownOfUs.Assets.TouAudio.PoliticianIntroSound,

        IconTmp = MiraAPI.Utilities.Assets.TmpSpriteUtils.CreateSpriteAsset(
            TownofplusAssets.DictadorIcon.LoadAsset(), "TownOfPlus.Role.Impostor.Dictador", 1.45f),

        Icon = TownofplusAssets.DictadorIcon,
        

        // Banner de la pagina de ajustes del rol.
        OptionsScreenshot = BannersTop.Dictador,
        DefaultRoleCount = 1,
        MaxRoleCount = 15,
        HideSettings = false,
        ShowInFreeplay = true,
    };

    public override void Initialize(PlayerControl player)
    {
        RoleBehaviourStubs.Initialize(this, player);
        Revealed = false;
        VotosEnContra = 0;

        // Partida nueva: se limpia también el desbloqueo por revelación del Mayor.
        DictadorManager.ReiniciarPartida();

        if (Player.AmOwner && !Revealed)
        {
            _meetingMenu = new MeetingMenu(
                this,
                Click,
                "Revelar",
                MeetingAbilityType.Click,
                TownofplusAssets.DictadorRevealButton,
                null!,
                IsExempt)
            {
                Position = new Vector3(-0.35f, 0f, -3f)
            };
        }
    }

    public override void OnMeetingStart()
    {
        RoleBehaviourStubs.OnMeetingStart(this);

        var meeting = MeetingHud.Instance;
        if (meeting == null)
        {
            return;
        }

        // Si ya estamos revelados, mostrar la animación post-reveal en reunión.
        if (Revealed)
        {
            DictadorManager.AnimarPostReveal(Player);
        }

        if (Player.AmOwner && !Revealed)
        {
            _meetingMenu?.GenButtons(meeting, Player.AmOwner && !Player.HasDied());
            HacerBotonRevealPequeno();
        }
    }

    /// <summary>
    ///     Escala los botones de Reveal del MeetingMenu a un tamaño claramente
    ///     menor (más discreto que el botón del Mayor).
    /// </summary>
    private void HacerBotonRevealPequeno()
    {
        if (_meetingMenu?.Buttons == null)
        {
            return;
        }

        foreach (var go in _meetingMenu.Buttons.Values)
        {
            if (go == null)
            {
                continue;
            }

            // Escala visiblemente menor: 60% del tamaño base.
            go.transform.localScale *= 0.6f;

            // Desplazar un poco el texto/label hacia abajo para que siga bien.
            var label = go.transform.Find("Label");
            if (label != null)
            {
                label.localScale = Vector3.one; // no re-escalar el texto aún más
            }
        }
    }

    public override void OnVotingComplete()
    {
        RoleBehaviourStubs.OnVotingComplete(this);
        if (Player.AmOwner)
        {
            _meetingMenu?.HideButtons();
        }
    }

    public override void Deinitialize(PlayerControl targetPlayer)
    {
        RoleBehaviourStubs.Deinitialize(this, targetPlayer);
        if (Player.AmOwner)
        {
            _meetingMenu?.Dispose();
            _meetingMenu = null;
        }
    }

    /// <summary>Click sobre el botón Reveal del propio votearea.</summary>
    public void Click(PlayerVoteArea voteArea, MeetingHud __)
    {
        if (!Player.AmOwner)
        {
            return;
        }

        // PATRÓN "Politician Reveal": durante la DISCUSIÓN no se puede revelar.
        if (ProfesorManager.DiscusionActiva)
        {
            return;
        }

        _meetingMenu?.HideButtons();

        if (!PuedeRevelarse)
        {
            return;
        }

        DictadorRpc.EnviarReveal(Player);
    }

    public bool IsExempt(PlayerVoteArea voteArea) =>
        voteArea?.PlayerId != Player.PlayerId;
}
