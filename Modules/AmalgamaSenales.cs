using System.Collections.Generic;
using MiraAPI.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TownOfPlus.Modules;

/// <summary>
///     Señales de cuerpo de la Amalgama (afterlife neutral), copiadas del
///     ESTILO del Amnesiac de TOU Mira (ArrowDeadBodyModifier: una flecha por
///     cuerpo), pero ejecutadas en LOCAL y sin modifiers:
///     <para>· Flecha GRIS que apunta a cada cuerpo válido (no reportado).</para>
///     <para>· Parpadeo GRIS semitransparente sobre el cuerpo/área (copia de sus
///     sprites, sin tocar los renderers originales del cadáver).</para>
///     NADA de esto lo ve nadie más que el propio Amalgama: los objetos se crean
///     solo en su cliente y se destruyen al salir del estado (reunión, revive,
///     fin de partida o deinitialize del rol).
/// </summary>
public static class AmalgamaSenales
{
    /// <summary>Velocidad del parpadeo gris (radianes por segundo).</summary>
    private const float VelocidadParpadeo = 5f;

    /// <summary>Opacidad mínima del parpadeo gris.</summary>
    private const float AlphaMin = 0.12f;

    /// <summary>Opacidad máxima del parpadeo gris.</summary>
    private const float AlphaMax = 0.42f;

    /// <summary>Gris de las señales (#8C8C8C aprox., flecha y parpadeo).</summary>
    private static readonly Color Gris = new(0.55f, 0.55f, 0.55f, 1f);

    /// <summary>Señal activa: el cuerpo, su flecha gris y su parpadeo gris.</summary>
    private sealed class Senal
    {
        public DeadBody Cuerpo = null!;
        public ArrowBehaviour? Flecha;
        public readonly List<SpriteRenderer> Brillos = new();
    }

    private static readonly List<Senal> Activas = new();

    /// <summary>
    ///     Motor por frame (HudManager.Update en el cliente del Amalgama): crea,
    ///     actualiza y limpia las señales de los cuerpos válidos.
    /// </summary>
    public static void Actualizar()
    {
        if (!DebeMostrar())
        {
            Limpiar();
            return;
        }

        try
        {
            // 1) Cuerpos VÁLIDOS para revivir (mismo criterio que el botón:
            //    no reportados y activos).
            var cuerpos = new List<DeadBody>();
            foreach (var cuerpo in Object.FindObjectsOfType<DeadBody>())
            {
                if (EsCuerpoValido(cuerpo))
                {
                    cuerpos.Add(cuerpo);
                }
            }

            // 2) Quitar las señales de cuerpos que ya no valen (reportados o
            //    destruidos).
            for (var i = Activas.Count - 1; i >= 0; i--)
            {
                var senal = Activas[i];
                if (senal.Cuerpo == null || !cuerpos.Contains(senal.Cuerpo))
                {
                    Destruir(senal);
                    Activas.RemoveAt(i);
                }
            }

            // 3) Crear la señal (flecha gris + parpadeo gris) de los cuerpos
            //    nuevos.
            foreach (var cuerpo in cuerpos)
            {
                if (Activas.Exists(s => s.Cuerpo == cuerpo))
                {
                    continue;
                }

                var senal = Crear(cuerpo);
                if (senal != null)
                {
                    Activas.Add(senal);
                }
            }

            // 4) Apuntar las flechas al cuerpo y animar el parpadeo gris.
            var alpha = Mathf.Lerp(AlphaMin, AlphaMax,
                0.5f + (0.5f * Mathf.Sin(Time.time * VelocidadParpadeo)));
            foreach (var senal in Activas)
            {
                if (senal.Flecha != null && senal.Cuerpo != null)
                {
                    senal.Flecha.target = senal.Cuerpo.TruePosition;
                    senal.Flecha.Update();
                }

                foreach (var brillo in senal.Brillos)
                {
                    if (brillo != null)
                    {
                        brillo.color = new Color(Gris.r, Gris.g, Gris.b, alpha);
                    }
                }
            }
        }
        catch
        {
            // La señal es cosmética: nunca rompe el HUD.
        }
    }

    /// <summary>Destruye todas las señales (reunión, revive, fin de partida...).</summary>
    public static void Limpiar()
    {
        foreach (var senal in Activas)
        {
            Destruir(senal);
        }

        Activas.Clear();
    }
    private static bool DebeMostrar()
    {
        if (MeetingHud.Instance != null || ExileController.Instance != null || LobbyBehaviour.Instance != null)
        {
            return false;
        }

        var local = PlayerControl.LocalPlayer;
        if (local == null || local.Data == null)
        {
            return false;
        }

        // Solo el Amalgama local en su estado afterlife (muerto y con el rol).
        return AmalgamaManager.EsAmalgama(local) && local.Data.IsDead;
    }

    private static bool EsCuerpoValido(DeadBody? cuerpo)
    {
        if (cuerpo == null || !cuerpo.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (cuerpo.Reported)
        {
            return false;
        }

        return true;
    }

    private static Senal? Crear(DeadBody cuerpo)
    {
        try
        {
            var senal = new Senal { Cuerpo = cuerpo };

            // A) Flecha gris estilo Amnesiac (Helpers.CreateArrow).
            try
            {
                var parent = Camera.main != null ? Camera.main.transform : PlayerControl.LocalPlayer.transform; var flecha = Helpers.CreateArrow(parent, Gris);
                if (flecha != null)
                {
                    flecha.target = cuerpo.TruePosition;
                    senal.Flecha = flecha;
                }
            }
            catch
            {
                senal.Flecha = null;
            }

            // B) Parpadeo gris transparente sobre el cuerpo: clones de los renderers
            // del cadáver en un GameObject hijo dedicado.
            try
            {
                var contenedor = new GameObject("TOP_AmalgamaBrilloCuerpo");
                contenedor.transform.SetParent(cuerpo.transform, false);

                foreach (var original in cuerpo.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (original == null || original.sprite == null)
                    {
                        continue;
                    }

                    var clonGo = new GameObject("Brillo_" + original.gameObject.name);
                    clonGo.transform.SetParent(contenedor.transform, false);
                    clonGo.transform.localPosition = original.transform.localPosition;
                    clonGo.transform.localRotation = original.transform.localRotation;
                    clonGo.transform.localScale = original.transform.localScale;

                    var clonRenderer = clonGo.AddComponent<SpriteRenderer>();
                    clonRenderer.sprite = original.sprite;
                    clonRenderer.material = original.material;
                    clonRenderer.sortingLayerID = original.sortingLayerID;
                    clonRenderer.sortingOrder = original.sortingOrder + 1;
                    clonRenderer.color = new Color(Gris.r, Gris.g, Gris.b, AlphaMin);

                    senal.Brillos.Add(clonRenderer);
                }
            }
            catch
            {
                // Si falla el parpadeo del sprite, la flecha sigue funcionando.
            }

            return senal;
        }
        catch
        {
            return null;
        }
    }

    private static void Destruir(Senal senal)
    {
        try
        {
            if (senal.Flecha != null && senal.Flecha.gameObject != null)
            {
                Object.Destroy(senal.Flecha.gameObject);
            }
        }
        catch
        {
        }

        foreach (var brillo in senal.Brillos)
        {
            try
            {
                if (brillo != null && brillo.gameObject != null)
                {
                    Object.Destroy(brillo.gameObject);
                }
            }
            catch
            {
            }
        }

        senal.Brillos.Clear();
    }

}
