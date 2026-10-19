using System.Collections.Generic;
using System.Linq;
using System.Text;
using Semblance.Core;
using Semblance.Residents;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Semblance.Notes
{
    /// <summary>
    /// Tab opens the notebook. Left: the Case File and a page per person. Right: that page's entries.
    /// Star (*) marks an entry suspicious; tick two entries and press "Link contradiction" to pair them.
    /// </summary>
    public class NotebookUI : MonoBehaviour
    {
        public static NotebookUI Instance { get; private set; }

        /// <summary>Extra sections other systems can add to the Case File page (evidence, mysteries...).</summary>
        public static readonly List<System.Func<string>> CaseFileSections = new List<System.Func<string>>();

        private GameObject root;
        private RectTransform subjectList;
        private RectTransform entryList;
        private ScrollRect entryScroll;
        private TextMeshProUGUI header;
        private Button linkButton;
        private TextMeshProUGUI linkLabel;
        private GameObject photoViewer;
        private RawImage photoViewerImage;
        private TextMeshProUGUI photoViewerCaption;

        private string currentSubject = Notebook.CaseSubject;
        // Ticked notes stay ticked across pages and even across closing the book, so two people's stories can be linked.
        private readonly HashSet<int> selected = new HashSet<int>();
        private bool dirty;
        private GameObject notesField;
        private TMP_InputField notesInput;
        private GameObject chooser;

        public bool IsOpen => root.activeSelf;
        private bool froze;

        private void Awake()
        {
            Instance = this;
            Build();
            root.SetActive(false);
            photoViewer.SetActive(false);
        }

        private void Start()
        {
            if (Notebook.Instance != null)
                Notebook.Instance.Changed += () => dirty = true;
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open(string subject = null)
        {
            if (subject != null)
                currentSubject = subject;
            root.SetActive(true);
            if (root.GetComponent<UI.SelectFirstOnEnable>() == null) root.AddComponent<UI.SelectFirstOnEnable>().enabled = true;
            else UI.SelectFirstOnEnable.Select(root);
            InputLock.Push(this, Close);
            froze = Time.timeScale != 0f; // the town waits while you read (unless something else already stopped it)
            Time.timeScale = 0f;
            Audio.GameAudio.Play("page_flip", 0.5f);
            Refresh();
        }

        public void Close()
        {
            if (photoViewer.activeSelf)
            {
                photoViewer.SetActive(false);
                return;
            }
            if (chooser != null && chooser.activeSelf)
            {
                chooser.SetActive(false);
                return;
            }
            if (notesInput != null && notesInput.isFocused)
            {
                notesInput.DeactivateInputField();
                return;
            }
            GameInput.TextEntry = false;
            root.SetActive(false);
            InputLock.Pop(this);
            if (froze) { froze = false; Time.timeScale = 1f; }
        }

        private void LateUpdate()
        {
            GameInput.TextEntry = IsOpen && notesInput != null && notesInput.isFocused;
            if (IsOpen && dirty)
                Refresh();
        }

        // ------------------------------------------------------------ Content

        private void Refresh()
        {
            dirty = false;
            var notebook = Notebook.Instance;
            if (notebook == null)
                return;

            UIFactory.Clear(subjectList);
            {
                // The front page: the detective's own file.
                bool onMe = currentSubject == ProfileTab;
                string me = "Det. " + (Case.CaseDirector.Instance != null ? Case.CaseDirector.Instance.DetectiveName : "Klein");
                var p = UIFactory.Button("Tab Me", subjectList, onMe ? "<b>" + me + "</b>" : me, 16, UIFactory.Ink,
                    onMe ? UIFactory.Paper : UIFactory.PaperDark, () => { currentSubject = ProfileTab; Refresh(); });
                p.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
                UIFactory.Layout(p, preferredHeight: 30);
            }
            {
                bool onMap = currentSubject == MapTab;
                var m = UIFactory.Button("Tab Map", subjectList, onMap ? "<b>Map</b>" : "Map", 16, UIFactory.Ink,
                    onMap ? UIFactory.Paper : UIFactory.PaperDark, () => { currentSubject = MapTab; Refresh(); });
                m.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
                UIFactory.Layout(m, preferredHeight: 30);
            }
            {
                bool onNotes = currentSubject == NotesTab;
                var n = UIFactory.Button("Tab Notes", subjectList, onNotes ? "<b>My notes</b>" : "My notes", 16, UIFactory.Ink,
                    onNotes ? UIFactory.Paper : UIFactory.PaperDark, () => { currentSubject = NotesTab; Refresh(); });
                n.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
                UIFactory.Layout(n, preferredHeight: 30);
            }
            foreach (var subject in notebook.Subjects)
            {
                string s = subject;
                int score = s == Notebook.CaseSubject ? 0 : notebook.SuspicionScore(s);
                string marks = score > 0 ? $"  <color={UIFactory.Hex(UIFactory.Red)}>{new string('*', Mathf.Min(score, 6))}</color>" : "";
                bool current = s == currentSubject;
                var b = UIFactory.Button("Tab " + s, subjectList, (current ? "<b>" : "") + s + (current ? "</b>" : "") + marks, 16, UIFactory.Ink,
                    current ? UIFactory.Paper : UIFactory.PaperDark, () => { currentSubject = s; Refresh(); });
                b.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
                UIFactory.Layout(b, preferredHeight: 30);
            }

            UIFactory.Clear(entryList);
            bool notesPage = currentSubject == NotesTab;
            if (notesField != null) notesField.SetActive(notesPage);
            entryList.parent.parent.gameObject.SetActive(!notesPage);
            linkButton.gameObject.SetActive(!notesPage);
            pendingLabel.gameObject.SetActive(!notesPage);
            if (notesPage)
            {
                header.text = "<b>MY NOTES</b>  <size=70%>in your own hand</size>";
                if (notesInput != null && notesInput.text != notebook.PlayerNotes) notesInput.SetTextWithoutNotify(notebook.PlayerNotes);
            }
            else if (currentSubject == ProfileTab)
                BuildProfile(notebook);
            else if (currentSubject == MapTab)
                BuildMap();
            else if (currentSubject == Notebook.CaseSubject)
                BuildCaseFile(notebook);
            else
                BuildSubjectPage(notebook, currentSubject);

            int left = notebook.LinksLeftToday;
            linkButton.interactable = selected.Count == 2 && left > 0;
            linkLabel.text = left <= 0 ? "No more links today. Sleep on it."
                : selected.Count == 2 ? $"Link contradiction ({left} left today)" : $"Tick two entries to link ({selected.Count}/2)";
            // What's ticked so far, wherever it was ticked, so a second note can be found on another page.
            if (selected.Count == 1)
            {
                var pending = notebook.Get(selected.First());
                pendingLabel.text = pending != null ? $"<color=#00000099>Ticked: <b>{pending.subject}</b>, \"{Short(pending.text, 46)}\". Tick another on any page.</color>" : "";
            }
            else pendingLabel.text = "";
            Canvas.ForceUpdateCanvases();
        }

        private const string MapTab = "~map";
        private const string ProfileTab = "~me";
        private const string NotesTab = "~notes";
        private TextMeshProUGUI pendingLabel;

        private static string Short(string text, int max) => string.IsNullOrEmpty(text) || text.Length <= max ? text : text.Substring(0, max - 1) + "...";

        /// <summary>A small question with a few answers, over the page (who a link points at; what a note is worth).</summary>
        private void Ask(string question, List<(string label, System.Action pick)> options)
        {
            if (chooser == null)
            {
                var dim = UIFactory.Panel("Chooser", root.transform, new Color(0, 0, 0, 0.55f));
                UIFactory.Stretch(dim.rectTransform);
                chooser = dim.gameObject;
            }
            UIFactory.Clear(chooser.transform);
            var box = UIFactory.Panel("Box", chooser.transform, UIFactory.Paper);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 70 + options.Count * 40 + 44));
            var title = UIFactory.Text("Question", box.transform, "<b>" + question + "</b>", 17, UIFactory.Ink, TextAlignmentOptions.Top);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(480, 50));
            float y = -66;
            foreach (var (label, pick) in options)
            {
                var action = pick;
                var b = UIFactory.Button("Option", box.transform, label, 15, UIFactory.Ink, UIFactory.PaperDark, () => { chooser.SetActive(false); action?.Invoke(); Refresh(); });
                UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(480, 34));
                y -= 40;
            }
            var cancel = UIFactory.Button("Cancel", box.transform, "Never mind", 14, UIFactory.Ink, new Color(0, 0, 0, 0.06f), () => chooser.SetActive(false));
            UIFactory.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(160, 28));
            chooser.SetActive(true);
            chooser.transform.SetAsLastSibling();
            UI.SelectFirstOnEnable.Select(chooser);
        }

        /// <summary>The people a link could count against: the subjects and speakers of its two notes.</summary>
        private static List<string> Candidates(NoteEntry a, NoteEntry b)
        {
            var names = new List<string>();
            foreach (var n in new[] { a.subject, a.source, b.subject, b.source })
                if (!string.IsNullOrEmpty(n) && n != Notebook.CaseSubject && !names.Contains(n)) names.Add(n);
            return names;
        }

        private void AskAgainst(ContradictionLink link)
        {
            var nb = Notebook.Instance;
            var a = nb.Get(link.a); var b = nb.Get(link.b);
            if (a == null || b == null) return;
            var names = Candidates(a, b);
            if (names.Count <= 1) return;
            var options = names.Select(n => (n, (System.Action)(() => nb.SetAgainst(link, n)))).ToList();
            if (names.Count == 2) options.Add(("Both of them", () => nb.SetAgainst(link, "both")));
            Ask("Who does this contradiction count against?", options);
        }

        private void AskVerdict(NoteEntry entry)
        {
            var nb = Notebook.Instance;
            var options = new List<(string, System.Action)>
            {
                ("Checks out: I've confirmed this is true", () => nb.SetVerdict(entry.id, Verdict.Verified)),
                ("Explained: there's an innocent reason for it", () => nb.SetVerdict(entry.id, Verdict.Explained)),
            };
            if (entry.verdict != 0) options.Add(("Clear my ruling", () => nb.SetVerdict(entry.id, Verdict.None)));
            Ask("What do I make of this note?", options);
        }

        /// <summary>
        /// The detective's own file: who you are, where you are, what state you're in, and everything that has
        /// happened to you that you can't explain.
        /// </summary>
        private void BuildProfile(Notebook notebook)
        {
            var clock = GameClock.Instance;
            string name = Case.CaseDirector.Instance != null ? Case.CaseDirector.Instance.DetectiveName : "Klein";
            header.text = $"<b>DET. {name.ToUpperInvariant()}</b>  <size=70%>Barlow County Investigations</size>";

            // Portrait frame with no photograph in it, beside the particulars.
            var row = UIFactory.Rect("Profile Row", entryList);
            UIFactory.Layout(row, preferredHeight: 150);
            var frame = UIFactory.Panel("Portrait", row, UIFactory.Ink, false);
            UIFactory.Place(frame.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, -2), new Vector2(112, 144));
            var inner = UIFactory.Panel("Portrait Inner", frame.transform, UIFactory.PaperDark, false);
            UIFactory.Stretch(inner.rectTransform, new Vector2(3, 3), new Vector2(-3, -3));
            var none = UIFactory.Text("No Photo", inner.transform, "<size=80%>NO\nPHOTOGRAPH\nON FILE</size>", 13, new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.45f), TextAlignmentOptions.Center);
            UIFactory.Stretch(none.rectTransform);
            var corner = UIFactory.Text("Corner", inner.transform, "<size=70%><i>attach</i></size>", 12, new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.3f), TextAlignmentOptions.BottomRight);
            UIFactory.Stretch(corner.rectTransform, new Vector2(0, 4), new Vector2(-6, 0));

            string where = "Semblance";
            var cam = Camera.main;
            if (cam != null)
            {
                var p = cam.transform.position;
                if (p.y < -10f) where = "underground";
                else where = World.LocationMarker.Describe(p);
                if (where.StartsWith("at ")) where = where.Substring(3);
                where = char.ToUpper(where[0]) + where.Substring(1);
            }
            float hour = clock != null ? clock.Hour : 18.5f;
            int day = clock != null ? clock.Day : 1;
            // Awake since half past six (ten, after a collapse); the first evening you came off a long drive.
            float awake = day == 1 ? Mathf.Max(0f, hour - 18.5f) + 9f : hour - (Save.SaveSystem.Flags.Contains("collapsed:" + day) ? 10f : 6.5f);
            string tired = awake < 7f ? "Rested" : awake < 12f ? "Fine" : awake < 15.5f ? "Tired" : awake < 19f ? "Exhausted" : "Dead on my feet";
            float nerve = Events.Nerve.Instance != null ? Events.Nerve.Instance.Value : 100f;
            string nerves = Events.Nerve.Describe(nerve);
            nerves = char.ToUpper(nerves[0]) + nerves.Substring(1);
            string particulars =
                $"<b>Name</b>  {name}\n" +
                $"<b>Age</b>  41\n" +
                $"<b>Occupation</b>  Private investigator, Barlow County Investigations\n" +
                $"<b>Date</b>  {GameFlow.DateLine(day)}, {GameClock.FormatHour(hour)}\n" +
                $"<b>Location</b>  {where}\n" +
                $"<b>Nerves</b>  {nerves} <size=75%><color=#00000066>({Mathf.RoundToInt(nerve)}/100)</color></size>\n" +
                $"<b>Sleep</b>  {tired} <size=75%><color=#00000066>({Mathf.RoundToInt(awake)} hours up)</color></size>";
            var text = UIFactory.Text("Particulars", row, particulars, 15, UIFactory.Ink, TextAlignmentOptions.TopLeft);
            UIFactory.Place(text.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(128, -2), new Vector2(560, 148));

            AddParagraph("<size=110%><b>Assignment</b></size>\nAn anonymous letter, postmarked Semblance: <i>\"One of us is not one of us.\"</i> Find out which of the sixteen inside the sound of the bell isn't who they seem, and name them before the town stops listening."
                + (clock != null ? $" <b>{Mathf.Max(0, clock.LastDay - clock.Day)} day(s) left.</b>" : ""), 8);

            // Everything that has happened to you that shouldn't have.
            var odd = notebook.EntriesFor(Notebook.CaseSubject).Where(e => e.kind == NoteKind.Event).ToList();
            AddParagraph("<size=110%><b>What's happened to me</b></size>", 8);
            if (odd.Count == 0)
                AddParagraph("<i>Nothing yet. Nights out here have a way of changing that.</i>");
            foreach (var e in odd)
                AddEntryRow(e);
        }

        /// <summary>
        /// The county map from the diner, pasted in: the two roads, every farm and who lives there, the buildings in
        /// town, and where you're standing. Blank until you've picked one up.
        /// </summary>
        private void BuildMap()
        {
            if (!World.TownMap.Found)
            {
                header.text = "<b>Map</b>";
                AddParagraph("<i>A blank back page. I don't know this county, and nobody's offered to draw it for me.</i>\n\n"
                    + "Diners out here usually keep a stack of free county maps by the register.");
                return;
            }
            header.text = "<b>Semblance</b>  <size=70%>the county map from Mae's counter, folded into the back</size>";
            const float size = 500f, metres = 470f; // the county runs about 450 m from the motel to the Pruitt place; fits one page
            float k = size / metres;
            var holder = UIFactory.Rect("Map Holder", entryList);
            UIFactory.Layout(holder, preferredHeight: size + 10f);
            var map = UIFactory.Panel("Map", holder, UIFactory.PaperDark, false);
            UIFactory.Place(map.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            var ink = new Color(0.18f, 0.15f, 0.12f, 0.75f);
            var road = UIFactory.Panel("Highway", map.transform, ink, false);
            UIFactory.Place(road.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, Mathf.Min(520f * k, size - 8f)));
            var county = UIFactory.Panel("County Road", map.transform, new Color(ink.r, ink.g, ink.b, 0.45f), false);
            UIFactory.Place(county.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f * k, 3f));
            var compass = UIFactory.Text("North", map.transform, "N", 16, ink, TextAlignmentOptions.Center);
            UIFactory.Place(compass.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(size * 0.42f, -14f), new Vector2(30, 24));

            // Who lives where, the way Mae wrote it in: first names under each farm.
            var households = new Dictionary<string, List<string>>();
            var cast = Case.CaseDirector.Instance != null ? Case.CaseDirector.Instance.Residents : Resident.All;
            foreach (var r in cast)
            {
                if (r == null || r.Profile == null || string.IsNullOrEmpty(r.Profile.home)) continue;
                string home = r.Profile.home == "The rectory" ? "First Church of Semblance" : r.Profile.home;
                if (!households.TryGetValue(home, out var names)) households[home] = names = new List<string>();
                names.Add(r.Profile.home == "The rectory" ? "Father " + r.LastName + " (rectory)" : r.FirstName);
            }

            foreach (var marker in World.LocationMarker.All)
            {
                string name = marker.DisplayName;
                if (marker.Priority < 1 && !name.Contains("square")) continue;
                if (name.Contains("farmhouse") || name.Contains("barn") || name.Contains("cellar") || name.Contains("crypt")) continue;
                if (name == "the churchyard" || name == "the town square") continue; // the church and the crossroads say it
                var p = marker.transform.position;
                var dot = UIFactory.Panel("Dot " + name, map.transform, ink, false);
                UIFactory.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(p.x * k, p.z * k), new Vector2(7, 7));
                string shown = name == "First Church of Semblance" ? "The church" : name;
                string text = households.TryGetValue(name, out var who)
                    ? $"<b>{shown}</b>\n<size=85%><i>{string.Join(", ", who)}</i></size>"
                    : shown;
                // Everything west of the highway is labelled on its left, so names don't run into each other.
                bool left = p.x < 0;
                var label = UIFactory.Text("Label " + name, map.transform, text, 12, UIFactory.Ink, left ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
                UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(left ? 1f : 0f, 0.5f), new Vector2(p.x * k + (left ? -6f : 6f), p.z * k), new Vector2(150, 30));
            }

            var cam = Camera.main;
            if (cam != null)
            {
                var p = cam.transform.position;
                if (p.y < -10f) p = World.LocationMarker.At(p) != null ? World.LocationMarker.At(p).transform.position : p;
                var you = UIFactory.Panel("You", map.transform, UIFactory.Red, false);
                UIFactory.Place(you.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(p.x * k, p.z * k), new Vector2(10, 10));
                you.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
                var youLabel = UIFactory.Text("You Label", map.transform, "<b>you</b>", 12, UIFactory.Red, TextAlignmentOptions.Left);
                UIFactory.Place(youLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(p.x * k + 8f, p.z * k - 12f), new Vector2(60, 16));
            }
        }

        private void BuildSubjectPage(Notebook notebook, string subject)
        {
            var resident = Resident.FindByName(subject);
            header.text = resident != null
                ? $"<b>{subject}</b>  <size=70%>{resident.Profile.occupation}, {resident.Profile.age}. {resident.Profile.home}.</size>"
                : $"<b>{subject}</b>";

            var entries = notebook.EntriesFor(subject).ToList();
            if (entries.Count == 0)
                AddParagraph("<i>Nothing noted yet.</i>");

            // Side by side: what the records say, what people say, what I've seen. Mismatches line up.
            var compareKinds = new[] { NoteKind.Archive, NoteKind.Gossip, NoteKind.Observation };
            if (entries.Any(e => compareKinds.Contains(e.kind)))
            {
                AddParagraph("<size=110%><b>Compare</b></size>  <size=80%><color=#00000088>records | what others say | what I've seen</color></size>", 4);
                var columns = UIFactory.Rect("Compare", entryList);
                var h = columns.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 10;
                h.childControlWidth = true;
                h.childControlHeight = true;
                h.childForceExpandWidth = true;
                h.childForceExpandHeight = false;
                foreach (var kind in compareKinds)
                {
                    var col = UIFactory.Rect(kind.ToString(), columns);
                    var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
                    v.spacing = 3;
                    v.childControlWidth = true;
                    v.childControlHeight = true;
                    v.childForceExpandHeight = false;
                    UIFactory.Layout(col, preferredWidth: 0, flexibleWidth: 1);
                    UIFactory.Text("Heading", col, $"<b>{KindHeading(kind)}</b>", 14, UIFactory.Ink);
                    var list = entries.Where(e => e.kind == kind && !(kind == NoteKind.Archive && e.title.StartsWith("Parish record"))).ToList();
                    if (list.Count == 0)
                        UIFactory.Text("Empty", col, "<i><color=#00000066>nothing yet</color></i>", 13, UIFactory.Ink);
                    foreach (var entry in list)
                        AddEntryRow(entry, col, 13, compact: true);
                }
            }

            foreach (var group in entries.Where(e => !compareKinds.Contains(e.kind) || (e.kind == NoteKind.Archive && e.title.StartsWith("Parish record"))).GroupBy(e => e.kind).OrderBy(g => g.Key))
            {
                AddParagraph($"<size=110%><b>{KindHeading(group.Key)}</b></size>", 6);
                foreach (var entry in group)
                    AddEntryRow(entry);
            }
        }

        private void BuildCaseFile(Notebook notebook)
        {
            var clock = GameClock.Instance;
            header.text = "<b>CASE FILE</b>  <size=70%>" + (clock != null ? $"{clock.Label}. {Mathf.Max(0, clock.LastDay - clock.Day)} day(s) left." : "") + "</size>";

            var sb = new StringBuilder();
            sb.AppendLine("<i>\"One of us is not one of us.\"</i>  An anonymous letter, postmarked Semblance.");
            sb.AppendLine("Find which resident isn't who they seem, then ring the church bell to accuse them.");
            sb.AppendLine("Weird is normal here. <b>Wrong</b> is not: look for details that don't match the records, the gossip, or yesterday.");
            AddParagraph(sb.ToString());

            AddParagraph("<size=110%><b>Suspicion</b></size>", 8);
            var ranked = notebook.Subjects.Where(s => s != Notebook.CaseSubject && Resident.FindByName(s) != null)
                .Select(s => (s, notebook.SuspicionScore(s))).Where(t => t.Item2 > 0).OrderByDescending(t => t.Item2).ToList();
            AddParagraph(ranked.Count == 0 ? "<i>Star suspicious notes and link contradictions to build a case.</i>"
                : string.Join("\n", ranked.Select(t => $"{t.s}: {new string('*', Mathf.Min(t.Item2, 12))} ({t.Item2})")));

            AddParagraph("<size=110%><b>Contradictions</b></size>", 8);
            if (notebook.Contradictions.Count == 0)
                AddParagraph("<i>None linked yet.</i>");
            foreach (var link in notebook.Contradictions.ToList())
            {
                var a = notebook.Get(link.a);
                var b = notebook.Get(link.b);
                if (a == null || b == null) continue;
                bool holds = NoteFacts.Conflict(a, b, out string why);
                string explained = holds ? NoteFacts.Explained(a, b) : null;
                string verdict = !holds ? $"<color=#00000088><i>{why} A hunch, not proof: it won't convince the town on its own.</i></color>"
                    : explained != null ? $"<color=#8a6a1a><b>Holds up, but</b> {explained} {why} It won't count against them.</color>"
                    : $"<color=#2f6b2a><b>Holds up.</b> {why}</color>";
                if (a.verdict == (int)Verdict.Explained || b.verdict == (int)Verdict.Explained)
                    verdict = "<color=#8a6a1a><i>I've explained one of these away myself. It won't count against anyone.</i></color>";
                string targets = string.Join(" and ", notebook.Targets(link));
                var row = AddParagraph($"<b>{a.subject}</b>: {a.text}\n   <color={UIFactory.Hex(UIFactory.Red)}>vs</color>  <b>{b.subject}</b>: {b.text}\n   {verdict}\n   <size=85%>Counts against <b>{targets}</b></size>");
                var remove = UIFactory.Button("Remove", row.transform, "x", 14, UIFactory.Red, new Color(0, 0, 0, 0.06f), () => notebook.RemoveContradiction(link));
                UIFactory.Place((RectTransform)remove.transform, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(24, 22));
                if (Candidates(a, b).Count > 1)
                {
                    var who = UIFactory.Button("Who", row.transform, "who?", 12, UIFactory.Ink, new Color(0, 0, 0, 0.06f), () => AskAgainst(link));
                    UIFactory.Place((RectTransform)who.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, 0), new Vector2(46, 22));
                }
            }

            var top = notebook.Subjects.Where(x => x != Notebook.CaseSubject).Select(x => (x, notebook.SuspicionScore(x))).OrderByDescending(t => t.Item2).FirstOrDefault();
            if (top.Item2 >= 3 && !Case.EndingDirector.MissingPersonFound)
                AddParagraph($"<size=110%><b>A thought</b></size>\nIf something has taken {top.x}'s place, where is the real one? It would want them close, to study. Every farmhouse out here has a root cellar. The church has a crypt.", 8);

            foreach (var section in CaseFileSections)
            {
                string text = section();
                if (!string.IsNullOrEmpty(text))
                    AddParagraph(text, 8);
            }

            var general = notebook.EntriesFor(Notebook.CaseSubject).Where(e => e.kind != NoteKind.Event && !(e.key != null && e.key.StartsWith("solved|"))).ToList();
            if (general.Count > 0)
            {
                AddParagraph("<size=110%><b>Other notes</b></size>", 8);
                foreach (var e in general)
                    AddEntryRow(e);
            }
        }

        private static string KindHeading(NoteKind kind)
        {
            switch (kind)
            {
                case NoteKind.Statement: return "What they told me";
                case NoteKind.Gossip: return "What others say about them";
                case NoteKind.Observation: return "What I've seen";
                case NoteKind.Archive: return "Parish records";
                case NoteKind.Photo: return "Photographs";
                case NoteKind.Evidence: return "Evidence";
                case NoteKind.Sighting: return "Sightings";
                default: return "Events";
            }
        }

        private TextMeshProUGUI AddParagraph(string text, float topPadding = 0)
        {
            var t = UIFactory.Text("Paragraph", entryList, text, 16, UIFactory.Ink);
            if (topPadding > 0)
                t.margin = new Vector4(0, topPadding, 0, 0);
            return t;
        }

        private void AddEntryRow(NoteEntry entry, Transform parent = null, float fontSize = 15, bool compact = false)
        {
            var row = UIFactory.Rect("Entry " + entry.id, parent != null ? parent : entryList);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.UpperLeft;

            var notebook = Notebook.Instance;
            var star = UIFactory.Button("Star", row, entry.suspicious ? "<color=#992018>*</color>" : "<color=#00000040>*</color>", 26, UIFactory.Ink,
                new Color(0, 0, 0, 0.05f), () => notebook.SetSuspicious(entry.id, !entry.suspicious));
            UIFactory.Layout(star, preferredHeight: 26, preferredWidth: 26);

            bool isSelected = selected.Contains(entry.id);
            var tick = UIFactory.Button("Select", row, isSelected ? "[x]" : "[ ]", 14, UIFactory.Ink, new Color(0, 0, 0, isSelected ? 0.15f : 0.05f), () =>
            {
                if (!selected.Remove(entry.id))
                {
                    if (selected.Count >= 2) selected.Clear();
                    selected.Add(entry.id);
                }
                Refresh();
            });
            UIFactory.Layout(tick, preferredHeight: 26, preferredWidth: 34);

            // The detective's ruling: confirmed, explained away, or still open.
            string ruling = entry.verdict == (int)Verdict.Verified ? "<color=#2f6b2a>TRUE</color>" : entry.verdict == (int)Verdict.Explained ? "<color=#8a6a1a>FINE</color>" : "<color=#00000050>ok?</color>";
            var judge = UIFactory.Button("Verdict", row, ruling, 12, UIFactory.Ink, new Color(0, 0, 0, 0.05f), () => AskVerdict(entry));
            UIFactory.Layout(judge, preferredHeight: 26, preferredWidth: 42);

            var photoTex = notebook.GetPhoto(entry.photoIndex);
            if (photoTex != null)
            {
                var thumb = UIFactory.Button("Thumb", row, "", 10, Color.clear, Color.white, () => ShowPhoto(entry));
                var raw = new GameObject("Image", typeof(RawImage)).GetComponent<RawImage>();
                raw.transform.SetParent(thumb.transform, false);
                raw.texture = photoTex;
                raw.raycastTarget = false;
                UIFactory.Stretch(raw.rectTransform, new Vector2(3, 3), new Vector2(-3, -3));
                float aspect = photoTex.width / (float)photoTex.height;
                UIFactory.Layout(thumb, preferredHeight: 72, preferredWidth: 72 * aspect);
            }

            string from = string.IsNullOrEmpty(entry.source) || entry.source == entry.subject ? "" : $", from {entry.source}";
            string stamp = $"<size=80%><color=#00000088>{GameClock.Stamp(entry.day, entry.hour)}{from}</color></size>";
            string tail = "";
            if (entry.verdict == (int)Verdict.Verified) tail = "  <size=80%><color=#2f6b2a>checks out</color></size>";
            else if (entry.verdict == (int)Verdict.Explained) tail = "  <size=80%><color=#8a6a1a>explained away</color></size>";
            else
            {
                string backing = NoteFacts.Corroboration(entry, notebook);
                if (backing != null) tail = $"  <size=80%><color=#2f6b2a>{backing}</color></size>";
            }
            string body = compact ? $"{entry.text} {stamp}{tail}" : $"<b>{entry.title}</b>  {stamp}{tail}\n{entry.text}";
            var text = UIFactory.Text("Text", row, body, fontSize, UIFactory.Ink);
            if (entry.verdict != 0) text.alpha = 0.6f;
            UIFactory.Layout(text, flexibleWidth: 1);
        }

        private void ShowPhoto(NoteEntry entry)
        {
            photoViewerImage.texture = Notebook.Instance.GetPhoto(entry.photoIndex);
            var tex = photoViewerImage.texture;
            float aspect = tex != null ? tex.width / (float)tex.height : 1f;
            photoViewerImage.rectTransform.sizeDelta = new Vector2(600 * aspect, 600);
            photoViewerCaption.text = $"{entry.title}. {GameClock.Stamp(entry.day, entry.hour)}\n{entry.text}";
            photoViewer.SetActive(true);
        }

        private void LinkSelected()
        {
            if (selected.Count != 2) return;
            var ids = selected.ToArray();
            var nb = Notebook.Instance;
            if (nb.LinksLeftToday <= 0)
            {
                linkLabel.text = "Your eyes are swimming. Sleep on it.";
                return;
            }
            var link = nb.AddContradiction(ids[0], ids[1]);
            var a = nb.Get(ids[0]);
            var b = nb.Get(ids[1]);
            bool holds = NoteFacts.Conflict(a, b, out string why);
            string explained = holds ? NoteFacts.Explained(a, b) : null;
            UI.PlayerHUD.Instance?.Toast(!holds ? $"<i>{why}</i>" : explained != null ? $"<b>That holds up</b>, but {explained}" : $"<b>That holds up.</b> {why}");
            selected.Clear();
            Refresh();
            if (link != null && Candidates(a, b).Count > 1) AskAgainst(link);
        }

        // ------------------------------------------------------------ Layout

        private void Build()
        {
            var canvas = UIFactory.Canvas("Notebook Canvas", transform, 40);
            var dim = UIFactory.Panel("Dim", canvas.transform, new Color(0, 0, 0, 0.7f));
            UIFactory.Stretch(dim.rectTransform);
            root = dim.gameObject;

            var cover = UIFactory.Panel("Cover", dim.transform, new Color(0.22f, 0.12f, 0.08f));
            UIFactory.Place(cover.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 660));

            var tabs = UIFactory.Panel("Tabs", cover.transform, UIFactory.PaperDark);
            UIFactory.Place(tabs.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -14), new Vector2(260, 632));
            subjectList = UIFactory.ScrollList("Subjects", tabs.transform, out _, 3f);
            UIFactory.Stretch((RectTransform)subjectList.parent.parent, new Vector2(4, 4), new Vector2(-4, -4));

            var page = UIFactory.Panel("Page", cover.transform, UIFactory.Paper);
            UIFactory.Place(page.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(284, -14), new Vector2(882, 632));

            header = UIFactory.Text("Header", page.transform, "", 22, UIFactory.Ink);
            UIFactory.Place(header.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -10), new Vector2(846, 34));
            var rule = UIFactory.Panel("Rule", page.transform, new Color(0.6f, 0.15f, 0.12f, 0.6f), false);
            UIFactory.Place(rule.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -46), new Vector2(854, 2));

            entryList = UIFactory.ScrollList("Entries", page.transform, out entryScroll, 5f, new RectOffset(10, 16, 6, 10));
            UIFactory.Stretch((RectTransform)entryList.parent.parent, new Vector2(8, 50), new Vector2(-8, -52));

            linkButton = UIFactory.Button("Link", page.transform, "", 15, UIFactory.Light, UIFactory.Red, LinkSelected);
            linkLabel = linkButton.GetComponentInChildren<TextMeshProUGUI>();
            UIFactory.Place((RectTransform)linkButton.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 10), new Vector2(300, 32));
            var hint = UIFactory.Text("Hint", page.transform, "* suspicious   [ ] tick to link   ok? your ruling", 13, new Color(0, 0, 0, 0.5f), TextAlignmentOptions.MidlineLeft);
            UIFactory.Place(hint.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(18, 4), new Vector2(540, 22));
            pendingLabel = UIFactory.Text("Pending", page.transform, "", 13, UIFactory.Ink, TextAlignmentOptions.MidlineLeft);
            UIFactory.Place(pendingLabel.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(18, 24), new Vector2(540, 22));

            var close = UIFactory.Button("Close", page.transform, "Close", 15, UIFactory.Light, UIFactory.Red, Close);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -9), new Vector2(84, 28));
            header.rectTransform.sizeDelta = new Vector2(740, 34);

            // The page for your own notes: a plain text field in the same paper.
            notesField = new GameObject("Notes Field", typeof(Image), typeof(TMP_InputField));
            notesField.transform.SetParent(page.transform, false);
            notesField.GetComponent<Image>().color = new Color(0, 0, 0, 0.03f);
            UIFactory.Stretch((RectTransform)notesField.transform, new Vector2(12, 50), new Vector2(-12, -52));
            notesInput = notesField.GetComponent<TMP_InputField>();
            var area = UIFactory.Rect("Text Area", notesField.transform);
            UIFactory.Stretch(area, new Vector2(10, 8), new Vector2(-10, -8));
            area.gameObject.AddComponent<RectMask2D>();
            var placeholder = UIFactory.Text("Placeholder", area, "<i>Write whatever you like here. Hunches, a list of who was where, things to ask tomorrow.</i>", 16, new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.4f), TextAlignmentOptions.TopLeft);
            UIFactory.Stretch(placeholder.rectTransform);
            var body = UIFactory.Text("Text", area, "", 16, UIFactory.Ink, TextAlignmentOptions.TopLeft);
            UIFactory.Stretch(body.rectTransform);
            notesInput.textViewport = area;
            notesInput.textComponent = body;
            notesInput.placeholder = placeholder;
            notesInput.lineType = TMP_InputField.LineType.MultiLineNewline;
            notesInput.richText = false;
            notesInput.characterLimit = 6000;
            notesInput.caretColor = UIFactory.Ink;
            notesInput.selectionColor = new Color(0.6f, 0.15f, 0.12f, 0.3f);
            notesInput.onValueChanged.AddListener(v => { if (Notebook.Instance != null) Notebook.Instance.PlayerNotes = v; });
            notesField.SetActive(false);

            var viewer = UIFactory.Panel("Photo Viewer", dim.transform, new Color(0, 0, 0, 0.85f));
            UIFactory.Stretch(viewer.rectTransform);
            viewer.gameObject.AddComponent<Button>().onClick.AddListener(() => photoViewer.SetActive(false));
            photoViewer = viewer.gameObject;
            var frame = UIFactory.Panel("Frame", viewer.transform, new Color(0.95f, 0.94f, 0.9f), false);
            UIFactory.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(10, 10));
            photoViewerImage = new GameObject("Image", typeof(RawImage)).GetComponent<RawImage>();
            photoViewerImage.transform.SetParent(viewer.transform, false);
            photoViewerImage.raycastTarget = false;
            UIFactory.Place(photoViewerImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(520, 520));
            frame.gameObject.SetActive(false);
            photoViewerCaption = UIFactory.Text("Caption", viewer.transform, "", 16, UIFactory.Light, TextAlignmentOptions.Top);
            UIFactory.Place(photoViewerCaption.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(900, 60));
        }
    }
}
