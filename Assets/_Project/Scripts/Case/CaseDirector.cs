using System.Collections.Generic;
using System.Linq;
using System.Text;
using Semblance.Core;
using Semblance.Residents;
using UnityEngine;

namespace Semblance.Case
{
    /// <summary>
    /// Sets up one playthrough from a seed: picks the mimic, gives it tells (count depends on difficulty),
    /// scatters red herrings over innocents, decides where the real resident is hidden, and every dawn makes
    /// the mimic's copy drift a little further from the original (innocents change too, for explained reasons).
    /// Everything is deterministic from the seed, so a save only needs the seed and the day.
    /// </summary>
    public class CaseDirector : MonoBehaviour
    {
        public static CaseDirector Instance { get; private set; }

        [Tooltip("0 = a new random case every run. Any other value replays the same case.")]
        [SerializeField] private int seed;
        [SerializeField] private string detectiveName = "Klein";
        [Tooltip("Print the solution to the console (for development).")]
        [SerializeField] private bool logSolution = false; // off for blind playtests; turn back on to see the answer in the console

        public int Seed { get; private set; }
        public Resident Mimic { get; private set; }
        public IReadOnlyList<Resident> Residents => residents;
        public string DetectiveName => detectiveName;
        /// <summary>Where the real resident is being kept: "Cellar:Family" or "Crypt".</summary>
        public string HidingPlace { get; private set; }
        public IReadOnlyList<string> TellDescriptions => tellDescriptions;
        public IEnumerable<string> RedHerringDescriptions => redHerringDescriptions;
        public bool MimicHasNoReflection { get; private set; }
        /// <summary>Decoys and innocent noise for this case (who else flares, sits apart, lies, has cold hands...).</summary>
        public CaseNoise Noise { get; private set; }
        /// <summary>Smaller signs the mimic drew, which innocents can share: sitting apart at church, hushing crickets.</summary>
        public bool MimicSitsWrong { get; private set; }
        public bool MimicHushesCrickets { get; private set; }
        public bool IsGenerated { get; private set; }

        public event System.Action CaseGenerated;

        /// <summary>Silences the solution log (used by the case sweep).</summary>
        public static bool QuietLogs { get; set; }
        public event System.Action<int> DriftApplied;

        private readonly List<Resident> residents = new List<Resident>();
        private readonly Dictionary<Resident, RunModifications> mods = new Dictionary<Resident, RunModifications>();
        private readonly List<string> tellDescriptions = new List<string>();
        private readonly List<string> redHerringDescriptions = new List<string>();
        private int driftDay = 1;

        public static readonly string[] WrongFoundersDay =
        {
            // Each gets exactly one shared fact wrong, said with total confidence.
            "It rained, sure. Mae won with her apple pie this year. Crane went on about the wheat and the tares.",
            "Cold and clear, not a cloud. Mae's cherry pie won, like always. Crane preached on the wheat and the tares.",
            "It rained. Greta's cherry pie finally beat Mae's. Crane talked about wheat, as I recall.",
            "Rain, and Mae's cherry pie, same as every year. Father Crane preached on the prodigal son.",
        };

