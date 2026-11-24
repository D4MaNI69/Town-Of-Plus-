using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.Events.Vanilla.Meeting.Voting;
using MiraAPI.Patches.Stubs;
using MiraAPI.Roles;
using TownOfPlus.Modules;
using TownOfPlus.Roles.Crewmate.CrewmateAfterlife;
using TownOfUs.Roles;
using UnityEngine;

namespace TownOfPlus.Patches;

/// <summary>
///     Inmunidad del Huésped: cancela cualquier intento de muerte (vanilla o
///     custom) contra este afterlife.
/// </summary>
public static class HuespedImmunityEvents
{
    [RegisterEvent]
    public static void BeforeMurderHandler(BeforeMurderEvent @event)
    {
        if (@event.Target?.Data?.Role is HuespedRole)
        {
            @event.Cancel();
            return;
        }

        // BLOQUEO del poseído: no puede ser asesinado durante el control (el
        // Huésped lo usa para salvarlo). Su propio botón de kill ya está
        // desactivado por HuespedBotonesPoseidoPatch, así que tampoco ataca.
        if (HuespedManager.Activa && @event.Target != null &&
            @event.Target.PlayerId == HuespedManager.ObjetivoId)
        {
            @event.Cancel();
        }
    }
}

/// <summary>
///     Aspecto del Huésped fantasma: copia la regla VANILLA de Among Us — un
///     observador muerto ve a los fantasmas, un vivo no. Usa el cuerpo vanilla
///     del jugador, sin depender de sprites propios (como el Cerrajero).
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class HuespedVisualPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (__instance == null || __instance.Data == null || __instance.Data.Role == null)
        {
            return;
        }

        if (__instance.Data.Role is not HuespedRole)
        {
            return;
        }

        var local = PlayerControl.LocalPlayer;
        if (local == null || local.Data == null)
        {
            return;
        }

        // Regla vanilla: los vivos no ven fantasmas; los muertos/afterlife sí.
        var deberiaVerse = local.Data.IsDead ||
                           local.Data.Role is IGhostRole ||
                           local.PlayerId == __instance.PlayerId;

        if (deberiaVerse)
        {
            Mostrar(__instance);
        }
        else
        {
            Ocultar(__instance);
        }
    }

    private static void Mostrar(PlayerControl player)
    {
        player.shouldAppearInvisible = false;
        try { player.gameObject.SetActive(true); } catch { }

        var cosmetics = player.cosmetics;
        if (cosmetics == null)
        {
            return;
        }

        var body = cosmetics.currentBodySprite?.BodySprite;
        if (body != null)
        {
            body.color = body.color.SetAlpha(1f);
        }

        try { cosmetics.ToggleNameVisible(true); } catch { }
        cosmetics.nameText.color = cosmetics.nameText.color.SetAlpha(1f);

        try
        {
            player.SetHatAndVisorAlpha(1f);
            if (cosmetics.currentPet != null && cosmetics.currentPet.gameObject != null)
            {
                cosmetics.currentPet.gameObject.SetActive(true);
            }

            if (cosmetics.skin != null && cosmetics.skin.gameObject != null)
            {
                cosmetics.skin.gameObject.SetActive(true);
            }

            if (cosmetics.hat != null && cosmetics.hat.gameObject != null)
            {
                cosmetics.hat.gameObject.SetActive(true);
            }

            if (cosmetics.visor != null && cosmetics.visor.gameObject != null)
            {
                cosmetics.visor.gameObject.SetActive(true);
            }
        }
        catch
        {
            // Cosmeticos opcionales.
        }

        foreach (var rend in player.transform.GetChild(2)
                     .GetComponentsInChildren<SpriteRenderer>())
        {
            rend.color = rend.color.SetAlpha(1f);
        }
    }

    private static void Ocultar(PlayerControl player)
    {
        player.shouldAppearInvisible = true;

        var cosmetics = player.cosmetics;
        if (cosmetics != null)
        {
            var body = cosmetics.currentBodySprite?.BodySprite;
            if (body != null)
            {
                body.color = body.color.SetAlpha(0f);
            }

            try { cosmetics.ToggleNameVisible(false); } catch { }
            cosmetics.nameText.color = cosmetics.nameText.color.SetAlpha(0f);

            foreach (var rend in player.transform.GetChild(2)
                         .GetComponentsInChildren<SpriteRenderer>())
            {
                rend.color = rend.color.SetAlpha(0f);
            }
        }
    }
}

