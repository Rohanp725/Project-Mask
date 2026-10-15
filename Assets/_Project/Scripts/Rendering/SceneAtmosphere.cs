using UnityEngine;
using UnityEngine.Rendering;

namespace Semblance.Rendering
{
    /// <summary>
    /// Applies fog, flat ambient light and camera background for the scene's mood.
    /// Kept in one component so a day/night system can drive these values later.
    /// </summary>
    [ExecuteAlways]
    public class SceneAtmosphere : MonoBehaviour
    {
        [SerializeField] private Color fogColor = new Color(0.075f, 0.082f, 0.09f);
        [SerializeField] private float fogStart = 3f;
        [SerializeField] private float fogEnd = 32f;
        [SerializeField] private Color ambientColor = new Color(0.075f, 0.08f, 0.1f);
        [Tooltip("How far the camera draws. Beyond the fog only emissive beacons (lamps, windows) show through.")]
        [SerializeField] private float drawDistance = 180f;
        [SerializeField] private Camera targetCamera;

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        public void Apply()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(ambientColor);
            RenderSettings.ambientProbe = probe;

            if (targetCamera != null)
            {
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = fogColor;
                targetCamera.farClipPlane = Mathf.Max(fogEnd + 10f, drawDistance);
            }
        }
    }
}
