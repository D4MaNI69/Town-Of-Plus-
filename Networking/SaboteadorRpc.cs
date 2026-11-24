using System;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using TownOfPlus.Modules;
using TownOfPlus.Roles.Impostor.ImpostorAfterlife;

namespace TownOfPlus.Networking;

/// <summary>
///     RPCs del Saboteador. Valida que el remitente sea realmente un
///     Saboteador para evitar envíos maliciosos.
/// </summary>
public static class SaboteadorRpc
{
    /// <summary>
    ///     Luces Fuera: sincroniza el apagón en TODOS los clientes. El efecto
    ///     es global y de duración fija enviada por el Saboteador.
    /// </summary>
    public static void EnviarLucesFuera(PlayerControl saboteador, float duracion)
    {
        // NO se aplica localmente aquí: el RPC usa LocalHandling.Before, que
        // EJECUTA el handler también en este cliente antes de enviarlo. Llamar
        // ApagarLuces aquí además causaba el MENSAJE/flash duplicado.
        try
        {
            RpcLucesFuera(saboteador, duracion);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Saboteador] RPC Luces Fuera no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.SaboteadorLucesFuera, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcLucesFuera(PlayerControl sender, float duracion)
    {
        if (sender == null || sender.Data == null ||
            sender.Data.Role is not SaboteadorRole)
        {
            return;
        }

        SaboteadorManager.ApagarLuces(duracion);
    }
}
