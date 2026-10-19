using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Semblance.Audio;
using Semblance.Case;
using Semblance.Core;
using Semblance.Notes;
using Semblance.Player;
using Semblance.Rendering;
using Semblance.Residents;
using Semblance.UI;
using Semblance.World;
using UnityEngine;
using UnityEngine.AI;

namespace Semblance.Events
{
    /// <summary>
    /// Everything uncanny. At night: figures in the fields, lamps dying, your name from the corn, knocking,
    /// footsteps that stop when you do, faces at windows, a figure walking backwards toward you, a stranger in
    /// the mirror, and at the highest awareness the hunt. By day: people seen where they can't be.
    /// The mimic's awareness of you decides how bad it gets.
    /// </summary>
    public class EventDirector : MonoBehaviour
    {
        public static EventDirector Instance { get; private set; }

        private Camera cam;
        private Transform player;
        private FirstPersonController controller;
        private float nextNight;
        private float nextDay;
        private bool busy;
        private int nightEvents, nightEventsDay = -1;
        private MeshRenderer[] windowCache;
        private bool resumeGrace;
        private string lastEvent;
        private int huntDay = -1;
        private float huntAt = -1f;
        private float nextLampDeath;
        private int afterHoursDay = -1;
        private float afterHoursAt = -1f;
        private Transform diner;

        private void Awake() => Instance = this;

        private void Start()
        {
            nextNight = Time.time + 60f;
            nextDay = Time.time + 150f;
            // Reloading after finding the captive doesn't get you out of what comes next.
            GameFlow.GameResumed += () =>
            {
                if (!Save.SaveSystem.Flags.Contains("hunt:forced")) return;
                ForcedHuntPending = true;
                resumeGrace = true; // give a reloaded player the motel to gather themselves in
            };
            GameFlow.SleepInterruption = SleepEvent;
            // The morning after it started plotting: somebody tells you it was asking after you, and there's a note.
            GameFlow.Woke += day =>
            {
                var aware = MimicAwareness.Instance;
                if (aware == null || !aware.IsPlotting || Save.SaveSystem.Flags.Contains("plot:morning")) return;
                Save.SaveSystem.Flags.Add("plot:morning");
                PlotMorning(day);
            };
        }

        /// <summary>It asked where you sleep, and somebody noticed. And it answered the question itself, under your door.</summary>
        private void PlotMorning(int day)
        {
            var mimic = Mimic;
            if (mimic == null) return;
            // A townsperson who'd have been asked: never the mimic, never its family.
            var witnesses = Resident.All.Where(r => r != null && r != mimic && r.isActiveAndEnabled && r.LastName != mimic.LastName
                && r.Avatar != null && r.Avatar.Modifications != null).OrderBy(_ => Random.value).ToList();
            var witness = witnesses.FirstOrDefault(r => r.DisplayName == CastNames.Mae || r.DisplayName == CastNames.Otis || r.DisplayName == CastNames.Crane) ?? witnesses.FirstOrDefault();
            if (witness != null)
            {
                string title = "About " + mimic.DisplayName;
                string line = $"{mimic.FirstName} came by yesterday asking which room you've got at the motel. Asked twice. I said I didn't know. Thought you'd want telling.";
                var existing = witness.Avatar.Modifications.ExtraGossip.FirstOrDefault(g => g.title == title);
                if (existing != null) existing.response = existing.response + "\n\n" + line;
                else witness.Avatar.Modifications.ExtraGossip.Add(new DialogueTopic(title, line));
            }
            // The note, on the floor inside Room 6.
            var wake = GameFlow.Instance != null ? GameFlow.Instance.WakeSpot : null;
            if (wake != null)
            {
                var note = GameObject.CreatePrimitive(PrimitiveType.Cube);
                note.name = "Note Under The Door";
                note.transform.position = wake.position + wake.forward * 1.1f + Vector3.up * 0.016f;
                note.transform.rotation = Quaternion.Euler(0, wake.eulerAngles.y + 14f, 0);
                note.transform.localScale = new Vector3(0.21f, 0.004f, 0.28f);
                var paper = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.name == "Paper");
                var r = note.GetComponent<Renderer>();
                if (paper != null) r.sharedMaterial = paper;
                var box = note.GetComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(2f, 60f, 2f);
                var insp = note.AddComponent<Interaction.InspectableObject>();
                typeof(Interaction.InspectableObject).GetField("prompt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(insp, "Pick up the note");
                insp.SetDescription("A sheet of lined paper pushed under the door in the night. Block capitals, pressed so hard the pencil's gone through in two places:\n\n<b>YOU ARE NOT FROM HERE. GO HOME.</b>\n\nNo name. The paper smells faintly of the diner.");
                insp.SetClue("plot_note");
                Notebook.Instance?.Record(NoteKind.Event, Notebook.CaseSubject, "A note under the door",
                    "Somebody pushed a note under the door of Room 6 in the night: GO HOME. Whoever wrote it knows where I sleep, and wanted me to know they know.", null, "plot_note");
            }
        }

