using System;
using System.Collections;
using System.Collections.Generic;
using MiraAPI.GameOptions;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using Reactor.Utilities;
using TownOfPlus.Modules;
using TownOfPlus.Options.Crewmate.CrewmateAfterlife;
using TownOfPlus.Roles.Crewmate.CrewmateAfterlife;

namespace TownOfPlus.Networking;

/// <summary>
///     RPCs del Cerrajero: abrir/cerrar UNA SOLA PUERTA (la más cercana en
///     rango). Nada de salas enteras ni de "todas las puertas de una sala".
///
///     Se usa SIEMPRE el sistema REAL de puertas del mapa y SOLO el host es
///     la fuente de verdad:
///     - CERRAR: el host aplica door.SetDoorway(false) y lo replica por RPC
///       a TODOS los clientes (animación + colisión reales en cada cliente).
///       El host programa la reapertura automática tras el "Tiempo de Puerta
///       Cerrada" configurado.
///     - ABRIR: RpcUpdateSystem(SystemTypes.Doors, doorId) — el canal
///       OFICIAL del juego que usan las consolas de reparación. DoorsSystem
///       lo procesa en TODOS los clientes (también sobre puertas cerradas
///       por sabotaje) con estado y colisión reales.
///     El remitente NUNCA aplica nada local: pide el cambio al host y recibe
///     el estado sincronizado como cualquier otro cliente.
/// </summary>
public static class CerrajeroRpc
{
    /// <summary>
    ///     Puertas con reapertura automática programada (timer de "Tiempo de
    ///     Puerta Cerrada"). Si el Cerrajero ABRE la puerta antes, se elimina
    ///     de aquí y el timer se cancela.
    /// </summary>
    private static readonly HashSet<byte> ReaperturasProgramadas = new();

