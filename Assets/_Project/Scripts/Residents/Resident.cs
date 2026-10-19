using System.Collections.Generic;
using System.Linq;
using System.Text;
using Semblance.Dialogue;
using Semblance.Interaction;
using UnityEngine;

namespace Semblance.Residents
{
    /// <summary>
    /// A townsperson the detective can talk to and look over. Dialogue comes from the profile, adjusted
    /// by speech quirks and by any per-run overrides the case generator applied (mimic tells).
    /// </summary>
    [RequireComponent(typeof(ResidentAvatar))]
    public class Resident : Interactable
    {
        public ResidentAvatar Avatar { get; private set; }
        public ResidentProfile Profile => Avatar.Profile;
        public string DisplayName => Profile != null ? Profile.displayName : name;

        public override string Prompt => "Talk to " + DisplayName;

        private static readonly List<Resident> AllResidents = new List<Resident>();
        public static IReadOnlyList<Resident> All => AllResidents;

        private void Awake()
        {
            Avatar = GetComponent<ResidentAvatar>();
        }

        private void OnEnable()
        {
            if (!AllResidents.Contains(this))
                AllResidents.Add(this);
        }

        private void OnDisable() => AllResidents.Remove(this);

        /// <summary>Turns "Their left eye..." into "Mae's left eye..." for notes and records.</summary>
        public string Named(string line) => ThirdPerson(line, FirstName, Profile.possessive);

        public static string ThirdPerson(string text, string name, string possessive = "their")
        {
            if (text.StartsWith("Their ")) text = name + "'s " + text.Substring(6);
            else if (text.StartsWith("They're ")) text = name + " is " + text.Substring(8);
            else if (text.StartsWith("They ")) text = name + " " + Conjugate(text.Substring(5));
            string objective = possessive == "her" ? "her" : possessive == "his" ? "him" : "them";
            string subject = possessive == "her" ? "she" : possessive == "his" ? "he" : "they";
            string reflexive = possessive == "her" ? "herself" : possessive == "his" ? "himself" : "themselves";
            text = text.Replace(" their ", $" {possessive} ").Replace(" them.", $" {objective}.").Replace(" them ", $" {objective} ")
                .Replace(" them,", $" {objective},").Replace("themselves", reflexive);
            if (subject != "they")
                text = text.Replace(" they're ", $" {subject}'s ").Replace(" they are ", $" {subject} is ").Replace(" they were ", $" {subject} was ")
                    .Replace(" they have ", $" {subject} has ").Replace(" they ", $" {subject} ").Replace(". They ", $". {char.ToUpper(subject[0])}{subject.Substring(1)} ");
            return text;
        }

        /// <summary>"wear leather gloves" -> "wears leather gloves", "never blink" -> "never blinks", "won't meet" stays.</summary>
        private static string Conjugate(string rest)
        {
            string[] words = rest.Split(' ');
            int i = 0;
            if (words[0] == "only" && words.Length > 2 && words[1] == "ever") i = 2;
            else if (words[0] == "never" || words[0] == "always" || words[0] == "constantly") i = 1;
            if (i >= words.Length) return rest;
            // Keep trailing punctuation off the verb ("They stutter." -> "stutters.").
            string full = words[i];
            string w = full.TrimEnd('.', ',', ';', '!', '?');
            string tail = full.Substring(w.Length);
            if (w == "won't" || w == "can't" || w == "will" || w == "must" || w.EndsWith("ed")) return rest;
            words[i] = w;
            if (w == "don't") words[i] = "doesn't";
            else if (w == "have") words[i] = "has";
            else if (w == "are") words[i] = "is";
            else if (w.EndsWith("ch") || w.EndsWith("sh") || w.EndsWith("ss") || w.EndsWith("x")) words[i] = w + "es";
            else if (!w.EndsWith("s")) words[i] = w + "s";
            words[i] += tail;
            return string.Join(" ", words);
        }

        public string FirstName => DisplayName.StartsWith("Father ") ? "Father " + LastName : DisplayName.Split(' ')[0];
        public string LastName => DisplayName.Substring(DisplayName.LastIndexOf(' ') + 1);

