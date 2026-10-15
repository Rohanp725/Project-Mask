using System.Collections.Generic;
using Semblance.Residents;
using UnityEditor;
using UnityEngine;

namespace Semblance.EditorTools
{
    /// <summary>
    /// The townsfolk of Semblance: their profiles (looks, oddities, dialogue, gossip) and where they stand.
    ///
    /// Consistency rules the mystery relies on:
    ///  - Every sided physical oddity is mentioned in someone else's gossip, with the correct side,
    ///    so a mirrored or missing detail on the mimic can be caught.
    ///  - Everyone who attended remembers Founders' Day the same way: it rained, Mae won the pie
    ///    contest with cherry, and Father Crane preached on the wheat and the tares.
    ///
    /// Profiles are created once as assets in Assets/_Project/Residents and can be edited in the Inspector.
    /// Building the town never overwrites them; use "Semblance/Reset Resident Profiles" to restore defaults.
    /// </summary>
    public static class ResidentCast
    {
        private const string ProfileDir = "Assets/_Project/Residents";

        private const FeatureSide L = FeatureSide.Left, R = FeatureSide.Right, N = FeatureSide.None;

        [MenuItem("Semblance/Reset Resident Profiles")]
        private static void ResetProfiles()
        {
            if (EditorUtility.DisplayDialog("Reset resident profiles?",
                    "This overwrites every profile in " + ProfileDir + " with the default cast, losing any edits.", "Reset", "Cancel"))
                EnsureProfiles(overwrite: true);
        }

        // ------------------------------------------------------------ Placement

        /// <summary>Spawns every resident at their dusk spot (the game starts at dusk); schedules take over at runtime.</summary>
        public static void PlaceResidents(Shader shader)
        {
            var root = new GameObject("Residents").transform;
            var spots = Object.FindObjectsByType<ScheduleSpot>(FindObjectsInactive.Include);
            foreach (var guid in AssetDatabase.FindAssets("t:ResidentProfile", new[] { ProfileDir }))
            {
                var profile = AssetDatabase.LoadAssetAtPath<ResidentProfile>(AssetDatabase.GUIDToAssetPath(guid));
                string duskSpot = profile.schedule != null && profile.schedule.Length > 2 ? profile.schedule[2] : null;
                var spot = System.Array.Find(spots, s => s.Id == duskSpot);
                if (spot == null)
                {
                    Debug.LogWarning($"[Semblance] {profile.displayName} has no valid dusk spot ('{duskSpot}').");
                    continue;
                }

                var go = new GameObject(profile.displayName);
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(spot.transform.position, spot.transform.rotation);

                var capsule = go.AddComponent<CapsuleCollider>();
                capsule.radius = 0.3f;
                capsule.height = 1.75f;
                capsule.center = new Vector3(0, 0.875f, 0);

                var avatar = go.AddComponent<ResidentAvatar>();
                avatar.SetProfile(profile, shader);
                go.AddComponent<ResidentBehaviour>();
                go.AddComponent<Resident>();
                var agent = go.AddComponent<UnityEngine.AI.NavMeshAgent>();
                agent.radius = 0.25f;
                agent.height = 1.75f;
                agent.speed = 1.3f;
                agent.angularSpeed = 300f;
                agent.acceleration = 5f;
                agent.stoppingDistance = 0.15f;
                agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                go.AddComponent<ResidentMover>();

            }
        }

        // ------------------------------------------------------------ Profiles

        public static void EnsureProfiles(bool overwrite)
        {
            if (!AssetDatabase.IsValidFolder(ProfileDir))
                AssetDatabase.CreateFolder("Assets/_Project", "Residents");

            foreach (var profile in DefaultCast())
            {
                string path = $"{ProfileDir}/{profile.name}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<ResidentProfile>(path);
                if (existing == null)
                    AssetDatabase.CreateAsset(profile, path);
                else if (overwrite)
                {
                    EditorUtility.CopySerialized(profile, existing);
                    existing.name = profile.name;
                    EditorUtility.SetDirty(existing);
                    Object.DestroyImmediate(profile);
                }
                else
                    Object.DestroyImmediate(profile);
            }
            AssetDatabase.SaveAssets();
        }

        private static FeatureInstance F(string id, FeatureSide side = N, string why = null, bool inArchive = true)
            => new FeatureInstance(id, side, why, inArchive);

