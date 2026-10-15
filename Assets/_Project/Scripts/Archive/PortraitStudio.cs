using System.Collections.Generic;
using Semblance.Residents;
using UnityEngine;

namespace Semblance.Archive
{
    public enum PhotoStyle { Archive, Snapshot }

    /// <summary>
    /// Renders head-and-shoulders portraits of residents in a hidden studio far below the town.
    /// Used for the 1979 parish directory photos (canonical looks, aged sepia) and anywhere else a face is needed.
    /// </summary>
    public class PortraitStudio : MonoBehaviour
    {
        public static PortraitStudio Instance { get; private set; }

        [SerializeField] private Shader residentShader;
        [SerializeField] private int size = 160;

        private static readonly Vector3 StagePosition = new Vector3(0, -1000f, 0);
        private Camera studioCamera;
        private RenderTexture target;
        private readonly Dictionary<string, Texture2D> archiveCache = new Dictionary<string, Texture2D>();

        private void Awake()
        {
            Instance = this;
            if (residentShader == null)
                residentShader = Shader.Find("Semblance/PS1 Lit");

            var stage = new GameObject("Portrait Studio").transform;
            stage.SetParent(transform, false);
            stage.position = StagePosition;

            var camGO = new GameObject("Studio Camera");
            camGO.transform.SetParent(stage, false);
            studioCamera = camGO.AddComponent<Camera>();
            studioCamera.enabled = false;
            studioCamera.clearFlags = CameraClearFlags.SolidColor;
            studioCamera.backgroundColor = new Color(0.55f, 0.55f, 0.55f);
            studioCamera.fieldOfView = 26f;
            studioCamera.nearClipPlane = 0.05f;
            studioCamera.farClipPlane = 6f;

            AddLight(stage, new Vector3(0.6f, 2.0f, 1.2f), 5f, 3.5f);
            AddLight(stage, new Vector3(-0.9f, 1.4f, 0.8f), 4f, 1.2f);

            target = new RenderTexture(size, size, 24) { filterMode = FilterMode.Point };
        }

        private static void AddLight(Transform parent, Vector3 localPos, float range, float intensity)
        {
            var go = new GameObject("Studio Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = new Color(1f, 0.95f, 0.88f);
            light.shadows = LightShadows.None;
        }

        /// <summary>The resident as they looked in 1979: canonical features only, no tells, sepia.</summary>
        public Texture2D ArchivePhoto(ResidentProfile profile)
        {
            if (profile == null)
                return null;
            if (archiveCache.TryGetValue(profile.name, out var cached) && cached != null)
                return cached;
            var mods = new RunModifications();
            foreach (var f in profile.features)
                if (!f.inArchive)
                    mods.RemovedFeatureIds.Add(f.id);
            var photo = Capture(profile, mods, PhotoStyle.Archive, profile.name.GetHashCode());
            archiveCache[profile.name] = photo;
            return photo;
        }

        public Texture2D Capture(ResidentProfile profile, RunModifications mods, PhotoStyle style, int seed)
        {
            var sitter = new GameObject("Sitter");
            sitter.SetActive(false);
            sitter.transform.SetParent(studioCamera.transform.parent, false);
            sitter.transform.localPosition = Vector3.zero;
            sitter.transform.localRotation = Quaternion.identity;
            var avatar = sitter.AddComponent<ResidentAvatar>();
            avatar.SetProfile(profile, residentShader);
            if (mods != null)
                avatar.ApplyRunModifications(mods);
            sitter.SetActive(true);

            // Frame on the actual head geometry (posture like a hunch moves it a long way from the pivot).
            var headShape = avatar.Rig?.Part("HeadShape");
            Vector3 focus = headShape != null ? headShape.GetComponent<Renderer>().bounds.center : sitter.transform.position + Vector3.up * 1.6f;
            focus -= Vector3.up * 0.07f;
            studioCamera.transform.position = focus + sitter.transform.forward * 1.25f + Vector3.up * 0.06f;
            studioCamera.transform.LookAt(focus);
            studioCamera.targetTexture = target;
            studioCamera.Render();

            var photo = ReadTarget(target);
            sitter.SetActive(false);
            Destroy(sitter);

            if (style == PhotoStyle.Archive)
                AgePhoto(photo, seed);
            return photo;
        }

        public static Texture2D ReadTarget(RenderTexture rt)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            return tex;
        }

        /// <summary>Sepia, faded contrast, grain, vignette and a couple of scratches: a 1979 directory print.</summary>
        public static void AgePhoto(Texture2D tex, int seed)
        {
            var rng = new System.Random(seed);
            var pixels = tex.GetPixels();
            int w = tex.width, h = tex.height;
            int scratchX1 = rng.Next(w), scratchX2 = rng.Next(w);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = pixels[y * w + x];
                float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                lum = Mathf.Lerp(0.18f, 0.88f, Mathf.Pow(lum, 0.8f));
                lum += ((float)rng.NextDouble() - 0.5f) * 0.08f;
                float dx = (x / (float)w - 0.5f) * 2f, dy = (y / (float)h - 0.5f) * 2f;
                lum *= Mathf.Lerp(1f, 0.55f, Mathf.Clamp01((dx * dx + dy * dy - 0.4f) * 0.9f));
                if ((x == scratchX1 && rng.NextDouble() < 0.7) || (x == scratchX2 && y > h / 3 && rng.NextDouble() < 0.5))
                    lum = 0.92f;
                pixels[y * w + x] = new Color(lum * 1.0f, lum * 0.86f, lum * 0.66f);
            }
            tex.SetPixels(pixels);
            tex.Apply();
        }
    }
}
