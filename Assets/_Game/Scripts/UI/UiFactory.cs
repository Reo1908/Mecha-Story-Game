using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MechaGame
{
    /// <summary>
    /// Zentrale Farb- und Stilwerte der UI, damit HUD, Pausenmenü und Werkstatt
    /// einheitlich aussehen. Änderungen hier wirken überall.
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color Panel = new Color(0.06f, 0.08f, 0.11f, 0.82f);
        public static readonly Color PanelSolid = new Color(0.06f, 0.08f, 0.11f, 0.97f);
        public static readonly Color Button = new Color(0.13f, 0.16f, 0.21f, 0.95f);
        public static readonly Color ButtonSelected = new Color(0.13f, 0.42f, 0.62f, 0.95f);
        public static readonly Color Accent = new Color(0.30f, 0.80f, 1.00f);
        public static readonly Color AccentDark = new Color(0.02f, 0.10f, 0.14f);
        public static readonly Color Text = new Color(0.93f, 0.96f, 1.00f);
        public static readonly Color TextMuted = new Color(0.93f, 0.96f, 1.00f, 0.55f);
        public static readonly Color TextFaint = new Color(0.93f, 0.96f, 1.00f, 0.35f);
        public static readonly Color Warning = new Color(1.00f, 0.76f, 0.29f);
        public static readonly Color Danger = new Color(1.00f, 0.36f, 0.28f);
    }

    /// <summary>
    /// Hilfsfunktionen, um die Prototyp-UI komplett in Code aufzubauen
    /// (Canvas, Panels, Texte, Buttons) — ohne Prefabs oder Szenen-Referenzen.
    /// Panels und Buttons nutzen ein zur Laufzeit erzeugtes 9-Slice-Sprite
    /// mit abgerundeten Ecken.
    /// </summary>
    public static class UiFactory
    {
        static Font _font;
        static Sprite _roundedSprite;

        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        /// <summary>Weißes 9-Slice-Sprite mit abgerundeten Ecken (wird eingefärbt).</summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (_roundedSprite == null)
                    _roundedSprite = BuildRoundedSprite(48, 12);
                return _roundedSprite;
            }
        }

        static Sprite BuildRoundedSprite(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                name = "UiRounded",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float half = size * 0.5f;
            float inner = half - radius;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Vorzeichenbehafteter Abstand zum Rand des abgerundeten Rechtecks,
                    // mit ~1px weicher Kante.
                    float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - half) - inner);
                    float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - half) - inner);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    byte alpha = (byte)(Mathf.Clamp01(0.5f - dist) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            float border = radius + 4f;
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
                return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        public static RectTransform CreateRect(Transform parent, string name,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image CreateImage(Transform parent, string name,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            RectTransform rect = CreateRect(parent, name, anchor, anchoredPosition, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Eingefärbtes Panel mit abgerundeten Ecken.</summary>
        public static Image CreatePanel(Transform parent, string name,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            Image image = CreateImage(parent, name, anchor, anchoredPosition, size, color);
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            return image;
        }

        public static Text CreateText(Transform parent, string name, string content, int fontSize,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color, TextAnchor alignment,
            FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = CreateRect(parent, name, anchor, anchoredPosition, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.text = content;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// Button mit abgerundeten Ecken. <paramref name="primary"/> färbt ihn in der
        /// Akzentfarbe (für die wichtigste Aktion eines Screens).
        /// </summary>
        public static Button CreateButton(Transform parent, string name, string label,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size, UnityAction onClick,
            int fontSize = 24, bool primary = false)
        {
            RectTransform rect = CreateRect(parent, name, anchor, anchoredPosition, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = primary ? UiTheme.Accent : UiTheme.Button;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            // Die Zustände tönen die Grundfarbe, damit Auswahl-Logik weiterhin
            // direkt image.color setzen kann (Slot-/Tab-Hervorhebung).
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.82f, 0.88f, 1f);
            colors.selectedColor = new Color(0.75f, 0.83f, 1f);
            colors.pressedColor = new Color(0.55f, 0.62f, 0.8f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            CreateText(rect, "Label", label, fontSize, new Vector2(0.5f, 0.5f), Vector2.zero, size,
                primary ? UiTheme.AccentDark : UiTheme.Text, TextAnchor.MiddleCenter,
                primary ? FontStyle.Bold : FontStyle.Normal);

            if (onClick != null)
                button.onClick.AddListener(onClick);
            return button;
        }
    }
}
