using UnityEngine;

namespace Semblance.Rendering
{
    /// <summary>Makes a light (and optionally its bulb's emission) stutter like a failing streetlamp.</summary>
    [RequireComponent(typeof(Light))]
    public class FlickeringLight : MonoBehaviour
    {
        [SerializeField] private Renderer bulbRenderer;
        [SerializeField, Range(0f, 1f)] private float flickerChance = 0.08f;
        [SerializeField] private Vector2 offDuration = new Vector2(0.03f, 0.25f);
        [SerializeField] private Vector2 calmDuration = new Vector2(0.5f, 4f);

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private Light targetLight;
        private float baseIntensity;
        private Color baseEmission;
        private MaterialPropertyBlock block;
        private float nextChange;
        private bool isOff;

        private void Awake()
        {
            targetLight = GetComponent<Light>();
            baseIntensity = targetLight.intensity;
            block = new MaterialPropertyBlock();
            if (bulbRenderer != null)
                baseEmission = bulbRenderer.sharedMaterial.GetColor(EmissionId);
        }

        private void Update()
        {
            if (Time.time < nextChange)
                return;

            if (isOff || Random.value > flickerChance)
            {
                isOff = false;
                nextChange = Time.time + Random.Range(calmDuration.x, calmDuration.y) * (Random.value < 0.3f ? 0.1f : 1f);
            }
            else
            {
                isOff = true;
                nextChange = Time.time + Random.Range(offDuration.x, offDuration.y);
            }

            float factor = isOff ? Random.Range(0f, 0.15f) : Random.Range(0.85f, 1f);
            targetLight.intensity = baseIntensity * factor;
            if (bulbRenderer != null)
            {
                block.SetColor(EmissionId, baseEmission * factor);
                bulbRenderer.SetPropertyBlock(block);
            }
        }
    }
}
