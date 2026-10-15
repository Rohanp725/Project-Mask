using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Semblance.Rendering
{
    /// <summary>
    /// Helper cameras (the output clear, mirrors) don't need the main camera's ambient occlusion, shadows, depth or
    /// colour copies. Each one would otherwise run the whole pipeline again.
    /// </summary>
    public static class LiteCamera
    {
        /// <summary>Index of Lite_Renderer (no renderer features) in the pipeline assets' renderer lists.</summary>
        public const int RendererIndex = 1;

        public static void Apply(Camera camera, bool shadows = false)
        {
            if (camera == null) return;
            var data = camera.GetUniversalAdditionalCameraData();
            if (data == null) return;
            if (UniversalRenderPipeline.asset != null && UniversalRenderPipeline.asset.rendererDataList.Length > RendererIndex)
                data.SetRenderer(RendererIndex);
            data.renderShadows = shadows;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.renderPostProcessing = false;
        }
    }
}