        /// <summary>
        /// Finds a resident from how people refer to them: "Mae Whitlock", "Father Crane" (same first and last word),
        /// or just a unique surname. Returns null for people who aren't residents (e.g. "Ruth Calder").
        /// </summary>
        public static Resident FindByName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            foreach (var r in AllResidents)
                if (r.DisplayName == name)
                    return r;
            var words = name.Split(' ');
            foreach (var r in AllResidents)
            {
                var rw = r.DisplayName.Split(' ');
                if (rw[0] == words[0] && rw[rw.Length - 1] == words[words.Length - 1])
                    return r;
            }
            return null;
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (DialogueUI.Instance != null)
                DialogueUI.Instance.Open(this, interactor);
        }

        // ------------------------------------------------------------ Dialogue content

        /// <summary>What the last greeting claimed (a stranger who knows your name), for the notebook.</summary>
        public string GreetingFact { get; private set; }

        /// <summary>What they say when they open the door to you; used once, by the next conversation.</summary>
        public string DoorstepGreeting { get; set; }

        public string GetGreeting()
        {
            var mods = Avatar.Modifications;
            GreetingFact = null;
            if (!string.IsNullOrEmpty(DoorstepGreeting))
            {
                string doorstep = DoorstepGreeting;
                DoorstepGreeting = null;
                return Format(doorstep, "greeting");
            }
            // The slip comes once it's comfortable with you: never on a first meeting, then on the third conversation,
            // or from Day 3 once you've met. Early on it's a slip you could miss, not a confession on the doorstep.
            var flags = Save.SaveSystem.Flags;
            int talks = flags.Contains($"talked3|{DisplayName}") ? 3 : flags.Contains($"talked2|{DisplayName}") ? 2 : flags.Contains($"talked1|{DisplayName}") ? 1 : 0;
            flags.Add($"talked{Mathf.Min(3, talks + 1)}|{DisplayName}");
            int today = Core.GameClock.Instance != null ? Core.GameClock.Instance.Day : 1;
            bool slips = talks >= 2 || (talks >= 1 && today >= 3);
            if (mods != null && !string.IsNullOrEmpty(mods.GreetingOverride) && slips)
            {
                GreetingFact = Notes.NoteFacts.KnowsName;
                float h = Core.GameClock.Instance != null ? Core.GameClock.Instance.Hour : 19f;
                string daypart = h < 12f ? "Morning" : h < 17f ? "Afternoon" : "Evening";
                return Format(mods.GreetingOverride.Replace("{daypart}", daypart), "greeting");
            }
            if (IsWary && Random.value < 0.6f)
            {
                string[] wary =
                {
                    "You've been asking about me.",
                    "Still here, Detective? Most people would have gone home by now.",
                    "You look tired, Detective. You should get some sleep.",
                    "Asking questions again? Folks are starting to talk about you.",
                    "I've told you everything I know. Twice.",
                };
                return Format(wary[Random.Range(0, wary.Length)], "greeting");
            }
            var clock = Core.GameClock.Instance;
            var cam = Camera.main;
            if (clock != null && cam != null && clock.IsDark && !string.IsNullOrEmpty(Profile.home) && Profile.home.EndsWith(" Farm")
                && World.LocationMarker.Describe(cam.transform.position).Contains(Profile.home.Replace(" Farm", "") + " farmhouse"))
            {
                string[] intruded =
                {
                    $"What are you doing in my house? It's {Core.GameClock.FormatHour(clock.Hour)}.",
                    "You don't knock in the city, Detective? Out here people get shot for less.",
                    "Lord. You gave me a fright. Shut the door behind you; something might follow you in.",
                };
                return Format(intruded[Random.Range(0, intruded.Length)], "intruded");
            }
            // At the Sunday service or the town meeting, everyone's greeting is about that.
            var beats = Case.StoryBeats.Instance;
            if (beats != null && beats.Gathering && beats.OverrideSpot(this) != null && DisplayName != Case.CastNames.Crane && clock != null)
            {
                string[] hushed = clock.Hour < 14f
                    ? new[] { "Shh. He's started. Sit, Detective, or stand at the back.", "Didn't take you for a churchgoer.", "Everybody comes on Sunday. Everybody." }
                    : new[] { "Everyone's here, Detective. Everyone's watching you.", "Are you going to tell us who it is?", "Find a spot along the wall." };
                return Format(hushed[Random.Range(0, hushed.Length)], "greeting");
            }

            // Their own lines often assume evening, or their own front room; in daylight, skip the ones that don't fit.
            var greetings = Profile.greetings != null ? Profile.greetings.ToList() : new System.Collections.Generic.List<string>();
            if (clock != null && clock.Hour < 17f)
            {
                string[] eveningWords = { "Evening", "Evenin'", "dark", "close at nine", "asleep", "resting" };
                greetings.RemoveAll(g => eveningWords.Any(w => g.Contains(w)));
                if (greetings.Count == 0)
                    greetings.Add(clock.Hour < 12f ? "Morning, Detective." : "Afternoon, Detective.");
            }
            string line = greetings.Count > 0 ? greetings[Random.Range(0, greetings.Count)] : "...";
            return Format(line, "greeting");
        }

