using System;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace TownOfPlus.Utilities;

/// <summary>
///     Sprites del Colega (crewmate support, color #3F51B5): ícono del rol,
///     botón Acompañar y botón de reunión Esperanza. Igual que el resto de
///     roles del mod, si el PNG incrustado no existe se genera por código.
///     <para>· Icons/Colega.png — ícono del rol (200 ppu).</para>
///     <para>· Buttons/Acompanar.png — botón de ronda para marcar (100 ppu).</para>
///     <para>· Buttons/Esperanza.png — botón de reunión (100 ppu).</para>
/// </summary>
public static class SpritesColega
{
    /// <summary>Color propio del Colega (#3F51B5).</summary>
    public static readonly Color32 Azul = new(0x3F, 0x51, 0xB5, 255);

    /// <summary>Tono claro para detalles del dibujo procedural.</summary>
    private static readonly Color32 Claro = new(0xD8, 0xDE, 0xF9, 255);

    private static readonly Color32 Transparente = new(0, 0, 0, 0);

    public static readonly LoadableAsset<Sprite> IconoRolAsset =
        new SpritePersonalizable("Colega", "TownOfPlus.Resources.Icons.Colega.png", 200f, IconoRol);

    public static readonly LoadableAsset<Sprite> BotonAcompanarAsset =
        new SpritePersonalizable("Acompanar", "TownOfPlus.Resources.Buttons.Acompanar.png", 100f, BotonAcompanar);

    public static readonly LoadableAsset<Sprite> BotonEsperanzaAsset =
        new SpritePersonalizable("Esperanza", "TownOfPlus.Resources.Buttons.Esperanza.png", 100f, BotonEsperanza);

    /// <summary>
    ///     Flecha que solo ve el Colega, apuntando a su marcado. PNG
    ///     Resources/Arrows/ColegaArrow.png (o flecha procedural).
    /// </summary>
    public static readonly LoadableAsset<Sprite> FlechaAsset =
        new SpritePersonalizable("ColegaArrow", "TownOfPlus.Resources.Arrows.ColegaArrow.png", 100f, Flecha);

    /// <summary>Ícono del rol: dos personas unidas (acompañante y acompañado).</summary>
    public static Sprite IconoRol()
    {
        var tex = Dibujar(256, px =>
        {
            // Insignia circular.
            Circulo(px, 256, 0.5f, 0.5f, 0.40f, 0.030f, Azul);

            // Dos cabezas.
            Circulo(px, 256, 0.37f, 0.64f, 0.070f, 0.040f, Azul);
            Circulo(px, 256, 0.63f, 0.64f, 0.070f, 0.040f, Azul);

            // Dos cuerpos inclinados hacia el centro.
            Linea(px, 256, new(0.37f, 0.57f), new(0.30f, 0.33f), 0.065f, Azul);
            Linea(px, 256, new(0.63f, 0.57f), new(0.70f, 0.33f), 0.065f, Azul);

            // Brazos que se encuentran.
            Linea(px, 256, new(0.31f, 0.47f), new(0.47f, 0.44f), 0.050f, Claro);
            Linea(px, 256, new(0.69f, 0.47f), new(0.53f, 0.44f), 0.050f, Claro);
        });
        return A(tex, 200f);
    }

    /// <summary>Botón Acompañar: vínculo entre dos personas.</summary>
    public static Sprite BotonAcompanar()
    {
        var tex = Dibujar(128, px =>
        {
            // Cabeza del acompañante (izquierda) y del acompañado (derecha).
            Circulo(px, 128, 0.30f, 0.68f, 0.105f, 0.055f, Azul);
            Circulo(px, 128, 0.70f, 0.68f, 0.105f, 0.055f, Azul);

            // Cuerpos.
            Linea(px, 128, new(0.30f, 0.57f), new(0.26f, 0.20f), 0.085f, Azul);
            Linea(px, 128, new(0.70f, 0.57f), new(0.74f, 0.20f), 0.085f, Azul);

            // Vínculo (línea que los une).
            Linea(px, 128, new(0.41f, 0.48f), new(0.59f, 0.48f), 0.060f, Claro);
        });
        return A(tex, 100f);
    }

    /// <summary>Botón Esperanza: dos flechas que se intercambian (swap).</summary>
    public static Sprite BotonEsperanza()
    {
        var tex = Dibujar(128, px =>
        {
            // Flecha superior: izquierda → derecha.
            Linea(px, 128, new(0.18f, 0.68f), new(0.80f, 0.68f), 0.075f, Azul);
            Linea(px, 128, new(0.80f, 0.68f), new(0.64f, 0.78f), 0.075f, Azul);
            Linea(px, 128, new(0.80f, 0.68f), new(0.64f, 0.58f), 0.075f, Azul);

            // Flecha inferior: derecha → izquierda.
            Linea(px, 128, new(0.82f, 0.32f), new(0.20f, 0.32f), 0.075f, Claro);
            Linea(px, 128, new(0.20f, 0.32f), new(0.36f, 0.42f), 0.075f, Claro);
            Linea(px, 128, new(0.20f, 0.32f), new(0.36f, 0.22f), 0.075f, Claro);
        });
        return A(tex, 100f);
    }

    /// <summary>Flecha procedural (apunta hacia arriba, #3F51B5).</summary>
    public static Sprite Flecha()
    {
        var tex = Dibujar(128, px =>
        {
            // Mástil.
            Linea(px, 128, new(0.50f, 0.16f), new(0.50f, 0.74f), 0.090f, Azul);

            // Cabeza de flecha.
            Linea(px, 128, new(0.50f, 0.80f), new(0.28f, 0.58f), 0.090f, Azul);
            Linea(px, 128, new(0.50f, 0.80f), new(0.72f, 0.58f), 0.090f, Azul);
        });
        return A(tex, 100f);
    }

    // ── Utilidades de dibujo (mismo estilo que SpritesDeudor) ──────────────

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
