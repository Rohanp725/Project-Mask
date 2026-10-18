using System.Collections.Generic;
using System.Linq;
using Semblance.Core;
using Semblance.Residents;

namespace Semblance.Case
{
    /// <summary>
    /// Everything in a case that can single somebody out, apart from the mimic's drawn tells.
    ///
    /// The rule: nothing may be true of the mimic in every seed. Each channel (a flaring photo, the back pew,
    /// a cold hand, a defensive answer, a bad record, a wrong Founders' Day story) is either a tell the mimic
    /// drew this time, or noise that lands on innocents, and usually both. Innocent noise always has an ordinary
    /// explanation somewhere in town, so a careful detective can clear people; the mimic's never does.
    /// </summary>
    public sealed class CaseNoise
    {
        // Seeded per case.
        public readonly HashSet<Resident> PhotoFlare = new HashSet<Resident>();
        public readonly Dictionary<Resident, string> BackPew = new Dictionary<Resident, string>();
        public readonly HashSet<Resident> CricketHush = new HashSet<Resident>();
        public readonly HashSet<Resident> FedUp = new HashSet<Resident>();
        public readonly HashSet<Resident> ColdHands = new HashSet<Resident>();
        public readonly HashSet<Resident> ColdFront = new HashSet<Resident>();
        /// <summary>Innocents whose 1979 record has a detail on the wrong side (Doc's mistake): feature id.</summary>
        public readonly Dictionary<Resident, string> RecordErrors = new Dictionary<Resident, string>();
        /// <summary>Residents who carry run-only gossip (witnesses). Kept alive through Day 3 so alibis don't vanish.</summary>
        public readonly HashSet<Resident> Witnesses = new HashSet<Resident>();
        public Resident Defensive;
        /// <summary>An innocent who won't come to the door this run, for a reason you see when you open it.</summary>
        public Resident DoorHider;
        public string DoorHiderReason;
        /// <summary>Whether the mimic lets a knock go unanswered when it's alone at home.</summary>
        public bool MimicHidesAtDoor;
        /// <summary>Whether the mimic answers a night knock awake and dressed (it never sleeps), or plays the woken sleeper.</summary>
        public bool MimicAwakeAtNight;
        public Resident NoticerDecoy;
        public Resident FoundersLiar;
        public Resident EchoSource;
        public bool MimicConfrontsHumanly;
        public bool MimicExcusesColdHands;

        public bool IsFlare(Resident r) => PhotoFlare.Contains(r);

        // ------------------------------------------------------------ Generation

        private readonly List<Resident> residents;
        private readonly Dictionary<Resident, RunModifications> mods;
        private readonly Resident mimic;
        private readonly System.Random rng;

        public CaseNoise(List<Resident> residents, Dictionary<Resident, RunModifications> mods, Resident mimic, System.Random rng)
        {
            this.residents = residents;
            this.mods = mods;
            this.mimic = mimic;
            this.rng = rng;
        }

        private List<Resident> Innocents => residents.Where(r => r != mimic).ToList();

        private Resident Pick(IEnumerable<Resident> pool)
        {
            var list = pool.ToList();
            return list.Count == 0 ? null : list[rng.Next(list.Count)];
        }

        private static bool IsPastorOrRecluse(Resident r) => r.DisplayName == CastNames.Crane || r.DisplayName == CastNames.Wendell;

        /// <summary>Gives a neighbour a line of gossip about somebody, for this run only.</summary>
        public bool AddWitness(Resident about, string line, IEnumerable<Resident> preferred = null, string explains = null, bool townsfolkOnly = false)
        {
            string title = "About " + about.DisplayName;
            bool Free(Resident w) => w != about && w != mimic && w.isActiveAndEnabled && !(townsfolkOnly && IsPastorOrRecluse(w))
                && !w.Profile.gossip.Exists(g => g.title == title) && !mods[w].ExtraGossip.Exists(g => g.title == title);
            var witness = Pick((preferred ?? Enumerable.Empty<Resident>()).Where(Free));
            if (witness == null) witness = Pick(residents.Where(Free));
            if (witness == null) return false;
            mods[witness].ExtraGossip.Add(new DialogueTopic(title, Fill(line, about)) { explains = explains });
            Witnesses.Add(witness);
            return true;
        }

        public static string Fill(string line, Resident r)
        {
            string poss = r.Profile.possessive;
            string obj = poss == "his" ? "him" : poss == "her" ? "her" : "them";
            string subj = poss == "his" ? "he" : poss == "her" ? "she" : "they";
            return line.Replace("{first}", r.FirstName).Replace("{last}", r.LastName)
                .Replace("{his}", poss).Replace("{him}", obj).Replace("{he}", subj);
        }

