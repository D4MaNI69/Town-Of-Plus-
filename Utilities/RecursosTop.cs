using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace TownOfPlus.Utilities;

/// <summary>
///     Sistema de recursos personalizables de Town Of Plus.
///     El jugador puede soltar SUS PROPIOS archivos en las carpetas de BepInEx:
///         BepInEx/plugins/TownOfPlus/Sonidos/*.wav  (audio .wav)
///         BepInEx/plugins/TownOfPlus/Sprites/*.png  (imágenes .png)
///     Prioridad: archivo externo del jugador → recurso incrustado en el DLL →
///     valor por defecto (silencio o mira procedural).
/// </summary>
public static class RecursosTop
{
    private static readonly Assembly Ensamblado = typeof(RecursosTop).Assembly;

    /// <summary>Carpeta de sonidos personalizados del jugador.</summary>
    public static string CarpetaSonidos => Path.Combine(Paths.PluginPath, "TownOfPlus", "Sonidos");

    /// <summary>Carpeta de imágenes personalizadas del jugador.</summary>
    public static string CarpetaSprites => Path.Combine(Paths.PluginPath, "TownOfPlus", "Sprites");

    /// <summary>Devuelve la primera ruta existente con las extensiones dadas, o null.</summary>
    public static string? BuscarArchivo(string carpeta, string nombreBase, params string[] extensiones)
    {
        try
        {
            if (!Directory.Exists(carpeta))
            {
                return null;
            }

            return extensiones
                .Select(ext => Path.Combine(carpeta, nombreBase + ext))
                .FirstOrDefault(File.Exists);
        }
        catch
        {
            return null;
        }
    }

    internal static bool ExisteRecurso(string nombreCompleto)
    {
        return NombreRecursoReal(nombreCompleto) != null;
    }

    /// <summary>
    ///     Devuelve el nombre REAL del recurso incrustado ignorando
    ///     mayúsculas/minúsculas (así preparando.wav y Preparando.wav funcionan
    ///     igual), o null si no existe.
    /// </summary>
    internal static string? NombreRecursoReal(string nombreCompleto)
    {
        try
        {
            foreach (var nombre in Ensamblado.GetManifestResourceNames())
            {
                if (string.Equals(nombre, nombreCompleto, StringComparison.OrdinalIgnoreCase))
                {
                    return nombre;
                }
            }
        }
        catch
        {
            // Sin acceso a los recursos.
        }

        return null;
    }

    /// <summary>Carga un PNG desde disco como Sprite (null si falla).</summary>
    public static Sprite? CargarPngDesdeArchivo(string ruta, float ppu = 100f)
    {
        try
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(ruta)))
            {
                return null;
            }

            tex.hideFlags = HideFlags.HideAndDontSave;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), ppu);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Sprite de la mira de francotirador a pantalla completa:
    ///     primero el PNG del jugador, luego un PNG incrustado opcional
    ///     (Resources/MiraFrancotirador.png antes de compilar) y si no hay
    ///     ninguno, el Manager dibuja una mira procedural.
    /// </summary>
    public static Sprite? CargarSpriteMiraScope()
    {
        var externo = BuscarArchivo(CarpetaSprites, "MiraFrancotirador", ".png");
        if (externo != null)
        {
            var spr = CargarPngDesdeArchivo(externo);
            if (spr != null)
            {
                return spr;
            }
        }

        const string recurso = "TownOfPlus.Resources.MiraFrancotirador.png";
        if (ExisteRecurso(recurso))
        {
            try
            {
                return new LoadableResourceAsset(recurso, 100f).LoadAsset();
            }
            catch
            {
                // PNG incrustado corrupto: usar la mira procedural.
            }
        }

        return null;
    }
}

/// <summary>Decodifica un .wav PCM (8/16/24/32 bits) o IEEE float a AudioClip.</summary>
public static class DecodificadorWavTop
{
    public static AudioClip? CargarDesdeArchivo(string ruta)
    {
        try
        {
            return Decodificar(File.ReadAllBytes(ruta), Path.GetFileNameWithoutExtension(ruta));
        }
        catch
        {
            return null;
        }
    }

