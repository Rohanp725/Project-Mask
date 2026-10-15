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
            yield return new WaitForSeconds(8f);
            PlayerHUD.Instance?.ShowMessage("From the treeline by the church, a man's voice, counting under his breath. <i>\"...two hundred and ten. Two hundred and eleven. Two hundred and twelve.\"</i> A long pause. <i>\"Then who painted the sign?\"</i>", 8f);
            Notebook.Instance?.Record(NoteKind.Event, Notebook.CaseSubject, "The bell at three",
                "At three in the morning someone rang the church bell and the whole town came out with lanterns. Somebody in the treeline counted them. Two hundred and twelve. All of them came.", null, "event|threeam");
        }

        private System.Random Rng(int salt) => new System.Random((CaseDirector.Instance != null ? CaseDirector.Instance.Seed : 1) * 13 + salt);

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
            Vector3 pos = spot.transform.position + spot.transform.right * 2.5f;
            var apparition = Events.Apparition.Create(Missing, Events.ApparitionLook.Double, pos, Quaternion.Euler(0, rng.Next(360), 90f));
            apparition.Avatar.Rig?.Tint("HeadShape", new Color(0.75f, 0.76f, 0.74f));
            body = apparition.gameObject;
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

            bool service = clock.Day == 2 && clock.Hour >= 9f && clock.Hour < 12f;
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
            if (clock.Day == MissingDay && clock.Hour >= 26.75f && !threeAm && !Events.Stalker.IsHunting && !InputLock.IsLocked)
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
