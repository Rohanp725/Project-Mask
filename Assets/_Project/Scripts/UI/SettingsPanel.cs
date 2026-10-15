using System;
using Semblance.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Semblance.UI
{
    /// <summary>Player options (shared by the main menu and the pause menu). Changes apply and save immediately.</summary>
    public static class SettingsPanel
    {
        public static GameObject Build(Transform parent, Action onBack)
        {
            var panel = UIFactory.Panel("Settings", parent, new Color(0.05f, 0.045f, 0.04f, 0.97f));
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660, 700));
            var title = UIFactory.Text("Title", panel.transform, "SETTINGS", 30, UIFactory.Light, TextAlignmentOptions.Top);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(560, 40));

            float y = -80;
            Slider("Mouse sensitivity", panel.transform, ref y, 0.02f, 0.3f, GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v, v => (v / 0.1f).ToString("0.0") + "x");
            Slider("Brightness", panel.transform, ref y, 0.6f, 1.8f, GameSettings.Brightness, v => GameSettings.Brightness = v, v => Mathf.RoundToInt(v * 100) + "%");
            Slider("Volume", panel.transform, ref y, 0f, 1f, GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, v => Mathf.RoundToInt(v * 100) + "%");
            Slider("Ambience", panel.transform, ref y, 0f, 1.5f, GameSettings.AmbienceVolume, v => GameSettings.AmbienceVolume = v, v => Mathf.RoundToInt(v * 100) + "%");
            Slider("Music", panel.transform, ref y, 0f, 1.5f, GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, v => Mathf.RoundToInt(v * 100) + "%");
            Slider("Field of view", panel.transform, ref y, 55f, 95f, GameSettings.FieldOfView, v => GameSettings.FieldOfView = Mathf.Round(v), v => Mathf.RoundToInt(v) + " deg");
            Slider("Resolution (PS1 look)", panel.transform, ref y, 0f, 3f, ResolutionIndex(), v => GameSettings.PixelHeight = Resolutions[Mathf.RoundToInt(v)], v => Resolutions[Mathf.RoundToInt(v)] + "p", true);
            Toggle("Dithering", panel.transform, ref y, GameSettings.Dither, v => GameSettings.Dither = v);
            Toggle("Invert mouse Y", panel.transform, ref y, GameSettings.InvertY, v => GameSettings.InvertY = v);
            Toggle("Tutorial hints", panel.transform, ref y, GameSettings.ShowHints, v => GameSettings.ShowHints = v);
            Slider("Text size", panel.transform, ref y, 0.85f, 1.5f, GameSettings.TextScale, v => GameSettings.TextScale = v, v => Mathf.RoundToInt(v * 100) + "%");
            Toggle("Camera bob and shake", panel.transform, ref y, GameSettings.CameraMotion, v => GameSettings.CameraMotion = v);
            Toggle("Sound captions", panel.transform, ref y, GameSettings.SoundCaptions, v => GameSettings.SoundCaptions = v);
            Toggle("Reduce scares", panel.transform, ref y, GameSettings.ReduceScares, v => GameSettings.ReduceScares = v);

            var controls = UIFactory.Button("Controls", panel.transform, "Controls...", 18, UIFactory.Light, new Color(1, 1, 1, 0.08f), null);
            UIFactory.Place((RectTransform)controls.transform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(32, y), new Vector2(220, 32));
            controls.onClick.AddListener(() => BuildControls(panel.transform));

            var back = UIFactory.Button("Back", panel.transform, "Back", 20, UIFactory.Light, UIFactory.Red, () =>
            {
                GameSettings.Save();
                onBack?.Invoke();
            });
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(200, 42));
            panel.gameObject.AddComponent<SelectFirstOnEnable>();
            return panel.gameObject;
        }

        /// <summary>Rebind keys: click an action, then press the key or mouse button you want. Gamepad works out of the box.</summary>
        private static void BuildControls(Transform settings)
        {
            var panel = UIFactory.Panel("Controls", settings, new Color(0.05f, 0.045f, 0.04f, 0.99f));
            UIFactory.Stretch(panel.rectTransform);
            var title = UIFactory.Text("Title", panel.transform, "CONTROLS", 28, UIFactory.Light, TextAlignmentOptions.Top);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(560, 40));
            var note = UIFactory.Text("Note", panel.transform, "Click an action, then press a key or mouse button. Esc cancels.\nMove: WASD / arrows. Gamepad: sticks, A interact, LT look closer, RT photo, LB camera, d-pad up flashlight, View notebook.", 14, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.Top);
            UIFactory.Place(note.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(600, 44));
            float y = -120;
            foreach (var (label, action) in GameInput.Rebindable)
            {
                var a = action;
                var b = UIFactory.Button(label, panel.transform, "", 18, UIFactory.Light, new Color(1, 1, 1, 0.06f), null);
                var text = b.GetComponentInChildren<TextMeshProUGUI>();
                text.alignment = TextAlignmentOptions.MidlineLeft;
                void Show() => text.text = $"{label}  <color=#{ColorUtility.ToHtmlStringRGB(UIFactory.Accent)}>{GameInput.Display(a)}</color>";
                Show();
                b.onClick.AddListener(() => { text.text = $"{label}  <i>press a key...</i>"; GameInput.Rebind(a, Show); });
                UIFactory.Place((RectTransform)b.transform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(40, y), new Vector2(560, 32));
                y -= 38;
            }
            var reset = UIFactory.Button("Reset", panel.transform, "Reset to defaults", 18, UIFactory.Light, new Color(0.25f, 0.22f, 0.2f), () =>
            {
                GameInput.ResetBindings();
                UnityEngine.Object.Destroy(panel.gameObject);
                BuildControls(settings);
            });
            UIFactory.Place((RectTransform)reset.transform, new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(-10, 20), new Vector2(220, 42));
            var back = UIFactory.Button("Back", panel.transform, "Back", 20, UIFactory.Light, UIFactory.Red, () => UnityEngine.Object.Destroy(panel.gameObject));
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(10, 20), new Vector2(220, 42));
            panel.gameObject.AddComponent<SelectFirstOnEnable>();
        }

        private static readonly int[] Resolutions = { 180, 240, 360, 480 };

        private static int ResolutionIndex()
        {
            int current = GameSettings.PixelHeight;
            for (int i = 0; i < Resolutions.Length; i++)
                if (Resolutions[i] >= current) return i;
            return 1;
        }

        private static void Slider(string label, Transform parent, ref float y, float min, float max, float value, Action<float> onChange, Func<float, string> format, bool whole = false)
        {
            var text = UIFactory.Text(label, parent, label, 18, UIFactory.Light, TextAlignmentOptions.MidlineLeft);
            UIFactory.Place(text.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(40, y), new Vector2(230, 30));
            var valueText = UIFactory.Text(label + " Value", parent, format(value), 16, new Color(1, 1, 1, 0.6f), TextAlignmentOptions.MidlineRight);
            UIFactory.Place(valueText.rectTransform, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-40, y), new Vector2(90, 30));

            var root = UIFactory.Rect(label + " Slider", parent);
            UIFactory.Place(root, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(280, y), new Vector2(220, 20));
            var bg = UIFactory.Panel("Background", root, new Color(1, 1, 1, 0.15f));
            UIFactory.Stretch(bg.rectTransform, new Vector2(0, 7), new Vector2(0, -7));
            var fillArea = UIFactory.Rect("Fill Area", root);
            UIFactory.Stretch(fillArea, new Vector2(0, 7), new Vector2(0, -7));
            var fill = UIFactory.Panel("Fill", fillArea, UIFactory.Accent, false);
            UIFactory.Stretch(fill.rectTransform);
            var handleArea = UIFactory.Rect("Handle Area", root);
            UIFactory.Stretch(handleArea);
            var handle = UIFactory.Panel("Handle", handleArea, UIFactory.Light);
            handle.rectTransform.sizeDelta = new Vector2(12, 0);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = whole;
            slider.value = value;
            slider.onValueChanged.AddListener(v =>
            {
                onChange(v);
                valueText.text = format(v);
            });
            y -= 38;
        }

        private static void Toggle(string label, Transform parent, ref float y, bool value, Action<bool> onChange)
        {
            bool state = value;
            var button = UIFactory.Button(label, parent, "", 18, UIFactory.Light, new Color(1, 1, 1, 0.06f), null);
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.MidlineLeft;
            void Refresh() => text.text = $"{label}  <color=#{ColorUtility.ToHtmlStringRGB(UIFactory.Accent)}>{(state ? "ON" : "OFF")}</color>";
            Refresh();
            button.onClick.AddListener(() => { state = !state; onChange(state); Refresh(); });
            UIFactory.Place((RectTransform)button.transform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(32, y), new Vector2(480, 30));
            y -= 34;
        }
    }
}
