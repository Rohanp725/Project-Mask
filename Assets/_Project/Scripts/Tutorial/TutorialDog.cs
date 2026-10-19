using Semblance.Audio;
using UnityEngine;
using UnityEngine.AI;

namespace Semblance.Tutorial
{
    /// <summary>
    /// Biscuit. In the dark she's two points of eye-shine and claws on concrete, coming down the aisle, stopping
    /// dead whenever the light's on her. With the lights up she's an old half-blind dog who's very pleased to see you.
    /// Built from boxes by the tutorial builder; this drives the legs, the tail and the sounds.
    /// </summary>
    public class TutorialDog : MonoBehaviour
    {
        private enum Mode { Idle, Walk, Follow, Sit }

        [SerializeField] private float walkSpeed = 2.1f;
        [SerializeField] private float chaseSpeed = 3.4f;

        private NavMeshAgent agent;
        private Mode mode = Mode.Idle;
        private Transform player;
        private Camera cam;
        private bool happy;
        private Transform[] legs;
        private Transform tail, head, body;
        private Vector3[] legRest;
        private Vector3 tailRest, headRest, bodyRest;
        private Quaternion tailRestRot;
        private AudioSource claws, pant;
        private float stride;

        public bool Arrived => agent != null && agent.enabled && !agent.pathPending && agent.remainingDistance < 0.35f;
        public float Speed => agent != null && agent.enabled ? agent.velocity.magnitude : 0f;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            body = transform.Find("Body");
            head = transform.Find("Head");
            tail = transform.Find("Tail");
            legs = new[] { transform.Find("Leg FL"), transform.Find("Leg FR"), transform.Find("Leg BL"), transform.Find("Leg BR") };
            legRest = new Vector3[legs.Length];
            for (int i = 0; i < legs.Length; i++) if (legs[i] != null) legRest[i] = legs[i].localPosition;
            if (tail != null) { tailRest = tail.localPosition; tailRestRot = tail.localRotation; }
            if (head != null) headRest = head.localPosition;
            if (body != null) bodyRest = body.localPosition;
        }

        private void Start()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            cam = Camera.main;
            claws = GameAudio.Loop("claws_loop", transform, 0f, 26f);
            pant = GameAudio.Loop("dog_pant_loop", transform, 0f, 11f);
        }

        /// <summary>Put her somewhere (on the navmesh) facing a direction, and switch her on.</summary>
        public void Appear(Vector3 at, Vector3 facing)
        {
            gameObject.SetActive(true);
            if (NavMesh.SamplePosition(at, out var hit, 2f, NavMesh.AllAreas)) at = hit.position;
            if (agent.enabled) agent.Warp(at); else transform.position = at;
            if (facing.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(facing, Vector3.up));
            mode = Mode.Idle;
            agent.isStopped = false;
        }

        public void Vanish() => gameObject.SetActive(false);

        public void WalkTo(Vector3 target, float speed = -1f)
        {
            mode = Mode.Walk;
            agent.isStopped = false;
            agent.stoppingDistance = 0.2f;
            agent.speed = speed > 0f ? speed : walkSpeed;
            if (NavMesh.SamplePosition(target, out var hit, 2f, NavMesh.AllAreas)) target = hit.position;
            agent.SetDestination(target);
        }

        /// <summary>Keep a few metres behind the player; freeze while the light's on her.</summary>
        public void Follow(float distance = 4.2f)
        {
            mode = Mode.Follow;
            agent.stoppingDistance = distance;
            agent.speed = chaseSpeed;
            agent.isStopped = false;
        }

        public void Stay()
        {
            mode = Mode.Idle;
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        }

        /// <summary>Lights on: she sits, and the tail goes.</summary>
        public void Sit()
        {
            mode = Mode.Sit;
            happy = true;
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            GameAudio.PlayAt("dog_bark", transform.position + Vector3.up * 0.4f, 0.8f, 1f, 25f);
        }

        /// <summary>Is she in the player's view, with a light on her?</summary>
        public bool Seen()
        {
            if (cam == null) return false;
            var to = transform.position + Vector3.up * 0.3f - cam.transform.position;
            float d = to.magnitude;
            if (d > 18f) return false;
            bool lit = Player.Flashlight.Instance != null && Player.Flashlight.Instance.IsOn;
            return lit && Vector3.Dot(cam.transform.forward, to / d) > 0.72f;
        }

        private void Update()
        {
            if (mode == Mode.Follow && player != null && agent.enabled && agent.isOnNavMesh)
            {
                bool seen = Seen();
                agent.isStopped = seen;
                if (!seen) agent.SetDestination(player.position);
            }

            float speed = Speed;
            // Legs: a trot, front-left with back-right.
            if (speed > 0.1f)
            {
                stride += Time.deltaTime * speed * 4.5f;
                for (int i = 0; i < legs.Length; i++)
                {
                    if (legs[i] == null) continue;
                    float phase = stride + (i == 0 || i == 3 ? 0f : Mathf.PI);
                    legs[i].localPosition = legRest[i] + new Vector3(0, Mathf.Max(0f, Mathf.Sin(phase)) * 0.05f, Mathf.Sin(phase) * 0.08f);
                }
                if (body != null) body.localPosition = bodyRest + Vector3.up * Mathf.Abs(Mathf.Sin(stride * 2f)) * 0.012f;
            }
            else
                for (int i = 0; i < legs.Length; i++) if (legs[i] != null) legs[i].localPosition = Vector3.Lerp(legs[i].localPosition, legRest[i], Time.deltaTime * 8f);

            if (tail != null)
            {
                float wag = happy ? Mathf.Sin(Time.time * 14f) * 35f : Mathf.Sin(Time.time * 1.3f) * 4f;
                tail.localRotation = tailRestRot * Quaternion.Euler(happy ? -20f : 25f, wag, 0);
            }
            if (head != null)
            {
                // Head low when hunting through the dark, up and nodding when she's pleased.
                var target = headRest + (mode == Mode.Sit ? new Vector3(0, 0.04f + Mathf.Sin(Time.time * 3f) * 0.01f, 0) : mode == Mode.Idle ? Vector3.zero : new Vector3(0, -0.05f, 0.02f));
                head.localPosition = Vector3.Lerp(head.localPosition, target, Time.deltaTime * 3f);
            }
            if (mode == Mode.Sit && body != null)
                body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(-14f, 0, 0), Time.deltaTime * 3f);

            if (claws != null) claws.volume = Mathf.MoveTowards(claws.volume, Mathf.Clamp01(speed / 2.2f) * 0.6f, Time.deltaTime * 3f);
            if (pant != null) pant.volume = Mathf.MoveTowards(pant.volume, happy ? 0.3f : mode == Mode.Idle ? 0f : 0.42f, Time.deltaTime);
        }

        private void OnDisable()
        {
            if (claws != null) claws.volume = 0f;
            if (pant != null) pant.volume = 0f;
        }
    }
}
