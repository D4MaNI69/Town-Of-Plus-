using System;
using System.Collections.Generic;
using MiraAPI.Utilities;
using Reactor.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TownOfPlus.Modules;

/// <summary>
///     Flechas rojas del disparo del Francotirador: en el momento del disparo
///     (cuando suena el disparo global) TODOS los clientes crean una flecha
///     roja individual (estilo Titiritero de TOU Mira) que apunta hacia la
///     posición del francotirador, para que todo el mundo sepa de dónde vino
///     el tiro. Cada flecha apunta en vivo (sigue al tirador si se mueve) y
///     desaparece al expirar su tiempo.
/// </summary>
public static class FlechasDisparo
{
    /// <summary>Segundos que dura la flecha en pantalla.</summary>
    private const float Duracion = 3f;

    private static readonly List<(byte TiradorId, ArrowBehaviour Flecha, float Expira)> Activas = new();

    /// <summary>Crea una flecha roja hacia el francotirador indicado (en ESTE cliente).</summary>
    public static void Crear(byte francotiradorId)
    {
        try
        {
            if (Camera.main == null || Resolver(francotiradorId) == null)
            {
                return;
            }

            var flecha = Helpers.CreateArrow(Camera.main.transform, Palette.ImpostorRed);
            if (flecha == null)
            {
                return;
            }

            Activas.Add((francotiradorId, flecha, Time.time + Duracion));
        }
        catch
        {
            // La flecha es cosmética: jamás rompe el disparo.
        }
    }

    /// <summary>Actualiza objetivos y expiración (llamar cada frame desde HudManager.Update).</summary>
    public static void Actualizar()
    {
        if (Activas.Count == 0)
        {
            return;
        }

        try
        {
            for (var i = Activas.Count - 1; i >= 0; i--)
            {
                var (tiradorId, flecha, expira) = Activas[i];
                if (flecha == null || Time.time > expira)
                {
                    if (flecha != null)
                    {
                        Object.Destroy(flecha.gameObject);
                    }

                    Activas.RemoveAt(i);
                    continue;
                }

                var tirador = Resolver(tiradorId);
                if (tirador == null)
                {
                    Object.Destroy(flecha.gameObject);
                    Activas.RemoveAt(i);
                    continue;
                }

                flecha.target = tirador.GetTruePosition();
                flecha.Update();
            }
        }
        catch
        {
            // Cosmético: ignorar cualquier fallo de frame.
        }
    }

    /// <summary>Destruye todas las flechas (inicio de reunión / fin de partida).</summary>
    public static void Limpiar()
    {
        foreach (var (_, flecha, _) in Activas)
        {
            try
            {
                if (flecha != null)
                {
                    Object.Destroy(flecha.gameObject);
                }
            }
            catch
            {
                // Ignorar.
            }
        }

        Activas.Clear();
    }

    private static PlayerControl? Resolver(byte id)
    {
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p != null && p.PlayerId == id)
            {
                return p;
            }
        }

        return null;
    }
}