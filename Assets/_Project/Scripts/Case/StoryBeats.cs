using System.Collections.Generic;
using System.Linq;
using Semblance.Core;
using Semblance.Investigation;
using Semblance.Notes;
using Semblance.Residents;
using Semblance.UI;
using UnityEngine;

namespace Semblance.Case
{
    /// <summary>
    /// Things that happen on particular days, so the week has a shape:
    ///   Day 2 (Sunday) 9:00-12:00  Sunday service. The whole town in one room.
    ///   Day 3 dawn                 Someone innocent goes missing; one neighbour saw somebody on the road.
    ///   Day 4 dawn                 They're found dead in a cornfield, drained.
    ///   Day 4 18:00-21:00          Town meeting in the church: name someone or leave.
    /// Time runs slowly while the detective is inside the church during a gathering, so it can be watched.
    /// </summary>
    public class StoryBeats : MonoBehaviour
    {
        public static StoryBeats Instance { get; private set; }

        public Resident Missing { get; private set; }

        private static int LastDay => GameSettings.DayCount; // same as the clock's last day in play; also right in the case sweep
        /// <summary>Someone goes missing on the third-to-last morning, and is found on the second-to-last.</summary>
        public static int MissingDay => Mathf.Max(2, LastDay - 2);
        public static int BodyDay => Mathf.Max(3, LastDay - 1);
        /// <summary>Who says they saw somebody on the road the night of the disappearance, and who they saw.</summary>
        public (Resident witness, Resident seen) Sighting { get; private set; }
        public bool BodyFound => body != null;
        public bool Gathering { get; private set; }
        private string gatheringName;

        private readonly Dictionary<Resident, string> seats = new Dictionary<Resident, string>();
        private float nextCheck;
        private bool announcedService, announcedMeeting, sermonShown;
        private GameObject body;

        private void Awake() => Instance = this;

        private void Start()
        {
            var clock = GameClock.Instance;
            if (clock != null)
                clock.DayStarted += OnDayStarted;
                GameFlow.Woke += OnWoke;
            var director = CaseDirector.Instance;
            if (director != null)
            {
                if (director.IsGenerated) OnDayStarted(clock != null ? clock.Day : 1);
                else director.CaseGenerated += () => OnDayStarted(GameClock.Instance != null ? GameClock.Instance.Day : 1);
            }
        }

        /// <summary>Where a resident should be right now because of an event, or null to follow their schedule.</summary>
        public string OverrideSpot(Resident r)
        {
            if (!Gathering) return null;
            if (r.DisplayName == CastNames.Crane) return "Church:Altar";
            if (r.DisplayName == CastNames.Wendell) return "Treeline:Church"; // he watches from the trees
            return seats.TryGetValue(r, out var seat) ? seat : null;
        }

        /// <summary>
        /// Every family has had its own pew for forty years (seat numbers run back row first, six to a row, from the
        /// left). A few people sit apart at the back by the door: innocents with reasons somebody in town knows, and,
        /// in some cases, the thing wearing one of them, which doesn't know where it's supposed to sit.
        /// </summary>
        private static readonly Dictionary<string, int[]> FamilyPews = new Dictionary<string, int[]>
        {
            { "Whitlock", new[] { 18, 19 } }, { "Pruitt", new[] { 21, 22, 23 } }, { "Hollis", new[] { 12, 13 } },
            { "Thorne", new[] { 16, 17 } }, { "Marsh", new[] { 7, 8 } }, { "Calder", new[] { 10 } },
            { "Kessler", new[] { 15 } }, { "Vance", new[] { 20 } },
        };

        private void AssignPews(CaseDirector director)
        {
            var used = new HashSet<int>();
            int[] backRow = { 5, 0, 4, 1 };
            int back = 0;
            var apart = director.Noise != null ? director.Noise.BackPew.Keys.OrderBy(r => StableHash.Of(r.DisplayName, director.Seed)).ToList() : new List<Resident>();
            foreach (var r in apart)
            {
                if (!r.isActiveAndEnabled || back >= backRow.Length) continue;
                seats[r] = "Church:Seat" + backRow[back];
                used.Add(backRow[back++]);
            }
            foreach (var r in director.Residents)
            {
                if (!r.isActiveAndEnabled || seats.ContainsKey(r) || r.DisplayName == CastNames.Crane || r.DisplayName == CastNames.Wendell) continue;
                if (FamilyPews.TryGetValue(r.LastName, out var pew))
                    foreach (int seat in pew)
                        if (used.Add(seat))
                        {
                            seats[r] = "Church:Seat" + seat;
                            break;
                        }
            }
            // Anyone left over (a family bigger than its pew) squeezes in wherever there's room, never the back row.
            int spare = 6;
            foreach (var r in director.Residents)
            {
                if (seats.ContainsKey(r) || !r.isActiveAndEnabled || r.DisplayName == CastNames.Crane || r.DisplayName == CastNames.Wendell) continue;
                while (used.Contains(spare) && spare < 23) spare++;
                used.Add(spare);
                seats[r] = "Church:Seat" + spare;
            }
        }

