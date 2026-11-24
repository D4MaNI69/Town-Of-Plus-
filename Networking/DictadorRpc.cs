using System;
using System.Linq;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using Reactor.Utilities;
using TownOfPlus.Modules;
using TownOfPlus.Roles.Impostor.ImpostorPower;

namespace TownOfPlus.Networking;

/// <summary>
///     RPC de revelación del Dictador: sincroniza el estado revelado en todos
///     los clientes y reproduce la animación de revelación del Mayor en el
///     votearea del jugador en la reunión.
/// </summary>
public static class DictadorRpc
{
    public static void EnviarReveal(PlayerControl dictador)
    {
        try
        {
            RpcReveal(dictador);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Dictador] RPC Reveal no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.DictadorReveal, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcReveal(PlayerControl dictador)
    {
        if (dictador?.Data?.Role is DictadorRole d)
        {
            d.Revealed = true;
        }

        if (dictador == null || MeetingHud.Instance == null)
        {
            return;
        }

        var voteArea = MeetingHud.Instance.playerStates
            .FirstOrDefault(x => x.PlayerId == dictador.PlayerId);
        if (voteArea != null)
        {
            Coroutines.Start(DictadorManager.CoAnimarReveal(voteArea, dictador));
        }
    }
}