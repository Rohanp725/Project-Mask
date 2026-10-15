using System.Collections;
using Semblance.Audio;
using Semblance.Case;
using Semblance.Core;
using Semblance.Notes;
using Semblance.Residents;
using Semblance.UI;
using UnityEngine;
using UnityEngine.AI;

namespace Semblance.Events
{
    /// <summary>
    /// The hunt. The mimic drops its disguise and comes for the detective through the dark. It barely moves while
    /// you look at it and closes the distance fast when you don't. Break line of sight, hide in a wardrobe or crouch
    /// in the corn. While it hunts, the resident it's been pretending to be is nowhere to be found (a clue).
    /// </summary>
    public class Stalker : MonoBehaviour
    {
        private Player.FirstPersonController playerController;

        public static Stalker Active { get; private set; }

        private Apparition body;
        private NavMeshAgent agent;
        private Transform player;
        private Camera cam;
        private Resident disguise;
        private Vector3 lastKnown;
        private float lostSince = -1f;
        private float nextRepath;
        private float nextStep;
        private AudioSource music;
        private AudioSource breath;
        private bool killing;
        private float startedAt;
        private float lastSawPlayer = -999f;
        private bool wasHidden, foundHidingPlace;
        private float flinchUntil, nextFlinch;

        /// <summary>0 far away .. 1 on top of you. Drives the fear effects during a hunt instead of the nerve meter.</summary>
        public static float Closeness { get; private set; }
        public static bool IsForced => Active != null && Active.forced;
        public bool IsKilling => killing;

        public static bool IsHunting => Active != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics() => Active = null;

        private bool forced;
        /// <summary>The first night it only follows: at the edge of the light, never closing, gone after a minute.</summary>
        private bool stalkOnly;
        private float seenTotal;

        public static bool Begin(bool forced = false, bool stalkOnly = false)
        {
            if (Active != null || CaseDirector.Instance == null || CaseDirector.Instance.Mimic == null) return false;
            var cam = Camera.main;
            if (cam == null) return false;
            var player = cam.transform.root;

            // Spawn out of sight, 35-55 m away (further for a forced hunt), on the navmesh.
            Vector3 spawn = Vector3.zero;
            bool found = false;
            for (int i = 0; i < 30 && !found; i++)
            {
                Vector3 dir = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward;
                // A forced hunt (you found the real one) gives you a head start: it comes from further off.
                Vector3 p = player.position + dir * (forced ? Random.Range(75f, 95f) : Random.Range(35f, 55f));
                if (!NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas)) continue;
                if (Vector3.Dot(cam.transform.forward, (hit.position - cam.transform.position).normalized) > 0.3f) continue;
                spawn = hit.position;
                found = true;
            }
            if (!found) return false;

            // Whatever you were doing, you're not doing it now.
            if (Dialogue.DialogueUI.Instance != null && Dialogue.DialogueUI.Instance.Current != null)
                Dialogue.DialogueUI.Instance.Close();
            var go = new GameObject("The Hunter");
            var stalker = go.AddComponent<Stalker>();
            stalker.forced = forced;
            stalker.stalkOnly = stalkOnly && !forced;
            Active = stalker;
            stalker.Setup(spawn, player, cam);
            return true;
        }

        private void Setup(Vector3 spawn, Transform target, Camera camera)
        {
            player = target;
            cam = camera;
            startedAt = Time.time;
            disguise = CaseDirector.Instance.Mimic;
            disguise.gameObject.SetActive(false);

            // It doesn't hunt in the face it wears in town: the shape is borrowed from anyone, and it's black.
            // Once the body has been found, it wears the dead one's shape.
            var everyone = CaseDirector.Instance.Residents;
            var dead = StoryBeats.Instance != null && GameClock.Instance != null && GameClock.Instance.Day >= StoryBeats.BodyDay ? StoryBeats.Instance.Missing : null;
            var shape = dead != null ? dead : everyone.Count > 0 ? everyone[Random.Range(0, everyone.Count)] : disguise;
            body = Apparition.Create(shape, ApparitionLook.HuntForm, spawn, Quaternion.identity);
            body.transform.SetParent(transform, false);
            transform.position = spawn;
            body.transform.localPosition = Vector3.zero;

            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 2f;
            agent.angularSpeed = 600f;
            agent.acceleration = 30f;
            agent.stoppingDistance = 0.4f;
            agent.Warp(spawn);

            music = GameAudio.Loop("chase_loop", null, 0f);
            breath = GameAudio.Loop("breathing", transform, 0.8f, 14f, AudioBus.Cue);
            AmbientAudio.Hush = 1f;
            GameAudio.Play("stinger_2", 0.7f);
            PlayerHUD.Instance?.Toast("<i>The crickets have stopped.</i>");
            if (stalkOnly)
                Notebook.Instance?.Record(NoteKind.Event, Notebook.CaseSubject, "Something followed me",
                    "Something kept pace with me at the edge of the light. When I stopped, it stopped. It never came closer. It didn't need to.", null, "stalked");
            else
                Notebook.Instance?.Record(NoteKind.Event, Notebook.CaseSubject, "It hunted me",
                    "Something came for me in the dark. Black all over, a shape I almost knew. It moved when I didn't look at it.",
                    null, $"hunt|{GameClock.Instance?.Day}");
        }

