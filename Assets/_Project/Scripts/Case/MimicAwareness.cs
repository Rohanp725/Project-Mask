using System;
using Semblance.Core;
using Semblance.Dialogue;
using Semblance.Player;
using Semblance.Residents;
using Semblance.UI;
using UnityEngine;

namespace Semblance.Case
{
    /// <summary>
    /// What the mimic knows about you, hidden from the player, in two parts.
    ///
    /// <b>Awareness</b> (0-100) is how much it has noticed you: being photographed, looked over, questioned, read up on,
    /// lingered near at night. It only makes the nights worse (tiers 0-2 drive the night events). It never starts the hunt.
    ///
    /// <b>Exposure</b> (0-100) is what it thinks you know about <i>it</i>: things you did where it could see you knew.
    /// Confronting it with evidence or a contradiction, asking it about its own household's evidence, being caught in
    /// its house, waking it, finding its victim, ringing the bell for nothing. Picking something up in an empty field
    /// tells it nothing. At 50 it stops pretending to itself and starts <b>plotting</b>: the nights fill up, it wears
    /// its own face in the dark, it follows you, it asks people which room you sleep in, it leaves you a note. Only
    /// after a night of that, or if you put proof to its face, or on the last night, does it decide to hunt. Finding its
    /// victim is the one thing it can't let stand: then it comes at the next dark, and you're told to run for the bell.
    /// </summary>
    public class MimicAwareness : MonoBehaviour
    {
        public static MimicAwareness Instance { get; private set; }

        public enum Stage { Unaware, Curious, Wary, Plotting, Hunting }

        [SerializeField, Range(0, 100)] private float awareness;
        [SerializeField, Range(0, 100)] private float exposure;
        [SerializeField] private float nightProximityRate = 0.3f;

        public float Value => awareness;
        public float Exposure => exposure;
        /// <summary>Night-event tier from awareness alone: 0 unaware, 1 curious (25), 2 hostile (50). Plotting counts as 2.</summary>
        public int Tier => IsPlotting ? Mathf.Max(2, AwarenessTier) : AwarenessTier;
        private int AwarenessTier => awareness >= 50 ? 2 : awareness >= 25 ? 1 : 0;
        public event Action<int> TierChanged;

        /// <summary>The day plotting began, or -1.</summary>
        public int PlotDay { get; private set; } = -1;
        public bool IsPlotting => PlotDay >= 0;
        /// <summary>The day it has decided to hunt (that night), or -1.</summary>
        public int HuntDay { get; private set; } = -1;
        /// <summary>True once the victim has been found: the hunt is coming whatever else happens.</summary>
        public bool VictimKnown { get; private set; }
        /// <summary>Real time at which the forced hunt begins after the victim is found, or -1.</summary>
        private float forcedHuntAt = -1f;
        private bool forcedHuntAtDark;
        private int lastTier;
        private int pendingToastTier;
        private bool warnedOfHunt, plotToastPending;
        private int notebookWatchedDay = -1;
        private int observedCount, photographedCount;
        private float proximityTonight;
        private int proximityDay = -1;
        private const float ProximityCapPerNight = 15f;

        public Stage Current =>
            Events.Stalker.IsHunting ? Stage.Hunting : IsPlotting ? Stage.Plotting : AwarenessTier >= 2 ? Stage.Wary : AwarenessTier >= 1 ? Stage.Curious : Stage.Unaware;

        private void Awake() => Instance = this;

