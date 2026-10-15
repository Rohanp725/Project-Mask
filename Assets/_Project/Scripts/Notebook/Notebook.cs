using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Semblance.Core;
using UnityEngine;

namespace Semblance.Notes
{
    public enum NoteKind { Statement, Gossip, Observation, Archive, Photo, Evidence, Sighting, Event }

    [Serializable]
    public class NoteEntry
    {
        public int id;
        public NoteKind kind;
        public string subject;
        public string source;
        public string title;
        public string text;
        public int day;
        public float hour;
        public bool suspicious;
        public string key;
        public int photoIndex = -1;
        /// <summary>What this note claims, in a checkable form (see NoteFacts). Empty for notes that claim nothing testable.</summary>
        public string fact;
        /// <summary>What this note explains away (a neighbour's account): "feat:id", "founders", "flare". See NoteFacts.Explained.</summary>
        public string explains;
    }

    [Serializable]
    public class ContradictionLink
    {
        public int a;
        public int b;
        public int day;
    }

    /// <summary>
    /// The detective's notebook: everything said, seen, read and photographed, filed by person.
    /// The player stars suspicious entries and links pairs of entries as contradictions.
    /// </summary>
    public class Notebook : MonoBehaviour
    {
        public static Notebook Instance { get; private set; }
        public const string CaseSubject = "Case File";

        private readonly List<NoteEntry> entries = new List<NoteEntry>();
        private readonly List<ContradictionLink> contradictions = new List<ContradictionLink>();
        private readonly List<string> subjects = new List<string> { CaseSubject };
        private readonly List<Texture2D> photos = new List<Texture2D>();
        private int nextId = 1;

        public IReadOnlyList<NoteEntry> Entries => entries;
        public IReadOnlyList<ContradictionLink> Contradictions => contradictions;
        public IReadOnlyList<string> Subjects => subjects;
        public IReadOnlyList<Texture2D> Photos => photos;

        /// <summary>Raised for genuinely new entries (not duplicates).</summary>
        public event Action<NoteEntry> EntryAdded;
        public event Action Changed;

        private void Awake() => Instance = this;

        public NoteEntry Record(NoteKind kind, string subject, string title, string text, string source = null, string key = null, int photoIndex = -1)
        {
            if (string.IsNullOrEmpty(subject))
                subject = CaseSubject;
            key ??= $"{kind}|{subject}|{title}|{text}";
            var existing = entries.Find(e => e.key == key);
            if (existing != null)
                return existing;

            var clock = GameClock.Instance;
            var entry = new NoteEntry
            {
                id = nextId++, kind = kind, subject = subject, source = source, title = title, text = text, key = key,
                day = clock != null ? clock.Day : 1, hour = clock != null ? clock.Hour : 0f, photoIndex = photoIndex,
            };
            entries.Add(entry);
            EnsureSubject(subject);
            EntryAdded?.Invoke(entry);
            Changed?.Invoke();
            return entry;
        }

        public bool Has(string key) => entries.Exists(e => e.key == key);

        public void EnsureSubject(string subject)
        {
            if (string.IsNullOrEmpty(subject) || subjects.Contains(subject))
                return;
            subjects.Add(subject);
            Changed?.Invoke();
        }

        public IEnumerable<NoteEntry> EntriesFor(string subject) => entries.Where(e => e.subject == subject);
        public NoteEntry Get(int id) => entries.Find(e => e.id == id);

        public void SetSuspicious(int id, bool value)
        {
            var e = Get(id);
            if (e == null || e.suspicious == value) return;
            e.suspicious = value;
            Changed?.Invoke();
        }

