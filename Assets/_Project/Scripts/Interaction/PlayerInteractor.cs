using Semblance.Core;
using Semblance.Player;
using Semblance.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Semblance.Interaction
{
    /// <summary>Raycasts from the camera to find interactables, shows their prompt, and triggers them on E.</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private PlayerHUD hud;
        [SerializeField] private FirstPersonController controller;
        [SerializeField] private float range = 2.6f;
        [Tooltip("Radius of the fallback sphere cast, so small objects are easier to target.")]
        [SerializeField] private float aimAssistRadius = 0.15f;
        [SerializeField] private LayerMask layers = ~0;

        public PlayerHUD Hud => hud;
        public FirstPersonController Controller => controller;
        public Interactable Current { get; private set; }

        private void Update()
        {
            if (hud.IsReading)
            {
                Current = null;
                hud.SetPrompt(null);
                if (Core.GameInput.Pressed(Core.GameInput.Interact)) hud.DismissReading();
                return;
            }
            // Inside a wardrobe you can still reach what's on its floor; otherwise a lock means no interaction.
            bool hiding = InputLock.Top is Events.HidingSpot;
            Current = (InputLock.IsLocked && !hiding) || (controller != null && controller.InputLocked) ? null : FindTarget();
            if (hiding && Current is Events.HidingSpot) Current = null;
            hud.SetPrompt(Current != null ? Current.Prompt : hiding ? "Climb out" : null);

            if (Current == null || Cursor.lockState != CursorLockMode.Locked)
                return;

            // The E that climbs out of a wardrobe is spent; it doesn't also open whatever's now in front of you.
            if (Core.GameInput.Pressed(Core.GameInput.Interact) && Time.frameCount > Events.HidingSpot.LastLeftFrame + 2)
                Current.Interact(this);
        }

        /// <summary>
        /// A paper on a desk sits inside the desk's crude box, a jar behind a counter's edge: the first thing a ray
        /// hits is often the furniture, not the thing on it. So the nearest interactable wins as long as nothing
        /// solid stands more than a furniture-edge's worth in front of it.
        /// </summary>
        private const float SeeThrough = 0.6f;

        private Interactable FindTarget()
        {
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            var best = Pick(Physics.RaycastAll(ray, range, layers, QueryTriggerInteraction.Collide));
            if (best != null) return best;
            // A thin sphere so small objects don't need pixel-perfect aim.
            return Pick(Physics.SphereCastAll(ray, aimAssistRadius, range, layers, QueryTriggerInteraction.Collide));
        }

        private Interactable Pick(RaycastHit[] hits)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            float blockedAt = float.PositiveInfinity;
            foreach (var hit in hits)
            {
                if (hit.collider.transform.root == transform) continue; // our own capsule
                if (TryGetInteractable(hit, out var interactable))
                    return hit.distance <= blockedAt + SeeThrough ? interactable : null;
                if (!hit.collider.isTrigger && hit.distance < blockedAt) blockedAt = hit.distance;
                if (hit.distance > blockedAt + SeeThrough) return null;
            }
            return null;
        }

        private static bool TryGetInteractable(RaycastHit hit, out Interactable interactable)
        {
            interactable = hit.collider.GetComponentInParent<Interactable>();
            return interactable != null && interactable.CanInteract;
        }
    }
}
