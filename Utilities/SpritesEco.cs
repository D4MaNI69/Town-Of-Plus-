using System;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace TownOfPlus.Utilities;

/// <summary>
///     Ícono del Eco (color #FFF099) con fallback procedural: una doble onda
///     de sonido (eco). Si existe Resources/Icons/Eco.png se usa el PNG
///     incrustado en su lugar.
/// </summary>
public static class SpritesEco
{
    /// <summary>Color del Eco (#FFF099).</summary>
    public static readonly Color32 AmarilloPalido = new(255, 240, 153, 255);

    /// <summary>Ícono del rol (Resources/Icons/Eco.png).</summary>
    public static readonly LoadableAsset<Sprite> IconoRolAsset =
        new SpritePersonalizable("Eco", "TownOfPlus.Resources.Icons.Eco.png", 200f, IconoRol);

    public static Sprite IconoRol()
    {
        var tex = Dibujar(256, px =>
        {
            // Persona (cabeza + cuerpo) a la izquierda + 2 ondas de eco.
            Circulo(px, 256, 0.30f, 0.62f, 0.060f, 0.055f);
            Linea(px, 256, new(0.22f, 0.30f), new(0.38f, 0.30f), 0.060f);
            Linea(px, 256, new(0.22f, 0.30f), new(0.22f, 0.52f), 0.060f);
            Linea(px, 256, new(0.38f, 0.30f), new(0.38f, 0.52f), 0.060f);
            Arco(px, 256, 0.30f, 0.50f, 0.16f, 0.055f, -60f, 60f);
            Arco(px, 256, 0.30f, 0.50f, 0.28f, 0.055f, -60f, 60f);
        });
        return A(tex, 200f);
    }

    private static Texture2D Dibujar(int size, Action<Color32[]> dibujo)
    {
        var px = new Color32[size * size];
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

    private static void Pintar(Color32[] px, int size, float x, float y, float grosor)
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
                    px[(yy * size) + xx] = AmarilloPalido;
                }
            }
        }
    }

    private static void Linea(Color32[] px, int size, Vector2 a, Vector2 b, float grosor)
    {
        var pasos = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(a, b) * size * 2f));
        for (var i = 0; i <= pasos; i++)
        {
            var t = i / (float)pasos;
            Pintar(px, size, Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), grosor);
        }
    }

    private static void Circulo(Color32[] px, int size, float cx, float cy, float radio, float grosor) =>
        Arco(px, size, cx, cy, radio, grosor, 0f, 360f);

    private static void Arco(Color32[] px, int size, float cx, float cy, float radio, float grosor,
        float gradosIni, float gradosFin)
    {
        var pasos = Mathf.Max(24, Mathf.CeilToInt((gradosFin - gradosIni) / 360f * 2f * Mathf.PI * radio * size * 2f));
        for (var i = 0; i <= pasos; i++)
        {
            var ang = (gradosIni + ((gradosFin - gradosIni) * i / pasos)) * Mathf.Deg2Rad;
            Pintar(px, size, cx + (Mathf.Cos(ang) * radio), cy + (Mathf.Sin(ang) * radio), grosor);
        }
    }
}
