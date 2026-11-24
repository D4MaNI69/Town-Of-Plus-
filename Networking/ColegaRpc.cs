using System;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using TownOfPlus.Modules;
using TownOfPlus.Roles.Crewmate.CrewmateSupport;

namespace TownOfPlus.Networking;

/// <summary>
///     RPCs del Colega. El host es la única verdad de la Esperanza; los
///     clientes solo avisan de sus propias acciones. Todas las llamadas se
///     bloquean en el lobby (el anticheat de TOU Mira marca los RPC custom en
///     lobby como cheating).
/// </summary>
public static class ColegaRpc
{
    private static bool EnPartida() => LobbyBehaviour.Instance == null;

    /// <summary>Acompañar: el Colega marca a un jugador vivo (permanente).</summary>
    public static void EnviarAcompanar(PlayerControl colega, PlayerControl objetivo)
    {
        try
        {
            RpcAcompanar(colega, objetivo);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Colega] RPC Acompañar no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.ColegaAcompanar, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcAcompanar(PlayerControl colega, PlayerControl objetivo)
    {
        if (!EnPartida() || colega?.Data == null || objetivo?.Data == null)
        {
            return;
        }

        // Solo un Colega real puede marcar, y solo una vez en toda la partida.
        if (colega.Data.Role is not ColegaRole || colega.Data.IsDead)
        {
            return;
        }

        if (ColegaManager.TieneMarca(colega.PlayerId))
        {
            return;
        }

        if (objetivo.Data.IsDead || objetivo.Data.Disconnected ||
            colega.PlayerId == objetivo.PlayerId)
        {
            return;
        }

        ColegaManager.Marcar(colega, objetivo);
    }

    /// <summary>
    ///     Aviso de que el marcado usó una habilidad (kill vanilla o botón
    ///     MiraAPI). Solo el cliente del Colega dueño de la marca reacciona.
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
                $"[Colega] RPC AvisoUso no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.ColegaUsoHabilidad, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcAvisoUso(PlayerControl objetivo)
    {
        if (!EnPartida() || objetivo?.Data == null)
        {
            return;
        }

        ColegaManager.OcultarFlecha3s(objetivo.PlayerId);
    }

    /// <summary>El Colega pide usar Esperanza en la reunión (lo valida el host).</summary>
    public static void EnviarEsperanza(PlayerControl colega)
    {
        try
        {
            RpcEsperanza(colega);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Colega] RPC Esperanza no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.ColegaEsperanza, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcEsperanza(PlayerControl colega)
    {
        if (!EnPartida() || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        ColegaManager.HostProcesarEsperanza(colega);
    }

    /// <summary>El host confirma la Esperanza y todos los clientes la aplican.</summary>
    public static void EnviarEsperanzaConfirmada(PlayerControl colega, byte marcaId)
    {
        try
        {
            RpcEsperanzaConfirmada(colega, marcaId);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Colega] RPC EsperanzaConfirmada no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.ColegaEsperanzaConfirmada, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcEsperanzaConfirmada(PlayerControl colega, byte marcaId)
    {
        if (!EnPartida() || colega?.Data == null)
        {
            return;
        }

        // La confirmación solo vale para la marca real de ese Colega.
        ColegaManager.AplicarEsperanza(colega, marcaId);
    }
}
