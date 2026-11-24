using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using AmongUs.Data;
using AmongUs.Data.Player;
using Assets.InnerNet;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TownOfPlus.Assets;
using UnityEngine;

namespace TownOfPlus.Patches;

/// <summary>
///     Anuncios propios de Town of plus+ al estilo TOU Mira / DivaniMods:
///     se leen de un JSON embebido en Resources/Announcements/modNews-en_US.json
///     y se inyectan en la lista de anuncios del menú principal.
///     El banner del mod SOLO aparece en sus propios anuncios (se filtra por el
///     Id "TouPlusModNews"), y sus números van en el rango [10000, 100000) para
///     NO superponerse con el banner que TOU Mira pone en sus anuncios (>= 100000).
/// </summary>
public class TouPlusModNews(int number, string title, string subTitle, string shortTitle, string text, string date)
{
    public const string NewsId = "TouPlusModNews";

    public Announcement ToAnnouncement()
    {
        return new Announcement
        {
            Date = Date,
            Number = Number,
            ShortTitle = ShortTitle,
            SubTitle = SubTitle,
            Title = Title,
            Text = Text,
            Language = (uint)DataManager.Settings.Language.CurrentLanguage,
            Id = NewsId
        };
    }

    public string Date { get; set; } = date;
    public int Number { get; set; } = number;
    public string ShortTitle { get; set; } = shortTitle;
    public string SubTitle { get; set; } = subTitle;
    public string Title { get; set; } = title;
    public string Text { get; set; } = text;
}

/// <summary>
///     Carga los anuncios propios desde el JSON embebido (dev/local, sin red).
///     Para añadir anuncios: edita Resources/Announcements/modNews-en_US.json
///     (título, cuerpo, fecha, id/número y logo). Números entre 10000 y 100000.
/// </summary>
public static class TopModNewsFetcher
{
    private const string ResourceName = "TownOfPlus.Resources.Announcements.modNews-en_US.json";

    private static bool _parsed;

    public static void CheckForNews()
    {
        if (_parsed)
        {
            return;
        }

        _parsed = true;

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var resourceStream = assembly.GetManifestResourceStream(ResourceName);
            if (resourceStream == null)
            {
                TownOfPlusPlugin.Instance?.Log.LogWarning(
                    $"[TOP] No se encontró el recurso de anuncios: {ResourceName}");
                return;
            }

            using var reader = new StreamReader(resourceStream);
            using var jsonDocument = JsonDocument.Parse(reader.ReadToEnd());
            var newsArray = jsonDocument.RootElement.GetProperty("News");

            foreach (var newsElement in newsArray.EnumerateArray())
            {
                var dateString = newsElement.GetProperty("Date").GetString() ?? "Unknown Date";
                var numberString = newsElement.GetProperty("Number").GetString();
                var number = numberString != null && int.TryParse(numberString, out var n) ? n : 0;
                if (number is >= 10000 and < 100000)
                {
                    number += 200000;
                }

                // RANGO TOP [200000, 300000): Divani usa [10000, 100000) y TOU
                // Mira >= 100000 para su banner. 10001 chocaba con el anuncio
                // 10001 de Divani y su prefijo filtraba por Number, ocultando
                // el nuestro. Se suma el desplazamiento para no romper JSONs.
                var shortTitle = newsElement.GetProperty("ShortTitle").GetString() ?? "No Short Title";
                var subTitle = newsElement.GetProperty("SubTitle").GetString() ?? "No Subtitle";
                var title = newsElement.GetProperty("Title").GetString() ?? "No Title";
                var body = string.Join(" ",
                    newsElement.GetProperty("Text").EnumerateArray().Select(e => e.GetString()));

                var modNew = new TouPlusModNews(number, title, subTitle, shortTitle, body, dateString);
                ModNewsHistory.AllModNews = ModNewsHistory.AllModNews.Add(modNew);
            }

            TownOfPlusPlugin.Instance?.Log.LogInfo(
                $"[TOP] Anuncios cargados: {ModNewsHistory.AllModNews.Count}.");
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[TOP] No se pudieron cargar los anuncios: {ex.Message}");
        }
    }
}

[HarmonyPatch]
public static class ModNewsHistory
{
    public static ImmutableList<TouPlusModNews> AllModNews = ImmutableList<TouPlusModNews>.Empty;

    /// <summary>Inyecta los anuncios del mod en la lista del menú principal.</summary>
    /// <remarks>
    ///     SOLO agrega los anuncios de Town Of Plus al principio del array.
    ///     NO elimina ni filtra anuncios de otros mods (Divani, TOU Mira, etc.).
    ///     Cada mod es responsable de sus propios anuncios.
    /// </remarks>
    [HarmonyPatch(typeof(PlayerAnnouncementData), nameof(PlayerAnnouncementData.SetAnnouncements))]
    [HarmonyPrefix]
    [HarmonyPriority(100)]
    public static void SetModAnnouncements_Prefix(ref Il2CppReferenceArray<Announcement> aRange)
    {
        if (AllModNews.IsEmpty)
        {
            return;
        }

        var aArray = aRange.ToArray();

        var finalAllNews = AllModNews.Select(n => n.ToAnnouncement()).ToList();
        // Mantener TODOS los anuncios existentes (incluidos los de Divani y otros mods)
        finalAllNews.AddRange(aArray);
        finalAllNews.Sort((a1, a2) => DateTime.Compare(
            DateTime.Parse(a2.Date, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            DateTime.Parse(a1.Date, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));

        var newArray = new Announcement[finalAllNews.Count];
        for (var i = 0; i < finalAllNews.Count; i++)
        {
            newArray[i] = finalAllNews[i];
        }

        aRange = newArray;
    }

    /// <summary>
    ///     Superpone el banner/logo del mod SOLO a los anuncios propios
    ///     (Id == "TouPlusModNews"). Así no pisa el banner de TOU Mira
    ///     (que identifica los suyos por Id "TouMiraModNews" / Number >= 100000).
    /// </summary>
    [HarmonyPatch(typeof(AnnouncementPanel), nameof(AnnouncementPanel.SetUp))]
    [HarmonyPostfix]
    public static void SetUpPanel_Postfix(AnnouncementPanel __instance,
        [HarmonyArgument(0)] Announcement announcement)
    {
        if (announcement.Id != TouPlusModNews.NewsId)
        {
            return;
        }

        var obj = new GameObject("TOP_ModLabel");
        obj.transform.SetParent(__instance.transform);
        obj.transform.localPosition = new Vector3(-0.8f, 0.13f, 0.5f);
        obj.transform.localScale = new Vector3(0.78f, 0.78f, 0.78f);
        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = TownofplusAssets.AnnouncementBanner.LoadAsset();
        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
    }
}