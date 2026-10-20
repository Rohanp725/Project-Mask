using System.Collections;
using Semblance.Core;
using Semblance.Interaction;
using Semblance.Player;
using Semblance.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Semblance.World
{
    /// <summary>
    /// The detective's car. E to get in or out; W/S to drive, A/D to steer, Space to brake, the mouse to look.
    /// Far faster than walking (and than anything chasing you), with headlights. At night it sometimes stalls.
    /// </summary>
    public class DetectiveCar : Interactable
    {
        public static DetectiveCar Instance { get; private set; }

        [SerializeField] private Transform seat;
        [SerializeField] private Light[] headlights;
        [SerializeField] private float maxSpeed = 15f;
        [SerializeField] private float reverseSpeed = 5f;
        [SerializeField] private float acceleration = 6f;
        [SerializeField] private float steerRate = 70f;
        [SerializeField] private Vector3 halfExtents = new Vector3(0.95f, 0.6f, 2.2f);

        public bool IsDriving { get; private set; }
        public float Speed => speed;

        private Transform player;
        private FirstPersonController controller;
        private Transform cameraRoot;
        private float speed;
        private float lookYaw, lookPitch;
        private float stalledUntil;
        private AudioSource engine;
        private int enteredFrame;

        public override string Prompt => Dead ? "It won't turn over" : "Get in the car";

        /// <summary>The engine's gone until morning (it died under you during a hunt).</summary>
        public bool Dead { get; private set; }

        /// <summary>Coughs, dies, the lights go, and a moment later you're out of it. It stays dead till morning.</summary>
        public void Die()
        {
            if (Dead) return;
            Dead = true;
            stalledUntil = float.PositiveInfinity;
            SetLights(false);
            if (engine) Destroy(engine.gameObject);
            Audio.GameAudio.PlayAt("car_stall", transform.position, 0.9f, 1f, 30f);
            if (IsDriving) StartCoroutine(DieAndGetOut());
        }

        private static IEnumerator EngineIn(AudioSource source)
        {
            yield return new WaitForSeconds(1.6f);
            for (float t = 0; t < 1.2f && source != null; t += Time.deltaTime)
            {
                Audio.GameAudio.SetVolume(source, Mathf.Lerp(0f, 0.35f, t / 1.2f));
                yield return null;
            }
            if (source != null) Audio.GameAudio.SetVolume(source, 0.35f);
        }

        private IEnumerator DieAndGetOut()
        {
            PlayerHUD.Instance?.ShowMessage("The engine coughs twice and dies. The headlights go with it. The key turns and nothing happens.", 4f);
            yield return new WaitForSeconds(2.6f);
            if (IsDriving) Exit();
            PlayerHUD.Instance?.ShowMessage("Nothing. It's not going anywhere, and neither are you unless you run.", 3.5f);
        }

        public void Configure(Transform seatPoint, Light[] lights)
        {
            seat = seatPoint;
            headlights = lights;
        }

        private Vector3 homePosition;
        private Quaternion homeRotation;

        private void Awake() => Instance = this;

        private void OnEnable() => Core.GameFlow.Woke += Revive;
        private void OnDisable() => Core.GameFlow.Woke -= Revive;

        private void Revive(int day)
        {
            Dead = false;
            stalledUntil = 0f;
        }

        private void Start()
        {
            homePosition = transform.position;
            homeRotation = transform.rotation;
        }

        /// <summary>Back in the motel lot, parked the way it was: where it is every morning.</summary>
        public void ReturnHome()
        {
            if (IsDriving) Exit();
            speed = 0f;
            transform.SetPositionAndRotation(homePosition, homeRotation);
            Physics.SyncTransforms();
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (IsDriving) return;
            if (Dead)
            {
                Audio.GameAudio.Play("flashlight_click", 0.5f, 0.6f);
                interactor.Hud.ShowMessage("The key turns. Nothing. The battery's flat, or something's been at the wires. It'll be morning before it goes anywhere.", 4f);
                return;
            }
            player = interactor.transform;
            controller = player.GetComponent<FirstPersonController>();
            cameraRoot = player.Find("Camera Root");
            controller.InputLocked = true;
            player.GetComponent<CharacterController>().enabled = false;
            IsDriving = true;
            enteredAt = player.position;
            enteredFrame = Time.frameCount;
            lookYaw = lookPitch = 0f;
            Audio.GameAudio.Play("car_door", 0.7f);
            // The starter and the first revs, then the idle takes over as the start fades.
            Audio.GameAudio.PlayAt("car_start", transform.position, 0.7f, 1f, 30f);
            engine = Audio.GameAudio.Loop("engine_loop", transform, 0f, 30f);
            StartCoroutine(EngineIn(engine));
            SetLights(true);
            SetCabinVisible(false);
            PlayerHUD.Instance?.SetHint("{move} drive and steer   {brake} brake   {interact} get out", 5f);
        }

        private Vector3 enteredAt;

        /// <summary>Out of the car right now (falling asleep at the wheel, an ending).</summary>
        public void ForceExit()
        {
            if (IsDriving) Exit();
        }

        private void Exit()
        {
            IsDriving = false;
            speed = 0f;
            SetLights(false);
            SetCabinVisible(true);
            if (engine) Destroy(engine.gameObject);
            Audio.GameAudio.Play("car_door", 0.7f);
            // Driver's side, then passenger's, then behind; failing all that, where you got in.
            Vector3 outside = enteredAt;
            foreach (var candidate in new[] { transform.position - transform.right * 2.2f, transform.position + transform.right * 2.2f, transform.position - transform.forward * 3.4f })
            {
                if (!NavMesh.SamplePosition(candidate, out var hit, 1.5f, NavMesh.AllAreas)) continue;
                if (Physics.CheckCapsule(hit.position + Vector3.up * 0.4f, hit.position + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                outside = hit.position;
                break;
            }
            player.SetPositionAndRotation(outside + Vector3.up * 0.05f, Quaternion.LookRotation(transform.forward));
            cameraRoot.localRotation = Quaternion.identity;
            player.GetComponent<CharacterController>().enabled = true;
            controller.InputLocked = false;
            Physics.SyncTransforms();
        }

        /// <summary>The cabin and glass would block the driver's view from inside, so they hide while driving.</summary>
        private void SetCabinVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.name == "Cabin" || r.name == "Windshield" || r.name.Contains("Window"))
                    r.enabled = visible;
        }

        private void SetLights(bool on)
        {
            if (headlights == null) return;
            foreach (var l in headlights) if (l != null) l.enabled = on;
        }

        /// <summary>The engine dies for a few seconds (night events).</summary>
        public void Stall(float seconds)
        {
            stalledUntil = Time.time + seconds;
            StartCoroutine(StallLights(seconds));
        }

        private IEnumerator StallLights(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                SetLights(Random.value < 0.3f);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.3f));
            }
            SetLights(IsDriving);
        }

        public bool IsStalled => Time.time < stalledUntil;

        private void Update()
        {
            if (!IsDriving) return;
            if (GameInput.Pressed(GameInput.Interact) && Time.frameCount > enteredFrame + 1 && Mathf.Abs(speed) < 3f && !InputLock.IsLocked)
            {
                Exit();
                return;
            }

            bool control = !InputLock.IsLocked && !IsStalled;
            Vector2 drive = control ? GameInput.MoveValue : Vector2.zero;
            float throttle = Mathf.Clamp(drive.y, -1f, 1f);
            float steer = Mathf.Clamp(drive.x, -1f, 1f);
            bool brake = GameInput.Held(GameInput.Brake) || IsStalled;

            if (brake) speed = Mathf.MoveTowards(speed, 0f, acceleration * 2.5f * Time.deltaTime);
            else if (throttle != 0f) speed = Mathf.Clamp(speed + throttle * acceleration * Time.deltaTime, -reverseSpeed, maxSpeed);
            else speed = Mathf.MoveTowards(speed, 0f, acceleration * 0.6f * Time.deltaTime);

            float turn = steer * steerRate * Mathf.Clamp(speed / 6f, -1f, 1f) * Time.deltaTime;
            transform.Rotate(0f, turn, 0f, Space.World);

            Vector3 move = transform.forward * speed * Time.deltaTime;
            if (move.sqrMagnitude > 0f)
            {
                Vector3 center = transform.position + Vector3.up * (halfExtents.y + 0.3f);
                if (Physics.BoxCast(center, halfExtents * 0.95f, move.normalized, out var hit, transform.rotation, move.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore)
                    && !hit.collider.transform.IsChildOf(transform) && hit.collider.name != "Ground Collider")
                {
                    if (Mathf.Abs(speed) > 4f) Audio.GameAudio.PlayAt("thud", hit.point, 0.8f, 1f, 20f);
                    speed = 0f;
                }
                else transform.position += move;
            }
            // Stick to the ground.
            if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out var ground, 5f, ~0, QueryTriggerInteraction.Ignore)
                && !ground.collider.transform.IsChildOf(transform))
                transform.position = new Vector3(transform.position.x, ground.point.y, transform.position.z);

            if (engine) engine.pitch = 0.8f + Mathf.Abs(speed) / maxSpeed * 0.9f;

            // Seat the detective and let them look around.
            Vector2 delta = InputLock.IsLocked ? Vector2.zero : GameInput.LookDelta * GameSettings.MouseSensitivity;
            lookYaw = Mathf.Clamp(lookYaw + delta.x, -110f, 110f);
            lookPitch = Mathf.Clamp(lookPitch - (GameSettings.InvertY ? -delta.y : delta.y), -40f, 40f);
            player.SetPositionAndRotation(seat.position - Vector3.up * 1.15f, transform.rotation * Quaternion.Euler(0, lookYaw, 0));
            cameraRoot.localRotation = Quaternion.Euler(lookPitch, 0, 0);
        }
    }
}
