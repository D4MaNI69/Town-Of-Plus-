using Il2CppInterop.Runtime.Attributes;
using MiraAPI.Patches.Stubs;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using Reactor.Utilities;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Utilities;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Roles.Crewmate.CrewmateAfterlife;

/// <summary>
///     ECO — afterlife (post-mortem) de tripulante.
///
///     Es el TERCER afterlife crew del mod (hermano del Cerrajero y del Huésped):
///     mismo pipeline de conversión post-reunión por RPC del host descrito en
///     <see cref="AfterlifeManager"/>, mismo aspecto fantasma (invisible para los
///     vivos, capa Ghost para atravesar paredes y visión ilimitada) y color propio
///     #FFF099.
///
///     DIFERENCIAS CLAVE (todo lo demás lo lleva <see cref="EcoManager"/>):
///     <para>· ASIGNACIÓN SOLO POR MUERTE EN REUNIÓN: únicamente se convierte en
///     Eco un tripulante que murió DENTRO de la reunión (expulsado o
///     adivinado/ejecutado por un rol de reunión). Un kill en ronda o una muerte
///     fuera de reunión NUNCA lo convierten en Eco.</para>
///     <para>· ANCLA: se le asigna 1 jugador VIVO aleatorio (crew, impostor o
///     neutral) y reaparece en su posición. Si se aleja demasiado del ancla vuelve
///     a ella. Si el ancla muere o se desconecta, el Eco vuelve a ser un
///     fantasma común (con chat, voz y visión de fantasma normales).</para>
///     <para>· VISIBILIDAD DE ROLES: el Eco NO ve el rol de NADIE (ni en partida
///     ni en reunión): todos los nombres salen en blanco. Única excepción: su
///     ancla, cuyo nombre ve con el color de SU rol (ver Patches/EcoNombrePatches).</para>
///     <para>· CHAT: sus mensajes de reunión solo los lee su ancla y NO ve el chat
///     de fantasmas.</para>
///     <para>· VOZ (Perfect Comms, opcional): habla y oye SOLO a su ancla; el
///     resto de vivos no lo oyen y él no oye a fantasmas/afterlife.</para>
/// </summary>
public sealed class EcoRole(IntPtr cppPtr)
    : CrewmateRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IGhostRole
{
    /// <summary>Color propio del Eco — #FFF099 (RGB 255,240,153).</summary>
    public static readonly Color32 EcoColor = new(255, 240, 153, 255);

    public bool Setup { get; set; }
    public bool Caught { get; set; }
    public bool Faded { get; set; }
    public bool CanBeClicked { get; set; }
    public bool GhostActive => Setup && !Caught;

    public void Spawn() { }
    public void FadeUpdate() { }
    public void Clicked() { }
    public bool CanCatch() => false;

    public string LocaleKey => "Eco";
    public string RoleName => "Eco";
    public string RoleDescription => "Repite tu voz solo para tu ancla desde el más allá";
    public string RoleLongDescription
    {
        get
        {
            var texto =
                "Muere en reunión (expulsado o adivinado) para volverte Eco:\n" +
                "se te asigna un jugador vivo y solo él te ve, te lee\n" +
                "y puede hablarte. NO ves el rol de nadie (salvo el de\n" +
                "tu ancla: su nombre lo ves con su color). Si te alejas,\n" +
                "vuelves a su lado; si tu ancla muere o se desconecta\n" +
                "pasas a ser un fantasma común de nuevo.";

            // TARGET del rol: si YA está asignado, la descripción/los avisos
            // dicen a QUIÉN está atado ("Estás atado a {jugador}").
            try
            {
                if (Player?.Data != null)
                {
                    var ancla = EcoManager.AnclaDe(Player.PlayerId);
                    if (ancla.HasValue)
                    {
                        texto += "\nEstás atado a " +
                                 EcoManager.NombreDelAncla(ancla, Player.PlayerId) + ".";
                    }
                }
            }
            catch { }

            return texto;
        }
    }

    public string GetAdvancedDescription() =>
        RoleLongDescription + MiscUtils.AppendOptionsText(GetType());

    /// <summary>
    ///     NUNCA se asigna como rol vivo al empezar la partida (estilo
    ///     Haunter/Spectre de TOU Mira): AfterlifeGhostRolePatch lo excluye del
    ///     pool inicial y solo el host lo otorga por RPC al final de una reunión en
    ///     la que el jugador murió (ExileController.WrapUp → AfterlifeManager).
    ///     Tampoco aparece en los menús de cambio de rol (Profesor/Ambassador/etc.)
    ///     por ser IGhostRole.
    /// </summary>
    public bool IsDraftable => false;

    public Color RoleColor => EcoColor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;
    public RoleAlignment RoleAlignment => RoleAlignment.CrewmateAfterlife;

    [HideFromIl2Cpp]
    public List<CustomButtonWikiDescription> Abilities { get; } =
    [
        new("Ancla",
            "Un vivo aleatorio te ve, te lee y te oye; si te alejas vuelves a él",
            TownofplusAssets.EcoIcon),
    ];

    public CustomRoleConfiguration Configuration => new(this)
    {
        UseVanillaKillButton = false,

        // Ícono PNG en lobby, chat y ajustes (como los roles de TOU Mira).
        IconTmp = MiraAPI.Utilities.Assets.TmpSpriteUtils.CreateSpriteAsset(
            TownofplusAssets.EcoIcon.LoadAsset(), "TownOfPlus.Role.Crewmate.Eco", 1.45f),

        // Logo de la lista de ajustes (RoleIconSolid/RoleIconWhite): MISMO
        // asset que IconTmp (patrón Divani: IconTmp + Icon juntos).
        Icon = TownofplusAssets.EcoIcon,

        // Banner de la pagina de ajustes del rol.
        OptionsScreenshot = BannersTop.Eco,

        // El contador y la probabilidad SI se muestran como sliders vanilla
        // (# DE ROL y PROBABILIDAD %) y los lee AfterlifeManager vía GetAssignData.
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

        // Aspecto fantasma igual que el Cerrajero/Huésped: capa Ghost (atraviesa
        // paredes) + invisibilidad por defecto (el EcoManager se encarga de que
        // SOLO su ancla y los fantasmas lo vean, al 80% de opacidad).
        player.gameObject.layer = LayerMask.NameToLayer("Ghost");
        player.shouldAppearInvisible = true;
        player.cosmetics.ToggleNameVisible(false);

        EcoManager.AlInicializarRol(player);

        // Aviso de conversión CON TARGET: SOLO lo ve el jugador convertido y
        // dice el NOMBRE de su ancla ("... estás atado a «{nombre}»"). Se
        // difiere hasta tener el ancla (llega por RPC justo después del cambio
        // de rol), para no mostrar nunca el aviso sin nombre.
        if (player.AmOwner)
            Coroutines.Start(EsperarAnclaYAvisar(player.PlayerId));
    }

    private static System.Collections.IEnumerator EsperarAnclaYAvisar(byte ecoId)
    {
        var intentos = 0;
        while (intentos < 100)
        {
            var anclaId = EcoManager.AnclaDe(ecoId);
            if (anclaId.HasValue)
            {
                var nombre = EcoManager.NombreDelAncla(anclaId, ecoId);
                var texto = EcoManager.TextoEco(
                    "Asignado", nombre,
                    "¡Ahora eres el Eco del más allá! Estás atado a {0}.");
                try
                {
                    Helpers.CreateAndShowNotification(
                        "<b>" + texto + "</b>",
                        Color.white, new Vector3(0f, 1f, -20f),
                        spr: TownofplusAssets.EcoIcon.LoadAsset());
                }
                catch
                {
                    // Sin notificación no pasa nada.
                }
                yield break;
            }
            intentos++;
            yield return new WaitForSeconds(0.1f);
        }
    }

    public override void Deinitialize(PlayerControl targetPlayer)
    {
        RoleBehaviourStubs.Deinitialize(this, targetPlayer);

        EcoManager.SalirDelRol(targetPlayer);

        // Si el Eco se convirtió en un FANTASMA COMÚN (su ancla murió o se
        // desconectó) debe SEGUIR siendo un fantasma: capa Ghost (atraviesa
        // paredes) e invisible para los vivos. Si es el jugador LOCAL, además
        // se le devuelven los SPRITES al 100% de opacidad (el Eco los tenía al
        // 80%/0 y su propio personaje se veía apagado o invisible). El aspecto
        // de vivo solo se restaura cuando el jugador NO está muerto (reset de
        // fin de partida, lobby, etc.).
        if (targetPlayer.Data != null && targetPlayer.Data.IsDead)
        {
            if (targetPlayer.AmOwner)
                EcoManager.RestaurarFantasmaComun(targetPlayer);
            else
            {
                targetPlayer.gameObject.layer = LayerMask.NameToLayer("Ghost");
                targetPlayer.shouldAppearInvisible = true;
                targetPlayer.cosmetics.ToggleNameVisible(false);
            }

            return;
        }

        targetPlayer.gameObject.layer = LayerMask.NameToLayer("Players");
        targetPlayer.shouldAppearInvisible = false;
        targetPlayer.cosmetics.ToggleNameVisible(true);
    }
}
