using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Semblance.Case;
using Semblance.Core;
using Semblance.Interaction;
using Semblance.Notes;
using Semblance.Residents;
using UnityEngine;

namespace Semblance.World
{
    /// <summary>
    /// A family's front door. You knock before you open it, and what happens depends on who's home and what state
    /// they're in: nobody home (silence), somebody up (they come to the door and ask what you want), somebody asleep
    /// (they wake, open up, and are not pleased), or somebody who'd rather you thought the house was empty. If you
    /// open a door nobody answered and find someone standing there awake, that goes in the notebook: it might be the
    /// mimic avoiding you, or a neighbour with a human reason for not answering.
    /// </summary>
    public class KnockableDoor : MonoBehaviour
    {
        [SerializeField] private string family;
        [SerializeField] private Transform insidePoint;
        [SerializeField] private Transform outsidePoint;
        [SerializeField, TextArea] private string lockedKnockMessage = "No answer.";

        private Door door;
        private Coroutine routine;
        private float knockedAt = -1000f;
        private bool answered;
        private readonly List<Resident> awakeInside = new List<Resident>();
        private float invitedUntil = -1000f;
        private readonly HashSet<Resident> insideAtEntry = new HashSet<Resident>();
        private bool playerWasInside;
        private float nextTrespassCheck;
        private const float HalfWidth = 4.8f, HalfDepth = 3.8f; // a farmhouse's inside, in its own frame

        public bool Busy => routine != null;

        /// <summary>You were let in (somebody answered); walking about inside isn't trespass for a while.</summary>
        public bool Invited => Time.time < invitedUntil;

        /// <summary>Somebody of the house is right there, awake, and hasn't let you in: the door isn't yours to open.</summary>
        public Resident Guard(Vector3 playerPosition)
        {
            if (Invited || door == null || IsInside(playerPosition)) return null;
            var clock = GameClock.Instance;
            float hour = clock != null ? clock.Hour : 12f;
            foreach (var r in Resident.All)
            {
                if (r == null || !r.isActiveAndEnabled || r.Profile == null || r.Profile.home != family + " Farm") continue;
                if (SleepingHours(hour) && !AwakeAtNight(r)) continue;
                if (Dialogue.DialogueUI.Instance != null && Dialogue.DialogueUI.Instance.Current == r) continue;
                if (Vector3.Distance(r.transform.position, door.transform.position) < 4.5f && !IsInside(r.transform.position)) return r;
            }
            return null;
        }

        private Transform House => door != null ? door.transform.parent.parent : transform;

        private bool IsInside(Vector3 worldPosition)
        {
            var p = House.InverseTransformPoint(worldPosition);
            return Mathf.Abs(p.x) < HalfWidth && Mathf.Abs(p.z) < HalfDepth && p.y > -1f && p.y < 3.5f;
        }

        /// <summary>The family's own people who are outside the house but close to its door.</summary>
        private List<Resident> OnTheStep()
        {
            var list = new List<Resident>();
            foreach (var r in Resident.All)
            {
                if (r == null || !r.isActiveAndEnabled || r.Profile == null || r.Profile.home != family + " Farm") continue;
                if (!IsInside(r.transform.position) && door != null && Vector3.Distance(r.transform.position, door.transform.position) < 7f) list.Add(r);
            }
            return list;
        }

        /// <summary>A knock is the thing to do unless someone just answered or you knocked a minute ago.</summary>
        public bool WantsKnock => routine == null && Time.time - knockedAt > 45f;

        public void Configure(string familyName, Transform inside, Transform outside, string lockedMessage = null)
        {
            family = familyName;
            insidePoint = inside;
            outsidePoint = outside;
            if (lockedMessage != null) lockedKnockMessage = lockedMessage;
        }

        private void Awake() => door = GetComponent<Door>();

        public void Knock(PlayerInteractor interactor)
        {
            if (routine != null) return;
            routine = StartCoroutine(KnockRoutine(interactor));
        }

