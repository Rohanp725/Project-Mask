using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Semblance.EditorTools
{
    /// <summary>
    /// The listening pass for the audio review (Semblance > Audio Audition). Steps through every sound in priority order:
    /// what it's for, your original pick, the final and its alternatives and variants, the review's verdict, and anything
    /// flagged (words, buried sounds). Rate 1-5, pick a version, leave a note; it all saves to review/audio/ratings.json.
    /// The list comes from review/audio/audition.json, written by the production step.
    /// </summary>
    public class AudioAudition : EditorWindow
    {
        [Serializable] private class Item
        {
            public string role, title, group, function, feel, where, verdict, confidence, evidence, production, before, after;
            public int priority;
            public string[] flags = new string[0], sources = new string[0], sourceLabels = new string[0];
            public string[] versions = new string[0], versionLabels = new string[0], variants = new string[0];
        }
        [Serializable] private class Manifest { public Item[] items = new Item[0]; public string generated; }
        [Serializable] private class Rating { public string role; public int rating; public string choice; public string note; public string time; }
        [Serializable] private class Ratings { public List<Rating> items = new List<Rating>(); }

        private static string Root => Path.GetDirectoryName(Application.dataPath);
        private static string ManifestPath => Path.Combine(Root, "review/audio/audition.json");
        private static string RatingsPath => Path.Combine(Root, "review/audio/ratings.json");

        private Manifest manifest;
        private Ratings ratings;
        private int index;
        private Vector2 listScroll, detailScroll;
        private bool unratedOnly, loop;
        private string filter = "";
        private string playing;

        [MenuItem("Semblance/Audio Audition")]
        public static void Open() => GetWindow<AudioAudition>("Audio Audition").Load();

        private void OnEnable() => Load();

        private void Load()
        {
            manifest = File.Exists(ManifestPath) ? JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)) : new Manifest();
            ratings = File.Exists(RatingsPath) ? JsonUtility.FromJson<Ratings>(File.ReadAllText(RatingsPath)) ?? new Ratings() : new Ratings();
            manifest.items = manifest.items.OrderBy(i => i.priority).ThenBy(i => i.role).ToArray();
            index = Mathf.Clamp(index, 0, Mathf.Max(0, manifest.items.Length - 1));
        }

        private Rating RatingFor(string role)
        {
            var r = ratings.items.FirstOrDefault(x => x.role == role);
            if (r == null) { r = new Rating { role = role, choice = "", note = "" }; ratings.items.Add(r); }
            return r;
        }

        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RatingsPath));
            File.WriteAllText(RatingsPath, JsonUtility.ToJson(ratings, true));
        }

        // ------------------------------------------------------------ preview playback (the editor's own clip preview)
        private static readonly Type AudioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        private static void Play(AudioClip clip, bool loop)
        {
            Stop();
            var m = AudioUtil?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            m?.Invoke(null, new object[] { clip, 0, loop });
        }
        private static void Stop() => AudioUtil?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);

        private void PlayButton(string label, string path)
        {
            var clip = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            using (new EditorGUI.DisabledScope(clip == null))
            {
                bool on = playing == path;
                if (GUILayout.Button((on ? "■ " : "▶ ") + label, GUILayout.Height(22)))
                {
                    if (on) { Stop(); playing = null; }
                    else { Play(clip, loop); playing = path; }
                }
            }
        }

        // ------------------------------------------------------------ layout
        private void OnGUI()
        {
            if (manifest.items.Length == 0)
            {
                EditorGUILayout.HelpBox("No audition list yet. It's written to review/audio/audition.json by the production step.", MessageType.Info);
                if (GUILayout.Button("Reload")) Load();
                return;
            }
            int rated = manifest.items.Count(i => RatingFor(i.role).rating > 0);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"{rated}/{manifest.items.Length} rated", EditorStyles.toolbarButton, GUILayout.Width(90));
                unratedOnly = GUILayout.Toggle(unratedOnly, "Unrated only", EditorStyles.toolbarButton, GUILayout.Width(90));
                loop = GUILayout.Toggle(loop, "Loop playback", EditorStyles.toolbarButton, GUILayout.Width(100));
                filter = GUILayout.TextField(filter, EditorStyles.toolbarSearchField, GUILayout.Width(160));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Stop", EditorStyles.toolbarButton)) { Stop(); playing = null; }
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton)) Load();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();
                DrawDetail();
            }
        }

        private void DrawList()
        {
            using (var s = new EditorGUILayout.ScrollViewScope(listScroll, GUILayout.Width(250)))
            {
                listScroll = s.scrollPosition;
                string group = null;
                for (int i = 0; i < manifest.items.Length; i++)
                {
                    var it = manifest.items[i]; var r = RatingFor(it.role);
                    if (unratedOnly && r.rating > 0 && i != index) continue;
                    if (!string.IsNullOrEmpty(filter) && it.role.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (it.group != group) { group = it.group; GUILayout.Label(group, EditorStyles.boldLabel); }
                    string mark = r.rating > 0 ? new string('★', r.rating) : "·";
                    var style = i == index ? EditorStyles.helpBox : EditorStyles.label;
                    if (GUILayout.Button($"{mark}  {it.title}", style)) Select(i);
                }
            }
        }

        private void DrawDetail()
        {
            var it = manifest.items[index]; var r = RatingFor(it.role);
            using (var s = new EditorGUILayout.ScrollViewScope(detailScroll))
            {
                detailScroll = s.scrollPosition;
                GUILayout.Label(it.title, new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 });
                var wrap = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true };
                GUILayout.Label($"<b>Where:</b> {it.where}", wrap);
                GUILayout.Label($"<b>What it does:</b> {it.function}", wrap);
                GUILayout.Label($"<b>How it should feel:</b> {it.feel}", wrap);
                EditorGUILayout.Space();

                GUILayout.Label("Your original pick", EditorStyles.boldLabel);
                if (it.sources.Length == 0) GUILayout.Label("(built without a source)", wrap);
                for (int k = 0; k < it.sources.Length; k++) PlayButton(it.sourceLabels.Length > k ? it.sourceLabels[k] : Path.GetFileName(it.sources[k]), it.sources[k]);

                EditorGUILayout.Space();
                GUILayout.Label("Final and alternatives (pick one)", EditorStyles.boldLabel);
                for (int k = 0; k < it.versions.Length; k++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string label = it.versionLabels.Length > k ? it.versionLabels[k] : Path.GetFileNameWithoutExtension(it.versions[k]);
                        bool chosen = r.choice == label || (string.IsNullOrEmpty(r.choice) && k == 0);
                        if (GUILayout.Toggle(chosen, "", GUILayout.Width(18)) && !chosen) { r.choice = label; Save(); }
                        PlayButton(label, it.versions[k]);
                    }
                }
                if (it.variants.Length > 0)
                {
                    GUILayout.Label($"Variants ({it.variants.Length}): the game picks one at random each time", wrap);
                    using (new EditorGUILayout.HorizontalScope())
                        for (int k = 0; k < it.variants.Length; k++) PlayButton((k + 1).ToString(), it.variants[k]);
                }

                EditorGUILayout.Space();
                GUILayout.Label($"<b>Review's verdict:</b> {it.verdict} ({it.confidence} confidence, provisional until you rate it)", wrap);
                if (!string.IsNullOrEmpty(it.before)) GUILayout.Label($"<b>Scores:</b> before {it.before}  →  after {it.after}", wrap);
                if (!string.IsNullOrEmpty(it.evidence)) GUILayout.Label($"<b>Evidence:</b> {it.evidence}", wrap);
                if (!string.IsNullOrEmpty(it.production)) GUILayout.Label($"<b>How it was made:</b> {it.production}", wrap);
                if (it.flags.Length > 0)
                {
                    GUILayout.Label("Flagged", EditorStyles.boldLabel);
                    foreach (var f in it.flags) GUILayout.Label("• " + f, wrap);
                }

                EditorGUILayout.Space();
                GUILayout.Label("Your rating", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int k = 1; k <= 5; k++)
                        if (GUILayout.Toggle(r.rating == k, k.ToString(), "Button", GUILayout.Width(40), GUILayout.Height(26)) && r.rating != k)
                        { r.rating = k; r.time = DateTime.Now.ToString("s"); Save(); }
                    GUILayout.Label("1 = wrong, 3 = usable, 5 = exactly right", EditorStyles.miniLabel);
                }
                EditorGUI.BeginChangeCheck();
                GUI.SetNextControlName("note_" + it.role);   // one named box per sound
                r.note = EditorGUILayout.TextArea(r.note ?? "", GUILayout.MinHeight(60));
                if (EditorGUI.EndChangeCheck()) Save();

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("◀ Previous", GUILayout.Height(26)) && index > 0) Select(index - 1);
                    if (GUILayout.Button("Next ▶", GUILayout.Height(26)) && index < manifest.items.Length - 1) Select(index + 1);
                }
            }
        }

        private void OnDisable() => Stop();

        /// <summary>
        /// Moving to another sound: drop keyboard focus first. IMGUI keeps a focused text box's text across redraws, so without
        /// this the note you were typing followed you to the next sound and saved there.
        /// </summary>
        private void Select(int i)
        {
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
            index = Mathf.Clamp(i, 0, manifest.items.Length - 1);
            Stop(); playing = null;
            Repaint();
        }
    }
}