        /// <summary>Openly wary of the detective: the mimic once it's hostile, or a fed-up innocent from the second day.</summary>
        public bool IsWary
        {
            get
            {
                var director = Case.CaseDirector.Instance;
                if (director == null) return false;
                if (director.Mimic == this)
                    return Case.MimicAwareness.Instance != null && Case.MimicAwareness.Instance.Tier >= 2;
                return director.Noise != null && director.Noise.FedUp.Contains(this)
                    && Core.GameClock.Instance != null && Core.GameClock.Instance.Day >= 2;
            }
        }

        public List<DialogueTopic> GetTopics()
        {
            var topics = new List<DialogueTopic>();
            var mods = Avatar.Modifications;
            foreach (var topic in Profile.AllTopics())
            {
                if (string.IsNullOrEmpty(topic.response))
                    continue;
                string response = topic.response;
                if (mods != null && mods.TopicOverrides.TryGetValue(topic.title, out var overridden))
                    response = overridden;
                var t = new DialogueTopic(topic.title, Format(response, topic.title));
                // What it claims, from the words as written (before stutters and mutters get in the way).
                if (topic.title == ResidentProfile.TopicFoundersDay) t.fact = Notes.NoteFacts.Founders(response, DisplayName);
                else if (topic.title == ResidentProfile.TopicStrange) t.fact = Notes.NoteFacts.Strange(response);
                else if (topic.title.StartsWith("About ") && topic.title != ResidentProfile.TopicSelf && topic.title != ResidentProfile.TopicTown) t.fact = Notes.NoteFacts.Gossip(FindByName(topic.title.Substring(6)), response);
                else t.fact = Notes.NoteFacts.Claims(response);
                topics.Add(t);
            }
            if (mods != null)
                foreach (var extra in mods.ExtraGossip)
                    if (!topics.Exists(t => t.title == extra.title))
                        topics.Add(new DialogueTopic(extra.title, Format(extra.response, extra.title))
                        {
                            fact = Notes.NoteFacts.Gossip(FindByName(extra.title.Substring(6)), extra.response),
                            explains = extra.explains,
                        });
            var missingTopic = MissingTopic();
            if (missingTopic != null) topics.Insert(Mathf.Min(3, topics.Count), missingTopic);
            return topics;
        }

