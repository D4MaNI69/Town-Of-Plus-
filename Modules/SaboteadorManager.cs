using System;
using System.Collections;
using MiraAPI.Utilities;
using Reactor.Utilities;
using TownOfPlus.Assets;
using TownOfPlus.Roles.Impostor.ImpostorAfterlife;
using UnityEngine;
using UnityEngine.UI;

namespace TownOfPlus.Modules;

/// <summary>
///     Estado y efectos del Saboteador. "Luces Fuera" dispara el SABOTAJE REAL
///     de Electrical/Luces del juego (canal vanilla SystemTypes.Sabotage, como
///     el botón de sabotaje vanilla), así que la visión la gestiona el propio
///     juego: los impostores no pierden visión y los tripulantes quedan con la
///     visión mínima de la sala. Otros sabotajes (O2/Reactor/Comms) no
///     interfieren. Al acabar el tiempo configurado, el propio rol RESUELVE el
///     sabotaje (patrón Engineer de TOU Mira: ActualSwitches = ExpectedSwitches
///     en todos los clientes) para no dejar el sabo colgado de forma rota.
/// </summary>
public static class SaboteadorManager
{
    public static bool EsSaboteadorLocal =>
        PlayerControl.LocalPlayer?.Data?.Role is SaboteadorRole;

    /// <summary>¿Hay un apagón del Saboteador en curso (ventana del efecto)?</summary>
    public static bool LucesApagadas { get; private set; }

    /// <summary>Duración configurada del apagón activo (para el mensaje X).</summary>
    public static float DuracionActual { get; private set; }

    /// <summary>¿El sabotaje vanilla de Electrical/Luces está activo ahora mismo?</summary>
    public static bool LucesVanillaSaboteadas
    {
        get
        {
            try
            {
                if (ShipStatus.Instance == null)
                {
                    return false;
                }

                return ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Electrical,
                           out var sistema) &&
                       sistema.TryCast<SwitchSystem>()?.IsActive == true;
            }
            catch
            {
                return false;
            }
        }
    }

    private static IEnumerator? _reparacionProgramada;

    /// <summary>
    ///     Activa el apagón en ESTE cliente (llega por RPC en todos los
    ///     clientes con la misma duración, así todo queda sincronizado).
    ///     En el HOST dispara además el sabotaje real de luces.
    /// </summary>
    public static void ApagarLuces(float duracion)
    {
        if (duracion <= 0f)
        {
            return;
        }

        LucesApagadas = true;
        DuracionActual = duracion;

        MostrarMensaje(duracion);

        // Los impostores (vivos o muertos enterados) ven un flash negro breve
        // para saber que fue el Saboteador y no un sabo vanilla normal.
        var local = PlayerControl.LocalPlayer;
        if (local?.Data?.Role?.IsImpostor == true)
        {
            Coroutines.Start(CoFlashImpostor());
        }

        // HOST: sabotaje REAL por el canal vanilla del juego.
        IniciarSabotajeReal();

        // Todos los clientes programan la resolución al acabar el tiempo.
        if (_reparacionProgramada != null)
        {
            Coroutines.Stop(_reparacionProgramada);
        }

        _reparacionProgramada = Coroutines.Start(CoResolverLuces(duracion));
    }

    /// <summary>
    ///     HOST: dispara el sabotaje real de Electrical/Luces por el MISMO
    ///     canal vanilla que usa el botón de sabotaje del juego
    ///     (RpcUpdateSystem(SystemTypes.Sabotage, Electrical)). Se reinicia el
    ///     cooldown del sistema de sabotaje solo en el host para que el efecto
    ///     del rol SIEMPRE salga (rol custom, no compite con el botón vanilla).
    /// </summary>
    public static void IniciarSabotajeReal()
    {
        if (ShipStatus.Instance == null || AmongUsClient.Instance == null ||
            !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        try
        {
            var sistemaSabo = ShipStatus.Instance.Systems[SystemTypes.Sabotage]
                .TryCast<SabotageSystemType>();
            if (sistemaSabo != null && sistemaSabo.Timer > 0f)
            {
                sistemaSabo.Timer = 0f;
            }

            ShipStatus.Instance.RpcUpdateSystem(
                SystemTypes.Sabotage, (byte)SystemTypes.Electrical);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Saboteador] No se pudo disparar el sabotaje real de luces: {ex.Message}");
        }
    }

    private static IEnumerator CoResolverLuces(float duracion)
    {
        yield return new WaitForSeconds(duracion);
        ResolverLuces();
    }

    /// <summary>
    ///     Resuelve el sabotaje real de luces (patrón Engineer de TOU Mira:
    ///     ActualSwitches = ExpectedSwitches en todos los clientes, que los
    ///     sincroniza vía dirty-flags del host). Nunca queda el sabo colgado.
    /// </summary>
    public static void ResolverLuces()
    {
        try
        {
            if (ShipStatus.Instance != null &&
                ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Electrical,
                    out var sistema) &&
                sistema.TryCast<SwitchSystem>() is { } luces &&
                luces.IsActive)
            {
                luces.ActualSwitches = luces.ExpectedSwitches;
            }
        }
        catch
        {
            // Nunca romper el cálculo de luces por un error del rol.
        }

        LucesApagadas = false;
        _reparacionProgramada = null;
    }

    /// <summary>Limpia estados locales (muerte/expulsión del rol, nueva partida).</summary>
    public static void LimpiarEstadosLocales()
    {
        // NO se toca el sistema vanilla de luces aquí: el sabotaje real lo
        // gestiona el juego (la reunión y el ciclo vanilla) y la reparación
        // programada del efecto sigue su curso aunque haya reunión.
        LucesApagadas = false;
    }

    private static void MostrarMensaje(float duracion)
    {
        try
        {
            Helpers.CreateAndShowNotification(
                $"<b>El Saboteador apagó las Luces durante {duracion:0}s</b>",
                Color.white, new Vector3(0f, 1f, -20f),
                spr: TownofplusAssets.SaboteadorIcon.LoadAsset());
        }
        catch
        {
            // Sin notificación no pasa nada.
        }
    }

    /// <summary>Flash negro breve para los impostores (fade in → hold → fade out).</summary>
    private static IEnumerator CoFlashImpostor()
    {
        var root = new GameObject("TOP_SaboteadorFlash");
        UnityEngine.Object.DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;

        var img = root.AddComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = false;

        const float entrada = 0.12f;
        const float mantener = 0.18f;
        const float salida = 0.35f;

        // Alpha máximo SEMITRANSPARENTE: el impostor sigue viendo el mapa,
        // solo recibe un aviso visual breve de que el Saboteador actuó.
        const float alphaMax = 0.55f;

        var t = 0f;
        while (t < entrada)
        {
            t += Time.unscaledDeltaTime;
            img.color = new Color(0f, 0f, 0f, alphaMax * Mathf.Clamp01(t / entrada));
            yield return null;
        }

        img.color = new Color(0f, 0f, 0f, alphaMax);
        yield return new WaitForSecondsRealtime(mantener);

        t = 0f;
        while (t < salida)
        {
            t += Time.unscaledDeltaTime;
            img.color = new Color(0f, 0f, 0f, alphaMax * (1f - Mathf.Clamp01(t / salida)));
            yield return null;
        }

        UnityEngine.Object.Destroy(root);
    }
}