using System;
using System.Collections.Generic;
using System.IO;
using Semblance.Notes;
using UnityEngine;

namespace Semblance.Save
{
    /// <summary>Everything needed to resume a case. The case itself is rebuilt from the seed.</summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int seed;
        public int difficulty;
        public int day;
        public float hour;
        public float awareness;
        public float exposure;
        public int plotDay = -1;
        public int huntDay = -1;
        public bool victimKnown;
        public string hint;
        public float nerve;
        public int film;
        public float battery;
        public Notebook.State notebook;
        public List<string> flags = new List<string>();
        public string savedAt;
    }

    /// <summary>
    /// Saves to persistentDataPath/semblance_save.json, once per night when the detective sleeps.
    /// Systems register how to write and read their part of the save.
    /// </summary>
    public static class SaveSystem
    {
        private static readonly List<(Action<SaveData> write, Action<SaveData> read)> Participants = new List<(Action<SaveData>, Action<SaveData>)>();

        /// <summary>Story/progress flags (evidence found, events seen...). Saved with the game.</summary>
        public static readonly HashSet<string> Flags = new HashSet<string>();

        public static string SavePath => Path.Combine(Application.persistentDataPath, "semblance_save.json");
        public static bool HasSave => File.Exists(SavePath);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            Participants.Clear();
            Flags.Clear();
        }

        public static void Register(Action<SaveData> write, Action<SaveData> read) => Participants.Add((write, read));

        public static void ClearParticipants() => Participants.Clear();

        /// <summary>True while a save is being applied (so per-day effects don't fire again for a day already lived).</summary>
        public static bool Loading { get; private set; }

        /// <summary>Set when a save file couldn't be read and was moved aside; the menu tells the player.</summary>
        public static string LastLoadProblem { get; set; }

        public static bool Save()
        {
            try
            {
                var data = new SaveData { savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
                data.flags.AddRange(Flags);
                foreach (var (write, _) in Participants)
                    write?.Invoke(data);
                string tmp = SavePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(tmp, SavePath);
                Debug.Log("[Semblance] Saved to " + SavePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Semblance] Save failed: " + e.Message);
                UI.PlayerHUD.Instance?.Toast("<color=#c03020>Couldn't save the game.</color> " + e.Message);
                return false;
            }
        }

        public static SaveData Peek()
        {
            if (!HasSave) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (data == null || data.seed == 0) throw new Exception("empty or incomplete save");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Semblance] Save unreadable: " + e.Message);
                try { File.Move(SavePath, SavePath + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")); } catch { }
                LastLoadProblem = "Your saved game couldn't be read, so it was set aside. Starting a new case.";
                return null;
            }
        }

        /// <summary>Applies a loaded save to every registered system.</summary>
        public static bool Load()
        {
            var data = Peek();
            if (data == null) return false;
            Flags.Clear();
            foreach (var f in data.flags) Flags.Add(f);
            Loading = true;
            try
            {
                foreach (var (_, read) in Participants)
                    read?.Invoke(data);
            }
            finally { Loading = false; }
            return true;
        }

        public static void DeleteSave()
        {
            if (HasSave) File.Delete(SavePath);
            Notebook.DeletePhotos();
        }
    }
}
