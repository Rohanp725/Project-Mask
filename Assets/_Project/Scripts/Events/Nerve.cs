using System.Collections.Generic;
using Semblance.Audio;
using Semblance.Core;
using Semblance.Rendering;
using Semblance.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Semblance.Events
{
    /// <summary>
    /// The detective's nerve (100 = steady, 0 = breaks). Darkness, scares and the hunt drain it; light, company
    /// and coffee restore it. Low nerve shows as a heartbeat, a closing vignette, a desaturated, grainy picture
    /// and, near the bottom, things in the corner of your eye that aren't there. At zero you black out.
    /// </summary>
    public class Nerve : MonoBehaviour
    {
        public static Nerve Instance { get; private set; }

        [SerializeField, Range(0, 100)] private float value = 100f;

        public float Value => value;
        public float Distress => 1f - value / 100f;
        /// <summary>Set by events while something frightening is in view (drains per second).</summary>
        public float ExternalDrain { get; set; }
        /// <summary>Coffee: awake longer, jumpier. Drains nerve all night, a little more per cup.</summary>
        public float CoffeeDrain { get; set; }

        private readonly List<Light> lights = new List<Light>();
        private float nextLightRefresh;
        private float nextHeartbeat;
        private float nextFalseSighting;
        private Image vignette;
        private Transform cameraRoot;
        private Vector3 shakeOffset;
        private bool fainting;

        public static string Describe(float v) => v > 75 ? "steady" : v > 50 ? "uneasy" : v > 25 ? "frightened" : "barely holding on";

        private void Awake() => Instance = this;

        private void Start()
        {
            BuildVignette();
            Save.SaveSystem.Register(d => d.nerve = value, d => value = d.nerve > 0 ? d.nerve : 100f);
            GameFlow.Woke += _ => { value = Mathf.Max(value, 85f); CoffeeDrain = 0f; };
        }

        /// <summary>When something last frightened the detective (Time.time).</summary>
        public float LastShockTime { get; private set; } = -999f;

        public void Shock(float amount)
        {
            value = Mathf.Clamp(value - amount, 0f, 100f);
            LastShockTime = Time.time;
        }
        public void Calm(float amount) => value = Mathf.Clamp(value + amount, 0f, 100f);

        /// <summary>Rough brightness at a point from nearby lamps and the time of day.</summary>
        public float LightLevel(Vector3 position)
        {
            if (Time.time > nextLightRefresh)
            {
                nextLightRefresh = Time.time + 15f; // lights are built with the town; this only catches the odd change
                lights.Clear();
                foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                    if (l.type == LightType.Point) lights.Add(l);
            }
            float level = DayNightCycle.CurrentAmbientLevel * 3f;
            if (Player.Flashlight.Instance != null && Player.Flashlight.Instance.IsOn) level += 1.2f;
            if (World.DetectiveCar.Instance != null && World.DetectiveCar.Instance.IsDriving) level += 1.5f;
            foreach (var l in lights)
            {
                if (l == null || !l.enabled) continue;
                float d = Vector3.Distance(l.transform.position, position);
                if (d > l.range) continue;
                float a = 1f - d / l.range;
                level += l.intensity * a * a * 0.25f;
            }
            return level;
        }

        private void Update()
        {
            var cam = Camera.main;
            if (cam == null || InputLock.IsLocked && !(InputLock.Top is Dialogue.DialogueUI)) { UpdateEffects(); return; }
            if (cameraRoot == null) cameraRoot = cam.transform.parent;

            float light = LightLevel(cam.transform.position);
            bool torch = Player.Flashlight.Instance != null && Player.Flashlight.Instance.IsOn;
            float rate = 0f;
            // The first night is gentler: the dark drains half as fast.
            float firstNight = GameClock.Instance != null && GameClock.Instance.Day == 1 ? 0.5f : 1f;
            if (light < 0.35f) rate -= 0.15f * firstNight;
            else if (torch && light - 1.2f < 0.35f) rate -= 0.06f * firstNight;
            else if (light > 1.1f) rate += 1.0f;
            if (light > 1.1f && AmbientAudio.IsIndoors(cam.transform.position - Vector3.up * 1.5f)) rate += 0.8f;
            rate -= ExternalDrain + CoffeeDrain;
            // While it's hunting you, the danger is being caught, not fainting: fear bottoms out but holds.
            // Nobody faints before they've slept once: the first night teaches the dark, it doesn't punish it.
            float floor = Stalker.IsHunting ? Mathf.Min(value, 5f) : 0f;
            if (!Save.SaveSystem.Flags.Contains("slept")) floor = Mathf.Max(floor, Mathf.Min(value, 15f));
            value = Mathf.Clamp(value + rate * Time.deltaTime, floor, 100f);

            if (value <= 0f && !fainting && GameFlow.Instance != null && !GameFlow.Instance.IsSleeping)
            {
                fainting = true;
                PlayerHUD.Instance?.ShowMessage("Your heart is going so fast it stops feeling like yours. The world tips over.", 3f);
                GameFlow.Instance.Collapse();
            }
            if (value > 20f) fainting = false;

            // The first time the heartbeat starts, say what it is.
            if (value < 60f && !Save.SaveSystem.Flags.Contains("hint:nerve") && Tutorial.TutorialStage.Instance == null) // the tutorial explains it itself
            {
                Save.SaveSystem.Flags.Add("hint:nerve");
                PlayerHUD.Instance?.SetHint("That's your heartbeat. The dark gets to you out here.\n<size=75%>Light calms you: lamps, lit rooms, Mae's coffee. Your flashlight ({flashlight}) helps a little.</size>", 9f);
            }

            // Things in the corner of your eye.
            if (value < 25f && Time.time > nextFalseSighting && EventDirector.Instance != null)
            {
                nextFalseSighting = Time.time + Random.Range(12f, 30f);
                EventDirector.Instance.FalseSighting();
            }
            UpdateEffects();
        }

        private void UpdateEffects()
        {
            float distress = Stalker.IsHunting ? Mathf.Lerp(0.35f, 1f, Stalker.Closeness) : Distress;
            // Fear shows at the edges and in the colour, but the road always stays readable.
            PS1ScreenEffect.Distress = Mathf.Clamp01((distress - 0.35f) / 0.65f) * 0.6f;
            DayNightCycle.DarknessBoost = Mathf.Clamp01((distress - 0.6f) / 0.4f) * 0.25f;
            if (vignette != null)
                vignette.color = new Color(0, 0, 0, Mathf.Clamp01((distress - 0.3f) * 1.1f) * 0.65f);

            if (distress > 0.4f && Time.time > nextHeartbeat)
            {
                float interval = Mathf.Lerp(1.3f, 0.42f, (distress - 0.4f) / 0.6f);
                nextHeartbeat = Time.time + interval;
                GameAudio.Play("heartbeat", Mathf.Lerp(0.15f, 0.7f, (distress - 0.4f) / 0.6f));
            }

            if (cameraRoot != null)
            {
                cameraRoot.localPosition -= shakeOffset;
                float shake = GameSettings.CameraMotion ? Mathf.Clamp01((distress - 0.6f) / 0.4f) * 0.012f : 0f;
                shakeOffset = new Vector3(Mathf.PerlinNoise(Time.time * 7f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * 7f) - 0.5f, 0f) * shake * 2f;
                cameraRoot.localPosition += shakeOffset;
            }
        }

        private void BuildVignette()
        {
            var canvas = UIFactory.Canvas("Nerve Canvas", transform, 5);
            vignette = UIFactory.Panel("Vignette", canvas.transform, new Color(0, 0, 0, 0), false);
            UIFactory.Stretch(vignette.rectTransform);
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.25f, d))));
            }
            tex.Apply();
            vignette.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
