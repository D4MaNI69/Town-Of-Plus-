using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using Reactor.Utilities;
using TownOfPlus.Options.General;
using TownOfUs.Modifiers.Game.Universal;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace TownOfPlus.Modules;

public static class SabotajesMejoradosManager
{
    private static GameObject? _overlayLuces;
    private static Image? _imagenLuces;
    private static IEnumerator? _fadeLucesRoutine;
    private static bool _lucesActivasPrevias;

    private static bool _reactorActivoPrevio;
    private static IEnumerator? _temblorReactorRoutine;
    private static IEnumerator? _cambioVelocidadReactorRoutine;
    private static float _multiplicadorReactor = 1f;

    private static bool _o2ActivoPrevio;

    /// <summary>Jugadores a los que ESTE manager les dio Drunk por O2 (solo no-impostores).</summary>
    private static readonly HashSet<byte> _drunkO2Dados = new();

    // ---------------------------------------------------------------------
    // Temblores del Reactor
    // ---------------------------------------------------------------------

    /// <summary>
    ///     Amplitud del temblor de cámara con Reactor activo.
    ///     0.042 = 0.035 base + 20% de intensidad extra.
    /// </summary>
    private const float AmplitudTemblorCamara = 0.042f;

    /// <summary>
    ///     Amplitud del temblor de la TAREA abierta (Minigame.Instance). Es la
    ///     MISMA para TODAS las tareas (mismo porcentaje/intensidad); lo único
    ///     que cambia es el PATRÓN de movimiento según el tipo de tarea, para
    ///     que el tripulante no pueda saber en qué tarea está.
    /// </summary>
    private const float AmplitudTemblorTarea = 0.02f;

    /// <summary>Offset de cámara aplicado en el frame anterior (para no derivar).</summary>
    private static Vector3 _offsetCamara;

    /// <summary>
    ///     Tarea (minigame) que se está sacudiendo AHORA mismo. Solo se sacude
    ///     la tarea ABIERTA por el jugador (<see cref="Minigame.Instance" />),
    ///     nunca minigames del mapa que no estén en uso.
    /// </summary>
    private static Minigame? _tareaSacudida;

    /// <summary>
    ///     Offset aplicado a la tarea en el frame anterior. Se guarda SOLO el
    ///     offset (nunca la posición "original"), porque el juego re-parenta y
    ///     recoloca los minigames al abrirlos: cachear la posición dejaba la
    ///     interfaz clavada en un sitio equivocado (fuera de pantalla) y el
    ///     jugador quedaba atrapado en la tarea.
    /// </summary>
    private static Vector3 _offsetTarea;

    public static float MultiplicadorVelocidadReactor => _multiplicadorReactor;
    public static bool O2Activo => _o2ActivoPrevio;

    /// <summary>
    ///     PAUSA por reunión/lobby: limpia lo VISUAL y lo TEMPORAL (overlay de
    ///     luces, coroutines de temblor/velocidad, Drunk de O2 dado por este
    ///     manager) pero MANTIENE los flags previos para que, al salir de la
    ///     reunión, si el sabotaje sigue activo se reanude solo.
    /// </summary>
    public static void PausarEnReunion()
    {
        if (_fadeLucesRoutine != null) { Coroutines.Stop(_fadeLucesRoutine); _fadeLucesRoutine = null; }
        if (_temblorReactorRoutine != null) { Coroutines.Stop(_temblorReactorRoutine); _temblorReactorRoutine = null; }
        if (_cambioVelocidadReactorRoutine != null) { Coroutines.Stop(_cambioVelocidadReactorRoutine); _cambioVelocidadReactorRoutine = null; }

        _multiplicadorReactor = 1f;
        RestaurarCamara();
        RestaurarTareas();
        DestruirOverlayLuces();
        LimpiarDrunkO2();
        // Resetear flags para que al salir de la reunión los efectos se
        // re-evalúen y se reanuden solos SOLO si el sabotaje sigue activo.
        // (En vanilla la reunión limpia reactor/O2, así que normalmente
        // quedarán en false; luces puede persistir y se recreará el overlay.)
        _lucesActivasPrevias = false;
        _reactorActivoPrevio = false;
        _o2ActivoPrevio = false;
    }

