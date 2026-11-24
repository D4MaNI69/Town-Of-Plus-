using System;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace TownOfPlus.Utilities;

/// <summary>
///     Sprites del Aislador (color #109FB9): ícono de rol, botones Soplar
///     (burbuja que sale de una boca), Burbuja (burbuja simple) y Escudo
///     (burbuja con anillo). Cada sprite puede ser reemplazado por un PNG
///     incrustado si existe (Resources/Icons/Aislador.png,
///     Resources/Buttons/Soplar.png, Resources/Buttons/Burbuja.png,
///     Resources/Buttons/Escudo.png).
/// </summary>
public static class SpritesAislador
{
    /// <summary>Color del Aislador (#109FB9).</summary>
    public static readonly Color32 Turquesa = new(16, 159, 185, 255);

    /// <summary>
    ///     PNG de burbuja compartido para marcados y escudo
    ///     (Resources/BurbujaEfecto.png → fallback procedural).
    ///     Es un PNG SUELTO en la raíz de Resources (como MiraRoja.png),
    ///     distinto del botón (Resources/Buttons/Burbuja.png).
    /// </summary>
    public static readonly LoadableAsset<Sprite> EfectoBurbujaAsset =
        new SpritePersonalizable("BurbujaEfecto", "TownOfPlus.Resources.BurbujaEfecto.png", 100f, Burbuja);

    /// <summary>
    ///     PNG de burbuja compartido para marcados y escudo
    ///     (Resources/Buttons/Burbuja.png → fallback procedural).
    /// </summary>
    public static readonly LoadableAsset<Sprite> BurbujaAsset =
        new SpritePersonalizable("Burbuja", "TownOfPlus.Resources.Buttons.Burbuja.png", 100f, Burbuja);

    /// <summary>Ícono del rol (Resources/Icons/Aislador.png).</summary>
    public static readonly LoadableAsset<Sprite> IconoRolAsset =
        new SpritePersonalizable("Aislador", "TownOfPlus.Resources.Icons.Aislador.png", 200f, IconoRol);

    /// <summary>Sprite del botón Soplar (Resources/Buttons/Soplar.png).</summary>
    public static readonly LoadableAsset<Sprite> BotonSoplarAsset =
        new SpritePersonalizable("Soplar", "TownOfPlus.Resources.Buttons.Soplar.png", 100f, BotonSoplar);

    /// <summary>Sprite del botón Escudo (Resources/Buttons/Escudo.png).</summary>
    public static readonly LoadableAsset<Sprite> BotonEscudoAsset =
        new SpritePersonalizable("Escudo", "TownOfPlus.Resources.Buttons.Escudo.png", 100f, BotonEscudo);

    public static Sprite IconoRol()
    {
        var tex = Dibujar(256, px =>
        {
            // Persona (cabeza + cuerpo) dentro de una burbuja.
            Arco(px, 256, 0.40f, 0.030f, 0f, 360f);
            Circulo(px, 256, 0.595f, 0.75f, 0.040f, 0.040f);
            Linea(px, 256, new(0.44f, 0.30f), new(0.56f, 0.30f), 0.045f);
            Linea(px, 256, new(0.44f, 0.30f), new(0.44f, 0.55f), 0.045f);
            Linea(px, 256, new(0.56f, 0.30f), new(0.56f, 0.55f), 0.045f);
            Linea(px, 256, new(0.44f, 0.55f), new(0.50f, 0.62f), 0.045f);
            Linea(px, 256, new(0.56f, 0.55f), new(0.50f, 0.62f), 0.045f);
        });
        return A(tex, 200f);
    }

    public static Sprite BotonSoplar()
    {
        var tex = Dibujar(128, px =>
        {
            // Boca que sopla pequeñas burbujas.
            Arco(px, 128, 0.24f, 0.045f, 120f, 240f);
            Circulo(px, 128, 0.55f, 0.72f, 0.045f, 0.045f);
            Circulo(px, 128, 0.72f, 0.55f, 0.040f, 0.040f);
            Circulo(px, 128, 0.78f, 0.78f, 0.035f, 0.035f);
        });
        return A(tex, 100f);
    }

    private static Texture2D Dibujar(int size, Action<Color32[]> dibujo)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        dibujo(px);
        tex.SetPixels32(px);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }

    private static Sprite A(Texture2D tex, float ppu) =>
        Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), ppu);

    public static Sprite Burbuja()
    {
        var tex = Dibujar(128, px =>
        {
            // Burbuja simple con brillo interior.
            Circulo(px, 128, 0.5f, 0.5f, 0.36f, 0.045f);
            Arco(px, 128, 0.62f, 0.68f, 0.12f, 0.025f, 130f, 200f);
        });
        return A(tex, 100f);
    }

    public static Sprite BotonEscudo()
    {
        var tex = Dibujar(128, px =>
        {
            // Escudo dentro de la burbuja.
            Circulo(px, 128, 0.5f, 0.5f, 0.36f, 0.045f);
            Poligono(px, 128, new[]
            {
                new Vector2(0.30f, 0.66f),
                new Vector2(0.70f, 0.66f),
                new Vector2(0.70f, 0.48f),
                new Vector2(0.50f, 0.28f),
                new Vector2(0.30f, 0.48f),
            }, 0.045f);
        });
        return A(tex, 100f);
    }

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
                    px[(yy * size) + xx] = Turquesa;
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

    private static void Arco(Color32[] px, int size, float radio, float grosor, float gradosIni, float gradosFin) =>
        Arco(px, size, 0.5f, 0.5f, radio, grosor, gradosIni, gradosFin);

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

    private static void Poligono(Color32[] px, int size, Vector2[] verts, float grosor)
    {
        for (var i = 0; i < verts.Length; i++)
        {
            Linea(px, size, verts[i], verts[(i + 1) % verts.Length], grosor);
        }
    }
}
