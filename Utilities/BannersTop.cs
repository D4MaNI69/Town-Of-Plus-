using System.Globalization;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Assets;
using UnityEngine;

namespace TownOfPlus.Utilities;

/// <summary>
///     Banners de ajustes de rol (como los OptionBanners de Divani Mods).
///     MiraAPI dibuja CustomRoleConfiguration.OptionsScreenshot como cabecera
///     de la pagina de ajustes de cada rol.
///     PNG incrustado: Resources/Banner/&lt;rol&gt;.png (372x232 pixeles).
///     Override del jugador: plugins/TownOfPlus/Sprites/&lt;rol&gt;.png.
///     Si no hay PNG: banner procedural de COLOR PLANO (sin RenderTexture:
///     leer texturas al arrancar podia romper los iconos de otros roles).
/// </summary>
public static class BannersTop
{
    private const int Ancho = 372;
    private const int Alto = 232;

    public static LoadableAsset<Sprite> Cerrajero { get; } =
        new BannerAsset("cerrajero", new Color32(194, 143, 49, 255));

    public static LoadableAsset<Sprite> Huesped { get; } =
        new BannerAsset("huesped", new Color32(16, 159, 185, 255));

    public static LoadableAsset<Sprite> Ermitano { get; } =
        new BannerAsset("ermitano", new Color32(112, 198, 251, 255));

    public static LoadableAsset<Sprite> Ritualista { get; } =
        new BannerAsset("ritualista", new Color32(255, 253, 208, 255));

    public static LoadableAsset<Sprite> Profesor { get; } =
        new BannerAsset("profesor", new Color32(74, 168, 255, 255));

    public static LoadableAsset<Sprite> Victima { get; } =
        new BannerAsset("victima", new Color32(100, 100, 100, 255));

    public static LoadableAsset<Sprite> Saboteador { get; } =
        new BannerAsset("saboteador", Palette.ImpostorRed);

    public static LoadableAsset<Sprite> Kamikaze { get; } =
        new BannerAsset("kamikaze", Palette.ImpostorRed);

    public static LoadableAsset<Sprite> Francotirador { get; } =
        new BannerAsset("francotirador", new Color32(220, 40, 40, 255));

    public static LoadableAsset<Sprite> Dictador { get; } =
        new BannerAsset("dictador", Palette.ImpostorRed);

    public static LoadableAsset<Sprite> Amalgama { get; } =
        new BannerAsset("amalgama", new Color32(255, 132, 205, 255));

    public static LoadableAsset<Sprite> Aislador { get; } =
        new BannerAsset("aislador", new Color32(16, 159, 185, 255));

    public static LoadableAsset<Sprite> Eco { get; } =
        new BannerAsset("eco", new Color32(255, 240, 153, 255));

    public static LoadableAsset<Sprite> Abogado { get; } =
        new BannerAsset("abogado", Palette.ImpostorRed);

    public static LoadableAsset<Sprite> Deudor { get; } =
        new BannerAsset("deudor", new Color32(0xA1, 0x82, 0x62, 255));

    public static LoadableAsset<Sprite> Colega { get; } =
        new BannerAsset("colega", new Color32(0x3F, 0x51, 0xB5, 255));

    /// <summary>
    ///     Activo de banner 100% a prueba de fallos: nunca lanza excepciones y
    ///     NUNCA usa RenderTexture/ReadPixels (eso corria al cargar los roles y
    ///     podia romper los iconos de la lista de ajustes).
    /// </summary>
    private sealed class BannerAsset : LoadableAsset<Sprite>
    {
        private readonly string _nombre;
        private readonly string _recurso;
        private readonly Color32 _color;
        private Sprite? _cache;

        public BannerAsset(string nombre, Color32 color)
        {
            _nombre = nombre;
            _recurso = "TownOfPlus.Resources.Banner." + nombre + ".png";
            _color = color;
        }

        public override Sprite LoadAsset()
        {
            if (_cache != null)
            {
                return _cache;
            }

            // 1) PNG incrustado en el DLL: Resources/Banner/<rol>.png
            try
            {
                var real = RecursosTop.NombreRecursoReal(_recurso);
                if (real != null)
                {
                    _cache = new LoadableResourceAsset(real, 100f).LoadAsset();
                }
            }
            catch
            {
                _cache = null;
            }

            // 2) Override del jugador: plugins/TownOfPlus/Sprites/<rol>.png
            if (_cache == null)
            {
                try
                {
                    var externo = RecursosTop.BuscarArchivo(
                        RecursosTop.CarpetaSprites, _nombre, ".png");
                    if (externo != null)
                    {
                        _cache = RecursosTop.CargarPngDesdeArchivo(externo, 100f);
                    }
                }
                catch
                {
                    _cache = null;
                }
            }

            // 3) Respaldo: color plano del rol (sin icono, sin RenderTexture).
            if (_cache == null)
            {
                try
                {
                    _cache = BannerProcedural(_color);
                }
                catch
                {
                    _cache = null;
                }
            }

            // Garantia: nunca devolver null (MiraAPI no lo espera).
            _cache ??= Sprite.Create(
                new Texture2D(2, 2, TextureFormat.RGBA32, false),
                new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
            return _cache;
        }
    }
    /// <summary>
    ///     Banner de respaldo de COLOR PLANO con una banda inferior oscura.
    ///     Todo con Texture2D/SetPixels32 normal: sin RenderTexture.
    /// </summary>
    private static Sprite BannerProcedural(Color32 color)
    {
        const int banda = 34;
        var oscuro = new Color32(
            (byte)(color.r * 55 / 100),
            (byte)(color.g * 55 / 100),
            (byte)(color.b * 55 / 100),
            255);

        var pixeles = new Color32[Ancho * Alto];
        for (var y = 0; y < Alto; y++)
        {
            var c = y < banda ? oscuro : color;
            for (var x = 0; x < Ancho; x++)
            {
                pixeles[(y * Ancho) + x] = c;
            }
        }

        var textura = new Texture2D(Ancho, Alto, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
        };
        textura.SetPixels32(pixeles);
        textura.Apply(false, false);

        return Sprite.Create(
            textura,
            new Rect(0, 0, Ancho, Alto),
            new Vector2(0.5f, 0.5f),
            100f);
    }
}