    public static AudioClip? Decodificar(byte[] bytes, string nombre)
    {
        if (bytes.Length < 44 ||
            bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F')
        {
            return null;
        }

        ushort canales = 0;
        ushort bits = 0;
        var rate = 0;
        ushort formato = 0;
        byte[]? data = null;

        var pos = 12; // saltar "RIFF" + tamaño + "WAVE"
        while (pos + 8 <= bytes.Length)
        {
            var id = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
            var size = BitConverter.ToInt32(bytes, pos + 4);
            var body = pos + 8;
            if (body + size > bytes.Length)
            {
                break;
            }

            if (id == "fmt " && size >= 16)
            {
                formato = BitConverter.ToUInt16(bytes, body);
                canales = BitConverter.ToUInt16(bytes, body + 2);
                rate = BitConverter.ToInt32(bytes, body + 4);
                bits = BitConverter.ToUInt16(bytes, body + 14);
            }
            else if (id == "data")
            {
                data = new byte[size];
                Buffer.BlockCopy(bytes, body, data, 0, size);
            }

            pos = body + size + (size & 1); // chunks alineados a 2 bytes
        }

        if (data == null || canales == 0 || rate <= 0 || bits == 0 || bits % 8 != 0)
        {
            return null;
        }

        var totalMuestras = data.Length / (bits / 8);
        if (totalMuestras <= 0)
        {
            return null;
        }

        var flotantes = new float[totalMuestras];
        var i = 0;
        switch (bits)
        {
            case 8:
                for (var o = 0; o < data.Length && i < totalMuestras; o += 1)
                {
                    flotantes[i++] = (data[o] - 128) / 128f;
                }
                break;
            case 16:
                for (var o = 0; o + 2 <= data.Length && i < totalMuestras; o += 2)
                {
                    flotantes[i++] = BitConverter.ToInt16(data, o) / 32768f;
                }
                break;
            case 24:
                for (var o = 0; o + 3 <= data.Length && i < totalMuestras; o += 3)
                {
                    var v = data[o] | (data[o + 1] << 8) | ((sbyte)data[o + 2] << 16);
                    flotantes[i++] = v / 8388608f;
                }
                break;
            case 32 when formato == 3: // IEEE float
                for (var o = 0; o + 4 <= data.Length && i < totalMuestras; o += 4)
                {
                    flotantes[i++] = BitConverter.ToSingle(data, o);
                }
                break;
            case 32: // PCM int32
                for (var o = 0; o + 4 <= data.Length && i < totalMuestras; o += 4)
                {
                    flotantes[i++] = BitConverter.ToInt32(data, o) / 2147483648f;
                }
                break;
            default:
                return null;
        }

        var clip = AudioClip.Create($"TOP_{nombre}", totalMuestras / canales, canales, rate, false);
        clip.hideFlags = HideFlags.HideAndDontSave;
        clip.SetData(flotantes, 0);
        return clip;
    }
}

/// <summary>
///     Sonido personalizable: busca primero el .wav del jugador en
///     plugins/TownOfPlus/Sonidos/&lt;Nombre&gt;.wav, luego el recurso incrustado
///     opcional del DLL y, si no hay nada, devuelve un clip silencioso
///     (así el juego nunca crashea por falta de sonido).
/// </summary>
public sealed class AudioPersonalizable : LoadableAsset<AudioClip>
{
    private static AudioClip? _silencio;

    private readonly string _nombreBase;
    private readonly string _recursoIncrustado;
    private AudioClip? _cache;

    public AudioPersonalizable(string nombreBase, string? recursoIncrustado = null)
    {
        _nombreBase = nombreBase;
        _recursoIncrustado = recursoIncrustado ?? string.Empty;
    }

