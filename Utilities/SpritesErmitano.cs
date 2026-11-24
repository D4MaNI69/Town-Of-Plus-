using System;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace TownOfPlus.Utilities;

/// <summary>
///     Sprites del Ermitaño (color celeste #70C6FB) tras el REWORK a Escape:
///     ícono de rol, botón <b>Escape</b> y el <b>PNG del cursor</b> que aparece
///     sobre el ratón al abrir el canal de transporte (al 70% de opacidad).
///     Cada sprite puede ser reemplazado por un PNG incrustado si existe
///     (Resources/Icons/Ermitano.png, Resources/Buttons/Escape.png,
///     Resources/EscapeCursor.png) o por el PNG del jugador en
///     plugins/TownOfPlus/Sprites; si no hay ninguno se usa el dibujo procedural.
/// </summary>
public static class SpritesErmitano
{
    /// <summary>Color celeste del rol (#70C6FB).</summary>
    public static readonly Color32 Celeste = new(112, 198, 251, 255);

    private static Sprite? _iconoRol;
    private static Sprite? _botonEscape;
    private static Sprite? _cursorEscape;

    /// <summary>
    ///     Ícono del rol: PNG incrustado (Resources/Icons/Ermitano.png) →
    ///     dibujo procedural (capucha de ermitaño).
    /// </summary>
    public static readonly LoadableAsset<Sprite> IconoRolAsset =
        new SpritePersonalizable("ermitano", "TownOfPlus.Resources.Icons.Ermitano.png", 200f, IconoRol);

    /// <summary>
    ///     Sprite del botón ESCAPE (Resources/Buttons/Escape.png).
    ///     PPU reducido (150 en vez de 200) para que el botón se vea del mismo
    ///     tamaño que los de Ritualista/Francotirador.
    /// </summary>
    public static readonly LoadableAsset<Sprite> BotonEscapeAsset =
        new SpritePersonalizable("Escape", "TownOfPlus.Resources.Buttons.Escape.png", 150f, BotonEscape);

    /// <summary>
    ///     PNG del cursor de Escape (Resources/EscapeCursor.png). Se dibuja sobre
    ///     el ratón mientras el canal de transporte está activo; el 70% de opacidad
    ///     lo aplica el manager en el SpriteRenderer.
    /// </summary>
    public static readonly LoadableAsset<Sprite> CursorEscapeAsset =
        new SpritePersonalizable("EscapeCursor", "TownOfPlus.Resources.EscapeCursor.png", 150f, CursorEscape);

    public static Sprite IconoRol()
    {
        if (_iconoRol != null)
        {
            return _iconoRol;
        }

        var tex = Dibujar(256, px =>
        {
            // Ermitaño: capucha triangular sobre la cabeza (círculo).
            Poligono(px, 256, new[]
            {
                new Vector2(0.16f, 0.22f),
                new Vector2(0.50f, 0.92f),
                new Vector2(0.84f, 0.22f),
            }, 0.038f);
            Circulo(px, 256, 0.10f, 0.030f);
        });

        _iconoRol = A(tex, 200f);
        return _iconoRol;
    }

    /// <summary>
    ///     Botón Escape: caja (el sitio del que se sale) + flecha diagonal hacia
    ///     fuera (el teletransporte).
    /// </summary>
    public static Sprite BotonEscape()
    {
        if (_botonEscape != null)
        {
            return _botonEscape;
        }

        var tex = Dibujar(128, px =>
        {
            // Caja de origen.
            Linea(px, 128, new(0.16f, 0.18f), new(0.58f, 0.18f), 0.055f);
            Linea(px, 128, new(0.16f, 0.18f), new(0.16f, 0.58f), 0.055f);
            Linea(px, 128, new(0.16f, 0.58f), new(0.38f, 0.58f), 0.055f);
            Linea(px, 128, new(0.58f, 0.18f), new(0.58f, 0.34f), 0.055f);

            // Flecha que escapa hacia arriba-derecha.
            Linea(px, 128, new(0.40f, 0.40f), new(0.84f, 0.84f), 0.055f);
            Linea(px, 128, new(0.84f, 0.84f), new(0.60f, 0.84f), 0.055f);
            Linea(px, 128, new(0.84f, 0.84f), new(0.84f, 0.60f), 0.055f);
        });

        _botonEscape = A(tex, 75f);
        return _botonEscape;
    }

    /// <summary>
    ///     PNG del cursor: retícula de teletransporte (dos círculos + cruz con
    ///     punto central) para marcar dónde va a caer el Escape.
    ///     Se dibuja con la MISMA PPU que el PNG incrustado para que el cursor
    ///     mida igual con o sin el archivo (0.85 unidades de mundo).
    /// </summary>
    public static Sprite CursorEscape()
    {
        if (_cursorEscape != null)
        {
            return _cursorEscape;
        }

        var tex = Dibujar(128, px =>
        {
            Circulo(px, 128, 0.36f, 0.050f);
            Circulo(px, 128, 0.16f, 0.045f);
            Linea(px, 128, new(0.14f, 0.50f), new(0.00f, 0.50f), 0.050f);
            Linea(px, 128, new(0.86f, 0.50f), new(1.00f, 0.50f), 0.050f);
            Linea(px, 128, new(0.50f, 0.14f), new(0.50f, 0.00f), 0.050f);
            Linea(px, 128, new(0.50f, 0.86f), new(0.50f, 1.00f), 0.050f);
            Circulo(px, 128, 0.05f, 0.045f);
        });

        _cursorEscape = A(tex, 150f);
        return _cursorEscape;
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
                    px[(yy * size) + xx] = Celeste;
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
        Arco(px, size, radio, grosor, 0f, 360f);
    }

    private static void Arco(Color32[] px, int size, float radio, float grosor, float gradosIni, float gradosFin)
    {
        var pasos = Mathf.Max(24, Mathf.CeilToInt((gradosFin - gradosIni) / 360f * 2f * Mathf.PI * radio * size * 2f));
        for (var i = 0; i <= pasos; i++)
        {
            var ang = (gradosIni + ((gradosFin - gradosIni) * i / pasos)) * Mathf.Deg2Rad;
            Pintar(px, size, 0.5f + (Mathf.Cos(ang) * radio), 0.5f + (Mathf.Sin(ang) * radio), grosor);
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
