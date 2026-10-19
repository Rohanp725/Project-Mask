using System.Collections.Generic;
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
        /// A paper on a desk sits inside the desk's crude box, a radio on a counter behind its edge: the first thing
        /// a ray hits is often the furniture, not the thing on it. So the ray may pass a piece of furniture when the
        /// thing it's aimed at lies within that piece's own bounds. Walls, partitions and cages are too tall to count
        /// as furniture, so nothing is reached through them.
        /// </summary>
        private const float FurnitureHeight = 2.3f;
        private const float FurnitureMargin = 0.15f;
        private const float SlabThickness = 0.3f; // anything thinner is a wall, a panel or a pane, not a piece of furniture

        private Interactable FindTarget()
        {
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            var best = Pick(Physics.RaycastAll(ray, range, layers, QueryTriggerInteraction.Collide));
            if (best != null) return best;
            // A thin sphere so small objects don't need pixel-perfect aim.
            return Pick(Physics.SphereCastAll(ray, aimAssistRadius, range, layers, QueryTriggerInteraction.Collide));
        }

        private readonly List<Collider> occluders = new List<Collider>();

        private Interactable Pick(RaycastHit[] hits)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            occluders.Clear();
            foreach (var hit in hits)
            {
                if (hit.collider.transform.root == transform) continue; // our own capsule
                if (TryGetInteractable(hit, out var interactable))
                {
                    foreach (var o in occluders)
                    {
                        var b = o.bounds;
                        b.Expand(FurnitureMargin * 2f);
                        if (!b.Contains(hit.point)) return null; // something in the way that it isn't sitting on
                    }
                    return interactable;
                }
                if (hit.collider.isTrigger) continue;
                var size = hit.collider.bounds.size;
                if (size.y > FurnitureHeight || Mathf.Min(size.x, size.z) < SlabThickness) return null; // a wall, a partition, a panel
                occluders.Add(hit.collider);
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
