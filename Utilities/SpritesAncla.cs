using System;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace TownOfPlus.Utilities;

/// <summary>
///     Icono del modificador Ancla (color #FFF099, mismo que el Eco):
///     cadena + ola de eco. Si existe Resources/Icons/Modif/Ancla.png se usa
///     el PNG incrustado en su lugar.
/// </summary>
public static class SpritesAncla
{
    /// <summary>Color del Ancla (#FFF099, igual que el Eco).</summary>
    public static readonly Color32 AmarilloPalido = new(255, 240, 153, 255);

    /// <summary>Icono del modificador (Resources/Icons/Modif/Ancla.png).</summary>
    public static readonly LoadableAsset<Sprite> IconoModAsset =
        new SpritePersonalizable("Ancla", "TownOfPlus.Resources.Icons.Modif.Ancla.png", 200f, IconoMod);

    public static Sprite IconoMod()
    {
        var tex = Dibujar(256, px =>
        {
            // Eslabon de cadena (ancla) + onda de eco a la derecha.
            Circulo(px, 256, 0.34f, 0.55f, 0.075f, 0.060f);
            Circulo(px, 256, 0.34f, 0.32f, 0.075f, 0.060f);
            Linea(px, 256, new(0.34f, 0.62f), new(0.34f, 0.78f), 0.055f);
            Linea(px, 256, new(0.24f, 0.78f), new(0.44f, 0.78f), 0.055f);
            Arco(px, 256, 0.55f, 0.50f, 0.14f, 0.055f, -60f, 60f);
            Arco(px, 256, 0.55f, 0.50f, 0.25f, 0.055f, -60f, 60f);
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
