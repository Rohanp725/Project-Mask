using Semblance.Core;
using Semblance.Dialogue;
using UnityEngine;
using UnityEngine.AI;

namespace Semblance.Residents
{
    /// <summary>
    /// Moves a resident through their daily schedule (one spot per phase of the day). Short trips are walked on
    /// the navmesh; long trips happen out of sight. Stops while talking to the detective.
    /// Other systems (night events, stalking) can take control with <see cref="Override"/>.
    /// </summary>
    [RequireComponent(typeof(Resident), typeof(NavMeshAgent))]
    public class ResidentMover : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 1.3f;

        private Resident resident;
        private NavMeshAgent agent;
        private Transform player;
        private ScheduleSpot target;
        private string pendingSpot;
        private float departAt;
        private bool overridden;

        public bool IsWalking => agent != null && agent.enabled && agent.hasPath && agent.velocity.sqrMagnitude > 0.02f;
        public Vector3 Velocity => agent != null && agent.enabled ? agent.velocity : Vector3.zero;
        public ScheduleSpot CurrentSpot => target;

        private void Awake()
        {
            resident = GetComponent<Resident>();
            agent = GetComponent<NavMeshAgent>();
            agent.speed = walkSpeed;
        }

        private void Start()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            var clock = GameClock.Instance;
            if (clock != null)
            {
                clock.PhaseChanged += OnPhaseChanged;
                clock.DayStarted += _ => { if (this != null && isActiveAndEnabled) GoTo(SpotFor(clock.Phase), instant: true); };
                GoTo(SpotFor(clock.Phase), instant: true);
            }
        }

        private void OnDestroy()
        {
            if (GameClock.Instance != null)
                GameClock.Instance.PhaseChanged -= OnPhaseChanged;
        }

        /// <summary>Re-check where they should be (a story beat started or ended).</summary>
        public void Reevaluate()
        {
            if (overridden || GameClock.Instance == null) return;
            pendingSpot = SpotFor(GameClock.Instance.Phase);
            departAt = Time.time + Random.Range(1f, 12f);
        }

        public string SpotFor(DayPhase phase)
        {
            var beat = Case.StoryBeats.Instance != null ? Case.StoryBeats.Instance.OverrideSpot(resident) : null;
            if (beat != null) return beat;
            var schedule = resident.Profile.schedule;
            if (schedule == null || schedule.Length == 0) return null;
            // The first evening the town stays out until half past ten, so a new arrival has someone to talk to.
            var clock = GameClock.Instance;
            if (phase == DayPhase.Night && clock != null && clock.Day == 1 && clock.Hour >= 18f && clock.Hour < 22.5f)
                phase = DayPhase.Dusk;
            int i = Mathf.Clamp((int)phase, 0, schedule.Length - 1);
            return schedule[i];
        }

        private void OnPhaseChanged(DayPhase phase)
        {
            pendingSpot = SpotFor(phase);
            departAt = Time.time + Random.Range(2f, 25f);
        }

        /// <summary>Takes the resident off their schedule (night events). Call Release to hand control back.</summary>
        public void Override(Vector3 position, Quaternion rotation)
        {
            overridden = true;
            Teleport(position, rotation);
        }

        public void WalkTo(Vector3 position)
        {
            overridden = true;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(position);
            }
        }

        public void Release()
        {
            overridden = false;
            var clock = GameClock.Instance;
            if (clock != null) GoTo(SpotFor(clock.Phase), instant: !VisibleToPlayer());
        }

        public void GoTo(string spotId, bool instant)
        {
            pendingSpot = null;
            var spot = ScheduleSpot.Find(spotId);
            if (spot == null) return;
            target = spot;
            if (instant || !agent.enabled)
                Teleport(spot.transform.position, spot.transform.rotation);
            else if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(spot.transform.position);
            }
            else
                Teleport(spot.transform.position, spot.transform.rotation);
        }

        private void Teleport(Vector3 position, Quaternion rotation)
        {
            if (agent.enabled && NavMesh.SamplePosition(position, out var hit, 2f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else
                transform.position = position;
            transform.rotation = rotation;
            if (agent.enabled && agent.isOnNavMesh)
                agent.ResetPath();
        }

        private bool VisibleToPlayer()
        {
            if (player == null) return false;
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 head = transform.position + Vector3.up * 1.6f;
            if (Vector3.Distance(cam.transform.position, head) > 45f) return false;
            var vp = cam.WorldToViewportPoint(head);
            return vp.z > 0 && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
        }

        private float DistanceToPlayer => player != null ? Vector3.Distance(player.position, transform.position) : 999f;

        private Vector3 lastStepPos;
        private float walked;

        /// <summary>Soft footsteps as they walk, on whatever they're walking on, so people are heard before they're seen.</summary>
        private void StepSounds()
        {
            if (!IsWalking) { lastStepPos = transform.position; return; }
            walked += Vector3.Distance(transform.position, lastStepPos);
            lastStepPos = transform.position;
            if (walked < 0.72f) return;
            walked = 0f;
            var surface = Audio.FootstepAudio.Detect(transform.position);
            AudioClip clip = Audio.FootstepAudio.StepClip(surface);
            Audio.GameAudio.PlayAt(clip, transform.position + Vector3.up * 0.1f, 0.16f, Random.Range(0.88f, 1.0f), 14f, Audio.AudioBus.World);
        }

        private void Update()
        {
            StepSounds();
            if (!agent.enabled) return;

            bool talking = DialogueUI.Instance != null && DialogueUI.Instance.Current == resident;
            if (agent.isOnNavMesh)
                agent.isStopped = talking;
            if (talking || overridden) return;

            if (pendingSpot != null && Time.time >= departAt)
            {
                var spot = ScheduleSpot.Find(pendingSpot);
                bool farTrip = spot != null && Vector3.Distance(spot.transform.position, transform.position) > 70f;
                bool unseen = !VisibleToPlayer() && DistanceToPlayer > 35f;
                bool destinationUnseen = spot != null && player != null && Vector3.Distance(player.position, spot.transform.position) > 35f;
                bool churchBound = pendingSpot.StartsWith("Church:");
                GoTo(pendingSpot, instant: unseen && (farTrip || destinationUnseen) && !churchBound);
            }

            // Mid-walk, skip the rest of a long journey once nobody's watching. Church-bound, they skip to the road
            // outside and walk in through the door like people do.
            if (target != null && agent.hasPath && agent.remainingDistance > 40f && !VisibleToPlayer() && DistanceToPlayer > 40f
                && Vector3.Distance(player != null ? player.position : Vector3.zero, target.transform.position) > 40f)
            {
                if (target.Id != null && target.Id.StartsWith("Church:") && Vector3.Distance(transform.position, target.transform.position) > 45f)
                {
                    Vector3 approach = target.transform.position + Vector3.right * 30f;
                    if (NavMesh.SamplePosition(approach, out var hit, 6f, NavMesh.AllAreas)) { agent.Warp(hit.position); agent.SetDestination(target.transform.position); }
                }
                else
                    Teleport(target.transform.position, target.transform.rotation);
            }

            // Settle into the spot's facing once arrived.
            if (target != null && !agent.pathPending && agent.isOnNavMesh && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
                transform.rotation = Quaternion.Slerp(transform.rotation, target.transform.rotation, 1f - Mathf.Exp(-4f * Time.deltaTime));
        }
    }
}