    public override AudioClip LoadAsset()
    {
        if (_cache != null)
        {
            return _cache;
        }

        // 1) PRIORIDAD: recurso incrustado en el DLL (carpeta Resources del
        //    proyecto Town of plus+). Ahí viven todos los sonidos del mod.
        var recursoReal = string.IsNullOrEmpty(_recursoIncrustado)
            ? null
            : RecursosTop.NombreRecursoReal(_recursoIncrustado);
        if (recursoReal != null)
        {
            try
            {
                // Reutiliza el decodificador WAV de MiraAPI para recursos incrustados.
                _cache = new LoadableAudioResourceAsset(recursoReal).LoadAsset();
            }
            catch
            {
                _cache = null;
            }
        }

        // 2) Respaldo: archivo externo del jugador en plugins/TownOfPlus/Sonidos.
        if (_cache == null)
        {
            var externo = RecursosTop.BuscarArchivo(
                RecursosTop.CarpetaSonidos, _nombreBase, ".wav", ".wave");
            if (externo != null)
            {
                _cache = DecodificadorWavTop.CargarDesdeArchivo(externo);
            }
        }

        _cache ??= ClipSilencio();
        return _cache;
    }

    private static AudioClip ClipSilencio()
    {
        if (_silencio != null)
        {
            return _silencio;
        }

        _silencio = AudioClip.Create("TOP_Silencio", 1, 1, 44100, false);
        _silencio.hideFlags = HideFlags.HideAndDontSave;
        return _silencio;
    }
}

/// <summary>Todos los sonidos configurables de Town Of Plus.</summary>
public static class SonidosTop
{
    /// <summary>Sonido de intro del Francotirador (pantalla de rol).</summary>
    public static AudioPersonalizable FrancotiradorIntro { get; } =
        new("Francotirador", "TownOfPlus.Resources.Sound.FrancotiradorIntro.wav");

    /// <summary>Sonido de intro del Ritualista (pantalla de rol).</summary>
    public static AudioPersonalizable RitualistaIntro { get; } =
        new("RitualistaIntro", "TownOfPlus.Resources.Sound.RitualistaIntro.wav");

    /// <summary>Sonido de intro del Ermitano (pantalla de rol).</summary>
    public static AudioPersonalizable ErmitanoIntro { get; } =
        new("ErmitanoIntro", "TownOfPlus.Resources.Sound.ErmitanoIntro.wav");

    /// <summary>Sonido al entrar en fase Preparando (solo para el Francotirador).
    /// El WAV incrustado Preparando.wav NO existe en el proyecto: se deja el
    /// nombre base ("Preparando") para el PNG/override del jugador; sin nada,
    /// suena silencio (el mod nunca crashea por falta de sonido).</summary>
    public static AudioPersonalizable Preparando { get; } =
        new("Preparando", "TownOfPlus.Resources.Sound.preparando.wav");

    /// <summary>Sonido ambiental EN BUCLE durante el zoom out.</summary>
    public static AudioPersonalizable ZoomOutLoop { get; } =
        new("ZoomOutLoop", "TownOfPlus.Resources.Sound.ZoomOutLoop.wav");

    /// <summary>Sonido de disparo global al matar (todos lo escuchan).</summary>
    public static AudioPersonalizable Disparo { get; } =
        new("Disparo", "TownOfPlus.Resources.Sound.Disparo.wav");

    /// <summary>Sonido de viento EN BUCLE durante el zoom out del Francotirador.</summary>
    public static AudioPersonalizable Viento { get; } =
        new("Viento", "TownOfPlus.Resources.Sound.Viento.wav");

    /// <summary>Sonido de intro del Cerrajero (pantalla de rol).</summary>
    /// <summary>Sonido de intro del Cerrajero (pantalla de rol). El WAV
    /// CerrajeroIntro.wav NO existe en el proyecto: se deja el nombre base
    /// ("CerrajeroIntro") para el override del jugador; sin nada, suena
    /// silencio (el mod nunca crashea por falta de sonido).</summary>
    public static AudioPersonalizable CerrajeroIntro { get; } =
        new("CerrajeroIntro", "TownOfPlus.Resources.Sound.CerrajeroIntro.wav");