    public static void ResetearTodo()
    {
        _lucesActivasPrevias = false;
        _reactorActivoPrevio = false;
        _o2ActivoPrevio = false;
        _multiplicadorReactor = 1f;

        if (_fadeLucesRoutine != null) { Coroutines.Stop(_fadeLucesRoutine); _fadeLucesRoutine = null; }
        if (_temblorReactorRoutine != null) { Coroutines.Stop(_temblorReactorRoutine); _temblorReactorRoutine = null; }
        if (_cambioVelocidadReactorRoutine != null) { Coroutines.Stop(_cambioVelocidadReactorRoutine); _cambioVelocidadReactorRoutine = null; }

        DestruirOverlayLuces();
        RestaurarCamara();
        RestaurarTareas();
        // Limpieza TOTAL (no solo tracking): nadie debe empezar/terminar
        // la partida con Drunk residual de un O2 anterior.
        LimpiarDrunkTotal();
    }

    /// <summary>
    ///     Limpieza TOTAL de Drunk (fin de partida / lobby): quita el modifier
    ///     a TODOS los jugadores tengan o no tracking, porque al terminar la
    ///     partida nadie debe quedarse con el efecto residual.
    /// </summary>
    public static void LimpiarDrunkTotal()
    {
        _drunkO2Dados.Clear();
        try
        {
            foreach (var player in PlayerControl.AllPlayerControls.ToArray())
            {
                if (player == null) continue;
                var comp = player.GetModifierComponent();
                if (comp == null) continue;
                foreach (var mod in comp.GetModifiers<DrunkModifier>().ToArray())
                {
                    player.RemoveModifier(mod);
                }
            }
        }
        catch { }
    }

    /// <summary>
    ///     Limpieza inmediata del Drunk del O2 (usada por los parches de
    ///     reparación del sabotaje para no esperar al siguiente Update).
    /// </summary>
    public static void ForzarLimpiarDrunkO2() => LimpiarDrunkO2();

    public static void Actualizar()
    {
        if (AmongUsClient.Instance == null || ShipStatus.Instance == null)
        {
            return;
        }

        // FUERA DE PARTIDA (lobby / intro / fin): limpieza TOTAL y no aplicar nada.
        // Esto evita el bug de Drunk a inicio de partida por lecturas de Countdown
        // antes de que el juego este en Started.
        try
        {
            var estado = AmongUsClient.Instance.GameState;
            if (estado != InnerNet.InnerNetClient.GameStates.Started ||
                LobbyBehaviour.Instance != null || !ShipStatus.Instance.TryCast<ShipStatus>())
            {
                ResetearTodo();
                return;
            }
        }
        catch { }

        // REUNIÓN: pausar todo MOMENTÁNEAMENTE (limpia visual y
        // temporal pero MANTIENE flags para reanudar si el sabotaje sigue).
        // Así al terminar la reunión los efectos vuelven solos si corresponde.
        if (MeetingHud.Instance != null || ExileController.Instance != null || LobbyBehaviour.Instance != null)
        {
            PausarEnReunion();
            return;
        }

        var local = PlayerControl.LocalPlayer;
        if (local == null || local.Data == null) return;

        ActualizarLuces(local);
        ActualizarReactor(local);
        ActualizarO2();
    }

    private static void ActualizarLuces(PlayerControl local)
    {
        var opt = OptionGroupSingleton<VisibilidadAfterlifeOptions>.Instance?.MejorSabotajeLuces.Value ?? false;
        var lucesActivas = opt && HaySabotajeLuces();

        if (lucesActivas != _lucesActivasPrevias)
        {
            _lucesActivasPrevias = lucesActivas;
            if (lucesActivas)
            {
                if (!local.Data.Role.IsImpostor)
                {
                    CrearOverlayLuces();
                    if (_fadeLucesRoutine != null) Coroutines.Stop(_fadeLucesRoutine);
                    _fadeLucesRoutine = CoFadeLuces(7f, 0.80f);
                    Coroutines.Start(_fadeLucesRoutine);
                }
            }
            else if (_imagenLuces != null && _overlayLuces != null)
            {
                // Al reparar: fade-out de 5s hasta transparente y luego destruir.
                if (_fadeLucesRoutine != null) Coroutines.Stop(_fadeLucesRoutine);
                _fadeLucesRoutine = CoFadeOutLuces(5f);
                Coroutines.Start(_fadeLucesRoutine);
            }
            else
            {
                DestruirOverlayLuces();
            }
        }
    }