/// <summary>
///     Replica fielmente el setter vanilla de PlayerControl.Visible: fuerza que
///     el Huésped sea visible solo cuando el observador esté muerto, sea un
///     afterlife o sea él mismo (regla vanilla de fantasmas).
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Visible), MethodType.Setter)]
public static class HuespedVisibleVanillaPatch
{
    public static void Prefix(PlayerControl __instance, ref bool value)
    {
        if (__instance == null || __instance.Data == null || __instance.Data.Role == null)
        {
            return;
        }

        if (__instance.Data.Role is not HuespedRole)
        {
            return;
        }

        var local = PlayerControl.LocalPlayer;
        if (local == null || local.Data == null)
        {
            return;
        }

        var deberiaVerse = local.Data.IsDead ||
                           local.Data.Role is IGhostRole ||
                           local.PlayerId == __instance.PlayerId;
        value = deberiaVerse;
    }
}

/// <summary>Nadie puede proteger al Huésped con el Escudo del Ángel Guardián.</summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ProtectPlayer))]
public static class HuespedProtectBlockPatch
{
    public static bool Prefix(PlayerControl target)
    {
        return target?.Data?.Role is not HuespedRole;
    }
}

/// <summary>Bloquea el targeting de roles custom sobre el Huésped.</summary>
[HarmonyPatch(typeof(RoleBehaviourStubs), nameof(RoleBehaviourStubs.IsValidTarget))]
public static class HuespedValidTargetPatch
{
    public static bool Prefix(RoleBehaviour __instance, NetworkedPlayerInfo target, ref bool __result)
    {
        if (target?.Object?.Data?.Role is HuespedRole)
        {
            __result = false;
            return false;
        }

        return true;
    }
}

/// <summary>Filtra al Huésped de los resultados de FindClosestTarget.</summary>
[HarmonyPatch(typeof(RoleBehaviourStubs), nameof(RoleBehaviourStubs.FindClosestTarget))]
public static class HuespedFindClosestTargetPatch
{
    public static void Postfix(ref PlayerControl __result)
    {
        if (__result?.Data?.Role is HuespedRole)
        {
            __result = null!;
        }
    }
}

/// <summary>
///     El Huésped no puede reportar cuerpos ni usar reuniones de emergencia.
/// </summary>
public static class HuespedMeetingEvents
{
    [RegisterEvent]
    public static void OnStartMeeting(StartMeetingEvent _)
    {
        // Cualquier reunión corta la posesión en curso (host difunde el fin).
        try
        {
            if (AmongUsClient.Instance.AmHost)
            {
                Networking.HuespedRpc.FinalizarPosesionGlobal();
            }

            HuespedManager.FinalizarPosesion();
        }
        catch
        {
            // Nunca romper la reunión por un error del rol.
        }
    }

    [RegisterEvent]
    public static void OnReportBody(ReportBodyEvent @event)
    {
        if (@event.Reporter?.Data?.Role is HuespedRole)
        {
            @event.Cancel();
            return;
        }

        // El POSEÍDO tampoco puede reportar mientras es controlado.
        if (HuespedManager.Activa && @event.Reporter != null &&
            @event.Reporter.PlayerId == HuespedManager.ObjetivoId)
        {
            @event.Cancel();
        }
    }

    [RegisterEvent]
    public static void OnBeforeVote(BeforeVoteEvent @event)
    {
        if (@event.Player?.Data?.Role is not HuespedRole)
        {
            return;
        }

        @event.Cancel();

        if (MeetingHud.Instance == null || @event.VoteArea == null)
        {
            return;
        }

        @event.VoteArea.SetVote(252);
        MeetingHud.Instance.Confirm(252);
    }
}

/// <summary>
///     Actualización por frame del Huésped: mientras controla, lee su WASD y lo
///     envía al host (poseer).
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class HuespedManagerUpdatePatch
{
    public static void Postfix()
    {
        try
        {
            HuespedManager.Actualizar();
        }
        catch
        {
            // Nunca romper el HUD por un fallo del rol.
        }
    }
}

