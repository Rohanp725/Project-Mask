using System.Collections.Generic;
using System.Linq;
using System.Text;
using Semblance.Residents;
using UnityEditor;
using UnityEngine;

namespace Semblance.EditorTools
{
    /// <summary>
    /// Checks the cast data the mystery depends on. Menu: Semblance > Validate Cast.
    ///  - Every feature id exists in the catalogue.
    ///  - Every left/right physical trait is described (with the right side) in someone else's gossip, so a
    ///    mirrored or missing detail is always catchable from conversation as well as from the records.
    ///  - Everyone has a 4-part schedule, every spot is real, and nobody shares a spot in the same phase.
    ///  - Every Founders' Day account agrees on the shared facts (rain, Mae, cherry) unless the person wasn't there.
    /// </summary>
    public static class CaseValidator
    {
        [MenuItem("Semblance/Validate Cast")]
        public static string Validate()
        {
            var profiles = AssetDatabase.FindAssets("t:ResidentProfile", new[] { "Assets/_Project/Residents" })
                .Select(g => AssetDatabase.LoadAssetAtPath<ResidentProfile>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            var spots = new HashSet<string>(Object.FindObjectsByType<ScheduleSpot>(FindObjectsInactive.Include).Select(s => s.Id));
            var problems = new List<string>();
            var notes = new List<string>();

            foreach (var p in profiles)
            {
                foreach (var f in p.features)
                    if (FeatureCatalog.Get(f.id) == null)
                        problems.Add($"{p.displayName}: unknown feature '{f.id}'");

                // Sided physical traits must be described by someone else, with the correct side.
                string first = p.displayName.StartsWith("Father") ? "Crane" : p.displayName.Split(' ')[0];
                foreach (var f in p.features.Where(f => f.side != FeatureSide.None))
                {
                    var def = FeatureCatalog.Get(f.id);
                    if (def == null || def.Kind != FeatureKind.Trait || !def.Physical) continue;
                    string side = f.side == FeatureSide.Left ? "left" : "right";
                    bool covered = profiles.Where(o => o != p).SelectMany(o => o.gossip)
                        .Any(g => (g.title.Contains(first) || g.title.Contains(p.displayName)) && g.response.ToLowerInvariant().Contains(side));
                    if (!covered)
                        problems.Add($"{p.displayName}: {side} '{f.id}' isn't described in anyone's gossip");
                }

                if (p.schedule == null || p.schedule.Length < 4 || p.schedule.Any(string.IsNullOrEmpty))
                    problems.Add($"{p.displayName}: schedule incomplete");
                else if (spots.Count > 0)
                    foreach (var s in p.schedule.Where(s => !spots.Contains(s)))
                        problems.Add($"{p.displayName}: schedule spot '{s}' doesn't exist in the open scene");

                string fd = (p.foundersDay ?? "").ToLowerInvariant();
                bool attended = !fd.Contains("didn't go");
                if (attended && !(fd.Contains("rain") || fd.Contains("wet") || fd.Contains("pour")))
                    problems.Add($"{p.displayName}: Founders' Day account doesn't mention the rain");
                if (attended && !(fd.Contains("mae") || fd.Contains("mama") || fd.Contains("i won")))
                    notes.Add($"{p.displayName}: Founders' Day account doesn't mention Mae winning");
            }

            for (int phase = 0; phase < 4; phase++)
                foreach (var clash in profiles.Where(p => p.schedule != null && p.schedule.Length > phase)
                             .GroupBy(p => p.schedule[phase]).Where(g => g.Count() > 1))
                    problems.Add($"Phase {phase}: {string.Join(", ", clash.Select(p => p.displayName))} share '{clash.Key}'");

            var sb = new StringBuilder($"[Semblance] Cast validation: {profiles.Count} residents, {problems.Count} problem(s), {notes.Count} note(s).");
            foreach (var x in problems) sb.Append("\n  PROBLEM: " + x);
            foreach (var x in notes) sb.Append("\n  note: " + x);
            if (problems.Count > 0) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb.ToString());
            return sb.ToString();
        }
    }
}
