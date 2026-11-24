using System;
using System.Linq;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using TownOfPlus.Modules;

namespace TownOfPlus.Networking;

/// <summary>
///     RPCs del Abogado (impostor support): estado (reuniones/último uso),
///     Terminar y Chantaje (marcar/limpiar). Chantaje sin cooldown.
/// </summary>
public static class AbogadoRpc
{
    public static void DifundirEstado(int reuniones, int ultimoUso)
    {
        try
        {
            if (AmongUsClient.Instance.AmHost)
            {
                RpcEstado(PlayerControl.LocalPlayer, reuniones, ultimoUso);
            }
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Abogado] Estado no difundido: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.AbogadoEstado, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcEstado(PlayerControl sender, int reuniones, int ultimoUso)
    {
        try { AbogadoManager.AplicarEstado(reuniones, ultimoUso); }
        catch { }
    }

    public static void EnviarTerminar(PlayerControl abogado)
    {
        try
        {
            RpcTerminar(abogado);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Abogado] Terminar no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.AbogadoTerminar, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcTerminar(PlayerControl abogado)
    {
        var id = abogado?.PlayerId ?? byte.MaxValue;
        try
        {
            // HOST valida el cooldown (única verdad).
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            {
                if (!AbogadoManager.GastarTerminar(id))
                {
                    return; // En espera: no pasó nada.
                }
            }
            else if (abogado?.AmOwner == true)
            {
                // Optimista en su cliente: marca el flag ya (el host
                // confirmará con el estado).
                AbogadoManager.AplicarTerminar();
                return;
            }

            AbogadoManager.AplicarTerminar();
        }
        catch
        {
            // Nunca romper la reunión por Terminar.
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.AbogadoChantaje, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcChantaje(PlayerControl abogado, byte marcadoId)
    {
        try
        {
            if (abogado?.Data?.Role is not Roles.Impostor.ImpostorSupport.AbogadoRole)
            {
                return;
            }

            // HOST valida (vivo, no aliado); los clientes aplican la marca.
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            {
                if (!AbogadoManager.MarcarChantaje(marcadoId))
                {
                    return;
                }
            }
            else
            {
                AbogadoManager.AplicarChantaje(marcadoId);
            }
        }
        catch
        {
            // Nunca romper la ronda por Chantaje.
        }
    }

    /// <summary>Se limpia la marca (difundido por el host al terminar ronda).</summary>
    public static void QuitarChantajeGlobal()
    {
        try
        {
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            {
                RpcQuitarChantaje(PlayerControl.LocalPlayer);
            }
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Abogado] Limpieza de chantaje no difundida: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.AbogadoChantajeLimpiar, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcQuitarChantaje(PlayerControl sender)
    {
        try { AbogadoManager.QuitarChantaje(); }
        catch { }
    }

    public static void EnviarChantaje(PlayerControl abogado, PlayerControl marcado)
    {
        try
        {
            RpcChantaje(abogado, marcado.PlayerId);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Abogado] Chantaje no enviado: {ex.Message}");
        }
    }
}