        private void Start()
        {
            PhotoCamera.PhotoTaken += subjects =>
            {
                var mimic = Mimic;
                if (mimic == null || !subjects.Contains(mimic)) return;
                Raise(6f, "photographed");
                photographedCount++;
                if (photographedCount >= 2) Expose(5f, "photographed again");
            };
            DialogueUI.TopicAsked += (resident, topic) =>
            {
                if (resident != Mimic) return;
                if (topic == "observe")
                {
                    Raise(3f, "looked over");
                    observedCount++;
                    if (observedCount >= 2) Expose(4f, "studied twice");
                }
                else if (topic == ResidentProfile.TopicFoundersDay || topic == ResidentProfile.TopicStrange) Raise(2f, "questioned");
                else Raise(0.75f, "talked");
            };
            Archive.ArchiveUI.CardViewed += resident => { if (resident == Mimic) Raise(1.5f, "record read"); };
            if (GameClock.Instance != null)
            {
                GameClock.Instance.PhaseChanged += _ => FlushToast();
                GameClock.Instance.DayStarted += _ => FlushToast();
            }
            Save.SaveSystem.Register(d =>
            {
                d.awareness = awareness;
                d.exposure = exposure;
                d.plotDay = PlotDay;
                d.huntDay = HuntDay;
                d.victimKnown = VictimKnown;
            }, d =>
            {
                awareness = d.awareness;
                exposure = d.exposure;
                PlotDay = d.plotDay;
                HuntDay = d.huntDay;
                VictimKnown = d.victimKnown;
                lastTier = Tier;
            });
        }

        private static Resident Mimic => CaseDirector.Instance != null ? CaseDirector.Instance.Mimic : null;

        // ------------------------------------------------------------ noticing

        public void Raise(float amount, string reason = null)
        {
            awareness = Mathf.Clamp(awareness + amount * GameSettings.AwarenessRate, 0f, 100f);
            CheckTier();
        }

        private void CheckTier()
        {
            int tier = Tier;
            if (tier == lastTier) return;
            if (tier > lastTier) pendingToastTier = Mathf.Max(pendingToastTier, tier);
            lastTier = tier;
            TierChanged?.Invoke(tier);
        }

        // ------------------------------------------------------------ knowing

        /// <summary>Something you did where it could tell you knew. Enough of it and it starts plotting.</summary>
        public void Expose(float amount, string reason = null)
        {
            if (Mimic == null) return;
            bool wasPlotting = IsPlotting;
            exposure = Mathf.Clamp(exposure + amount * GameSettings.AwarenessRate, 0f, 100f);
            if (!IsPlotting && exposure >= 50f) BeginPlotting();
            // Proof put to its face once it's already plotting: it stops waiting. (The thing that started it plotting
            // doesn't count twice: the first night it only follows.)
            if (wasPlotting && amount >= 15f && HuntDay < 0 && GameClock.Instance != null)
                ScheduleHunt(GameClock.Instance.Day);
            CheckTier();
        }

        private void BeginPlotting()
        {
            var clock = GameClock.Instance;
            PlotDay = clock != null ? clock.Day : 1;
            plotToastPending = true;
            awareness = Mathf.Max(awareness, 50f);
            CheckTier();
            PlottingBegan?.Invoke();
        }

        /// <summary>Raised once, when it decides you're a problem to be solved.</summary>
        public event Action PlottingBegan;

        private void ScheduleHunt(int day)
        {
            HuntDay = day;
            HuntScheduled?.Invoke(day);
        }

        public event Action<int> HuntScheduled;

        /// <summary>
        /// You found the one it's wearing. It will know the next time it goes down there, and it goes down every
        /// night. If it's dark already you have a couple of minutes; if not, it comes with the dark.
        /// </summary>
        public void VictimFound()
        {
            VictimKnown = true;
            exposure = 100f;
            if (!IsPlotting) BeginPlotting();
            var clock = GameClock.Instance;
            if (clock != null && clock.IsDark)
            {
                forcedHuntAt = Time.time + 150f;
                forcedHuntAtDark = false;
            }
            else
            {
                forcedHuntAt = -1f;
                forcedHuntAtDark = true;
            }
            warnedOfHunt = true;
            PlayerHUD.Instance?.Toast("<i>It will know. Get to the church and ring the bell.</i>");
        }

