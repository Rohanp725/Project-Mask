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
    /// How much the mimic knows you're onto it (0-100), hidden from the player.
    /// Tiers: 0 unaware, 1 curious (from 25), 2 hostile (from 50), 3 hunting (from 75).
    /// Raised by photographing it, questioning it, reading its record, finding evidence and lingering near it at night.
    /// Higher tiers unlock worse night events and, at 3, the hunt.
    /// </summary>
    public class MimicAwareness : MonoBehaviour
    {
        public static MimicAwareness Instance { get; private set; }

        [SerializeField, Range(0, 100)] private float awareness;
        [SerializeField] private float nightProximityRate = 0.3f;
        [SerializeField] private float dailyGrowth = 16f;

        public float Value => awareness;
        public int Tier => awareness >= 75 ? 3 : awareness >= 50 ? 2 : awareness >= 25 ? 1 : 0;
        public event Action<int> TierChanged;

        private int lastTier;

        private void Awake() => Instance = this;

        private void Start()
        {
            PhotoCamera.PhotoTaken += subjects =>
            {
                var mimic = Mimic;
                if (mimic != null && subjects.Contains(mimic)) Raise(6f, "photographed");
            };
            DialogueUI.TopicAsked += (resident, topic) =>
            {
                if (resident != Mimic) return;
                if (topic == "observe") Raise(3f, "looked over");
                else if (topic == ResidentProfile.TopicFoundersDay || topic == ResidentProfile.TopicStrange) Raise(2f, "questioned");
                else Raise(0.75f, "talked");
            };
            Archive.ArchiveUI.CardViewed += resident => { if (resident == Mimic) Raise(1.5f, "record read"); };
            if (GameClock.Instance != null)
            {
                // Restoring a save also "starts" its day: don't grow awareness for a day that was already counted.
                GameClock.Instance.DayStarted += _ => { if (!Save.SaveSystem.Loading) Raise(dailyGrowth, "another day"); };
                // The feeling of being watched arrives later, not the moment you look at it: a toast that fired on
                // the spot would point straight at the mimic.
                GameClock.Instance.PhaseChanged += _ => FlushToast();
                GameClock.Instance.DayStarted += _ => FlushToast();
            }
            Save.SaveSystem.Register(d => d.awareness = awareness, d => { awareness = d.awareness; lastTier = Tier; });
        }

        private static Resident Mimic => CaseDirector.Instance != null ? CaseDirector.Instance.Mimic : null;

        public void Raise(float amount, string reason = null)
        {
            awareness = Mathf.Clamp(awareness + amount * GameSettings.AwarenessRate, 0f, 100f);
            int tier = Tier;
            if (tier == lastTier) return;
            if (tier > lastTier)
                pendingToastTier = Mathf.Max(pendingToastTier, tier);
            lastTier = tier;
            TierChanged?.Invoke(tier);
        }

        private int pendingToastTier;
        private bool warnedOfHunt;

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
            if (pendingToastTier <= 0) return;
            PlayerHUD.Instance?.Toast(pendingToastTier switch
            {
                1 => "<i>You feel watched.</i>",
                2 => "<i>Someone in this town knows what you're doing.</i>",
                _ => "<i>It knows. It's coming for you.</i>",
            });
            if (pendingToastTier >= 3) warnedOfHunt = true;
            pendingToastTier = 0;
        }

        private void Update()
        {
            var mimic = Mimic;
            var clock = GameClock.Instance;
            var cam = Camera.main;
            if (mimic == null || clock == null || cam == null || !mimic.isActiveAndEnabled) return;
            if (clock.IsDark && Vector3.Distance(cam.transform.position, mimic.transform.position) < 6f)
                Raise(nightProximityRate * Time.deltaTime, null);
        }
    }
}
