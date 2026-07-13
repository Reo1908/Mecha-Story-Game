using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MechaGame
{
    /// <summary>
    /// Hilfsfunktionen, um die schlichte Prototyp-UI komplett in Code aufzubauen
    /// (Canvas, Texte, Bilder, Buttons) — ohne Prefabs oder Szenen-Referenzen.
    /// </summary>
    public static class UiFactory
    {
        static Font _font;

        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
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

        public static Text CreateText(Transform parent, string name, string content, int fontSize,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color, TextAnchor alignment)
        {
            RectTransform rect = CreateRect(parent, name, anchor, anchoredPosition, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.text = content;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size, UnityAction onClick, int fontSize = 24)
        {
            RectTransform rect = CreateRect(parent, name, anchor, anchoredPosition, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.14f, 0.16f, 0.2f, 0.9f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.9f, 0.9f, 1f);
            colors.selectedColor = new Color(0.8f, 0.85f, 1f);
            colors.pressedColor = new Color(0.6f, 0.65f, 0.8f);
            button.colors = colors;

            CreateText(rect, "Label", label, fontSize,
                new Vector2(0.5f, 0.5f), Vector2.zero, size, Color.white, TextAnchor.MiddleCenter);

            if (onClick != null)
                button.onClick.AddListener(onClick);
            return button;
        }
    }
}
