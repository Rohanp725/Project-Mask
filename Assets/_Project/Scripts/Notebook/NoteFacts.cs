using System.Collections.Generic;
using System.Linq;
using Semblance.Core;
using Semblance.Residents;

namespace Semblance.Notes
{
    /// <summary>
    /// What a note actually claims, in a form the game can check, so a linked contradiction only counts as proof
    /// when the two notes really disagree. Free linking stays allowed (it's how players think); the Case File just
    /// says whether a link holds up.
    ///
    /// Fact formats (NoteEntry.fact):
    ///   rec|feature|side|set   a physician's note from the 1979 records; set = every recorded feature "id:side"
    ///   obs|feature|side|set   something seen when looking a person over; set = everything visible then
    ///   say|feature|side       gossip describing someone's sided feature
    ///   fd|weather|winner|flavor|sermon   a Founders' Day account ("?" = not mentioned)
    ///   strange|hash           an answer to "Seen anything strange?" (identical answers from two people = an echo)
    ///   self|reason            a statement that can't be innocent on its face (knowing your name)
    ///   claim|topic|value      something said or found that makes a checkable claim ("Ruth is in Tulsa" vs the certificate)
    /// </summary>
    public static class NoteFacts
    {
        // ------------------------------------------------------------ Building facts

        public static string Set(IEnumerable<FeatureInstance> features)
            => string.Join(",", features.Select(f => f.id + ":" + (int)f.side).Distinct());

        public static string Record(FeatureInstance f, string set) => $"rec|{f.id}|{(int)f.side}|{set}";
        public static string Observed(FeatureInstance f, string set) => $"obs|{(f != null ? f.id : "-")}|{(f != null ? (int)f.side : -1)}|{set}";
        public static string Strange(string rawAnswer) => "strange|" + StableHash.Of(Notebook.StripTags(rawAnswer ?? "").Trim());
        public const string KnowsName = "self|knows your name";

        /// <summary>Claims that statements can make about the world, found in what people say.</summary>
        public static string Claims(string rawText)
        {
            string t = Notebook.StripTags(rawText ?? "").ToLowerInvariant();
            if (t.Contains("tulsa") && (t.Contains("ruth") || t.Contains("wife") || t.Contains("her sister"))) return "claim|ruth|away";
            return null;
        }

        /// <summary>What a piece of evidence establishes, by its id.</summary>
        public static string EvidenceFact(string id)
        {
            switch (id)
            {
                case "calder_certificate": return "claim|ruth|dead";
                case "grave_headstone": return "claim|ruth|dead";
                default: return null;
            }
        }

        private static string ClaimText(string topic, string value)
        {
            if (topic == "ruth") return value == "away" ? "Ruth Calder is away in Tulsa" : value == "dead" ? "Ruth Calder died in August" : value;
            return $"{topic} is {value}";
        }

        /// <summary>
        /// Does anything else in the notebook back this note up? The records agreeing with gossip, my own eyes agreeing
        /// with the records, several people telling Founders' Day the same way. Shown on the note; not a verdict.
        /// </summary>
        public static string Corroboration(NoteEntry e, Notebook notebook)
        {
            if (e == null || notebook == null || string.IsNullOrEmpty(e.fact)) return null;
            var p = e.fact.Split('|');
            switch (p[0])
            {
                case "say":
                {
                    bool rec = notebook.EntriesFor(e.subject).Any(x => x.fact != null && x.fact.StartsWith("rec|" + p[1] + "|" + p[2] + "|"));
                    bool seen = notebook.EntriesFor(e.subject).Any(x => x.fact != null && x.fact.StartsWith("obs|" + p[1] + "|" + p[2] + "|"));
                    return rec && seen ? "The records agree, and so do my own eyes." : rec ? "The records agree." : seen ? "I saw it myself." : null;
                }
                case "obs":
                    if (p[1] == "-") return null;
                    return notebook.EntriesFor(e.subject).Any(x => x.fact != null && x.fact.StartsWith("rec|" + p[1] + "|" + p[2] + "|")) ? "Matches the records." : null;
                case "fd":
                {
                    int agree = 0;
                    foreach (var other in notebook.Entries)
                    {
                        if (other == e || other.subject == e.subject || other.fact == null || !other.fact.StartsWith("fd|")) continue;
                        var q = other.fact.Split('|');
                        bool same = true, overlap = false;
                        for (int i = 1; i <= 4; i++)
                        {
                            if (p[i] == "?" || q[i] == "?") continue;
                            overlap = true;
                            if (p[i] != q[i]) { same = false; break; }
                        }
                        if (same && overlap) agree++;
                    }
                    return agree >= 2 ? $"{agree} others tell it the same way." : null;
                }
                default:
                    return null;
            }
        }

