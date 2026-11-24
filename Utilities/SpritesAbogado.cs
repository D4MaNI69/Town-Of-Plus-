using System;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace TownOfPlus.Utilities;

/// <summary>
///     Íconos y botones del Abogado (impostor support): todos los PNG viven en
///     Resources/ del proyecto y, si faltan, se generan por código (mismo patrón
///     que SpritesEco/SpritesProfesor).
///     <para>· Icons/Abogado.png — ícono del rol.</para>
///     <para>· Buttons/Chantaje.png — botón de ronda (marcar jugador).</para>
///     <para>· Buttons/Terminar.png — botón de reunión (forzar skip de todos).</para>
///     El botón de reunión tiene además versión GRIS (en espera).
/// </summary>
public static class SpritesAbogado
{
    /// <summary>Color del nombre del CHANTajeado (#535b61).</summary>
    public static readonly Color32 ColorChantaje = new(0x53, 0x5B, 0x61, 255);

    public static readonly LoadableAsset<Sprite> IconoRolAsset =
        new SpritePersonalizable("Abogado", "TownOfPlus.Resources.Icons.Abogado.png", 200f, IconoRol);

    public static readonly LoadableAsset<Sprite> BotonChantajeAsset =
        new SpritePersonalizable("Chantaje", "TownOfPlus.Resources.Buttons.Chantaje.png", 100f, BotonChantaje);

    public static readonly LoadableAsset<Sprite> BotonTerminarAsset =
        new SpritePersonalizable("Terminar", "TownOfPlus.Resources.Buttons.Terminar.png", 100f, BotonTerminar);

    public static readonly LoadableAsset<Sprite> BotonTerminarGrisAsset =
        new SpritePersonalizable("TerminarGris", string.Empty, 100f, BotonTerminarGris);

    private static readonly Color32 Rojo = Palette.ImpostorRed;
    private static readonly Color32 Gris = new(120, 120, 120, 255);
    private static readonly Color32 Claro = new(255, 255, 255, 255);
    private static readonly Color32 Transparente = new(0, 0, 0, 0);

    /// <summary>Ícono: balanza de la justicia (impostor red).</summary>
    public static Sprite IconoRol()
    {
        var tex = Dibujar(256, px =>
        {
            Linea(px, 256, new(0.5f, 0.20f), new(0.5f, 0.78f), 0.055f, Rojo);
            Linea(px, 256, new(0.34f, 0.20f), new(0.66f, 0.20f), 0.055f, Rojo);
            Linea(px, 256, new(0.22f, 0.70f), new(0.78f, 0.70f), 0.050f, Rojo);
            Arco(px, 256, 0.22f, 0.60f, 0.10f, 0.045f, Rojo, 0f, 180f);
            Arco(px, 256, 0.78f, 0.60f, 0.10f, 0.045f, Rojo, 0f, 180f);
            Linea(px, 256, new(0.22f, 0.70f), new(0.22f, 0.62f), 0.030f, Rojo);
            Linea(px, 256, new(0.78f, 0.70f), new(0.78f, 0.62f), 0.030f, Rojo);
            Circulo(px, 256, 0.5f, 0.80f, 0.055f, 0.050f, Rojo);
        });
        return A(tex, 200f);
    }

    /// <summary>Botón Chantaje: bocadillo sellado con una X.</summary>
    public static Sprite BotonChantaje()
    {
        var tex = Dibujar(128, px =>
        {
            var color = ColorChantaje;
            Linea(px, 128, new(0.22f, 0.34f), new(0.78f, 0.34f), 0.10f, color);
            Linea(px, 128, new(0.22f, 0.34f), new(0.22f, 0.70f), 0.10f, color);
            Linea(px, 128, new(0.78f, 0.34f), new(0.78f, 0.70f), 0.10f, color);
            Linea(px, 128, new(0.22f, 0.70f), new(0.78f, 0.70f), 0.10f, color);
            Linea(px, 128, new(0.32f, 0.34f), new(0.26f, 0.20f), 0.08f, color);
            Linea(px, 128, new(0.26f, 0.20f), new(0.42f, 0.34f), 0.08f, color);
            Linea(px, 128, new(0.40f, 0.42f), new(0.60f, 0.62f), 0.075f, Claro);
            Linea(px, 128, new(0.60f, 0.42f), new(0.40f, 0.62f), 0.075f, Claro);
        });
        return A(tex, 100f);
    }