        public void Generate(bool mimicSitsWrong, bool mimicHushesCrickets)
        {
            var innocents = Innocents;

            // Photographs: one or two innocents' eyes catch the flash white too (it happens with pale eyes).
            int flares = 1 + rng.Next(2);
            foreach (var r in innocents.OrderBy(_ => rng.Next()).Take(flares))
            {
                PhotoFlare.Add(r);
                AddWitness(r, "{first}'s got pale eyes. They catch a camera flash like a cat's in headlights. Every school photo, two white coins.", explains: "flare");
            }

            // Sunday pews: one or two innocents sit apart from their families, for reasons somebody can tell you.
            if (mimicSitsWrong) BackPew[mimic] = null;
            string[] pewReasons =
            {
                "{first} always sits at the back. Late from milking, every Sunday since '62, and too proud to walk up the aisle in boots.",
                "{first} hasn't sat with the rest of the {last}s since the fight at harvest. Sits at the back like a stranger now.",
                "{first} sits by the door so {he} can leave before the last hymn. Can't abide the singing since the funeral.",
                "{first} sits at the back with a hip flask. Everybody knows. Nobody says.",
            };
            int pewCount = 1 + rng.Next(2);
            foreach (var r in innocents.Where(r => !IsPastorOrRecluse(r)).OrderBy(_ => rng.Next()).Take(pewCount))
            {
                string reason = pewReasons[rng.Next(pewReasons.Length)];
                BackPew[r] = reason;
                AddWitness(r, reason);
            }

            // Crickets go quiet around one innocent who keeps very still at night (Irene's porch, a watcher), and
            // around the mimic only if it drew that sign.
            if (mimicHushesCrickets) CricketHush.Add(mimic);
            var hush = Pick(innocents.Where(r => !IsPastorOrRecluse(r)));
            if (hush != null)
            {
                CricketHush.Add(hush);
                AddWitness(hush, "{first} sits out on the porch at night so still the crickets forget {he}'s there. Always has. Gives you a turn.");
            }

            // A few people are simply fed up with a stranger asking questions.
            foreach (var r in innocents.OrderBy(_ => rng.Next()).Take(2 + rng.Next(2))) FedUp.Add(r);

            // Confrontation: one innocent gets defensive and evasive; half the time the mimic answers like a person.
            Defensive = Pick(innocents);
            MimicConfrontsHumanly = rng.NextDouble() < 0.5;

            // Doors: one innocent won't answer a knock for a reason of their own; the mimic sometimes won't either.
            MimicHidesAtDoor = rng.NextDouble() < 0.5;
            MimicAwakeAtNight = rng.NextDouble() < 0.5;
            DoorHider = Pick(innocents.Where(r => !IsPastorOrRecluse(r) && r != Defensive));
            string[] hidingReasons =
            {
                "{first} is at the kitchen table with a bottle and the bank's letter, and doesn't look up.",
                "{first} is packing a suitcase on the bed. {first} sees me and keeps packing.",
                "{first} is on {his} knees with a bucket, scrubbing at something on the floor. \"Dog,\" {he} says. There's no dog.",
                "{first} is sitting in the dark with the radio on low and a photograph in {his} lap. \"Not today, Detective.\"",
                "{first} has every drawer in the house open and is going through them, fast, like somebody looking for something before someone else finds it.",
            };
            DoorHiderReason = hidingReasons[rng.Next(hidingReasons.Length)]; // a template: filled in for whoever it turns out to be

            // Cold hands: about a quarter of innocents. The mimic, if it drew cold hands, sometimes makes an excuse too.
            foreach (var r in innocents)
                if (rng.NextDouble() < 0.25) ColdHands.Add(r);
            MimicExcusesColdHands = rng.NextDouble() < 0.4;

            // After the disappearance, two innocents are as cold about it as the mimic might be.
            ColdFront.Add(mimic);
            foreach (var r in innocents.OrderBy(_ => rng.Next()).Take(2)) ColdFront.Add(r);

            // The tell-noticer can misfire on somebody with a twitchy, staring or nervous manner.
            NoticerDecoy = Pick(innocents.Where(r => r.Profile.features.Concat(mods[r].AddedFeatures).Any(f =>
            {
                var d = FeatureCatalog.Get(f.id);
                return d != null && (d.Category == FeatureCategory.Behaviour || f.id == "avoids_gaze" || f.id == "nervous_laugh");
            }))) ?? Pick(innocents);

            AddRecordError(innocents);
            AddRecentChange(innocents);
            AddFoundersLiar(innocents);
        }

