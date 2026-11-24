using System;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace TownOfPlus.Utilities;

public static class SpritesProfesor
{
    public static readonly Color32 AzulTiza = new(74, 168, 255, 255);

    private static Sprite? _icono;
    private static Sprite? _botonReentrenar;
    private static Sprite? _botonCambiar;

    public static readonly LoadableAsset<Sprite> IconoRolAsset =
        new SpritePersonalizable("Profesor", "TownOfPlus.Resources.Icons.profesor.png", 200f, Icono);

    public static readonly LoadableAsset<Sprite> BotonReentrenarAsset =
        new SpritePersonalizable("ProfesorReentrenar", "TownOfPlus.Resources.Buttons.ProfesorReentrenar.png", 100f, BotonReentrenar);

    public static readonly LoadableAsset<Sprite> BotonCambiarAsset =
        new SpritePersonalizable("ProfesorCambiar", "TownOfPlus.Resources.Buttons.ProfesorCambiar.png", 100f, BotonCambiar);

    public static Sprite Icono()
    {
        if (_icono != null)
        {
            return _icono;
        }

        var tex = Dibujar(256, px =>
        {
            Circulo(px, 256, 0.44f, 0.032f);
            Libro(px, 256);
            Birrete(px, 256);
        });
        _icono = A(tex, 200f);
        return _icono;
    }

    public static Sprite BotonReentrenar()
    {
        if (_botonReentrenar != null)
        {
            return _botonReentrenar;
        }

        var tex = Dibujar(128, px =>
        {
            Circulo(px, 128, 0.42f, 0.06f);
            Libro(px, 128);
        });
        _botonReentrenar = A(tex, 100f);
        return _botonReentrenar;
    }

    public static Sprite BotonCambiar()
    {
        if (_botonCambiar != null)
        {
            return _botonCambiar;
        }

        var tex = Dibujar(128, px =>
        {
            Circulo(px, 128, 0.42f, 0.06f);
            Flechas(px, 128);
        });
        _botonCambiar = A(tex, 100f);
        return _botonCambiar;
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
        Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);

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
                    px[(yy * size) + xx] = AzulTiza;
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

    private static void Libro(Color32[] px, int size)
    {
        Linea(px, size, new(0.30f, 0.35f), new(0.70f, 0.35f), 0.05f);
        Linea(px, size, new(0.30f, 0.48f), new(0.70f, 0.48f), 0.05f);
        Linea(px, size, new(0.30f, 0.61f), new(0.70f, 0.61f), 0.05f);
        Linea(px, size, new(0.30f, 0.30f), new(0.30f, 0.66f), 0.05f);
        Linea(px, size, new(0.70f, 0.30f), new(0.70f, 0.66f), 0.05f);
    }

    private static void Birrete(Color32[] px, int size)
    {
        Linea(px, size, new(0.32f, 0.74f), new(0.68f, 0.74f), 0.05f);
        Linea(px, size, new(0.40f, 0.74f), new(0.40f, 0.82f), 0.045f);
        Linea(px, size, new(0.60f, 0.74f), new(0.60f, 0.82f), 0.045f);
        Linea(px, size, new(0.36f, 0.82f), new(0.64f, 0.82f), 0.045f);
    }

    private static void Flechas(Color32[] px, int size)
    {
        Linea(px, size, new(0.28f, 0.62f), new(0.72f, 0.62f), 0.05f);
        Linea(px, size, new(0.60f, 0.54f), new(0.72f, 0.62f), 0.05f);
        Linea(px, size, new(0.60f, 0.70f), new(0.72f, 0.62f), 0.05f);
        Linea(px, size, new(0.28f, 0.38f), new(0.72f, 0.38f), 0.05f);
        Linea(px, size, new(0.28f, 0.38f), new(0.40f, 0.30f), 0.05f);
        Linea(px, size, new(0.28f, 0.38f), new(0.40f, 0.46f), 0.05f);
    }
}
