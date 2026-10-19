using System;
using UnityEngine;

namespace Semblance.Core
{
    public enum Difficulty { Easy, Normal, Hard }

    /// <summary>
    /// Difficulty and player options, saved in PlayerPrefs.
    ///   Easy:   4 tells, 2 red herrings, 6 days, details listed when you look someone over.
    ///   Normal: 3 tells, 5 red herrings, 5 days, details listed.
    ///   Hard:   2 tells, 6 red herrings, 4 days, no detail lists (spot everything yourself).
    /// </summary>
    public static class GameSettings
    {
        public static event Action Changed;

        public static Difficulty Difficulty { get => (Difficulty)Get("difficulty", 1); set => Set("difficulty", (int)value); }
        public static float MouseSensitivity { get => GetF("mouse", 0.1f); set => SetF("mouse", value); }
        public static float Brightness { get => GetF("brightness", 1f); set => SetF("brightness", value); }
        public static float MasterVolume { get => GetF("volume", 0.8f); set => SetF("volume", value); }
        /// <summary>Level of the always-on beds (wind, crickets, room tone, drones), relative to everything else.</summary>
        public static float AmbienceVolume { get => GetF("ambience", 0.8f); set => SetF("ambience", value); }
        /// <summary>The chase, the menu theme and the jukebox, relative to everything else.</summary>
        public static float MusicVolume { get => GetF("music", 0.8f); set => SetF("music", value); }
        public static float FieldOfView { get => GetF("fov", 70f); set => SetF("fov", value); }
        public static int PixelHeight { get => Get("pixelHeight", 240); set => Set("pixelHeight", value); }
        public static bool Dither { get => Get("dither", 1) == 1; set => Set("dither", value ? 1 : 0); }
        public static bool InvertY { get => Get("invertY", 0) == 1; set => Set("invertY", value ? 1 : 0); }
        public static bool ShowHints { get => Get("hints", 1) == 1; set => Set("hints", value ? 1 : 0); }
        /// <summary>The tutorial has been started at least once: the game no longer opens straight into it.</summary>
        public static bool TutorialSeen { get => Get("tutorialSeen", 0) == 1; set => Set("tutorialSeen", value ? 1 : 0); }
        /// <summary>Scales all interface text and panels (0.85 - 1.5).</summary>
        public static float TextScale { get => GetF("textScale", 1f); set => SetF("textScale", value); }
        /// <summary>Head bob and fear camera shake. Off for players who get motion sick.</summary>
        public static bool CameraMotion { get => Get("cameraMotion", 1) == 1; set => Set("cameraMotion", value ? 1 : 0); }
        /// <summary>Short captions for sounds you have to hear (knocking, whispers, footsteps), with direction.</summary>
        public static bool SoundCaptions { get => Get("captions", 0) == 1; set => Set("captions", value ? 1 : 0); }
        /// <summary>Fewer, gentler scares: one event a night and no faces jumping out.</summary>
        public static bool ReduceScares { get => Get("reduceScares", 0) == 1; set => Set("reduceScares", value ? 1 : 0); }

        public static bool ListObservedDetails => Difficulty != Difficulty.Hard;
        public static int TellCount => TellCountFor(Difficulty);
        public static int TellCountFor(Difficulty d) => d == Difficulty.Easy ? 4 : d == Difficulty.Normal ? 3 : 2;
        public static int RedHerringCount => Difficulty == Difficulty.Easy ? 2 : Difficulty == Difficulty.Normal ? 5 : 6;
        public static int DayCount => Difficulty == Difficulty.Easy ? 6 : Difficulty == Difficulty.Normal ? 5 : 4;
        /// <summary>How fast the mimic's awareness of you grows.</summary>
        public static float AwarenessRate => Difficulty == Difficulty.Easy ? 0.7f : Difficulty == Difficulty.Normal ? 1f : 1.35f;

        private static int Get(string key, int fallback) => PlayerPrefs.GetInt("semblance." + key, fallback);
        private static float GetF(string key, float fallback) => PlayerPrefs.GetFloat("semblance." + key, fallback);

        private static void Set(string key, int value)
        {
            PlayerPrefs.SetInt("semblance." + key, value);
            Changed?.Invoke();
        }

        private static void SetF(string key, float value)
        {
            PlayerPrefs.SetFloat("semblance." + key, value);
            Changed?.Invoke();
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
