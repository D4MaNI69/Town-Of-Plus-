using System;
using System.Collections;
using System.Linq;
using AmongUs.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using Reactor.Utilities;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Roles.Neutral.NeutralAfterlife;
using TownOfUs.Modules;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Networking;

/// <summary>
///     RPCs de la Amalgama (afterlife neutral). Flujo del revive:
///     1) El cliente local canaliza N segundos (AmalgamaManager).
///     2) Al completar pide al host revivir (PedirRevivir / RpcPedirRevivir).
///     3) El HOST elige un rol ALEATORIO del pool de roles habilitados
///        (cantidad ≥ 1), lee la posición del cuerpo y difunde
///        RpcCompletarRevivir a TODOS los clientes (LocalHandling.Before:
///        también corre en el host). NUNCA copia la apariencia del cadáver:
///        la Amalgama conserva su CUERPO ORIGINAL al revivir.
///     4) En CADA cliente se usa el canal OFICIAL de revive de TOU Mira
///        (el flujo del Altruist / ReviveUtilities.RevivePlayer, sin el gate
///        HasDied): limpia el historial de muertes, REVIVE al jugador
///        (el juego vuelve a detectarlo vivo: asiste a reuniones y vota),
///        asigna el rol nuevo con ChangeRole local en cada cliente y
///        conserva el cuerpo original.
/// </summary>
public static class AmalgamaRpc
{
    /// <summary>Pide al host revivir en el cuerpo (fin de canalización local).</summary>
    public static void PedirRevivir(PlayerControl amalgama, byte bodyParentId)
    {
        try
        {
            if (AmongUsClient.Instance.AmHost)
            {
                ElegirYCompletar(amalgama, bodyParentId);
                return;
            }

            RpcPedirRevivir(amalgama, bodyParentId);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Amalgama] Petición de revive no enviada: {ex.Message}");
        }
    }

    /// <summary>Petición de revive (cliente → host). Solo el host procesa.</summary>
    [MethodRpc((uint)TownOfPlusRpcCalls.AmalgamaPedirRevivir, LocalHandling = RpcLocalHandling.None)]
    public static void RpcPedirRevivir(PlayerControl sender, byte bodyParentId)
    {
        if (sender == null || !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        ElegirYCompletar(sender, bodyParentId);
    }

    /// <summary>Host: elige el rol aleatorio del pool y difunde la finalización.</summary>
    private static void ElegirYCompletar(PlayerControl amalgama, byte bodyParentId)
    {
        if (amalgama == null || !AmalgamaManager.EsAmalgama(amalgama))
        {
            return;
        }

        var rol = AmalgamaManager.ElegirRolAleatorio();
        if (rol == null)
        {
            return;
        }

        // Posición del cuerpo ANTES de destruirlo (el color/apariencia es el
        // PROPIO de la Amalgama: no se copia el del cadáver).
        var cuerpo = AmalgamaManager.ObtenerCuerpo(bodyParentId);
        var posicion = cuerpo != null ? cuerpo.TruePosition : (Vector2)amalgama.transform.position;

        var esCustom = CustomRoleUtils.IsCustomRole(rol);
        var roleId = esCustom ? RoleId.Get(rol.GetType()) : (ushort)rol.Role;

        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Amalgama] Host completa revive de {amalgama.Data?.PlayerName} " +
            $"→ rol {(esCustom ? "custom" : "vanilla")} {rol.GetType().Name}.");

        RpcCompletarRevivir(amalgama, bodyParentId, roleId, esCustom, posicion.x, posicion.y);
    }

    /// <summary>
    ///     Finalización del revive (corre en TODOS los clientes, incluido el
    ///     emisor/host). Replica EL FLUJO OFICIAL del Altruist
    ///     (ReviveUtilities.RevivePlayer, el mismo que usa TOU Mira sin
    ///     reportes de cheater):
    ///     - Destruye el cadáver usado como catalizador.
    ///     - Limpia el historial de muerte (ClearMurder).
    ///     - REVIVE al jugador (pasa de fantasma a VIVO).
    ///     - Cambia al rol aleatorio del pool con ChangeRole LOCAL en cada
    ///       cliente (sincronización completa), NO con un RPC por jugador.
    ///     - CONSERVA su CUERPO ORIGINAL (color/skin/cosméticos/nombre propios;
    ///       NO se copia la apariencia del cadáver).
    ///     NOTA: ReviveUtilities.RevivePlayer aborta con el gate "HasDied", que
    ///     en una Amalgama (rol afterlife) es false; por eso se replica su
    ///     lógica exacta sin ese gate. Cada RPC de posición/cosmética solo lo
    ///     envía el dueño (anticheat-safe).
    /// </summary>
    [MethodRpc((uint)TownOfPlusRpcCalls.AmalgamaCompletarRevivir, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcCompletarRevivir(PlayerControl amalgama, byte bodyParentId, ushort roleId, bool esCustom, float posX, float posY)
    {
        if (amalgama == null || amalgama.Data == null || amalgama.Data.Disconnected)
        {
            return;
        }

        // 1) El cadáver usado como catalizador desaparece (local, cosmético).
        AmalgamaManager.DestruirCuerpo(bodyParentId);

        // 2) Limpiar el historial de muerte (igual que ReviveUtilities).
        try
        {
            GameHistory.ClearMurder(amalgama);
        }
        catch
        {
            // Sin historial no pasa nada.
        }

        var inMeetingOrExile = MeetingHud.Instance != null || ExileController.Instance != null;

        // 3) REVIVE REAL a VIVO (patrón ReviveUtilities). Se conserva el
        //    color/apariencia ORIGINALES de la Amalgama.
        try
        {
            amalgama.Revive();
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Amalgama] Revive() falló: {ex.Message}");
        }

        // 4) Posición del cadáver catalizador. Anticheat-safe: SOLO el dueño
        //    envía RpcSnapTo (patrón ReviveUtilities); el resto solo aplica la
        //    posición local (el NetTransform la sincroniza en el siguiente tick).
        if (!inMeetingOrExile)
        {
            var position = new Vector2(posX, posY);
            amalgama.transform.position = position;

            if (amalgama.AmOwner)
            {
                try
                {
                    amalgama.NetTransform.RpcSnapTo(position);
                }
                catch
                {
                    // Sin snap no se rompe el revive.
                }
            }

            try
            {
                if (amalgama.MyPhysics?.body != null)
                {
                    amalgama.MyPhysics.body.position = position;
                    Physics2D.SyncTransforms();
                }
            }
            catch
            {
                // Sin física no pasa nada.
            }
        }

        // 5) Asignar el rol aleatorio del pool. El HOST lo hace con
        //    RpcChangeRole de TOU Mira (el canal OFICIAL que usan todos los
        //    afterlife: ConvertirAfterlife/ConvertirSaboteador). Se replica a
        //    TODOS los clientes y funciona tanto para roles vanilla como
        //    custom (RoleId.Get / CustomRoleManager). El remitente se
        //    sincroniza como el resto de clientes vía su propio RPC, sin
        //    duplicar envíos → anticheat-safe.
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
        {
            try
            {
                amalgama.RpcChangeRole(roleId, true);
            }
            catch (Exception ex)
            {
                TownOfPlusPlugin.Instance?.Log.LogWarning(
                    $"[Amalgama] Cambio de rol (RpcChangeRole) falló: {ex.Message}");
            }

            // 5a) +1s DESPUÉS del rol: UN modifier aleatorio de la MISMA
            //     facción que el rol nuevo, SOLO de modifiers habilitados en
            //     la sala con cantidad > 0. Sin modifier válido, nada.
            Coroutines.Start(CoModifierPostRol(amalgama, roleId, esCustom));
        }

        // 6) Restaurar visibilidad y colisión VIVA. Se conserva SIEMPRE el
        //    cuerpo/apariencia ORIGINAL de la Amalgama (no se copia el cadáver).
        AmalgamaManager.ResetearJugadorAVisible(amalgama);
        AmalgamaManager.RestaurarColisionViva(amalgama);

        // 7) Aviso al revivido (solo owner; mantiene SU cuerpo, rol nuevo).
        if (amalgama.AmOwner)
        {
            try
            {
                Helpers.CreateAndShowNotification(
                    "<b>¡Has vuelto a la vida!</b>\n" +
                    "Recibes un rol nuevo. Quedas vivo " +
                    "hasta que te maten o expulsen de nuevo.",
                    Color.white, new Vector3(0f, 1f, -20f),
                    spr: TownofplusAssets.AmalgamaIcon.LoadAsset());
            }
            catch
            {
                // Sin notificación no pasa nada.
            }
        }
    }

    /// <summary>
    ///     HOST: espera 1 segundo tras asignar el rol aleatorio del revive y
    ///     asigna UN modifier aleatorio de la MISMA facción (crew / imp /
    ///     neutral) del pool de modifiers habilitados en la sala (cantidad &gt; 0),
    ///     vía el pipeline oficial de MiraAPI (PlayerControl.RpcAddModifier).
    ///     Si no hay modifier válido de esa facción, NO se asigna nada.
    /// </summary>
    private static IEnumerator CoModifierPostRol(PlayerControl amalgama, ushort roleId, bool esCustom)
    {
        yield return new WaitForSeconds(1f);

        // Cancelaciones seguras: desconectado, muerto de nuevo o reunión en curso.
        if (amalgama == null || amalgama.Data == null || amalgama.Data.IsDead ||
            amalgama.Data.Disconnected || MeetingHud.Instance != null || ExileController.Instance != null)
        {
            yield break;
        }

        var rolNuevo = ResolverRol(roleId, esCustom);
        if (rolNuevo == null)
        {
            yield break;
        }

        var modType = AmalgamaManager.ElegirModifierAleatorio(rolNuevo);
        if (modType == null)
        {
            yield break;
        }

        try
        {
            // Canal oficial de MiraAPI: RPC sincronizado a todos los clientes.
            amalgama.RpcAddModifier(modType);

            if (amalgama.AmOwner)
            {
                Helpers.CreateAndShowNotification(
                    "<b>¡Destino sellado!</b>\nRecibiste un modificador aleatorio junto a tu rol nuevo.",
                    Color.white, new Vector3(0f, 1f, -20f),
                    spr: TownofplusAssets.AmalgamaIcon.LoadAsset());
            }
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Amalgama] Asignación del modifier post-rol falló: {ex.Message}");
        }
    }

    /// <summary>Resuelve el RoleBehaviour asignado en el revive (custom o vanilla).</summary>
    private static RoleBehaviour? ResolverRol(ushort roleId, bool esCustom)
    {
        try
        {
            if (esCustom)
            {
                return CustomRoleManager.AllRoles.FirstOrDefault(x =>
                    (ushort)RoleId.Get(x.GetType()) == roleId);
            }

            foreach (var rol in RoleManager.Instance.AllRoles)
            {
                if (rol != null && (ushort)rol.Role == roleId)
                {
                    return rol;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}