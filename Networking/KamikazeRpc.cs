using System;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using TownOfPlus.Modules;
using TownOfPlus.Roles.Impostor.ImpostorConcealing;

namespace TownOfPlus.Networking;

/// <summary>
///     RPCs del Kamikaze. Todas validan que el remitente sea realmente un
///     Kamikaze para evitar envíos maliciosos.
/// </summary>
public static class KamikazeRpc
{
    private static bool RemitenteValido(PlayerControl? jugador)
    {
        // Anticheat de TOU Mira: cualquier RPC custom recibido/enviado en el
        // LOBBY dispara el aviso de "cheating" del host (patrón Puppeteer/etc:
        // LobbyBehaviour => rechazar). Las habilidades del Kamikaze solo
        // existen dentro de la partida.
        if (LobbyBehaviour.Instance != null)
        {
            return false;
        }

        return jugador != null && jugador.Data != null && jugador.Data.Role is KamikazeRole;
    }

    /// <summary>
    ///     RPC del ciclo Engaño: sincroniza el índice del ciclo en todos los
    ///     clientes (1→2→3→4→1…). El efecto concreto llega por su RPC propia.
    /// </summary>
    public static void EnviarEngano(PlayerControl kamikaze, byte habilidad)
    {
        try
        {
            RpcEngano(kamikaze, habilidad);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Kamikaze] RPC Engaño no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.KamikazeEngano, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcEngano(PlayerControl sender, byte habilidad)
    {
        if (!RemitenteValido(sender))
        {
            return;
        }

        KamikazeManager.IndiceCiclo = habilidad % 4;
    }

    /// <summary>Cambiaformas: transforma al Kamikaze en un jugador vivo aleatorio.</summary>
    public static void EnviarCambiaformas(PlayerControl kamikaze, PlayerControl objetivo)
    {
        try
        {
            RpcCambiaformas(kamikaze, objetivo);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Kamikaze] RPC Cambiaformas no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.KamikazeCambiaformas, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcCambiaformas(PlayerControl sender, PlayerControl objetivo)
    {
        if (!RemitenteValido(sender) || objetivo == null)
        {
            return;
        }

        KamikazeManager.AplicarShift(sender, objetivo);
    }

    /// <summary>Camuflaje: el Kamikaze se vuelve invisible 1 segundo en todos los clientes.</summary>
    public static void EnviarCamuflaje(PlayerControl kamikaze)
    {
        try
        {
            RpcCamuflaje(kamikaze);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Kamikaze] RPC Camuflaje no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.KamikazeCamuflaje, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcCamuflaje(PlayerControl sender)
    {
        if (!RemitenteValido(sender))
        {
            return;
        }

        KamikazeManager.Camuflaje(sender);
    }

    /// <summary>Ducto falso: coloca el sprite del ducto en todos los clientes.</summary>
    public static void EnviarDuctoFalso(PlayerControl kamikaze, UnityEngine.Vector2 posicion)
    {
        try
        {
            RpcDuctoFalso(kamikaze, posicion);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Kamikaze] RPC Ducto Falso no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.KamikazeDuctoFalso, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcDuctoFalso(PlayerControl sender, UnityEngine.Vector2 posicion)
    {
        if (!RemitenteValido(sender))
        {
            return;
        }

        KamikazeManager.DuctoFalso(sender, posicion);
    }

    /// <summary>Arrastre: el Kamikaze arrastra un cuerpo durante un máximo de 3 segundos.</summary>
    public static void EnviarArrastre(PlayerControl kamikaze, byte bodyId)
    {
        try
        {
            RpcArrastre(kamikaze, bodyId);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Kamikaze] RPC Arrastre no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.KamikazeArrastre, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcArrastre(PlayerControl sender, byte bodyId)
    {
        if (!RemitenteValido(sender))
        {
            return;
        }

        KamikazeManager.IniciarArrastre(sender, bodyId);
    }

    /// <summary>
    ///     Victoria por expulsión en la reunión N: sincroniza la detonación a
    ///     todos los clientes y (solo host) dispara el game over de impostores.
    /// </summary>
    public static void EnviarDetonacion(PlayerControl kamikaze)
    {
        KamikazeManager.AplicarDetonacion(kamikaze);

        try
        {
            RpcDetonacion(kamikaze);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Kamikaze] RPC Detonación no enviada: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.KamikazeDetonacion, LocalHandling = RpcLocalHandling.None)]
    public static void RpcDetonacion(PlayerControl sender)
    {
        if (LobbyBehaviour.Instance != null)
        {
            return;
        }

        if (sender == null || sender.Data == null ||
            sender.Data.Role is not KamikazeRole)
        {
            return;
        }

        KamikazeManager.AplicarDetonacion(sender);
    }
}