        /// <summary>
        /// Each red herring's excuse (what they say when asked), paired with what a neighbour saw that backs it up.
        /// The mimic never gets a witness; innocents usually do, so a careful detective can clear people.
        /// {first} and {last} are the person with the odd detail.
        /// </summary>
        private static readonly Dictionary<string, (string why, string seen)[]> RedHerringExplanations = new Dictionary<string, (string, string)[]>
        {
            { "bandaged_hand", new[]
            {
                ("Caught it on barbed wire.", "{first} tore a hand open on the barbed wire out by the road. I saw it happen. Bled all over my truck seat."),
                ("Kitchen knife slipped. Stupid of me.", "{first} came over for iodine, hand wrapped in a dish towel. Kitchen knife. Clumsy as a calf, always has been."),
            } },
            { "fresh_scratches", new[]
            {
                ("Barn cat. Mean old thing.", "That barn cat at the {last} place took a swipe at me too. Mean as sin. {first}'s face got the worst of it."),
                ("Walked into a blackberry bramble in the dark.", "{first} came out of the brambles by the creek looking like they'd lost a fight. Picking blackberries in the dark. Don't ask me."),
            } },
            { "dark_stains", new[]
            {
                ("Butchered a hog this morning.", "{first} killed a hog this week. I heard it squeal from my porch. That's hog blood, Detective, I'd bet the farm on it."),
                ("Changed the oil on the truck. Don't ask.", "{first} had their head under a truck hood half the day. Oil to the elbows."),
                ("Nosebleed. I get them when the weather turns.", "{first}'s had nosebleeds since grade school. Weather turns, nose goes. Like a barometer."),
            } },
            { "insomnia", new[]
            {
                ("Haven't slept right since the bell started ringing.", "Nobody sleeps right since the bell started. {first} least of all. I see the lamp on at the {last} place at three every night."),
                ("Bad dreams. Everybody's having them.", "{first} told me about the dreams. Same as mine. A room full of mirrors, all turned to face the wall."),
            } },
            { "dirty_nails", new[]
            {
                ("Been digging fence posts all week.", "{first}'s been putting new fence posts in along the road all week. I've watched every hole go in."),
                ("Pulling the last of the potatoes.", "{first} brought me a sack of potatoes. Still dirty. Good potatoes."),
            } },
            { "black_eye", new[]
            {
                ("Walked into the barn door in the dark. The door won.", "{first} walked into the barn door Tuesday night. I heard the door from the road."),
                ("Hank and I had words. Hank's got the other one.", "{first} and Hank Marsh had a set-to outside the store. Both of them wearing it."),
            } },
            { "split_lip", new[]
            {
                ("Cold sore. Every October.", "{first} gets a cold sore on {his} lip every October, regular as the frost."),
                ("Bit it. Chewing and talking at the same time, like my mother told me not to.", null),
            } },
            { "sunburn", new[]
            {
                ("Last of the sun, topping the beet. Forgot my hat.", "{first} was out topping beet all Saturday without a hat. Red as a stop sign."),
            } },
            { "chewed_nails", new[]
            {
                ("I bite them. Always have. Mother used to paint them with mustard.", "{first} has bitten {his} nails to the quick since school."),
            } },
            { "red_eyes", new[]
            {
                ("Hay fever. The corn dust gets me every year about now.", "{first}'s eyes stream every harvest. Hay fever. Can't go near the corn."),
                ("I've been crying, Detective. Is that allowed?", null),
            } },
            { "muddy_boots", new[]
            {
                ("The low field's a bog since the rain. I've been in it to the ankle.", "{first}'s been out in the low field; it's a bog since the rain."),
            } },
            { "gloves", new[]
            {
                ("Burned my hands on the stove Tuesday.", "{first} burned both hands on that old stove. I wrapped them myself. Blisters like quarters."),
                ("Rash. Doc says it's nerves.", "Doc's seen {first}'s hands. A rash, nerves, he says. Half this town's got nerves this year."),
            } },
            { "avoids_gaze", new (string, string)[] { (null, "{first} won't look anybody in the eye since the bell started. It isn't just you, Detective.") } },
            { "nervous_laugh", new (string, string)[] { (null, "{first} laughs when they're scared. Did it at their own daddy's funeral. Don't take it personal.") } },
            { "talks_to_self", new (string, string)[] { (null, "{first} talks to themselves when they're frightened. Started in September. Counting, mostly.") } },
        };

        private void Awake() => Instance = this;

        private void Start()
        {
            Save.SaveSystem.Register(d => d.seed = Seed, null);
            int startSeed = seed;
            if (SceneLoader.ReplaySeed != 0)
            {
                startSeed = SceneLoader.ReplaySeed;
                SceneLoader.ReplaySeed = 0;
            }
            if (SceneLoader.ContinueRequested)
            {
                var save = Save.SaveSystem.Peek();
                if (save != null) startSeed = save.seed;
            }
            GenerateCase(startSeed);
            if (GameClock.Instance != null)
                GameClock.Instance.DayStarted += day => ApplyDriftUpTo(day);
        }

