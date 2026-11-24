using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MiraAPI.GameOptions;
using MiraAPI.Networking;
using Reactor.Utilities;
using TownOfPlus.Networking;
using TownOfPlus.Options.Impostor.ImpostorKilling;
using TownOfPlus.Roles.Impostor.ImpostorKilling;
using TownOfPlus.Utilities;
using TownOfUs.Networking;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace TownOfPlus.Modules;

/// <summary>Fases del ciclo del rifle del Francotirador.</summary>
public enum FrancotiradorPhase
{
    /// <summary>Esperando a que el rifle se recargue.</summary>
    Recargando,

    /// <summary>Bala lista: puede presionar "Recargar".</summary>
    ListoParaRecargar,

    /// <summary>Preparando el disparo (quieto, 3 segundos).</summary>
    Preparando,

    /// <summary>Rifle armado: puede presionar "Disparar".</summary>
    Armado,

    /// <summary>Zoom out activo: eligiendo objetivo.</summary>
    Apuntando,
}

/// <summary>
///     Máquina de estados del Francotirador (solo cliente local): recarga,
///     preparación con movimiento bloqueado, zoom out con visión libre,
///     puntos rojos sobre los objetivos, nubes inmersivas y disparo remoto.
/// </summary>
public static class FrancotiradorManager
{
    /// <summary>Segundos fijos de preparación (diseño del rol).</summary>
    public const float PreparandoSegundos = 3f;

    private static readonly Dictionary<byte, (PlayerControl Jugador, RectTransform Mira)> Miras = new();
    private static readonly List<SpriteRenderer> SombrasApagadas = new();
    private static GameObject? _canvasMiras;
    private static GameObject? _nubesRoot;
    private static Camera? _cam;
    private static float _zoomOriginal = -1f;
    private static bool _cicloIniciado;
    private static AudioSource? _sonidoLoop;
    private static bool _sombraGlobalApagada;

    /// <summary>Fase actual del rifle local.</summary>
    public static FrancotiradorPhase Fase { get; private set; } = FrancotiradorPhase.Recargando;

    /// <summary>Instante (Time.time) en que termina la fase actual.</summary>
    public static float FaseFin { get; private set; }

    /// <summary>¿El jugador local es un Francotirador vivo?</summary>
    public static bool EsLocalFrancotirador
    {
        get
        {
            var local = PlayerControl.LocalPlayer;
            return local != null && local.Data != null && !local.Data.IsDead &&
                   local.Data.Role is FrancotiradorRole;
        }
    }

    /// <summary>¿Está el Francotirador local en zoom out?</summary>
    public static bool EstaApuntando => EsLocalFrancotirador && Fase == FrancotiradorPhase.Apuntando;

    /// <summary>Segundos restantes de la fase actual.</summary>
    public static float SegundosRestantes => Mathf.Max(0f, FaseFin - Time.time);

    /// <summary>
    ///     Ciclo principal del rifle; corre cada frame desde FrancotiradorMotorPatch
    ///     (HudManager.Update), independientemente del estado del botón.
    /// </summary>
    public static void Actualizar()
    {
        // Primer arranque: arranca la primera recarga al empezar la partida.
        if (!_cicloIniciado && EsLocalFrancotirador)
        {
            _cicloIniciado = true;
            ReiniciarCiclo();
            return;
        }

        if (!EsLocalFrancotirador)
        {
            if (Fase != FrancotiradorPhase.Recargando)
            {
                ReiniciarCiclo();
            }

            return;
        }

        // Seguridad: reunión o minijuego cancela preparación y apuntado.
        if ((MeetingHud.Instance != null || Minigame.Instance != null) &&
            (Fase == FrancotiradorPhase.Apuntando || Fase == FrancotiradorPhase.Preparando))
        {
            CancelarApuntado(true);
            return;
        }

        // Red de seguridad: si la cámara desapareció (cambio de escena), salir ya.
        if (Fase == FrancotiradorPhase.Apuntando && _cam == null)
        {
            CancelarApuntado(true);
            return;
        }

        // El Francotirador no usa el botón vanilla de habilidad/vent: ocultarlo siempre.
        var hud = HudManager.Instance;
        if (hud != null && hud.AbilityButton != null && hud.AbilityButton.gameObject.activeSelf)
        {
            hud.AbilityButton.gameObject.SetActive(false);
        }

        // Congelación REAL: sin input y con velocidad a cero cada frame
        // (así no se desliza ni atraviesa nada durante Preparando/Apuntando).
        if (Fase is FrancotiradorPhase.Preparando or FrancotiradorPhase.Apuntando)
        {
            Congelar();
        }

        switch (Fase)
        {
            case FrancotiradorPhase.Recargando when Time.time >= FaseFin:
                Fase = FrancotiradorPhase.ListoParaRecargar;
                break;

            case FrancotiradorPhase.Preparando when Time.time >= FaseFin:
                EntrarArmado();
                break;

            case FrancotiradorPhase.Apuntando:
                GestionarApuntado();
                break;
        }
    }

