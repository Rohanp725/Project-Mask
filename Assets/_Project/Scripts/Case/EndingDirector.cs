using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Semblance.Archive;
using Semblance.Core;
using Semblance.Notes;
using Semblance.Residents;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Semblance.Case
{
    public enum EndingKind { Rescued, Caught, WrongAccusation, OutOfTime, Killed }

    /// <summary>
    /// Plays an ending as a sequence of typed-out passages over black (with a portrait where it matters),
    /// then a case-closed screen that reveals the truth and the player's stats.
    /// </summary>
    public class EndingDirector : MonoBehaviour
    {
        public static EndingDirector Instance { get; private set; }

        /// <summary>Set by the investigation when the real (replaced) resident has been found alive.</summary>
        public static bool MissingPersonFound { get; set; }
        public bool IsPlaying { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics() => MissingPersonFound = false;

        private Canvas canvas;
        private Image black;
        private TextMeshProUGUI passage;
        private RawImage portrait;
        private GameObject summary;
        private TextMeshProUGUI summaryText;
        private TextMeshProUGUI continueHint;
        private GameObject retryButton, replayButton;

        private void Awake()
        {
            Instance = this;
            Build();
            canvas.gameObject.SetActive(false);
        }

        public void Accuse(Resident accused)
        {
            Save.SaveSystem.Flags.Remove("hunt:forced");
            var director = CaseDirector.Instance;
            bool correct = director != null && accused == director.Mimic;
            Play(correct ? (MissingPersonFound ? EndingKind.Rescued : EndingKind.Caught) : EndingKind.WrongAccusation, accused);
        }

        private static bool Daytime => GameClock.Instance != null && !GameClock.Instance.IsDark && GameClock.Instance.Hour < 20f;

        public void Play(EndingKind kind, Resident accused = null)
        {
            if (IsPlaying) return;
            IsPlaying = true;
            Events.Stalker.Active?.End();
            UI.PlayerHUD.Instance?.SetHint(null);
            World.DetectiveCar.Instance?.ForceExit();
            InputLock.Push(this, null);
            if (GameClock.Instance != null) GameClock.Instance.Frozen = true;
            Audio.GameAudio.Play(kind == EndingKind.Killed ? "stinger_death" : "bell_toll", 0.9f);
            StartCoroutine(Run(kind, accused));
        }

        private IEnumerator Run(EndingKind kind, Resident accused)
        {
            canvas.gameObject.SetActive(true);
            summary.SetActive(false);
            portrait.gameObject.SetActive(false);
            passage.text = "";
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 1.5f)
            {
                black.color = new Color(0, 0, 0, t);
                yield return null;
            }
            black.color = Color.black;

            var mimic = CaseDirector.Instance != null ? CaseDirector.Instance.Mimic : null;
            foreach (var (text, face) in Script(kind, accused, mimic))
            {
                if (face != null)
                {
                    portrait.texture = face;
                    portrait.gameObject.SetActive(true);
                }
                else
                    portrait.gameObject.SetActive(false);
                yield return Type(text);
            }

            portrait.gameObject.SetActive(false);
            passage.text = "";
            ShowSummary(kind, accused, mimic);
        }

        private IEnumerator Type(string text)
        {
            passage.text = text;
            passage.maxVisibleCharacters = 0;
            continueHint.gameObject.SetActive(false);
            float shown = 0;
            yield return null;
            int total = passage.textInfo.characterCount;
            while (shown < total)
            {
                shown += Time.unscaledDeltaTime * 38f;
                passage.maxVisibleCharacters = (int)shown;
                if (Pressed()) shown = total;
                yield return null;
                total = passage.textInfo.characterCount;
            }
            passage.maxVisibleCharacters = 99999;
            continueHint.gameObject.SetActive(true);
            yield return null;
            while (!Pressed())
                yield return null;
            yield return null;
        }

        private static bool Pressed()
        {
            return GameInput.Pressed(GameInput.Advance) || GameInput.Pressed(GameInput.Interact);
        }

        private static Texture2D Face(Resident r, bool asIs)
        {
            if (r == null || PortraitStudio.Instance == null) return null;
            if (!asIs) return PortraitStudio.Instance.ArchivePhoto(r.Profile);
            var tex = PortraitStudio.Instance.Capture(r.Profile, r.Avatar.Modifications, PhotoStyle.Snapshot, 7);
            return tex;
        }

        private IEnumerable<(string, Texture2D)> Script(EndingKind kind, Resident accused, Resident mimic)
        {
            string town = "Semblance";
            string accusedName = accused != null ? accused.DisplayName : "someone";
            string accusedFirst = accused != null ? accused.FirstName : "them";
            // The count: 212 souls when the letter was sent, less whoever it killed on the third night.
            var beats = StoryBeats.Instance;
            var taken = beats != null ? beats.Missing : null;
            int souls = 212 - (taken != null ? 1 : 0);
            // Whoever's left to fire after it as it runs.
            var shooter = new[] { CastNames.Harlan, CastNames.Greta, CastNames.Silas, CastNames.Hank }
                .Select(Resident.FindByName).FirstOrDefault(r => r != null && r != mimic && r != taken && r.gameObject.activeSelf);
            string shot = shooter != null
                ? $"{shooter.DisplayName} fired both barrels after it. Nobody went in after it."
                : "Somebody fired a shotgun into the corn after it. Nobody went in.";
            switch (kind)
            {
                case EndingKind.Rescued:
                case EndingKind.Caught:
                    yield return (Daytime
                        ? "You rang the bell until your hands bled. They came in from the fields one by one: farmers still in their overalls, Mae in her apron, Father Crane from the vestry."
                        : "You rang the bell until your hands bled. They came out of the dark one by one: farmers in nightclothes, Mae still in her apron, Father Crane with a lantern.", null);
                    yield return ($"You said the name out loud: <b>{accusedName}</b>.\n\nThe whole town turned to look.", Face(accused, true));
                    yield return ($"For a moment {accusedFirst} only smiled. Then the smile kept going, past where a smile should stop, and the face slid. Not off. <i>Sideways.</i> Like a coat hung on the wrong hook.", null);
                    yield return ($"It went over the churchyard fence on too many limbs and into the corn. {shot} Nobody had to: the cattle stopped dying, the sign on the highway stopped changing in the night, and by Sunday the moths came back to Irene's porch.", null);
                    if (kind == EndingKind.Rescued)
                        yield return ($"You carried the real {accusedFirst} out of the dark yourself. Thin, grey, but breathing. Doc Pruitt says they'll live.\n\n\"It wore me,\" they whispered. \"It practised my face on me.\"", Face(accused, false));
                    else
                        yield return ($"The real {accusedFirst} was never found. The town held a service anyway. The grave was very shallow, and the earth on it was disturbed from below by Monday.", null);
                    int left = kind == EndingKind.Rescued ? souls : souls - 1;
                    string painter = mimic != null && mimic.DisplayName == CastNames.Wendell ? CastNames.Otis : "Wendell Dobbs";
                    yield return ($"{painter} repainted the welcome sign before you left, and stood back, and counted on his fingers to be sure.\n\n<size=140%><b>{town.ToUpperInvariant()}  -  POP. {left}</b></size>", null);
                    break;

                case EndingKind.WrongAccusation:
                    yield return (Daytime
                        ? "You rang the bell until your hands bled. They came in from the fields one by one and filled the church."
                        : "You rang the bell until your hands bled. They came out of the dark one by one, blinking in the lantern light.", null);
                    yield return ($"You said the name out loud: <b>{accusedName}</b>.\n\n{accusedFirst} didn't run. {accusedFirst} cried. Said your name like a question.", Face(accused, true));
                    yield return ($"The town did what towns like this do. You didn't stop them. By morning {(mimic != null && mimic.DisplayName == CastNames.Mae || accused != null && accused.DisplayName == CastNames.Mae || taken != null && taken.DisplayName == CastNames.Mae ? "nobody at the diner would pour you coffee" : "Mae wouldn't pour you coffee")}, and everyone very politely avoided your eyes.", null);
                    yield return ("That night something knocked on the door of Room 6. Three slow knocks, then three more, in a voice you'd heard every day this week:\n\n<i>\"Detective? It's only me. Let me in. I'm one of you now.\"</i>", null);
                    if (mimic != null)
                        yield return ($"It was <b>{mimic.DisplayName}</b>. It had been, the whole time.\n\nYou let it in.", Face(mimic, true));
                    // One innocent fewer; it, and now you.
                    yield return ($"<size=140%><b>{town.ToUpperInvariant()}  -  POP. {souls - 1 + 2}</b></size>", null);
                    break;

                case EndingKind.OutOfTime:
                    yield return ("On the morning of the last day you woke in Room 6 and couldn't remember the name of the agency that sent you.", null);
                    yield return ("Mae poured your coffee without asking. You'd stopped noticing the taste. Everyone was so kind. Nobody looked at you too long anymore. Why would they? You were one of them.", null);
                    if (mimic != null)
                        yield return ($"<b>{mimic.DisplayName}</b> sat in the booth across from you and smiled the whole time, and you smiled back, and you couldn't stop.", Face(mimic, true));
                    yield return ($"The sign on the highway had been right since Sunday. It counts the new ones. It never counts the dead.\n\n<size=140%><b>{town.ToUpperInvariant()}  -  POP. 214</b></size>", null);
                    break;

                case EndingKind.Killed:
                    yield return (Daytime
                        ? "It didn't wait for dark. In the grey light it was faster than anything that shape should be."
                        : "It was faster in the dark than anything that shape should be.", null);
                    yield return ("In the morning Mae asked where the detective had gone. Somebody said you'd left early. Somebody else agreed. The motel key was back on its hook.", null);
                    if (mimic != null)
                        yield return ($"<b>{mimic.DisplayName}</b> waved goodbye to your car as it drove out of town, though nobody was driving it.", Face(mimic, true));
                    yield return ($"<size=140%><b>{town.ToUpperInvariant()}  -  POP. {souls + 1}</b></size>", null);
                    break;
            }
        }

        private void ShowSummary(EndingKind kind, Resident accused, Resident mimic)
        {
            var notebook = Notebook.Instance;
            var director = CaseDirector.Instance;
            var clock = GameClock.Instance;
            var sb = new StringBuilder();
            string verdict = kind switch
            {
                EndingKind.Rescued => "<color=#7fbf6a>CASE CLOSED: THE MIMIC WAS EXPOSED AND ITS VICTIM SAVED</color>",
                EndingKind.Caught => "<color=#c9b35a>CASE CLOSED: THE MIMIC WAS EXPOSED</color>",
                EndingKind.WrongAccusation => "<color=#c03020>CASE CLOSED: AN INNOCENT WAS CONDEMNED</color>",
                EndingKind.OutOfTime => "<color=#c03020>CASE ABANDONED: YOU RAN OUT OF TIME</color>",
                _ => "<color=#c03020>THE DETECTIVE WAS NEVER SEEN AGAIN</color>",
            };
            sb.AppendLine($"<size=130%><b>{verdict}</b></size>\n");
            if (mimic != null)
            {
                sb.AppendLine($"The mimic was <b>{mimic.DisplayName}</b>.");
                if (accused != null && accused != mimic)
                    sb.AppendLine($"You accused <b>{accused.DisplayName}</b>.");
                sb.AppendLine("\n<b>Its tells:</b>");
                foreach (var tell in director.TellDescriptions)
                    sb.AppendLine("  - " + tell);
                var herrings = director.RedHerringDescriptions.ToList();
                if (herrings.Count > 0)
                {
                    sb.AppendLine("\n<b>Red herrings:</b>");
                    foreach (var h in herrings)
                        sb.AppendLine("  - " + h);
                }
            }
            sb.AppendLine();
            if (clock != null) sb.AppendLine($"Days in Semblance: {clock.Day}");
            if (notebook != null)
            {
                sb.AppendLine($"Notes taken: {notebook.Entries.Count}    Photos: {notebook.Photos.Count}    Contradictions linked: {notebook.Contradictions.Count}");
                if (mimic != null)
                    sb.AppendLine($"Your suspicion of {mimic.FirstName}: {notebook.SuspicionScore(mimic.DisplayName)}");
            }
            sb.AppendLine($"Case seed: {director?.Seed}   Difficulty: {GameSettings.Difficulty}");
            summaryText.text = sb.ToString();
            summary.SetActive(true);
            if (summary.GetComponent<UI.SelectFirstOnEnable>() == null) summary.AddComponent<UI.SelectFirstOnEnable>();
            bool canRetry = kind == EndingKind.Killed && Save.SaveSystem.HasSave;
            retryButton.SetActive(canRetry);
            // A wrong name or running out of days ends the case, but you can take the same case again from the start.
            replayButton.SetActive(!canRetry && (kind == EndingKind.WrongAccusation || kind == EndingKind.OutOfTime || kind == EndingKind.Killed));
            if (!canRetry)
                Save.SaveSystem.DeleteSave();
        }

        private void Build()
        {
            canvas = UIFactory.Canvas("Ending Canvas", transform, 200);
            black = UIFactory.Panel("Black", canvas.transform, new Color(0, 0, 0, 0));
            UIFactory.Stretch(black.rectTransform);

            portrait = new GameObject("Portrait", typeof(RawImage)).GetComponent<RawImage>();
            portrait.transform.SetParent(canvas.transform, false);
            UIFactory.Place(portrait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(240, 240));

            passage = UIFactory.Text("Passage", canvas.transform, "", 24, UIFactory.Light, TextAlignmentOptions.Top);
            UIFactory.Place(passage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1), new Vector2(0, 40), new Vector2(860, 320));

            continueHint = UIFactory.Text("Continue", canvas.transform, "click to continue", 14, new Color(1, 1, 1, 0.4f), TextAlignmentOptions.Bottom);
            UIFactory.Place(continueHint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(400, 24));

            var panel = UIFactory.Panel("Summary", canvas.transform, new Color(0.06f, 0.05f, 0.05f, 1f));
            UIFactory.Stretch(panel.rectTransform);
            summary = panel.gameObject;
            // The summary can run long (tells, drift, every red herring): it scrolls, and never runs under the buttons.
            var holder = UIFactory.Rect("Summary Holder", panel.transform);
            UIFactory.Place(holder, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(940, 520));
            var content = UIFactory.ScrollList("Summary Scroll", holder, out _, 0f);
            UIFactory.Stretch((RectTransform)content.parent.parent);
            summaryText = UIFactory.Text("Text", content, "", 18, UIFactory.Light, TextAlignmentOptions.TopLeft);
            var again = UIFactory.Button("Again", panel.transform, "New case", 20, UIFactory.Light, UIFactory.Red, () => SceneLoader.NewGame());
            UIFactory.Place((RectTransform)again.transform, new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(-10, 50), new Vector2(220, 44));
            var menu = UIFactory.Button("Menu", panel.transform, "Main menu", 20, UIFactory.Light, new Color(0.25f, 0.22f, 0.2f), () => SceneLoader.MainMenu());
            UIFactory.Place((RectTransform)menu.transform, new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(10, 50), new Vector2(220, 44));
            var retry = UIFactory.Button("Retry", panel.transform, "Try again (last save)", 18, UIFactory.Light, new Color(0.3f, 0.12f, 0.1f), () => SceneLoader.Continue());
            UIFactory.Place((RectTransform)retry.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(320, 44));
            retryButton = retry.gameObject;
            retryButton.SetActive(false);
            var replay = UIFactory.Button("Replay", panel.transform, "Try this case again", 18, UIFactory.Light, new Color(0.3f, 0.12f, 0.1f),
                () => SceneLoader.Replay(CaseDirector.Instance != null ? CaseDirector.Instance.Seed : 0));
            UIFactory.Place((RectTransform)replay.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(320, 44));
            replayButton = replay.gameObject;
            replayButton.SetActive(false);
        }
    }
}