    /// <summary>
    ///     Abre UNA puerta concreta. El host aplica el cambio por el canal
    ///     oficial (el RPC no vuelve a él); el resto de clientes piden el
    ///     cambio al host.
    /// </summary>
    public static void EnviarAbrirPuerta(byte doorId)
    {
        try
        {
            if (AmongUsClient.Instance.AmHost)
            {
                AbrirPuertaHost(doorId);
                return;
            }

            RpcAbrirPuerta(PlayerControl.LocalPlayer, doorId);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Cerrajero] RPC abrir puerta no enviado: {ex.Message}");
        }
    }

    /// <summary>
    ///     Cierra UNA puerta concreta. Mismo patrón host-authoritative.
    /// </summary>
    public static void EnviarCerrarPuerta(byte doorId)
    {
        try
        {
            if (AmongUsClient.Instance.AmHost)
            {
                CerrarPuertaHost(doorId);
                return;
            }

            RpcCerrarPuerta(PlayerControl.LocalPlayer, doorId);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Cerrajero] RPC cerrar puerta no enviado: {ex.Message}");
        }
    }

    /// <summary>
    ///     Petición de ABRIR PUERTA (cliente → host). LocalHandling.None: el
    ///     remitente NO aplica nada local; solo el host procesa el cambio en
    ///     el sistema REAL de puertas y el estado se replica a todos.
    /// </summary>
    [MethodRpc((uint)TownOfPlusRpcCalls.CerrajeroAbrirPuerta, LocalHandling = RpcLocalHandling.None)]
    public static void RpcAbrirPuerta(PlayerControl sender, byte doorId)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            // Host: valida al remitente (fuente de verdad única) y aplica.
            if (sender?.Data?.Role is not CerrajeroRole)
            {
                return;
            }

            AbrirPuertaHost(doorId);
            return;
        }

        // Cliente: aplica el estado de puerta replicado por el host
        // (apertura real: animación + colisión).
        AplicarAbrirPuerta(doorId);
    }

    /// <summary>
    ///     Petición de CERRAR PUERTA (cliente → host). El host valida y
    ///     replica el estado a todos los clientes vía broadcast del mismo
    ///     RPC (los demás clientes aplican el estado de puerta que llega).
    /// </summary>
    [MethodRpc((uint)TownOfPlusRpcCalls.CerrajeroCerrarPuerta, LocalHandling = RpcLocalHandling.None)]
    public static void RpcCerrarPuerta(PlayerControl sender, byte doorId)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            // Host: valida al remitente (fuente de verdad única) y replica.
            if (sender?.Data?.Role is not CerrajeroRole)
            {
                return;
            }

            CerrarPuertaHost(doorId);
            return;
        }

        // Cliente: aplica el estado de puerta replicado por el host.
        AplicarCerrarPuerta(doorId);
    }

    /// <summary>
    ///     HOST: abre UNA puerta FORZANDO la apertura real:
    ///     - Cancela el timer de "Tiempo de Puerta Cerrada" de esa puerta
    ///       (el Abrir del Cerrajero ignora el cierre programado).
    ///     - Aplica SetDoorway(true) en el host y lo replica a TODOS los
    ///       clientes por broadcast (animación + colisión reales).
    ///     - Además envía RpcUpdateSystem(SystemTypes.Doors, doorId) (el canal
    ///       OFICIAL del juego, como las consolas de reparación) para dejar
    ///       coherente el estado interno de DoorsSystem (también rompe cierres
    ///       por sabotaje).
    /// </summary>
    public static void AbrirPuertaHost(byte doorId)
    {
        try
        {
            if (ShipStatus.Instance == null)
            {
                return;
            }

            // El Abrir manual ignora/cancela el timer de cierre programado.
            ReaperturasProgramadas.Remove(doorId);

            // Apertura FORZADA: aplicar en el host y replicar a los clientes.
            AplicarAbrirPuerta(doorId);
            RpcAbrirPuertaBroadcast(doorId);

            // Canal oficial para mantener coherente el DoorsSystem vanilla.
            ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, doorId);

            TownOfPlusPlugin.Instance?.Log.LogInfo(
                $"[Cerrajero] Host abrió (forzado) la puerta {doorId} (SetDoorway(true) + RpcUpdateSystem(Doors)).");
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Cerrajero] Error al abrir puerta en el host: {ex.Message}");
        }
    }

    /// <summary>
    ///     HOST: cierra UNA puerta. Aplica SetDoorway(false) localmente y lo
    ///     replica a TODOS los clientes por broadcast del RPC de cierre
    ///     (animación + colisión reales en cada cliente). Programa la
    ///     reapertura automática tras el "Tiempo de Puerta Cerrada".
    /// </summary>
    public static void CerrarPuertaHost(byte doorId)
    {
        try
        {
            if (ShipStatus.Instance == null)
            {
                return;
            }

            // Aplicar en el host y replicar a todos los clientes.
            AplicarCerrarPuerta(doorId);
            RpcCerrarPuertaBroadcast(doorId);

            // Reapertura automática tras el tiempo configurado (solo host).
            // La puerta NUNCA queda cerrada para siempre: si la reunión
            // interrumpe el timer, la corrutina sigue esperando y reabre al
            // volver al juego.
            var tiempo = OptionGroupSingleton<CerrajeroOptions>.Instance.PuertaCerradaTiempo.Value;
            ReaperturasProgramadas.Add(doorId);
            if (tiempo > 0f)
            {
                Coroutines.Start(CoReabrirPuerta(doorId, tiempo));
            }

            TownOfPlusPlugin.Instance?.Log.LogInfo(
                $"[Cerrajero] Host cerró la puerta {doorId} (reabre en {tiempo:0.#}s).");
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Cerrajero] Error al cerrar puerta en el host: {ex.Message}");
        }
    }

    /// <summary>
    ///     Broadcast del cierre replicado: al llamarse desde el host, llega
    ///     a todos los demás clientes (el host ya aplicó su estado local en
    ///     CerrarPuertaHost y el LocalHandling.None evita doble aplicación).
    /// </summary>
    private static void RpcCerrarPuertaBroadcast(byte doorId)
    {
        try
        {
            RpcCerrarPuerta(PlayerControl.LocalPlayer, doorId);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Cerrajero] Broadcast de cierre de puerta falló: {ex.Message}");
        }
    }

    /// <summary>
    ///     Broadcast de la apertura replicada a los demás clientes (el host
    ///     ya aplicó su estado local en AbrirPuertaHost y LocalHandling.None
    ///     evita doble aplicación).
    /// </summary>
    private static void RpcAbrirPuertaBroadcast(byte doorId)
    {
        try
        {
            RpcAbrirPuerta(PlayerControl.LocalPlayer, doorId);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Cerrajero] Broadcast de apertura de puerta falló: {ex.Message}");
        }
    }

    /// <summary>
    ///     Aplica el CIERRE REAL de una puerta en el cliente actual:
    ///     animación + colisión (SetDoorway(false)), usando el sistema REAL
    ///     de puertas del mapa.
    /// </summary>
    private static void AplicarCerrarPuerta(byte doorId)
    {
        var door = CerrajeroManager.PuertaPorId(doorId);
        if (door == null || !door.IsOpen)
        {
            return;
        }

        door.SetDoorway(false);
    }

    /// <summary>
    ///     Aplica la APERTURA REAL de una puerta en el cliente actual:
    ///     animación + colisión (SetDoorway(true)), usando el sistema REAL
    ///     de puertas del mapa.
    /// </summary>
    private static void AplicarAbrirPuerta(byte doorId)
    {
        var door = CerrajeroManager.PuertaPorId(doorId);
        if (door == null || door.IsOpen)
        {
            return;
        }

        door.SetDoorway(true);
    }

    /// <summary>
    ///     Reapertura automática tras el "Tiempo de Puerta Cerrada".
    ///     RoBUSTA: espera el tiempo completo aunque haya una reunión en
    ///     curso (antes abortaba con MeetingHud abierto y la puerta quedaba
    ///     cerrada para siempre). Si el Cerrajero abrió la puerta antes (o
    ///     el timer se canceló), no hace nada.
    /// </summary>
    private static IEnumerator CoReabrirPuerta(byte doorId, float espera)
    {
        var inicio = UnityEngine.Time.time;
        var fin = inicio + espera;

        // Poll corto: se comprueba la cancelación (apertura manual) y se
        // tolera cualquier reunión que ocurra durante la espera.
        while (UnityEngine.Time.time < fin)
        {
            if (!ReaperturasProgramadas.Contains(doorId))
            {
                // El Cerrajero abrió la puerta antes de tiempo: cancelado.
                yield break;
            }

            yield return new UnityEngine.WaitForSeconds(0.25f);
        }

        ReaperturasProgramadas.Remove(doorId);

        if (ShipStatus.Instance == null)
        {
            yield break;
        }

        var door = CerrajeroManager.PuertaPorId(doorId);
        if (door == null || door.IsOpen)
        {
            // Ya está abierta.
            yield break;
        }

        // Reabre con el canal oficial (se aplica en TODOS los clientes),
        // incluso si la reunión sigue en curso: al volver al juego la
        // puerta ya estará en su estado correcto.
        AbrirPuertaHost(doorId);
    }
}