        private bool PlayerSeesMe()
        {
            Vector3 head = transform.position + Vector3.up * 1.6f;
            Vector3 to = head - cam.transform.position;
            if (to.magnitude > 45f || Vector3.Dot(cam.transform.forward, to.normalized) < 0.55f) return false;
            return !Physics.Linecast(cam.transform.position, head, ~0, QueryTriggerInteraction.Ignore);
        }

        private bool ICanSeePlayer(out float distance)
        {
            distance = Vector3.Distance(transform.position, player.position);
            if (HidingSpot.PlayerHidden) return false;
            bool crouchingInCorn = SurfaceZone.InCorn(player.position) && (playerController != null ? playerController : (playerController = player.GetComponent<Player.FirstPersonController>())).IsCrouching;
            float range = crouchingInCorn ? 5f : 60f;
            if (distance > range) return false;
            Vector3 eye = transform.position + Vector3.up * 1.7f;
            return !Physics.Linecast(eye, cam.transform.position, ~0, QueryTriggerInteraction.Ignore);
        }

        private void Update()
        {
            if (killing || player == null) return;
            if (GameFlow.Instance != null && GameFlow.Instance.IsSleeping) return;
            var clock = GameClock.Instance;
            if (clock != null && !clock.IsDark && !forced) { End(); return; }
            // A daylight hunt: the day drains out of the sky over a few seconds.
            // The town's lamps snap on as it goes, so the road to the church stays readable: you have to get there.
            if (forced)
            {
                Rendering.DayNightCycle.HuntDarkness = Mathf.MoveTowards(Rendering.DayNightCycle.HuntDarkness, 0.55f, Time.deltaTime * 0.2f);
                Rendering.LampSwitch.ForceOn = true;
            }

            bool seen = PlayerSeesMe();
            bool hasTarget = ICanSeePlayer(out float distance);
            if (hasTarget)
            {
                lastKnown = player.position;
                lostSince = -1f;
                lastSawPlayer = Time.time;
            }
            else if (lostSince < 0) lostSince = Time.time;

            // A wardrobe only hides you if it didn't watch you climb in. In the forced hunt it won't be waited out.
            bool hidden = HidingSpot.PlayerHidden;
            if (hidden && !wasHidden)
                foundHidingPlace = HidingSpot.EnteredTime - lastSawPlayer < 2.5f;
            if (hidden && forced && Time.time - HidingSpot.EnteredTime > 45f)
                foundHidingPlace = true;
            if (!hidden) foundHidingPlace = false;
            wasHidden = hidden;
            if (hidden && foundHidingPlace && HidingSpot.Occupied != null)
            {
                agent.speed = 3.2f;
                agent.SetDestination(HidingSpot.Occupied.transform.position);
                if (Vector3.Distance(transform.position, HidingSpot.Occupied.transform.position) < 1.8f)
                {
                    GameAudio.PlayAt("door_creak_1", HidingSpot.Occupied.transform.position, 1f, 0.7f, 20f);
                    HidingSpot.Occupied.Leave();
                    StartCoroutine(Kill());
                }
                return;
            }

            // A flashlight beam full in the face makes it flinch, for a price.
            var torch = Player.Flashlight.Instance;
            if (seen && torch != null && torch.IsOn && torch.Battery > 6f && distance < 16f && Time.time > nextFlinch)
            {
                flinchUntil = Time.time + 1.4f;
                nextFlinch = Time.time + 7f;
                torch.AddBattery(-6f);
                GameAudio.PlayAt("creature_shriek", transform.position, 0.45f, 1.3f, 30f);
            }

            // It creeps while you watch it and runs when you don't. Backing away while staring is slower than it creeps.
            agent.speed = Time.time < flinchUntil ? 0f : seen ? 1.6f : distance > 20f ? 7f : 4.6f;
            if (stalkOnly)
            {
                // Following, not hunting: it keeps its distance and leaves once it's been seen, or after a minute.
                if (seen) seenTotal += Time.deltaTime;
                if (seenTotal > 3f || Time.time - startedAt > 60f) { End(); return; }
                if (Time.time > nextRepath)
                {
                    nextRepath = Time.time + 0.3f;
                    Vector3 away = (transform.position - player.position);
                    away.y = 0f;
                    Vector3 keep = player.position + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * 14f;
                    agent.speed = distance > 18f ? 4.6f : 1.2f;
                    agent.SetDestination(keep);
                }
                if (Nerve.Instance != null) Nerve.Instance.ExternalDrain = seen ? 2f : 0.5f;
                return;
            }
            if (Time.time > nextRepath)
            {
                nextRepath = Time.time + 0.25f;
                agent.SetDestination(hasTarget ? player.position : lastKnown);
            }

            if (Time.time > nextStep && agent.velocity.sqrMagnitude > 0.5f)
            {
                nextStep = Time.time + (seen ? 0.9f : 0.32f);
                GameAudio.PlayAt(GameAudio.Variant("creature_step", 3), transform.position, 0.8f, Random.Range(0.85f, 1.1f), 30f);
            }

            float closeness = 1f - Mathf.InverseLerp(4f, 35f, distance);
            Closeness = closeness;
            if (music) GameAudio.SetVolume(music, Mathf.Lerp(0.15f, 0.6f, closeness));
            // While it hunts, the world goes quiet around its footsteps.
            GameAudio.Duck(0.6f, 0.3f);
            if (Nerve.Instance != null) Nerve.Instance.ExternalDrain = seen ? 3f : 1f + closeness * 2f;

            if (hasTarget && distance < 1.4f && !HidingSpot.PlayerHidden)
            {
                StartCoroutine(Kill());
                return;
            }

            // It gives up after losing you for a while, or after a long hunt.
            if (forced)
            {
                // It knows what you found. It doesn't give up; it circles back round.
                if (lostSince > 0 && Time.time - lostSince > 25f) Reacquire();
            }
            else if ((lostSince > 0 && Time.time - lostSince > 30f) || Time.time - startedAt > 240f)
                End();
        }

