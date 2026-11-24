using System.Collections.Generic;
using System.Linq;
using MiraAPI.GameOptions;
using TownOfPlus.Options.Crewmate.CrewmateAfterlife;
using TownOfPlus.Roles.Crewmate.CrewmateAfterlife;
using UnityEngine;

namespace TownOfPlus.Modules;

public static class CerrajeroManager
{
    private static PlayerControl? _jugadorLocal;

    public static void Reiniciar(PlayerControl player)
    {
        _jugadorLocal = player;
    }

    public static void Salir(PlayerControl player)
    {
        if (player == _jugadorLocal)
        {
            _jugadorLocal = null;
        }
    }

    public static bool EsCerrajeroLocal
    {
        get
        {
            var local = PlayerControl.LocalPlayer;
            return local != null && local.Data != null && local.Data.Role is CerrajeroRole;
        }
    }

    public static void Actualizar()
    {
    }

    /// <summary>
    ///     Busca la PUERTA MÁS CERCANA al Cerrajero dentro del radio del rol.
    ///     Comportamiento POR PUERTA (nada de salas enteras):
    ///     - filtroAbiertas == false → busca puertas CERRADAS (botón Abrir).
    ///     - filtroAbiertas == true → busca puertas ABIERTAS (botón Cerrar).
    ///     - filtroAbiertas == null → cualquier puerta en rango.
    /// </summary>
    public static OpenableDoor? ObtenerPuertaCercana(bool? filtroAbiertas)
    {
        if (_jugadorLocal == null || ShipStatus.Instance == null)
        {
            return null;
        }

        var pos = _jugadorLocal.GetTruePosition();
        var radio = OptionGroupSingleton<CerrajeroOptions>.Instance.SearchRadius.Value;

        OpenableDoor? mejor = null;
        var mejorDist = float.MaxValue;

        foreach (var door in ShipStatus.Instance.AllDoors)
        {
            if (door == null)
            {
                continue;
            }

            if (filtroAbiertas.HasValue && door.IsOpen != filtroAbiertas.Value)
            {
                continue;
            }

            var dist = Vector2.Distance(pos, door.transform.position);
            if (dist <= radio && dist < mejorDist)
            {
                mejorDist = dist;
                mejor = door;
            }
        }

        return mejor;
    }

    /// <summary>
    ///     Localiza una puerta por su Id en el sistema REAL de puertas del mapa.
    /// </summary>
    public static OpenableDoor? PuertaPorId(byte doorId)
    {
        var ship = ShipStatus.Instance;
        if (ship == null || ship.AllDoors == null)
        {
            return null;
        }

        foreach (var door in ship.AllDoors)
        {
            if (door != null && door.Id == doorId)
            {
                return door;
            }
        }

        return null;
    }
}
