using System.Collections.Generic;
using Semblance.Rendering;
using Semblance.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace Semblance.EditorTools
{
    /// <summary>
    /// Builds the main menu scene (a foggy road at night, the welcome sign, one lamp) and the UI font asset.
    /// Menu: Semblance > Build Main Menu.
    /// </summary>
    public static class MenuBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string FontSource = "Assets/_Project/Fonts/SpecialElite-Regular.ttf";
        private const string FontAssetPath = "Assets/_Project/Resources/Fonts/SpecialElite SDF.asset";

        [MenuItem("Semblance/Build Main Menu")]
        public static void Build()
        {
            EnsureFontAsset();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material M(string name) => AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Project/Materials/{name}.mat");

            GameObject Box(string name, Vector3 pos, Vector3 size, string mat, Transform parent = null, Vector3 euler = default)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(euler);
                go.transform.localScale = size;
                go.GetComponent<Renderer>().sharedMaterial = M(mat);
                return go;
            }

            var world = new GameObject("Set").transform;
            Box("Ground", new Vector3(0, -0.5f, 30), new Vector3(200, 1, 200), "Grass", world);
            Box("Road", new Vector3(0, 0.01f, 30), new Vector3(7, 0.02f, 200), "Asphalt", world);
            for (float z = -40; z < 100; z += 6)
                Box("Dash", new Vector3(0, 0.03f, z), new Vector3(0.15f, 0.01f, 2.2f), "RoadLine", world);

            // Welcome sign
            var sign = new GameObject("Welcome Sign").transform;
            sign.SetParent(world, false);
            sign.position = new Vector3(-5.5f, 0, 14f);
            sign.rotation = Quaternion.Euler(0, 180f + 18f, 0);
            Box("Post L", new Vector3(-1.5f, 1f, -0.15f), new Vector3(0.18f, 2f, 0.18f), "Wood", sign);
            Box("Post R", new Vector3(1.5f, 1f, -0.15f), new Vector3(0.18f, 2f, 0.18f), "Wood", sign);
            Box("Board", new Vector3(0, 2.1f, 0), new Vector3(3.8f, 1.5f, 0.12f), "SignWhite", sign);
            Text(sign, "WELCOME TO", new Vector3(0, 2.6f, 0.07f), new Vector2(2.4f, 0.28f), new Color(0.12f, 0.12f, 0.12f));
            Text(sign, "SEMBLANCE", new Vector3(0, 2.2f, 0.07f), new Vector2(3.3f, 0.5f), new Color(0.12f, 0.12f, 0.12f));
            Text(sign, "POP.", new Vector3(0.42f, 1.7f, 0.07f), new Vector2(0.7f, 0.3f), new Color(0.12f, 0.12f, 0.12f));
            Text(sign, "212", new Vector3(-0.3f, 1.7f, 0.07f), new Vector2(0.7f, 0.3f), new Color(0.12f, 0.12f, 0.12f, 0.4f));
            var fresh = Text(sign, "213", new Vector3(-0.38f, 1.76f, 0.075f), new Vector2(0.78f, 0.36f), new Color(0.48f, 0.07f, 0.05f));
            fresh.transform.localRotation = Quaternion.Euler(0, 180f, -6f);

            // A few pines and one lamp
            var cone = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Meshes/PineCone.asset");
            var rng = new System.Random(5);
            for (int i = 0; i < 40; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                var p = new Vector3(side * (12f + (float)rng.NextDouble() * 30f), 0, -10f + (float)rng.NextDouble() * 110f);
                float s = 1f + (float)rng.NextDouble() * 0.7f;
                foreach (var (y, sc) in new[] { (1.2f, new Vector3(2.8f, 3.4f, 2.8f)), (3.3f, new Vector3(2f, 2.8f, 2f)) })
                {
                    var go = new GameObject("Pine", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(world, false);
                    go.transform.position = p + Vector3.up * y * s;
                    go.transform.localScale = sc * s;
                    go.GetComponent<MeshFilter>().sharedMesh = cone;
                    go.GetComponent<MeshRenderer>().sharedMaterial = M("Pine");
                }
            }
            var lamp = new GameObject("Lamp").transform;
            lamp.SetParent(world, false);
            lamp.position = new Vector3(4.2f, 0, 22f);
            lamp.rotation = Quaternion.Euler(0, -90f, 0);
            Box("Pole", new Vector3(0, 2.2f, 0), new Vector3(0.16f, 4.4f, 0.16f), "Metal", lamp);
            Box("Arm", new Vector3(0, 4.3f, 0.6f), new Vector3(0.08f, 0.08f, 1.2f), "Metal", lamp);
            Box("Head", new Vector3(0, 4.2f, 1.15f), new Vector3(0.35f, 0.18f, 0.5f), "Bulb", lamp);
            var lightGO = new GameObject("Light");
            lightGO.transform.SetParent(lamp, false);
            lightGO.transform.localPosition = new Vector3(0, 3.9f, 1.15f);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 13f;
            light.intensity = 10f;
            light.color = new Color(1f, 0.78f, 0.52f);
            SetField(lightGO.AddComponent<FlickeringLight>(), "flickerChance", 0.3f);

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.intensity = 0.1f;
            moon.color = new Color(0.6f, 0.66f, 0.8f);
            moon.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            RenderSettings.sun = moon;

            // Camera with the PS1 effect
            var rig = new GameObject("Camera Rig").transform;
            rig.position = new Vector3(1.4f, 1.6f, -2f);
            rig.rotation = Quaternion.Euler(-2f, -8f, 0);
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.transform.SetParent(rig, false);
            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.05f;
            camGO.AddComponent<AudioListener>();
            camGO.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            SetField(camGO.AddComponent<PS1ScreenEffect>(), "screenShader", Shader.Find("Semblance/PS1 Screen"));

            var atmosphere = new GameObject("Atmosphere").AddComponent<SceneAtmosphere>();
            SetField(atmosphere, "targetCamera", cam);
            atmosphere.Apply();

            var menu = new GameObject("Main Menu").AddComponent<MainMenuController>();
            SetField(menu, "cameraRig", rig);

            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildOrder();
            Debug.Log("[Semblance] Main menu built: " + ScenePath);
        }

        private static TextMeshPro Text(Transform parent, string text, Vector3 pos, Vector2 size, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.5f;
            tmp.fontSizeMax = 12f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = size;
            return tmp;
        }

        public static void SetBuildOrder()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(TownBuilder.ScenePath, true),
            };
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TownBuilder.TutorialScenePath) != null)
                scenes.Add(new EditorBuildSettingsScene(TownBuilder.TutorialScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void EnsureFontAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) != null) return;
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontSource);
            if (font == null)
            {
                Debug.LogWarning("[Semblance] Font source missing; UI will use the default font.");
                return;
            }
            System.IO.Directory.CreateDirectory("Assets/_Project/Resources/Fonts");
            var asset = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            asset.name = "SpecialElite SDF";
            AssetDatabase.CreateAsset(asset, FontAssetPath);
            if (asset.atlasTextures != null && asset.atlasTextures.Length > 0)
            {
                asset.atlasTextures[0].name = "SpecialElite Atlas";
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            }
            asset.material.name = "SpecialElite Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            // Fall back to TMP's default font for any glyph the typewriter face doesn't have.
            var fallback = TMP_Settings.defaultFontAsset;
            if (fallback != null)
                asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private static void SetField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) return;
            switch (value)
            {
                case Object o: p.objectReferenceValue = o; break;
                case float f: p.floatValue = f; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
