using System.Linq;
using Semblance.Archive;
using Semblance.Core;
using Semblance.Notes;
using Semblance.Residents;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Semblance.Case
{
    /// <summary>Choosing who to name after ringing the bell. Two clicks: pick a face, then confirm. No take-backs.</summary>
    public class AccusationUI : MonoBehaviour
    {
        public static AccusationUI Instance { get; private set; }

        private GameObject root;
        private RectTransform grid;
        private TextMeshProUGUI prompt;
        private Button confirm;
        private Resident chosen;

        private void Awake()
        {
            Instance = this;
            Build();
            root.SetActive(false);
        }

        public static bool IsOpen => Instance != null && Instance.root != null && Instance.root.activeSelf;

        public void Open()
        {
            UI.PlayerHUD.Instance?.SetHint(null);
            chosen = null;
            root.SetActive(true);
            if (root.GetComponent<UI.SelectFirstOnEnable>() == null) root.AddComponent<UI.SelectFirstOnEnable>();
            InputLock.Push(this, Close);
            Populate();
            UpdatePrompt();
        }

        private bool accusing;

        public void Close()
        {
            root.SetActive(false);
            InputLock.Pop(this);
            if (!accusing && EndingDirector.Instance != null && !EndingDirector.Instance.IsPlaying)
            {
                // The town came running for nothing. They'll remember, and so will it.
                var speaker = new[] { CastNames.Mae, CastNames.Crane, CastNames.Otis, CastNames.Greta }.Select(Resident.FindByName)
                    .FirstOrDefault(x => x != null && x.isActiveAndEnabled && (CaseDirector.Instance == null || x != CaseDirector.Instance.Mimic));
                UI.PlayerHUD.Instance?.ShowMessage(speaker != null
                    ? $"The first lanterns reach the church while the bell's still humming. {speaker.FirstName} looks at you a long time. <i>\"You rang that for nothing? Don't do that to us again, Detective.\"</i>"
                    : "The first lanterns reach the church while the bell's still humming. Nobody says anything. They just look at you.", 6f);
                MimicAwareness.Instance?.Raise(5f, "the bell");
                MimicAwareness.Instance?.Expose(10f, "rang the bell for nothing");
            }
            // Backing out of the accusation doesn't call it off: if it was hunting you, it still is.
            if (!accusing && Save.SaveSystem.Flags.Contains("hunt:forced") && EndingDirector.Instance != null && !EndingDirector.Instance.IsPlaying)
                Events.EventDirector.ForcedHuntPending = true;
        }

        private void Populate()
        {
            UIFactory.Clear(grid);
            var notebook = Notebook.Instance;
            var everyone = CaseDirector.Instance != null && CaseDirector.Instance.Residents.Count > 0 ? CaseDirector.Instance.Residents : Resident.All;
            var taken = StoryBeats.Instance != null ? StoryBeats.Instance.Missing : null;
            foreach (var r in everyone.OrderBy(r => r.LastName))
            {
                var resident = r;
                bool gone = r == taken;
                var cell = UIFactory.Button("Suspect " + r.DisplayName, grid, "", 12, UIFactory.Ink, UIFactory.Paper, () => { if (!gone) { chosen = resident; UpdatePrompt(); } });
                if (gone) cell.GetComponent<Button>().interactable = false;
                var image = new GameObject("Face", typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(cell.transform, false);
                image.raycastTarget = false;
                image.texture = PortraitStudio.Instance != null ? PortraitStudio.Instance.ArchivePhoto(r.Profile) : null;
                UIFactory.Place(image.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(120, 120));
                int score = notebook != null ? notebook.SuspicionScore(r.DisplayName) : 0;
                var label = cell.GetComponentInChildren<TextMeshProUGUI>();
                label.text = $"<b>{r.DisplayName}</b>" + (gone ? $"\n<i>{(StoryBeats.Instance.BodyFound ? "dead" : "missing")}</i>"
                    : score > 0 ? $"\n<color={UIFactory.Hex(UIFactory.Red)}>{new string('*', Mathf.Min(score, 8))}</color>" : "");
                label.alignment = TextAlignmentOptions.Bottom;
                label.fontSize = 14;
            }
        }

        /// <summary>How much the detective can show the town: linked contradictions and evidence about them.</summary>
        public static int ProofNeeded => GameSettings.Difficulty == Difficulty.Easy ? 2 : 3;

        /// <summary>Above Easy, the town wants more than one kind of proof (not just the same lie linked five ways).</summary>
        public static bool NeedsTwoKinds => GameSettings.Difficulty != Difficulty.Easy;

        /// <summary>"Two white coins" only means something next to a photo of somebody whose eyes didn't do that.</summary>
        private static bool PhotographedSomeoneElse(Notebook notebook, Resident r)
            => notebook.Entries.Any(e => e.kind == NoteKind.Photo && e.subject != r.DisplayName && e.subject != Notebook.CaseSubject);

        public static int ProofAgainst(Resident r)
        {
            var notebook = Notebook.Instance;
            if (notebook == null || r == null) return 0;
            // Only links that really hold up count, and each lie or changed feature counts once, however it's linked.
            var caughtOut = new System.Collections.Generic.HashSet<string>();
            foreach (var link in notebook.Contradictions)
            {
                var a = notebook.Get(link.a);
                var b = notebook.Get(link.b);
                if (NoteFacts.Explained(a, b) != null) continue; // holds up, but the town has an honest answer for it
                if (a.verdict == (int)Verdict.Explained || b.verdict == (int)Verdict.Explained) continue; // the detective explained it away
                var implicated = NoteFacts.Implicated(a, b).ToList();
                if (implicated.Count == 0) continue; // a hunch the facts can't back
                if (string.IsNullOrEmpty(link.against))
                {
                    foreach (var (subject, key) in implicated)
                        if (subject == r.DisplayName) caughtOut.Add(key);
                }
                else if (notebook.Targets(link).Contains(r.DisplayName))
                    caughtOut.Add(implicated[0].proofKey); // the detective says who this one points at
            }
            // Some slips need no second note: a stranger who greets you by name.
            foreach (var e in notebook.EntriesFor(r.DisplayName))
                if (e.fact != null && e.fact.StartsWith("self|")) caughtOut.Add("self|" + r.DisplayName);
            int contradictions = Mathf.Min(caughtOut.Count, 2);
            var entries = notebook.EntriesFor(r.DisplayName).ToList();
            // Finding the real one counts as the captive (two), not again as a piece of physical evidence.
            // Physical: evidence about them or found in their household, a cold hand nobody explained, a flare in a photo
            // that nobody explained. Each counts once.
            string household = CaseDirector.Household(r);
            // Evidence filed under a person is a side story (a dead wife's shoes, a drowned boy's crayons); only what the
            // creature itself leaves in a house counts, and that's filed under the household.
            int coldHand = entries.Any(e => e.title == "Shook hands" && e.fact == "cold") ? 1 : 0;
            int flare = entries.Any(e => e.kind == NoteKind.Photo && e.text.Contains("flared white")) && !entries.Any(e => e.explains == "flare")
                   && PhotographedSomeoneElse(notebook, r) ? 1 : 0;
            int witnessed = entries.Count(e => e.title == "Something I noticed");
            // What was found in the house says which family, not which person: it counts once, unless something
            // about this person in particular (a link, a cold hand, a flare, a thing you saw) says it's them.
            int householdFinds = notebook.EntriesFor(household).Count(e => e.kind == NoteKind.Evidence);
            bool personal = contradictions > 0 || coldHand > 0 || flare > 0 || witnessed > 0;
            int physical = Mathf.Min(householdFinds, personal ? 2 : 1) + coldHand + flare;
            int victim = EndingDirector.MissingPersonFound && CaseDirector.Instance != null && CaseDirector.Instance.Mimic == r ? 2 : 0;
            int proof = contradictions + physical + witnessed + victim;
            // The captive is overwhelming but it's one kind of proof: the town still wants something else.
            int kinds = (contradictions > 0 ? 1 : 0) + (physical > 0 ? 1 : 0) + (witnessed > 0 ? 1 : 0) + (victim > 0 ? 1 : 0);
            if (NeedsTwoKinds && kinds < 2) proof = Mathf.Min(proof, ProofNeeded - 1);
            return proof;
        }

        private void UpdatePrompt()
        {
            bool dark = GameClock.Instance != null && GameClock.Instance.IsDark;
            int proof = chosen != null ? ProofAgainst(chosen) : 0;
            prompt.text = chosen == null
                ? (dark ? "The bell's still humming. The whole town is coming up the road with lanterns." : "The bell's still humming. The town is coming in from the fields.") + "\n<b>Who isn't one of us?</b>"
                : $"Name <b>{chosen.DisplayName}</b> in front of everyone? There's no taking it back.\n<size=80%>Proof you can show them: {proof} of {ProofNeeded} needed{(NeedsTwoKinds ? ", of more than one kind" : "")}.</size>";
            confirm.gameObject.SetActive(chosen != null);
            foreach (Transform cell in grid)
                cell.GetComponent<Image>().color = chosen != null && cell.name == "Suspect " + chosen.DisplayName ? new Color(0.95f, 0.6f, 0.5f) : UIFactory.Paper;
        }

        private void Build()
        {
            var canvas = UIFactory.Canvas("Accusation Canvas", transform, 60);
            var dim = UIFactory.Panel("Dim", canvas.transform, new Color(0.03f, 0.02f, 0.02f, 0.94f));
            UIFactory.Stretch(dim.rectTransform);
            root = dim.gameObject;

            prompt = UIFactory.Text("Prompt", dim.transform, "", 22, UIFactory.Light, TextAlignmentOptions.Top);
            UIFactory.Place(prompt.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(1000, 70));

            grid = UIFactory.Rect("Grid", dim.transform);
            UIFactory.Place(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1180, 500));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(132, 168);
            layout.spacing = new Vector2(10, 10);
            layout.childAlignment = TextAnchor.MiddleCenter;

            confirm = UIFactory.Button("Confirm", dim.transform, "Say the name", 20, UIFactory.Light, UIFactory.Red, () =>
            {
                var accused = chosen;
                if (accused == null) return;
                if (ProofAgainst(accused) < ProofNeeded)
                {
                    var speaker = new[] { CastNames.Mae, CastNames.Crane, CastNames.Otis, CastNames.Greta }.Select(Resident.FindByName)
                        .FirstOrDefault(x => x != null && x != accused && x.isActiveAndEnabled && (CaseDirector.Instance == null || x != CaseDirector.Instance.Mimic));
                    string folds = speaker != null ? $"{speaker.FirstName} folds {speaker.Profile.possessive} arms." : "Nobody moves.";
                    prompt.text = $"{folds} <i>\"You can't hang somebody on a feeling, Detective. Show us something.\"</i>\n<size=80%>Link contradictions about {accused.FirstName} in your notebook (Tab), and find something harder: physical evidence, or something you felt or saw yourself. {ProofAgainst(accused)} of {ProofNeeded}.</size>";
                    return;
                }
                accusing = true;
                Close();
                accusing = false;
                EndingDirector.Instance?.Accuse(accused);
            });
            UIFactory.Place((RectTransform)confirm.transform, new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(-10, 28), new Vector2(240, 46));
            var back = UIFactory.Button("Back", dim.transform, "Not yet", 20, UIFactory.Light, new Color(0.25f, 0.22f, 0.2f), Close);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(10, 28), new Vector2(240, 46));
        }
    }
}