        private bool threeAm;
        private int duskSavedDay = -1;
        private static bool camOk() => Camera.main != null && Camera.main.transform.position.y > -10f;

        private System.Collections.IEnumerator ThreeInTheMorning()
        {
            for (int i = 0; i < 3; i++)
            {
                Audio.GameAudio.Play("bell_toll", 0.6f);
                yield return new WaitForSeconds(2.2f);
            }
            PlayerHUD.Instance?.ShowMessage("The church bell. Three in the morning. Up and down the road, doors open, and lanterns come out of the dark one at a time, all walking toward the church.", 7f);
            StartCoroutine(LanternWalk());
            yield return new WaitForSeconds(8f);
            PlayerHUD.Instance?.ShowMessage("From the treeline by the church, a man's voice, counting under his breath. <i>\"...two hundred and ten. Two hundred and eleven. Two hundred and twelve.\"</i> A long pause. <i>\"Then who painted the sign?\"</i>", 8f);
            Notebook.Instance?.Record(NoteKind.Event, Notebook.CaseSubject, "The bell at three",
                "At three in the morning someone rang the church bell and the whole town came out with lanterns. Somebody in the treeline counted them. Two hundred and twelve. All of them came.", null, "event|threeam");
        }

        /// <summary>
        /// The town answering the bell: shapes with lanterns coming up both sides of the highway toward the church,
        /// for as long as the beat lasts. They walk; they don't arrive. Nobody should get close enough to count them.
        /// </summary>
        private System.Collections.IEnumerator LanternWalk()
        {
            var director = CaseDirector.Instance;
            var cam = Camera.main;
            if (director == null || cam == null) yield break;
            var churchZ = 28f;
            var walkers = new List<(Transform t, Light l)>();
            var faces = director.Residents.Where(r => r != null).OrderBy(_ => Random.value).Take(7).ToList();
            float baseZ = Mathf.Clamp(cam.transform.position.z, -200f, 0f);
            for (int i = 0; i < faces.Count; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * (RoadSide + Random.Range(0f, 1.5f));
                float z = baseZ - 70f + i * 18f + Random.Range(-6f, 6f);
                if (z > churchZ - 20f) z = churchZ - 20f - i * 4f;
                var shade = Events.Apparition.Create(faces[i], Events.ApparitionLook.Shade, new Vector3(x, 0.05f, z), Quaternion.LookRotation(Vector3.forward));
                var light = new GameObject("Lantern").AddComponent<Light>();
                light.transform.SetParent(shade.transform, false);
                light.transform.localPosition = new Vector3(0.35f, 1.0f, 0.2f);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.72f, 0.4f);
                light.range = 7f;
                light.intensity = 1.6f;
                light.shadows = LightShadows.None;
                walkers.Add((shade.transform, light));
                Audio.GameAudio.PlayAt("door_close", new Vector3(x * 3f, 1f, z), 0.4f, Random.Range(0.8f, 1f), 60f);
                yield return new WaitForSeconds(Random.Range(0.4f, 1.1f));
            }
            float until = Time.time + 42f;
            while (Time.time < until)
            {
                foreach (var (t, l) in walkers)
                {
                    if (t == null) continue;
                    t.position += Vector3.forward * 1.3f * Time.deltaTime;
                    l.intensity = 1.6f + Mathf.Sin(Time.time * 7f + t.position.x) * 0.25f;
                    if (t.position.z > churchZ - 8f) t.gameObject.SetActive(false);
                }
                yield return null;
            }
            foreach (var (t, l) in walkers) if (t != null) Destroy(t.gameObject);
        }

        private const float RoadSide = 4.6f;

        private System.Random Rng(int salt) => new System.Random((CaseDirector.Instance != null ? CaseDirector.Instance.Seed : 1) * 13 + salt);

