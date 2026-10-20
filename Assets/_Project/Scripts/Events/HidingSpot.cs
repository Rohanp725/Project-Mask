using Semblance.Core;
using Semblance.Interaction;
using UnityEngine;

namespace Semblance.Events
{
    /// <summary>
    /// A wardrobe or locker you can climb into. While inside, the hunter can't see you unless it watched you get in.
    /// The doors are louvred in the world, so you look out between the slats and can still look around.
    /// </summary>
    public class HidingSpot : Interactable
    {
        [SerializeField] private Transform inside;

        public static HidingSpot Occupied { get; private set; }
        public static bool PlayerHidden => Occupied != null;
        /// <summary>Where the player was hiding when the hunter last had eyes on them.</summary>
        public static Vector3 EnteredAt { get; private set; }
        /// <summary>When the detective climbed in (Time.time).</summary>
        public static float EnteredTime { get; private set; } = -999f;
        /// <summary>The frame the detective last climbed out: the same E press mustn't climb straight back in.</summary>
        public static int LastLeftFrame { get; private set; } = -100;

        private Transform player;
        private Vector3 returnPosition;
        private Quaternion returnRotation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics() { Occupied = null; }

        [SerializeField] private string hidePrompt = "Hide in the wardrobe";
        public override string Prompt => hidePrompt;

        [SerializeField] private string enterSound = "door_creak_2", exitSound = "door_creak_1";

        public void Configure(Transform insidePoint, string promptText = null, string enter = null, string exit = null)
        {
            inside = insidePoint;
            if (promptText != null) hidePrompt = promptText;
            if (enter != null) enterSound = enter;
            if (exit != null) exitSound = exit;
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (Occupied != null || Time.frameCount <= LastLeftFrame + 2) return;
            player = interactor.transform;
            returnPosition = player.position;
            returnRotation = player.rotation;
            var cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            player.SetPositionAndRotation(inside.position, inside.rotation);
            Occupied = this;
            EnteredAt = inside.position;
            EnteredTime = Time.time;
            InputLock.Push(this, Leave);
            Audio.GameAudio.PlayAt(enterSound, transform.position, 0.5f, enterSound == "door_creak_2" ? 1.3f : 1f, 15f);
            UI.PlayerHUD.Instance?.SetHint("Hiding. Hold your breath.", 4f);
            enteredFrame = Time.frameCount;
        }

        private int enteredFrame;
        private PlayerInteractor interactor;

        private void Update()
        {
            // The same key that got you in gets you out (Esc works too).
            // E climbs out, unless you're looking at something on the wardrobe floor (then E examines it).
            if (Occupied == this && Time.frameCount > enteredFrame + 2 && GameInput.Pressed(GameInput.Interact))
            {
                if (interactor == null) interactor = FindFirstObjectByType<PlayerInteractor>();
                if (interactor == null || interactor.Current == null) Leave();
            }
        }

        public void Leave()
        {
            if (Occupied != this) return;
            player.SetPositionAndRotation(returnPosition, returnRotation);
            var cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = true;
            Occupied = null;
            LastLeftFrame = Time.frameCount;
            InputLock.Pop(this);
            UI.PlayerHUD.Instance?.RestoreHint();
            UI.PlayerHUD.Instance?.SetPrompt(null);
            Audio.GameAudio.PlayAt(exitSound, transform.position, 0.5f, exitSound == "door_creak_1" ? 1.2f : 1f, 15f);
        }

    }
}
