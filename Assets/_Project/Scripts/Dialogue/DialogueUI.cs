using System.Collections.Generic;
using System.Linq;
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
    /// Conversation window: the resident's line (typed out), and clickable choices for each topic,
    /// "Look them over" and "Leave". Choose with the mouse (or a gamepad); Esc leaves. The notebook opens over it.
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
        private ScrollRect choiceScroll;
        private readonly List<(Button button, TextMeshProUGUI label)> choiceButtons = new List<(Button, TextMeshProUGUI)>();
        private int highlighted = -1;

        /// <summary>Gamepad/arrow-key choice highlight (underlined), confirmed with the south button.</summary>
        private void Highlight(int index)
        {
            highlighted = index;
            for (int i = 0; i < choiceButtons.Count; i++)
                choiceButtons[i].label.fontStyle = i == highlighted ? FontStyles.Underline : FontStyles.Normal;
            // Keep the highlighted line in view.
            if (choiceScroll != null && choiceActions.Count > 1)
                choiceScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(index / (float)(choiceActions.Count - 1));
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
            menu = Menu.Root;
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
        private enum Menu { Root, People, Clues, Confront }
        private Menu menu = Menu.Root;

        private sealed class Choice
        {
            public string Label;
            public System.Action Action;
            public bool Asked;      // greyed: already asked and answered
            public bool Current;    // the thing they're answering right now
            public bool IsBack;
        }

        /// <summary>"About Nell Hollis" is a person; "About yourself" and "About Semblance" stay on the front page.</summary>
        private static bool IsPersonTopic(DialogueTopic t) =>
            t.title.StartsWith("About ") && t.title != ResidentProfile.TopicSelf && t.title != ResidentProfile.TopicTown;

        private bool WasAsked(string key) => resident != null && Save.SaveSystem.Flags.Contains(ClueTopics.AskedFlag(resident, key));
        private void MarkAsked(string key) { if (resident != null) Save.SaveSystem.Flags.Add(ClueTopics.AskedFlag(resident, key)); }

        /// <summary>
        /// The choices are a small menu: the general questions on the front page, people and found things on their
        /// own pages, so the list never runs off the screen. Anything already asked is greyed but can be asked again.
        /// </summary>
        private void RefreshChoices()
        {
            var choices = new List<Choice>();
            string accent = ColorUtility.ToHtmlStringRGB(accentColor);
            if (confronting) menu = Menu.Confront;

            switch (menu)
            {
                case Menu.Confront:
                    foreach (var (label, text) in ConfrontOptions())
                    {
                        string about = text;
                        choices.Add(new Choice { Label = label, Asked = WasAsked("confront|" + about.GetHashCode()), Action = () =>
                        {
                            confronting = false;
                            menu = Menu.Root;
                            currentTopic = "confront";
                            MarkAsked("confront|" + about.GetHashCode());
                            string reply = about.StartsWith("evidence|") ? resident.Confront(about, about.Substring(9)) : resident.Confront(about);
                            Say(reply);
                            Notebook.Instance?.Record(NoteKind.Statement, resident.DisplayName, "When I confronted them", Notebook.StripTags(reply), resident.DisplayName,
                                $"confront|{resident.DisplayName}|{about.GetHashCode()}");
                            RefreshChoices();
                            TopicAsked?.Invoke(resident, "confront");
                        } });
                    }
                    choices.Add(new Choice { Label = "Never mind", IsBack = true, Action = () => { confronting = false; menu = Menu.Root; RefreshChoices(); } });
                    break;

                case Menu.People:
                    foreach (var topic in resident.GetTopics())
                    {
                        if (!IsPersonTopic(topic)) continue;
                        var t = topic;
                        choices.Add(new Choice { Label = t.title.Substring(6), Asked = WasAsked(t.title), Current = t.title == currentTopic, Action = () => AskTopic(t) });
                    }
                    choices.Add(new Choice { Label = "Back", IsBack = true, Action = () => { menu = Menu.Root; RefreshChoices(); } });
                    break;

                case Menu.Clues:
                    foreach (var q in ClueTopics.For(resident))
                    {
                        var question = q;
                        choices.Add(new Choice
                        {
                            Label = (question.Parent != null ? "<color=#" + accent + ">> </color>" : "") + question.Question,
                            Asked = WasAsked(question.Key),
                            Current = currentTopic == "clue:" + question.Key,
                            Action = () => AskClue(question),
                        });
                    }
                    choices.Add(new Choice { Label = "Back", IsBack = true, Action = () => { menu = Menu.Root; RefreshChoices(); } });
                    break;

                default:
                {
                    var topics = resident.GetTopics();
                    foreach (var topic in topics)
                    {
                        if (IsPersonTopic(topic)) continue;
                        var t = topic;
                        choices.Add(new Choice { Label = t.title, Asked = WasAsked(t.title), Current = t.title == currentTopic, Action = () => AskTopic(t) });
                    }
                    int people = topics.Count(IsPersonTopic);
                    if (people > 0)
                        choices.Add(new Choice { Label = $"About people... <size=75%><color=#{accent}>({people})</color></size>", Action = () => { menu = Menu.People; RefreshChoices(); } });
                    var clues = ClueTopics.For(resident);
                    if (clues.Count > 0)
                    {
                        int fresh = clues.Count(q => !WasAsked(q.Key));
                        choices.Add(new Choice { Label = $"About what I've found... <size=75%><color=#{accent}>({(fresh > 0 ? fresh + " new" : clues.Count.ToString())})</color></size>", Action = () => { menu = Menu.Clues; RefreshChoices(); } });
                    }
                    choices.Add(new Choice { Label = "Look them over", Asked = WasAsked("observe"), Current = currentTopic == "observe", Action = () =>
                    {
                        currentTopic = "observe"; MarkAsked("observe");
                        Say("<color=#" + accent + ">You look them over.</color>\n" + resident.GetObservation());
                        RefreshChoices(); LogObservation(); TopicAsked?.Invoke(resident, "observe");
                    } });
                    choices.Add(new Choice { Label = "Shake hands", Asked = WasAsked("hands"), Current = currentTopic == "hands", Action = () =>
                    {
                        currentTopic = "hands"; MarkAsked("hands"); Say(resident.ShakeHands()); RefreshChoices(); TopicAsked?.Invoke(resident, "hands");
                    } });
                    if (ConfrontOptions().Count > 0)
                        choices.Add(new Choice { Label = "<color=#c07050>Confront them...</color>", Action = () => { confronting = true; menu = Menu.Confront; RefreshChoices(); } });
                    choices.Add(new Choice { Label = "Leave", IsBack = true, Action = Close });
                    break;
                }
            }

            choiceActions.Clear();
            EnsureButtonCount(choices.Count);
            highlighted = -1;
            for (int i = 0; i < choiceButtons.Count; i++)
            {
                choiceButtons[i].label.fontStyle = FontStyles.Normal;
                bool active = i < choices.Count;
                choiceButtons[i].button.gameObject.SetActive(active);
                if (!active) continue;
                var c = choices[i];
                choiceActions.Add(c.Action);
                string label = c.IsBack ? $"<color=#{accent}>< </color>{c.Label}" : c.Label;
                choiceButtons[i].label.text = label;
                // Greyed once asked, dimmest while it's the thing being answered.
                choiceButtons[i].label.alpha = c.Current ? 0.4f : c.Asked ? 0.55f : 1f;
                // Long questions wrap onto a second line; the row grows to fit.
                float h = choiceButtons[i].label.GetPreferredValues(choiceButtons[i].label.text, 372f, 0f).y;
                UIFactory.Layout(choiceButtons[i].button.GetComponent<RectTransform>(), preferredHeight: Mathf.Max(24f, h + 6f));
            }
            if (choiceScroll != null) choiceScroll.verticalNormalizedPosition = 1f;
        }

        private void AskTopic(DialogueTopic t)
        {
            currentTopic = t.title;
            MarkAsked(t.title);
            Say(t.response);
            RefreshChoices();
            LogTopic(t);
            TopicAsked?.Invoke(resident, t.title);
        }

        /// <summary>A question about something found: answered in their own voice, written down, and maybe opening a follow-up.</summary>
        private void AskClue(ClueQuestion q)
        {
            currentTopic = "clue:" + q.Key;
            bool first = !WasAsked(q.Key);
            MarkAsked(q.Key);
            string answer = resident.Format(q.AnswerFor(resident), "clue:" + q.Key);
            Say(answer);
            var noted = Notebook.Instance?.Record(NoteKind.Statement, resident.DisplayName, q.Question, Notebook.StripTags(answer), resident.DisplayName, $"clue|{resident.DisplayName}|{q.Key}");
            if (noted != null && noted.fact == null) noted.fact = NoteFacts.Claims(answer);
            RefreshChoices();
            TopicAsked?.Invoke(resident, "clue:" + q.Key);
            if (first) Case.MimicAwareness.Instance?.Raise(1.5f, "asked about a clue");
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
                options.Add(("The " + e.title.ToLowerInvariant(), "evidence|" + (e.key != null && e.key.StartsWith("evidence|") ? e.key.Substring(9) : e.title)));
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
                ? $"Everything I could see on {resident.FirstName} today is in the notes above, and nothing else. If the records list something that isn't here, it wasn't there to see."
                : $"I studied {resident.FirstName} closely. I'll have to trust my own eyes about what I saw.";
            var all = notebook.Record(NoteKind.Observation, resident.DisplayName, "Nothing else to see", summary, null, $"observe-all|{resident.DisplayName}|{day}");
            if (all.fact == null) all.fact = NoteFacts.Observed(null, set);
        }

        private void Choose(int index)
        {
            if (index >= 0 && index < choiceActions.Count)
                choiceActions[index]();
        }

        private void LateUpdate()
        {
            if (!IsOpen || !ReferenceEquals(InputLock.Top, this))
                return; // the notebook (or a chooser) is open over the conversation: it waits

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

            // Mouths move while a spoken line is still being typed out; looking them over or a handshake is you, not them.
            if (resident != null && resident.TryGetComponent<ResidentBehaviour>(out var behaviour))
                behaviour.IsTalking = bodyText.maxVisibleCharacters < bodyText.textInfo.characterCount
                    && currentTopic != "observe" && currentTopic != "hands";

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                revealProgress = 99999f;
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

            // The choices scroll if there are more than fit (mouse wheel, or the gamepad highlight).
            choiceContainer = UIFactory.ScrollList("Choices", root.transform, out choiceScroll, 2f, new RectOffset(0, 6, 4, 4));
            SetRect((RectTransform)choiceScroll.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -12), new Vector2(410, 280), new Vector2(1, 1));
            bodyText.rectTransform.sizeDelta = new Vector2(630, 230);
        }

        private void EnsureButtonCount(int count)
        {
            while (choiceButtons.Count < count)
            {
                int index = choiceButtons.Count;
                var go = new GameObject("Choice " + index, typeof(Image), typeof(Button));
                go.transform.SetParent(choiceContainer, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(380, 24);
                UIFactory.Layout(go.GetComponent<RectTransform>(), preferredHeight: 24);
                // The row's backing is white and the button tints it: invisible at rest, a pale bar under the mouse.
                var image = go.GetComponent<Image>();
                image.color = Color.white;
                var button = go.GetComponent<Button>();
                var colors = button.colors;
                colors.normalColor = new Color(1, 1, 1, 0f);
                colors.highlightedColor = new Color(1, 0.92f, 0.75f, 0.16f);
                colors.selectedColor = new Color(1, 0.92f, 0.75f, 0.16f);
                colors.pressedColor = new Color(1, 0.92f, 0.75f, 0.3f);
                colors.fadeDuration = 0.08f;
                button.colors = colors;
                button.onClick.AddListener(() => Choose(index));

                var label = CreateText("Label", go.transform, 17, TextAlignmentOptions.MidlineLeft);
                SetRect(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                label.rectTransform.offsetMin = new Vector2(8, 0);
                label.rectTransform.offsetMax = new Vector2(-6, 0);
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
