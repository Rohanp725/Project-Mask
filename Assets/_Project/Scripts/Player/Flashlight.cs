using Semblance.Core;
using Semblance.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Semblance.Player
{
    /// <summary>
    /// A 1980s flashlight. F toggles it. Batteries run down (spares at the general store), it stutters when they're
    /// low, and frightening things make it die for a moment. Lets you see faces after dark.
    /// </summary>
    public class Flashlight : MonoBehaviour
    {
        private int shownPercent = -2; // the label is only rebuilt when the number changes

        public static Flashlight Instance { get; private set; }

        [SerializeField] private Camera viewCamera;
        [SerializeField, Range(0, 100)] private float battery = 100f;
        [SerializeField] private float drainPerSecond = 0.17f;

        public bool IsOn { get; private set; }
        public float Battery => battery;

        private Light beam;
        private float baseIntensity = 18f;
        private float deadUntil;
        private TextMeshProUGUI label;

        private void Awake()
        {
            Instance = this;
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            var go = new GameObject("Flashlight Beam");
            go.transform.SetParent(viewCamera.transform, false);
            go.transform.localPosition = new Vector3(0.18f, -0.15f, 0.1f);
            go.transform.localRotation = Quaternion.Euler(5f, 0f, 0f);
            beam = go.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.range = 24f;
            beam.spotAngle = 48f;
            beam.innerSpotAngle = 20f;
            beam.intensity = baseIntensity;
            beam.color = new Color(1f, 0.93f, 0.78f);
            beam.shadows = LightShadows.None;
            beam.enabled = false;

            var canvas = UIFactory.Canvas("Flashlight Canvas", transform, 11);
            label = UIFactory.Text("Battery", canvas.transform, "", 14, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.TopRight);
            UIFactory.Place(label.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -52), new Vector2(240, 22)); // under the clock and its date
        }

        private void Start()
        {
            Save.SaveSystem.Register(d => d.battery = battery, d => battery = d.battery > 0 ? d.battery : 100f);
        }

        public void AddBattery(float amount) => battery = Mathf.Min(100f, battery + amount);

        /// <summary>The light dies for a while (fear, night events).</summary>
        public void Kill(float seconds) => deadUntil = Time.time + seconds;

        private void Update()
        {
            if (GameInput.Pressed(GameInput.Flashlight) && (!InputLock.IsLocked || InputLock.Top is Events.HidingSpot))
            {
                IsOn = !IsOn && battery > 0f;
                Audio.GameAudio.Play(IsOn || battery <= 0f ? "flashlight_click" : "flashlight_click_off", 0.6f, battery > 0f ? 1f : 0.7f);
                if (battery <= 0f) PlayerHUD.Instance?.Toast("The flashlight's dead. Otis sells batteries at the general store.");
            }

            if (IsOn)
            {
                battery = Mathf.Max(0f, battery - drainPerSecond * Time.deltaTime);
                if (battery <= 0f)
                {
                    IsOn = false;
                    PlayerHUD.Instance?.Toast("The flashlight dies. Otis sells batteries at the general store.");
                }
            }

            bool dead = Time.time < deadUntil;
            bool covered = InputLock.IsLocked && !(InputLock.Top is Dialogue.DialogueUI) && !(InputLock.Top is Events.HidingSpot);
            beam.enabled = IsOn && !dead && !covered;
            if (beam.enabled)
            {
                // Low batteries stutter.
                float flicker = battery < 20f && Random.value < 0.08f ? Random.Range(0.1f, 0.6f) : 1f;
                beam.intensity = baseIntensity * Mathf.Lerp(0.45f, 1f, battery / 100f) * flicker;
            }
            // Hidden under full-screen screens (accusation, notebook, archive, endings), like the rest of the HUD.
            int shown = !covered && (IsOn || battery < 30f) ? Mathf.CeilToInt(battery) : -1;
            if (shown != shownPercent)
            {
                shownPercent = shown;
                label.text = shown < 0 ? "" : shown == 0 ? "FLASHLIGHT DEAD" : $"FLASHLIGHT {shown}%";
            }
        }
    }
}
