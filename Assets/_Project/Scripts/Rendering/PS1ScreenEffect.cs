using UnityEngine;
using UnityEngine.UI;

namespace Semblance.Rendering
{
    /// <summary>
    /// Renders this camera into a low-resolution, point-filtered texture and shows it full screen
    /// through a dithering / color-reduction shader, for a PS1-style image.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PS1ScreenEffect : MonoBehaviour
    {
        [SerializeField] private int verticalResolution = 240;
        [SerializeField] private Shader screenShader;
        [SerializeField, Range(2, 256)] private int colorLevels = 32;
        [SerializeField, Range(0f, 1f)] private float ditherStrength = 1f;

        private static readonly int ColorLevelsId = Shader.PropertyToID("_ColorLevels");
        private static readonly int DitherStrengthId = Shader.PropertyToID("_DitherStrength");
        private static readonly int DistressId = Shader.PropertyToID("_Distress");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");

        /// <summary>0..1 fear effect (desaturation, grain, vignette), driven by the detective's nerve.</summary>
        public static float Distress { get; set; }

        private Camera sourceCamera;
        private RenderTexture renderTexture;
        private Material screenMaterial;
        private GameObject outputRoot;
        private RawImage outputImage;
        private int lastScreenWidth;
        private int lastScreenHeight;

        private void OnEnable()
        {
            sourceCamera = GetComponent<Camera>();
            if (screenShader == null)
                screenShader = Shader.Find("Semblance/PS1 Screen");
            if (screenShader != null)
                screenMaterial = new Material(screenShader) { hideFlags = HideFlags.HideAndDontSave };

            CreateOutput();
            RebuildTexture();
        }

        private void OnDisable()
        {
            if (sourceCamera != null)
                sourceCamera.targetTexture = null;
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
            if (outputRoot != null)
                Destroy(outputRoot);
            if (screenMaterial != null)
                Destroy(screenMaterial);
        }

        private void Update()
        {
            // A camera that isn't rendering has nothing to show: hide its output rather than a stale frame
            // (the tutorial hands the screen to a second camera for its way in).
            if (outputImage != null && outputImage.enabled != sourceCamera.enabled) outputImage.enabled = sourceCamera.enabled;
            verticalResolution = Core.GameSettings.PixelHeight;
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight || renderTexture == null || renderTexture.height != Mathf.Max(64, verticalResolution))
                RebuildTexture();

            if (screenMaterial != null)
            {
                screenMaterial.SetFloat(ColorLevelsId, colorLevels);
                screenMaterial.SetFloat(DitherStrengthId, Core.GameSettings.Dither ? ditherStrength : 0f);
                screenMaterial.SetFloat(ColorLevelsId, Core.GameSettings.Dither ? colorLevels : 256f);
                screenMaterial.SetFloat(DistressId, Distress);
                screenMaterial.SetFloat(BrightnessId, Core.GameSettings.Brightness);
            }
        }

        private void CreateOutput()
        {
            // A camera that renders nothing, so the display is cleared and overlay UI still draws.
            outputRoot = new GameObject("PS1 Output");
            var clearCamera = outputRoot.AddComponent<Camera>();
            clearCamera.clearFlags = CameraClearFlags.SolidColor;
            clearCamera.backgroundColor = Color.black;
            clearCamera.cullingMask = 0;
            clearCamera.depth = sourceCamera.depth + 1;
            LiteCamera.Apply(clearCamera);

            var canvasGO = new GameObject("PS1 Canvas", typeof(Canvas));
            canvasGO.transform.SetParent(outputRoot.transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100 + Mathf.RoundToInt(sourceCamera.depth);

            outputImage = new GameObject("PS1 Image", typeof(RawImage)).GetComponent<RawImage>();
            outputImage.transform.SetParent(canvasGO.transform, false);
            outputImage.raycastTarget = false;
            outputImage.material = screenMaterial;
            var rect = outputImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void RebuildTexture()
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            int height = Mathf.Max(64, verticalResolution);
            int width = Mathf.Max(64, Mathf.RoundToInt(height * (float)Screen.width / Mathf.Max(1, Screen.height)));

            if (renderTexture != null)
            {
                sourceCamera.targetTexture = null;
                renderTexture.Release();
                Destroy(renderTexture);
            }

            renderTexture = new RenderTexture(width, height, 24)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "PS1 Low-Res Target"
            };
            renderTexture.Create();
            sourceCamera.targetTexture = renderTexture;
            outputImage.texture = renderTexture;
        }
    }
}