        /// <summary>The morning after: what the day asks of you, on screen, not only in the notebook.</summary>
        private void OnWoke(int day)
        {
            if (Missing == null) return;
            string where = Missing.Profile != null && !string.IsNullOrEmpty(Missing.Profile.home) ? Missing.Profile.home : "the farm";
            if (day == MissingDay)
            {
                PlayerHUD.Instance?.SetHint($"<b>{Missing.DisplayName} didn't come home last night.</b>\n<size=75%>The family will be at {where}. Somebody on the road may have seen something. Ask around.</size>", 0f);
                HoldTheDoorOpen();
            }
            else if (day == BodyDay)
            {
                string field = body != null ? World.LocationMarker.Describe(body.transform.position) : "in a field";
                PlayerHUD.Instance?.SetHint($"<b>They found {Missing.FirstName} this morning {field}.</b>\n<size=75%>The padlocks on the cellar hatches are lying open. Bring a flashlight.</size>", 0f);
            }
        }

        /// <summary>"The door was found standing open, the porch light still on." So it is.</summary>
        private void HoldTheDoorOpen()
        {
            foreach (var k in FindObjectsByType<World.KnockableDoor>(FindObjectsInactive.Exclude))
            {
                if (k.Family != Missing.LastName) continue;
                k.GetComponent<World.Door>()?.ForceOpen(true);
                var house = k.transform.parent != null ? k.transform.parent.parent : null;
                if (house == null) continue;
                foreach (var lamp in house.GetComponentsInChildren<Rendering.LampSwitch>(true))
                    if (lamp.name == "Porch Light") lamp.Hold = true;
            }
        }

        private void OnDayStarted(int day)
        {
            var director = CaseDirector.Instance;
            if (director == null || !director.IsGenerated) return;

            // Day 3: someone is taken. Never the mimic or the people who run the town's places (Mae, Father Crane,
            // Otis), and never anyone a clue depends on: the person the mimic echoes, or a witness to someone's alibi.
            if (day >= MissingDay && Missing == null)
            {
                var rng = Rng(3);
                var noise = director.Noise;
                bool Runs(Resident r) => r.DisplayName == CastNames.Mae || r.DisplayName == CastNames.Crane || r.DisplayName == CastNames.Otis;
                // Nor the only person who describes one of the mimic's recorded details: that check must stay possible.
                var soleDescribers = new HashSet<Resident>();
                foreach (var f in director.Mimic.Profile.features.Where(f => f.side != FeatureSide.None && f.inArchive))
                {
                    var describers = director.Residents.Where(w => w != director.Mimic && w.Profile.gossip.Any(g => g.title == "About " + director.Mimic.DisplayName
                        && Notes.NoteFacts.Gossip(director.Mimic, g.response) == $"say|{f.id}|{(int)f.side}")).ToList();
                    if (describers.Count == 1) soleDescribers.Add(describers[0]);
                }
                var candidates = director.Residents.Where(r => r != director.Mimic && !Runs(r) && !soleDescribers.Contains(r)
                    && (noise == null || (r != noise.EchoSource && !noise.Witnesses.Contains(r) && r != noise.FoundersLiar))).ToList();
                if (candidates.Count == 0)
                    candidates = director.Residents.Where(r => r != director.Mimic && !Runs(r) && (noise == null || r != noise.EchoSource)).ToList();
                if (candidates.Count > 0)
                {
                    Missing = candidates[rng.Next(candidates.Count)];
                    Sighting = PickSighting(director, Missing, Rng(31));
                    Missing.gameObject.SetActive(false);
                    if (day == MissingDay)
                    {
                        Notebook.Instance?.Record(NoteKind.Event, Missing.DisplayName, "Missing",
                            $"{Missing.DisplayName} didn't come home last night. The door was found standing open, the porch light still on.", null, "missing|" + Missing.DisplayName);
                        PlayerHUD.Instance?.Toast($"<b>{Missing.DisplayName} is missing.</b>");
                        Audio.GameAudio.Play("sting_soft", 0.4f);
                    }
                }
            }

            // Day 4: they're found.
            if (day >= BodyDay && Missing != null && body == null)
            {
                PlaceBody(day == BodyDay);
                // Whatever locked the cellars has been back; the padlocks lie in the dirt.
                Save.SaveSystem.Flags.Add("padlocks:off");
                foreach (var hatch in FindObjectsByType<CellarHatch>(FindObjectsInactive.Include)) hatch.SetPadlock(false);
            }
        }

