using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Semblance.Core
{
    /// <summary>Small helpers for building uGUI screens from code (all screens use a 1280x720 reference).</summary>
    public static class UIFactory
    {
        public static readonly Color Ink = new Color(0.16f, 0.13f, 0.1f);
        public static readonly Color Paper = new Color(0.86f, 0.82f, 0.71f);
        public static readonly Color PaperDark = new Color(0.76f, 0.71f, 0.6f);
        public static readonly Color Light = new Color(0.92f, 0.89f, 0.8f);
        public static readonly Color Accent = new Color(0.85f, 0.65f, 0.35f);
        public static readonly Color Red = new Color(0.6f, 0.12f, 0.1f);

        private static TMP_FontAsset font;
        private static bool fontLoaded;

        /// <summary>The typewriter face (Special Elite, Apache 2.0) used for all UI text.</summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (!fontLoaded)
                {
                    fontLoaded = true;
                    font = Resources.Load<TMP_FontAsset>("Fonts/SpecialElite SDF");
                }
                return font;
            }
            set { font = value; fontLoaded = true; }
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current == null && UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Semblance.UI.SelectionMarker.Ensure();
        }

        public static Canvas Canvas(string name, Transform parent, int sortingOrder)
        {
            EnsureEventSystem();
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.matchWidthOrHeight = 1f;
            ApplyTextScale(scaler);
            Scalers.Add(scaler);
            if (!scaleHooked)
            {
                scaleHooked = true;
                GameSettings.Changed += () =>
                {
                    Scalers.RemoveAll(s => s == null);
                    foreach (var s in Scalers) ApplyTextScale(s);
                };
            }
            return canvas;
        }

        private static readonly List<CanvasScaler> Scalers = new List<CanvasScaler>();
        private static bool scaleHooked;

        /// <summary>A smaller reference resolution makes everything on the canvas bigger.</summary>
        private static void ApplyTextScale(CanvasScaler scaler)
            => scaler.referenceResolution = new Vector2(1280, 720) / Mathf.Clamp(GameSettings.TextScale, 0.85f, 1.5f);

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Panel(string name, Transform parent, Color color, bool raycast = true)
        {
            var image = new GameObject(name, typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var tmp = new GameObject(name, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            tmp.transform.SetParent(parent, false);
            if (Font != null)
                tmp.font = Font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        public static Button Button(string name, Transform parent, string label, float size, Color textColor, Color background, Action onClick)
        {
            var image = Panel(name, parent, background);
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.selectedColor = new Color(1.5f, 1.35f, 1.1f);
            colors.colorMultiplier = 1.2f;
            button.colors = colors;
            if (onClick != null)
                button.onClick.AddListener(() => onClick());
            var text = Text("Label", image.transform, label, size, textColor, TextAlignmentOptions.Midline);
            Stretch(text.rectTransform, new Vector2(8, 2), new Vector2(-8, -2));
            return button;
        }

        public static void Stretch(RectTransform rect, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        /// <summary>Anchors a rect to a point of its parent (0..1) and sets its pivot, position and size.</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>A vertical scrolling list. Add children to the returned content transform.</summary>
        public static RectTransform ScrollList(string name, Transform parent, out ScrollRect scroll, float spacing = 4f, RectOffset padding = null)
        {
            var root = Panel(name, parent, new Color(0, 0, 0, 0.001f));
            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = Rect("Viewport", root.transform);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(6, 6, 6, 6);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            return content;
        }

        public static void Clear(Transform t)
        {
            // Detach first so layouts rebuilt this frame don't still count the old children.
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i);
                child.SetParent(null, false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        public static LayoutElement Layout(Component c, float preferredHeight = -1, float preferredWidth = -1, float flexibleWidth = -1)
        {
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.preferredWidth = preferredWidth;
            if (preferredWidth > 0) le.minWidth = preferredWidth;
            if (preferredHeight > 0) le.minHeight = preferredHeight;
            le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }
}
