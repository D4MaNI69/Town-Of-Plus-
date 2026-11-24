﻿using System.Collections;
using MiraAPI.Hud;
using MiraAPI.Modifiers;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using Reactor.Utilities;
using TownOfPlus.Modifiers.Crewmate;
using TownOfPlus.Roles.Crewmate.CrewmateInvestigative;
using TownOfPlus.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TownOfPlus.Modules;

/// <summary>
///     ESTADO DEL ERMITAï¿½O ï¿½ rework TOTAL a la habilidad ESCAPE (se eliminaron
///     Marcar, Volver y la marca fija del mapa).
///
///     ESCAPE (solo en partida, nunca en reuniï¿½n):
///     <para>1. Al pulsar el botï¿½n aparece un PNG de cursor SOBRE EL RATï¿½N (70%
///     de opacidad, visible SOLO en el cliente del Ermitaï¿½o). El Ermitaï¿½o puede
///     SEGUIR MOVIï¿½NDOSE mientras el PNG estï¿½ activo (sin congelaciï¿½n: mejora
///     propia respecto al Phaser, que sï¿½ congela).</para>
///     <para>2. El botï¿½n entra en "recarga corta" de <see cref="DuracionCanalEscape"/>
///     segundos (modo efecto): es la ventana del CANAL DE TRANSPORTE.</para>
///     <para>3. Click izquierdo: si el cursor está en un sitio VÁLIDO se vuelve
///     INVISIBLE al instante (modifier Swoop, <see cref="DuracionInvisibilidad"/> s)
///     y <see cref="RetardoTeletransporte"/> s DESPUÉS se teletransporta al punto
///     elegido (patrón Escapist/Swooper: el <c>RpcSnapTo</c> lo hace el propio dueño).</para>
///     <para>ï¿½ Tras el TP o al agotarse el canal: COOLDOWN LARGO (option
///     <c>CooldownEscape</c>: 40 s por defecto, rango 30-60).</para>
///
///     VALIDACION DE POSICION = COPIA EXACTA DEL PHASER DE STARGAZER, sin
///     depender de Stargazer (ni de Reactor mas alla de lo que ya usa el mod).
///     El codigo real del Phaser es, literalmente:
///     <code>IsTargetValid(pos) =&gt; Helpers.GetRoom(pos)</code> y, dentro de
///     MiraAPI, <code>GetRoom</code> es
///     <code>ShipStatus.Instance.AllRooms.FirstOrDefault(r =&gt; r.roomArea.OverlapPoint(pos))</code>.
///     Por tanto la UNICA condicion para el Escape es que el punto del cursor
///     este DENTRO de una sala del mapa: sin paredes, sin raycasts, sin limite
///     de vision y sin limite de distancia.
///     Consecuencia (igual que el Phaser): se PUEDE saltar a cualquier sala,
///     incluso entre salas y atravesando paredes, pero NUNCA se acaba dentro de
///     una pared ni fuera del mapa, porque las paredes y el vacio no forman
///     parte de ninguna sala.
///     Si el punto NO es valido: aviso SOLO para el Ermitaño (con el logo del
///     rol) con el texto "No puedes moverte aqui" y el canal SIGUE ABIERTO para
///     reintentar (el Phaser tambien ignora el click invalido sin cerrar la mira
///     ni gastar cooldown).
/// </summary>
public static class ErmitanoManager
{
    /// <summary>
    ///     Duraciï¿½n del CANAL DE TRANSPORTE: la "recarga corta" de 5 s que el
    ///     botï¿½n Escape muestra mientras el PNG del cursor estï¿½ activo (mismo
    ///     patrï¿½n que la Preparaciï¿½n del Francotirador y el Escudo del Aislador).
    /// </summary>
    public const float DuracionCanalEscape = 5f;

    /// <summary>Opacidad del PNG del cursor sobre el ratï¿½n (70%).</summary>
    public const float OpacidadCursorEscape = 0.7f;

    /// <summary>
    ///     Duraciï¿½n de la invisibilidad tras el Escape (la que ya usa el mod para
    ///     Kamikaze/Swoop: 3 segundos).
    /// </summary>
    public const float DuracionInvisibilidad = 3f;

    /// <summary>
    ///     Retardo entre volverse INVISIBLE y moverse al punto elegido: se
    ///     activa la invisibilidad al instante y 0.1 s despues salta (misma
    ///     idea que el Phaser de Stargazer, que espera 0.5 s antes del
    ///     <c>RpcSnapTo</c>).
    /// </summary>
    public const float RetardoTeletransporte = 0.1f;

