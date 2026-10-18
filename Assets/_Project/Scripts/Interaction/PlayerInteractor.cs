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
            Current = InputLock.IsLocked || (controller != null && controller.InputLocked) ? null : FindTarget();
            hud.SetPrompt(Current != null ? Current.Prompt : null);

            if (Current == null || Cursor.lockState != CursorLockMode.Locked)
                return;

            if (Core.GameInput.Pressed(Core.GameInput.Interact))
                Current.Interact(this);
        }

        private Interactable FindTarget()
        {
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);

            // Precise ray first, then a thin sphere so small objects don't need pixel-perfect aim.
            if (Physics.Raycast(ray, out var hit, range, layers, QueryTriggerInteraction.Collide)
                && TryGetInteractable(hit, out var interactable))
                return interactable;
            if (Physics.SphereCast(ray, aimAssistRadius, out hit, range, layers, QueryTriggerInteraction.Collide)
                && TryGetInteractable(hit, out interactable))
                return interactable;
            return null;
        }

        private static bool TryGetInteractable(RaycastHit hit, out Interactable interactable)
        {
            interactable = hit.collider.GetComponentInParent<Interactable>();
            return interactable != null && interactable.CanInteract;
        }
    }
}