    /// <summary>Reinicia al estado inicial (recarga completa).</summary>
    public static void ReiniciarCiclo()
    {
        CancelarApuntadoInterno();
        Fase = FrancotiradorPhase.Recargando;
        FaseFin = Time.time + OptionGroupSingleton<FrancotiradorOptions>.Instance.TiempoRecarga.Value;
        SetMovimiento(true);
    }

    /// <summary>Click en "Recargar": pasa a Preparando (quieto 3 s).</summary>
    public static void IniciarRecarga()
    {
        if (!EsLocalFrancotirador || Fase != FrancotiradorPhase.ListoParaRecargar)
        {
            return;
        }

        Fase = FrancotiradorPhase.Preparando;
        FaseFin = Time.time + PreparandoSegundos;
        SetMovimiento(false);
        ReproducirPreparando();
    }

    /// <summary>Sonido de preparación (Resources/Sound/Preparando.wav, solo para él).</summary>
    private static void ReproducirPreparando()
    {
        try
        {
            if (SoundManager.Instance == null)
            {
                return;
            }

            var clip = SonidosTop.Preparando.LoadAsset();
            if (clip != null)
            {
                AudioTop.Sonar(clip, false, 0.8f);
            }
        }
        catch
        {
            // Sin sonido configurado: no pasa nada.
        }
    }

    /// <summary>La preparación terminó: rifle armado ("Disparar").</summary>
    private static void EntrarArmado()
    {
        Fase = FrancotiradorPhase.Armado;
        SetMovimiento(true);
    }

    /// <summary>Click en "Disparar": entra al zoom out.</summary>
    public static void IniciarApuntado()
    {
        if (!EsLocalFrancotirador || Fase != FrancotiradorPhase.Armado)
        {
            return;
        }

        _cam = Camera.main;
        if (_cam == null)
        {
            return;
        }

        if (_zoomOriginal < 0f)
        {
            _zoomOriginal = _cam.orthographicSize;
        }

        Fase = FrancotiradorPhase.Apuntando;
        var duracion = OptionGroupSingleton<FrancotiradorOptions>.Instance.DuracionZoom.Value;
        FaseFin = Time.time + duracion;
        Congelar();
        OcultarHud();
        ApagarSombras();
        ApagarSombraGlobal();
        CrearMiras();
        CrearNubes();
        FrancotiradorScope.Crear();
        IniciarSonidoLoop();
        Coroutines.Start(CoZoom(_cam.orthographicSize, NivelZoomObjetivo(), 0.5f));
    }

    /// <summary>
    ///     Sale del zoom out. Con reinicioTotal vuelve a "Recargando…" (tras matar,
    ///     por reunión o al acabar el tiempo de zoom sin disparar); si no, queda
    ///     Armado ("Disparar").
    /// </summary>
    public static void CancelarApuntado(bool reinicioTotal)
    {
        var estabaApuntando = Fase == FrancotiradorPhase.Apuntando;
        CancelarApuntadoInterno();

        if (reinicioTotal)
        {
            ReiniciarCiclo();
        }
        else if (estabaApuntando)
        {
            Fase = FrancotiradorPhase.Armado;
            SetMovimiento(true);
        }
    }

    private static void CancelarApuntadoInterno()
    {
        DestruirMiras();
        DestruirNubes();
        FrancotiradorScope.Destruir();
        PararSonidoLoop();
        EncenderSombras();
        EncenderSombraGlobal();
        RestaurarHud();

        if (_cam != null && _zoomOriginal > 0f &&
            Mathf.Abs(_cam.orthographicSize - _zoomOriginal) > 0.01f)
        {
            Coroutines.Start(CoZoom(_cam.orthographicSize, _zoomOriginal, 0.45f));
        }
    }