        /// <summary>
        /// Whether tonight is the night, and whether it only follows (the first night of plotting) or hunts.
        /// Called by the event director once it's dark enough.
        /// </summary>
        public bool WantsHunt(GameClock clock, out bool stalkOnly)
        {
            stalkOnly = false;
            if (clock == null || Mimic == null) return false;
            int day = clock.Day;
            bool lastNight = day >= clock.LastDay;
            bool eveOfLast = day == clock.LastDay - 1 && exposure >= 50f;
            if (HuntDay == day || lastNight || eveOfLast) return true;
            // A night of plotting, and enough known: it decides.
            if (IsPlotting && day > PlotDay && exposure >= 75f) { ScheduleHunt(day); return true; }
            // The first night it only follows, to see where you go.
            if (IsPlotting && day == PlotDay) { stalkOnly = true; return true; }
            return false;
        }

        /// <summary>The hunt never comes unannounced: say so as it starts, if the warning hasn't come already.</summary>
        public void WarnOfHunt()
        {
            if (warnedOfHunt) return;
            warnedOfHunt = true;
            pendingToastTier = 0;
            PlayerHUD.Instance?.Toast("<i>It knows. It's coming for you.</i>");
        }

        private void FlushToast()
        {
            if (plotToastPending)
            {
                plotToastPending = false;
                pendingToastTier = 0;
                PlayerHUD.Instance?.Toast("<i>Something in this town has decided about you.</i>");
                return;
            }
            if (pendingToastTier <= 0) return;
            PlayerHUD.Instance?.Toast(pendingToastTier switch
            {
                1 => "<i>You feel watched.</i>",
                _ => "<i>Someone in this town knows what you're doing.</i>",
            });
            pendingToastTier = 0;
        }

        private float nextHint;

        private void Update()
        {
            var mimic = Mimic;
            var clock = GameClock.Instance;
            var cam = Camera.main;
            if (mimic == null || clock == null || cam == null) return;

            // The forced hunt, once the victim is found: at the next dark, or in a couple of minutes if it's dark now.
            if (VictimKnown && !Events.Stalker.IsHunting && !Events.EventDirector.ForcedHuntPending
                && (EndingDirector.Instance == null || !EndingDirector.Instance.IsPlaying))
            {
                if (forcedHuntAtDark && clock.IsDark) { forcedHuntAtDark = false; forcedHuntAt = Time.time + 60f; }
                if (forcedHuntAt > 0f && Time.time >= forcedHuntAt)
                {
                    forcedHuntAt = -1f;
                    Events.EventDirector.ForcedHuntPending = true;
                }
                else if (Time.time > nextHint && !AccusationUI.IsOpen)
                {
                    nextHint = Time.time + 2f;
                    string when = forcedHuntAt > 0f ? "It's coming." : "It comes with the dark.";
                    PlayerHUD.Instance?.SetHint($"<b>Get to the church and ring the bell.</b> {when}\n<size=75%>{BellRope.DirectionHint(cam.transform.position)}</size>", 0f);
                }
            }

            if (!mimic.isActiveAndEnabled) return;
            float distance = Vector3.Distance(cam.transform.position, mimic.transform.position);
            // Standing near it in the dark: capped per night, and nothing while you're actually talking to someone
            // (the game sends you into the diner on night one; that mustn't cost you).
            if (proximityDay != clock.Day) { proximityDay = clock.Day; proximityTonight = 0f; }
            bool talking = DialogueUI.Instance != null && DialogueUI.Instance.Current != null;
            if (clock.IsDark && distance < 6f && !talking && proximityTonight < ProximityCapPerNight)
            {
                float step = nightProximityRate * Time.deltaTime;
                proximityTonight += step;
                Raise(step, null);
            }
            // It watched you write. Once a day, standing close to it with the notebook open tells it something.
            if (distance < 4f && notebookWatchedDay != clock.Day && Notes.NotebookUI.Instance != null && Notes.NotebookUI.Instance.IsOpen)
            {
                notebookWatchedDay = clock.Day;
                Expose(2f, "watched you write");
            }
        }
    }
}
