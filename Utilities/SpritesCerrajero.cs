using System;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace TownOfPlus.Utilities;

/// <summary>
///     Sprites animados del Cerrajero (fantasma con máscara rojo/azul).
///     Carga los 48 frames desde Resources/Cerrajero Fant/ghost0001.png … ghost0048.png.
/// </summary>
public static class SpritesCerrajero
{
    private static Sprite[]? _frames;
    private static float _timer;
    private static int _indice;

    public const int TotalFrames = 48;
    public const float SegundosPorFrame = 0.1f;

    private static Sprite[] ObtenerFrames()
    {
        if (_frames != null && _frames.Length == TotalFrames)
        {
            return _frames;
        }

        _frames = new Sprite[TotalFrames];
        for (var i = 0; i < TotalFrames; i++)
        {
            var numero = i + 1;
            var ruta = $"TownOfPlus.Resources.Cerrajero Fant.ghost{numero:D4}.png";
            try
            {
                _frames[i] = new LoadableResourceAsset(ruta, 100f).LoadAsset();
            }
            catch
            {
                _frames[i] = null!;
            }
        }

        return _frames;
    }

    public static Sprite ObtenerFrameActual()
    {
        var frames = ObtenerFrames();
        if (frames.Length == 0)
        {
            return null!;
        }

        _timer += Time.deltaTime;
        if (_timer >= SegundosPorFrame)
        {
            _timer -= SegundosPorFrame;
            _indice = (_indice + 1) % frames.Length;
        }

        return frames[_indice] ?? frames[0];
    }

    public static void ReiniciarAnimacion()
    {
        _timer = 0f;
        _indice = 0;
    }
}