/// <summary>
///     Congelación del POSEÍDO + ancla del Huésped (PlayerPhysics.FixedUpdate).
///
///     - Si ESTE cliente es el POSEÍDO: se anula su velocidad y se inyecta la
///       dirección del Huésped mediante SetNormalizedVelocity (el MISMO camino
///       que el movimiento vanilla: animaciones + colisiones/muros + sync por
///       NetTransform). El teclado ya está inutilizado (moveable=false), así
///       que no puede contrarrestar el control.
///     - Si ESTE cliente es el HUÉSPED que controla: su fantasma se ancla sobre
///       el cuerpo poseído (el fantasma lo sigue; la cámara no se pierde).
/// </summary>
[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
public static class HuespedFreezePatch
{
    public static void Postfix(PlayerPhysics __instance)
    {
        if (__instance?.body == null || !HuespedManager.Activa)
        {
            return;
        }

        try
        {
            var miJugador = __instance.myPlayer;
            var local = PlayerControl.LocalPlayer;
            if (local == null || miJugador == null)
            {
                return;
            }

            // PREDICCIÓN en el cliente del HUÉSPED que controla: mueve la copia
            // LOCAL del poseído con su propia dirección de teclado (sin pasar
            // por la red), al mismo tiempo que el poseído real se mueve en su
            // cliente. Ambos simulamos lo mismo → el controlador ve el
            // movimiento EN TIEMPO REAL (antes solo veía los snapshots de red,
            // que llegan con el retardo de RTT + interpolación). El
            // HuespedCntPredictionPatch evita que el NetTransform arrastre la
            // copia local hacia el último snapshot.
            if (HuespedManager.SoyControlador &&
                miJugador.PlayerId == HuespedManager.ObjetivoId)
            {
                var dirPred = HuespedManager.DireccionLocal;
                if (dirPred.sqrMagnitude > 0.0001f)
                {
                    dirPred = dirPred.normalized;
                }
                else
                {
                    dirPred = Vector2.zero;
                }

                try { __instance.HandleAnimation(miJugador.Data.IsDead); } catch { }
                try { __instance.SetNormalizedVelocity(dirPred); } catch { }
                try
                {
                    if (dirPred != Vector2.zero && Mathf.Abs(dirPred.x) > 0.01f)
                    {
                        __instance.FlipX = dirPred.x < 0f;
                    }
                }
                catch { }

                return;
            }

            if (miJugador.PlayerId != local.PlayerId)
            {
                return;
            }

            // POSEÍDO: mover con la dirección del Huésped por el MISMO camino
            // vanilla que usa TOU Mira (ApplyControlledMovement): se pasa la
            // dirección NORMALIZADA a SetNormalizedVelocity y el JUEGO la
            // escala con la velocidad real configurada en el lobby
            // (PlayerSpeedMod del host: 0.75, 1.75, ...). NO se impone una
            // velocidad constante propia: por eso antes el poseído iba a
            // 1.6 fija sin importar la configuración de la sala.
            if (HuespedManager.SoyObjetivo)
            {
                var dir = HuespedManager.DireccionActual;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    dir = dir.normalized;
                }
                else
                {
                    dir = Vector2.zero;
                }

                try { __instance.HandleAnimation(miJugador.Data.IsDead); } catch { }
                try { __instance.SetNormalizedVelocity(dir); } catch { }
                try
                {
                    if (dir != Vector2.zero && Mathf.Abs(dir.x) > 0.01f)
                    {
                        __instance.FlipX = dir.x < 0f;
                    }
                }
                catch { }

                return;
            }

            // HUÉSPED que controla: anclar el fantasma sobre el poseído.
            if (HuespedManager.FantasmaAnclado)
            {
                var objetivo = ResolverPoseido();
                if (objetivo != null)
                {
                    __instance.body.position = objetivo.GetTruePosition() + new Vector2(0f, 0.35f);
                    __instance.body.velocity = Vector2.zero;
                }
            }
        }
        catch
        {
            // Un tick de posesión nunca debe romper la física.
        }
    }

    private static PlayerControl? ResolverPoseido()
    {
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p != null && HuespedManager.Activa && p.PlayerId == HuespedManager.ObjetivoId)
            {
                return p;
            }
        }

        return null;
    }
}

/// <summary>
///     Durante la posesión, en el cliente del HUÉSPED que controla, se
///     desactiva el NetTransform del poseído (interpolación de snapshots):
///     sin esto, cada frame la red devolvía el cuerpo al último snapshot
///     recibido y anulaba la predicción local → el controlador veía el
///     movimiento con el retardo de red completo. Con el skip, la física
///     local (SetNormalizedVelocity con el input del controlador) es la que
///     posiciona al poseído en pantalla. Los snapshots siguen llegando por
///     OnDeserialize y corrigen pequeñas divergencias (muros, empujones);
///     al acabar la posesión el NetTransform vuelve a mandar y el cuerpo se
///     recoloca con la posición autoritativa.
/// </summary>
[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.FixedUpdate))]
public static class HuespedCntPredictionPatch
{
    public static bool Prefix(CustomNetworkTransform __instance)
    {
        if (!HuespedManager.Activa || !HuespedManager.SoyControlador)
        {
            return true;
        }

        try
        {
            var jugador = __instance.GetComponent<PlayerControl>();
            if (jugador != null && jugador.PlayerId == HuespedManager.ObjetivoId)
            {
                return false; // la física local del controlador manda
            }
        }
        catch
        {
            // Nunca romper el NetTransform por un fallo de predicción.
        }

        return true;
    }
}

[HarmonyPatch(typeof(RoleBehaviour), nameof(RoleBehaviour.TeamColor), MethodType.Getter)]
public static class HuespedTeamColorPatch
{
    public static bool Prefix(RoleBehaviour __instance, ref Color __result)
    {
        if (__instance is HuespedRole)
        {
            __result = HuespedRole.HuespedColor;
            return false;
        }

        return true;
    }
}