        /// <summary>
        /// One neighbour saw somebody on the road past the victim's place that night: the mimic or an innocent,
        /// even odds. It's a lead, not a verdict; the named person's family can say where they really were.
        /// </summary>
        private static (Resident, Resident) PickSighting(CaseDirector director, Resident missing, System.Random rng)
        {
            var mimic = director.Mimic;
            var people = director.Residents.Where(r => r != missing && r.LastName != missing.LastName).OrderBy(r => r.DisplayName).ToList();
            // Only people with family at home can be named: someone has to be able to say where they were.
            bool HasFamily(Resident r) => director.Residents.Count(x => x != r && x != missing && x.LastName == r.LastName) > 0;
            Resident seen = null;
            if (mimic != null && people.Contains(mimic) && HasFamily(mimic) && rng.NextDouble() < 0.5)
                seen = mimic;
            else
            {
                var innocents = people.Where(r => r != mimic && HasFamily(r)).ToList();
                if (innocents.Count > 0) seen = innocents[rng.Next(innocents.Count)];
            }
            if (seen == null) return (null, null);
            var witnesses = people.Where(r => r != seen && r != mimic && r.LastName != seen.LastName).ToList();
            return witnesses.Count > 0 ? (witnesses[rng.Next(witnesses.Count)], seen) : (null, null);
        }