        /// <summary>
        /// Once someone has gone missing (Day 3), everyone can be asked about it. Family grieve. One neighbour saw
        /// somebody on the road that night (the mimic or an innocent, even odds), and the family of whoever was named
        /// can say where they were. The mimic is cold about it, and so are two innocents. These lines are never run
        /// through speech quirks: nobody laughs nervously about a death.
        /// </summary>
        private DialogueTopic MissingTopic()
        {
            var beats = Case.StoryBeats.Instance;
            var director = Case.CaseDirector.Instance;
            if (beats == null || director == null || beats.Missing == null || beats.Missing == this) return null;
            var m = beats.Missing;
            var mimic = director.Mimic;
            var noise = director.Noise;
            bool dead = beats.BodyFound;
            string title = dead ? $"{m.FirstName}'s death" : $"{m.FirstName} going missing";
            string he = m.Profile.possessive == "his" ? "he" : m.Profile.possessive == "her" ? "she" : "they";
            string him = m.Profile.possessive == "his" ? "him" : m.Profile.possessive == "her" ? "her" : "them";
            string He = char.ToUpper(he[0]) + he.Substring(1);

            // Family grieve, whoever (or whatever) they are.
            if (LastName == m.LastName)
            {
                string line;
                if (dead)
                    line = $"Don't. Please. They wouldn't let me see {him}. They said I shouldn't remember {him} like that.";
                else if (m.DisplayName == Case.CastNames.Hank)
                    line = "Hank drives home from Barlow at dawn. Twelve years, he's never once been late. His truck's in the yard. The engine was still warm. Hank isn't.";
                else
                    line = $"{m.FirstName} has never once slept anywhere but this house. Never. The porch light was on. {He} always turns it off.";
                return new DialogueTopic(title, line);
            }

            var sighting = beats.Sighting;
            if (sighting.witness == this && sighting.seen != null)
                return new DialogueTopic(title, $"I saw {sighting.seen.FirstName} on the road past the {m.LastName} place that night. Late. Walking slow, no light. I waved. {sighting.seen.FirstName} didn't wave back.");
            if (sighting.seen != null && sighting.seen != this && LastName == sighting.seen.LastName && DisplayName != m.DisplayName)
            {
                // Family can account for an innocent. For the mimic, they slept through it.
                bool innocent = sighting.seen != mimic;
                string seenHim = sighting.seen.Profile.possessive == "his" ? "him" : sighting.seen.Profile.possessive == "her" ? "her" : "them";
                return new DialogueTopic(title, innocent
                    ? $"Whoever says they saw {sighting.seen.FirstName} out that night is wrong. {sighting.seen.FirstName} was home. I sat up with {seenHim} till two, then I heard {seenHim} snoring through the wall till dawn."
                    : $"{sighting.seen.FirstName}? Home, I suppose. I sleep heavy. I couldn't swear to it.");
            }

            if (noise != null && noise.ColdFront.Contains(this))
            {
                string[] cold = dead
                    ? new[]
                    {
                        "Animals. That's what everyone's saying, isn't it? Then that's what it was.",
                        "Dead's dead, Detective. Nothing you do brings anybody back up.",
                        "I've buried better men than that. I'll bury worse. Was there something you wanted?",
                        "The ground out here takes what it wants. Always has. Write that in your little book.",
                        "Doc'll sign the paper and we'll sing the hymn. That's how it goes here.",
                    }
                    : new[]
                    {
                        "People wander off, Detective. Try the fields. Things always turn up in the fields.",
                        $"Folks go missing out here every few years. You don't go looking. You wait, and {he} turns up, or {he} doesn't.",
                        $"{m.FirstName} was always one for wandering. I wouldn't lose sleep over it. I'm not.",
                        "Grown folk can go where they like. It's not my business and it's not yours.",
                        $"{m.FirstName}'ll be back when {he}'s hungry. Or {he} won't. Either way, I've got chores.",
                    };
                // Each of the cold ones gets a different line, so nobody repeats anybody else word for word.
                var front = noise.ColdFront.OrderBy(x => Core.StableHash.Of(x.DisplayName, director.Seed)).ToList();
                int start = (int)((uint)director.Seed % (uint)cold.Length);
                return new DialogueTopic(title, cold[(start + front.IndexOf(this)) % cold.Length]);
            }

            string[] lines = dead
                ? new[]
                {
                    "Doc says animal. I've never seen an animal do that.",
                    "Two little holes. Like Greta's cows. Nobody wants to say that out loud.",
                    "I'm locking my door tonight. First time in forty years.",
                }
                : new[]
                {
                    $"{m.FirstName}? Lord. I saw {him} at church Sunday, right as rain.",
                    "Folks don't just leave Semblance. Nobody leaves.",
                    $"It'll be like the others. You won't find {him}. Not the way {he} was.",
                    "I didn't see anything. I was asleep. Everyone was asleep.",
                };
            return new DialogueTopic(title, Core.StableHash.Pick(lines, DisplayName));
        }

        private int handshakeDay = -1;

        /// <summary>A few innocents in every case have cold hands for ordinary reasons, so a cold handshake isn't a verdict.</summary>
        private bool ChillyHands
        {
            get
            {
                var director = Case.CaseDirector.Instance;
                return director != null && director.Noise != null && director.Mimic != this && director.Noise.ColdHands.Contains(this);
            }
        }

