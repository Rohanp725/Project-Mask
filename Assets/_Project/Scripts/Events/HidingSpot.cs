using Semblance.Core;
using Semblance.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace Semblance.Events
{
    /// <summary>A wardrobe you can climb into. While inside, the hunter can't see you unless it watched you get in.</summary>
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
        private static GameObject slats;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics() { Occupied = null; slats = null; }

        [SerializeField] private string hidePrompt = "Hide in the wardrobe";
        public override string Prompt => hidePrompt;

        public void Configure(Transform insidePoint, string promptText = null)
        {
            inside = insidePoint;
            if (promptText != null) hidePrompt = promptText;
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
            ShowSlats(true);
            Audio.GameAudio.PlayAt("door_creak_2", transform.position, 0.5f, 1.3f, 15f);
            UI.PlayerHUD.Instance?.SetHint("Hiding. Hold your breath.", 0f);
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
            ShowSlats(false);
            UI.PlayerHUD.Instance?.SetHint(null);
            UI.PlayerHUD.Instance?.SetPrompt(null);
            Audio.GameAudio.PlayAt("door_creak_1", transform.position, 0.5f, 1.2f, 15f);
        }

        private static void ShowSlats(bool show)
        {
            if (slats == null)
            {
                // Louvred doors: dark slats with the room showing in the gaps between them, so what walks past can be seen.
                var canvas = UIFactory.Canvas("Hiding Canvas", null, 4);
                slats = canvas.gameObject;
                var layout = UIFactory.Rect("Slats", canvas.transform);
                UIFactory.Stretch(layout);
                var slat = new Color(0.02f, 0.015f, 0.01f, 0.97f);
                // Solid above and below the louvres, and down both sides.
                UIFactory.Place(UIFactory.Panel("Top", layout, slat, false).rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(2000, 150));
                UIFactory.Place(UIFactory.Panel("Bottom", layout, slat, false).rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(2000, 150));
                UIFactory.Place(UIFactory.Panel("Left", layout, slat, false).rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(300, 2000));
                UIFactory.Place(UIFactory.Panel("Right", layout, slat, false).rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(300, 2000));
                for (int i = 0; i < 9; i++)
                {
                    float y = 150f + i * 46f;
                    var bar = UIFactory.Panel("Slat", layout, slat, false);
                    UIFactory.Place(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, y), new Vector2(700, 34));
                    // The lit edge of each slat where the room's light catches it.
                    var lip = UIFactory.Panel("Slat Edge", layout, new Color(0.25f, 0.2f, 0.14f, 0.9f), false);
                    UIFactory.Place(lip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, y + 34f), new Vector2(700, 3));
                }
                var dim = UIFactory.Panel("Dim", layout, new Color(0, 0, 0, 0.35f), false);
                UIFactory.Stretch(dim.rectTransform);
                dim.transform.SetAsFirstSibling();
            }
            slats.SetActive(show);
        }
    }
}