    /// <summary>Sonido de intro del Kamikaze (pantalla de rol).</summary>
    public static AudioPersonalizable KamikazeIntro { get; } =
        new("KamikazeIntro", "TownOfPlus.Resources.Sound.KamikazeIntro.wav");

    /// <summary>Sonido EN BUCLE durante la canalización del revive de la Amalgama.</summary>
    public static AudioPersonalizable Revive { get; } =
        new("Revive", "TownOfPlus.Resources.Sound.Revive.wav");

    /// <summary>Sonido de intro del Profesor (pantalla de rol).</summary>
    public static AudioPersonalizable ProfesorIntro { get; } =
        new("ProfesorIntro", "TownOfPlus.Resources.Sound.ProfesorIntro.wav");

    /// <summary>Sonido de intro del Aislador (pantalla de rol).</summary>
    public static AudioPersonalizable AisladorIntro { get; } =
        new("AisladorIntro", "TownOfPlus.Resources.Sound.AisladorIntro.wav");

    /// <summary>
    ///     Sonido al CERRAR una puerta — Resources/Sound/conspiranoico/hay.wav
    ///     (soundpack "Usar hay un conspiranoico soundpack").
    /// </summary>
    public static AudioPersonalizable PuertaCerrar { get; } =
        new("conspiranoico\\hay", "TownOfPlus.Resources.Sound.conspiranoico.hay.wav");

    /// <summary>
    ///     Sonido al ABRIR una puerta — Resources/Sound/conspiranoico/corran.wav
    ///     (soundpack "Usar hay un conspiranoico soundpack").
    /// </summary>
    public static AudioPersonalizable PuertaAbrir { get; } =
        new("conspiranoico\\corran", "TownOfPlus.Resources.Sound.conspiranoico.corran.wav");
}

/// <summary>
///     Sprite personalizable: busca primero el PNG incrustado en el DLL
///     (carpeta Resources del proyecto Town of plus+), luego el archivo del
///     jugador en plugins/TownOfPlus/Sprites/&lt;Nombre&gt;.png y, si no hay
///     ninguno, genera el sprite procedural de respaldo.
/// </summary>
public sealed class SpritePersonalizable : LoadableAsset<Sprite>
{
    private readonly string _nombreBase;
    private readonly string _recursoIncrustado;
    private readonly float _ppu;
    private readonly Func<Sprite> _fallback;
    private Sprite? _cache;

    public SpritePersonalizable(string nombreBase, string recursoIncrustado, float ppu, Func<Sprite> fallback)
    {
        _nombreBase = nombreBase;
        _recursoIncrustado = recursoIncrustado;
        _ppu = ppu;
        _fallback = fallback;
    }

    public override Sprite LoadAsset()
    {
        if (_cache != null)
        {
            return _cache;
        }

        // 1) PRIORIDAD: PNG incrustado en el DLL (carpeta Resources del proyecto).
        var recursoReal = string.IsNullOrEmpty(_recursoIncrustado)
            ? null
            : RecursosTop.NombreRecursoReal(_recursoIncrustado);
        if (recursoReal != null)
        {
            try
            {
                _cache = new LoadableResourceAsset(recursoReal, _ppu).LoadAsset();
            }
            catch
            {
                _cache = null;
            }
        }

        // 2) Respaldo: PNG del jugador en plugins/TownOfPlus/Sprites.
        if (_cache == null)
        {
            var externo = RecursosTop.BuscarArchivo(
                RecursosTop.CarpetaSprites, _nombreBase, ".png");
            if (externo != null)
            {
                _cache = RecursosTop.CargarPngDesdeArchivo(externo, _ppu);
            }
        }

        // 3) Último recurso: sprite procedural generado por código.
        _cache ??= _fallback();
        return _cache;
    }
}