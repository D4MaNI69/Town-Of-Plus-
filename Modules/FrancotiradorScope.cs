using TownOfPlus.Utilities;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TownOfPlus.Modules;

/// <summary>
///     Overlay a pantalla completa con el PNG de la mira de francotirador
///     durante el zoom out. Usa el PNG del jugador (plugins/TownOfPlus/
///     Sprites/MiraFrancotirador.png), o uno incrustado opcional, o genera
///     una mira procedural (viñeta oscura + círculo + cruz roja).
/// </summary>
public static class FrancotiradorScope
{
    /// <summary>Debajo de las miras rojas (canvas 2000) pero sobre el mundo.</summary>
    private const int OrdenCanvas = 1900;

    private static GameObject? _root;
    private static Texture2D? _texturaProcedural;
    private static int _texAncho = -1;
    private static int _texAlto = -1;

    public static void Crear()
    {
        Destruir();

        _root = new GameObject("TOP_ScopeFrancotirador");
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OrdenCanvas;

        var go = new GameObject("ImagenScope");
        go.transform.SetParent(_root.transform, false);
        var img = go.AddComponent<Image>();
        img.raycastTarget = false; // nunca bloquear clics del disparo
        var rt = img.rectTransform;

        var sprite = RecursosTop.CargarSpriteMiraScope();
        if (sprite != null)
        {
            // PNG del jugador o incrustado: cubrir la pantalla manteniendo proporción.
            img.sprite = sprite;
            img.color = new Color(1f, 1f, 1f, 0.98f);

            var arTex = sprite.texture.width / (float)sprite.texture.height;
            var arPantalla = Screen.width / (float)Screen.height;
            var size = arTex > arPantalla
                ? new Vector2(Screen.height * arTex, Screen.height)
                : new Vector2(Screen.width, Screen.width / arTex);

            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }
        else
        {
            // Mira procedural generada a la resolución actual.
            var tex = TexturaProcedural(Screen.width, Screen.height);
            var spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
            img.sprite = spr;
            img.color = Color.white;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    public static void Destruir()
    {
        if (_root != null)
        {
            Object.Destroy(_root);
            _root = null;
        }
    }

    /// <summary>
    ///     Viñeta oscura suave con agujero circular amplio (sin cruz ni anillo
    ///     rojo: solo enmarca la pantalla para no obstruir la visión del mapa).
    /// </summary>
    private static Texture2D TexturaProcedural(int ancho, int alto)
    {
        if (_texturaProcedural != null && _texAncho == ancho && _texAlto == alto)
        {
            return _texturaProcedural;
        }

        // Limitar resolución para no generar texturas gigantes (se estiran igual).
        var escala = Mathf.Min(1f, 1600f / Mathf.Max(ancho, alto));
        var w = Mathf.Max(256, Mathf.RoundToInt(ancho * escala));
        var h = Mathf.Max(256, Mathf.RoundToInt(alto * escala));

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixeles = new Color32[w * h];

        var cx = w / 2f;
        var cy = h / 2f;
        // Círculo claro amplio: solo las esquinas se oscurecen un poco.
        var radio = 0.60f * Mathf.Min(w, h);

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var dx = x - cx;
                var dy = y - cy;
                var d = Mathf.Sqrt((dx * dx) + (dy * dy)) / radio;

                Color32 c;
                if (d > 1f)
                {
                    // Fuera del círculo: franja oscura FINA y difuminada.
                    var a = Mathf.Clamp01((d - 1f) * 5f) * 225f;
                    c = new Color32(8, 8, 12, (byte)a);
                }
                else
                {
                    // Interior despejado: solo un oscurecido muy leve pegado al borde.
                    var a = Mathf.Clamp01((d - 0.90f) * 6f) * 70f;
                    c = new Color32(6, 6, 10, (byte)a);
                }

                pixeles[(y * w) + x] = c;
            }
        }

        tex.SetPixels32(pixeles);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;

        _texturaProcedural = tex;
        _texAncho = ancho;
        _texAlto = alto;
        return tex;
    }
}