    /// <summary>Gestión por frame del zoom out: tiempo, miras y disparo.</summary>
    private static void GestionarApuntado()
    {
        // Mover las miras rojas a la posición en pantalla de cada objetivo.
        ActualizarMirasEnPantalla();

        // Fin del tiempo de zoom SIN disparar: el ciclo vuelve a "Recargando…"
        // (así no se puede espiar el mapa gratis una y otra vez).
        if (Time.time >= FaseFin)
        {
            CancelarApuntado(true);
            return;
        }

        // Clic izquierdo SOBRE una mira = asesinar a ese jugador a distancia.
        if (Input.GetMouseButtonDown(0))
        {
            PlayerControl? objetivo = null;
            var mejorPx = 90f; // tolerancia de clic en píxeles

            foreach (var kvp in Miras)
            {
                var (jugador, mira) = kvp.Value;
                if (!EsObjetivoValido(jugador) || !mira.gameObject.activeSelf)
                {
                    continue;
                }

                var d = Vector2.Distance(mira.position, Input.mousePosition);
                if (d < mejorPx)
                {
                    mejorPx = d;
                    objetivo = jugador;
                }
            }

            if (objetivo != null)
            {
                Disparar(objetivo);
            }
        }
    }

    /// <summary>
    ///     ¿Es un objetivo válido? (tripulante o neutral vivo, no uno mismo).
    ///     NUNCA son objetivo los roles afterlife/post-mortem (Haunter,
    ///     Spectre, Revenant, etc.): no tienen mira roja y son
    ///     inmunes al disparo del Francotirador.
    /// </summary>
    public static bool EsObjetivoValido(PlayerControl p)
    {
        return p != null && !p.AmOwner && p.Data != null && !p.Data.IsDead &&
               !p.Data.Disconnected && !p.IsImpostorAligned() &&
               p.Data.Role is not IGhostRole;
    }

    /// <summary>¡PUM! Mata al objetivo desde la distancia y vuelve a Recargando…</summary>
    private static void Disparar(PlayerControl objetivo)
    {
        if (!EstaApuntando || !EsObjetivoValido(objetivo))
        {
            Debug.Log("[TownOfPlus][Francotirador] Disparo rechazado " +
                      $"(apuntando={EstaApuntando}, válido={EsObjetivoValido(objetivo)}).");
            return;
        }

        Debug.Log($"[TownOfPlus][Francotirador] Disparando a {objetivo.Data?.PlayerName}.");

        // PRIMERO el kill a distancia SIN teletransportarse (RPC especial de
        // TOU): nada debe impedir el disparo.
        try
        {
            PlayerControl.LocalPlayer.RpcSpecialMurder(
                objetivo, MeetingCheck.OutsideMeeting, teleportMurderer: false);
        }
        catch (Exception ex)
        {
            // Respaldo: vía de murder original de MiraAPI.
            Debug.LogWarning($"[TownOfPlus][Francotirador] RpcSpecialMurder falló " +
                             $"({ex.Message}); intentando RpcCustomMurder...");
            PlayerControl.LocalPlayer.RpcCustomMurder(objetivo, MeetingCheck.OutsideMeeting);
        }

        // DESPUÉS el disparo GLOBAL (todos lo escuchan). Si el envío falla
        // (p. ej. en freeplay no hay clientes remotos), el kill ya fue enviado.
        try
        {
            FrancotiradorRpc.EnviarDisparoGlobal();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TownOfPlus][Francotirador] Sonido global no enviado: {ex.Message}");
        }

