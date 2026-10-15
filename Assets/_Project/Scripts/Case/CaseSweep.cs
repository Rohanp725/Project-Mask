using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Semblance.Core;
using UnityEngine;

namespace Semblance.Case
{
    /// <summary>
    /// Development test: generates many cases (one per frame, so the editor stays responsive), runs each one's
    /// overnight drift through the last day, and checks it. Writes Logs/CaseSweepReport.txt, then leaves Play mode.
    /// Started from the menu: Semblance > Run Overnight Case Sweep.
    /// </summary>
    public class CaseSweep : MonoBehaviour
    {
        public int casesPerDifficulty = 200;

        private IEnumerator Start()
        {
            // Let the town finish loading.
            yield return null;
            yield return null;
            var director = CaseDirector.Instance;
            var original = GameSettings.Difficulty;
            CaseDirector.QuietLogs = true;
            var report = new StringBuilder($"Semblance case sweep, {System.DateTime.Now:yyyy-MM-dd HH:mm}\n\n");
            int totalProblems = 0;

            foreach (var difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard })
            {
                GameSettings.Difficulty = difficulty;
                int errors = 0, wrongTells = 0, noHiding = 0, leaks = 0, unexplained = 0, unreachable = 0;
                var mimics = new Dictionary<string, int>();
                var tellKinds = new Dictionary<string, int>();
                var firstErrors = new List<string>();

                for (int i = 1; i <= casesPerDifficulty; i++)
                {
                    try
                    {
                        director.GenerateCase(i * 7919 + (int)difficulty * 104729);
                        if (director.TellDescriptions.Count(t => !t.StartsWith("A smaller sign")) != GameSettings.TellCount) wrongTells++;
                        if (string.IsNullOrEmpty(director.HidingPlace)) noHiding++;
                        string problem = CheckNoise(director) ?? CheckReachable(director);
                        if (problem != null)
                        {
                            if (problem.StartsWith("leak")) leaks++;
                            else if (problem.StartsWith("unexplained")) unexplained++;
                            else unreachable++;
                            if (firstErrors.Count < 6) firstErrors.Add($"seed {director.Seed}: {problem}");
                        }
                        Count(mimics, director.Mimic.DisplayName);
                        foreach (var tell in director.TellDescriptions) Count(tellKinds, Kind(tell));
                        director.ApplyDriftUpTo(GameSettings.DayCount);
                    }
                    catch (System.Exception e)
                    {
                        errors++;
                        if (firstErrors.Count < 3) firstErrors.Add(e.GetType().Name + ": " + e.Message);
                    }
                    yield return null;
                }

                int problems = errors + wrongTells + noHiding + leaks + unexplained + unreachable;
                totalProblems += problems;
                report.AppendLine($"== {difficulty}: {casesPerDifficulty} cases, {problems} problem(s)");
                report.AppendLine($"   exceptions: {errors}   wrong tell count: {wrongTells}   no hiding place: {noHiding}");
                report.AppendLine($"   mimic-only signals: {leaks}   unexplained innocent noise: {unexplained}   unreachable evidence: {unreachable}");
                foreach (var e in firstErrors) report.AppendLine("   e.g. " + e);
                report.AppendLine("   mimic picked: " + string.Join(", ", mimics.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {p.Value}")));
                report.AppendLine("   tells used:   " + string.Join(", ", tellKinds.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {p.Value}")));
                report.AppendLine();
            }

            GameSettings.Difficulty = original;
            CaseDirector.QuietLogs = false;
            report.AppendLine(totalProblems == 0
                ? "RESULT: PASS. Every case generated cleanly, with the right tells, no signal that only the mimic gives, every innocent oddity explainable, and every clue reachable."
                : $"RESULT: {totalProblems} PROBLEM(S). See above.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/CaseSweepReport.txt", report.ToString());
            Debug.Log("[Semblance] Case sweep finished. Report: Logs/CaseSweepReport.txt\n" + report);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#endif
        }

        /// <summary>Nothing may single out the mimic on its own, and every innocent oddity must be explainable.</summary>
        private static string CheckNoise(CaseDirector d)
        {
            var noise = d.Noise;
            var mimic = d.Mimic;
            if (noise == null) return "leak: no noise generated";
            if (noise.PhotoFlare.Count(r => r != mimic) == 0) return "leak: only the mimic flares in photos";
            if (noise.BackPew.Keys.Count(r => r != mimic) == 0) return "leak: only the mimic sits at the back";
            if (noise.CricketHush.Count(r => r != mimic) == 0) return "leak: only the mimic hushes crickets";
            if (noise.ColdFront.Count(r => r != mimic) < 2) return "leak: the mimic is alone in being cold about the missing";
            if (noise.Defensive == null || noise.Defensive == mimic) return "leak: no defensive innocent";
            if (noise.NoticerDecoy == null || noise.NoticerDecoy == mimic) return "leak: no noticer decoy";
            if (mimic.Avatar.Modifications != null && mimic.Avatar.Modifications.Notes.Contains("NO REFLECTION") != d.MimicHasNoReflection)
                return "leak: reflection state inconsistent";
            if (d.MimicHasNoReflection && mimic.gameObject.layer != Rendering.Layers.NoReflection) return "leak: no-reflection layer not applied";
            foreach (var r in d.Residents)
                if (r != mimic && r.gameObject.layer == Rendering.Layers.NoReflection) return "leak: an innocent has no reflection (stale state)";

            foreach (var r in noise.PhotoFlare.Where(r => r != mimic))
                if (!d.Residents.Any(w => w.Avatar.Modifications != null && w.Avatar.Modifications.ExtraGossip.Any(g => g.title == "About " + r.DisplayName && g.explains == "flare")))
                    return "unexplained: nobody explains why " + r.DisplayName + " flares in photos";
            foreach (var r in noise.CricketHush.Where(r => r != mimic))
                if (!d.Residents.Any(w => w.Avatar.Modifications != null && w.Avatar.Modifications.ExtraGossip.Any(g => g.title == "About " + r.DisplayName && g.response.Contains("crickets"))))
                    return "unexplained: nobody explains the quiet around " + r.DisplayName;
            int last = GameSettings.DayCount;
            if (!(StoryBeats.MissingDay >= 2 && StoryBeats.MissingDay < StoryBeats.BodyDay && StoryBeats.BodyDay < last + 1))
                return $"unexplained: story days out of order (missing {StoryBeats.MissingDay}, body {StoryBeats.BodyDay}, last {last})";
            if (noise.FoundersLiar == null) return "unexplained: no Founders' Day liar";
            if (!d.Residents.Any(w => w.Avatar.Modifications != null && w.Avatar.Modifications.ExtraGossip.Any(g => g.title == "About " + noise.FoundersLiar.DisplayName && g.response.Contains("Founders' Day"))))
                return "unexplained: nobody knows why " + noise.FoundersLiar.DisplayName + " lies about Founders' Day";
            foreach (var pair in noise.RecordErrors)
            {
                var truth = pair.Key.Profile.features.First(f => f.id == pair.Value);
                string tag = $"say|{truth.id}|{(int)truth.side}";
                bool vouched = d.Residents.Any(w => w != mimic && w != pair.Key
                    && (w.Profile.gossip.Any(g => g.title == "About " + pair.Key.DisplayName && Notes.NoteFacts.Gossip(pair.Key, g.response) == tag)
                        || (w.Avatar.Modifications != null && w.Avatar.Modifications.ExtraGossip.Any(g => g.title == "About " + pair.Key.DisplayName && Notes.NoteFacts.Gossip(pair.Key, g.response) == tag))));
                if (!vouched) return $"unexplained: nobody describes {pair.Key.DisplayName}'s {truth.id} correctly against Doc's mistake";
            }
            return null;
        }

        /// <summary>Every clue placed for this mimic must be somewhere the player can get to.</summary>
        private static string CheckReachable(CaseDirector d)
        {
            string place = d.HidingPlace;
            if (Investigation.CellarRoom.Find(place) == null) return "unreachable: no room " + place;
            string inside = place.StartsWith("Cellar:") ? $"House:{place.Substring(7)}:Inside" : "Church:Pew2";
            string kitchen = place.StartsWith("Cellar:") ? $"House:{place.Substring(7)}:Kitchen" : "Church:Altar";
            if (Residents.ScheduleSpot.Find(inside) == null) return "unreachable: no spot " + inside;
            if (Residents.ScheduleSpot.Find(kitchen) == null) return "unreachable: no spot " + kitchen;
            return null;
        }

        private static void Count(Dictionary<string, int> d, string key) => d[key] = d.TryGetValue(key, out var n) ? n + 1 : 1;

        private static string Kind(string tell)
        {
            if (tell.StartsWith("Day ")) return "overnight drift";
            if (tell.StartsWith("A mirrored")) return "mirrored detail";
            if (tell.StartsWith("A missing")) return "missing detail";
            if (tell.StartsWith("A smaller sign")) return tell.Contains("crickets") ? "sign: crickets" : "sign: sits apart";
            if (tell.StartsWith("In photographs")) return "photo flare";
            if (tell.Contains("cold")) return "cold hands";
            if (tell.Contains("blinked")) return "never blinks";
            if (tell.Contains("breathe")) return "no breathing";
            if (tell.Contains("neck")) return "head snaps";
            if (tell.Contains("reflection")) return "no reflection";
            if (tell.Contains("greeted")) return "knows your name";
            if (tell.Contains("Founders")) return "wrong memory";
            if (tell.Contains("repeated")) return "echoes someone";
            return "other";
        }
    }
}
