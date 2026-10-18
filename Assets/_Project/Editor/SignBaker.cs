using System.Collections.Generic;
using System.Text;
using Semblance.Rendering;
using Semblance.World;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Semblance.EditorTools
{
    /// <summary>
    /// Lettering in the world is baked, not rendered live. Each sign's text is drawn once with TextMeshPro into a
    /// small texture (about 64 pixels per metre, like everything else here), weathered a little (flaking paint, a
    /// painter's drop shadow, grime), and put on a quad that uses the PS1 lit shader with alpha clipping. So the
    /// words sit under the same light, fog, dither and vertex snap as the board they're painted on.
    /// </summary>
    public static partial class TownBuilder
    {
        private const string SignDir = RootDir + "/Generated/Signs";
        private const int SignPixelsPerMetre = 64;
        private static int signIndex;
        private static readonly HashSet<string> signKeys = new HashSet<string>();

        /// <summary>Called once per build: old bakes go, so renamed or removed signs don't pile up.</summary>
        private static void ResetSignBakes()
        {
            signIndex = 0;
            signKeys.Clear();
            if (AssetDatabase.IsValidFolder(SignDir))
                AssetDatabase.DeleteAsset(SignDir);
            EnsureFolder(RootDir + "/Generated");
            EnsureFolder(SignDir);
        }

        /// <summary>
        /// Painted lettering. <paramref name="size"/> is the board area the text may use; the text itself keeps a
        /// margin inside it. Readable from the node's local -Z, like the old TextMeshPro, so the default rotation
        /// (facing the parent's +Z) is unchanged. <paramref name="variants"/> bakes several texts the sign can switch
        /// between at runtime (the population count); the first is shown.
        /// </summary>
        /// <param name="localRotation">Defaults to facing local +Z.</param>
        private static WorldText AddText(Transform parent, string text, Vector3 localPos, Vector2 size, Color color,
            Quaternion? localRotation = null, bool italic = false, string[] variants = null, bool glow = false, float wear = 0.18f)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRotation ?? Quaternion.Euler(0, 180f, 0);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Lettering";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(go.transform, false);
            // A hair toward the viewer, so the paint never fights the board for depth.
            quad.transform.localPosition = new Vector3(0, 0, -0.003f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;

            var names = variants ?? new[] { text };
            var materials = new Material[names.Length];
            int index = signIndex++;
            for (int i = 0; i < names.Length; i++)
                materials[i] = BakeSign(names[i], size, color, italic, glow, wear, $"sign_{index:000}_{Slug(names[i])}");
            renderer.sharedMaterial = materials[0];

            var worldText = go.AddComponent<WorldText>();
            worldText.Configure(renderer, names, materials);
            return worldText;
        }

        private static string Slug(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
                if (sb.Length >= 24) break;
            }
            string s = sb.ToString().Trim('_');
            if (s.Length == 0) s = "BLANK";
            string key = s;
            int n = 2;
            while (!signKeys.Add(key)) key = s + "_" + n++;
            return key;
        }

        /// <summary>Draws the text with TextMeshPro into a texture, weathers it, and saves texture and material.</summary>
        private static Material BakeSign(string text, Vector2 size, Color color, bool italic, bool glow, float wear, string key)
        {
            int w = Mathf.Clamp(Mathf.RoundToInt(size.x * SignPixelsPerMetre), 8, 1024);
            int h = Mathf.Clamp(Mathf.RoundToInt(size.y * SignPixelsPerMetre), 8, 1024);

            // The bake happens far above the town, where no light or geometry can get into the shot.
            var origin = new Vector3(0, 5000f, 0);
            var textGO = new GameObject("Sign Bake Text");
            textGO.transform.position = origin;
            var tmp = textGO.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.color = new Color(color.r, color.g, color.b, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = italic ? FontStyles.Bold | FontStyles.Italic : FontStyles.Bold;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f;
            tmp.fontSizeMax = 12f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(size.x * 0.9f, size.y * 0.7f);
            tmp.ForceMeshUpdate(true, true);

            var camGO = new GameObject("Sign Bake Camera");
            camGO.transform.position = origin + Vector3.back; // TMP reads from -Z
            camGO.transform.rotation = Quaternion.identity;
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = size.y * 0.5f;
            cam.aspect = size.x / size.y;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 3f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(color.r, color.g, color.b, 0f);
            cam.allowHDR = false;
            cam.allowMSAA = false;
            camGO.AddComponent<UniversalAdditionalCameraData>();
            LiteCamera.Apply(cam);

            var rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear }; // URP wants a depth buffer on a camera target
            cam.targetTexture = rt;
            cam.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = previous;
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(textGO);

            // The painter's shadow suits one big line of lettering; on small multi-line boards it just thickens the text.
            Weather(tex, color, wear, size.y >= 0.45f && text.IndexOf('\n') < 0, !glow, StableSeed(key));
            tex.name = key;
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply(false, false);
            string texPath = $"{SignDir}/{key}.asset";
            AssetDatabase.DeleteAsset(texPath);
            AssetDatabase.CreateAsset(tex, texPath);

            var material = new Material(litShader) { name = key };
            material.SetTexture("_BaseMap", tex);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_WorldUV", 0f);
            material.SetFloat("_AffineStrength", 0f);
            material.SetFloat("_AlphaClip", 1f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            if (glow)
            {
                // Lettering on a lit sign (the neon boards, the phone box header) keeps its light at night.
                material.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * 0.9f);
                material.SetFloat("_FogPierce", 0.45f);
            }
            else
            {
                material.SetColor("_EmissionColor", Color.black);
                material.SetFloat("_FogPierce", 0f);
            }
            string matPath = $"{SignDir}/{key}.mat";
            AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(material, matPath);
            return material;
        }

        private static int StableSeed(string key)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in key) h = h * 31 + c;
                return h;
            }
        }

        /// <summary>
        /// Paint that has been outside: the coverage is eroded where a slow noise falls below the wear level
        /// (flaking at the edges first), big signs get the painter's offset shadow, faded colours stay faint, and a
        /// few specks of grime land on the board around the letters.
        /// </summary>
        private static void Weather(Texture2D tex, Color color, float wear, bool dropShadow, bool grime, int seed)
        {
            int w = tex.width, h = tex.height;
            var src = tex.GetPixels32();
            var rng = new Random(seed);
            float ox = (float)rng.NextDouble() * 100f, oy = (float)rng.NextDouble() * 100f;
            // A faded colour (alpha below 1) can't be translucent on a cutout: it becomes paler, thinner paint.
            float fade = Mathf.Clamp01(color.a);
            var ink = Color.Lerp(new Color(0.62f, 0.6f, 0.56f), new Color(color.r, color.g, color.b), fade);
            var shade = ink * 0.3f;
            wear += (1f - fade) * 0.3f;
            fade = 1f;

            // Coverage from the render: the text shader blends alpha with itself, so take the root to get it back.
            var cover = new float[w * h];
            for (int i = 0; i < src.Length; i++) cover[i] = Mathf.Sqrt(src[i].a / 255f);

            var outPixels = new Color32[w * h];
            int sx = Mathf.Max(1, Mathf.RoundToInt(h * 0.035f)), sy = sx; // shadow offset, down and to the right
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float flake = Mathf.PerlinNoise(ox + x * 0.16f, oy + y * 0.16f) * 0.65f + Mathf.PerlinNoise(ox + x * 0.5f, oy + y * 0.5f) * 0.35f;
                float a = cover[i] * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(wear - 0.12f, wear + 0.12f, flake)) * fade;

                float s = 0f;
                if (dropShadow && x - sx >= 0 && y - sy >= 0)
                {
                    int j = (y - sy) * w + (x - sx);
                    float flakeS = Mathf.PerlinNoise(ox + 7f + x * 0.16f, oy + 3f + y * 0.16f);
                    s = cover[j] * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(wear - 0.05f, wear + 0.2f, flakeS)) * fade * 0.85f;
                }

                // Text over shadow over nothing.
                float outA = a + s * (1f - a);
                Color rgb = outA > 0.001f ? (ink * a + shade * s * (1f - a)) / outA : ink;

                // Grime: rare dark specks on the board itself (the quad covers the lettering area and its margin).
                if (grime && outA < 0.5f && rng.NextDouble() < 0.0015)
                {
                    rgb = new Color(0.3f, 0.27f, 0.23f);
                    outA = Mathf.Max(outA, 0.6f);
                }
                outPixels[i] = new Color32((byte)(Mathf.Clamp01(rgb.r) * 255f), (byte)(Mathf.Clamp01(rgb.g) * 255f), (byte)(Mathf.Clamp01(rgb.b) * 255f), (byte)(Mathf.Clamp01(outA) * 255f));
            }
            tex.SetPixels32(outPixels);
        }
    }
}
