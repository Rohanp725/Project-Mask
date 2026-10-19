using System.Collections.Generic;
using System.Linq;
using System.Text;
using Semblance.Core;
using Semblance.Interaction;
using Semblance.Notes;
using Semblance.Residents;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Semblance.Archive
{
    /// <summary>
    /// The parish records kept in the church: a 1979 directory photo and Doc Pruitt's physician notes for every
    /// resident. This is the baseline the player compares people against. Reading a card files it in the notebook.
    /// </summary>
    public class ArchiveUI : MonoBehaviour
    {
        public static ArchiveUI Instance { get; private set; }
        public static event System.Action<Resident> CardViewed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics() => CardViewed = null;

        private GameObject root;
        private RectTransform list;
        private RawImage photo;
        private TextMeshProUGUI card;
        private Resident selected;

        private void Awake()
        {
            Instance = this;
            Build();
            root.SetActive(false);
        }

        public void Open()
        {
            root.SetActive(true);
            if (root.GetComponent<UI.SelectFirstOnEnable>() == null) root.AddComponent<UI.SelectFirstOnEnable>();
            InputLock.Push(this, Close);
            RefreshList();
            var first = SortedResidents().FirstOrDefault();
            if (selected == null && first != null)
                Show(first, fileIt: false);
        }

        public void Close()
        {
            root.SetActive(false);
            InputLock.Pop(this);
        }

        /// <summary>Everyone on file, including anyone missing or dead: their card stays readable.</summary>
        private static IEnumerable<Resident> SortedResidents()
            => (Case.CaseDirector.Instance != null && Case.CaseDirector.Instance.Residents.Count > 0
                    ? (IEnumerable<Resident>)Case.CaseDirector.Instance.Residents : Resident.All)
               .OrderBy(r => r.LastName).ThenBy(r => r.DisplayName);

        private void RefreshList()
        {
            UIFactory.Clear(list);
            foreach (var r in SortedResidents())
            {
                var resident = r;
                string label = $"{r.LastName.ToUpperInvariant()}, {r.DisplayName.Substring(0, r.DisplayName.Length - r.LastName.Length).Trim()}";
                var b = UIFactory.Button("Card " + r.DisplayName, list, label, 16, UIFactory.Ink, new Color(0.8f, 0.7f, 0.48f), () => Show(resident));
                b.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
                UIFactory.Layout(b, preferredHeight: 28);
            }
        }

        private void Show(Resident r) => Show(r, true);

        private void Show(Resident r, bool fileIt)
        {
            selected = r;
            var studio = PortraitStudio.Instance;
            photo.texture = studio != null ? studio.ArchivePhoto(r.Profile) : null;
            card.text = BuildCard(r, out var lines);
            if (!fileIt) return;
            FileInNotebook(r, lines);
            CardViewed?.Invoke(r);
        }

        public static string BuildCard(Resident r, out List<string> recordLines)
        {
            var p = r.Profile;
            recordLines = ArchivedLines(r);
            var sb = new StringBuilder();
            sb.AppendLine($"<size=130%><b>{p.displayName.ToUpperInvariant()}</b></size>");
            sb.AppendLine($"Born {1983 - p.age}.  {p.occupation}.  {p.home}.");
            sb.AppendLine("<i>Directory photograph, Founders' Day 1979.</i>");
            sb.AppendLine();
            sb.AppendLine("<b>PHYSICIAN'S NOTES</b> <i>(Dr. A. Pruitt)</i>");
            if (recordLines.Count == 0)
                sb.AppendLine("Nothing remarkable.");
            foreach (var line in recordLines)
                sb.AppendLine("- " + line);
            return sb.ToString();
        }

        /// <summary>Canonical traits as Doc Pruitt wrote them down, with his note on the cause where known.</summary>
        public static List<string> ArchivedLines(Resident r) => ArchivedRecords(r).Select(x => x.line).ToList();

        /// <summary>Doc's notes with the feature each one describes. Doc made one honest mistake per case (wrong side).</summary>
        public static List<(string line, FeatureInstance feature)> ArchivedRecords(Resident r)
        {
            var lines = new List<(string, FeatureInstance)>();
            var director = Case.CaseDirector.Instance;
            string wrong = director != null && director.Noise != null && director.Noise.RecordErrors.TryGetValue(r, out var id) ? id : null;
            foreach (var original in r.Profile.features)
            {
                var d = FeatureCatalog.Get(original.id);
                if (d == null || d.Kind != FeatureKind.Trait || !original.inArchive || !d.Listed)
                    continue;
                var f = original;
                if (original.id == wrong)
                    f = new FeatureInstance(original.id, original.side == FeatureSide.Left ? FeatureSide.Right : FeatureSide.Left, original.explanation, true);
                // Doc's own words from 1979; what people say about their marks now is in what they tell you.
                string line = ThirdPerson(d.Describe(f.side), r.FirstName, r.Profile.possessive);
                lines.Add((line, f));
            }
            return lines;
        }

        public static string ThirdPerson(string text, string name, string possessive = "their") => Resident.ThirdPerson(text, name, possessive);

        private static void FileInNotebook(Resident r, List<string> lines)
        {
            var notebook = Notebook.Instance;
            if (notebook == null)
                return;
            int photoIndex = -1;
            string summaryKey = "archive|" + r.DisplayName;
            if (!notebook.Has(summaryKey) && PortraitStudio.Instance != null)
                photoIndex = notebook.AddPhoto(PortraitStudio.Instance.ArchivePhoto(r.Profile));
            notebook.Record(NoteKind.Archive, r.DisplayName, "Parish record, 1979", "Directory photograph and physician's notes.", "Parish records", summaryKey, photoIndex);
            var records = ArchivedRecords(r);
            string set = NoteFacts.Set(records.Select(x => x.feature));
            foreach (var (line, feature) in records)
            {
                var e = notebook.Record(NoteKind.Archive, r.DisplayName, "Physician's note", Notebook.StripTags(line), "Dr. A. Pruitt", $"archive|{r.DisplayName}|{line}");
                if (e.fact == null) e.fact = NoteFacts.Record(feature, set);
            }
        }

        private void Build()
        {
            var canvas = UIFactory.Canvas("Archive Canvas", transform, 30);
            var dim = UIFactory.Panel("Dim", canvas.transform, new Color(0, 0, 0, 0.75f));
            UIFactory.Stretch(dim.rectTransform);
            root = dim.gameObject;

            var drawer = UIFactory.Panel("Drawer", dim.transform, new Color(0.35f, 0.33f, 0.3f));
            UIFactory.Place(drawer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 600));

            var title = UIFactory.Text("Title", drawer.transform, "PARISH RECORDS: FIRST CHURCH OF SEMBLANCE", 20, UIFactory.Light);
            UIFactory.Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -12), new Vector2(700, 30));
            var close = UIFactory.Button("Close", drawer.transform, "Close", 15, UIFactory.Light, new Color(1, 1, 1, 0.12f), Close);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -10), new Vector2(90, 30));
            var hint = UIFactory.Text("Hint", drawer.transform, "Esc", 13, new Color(1, 1, 1, 0.45f), TextAlignmentOptions.TopRight);
            UIFactory.Place(hint.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-112, -16), new Vector2(60, 24));

            var listHolder = UIFactory.Rect("List Holder", drawer.transform);
            UIFactory.Place(listHolder, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -50), new Vector2(250, 530));
            list = UIFactory.ScrollList("Cards", listHolder, out _, 3f);
            UIFactory.Stretch((RectTransform)list.parent.parent);

            var cardPanel = UIFactory.Panel("Card", drawer.transform, UIFactory.Paper);
            UIFactory.Place(cardPanel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(285, -50), new Vector2(695, 530));

            var frame = UIFactory.Panel("Photo Frame", cardPanel.transform, new Color(0.95f, 0.93f, 0.88f));
            UIFactory.Place(frame.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(232, 262));
            photo = new GameObject("Photo", typeof(RawImage)).GetComponent<RawImage>();
            photo.transform.SetParent(frame.transform, false);
            UIFactory.Place(photo.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(208, 208));
            var caption = UIFactory.Text("Caption", frame.transform, "<i>Founders' Day, 1979</i>", 13, UIFactory.Ink, TextAlignmentOptions.Bottom);
            UIFactory.Place(caption.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(220, 30));

            card = UIFactory.Text("Card Text", cardPanel.transform, "", 17, UIFactory.Ink);
            UIFactory.Place(card.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(270, -20), new Vector2(405, 490));
        }
    }
}
