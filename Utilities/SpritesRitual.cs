using System;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace TownOfPlus.Utilities;

/// <summary>
///     Sprites procedurales del Ritualista (color crema): ícono de rol,
///     botón Ritual (círculo + estrella) y botón Sacrificar (daga).
///     Generados una sola vez y guardados en caché.
/// </summary>
public static class SpritesRitual
{
    /// <summary>Color crema del rol (#FFFDD0).</summary>
    public static readonly Color32 Crema = new(255, 253, 208, 255);

    private static Sprite? _iconoRol;
    private static Sprite? _botonRitual;
    private static Sprite? _botonSacrificar;

    /// <summary>
    ///     Ícono del rol: PNG incrustado (Resources/Icons/ritualista.png) → PNG del
    ///     jugador → dibujo procedural.
    /// </summary>
    public static readonly LoadableAsset<Sprite> IconoRolAsset =
        new SpritePersonalizable("ritualista", "TownOfPlus.Resources.Icons.ritualista.png", 200f, IconoRol);

    /// <summary>Sprite del botón Ritual (Resources/Buttons/RitualBoton.png).</summary>
    public static readonly LoadableAsset<Sprite> BotonRitualAsset =
        new SpritePersonalizable("RitualBoton", "TownOfPlus.Resources.Buttons.RitualBoton.png", 200f, BotonRitual);

    /// <summary>Sprite del botón Sacrificar (Resources/Buttons/SacrificarBoton.png).</summary>
    public static readonly LoadableAsset<Sprite> BotonSacrificarAsset =
        new SpritePersonalizable("SacrificarBoton", "TownOfPlus.Resources.Buttons.SacrificarBoton.png", 200f, BotonSacrificar);

    public static Sprite IconoRol()
    {
        if (_iconoRol != null)
        {
            return _iconoRol;
        }

        var tex = Dibujar(256, px =>
        {
            Circulo(px, 256, 0.46f, 0.030f);
            Estrella(px, 256, 0.32f, 0.026f, 5);
            Circulo(px, 256, 0.08f, 0.030f);
        });

        _iconoRol = A(tex, 200f);
        return _iconoRol;
    }

    public static Sprite BotonRitual()
    {
        if (_botonRitual != null)
        {
            return _botonRitual;
        }

        var tex = Dibujar(128, px =>
        {
            Circulo(px, 128, 0.44f, 0.055f);
            Estrella(px, 128, 0.28f, 0.045f, 5);
        });

        _botonRitual = A(tex, 100f);
        return _botonRitual;
    }

    public static Sprite BotonSacrificar()
    {
        if (_botonSacrificar != null)
        {
            return _botonSacrificar;
        }

        var tex = Dibujar(128, px =>
        {
            // Daga estilizada: hoja triangular + mango en cruz.
            Poligono(px, 128, new[]
            {
                new Vector2(0.50f, 0.10f),
                new Vector2(0.33f, 0.60f),
                new Vector2(0.67f, 0.60f),
            }, 0.050f);
            Linea(px, 128, new(0.50f, 0.60f), new(0.50f, 0.90f), 0.055f);
            Linea(px, 128, new(0.36f, 0.76f), new(0.64f, 0.76f), 0.055f);
        });

        _botonSacrificar = A(tex, 100f);
        return _botonSacrificar;
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
                    px[(yy * size) + xx] = Crema;
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

    private static void Circulo(Color32[] px, int size, float radio, float grosor)
    {
        var pasos = Mathf.Max(24, Mathf.CeilToInt(2f * Mathf.PI * radio * size * 2f));
        for (var i = 0; i < pasos; i++)
        {
            var ang = i * Mathf.PI * 2f / pasos;
            Pintar(px, size, 0.5f + (Mathf.Cos(ang) * radio), 0.5f + (Mathf.Sin(ang) * radio), grosor);
        }
    }

    private static void Estrella(Color32[] px, int size, float radio, float grosor, int puntas)
    {
        var verts = new Vector2[puntas * 2];
        for (var i = 0; i < puntas * 2; i++)
        {
            var ang = (Mathf.PI / 2f) + (i * Mathf.PI / puntas);
            var r = i % 2 == 0 ? radio : radio * 0.42f;
            verts[i] = new Vector2(0.5f + (Mathf.Cos(ang) * r), 0.5f + (Mathf.Sin(ang) * r));
        }

        Poligono(px, size, verts, grosor);
    }

    private static void Poligono(Color32[] px, int size, Vector2[] verts, float grosor)
    {
        for (var i = 0; i < verts.Length; i++)
        {
            Linea(px, size, verts[i], verts[(i + 1) % verts.Length], grosor);
        }
    }
}