        /// <summary>Who of this family is in the house right now (not missing, not dead, not out).</summary>
        private List<Resident> Household()
        {
            var home = new List<Resident>();
            foreach (var r in Resident.All)
            {
                if (r == null || !r.isActiveAndEnabled || r.Profile == null || r.Profile.home != family + " Farm") continue;
                var mover = r.GetComponent<ResidentMover>();
                var spot = mover != null ? mover.CurrentSpot : null;
                bool inHouse = spot != null && spot.Id != null && spot.Id.StartsWith("House:" + family + ":") && !spot.Id.EndsWith(":Porch")
                               && insidePoint != null && Vector3.Distance(r.transform.position, insidePoint.position) < 9f;
                if (inHouse) home.Add(r);
            }
            return home;
        }

        private static bool SleepingHours(float hour) => hour >= 23f || hour < 6.5f;
        private static bool NightHours(float hour) => hour >= 20.5f || hour < 6.5f;

        /// <summary>The mimic never sleeps; a few people genuinely can't.</summary>
        private static bool AwakeAtNight(Resident r)
        {
            var director = CaseDirector.Instance;
            if (director != null && director.Mimic == r) return director.Noise == null || director.Noise.MimicAwakeAtNight;
            return r.VisibleFeatures().Any(f => f.id == "insomnia");
        }

        /// <summary>Won't come to the door even though they're up.</summary>
        private static bool Hides(Resident r, float hour)
        {
            var director = CaseDirector.Instance;
            var noise = director != null ? director.Noise : null;
            if (noise == null) return false;
            if (director.Mimic == r) return noise.MimicHidesAtDoor;
            if (noise.DoorHider == r) return true;
            return noise.FedUp.Contains(r) && NightHours(hour);
        }

        private IEnumerator KnockRoutine(PlayerInteractor interactor)
        {
            knockedAt = Time.time;
            answered = false;
            awakeInside.Clear();
            Audio.GameAudio.PlayAt("knock", door.transform.position, 0.75f, Random.Range(0.95f, 1.05f), 22f, Audio.AudioBus.World);
            var hud = interactor.Hud;

            if (door.IsLocked)
            {
                yield return new WaitForSeconds(1.8f);
                hud.ShowMessage(lockedKnockMessage, 4.5f);
                routine = null;
                yield break;
            }

            var clock = GameClock.Instance;
            float hour = clock != null ? clock.Hour : 12f;
            bool sleeping = SleepingHours(hour);

            // Somebody's on the porch: they're not letting a stranger knock on their own door.
            var step = OnTheStep();
            if (step.Count > 0)
            {
                var who2 = step[0];
                yield return new WaitForSeconds(0.6f);
                var mover2 = who2.GetComponent<ResidentMover>();
                if (mover2 != null && outsidePoint != null) mover2.WalkTo(outsidePoint.position + (outsidePoint.position - door.transform.position).normalized * 0.4f);
                float w2 = 0f;
                while (w2 < 6f && outsidePoint != null && Vector3.Distance(who2.transform.position, outsidePoint.position) > 1.3f) { w2 += Time.deltaTime; yield return null; }
                who2.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(interactor.transform.position - who2.transform.position, Vector3.up));
                string[] porch =
                {
                    "That's my house, Detective. Whatever you want, you can ask me out here.",
                    "You don't knock on a man's door with him standing next to you. What is it?",
                    "Nobody's in there but mine. Ask me.",
                };
                who2.DoorstepGreeting = porch[Random.Range(0, porch.Length)];
                var dlg = Dialogue.DialogueUI.Instance;
                if (dlg != null)
                {
                    dlg.Open(who2, interactor);
                    while (dlg.Current == who2) yield return null;
                }
                mover2?.Release();
                routine = null;
                yield break;
            }

            var home = Household();