        private static ResidentProfile Make(string name, string occupation, int age, string home, string bio,
            ResidentAppearance look, FeatureInstance[] features, string[] greetings,
            string self, string town, string strange, string foundersDay, params (string about, string line)[] gossip)
        {
            var p = ScriptableObject.CreateInstance<ResidentProfile>();
            p.name = name;
            p.displayName = name;
            p.occupation = occupation;
            p.age = age;
            p.possessive = Possessive.TryGetValue(name, out var poss) ? poss : "their";
            p.home = home;
            p.bio = bio;
            p.appearance = look;
            p.features = new List<FeatureInstance>(features);
            p.greetings = greetings;
            p.aboutSelf = self;
            p.aboutTown = town;
            p.seenAnythingStrange = strange;
            p.foundersDay = foundersDay;
            foreach (var (about, line) in gossip)
                p.gossip.Add(new DialogueTopic("About " + about, line));
            if (ExtraGossip.TryGetValue(name, out var extra))
                p.gossip.Add(new DialogueTopic("About " + extra.about, extra.line));
            if (Schedules.TryGetValue(name, out var schedule))
                p.schedule = schedule;
            return p;
        }

        private static Color C(float r, float g, float b) => new Color(r, g, b);

        /// <summary>Morning, Afternoon, Dusk, Night. No two people share a spot in the same phase.</summary>
        private static readonly Dictionary<string, string[]> Schedules = new Dictionary<string, string[]>
        {
            { "Mae Whitlock",       new[] { "Diner:Counter", "Diner:Counter", "Diner:Counter", "House:Whitlock:Inside" } },
            { "Ada Whitlock",       new[] { "Diner:Stool", "Diner:Window", "Diner:Stool", "House:Whitlock:Kitchen" } },
            { "Lyle Pruitt",        new[] { "House:Pruitt:Porch", "Store:Aisle", "Diner:Booth", "House:Pruitt:Inside" } },
            { "Otis Pruitt",        new[] { "Store:Counter", "Store:Counter", "Store:Counter", "House:Pruitt:Kitchen" } },
            { "Doc Abner Pruitt",   new[] { "House:Pruitt:Inside", "Diner:Booth", "House:Pruitt:Porch", "House:Pruitt:Porch" } },
            { "Father Elias Crane", new[] { "Church:Altar", "Church:Pew2", "Church:Altar", "Church:Pew" } },
            { "Harlan Hollis",      new[] { "Field:Hollis", "Field:Hollis", "House:Hollis:Porch", "House:Hollis:Inside" } },
            { "Nell Hollis",        new[] { "House:Hollis:Kitchen", "Church:Pew", "House:Hollis:Inside", "House:Hollis:Kitchen" } },
            { "June Marsh",         new[] { "House:Marsh:Porch", "Square", "House:Marsh:Inside", "House:Marsh:Inside" } },
            { "Hank Marsh",         new[] { "House:Marsh:Inside", "Field:Marsh", "Gate:Marsh", "Highway:South" } },
            { "Amos Calder",        new[] { "House:Calder:Porch", "Churchyard", "House:Calder:Porch", "House:Calder:Inside" } },
            { "Wendell Dobbs",      new[] { "House:Dobbs:Porch", "Treeline:Church", "House:Dobbs:Porch", "Treeline:Church" } },
            { "Greta Kessler",      new[] { "Field:Kessler", "Barn:Kessler:Inside", "House:Kessler:Porch", "House:Kessler:Inside" } },
            { "Silas Thorne",       new[] { "Barn:Thorne:Inside", "Diner:Stool", "House:Thorne:Inside", "House:Thorne:Inside" } },
            { "Bev Thorne",         new[] { "House:Thorne:Kitchen", "House:Thorne:Porch", "House:Thorne:Porch", "House:Thorne:Kitchen" } },
            { "Irene Vance",        new[] { "House:Vance:Inside", "House:Vance:Inside", "House:Vance:Porch", "House:Vance:Porch" } },
        };

        /// <summary>Extra gossip so every left/right detail in town is described correctly by someone.</summary>
        private static readonly Dictionary<string, (string about, string line)> ExtraGossip = new Dictionary<string, (string, string)>
        {
            { "Mae Whitlock", ("Ada Whitlock", "My Ada's left eye is green, like her daddy's. That's how I know she's mine.") },
            { "Harlan Hollis", ("Nell Hollis", "Nell's left eye's always been small. Squints like she's aiming at you.") },
            { "Silas Thorne", ("Bev Thorne", "Bev's got a mole on her right cheek. I call it her beauty spot. She calls it a mole.") },
            { "Lyle Pruitt", ("Doc Abner Pruitt", "Grandpa Abner wears a hearing aid in his right ear. Turns it off when Dad talks.") },
        };