    /// <summary>Aviso EXACTO que se muestra cuando el cursor estï¿½ en un sitio invï¿½lido.</summary>
    public const string MensajeInvalido = "No puedes moverte aquï¿½";

    /// <summary>Plano Z del PNG del cursor (por encima del mapa).</summary>
    private const float ZCursor = -5f;

    /// <summary>
    ///     Margen de gracia al abrir el canal: el click que pulsa el PROPIO botï¿½n
    ///     (mismo frame/posterior) no cuenta como click de teletransporte.
    /// </summary>
    private const float GraciaInicioCanal = 0.25f;

    private static CustomActionButton? _botonEscape;
    private static GameObject? _cursorEscape;
    private static float _finCanal;
    private static float _finGracia;

    /// <summary>ï¿½Estï¿½ el canal de transporte activo (PNG del cursor en pantalla)?</summary>
    public static bool CanalEscapeActivo { get; private set; }

    /// <summary>Segundos que quedan del canal de transporte (0 si no estï¿½ activo).</summary>
    public static float SegundosCanalRestantes =>
        CanalEscapeActivo ? Mathf.Max(0f, _finCanal - Time.time) : 0f;

    /// <summary>ï¿½El jugador local es un Ermitaï¿½o vivo?</summary>
    public static bool EsLocalErmitano
    {
        get
        {
            var local = PlayerControl.LocalPlayer;
            return local != null && local.Data != null && !local.Data.IsDead &&
                   local.Data.Role is ErmitanoRole;
        }
    }

    /// <summary>Devuelve el primer Ermitaï¿½o activo de la partida (o null).</summary>
    public static ErmitanoRole? ObtenerErmitano() =>
        CustomRoleUtils.GetActiveRolesOfType<ErmitanoRole>().FirstOrDefault();

    /// <summary>Limpia el estado al empezar una partida nueva.</summary>
    public static void ReiniciarPartida()
    {
        TerminarCanal(false);
    }

    /// <summary>Limpia los estados puramente locales (al morir/salir el dueï¿½o o en reuniï¿½n).</summary>
    public static void LimpiarEstadosLocales()
    {
        TerminarCanal(false);
    }

    // -- CANAL DE TRANSPORTE (PNG del cursor sobre el raton) -------------

    /// <summary>ï¿½Estamos en un estado en el que el Escape puede existir?</summary>
    public static bool EnPartida() =>
        LobbyBehaviour.Instance == null &&
        MeetingHud.Instance == null &&
        ExileController.Instance == null &&
        Minigame.Instance == null &&
        EsLocalErmitano;

    /// <summary>
    ///     Abre el CANAL DE TRANSPORTE: crea el PNG del cursor sobre el ratï¿½n
    ///     (70% de opacidad) y deja el botï¿½n en recarga corta
    ///     (<see cref="DuracionCanalEscape"/> s) hasta que haya TP, timeout o se
    ///     cancele el intento. Lo llama el OnClick del botï¿½n Escape.
    ///     El Ermitaï¿½o puede moverse libremente durante el canal.
    /// </summary>
    public static void IniciarCanalEscape(CustomActionButton? boton)
    {
        if (CanalEscapeActivo || boton == null || !EnPartida())
        {
            return;
        }

        CanalEscapeActivo = true;
        _botonEscape = boton;
        _finCanal = Time.time + DuracionCanalEscape;
        _finGracia = Time.time + GraciaInicioCanal;

        CrearCursorEscape(PosicionCursor());
    }

    /// <summary>
    ///     Ciclo del canal de transporte: corre cada frame desde
    ///     ErmitanoMotorPatch (HudManager.Update), independientemente del estado
    ///     del botï¿½n (asï¿½ el canal nunca se queda colgado, igual que el motor del
    ///     Francotirador).
    /// </summary>
    public static void Actualizar()
    {
        if (!CanalEscapeActivo)
        {
            return;
        }

        // Seguridad: cualquier interrupciï¿½n ajena (muerte, reuniï¿½n, minijuego,
        // lobby, pï¿½rdida del rol) cierra el canal SIN consumir el cooldown largo:
        // el intento se resetea y Escape queda disponible otra vez.
        if (!EnPartida())
        {
            TerminarCanal(false);
            return;
        }

        // El PNG del cursor sigue al ratï¿½n en tiempo real.
        var mundo = PosicionCursor();
        if (_cursorEscape != null)
        {
            _cursorEscape.transform.position = new Vector3(mundo.x, mundo.y, ZCursor);
        }

        // Click izquierdo = intento de teletransporte a la posiciï¿½n del cursor.
        if (Time.time >= _finGracia && Input.GetMouseButtonDown(0))
        {
            IntentarEscapar(mundo);
            return;
        }

        // Fin de la recarga corta: se cierra el canal consumiendo el cooldown largo.
        if (Time.time >= _finCanal)
        {
            TerminarCanal(true);
        }
    }

