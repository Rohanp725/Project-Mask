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

        private Transform player;
        private Vector3 returnPosition;
        private Quaternion returnRotation;
        private static GameObject slats;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics() { Occupied = null; slats = null; }

        public override string Prompt => "Hide in the wardrobe";

        public void Configure(Transform insidePoint) => inside = insidePoint;

        public override void Interact(PlayerInteractor interactor)
        {
            if (Occupied != null) return;
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
            UI.PlayerHUD.Instance?.SetHint("Hiding. Hold your breath. Press {cancel} to climb out.", 0f);
        }

        public void Leave()
        {
            if (Occupied != this) return;
            player.SetPositionAndRotation(returnPosition, returnRotation);
            var cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = true;
            Occupied = null;
            InputLock.Pop(this);
            ShowSlats(false);
            UI.PlayerHUD.Instance?.SetHint(null);
            Audio.GameAudio.PlayAt("door_creak_1", transform.position, 0.5f, 1.2f, 15f);
        }

        private static void ShowSlats(bool show)
        {
            if (slats == null)
            {
                var canvas = UIFactory.Canvas("Hiding Canvas", null, 4);
                slats = canvas.gameObject;
                var dark = UIFactory.Panel("Dark", canvas.transform, new Color(0, 0, 0, 0.92f), false);
                UIFactory.Stretch(dark.rectTransform);
                var layout = UIFactory.Rect("Slats", canvas.transform);
                UIFactory.Stretch(layout);
                for (int i = 0; i < 7; i++)
                {
                    var gap = UIFactory.Panel("Gap", layout, new Color(0, 0, 0, 0f), false);
                    UIFactory.Place(gap.rectTransform, new Vector2(0.5f, 0.3f + i * 0.06f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 6));
                }
                // The gaps are just less-dark strips; a real cut-out isn't needed at PS1 resolution.
                dark.color = new Color(0, 0, 0, 0.82f);
            }
            slats.SetActive(show);
        }
    }
}