            Resident who = null;
            foreach (var r in home)
            {
                bool up = !sleeping || AwakeAtNight(r);
                if (Hides(r, hour) && up) { awakeInside.Add(r); continue; }
                if (who == null || (!AwakeAtNight(who) && AwakeAtNight(r))) who = r; // whoever's already up gets there first
            }

            if (who == null)
            {
                yield return new WaitForSeconds(sleeping ? 4f : 2.6f);
                hud.ShowMessage(home.Count == 0 ? "No answer. Nothing moves inside." : "No answer.", 3f);
                routine = null;
                yield break;
            }

            // Woken once tonight already: they're not getting up again.
            int day = clock != null ? clock.Day : 1;
            string wokeFlag = $"woke|{who.DisplayName}|{day}";
            bool wakingThem = sleeping && !AwakeAtNight(who);
            string thrownFlag = $"thrown|{family}|{day}";
            if (Save.SaveSystem.Flags.Contains(thrownFlag))
            {
                yield return new WaitForSeconds(2f);
                hud.ShowMessage($"The door doesn't open. From inside: \"I told you, Detective.\"", 4f);
                routine = null;
                yield break;
            }
            if (wakingThem && Save.SaveSystem.Flags.Contains(wokeFlag))
            {
                yield return new WaitForSeconds(3f);
                hud.ShowMessage($"Nothing. Then, from inside, {who.FirstName}'s voice, not loud: \"Go home, Detective.\"", 4.5f);
                routine = null;
                yield break;
            }

            float delay = wakingThem ? Random.Range(4.5f, 7f) : Random.Range(1.2f, 2.6f);
            yield return new WaitForSeconds(delay * 0.5f);
            var mover = who.GetComponent<ResidentMover>();
            if (mover != null && insidePoint != null) mover.WalkTo(insidePoint.position);
            yield return new WaitForSeconds(delay * 0.5f);

            float waited = 0f;
            while (waited < 9f && insidePoint != null && Vector3.Distance(who.transform.position, insidePoint.position) > 0.9f)
            {
                waited += Time.deltaTime;
                if (Vector3.Distance(interactor.transform.position, door.transform.position) > 7f || !who.isActiveAndEnabled)
                {
                    // You didn't wait. They'll go back to whatever they were doing.
                    mover?.Release();
                    routine = null;
                    yield break;
                }
                yield return null;
            }
            if (insidePoint != null && outsidePoint != null)
                who.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(outsidePoint.position - insidePoint.position, Vector3.up));

            door.ForceOpen(true);
            if (wakingThem)
            {
                Save.SaveSystem.Flags.Add(wokeFlag);
                Events.Nerve.Instance?.Shock(4f);
                if (CaseDirector.Instance != null && CaseDirector.Instance.Mimic == who) { MimicAwareness.Instance?.Raise(5f, "woken at night"); MimicAwareness.Instance?.Expose(8f, "woken at night"); }
            }
            who.DoorstepGreeting = Greeting(who, hour, wakingThem);
            RecordAnswer(who, hour, wakingThem);
            answered = true;
            invitedUntil = Time.time + (wakingThem ? 0f : 240f); // let in by day; at night they want you gone

