using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEngine;

namespace Semblance.EditorTools
{
    /// <summary>
    /// Phase 7 of the audio review: records the game's mix (the AudioListener's output) with the Unity Recorder, in play
    /// mode, to review/audio/mix/&lt;name&gt;.wav. Start() and Stop() are separate calls so a moment can play out between
    /// them. Solo() mutes every bus but one (for measuring the balance between music, ambience, effects and voices).
    /// </summary>
    public static class MixCapture
    {
        private static RecorderController controller;
        public static string Current { get; private set; }

        private static float stopAt = -1f;
        public static bool IsRecording => controller != null && controller.IsRecording();

        /// <summary>Records `seconds` of game time, then stops by itself. (The Recorder renders audio offline at a fixed
        /// 30 fps of game time, so a capture can finish faster than real time and still be in sync.)</summary>
        public static string Start(string name, float seconds)
        {
            var r = Start(name);
            stopAt = Time.time + seconds;
            EditorApplication.update -= Watch; EditorApplication.update += Watch;
            return r + $" for {seconds:0} s";
        }

        private static void Watch()
        {
            if (!EditorApplication.isPlaying || stopAt < 0f) { EditorApplication.update -= Watch; return; }
            if (Time.time >= stopAt) { stopAt = -1f; Stop(); EditorApplication.update -= Watch; }
        }

        public static string Start(string name)
        {
            if (!EditorApplication.isPlaying) return "not playing";
            if (controller != null && controller.IsRecording()) controller.StopRecording();
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var audio = ScriptableObject.CreateInstance<AudioRecorderSettings>();
            audio.name = "Mix " + name;
            audio.Enabled = true;
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "review", "audio", "mix");
            Directory.CreateDirectory(dir);
            audio.OutputFile = Path.Combine(dir, name).Replace('\\', '/');
            settings.AddRecorderSettings(audio);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30f;
            settings.CapFrameRate = false;
            controller = new RecorderController(settings);
            controller.PrepareRecording();
            Current = name;
            return controller.StartRecording() ? "recording " + name : "failed to start";
        }

        public static string Stop()
        {
            if (controller == null || !controller.IsRecording()) return "not recording";
            controller.StopRecording();
            var n = Current; Current = null;
            return "stopped " + n;
        }

        private static Dictionary<Audio.AudioBus, float> Levels =>
            (Dictionary<Audio.AudioBus, float>)typeof(Audio.GameAudio).GetField("BusLevel", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        private static Dictionary<Audio.AudioBus, float> saved;

        /// <summary>Only this bus is heard (null restores the mix).</summary>
        public static string Solo(string bus)
        {
            var levels = Levels;
            if (saved == null) saved = new Dictionary<Audio.AudioBus, float>(levels);
            foreach (var k in new List<Audio.AudioBus>(levels.Keys))
                levels[k] = bus == null || k.ToString() == bus ? saved[k] : 0f;
            if (bus == null) saved = null;
            return bus == null ? "mix restored" : "solo " + bus;
        }
    }
}
