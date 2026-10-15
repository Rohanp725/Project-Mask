using System;
using UnityEngine;

namespace Semblance.Core
{
    public enum DayPhase { Morning, Afternoon, Dusk, Night }

    /// <summary>
    /// In-game time. Hours run from 6 (6 AM) to 27 (3 AM the next morning); staying up past that makes the
    /// detective pass out. Time stops while a menu or conversation is open.
    /// </summary>
    public class GameClock : MonoBehaviour
    {
        public static GameClock Instance { get; private set; }

        public const float MorningHour = 6f;
        public const float AfternoonHour = 12f;
        public const float DuskHour = 18f;
        public const float NightHour = 21f;
        public const float CollapseHour = 27f;

        [SerializeField] private float secondsPerGameHour = 90f;

        /// <summary>Multiplier on how fast time passes (story beats slow it while you're inside a gathering).</summary>
        public static float RateScale { get; set; } = 1f;
        [SerializeField] private int startDay = 1;
        [SerializeField] private float startHour = 18.5f;

        public int Day { get; private set; }
        public float Hour { get; private set; }
        public int LastDay { get; set; } = 5;
        public bool Frozen { get; set; }
        public DayPhase Phase => PhaseAt(Hour);
        public bool IsDark => Hour >= NightHour || Hour < 6.5f;

        public event Action<DayPhase> PhaseChanged;
        public event Action<int> DayStarted;
        /// <summary>Raised when the detective stays awake past 3 AM.</summary>
        public event Action Collapsed;
        /// <summary>Raised once per in-game hour.</summary>
        public event Action<int> HourPassed;

        private DayPhase lastPhase;

        public static DayPhase PhaseAt(float hour)
        {
            if (hour < AfternoonHour && hour >= MorningHour) return DayPhase.Morning;
            if (hour < DuskHour && hour >= AfternoonHour) return DayPhase.Afternoon;
            if (hour < NightHour && hour >= DuskHour) return DayPhase.Dusk;
            return DayPhase.Night;
        }

        private void Awake()
        {
            Instance = this;
            Day = startDay;
            Hour = startHour;
            lastPhase = Phase;
        }

        private void Update()
        {
            if (Frozen || InputLock.IsLocked)
                return;
            Advance(Time.deltaTime * RateScale / Mathf.Max(1f, secondsPerGameHour));
        }

        public void Advance(float hours)
        {
            int previousHour = Mathf.FloorToInt(Hour);
            Hour += hours;
            if (Mathf.FloorToInt(Hour) != previousHour)
                HourPassed?.Invoke(Mathf.FloorToInt(Hour));
            CheckPhase();
            if (Hour >= CollapseHour)
            {
                Collapsed?.Invoke();
                if (Hour > CollapseHour) Hour = CollapseHour; // held at 3 AM until something lets the night end
            }
        }

        /// <summary>Jumps to a time, e.g. after sleeping. Rolls the day over if needed.</summary>
        public void SetTime(int day, float hour)
        {
            bool newDay = day != Day;
            Day = day;
            Hour = hour;
            if (newDay)
                DayStarted?.Invoke(Day);
            CheckPhase(force: true);
        }

        public void SleepUntilMorning() => SetTime(Day + 1, MorningHour);

        private void CheckPhase(bool force = false)
        {
            var phase = Phase;
            if (phase == lastPhase && !force)
                return;
            lastPhase = phase;
            PhaseChanged?.Invoke(phase);
        }

        public static string FormatHour(float hour)
        {
            float h = hour % 24f;
            int hh = Mathf.FloorToInt(h);
            int mm = Mathf.FloorToInt((h - hh) * 60f / 10f) * 10;
            string suffix = hh >= 12 ? "PM" : "AM";
            int display = hh % 12 == 0 ? 12 : hh % 12;
            return $"{display}:{mm:00} {suffix}";
        }

        public string Label => $"Day {Day}, {FormatHour(Hour)}";
        public static string Stamp(int day, float hour) => $"Day {day}, {FormatHour(hour)}";
    }
}
