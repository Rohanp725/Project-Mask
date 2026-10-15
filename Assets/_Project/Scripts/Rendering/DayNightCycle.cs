using System.Collections.Generic;
using System;
using Semblance.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Semblance.Rendering
{
    /// <summary>
    /// Drives fog, ambient light, the sun/moon and the camera background from the game clock.
    /// Semblance never gets bright: days are overcast and foggy, nights are black.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        [Serializable]
        private struct Key
        {
            public float hour;
            public Color fog;
            public float fogEnd;
            public Color ambient;
            public Color sunColor;
            public float sunIntensity;
            public float sunPitch;

            public Key(float hour, Color fog, float fogEnd, Color ambient, Color sunColor, float sunIntensity, float sunPitch)
            {
                this.hour = hour; this.fog = fog; this.fogEnd = fogEnd; this.ambient = ambient;
                this.sunColor = sunColor; this.sunIntensity = sunIntensity; this.sunPitch = sunPitch;
            }
        }

        [SerializeField] private Light sun;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float fogStart = 3f;

        /// <summary>Extra darkness multiplier other systems can apply (e.g. low nerve).</summary>
        public static float DarknessBoost { get; set; }

        /// <summary>The sky going wrong while it hunts you in daylight (0..1), on top of <see cref="DarknessBoost"/>.</summary>
        public static float HuntDarkness { get; set; }
        public static float CurrentAmbientLevel { get; private set; }

        private static readonly Key[] Keys =
        {
            new Key(6f,  new Color(0.17f, 0.19f, 0.22f), 30f, new Color(0.16f, 0.17f, 0.21f), new Color(0.6f, 0.62f, 0.7f), 0.25f, 8f),
            new Key(8f,  new Color(0.36f, 0.38f, 0.38f), 48f, new Color(0.3f, 0.31f, 0.32f),  new Color(0.85f, 0.85f, 0.82f), 0.55f, 20f),
            new Key(13f, new Color(0.42f, 0.44f, 0.43f), 60f, new Color(0.36f, 0.37f, 0.38f), new Color(0.9f, 0.9f, 0.86f), 0.7f, 50f),
            new Key(17f, new Color(0.36f, 0.34f, 0.31f), 50f, new Color(0.3f, 0.28f, 0.27f),  new Color(0.95f, 0.75f, 0.55f), 0.55f, 18f),
            new Key(19f, new Color(0.22f, 0.15f, 0.13f), 36f, new Color(0.17f, 0.14f, 0.15f), new Color(0.9f, 0.45f, 0.3f), 0.35f, 4f),
            // Nights are black: the lamps and your flashlight are the only light worth the name.
            new Key(20.5f, new Color(0.06f, 0.06f, 0.08f), 28f, new Color(0.06f, 0.06f, 0.085f), new Color(0.45f, 0.5f, 0.7f), 0.1f, 30f),
            new Key(22f, new Color(0.035f, 0.038f, 0.045f), 24f, new Color(0.04f, 0.042f, 0.055f), new Color(0.6f, 0.66f, 0.8f), 0.06f, 40f),
            new Key(27f, new Color(0.03f, 0.032f, 0.04f), 24f, new Color(0.035f, 0.037f, 0.05f), new Color(0.6f, 0.66f, 0.8f), 0.05f, 40f),
        };

        public void Configure(Light sunLight, Camera camera)
        {
            sun = sunLight;
            targetCamera = camera;
        }

        private readonly List<Renderer> litWindows = new List<Renderer>();
        private Color windowEmission;
        private float windowGlow = -1f;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private void Start()
        {
            // Every lit window shares one material; its glow is scaled per renderer so the asset itself never changes.
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                if (r.sharedMaterial != null && r.sharedMaterial.name == "WindowLit")
                {
                    if (litWindows.Count == 0) windowEmission = r.sharedMaterial.GetColor(EmissionId);
                    litWindows.Add(r);
                }
        }

        private void LateUpdate()
        {
            var clock = GameClock.Instance;
            if (clock == null) return;
            Apply(clock.Hour);
        }

        /// <summary>Windows glow against the dark; in daylight they're just glass with a lamp behind it.</summary>
        private void ApplyWindowGlow(float ambientLevel)
        {
            float glow = 1f - Mathf.InverseLerp(0.1f, 0.3f, ambientLevel);
            if (Mathf.Abs(glow - windowGlow) < 0.03f) return;
            windowGlow = glow;
            var block = new MaterialPropertyBlock();
            // By day the pane reads as glass (a cool grey sheen); after dark the lamp behind it takes over.
            var dayGlass = new Color(0.16f, 0.19f, 0.22f);
            block.SetColor(EmissionId, Color.Lerp(dayGlass, windowEmission, glow));
            foreach (var r in litWindows)
                if (r != null) r.SetPropertyBlock(block);
        }

        public void Apply(float hour)
        {
            hour = Mathf.Clamp(hour, Keys[0].hour, Keys[Keys.Length - 1].hour);
            int i = 0;
            while (i < Keys.Length - 2 && hour > Keys[i + 1].hour) i++;
            var a = Keys[i];
            var b = Keys[i + 1];
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.hour, b.hour, hour));

            float dark = Mathf.Clamp(DarknessBoost + HuntDarkness, 0f, 0.65f); // dark, never illegible
            Color fog = Color.Lerp(a.fog, b.fog, t) * (1f - dark * 0.6f);
            Color ambient = Color.Lerp(a.ambient, b.ambient, t) * (1f - dark * 0.5f);
            fog.a = 1f;
            ambient.a = 1f;
            CurrentAmbientLevel = ambient.grayscale;
            ApplyWindowGlow(Color.Lerp(a.ambient, b.ambient, t).grayscale);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = Mathf.Lerp(a.fogEnd, b.fogEnd, t) * (1f - dark * 0.3f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(ambient);
            RenderSettings.ambientProbe = probe;

            if (sun != null)
            {
                sun.color = Color.Lerp(a.sunColor, b.sunColor, t);
                sun.intensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t) * (1f - dark * 0.5f);
                float yaw = Mathf.Lerp(80f, 280f, Mathf.InverseLerp(6f, 27f, hour));
                sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(a.sunPitch, b.sunPitch, t), yaw, 0);
                // At night the sun is too dim to cast anything visible; skip drawing the shadow cascades at all.
                var shadows = sun.intensity < 0.1f ? LightShadows.None : LightShadows.Hard;
                if (sun.shadows != shadows) sun.shadows = shadows;
            }
            if (targetCamera != null)
                targetCamera.backgroundColor = fog;
        }
    }
}