        /// <summary>A handshake. Mostly nothing, a flavour of who they are... unless their hand is cold.</summary>
        public string ShakeHands()
        {
            int day = Core.GameClock.Instance != null ? Core.GameClock.Instance.Day : 0;
            bool cold = Avatar.ActiveFeatures.Any(f => f.id == "cold_hands");
            bool hook = Avatar.ActiveFeatures.Any(f => f.id == "hook_hand" && f.side == FeatureSide.Right);
            string text;
            if (handshakeDay == day || (Notes.Notebook.Instance != null && Notes.Notebook.Instance.Has($"hands|{DisplayName}|{day}")))
                text = $"{FirstName} looks at your hand, then at you. \"We already did that, Detective.\"";
            else if (hook)
                text = $"{FirstName} offers you the left hand instead, the steel hook tucked away.";
            else if (cold || ChillyHands)
            {
                // Everyone with cold hands draws from the same descriptions; most innocents explain, and so, sometimes,
                // does the mimic. The excuse isn't the tell. The tell is what the rest of the town says about them.
                var director = Case.CaseDirector.Instance;
                int seed = director != null ? director.Seed : 0;
                string[] feel =
                {
                    "cold and slightly damp, like something brought up from a cellar",
                    "cold as well water, and the grip is very firm",
                    "chilled right through",
                    "cold and bony",
                };
                string[] excuses =
                {
                    "\"Sorry. Poor circulation, always have.\"",
                    "\"Been out at the pump all evening.\"",
                    "\"Doc says it's my blood. Thin as tea.\"",
                    "\"Cellar work. Don't mind me.\"",
                };
                bool excuse = cold ? director != null && director.Noise != null && director.Noise.MimicExcusesColdHands : Core.StableHash.Of(DisplayName, seed) % 10 < 7;
                text = $"{FirstName}'s hand is {Core.StableHash.Pick(feel, DisplayName, seed)}." + (excuse ? " " + Core.StableHash.Pick(excuses, DisplayName, seed + 1) : "");
            }
            else
            {
                string[] warm = { "rough and warm, a working hand", "dry, papery and brief", "damp with nerves", "warm, and the grip lingers a moment too long", "callused and strong" };
                text = $"{FirstName}'s hand is {Core.StableHash.Pick(warm, DisplayName)}.";
            }
            if (handshakeDay != day)
            {
                var e = Notes.Notebook.Instance?.Record(Notes.NoteKind.Observation, DisplayName, "Shook hands", text, null, $"hands|{DisplayName}|{day}");
                if (e != null && e.fact == null && text.Contains("cold"))
                    e.fact = text.Contains("\"") ? "cold-excused" : "cold";
            }
            handshakeDay = day;
            return text;
        }

        /// <summary>
        /// The detective puts a contradiction or a piece of evidence to them. Innocent people explain (often with
        /// a human reason); the mimic insists, deflects, and slips.
        /// </summary>
        public string Confront(string about)
        {
            var director = Case.CaseDirector.Instance;
            bool mimic = director != null && director.Mimic == this;
            bool defensive = director != null && director.Noise != null && director.Noise.Defensive == this;
            var rng = new System.Random(Core.StableHash.Of(DisplayName + about));
            string[] deflect =
            {
                "That's how it's always been, Detective. Ask anyone. Ask them all.",
                "You're tired. Everybody looks wrong to you when you're tired. Go and sleep.",
                "I don't know what you think you saw. I've always been like this. Always.",
                DisplayName == Case.CastNames.Doc ? "Then I wrote it down wrong. I'm old. Old men write things down wrong." : "Doc wrote it down wrong. He's old. Old men write things down wrong.",
            };
            if (mimic)
                Case.MimicAwareness.Instance?.Raise(6f, "confronted");
            if (mimic && (director.Noise == null || !director.Noise.MimicConfrontsHumanly))
            {
                if (TryGetComponent<ResidentBehaviour>(out var behaviour)) behaviour.Glitch(1.2f);
                return $"{FirstName} smiles a little too long before answering.\n\"{deflect[rng.Next(deflect.Length)]}\"";
            }
            if (defensive)
            {
                // An innocent with something to hide that isn't this: evasive, and it looks bad.
                string[] evasive =
                {
                    "I don't have to explain myself to you. Not to you, not to anyone.",
                    "You've no right asking me that. Ask the others what they were doing, why don't you.",
                    deflect[0],
                };
                return $"{FirstName} stares at you a long moment before answering.\n\"{evasive[rng.Next(evasive.Length)]}\"";
            }
            // Innocent (or the mimic, answering like one): explain the oddest thing about them, in their own words.
            var explained = Avatar.ActiveFeatures.Where(f => !string.IsNullOrEmpty(f.explanation)).ToList();
            if (explained.Count > 0)
            {
                var f = explained[rng.Next(explained.Count)];
                return $"{FirstName} sighs. \"{Notes.Notebook.StripTags(f.explanation)}\"\n<i>It sounds rehearsed, but it sounds true. Doc's records would say.</i>";
            }
            string[] generic =
            {
                "I don't know what to tell you. I've looked like this my whole life. Check Doc's records if you don't believe me.",
                "Somebody's been telling stories. This town loves a story.",
                "You think it's me? Look harder, Detective. You're wasting days.",
            };
            return $"\"{generic[rng.Next(generic.Length)]}\"";
        }