        private static readonly Dictionary<string, string> Possessive = new Dictionary<string, string>
        {
            { "Mae Whitlock", "her" }, { "Father Elias Crane", "his" }, { "Otis Pruitt", "his" }, { "Harlan Hollis", "his" },
            { "June Marsh", "her" }, { "Amos Calder", "his" }, { "Wendell Dobbs", "his" }, { "Greta Kessler", "her" },
            { "Silas Thorne", "his" }, { "Irene Vance", "her" }, { "Lyle Pruitt", "his" },
            { "Doc Abner Pruitt", "his" }, { "Hank Marsh", "his" }, { "Bev Thorne", "her" }, { "Nell Hollis", "her" }, { "Ada Whitlock", "her" },
        };

        private static IEnumerable<ResidentProfile> DefaultCast()
        {
            yield return Make("Mae Whitlock", "Owner, Mae's Diner", 58, "Whitlock Farm",
                "Has run the diner for thirty-one years. Widowed in '76; still wears the ring. Wins the Founders' Day pie contest every year.",
                new ResidentAppearance { height = 0.92f, build = 1.15f, skin = C(0.86f, 0.7f, 0.6f), hairColor = C(0.62f, 0.6f, 0.58f), hair = HairStyle.Bun,
                    shirt = C(0.62f, 0.4f, 0.45f), pants = C(0.3f, 0.26f, 0.3f), apron = true },
                new[]
                {
                    F("eye_large", L, "Born with it. Mama said it was so I'd see twice the trouble."),
                    F("ring", L, "Earl's been gone seven years. Feels rude to take it off."),
                    F("mouth_wide"),
                    F("burn_scars", R, "Fryer boiled over in '70. Took the skin right off."),
                },
                new[] { "Sit anywhere, hon. Coffee's burnt but it's hot.", "Evening, Detective. You look like you need pie." },
                "Thirty-one years behind this counter. My Earl built the place, God rest him. I still wear his ring. Feels rude not to.",
                "Semblance takes care of its own. We look a little rough around the edges, sure. Something in the water, old Doc Pruitt used to say. Nobody here minds.",
                "Strange? Hon, you'll have to narrow that down. ...Fine. Somebody's been eating the pies out of my case at night. Not cutting them. Eating them. With their hands.",
                "Founders' Day? It rained buckets, so we all crammed into the church hall. I won the pie contest. Again. Cherry.",
                ("Harlan Hollis", "Harlan lost the little finger off his right hand to a baler in '71. Didn't even stop working. Wrapped it in his hanky and finished the row."),
                ("Father Crane", "Father Crane hasn't eaten a bite in here in weeks. Says he's fasting. I say he looks like a coat hanger."));

            yield return Make("Father Elias Crane", "Pastor, First Church of Semblance", 64, "The rectory",
                "Has kept the church for thirty years. Fasting this month. Gave the Founders' Day sermon on the wheat and the tares.",
                new ResidentAppearance { height = 1.12f, build = 0.85f, skin = C(0.8f, 0.72f, 0.66f), hair = HairStyle.None, clericalCollar = true,
                    pants = C(0.07f, 0.07f, 0.08f), shoes = C(0.05f, 0.05f, 0.05f) },
                new[]
                {
                    F("gaunt", N, "I'm fasting. A man should know what he's made of."),
                    F("pale"),
                    F("neck_long"),
                    F("bald"),
                    F("deep_set_eyes"),
                    F("head_tilt", R, "Mother said I was born listening for God. Head cocked like a dog at the door."),
                },
                new[] { "Welcome to the house of the Lord, Detective. Mind the third pew; it bites.", "Ah. The detective. Come in out of the dark." },
                "Thirty years I've kept this church. I'm fasting this month. A man should know what he's made of, and what he isn't.",
                "We are all made in His image. Some of us are simply... rougher drafts.",
                "The bell rang by itself ten nights ago, at three in the morning. I climbed the tower. Nobody there. The rope was still swinging.",
                "It rained. Mae won with her cherry pie, as she does. I preached on the parable of the wheat and the tares. Rather fitting, in hindsight.",
                ("Mae Whitlock", "Mae's left eye is the larger one. She'll tell you it lets her see twice the trouble. She isn't wrong."),
                ("Ruth Calder", "Ruth Calder stopped coming to service in August. Amos says she's at her sister's in Tulsa. In forty years I never heard of a sister."));

            yield return Make("Otis Pruitt", "Clerk, Semblance General", 29, "Pruitt Farm",
                "Runs the store for his father, the retired town doctor. Lost his nose to frostbite as a boy. Born with an extra finger.",
                new ResidentAppearance { height = 0.98f, build = 0.95f, skin = C(0.83f, 0.68f, 0.56f), hairColor = C(0.35f, 0.25f, 0.15f), hair = HairStyle.Combover,
                    shirt = C(0.55f, 0.5f, 0.38f), pants = C(0.3f, 0.28f, 0.22f), glasses = true, hat = HatStyle.Cap, hatColor = C(0.45f, 0.15f, 0.12f) },
                new[]
                {
                    F("nose_missing", N, "Frostbite. Walked home from Barlow in the blizzard of '63. I was nine."),
                    F("finger_extra", L, "Born with it. Daddy says it's a lucky hand."),
                    F("ears_large"),
                    F("stutter"),
                },
                new[] { "H-help you find something?", "Oh. Evening. W-we close at nine." },
                "Run the store for my daddy. He was the town doctor. Retired now. M-mostly he sits by the window.",
                "Folks here don't like strangers looking too close. No offense, Detective.",
                "Somebody bought every mirror off my rack in one night. Money was just sitting on the counter in the morning. Exact change.",
                "Rained. Mae won the pie contest. Cherry. She always wins.",
                ("Greta Kessler", "Greta's cows keep dying. She buries them herself now. Won't let the vet near the bodies."),
                ("Wendell Dobbs", "Wendell Dobbs ain't been to town since spring. He leaves a grocery list in his mailbox and I leave the box at his gate."));

            yield return Make("Harlan Hollis", "Corn farmer", 61, "Hollis Farm",
                "Sixty years of corn. Lost a finger to a baler in '71. Doesn't go into his fields after dark anymore.",
                new ResidentAppearance { height = 1.0f, build = 1.15f, skin = C(0.72f, 0.55f, 0.42f), hairColor = C(0.5f, 0.48f, 0.45f), hair = HairStyle.Short,
                    shirt = C(0.6f, 0.55f, 0.48f), overalls = true, hat = HatStyle.Cap, hatColor = C(0.25f, 0.35f, 0.25f), beard = true },
                new[]
                {
                    F("finger_missing", R, "Baler. Summer of '71."),
                    F("hunchback"),
                    F("jaw_large"),
                    F("hands_large"),
                },
                new[] { "Evenin'.", "You're on my land, Detective." },
                "Corn. Sixty years of corn. It don't ask questions.",
                "Town's fine. People are fine. You should go home.",
                "Something walks my rows at night. The corn moves against the wind. I don't go out there after dark no more.",
                "Wet. Mae's cherry pie took it. Crane went on about weeds in the wheat.",
                ("Irene Vance", "Irene collects moths. Pins 'em to boards. House is full of 'em. Harmless."),
                ("Silas Thorne", "Silas has a scar through his left eyebrow. Bar fight in Amarillo. Proud of it, too."));

            yield return Make("June Marsh", "Mother of four", 35, "Marsh Farm",
                "Four children, husband works nights at the grain elevator in Barlow. Hardly sleeps.",
                new ResidentAppearance { height = 0.95f, build = 0.9f, skin = C(0.88f, 0.74f, 0.64f), hairColor = C(0.55f, 0.3f, 0.15f), hair = HairStyle.Long,
                    shirt = C(0.4f, 0.5f, 0.55f), pants = C(0.35f, 0.3f, 0.28f) },
                new[]
                {
                    F("eyes_wide_set"),
                    F("freckles"),
                    F("birthmark", R, "Mama called it a kiss from the devil. I call it Ohio."),
                    F("ring", L),
                    F("rapid_blink"),
                },
                new[] { "Oh! You startled me. The kids are asleep, please keep your voice down.", "Detective. Come in, quietly." },
                "Four kids, and a husband who works nights at the grain elevator in Barlow. I don't sleep much.",
                "It's a good place to raise children. Mostly.",
                "My youngest keeps drawing a fifth person in our family pictures. When I ask who it is, she says, 'the new one.'",
                "It poured! The kids loved it. Mae won the pie contest, cherry. She always does.",
                ("Otis Pruitt", "Otis has six fingers on his left hand. My kids think it's the best thing in the world."),
                ("Amos Calder", "Amos has started buying women's shoes at the store. Otis told me. Ruth's size."));

            yield return Make("Amos Calder", "Farmer", 66, "Calder Farm",
                "Married to Ruth for forty years. Says she's visiting her sister in Tulsa. Lost an ear to a dog as a boy.",
                new ResidentAppearance { height = 0.97f, build = 1f, skin = C(0.78f, 0.62f, 0.5f), hairColor = C(0.7f, 0.7f, 0.68f), hair = HairStyle.Short,
                    shirt = C(0.35f, 0.32f, 0.3f), pants = C(0.25f, 0.22f, 0.2f), hat = HatStyle.Fedora, hatColor = C(0.2f, 0.18f, 0.15f) },
                new[]
                {
                    F("ear_missing", L, "Dog got it when I was six. Don't ask me again."),
                    F("mouth_crooked", R),
                    F("tremor"),
                    F("avoids_gaze"),
                },
                new[] { "Ruth's not here.", "What do you want." },
                "Farmed this land forty years. Ruth's visiting her sister. In Tulsa.",
                "Leave the town be.",
                "Nothing strange here. Nothing.",
                "Rained. Ruth made her rhubarb pie. Lost to Mae's cherry. She always loses to Mae.",
                ("Ruth Calder", "Ruth's fine. She writes. ...She'll write."),
                ("Father Crane", "Crane's head's sat crooked since he was a boy. Leans right, like he's listening at a door."));

            yield return Make("Wendell Dobbs", "Recluse", 50, "Dobbs Farm",
                "Hasn't come to town since spring. Counts the congregation every Sunday from the treeline.",
                new ResidentAppearance { height = 1.04f, build = 0.9f, skin = C(0.75f, 0.68f, 0.6f), hairColor = C(0.3f, 0.28f, 0.25f), hair = HairStyle.Wild,
                    shirt = C(0.3f, 0.32f, 0.28f), pants = C(0.2f, 0.2f, 0.22f) },
                new[]
                {
                    F("pale"),
                    F("eye_cloudy", R, "Went milky last winter. Lets me see what the other one can't."),
                    F("head_twitch"),
                    F("patchy_hair"),
                    F("neck_short"),
                    F("stares"),
                },
                new[] { "You're standing on my shadow.", "Don't come any closer. I can see fine from here." },
                "I keep to myself. It's healthier.",
                "There's more of them than there used to be. More of us. I count.",
                "Count the people at church this Sunday. Then go read the sign on the highway. Then tell me I'm the mad one.",
                "Didn't go. I never go. Heard it rained.",
                ("June Marsh", "June's got a birthmark on her right cheek. Shaped like Ohio. She'll tell you so."),
                ("Mae Whitlock", "Mae puts something in the coffee. Makes you less curious. Don't drink it."));

            yield return Make("Greta Kessler", "Cattle farmer", 47, "Kessler Farm",
                "Fourteen head of cattle dead since June, cause undetermined. Buries them herself.",
                new ResidentAppearance { height = 1.08f, build = 1.2f, skin = C(0.8f, 0.64f, 0.52f), hairColor = C(0.75f, 0.65f, 0.4f), hair = HairStyle.Short,
                    shirt = C(0.45f, 0.25f, 0.2f), pants = C(0.25f, 0.25f, 0.28f) },
                new[]
                {
                    F("cheek_scar", L, "Heifer kicked me in '74. Don't ask me about it unless you want one to match."),
                    F("ear_high", R),
                    F("stout"),
                    F("dark_stains", N, "Calving. It's messy work."),
                },
                new[] { "Don't touch the fence. It's live.", "You lost, Detective?" },
                "Fourteen head dead since June. Vet says 'undetermined'. I say something's draining them.",
                "Semblance is a closed fist. You don't pry it open. You wait.",
                "The cattle are found standing. Dead, but standing. Like they're waiting for something.",
                "I left before the judging. Calving. Somebody told me Mae won again; cherry, I suppose. It was pouring when I drove home.",
                ("Harlan Hollis", "Harlan's got a jaw like a cinder block. I broke two knuckles on it in '68."),
                ("Otis Pruitt", "Otis lost his nose to frostbite the winter of '63. Walked home from Barlow in a blizzard. He was nine."));

            yield return Make("Silas Thorne", "Mechanic and sorghum farmer", 44, "Thorne Farm",
                "Fixes engines, grows sorghum. Wears his wedding ring on the right hand like all the Thornes.",
                new ResidentAppearance { height = 1.02f, build = 1.05f, skin = C(0.74f, 0.58f, 0.46f), hairColor = C(0.15f, 0.12f, 0.1f), hair = HairStyle.Short,
                    shirt = C(0.3f, 0.35f, 0.42f), pants = C(0.18f, 0.18f, 0.2f), mustache = true },
                new[]
                {
                    F("brow_scar", L, "Bar fight. Amarillo, '61. You should've seen the other fella."),
                    F("nose_crooked"),
                    F("teeth_showing"),
                    F("head_large"),
                    F("ring", R, "Thornes wear it on the right. Old-country habit."),
                },
                new[] { "Detective. Heard you were in town.", "Evening. Wipe your boots." },
                "Fix engines, grow sorghum, mind my own business.",
                "People here are what they are. Ain't no shame in it.",
                "Our family photo from '79. I'm blurry in it. I never move in photos. Never.",
                "Rained. Mae won. Cherry. My boy threw up on the hayride.",
                ("Greta Kessler", "Greta's got a scar on her left cheek from a heifer. Don't ask her about it."),
                ("Wendell Dobbs", "Wendell's right eye went milky last winter. Says it lets him see what the other one can't."));

            yield return Make("Irene Vance", "Moth collector", 72, "Vance Farm",
                "Collects moths: 2,406 specimens. Had polio as a girl, which left one arm longer than the other.",
                new ResidentAppearance { height = 0.9f, build = 0.85f, skin = C(0.86f, 0.78f, 0.72f), hairColor = C(0.88f, 0.88f, 0.85f), hair = HairStyle.Long,
                    shirt = C(0.42f, 0.38f, 0.5f), pants = C(0.3f, 0.28f, 0.32f), glasses = true },
                new[]
                {
                    F("head_small"),
                    F("eye_high", R),
                    F("unibrow"),
                    F("arm_long", L, "Polio, when I was a girl. The left one kept growing to make up for it."),
                    F("talks_to_self"),
                    F("sway"),
                },
                new[] { "Shh. They're resting.", "Oh, a visitor. Come into the light. Everything does." },
                "I collect moths. Two thousand, four hundred and six. They come to the light. Everything does, eventually.",
                "Semblance was here before the highway. Before the maps. It will be here after.",
                "The moths stopped coming to my porch light last week. Something else is drawing them.",
                "Rain. Lovely rain. The moths hate it. Mae's cherry pie, of course.",
                ("Silas Thorne", "Silas wears his wedding ring on his right hand. The Thornes always have. Odd family."),
                ("June Marsh", "June's eyes are set so wide. Like a deer's. A prey animal."));

            yield return Make("Lyle Pruitt", "High school senior", 17, "Pruitt Farm",
                "Otis's nephew. Lanky, bored, planning to leave for Chicago. Hangs around the diner at night.",
                new ResidentAppearance { height = 1.07f, build = 0.82f, skin = C(0.84f, 0.7f, 0.58f), hairColor = C(0.4f, 0.28f, 0.16f), hair = HairStyle.Wild,
                    shirt = C(0.6f, 0.2f, 0.18f), pants = C(0.2f, 0.25f, 0.4f) },
                new[]
                {
                    F("lazy_eye", L),
                    F("freckles"),
                    F("head_flat"),
                    F("neck_long"),
                    F("fresh_scratches", R, "Barn cat. Mean one.", inArchive: false),
                },
                new[] { "You're the cop? Cool. You gonna arrest somebody?", "Hey. Detective. Got a cigarette?" },
                "Finish school next year, then I'm gone. Chicago. Anywhere.",
                "Everybody here's weird-looking and nobody talks about it. It's like a rule.",
                "I saw Mrs. Calder at the diner window last week. Like two in the morning. Just standing outside, looking in. Then she wasn't.",
                "I think it rained? I was out back of the church with Ada's cousin most of it, okay? Aunt Mae won the pie thing. She's not my real aunt.",
                ("Amos Calder", "Mr. Calder's missing his left ear. Says a dog got it. My dad says it was Mrs. Calder."),
                ("Irene Vance", "Mrs. Vance's left arm is too long. From polio. She can reach the top shelf without a stool."));

            yield return Make("Doc Abner Pruitt", "Retired town doctor", 78, "Pruitt Farm",
                "Delivered half the town and stitched up the other half. Wrote the physician's notes in the parish records for forty years.",
                new ResidentAppearance { height = 0.95f, build = 0.88f, skin = C(0.84f, 0.74f, 0.66f), hair = HairStyle.None,
                    shirt = C(0.45f, 0.38f, 0.3f), pants = C(0.3f, 0.28f, 0.26f), glasses = true },
                new[]
                {
                    F("glass_eye", L, "Korea. Chosin Reservoir. The cold took it, not the Chinese."),
                    F("hearing_aid", R),
                    F("wart"),
                    F("gaunt"),
                },
                new[] { "Eh? Speak up. Detective, is it?", "Sit down before you fall down." },
                "Forty years I was the only doctor for thirty miles. Delivered half this town and sewed up the other half. Every scar in Semblance, I wrote down. Set bones in every root cellar from here to the county line; every farmhouse out here has one.",
                "People ask why so many of us look the way we do. Inbreeding, they think. It's not. It's the land. The land here keeps what it touches, and changes it a little.",
                "Somebody's been in my old surgery files. Put them back in the wrong order. Nobody in this family puts anything back right except me.",
                "Rained, I'm fairly sure. Or was that '82? They all run together at my age. Mae's cherry pie won, that I'd swear to. I slept through the sermon.",
                ("Mae Whitlock", "Mae wears Earl's ring on her left hand still. I took it off his finger myself, after. She asked for it back the same day."),
                ("Lyle Pruitt", "My grandson's left eye wanders. Born that way. I told his father not to fuss; it'll make him interesting."));

            yield return Make("Hank Marsh", "Night shift, grain elevator in Barlow", 38, "Marsh Farm",
                "Works nights in Barlow and sleeps through the days. Navy, 1963 to 1967.",
                new ResidentAppearance { height = 1.03f, build = 1.1f, skin = C(0.8f, 0.63f, 0.5f), hairColor = C(0.3f, 0.22f, 0.15f), hair = HairStyle.Short,
                    shirt = C(0.35f, 0.38f, 0.32f), pants = C(0.22f, 0.28f, 0.4f), hat = HatStyle.Cap, hatColor = C(0.6f, 0.5f, 0.2f) },
                new[]
                {
                    F("tattoo_hand", R, "Navy. Everybody got one. Mine's an anchor because I couldn't afford the girl."),
                    F("cigarette"),
                    F("gap_teeth"),
                    F("insomnia", N, "I sleep days. You try it.", inArchive: false),
                },
                new[] { "Make it quick. I've got a shift.", "You're the one June told me about." },
                "Nights at the elevator in Barlow, days asleep, Sundays at church so June doesn't skin me. That's my whole life, Detective.",
                "I drive out of this town every night, and every night it's harder to come back. Like the road's longer on the way in.",
                "Back in June, coming home at dawn, I passed a fella walking the highway into town carrying a sample case. No car, no coat, nobody walks to Semblance. When I looked in the mirror there was nobody on the road.",
                "Slept through most of it. Woke up to rain on the church roof and Mae's cherry pie. That's all I need from a holiday.",
                ("June Marsh", "June wears her ring on her left hand like a normal person. Not like the Thornes."),
                ("Amos Calder", "Amos's mouth droops on the right. Stroke, maybe. He won't let Doc look at it."));

            yield return Make("Bev Thorne", "Farmer's wife", 41, "Thorne Farm",
                "Married Silas at nineteen. Lost her left hand to a thresher in '77 and polishes her hook every morning.",
                new ResidentAppearance { height = 0.97f, build = 1f, skin = C(0.82f, 0.66f, 0.55f), hairColor = C(0.45f, 0.32f, 0.2f), hair = HairStyle.Bun,
                    shirt = C(0.55f, 0.45f, 0.5f), pants = C(0.3f, 0.28f, 0.3f) },
                new[]
                {
                    F("hook_hand", L, "Thresher, harvest of '77. Hook's better than the hand ever was. Doesn't feel the cold."),
                    F("gold_tooth"),
                    F("mole", R),
                },
                new[] { "Mind the dog. We don't have a dog. Mind it anyway.", "Detective. Silas said you'd come round." },
                "Married Silas when I was nineteen. Raised two boys and lost a hand. Got a hook for it. I don't miss the hand.",
                "Semblance doesn't like change. We're all a bit broken, and we're used to the shape of each other's broken bits.",
                "Something's been taking eggs out from under the hens without waking them. Shells left whole, a neat hole in one end. Sucked dry.",
                "Rained. Mae won with her cherry, Greta came second with her apple, and the Father talked about weeds. Same as every year.",
                ("Irene Vance", "Irene's right eye sits up high. Gives her a look like she's always asking a question."),
                ("Greta Kessler", "Greta's right ear sits up high. Hats never sit straight on her."));

            yield return Make("Nell Hollis", "Farmer's wife", 59, "Hollis Farm",
                "Married to Harlan for forty years. Hums hymns under her breath. Never takes her gloves off, which she blames on eczema.",
                new ResidentAppearance { height = 0.93f, build = 1.05f, skin = C(0.78f, 0.62f, 0.5f), hairColor = C(0.6f, 0.58f, 0.55f), hair = HairStyle.Bun,
                    shirt = C(0.5f, 0.42f, 0.3f), pants = C(0.35f, 0.3f, 0.25f), hat = HatStyle.Bonnet, hatColor = C(0.7f, 0.68f, 0.6f) },
                new[]
                {
                    F("eye_small", L, "Born squinting. Mama said I was taking aim at the Lord."),
                    F("cleft_chin"),
                    F("hums"),
                    F("gloves", N, "Eczema. The lye soap does it. Don't ask to see.", inArchive: false),
                },
                new[] { "Come in out of the dark, Detective, before it notices you.", "Harlan's in the field. He's always in the field." },
                "Forty years married to Harlan. I keep the house, I keep the hymns, and I keep my gloves on. Eczema. Don't ask to see.",
                "Folks like to say Semblance is cursed. It isn't. It's chosen. There's a difference, and the difference is you don't get a say.",
                "The corn's growing in rows that were never planted. New rows, straight as a ruler, right up to our back door.",
                "It rained and we sang in the church hall. Mae's cherry pie won. Father Crane preached on the wheat and the tares, and I cried. I don't know why.",
                ("Mae Whitlock", "Mae's burns cover the right side of her face. The fryer, in '70. She went back to work the next week."),
                ("Hank Marsh", "Hank's got an anchor tattooed on the back of his right hand. Navy. June hates it."));

            yield return Make("Ada Whitlock", "Waitress, Mae's Diner", 30, "Whitlock Farm",
                "Mae's daughter. Came home from Tulsa when her father died and never left again. Cracks her knuckles when she's nervous, which is always.",
                new ResidentAppearance { height = 0.98f, build = 0.92f, skin = C(0.86f, 0.72f, 0.62f), hairColor = C(0.5f, 0.3f, 0.18f), hair = HairStyle.Long,
                    shirt = C(0.62f, 0.4f, 0.45f), pants = C(0.3f, 0.26f, 0.3f), apron = true },
                new[]
                {
                    F("heterochromia", L, "Daddy's eye. Mama says it's how she knows I'm hers."),
                    F("freckles"),
                    F("chin_long"),
                    F("knuckle_cracking"),
                },
                new[] { "Coffee? It's on the house if you don't tell Mama.", "You're the detective. Lyle won't shut up about you." },
                "I waitress for Mama. I was in Tulsa four years, then Daddy died and I came home, and I don't really remember deciding to stay.",
                "Everybody here's lived through something that left a mark. We don't stare at each other's marks. It's polite.",
                "Somebody's been sitting in the back booth after close. The seat's still warm in the morning, and there's a full cup of coffee gone stone cold. Nobody drank it.",
                "Rained. Mama won, cherry. I helped with the crust and she won't admit it.",
                ("Doc Abner Pruitt", "Doc's left eye is glass. He takes it out to scare the kids. It worked on me till I was twenty."),
                ("Bev Thorne", "Bev lost her left hand to a thresher. Wears a hook. She can open a bottle with it faster than Silas can with an opener."));
        }
    }
}
