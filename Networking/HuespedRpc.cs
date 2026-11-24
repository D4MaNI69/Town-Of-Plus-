using System;
using MiraAPI.GameOptions;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using TownOfPlus.Modules;
using TownOfPlus.Options.Crewmate.CrewmateAfterlife;
using TownOfPlus.Roles.Crewmate.CrewmateAfterlife;
using UnityEngine;

namespace TownOfPlus.Networking;

/// <summary>
///     RPCs del Huésped (afterlife crew) — habilidad POSEER.
///
///     Flujo (inicio/fin host-authoritative, movimiento cliente-autoritativo):
///     1) El Huésped (cliente) pide la posesión: RpcPeticionPosesion → host.
///     2) El HOST valida, lee el tiempo de posesión de las options y difunde
///        RpcPosesionInicio a TODOS (LocalHandling.Before: también se aplica en
///        el propio host).
///     3) Durante la posesión el controlador envía su dirección WASD por el
///        canal UNRELIABLE (HuespedInputUnreliableRpc, LocalHandling.Before):
///        el host lo reenvía a TODOS en un único salto y cada cliente valida al
///        remitente. El cliente del poseído inyecta el movimiento por
///        SetNormalizedVelocity (velocidad REAL del tripulante, animaciones
///        vanilla + muros).
///     4) Al acabar el tiempo (gestión del host) o al cortar la posesión, se
///        difunde RpcPosesionFin a TODOS y cada cliente limpia su estado local.
///
///     El remitente de cada RPC se valida para evitar envíos maliciosos.
/// </summary>
public static class HuespedRpc
{
    /// <summary>Petición de posesión (cliente → host). El host valida y difunde el inicio.</summary>
    public static void PedirPosesion(PlayerControl huesped, PlayerControl objetivo)
    {
        try
        {
            if (AmongUsClient.Instance.AmHost)
            {
                ProcesarPeticion(huesped, objetivo);
                return;
            }

            RpcPeticionPosesion(huesped, objetivo.PlayerId);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Huésped] Petición de posesión no enviada: {ex.Message}");
        }
    }

    /// <summary>Handler de la petición (solo host).</summary>
    [MethodRpc((uint)TownOfPlusRpcCalls.HuespedPeticionPosesion, LocalHandling = RpcLocalHandling.None)]
    public static void RpcPeticionPosesion(PlayerControl sender, byte objetivoId)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            return;
        }

        var objetivo = Resolver(objetivoId);
        if (objetivo == null)
        {
            return;
        }

        ProcesarPeticion(sender, objetivo);
    }

    /// <summary>
    ///     HOST: valida que el remitente sea un Huésped real y que el objetivo
    ///     sea un vivo válido; después difunde el inicio de la posesión.
    /// </summary>
    private static void ProcesarPeticion(PlayerControl huesped, PlayerControl objetivo)
    {
        if (huesped == null || huesped.Data == null || huesped.Data.Disconnected ||
            huesped.Data.Role is not HuespedRole)
        {
            return;
        }

        if (objetivo == null || objetivo.Data == null || objetivo.Data.IsDead ||
            objetivo.Data.Disconnected || objetivo == huesped)
        {
            return;
        }

        // Una sola posesión a la vez.
        if (HuespedManager.Activa)
        {
            return;
        }

        var duracion = OptionGroupSingleton<HuespedOptions>.Instance.TiempoPosesion.Value;
        RpcPosesionInicio(huesped, huesped.PlayerId, objetivo.PlayerId, duracion);
    }

    /// <summary>
    ///     Inicio de la posesión (host → todos; LocalHandling.Before también lo
    ///     aplica en el host). Corre en TODOS los clientes: cada uno marca su
    ///     estado y el host arranca la corrutina de movimiento.
    /// </summary>
    [MethodRpc((uint)TownOfPlusRpcCalls.HuespedPosesionInicio, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcPosesionInicio(PlayerControl sender, byte controladorId, byte objetivoId, float duracion)
    {
        HuespedManager.EstablecerPosesion(controladorId, objetivoId, duracion);
    }

    /// <summary>Difunde el fin de la posesión a todos (solo host).</summary>
    public static void FinalizarPosesionGlobal()
    {
        try
        {
            if (AmongUsClient.Instance.AmHost)
            {
                RpcPosesionFin(PlayerControl.LocalPlayer);
            }
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Huésped] Difusión del fin de posesión no enviada: {ex.Message}");
        }
    }

    /// <summary>
    ///     Fin de la posesión (host → todos; LocalHandling Before). Corre en
    ///     TODOS los clientes y cada uno limpia su estado local.
    /// </summary>
    [MethodRpc((uint)TownOfPlusRpcCalls.HuespedPosesionFin, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcPosesionFin(PlayerControl sender)
    {
        try
        {
            HuespedManager.FinalizarPosesion();
        }
        catch
        {
            // Nunca romper la limpieza.
        }
    }

    /// <summary>Dirección WASD del controlador (hacia el host, con throttling).</summary>
    public static void EnviarPosesionInput(float x, float y)
    {
        try
        {
            if (AmongUsClient.Instance.AmHost)
            {
                HuespedManager.SetMovimiento(PlayerControl.LocalPlayer.PlayerId, x, y);
                return;
            }

            RpcPosesionInput(PlayerControl.LocalPlayer, x, y);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Huésped] Input de posesión no enviado: {ex.Message}");
        }
    }

    /// <summary>
    ///     Dirección del controlador hacia el host (cliente → host, LocalHandling
    ///     None: el remitente no la aplica localmente; el host la valida y la
    ///     reenvía a todos para que el poseído la consuma).
    /// </summary>
    [MethodRpc((uint)TownOfPlusRpcCalls.HuespedPosesionInput, LocalHandling = RpcLocalHandling.None)]
    public static void RpcPosesionInput(PlayerControl sender, float x, float y)
    {
        if (!AmongUsClient.Instance.AmHost || sender == null)
        {
            return;
        }

        HuespedManager.SetMovimiento(sender.PlayerId,
            Mathf.Clamp(x, -1f, 1f), Mathf.Clamp(y, -1f, 1f));
    }

    /// <summary>
    ///     Dirección validada que el host reenvía a TODOS (host → todos,
    ///     LocalHandling Before: también se aplica en el host). Corre en todos
    ///     los clientes: solo el cliente del poseído la usa para moverse.
    /// </summary>
    [MethodRpc((uint)TownOfPlusRpcCalls.HuespedPosesionDireccion, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcPosesionDireccion(PlayerControl sender, float x, float y)
    {
        try
        {
            HuespedManager.AplicarDireccion(new Vector2(x, y));
        }
        catch
        {
            // Un paquete de dirección nunca debe romper nada.
        }
    }

    /// <summary>El host reenvía la dirección validada a todos los clientes.</summary>
    public static void DifundirDireccion(byte controladorId, float x, float y)
    {
        try
        {
            if (AmongUsClient.Instance.AmHost)
            {
                RpcPosesionDireccion(PlayerControl.LocalPlayer, x, y);
            }
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Huésped] Dirección de posesión no difundida: {ex.Message}");
        }
    }

    /// <summary>Resuelve un PlayerControl por PlayerId.</summary>
    private static PlayerControl? Resolver(byte id)
    {
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p != null && p.PlayerId == id)
            {
                return p;
            }
        }

        return null;
    }
}