        private static int Tier => MimicAwareness.Instance != null ? MimicAwareness.Instance.Tier : 0;
        private static Resident Mimic => CaseDirector.Instance != null ? CaseDirector.Instance.Mimic : null;

        /// <summary>The face an apparition wears: the mimic's half the time, anyone else's the rest. It practises faces.</summary>
        private static Resident WornFace()
        {
            var mimic = Mimic;
            // Once it's plotting it has stopped practising: it wears its own face in the dark.
            if (mimic != null && MimicAwareness.Instance != null && MimicAwareness.Instance.IsPlotting) return mimic;
            var others = Resident.All.Where(r => r != mimic && r.isActiveAndEnabled).ToList();
            if (mimic == null || (others.Count > 0 && Random.value < 0.5f))
                return others.Count > 0 ? others[Random.Range(0, others.Count)] : mimic;
            return mimic;
        }

        /// <summary>Set when the detective finds the real resident: the mimic comes as soon as they surface.</summary>
        public static bool ForcedHuntPending { get; set; }

        private bool Ready()
        {
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null) return false;
                player = cam.transform.root;
                controller = player.GetComponent<FirstPersonController>();
            }
            return true;
        }

        private void Update()
        {
            if (!Ready() || Mimic == null) return;
            if (resumeGrace && ForcedHuntPending)
            {
                var wake = GameObject.Find("Wake Spot");
                if (wake == null || Vector3.Distance(player.position, wake.transform.position) > 30f) resumeGrace = false;
            }
            if (ForcedHuntPending && !resumeGrace && player.position.y > -10f && !Stalker.IsHunting && !InputLock.IsLocked
                && (EndingDirector.Instance == null || !EndingDirector.Instance.IsPlaying))
            {
                ForcedHuntPending = false;
                Stalker.Begin(forced: true);
                return;
            }
            var clock = GameClock.Instance;
            if (clock == null || EndingDirector.Instance != null && EndingDirector.Instance.IsPlaying) return;
            if (busy || Stalker.IsHunting || (InputLock.IsLocked && !(InputLock.Top is Dialogue.DialogueUI))) return;
            if (GameFlow.Instance != null && GameFlow.Instance.IsSleeping) return;

            bool night = clock.IsDark;
            int tier = Tier;
            // The calendar sets a floor under how bad the nights get, whatever the player does.
            int eventTier = Mathf.Max(tier, Mathf.Clamp(clock.Day - 2, 0, 2));

            // The hunt: once per night, and only when it has decided. The first night of plotting it only follows.
            bool stalk = false;
            bool huntWorthy = MimicAwareness.Instance != null && MimicAwareness.Instance.WantsHunt(clock, out stalk);
            if (night && huntWorthy && clock.Hour >= 21.5f && huntDay != clock.Day)
            {
                if (huntAt < 0) huntAt = Time.time + Random.Range(25f, 80f);
                else if (Time.time > huntAt && !InputLock.IsLocked)
                {
                    huntAt = -1f;
                    if (!stalk) MimicAwareness.Instance?.WarnOfHunt();
                    if (Stalker.Begin(false, stalk)) huntDay = clock.Day;
                    else huntAt = Time.time + 20f; // nowhere to come from (a cellar, the crypt): try again shortly
                    return;
                }
            }
            // Plotting nights: the lamps along your way die as you reach them.
            bool plotting = MimicAwareness.Instance != null && MimicAwareness.Instance.IsPlotting;
            if (night && plotting && Time.time > nextLampDeath)
            {
                nextLampDeath = Time.time + Random.Range(35f, 60f);
                var lamp = Rendering.LampSwitch.Lamps.Where(l => l != null && l.IsOn && Vector3.Distance(l.transform.position, player.position) < 14f)
                    .OrderBy(l => Vector3.Distance(l.transform.position, player.position)).FirstOrDefault();
                if (lamp != null) { lamp.Kill(Random.Range(15f, 30f)); GameAudio.PlayAt("lamp_die", lamp.transform.position, 0.6f, 1f, 25f); }
            }

            // After midnight, once a night, Mae's lights come on by themselves if you're anywhere near the road.
            if (night && clock.Hour >= 24f && afterHoursDay != clock.Day && !GameSettings.ReduceScares)
            {
                if (diner == null) { var go = GameObject.Find("Diner"); diner = go != null ? go.transform : null; }
                if (diner != null && Vector3.Distance(player.position, diner.position) < 45f && !AmbientAudio.IsIndoors(player.position))
                {
                    if (afterHoursAt < 0) afterHoursAt = Time.time + Random.Range(4f, 15f);
                    else if (Time.time > afterHoursAt)
                    {
                        afterHoursAt = -1f;
                        afterHoursDay = clock.Day;
                        StartCoroutine(Run(AfterHours()));
                        return;
                    }
                }
            }

            // A night gets two or three things, spaced out, so each one lands; a night of constant scares is just noise.
            if (nightEventsDay != clock.Day) { nightEventsDay = clock.Day; nightEvents = 0; }
            int cap = GameSettings.ReduceScares ? 1 : plotting ? 4 : tier >= 2 ? 3 : 2;
            if (night && nightEvents < cap && Time.time > nextNight)
            {
                nextNight = Time.time + Random.Range(90f, 180f) * (1f - tier * 0.1f) * (plotting ? 0.65f : 1f);
                var evt = PickNightEvent(eventTier);
                if (evt != null) StartCoroutine(Run(evt));
            }
            else if (!night && Time.time > nextDay)
            {
                nextDay = Time.time + Random.Range(140f, 260f);
                if (Random.value < 0.6f) StartCoroutine(Run(TwoPlaces()));
            }
        }

        private IEnumerator Run(IEnumerator routine)
        {
            busy = true;
            float started = Time.time;
            yield return routine;
            busy = false;
            bool landed = Nerve.Instance == null || Nerve.Instance.LastShockTime >= started;
            if (landed) nightEvents++;
            else if (GameClock.Instance != null && GameClock.Instance.IsDark)
                nextNight = Mathf.Min(nextNight, Time.time + Random.Range(10f, 20f));
        }

        private IEnumerator PickNightEvent(int tier)
        {
            // Early on it's only things you hear; things you see come once it's hostile. The same thing never twice running.
            bool indoors = AmbientAudio.IsIndoors(player.position);
            var options = new List<(float weight, string name, System.Func<IEnumerator> run)>
            {
                (0.8f, "lamp", LampDies),
                (1f, "name", NameCalled),
            };
            if (indoors) options.Add((1.2f, "knock", Knocking));
            if (World.DetectiveCar.Instance != null && World.DetectiveCar.Instance.IsDriving) options.Add((MimicAwareness.Instance != null && MimicAwareness.Instance.IsPlotting ? 6f : 3f, "car", CarStalls));
            if (tier >= 1) options.Add((1f, "steps", FootstepsBehind));
            if (tier >= 2 && !GameSettings.ReduceScares)
            {
                options.Add((1f, "figure", DistantFigure));
                options.Add((0.7f, "double", TwoPlaces));
                if (NearestMirror(5f) != null) options.Add((3f, "mirror", MirrorGhost));
                if (indoors) options.Add((1.5f, "window", FaceAtWindow));
                else options.Add((1.2f, "backwards", BackwardsWalker));
                options.Add((0.8f, "lights", LightsOut));
            }
            if (options.Count > 1) options.RemoveAll(o => o.name == lastEvent);
            float total = options.Sum(o => o.weight);
            float roll = Random.value * total;
            foreach (var o in options)
            {
                roll -= o.weight;
                if (roll <= 0) { lastEvent = o.name; return o.run(); }
            }
            lastEvent = options[0].name;
            return options[0].run();
        }

        private static void LogEvent(string title, string text)
        {
            var clock = GameClock.Instance;
            Notebook.Instance?.Record(NoteKind.Event, Notebook.CaseSubject, title, text, null, $"event|{title}");
        }

        private bool TryGroundPoint(Vector3 around, float minDist, float maxDist, float maxAngleFromView, out Vector3 point)
        {
            for (int i = 0; i < 25; i++)
            {
                float angle = Random.Range(-maxAngleFromView, maxAngleFromView);
                Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                Vector3 p = around + dir * Random.Range(minDist, maxDist);
                if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas))
                {
                    point = hit.position;
                    return true;
                }
            }
            point = Vector3.zero;
            return false;
        }

        private Quaternion Facing(Vector3 from, Vector3 to)
        {
            var dir = Vector3.ProjectOnPlane(to - from, Vector3.up);
            return dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : Quaternion.identity;
        }

        // ------------------------------------------------------------ Night events

        private IEnumerator DistantFigure()
        {
            if (!TryGroundPoint(player.position, 26f, 34f, 55f, out var p)) yield break;
            var shade = Apparition.Create(WornFace(), ApparitionLook.Shade, p, Facing(p, player.position));
            float seenFor = 0f, life = 0f;
            bool shocked = false;
            AmbientAudio.Hush = 0.8f;
            while (shade != null && life < 30f)
            {
                life += Time.deltaTime;
                if (shade.IsSeenBy(cam, 45f, 0.9f))
                {
                    seenFor += Time.deltaTime;
                    if (!shocked) { shocked = true; Nerve.Instance?.Shock(8f); GameAudio.Play("sting_soft", 0.35f); }
                    if (Nerve.Instance != null) Nerve.Instance.ExternalDrain = 3f;
                }
                if (seenFor > 1.6f || Vector3.Distance(player.position, p) < 14f) break;
                yield return null;
            }
            if (Nerve.Instance != null) Nerve.Instance.ExternalDrain = 0f;
            shade?.Vanish();
            AmbientAudio.Hush = 0f;
            if (shocked) LogEvent("A figure in the dark", $"Someone stood {LocationMarker.Describe(p)}, watching me. Black all over, but I knew the shape of them. When I looked again, nobody was there.");
        }

        private IEnumerator CarStalls()
        {
            var car = World.DetectiveCar.Instance;
            if (car == null || !car.IsDriving) yield break;
            car.Stall(7f);
            Audio.GameAudio.Play("lamp_die", 0.5f, 0.6f);
            yield return new WaitForSeconds(2.5f);
            Vector3 ahead = car.transform.position + car.transform.forward * 16f;
            if (NavMesh.SamplePosition(ahead, out var hit, 4f, NavMesh.AllAreas))
            {
                var shade = Apparition.Create(WornFace(), ApparitionLook.Shade, hit.position, Facing(hit.position, car.transform.position));
                Nerve.Instance?.Shock(10f);
                yield return new WaitForSeconds(2.2f);
                shade.Vanish();
            }
            yield return new WaitForSeconds(2.5f);
            LogEvent("The car died", "The engine cut out on the dark road and the headlights stuttered. For a second there was someone standing in the beams. Then the engine caught again.");
        }

        private IEnumerator LampDies()
        {
            var lamp = LampSwitch.Lamps.Where(l => l.IsOn && l.Lamp != null && l.Lamp.enabled)
                .OrderBy(l => Vector3.Distance(l.transform.position, player.position)).FirstOrDefault();
            if (lamp == null || Vector3.Distance(lamp.transform.position, player.position) > 35f) yield break;
            GameAudio.PlayAt("lamp_die", lamp.transform.position + Vector3.up * 4f, 0.8f, 1f, 40f);
            lamp.Kill(14f);
            Nerve.Instance?.Shock(4f);
            yield return new WaitForSeconds(3f);
            GameAudio.PlayAt("whisper_loop", lamp.transform.position, 0.35f, 0.9f, 20f);
            yield return new WaitForSeconds(4f);
        }

        private IEnumerator NameCalled()
        {
            // From behind and to one side, close enough to be unmistakably a voice saying your name.
            Vector3 back = -Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector3 pos = player.position + Quaternion.Euler(0, Random.Range(-60f, 60f), 0) * back * Random.Range(9f, 14f) + Vector3.up * 1.5f;
            AmbientAudio.Hush = 0.9f;
            yield return new WaitForSeconds(0.8f); // the night goes quiet first
            GameAudio.PlayAt("name_whisper", pos, 1f, 1f, 40f);
            yield return new WaitForSeconds(1.2f);
            PlayerHUD.Instance?.ShowMessage($"<i>\"...{CaseDirector.Instance.DetectiveName}...\"</i>", 2.5f);
            Nerve.Instance?.Shock(6f);
            yield return new WaitForSeconds(5f);
            AmbientAudio.Hush = 0f;
            LogEvent("Someone called my name", $"Out in the dark, {LocationMarker.Describe(pos)}, something whispered my name. I never told anyone here my name.");
        }

        private IEnumerator Knocking()
        {
            var door = Door.All
                .OrderBy(d => Vector3.Distance(d.transform.position, player.position)).FirstOrDefault();
            if (door == null || Vector3.Distance(door.transform.position, player.position) > 15f) yield break;
            GameAudio.PlayAt("knock_slow", door.transform.position, 1f, 1f, 25f);
            Nerve.Instance?.Shock(6f);
            yield return new WaitForSeconds(2.5f);
            GameAudio.PlayAt("knock", door.transform.position, 1f, 0.9f, 25f);
            yield return new WaitForSeconds(6f);
            LogEvent("Knocking", $"Someone knocked at the door {LocationMarker.Describe(door.transform.position)}. Three, then three more. Nobody was there.");
        }

        private IEnumerator FootstepsBehind()
        {
            float until = Time.time + 9f;
            bool stopped = false;
            System.Action echo = () =>
            {
                if (stopped) return;
                Vector3 behind = player.position - Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized * 4f;
                GameAudio.PlayAt(GameAudio.Variant("footstep_dirt", 4), behind, 0.55f, 0.8f, 12f);
            };
            controller.Footstep += echo;
            yield return new WaitForSeconds(1f);
            while (Time.time < until)
            {
                // Turn around and they stop.
                if (controller.CurrentSpeed < 0.2f) { stopped = true; break; }
                yield return null;
            }
            controller.Footstep -= echo;
            Nerve.Instance?.Shock(5f);
            LogEvent("Footsteps", "Someone walked behind me, step for step. When I stopped, they stopped.");
        }

        /// <summary>A resident seen where they can't be: the mimic practising, or someone else's face it's trying on.</summary>
        private IEnumerator TwoPlaces()
        {
            var residents = Resident.All.Where(r => r.isActiveAndEnabled).ToList();
            if (residents.Count == 0) yield break;
            var subject = residents[Random.Range(0, residents.Count)];
            if (!TryGroundPoint(player.position, 30f, 40f, 40f, out var p)) yield break;
            if (Vector3.Distance(subject.transform.position, p) < 60f) yield break; // must be impossible

            var dbl = Apparition.Create(subject, ApparitionLook.Double, p, Facing(player.position, p));
            float life = 0f, seen = 0f;
            bool logged = false;
            while (dbl != null && life < 35f)
            {
                life += Time.deltaTime;
                dbl.transform.position += dbl.transform.forward * 0.6f * Time.deltaTime;
                if (dbl.IsSeenBy(cam, 45f, 0.85f))
                {
                    seen += Time.deltaTime;
                    if (seen > 0.8f && !logged)
                    {
                        logged = true;
                        var clock = GameClock.Instance;
                        string where = LocationMarker.Describe(dbl.transform.position);
                        Notebook.Instance?.Record(NoteKind.Sighting, subject.DisplayName, "Seen?", $"I think I saw {subject.FirstName} {where}, {GameClock.FormatHour(clock.Hour)}, walking away from me.", null, $"double|{subject.DisplayName}|{clock.Day}|{clock.Phase}");
                        PlayerHUD.Instance?.Toast($"<i>Was that {subject.FirstName}?</i>");
                    }
                }
                if (Vector3.Distance(player.position, dbl.transform.position) < 15f) break;
                yield return null;
            }
            dbl?.Vanish();
        }

        private IEnumerator BackwardsWalker()
        {
            if (!TryGroundPoint(player.position, 28f, 32f, 25f, out var p)) yield break;
            var shade = Apparition.Create(WornFace(), ApparitionLook.Shade, p, Facing(player.position, p)); // facing away
            AmbientAudio.Hush = 1f;
            bool seenOnce = false;
            float life = 0f;
            while (shade != null && life < 40f)
            {
                life += Time.deltaTime;
                Vector3 to = Vector3.ProjectOnPlane(player.position - shade.transform.position, Vector3.up);
                if (to.magnitude < 10f) break;
                shade.transform.position += to.normalized * 1.1f * Time.deltaTime; // walks toward you, backwards
                if (!seenOnce && shade.IsSeenBy(cam, 40f, 0.9f)) { seenOnce = true; Nerve.Instance?.Shock(10f); }
                yield return null;
            }
            if (seenOnce) GameAudio.Play("stinger_1", 0.6f);
            shade?.Vanish();
            AmbientAudio.Hush = 0f;
            if (seenOnce) LogEvent("Walking backwards", "Something came up the road toward me with its back to me. It never turned around. At ten paces it simply wasn't there.");
        }

        private IEnumerator FaceAtWindow()
        {
            windowCache ??= FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude).Where(m => m.name == "Window").ToArray();
            var windows = windowCache
                .Where(r => r.name == "Window" && Vector3.Distance(r.transform.position, player.position) < 12f)
                .OrderByDescending(r => Vector3.Dot(cam.transform.forward, (r.transform.position - cam.transform.position).normalized)).ToList();
            if (windows.Count == 0) yield break;
            var window = windows[0].transform;
            Vector3 outward = window.parent.forward; // building fronts face +Z
            Vector3 pos = window.position + outward * 0.45f - Vector3.up * 1.55f;
            var face = Apparition.Create(WornFace(), ApparitionLook.Double, pos, Quaternion.LookRotation(-outward));
            face.Avatar.Rig?.Tint("HeadShape", new Color(0.8f, 0.8f, 0.76f));
            float life = 0f, seen = 0f;
            while (face != null && life < 8f)
            {
                life += Time.deltaTime;
                if (Vector3.Dot(cam.transform.forward, (window.position - cam.transform.position).normalized) > 0.85f) seen += Time.deltaTime;
                if (seen > 0.6f) break;
                yield return null;
            }
            if (seen > 0.6f)
            {
                GameAudio.Play("stinger_3", 0.75f);
                Nerve.Instance?.Shock(14f);
                LogEvent("A face at the window", $"Someone was pressed against the window {LocationMarker.Describe(window.position)}, looking in at me. Pale. Smiling. It was a face I knew.");
            }
            face?.Vanish();
        }

        /// <summary>
        /// The diner lights up after close, jukebox going, door unlocked. Go in and the music stops with the door,
        /// the lights are out, and every stool from the counter is standing in a ring in the middle of the floor.
        /// Don't, and it's dark again by the time you cross the road.
        /// </summary>
        private IEnumerator AfterHours()
        {
            var hours = diner.GetComponentInChildren<World.BusinessHours>();
            var windows = diner.GetComponentsInChildren<MeshRenderer>().Where(m => m.name == "Window").ToList();
            var stools = diner.GetComponentsInChildren<Transform>().Where(t => t.name == "Stool").ToList();
            var block = new MaterialPropertyBlock();
            block.SetColor(Shader.PropertyToID("_EmissionColor"), new Color(1f, 0.72f, 0.38f) * 1.1f);
            foreach (var w in windows) w.SetPropertyBlock(block);
            if (hours != null) hours.Override = true;
            var music = GameAudio.Loop("jukebox_loop", diner, 0.3f, 34f);
            float started = Time.time;
            bool entered = false;
            while (Time.time - started < 80f)
            {
                bool inside = hours != null ? hours.Inside(player.position) : Vector3.Distance(player.position, diner.position) < 5f;
                if (inside) { entered = true; break; }
                yield return null;
            }
            if (music != null) Destroy(music.gameObject);
            Rendering.DayNightCycle.Instance?.RefreshWindows();
            if (!entered)
            {
                if (hours != null) hours.Override = false;
                Nerve.Instance?.Shock(4f);
                LogEvent("Lights at Mae's", $"Mae's was lit up at {GameClock.Instance.Label}, hours after close, and the jukebox was going. By the time I got across the road it was dark and the door was locked.");
                yield break;
            }
            // Inside: dark, silent, and the stools have been arranged.
            var lamps = diner.GetComponentsInChildren<Light>().Where(l => l.enabled).ToList();
            foreach (var l in lamps) l.enabled = false;
            GameAudio.Play("stinger_1", 0.6f);
            Nerve.Instance?.Shock(10f);
            var homes = stools.Select(s => (s.position, s.rotation)).ToList();
            Vector3 ringCenter = diner.TransformPoint(new Vector3(0f, 0f, 0.9f));
            for (int i = 0; i < stools.Count; i++)
            {
                float a = i / (float)stools.Count * Mathf.PI * 2f;
                var p = ringCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.25f;
                stools[i].position = new Vector3(p.x, stools[i].position.y, p.z);
            }
            PlayerHUD.Instance?.ShowMessage("The jukebox stops the moment the door opens. The lights are off. Every stool from the counter is standing in a ring in the middle of the floor.", 6.5f);
            LogEvent("Mae's, after hours", $"Mae's was lit at {GameClock.Instance.Label}, long after close, the jukebox going and the door open. When I went in the music stopped and the lights were out. Every stool from the counter had been set in a ring in the middle of the floor. Nobody was there. I didn't stay to count them.");
            float t = 0f;
            while (t < 30f && Vector3.Distance(player.position, diner.position) < 9f) { t += Time.deltaTime; yield return null; }
            for (int i = 0; i < stools.Count; i++) stools[i].SetPositionAndRotation(homes[i].position, homes[i].rotation);
            foreach (var l in lamps) if (l != null) l.enabled = true;
            if (hours != null) hours.Override = false;
        }

        private IEnumerator LightsOut()
        {
            var near = LampSwitch.Lamps.Where(l => Vector3.Distance(l.transform.position, player.position) < 50f).ToList();
            if (near.Count == 0) yield break;
            foreach (var l in near) l.Kill(15f);
            Player.Flashlight.Instance?.Kill(15f);
            GameAudio.Play("lamp_die", 0.7f);
            AmbientAudio.Hush = 1f;
            Nerve.Instance?.Shock(10f);
            GameAudio.PlayAt("whisper_loop", player.position + cam.transform.right * 3f, 0.6f, 0.8f, 15f);
            yield return new WaitForSeconds(14.5f);
            if (TryGroundPoint(player.position, 12f, 16f, 30f, out var p))
            {
                var shade = Apparition.Create(WornFace(), ApparitionLook.Shade, p, Facing(p, player.position));
                yield return new WaitForSeconds(1.0f);
                shade.Vanish();
            }
            AmbientAudio.Hush = 0f;
            LogEvent("The lights went out", "Every lamp around me died at once. When they came back on, someone had been standing much closer than before.");
        }

        private static PlanarMirror NearestMirror(float range)
        {
            var cam = Camera.main;
            if (cam == null) return null;
            return PlanarMirror.All.Where(m => Vector3.Distance(m.transform.position, cam.transform.position) < range)
                .OrderBy(m => Vector3.Distance(m.transform.position, cam.transform.position)).FirstOrDefault();
        }

        private IEnumerator MirrorGhost()
        {
            var mirror = NearestMirror(5f);
            if (mirror == null) yield break;
            float wait = 0f;
            while (Vector3.Dot(cam.transform.forward, (mirror.transform.position - cam.transform.position).normalized) < 0.8f && wait < 20f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            if (wait >= 20f) yield break;
            Vector3 behind = player.position - Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized * 1.3f;
            var ghost = Apparition.Create(WornFace(), ApparitionLook.Double, behind, Facing(behind, player.position), Layers.MirrorGhost);
            GameAudio.Play("sting_soft", 0.6f);
            Nerve.Instance?.Shock(12f);
            yield return new WaitForSeconds(1.6f);
            ghost.Vanish();
            LogEvent("In the mirror", "In the mirror, someone was standing right behind me. I spun round. Nobody. In the mirror, still nobody, now.");
        }

        // ------------------------------------------------------------ While asleep / low nerve

        private IEnumerator SleepEvent()
        {
            int tier = Tier;
            var fader = ScreenFader.Instance;
            bool plotting = MimicAwareness.Instance != null && MimicAwareness.Instance.IsPlotting;
            if ((plotting || (tier >= 1 && Random.value < 0.55f)) && GameFlow.Instance != null && GameFlow.Instance.WakeSpot != null && Mimic != null)
            {
                var spot = GameFlow.Instance.WakeSpot;
                GameFlow.Instance.PlacePlayerAtWakeSpot();
                var shade = Apparition.Create(WornFace(), ApparitionLook.Shade, spot.position + spot.forward * 2.2f, Quaternion.LookRotation(-spot.forward));
                yield return new WaitForSeconds(0.5f);
                fader?.SetBlack(0.35f);
                GameAudio.Play("heartbeat", 0.8f);
                PlayerHUD.Instance?.ShowMessage("<i>3:12 AM. Someone is standing at the foot of the bed.</i>", 2.5f);
                yield return new WaitForSeconds(2.6f);
                GameAudio.Play("stinger_1", 0.8f);
                fader?.SetBlack(1f);
                shade.Vanish();
                yield return new WaitForSeconds(1f);
                LogEvent("Someone in my room", "I woke at 3:12 AM. Someone was standing at the foot of the bed, perfectly still, watching me sleep. When I blinked they were gone. The door was still locked.");
            }
            else if (Random.value < 0.3f)
            {
                GameAudio.Play("knock_slow", 0.7f);
                yield return new WaitForSeconds(2.5f);
                LogEvent("Knocking in the night", "Three slow knocks on the door of Room 6 at 2:14 AM. When I opened it, nobody was there. The gravel outside was undisturbed.");
            }
        }

        /// <summary>Something in the corner of your eye (low nerve). Never logged; it isn't real.</summary>
        public void FalseSighting()
        {
            if (!Ready() || Mimic == null || busy) return;
            float side = Random.value < 0.5f ? -1f : 1f;
            Vector3 dir = Quaternion.Euler(0, side * Random.Range(55f, 75f), 0) * Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector3 p = player.position + dir * Random.Range(8f, 14f);
            if (!NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) return;
            var shade = Apparition.Create(WornFace(), ApparitionLook.Shade, hit.position, Facing(hit.position, player.position));
            StartCoroutine(VanishSoon(shade, 0.7f));
        }

        private IEnumerator VanishSoon(Apparition a, float seconds)
        {
            float t = 0;
            while (a != null && t < seconds && !a.IsSeenBy(cam, 30f, 0.95f))
            {
                t += Time.deltaTime;
                yield return null;
            }
            a?.Vanish();
        }
    }
}