        CancelarApuntado(true); // ciclo completo: vuelve a "Recargando…"
    }

    private static float NivelZoomObjetivo()
    {
        return OptionGroupSingleton<FrancotiradorOptions>.Instance.NivelZoom.Value;
    }

    /// <summary>Bloquea o libera el movimiento del jugador local.</summary>
    private static void SetMovimiento(bool libre)
    {
        var local = PlayerControl.LocalPlayer;
        if (local != null)
        {
            local.moveable = libre;
        }
    }

    /// <summary>
    ///     Congelación total: bloquea el input Y anula la velocidad del cuerpo
    ///     cada frame, de modo que el Francotirador quede clavado en su sitio
    ///     (antes, con moveable=false a medias, se deslizaba e incluso
    ///     atravesaba paredes).
    /// </summary>
    private static void Congelar()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null)
        {
            return;
        }

        local.moveable = false;

        var body = local.MyPhysics != null ? local.MyPhysics.body : null;
        if (body != null && body.velocity.sqrMagnitude > 0f)
        {
            body.velocity = Vector2.zero;
        }
    }

    /// <summary>Oculta mapa, sabotaje y reporte durante el zoom.</summary>
    private static void OcultarHud()
    {
        var hm = HudManager.Instance;
        if (hm == null)
        {
            return;
        }

        hm.MapButton?.gameObject.SetActive(false);
        hm.SabotageButton?.gameObject.SetActive(false);
        hm.ReportButton?.gameObject.SetActive(false);
    }

    private static void RestaurarHud()
    {
        var hm = HudManager.Instance;
        if (hm == null)
        {
            return;
        }

        // El botón de habilidad NO se restaura: el Francotirador no lo usa
        // y se mantiene oculto todo el tiempo (lo gestiona su botón principal).
        hm.MapButton?.gameObject.SetActive(true);
        hm.SabotageButton?.gameObject.SetActive(true);
        hm.ReportButton?.gameObject.SetActive(true);
    }

    /// <summary>
    ///     Apaga TODOS los SpriteRenderers de cada LightSource del mapa
    ///     (polígonos de sombra y halo de luz) usando reflexión: así el zoom out
    ///     muestra cada habitación sin zonas oscuras, sea cual sea el nombre
    ///     interno de los campos en esta versión del juego.
    /// </summary>
    private static void ApagarSombras()
    {
        SombrasApagadas.Clear();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        foreach (var ls in Object.FindObjectsOfType<LightSource>())
        {
            foreach (var f in typeof(LightSource).GetFields(flags))
            {
                try
                {
                    if (f.FieldType == typeof(SpriteRenderer) &&
                        f.GetValue(ls) is SpriteRenderer sr && sr.enabled)
                    {
                        sr.enabled = false;
                        SombrasApagadas.Add(sr);
                    }
                }
                catch
                {
                    // Campo no accesible en esta versión: ignorar.
                }
            }

            foreach (var p in typeof(LightSource).GetProperties(flags))
            {
                try
                {
                    if (p.PropertyType == typeof(SpriteRenderer) &&
                        p.GetValue(ls) is SpriteRenderer sr && sr.enabled)
                    {
                        sr.enabled = false;
                        SombrasApagadas.Add(sr);
                    }
                }
                catch
                {
                    // Propiedad no accesible en esta versión: ignorar.
                }
            }
        }
    }

    private static void EncenderSombras()
    {
        foreach (var sombra in SombrasApagadas)
        {
            if (sombra != null)
            {
                sombra.enabled = true;
            }
        }

        SombrasApagadas.Clear();
    }

    /// <summary>
    ///     Apaga el ShadowQuad global del HUD (el gran overlay negro de visión,
    ///     igual que hace el zoom de Town of Us): elimina TODA la sombra restante.
    /// </summary>
    private static void ApagarSombraGlobal()
    {
        var hm = HudManager.Instance;
        if (hm == null || hm.ShadowQuad == null || !hm.ShadowQuad.gameObject.activeSelf)
        {
            return;
        }

        hm.ShadowQuad.gameObject.SetActive(false);
        _sombraGlobalApagada = true;
    }

    private static void EncenderSombraGlobal()
    {
        if (!_sombraGlobalApagada)
        {
            return;
        }

        var hm = HudManager.Instance;
        var local = PlayerControl.LocalPlayer;
        if (hm != null && hm.ShadowQuad != null && local?.Data != null)
        {
            // Igual que TOU: vuelve solo si el jugador no está muerto.
            hm.ShadowQuad.gameObject.SetActive(!local.Data.IsDead);
        }

        _sombraGlobalApagada = false;
    }

    /// <summary>Arranca el sonido ambiental .wav EN BUCLE durante el zoom out (siempre ON, volumen 1.0).</summary>
    private static void IniciarSonidoLoop()
    {
        PararSonidoLoop();
        try
        {
            if (SoundManager.Instance == null)
            {
                return;
            }

            var clip = SonidosTop.Viento.LoadAsset();
            if (clip == null)
            {
                return;
            }

            const float volumen = 1.0f;
            _sonidoLoop = AudioTop.Sonar(clip, true, volumen);
        }
        catch
        {
            _sonidoLoop = null;
        }
    }

    private static void PararSonidoLoop()
    {
        if (_sonidoLoop == null)
        {
            return;
        }

        try
        {
            _sonidoLoop.Stop();
        }
        catch
        {
            // Ya estaba destruido.
        }

        _sonidoLoop = null;
    }

    /// <summary>Animación suave de zoom (aleja o acerca la cámara).</summary>
    private static IEnumerator CoZoom(float desde, float hasta, float duracion)
    {
        var t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, duracion);
            var k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            if (_cam != null)
            {
                _cam.orthographicSize = Mathf.Lerp(desde, hasta, k);
            }

            yield return null;
        }
    }

    /// <summary>
    ///     Crea una mira roja (PNG) por cada tripulante/neutral vivo, sobre un
    ///     canvas de pantalla: SIEMPRE visibles por encima del mapa y las sombras,
    ///     y solo el Francotirador puede verlas (existen solo en su cliente).
    /// </summary>
    private static void CrearMiras()
    {
        DestruirMiras();

        _canvasMiras = new GameObject("TOP_MirasFrancotirador");
        var canvas = _canvasMiras.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;

        var spriteMira = TownOfPlus.Assets.TownofplusAssets.MiraRoja.LoadAsset();
        if (spriteMira == null)
        {
            return;
        }

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!EsObjetivoValido(p))
            {
                continue;
            }

            var imgGo = new GameObject($"Mira_{p.PlayerId}");
            imgGo.transform.SetParent(_canvasMiras.transform, false);

            var img = imgGo.AddComponent<Image>();
            img.sprite = spriteMira;
            img.color = new Color(1f, 0.3f, 0.3f, 0.95f);
            img.raycastTarget = false;
            img.rectTransform.sizeDelta = new Vector2(72f, 72f);

            Miras[p.PlayerId] = (p, img.rectTransform);
        }

        ActualizarMirasEnPantalla();
    }

    /// <summary>Mueve cada mira a la posición en pantalla de su jugador.</summary>
    private static void ActualizarMirasEnPantalla()
    {
        var cam = _cam != null ? _cam : Camera.main;
        if (cam == null)
        {
            return;
        }

        foreach (var kvp in Miras)
        {
            var (jugador, mira) = kvp.Value;

            if (!EsObjetivoValido(jugador))
            {
                mira.gameObject.SetActive(false);
                continue;
            }

            mira.gameObject.SetActive(true);
            var pos = cam.WorldToScreenPoint(jugador.GetTruePosition());
            pos.z = 0f;
            mira.position = pos;
        }
    }

    private static void DestruirMiras()
    {
        Miras.Clear();

        if (_canvasMiras != null)
        {
            Object.Destroy(_canvasMiras);
            _canvasMiras = null;
        }
    }

    /// <summary>Nubes suaves que cruzan la pantalla durante el zoom out.</summary>
    private static void CrearNubes()
    {
        DestruirNubes();
        var cam = _cam != null ? _cam : Camera.main;
        if (cam == null)
        {
            return;
        }

        _nubesRoot = new GameObject("TOP_NubesFrancotirador");
        _nubesRoot.transform.SetParent(cam.transform, false);

        var tex = TexturaSuave(128);
        var sprite = Sprite.Create(tex, new Rect(0, 0, 128, 128),
            new Vector2(0.5f, 0.5f), 100f);

        // Varias nubes a distintas alturas y profundidades.
        for (var i = 0; i < 5; i++)
        {
            var nube = new GameObject($"Nube{i}");
            nube.transform.SetParent(_nubesRoot.transform, false);

            var alto = cam.orthographicSize;
            var ancho = alto * cam.aspect;
            var x = Random.Range(-ancho, ancho);
            var y = Random.Range(-alto * 0.6f, alto * 0.9f);

            nube.transform.localPosition = new Vector3(x, y, 6f);
            nube.transform.localScale = new Vector3(
                Random.Range(4.5f, 8f), Random.Range(1.6f, 2.6f), 1f);

            var rend = nube.AddComponent<SpriteRenderer>();
            rend.sprite = sprite;
            rend.color = new Color(1f, 1f, 1f, Random.Range(0.10f, 0.22f));
            rend.sortingOrder = 60;

            var deriva = nube.AddComponent<FrancotiradorNubeDeriva>();
            deriva.Velocidad = Random.Range(0.35f, 0.9f) * (Random.value > 0.5f ? 1f : -1f);
            deriva.Ancho = ancho + 6f;
        }
    }

    private static void DestruirNubes()
    {
        if (_nubesRoot != null)
        {
            Object.Destroy(_nubesRoot);
            _nubesRoot = null;
        }
    }

    /// <summary>Textura circular blanca con caída suave hacia los bordes.</summary>
    private static Texture2D TexturaSuave(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var centro = size / 2f;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x - centro) / centro;
                var dy = (y - centro) / centro;
                var d = Mathf.Sqrt((dx * dx) + (dy * dy));
                var a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }

        tex.Apply();
        return tex;
    }
}