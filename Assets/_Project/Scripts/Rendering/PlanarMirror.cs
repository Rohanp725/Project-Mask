using System.Collections.Generic;
using UnityEngine;

namespace Semblance.Rendering
{
    /// <summary>Named render layers used by mirrors.</summary>
    public static class Layers
    {
        /// <summary>Things with no reflection (the mimic, when that's its tell).</summary>
        public static int NoReflection => Get("NoReflection");
        /// <summary>Things only visible in mirrors.</summary>
        public static int MirrorGhost => Get("MirrorGhost");

        private static int Get(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            return layer < 0 ? 0 : layer;
        }
    }

    /// <summary>
    /// A low-res working mirror. Renders the room from the reflected viewpoint (clipped at the glass) into a small
    /// texture. It leaves out anything on the NoReflection layer and shows things on the MirrorGhost layer.
    /// The town has very few of these. People here don't like them.
    /// </summary>
    public class PlanarMirror : MonoBehaviour
    {
        [SerializeField] private Renderer glass;
        [SerializeField] private Vector2 size = new Vector2(2f, 1f);
        [SerializeField] private int resolution = 160;
        [SerializeField] private float activeRange = 14f;

        private static readonly List<PlanarMirror> Mirrors = new List<PlanarMirror>();
        public static IReadOnlyList<PlanarMirror> All => Mirrors;

        private Camera mirrorCamera;
        private RenderTexture target;
        private int frame;

        public void Configure(Renderer glassRenderer, Vector2 glassSize)
        {
            glass = glassRenderer;
            size = glassSize;
        }

        private void OnEnable() => Mirrors.Add(this);
        private void OnDisable() => Mirrors.Remove(this);

        private void Start()
        {
            int h = Mathf.Max(32, Mathf.RoundToInt(resolution * size.y / size.x));
            target = new RenderTexture(resolution, h, 16) { filterMode = FilterMode.Point };
            var go = new GameObject("Mirror Camera");
            go.transform.SetParent(transform, false);
            mirrorCamera = go.AddComponent<Camera>();
            mirrorCamera.enabled = false;
            mirrorCamera.targetTexture = target;
            mirrorCamera.cullingMask = ~(1 << Layers.NoReflection);
            mirrorCamera.clearFlags = CameraClearFlags.SolidColor;
            mirrorCamera.backgroundColor = Color.black;
            mirrorCamera.farClipPlane = 40f;
            LiteCamera.Apply(mirrorCamera);

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader);
            material.SetTexture("_BaseMap", target);
            material.SetTextureScale("_BaseMap", new Vector2(-1f, 1f));
            material.SetTextureOffset("_BaseMap", new Vector2(1f, 0f));
            material.SetColor("_BaseColor", new Color(0.82f, 0.85f, 0.85f));
            glass.sharedMaterial = material;
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || mirrorCamera == null) return;
            Vector3 normal = transform.forward; // faces into the room
            Vector3 toCam = cam.transform.position - transform.position;
            if (toCam.magnitude > activeRange || Vector3.Dot(toCam, normal) <= 0f) return;
            if (++frame % 2 != 0) return;

            // Camera mirrored through the glass, looking back through it into the room.
            float d = Vector3.Dot(toCam, normal);
            Vector3 reflected = cam.transform.position - 2f * d * normal;
            mirrorCamera.transform.position = reflected;
            mirrorCamera.transform.rotation = Quaternion.LookRotation(transform.position - reflected, Vector3.up);
            float distance = Vector3.Distance(reflected, transform.position);
            mirrorCamera.ResetProjectionMatrix();
            mirrorCamera.aspect = size.x / size.y;
            mirrorCamera.fieldOfView = Mathf.Clamp(2f * Mathf.Atan(size.y * 0.5f / distance) * Mathf.Rad2Deg, 5f, 120f);

            // Clip everything on the wall's side of the glass.
            var m = mirrorCamera.worldToCameraMatrix;
            Vector3 cPos = m.MultiplyPoint(transform.position + normal * 0.01f);
            Vector3 cNormal = m.MultiplyVector(normal).normalized;
            var plane = new Vector4(cNormal.x, cNormal.y, cNormal.z, -Vector3.Dot(cPos, cNormal));
            mirrorCamera.projectionMatrix = mirrorCamera.CalculateObliqueMatrix(plane);
            mirrorCamera.Render();
        }
    }
}