        /// <summary>Canon: it rained, Mae won, with cherry, and Crane preached the wheat and the tares.</summary>
        public static readonly string[] FoundersCanon = { "rain", "mae", "cherry", "tares" };

        public static string Founders(string raw, string speaker = null)
        {
            string t = (raw ?? "").ToLowerInvariant();
            // Mae tells it in the first person.
            if (speaker == Case.CastNames.Mae) t = t.Replace("i won", "mae won").Replace("my cherry pie won", "mae won cherry").Replace("beat mine", "beat mae");
            if (speaker == Case.CastNames.Greta) t = t.Replace("my cherry pie finally beat", "greta's cherry pie finally beat");
            string weather = t.Contains("clear") ? "clear"
                : t.Contains("rain") || t.Contains("pour") || t.Contains("bucket") || t.Contains("wet") ? "rain" : "?";
            string winner = t.Contains("beat mae") ? "other"
                : (t.Contains("mae") || t.Contains("mama")) && (t.Contains("won") || t.Contains("win") || t.Contains("took")) ? "mae" : "?";
            string flavor = t.Contains("apple pie") ? "apple" : t.Contains("cherry") ? "cherry" : "?";
            string sermon = t.Contains("prodigal") ? "prodigal" : t.Contains("wheat") || t.Contains("tares") || t.Contains("weeds") ? "tares" : "?";
            return $"fd|{weather}|{winner}|{flavor}|{sermon}";
        }

        private static readonly Dictionary<string, string[]> FeatureWords = new Dictionary<string, string[]>
        {
            { "heterochromia", new[] { "eye" } }, { "head_tilt", new[] { "head", "lean" } }, { "brow_scar", new[] { "brow" } },
            { "hearing_aid", new[] { "hearing" } }, { "tattoo_hand", new[] { "tattoo", "anchor" } }, { "hook_hand", new[] { "hook" } },
            { "burn_scars", new[] { "burn" } }, { "cheek_scar", new[] { "scar", "cheek" } }, { "eye_cloudy", new[] { "milky", "eye" } },
        };

        /// <summary>Which of a person's sided features a line of gossip describes, and on which side, if any.</summary>
        public static string Gossip(Resident about, string raw)
        {
            if (about == null || string.IsNullOrEmpty(raw)) return null;
            string t = raw.ToLowerInvariant();
            bool left = t.Contains("left"), right = t.Contains("right");
            if (left == right) return null; // no side, or both: not a claim about one side
            var side = left ? FeatureSide.Left : FeatureSide.Right;
            var sided = about.Profile.features.Where(f => f.side != FeatureSide.None).ToList();
            string[] Words(FeatureInstance f) => FeatureWords.TryGetValue(f.id, out var w) ? w : f.id.Split('_').Where(x => x.Length >= 3).ToArray();
            // Whole words first ("ear" shouldn't be found in "years"), then parts of words ("brow" in "eyebrow").
            var match = sided.FirstOrDefault(f => Words(f).Any(x => System.Text.RegularExpressions.Regex.IsMatch(t, @"\b" + x + @"s?\b")))
                        ?? sided.FirstOrDefault(f => Words(f).Any(x => t.Contains(x)));
            return match != null ? $"say|{match.id}|{(int)side}" : null;
        }

        // ------------------------------------------------------------ Checking links

        private static Dictionary<string, int> ParseSet(string set)
        {
            var d = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(set)) return d;
            foreach (var item in set.Split(','))
            {
                int colon = item.LastIndexOf(':');
                if (colon <= 0) continue;
                if (int.TryParse(item.Substring(colon + 1), out int side)) d[item.Substring(0, colon)] = side;
            }
            return d;
        }

