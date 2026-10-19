using System.Collections.Generic;
using Semblance.Core;
using UnityEngine;

namespace Semblance.Rendering
{
    /// <summary>An outdoor lamp that switches on at dusk and off at dawn. Night events can also kill it temporarily.</summary>
    public class LampSwitch : MonoBehaviour
    {
        [SerializeField] private Light lamp;
        [SerializeField] private Renderer bulb;
        [SerializeField] private float onHour = 18.75f;
        [SerializeField] private float offHour = 6.75f;

        private static readonly List<LampSwitch> All = new List<LampSwitch>();
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public static IReadOnlyList<LampSwitch> Lamps => All;

        /// <summary>Every lamp in town lit regardless of the hour (the daylight hunt: porches snap on as the sky goes).</summary>
        public static bool ForceOn { get; set; }
        /// <summary>This one lamp stays lit whatever the hour (a porch light nobody came home to switch off).</summary>
        public bool Hold { get; set; }
        public Light Lamp => lamp;
        public bool IsOn { get; private set; } = true;

        private float baseIntensity;
        private Color baseEmission;
        private MaterialPropertyBlock block;
        private float killedUntil;

        public void Configure(Light light, Renderer bulbRenderer)
        {
            lamp = light;
            bulb = bulbRenderer;
        }

        private void Awake() => Init();

        private void Init()
        {
            if (block != null) return;
            block = new MaterialPropertyBlock();
            if (lamp != null) baseIntensity = lamp.intensity;
            if (bulb != null && bulb.sharedMaterial != null) baseEmission = bulb.sharedMaterial.GetColor(EmissionId);
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        /// <summary>Makes the lamp go dark for a while (night events).</summary>
        public void Kill(float seconds) => killedUntil = Time.time + seconds;

        private void Update()
        {
            Init();
            var clock = GameClock.Instance;
            float h = clock != null ? clock.Hour : 22f;
            bool scheduled = ForceOn || Hold || h >= onHour || h < offHour || h >= 24f;
            bool on = scheduled && Time.time >= killedUntil;
            if (on == IsOn) return;
            IsOn = on;
            if (lamp != null)
            {
                lamp.enabled = on;
                // FlickeringLight on the same object resets intensity itself; keep base for others.
                if (on && lamp.intensity <= 0.01f) lamp.intensity = baseIntensity;
            }
            if (bulb != null)
            {
                block.SetColor(EmissionId, on ? baseEmission : Color.black);
                bulb.SetPropertyBlock(block);
            }
        }
    }
}