        private void PlaceBody(bool announce)
        {
            var rng = Rng(4);
            var fields = ScheduleSpot.All.Where(s => s.Id.StartsWith("Field:")).OrderBy(s => s.Id).ToList();
            if (fields.Count == 0) return;
            var spot = fields[rng.Next(fields.Count)];
            // On the clear ground at the field's edge, not in the stalks: the body is the image of the day.
            Vector3 pos = spot.transform.position;
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var onGround, 3f, UnityEngine.AI.NavMesh.AllAreas)) pos = onGround.position;
            // Laid on its side the rig's pivot is at the hips: lift it so it lies on the ground, not in it.
            var apparition = Events.Apparition.Create(Missing, Events.ApparitionLook.Double, pos + Vector3.up * 0.3f, Quaternion.Euler(0, rng.Next(360), 90f));
            apparition.Avatar.Rig?.Tint("HeadShape", new Color(0.75f, 0.76f, 0.74f));
            body = apparition.gameObject;
            // A lantern somebody left beside it, and crows: you find it by the light and the noise, not by the prompt.
            var lantern = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lantern.name = "Left Lantern";
            Object.Destroy(lantern.GetComponent<Collider>());
            lantern.transform.SetParent(body.transform.parent, true);
            lantern.transform.position = pos + Vector3.right * 1.1f + Vector3.up * 0.18f;
            lantern.transform.localScale = new Vector3(0.18f, 0.36f, 0.18f);
            var brass = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.name == "Brass");
            if (brass != null) lantern.GetComponent<Renderer>().sharedMaterial = brass;
            var glow = new GameObject("Lantern Light").AddComponent<Light>();
            glow.transform.SetParent(lantern.transform, false);
            glow.transform.localPosition = Vector3.up * 0.3f;
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.75f, 0.4f);
            glow.range = 9f;
            glow.intensity = 2.2f;
            glow.shadows = LightShadows.None;
            var crows = new GameObject("Crows");
            crows.transform.position = pos + Vector3.up * 2f;
            crows.AddComponent<Events.CrowCalls>();
            var trigger = new GameObject("Body Trigger");
            trigger.transform.position = pos + Vector3.up * 0.5f;
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2.5f, 1.5f, 2.5f);
            trigger.AddComponent<EvidenceItem>().Configure("body_" + Missing.DisplayName.Replace(" ", ""), Missing.FirstName + "'s body",
                $"{Missing.DisplayName}, face-down in the dirt. No blood anywhere. Two neat holes behind the jaw, a finger's width apart, like Greta's cattle.",
                Missing.DisplayName, "Kneel by the body", 18f);
            if (announce)
            {
                string where = World.LocationMarker.Describe(pos);
                Notebook.Instance?.Record(NoteKind.Event, Missing.DisplayName, "Found",
                    $"They found {Missing.FirstName} this morning {where}. Nobody will say how. Everyone says it was an animal. Out at the farms, the new padlocks on cellar hatches are lying open in the dirt.", null, "found|" + Missing.DisplayName);
                PlayerHUD.Instance?.Toast($"<b>{Missing.FirstName} has been found {where}.</b>");
            }
        }

        private void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 2f;
            var clock = GameClock.Instance;
            var director = CaseDirector.Instance;
            if (clock == null || director == null || !director.IsGenerated) return;

            // The first evening ends: everyone heads home, and the detective is told where they've gone.
            if (clock.Day == 1 && clock.Hour >= 22.5f && !Save.SaveSystem.Flags.Contains("hint:townhome"))
            {
                Save.SaveSystem.Flags.Add("hint:townhome");
                foreach (var r in Resident.All) r.GetComponent<ResidentMover>()?.Reevaluate();
                PlayerHUD.Instance?.SetHint("The town is going home for the night. Mae's closing up.\n<size=75%>Father Crane keeps the church open late. Everyone else is out at their farms. Sleep in Room 6 when you're done.</size>", 12f);
            }

            bool service = clock.Day == 2 && clock.Hour >= 8.5f && clock.Hour < 12f;
            // The town meeting is the last morning: name them by tonight.
            bool meeting = clock.Day == LastDay && clock.Hour >= 9f && clock.Hour < 12f;
            bool gathering = service || meeting;

            // Inside the church during a gathering, time slows so the room can actually be watched.
            var camera = Camera.main;
            bool inside = gathering && camera != null && World.LocationMarker.Describe(camera.transform.position).Contains("First Church");
            GameClock.RateScale = inside ? 0.25f : 1f;

            if (clock.Day == 2 && !announcedService && clock.Hour >= 8.5f && clock.Hour < 11f)
            {
                announcedService = true;
                Audio.GameAudio.Play("bell_toll", 0.45f);
                PlayerHUD.Instance?.Toast("The church bell is ringing for Sunday service.");
            }
            if (clock.Day == LastDay && !announcedMeeting && clock.Hour >= 8.5f && clock.Hour < 11.5f)
            {
                announcedMeeting = true;
                Audio.GameAudio.Play("bell_toll", 0.45f);
                PlayerHUD.Instance?.Toast("The town is meeting in the church this morning. About you.");
            }

            // 3 AM on the night after the disappearance: Wendell rings the bell and counts who comes.
            if (clock.Day == MissingDay && clock.Hour >= 26.5f && !threeAm && !Events.Stalker.IsHunting && !InputLock.IsLocked)
            {
                threeAm = true;
                StartCoroutine(ThreeInTheMorning());
            }

            // A save at nine every night, so dying in the dark costs an evening, not a day.
            if (clock.Hour >= 21f && clock.Hour < 21.5f && duskSavedDay != clock.Day && !Events.Stalker.IsHunting && !InputLock.IsLocked
                && GameFlow.Instance != null && !GameFlow.Instance.IsSleeping && camOk())
            {
                duskSavedDay = clock.Day;
                if (Save.SaveSystem.Save()) PlayerHUD.Instance?.Toast("<size=80%>Saved.</size>");
            }

            if (gathering != Gathering)
            {
                Gathering = gathering;
                gatheringName = service ? "service" : "meeting";
                seats.Clear();
                if (gathering)
                    AssignPews(director);
                sermonShown = false;
                foreach (var r in Resident.All)
                    r.GetComponent<ResidentMover>()?.Reevaluate();
            }

            // Walking in during the gathering.
            var cam = Camera.main;
            if (Gathering && !sermonShown && cam != null && World.LocationMarker.Describe(cam.transform.position).Contains("First Church"))
            {
                sermonShown = true;
                int heads = seats.Count(s => s.Key.isActiveAndEnabled) + 1;
                if (gatheringName == "service")
                {
                    PlayerHUD.Instance?.ShowMessage("<i>\"...and while men slept, his enemy came and sowed tares among the wheat, and went his way.\"</i>\nFather Crane doesn't look up from the altar. Every head in the pews turns to watch you come in.", 8f);
                    Notebook.Instance?.Record(NoteKind.Event, Notebook.CaseSubject, "Sunday service",
                        $"The whole town at church, {heads} heads counting Father Crane. Families in the same pews they've had for forty years; a few people on their own at the back by the door. Wendell Dobbs was watching from the treeline, as always.", null, "service");
                }
                else
                {
                    var speaker = new[] { CastNames.Harlan, CastNames.Greta, CastNames.Silas, CastNames.Mae }
                        .Select(Resident.FindByName).FirstOrDefault(r => r != null && r.isActiveAndEnabled && r != director.Mimic);
                    string who = speaker != null ? speaker.FirstName : "Someone";
                    PlayerHUD.Instance?.ShowMessage($"The meeting goes quiet when you walk in. {who} stands up.\n<i>\"Name them, Detective. Name them by tonight, or be gone.\"</i>", 8f);
                    Notebook.Instance?.Record(NoteKind.Event, Notebook.CaseSubject, "Town meeting",
                        "The town met about me. They want a name, or they want me gone. They're frightened. So am I.", null, "meeting");
                }
            }
        }
    }
}