            var dialogue = Dialogue.DialogueUI.Instance;
            if (dialogue != null)
            {
                dialogue.Open(who, interactor);
                while (dialogue.Current == who) yield return null;
            }
            yield return new WaitForSeconds(wakingThem ? 0.4f : 1.2f);
            door.ForceOpen(false);
            mover?.Release();
            routine = null;
        }

        private void Update()
        {
            if (routine != null || Time.time < nextTrespassCheck || door == null) return;
            nextTrespassCheck = Time.time + 0.5f;
            var cam = Camera.main;
            if (cam == null) return;
            bool inside = IsInside(cam.transform.position);
            if (inside && !playerWasInside)
            {
                // Remember who was already in when you came in: it's whoever arrives after that catches you.
                insideAtEntry.Clear();
                foreach (var r in Resident.All)
                    if (r != null && r.isActiveAndEnabled && IsInside(r.transform.position)) insideAtEntry.Add(r);
            }
            playerWasInside = inside;
            if (!inside || Invited || InputLock.IsLocked) return;
            var clock = GameClock.Instance;
            float hour = clock != null ? clock.Hour : 12f;
            foreach (var r in Resident.All)
            {
                if (r == null || !r.isActiveAndEnabled || r.Profile == null || r.Profile.home != family + " Farm") continue;
                if (insideAtEntry.Contains(r) || !IsInside(r.transform.position)) continue;
                if (SleepingHours(hour) && !AwakeAtNight(r)) continue;
                var pi = FindFirstObjectByType<PlayerInteractor>();
                if (pi != null) routine = StartCoroutine(CaughtRoutine(r, pi));
                return;
            }
        }

        /// <summary>They came home and you were in their house. They walk you to the door, and the door stays shut after.</summary>
        private IEnumerator CaughtRoutine(Resident who, PlayerInteractor interactor)
        {
            var mover = who.GetComponent<ResidentMover>();
            Events.Nerve.Instance?.Shock(8f);
            if (CaseDirector.Instance != null && CaseDirector.Instance.Mimic == who) { MimicAwareness.Instance?.Raise(8f, "caught in its house"); MimicAwareness.Instance?.Expose(15f, "caught in its house"); }
            if (mover != null) mover.WalkTo(interactor.transform.position);
            float t = 0f;
            while (t < 6f && Vector3.Distance(who.transform.position, interactor.transform.position) > 1.8f) { t += Time.deltaTime; yield return null; }
            who.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(interactor.transform.position - who.transform.position, Vector3.up));
            string[] caught =
            {
                "What the hell are you doing in my house? ...No. Don't answer. Out. Now.",
                "You walked in. Nobody answered, so you walked in. Is that how they do it in the city? Get out.",
                "I ought to fetch the gun. I'm not going to, because you're leaving. Say your piece on the porch.",
            };
            who.DoorstepGreeting = caught[Random.Range(0, caught.Length)];
            var dlg = Dialogue.DialogueUI.Instance;
            if (dlg != null)
            {
                dlg.Open(who, interactor);
                while (dlg.Current == who) yield return null;
            }
            // Walked to the door and shut out.
            var cc = interactor.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            if (outsidePoint != null) interactor.transform.position = outsidePoint.position + (outsidePoint.position - door.transform.position).normalized * 0.8f;
            if (cc != null) cc.enabled = true;
            door.ForceOpen(false);
            int day = GameClock.Instance != null ? GameClock.Instance.Day : 1;
            Save.SaveSystem.Flags.Add($"thrown|{family}|{day}");
            interactor.Hud.ShowMessage($"{who.FirstName} walks you to the door and shuts it behind you. The bolt goes across.", 5f);
            Notebook.Instance?.Record(NoteKind.Event, Notebook.CaseSubject, "Thrown out", $"{who.DisplayName} caught me in the house and put me out. I won't be welcome there again today.", null, $"thrown|{family}|{day}");
            playerWasInside = false;
            mover?.Release();
            routine = null;
        }

        private static string Greeting(Resident who, float hour, bool woken)
        {
            var noise = CaseDirector.Instance != null ? CaseDirector.Instance.Noise : null;
            string daypart = hour < 12f ? "Morning" : hour < 17f ? "Afternoon" : "Evening";
            string[] pool;
            if (woken)
                pool = new[]
                {
                    "Do you know what time it is? ...The detective. Of course it is. Say what you came to say and get off my porch.",
                    "It's the middle of the night. If this is about the sign, I don't care about the sign. Talk, then go.",
                    "There's a shotgun by this door, Detective. I'd have brought it if I'd been thinking straight. What.",
                };
            else if (SleepingHours(hour))
                pool = new[]
                {
                    "Detective. Can't sleep either? Well. Come on, say it.",
                    "I saw you coming up the drive. I don't sleep much. What do you need?",
                    "You keep late hours for a city man. So do I. Go on.",
                };
            else if (NightHours(hour))
                pool = new[]
                {
                    "It's late, Detective. Keep it short.",
                    "Evening. I was about to turn in. What is it?",
                };
            else if (noise != null && noise.FedUp.Contains(who))
                pool = new[] { "What now, Detective?", "You again. Make it quick, I've got work." };
            else
                pool = new[]
                {
                    "{daypart}, Detective. Something I can do for you?",
                    "Yes? ...Oh. The detective. What is it?",
                    "You found the place, then. What do you want?",
                };
            return pool[Random.Range(0, pool.Length)].Replace("{daypart}", daypart);
        }

        /// <summary>Somebody answering wide awake at one in the morning is worth writing down.</summary>
        private static void RecordAnswer(Resident who, float hour, bool woken)
        {
            if (woken || !SleepingHours(hour) || Notebook.Instance == null) return;
            var director = CaseDirector.Instance;
            bool mimic = director != null && director.Mimic == who;
            string he = who.Profile.possessive == "his" ? "he" : who.Profile.possessive == "her" ? "she" : "they";
            string stamp = GameClock.FormatHour(hour);
            if (mimic)
            {
                var e = Notebook.Instance.Record(NoteKind.Observation, who.DisplayName, "Answered the door at night",
                    $"{who.FirstName} opened the door at {stamp}, dressed, wide awake, as if {he}'d been standing behind it. No light on in the house.",
                    null, $"doornight|{who.DisplayName}");
                if (e != null) { e.fact = "self|awake"; Notebook.Instance.SetSuspicious(e.id, true); }
            }
            else
            {
                var e = Notebook.Instance.Record(NoteKind.Observation, who.DisplayName, "Answered the door at night",
                    $"{who.FirstName} opened the door at {stamp}, dressed, wide awake. \"Can't sleep. Never could.\" The rings under {who.Profile.possessive} eyes agree.",
                    null, $"doornight|{who.DisplayName}");
                if (e != null) e.explains = "insomnia";
            }
        }

        /// <summary>The player opened the door themselves. If a knock went unanswered and somebody was in, that's noticed.</summary>
        public void OnOpened(PlayerInteractor interactor)
        {
            if (answered || Time.time - knockedAt > 90f || awakeInside.Count == 0) return;
            var r = awakeInside[0];
            awakeInside.Clear();
            var director = CaseDirector.Instance;
            var noise = director != null ? director.Noise : null;
            string he = r.Profile.possessive == "his" ? "he" : r.Profile.possessive == "her" ? "she" : "they";
            string text;
            string explains = null;
            string fact = null;
            if (director != null && director.Mimic == r)
            {
                text = $"{r.FirstName} is standing in the middle of the room, facing the door. Not sitting, not doing anything. {char.ToUpper(he[0]) + he.Substring(1)} heard me knock. {char.ToUpper(he[0]) + he.Substring(1)} was there the whole time.";
                fact = "self|hid";
            }
            else if (noise != null && noise.DoorHider == r)
            {
                text = $"{r.FirstName} didn't answer, and I can see why: " + CaseNoise.Fill(noise.DoorHiderReason, r);
                explains = "hiding";
            }
            else
            {
                text = $"{r.FirstName} looks up at me from the dark. \"I heard you. I was hoping you'd take the hint.\"";
                explains = "fedup";
            }
            interactor.Hud.ShowReading(text);
            Audio.GameAudio.Play("sting_soft", fact != null ? 0.45f : 0.2f);
            var e = Notebook.Instance?.Record(NoteKind.Observation, r.DisplayName, "Didn't answer the door", text, null, $"doorhid|{r.DisplayName}|{(GameClock.Instance != null ? GameClock.Instance.Day : 0)}");
            if (e == null) return;
            e.fact = fact;
            e.explains = explains;
            if (fact != null) Notebook.Instance.SetSuspicious(e.id, true);
        }
    }
}
