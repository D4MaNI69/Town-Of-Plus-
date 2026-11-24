using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TownOfPlus.Patches;

/// <summary>
///     Añade el texto "TOWN OF PLUS (versión)" al menú principal. La versión
///     sale de TownOfPlusPlugin.Version, así que se actualiza sola.
///     Se re-aplica en cada carga del menú principal (al abrir el juego y al
///     volver del lobby/partida, cuando la escena MainMenu se recarga): se
///     limpia cualquier instancia previa para no acumular textos duplicados.
/// </summary>
[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
public static class MenuLogoPatch
{
    private const int OrdenCanvas = 500;

    /// <summary>Raíz del texto de versión creado (para limpiar duplicados).</summary>
    private static GameObject? _raizActual;

    public static void Postfix(MainMenuManager __instance)
    {
        try
        {
            // Limpiar una instancia previa (re-aplicación al volver al menú).
            if (_raizActual != null)
            {
                UnityEngine.Object.Destroy(_raizActual);
                _raizActual = null;
            }

            var root = new GameObject("TOP_LogoMenu");
            _raizActual = root;
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OrdenCanvas;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            // Texto "TOWN OF PLUS (versión)".
            var textGo = new GameObject("TOP_LogoTexto");
            textGo.transform.SetParent(root.transform, false);

            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.text = $"TOWN OF PLUS <size=60%>({TownOfPlusPlugin.Version})</size>";
            tmp.fontSize = 34f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = new Color32(255, 94, 0, 255);
            tmp.raycastTarget = false;

            var fuente = TMP_Settings.defaultFontAsset;
            if (fuente != null)
            {
                tmp.font = fuente;
            }

            var trt = tmp.rectTransform;
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(1f, 1f);
            trt.sizeDelta = new Vector2(560f, 70f);
            trt.anchoredPosition = new Vector2(-60f, -160f);
        }
        catch
        {
            // Nunca romper el menú por un error del logo.
        }
    }
}