using Semblance.Interaction;
using UnityEngine;

namespace Semblance.World
{
    /// <summary>A hinged door that swings open/closed when used. Put it on the door panel; it rotates the hinge.</summary>
    public class Door : Interactable
    {
        [SerializeField] private Transform hinge;
        [Tooltip("Positive swings the door inward (away from the building's front).")]
        [SerializeField] private float openAngle = 100f;
        [SerializeField] private float swingSpeed = 220f;
        [SerializeField] private bool locked;
        [SerializeField, TextArea(1, 3)] private string lockedMessage = "It's locked.";

        private Quaternion closedRotation;
        private float currentAngle;
        private bool isOpen;

        public override string Prompt => locked ? "Try door" : isOpen ? "Close door" : "Open door";

        private void Awake()
        {
            if (hinge == null)
                hinge = transform.parent;
            closedRotation = hinge.localRotation;
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (locked)
            {
                interactor.Hud.ShowMessage(lockedMessage, 4f);
                return;
            }
            isOpen = !isOpen;
            PlaySwing(isOpen);
        }

        private bool wasResidentOpen;

        private void PlaySwing(bool opening)
        {
            if (opening) Audio.GameAudio.PlayAt(Random.value < 0.5f ? "door_creak_1" : "door_creak_2", transform.position, 0.6f, Random.Range(0.9f, 1.1f), 18f);
            else Audio.GameAudio.PlayAt("door_close", transform.position, 0.5f, Random.Range(0.9f, 1.1f), 18f);
        }

        public void SetLocked(bool value) => locked = value;

        private float residentHoldUntil;
        private float nextResidentCheck;

        private static readonly System.Collections.Generic.List<Door> Registry = new System.Collections.Generic.List<Door>();
        /// <summary>Every door in the scene (no scene searches at runtime).</summary>
        public static System.Collections.Generic.IReadOnlyList<Door> All => Registry;
        private void OnEnable() => Registry.Add(this);
        private void OnDisable() => Registry.Remove(this);

        private void Update()
        {
            // Residents open doors as they pass through, then they swing shut behind them (checked a few times a second).
            if (!locked && Time.time >= nextResidentCheck)
            {
                nextResidentCheck = Time.time + 0.2f + Random.value * 0.05f;
                Vector3 center = hinge.position + hinge.right * 0.6f;
                foreach (var r in Residents.Resident.All)
                    if ((r.transform.position - center).sqrMagnitude < 2.6f)
                    {
                        residentHoldUntil = Time.time + 1.5f;
                        break;
                    }
            }
            bool residentOpen = Time.time < residentHoldUntil;
            if (residentOpen != wasResidentOpen && !isOpen)
                PlaySwing(residentOpen);
            wasResidentOpen = residentOpen;
            float target = isOpen || residentOpen ? openAngle : 0f;
            if (Mathf.Approximately(currentAngle, target))
                return;
            currentAngle = Mathf.MoveTowards(currentAngle, target, swingSpeed * Time.deltaTime);
            hinge.localRotation = closedRotation * Quaternion.Euler(0, currentAngle, 0);
        }
    }
}