        /// <summary>What the detective notices when looking this person over closely.</summary>
        public string GetObservation()
        {
            var lines = GetObservationLines();
            if (lines.Count == 0)
                return "Nothing about them stands out.";
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append("• ").Append(line);
            }
            return sb.ToString();
        }

        /// <summary>What can be seen right now, as features (for checking notes against the records).</summary>
        public List<FeatureInstance> VisibleFeatures()
            => Avatar.ActiveFeatures.Where(f => FeatureCatalog.Get(f.id) is FeatureDefinition d && d.Listed).ToList();

        /// <summary>
        /// Visible details, one per line. When the difficulty hides detail lists, only a vague impression is
        /// given and the player has to spot things with their own eyes.
        /// </summary>
        public List<string> GetObservationLines()
        {
            var lines = new List<string>();
            foreach (var feature in Avatar.ActiveFeatures)
            {
                var definition = FeatureCatalog.Get(feature.id);
                if (definition == null || !definition.Listed)
                    continue;
                lines.Add(definition.Describe(feature.side));
            }
            if (!Core.GameSettings.ListObservedDetails && lines.Count > 0)
                return new List<string> { Core.GameInput.Format($"You study {FirstName} closely. Better trust your own eyes: hold {{focus}} on a face to look closer.") };
            return lines;
        }

        // ------------------------------------------------------------ Speech quirks

        public string Format(string text, string key)
        {
            var speech = Avatar.GetSpeechFlags();
            if (speech == SpeechFlags.None || string.IsNullOrEmpty(text))
                return text;

            // Deterministic per line so asking twice gives the same answer.
            var rng = new System.Random(Core.StableHash.Of(DisplayName + key));

            if ((speech & SpeechFlags.Stutter) != 0)
                text = Stutter(text, rng);
            if ((speech & SpeechFlags.NervousLaugh) != 0 && rng.NextDouble() < 0.35)
                text = InsertAfterFirstSentence(text, " <i>*laughs*</i>");
            if ((speech & SpeechFlags.TalksToSelf) != 0 && rng.NextDouble() < 0.4)
            {
                string[] mutters = { "(no, not now)", "(they're listening)", "(don't say it, don't)", "(count, count, count)" };
                text = InsertAfterFirstSentence(text, " <i>" + mutters[rng.Next(mutters.Length)] + "</i>");
            }
            if ((speech & SpeechFlags.Hums) != 0 && rng.NextDouble() < 0.3)
                text += " <i>*hums a few bars of a hymn*</i>";
            if ((speech & SpeechFlags.Whisper) != 0)
                text = "<i><size=85%>" + text.ToLowerInvariant() + "</size></i>";
            return text;
        }

        private static string Stutter(string text, System.Random rng)
        {
            var words = text.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length < 3 || !char.IsLetter(words[i][0]))
                    continue;
                if ((i == 0 && rng.NextDouble() < 0.7) || rng.NextDouble() < 0.05)
                    words[i] = words[i][0] + "-" + char.ToLowerInvariant(words[i][0]) + words[i].Substring(1);
            }
            return string.Join(" ", words);
        }

        private static string InsertAfterFirstSentence(string text, string insert)
        {
            int end = text.IndexOfAny(new[] { '.', '?', '!' });
            return end < 0 || end == text.Length - 1 ? text + insert : text.Insert(end + 1, insert);
        }
    }
}
