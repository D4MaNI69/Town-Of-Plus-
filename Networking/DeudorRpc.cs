using System;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using TownOfPlus.Modules;
using TownOfPlus.Roles.Crewmate.CrewmatePower;

namespace TownOfPlus.Networking;

/// <summary>
///     RPCs del Deudor. El host es la única verdad de las deudas y de la
///     redirección de votos; los clientes solo avisan de sus propias acciones.
///     Todas las llamadas se bloquean en el lobby (el anticheat de TOU Mira
///     marca los RPC custom en lobby como cheating).
/// </summary>
public static class DeudorRpc
{
    private static bool EnPartida() => LobbyBehaviour.Instance == null;

    /// <summary>Trato: el Deudor marca a un jugador vivo (ronda).</summary>
    public static void EnviarTrato(PlayerControl deudor, PlayerControl objetivo)
    {
        try
        {
            RpcTrato(deudor, objetivo);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Deudor] RPC Trato no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.DeudorTrato, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcTrato(PlayerControl deudor, PlayerControl objetivo)
    {
        if (!EnPartida() || deudor?.Data == null || objetivo?.Data == null)
        {
            return;
        }

        // Solo un Deudor real puede marcar, y solo a un jugador vivo distinto.
        if (deudor.Data.Role is not DeudorRole || deudor.Data.IsDead)
        {
            return;
        }

        if (objetivo.Data.IsDead || objetivo.Data.Disconnected ||
            deudor.PlayerId == objetivo.PlayerId)
        {
            return;
        }

        DeudorManager.Marcar(deudor, objetivo);
    }

    /// <summary>
    ///     Aviso de que un jugador marcado usó una habilidad (kill vanilla o
    ///     botón MiraAPI). Lo procesa SOLO el host, que luego difunde el favor.
    /// </summary>
    public static void EnviarAvisoUso(PlayerControl objetivo)
    {
        try
        {
            RpcAvisoUso(objetivo);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Deudor] RPC AvisoUso no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.DeudorUsoHabilidad, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcAvisoUso(PlayerControl objetivo)
    {
        if (!EnPartida() || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        DeudorManager.HostProcesarUso(objetivo);
    }

    /// <summary>El host difunde que un marcado pasó a deber un favor.</summary>
    public static void EnviarFavor(PlayerControl objetivo, byte acreedorId)
    {
        try
        {
            RpcFavor(objetivo, acreedorId);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Deudor] RPC Favor no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.DeudorFavor, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcFavor(PlayerControl objetivo, byte acreedorId)
    {
        if (!EnPartida())
        {
            return;
        }

        DeudorManager.OtorgarFavor(objetivo, acreedorId);
    }
}