        public void GenerateCase(int requestedSeed)
        {
            residents.Clear();
            residents.AddRange(Resident.All);
            residents.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName)); // stable order: a seed always replays the same case
            if (residents.Count < 2)
            {
                Debug.LogWarning("[Semblance] Not enough residents to generate a case.");
                return;
            }

            Seed = requestedSeed != 0 ? requestedSeed : System.Environment.TickCount & 0x7fffffff;
            var rng = new System.Random(Seed);
            // Generating again (the case sweep, a replay) must not inherit anything from the last case.
            if (Mimic != null)
            {
                Mimic.Avatar.Rebuilt -= ApplyNoReflection;
                if (MimicHasNoReflection)
                    foreach (var t in Mimic.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            }
            MimicHasNoReflection = false;
            mods.Clear();
            tellDescriptions.Clear();
            redHerringDescriptions.Clear();
            usedRedHerrings.Clear();
            foreach (var r in residents)
                mods[r] = new RunModifications();

            Mimic = residents[rng.Next(residents.Count)];
            var noiseRng = new System.Random(Seed * 7919 + 13);
            Noise = new CaseNoise(residents, mods, Mimic, noiseRng);
            ApplyMimicTells(Mimic, mods[Mimic], rng, GameSettings.TellCount);
            HidingPlace = HidingPlaceFor(Mimic);

            // The cast's own scratches, stains and sleepless eyes aren't there every time.
            foreach (var r in residents)
                foreach (var f in r.Profile.features)
                    if (FeatureCatalog.Get(f.id)?.Kind == FeatureKind.RedHerring && noiseRng.NextDouble() < 0.6)
                        mods[r].RemovedFeatureIds.Add(f.id);
                    else if (FeatureCatalog.Get(f.id)?.Kind == FeatureKind.RedHerring)
                        usedRedHerrings.Add(f.id);

            var innocents = residents.Where(r => r != Mimic).OrderBy(_ => rng.Next()).Take(GameSettings.RedHerringCount);
            foreach (var innocent in innocents)
                ApplyRedHerring(innocent, mods[innocent], rng);

            // Smaller signs, which innocents share: drawn separately so they never say "mimic" on their own.
            MimicSitsWrong = Mimic.DisplayName != CastNames.Crane && Mimic.DisplayName != CastNames.Wendell && noiseRng.NextDouble() < 0.5;
            MimicHushesCrickets = noiseRng.NextDouble() < 0.4;
            if (MimicSitsWrong) tellDescriptions.Add("A smaller sign: at Sunday service it sat apart from its family, at the back by the door.");
            if (MimicHushesCrickets) tellDescriptions.Add("A smaller sign: at night the crickets went quiet wherever it stood.");
            Noise.Generate(MimicSitsWrong, MimicHushesCrickets);
            foreach (var pair in Noise.RecordErrors)
                mods[pair.Key].Notes.Add($"records wrong about '{pair.Value}' (Doc's mistake)");

            foreach (var resident in residents)
                resident.Avatar.ApplyRunModifications(mods[resident]);

            if (MimicHasNoReflection)
            {
                ApplyNoReflection();
                Mimic.Avatar.Rebuilt += ApplyNoReflection;
            }

            driftDay = 1;
            IsGenerated = true;
            if (logSolution && !QuietLogs)
                LogSolution();
            CaseGenerated?.Invoke();
        }

