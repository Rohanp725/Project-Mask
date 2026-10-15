using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Semblance.EditorTools
{
    /// <summary>
    /// Development helper: renders the game camera and every overlay canvas (HUD, dialogue, notebook...) into a PNG,
    /// so screens can be checked without the Game view having focus.
    /// </summary>
    public static class DevCapture
    {
        public static string CaptureScreen(string path, int width = 1280, int height = 720)
        {
            var main = Camera.main;
            if (main != null)
                main.Render();

            var rt = new RenderTexture(width, height, 24);
            var camGO = new GameObject("DevCapture Camera");
            camGO.transform.position = new Vector3(0, -5000, 0);
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 50f;
            cam.targetTexture = rt;

            var changed = new List<(Canvas canvas, RenderMode mode)>();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay || !canvas.enabled)
                    continue;
                changed.Add((canvas, canvas.renderMode));
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f - canvas.sortingOrder * 0.001f;
            }
            Canvas.ForceUpdateCanvases();
            cam.Render();

            foreach (var (canvas, mode) in changed)
            {
                canvas.renderMode = mode;
                canvas.worldCamera = null;
            }

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(tex);
            rt.Release();
            return path;
        }
    }
}
