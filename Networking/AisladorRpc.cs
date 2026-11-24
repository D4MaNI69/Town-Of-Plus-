using System;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using TownOfPlus.Modules;
using TownOfPlus.Roles.Crewmate.CrewmateSupport;

namespace TownOfPlus.Networking;

/// <summary>
///     RPCs del Aislador. Todas validan que el remitente sea realmente un
///     Aislador para evitar envíos maliciosos.
/// </summary>
public static class AisladorRpc
{
    private static bool RemitenteValido(PlayerControl? jugador)
    {
        // Anticheat de TOU Mira: los RPC custom en el LOBBY disparan el aviso
        // de "cheating". Las habilidades del Aislador solo existen en partida.
        if (LobbyBehaviour.Instance != null)
        {
            return false;
        }

        return jugador != null && jugador.Data != null && jugador.Data.Role is AisladorRole;
    }

    /// <summary>Soplar: marca a un jugador vivo (tinte solo para el Aislador).</summary>
    public static void EnviarMarcar(PlayerControl aislador, PlayerControl objetivo)
    {
        try
        {
            RpcMarcar(aislador, objetivo);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Aislador] RPC Marcar no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.AisladorMarcar, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcMarcar(PlayerControl sender, PlayerControl objetivo)
    {
        if (!RemitenteValido(sender))
        {
            return;
        }

        AisladorManager.Marcar(sender, objetivo);
    }

    /// <summary>Burbuja: encierra a todos los marcados 3 s y limpia las marcas.</summary>
    public static void EnviarBurbuja(PlayerControl aislador)
    {
        try
        {
            RpcBurbuja(aislador);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Aislador] RPC Burbuja no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.AisladorBurbuja, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcBurbuja(PlayerControl sender)
    {
        if (!RemitenteValido(sender))
        {
            return;
        }

        AisladorManager.AplicarBurbuja(sender);
    }

    /// <summary>Escudo: burbuja que sigue al Aislador e inmunidad a kills.</summary>
    public static void EnviarEscudo(PlayerControl aislador)
    {
        try
        {
            RpcEscudo(aislador);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Aislador] RPC Escudo no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.AisladorEscudo, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcEscudo(PlayerControl sender)
    {
        if (!RemitenteValido(sender))
        {
            return;
        }

        AisladorManager.AplicarEscudo(sender);
    }
}