        private void ApplyNoReflection()
        {
            if (Mimic == null) return;
            int layer = Rendering.Layers.NoReflection;
            foreach (var t in Mimic.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        /// <summary>A neighbour's account of the mimic's behaviour tell, set somewhere the mimic actually goes.</summary>
        private static string BehaviourRumour(string tell, Resident mimic)
        {
            bool diner = mimic.Profile.schedule != null && mimic.Profile.schedule.Any(s => s != null && s.StartsWith("Diner:"));
            bool church = mimic.DisplayName != CastNames.Wendell && mimic.DisplayName != CastNames.Crane;
            switch (tell)
            {
                case "never_blinks":
                    return church ? "Have you ever seen {first} blink? I watched all through the sermon once. I don't think so."
                                  : "Have you ever seen {first} blink? I talked to {him} across the fence for ten minutes once. I don't think so.";
                case "no_breathing":
                    return diner ? "Sat next to {first} at the diner the other day. Couldn't hear {him} breathe. Just the coffee going cold."
                                 : "Stood talking to {first} over the fence the other morning, close as I am to you. Cold enough to see your breath. I could see mine.";
                case "head_snaps":
                    return "{first} turned round to look at me on the road. Turned too far. I heard the neck go, like a knuckle.";
                case "no_reflection":
                    return "I walked past Mae's mirror behind {first}. I saw me. I didn't see {first}.";
                default:
                    return null;
            }
        }

        /// <summary>Mae and Greta tell their own pie stories in the first person.</summary>
        public static string FirstPerson(string line, Resident speaker)
        {
            if (speaker == null) return line;
            if (speaker.DisplayName == CastNames.Mae)
                return line.Replace("Mae won with her", "I won with my").Replace("Mae's cherry pie won", "My cherry pie won")
                           .Replace("Greta's cherry pie finally beat Mae's", "Greta's cherry pie finally beat mine").Replace("Mae's cherry pie, same", "my cherry pie, same");
            if (speaker.DisplayName == CastNames.Greta)
                return line.Replace("Greta's cherry pie finally beat Mae's", "My cherry pie finally beat Mae's");
            return line;
        }

        /// <summary>The household a resident belongs to, as filed in the notebook ("The Hollis place", "The rectory").</summary>
        public static string Household(Resident r)
        {
            string home = r != null ? r.Profile.home ?? "" : "";
            return home.EndsWith(" Farm") ? "The " + home.Substring(0, home.Length - 5) + " place" : "The rectory";
        }

        public static string HidingPlaceFor(Resident r)
        {
            string home = r.Profile.home ?? "";
            if (home.EndsWith(" Farm"))
                return "Cellar:" + home.Substring(0, home.Length - 5);
            return "Crypt";
        }

        // ------------------------------------------------------------ Tells

        private void ApplyMimicTells(Resident mimic, RunModifications m, System.Random rng, int count)
        {
            var kinds = new List<string> { "physical", "behaviour", "speech", "physical2" };
            if (count <= 2)
                kinds = new List<string> { "behaviour", rng.Next(2) == 0 ? "physical" : "speech" };
            else if (count == 3)
                kinds = new List<string> { "physical", "behaviour", "speech" };

            foreach (var kind in kinds)
            {
                if (kind.StartsWith("physical")) ApplyPhysicalTell(mimic, m, rng);
                else if (kind == "behaviour") ApplyBehaviourTell(m, rng);
                else ApplySpeechTell(mimic, m, rng);
            }
        }

        /// <summary>Whose eyes flare white in a flash photo: the mimic if it drew that tell, plus this case's innocent decoys.</summary>
        public bool FlaresInPhotos(Resident r)
            => r != null && ((r == Mimic && mods.TryGetValue(r, out var m) && m.Notes.Contains("PHOTO FLARE")) || (Noise != null && Noise.IsFlare(r)));

        private static IEnumerable<FeatureInstance> PhysicalTraits(Resident r) => r.Profile.features.Where(f =>
        {
            var d = FeatureCatalog.Get(f.id);
            return d != null && d.Kind == FeatureKind.Trait && d.Physical && f.inArchive;
        });

        private void ApplyPhysicalTell(Resident mimic, RunModifications m, System.Random rng)
        {
            var traits = PhysicalTraits(mimic).Where(f => !m.SideOverrides.ContainsKey(f.id) && !m.RemovedFeatureIds.Contains(f.id)).ToList();
            var sided = traits.Where(f => f.side != FeatureSide.None).ToList();
            var options = new List<System.Action>();
            if (sided.Count > 0)
                options.Add(() =>
                {
                    var f = sided[rng.Next(sided.Count)];
                    var flipped = f.side == FeatureSide.Left ? FeatureSide.Right : FeatureSide.Left;
                    m.SideOverrides[f.id] = flipped;
                    m.Notes.Add($"MIRRORED '{f.id}' ({f.side} -> {flipped})");
                    tellDescriptions.Add($"A mirrored detail: {Describe(f.id, flipped)} (the records say {f.side.ToString().ToLowerInvariant()}).");
                });
            if (traits.Count > 0)
                options.Add(() =>
                {
                    var f = traits[rng.Next(traits.Count)];
                    m.RemovedFeatureIds.Add(f.id);
                    m.Notes.Add($"MISSING '{f.id}'");
                    tellDescriptions.Add($"A missing detail: the records note \"{Describe(f.id, f.side)}\", but it was gone.");
                });
            if (!m.AddedFeatures.Exists(f => f.id == "cold_hands"))
                options.Add(() =>
                {
                    m.AddedFeatures.Add(new FeatureInstance("cold_hands", FeatureSide.None, null, false));
                    m.Notes.Add("COLD HANDS");
                    tellDescriptions.Add("Its hands were cold and damp, like meat from a cooler.");
                });
            if (!m.Notes.Contains("PHOTO FLARE"))
                options.Add(() =>
                {
                    m.Notes.Add("PHOTO FLARE");
                    tellDescriptions.Add("In photographs its eyes flared white and its face smeared, as if it had moved. It hadn't.");
                });
            if (options.Count > 0)
                options[rng.Next(options.Count)]();
        }

        private void ApplyBehaviourTell(RunModifications m, System.Random rng)
        {
            // Every behaviour tell has to be catchable: on Hard (no detail lists, no help) breathing is too subtle to
            // see, and "no reflection" only counts if the mimic actually spends time in front of Mae's mirror.
            var tells = new List<string> { "never_blinks", "head_snaps" };
            if (GameSettings.Difficulty != Difficulty.Hard) tells.Add("no_breathing");
            if (ReflectionCatchable(Mimic)) tells.Add("no_reflection");
            string tell = tells[rng.Next(tells.Count)];
            if (tell == "no_reflection")
            {
                MimicHasNoReflection = true;
                m.Notes.Add("NO REFLECTION");
                tellDescriptions.Add("It had no reflection in Mae's mirror.");
                return;
            }
            m.AddedFeatures.Add(new FeatureInstance(tell, FeatureSide.None, null, false));
            m.Notes.Add(tell.ToUpperInvariant());
            tellDescriptions.Add(tell switch
            {
                "never_blinks" => "It never blinked.",
                "no_breathing" => "Its chest never rose or fell. It didn't breathe.",
                _ => "Its head snapped round to follow you, further than a neck can turn.",
            });
        }

        /// <summary>The diner mirror is the only one residents pass. Mae stands in front of it all day, so for her it's a giveaway, not a clue.</summary>
        /// <summary>The mirror check needs them within six metres of Mae's mirror: only the counter and the stools are.</summary>
        public static bool ReflectionCatchable(Resident r)
            => r.DisplayName != CastNames.Mae && r.Profile.schedule != null && r.Profile.schedule.Any(s => s == "Diner:Counter" || s == "Diner:Stool");

        private void ApplySpeechTell(Resident mimic, RunModifications m, System.Random rng)
        {
            var others = residents.Where(r => r != mimic && !string.IsNullOrEmpty(r.Profile.seenAnythingStrange)).ToList();
            int speechTell = rng.Next(others.Count > 0 ? 3 : 2);
            switch (speechTell)
            {
                case 0:
                    m.GreetingOverride = "{daypart}, Detective " + detectiveName + ".";
                    m.AddedFeatures.Add(new FeatureInstance("knows_name", FeatureSide.None, null, false));
                    m.Notes.Add("KNOWS NAME");
                    tellDescriptions.Add($"It greeted you as \"Detective {detectiveName}\". You never told anyone your name.");
                    break;
                case 1:
                    m.TopicOverrides[ResidentProfile.TopicFoundersDay] = FirstPerson(WrongFoundersDay[rng.Next(WrongFoundersDay.Length)], mimic);
                    m.AddedFeatures.Add(new FeatureInstance("wrong_memory", FeatureSide.None, null, false));
                    m.Notes.Add("WRONG FOUNDERS' DAY MEMORY");
                    tellDescriptions.Add("It remembered Founders' Day wrong. Everyone else remembered the rain and Mae's cherry pie.");
                    break;
                default:
                    var source = others[rng.Next(others.Count)];
                    m.TopicOverrides[ResidentProfile.TopicStrange] = source.Profile.seenAnythingStrange;
                    m.AddedFeatures.Add(new FeatureInstance("echo_speech", FeatureSide.None, null, false));
                    m.Notes.Add($"ECHOES {source.DisplayName}");
                    Noise.EchoSource = source;
                    tellDescriptions.Add($"Asked about anything strange, it repeated {source.DisplayName}'s words exactly.");
                    break;
            }
        }

        private readonly HashSet<string> usedRedHerrings = new HashSet<string>();

        private void ApplyRedHerring(Resident resident, RunModifications m, System.Random rng)
        {
            var existing = new HashSet<string>(resident.Profile.features.Select(f => f.id).Concat(m.AddedFeatures.Select(f => f.id)));
            var candidates = FeatureCatalog.All.Where(d => d.Kind == FeatureKind.RedHerring && !existing.Contains(d.Id)).OrderBy(d => d.Id).ToList();
            if (candidates.Count == 0)
                return;
            // Deal from the deck before reshuffling: five fresh bandages in one town is a joke, not noise.
            var fresh = candidates.Where(d => !usedRedHerrings.Contains(d.Id)).ToList();
            if (fresh.Count > 0) candidates = fresh;
            var definition = candidates[rng.Next(candidates.Count)];
            var side = definition.Sided ? (rng.Next(2) == 0 ? FeatureSide.Left : FeatureSide.Right) : FeatureSide.None;
            RedHerringExplanations.TryGetValue(definition.Id, out var explanations);
            var pick = explanations != null ? explanations[rng.Next(explanations.Length)] : (null, null);
            string explanation = pick.why;
            m.AddedFeatures.Add(new FeatureInstance(definition.Id, side, explanation, inArchive: false));
            usedRedHerrings.Add(definition.Id);

            // Most of the time somebody else in town saw the ordinary reason for it.
            if (pick.seen != null && rng.NextDouble() < 0.7 && Noise != null
                && Noise.AddWitness(resident, pick.seen, residents.Where(w => w.LastName != resident.LastName)))
                m.Notes.Add("(vouched for)");
            m.Notes.Add($"red herring '{definition.Id}'" + (side != FeatureSide.None ? $" ({side})" : ""));
            redHerringDescriptions.Add($"{resident.DisplayName}: {resident.Named(definition.Describe(side))}" + (explanation != null ? $" (\"{explanation}\")" : ""));
        }

        private string Describe(string id, FeatureSide side)
        {
            var d = FeatureCatalog.Get(id);
            return d == null ? id : Mimic != null ? Mimic.Named(d.Describe(side)) : d.Describe(side);
        }

        // ------------------------------------------------------------ Daily drift

        /// <summary>Applies the overnight changes for every day up to <paramref name="day"/> (idempotent).</summary>
        public void ApplyDriftUpTo(int day)
        {
            if (!IsGenerated) return;
            bool changed = false;
            while (driftDay < day)
            {
                driftDay++;
                ApplyDrift(driftDay);
                changed = true;
            }
            if (!changed) return;
            foreach (var r in residents)
                r.Avatar.ApplyRunModifications(mods[r]);
            DriftApplied?.Invoke(day);
        }

        private void ApplyDrift(int day)
        {
            var rng = new System.Random(Seed * 31 + day * 7919);

            // Day 2: somebody has noticed the thing you'd have to stare at to see, and somebody's noticed an innocent too.
            if (day == 2 && Noise != null)
            {
                string mimicLine = mods[Mimic].AddedFeatures.Select(f => f.id).Concat(MimicHasNoReflection ? new[] { "no_reflection" } : new string[0])
                    .Select(t => BehaviourRumour(t, Mimic)).FirstOrDefault(l => l != null);
                // Somebody who gets about town says it: not the pastor, not the man who hasn't left his farm since spring.
                if (mimicLine != null) Noise.AddWitness(Mimic, mimicLine, townsfolkOnly: true);
                if (Noise.NoticerDecoy != null)
                    Noise.AddWitness(Noise.NoticerDecoy, "{first}'s been strange lately. Stares right through you, and I swear some days {he} doesn't blink. Nerves, I'd say. Everybody's got them.", townsfolkOnly: true);
            }

            // The mimic's copy slips a little more on the second and third nights, holds, then slips once more near the end.
            var m = mods[Mimic];
            if (day > 3 && day != GameSettings.DayCount - 1)
            {
                var someone = residents.Where(r => r != Mimic).OrderBy(_ => rng.Next()).First();
                ApplyRedHerring(someone, mods[someone], rng);
                return;
            }
            var traits = PhysicalTraits(Mimic).Where(f => !m.SideOverrides.ContainsKey(f.id) && !m.RemovedFeatureIds.Contains(f.id)).ToList();
            var sided = traits.Where(f => f.side != FeatureSide.None).ToList();
            int roll = rng.Next(3);
            if (roll == 0 && sided.Count > 0)
            {
                var f = sided[rng.Next(sided.Count)];
                var flipped = f.side == FeatureSide.Left ? FeatureSide.Right : FeatureSide.Left;
                m.SideOverrides[f.id] = flipped;
                m.Notes.Add($"day {day}: DRIFT mirrored '{f.id}'");
                tellDescriptions.Add($"Day {day}: overnight, a detail moved to the other side ({Describe(f.id, flipped)}).");
            }
            else if (roll == 1 && traits.Count > 1)
            {
                var f = traits[rng.Next(traits.Count)];
                m.RemovedFeatureIds.Add(f.id);
                m.Notes.Add($"day {day}: DRIFT lost '{f.id}'");
                tellDescriptions.Add($"Day {day}: overnight, a lifelong detail simply wasn't there anymore ({Describe(f.id, f.side)}).");
            }
            else
            {
                // A brand-new detail no record mentions: the copy is improvising.
                string[] newDetails = { "heterochromia", "mole", "eye_high", "freckles", "brow_scar" };
                var candidates = newDetails.Where(id => FeatureCatalog.Get(id) != null && !Mimic.Profile.features.Exists(f => f.id == id) && !m.AddedFeatures.Exists(f => f.id == id)).ToList();
                if (candidates.Count > 0)
                {
                    string id = candidates[rng.Next(candidates.Count)];
                    var def = FeatureCatalog.Get(id);
                    var side = def.Sided ? (rng.Next(2) == 0 ? FeatureSide.Left : FeatureSide.Right) : FeatureSide.None;
                    m.AddedFeatures.Add(new FeatureInstance(id, side, null, false));
                    m.Notes.Add($"day {day}: DRIFT new '{id}'");
                    tellDescriptions.Add($"Day {day}: overnight, a new detail appeared that no record mentions ({Describe(id, side)}).");
                }
            }

            // Innocent people change too, for ordinary reasons (noise): one a day until day 3, and preferably somebody
            // who doesn't have a fresh bandage already.
            if (day <= 3)
            {
                var pool = residents.Where(r => r != Mimic).OrderBy(_ => rng.Next()).ToList();
                var innocent = pool.FirstOrDefault(r => !mods[r].AddedFeatures.Any(f => FeatureCatalog.Get(f.id)?.Kind == FeatureKind.RedHerring)) ?? pool[0];
                ApplyRedHerring(innocent, mods[innocent], rng);
            }
        }

        private void LogSolution()
        {
            var sb = new StringBuilder();
            sb.Append($"[Semblance] Case seed {Seed} ({GameSettings.Difficulty}). MIMIC: {Mimic.DisplayName} -> {string.Join(", ", mods[Mimic].Notes)}. Real {Mimic.FirstName} hidden in: {HidingPlace}");
            foreach (var pair in mods)
                if (pair.Key != Mimic && pair.Value.Notes.Count > 0)
                    sb.Append($"\n   {pair.Key.DisplayName}: {string.Join(", ", pair.Value.Notes)}");
            Debug.Log(sb.ToString());
        }
    }
}
