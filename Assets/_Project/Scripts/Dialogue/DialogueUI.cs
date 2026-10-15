using System.Collections.Generic;
using Semblance.Core;
using Semblance.Interaction;
using Semblance.Notes;
using Semblance.Residents;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Semblance.Dialogue
{
    /// <summary>
    /// Conversation window: the resident's line (typed out), and numbered choices for each topic,
    /// "Look them over" and "Leave". Choose with the mouse or number keys; Esc leaves.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        public static DialogueUI Instance { get; private set; }

        [SerializeField] private float charactersPerSecond = 55f;
        [SerializeField] private Color textColor = new Color(0.92f, 0.89f, 0.8f);
        [SerializeField] private Color accentColor = new Color(0.85f, 0.65f, 0.35f);
        [SerializeField] private Color panelColor = new Color(0.04f, 0.04f, 0.05f, 0.9f);

        public bool IsOpen => root != null && root.activeSelf;

        private GameObject root;
        private TextMeshProUGUI nameText;
        private TextMeshProUGUI bodyText;
        private RectTransform choiceContainer;
        private readonly List<(Button button, TextMeshProUGUI label)> choiceButtons = new List<(Button, TextMeshProUGUI)>();
        private int highlighted = -1;

        /// <summary>Gamepad/arrow-key choice highlight (underlined), confirmed with the south button.</summary>
        private void Highlight(int index)
        {
            highlighted = index;
            for (int i = 0; i < choiceButtons.Count; i++)
                choiceButtons[i].label.fontStyle = i == highlighted ? FontStyles.Underline : FontStyles.Normal;
        }
        private readonly List<System.Action> choiceActions = new List<System.Action>();

        private Resident resident;
        private PlayerInteractor interactor;
        private float revealProgress;
        private string currentTopic;

        private void Awake()
        {
            Instance = this;
            BuildUI();
            root.SetActive(false);
        }

        public void Open(Resident target, PlayerInteractor by)
        {
            resident = target;
            interactor = by;
            InputLock.Push(this, Close);
            root.SetActive(true);
            nameText.text = $"{resident.DisplayName}  <size=70%><color=#{ColorUtility.ToHtmlStringRGB(textColor * 0.7f)}>{resident.Profile.occupation}</color></size>";
            currentTopic = null;
            confronting = false;
            string greeting = resident.GetGreeting();
            Say(greeting);
            RefreshChoices();
            // The first thing they ever said to you matters (does a stranger already know your name?).
            var first = Notebook.Instance?.Record(NoteKind.Statement, resident.DisplayName, "First greeting", Notebook.StripTags(greeting), resident.DisplayName, $"greeting|{resident.DisplayName}");
            if (first != null && first.fact == null) first.fact = resident.GreetingFact;
            if (resident.GreetingFact != null && Notebook.Instance != null)
            {
                // A stranger who calls you by name. You never told anyone. It stands on its own.
                var slip = Notebook.Instance.Record(NoteKind.Statement, resident.DisplayName, "Called me by name", Notebook.StripTags(greeting) + " I never told anyone here my name.", resident.DisplayName, $"named|{resident.DisplayName}");
                slip.fact = resident.GreetingFact;
                Notebook.Instance.SetSuspicious(slip.id, true);
            }
            Opened?.Invoke(resident);
        }

        public void Close()
        {
            root.SetActive(false);
            InputLock.Pop(this);
            var was = resident;
            resident = null;
            if (was != null)
            {
                if (was.TryGetComponent<ResidentBehaviour>(out var b))
                    b.IsTalking = false;
                Closed?.Invoke(was);
            }
        }

        private void Say(string line)
        {
            bodyText.text = line;
            bodyText.maxVisibleCharacters = 0;
            revealProgress = 0f;
        }

        private bool confronting;

        private void RefreshChoices()
        {
            choiceActions.Clear();
            var labels = new List<string>();
            var currentFlags = new List<bool>();

            if (confronting)
            {
                foreach (var (label, text) in ConfrontOptions())
                {
                    string about = text;
                    labels.Add(label);
                    currentFlags.Add(false);
                    choiceActions.Add(() =>
                    {
                        confronting = false;
                        currentTopic = "confront";
                        string reply = resident.Confront(about);
                        Say(reply);
                        Notebook.Instance?.Record(NoteKind.Statement, resident.DisplayName, "When I confronted them", Notebook.StripTags(reply), resident.DisplayName,
                            $"confront|{resident.DisplayName}|{about.GetHashCode()}");
                        RefreshChoices();
                        TopicAsked?.Invoke(resident, "confront");
                    });
                }
                labels.Add("Never mind");
                currentFlags.Add(false);
                choiceActions.Add(() => { confronting = false; RefreshChoices(); });
            }
            else
            {
                foreach (var topic in resident.GetTopics())
                {
                    var t = topic;
                    labels.Add(t.title);
                    currentFlags.Add(t.title == currentTopic);
                    choiceActions.Add(() => { currentTopic = t.title; Say(t.response); RefreshChoices(); LogTopic(t); TopicAsked?.Invoke(resident, t.title); });
                }
                labels.Add("Look them over");
                currentFlags.Add(currentTopic == "observe");
                choiceActions.Add(() => { currentTopic = "observe"; Say("<color=#" + ColorUtility.ToHtmlStringRGB(accentColor) + ">You look them over.</color>\n" + resident.GetObservation()); RefreshChoices(); LogObservation(); TopicAsked?.Invoke(resident, "observe"); });
                labels.Add("Shake hands");
                currentFlags.Add(currentTopic == "hands");
                choiceActions.Add(() => { currentTopic = "hands"; Say(resident.ShakeHands()); RefreshChoices(); TopicAsked?.Invoke(resident, "hands"); });
                if (ConfrontOptions().Count > 0)
                {
                    labels.Add("<color=#c07050>Confront them...</color>");
                    currentFlags.Add(false);
                    choiceActions.Add(() => { confronting = true; RefreshChoices(); });
                }
                labels.Add("Leave");
                currentFlags.Add(false);
                choiceActions.Add(Close);
            }

            EnsureButtonCount(labels.Count);
            highlighted = -1;
            for (int i = 0; i < choiceButtons.Count; i++)
            {
                choiceButtons[i].label.fontStyle = FontStyles.Normal;
                bool active = i < labels.Count;
                choiceButtons[i].button.gameObject.SetActive(active);
                if (!active)
                    continue;
                string number = i < 9 ? (i + 1).ToString() : "-";
                choiceButtons[i].label.text = $"<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>{number}.</color> {labels[i]}";
                choiceButtons[i].label.alpha = currentFlags[i] ? 0.45f : 1f;
            }
        }

        /// <summary>Things the detective can put to this person: linked contradictions and evidence about them.</summary>
        private List<(string label, string text)> ConfrontOptions()
        {
            var options = new List<(string, string)>();
            var notebook = Notebook.Instance;
            if (notebook == null || resident == null) return options;
            foreach (var link in notebook.Contradictions)
            {
                var a = notebook.Get(link.a);
                var b = notebook.Get(link.b);
                if (a == null || b == null || (a.subject != resident.DisplayName && b.subject != resident.DisplayName)) continue;
                string summary = Short(a.text) + " / " + Short(b.text);
                options.Add(("\"" + Short(a.text, 34) + "\"...", summary));
                if (options.Count >= 5) return options;
            }
            foreach (var e in notebook.EntriesFor(resident.DisplayName))
            {
                if (e.kind != NoteKind.Evidence) continue;
                options.Add(("The " + e.title.ToLowerInvariant(), e.text));
                if (options.Count >= 6) break;
            }
            return options;
        }

        private static string Short(string text, int max = 60) => string.IsNullOrEmpty(text) || text.Length <= max ? text : text.Substring(0, max - 1) + "...";

        /// <summary>Raised when the player asks a topic ("observe" for looking them over).</summary>
        public static event System.Action<Resident, string> TopicAsked;
        public static event System.Action<Resident> Opened;
        public static event System.Action<Resident> Closed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics() { TopicAsked = null; Opened = null; Closed = null; }

        public Resident Current => resident;

        private void LogTopic(DialogueTopic topic)
        {
            var notebook = Notebook.Instance;
            if (notebook == null) return;
            string text = Notebook.StripTags(topic.response);
            var mods = resident.Avatar.Modifications;
            bool isGossip = resident.Profile.gossip.Exists(g => g.title == topic.title)
                || (mods != null && mods.ExtraGossip.Exists(g => g.title == topic.title));
            if (isGossip)
            {
                string aboutName = topic.title.Substring(6);
                var about = Resident.FindByName(aboutName);
                string subject = about != null ? about.DisplayName : aboutName;
                var g = notebook.Record(NoteKind.Gossip, subject, resident.DisplayName + " says", text, resident.DisplayName, $"gossip|{resident.DisplayName}|{subject}");
                if (g.fact == null) g.fact = topic.fact;
                if (g.explains == null) g.explains = topic.explains;
            }
            else
            {
                var e = notebook.Record(NoteKind.Statement, resident.DisplayName, topic.title, text, resident.DisplayName, $"topic|{resident.DisplayName}|{topic.title}|{Core.StableHash.Of(text)}");
                if (e.fact == null) e.fact = topic.fact;
            }
        }

        private void LogObservation()
        {
            var notebook = Notebook.Instance;
            if (notebook == null) return;
            var visible = resident.VisibleFeatures();
            string set = NoteFacts.Set(visible);
            int day = Core.GameClock.Instance != null ? Core.GameClock.Instance.Day : 1;
            if (Core.GameSettings.ListObservedDetails)
            {
                // One note per detail (first time it's seen), each checkable against the records...
                foreach (var f in visible)
                {
                    var d = FeatureCatalog.Get(f.id);
                    string line = d.Describe(f.side);
                    var e = notebook.Record(NoteKind.Observation, resident.DisplayName, "Looked them over", resident.Named(line), null, $"observe|{resident.DisplayName}|{line}");
                    if (e.fact == null) e.fact = NoteFacts.Observed(f, "");
                }
            }
            // ...and one note per day for the whole look, so something missing (or changed overnight) can be shown.
            string summary = Core.GameSettings.ListObservedDetails
                ? $"Day {day}: I looked {resident.FirstName} over carefully. Everything I could see is in these notes, and nothing else."
                : $"Day {day}: I studied {resident.FirstName} closely. I'll have to trust my own eyes about what I saw.";
            var all = notebook.Record(NoteKind.Observation, resident.DisplayName, "A careful look", summary, null, $"observe-all|{resident.DisplayName}|{day}");
            if (all.fact == null) all.fact = NoteFacts.Observed(null, set);
        }

        private void Choose(int index)
        {
            if (index >= 0 && index < choiceActions.Count)
                choiceActions[index]();
        }

        private void LateUpdate()
        {
            if (!IsOpen)
                return;

            // Typewriter reveal
            int before = bodyText.maxVisibleCharacters;
            revealProgress += charactersPerSecond * Time.unscaledDeltaTime;
            bodyText.maxVisibleCharacters = (int)Mathf.Min(revealProgress, 100000f);
            // Mumbled voice: a blip every few letters, pitched per person.
            if (resident != null && bodyText.maxVisibleCharacters / 3 != before / 3 && bodyText.maxVisibleCharacters < bodyText.textInfo.characterCount)
            {
                float pitch = 0.75f + Mathf.Abs(resident.DisplayName.GetHashCode() % 50) / 100f;
                bool whisper = (resident.Avatar.GetSpeechFlags() & SpeechFlags.Whisper) != 0;
                Audio.GameAudio.Play(Audio.GameAudio.Variant("voice_blip", 5), whisper ? 0.08f : 0.22f, pitch * Random.Range(0.95f, 1.05f));
            }

            // Mouths move while the line is still being typed out.
            if (resident != null && resident.TryGetComponent<ResidentBehaviour>(out var behaviour))
                behaviour.IsTalking = bodyText.maxVisibleCharacters < bodyText.textInfo.characterCount;

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                revealProgress = 99999f;
            int digit = GameInput.PressedDigit();
            if (digit >= 0)
            {
                Choose(digit);
                return;
            }
            // Gamepad (and arrow keys): move a highlight through the choices, confirm with the south button.
            if (choiceActions.Count > 0)
            {
                if (GameInput.Pressed(GameInput.NextChoice)) Highlight(Mathf.Min(highlighted + 1, choiceActions.Count - 1));
                if (GameInput.Pressed(GameInput.PrevChoice)) Highlight(Mathf.Max(highlighted - 1, 0));
                if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame && highlighted >= 0)
                    Choose(highlighted);
            }
        }

        // ------------------------------------------------------------ UI construction

        private void BuildUI()
        {
            if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGO = new GameObject("Dialogue Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1f;

            root = new GameObject("Dialogue Panel", typeof(Image));
            root.transform.SetParent(canvasGO.transform, false);
            var panel = root.GetComponent<Image>();
            panel.color = panelColor;
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0, 24);
            panelRect.sizeDelta = new Vector2(1100, 300);

            nameText = CreateText("Name", root.transform, 26, TextAlignmentOptions.TopLeft);
            nameText.color = accentColor;
            SetRect(nameText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -14), new Vector2(640, 36));

            bodyText = CreateText("Line", root.transform, 21, TextAlignmentOptions.TopLeft);
            SetRect(bodyText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -54), new Vector2(640, 230));

            var container = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup));
            container.transform.SetParent(root.transform, false);
            choiceContainer = container.GetComponent<RectTransform>();
            SetRect(choiceContainer, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -12), new Vector2(380, 280), new Vector2(1, 1));
            var layout = container.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 2;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
        }

        private void EnsureButtonCount(int count)
        {
            while (choiceButtons.Count < count)
            {
                int index = choiceButtons.Count;
                var go = new GameObject("Choice " + index, typeof(Image), typeof(Button));
                go.transform.SetParent(choiceContainer, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(380, 23);
                var image = go.GetComponent<Image>();
                image.color = new Color(1, 1, 1, 0.0f);
                var button = go.GetComponent<Button>();
                var colors = button.colors;
                colors.highlightedColor = new Color(1, 1, 1, 0.08f);
                colors.pressedColor = new Color(1, 1, 1, 0.15f);
                button.colors = colors;
                button.onClick.AddListener(() => Choose(index));

                var label = CreateText("Label", go.transform, 18, TextAlignmentOptions.MidlineLeft);
                SetRect(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                label.rectTransform.offsetMin = new Vector2(8, 0);
                choiceButtons.Add((button, label));
            }
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

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot ?? new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
