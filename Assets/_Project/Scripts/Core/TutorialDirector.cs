using System;
using System.Collections.Generic;
using Semblance.Archive;
using Semblance.Dialogue;
using Semblance.Notes;
using Semblance.Player;
using Semblance.UI;
using Semblance.World;
using UnityEngine;

namespace Semblance.Core
{
    /// <summary>
    /// First-day hints. Each one stays up until the player has done the thing, then the next appears.
    /// Can be turned off in Settings ("Tutorial hints").
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        public static TutorialDirector Instance { get; private set; }

        private sealed class Step
        {
            public string Hint;
            public Func<bool> Done;
            public int MinDay = 1;
        }

        private readonly List<Step> steps = new List<Step>();
        private int index = -1;
        private bool talked, asked, observed, recordsRead, photographed;
        private float finalUntil;

        private void Awake() => Instance = this;

        private void Start()
        {
            DialogueUI.Opened += _ => talked = true;
            DialogueUI.TopicAsked += (_, topic) => { if (topic == "observe") observed = true; else asked = true; };
            ArchiveUI.CardViewed += _ => recordsRead = true;
            PhotoCamera.PhotoTaken += _ => photographed = true;
            // After the first sleep, skip whatever first-evening hints are left and carry on with the next morning's.
            GameFlow.Woke += _ =>
            {
                if (index < 0) return;
                int next = steps.FindIndex(s => s.MinDay >= 2);
                if (next >= 0 && index < next) index = next;
                Show();
            };

            Transform player = null;
            Func<Transform> P = () => player != null ? player : (player = Camera.main != null ? Camera.main.transform.root : null);
            steps.Add(new Step { Hint = "Walk north up the highway to Mae's Diner, or take your car ({interact}).\n<size=75%>{move} to move, {sprint} to run, {flashlight} for your flashlight, {cancel} for the menu. Mae keeps free county maps by the diner register.</size>",
                Done = () => talked || (P() != null && LocationMarker.Describe(P().position).Contains("Diner")) });
            steps.Add(new Step { Hint = "Press <b>{interact}</b> to talk to someone. Ask about things: everything they tell you goes in your notebook.", Done = () => talked && asked });
            steps.Add(new Step { Hint = "Choose <b>Look them over</b> to study a face.\n<size=75%>In the world, hold <b>{focus}</b> on someone to look closer; keep looking and you may notice things.</size>", Done = () => observed });
            // Linking, and the point of it: not every contradiction is the monster. The case always has one innocent
            // who told Founders' Day wrong for a human reason, and someone nearby who knows why.
            steps.Add(new Step { Hint = "Ask a few people about <b>Founders' Day</b>. When two stories don't match, open your notebook ({notebook}), tick both and <b>link</b> them.",
                Done = () => Notebook.Instance != null && Notebook.Instance.Contradictions.Count > 0 });
            steps.Add(new Step
            {
                Hint = null, // built when shown: the case may not exist yet at Start
                Done = () =>
                {
                    var liar = Case.CaseDirector.Instance != null && Case.CaseDirector.Instance.Noise != null ? Case.CaseDirector.Instance.Noise.FoundersLiar : null;
                    return liar == null || Notebook.Instance == null || System.Linq.Enumerable.Any(Notebook.Instance.EntriesFor(liar.DisplayName),
                        e => e.kind == NoteKind.Gossip && e.text.Contains("Founders' Day"));
                },
            });
            steps.Add(new Step { MinDay = 2, Hint = "The parish records in the church show how everyone looked in 1979.\n<size=75%>The church is across the road from the diner.</size>", Done = () => recordsRead });
            steps.Add(new Step { MinDay = 2, Hint = "Press <b>{camera}</b> to raise your camera, then <b>{shoot}</b> to take a photo. Film is limited.", Done = () => photographed });
        }

        private static string LiarHint()
        {
            var liar = Case.CaseDirector.Instance != null && Case.CaseDirector.Instance.Noise != null ? Case.CaseDirector.Instance.Noise.FoundersLiar : null;
            if (liar == null)
                return "A link that holds up is proof. But people lie for ordinary reasons too: find out why before you decide what it means.";
            string does = liar.Profile.possessive == "his" ? "he does" : liar.Profile.possessive == "her" ? "she does" : "they do";
            return "A link that holds up is proof, but people lie for ordinary reasons too.\n<size=75%>Somebody knows why " + liar.FirstName
                   + " tells Founders' Day the way " + does + ". Ask around about " + liar.FirstName + ".</size>";
        }

        public void Begin()
        {
            if (!GameSettings.ShowHints) return;
            index = 0;
            Show();
        }

        private void Show()
        {
            if (index < 0) return;
            if (index >= steps.Count)
            {
                PlayerHUD.Instance?.SetHint("<i>Weird is normal here. Wrong is not.</i>\n<size=75%>Link contradictions in your notebook: the town wants proof. Sleep in Room 6 (saves). Ring the church bell when you know.</size>", 16f);
                index = -1;
                return;
            }
            PlayerHUD.Instance?.SetHint(steps[index].Hint ?? LiarHint());
        }

        private void Update()
        {
            if (index < 0 || IntroSequence.Playing) return;
            if (!GameSettings.ShowHints)
            {
                PlayerHUD.Instance?.SetHint(null);
                index = -1;
                return;
            }
            // Skip ahead past anything the player has already done on their own.
            bool advanced = false;
            var clock = GameClock.Instance;
            if (index >= 0 && index < steps.Count && clock != null && clock.Day < steps[index].MinDay)
                return; // wait for tomorrow
            while (index >= 0 && index < steps.Count && steps[index].Done())
            {
                index++;
                advanced = true;
            }
            if (!advanced) return;
            // Don't jump ahead to tomorrow's advice: the next step waits for its day (Woke shows it then).
            if (index < steps.Count && clock != null && clock.Day < steps[index].MinDay)
                PlayerHUD.Instance?.SetHint(null);
            else
                Show();
        }
    }
}
