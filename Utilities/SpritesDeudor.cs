using System;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace TownOfPlus.Utilities;

/// <summary>
///     Sprites del Deudor (crewmate power, color #A18262): ícono del rol y
///     botón Trato. Igual que el resto de roles del mod, si el PNG incrustado
///     no existe se genera por código.
///     <para>· Icons/Deudor.png — ícono del rol (200 ppu).</para>
///     <para>· Buttons/Trato.png — botón de ronda para marcar jugadores (100 ppu).</para>
/// </summary>
public static class SpritesDeudor
{
    /// <summary>Color propio del Deudor (#A18262).</summary>
    public static readonly Color32 Marron = new(0xA1, 0x82, 0x62, 255);

    /// <summary>Tono claro para detalles del dibujo procedural.</summary>
    private static readonly Color32 Claro = new(0xF0, 0xDF, 0xC8, 255);

    private static readonly Color32 Transparente = new(0, 0, 0, 0);

    public static readonly LoadableAsset<Sprite> IconoRolAsset =
        new SpritePersonalizable("Deudor", "TownOfPlus.Resources.Icons.Deudor.png", 200f, IconoRol);

    public static readonly LoadableAsset<Sprite> BotonTratoAsset =
        new SpritePersonalizable("Trato", "TownOfPlus.Resources.Buttons.Trato.png", 100f, BotonTrato);

    /// <summary>Ícono del rol: moneda sobre un apretón de manos.</summary>
    public static Sprite IconoRol()
    {
        var tex = Dibujar(256, px =>
        {
            // Insignia circular.
            Circulo(px, 256, 0.5f, 0.5f, 0.40f, 0.030f, Marron);

            // Moneda.
            Circulo(px, 256, 0.5f, 0.67f, 0.095f, 0.035f, Marron);
            Linea(px, 256, new(0.5f, 0.60f), new(0.5f, 0.74f), 0.025f, Marron);

            // Apretón de manos.
            Linea(px, 256, new(0.30f, 0.32f), new(0.46f, 0.44f), 0.055f, Marron);
            Linea(px, 256, new(0.70f, 0.32f), new(0.54f, 0.44f), 0.055f, Marron);
            Linea(px, 256, new(0.43f, 0.46f), new(0.57f, 0.46f), 0.060f, Claro);
        });
        return A(tex, 200f);
    }

    /// <summary>Botón Trato: moneda + apretón de manos.</summary>
    public static Sprite BotonTrato()
    {
        var tex = Dibujar(128, px =>
        {
            // Moneda.
            Circulo(px, 128, 0.5f, 0.74f, 0.14f, 0.050f, Marron);
            Linea(px, 128, new(0.5f, 0.64f), new(0.5f, 0.84f), 0.035f, Marron);

            // Manos que cierran el trato.
            Linea(px, 128, new(0.18f, 0.30f), new(0.45f, 0.46f), 0.090f, Marron);
            Linea(px, 128, new(0.82f, 0.30f), new(0.55f, 0.46f), 0.090f, Marron);
            Linea(px, 128, new(0.42f, 0.48f), new(0.58f, 0.48f), 0.100f, Claro);
        });
        return A(tex, 100f);
    }

    // ── Utilidades de dibujo (mismo estilo que SpritesAbogado) ──────────────

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
