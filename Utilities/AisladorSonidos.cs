using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TownOfPlus.Utilities;

/// <summary>
///     Sonidos del Aislador, cargados de recursos incrustados:
///     <para>· Resources/Sound/AisladorIntro.wav — música de intro al ser
///     asignado el rol (solo para el propio Aislador).</para>
///     <para>· Resources/Sound/POP.wav — al explotar una burbuja; SOLO lo oye
///     el dueño de esa burbuja (un marcado oye la suya, el Aislador la del
///     escudo). Nadie más lo escucha.</para>
///     Unity no decodifica WAV desde byte[], así que se parsea el RIFF
///     (PCM 8/16 bits) y se crea el AudioClip manualmente.
/// </summary>
public static class AisladorSonidos
{
    private const string PrefijoRecursos = "TownOfPlus.Resources.Sound.";

    private static AudioClip? _intro;
    private static AudioClip? _pop;
    private static AudioSource? _fuente;

    /// <summary>Música de intro. Se reproduce una vez al recibir el rol.</summary>
    public static void ReproducirIntro()
    {
        Reproducir2D(Cargar(ref _intro, "AisladorIntro.wav"));
    }

    /// <summary>POP de una burbuja (2D, solo para el dueño local).</summary>
    public static void ReproducirPop()
    {
        Reproducir2D(Cargar(ref _pop, "POP.wav"));
    }

    private static void Reproducir2D(AudioClip? clip)
    {
        if (clip == null)
        {
            return;
        }

        try
        {
            if (_fuente == null)
            {
                var go = new GameObject("AisladorSonidos");
                Object.DontDestroyOnLoad(go);
                _fuente = go.AddComponent<AudioSource>();
                _fuente.playOnAwake = false;
                _fuente.spatialBlend = 0f; // 2D: oible sin importar posición
            }

            _fuente.clip = clip;
            _fuente.volume = 1f;
            _fuente.Play();
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Aislador] No se pudo reproducir el sonido: {ex.Message}");
        }
    }

    private static AudioClip? Cargar(ref AudioClip? cache, string nombre)
    {
        if (cache != null)
        {
            return cache;
        }

        try
        {
            var ensamblado = typeof(AisladorSonidos).Assembly;
            using var flujo = ensamblado.GetManifestResourceStream(PrefijoRecursos + nombre);
            if (flujo == null)
            {
                // El WAV aún no existe: silencio, sin errores.
                TownOfPlusPlugin.Instance?.Log.LogWarning(
                    $"[Aislador] Recurso de sonido no encontrado: {PrefijoRecursos}{nombre}");
                return null;
            }

            using var lector = new BinaryReader(flujo);
            var datos = lector.ReadBytes((int)flujo.Length);
            cache = ParsearWav(nombre, datos);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Aislador] No se pudo cargar {nombre}: {ex.Message}");
        }

        return cache;
    }

    /// <summary>Parser WAV mínimo: RIFF → fmt (PCM) → data.</summary>
    private static AudioClip? ParsearWav(string nombre, byte[] datos)
    {
        if (datos.Length < 44 || datos[0] != 'R' || datos[1] != 'I')
        {
            return null;
        }

        var pos = 12; // salta "RIFF****WAVE"
        var canales = 1;
        var frecuencia = 44100;
        int bits = 16;
        byte[]? muestras = null;

        while (pos + 8 <= datos.Length)
        {
            var id = System.Text.Encoding.ASCII.GetString(datos, pos, 4);
            var tam = BitConverter.ToInt32(datos, pos + 4);
            pos += 8;

            if (id == "fmt ")
            {
                canales = BitConverter.ToInt16(datos, pos + 2);
                frecuencia = BitConverter.ToInt32(datos, pos + 4);
                bits = BitConverter.ToInt16(datos, pos + 14);
            }
            else if (id == "data")
            {
                muestras = new byte[tam];
                Buffer.BlockCopy(datos, pos, muestras, 0, Math.Min(tam, datos.Length - pos));
                break;
            }

            pos += tam + (tam & 1); // los chunks van alineados a 2 bytes
        }

        if (muestras == null || muestras.Length == 0)
        {
            return null;
        }

        var totalMuestras = muestras.Length / (bits / 8);
        var clip = AudioClip.Create(nombre, totalMuestras / canales, canales, frecuencia, false);
        var flotantes = new float[totalMuestras];
        if (bits == 16)
        {
            for (var i = 0; i < totalMuestras; i++)
            {
                flotantes[i] = BitConverter.ToInt16(muestras, i * 2) / 32768f;
            }
        }
        else // 8 bits sin signo
        {
            for (var i = 0; i < totalMuestras; i++)
            {
                flotantes[i] = (muestras[i] - 128) / 128f;
            }
        }

        clip.SetData(flotantes, 0);
        clip.hideFlags = HideFlags.HideAndDontSave;
        return clip;
    }
}