        public bool AddContradiction(int a, int b)
        {
            if (a == b || Get(a) == null || Get(b) == null)
                return false;
            if (contradictions.Exists(c => (c.a == a && c.b == b) || (c.a == b && c.b == a)))
                return false;
            if (LinksLeftToday <= 0)
                return false;
            contradictions.Add(new ContradictionLink { a = a, b = b, day = GameClock.Instance != null ? GameClock.Instance.Day : 1 });
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Building a case takes thought: only a few new links a day. (Stops ticking every record against every look
        /// to make the game do the deduction.) Removing a link gives none back.
        /// </summary>
        public int LinksPerDay => GameSettings.Difficulty == Difficulty.Easy ? 6 : GameSettings.Difficulty == Difficulty.Normal ? 4 : 3;
        public int LinksLeftToday
        {
            get
            {
                int day = GameClock.Instance != null ? GameClock.Instance.Day : 1;
                return LinksPerDay - contradictions.Count(c => c.day == day) - removedToday;
            }
        }
        private int removedToday, removedDay = -1;

        public void RemoveContradiction(ContradictionLink link)
        {
            int day = GameClock.Instance != null ? GameClock.Instance.Day : 1;
            if (removedDay != day) { removedDay = day; removedToday = 0; }
            if (link.day == day) removedToday++;
            RemoveContradictionInternal(link);
        }

        private void RemoveContradictionInternal(ContradictionLink link)
        {
            if (contradictions.Remove(link))
                Changed?.Invoke();
        }

        public int AddPhoto(Texture2D photo)
        {
            photos.Add(photo);
            return photos.Count - 1;
        }

        public Texture2D GetPhoto(int index) => index >= 0 && index < photos.Count ? photos[index] : null;

        /// <summary>How strongly the player's own notes point at someone: stars + contradictions.</summary>
        public int SuspicionScore(string subject)
        {
            int score = entries.Count(e => e.subject == subject && e.suspicious);
            foreach (var c in contradictions)
            {
                var a = Get(c.a);
                var b = Get(c.b);
                if ((a != null && a.subject == subject) || (b != null && b.subject == subject))
                    score += 2;
            }
            return score;
        }

        public static string StripTags(string text) => string.IsNullOrEmpty(text) ? text : Regex.Replace(text, "<.*?>", string.Empty);

        // ------------------------------------------------------------ Save support

        [Serializable]
        public class State
        {
            public List<NoteEntry> entries = new List<NoteEntry>();
            public List<ContradictionLink> contradictions = new List<ContradictionLink>();
            public List<string> subjects = new List<string>();
            public int nextId;
            public int photoCount;
        }

        public static string PhotoDir => System.IO.Path.Combine(Application.persistentDataPath, "semblance_photos");

        private void SavePhotos()
        {
            try
            {
                System.IO.Directory.CreateDirectory(PhotoDir);
                for (int i = 0; i < photos.Count; i++)
                {
                    string path = System.IO.Path.Combine(PhotoDir, $"photo_{i}.png");
                    if (photos[i] != null && !System.IO.File.Exists(path))
                        System.IO.File.WriteAllBytes(path, photos[i].EncodeToPNG());
                }
            }
            catch (Exception e) { Debug.LogWarning("[Semblance] Couldn't save photos: " + e.Message); }
        }

        private void LoadPhotos(int count)
        {
            photos.Clear();
            for (int i = 0; i < count; i++)
            {
                string path = System.IO.Path.Combine(PhotoDir, $"photo_{i}.png");
                Texture2D tex = null;
                if (System.IO.File.Exists(path))
                {
                    tex = new Texture2D(2, 2, TextureFormat.RGB24, false) { filterMode = FilterMode.Point };
                    if (!tex.LoadImage(System.IO.File.ReadAllBytes(path))) tex = null;
                }
                photos.Add(tex);
            }
        }

        /// <summary>Removes saved photos (a new case starts with an empty camera).</summary>
        public static void DeletePhotos()
        {
            try { if (System.IO.Directory.Exists(PhotoDir)) System.IO.Directory.Delete(PhotoDir, true); }
            catch (Exception e) { Debug.LogWarning("[Semblance] Couldn't clear photos: " + e.Message); }
        }

        public State GetState()
        {
            SavePhotos();
            return BuildState();
        }

        private State BuildState() => new State
        {
            photoCount = photos.Count,
            entries = new List<NoteEntry>(entries),
            contradictions = new List<ContradictionLink>(contradictions),
            subjects = new List<string>(subjects),
            nextId = nextId,
        };

        public void LoadState(State state)
        {
            entries.Clear();
            entries.AddRange(state.entries);
            contradictions.Clear();
            contradictions.AddRange(state.contradictions);
            subjects.Clear();
            subjects.AddRange(state.subjects);
            if (!subjects.Contains(CaseSubject))
                subjects.Insert(0, CaseSubject);
            nextId = Mathf.Max(state.nextId, entries.Count == 0 ? 1 : entries.Max(e => e.id) + 1);
            LoadPhotos(state.photoCount);
            foreach (var e in entries)
                if (e.photoIndex >= photos.Count || (e.photoIndex >= 0 && photos[e.photoIndex] == null))
                    e.photoIndex = -1;
            Changed?.Invoke();
        }
    }
}
