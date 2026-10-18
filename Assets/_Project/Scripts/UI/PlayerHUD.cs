using System.Collections.Generic;
using Semblance.Core;
using Semblance.Notes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Semblance.UI
{
    /// <summary>
    /// Minimal first-person HUD built at runtime: crosshair, interaction prompt and a timed message box.
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        [SerializeField] private Color textColor = new Color(0.92f, 0.89f, 0.8f);
        [SerializeField] private Color panelColor = new Color(0.05f, 0.05f, 0.06f, 0.82f);

        private Image crosshair;
        private TextMeshProUGUI promptText;
        private GameObject messagePanel;
        private TextMeshProUGUI messageText;
        private float messageHideTime;
        private RectTransform toastStack;
        private TextMeshProUGUI clockText;
        private TextMeshProUGUI hintText;
        private Canvas canvas;
        private readonly List<(TextMeshProUGUI text, float born)> toasts = new List<(TextMeshProUGUI, float)>();
        private float hintHideTime;

        public static PlayerHUD Instance { get; private set; }
        private const float ToastLife = 4f;

        private void Awake()
        {
            Instance = this;
            BuildUI();
            SetPrompt(null);
            messagePanel.SetActive(false);
        }

        private void Start()
        {
            if (Notebook.Instance != null)
                Notebook.Instance.EntryAdded += e =>
                {
                    if (e.kind == NoteKind.Sighting) return;
                    // In conversation, notes pile up quietly and are summed up once you leave.
                    if (Dialogue.DialogueUI.Instance != null && Dialogue.DialogueUI.Instance.Current != null)
                    {
                        notedInConversation++;
                        return;
                    }
                    Toast($"Noted: <b>{e.subject}</b>. {e.title}");
                };
            Dialogue.DialogueUI.Closed += r =>
            {
                if (notedInConversation > 0 && r != null)
                    Toast($"Noted {notedInConversation} thing{(notedInConversation == 1 ? "" : "s")} about <b>{r.FirstName}</b>.");
                notedInConversation = 0;
            };
        }

        private int shownMinute = -1;

        private void Update()
        {
            if (messagePanel.activeSelf && Time.time >= messageHideTime)
                messagePanel.SetActive(false);
            if (IsReading && (GameInput.Pressed(GameInput.Advance) || InputLock.IsLocked))
                DismissReading(); // (a menu or conversation opening over it closes it too)

            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - toasts[i].born;
                if (age > ToastLife)
                {
                    Destroy(toasts[i].text.gameObject);
                    toasts.RemoveAt(i);
                }
                else
                    toasts[i].text.alpha = Mathf.Clamp01((ToastLife - age) / 0.8f);
            }

            var clock = GameClock.Instance;
            if (clock != null)
            {
                // Only rebuild the label (and its text mesh) when the minute changes.
                int minuteKey = clock.Day * 10000 + Mathf.FloorToInt(clock.Hour * 60f);
                if (minuteKey != shownMinute)
                {
                    shownMinute = minuteKey;
                    clockText.text = clock.Label;
                }
            }
            if (hintText.gameObject.activeSelf && hintHideTime > 0 && Time.unscaledTime > hintHideTime)
                hintText.gameObject.SetActive(false);

            // Hide the in-world HUD while a full screen (notebook, archive, menus) is open.
            canvas.enabled = !InputLock.IsLocked || InputLock.Top is Dialogue.DialogueUI || InputLock.Top is Events.HidingSpot;
        }

        /// <summary>Small notice in the top-right corner (new notebook entries, items...).</summary>
        private int notedInConversation;
        private TextMeshProUGUI caption;
        private float captionUntil;

        /// <summary>A sound caption, bottom centre, for a couple of seconds.</summary>
        public void Caption(string text)
        {
            if (caption == null)
            {
                var canvas = GetComponentInChildren<Canvas>();
                if (canvas == null) return;
                caption = UIFactory.Text("Caption", canvas.transform, "", 17, new Color(1f, 0.95f, 0.85f, 0.9f), TextAlignmentOptions.Bottom);
                UIFactory.Place(caption.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(700, 30));
            }
            caption.text = $"<i>{text}</i>";
            captionUntil = Time.unscaledTime + 2.8f;
        }

        private void LateUpdate()
        {
            if (caption != null)
                caption.alpha = Mathf.Clamp01((captionUntil - Time.unscaledTime) / 0.6f);
        }

        public void Toast(string text)
        {
            var t = UIFactory.Text("Toast", toastStack, text, 15, textColor, TextAlignmentOptions.TopRight);
            UIFactory.Layout(t, preferredHeight: 22);
            toasts.Add((t, Time.unscaledTime));
            if (toasts.Count > 5)
            {
                Destroy(toasts[0].text.gameObject);
                toasts.RemoveAt(0);
            }
        }

        /// <summary>Persistent hint line at the top-center (tutorial and objectives). Duration 0 = until replaced.</summary>
        public void SetHint(string text, float duration = 0f)
        {
            text = GameInput.Format(text);
            hintText.gameObject.SetActive(!string.IsNullOrEmpty(text));
            hintText.text = text;
            hintHideTime = duration > 0 ? Time.unscaledTime + duration : 0f;
        }

        public void SetClockVisible(bool visible) => clockText.gameObject.SetActive(visible);

        public void SetPrompt(string prompt)
        {
            bool show = !string.IsNullOrEmpty(prompt);
            promptText.gameObject.SetActive(show);
            if (show)
                promptText.text = $"<b>[{GameInput.Glyph(GameInput.Interact)}]</b> " + prompt;
            crosshair.color = show ? textColor : new Color(textColor.r, textColor.g, textColor.b, 0.45f);
            crosshair.rectTransform.sizeDelta = Vector2.one * (show ? 7f : 4f);
        }

        public void ShowMessage(string message, float duration)
        {
            IsReading = false;
            messageText.text = message;
            messagePanel.SetActive(true);
            messageHideTime = Time.time + duration;
        }

        /// <summary>True while something you're reading or looking at is on screen, waiting to be dismissed.</summary>
        public bool IsReading { get; private set; }

        /// <summary>
        /// Text you read at your own pace (a note, a card, what you see when you look at something): it stays until
        /// you press interact or advance. Scares and story moments keep using the timed <see cref="ShowMessage"/>.
        /// </summary>
        public void ShowReading(string message)
        {
            messageText.text = message + $"\n<size=65%><color=#{ColorUtility.ToHtmlStringRGB(textColor * 0.6f)}>[{GameInput.Glyph(GameInput.Interact)}] close</color></size>";
            messagePanel.SetActive(true);
            messageHideTime = float.PositiveInfinity;
            IsReading = true;
        }

        public void DismissReading()
        {
            if (!IsReading) return;
            IsReading = false;
            messagePanel.SetActive(false);
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGO.transform.SetParent(transform, false);
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1f;

            crosshair = new GameObject("Crosshair", typeof(Image)).GetComponent<Image>();
            crosshair.transform.SetParent(canvasGO.transform, false);
            crosshair.raycastTarget = false;

            promptText = CreateText("Prompt", canvasGO.transform, 22, TextAlignmentOptions.Center);
            var promptRect = promptText.rectTransform;
            promptRect.anchoredPosition = new Vector2(0, -48);
            promptRect.sizeDelta = new Vector2(600, 40);

            messagePanel = new GameObject("Message Panel", typeof(Image));
            messagePanel.transform.SetParent(canvasGO.transform, false);
            var panelImage = messagePanel.GetComponent<Image>();
            panelImage.color = panelColor;
            panelImage.raycastTarget = false;
            var panelRect = panelImage.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0, 40);
            panelRect.sizeDelta = new Vector2(760, 130);

            messageText = CreateText("Message", messagePanel.transform, 22, TextAlignmentOptions.Center);
            var messageRect = messageText.rectTransform;
            messageRect.anchorMin = Vector2.zero;
            messageRect.anchorMax = Vector2.one;
            messageRect.offsetMin = new Vector2(24, 14);
            messageRect.offsetMax = new Vector2(-24, -14);

            toastStack = UIFactory.Rect("Toasts", canvasGO.transform);
            UIFactory.Place(toastStack, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -44), new Vector2(520, 200));
            var toastLayout = toastStack.gameObject.AddComponent<VerticalLayoutGroup>();
            toastLayout.childAlignment = TextAnchor.UpperRight;
            toastLayout.childControlHeight = true;
            toastLayout.childControlWidth = true;
            toastLayout.childForceExpandHeight = false;
            toastLayout.spacing = 2;

            clockText = UIFactory.Text("Clock", canvasGO.transform, "", 16, new Color(textColor.r, textColor.g, textColor.b, 0.7f), TextAlignmentOptions.TopRight);
            UIFactory.Place(clockText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -12), new Vector2(300, 26));

            hintText = UIFactory.Text("Hint", canvasGO.transform, "", 17, textColor, TextAlignmentOptions.Top);
            UIFactory.Place(hintText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(1000, 72));
            hintText.gameObject.SetActive(false);
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, float size, TextAlignmentOptions alignment)
        {
            var text = new GameObject(name, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            if (UIFactory.Font != null) text.font = UIFactory.Font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = textColor;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }
    }
}