    /// <summary>
    ///     Intenta el teletransporte a la posicion del cursor, EXACTAMENTE como
    ///     el Phaser de Stargazer: si el punto NO esta dentro de una sala, el
    ///     click se IGNORA (aviso "No puedes moverte aqui") y el canal SIGUE
    ///     ABIERTO para reintentar, sin cerrar el canal ni gastar el cooldown
    ///     largo (el Phaser hace "return" sin cerrar la mira).
    /// </summary>
    private static void IntentarEscapar(Vector2 destino)
    {
        // Copia del Phaser de Stargazer (PhaseButton.IsTargetValid):
        // click invalido = se ignora y el canal SIGUE ABIERTO (el Phaser hace
        // 'return' sin cerrar la mira ni gastar cooldown).
        if (!EsPosicionEscapeValida(destino))
        {
            AvisarEscapeInvalido(MensajeInvalido);
            return;
        }

        var local = PlayerControl.LocalPlayer;
        if (local == null)
        {
            TerminarCanal(false);
            return;
        }

        EjecutarEscape(local, destino);

        // Canal consumido con ï¿½xito: cooldown largo (option CooldownEscape).
        TerminarCanal(true);
    }

    /// <summary>
    ///     Invisibilidad + TP del Escape (patrón Escapist/Swooper de TOU Mira +
    ///     Phaser de Stargazer): PRIMERO se activa la invisibilidad (modifier Swoop
    ///     del propio Ermitaño) y <see cref="RetardoTeletransporte"/> segundos
    ///     DESPUÉS salta (<c>RpcSnapTo</c>, que replica la posición al resto).
    ///     Así el salto ocurre con el Ermitaño ya invisible.
    /// </summary>
    public static void EjecutarEscape(PlayerControl ermitano, Vector2 destino)
    {
        if (ermitano?.Data == null)
        {
            return;
        }

        // 1) Invisibilidad YA (el TP llega después).
        try
        {
            ermitano.RpcAddModifier<ErmitanoSwoopModifier>();
        }
        catch (System.Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Ermitaño] Invisibilidad del Escape falló: {ex.Message}");
        }