        private IEnumerator Kill()
        {
            killing = true;
            agent.isStopped = true;
            InputLock.Push(this, null);
            GameAudio.Play("creature_shriek", 1f);
            GameAudio.Play("stinger_death", 0.9f);
            // Snap the view onto its face.
            var face = transform.position + Vector3.up * 1.7f;
            var camT = cam.transform;
            body.transform.position = camT.position + Vector3.ProjectOnPlane(camT.forward, Vector3.up).normalized * 0.7f - Vector3.up * 1.6f;
            body.transform.rotation = Quaternion.LookRotation(-Vector3.ProjectOnPlane(camT.forward, Vector3.up));
            for (float t = 0; t < 1.3f; t += Time.deltaTime)
            {
                if (GameSettings.CameraMotion)
                    camT.parent.localRotation = Quaternion.Euler(Random.Range(-3f, 3f), 0, Random.Range(-4f, 4f));
                yield return null;
            }
            if (music) Destroy(music.gameObject);
            InputLock.Pop(this);
            EndingDirector.Instance?.Play(EndingKind.Killed);
        }

        private void Reacquire()
        {
            lostSince = -1f;
            Vector3 behind = -Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            NavMeshHit hit = default;
            bool placed = false;
            for (int i = 0; i < 12 && !placed; i++)
            {
                Vector3 dir = Quaternion.Euler(0, Random.Range(-50f, 50f), 0) * behind;
                if (!NavMesh.SamplePosition(player.position + dir * Random.Range(30f, 40f), out hit, 6f, NavMesh.AllAreas)) continue;
                Vector3 head = hit.position + Vector3.up * 1.6f;
                placed = Vector3.Dot(cam.transform.forward, (head - cam.transform.position).normalized) < 0.2f
                         || Physics.Linecast(cam.transform.position, head, ~0, QueryTriggerInteraction.Ignore);
            }
            if (placed) agent.Warp(hit.position);
            lastKnown = player.position;
            GameAudio.PlayAt("creature_shriek", hit.position, 0.6f, 0.8f, 80f);
        }

        public void End()
        {
            if (Active != this) return;
            Active = null;
            Closeness = 0f;
            if (disguise != null) disguise.gameObject.SetActive(true);
            disguise?.GetComponent<ResidentMover>()?.Release();
            if (music) Destroy(music.gameObject);
            AmbientAudio.Hush = 0f;
            Rendering.DayNightCycle.HuntDarkness = 0f;
            Rendering.LampSwitch.ForceOn = false;
            if (Nerve.Instance != null) Nerve.Instance.ExternalDrain = 0f;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            InputLock.Pop(this);
            if (Active == this)
            {
                Active = null;
                Rendering.DayNightCycle.HuntDarkness = 0f;
            Rendering.LampSwitch.ForceOn = false;
            }
        }
    }
}