        private static bool IsPermanentTrait(string id)
        {
            var d = FeatureCatalog.Get(id);
            return d != null && d.Kind == FeatureKind.Trait && d.Physical;
        }

        private static string Side(int s) => s == (int)FeatureSide.Left ? "left" : s == (int)FeatureSide.Right ? "right" : "";
        private static string Feature(string id) => id.Replace('_', ' ');

        /// <summary>Do these two notes really contradict each other? <paramref name="why"/> says how (or why not).</summary>
        public static bool Conflict(NoteEntry a, NoteEntry b, out string why) => Check(a, b, out why, out _);

        private static bool Check(NoteEntry a, NoteEntry b, out string why, out string featureKey)
        {
            why = "These don't actually contradict each other.";
            featureKey = null;
            if (a == null || b == null || string.IsNullOrEmpty(a.fact) || string.IsNullOrEmpty(b.fact)) return false;
            var pa = a.fact.Split('|');
            var pb = b.fact.Split('|');
            string ka = pa[0], kb = pb[0];

            if (ka == "self" && a.subject == b.subject) { why = $"{a.subject}: {pa[1]}."; return true; }
            if (kb == "self" && a.subject == b.subject) { why = $"{b.subject}: {pb[1]}."; return true; }

            if (ka == "claim" && kb == "claim")
            {
                if (pa[1] == pb[1] && pa[2] != pb[2]) { why = $"One says {ClaimText(pa[1], pa[2])}; the other, that {ClaimText(pb[1], pb[2])}."; return true; }
                why = pa[1] == pb[1] ? "They say the same thing." : "These are claims about different things.";
                return false;
            }

            if (ka == "fd" && kb == "fd")
            {
                string[] facts = { "the weather", "who won", "the pie", "the sermon" };
                for (int i = 1; i <= 4; i++)
                    if (pa[i] != "?" && pb[i] != "?" && pa[i] != pb[i]) { why = $"They remember {facts[i - 1]} differently."; return true; }
                why = "Their Founders' Day stories agree.";
                return false;
            }
            if (ka == "strange" && kb == "strange")
            {
                if (pa[1] == pb[1] && a.subject != b.subject) { why = "Word for word the same story, from two different people."; return true; }
                return false;
            }

            if (a.subject != b.subject) { why = "These are about different people."; return false; }
            // Order: rec, say, obs.
            if (Rank(ka) > Rank(kb)) { (a, b) = (b, a); (pa, pb) = (pb, pa); (ka, kb) = (kb, ka); }

            if ((ka == "rec" || ka == "say") && kb == "obs")
            {
                string source = ka == "rec" ? "The records" : "Others";
                string f = pa[1];
                int side = int.Parse(pa[2]);
                if (pb[1] != "-")
                {
                    // One detail I noted: on the other side, or something the records never mention at all.
                    string g = pb[1];
                    int gSide = int.Parse(pb[2]);
                    if (g == f && gSide != side) { featureKey = f; why = $"{source} put the {Feature(f)} on the {Side(side)}; I saw it on the {Side(gSide)}."; return true; }
                    if (ka == "rec" && !ParseSet(pa[3]).ContainsKey(g) && IsPermanentTrait(g)) { featureKey = g; why = $"I saw a {Feature(g)} that the records never mention."; return true; }
                    return false;
                }
                // Everything I could see on one look.
                var seen = ParseSet(pb[3]);
                featureKey = f;
                if (!seen.TryGetValue(f, out int seenSide)) { why = $"{source} describe the {Feature(f)}; it wasn't there when I looked."; return true; }
                if (seenSide != side) { why = $"{source} put the {Feature(f)} on the {Side(side)}; I saw it on the {Side(seenSide)}."; return true; }
                if (ka == "rec")
                {
                    var recorded = ParseSet(pa[3]);
                    foreach (var pair in seen)
                        if (!recorded.ContainsKey(pair.Key) && IsPermanentTrait(pair.Key))
                        { featureKey = pair.Key; why = $"I saw a {Feature(pair.Key)} that the records never mention."; return true; }
                }
                return false;
            }
            if (ka == "rec" && kb == "say")
            {
                featureKey = pa[1];
                if (pa[1] == pb[1] && pa[2] != pb[2]) { why = $"The records and the neighbours disagree about which side the {Feature(pa[1])} is on."; return true; }
                return false;
            }
            if (ka == "obs" && kb == "obs" && pa[1] == "-" && pb[1] == "-")
            {
                var x = ParseSet(pa[3]);
                var y = ParseSet(pb[3]);
                foreach (var pair in x)
                    if (IsPermanentTrait(pair.Key) && (!y.TryGetValue(pair.Key, out int s) || s != pair.Value)) { why = $"The {Feature(pair.Key)} changed between one look and the next."; featureKey = pair.Key; return true; }
                foreach (var pair in y)
                    if (IsPermanentTrait(pair.Key) && !x.ContainsKey(pair.Key)) { why = $"A {Feature(pair.Key)} appeared between one look and the next."; featureKey = pair.Key; return true; }
                return false;
            }
            return false;
        }

