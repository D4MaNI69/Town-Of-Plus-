using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using Reactor.Utilities;
using TownOfPlus.Modules;
using TownOfPlus.Utilities;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Networking;

public static class ProfesorRpc
{
    public static void EnviarMarcar(PlayerControl profesor, byte targetId)
    {
        try
        {
            if (AmongUsClient.Instance.AmHost)
            {
                FijarMarcadoLocal(profesor, targetId);
                return;
            }

            RpcMarcar(profesor, targetId);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning($"[Profesor] Marcar no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.ProfesorMarcar, LocalHandling = RpcLocalHandling.None)]
    public static void RpcMarcar(PlayerControl profesor, byte targetId)
    {
        if (profesor == null || !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        FijarMarcadoLocal(profesor, targetId);
    }

    private static void FijarMarcadoLocal(PlayerControl profesor, byte targetId)
    {
        // Multi-marcado por cargas: solo registramos el objetivo pendiente; la
        // conversión de rol se resuelve al final de la reunión (AlTerminarReunion).
        ProfesorManager.FijarMarcado(profesor.PlayerId, targetId);
    }

    public static void EnviarOpciones(PlayerControl target, List<RoleBehaviour> opciones)
    {
        try
        {
            var ids = string.Join(",", opciones.Select(r => (ushort)r.Role));
            RpcOpciones(target, ids);
            ProfesorManager.GuardarOpciones(target.PlayerId, opciones);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning($"[Profesor] Opciones no enviadas: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.ProfesorOpciones, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcOpciones(PlayerControl target, string roleIds)
    {
        if (target == null)
        {
            return;
        }

        try
        {
            var lista = new List<RoleBehaviour>();
            foreach (var parte in roleIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!ushort.TryParse(parte, out var id))
                {
                    continue;
                }

                var rol = RoleManager.Instance.GetRole((RoleTypes)id);
                if (rol != null && CambioRolProfesor.EsDraftable(rol))
                {
                    lista.Add(rol);
                }
            }

            if (lista.Count > 0)
            {
                ProfesorManager.GuardarOpciones(target.PlayerId, lista);
                if (target.AmOwner)
                {
                    Helpers.CreateAndShowNotification(
                        "<b>¡Fuiste reentrenado!</b>\nUsa Cambiar Rol para elegir tu nuevo rol.",
                        Color.white, new Vector3(0f, 1f, -20f),
                        spr: SpritesProfesor.IconoRolAsset.LoadAsset());
                }
            }
        }
        catch
        {
        }
    }

    public static void EnviarAvisoFallido(PlayerControl target)
    {
        try
        {
            RpcAvisoFallido(target);
            if (target.AmOwner)
            {
                MostrarAvisoFallido();
            }
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning($"[Profesor] Aviso no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.ProfesorAviso, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcAvisoFallido(PlayerControl target)
    {
        if (target == null || !target.AmOwner)
        {
            return;
        }

        MostrarAvisoFallido();
    }

    private static void MostrarAvisoFallido()
    {
        try
        {
            Coroutines.Start(MiscUtils.CoFlash(Color.white));
            Helpers.CreateAndShowNotification(
                "<b>El Profesor intentó reentrenarte</b>",
                Color.white, new Vector3(0f, 1f, -20f),
                spr: SpritesProfesor.IconoRolAsset.LoadAsset());
        }
        catch
        {
        }
    }

    public static void EnviarCambioRol(PlayerControl jugador, ushort roleId)
    {
        try
        {
            if (AmongUsClient.Instance.AmHost)
            {
                AplicarCambioRol(jugador, roleId);
                return;
            }

            RpcCambioRol(jugador, roleId);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning($"[Profesor] Cambio no enviado: {ex.Message}");
        }
    }

    [MethodRpc((uint)TownOfPlusRpcCalls.ProfesorCambioRol, LocalHandling = RpcLocalHandling.None)]
    public static void RpcCambioRol(PlayerControl jugador, ushort roleId)
    {
        if (jugador == null || !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        AplicarCambioRol(jugador, roleId);
    }

    private static void AplicarCambioRol(PlayerControl jugador, ushort roleId)
    {
        if (jugador?.Data == null || jugador.Data.IsDead)
        {
            return;
        }

        if (!ProfesorManager.ConsumirCambio(jugador.PlayerId, roleId))
        {
            return;
        }

        try
        {
            jugador.RpcChangeRole(roleId, false);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning($"[Profesor] RpcChangeRole fallo: {ex.Message}");
        }
    }
}
