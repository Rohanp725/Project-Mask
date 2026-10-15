using Semblance.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Semblance.UI
{
    /// <summary>An old-VHS look over the screen: scanlines, rolling noise, a tracking band and a tape timestamp.</summary>
    public class VhsOverlay : MonoBehaviour
    {
        [SerializeField] private float strength = 1f;
        [SerializeField] private bool showTimestamp = true;

        private RawImage scanlines;
        private RawImage noise;
        private Image band;
        private TextMeshProUGUI stamp;
        private Texture2D noiseTex;
        private float bandY;

        public float Strength { get => strength; set => strength = value; }

        private void Awake()
        {
            var canvas = UIFactory.Canvas("VHS Canvas", transform, 180);
            overlayCanvas = canvas;
            var lines = new Texture2D(1, 4) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            lines.SetPixels(new[] { new Color(0, 0, 0, 0.35f), new Color(0, 0, 0, 0), new Color(0, 0, 0, 0.12f), new Color(0, 0, 0, 0) });
            lines.Apply();
            scanlines = new GameObject("Scanlines", typeof(RawImage)).GetComponent<RawImage>();
            scanlines.transform.SetParent(canvas.transform, false);
            scanlines.texture = lines;
            scanlines.raycastTarget = false;
            UIFactory.Stretch(scanlines.rectTransform);
            scanlines.uvRect = new Rect(0, 0, 1, 180);

            noiseTex = new Texture2D(128, 72) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            noiseTex.SetPixels(new Color[128 * 72]);
            noiseTex.Apply();
            noise = new GameObject("Noise", typeof(RawImage)).GetComponent<RawImage>();
            noise.transform.SetParent(canvas.transform, false);
            noise.texture = noiseTex;
            noise.raycastTarget = false;
            UIFactory.Stretch(noise.rectTransform);

            band = UIFactory.Panel("Tracking", canvas.transform, new Color(1, 1, 1, 0.05f), false);
            UIFactory.Place(band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 26));

            stamp = UIFactory.Text("Stamp", canvas.transform, "", 22, new Color(1, 1, 1, 0.75f), TextAlignmentOptions.BottomLeft);
            UIFactory.Place(stamp.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 24), new Vector2(500, 40));
        }

        private Canvas overlayCanvas;

        private void OnDestroy()
        {
            if (overlayCanvas != null) Destroy(overlayCanvas.gameObject);
        }

        private Color32[] noisePixels;
        private float nextNoise;
        private bool stampShowsPlay;
        private System.Random noiseRng = new System.Random();

        private void Update()
        {
            // About 20 times a second whatever the frame rate, into one reused buffer (no garbage per refresh).
            if (Time.unscaledTime >= nextNoise)
            {
                nextNoise = Time.unscaledTime + 0.05f;
                int w = noiseTex.width, h = noiseTex.height;
                if (noisePixels == null || noisePixels.Length != w * h) noisePixels = new Color32[w * h];
                for (int y = 0; y < h; y++)
                {
                    float row = noiseRng.NextDouble() < 0.04 ? 0.05f + (float)noiseRng.NextDouble() * 0.13f : 0f; // a bright tape streak
                    int streakStart = noiseRng.Next(w), streakLen = 4 + noiseRng.Next(36);
                    for (int x = 0; x < w; x++)
                    {
                        bool inStreak = row > 0 && x >= streakStart && x < streakStart + streakLen;
                        float speck = !inStreak && noiseRng.Next(1000) >= 997 ? 0.12f : 0f;
                        float a = (inStreak ? row : speck) * strength;
                        noisePixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(a * 255f, 0f, 255f));
                    }
                }
                noiseTex.SetPixels32(noisePixels);
                noiseTex.Apply(false);
            }
            bandY += Time.unscaledDeltaTime * 90f;
            if (bandY > 420f) bandY = -420f - Random.Range(0f, 400f);
            band.rectTransform.anchoredPosition = new Vector2(Random.Range(-3f, 3f), bandY);
            band.color = new Color(1, 1, 1, 0.05f * strength);
            scanlines.color = new Color(1, 1, 1, strength);
            stamp.gameObject.SetActive(showTimestamp);
            if (showTimestamp)
            {
                bool play = Mathf.FloorToInt(Time.unscaledTime * 2) % 2 == 0;
                if (play != stampShowsPlay || string.IsNullOrEmpty(stamp.text))
                {
                    stampShowsPlay = play;
                    stamp.text = play ? "PLAY >  OCT.15 1983" : "PLAY    OCT.15 1983";
                }
            }
        }
    }
}