    // ── Terminar (normal + gris) ───────────────────────────────────────────

    public static Sprite BotonTerminar() => Mazo(Claro, Rojo);

    public static Sprite BotonTerminarGris() => Mazo(Gris, Gris);

    private static Sprite Mazo(Color32 colorA, Color32 colorB)
    {
        var tex = Dibujar(128, px =>
        {
            Linea(px, 128, new(0.30f, 0.66f), new(0.70f, 0.66f), 0.20f, colorA);
            Linea(px, 128, new(0.30f, 0.66f), new(0.30f, 0.86f), 0.18f, colorA);
            Linea(px, 128, new(0.70f, 0.66f), new(0.70f, 0.86f), 0.18f, colorA);
            Linea(px, 128, new(0.30f, 0.86f), new(0.70f, 0.86f), 0.18f, colorA);
            Linea(px, 128, new(0.50f, 0.66f), new(0.50f, 0.40f), 0.10f, colorB);
            Linea(px, 128, new(0.24f, 0.30f), new(0.76f, 0.30f), 0.07f, colorB);
        });
        return A(tex, 100f);
    }

    // ── Utilidades de dibujo (mismo estilo que SpritesEco) ──────────────────

    private static Texture2D Dibujar(int size, Action<Color32[]> dibujo)
    {
        var px = new Color32[size * size];
        for (var i = 0; i < px.Length; i++)
        {
            px[i] = Transparente;
        }

        dibujo(px);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply(false, false);
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }

    private static Sprite A(Texture2D tex, float ppu) =>
        Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), ppu);

    private static void Pintar(Color32[] px, int size, float x, float y, float grosor, Color32 color)
    {
        var r = Mathf.Max(1f, grosor * size * 0.5f);
        var cx = x * size;
        var cy = y * size;
        var x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r));
        var x1 = Mathf.Min(size - 1, Mathf.CeilToInt(cx + r));
        var y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r));
        var y1 = Mathf.Min(size - 1, Mathf.CeilToInt(cy + r));
        var rr = r * r;

        for (var yy = y0; yy <= y1; yy++)
        {
            for (var xx = x0; xx <= x1; xx++)
            {
                var dx = xx - cx;
                var dy = yy - cy;
                if ((dx * dx) + (dy * dy) <= rr)
                {
                    px[(yy * size) + xx] = color;
                }
            }
        }
    }

    private static void Linea(Color32[] px, int size, Vector2 a, Vector2 b, float grosor, Color32 color)
    {
        var pasos = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(a, b) * size * 2f));
        for (var i = 0; i <= pasos; i++)
        {
            var t = i / (float)pasos;
            Pintar(px, size, Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), grosor, color);
        }
    }

    private static void Circulo(Color32[] px, int size, float cx, float cy, float radio, float grosor, Color32 color) =>
        Arco(px, size, cx, cy, radio, grosor, color, 0f, 360f);

    private static void Arco(Color32[] px, int size, float cx, float cy, float radio, float grosor,
        Color32 color, float gradosIni, float gradosFin)
    {
        var pasos = Mathf.Max(24, Mathf.CeilToInt(
            (gradosFin - gradosIni) / 360f * 2f * Mathf.PI * radio * size * 2f));
        for (var i = 0; i <= pasos; i++)
        {
            var ang = (gradosIni + ((gradosFin - gradosIni) * i / pasos)) * Mathf.Deg2Rad;
            Pintar(px, size, cx + (Mathf.Cos(ang) * radio), cy + (Mathf.Sin(ang) * radio), grosor, color);
        }
    }
}