        /// <summary>D(a): Doc wrote one innocent's detail on the wrong side. Gossip and your own eyes agree with each other.</summary>
        private void AddRecordError(List<Resident> innocents)
        {
            var candidates = innocents.Where(r => r.DisplayName != CastNames.Doc && r.Profile.features.Any(f => f.inArchive && f.side != FeatureSide.None
                && FeatureCatalog.Get(f.id)?.Physical == true)).ToList();
            foreach (var who in candidates.OrderBy(_ => rng.Next()))
            {
                var features = who.Profile.features.Where(f => f.inArchive && f.side != FeatureSide.None && FeatureCatalog.Get(f.id)?.Physical == true)
                    .OrderBy(f => f.id).ToList();
                var chosen = features[rng.Next(features.Count)];
                // The neighbour says which side it really is, so the mistake can be cleared without trusting anyone else.
                // Only make the mistake if someone is there to correct it.
                string truth = who.Named(FeatureCatalog.Get(chosen.id).Describe(chosen.side));
                if (!AddWitness(who, "Doc's records? Doc wrote half this town down back to front. He's had {first} wrong for twenty years. " + truth + " Always has been.", explains: "feat:" + chosen.id))
                    continue;
                RecordErrors[who] = chosen.id;
                return;
            }
        }

        /// <summary>D(b): one innocent has a genuine change since the 1979 records, and a neighbour saw it happen.</summary>
        private void AddRecentChange(List<Resident> innocents)
        {
            var changes = new (string id, string why, string seen)[]
            {
                ("brow_scar", "Caught a fence post with my face in March. Doc put six stitches in it.", "{first} split {his} eyebrow open on a fence post back in March. Doc stitched it at the diner counter. Six stitches."),
                ("finger_missing", "Grain auger. This spring. I don't want to talk about it.", "{first} lost that finger to the grain auger in April. I drove {him} to Doc's with it in a coffee can."),
                ("cheek_scar", "Barbed wire, when the bull got loose in May.", "The {last} bull got loose in May. {first} went through the wire after it. Face first."),
            };
            foreach (var r in innocents.OrderBy(_ => rng.Next()))
            {
                var existing = new HashSet<string>(r.Profile.features.Select(f => f.id).Concat(mods[r].AddedFeatures.Select(f => f.id)));
                var options = changes.Where(c => !existing.Contains(c.id)).ToList();
                if (options.Count == 0 || RecordErrors.ContainsKey(r)) continue;
                var c = options[rng.Next(options.Count)];
                var side = rng.Next(2) == 0 ? FeatureSide.Left : FeatureSide.Right;
                if (!AddWitness(r, c.seen, explains: "feat:" + c.id)) continue; // only if somebody saw it happen
                mods[r].AddedFeatures.Add(new FeatureInstance(c.id, side, c.why, inArchive: false));
                mods[r].Notes.Add($"recent change '{c.id}' ({side}), not in records");
                return;
            }
        }

        /// <summary>D(c): one innocent lies about Founders' Day for a human reason, and somebody knows why.</summary>
        private void AddFoundersLiar(List<Resident> innocents)
        {
            // Prefer someone in the town center, so a player meets a resolvable contradiction on the first evening.
            var center = new[] { CastNames.Ada, CastNames.Lyle, CastNames.Otis };
            var liar = Pick(innocents.Where(r => center.Contains(r.DisplayName)))
                       ?? Pick(innocents.Where(r => !IsPastorOrRecluse(r) && r.DisplayName != CastNames.Greta));
            if (liar == null) return;
            FoundersLiar = liar;
            mods[liar].TopicOverrides[ResidentProfile.TopicFoundersDay] = CaseDirector.FirstPerson(CaseDirector.WrongFoundersDay[rng.Next(CaseDirector.WrongFoundersDay.Length)], liar);
            mods[liar].Notes.Add("lies about Founders' Day (wasn't there)");
            string[] why =
            {
                "{first} wasn't at Founders' Day, whatever {he} tells you. Spent it drinking in Barlow. Too ashamed to say so.",
                "{first} missed Founders' Day this year. Sick as a dog. Hates admitting it, so {he} just says what everyone else says, and gets it wrong.",
                "{first} told you about Founders' Day? {first} wasn't there. Drove to Tulsa that morning and wouldn't say why.",
            };
            var line = why[rng.Next(why.Length)];
            AddWitness(liar, line, residents.Where(r => center.Contains(r.DisplayName) || r.DisplayName == CastNames.Mae), "founders");
        }
    }

    /// <summary>The cast's names in one place (so story logic doesn't scatter string literals).</summary>
    public static class CastNames
    {
        public const string Mae = "Mae Whitlock", Ada = "Ada Whitlock", Crane = "Father Elias Crane", Otis = "Otis Pruitt",
            Lyle = "Lyle Pruitt", Doc = "Doc Abner Pruitt", Harlan = "Harlan Hollis", Nell = "Nell Hollis", June = "June Marsh",
            Hank = "Hank Marsh", Amos = "Amos Calder", Wendell = "Wendell Dobbs", Greta = "Greta Kessler", Silas = "Silas Thorne",
            Bev = "Bev Thorne", Irene = "Irene Vance";
    }
}