        private static int Rank(string kind) => kind == "rec" ? 0 : kind == "say" ? 1 : kind == "obs" ? 2 : 3;

        /// <summary>
        /// A contradiction can hold up and still be innocent: Doc wrote it down wrong, it's a new scar, they lied about
        /// Founders' Day for a reason. If the notebook holds the account that explains it, says who explains it.
        /// </summary>
        public static string Explained(NoteEntry a, NoteEntry b)
        {
            var notebook = Notebook.Instance;
            if (notebook == null || !Check(a, b, out _, out string featureKey)) return null;
            var pa = a.fact.Split('|');
            var pb = b.fact.Split('|');
            if (pa[0] == "fd" && pb[0] == "fd")
            {
                foreach (var (e, p) in new[] { (a, pa), (b, pb) })
                    if (Enumerable.Range(0, 4).Any(i => p[i + 1] != "?" && p[i + 1] != FoundersCanon[i]))
                    {
                        var why = notebook.EntriesFor(e.subject).FirstOrDefault(g => g.explains == "founders");
                        if (why != null) return $"{why.source} says {e.subject.Split(' ')[0]} wasn't really there.";
                    }
                return null;
            }
            if (featureKey == null) return null;
            var explanation = notebook.EntriesFor(a.subject).FirstOrDefault(g => g.explains == "feat:" + featureKey);
            return explanation != null ? $"{explanation.source} explains it." : null;
        }

        /// <summary>
        /// For proof: which notes in a holding link count against their speaker or subject. A Founders' Day story
        /// counts only against whoever strays from what happened; an echo counts against both (you decide who copied);
        /// record-vs-sight conflicts count once per feature.
        /// </summary>
        public static IEnumerable<(string subject, string proofKey)> Implicated(NoteEntry a, NoteEntry b)
        {
            if (!Check(a, b, out _, out string featureKey)) yield break;
            var pa = a.fact.Split('|');
            var pb = b.fact.Split('|');
            if (pa[0] == "fd" && pb[0] == "fd")
            {
                foreach (var (e, p) in new[] { (a, pa), (b, pb) })
                    if (Enumerable.Range(0, 4).Any(i => p[i + 1] != "?" && p[i + 1] != FoundersCanon[i]))
                        yield return (e.subject, "fd|" + e.id);
                yield break;
            }
            if (pa[0] == "strange")
            {
                yield return (a.subject, "strange|" + a.id);
                yield return (b.subject, "strange|" + b.id);
                yield break;
            }
            if (pa[0] == "self" || pb[0] == "self")
            {
                yield return (a.subject, "self|" + a.subject);
                yield break;
            }
            if (pa[0] == "claim")
            {
                // The paper is the truth; whoever said otherwise is the one caught out. Two people disagreeing: both.
                bool aSaid = a.kind == NoteKind.Statement || a.kind == NoteKind.Gossip;
                bool bSaid = b.kind == NoteKind.Statement || b.kind == NoteKind.Gossip;
                if (aSaid && !bSaid) yield return (a.source ?? a.subject, "claim|" + pa[1]);
                else if (bSaid && !aSaid) yield return (b.source ?? b.subject, "claim|" + pb[1]);
                else { yield return (a.subject, "claim|" + pa[1]); yield return (b.subject, "claim|" + pb[1]); }
                yield break;
            }
            // Feature conflicts: one proof per feature, however many ways it's linked.
            yield return (a.subject, "feat|" + featureKey);
        }
    }
}