    private static bool HaySabotajeLuces()
    {
        var ship = ShipStatus.Instance;
        if (ship == null || ship.Systems == null) return false;
        if (ship.Systems.TryGetValue(SystemTypes.Electrical, out var system))
        {
            if (system.Cast<SwitchSystem>() is { } switches)
                return switches.ActualSwitches != switches.ExpectedSwitches;
        }
        return false;
    }

    private static void CrearOverlayLuces()
    {
        DestruirOverlayLuces();
        try
        {
            var go = new GameObject("TOP_OverlayLucesMejoradas");
            Object.DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            _overlayLuces = go;
            _imagenLuces = image;
        }
        catch { _overlayLuces = null; _imagenLuces = null; }
    }

    private static void DestruirOverlayLuces()
    {
        if (_fadeLucesRoutine != null) { Coroutines.Stop(_fadeLucesRoutine); _fadeLucesRoutine = null; }
        if (_overlayLuces != null)
        {
            try { Object.Destroy(_overlayLuces); } catch { }
            _overlayLuces = null;
            _imagenLuces = null;
        }
    }

    /// <summary>
    ///     Fade-OUT del overlay de luces en `duracion` segundos (al reparar):
    ///     baja desde el alpha actual hasta 0 y luego destruye el overlay.
    /// </summary>
    private static IEnumerator CoFadeOutLuces(float duracion)
    {
        var inicio = _imagenLuces != null ? _imagenLuces.color.a : 0f;
        var elapsed = 0f;
        while (elapsed < duracion && _imagenLuces != null)
        {
            elapsed += Time.deltaTime;
            var a = Mathf.Lerp(inicio, 0f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duracion)));
            if (_imagenLuces != null) _imagenLuces.color = new Color(0f, 0f, 0f, a);
            yield return null;
        }
        DestruirOverlayLuces();
        _fadeLucesRoutine = null;
    }

    private static IEnumerator CoFadeLuces(float duracion, float alphaObjetivo)
    {
        var elapsed = 0f;
        while (elapsed < duracion && _imagenLuces != null)
        {
            elapsed += Time.deltaTime;
            var a = Mathf.Lerp(0f, alphaObjetivo, elapsed / duracion);
            if (_imagenLuces != null) _imagenLuces.color = new Color(0f, 0f, 0f, a);
            yield return null;
        }
        if (_imagenLuces != null) _imagenLuces.color = new Color(0f, 0f, 0f, alphaObjetivo);
        _fadeLucesRoutine = null;
    }
    // Reactor & O2
    private static void ActualizarReactor(PlayerControl local)
    {
        var opt = OptionGroupSingleton<VisibilidadAfterlifeOptions>.Instance?.MejorSabotajeReactor.Value ?? false;
        var reactorActivo = opt && HaySabotajeReactor();

        if (reactorActivo != _reactorActivoPrevio)
        {
            _reactorActivoPrevio = reactorActivo;
            if (reactorActivo)
            {
                if (!local.Data.Role.IsImpostor)
                {
                    if (_temblorReactorRoutine != null) Coroutines.Stop(_temblorReactorRoutine);
                    _temblorReactorRoutine = CoTemblorReactor();
                    Coroutines.Start(_temblorReactorRoutine);
                }

                if (_cambioVelocidadReactorRoutine != null) Coroutines.Stop(_cambioVelocidadReactorRoutine);
                _cambioVelocidadReactorRoutine = CoVariacionVelocidadReactor();
                Coroutines.Start(_cambioVelocidadReactorRoutine);
            }
            else
            {
                if (_temblorReactorRoutine != null) { Coroutines.Stop(_temblorReactorRoutine); _temblorReactorRoutine = null; }
                if (_cambioVelocidadReactorRoutine != null) { Coroutines.Stop(_cambioVelocidadReactorRoutine); _cambioVelocidadReactorRoutine = null; }
                _multiplicadorReactor = 1f;
                // FIX: al parar la coroutine su cola (restaurar) NO se ejecuta,
                // así que la cámara y la tarea se quedaban con el último offset
                // pegado. Se restauran aquí de forma explícita.
                RestaurarCamara();
                RestaurarTareas();
            }
        }
    }

    private static bool HaySabotajeReactor()
    {
        var ship = ShipStatus.Instance;
        if (ship == null || ship.Systems == null) return false;
        if (ship.Systems.TryGetValue(SystemTypes.Reactor, out var sysR) && sysR.Cast<ICriticalSabotage>() is { IsActive: true }) return true;
        if (ship.Systems.TryGetValue(SystemTypes.Laboratory, out var sysL) && sysL.Cast<ICriticalSabotage>() is { IsActive: true }) return true;
        return false;
    }

    private static IEnumerator CoTemblorReactor()
    {
        // Temblor INDIVIDUAL por cliente: se ejecuta solo en el cliente local
        // (cada jugador sacude su propia cámara con su PlayerCam).
        // Intensidad ~35% del sacudón de una explosión (+20% respecto a la
        // versión anterior), repetida en bucle mientras el sabotaje siga.
        // Además, la TAREA que el jugador tenga ABIERTA (Minigame.Instance) se
        // sacude con la MISMA intensidad para todas pero con un patrón de
        // movimiento distinto según el tipo de tarea.
        while (_reactorActivoPrevio)
        {
            SacudirCamara();
            SacudirTareas();
            yield return null;
        }

        RestaurarCamara();
        RestaurarTareas();
        _temblorReactorRoutine = null;
    }

    /// <summary>
    ///     Aplica el offset de temblor a la cámara local corrigiendo el offset
    ///     del frame anterior. Sin esta corrección el offset se ACUMULABA y la
    ///     cámara se iba de madre cada frame.
    /// </summary>
    private static void SacudirCamara()
    {
        try
        {
            var cam = Camera.main;
            if (cam == null) return;

            cam.transform.position -= _offsetCamara;
            _offsetCamara = new Vector3(
                Random.Range(-AmplitudTemblorCamara, AmplitudTemblorCamara),
                Random.Range(-AmplitudTemblorCamara, AmplitudTemblorCamara),
                0f);
            cam.transform.position += _offsetCamara;
        }
        catch { }
    }

    private static void RestaurarCamara()
    {
        try
        {
            var cam = Camera.main;
            if (cam != null) cam.transform.position -= _offsetCamara;
        }
        catch { }
        _offsetCamara = Vector3.zero;
    }

    /// <summary>
    ///     Válvula de seguridad usada por los parches de Minigame.Begin: el
    ///     juego acaba de recolocar la tarea, así que se Olvida el estado SIN
    ///     mover nada (así el "deshacer offset" nunca resta de una posición que
    ///     no hayamos escrito nosotros).
    /// </summary>
    public static void OlvidarTemblorTarea()
    {
        _tareaSacudida = null;
        _offsetTarea = Vector3.zero;
    }

    /// <summary>
    ///     Válvula de seguridad usada por los parches de Minigame.Close: deshace
    ///     el último offset aplicado para devolver la tarea a su sitio exacto.
    /// </summary>
    public static void RestaurarTemblorTarea() => RestaurarTareas();

    /// <summary>
    ///     Sacude la TAREA que el jugador tiene ABIERTA con la MISMA amplitud
    ///     para todas (mismo porcentaje de temblor) pero con un PATRÓN de
    ///     movimiento distinto según el TaskType, de modo que el tripulante no
    ///     pueda deducir en qué tarea se encuentra.
    ///     <para>
    ///         Se sacude únicamente <see cref="Minigame.Instance" /> (la tarea
    ///         en uso) y SIEMPRE se deshace el offset del frame anterior antes
    ///         de aplicar el nuevo: el juego sigue siendo el único dueño de la
    ///         posición del minigame, así que es imposible dejarlo atrapado /
    ///         fuera de pantalla o romper el padre al que está enganchado.
    ///     </para>
    /// </summary>
    private static void SacudirTareas()
    {
        try
        {
            Minigame? actual;
            try { actual = Minigame.Instance; }
            catch { actual = null; }

            // Cambió la tarea (se abrió otra o se cerró): devolver la anterior
            // a su sitio ANTES de tocar la nueva.
            if (!ReferenceEquals(actual, _tareaSacudida)) RestaurarTareas();

            if (actual == null) return;

            var tr = actual.transform;
            if (tr == null) return;

            // 1) Deshacer el offset del frame anterior => posición REAL del
            //    juego (aunque el juego la haya cambiado, manda el juego).
            tr.localPosition -= _offsetTarea;

            // 2) Aplicar un offset nuevo (siempre se recalcula: no acumula).
            try { _offsetTarea = PatronTemblor(actual.TaskType, Time.time); }
            catch { _offsetTarea = Vector3.zero; }
            tr.localPosition += _offsetTarea;

            _tareaSacudida = actual;
        }
        catch { }
    }

    /// <summary>
    ///     Devuelve el desplazamiento del frame para el patrón del tipo de
    ///     tarea. TODOS los patrones usan la MISMA amplitud
    ///     (<see cref="AmplitudTemblorTarea"/>); solo cambia la FORMA.
    /// </summary>
    private static Vector3 PatronTemblor(TaskTypes taskType, float t)
    {
        var patron = (int)taskType % 6;
        return patron switch
        {
            0 => new Vector3(Mathf.Sin(t * 6f) * AmplitudTemblorTarea, 0f), // horizontal
            1 => new Vector3(0f, Mathf.Sin(t * 6f) * AmplitudTemblorTarea), // vertical
            2 => new Vector3( // circular
                Mathf.Cos(t * 6f) * AmplitudTemblorTarea,
                Mathf.Sin(t * 6f) * AmplitudTemblorTarea),
            3 => new Vector3( // diagonal
                Mathf.Sin(t * 4.5f) * AmplitudTemblorTarea,
                Mathf.Sin(t * 4.5f) * AmplitudTemblorTarea),
            4 => new Vector3( // zigzag (cuadrada)
                (Mathf.Sin(t * 9f) >= 0f ? 1f : -1f) * AmplitudTemblorTarea, 0f),
            _ => new Vector3( // aleatorio puro
                Random.Range(-AmplitudTemblorTarea, AmplitudTemblorTarea),
                Random.Range(-AmplitudTemblorTarea, AmplitudTemblorTarea))
        };
    }

    /// <summary>
    ///     Devuelve la tarea sacudida a su posición original (deshace el último
    ///     offset aplicado) y olvida su estado.
    /// </summary>
    private static void RestaurarTareas()
    {
        try
        {
            if (_tareaSacudida != null)
            {
                try { _tareaSacudida.transform.localPosition -= _offsetTarea; } catch { }
            }
        }
        catch { }
        _tareaSacudida = null;
        _offsetTarea = Vector3.zero;
    }

    /// <summary>
    ///     Ciclo de velocidad del sabotage de Reactor: cada 1 SEGUNDO se sortea
    ///     un único cambio para TODOS los jugadores (respetando el prefijo de
    ///     velocidad de la sala):
    ///     · -40% de velocidad  → 50% de probabilidad (multiplicador 0.60)
    ///     · Velocidad normal   → 30% de probabilidad (multiplicador 1.00)
    ///     · +20% de velocidad  → 20% de probabilidad (multiplicador 1.20)
    ///     Al terminar el sabotaje el multiplicador vuelve a 1 (velocidad original).
    /// </summary>
    private static IEnumerator CoVariacionVelocidadReactor()
    {
        while (_reactorActivoPrevio)
        {
            var dado = Random.value;
            if (dado < 0.50f)
            {
                _multiplicadorReactor = 0.60f; // -40% de velocidad
            }
            else if (dado < 0.80f)
            {
                _multiplicadorReactor = 1.00f; // Velocidad normal
            }
            else
            {
                _multiplicadorReactor = 1.20f; // +20% de velocidad
            }

            yield return new WaitForSeconds(1f);
        }

        // Sabotage terminado: TODOS vuelven a su velocidad original.
        _multiplicadorReactor = 1f;
        _cambioVelocidadReactorRoutine = null;
    }

    private static void ActualizarO2()
    {
        var opt = OptionGroupSingleton<VisibilidadAfterlifeOptions>.Instance?.MejorSabotajeO2.Value ?? false;
        var o2Activo = opt && HaySabotajeO2();

        if (o2Activo)
        {
            _o2ActivoPrevio = true;
            // Reanudar tras reunión / reaplicar si se perdió: el Drunk debe
            // estar SIEMPRE presente mientras el sabotaje siga activo.
            AplicarDrunkO2();
        }
        else
        {
            // FIX: no depender SOLO del cambio de flag. Si el sabotaje ya no
            // está activo (se reparó, expiró, terminó la partida, etc.) el
            // Drunk se quita SIEMPRE, frame a frame, sin condiciones.
            _o2ActivoPrevio = false;
            LimpiarDrunkO2();
        }
    }

    private static bool HaySabotajeO2()
    {
        var ship = ShipStatus.Instance;
        if (ship == null || ship.Systems == null) return false;
        if (ship.Systems.TryGetValue(SystemTypes.LifeSupp, out var sysO2))
        {
            // IsActive es la señal oficial (Countdown baja solo durante O2).
            // Se exige ADEMÁS Countdown en rango de sabotaje real para evitar
            // falsos positivos al inicio de partida (Countdown != 10000 por
            // restos de lecturas/il2cpp antes de que el juego esté en Started).
            if (sysO2.Cast<LifeSuppSystemType>() is { } lifeSupp)
            {
                try
                {
                    if (!lifeSupp.IsActive) return false;
                    var cd = lifeSupp.Countdown;
                    return cd > 0f && cd < 10000f;
                }
                catch { return false; }
            }
        }
        return false;
    }

    private static bool EsImpostor(PlayerControl? player)
    {
        try { return player != null && player.Data != null && player.Data.Role.IsImpostor; }
        catch { return false; }
    }

    private static void AplicarDrunkO2()
    {
        // Solo al LOCAL no-impostor: AddModifier es local (MiraAPI no lo
        // sincroniza), así que cada cliente se lo aplica a sí mismo y el
        // host NO puede repartirlo por los demás.
        var local = PlayerControl.LocalPlayer;
        try
        {
            if (local == null || EsImpostor(local)) return;
            if (!local.HasModifier<DrunkModifier>())
            {
                local.AddModifier<DrunkModifier>();
            }
            _drunkO2Dados.Add(local.PlayerId);
        }
        catch { }
    }

    /// <summary>
    ///     Elimina el Drunk dado por este manager. Se recorren los
    ///     modificadores REALES del jugador (ModifierComponent.ActiveModifiers)
    ///     en vez de llamar a RemoveModifier&lt;T&gt;(), porque ese overload lanza
    ///     InvalidOperationException si hay más de una instancia y el `catch`
    ///     se tragaba la excepción dejando el Drunk pegado para siempre.
    /// </summary>
    private static void LimpiarDrunkO2()
    {
        if (_drunkO2Dados.Count == 0) return;

        foreach (var id in _drunkO2Dados)
        {
            try
            {
                var player = PlayerControl.AllPlayerControls.ToArray()
                    .FirstOrDefault(p => p != null && p.PlayerId == id);
                if (player == null) continue;

                var comp = player.GetModifierComponent();
                if (comp == null) continue;

                // Copia materializada: RemoveModifier encola en _toRemove y
                // el componente lo procesa en su FixedUpdate.
                foreach (var mod in comp.GetModifiers<DrunkModifier>().ToArray())
                {
                    player.RemoveModifier(mod);
                }
            }
            catch { }
        }
        _drunkO2Dados.Clear();
    }
}

[HarmonyPatch(typeof(LogicOptions), nameof(LogicOptions.GetPlayerSpeedMod))]
public static class SabotajesSpeedPatch
{
    public static void Postfix(PlayerControl pc, ref float __result)
    {
        if (SabotajesMejoradosManager.MultiplicadorVelocidadReactor != 1f)
            __result *= SabotajesMejoradosManager.MultiplicadorVelocidadReactor;

        if (SabotajesMejoradosManager.O2Activo)
            __result *= 0.80f;
    }
}