        // 2) 0.1 s después, el salto (ya invisible).
        Coroutines.Start(CoTeletransportarConRetardo(ermitano, destino, RetardoTeletransporte));
    }

    /// <summary>
    ///     Espera <paramref name="espera"/> segundos con el Ermitaño YA invisible y
    ///     entonces lo mueve al destino. Si para entonces ha muerto o ya no existe,
    ///     no salta.
    /// </summary>
    private static IEnumerator CoTeletransportarConRetardo(
        PlayerControl ermitano, Vector2 destino, float espera)
    {
        yield return new WaitForSeconds(espera);

        if (ermitano?.Data == null || ermitano.Data.IsDead)
        {
            yield break;
        }

        try
        {
            ermitano.NetTransform.RpcSnapTo(destino);
        }
        catch (System.Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Ermitaño] Teletransporte del Escape falló: {ex.Message}");
        }
    }

    /// <summary>
    ///     Cierra el canal de transporte: quita el PNG del cursor sobre el raton y
    ///     deja el boton en cooldown largo
    ///     (<paramref name="consumirCooldown"/> = true) o DISPONIBLE al instante
    ///     (<paramref name="consumirCooldown"/> = false: click invï¿½lido o
    ///     interrupciï¿½n ajena).
    /// </summary>
    private static void TerminarCanal(bool consumirCooldown)
    {
        var estabaActivo = CanalEscapeActivo;
        CanalEscapeActivo = false;
        _finCanal = 0f;
        _finGracia = 0f;

        DestruirCursorEscape();

        if (_botonEscape != null)
        {
            try
            {
                // Fin del modo efecto (recarga corta del canal). Al fallar el
                // Escape el boton queda DISPONIBLE al instante (sin cooldown
                // largo: solo se consume al teletransportarse o al agotarse el canal).
                _botonEscape.EffectActive = false;
                _botonEscape.Timer = consumirCooldown ? _botonEscape.Cooldown : 0f;
            }
            catch
            {
                // Si el botï¿½n ya no existe, el juego lo repone solo.
            }

            _botonEscape = null;
        }

        if (estabaActivo)
        {
            TownOfPlusPlugin.Instance?.Log.LogInfo(
                consumirCooldown
                    ? "[Ermitaï¿½o] Canal de Escape cerrado con cooldown largo."
                    : "[Ermitaï¿½o] Intento de Escape reseteado (sin cooldown).");
        }
    }

    // -- PNG DEL CURSOR (solo cliente del Ermitaï¿½o) --------------------------

    /// <summary>Posiciï¿½n del mundo bajo el ratï¿½n (ScreenToWorldPoint, idea del Phaser).</summary>
    private static Vector2 PosicionCursor()
    {
        try
        {
            var cam = Camera.main;
            var local = PlayerControl.LocalPlayer;
            if (cam == null)
            {
                return local != null ? local.GetTruePosition() : Vector2.zero;
            }

            var mundo = cam.ScreenToWorldPoint(Input.mousePosition);
            return new Vector2(mundo.x, mundo.y);
        }
        catch
        {
            return PlayerControl.LocalPlayer != null
                ? PlayerControl.LocalPlayer.GetTruePosition()
                : Vector2.zero;
        }
    }

    /// <summary>
    ///     Crea el PNG del cursor sobre el ratï¿½n al 70% de opacidad. Se dibuja como
    ///     sprite del mundo (capa por defecto + sortingOrder alto, igual que las
    ///     burbujas del Aislador): aunque la IDEA del Phaser de Stargazer se copia
    ///     (seguir el ratï¿½n con ScreenToWorldPoint y validar la posiciï¿½n), aquï¿½ NO
    ///     se cambia de capa para no depender de la cï¿½mara de UI.
    /// </summary>
    private static void CrearCursorEscape(Vector2 posicion)
    {
        DestruirCursorEscape();

        try
        {
            var spr = SpritesErmitano.CursorEscapeAsset.LoadAsset();
            if (spr == null)
            {
                return;
            }

            _cursorEscape = new GameObject("ErmitanoCursorEscape");
            Object.DontDestroyOnLoad(_cursorEscape);
            _cursorEscape.transform.position = new Vector3(posicion.x, posicion.y, ZCursor);
            _cursorEscape.transform.localScale = Vector3.one;

            var renderer = _cursorEscape.AddComponent<SpriteRenderer>();
            renderer.sprite = spr;
            renderer.sortingOrder = 500;

            // 70% de opacidad: diseï¿½o del rol.
            renderer.color = new Color(1f, 1f, 1f, OpacidadCursorEscape);
        }
        catch (System.Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Ermitaï¿½o] No se pudo crear el cursor del Escape: {ex.Message}");
        }
    }

    private static void DestruirCursorEscape()
    {
        if (_cursorEscape == null)
        {
            return;
        }

        try
        {
            Object.Destroy(_cursorEscape);
        }
        catch
        {
            // Ya destruido.
        }

        _cursorEscape = null;
    }

    // -- VALIDACION DE POSICION (copia del Phaser de Stargazer) ---------------

    /// <summary>
    ///     Copia EXACTA del Phaser de Stargazer SIN depender de Stargazer
    ///     (PhaseButton.IsTargetValid usa Helpers.GetRoom de MiraAPI):
    ///     la UNICA condicion es que el punto este DENTRO de una sala real.
    ///     Sin paredes, sin raycasts, sin distancia, sin vision.
    /// </summary>
    public static bool EsPosicionEscapeValida(Vector2 posicion)
    {
        try
        {
            return Helpers.GetRoom((Vector3)posicion) != null;
        }
        catch (System.Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(ex.Message);
            return false;
        }
    }

    /// <summary>
    ///     Aviso del Escape fallido: aparece SOLO para el Ermitaï¿½o (el canal de
    ///     transporte solo existe en su cliente) con el logo del rol, el texto EXACTO
    ///     <see cref="MensajeInvalido"/> y el sonido de denegado de TOU Mira. El
    ///     motivo tï¿½cnico se deja en el log, no en pantalla.
    /// </summary>
    public static void AvisarEscapeInvalido(string motivo)
    {
        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Ermitaï¿½o] Escape rechazado: {(string.IsNullOrEmpty(motivo) ? MensajeInvalido : motivo)}.");

        try
        {
            Helpers.CreateAndShowNotification(
                $"<b>{MensajeInvalido}</b>\nElige un punto dentro del mapa.",
                ErmitanoRole.ErmitanoColor, new Vector3(0f, 1f, -20f),
                spr: SpritesErmitano.IconoRolAsset.LoadAsset());
        }
        catch
        {
            // Sin notificaciï¿½n no pasa nada.
        }

        try
        {
            AudioTop.Sonar(TownOfUs.Assets.TouAudio.DenySound);
        }
        catch
        {
            // Sin sonido no pasa nada.
        }
    }
}
