using System;
using System.Linq;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using TownOfPlus.Modules;
using TownOfPlus.Roles.Crewmate.CrewmateSupport;

namespace TownOfPlus.Networking;

/// <summary>
///     RPCs del rol Víctima:
///     - VictimaNota: la Víctima guarda una nota (lista interna en TODOS los
///       clientes; nadie la ve todavía). Lo envía la propia Víctima.
///     - VictimaRevelarNotas: el HOST revela todas las notas de una Víctima
///       muerta/expulsada al comenzar la reunión posterior a su muerte
///       (alerta afterlife + burbuja con las notas, visibles para todos).
/// </summary>
public static class VictimaRpc
{
    /// <summary>
    ///     La Víctima local guarda una nota. Se añade primero en local (el
    ///     RPC no vuelve al emisor) y luego se sincroniza al resto.
    /// </summary>
    public static void EnviarNota(PlayerControl victima, string nota)
    {
        VictimaManager.GuardarNota(victima, nota);

        try
        {
            RpcNota(victima, nota);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Victima] RPC Nota no enviado: {ex.Message}");
        }
    }

    /// <summary>Se ejecuta en cada cliente remoto: guarda la nota en la lista persistente.</summary>
    [MethodRpc((uint)TownOfPlusRpcCalls.VictimaNota, LocalHandling = RpcLocalHandling.None)]
    public static void RpcNota(PlayerControl victima, string nota)
    {
        // Se acepta si es la propia Víctima O si ya tiene notas guardadas
        // (para no perder nada aunque el chequeo de rol vivo falle).
        if (victima == null ||
            (!VictimaManager.EsVictima(victima) && VictimaManager.NotasDe(victima).Count == 0))
        {
            return;
        }

        VictimaManager.GuardarNota(victima, nota);
    }

    /// <summary>
    ///     El host revela las notas de la Víctima muerta/expulsada. Se muestra en
    ///     TODOS los jugadores. Si no hay notas, se envía igualmente la
    ///     revelación para que salga el AVISO de que era la Víctima.
    /// </summary>
    public static void EnviarRevelacion(PlayerControl victima, System.Collections.Generic.List<string> notas)
    {
        // El host también muestra en local (el RPC no vuelve a él).
        VictimaManager.RevelarLocal(victima, notas);

        var serializado = string.Join("\n", notas);

        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Victima] RPC Revelación: enviando {notas.Count} nota(s) a todos.");

        try
        {
            RpcRevelar(victima, serializado);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Victima] RPC Revelación no enviado: {ex.Message}");
        }
    }

    /// <summary>Se ejecuta en cada cliente remoto: aviso + burbuja con las notas.</summary>
    [MethodRpc((uint)TownOfPlusRpcCalls.VictimaRevelarNotas, LocalHandling = RpcLocalHandling.None)]
    public static void RpcRevelar(PlayerControl victima, string notasSerializadas)
    {
        if (victima == null)
        {
            return;
        }

        // Sin notas: solo se revela que era la Víctima (aviso del rol).
        var notas = string.IsNullOrEmpty(notasSerializadas)
            ? new System.Collections.Generic.List<string>()
            : notasSerializadas.Split('\n').ToList();

        VictimaManager.RevelarLocal(victima, notas);
    }
}