using System;
using Semblance.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Semblance.Player
{
    /// <summary>
    /// First-person walking controller: mouse look, walk/sprint/crouch, gravity and head bob.
    /// Set <see cref="InputLocked"/> while dialogue, the notebook or other menus are open.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraRoot;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 2.4f;
        [SerializeField] private float sprintSpeed = 4.2f;
        [SerializeField] private float crouchSpeed = 1.3f;
        [SerializeField] private float acceleration = 12f;
        [SerializeField] private float gravity = -20f;

        [Header("Crouch")]
        [SerializeField] private float standHeight = 1.75f;
        [SerializeField] private float crouchHeight = 1.1f;
        [SerializeField] private float eyeOffsetFromTop = 0.15f;
        [SerializeField] private float crouchTransitionSpeed = 8f;

        [Header("Look")]
        [SerializeField] private float maxPitch = 85f;

        [Header("Focus (hold right mouse)")]
        [SerializeField] private float focusFov = 28f;
        [SerializeField] private float focusSpeed = 8f;

        [Header("Head Bob")]
        [Tooltip("Bob cycles per meter walked; each cycle is two steps.")]
        [SerializeField] private float bobFrequency = 0.65f;
        [SerializeField] private float bobAmplitude = 0.035f;

        /// <summary>Raised once per step while walking (hook footstep audio here).</summary>
        public event Action Footstep;

        public bool InputLocked { get; set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public float CurrentSpeed => new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;

        public bool IsFocusing { get; private set; }

        private CharacterController controller;
        private Camera viewCamera;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private float pitch;
        private float currentHeight;
        private float bobPhase;
        private Vector3 bobOffset;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            viewCamera = cameraRoot.GetComponentInChildren<Camera>();
            currentHeight = standHeight;
            ApplyHeight();
        }

        private void Start()
        {
            SetCursorLocked(true);
        }

        private void Update()
        {
            HandleCursor();
            bool hasControl = !InputLocked && !InputLock.IsLocked && Cursor.lockState == CursorLockMode.Locked;
            // Inside a wardrobe you can't move, but you can turn your head to the slats or the floor.
            bool peeking = !InputLocked && InputLock.Top is Events.HidingSpot && Cursor.lockState == CursorLockMode.Locked;

            // Focus: squint at small details. Slows the look so you can hold steady on a face.
            IsFocusing = hasControl && GameInput.Held(GameInput.Focus);
            if (viewCamera != null)
            {
                float targetFov = IsFocusing ? focusFov : GameSettings.FieldOfView;
                viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-focusSpeed * Time.deltaTime));
            }

            if (hasControl || peeking)
                Look(GameInput.LookDelta * (viewCamera != null ? viewCamera.fieldOfView / GameSettings.FieldOfView : 1f));

            Move(hasControl);
            UpdateCrouch(hasControl);
            UpdateHeadBob();
        }

        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void HandleCursor()
        {
            // Esc is handled by UIRoot (pause menu). Clicking back into the game re-captures the mouse.
            if (!InputLocked && !InputLock.IsLocked && Cursor.lockState != CursorLockMode.Locked && GameInput.MouseClicked)
                SetCursorLocked(true);
        }

        private void Look(Vector2 delta)
        {
            float sensitivity = GameSettings.MouseSensitivity;
            float dy = GameSettings.InvertY ? -delta.y : delta.y;
            pitch = Mathf.Clamp(pitch - dy * sensitivity, -maxPitch, maxPitch);
            transform.Rotate(Vector3.up, delta.x * sensitivity, Space.Self);
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Move(bool hasControl)
        {
            Vector2 input = hasControl ? GameInput.MoveValue : Vector2.zero;
            input = Vector2.ClampMagnitude(input, 1f);

            IsSprinting = hasControl && !IsCrouching && GameInput.Held(GameInput.Sprint) && input.y > 0f;
            float speed = IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
            if (input.y < 0f) speed *= 0.55f; // walking backwards is slow and clumsy

            Vector3 target = transform.TransformDirection(new Vector3(input.x, 0f, input.y)) * speed;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, acceleration * Time.deltaTime);

            if (!controller.enabled) return; // in the car, hiding, travelling
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;

            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        private void UpdateCrouch(bool hasControl)
        {
            bool wantsCrouch = hasControl && GameInput.Held(GameInput.Crouch);
            if (!wantsCrouch && IsCrouching && HasCeilingAbove())
                wantsCrouch = true;
            IsCrouching = wantsCrouch;

            float targetHeight = IsCrouching ? crouchHeight : standHeight;
            currentHeight = Mathf.MoveTowards(currentHeight, targetHeight, crouchTransitionSpeed * Time.deltaTime);
            ApplyHeight();
        }

        private bool HasCeilingAbove()
        {
            float radius = controller.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (currentHeight - radius);
            return Physics.SphereCast(origin, radius, Vector3.up, out _, standHeight - currentHeight + 0.05f,
                ~0, QueryTriggerInteraction.Ignore);
        }

        private void ApplyHeight()
        {
            controller.height = currentHeight;
            controller.center = Vector3.up * (currentHeight * 0.5f);
            cameraRoot.localPosition = Vector3.up * (currentHeight - eyeOffsetFromTop) + (GameSettings.CameraMotion ? bobOffset : Vector3.zero);
        }

        private void UpdateHeadBob()
        {
            float speed = CurrentSpeed;
            if (controller.isGrounded && speed > 0.1f)
            {
                float previousPhase = bobPhase;
                bobPhase += speed * bobFrequency * Time.deltaTime;

                // One footstep per half cycle (each low point of the bob).
                if (Mathf.Floor(previousPhase * 2f + 0.25f) != Mathf.Floor(bobPhase * 2f + 0.25f))
                    Footstep?.Invoke();

                float strength = Mathf.Clamp01(speed / walkSpeed);
                bobOffset = new Vector3(
                    Mathf.Cos(bobPhase * Mathf.PI * 2f) * bobAmplitude * 0.5f,
                    Mathf.Sin(bobPhase * Mathf.PI * 4f) * bobAmplitude,
                    0f) * strength;
            }
            else
            {
                bobOffset = Vector3.Lerp(bobOffset, Vector3.zero, 10f * Time.deltaTime);
            }
            cameraRoot.localPosition = Vector3.up * (currentHeight - eyeOffsetFromTop) + (GameSettings.CameraMotion ? bobOffset : Vector3.zero);
        }
    }
}
