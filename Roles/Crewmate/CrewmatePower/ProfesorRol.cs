using System;
using Il2CppInterop.Runtime.Attributes;
using MiraAPI.Patches.Stubs;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Utilities;
using TownOfUs.Modules;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Roles.Crewmate.CrewmatePower;

public sealed class ProfesorRole(IntPtr cppPtr)
    : CrewmateRole(cppPtr), ITownOfUsRole, IWikiDiscoverable
{
    public static readonly Color32 ProfesorColor = new(74, 168, 255, 255);

    public string LocaleKey => "Profesor";
    public string RoleName => "Profesor";
    public string RoleDescription => "Reentrena tripulantes para cambiarles el rol";
    public string RoleLongDescription =>
        "Completa tareas para ganar cargas de reentrenar.\n" +
        "En reunion marca a un tripulante (o a ti mismo).\n" +
        "Al terminar podra cambiar de rol entre 3 opciones.";

    public string GetAdvancedDescription() =>
        RoleLongDescription + MiscUtils.AppendOptionsText(GetType());

    public Color RoleColor => ProfesorColor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;
    public RoleAlignment RoleAlignment => RoleAlignment.CrewmatePower;

    public int CargasDisponibles { get; set; }
    public int RondasCooldown { get; set; }

    [HideFromIl2Cpp]
    public NetworkedPlayerInfo? SelectedPlr { get; set; }

    private MeetingMenu? _meetingMenu;

    [HideFromIl2Cpp]
    public List<CustomButtonWikiDescription> Abilities { get; } =
    [
        new("Reentrenar",
            "En reunion marca a un tripulante para que cambie de rol. Gana cargas con tareas.",
            SpritesProfesor.BotonReentrenarAsset),
        new("Cambiar Rol",
            "Si fuiste reentrenado, elige entre 3 roles de tripulante en ronda.",
            SpritesProfesor.BotonCambiarAsset),
    ];

    public CustomRoleConfiguration Configuration => new(this)
    {
        UseVanillaKillButton = false,

        // Sonido de la pantalla de rol: Resources/Sound/ProfesorIntro.wav
        IntroSound = SonidosTop.ProfesorIntro,

        IconTmp = TmpSpriteUtils.CreateSpriteAsset(
            SpritesProfesor.IconoRolAsset.LoadAsset(), "TownOfPlus.Role.Crewmate.Profesor", 1.45f),

        Icon = SpritesProfesor.IconoRolAsset,
        

        // Banner de la pagina de ajustes del rol.
        OptionsScreenshot = BannersTop.Profesor,
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
        CargasDisponibles = 0;
        RondasCooldown = 0;
        SelectedPlr = null;
        ProfesorManager.AlInicializarProfesor(player);

        // PATRÓN DICTADOR (funciona seguro): botón de reunión tipo CLICK,
        // un solo sprite y el Click valida y marca.
        if (Player.AmOwner)
        {
            _meetingMenu = new MeetingMenu(
                this,
                Click,
                "Reentrenar",
                MeetingAbilityType.Click,
                SpritesProfesor.BotonReentrenarAsset,
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
        SelectedPlr = null;

        var meeting = MeetingHud.Instance;
        if (meeting == null)
        {
            return;
        }

        if (Player.AmOwner)
        {
            _meetingMenu?.GenButtons(meeting, Player.AmOwner && !Player.HasDied());
            HacerBotonReentrenarPequeno();
        }
    }

    /// <summary>
    ///     PATRÓN DICTADOR: escala el botón de Reentrenar del MeetingMenu a un
    ///     tamaño menor (60%), igual que el botón de Revelar del Dictador, para
    ///     que no se vea gigante en la reunión.
    /// </summary>
    private void HacerBotonReentrenarPequeno()
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

            go.transform.localScale *= 0.6f;

            // El texto no se re-escala (mantener legible).
            var label = go.transform.Find("Label");
            if (label != null)
            {
                label.localScale = Vector3.one;
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
        SelectedPlr = null;
        if (Player.AmOwner)
        {
            _meetingMenu?.Dispose();
            _meetingMenu = null;
        }
    }

    /// <summary>
    ///     Exención: el botón NO aparece sobre ese votearea. Reglas del botón
    ///     (PEDIDO 2): 0 cargas = sin botones; marcar a un jugador = el botón de
    ///     ESE jugador desaparece para siempre; si se gastan todas las cargas en
    ///     la reunión = desaparecen TODOS los botones restantes.
    /// </summary>
    private bool IsExempt(PlayerVoteArea voteArea)
    {
        var target = GameData.Instance?.GetPlayerById(voteArea.PlayerId)?.Object;
        if (target == null || target.Data == null || target.Data.Disconnected || target.Data.IsDead)
        {
            return true;
        }

        // Base: solo Profesor, dentro del max global, sin cooldown y CON cargas.
        if (!ProfesorManager.PuedeMarcar(Player))
        {
            return true;
        }

        // Anti-re-reentreno: quien ya fue reentrenado esta partida no es
        // marcable; su botón desaparece para siempre en la partida.
        if (ProfesorManager.YaFueReentrenado(voteArea.PlayerId))
        {
            return true;
        }

        // Ya marcado en ESTA reunión.
        if (ProfesorManager.MarcadoEstaReunion(Player.PlayerId, voteArea.PlayerId))
        {
            return true;
        }

        // Sin cargas suficientes para marcar a más jugadores en esta reunión:
        // al gastar la última carga desaparecen TODOS los botones restantes.
        if (ProfesorManager.MarcadosEnReunion(Player.PlayerId) >= ProfesorManager.CargasDe(Player.PlayerId))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Click sobre el votearea del target (flujo multi-marcado por cargas).
    ///     Marca al jugador; al gastar todas las cargas desaparecen TODOS los
    ///     botones. El botón del marcado queda oculto para siempre en la partida.
    /// </summary>
    public void Click(PlayerVoteArea voteArea, MeetingHud __)
    {
        if (!Player.AmOwner || voteArea == null)
        {
            return;
        }

        // Regla global: en DISCUSIÓN los botones de habilidad están bloqueados
        // (se usan en la fase de VOTACIÓN, como el Reveal del Dictador).
        if (ProfesorManager.DiscusionActiva)
        {
            return;
        }

        if (IsExempt(voteArea))
        {
            return;
        }

        var targetId = voteArea.PlayerId;
        if (ProfesorManager.MarcadoEstaReunion(Player.PlayerId, targetId))
        {
            return;
        }

        if (ProfesorManager.MarcadosEnReunion(Player.PlayerId) >= ProfesorManager.CargasDe(Player.PlayerId))
        {
            return;
        }

        ProfesorManager.AniadirMarcadoReunion(Player.PlayerId, targetId);

        // PEDIDO 2: aviso SOLO para el Profesor (quien marca es siempre local).
        MostrarAvisoReentreno(targetId);

        Networking.ProfesorRpc.EnviarMarcar(Player, targetId);
    }

    /// <summary>Aviso local "Reentrenaste a (jugador)" para el Profesor.</summary>
    private void MostrarAvisoReentreno(byte targetId)
    {
        try
        {
            var nombre = MiscUtils.PlayerById(targetId)?.Data?.PlayerName ?? "un tripulante";
            Helpers.CreateAndShowNotification(
                $"<b>Reentrenaste a {nombre}</b>",
                Color.white, new Vector3(0f, 1f, -20f),
                spr: SpritesProfesor.IconoRolAsset.LoadAsset());
        }
        catch
        {
        }
    }
}
