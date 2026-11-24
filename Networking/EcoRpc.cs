using System;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using TownOfPlus.Modules;

namespace TownOfPlus.Networking;

/// <summary>
///     RPCs del Eco (afterlife crew, SOLO muerte en reunión):
///     · AsignarAnclaGlobal (host → todos): el host ya convirtió al Eco con
///       RpcChangeRole y difunde eco + ancla. Cada cliente guarda el par y, si
///       el local es el Eco, reaparece junto a su ancla.
///     · VolverAlAnclaGlobal (host → todos): TP por rango o post-reunión. Solo
///       el DUEÑO del Eco ejecuta el SnapTo (la red lo replica al resto).
/// </summary>
public static class EcoRpc
{
    public static void AsignarAnclaGlobal(PlayerControl eco, PlayerControl ancla)
    {
        try
        {
            if (AmongUsClient.Instance.AmHost)
                RpcAsignarAncla(PlayerControl.LocalPlayer, eco.PlayerId, ancla.PlayerId);
            else
                TownOfPlusPlugin.Instance?.Log.LogWarning("[Eco] Solo el host asigna el ancla.");
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning($"[Eco] Ancla no difundida: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.EcoAsignarAncla, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcAsignarAncla(PlayerControl sender, byte ecoId, byte anclaId)
    {
        try { EcoManager.AplicarAncla(ecoId, anclaId); }
        catch { }
    }

    public static void VolverAlAnclaGlobal(byte ecoId)
    {
        try
        {
            if (AmongUsClient.Instance.AmHost)
                RpcVolverAlAncla(PlayerControl.LocalPlayer, ecoId);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning($"[Eco] TP al ancla no difundido: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.EcoVolverAlAncla, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcVolverAlAncla(PlayerControl sender, byte ecoId)
    {
        try
        {
            var ancla = EcoManager.AnclaDe(ecoId);
            if (ancla.HasValue)
                EcoManager.LlevarJuntoAlAncla(ecoId, ancla.Value);
        }
        catch { }
    }

    /// <summary>
    ///     El ancla murió/se desconectó: se borra el par en TODOS los clientes
    ///     para que dejen de aplicarse los filtros del Eco (chat, visibilidad y
    ///     voz). El cambio de rol a fantasma común lo hace el host con
    ///     RpcChangeRole (va en su propio RPC de TOU Mira/MiraAPI).
    /// </summary>
    public static void DevolverAFantasmaGlobal(byte ecoId)
    {
        try
        {
            if (AmongUsClient.Instance.AmHost)
                RpcDevolverAFantasma(PlayerControl.LocalPlayer, ecoId);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning($"[Eco] Devolución no difundida: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.EcoDevolverAFantasma, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcDevolverAFantasma(PlayerControl sender, byte ecoId)
    {
        try { EcoManager.QuitarEco(ecoId); }
        catch { }
    }
}
