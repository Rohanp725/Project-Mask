using System.Collections.Generic;
using System.Linq;
using Semblance.Residents;
using Semblance.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Random = System.Random;

namespace Semblance.EditorTools
{
    /// <summary>
    /// Night Shift: the county sheriff's office two nights before Semblance, built the same way as the town.
    /// An office floor with one lit window, a stairwell, the archive under it, and the boiler room at the far end.
    /// </summary>
    public static partial class TownBuilder
    {
        public const string TutorialScenePath = SceneDir + "/Semblance_Tutorial.unity";
        private const string TutorialProfileDir = "Assets/_Project/Tutorial/Residents";

        private static readonly List<Transform> TutorialAnchors = new List<Transform>();
        private static Transform tutorialAnchorRoot;

        [MenuItem("Semblance/Build Tutorial")]
        private static void BuildTutorialFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            BuildTutorial();
        }

        public static void BuildTutorial()
        {
            Materials.Clear();
            Anchors.Clear();
            DeferredLamps.Clear();
            BatchedMeshes.Clear();
            TutorialAnchors.Clear();
            litShader = Shader.Find("Semblance/PS1 Lit");
            if (litShader == null) throw new System.InvalidOperationException("Shader 'Semblance/PS1 Lit' not found.");
            EnsureFolder(MaterialDir);
            EnsureFolder(MeshDir);
            EnsureFolder(SceneDir);
            EnsureFolder("Assets/_Project/Tutorial");
            EnsureFolder(TutorialProfileDir);
            cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            quadMesh = GetOrCreateMesh("QuadUp", CreateQuadUp);
            cylinderMesh = GetOrCreateMesh("Cylinder6", () => CreateCylinder(6));
            boxInsideMesh = GetOrCreateMesh("BoxInside", CreateBoxInside);
            if (TextureSynth.Load("grass") == null) TextureSynth.GenerateAll();
            MenuBuilder.EnsureFontAsset();
            DefineMaterials();
            EnsureWritingMaterial();
            Mat("ExitRed", 0.45f, 0.07f, 0.05f, new Color(1f, 0.16f, 0.1f) * 1.3f);
            Mat("EyeShine", 0.5f, 0.65f, 0.3f, new Color(0.6f, 0.95f, 0.35f) * 1.6f);
            Mat("Concrete", 0.36f, 0.36f, 0.34f);
            Mat("ConcreteDark", 0.26f, 0.26f, 0.25f);
            Mat("Tube", 0.9f, 0.93f, 0.95f, new Color(0.85f, 0.92f, 1f) * 1.1f);
            EnsureLayer("NoReflection");
            EnsureLayer("MirrorGhost");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var env = new GameObject("Environment").transform;
            tutorialAnchorRoot = Group("Tutorial Anchors", null);
            var rng = new Random(1013);

            var office = Group("Office", env);
            var stairwell = Group("Stairwell", env);
            var archive = Group("Archive", env);
            var boiler = Group("Boiler Room", env);
            var exterior = Group("Exterior", env);

            BuildOffice(office, rng);
            BuildStairwell(stairwell);
            BuildArchive(archive, rng);
            var (breaker, closetDoor, furnaceGlow) = BuildBoilerRoom(boiler, rng);
            BuildExterior(exterior, rng);

            MarkStatic(env);
            foreach (var door in env.GetComponentsInChildren<World.Door>(true))
                foreach (var t in door.transform.parent.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            BakeNavMesh(env, SceneDir + "/Semblance_Tutorial_NavMesh.asset");

            Group("Lights", null);
            foreach (var lamp in DeferredLamps) lamp();

            var window = TutorialAnchor("Window", new Vector3(7.3f, 0.1f, -0.7f), Vector3.right);
            var playerCamera = BuildPlayer(window.position);
            BuildSkyAndAtmosphere(playerCamera);

            // The cinematic camera for the way in: outside, in the rain.
            var cineGO = new GameObject("Cinematic Camera");
            var cine = cineGO.AddComponent<Camera>();
            cine.fieldOfView = 52f;
            cine.nearClipPlane = 0.05f;
            cine.enabled = false;
            cineGO.SetActive(false);
            int ghost = LayerMask.NameToLayer("MirrorGhost");
            if (ghost >= 0) cine.cullingMask = ~(1 << ghost);
            cineGO.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            SetField(cineGO.AddComponent<Rendering.PS1ScreenEffect>(), "screenShader", Shader.Find("Semblance/PS1 Screen"));

            var dog = BuildDog(archive, TutorialAnchors.First(a => a.name == "A:DogEast").position);
            var alma = TutorialProfile("Alma Reyes", AlmaProfile);
            var teo = TutorialProfile("Teodoro Baptiste", TeoProfile);
            var residents = Group("Residents", null);
            PlaceTutorialResident(residents, alma, new Vector3(-3.5f, 0.1f, -4.5f), 0f);
            PlaceTutorialResident(residents, teo, new Vector3(12f, -3.5f, -0.3f), 180f);

            // Systems: only what the office needs. No case, no mimic, no story beats.
            new GameObject("Scene Boot").AddComponent<Core.SceneBoot>();
            var systems = new GameObject("Game Systems").transform;
            systems.gameObject.AddComponent<Core.GameClock>();
            systems.gameObject.AddComponent<Core.UIRoot>();
            systems.gameObject.AddComponent<Notes.Notebook>();
            systems.gameObject.AddComponent<Notes.NotebookUI>();
            systems.gameObject.AddComponent<Audio.AmbientAudio>();
            systems.gameObject.AddComponent<Events.Nerve>();
            systems.gameObject.AddComponent<Core.ScreenFader>();
            systems.gameObject.AddComponent<UI.PauseMenu>();
            var stage = systems.gameObject.AddComponent<TutorialStage>();
            SetField(stage, "anchors", TutorialAnchors.ToArray());
            SetField(stage, "cinematic", cine);
            SetField(stage, "lightRoots", new[] { office, stairwell, archive, boiler });
            SetField(stage, "emergencyLights", env.GetComponentsInChildren<Light>(true).Where(l => l.name == "Emergency Light").ToArray());
            SetField(stage, "dog", dog);
            SetField(stage, "breaker", breaker);
            SetField(stage, "closetDoor", closetDoor);
            SetField(stage, "furnaceGlow", furnaceGlow);

            EditorSceneManager.SaveScene(scene, TutorialScenePath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuBuilder.ScenePath) != null) MenuBuilder.SetBuildOrder();
            else AddSceneToBuildSettings(TutorialScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Semblance] Tutorial built: " + TutorialScenePath);
        }

        // ------------------------------------------------------------ pieces

        private static Transform TutorialAnchor(string name, Vector3 position, Vector3 forward)
        {
            var go = new GameObject("A:" + name);
            go.transform.SetParent(tutorialAnchorRoot, false);
            go.transform.position = position;
            if (forward.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(forward);
            TutorialAnchors.Add(go.transform);
            return go.transform;
        }

        /// <summary>A wall slab in the x = const plane, from (zA, yA) to (zB, yB).</summary>
        private static void WallX(Transform parent, string name, float x, float zA, float zB, float yA, float yB, string material)
            => Box(name, parent, new Vector3(x, (yA + yB) * 0.5f, (zA + zB) * 0.5f), new Vector3(0.2f, yB - yA, zB - zA), material);

        private static void WallZ(Transform parent, string name, float z, float xA, float xB, float yA, float yB, string material)
            => Box(name, parent, new Vector3((xA + xB) * 0.5f, (yA + yB) * 0.5f, z), new Vector3(xB - xA, yB - yA, 0.2f), material);

        /// <summary>A door in a wall: hinge at the given corner, panel 0.9 wide swinging about it. Returns the Door.</summary>
        private static World.Door TutorialDoor(Transform parent, string name, Vector3 hingePos, float yaw, bool locked = false, string lockedMessage = null)
        {
            var hinge = Group(name + " Hinge", parent);
            hinge.localPosition = hingePos;
            hinge.localRotation = Quaternion.Euler(0, yaw, 0);
            var panel = Box(name, hinge, new Vector3(0.45f, 1.04f, 0), new Vector3(0.86f, 2.06f, 0.07f), "Door");
            foreach (var face in new[] { -1f, 1f })
            {
                Box("Knob Rose", hinge, new Vector3(0.77f, 1.0f, face * 0.04f), new Vector3(0.07f, 0.07f, 0.012f), "Brass", collider: false);
                Box("Knob", hinge, new Vector3(0.77f, 1.0f, face * 0.07f), new Vector3(0.05f, 0.05f, 0.05f), "Brass", collider: false);
            }
            // Casing either side and over the top, so the opening reads as a doorway and not a hole.
            foreach (var x in new[] { -0.05f, 0.95f })
                Box("Casing", hinge, new Vector3(x, 1.05f, 0), new Vector3(0.08f, 2.14f, 0.3f), "Wood", collider: false);
            Box("Casing Head", hinge, new Vector3(0.45f, 2.13f, 0), new Vector3(1.08f, 0.08f, 0.3f), "Wood", collider: false);
            var door = panel.AddComponent<World.Door>();
            SetField(door, "hinge", hinge);
            if (locked)
            {
                SetField(door, "locked", true);
                SetField(door, "lockedMessage", lockedMessage ?? "It's locked.");
            }
            return door;
        }

        /// <summary>A bare bulb in a wire cage: the basement's idea of lighting.</summary>
        private static void CagedBulb(Transform parent, Vector3 pos, float range, float intensity, bool flicker = false)
        {
            InteriorLight(parent, pos, range, intensity, flicker);
            var fixture = Group("Caged Bulb", parent);
            fixture.localPosition = pos;
            Part("Base", fixture, new Vector3(0, 0.14f, 0), new Vector3(0.12f, 0.06f, 0.12f), "Metal");
            Round("Bulb", fixture, new Vector3(0, 0.02f, 0), 0.09f, 0.12f, "Bulb");
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                Part("Cage Wire", fixture, new Vector3(Mathf.Cos(a) * 0.08f, 0.02f, Mathf.Sin(a) * 0.08f), new Vector3(0.008f, 0.22f, 0.008f), "Metal");
            }
            Part("Cage Ring", fixture, new Vector3(0, -0.08f, 0), new Vector3(0.18f, 0.008f, 0.18f), "Metal");
        }

        /// <summary>A fluorescent fixture flush to the ceiling: a cool tube in a steel tray.</summary>
        private static void Fluorescent(Transform parent, Vector3 pos, float ceiling, bool flicker = false, bool lit = true)
        {
            var go = new GameObject("Fluorescent");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            if (lit)
            {
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.86f, 0.92f, 1f);
                light.range = 7.5f;
                light.intensity = 1.5f;
                light.shadows = LightShadows.None;
                if (flicker) SetField(go.AddComponent<Rendering.FlickeringLight>(), "flickerChance", 0.12f);
            }
            Part("Tray", go.transform, new Vector3(0, ceiling - pos.y - 0.05f, 0), new Vector3(1.3f, 0.1f, 0.3f), "Metal");
            Part(lit ? "Tube" : "Dead Tube", go.transform, new Vector3(0, ceiling - pos.y - 0.12f, 0), new Vector3(1.2f, 0.06f, 0.06f), lit ? "Tube" : "SignWhite");
        }

        private static GameObject ExitSign(Transform parent, Vector3 pos, float yaw)
        {
            var sign = Group("Exit Sign", parent);
            sign.localPosition = pos;
            sign.localRotation = Quaternion.Euler(0, yaw, 0);
            var face = Part("Fixture", sign, Vector3.zero, new Vector3(0.44f, 0.17f, 0.06f), "ExitRed");
            // Four pale strokes where the letters are: enough at this resolution.
            foreach (var x in new[] { -0.14f, -0.05f, 0.04f, 0.13f })
                Part("Letter", sign, new Vector3(x, 0, 0.032f), new Vector3(0.05f, 0.1f, 0.004f), "SignWhite");
            return face;
        }

        /// <summary>A steel locker you can stand inside: the archive's hiding place.</summary>
        private static void Locker(Transform root, Vector3 localBase, float yaw)
        {
            var w = Box("Locker", root, localBase + new Vector3(0, 1.05f, 0), new Vector3(0.65f, 2.1f, 1.0f), "Galvanized");
            w.transform.localRotation = Quaternion.Euler(0, yaw + 90f, 0);
            var lining = MeshPart("Locker Lining", root, boxInsideMesh, localBase + new Vector3(0, 1.05f, 0), new Vector3(0.62f, 2.06f, 0.96f), "Metal");
            lining.transform.localRotation = Quaternion.Euler(0, yaw + 90f, 0);
            var face = Group("Locker Face", root);
            face.localPosition = localBase;
            face.localRotation = Quaternion.Euler(0, yaw, 0);
            foreach (var x in new[] { -0.245f, 0.245f })
            {
                Part("Door", face, new Vector3(x, 1.05f, 0.33f), new Vector3(0.47f, 1.98f, 0.015f), "Galvanized");
                Part("Handle", face, new Vector3(x + (x < 0 ? 0.16f : -0.16f), 1.1f, 0.345f), new Vector3(0.02f, 0.12f, 0.02f), "Metal");
                for (int v = 0; v < 3; v++)
                    Part("Vent", face, new Vector3(x, 1.75f - v * 0.06f, 0.34f), new Vector3(0.3f, 0.02f, 0.006f), "Metal");
            }
            Part("Top", face, new Vector3(0, 2.11f, 0), new Vector3(1.02f, 0.03f, 0.68f), "Metal");
            var inside = Group("Inside", root);
            inside.localPosition = localBase + new Vector3(0, 0.1f, 0);
            inside.localRotation = Quaternion.Euler(0, yaw, 0);
            w.AddComponent<Events.HidingSpot>().Configure(inside, "Hide in the locker");
        }

        /// <summary>A row of archive shelving: boxes of files, each with its label strip, the odd one pulled out.</summary>
        private static void ArchiveRow(Transform root, Vector3 pos, float length, Random rng)
        {
            var s = Piece("Archive Shelf", root, pos, 0f, new Vector3(length, 2.2f, 0.7f), FloorY);
            foreach (var x in new[] { -length * 0.5f + 0.03f, length * 0.5f - 0.03f })
                Part("Upright", s, new Vector3(x, FloorY + 1.1f, 0), new Vector3(0.05f, 2.2f, 0.7f), "Metal");
            Part("Spine", s, new Vector3(0, FloorY + 1.1f, 0), new Vector3(length, 2.2f, 0.03f), "Metal");
            string[] cartons = { "Paper", "Cream", "PlainCream", "Paper" };
            for (int b = 0; b < 5; b++)
            {
                float y = FloorY + 0.06f + b * 0.43f;
                Part("Board", s, new Vector3(0, y, 0), new Vector3(length - 0.06f, 0.03f, 0.7f), "Galvanized");
                foreach (float face in new[] { -1f, 1f })
                {
                    for (float x = -length * 0.5f + 0.25f; x < length * 0.5f - 0.2f; x += 0.42f)
                    {
                        if (rng.NextDouble() < 0.12) continue;
                        float pull = rng.NextDouble() < 0.1 ? 0.08f : 0f;
                        var box = Part("Carton", s, new Vector3(x, y + 0.16f, face * (0.19f + pull)), new Vector3(0.38f, 0.27f, 0.3f), cartons[rng.Next(cartons.Length)]);
                        box.transform.localRotation = Quaternion.Euler(0, Lerp(rng, -3f, 3f), 0);
                        Part("Label", s, new Vector3(x, y + 0.2f, face * (0.345f + pull)), new Vector3(0.16f, 0.06f, 0.004f), "Writing");
                    }
                }
            }
        }

        private static Transform OfficeDesk(Transform root, Vector3 pos, float yaw, Random rng, bool working)
        {
            var desk = Table("Desk", root, pos, yaw, 1.7f, 0.85f, "Wood", 0.76f);
            float top = FloorY + 0.76f;
            Part("Modesty Panel", desk, new Vector3(0, FloorY + 0.4f, 0.33f), new Vector3(1.6f, 0.7f, 0.03f), "Wood");
            Part("Drawers", desk, new Vector3(0.55f, FloorY + 0.4f, 0), new Vector3(0.5f, 0.7f, 0.7f), "Wood");
            foreach (var y in new[] { 0.2f, 0.42f, 0.64f })
                Part("Drawer Pull", desk, new Vector3(0.55f, FloorY + y, -0.36f), new Vector3(0.12f, 0.02f, 0.02f), "Brass");
            if (working)
            {
                Part("Blotter", desk, new Vector3(-0.15f, top + 0.006f, -0.02f), new Vector3(0.62f, 0.012f, 0.45f), "Leather");
                var tw = Part("Typewriter", desk, new Vector3(0.45f, top + 0.08f, 0.05f), new Vector3(0.4f, 0.16f, 0.34f), "Metal");
                Part("Keys", desk, new Vector3(0.45f, top + 0.12f, -0.08f), new Vector3(0.34f, 0.03f, 0.12f), "Galvanized");
                Part("Roller", desk, new Vector3(0.45f, top + 0.2f, 0.16f), new Vector3(0.44f, 0.05f, 0.05f), "Metal");
                Part("Page", desk, new Vector3(0.45f, top + 0.3f, 0.17f), new Vector3(0.22f, 0.26f, 0.004f), "Writing");
            }
            else
            {
                for (int i = 0; i < 3; i++)
                    Part("Papers", desk, new Vector3(Lerp(rng, -0.5f, 0.3f), top + 0.004f + i * 0.004f, Lerp(rng, -0.25f, 0.2f)), new Vector3(0.21f, 0.004f, 0.28f), "Paper")
                        .transform.localRotation = Quaternion.Euler(0, Lerp(rng, -25f, 25f), 0);
                Part("Telephone", desk, new Vector3(0.5f, top + 0.06f, -0.15f), new Vector3(0.22f, 0.11f, 0.18f), "HideBlack");
                Part("Handset", desk, new Vector3(0.5f, top + 0.14f, -0.15f), new Vector3(0.24f, 0.04f, 0.06f), "HideBlack");
            }
            Chair(root, pos + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, -0.75f), yaw, "Leather");
            return desk;
        }

        private static void DeskLamp(Transform root, Vector3 pos, string shade, Color color, float intensity)
        {
            var lamp = Group("Desk Lamp", root);
            lamp.localPosition = pos;
            Round("Base", lamp, Vector3.zero, 0.16f, 0.02f, "Metal");
            Part("Stem", lamp, new Vector3(0, 0.2f, 0), new Vector3(0.02f, 0.38f, 0.02f), "Metal");
            Part("Arm", lamp, new Vector3(0, 0.39f, 0.08f), new Vector3(0.02f, 0.02f, 0.18f), "Metal");
            Round("Shade", lamp, new Vector3(0, 0.4f, 0.16f), 0.26f, 0.12f, shade);
            Round("Bulb", lamp, new Vector3(0, 0.36f, 0.16f), 0.06f, 0.05f, "Bulb");
            var go = new GameObject("Lamp Light");
            go.transform.SetParent(lamp, false);
            go.transform.localPosition = new Vector3(0, 0.32f, 0.16f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = 4.5f;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------ the office
        //
        // The floor plan, 16 x 11 inside, walls at x = +-8, z = +-5.5 (east is the window wall):
        //
        //   north wall:  filing cabinets | clock | the evidence board under its light |  typing table  | ARMORY cage
        //   north corridor (3.3 m) ------------------------------------------------- barricade -+
        //   [cubicle][cubicle][cubicle]   <- north row, open to the north corridor               |  Klein's desk, lamp on
        //   ========= spine =========     <- solid panel wall, x -3..3                             |  the middle window
        //   [cubicle][cubicle][cubicle]   <- south row, open to the south corridor                |
        //   south corridor (3.3 m) ------------------------------------------------- sheriff's office (glazed, locked)
        //   south wall:  coat rack | LOBBY DOOR | front desk (Reyes) | waiting chairs | coffee
        //   west passage: stairwell door under EXIT, water cooler, county map, dispatch set
        //
        // Two ways round the block normally; tonight the east end of the north corridor is stacked with archive
        // cartons and chairs, so the only route is the loop: window, lamp, south corridor, front desk, west
        // passage, north corridor, board, and back to the red EXIT sign.

        private static void BuildOffice(Transform office, Random rng)
        {
            const float h = 3.3f, ceiling = 3.2f;
            Box("Floor", office, new Vector3(0, 0.05f, 0), new Vector3(16.4f, 0.1f, 11.4f), "Floor");
            Box("Ceiling", office, new Vector3(0, ceiling + 0.06f, 0), new Vector3(16.4f, 0.12f, 11.4f), "Ceiling");
            string wall = "StoreWall";
            WallZ(office, "Wall North", 5.6f, -8.2f, 8.2f, 0, h, wall);
            // South: the lobby door, locked for the night.
            WallZ(office, "Wall South", -5.6f, -8.2f, -6.5f, 0, h, wall);
            WallZ(office, "Wall South", -5.6f, -5.6f, 8.2f, 0, h, wall);
            WallZ(office, "Lintel", -5.6f, -6.5f, -5.6f, 2.14f, h, wall);
            TutorialDoor(office, "Lobby Door", new Vector3(-6.5f, 0.1f, -5.6f), 0f, true, "Locked for the night. The lobby's dark, and the sheriff has the only key.");
            // West: the stairwell door, under the exit sign.
            WallX(office, "Wall West", -8.1f, -5.7f, 2.5f, 0, h, wall);
            WallX(office, "Wall West", -8.1f, 3.4f, 5.7f, 0, h, wall);
            WallX(office, "Lintel", -8.1f, 2.5f, 3.4f, 2.14f, h, wall);
            TutorialDoor(office, "Stairwell Door", new Vector3(-8.1f, 0.1f, 2.5f), -90f);
            ExitSign(office, new Vector3(-7.95f, 2.42f, 2.95f), 90f);
            // East: three windows onto the rain. No glass: the room's open to the night, and the night to it.
            float[] windows = { -3.2f, 0f, 3.2f };
            WallX(office, "Wall East Low", 8.1f, -5.7f, 5.7f, 0, 0.95f, wall);
            WallX(office, "Wall East High", 8.1f, -5.7f, 5.7f, 2.45f, h, wall);
            float[][] piers = { new[] { -5.7f, -3.85f }, new[] { -2.55f, -0.65f }, new[] { 0.65f, 2.55f }, new[] { 3.85f, 5.7f } };
            foreach (var p in piers) WallX(office, "Wall East Pier", 8.1f, p[0], p[1], 0.95f, 2.45f, wall);
            foreach (float z in windows)
            {
                Box("Window Head", office, new Vector3(8.1f, 2.49f, z), new Vector3(0.3f, 0.08f, 1.48f), "Trim", collider: false);
                Box("Window Sill", office, new Vector3(8.05f, 0.91f, z), new Vector3(0.38f, 0.08f, 1.52f), "Trim", collider: false);
                foreach (var side in new[] { -1f, 1f })
                    Box("Window Jamb", office, new Vector3(8.1f, 1.7f, z + side * 0.69f), new Vector3(0.3f, 1.5f, 0.08f), "Trim", collider: false);
                Box("Mullion", office, new Vector3(8.1f, 1.7f, z), new Vector3(0.06f, 1.5f, 0.04f), "Trim", collider: false);
                Box("Transom", office, new Vector3(8.1f, 1.9f, z), new Vector3(0.06f, 0.04f, 1.3f), "Trim", collider: false);
                var glass = new GameObject("Window Glass");
                glass.transform.SetParent(office, false);
                glass.transform.localPosition = new Vector3(8.1f, 1.7f, z);
                glass.AddComponent<BoxCollider>().size = new Vector3(0.1f, 1.5f, 1.3f);
                Box("Blind", office, new Vector3(7.92f, 2.37f, z), new Vector3(0.12f, 0.12f, 1.34f), "PlainCream", collider: false);
                Box("Blind Cord", office, new Vector3(7.92f, 1.9f, z + 0.6f), new Vector3(0.006f, 0.9f, 0.006f), "Rope", collider: false);
                SoundEmitter(office, new Vector3(7.9f, 1.7f, z), "rain_loop", 0.22f, 7f);
            }
            // Skirting and a picture rail: the room's been here since the thirties.
            Part("Skirting", office, new Vector3(0, 0.17f, 5.47f), new Vector3(16f, 0.14f, 0.03f), "Wood");
            Part("Skirting", office, new Vector3(0, 0.17f, -5.47f), new Vector3(16f, 0.14f, 0.03f), "Wood");
            Part("Skirting", office, new Vector3(-7.97f, 0.17f, 0), new Vector3(0.03f, 0.14f, 11f), "Wood");
            Part("Skirting", office, new Vector3(7.97f, 0.17f, 0), new Vector3(0.03f, 0.14f, 11f), "Wood");
            Part("Picture Rail", office, new Vector3(0, 2.75f, 5.47f), new Vector3(16f, 0.05f, 0.04f), "Wood");

            // Lighting: tubes over the corridors and the block, one of them tired; the lamps make the pools you walk between.
            foreach (var (x, z, flicker) in new[] { (-4.5f, 3.7f, false), (0f, 3.7f, false), (-4.5f, -3.7f, true), (0f, -3.7f, false), (4.5f, -3.7f, false), (-1.6f, 0f, false), (1.6f, 0f, false), (6.2f, 1.2f, false) })
                Fluorescent(office, new Vector3(x, ceiling - 0.3f, z), ceiling, flicker);
            SoundEmitter(office, new Vector3(0, 3f, 0), "lamp_buzz_loop", 0.1f, 10f);

            // Klein's desk, by the middle window, the lamp still on. The first pool of light.
            var klein = OfficeDesk(office, new Vector3(6.3f, 0, 0.6f), 90f, rng, working: true);
            DeskLamp(office, new Vector3(6.1f, FloorY + 0.76f, 1.25f), "LampShade", new Color(1f, 0.78f, 0.5f), 2.6f);
            Round("Ashtray", klein, new Vector3(-0.1f, FloorY + 0.77f, -0.33f), 0.12f, 0.03f, "Glass");
            Round("Coffee Cup", klein, new Vector3(-0.55f, FloorY + 0.8f, -0.3f), 0.08f, 0.09f, "SignWhite");
            Part("Case Folder", klein, new Vector3(0.1f, FloorY + 0.772f, -0.3f), new Vector3(0.3f, 0.012f, 0.24f), "Mustard");
            var envelope = Part("Envelope", klein, new Vector3(-0.15f, FloorY + 0.776f, -0.02f), new Vector3(0.24f, 0.012f, 0.13f), "Paper");
            envelope.transform.localRotation = Quaternion.Euler(0, 12f, 0);
            Part("Postmark", klein, new Vector3(-0.08f, FloorY + 0.784f, 0.01f), new Vector3(0.05f, 0.004f, 0.05f), "Writing");
            Part("Sheriff's Note", klein, new Vector3(-0.3f, FloorY + 0.774f, -0.1f), new Vector3(0.12f, 0.004f, 0.08f), "Writing");
            Inspect(envelope, "Read the letter",
                "A plain envelope, postmarked SEMBLANCE, OCT 10. Inside, one line in pencil, pressed hard enough to tear the paper:\n\n<i>\"One of us is not one of us.\"</i>\n\nClipped to it, in the sheriff's hand: <i>Klein. Look into this.</i>",
                new Vector3(0.5f, 0.4f, 0.5f), clue: "tut_letter");
            TutorialAnchor("Desk", new Vector3(5.4f, 0.1f, 0.6f), Vector3.right);
            Part("Klein's Hat", office, new Vector3(6.9f, FloorY + 1.84f, -1.3f), new Vector3(0.32f, 0.09f, 0.32f), "Suit");
            Part("Klein's Hat Crown", office, new Vector3(6.9f, FloorY + 1.95f, -1.3f), new Vector3(0.18f, 0.14f, 0.18f), "Suit");
            Part("Hat Stand", office, new Vector3(6.9f, FloorY + 0.9f, -1.3f), new Vector3(0.04f, 1.8f, 0.04f), "Wood");
            Part("Coat", office, new Vector3(6.9f, FloorY + 1.25f, -1.2f), new Vector3(0.28f, 0.95f, 0.16f), "Suit");

            BuildCubicles(office, rng);
            BuildSheriffsOffice(office, rng);
            BuildArmory(office, rng);
            BuildFrontDesk(office, rng);
            BuildOfficeClutter(office, rng);
            BuildBarricade(office, rng);

            // The board on the back wall, under its own light: the far pool, where the route ends.
            var board = Group("Evidence Board", office);
            board.localPosition = new Vector3(0f, 1.85f, 5.47f);
            var cork = Part("Cork", board, Vector3.zero, new Vector3(2.5f, 1.45f, 0.04f), "Hay");
            Part("Board Frame", board, new Vector3(0, 0.74f, 0), new Vector3(2.58f, 0.05f, 0.05f), "Wood");
            Part("Board Frame", board, new Vector3(0, -0.74f, 0), new Vector3(2.58f, 0.05f, 0.05f), "Wood");
            Part("Board Frame", board, new Vector3(-1.27f, 0, 0), new Vector3(0.05f, 1.5f, 0.05f), "Wood");
            Part("Board Frame", board, new Vector3(1.27f, 0, 0), new Vector3(0.05f, 1.5f, 0.05f), "Wood");
            Part("Map", board, new Vector3(-0.72f, 0.1f, -0.025f), new Vector3(0.75f, 0.6f, 0.005f), "MapPaper");
            var pinned = new List<Vector3>();
            for (int i = 0; i < 11; i++)
            {
                var p = new Vector3(Lerp(rng, -0.25f, 1.1f), Lerp(rng, -0.6f, 0.6f), -0.025f);
                pinned.Add(p);
                var sheet = Part("Pinned Paper", board, p, new Vector3(0.2f, 0.26f, 0.004f), i % 3 == 0 ? "Writing" : "Paper");
                sheet.transform.localRotation = Quaternion.Euler(0, 0, Lerp(rng, -9f, 9f));
                Part("Pin", board, p + new Vector3(0, 0.12f, -0.006f), new Vector3(0.016f, 0.016f, 0.01f), i % 2 == 0 ? "BarnRed" : "Brass");
            }
            Part("Pinned Letter", board, new Vector3(0.4f, 0.05f, -0.03f), new Vector3(0.22f, 0.3f, 0.004f), "Writing");
            Part("Pin", board, new Vector3(0.4f, 0.19f, -0.036f), new Vector3(0.016f, 0.016f, 0.01f), "BarnRed");
            for (int i = 0; i < 4; i++)
            {
                var from = pinned[i * 2];
                var to = new Vector3(0.4f, 0.19f, -0.034f);
                var mid = (from + to) * 0.5f;
                var dir = to - from;
                var thread = Part("Thread", board, mid, new Vector3(dir.magnitude, 0.006f, 0.003f), "PlainBarnRed");
                thread.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }
            Inspect(cork, "Look at the board",
                "Three months of the county: a stolen truck, a barn fire, a man who walked into the river and didn't walk out. In the middle, pinned where the sheriff could see it from his door, the Semblance letter, with red thread run to everything and nothing.",
                new Vector3(2.4f, 1.4f, 0.5f), clue: "tut_board");
            Part("Picture Light", office, new Vector3(0f, 2.72f, 5.3f), new Vector3(1.2f, 0.06f, 0.14f), "Brass");
            Part("Picture Light Bulb", office, new Vector3(0f, 2.68f, 5.32f), new Vector3(1.0f, 0.02f, 0.06f), "Bulb");
            var boardLight = new GameObject("Board Light");
            boardLight.transform.SetParent(office, false);
            boardLight.transform.localPosition = new Vector3(0f, 2.55f, 5.0f);
            var bl = boardLight.AddComponent<Light>();
            bl.type = LightType.Point;
            bl.color = new Color(1f, 0.85f, 0.6f);
            bl.range = 4f;
            bl.intensity = 1.8f;
            bl.shadows = LightShadows.None;

            // The clock, stopped at twenty to twelve as far as anyone will ever know.
            var clock = Group("Wall Clock", office);
            clock.localPosition = new Vector3(-3.4f, 2.6f, 5.47f);
            Round("Clock Face", clock, Vector3.zero, 0.44f, 0.04f, "SignWhite").transform.localRotation = Quaternion.Euler(90f, 0, 0);
            Round("Clock Rim", clock, new Vector3(0, 0, 0.005f), 0.48f, 0.03f, "HideBlack").transform.localRotation = Quaternion.Euler(90f, 0, 0);
            Part("Hour Hand", clock, new Vector3(-0.06f, 0.07f, -0.03f), new Vector3(0.025f, 0.15f, 0.005f), "HideBlack").transform.localRotation = Quaternion.Euler(0, 0, 32f);
            Part("Minute Hand", clock, new Vector3(-0.08f, 0.05f, -0.035f), new Vector3(0.015f, 0.2f, 0.005f), "HideBlack").transform.localRotation = Quaternion.Euler(0, 0, 60f);
            SoundEmitter(office, new Vector3(-3.4f, 2.6f, 5.3f), "clock_tick_loop", 0.2f, 9f);

            AddMarker(office, "the Investigations floor", 11f, 2);
            Zone(office, "Office Floor", new Vector3(0, 0, 0), new Vector3(16f, 1f, 11f), Audio.Surface.Wood, 1);
        }

        /// <summary>
        /// Six cubicles in two rows of three, back to back along a solid spine (x -3..3, z -2.2..2.2). Each one is
        /// 2 m wide and 2.2 deep, walled on three sides, with a 1.1 m opening onto its corridor. Everything pinned
        /// to a panel is placed on that panel's face.
        /// </summary>
        private static void BuildCubicles(Transform office, Random rng)
        {
            const float h = 1.55f, t = 0.07f;
            void Panel(string name, Vector3 center, float length, bool alongX)
            {
                var size = alongX ? new Vector3(length, h, t) : new Vector3(t, h, length);
                Box(name, office, center + Vector3.up * (FloorY + h * 0.5f), size, "PlainSage");
                Box("Panel Trim", office, center + Vector3.up * (FloorY + h + 0.02f), alongX ? new Vector3(length, 0.04f, t + 0.03f) : new Vector3(t + 0.03f, 0.04f, length), "HideBlack", collider: false);
                Box("Panel Foot", office, center + Vector3.up * (FloorY + 0.03f), alongX ? new Vector3(length, 0.06f, t + 0.03f) : new Vector3(t + 0.03f, 0.06f, length), "HideBlack", collider: false);
            }
            Panel("Spine", new Vector3(0, 0, 0), 6f + t, true);
            foreach (float x in new[] { -3f, -1f, 1f, 3f })
            {
                Panel("Side Panel", new Vector3(x, 0, 1.1f), 2.2f, false);
                Panel("Side Panel", new Vector3(x, 0, -1.1f), 2.2f, false);
            }
            int index = 0;
            foreach (float row in new[] { 1f, -1f })          // north row opens north, south row opens south
                foreach (float cx in new[] { -2f, 0f, 2f })   // cubicle centres
                {
                    float front = row * 2.2f;
                    // Front panel on the left 0.9 m, the opening on the right (mirrored for the south row so openings alternate).
                    float panelX = row > 0 ? cx - 1f + 0.45f : cx + 1f - 0.45f;
                    Panel("Front Panel", new Vector3(panelX, 0, front), 0.9f, true);
                    // Desk against the spine, chair facing it.
                    var desk = OfficeDesk(office, new Vector3(cx, 0, row * 0.45f), row > 0 ? 0f : 180f, rng, working: index == 1);
                    float backZ = row * (t * 0.5f + 0.006f);         // the spine's face
                    float sideX = cx - 1f + t * 0.5f + 0.006f;       // the left panel's inner face
                    CubicleStory(office, desk, index, cx, row, backZ, sideX, rng);
                    index++;
                }
            Part("Wastebasket", office, new Vector3(-3.3f, FloorY + 0.15f, 2.5f), new Vector3(0.26f, 0.3f, 0.26f), "Galvanized");
            Part("Wastebasket", office, new Vector3(3.3f, FloorY + 0.15f, -2.5f), new Vector3(0.26f, 0.3f, 0.26f), "Galvanized");
        }

        /// <summary>What each cubicle says about whoever sits there.</summary>
        private static void CubicleStory(Transform office, Transform desk, int index, float cx, float row, float backZ, float sideX, Random rng)
        {
            float top = FloorY + 0.76f;
            // Something on the spine, something on the side panel: always on a face, never in the air.
            Part("Calendar", office, new Vector3(cx + Lerp(rng, -0.5f, 0.5f), FloorY + 1.2f, backZ), new Vector3(0.22f, 0.3f, 0.008f), "Writing");
            Part("Pinned Photo", office, new Vector3(sideX, FloorY + 1.1f, row * Lerp(rng, 0.5f, 1.4f)), new Vector3(0.008f, 0.1f, 0.14f), "Photo");
            switch (index)
            {
                case 0: // A coat left over the chair, a cup gone cold: somebody meant to come back.
                    Part("Coat", office, new Vector3(cx, FloorY + 0.75f, row * 1.3f), new Vector3(0.44f, 0.5f, 0.2f), "Suit");
                    Round("Cold Cup", desk, new Vector3(-0.4f, top + 0.045f, -0.2f), 0.08f, 0.09f, "SignWhite");
                    break;
                case 1: // The typewriter mid-sentence; an ashtray that needed emptying yesterday.
                    Round("Ashtray", desk, new Vector3(-0.5f, top + 0.015f, -0.25f), 0.12f, 0.03f, "Glass");
                    for (int i = 0; i < 5; i++) Part("Butt", desk, new Vector3(-0.5f + Lerp(rng, -0.04f, 0.04f), top + 0.035f, -0.25f + Lerp(rng, -0.04f, 0.04f)), new Vector3(0.03f, 0.006f, 0.006f), "SignWhite").transform.localRotation = Quaternion.Euler(0, Lerp(rng, 0, 180f), 0);
                    break;
                case 2: // A child's drawing pinned up, and a lunchbox never opened.
                    Part("Drawing", office, new Vector3(cx + 0.55f, FloorY + 1.0f, backZ), new Vector3(0.24f, 0.18f, 0.008f), "Paper");
                    foreach (var (dx, dy, mat) in new[] { (-0.06f, 0f, "PlainBarnRed"), (0.02f, 0.02f, "PlainBlue"), (0.07f, -0.03f, "Mustard") })
                        Part("Crayon", office, new Vector3(cx + 0.55f + dx, FloorY + 1.0f + dy, backZ - row * 0.005f), new Vector3(0.05f, 0.06f, 0.004f), mat);
                    Part("Lunchbox", desk, new Vector3(0.45f, top + 0.08f, -0.2f), new Vector3(0.26f, 0.16f, 0.14f), "PlainBlue");
                    Part("Lunchbox Latch", desk, new Vector3(0.45f, top + 0.08f, -0.275f), new Vector3(0.06f, 0.03f, 0.01f), "Brass");
                    break;
                case 3: // Cleared out: a carton packed on the desk, the nameplate face down.
                    Part("Packed Carton", desk, new Vector3(0, top + 0.14f, 0), new Vector3(0.4f, 0.28f, 0.32f), "Paper");
                    Part("Carton Flap", desk, new Vector3(0, top + 0.29f, 0.17f), new Vector3(0.4f, 0.01f, 0.1f), "Paper").transform.localRotation = Quaternion.Euler(-25f, 0, 0);
                    Part("Nameplate", desk, new Vector3(-0.5f, top + 0.015f, -0.25f), new Vector3(0.28f, 0.03f, 0.06f), "Brass");
                    break;
                case 4: // Yesterday's paper, folded to the back page, and a transistor radio.
                    Part("Newspaper", desk, new Vector3(-0.1f, top + 0.006f, -0.1f), new Vector3(0.42f, 0.012f, 0.3f), "Paper").transform.localRotation = Quaternion.Euler(0, -14f, 0);
                    Part("Headline", desk, new Vector3(-0.1f, top + 0.014f, -0.1f), new Vector3(0.36f, 0.004f, 0.24f), "Writing").transform.localRotation = Quaternion.Euler(0, -14f, 0);
                    Part("Transistor", desk, new Vector3(0.5f, top + 0.06f, -0.2f), new Vector3(0.18f, 0.12f, 0.06f), "HideBlack");
                    Part("Aerial", desk, new Vector3(0.56f, top + 0.3f, -0.2f), new Vector3(0.006f, 0.4f, 0.006f), "Metal").transform.localRotation = Quaternion.Euler(0, 0, -20f);
                    break;
                default: // Files to the ceiling and half a sandwich on its wax paper.
                    for (int i = 0; i < 3; i++) Part("File Stack", desk, new Vector3(0.45f, top + 0.05f + i * 0.1f, -0.1f + i * 0.02f), new Vector3(0.3f, 0.09f, 0.24f), i == 1 ? "Mustard" : "Paper").transform.localRotation = Quaternion.Euler(0, Lerp(rng, -6f, 6f), 0);
                    Part("Wax Paper", desk, new Vector3(-0.45f, top + 0.004f, -0.25f), new Vector3(0.22f, 0.004f, 0.2f), "Cream");
                    Part("Sandwich", desk, new Vector3(-0.42f, top + 0.03f, -0.25f), new Vector3(0.12f, 0.05f, 0.1f), "PlainCream");
                    Part("Filling", desk, new Vector3(-0.42f, top + 0.03f, -0.25f), new Vector3(0.13f, 0.012f, 0.11f), "Mustard");
                    break;
            }
        }

        /// <summary>The front desk along the south wall, the sergeant behind it, the lobby door at her elbow.</summary>
        private static void BuildFrontDesk(Transform office, Random rng)
        {
            var front = Piece("Front Desk", office, new Vector3(-2.75f, 0, -4.4f), 0f, new Vector3(3.6f, 1.05f, 0.8f), FloorY);
            Part("Counter Front", front, new Vector3(0, FloorY + 0.5f, 0.38f), new Vector3(3.6f, 1.0f, 0.04f), "Door");
            Part("Counter Top", front, new Vector3(0, FloorY + 1.0f, 0), new Vector3(3.7f, 0.05f, 0.9f), "Wood");
            Part("Counter Shelf", front, new Vector3(0, FloorY + 0.5f, -0.1f), new Vector3(3.4f, 0.04f, 0.5f), "Wood");
            Part("Counter Edge", front, new Vector3(0, FloorY + 0.975f, 0.44f), new Vector3(3.7f, 0.05f, 0.03f), "Brass");
            Part("Telephone", front, new Vector3(-1.0f, FloorY + 1.08f, -0.1f), new Vector3(0.22f, 0.11f, 0.18f), "HideBlack");
            Part("Handset", front, new Vector3(-1.0f, FloorY + 1.16f, -0.1f), new Vector3(0.24f, 0.04f, 0.06f), "HideBlack");
            Part("Telephone", front, new Vector3(-0.55f, FloorY + 1.08f, -0.15f), new Vector3(0.22f, 0.11f, 0.18f), "HideBlack");
            Part("Handset", front, new Vector3(-0.55f, FloorY + 1.16f, -0.15f), new Vector3(0.24f, 0.04f, 0.06f), "HideBlack");
            Part("Log Book", front, new Vector3(0.4f, FloorY + 1.04f, -0.05f), new Vector3(0.34f, 0.03f, 0.26f), "Leather");
            Part("Log Page", front, new Vector3(0.4f, FloorY + 1.058f, -0.05f), new Vector3(0.3f, 0.004f, 0.22f), "Writing");
            Part("Nameplate", front, new Vector3(1.2f, FloorY + 1.06f, 0.25f), new Vector3(0.28f, 0.06f, 0.03f), "Brass");
            Part("Bell", front, new Vector3(-1.5f, FloorY + 1.06f, 0.25f), new Vector3(0.09f, 0.06f, 0.09f), "Chrome");
            var radio = Part("Radio", front, new Vector3(1.4f, FloorY + 1.1f, -0.2f), new Vector3(0.32f, 0.17f, 0.14f), "Wood");
            Part("Dial", front, new Vector3(1.4f, FloorY + 1.1f, -0.125f), new Vector3(0.2f, 0.08f, 0.005f), "SignWhite");
            SoundEmitter(front, new Vector3(1.4f, FloorY + 1.1f, -0.2f), "static_loop", 0.035f, 5f);
            Inspect(radio.gameObject, "Listen to the radio", "County dispatch, turned down to a murmur. A deputy somewhere asking for a tow. Rain reported on every road north.", new Vector3(0.4f, 0.3f, 0.3f));
            DeskLamp(office, new Vector3(-4.2f, FloorY + 1.03f, -4.5f), "Sage", new Color(0.8f, 1f, 0.75f), 1.8f);
            Chair(office, new Vector3(-2.3f, 0, -5.05f), 0f, "Leather");
            // The duty roster beside her, where she'd see the swap if it were on it.
            var roster = Group("Duty Roster", office);
            roster.localPosition = new Vector3(-5.0f, 1.9f, -5.465f);
            Part("Roster Board", roster, Vector3.zero, new Vector3(0.9f, 0.7f, 0.03f), "Hay");
            Part("Roster Sheet", roster, new Vector3(0, 0.05f, -0.02f), new Vector3(0.5f, 0.5f, 0.004f), "Writing");
            Part("Notice", roster, new Vector3(0.32f, 0.15f, -0.02f), new Vector3(0.16f, 0.22f, 0.004f), "Paper");
            Part("Notice", roster, new Vector3(-0.33f, -0.15f, -0.02f), new Vector3(0.16f, 0.2f, 0.004f), "Paper");
            Inspect(roster.GetChild(0).gameObject, "Read the roster",
                "DUTY ROSTER, WEEK OF OCT 10. Nights: REYES. Custodial: T. BAPTISTE, M-W-F. Thursday is blank. Somebody has pencilled a question mark in it and rubbed most of it out again.",
                new Vector3(1f, 0.8f, 0.5f), clue: "tut_roster");
            // The waiting chairs and the wanted sheets over them, the coffee at the far end.
            foreach (var x in new[] { 0.3f, 1.0f, 1.7f })
                Chair(office, new Vector3(x, 0, -5.15f), 0f, "Vinyl");
            foreach (var (x, y) in new[] { (0.0f, 2.05f), (0.45f, 1.75f), (0.95f, 2.05f), (1.45f, 1.75f), (1.95f, 2.0f) })
            {
                Part("Wanted Sheet", office, new Vector3(x, y, -5.465f), new Vector3(0.24f, 0.32f, 0.006f), "Paper").transform.localRotation = Quaternion.Euler(0, 0, Lerp(rng, -5f, 5f));
                Part("Wanted Face", office, new Vector3(x, y + 0.04f, -5.455f), new Vector3(0.12f, 0.14f, 0.004f), "Photo");
                Part("Wanted Text", office, new Vector3(x, y - 0.1f, -5.455f), new Vector3(0.2f, 0.08f, 0.004f), "Writing");
            }
            var coffee = Table("Coffee Table", office, new Vector3(3.3f, 0, -4.95f), 0f, 1.1f, 0.5f, "Wood", 0.8f);
            Round("Percolator", coffee, new Vector3(-0.25f, FloorY + 0.95f, 0), 0.2f, 0.3f, "Chrome");
            Round("Percolator Lid", coffee, new Vector3(-0.25f, FloorY + 1.11f, 0), 0.08f, 0.04f, "HideBlack");
            foreach (var x in new[] { 0.15f, 0.35f })
                Round("Cup", coffee, new Vector3(x, FloorY + 0.84f, 0.05f), 0.08f, 0.08f, "SignWhite");
            Part("Sugar Tin", coffee, new Vector3(0.25f, FloorY + 0.87f, -0.15f), new Vector3(0.1f, 0.14f, 0.1f), "Mustard");
            Part("Coffee Notice", office, new Vector3(3.3f, 1.6f, -5.465f), new Vector3(0.2f, 0.14f, 0.006f), "Writing");
            // A runner down the south corridor: a line on the floor that goes where you're going.
            Part("Runner", office, new Vector3(0f, FloorY + 0.006f, -3.3f), new Vector3(10f, 0.012f, 1.1f), "Rug");
        }

        /// <summary>The sheriff's office in the south-east corner: a glazed partition, blinds down, his light off, the door locked. No gaps.</summary>
        private static void BuildSheriffsOffice(Transform office, Random rng)
        {
            const float h = 3.3f, t = 0.12f;
            // A partition segment: wood to 1.1, glass to 2.3, plaster to the ceiling, the glass the exact length of the segment.
            void Segment(Vector3 a, Vector3 b, bool blindsSideNegX)
            {
                bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
                float length = alongX ? Mathf.Abs(b.x - a.x) : Mathf.Abs(b.z - a.z);
                var c = (a + b) * 0.5f;
                Vector3 S(float len, float thick) => alongX ? new Vector3(len, 1f, thick) : new Vector3(thick, 1f, len);
                Box("Partition Low", office, new Vector3(c.x, 0.55f, c.z), Vector3.Scale(S(length, t), new Vector3(1, 1.1f, 1)), "Wood");
                Box("Partition Glass", office, new Vector3(c.x, 1.7f, c.z), Vector3.Scale(S(length, t * 0.4f), new Vector3(1, 1.2f, 1)), "Glass");
                Box("Partition High", office, new Vector3(c.x, 2.8f, c.z), Vector3.Scale(S(length, t), new Vector3(1, h - 2.3f, 1)), "StoreWall");
                foreach (var y in new[] { 1.1f, 2.3f })
                    Box("Partition Rail", office, new Vector3(c.x, y, c.z), Vector3.Scale(S(length + 0.02f, t + 0.02f), new Vector3(1, 0.06f, 1)), "Wood", collider: false);
                // Blinds inside, drawn, the full length.
                var off = alongX ? new Vector3(0, 0, blindsSideNegX ? -0.08f : 0.08f) : new Vector3(blindsSideNegX ? -0.08f : 0.08f, 0, 0);
                Box("Blind", office, new Vector3(c.x, 1.7f, c.z) + off, Vector3.Scale(S(length - 0.02f, 0.02f), new Vector3(1, 1.16f, 1)), "PlainCream", collider: false);
            }
            // West wall of the office with the door in it; north wall full length.
            Segment(new Vector3(4.4f, 0, -5.5f), new Vector3(4.4f, 0, -3.4f), false);
            Box("Door Post", office, new Vector3(4.4f, 1.65f, -2.45f), new Vector3(t, h, 0.1f), "StoreWall");
            Box("Door Lintel", office, new Vector3(4.4f, 2.72f, -2.95f), new Vector3(t, h - 2.14f, 1.0f), "StoreWall");
            Segment(new Vector3(4.4f, 0, -2.4f), new Vector3(8.0f, 0, -2.4f), false);
            Box("Corner Post", office, new Vector3(4.4f, 1.65f, -2.4f), new Vector3(t + 0.02f, h, t + 0.02f), "Wood");
            TutorialDoor(office, "Sheriff's Door", new Vector3(4.4f, 0.1f, -3.4f), -90f, true, "The sheriff's office. Locked, and his light's off. He went home at six like a man with a home.");
            Part("Door Plate", office, new Vector3(4.355f, 1.7f, -2.95f), new Vector3(0.006f, 0.08f, 0.26f), "Brass");
            // Inside: the big desk, the flag, the cabinet, the hat on the stand, his tube off.
            var desk = Table("Sheriff's Desk", office, new Vector3(6.4f, 0, -4.3f), 0f, 2.0f, 0.9f, "Wood", 0.78f);
            Part("Modesty Panel", desk, new Vector3(0, FloorY + 0.4f, 0.4f), new Vector3(1.9f, 0.74f, 0.03f), "Wood");
            Part("Blotter", desk, new Vector3(0, FloorY + 0.786f, 0), new Vector3(0.7f, 0.012f, 0.5f), "Leather");
            Part("Telephone", desk, new Vector3(0.7f, FloorY + 0.84f, -0.2f), new Vector3(0.22f, 0.11f, 0.18f), "HideBlack");
            Part("Handset", desk, new Vector3(0.7f, FloorY + 0.92f, -0.2f), new Vector3(0.24f, 0.04f, 0.06f), "HideBlack");
            DeskLamp(office, new Vector3(5.7f, FloorY + 0.78f, -4.6f), "Sage", new Color(0.8f, 1f, 0.75f), 0f);
            Chair(office, new Vector3(6.4f, 0, -5.05f), 0f, "Leather");
            Fluorescent(office, new Vector3(6.2f, 2.9f, -3.9f), 3.2f, false, lit: false);
            var flag = Group("Flag", office);
            flag.localPosition = new Vector3(7.6f, 0, -5.1f);
            Round("Flag Pole", flag, new Vector3(0, FloorY + 1.1f, 0), 0.04f, 2.2f, "Brass");
            Round("Flag Base", flag, new Vector3(0, FloorY + 0.02f, 0), 0.36f, 0.04f, "Brass");
            Part("Flag Cloth", flag, new Vector3(0.12f, FloorY + 1.6f, 0), new Vector3(0.24f, 1.0f, 0.05f), "PlainBlue");
            Part("Flag Stripe", flag, new Vector3(0.12f, FloorY + 1.35f, 0.028f), new Vector3(0.24f, 0.4f, 0.006f), "PlainBarnRed");
            var cab = Piece("Filing Cabinet", office, new Vector3(7.6f, 0, -2.8f), 0f, new Vector3(0.5f, 1.32f, 0.62f), FloorY);
            Part("Body", cab, new Vector3(0, FloorY + 0.66f, 0), new Vector3(0.5f, 1.32f, 0.62f), "Galvanized");
            Part("Hat Stand", office, new Vector3(4.9f, FloorY + 0.9f, -5.1f), new Vector3(0.04f, 1.8f, 0.04f), "Wood");
            Part("Hat", office, new Vector3(4.9f, FloorY + 1.84f, -5.1f), new Vector3(0.34f, 0.1f, 0.34f), "Suit");
            Part("Hat Crown", office, new Vector3(4.9f, FloorY + 1.95f, -5.1f), new Vector3(0.18f, 0.14f, 0.18f), "Suit");
            Picture(office, new Vector3(6.4f, 2.0f, -5.47f), 0f, new Vector2(0.5f, 0.38f));
        }

        /// <summary>The armory: a cage in the north-east corner, rifles racked behind the mesh, the gate padlocked.</summary>
        private static void BuildArmory(Transform office, Random rng)
        {
            const float h = 2.4f;
            float x0 = 5.2f, z0 = 2.7f, x1 = 8.0f, z1 = 5.5f;
            void Mesh(Vector3 a, Vector3 b, float gapFrom = 0f, float gapTo = 0f)
            {
                bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
                float length = alongX ? b.x - a.x : b.z - a.z;
                var c = (a + b) * 0.5f;
                foreach (var e in new[] { a, b })
                    Box("Cage Post", office, new Vector3(e.x, FloorY + h * 0.5f, e.z), new Vector3(0.06f, h, 0.06f), "Metal");
                Box("Cage Rail", office, new Vector3(c.x, FloorY + h, c.z), new Vector3(alongX ? length : 0.05f, 0.05f, alongX ? 0.05f : length), "Metal", collider: false);
                Box("Cage Rail", office, new Vector3(c.x, FloorY + 0.03f, c.z), new Vector3(alongX ? length : 0.05f, 0.05f, alongX ? 0.05f : length), "Metal", collider: false);
                for (float d = 0.1f; d < length; d += 0.1f)
                {
                    if (gapTo > gapFrom && d > gapFrom && d < gapTo) continue;
                    var p = a + (alongX ? new Vector3(d, 0, 0) : new Vector3(0, 0, d));
                    Box("Wire", office, new Vector3(p.x, FloorY + h * 0.5f, p.z), new Vector3(0.012f, h, 0.012f), "Galvanized", collider: false);
                }
                for (float y = 0.3f; y < h; y += 0.3f)
                {
                    if (gapTo > gapFrom)
                    {
                        Box("Wire", office, new Vector3(alongX ? a.x + gapFrom * 0.5f : c.x, FloorY + y, alongX ? c.z : a.z + gapFrom * 0.5f), new Vector3(alongX ? gapFrom : 0.012f, 0.012f, alongX ? 0.012f : gapFrom), "Galvanized", collider: false);
                        Box("Wire", office, new Vector3(alongX ? a.x + (gapTo + length) * 0.5f : c.x, FloorY + y, alongX ? c.z : a.z + (gapTo + length) * 0.5f), new Vector3(alongX ? length - gapTo : 0.012f, 0.012f, alongX ? 0.012f : length - gapTo), "Galvanized", collider: false);
                    }
                    else
                        Box("Wire", office, new Vector3(c.x, FloorY + y, c.z), new Vector3(alongX ? length : 0.012f, 0.012f, alongX ? 0.012f : length), "Galvanized", collider: false);
                }
                // One invisible slab per run holds you back, so the wire doesn't need a hundred colliders.
                void Slab(float from, float to)
                {
                    var block = new GameObject("Cage Collider");
                    block.transform.SetParent(office, false);
                    var mid = a + (alongX ? new Vector3((from + to) * 0.5f, 0, 0) : new Vector3(0, 0, (from + to) * 0.5f));
                    block.transform.localPosition = new Vector3(mid.x, FloorY + h * 0.5f, mid.z);
                    block.AddComponent<BoxCollider>().size = new Vector3(alongX ? to - from : 0.06f, h, alongX ? 0.06f : to - from);
                }
                if (gapTo > gapFrom) { Slab(0f, gapFrom); Slab(gapTo, length); }
                else Slab(0f, length);
            }
            Mesh(new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), 0.3f, 1.2f); // front, with the gate
            Mesh(new Vector3(x0, 0, z0), new Vector3(x0, 0, z1));
            var hinge = Group("Armory Gate Hinge", office);
            hinge.localPosition = new Vector3(x0 + 1.2f, FloorY, z0);
            hinge.localRotation = Quaternion.Euler(0, 180f, 0);
            var gate = Box("Armory Gate", hinge, new Vector3(0.45f, h * 0.5f, 0), new Vector3(0.9f, h, 0.04f), "Galvanized");
            gate.GetComponent<Renderer>().enabled = false;
            foreach (var y in new[] { 0.03f, h * 0.5f, h - 0.03f })
                Box("Gate Frame", hinge, new Vector3(0.45f, y, 0), new Vector3(0.9f, 0.05f, 0.05f), "Metal", collider: false);
            foreach (var x in new[] { 0.025f, 0.875f })
                Box("Gate Frame", hinge, new Vector3(x, h * 0.5f, 0), new Vector3(0.05f, h, 0.05f), "Metal", collider: false);
            for (float d = 0.12f; d < 0.86f; d += 0.1f)
                Box("Wire", hinge, new Vector3(d, h * 0.5f, 0), new Vector3(0.012f, h, 0.012f), "Galvanized", collider: false);
            for (float y = 0.3f; y < h; y += 0.3f)
                Box("Wire", hinge, new Vector3(0.45f, y, 0), new Vector3(0.86f, 0.012f, 0.012f), "Galvanized", collider: false);
            Box("Padlock", hinge, new Vector3(0.84f, 1.05f, 0.03f), new Vector3(0.06f, 0.08f, 0.03f), "Brass", collider: false);
            Box("Chain", hinge, new Vector3(0.88f, 1.05f, 0), new Vector3(0.12f, 0.025f, 0.025f), "Metal", collider: false);
            var door = gate.AddComponent<World.Door>();
            SetField(door, "hinge", hinge);
            SetField(door, "locked", true);
            SetField(door, "lockedMessage", "The armory. Chained and padlocked; the sheriff keeps the key on him. Five rifles, two shotguns, a box of flares. None of it any use for what's wrong tonight.");
            Part("Armory Sign", office, new Vector3(x0 + 0.75f, FloorY + h + 0.22f, z0 - 0.02f), new Vector3(0.9f, 0.22f, 0.03f), "SignWhite");
            Part("Sign Stripe", office, new Vector3(x0 + 0.75f, FloorY + h + 0.22f, z0 - 0.04f), new Vector3(0.9f, 0.04f, 0.005f), "PlainBarnRed");
            var rack = Piece("Gun Rack", office, new Vector3(6.6f, 0, 5.2f), 0f, new Vector3(2.4f, 1.9f, 0.4f), FloorY);
            Part("Rack Back", rack, new Vector3(0, FloorY + 0.95f, 0.15f), new Vector3(2.4f, 1.9f, 0.04f), "Wood");
            Part("Rack Shelf", rack, new Vector3(0, FloorY + 0.25f, 0), new Vector3(2.4f, 0.04f, 0.36f), "Wood");
            Part("Rack Bar", rack, new Vector3(0, FloorY + 1.3f, -0.05f), new Vector3(2.4f, 0.05f, 0.05f), "Wood");
            for (int i = 0; i < 7; i++)
            {
                float x = -1.05f + i * 0.35f;
                bool shotgun = i >= 5;
                Part("Stock", rack, new Vector3(x, FloorY + 0.55f, 0), new Vector3(0.06f, 0.6f, 0.08f), "Wood");
                Part("Barrel", rack, new Vector3(x, FloorY + (shotgun ? 1.15f : 1.25f), 0), new Vector3(0.03f, shotgun ? 0.6f : 0.8f, 0.03f), "Metal");
                Part("Receiver", rack, new Vector3(x, FloorY + 0.88f, 0), new Vector3(0.05f, 0.14f, 0.07f), "Metal");
                Part("Rack Clip", rack, new Vector3(x, FloorY + 1.3f, -0.05f), new Vector3(0.08f, 0.03f, 0.08f), "Brass");
            }
            foreach (var (x, z, y, mat) in new[] { (5.7f, 3.4f, 0f, "Mustard"), (5.7f, 3.9f, 0f, "Mustard"), (6.3f, 3.5f, 0f, "Sage"), (5.7f, 3.65f, 0.3f, "Mustard") })
            {
                Part("Ammo Crate", office, new Vector3(x, FloorY + 0.14f + y, z), new Vector3(0.42f, 0.28f, 0.3f), mat);
                Part("Crate Label", office, new Vector3(x, FloorY + 0.16f + y, z - 0.152f), new Vector3(0.2f, 0.08f, 0.004f), "Writing");
            }
            Part("Vest", office, new Vector3(7.8f, FloorY + 1.3f, 4.2f), new Vector3(0.06f, 0.6f, 0.5f), "Suit");
            Part("Vest Hook", office, new Vector3(7.9f, FloorY + 1.65f, 4.2f), new Vector3(0.08f, 0.04f, 0.04f), "Brass");
            Part("Flare Box", office, new Vector3(7.5f, FloorY + 0.12f, 4.9f), new Vector3(0.5f, 0.24f, 0.26f), "BarnRed");
            CagedBulb(office, new Vector3(6.6f, 2.9f, 4.1f), 4.5f, 0.9f);
        }

        /// <summary>The west passage and the north corridor: cooler, map, dispatch, cabinets, typing table, plant, extinguisher.</summary>
        private static void BuildOfficeClutter(Transform office, Random rng)
        {
            var cooler = Group("Water Cooler", office);
            cooler.localPosition = new Vector3(-7.5f, 0, 1.3f);
            Part("Cabinet", cooler, new Vector3(0, FloorY + 0.5f, 0), new Vector3(0.36f, 1.0f, 0.36f), "PlainCream");
            Round("Bottle", cooler, new Vector3(0, FloorY + 1.26f, 0), 0.3f, 0.52f, "Glass");
            Part("Tap", cooler, new Vector3(0.2f, FloorY + 0.85f, 0), new Vector3(0.06f, 0.06f, 0.04f), "PlainBlue");
            Part("Cup Sleeve", cooler, new Vector3(0.05f, FloorY + 0.95f, -0.22f), new Vector3(0.07f, 0.28f, 0.07f), "SignWhite");
            Inspect(cooler.GetChild(0).gameObject, "Look at the cooler", "The bottle's a third full. Somebody has written DO NOT DRINK on a strip of tape, and somebody else has written WHY NOT under it.", new Vector3(0.5f, 1.2f, 0.5f));
            var map = Group("County Map", office);
            map.localPosition = new Vector3(-7.96f, 1.9f, -1.4f);
            map.localRotation = Quaternion.Euler(0, 90f, 0);
            Part("Map Sheet", map, Vector3.zero, new Vector3(1.7f, 1.25f, 0.01f), "MapPaper");
            Part("Map Frame", map, new Vector3(0, 0.64f, 0), new Vector3(1.76f, 0.04f, 0.03f), "Wood");
            Part("Map Frame", map, new Vector3(0, -0.64f, 0), new Vector3(1.76f, 0.04f, 0.03f), "Wood");
            for (int i = 0; i < 9; i++)
                Part("Map Pin", map, new Vector3(Lerp(rng, -0.75f, 0.75f), Lerp(rng, -0.5f, 0.5f), -0.01f), new Vector3(0.02f, 0.02f, 0.015f), i == 4 ? "PlainBarnRed" : "Brass");
            Part("Map Road", map, new Vector3(0.1f, 0.05f, -0.006f), new Vector3(0.02f, 1.1f, 0.002f), "Rust").transform.localRotation = Quaternion.Euler(0, 0, 12f);
            Inspect(map.GetChild(0).gameObject, "Look at the map", "Barlow County. Pins in every town but one. Somebody has drawn a circle round Semblance in pencil, two hours north, and written the mileage beside it: 94.", new Vector3(1.8f, 1.3f, 0.5f), clue: "tut_map");
            // Dispatch against the west wall, facing into the room.
            var dispatch = Table("Dispatch", office, new Vector3(-7.6f, 0, -3.1f), -90f, 1.1f, 0.6f, "Wood", 0.8f);
            Part("Transceiver", dispatch, new Vector3(0, FloorY + 0.97f, 0.05f), new Vector3(0.6f, 0.3f, 0.36f), "Metal");
            Part("Dial Panel", dispatch, new Vector3(0, FloorY + 0.97f, -0.135f), new Vector3(0.5f, 0.2f, 0.005f), "Galvanized");
            foreach (var x in new[] { -0.18f, -0.06f, 0.06f, 0.18f })
                Round("Knob", dispatch, new Vector3(x, FloorY + 0.92f, -0.15f), 0.04f, 0.02f, "HideBlack").transform.localRotation = Quaternion.Euler(90f, 0, 0);
            Part("Meter", dispatch, new Vector3(0.1f, FloorY + 1.04f, -0.14f), new Vector3(0.14f, 0.06f, 0.004f), "Bulb");
            Part("Mic Stand", dispatch, new Vector3(-0.42f, FloorY + 0.9f, -0.1f), new Vector3(0.02f, 0.2f, 0.02f), "Metal");
            Part("Microphone", dispatch, new Vector3(-0.42f, FloorY + 1.02f, -0.1f), new Vector3(0.07f, 0.08f, 0.05f), "HideBlack");
            Part("Speaker", office, new Vector3(-7.95f, 2.3f, -3.1f), new Vector3(0.1f, 0.3f, 0.4f), "Wood");
            Part("Speaker Cloth", office, new Vector3(-7.89f, 2.3f, -3.1f), new Vector3(0.004f, 0.24f, 0.32f), "Fabric");
            SoundEmitter(dispatch, new Vector3(0, 1f, 0), "static_loop", 0.05f, 6f);
            Chair(office, new Vector3(-6.75f, 0, -3.1f), -90f, "Leather");
            Part("Extinguisher Bracket", office, new Vector3(-7.95f, 1.1f, 0.2f), new Vector3(0.06f, 0.2f, 0.16f), "Metal");
            Round("Extinguisher", office, new Vector3(-7.84f, 0.95f, 0.2f), 0.14f, 0.5f, "BarnRed");
            Part("Extinguisher Hose", office, new Vector3(-7.78f, 1.1f, 0.12f), new Vector3(0.03f, 0.2f, 0.03f), "HideBlack");
            // The coat rack by the lobby door, with the one coat nobody came back for.
            var rack = Group("Coat Rack", office);
            rack.localPosition = new Vector3(-7.4f, 0, -4.9f);
            Round("Pole", rack, new Vector3(0, FloorY + 0.9f, 0), 0.05f, 1.8f, "Wood");
            Round("Foot", rack, new Vector3(0, FloorY + 0.02f, 0), 0.4f, 0.04f, "Wood");
            foreach (var a in new[] { 0f, 90f, 180f, 270f })
                Part("Hook", rack, Quaternion.Euler(0, a, 0) * new Vector3(0.1f, FloorY + 1.75f, 0), new Vector3(0.16f, 0.02f, 0.02f), "Brass").transform.localRotation = Quaternion.Euler(0, a, 0);
            Part("Coat", rack, new Vector3(0.14f, FloorY + 1.25f, 0.02f), new Vector3(0.28f, 0.95f, 0.16f), "Suit");
            Part("Umbrella", rack, new Vector3(-0.15f, FloorY + 0.45f, 0.1f), new Vector3(0.05f, 0.9f, 0.05f), "HideBlack").transform.localRotation = Quaternion.Euler(0, 0, 12f);
            // North corridor: the cabinets, a plant that's seen better years, the typing table at the dead end.
            for (int i = 0; i < 4; i++)
            {
                var cab = Piece("Filing Cabinet", office, new Vector3(-6.9f + i * 0.56f, 0, 5.15f), 0f, new Vector3(0.5f, 1.32f, 0.62f), FloorY);
                Part("Body", cab, new Vector3(0, FloorY + 0.66f, 0), new Vector3(0.5f, 1.32f, 0.62f), "Galvanized");
                for (int d = 0; d < 3; d++)
                {
                    Part("Drawer Line", cab, new Vector3(0, FloorY + 0.22f + d * 0.42f, -0.311f), new Vector3(0.46f, 0.01f, 0.004f), "Metal");
                    Part("Drawer Pull", cab, new Vector3(0, FloorY + 0.4f + d * 0.42f, -0.325f), new Vector3(0.1f, 0.025f, 0.02f), "Metal");
                    Part("Drawer Label", cab, new Vector3(0.14f, FloorY + 0.4f + d * 0.42f, -0.316f), new Vector3(0.08f, 0.04f, 0.004f), "Writing");
                }
            }
            Part("Cabinet Top Clutter", office, new Vector3(-6.3f, FloorY + 1.4f, 5.15f), new Vector3(0.5f, 0.14f, 0.3f), "Paper");
            var plant = Group("Plant", office);
            plant.localPosition = new Vector3(-7.5f, 0, 5.0f);
            Round("Pot", plant, new Vector3(0, FloorY + 0.18f, 0), 0.36f, 0.36f, "Rust");
            Round("Trunk", plant, new Vector3(0, FloorY + 0.7f, 0), 0.05f, 0.7f, "Bark");
            foreach (var (dx, dz, yaw) in new[] { (0.15f, 0.05f, 20f), (-0.12f, 0.1f, 140f), (0.02f, -0.16f, 260f), (0.1f, -0.1f, 330f) })
                Part("Leaf", plant, new Vector3(dx, FloorY + 1.05f, dz), new Vector3(0.1f, 0.02f, 0.4f), "Sage").transform.localRotation = Quaternion.Euler(-30f, yaw, 0);
            var typing = Table("Typing Table", office, new Vector3(3.9f, 0, 4.9f), 0f, 0.9f, 0.5f, "Wood", 0.7f);
            Part("Typewriter", typing, new Vector3(0, FloorY + 0.78f, 0), new Vector3(0.4f, 0.16f, 0.34f), "Metal");
            Part("Dust Cover", typing, new Vector3(0, FloorY + 0.88f, 0), new Vector3(0.44f, 0.04f, 0.38f), "Fabric");
            Picture(office, new Vector3(-5.0f, 2.2f, 5.47f), 0f, new Vector2(0.6f, 0.45f));
        }

        /// <summary>
        /// Tonight's block on the east end of the north corridor: a year of archive cartons stacked where somebody
        /// meant to carry them down, and two chairs on top so nobody tries. It closes the loop into one route.
        /// </summary>
        private static void BuildBarricade(Transform office, Random rng)
        {
            float z = 2.45f;
            var wall = new GameObject("Barricade Collider");
            wall.transform.SetParent(office, false);
            wall.transform.localPosition = new Vector3(4.1f, FloorY + 0.9f, z);
            wall.AddComponent<BoxCollider>().size = new Vector3(2.2f, 1.8f, 0.7f);
            int n = 0;
            foreach (var x in new[] { 3.25f, 3.7f, 4.15f, 4.6f, 5.0f })
            {
                int height = x == 4.15f ? 4 : 3;
                for (int i = 0; i < height; i++)
                {
                    var carton = Part("Archive Carton", office, new Vector3(x + Lerp(rng, -0.03f, 0.03f), FloorY + 0.135f + i * 0.275f, z + Lerp(rng, -0.04f, 0.04f)), new Vector3(0.4f, 0.27f, 0.32f), n++ % 3 == 1 ? "Cream" : "Paper");
                    carton.transform.localRotation = Quaternion.Euler(0, Lerp(rng, -6f, 6f), 0);
                    Part("Carton Label", office, new Vector3(x, FloorY + 0.17f + i * 0.275f, z - 0.165f), new Vector3(0.16f, 0.06f, 0.004f), "Writing");
                }
            }
            // One chair up on the cartons, another tipped against the stack with its legs in the aisle.
            Chair(office, new Vector3(3.7f, 0.82f, z), 90f, "Wood");
            var tipped = Chair(office, new Vector3(5.0f, 0.2f, z - 0.55f), 0f, "Wood");
            tipped.localRotation = Quaternion.Euler(-62f, 0, 0);
            Part("Wet Floor Sign", office, new Vector3(3.6f, FloorY + 0.3f, 1.9f), new Vector3(0.3f, 0.6f, 0.04f), "Mustard").transform.localRotation = Quaternion.Euler(-12f, 0, 0);
            Part("Wet Floor Text", office, new Vector3(3.6f, FloorY + 0.3f, 1.87f), new Vector3(0.22f, 0.3f, 0.004f), "Writing").transform.localRotation = Quaternion.Euler(-12f, 0, 0);
            Inspect(wall, "Look at the boxes", "Archive cartons, a year of them, stacked across the corridor where somebody meant to carry them down to the basement. The chairs on top say nobody meant it very hard. You'd have to go round.", null);
        }

        // ------------------------------------------------------------ the stairwell



        private static void BuildStairwell(Transform stairwell)
        {
            const float top = 3.3f, bottom = -3.6f;
            string wall = "Concrete";
            Box("Landing", stairwell, new Vector3(-9.2f, 0.05f, 3.0f), new Vector3(2.4f, 0.1f, 3.0f), "Concrete");
            for (int i = 0; i < 20; i++)
            {
                float stepTop = 0.1f - 0.18f * (i + 1);
                float z = 1.5f - 0.3f * i - 0.15f;
                Box("Step", stairwell, new Vector3(-9.2f, (stepTop + bottom) * 0.5f, z), new Vector3(2.4f, stepTop - bottom, 0.3f), "Concrete");
                Box("Nosing", stairwell, new Vector3(-9.2f, stepTop + 0.005f, z + 0.14f), new Vector3(2.4f, 0.012f, 0.03f), "Metal", collider: false);
            }
            Box("Lower Landing", stairwell, new Vector3(-9.2f, -3.55f, -5.35f), new Vector3(2.4f, 0.1f, 1.7f), "Concrete");
            Box("Ceiling", stairwell, new Vector3(-9.2f, 3.26f, -0.85f), new Vector3(2.8f, 0.12f, 11.7f), "ConcreteDark");
            WallX(stairwell, "Wall West", -10.5f, -6.3f, 4.6f, bottom, top, wall);
            WallZ(stairwell, "Wall North", 4.6f, -10.6f, -8.0f, bottom, top, wall);
            WallZ(stairwell, "Wall South", -6.3f, -10.6f, -8.0f, bottom, top, wall);
            // East: below the office floor, with the archive door; above it, the office's own west wall takes over.
            WallX(stairwell, "Wall East", -8.1f, -6.3f, -5.9f, bottom, 0f, wall);
            WallX(stairwell, "Wall East", -8.1f, -5.0f, 4.6f, bottom, 0f, wall);
            WallX(stairwell, "Lintel", -8.1f, -5.9f, -5.0f, -1.4f, 0f, wall);
            WallX(stairwell, "Wall East Upper", -8.1f, -6.3f, -5.5f, 0f, top, wall);
            TutorialDoor(stairwell, "Archive Door", new Vector3(-8.1f, -3.5f, -5.9f), 90f);
            // Handrail down the open side.
            var rail = Box("Handrail", stairwell, new Vector3(-8.35f, -0.8f, -1.5f), new Vector3(0.05f, 0.05f, 7.0f), "Metal", collider: false);
            rail.transform.localRotation = Quaternion.Euler(-31f, 0, 0);
            foreach (var z in new[] { 1.2f, -1.5f, -4.2f })
            {
                float y = 0.1f - 0.18f * ((1.5f - z) / 0.3f) + 0.45f;
                Box("Rail Post", stairwell, new Vector3(-8.35f, y, z), new Vector3(0.04f, 0.9f, 0.04f), "Metal", collider: false);
            }
            CagedBulb(stairwell, new Vector3(-9.2f, 2.7f, 2.6f), 6f, 1.2f);
            CagedBulb(stairwell, new Vector3(-9.2f, -1.3f, -5.2f), 5f, 1.0f, flicker: true);
            SoundEmitter(stairwell, new Vector3(-9.8f, -3f, -5.8f), "drip_loop", 0.25f, 7f);
            ExitSign(stairwell, new Vector3(-8.25f, -1.15f, -5.45f), 90f);
            Zone(stairwell, "Stair Floor", new Vector3(-9.2f, -1.5f, -1f), new Vector3(2.4f, 8f, 11f), Audio.Surface.Asphalt, 2);
            AddMarker(stairwell, "the stairwell", 3.5f, 3, new Vector3(-9.2f, 0, -1f));
        }

        // ------------------------------------------------------------ the archive

        private static void BuildArchive(Transform archive, Random rng)
        {
            const float floor = -3.5f, top = -0.9f, bottom = -3.6f;
            string wall = "Concrete";
            Box("Floor", archive, new Vector3(1f, floor - 0.05f, -6f), new Vector3(18.4f, 0.1f, 10.4f), "Concrete");
            Box("Ceiling", archive, new Vector3(1f, top + 0.06f, -6f), new Vector3(18.4f, 0.12f, 10.4f), "ConcreteDark");
            WallX(archive, "Wall West", -8.1f, -11.1f, -6.3f, bottom, top, wall);
            WallX(archive, "Wall East", 10.1f, -11.1f, -5.45f, bottom, top, wall);
            WallX(archive, "Wall East", 10.1f, -4.55f, -1.0f, bottom, top, wall);
            WallX(archive, "Lintel", 10.1f, -5.45f, -4.55f, -1.4f, top, wall);
            WallZ(archive, "Wall South", -11.1f, -8.2f, 10.2f, bottom, top, wall);
            WallZ(archive, "Wall North", -0.9f, -8.2f, 10.2f, bottom, top, wall);
            TutorialDoor(archive, "Boiler Door", new Vector3(10.1f, floor, -5.45f), -90f);
            // Pipes under the ceiling, and a column or two the shelves are built around.
            foreach (var z in new[] { -4.6f, -6.4f })
            {
                var pipe = Round("Pipe", archive, new Vector3(1f, top - 0.18f, z), 0.14f, 18f, "Rust");
                pipe.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            }
            foreach (var x in new[] { -1.5f, 1.5f })
                Box("Column", archive, new Vector3(x, (floor + top) * 0.5f, -5.0f + (x < 0 ? -1.4f : 1.4f)), new Vector3(0.4f, top - floor, 0.4f), "ConcreteDark");

            foreach (var z in new[] { -9.8f, -8.0f, -6.3f, -3.7f, -2.0f })
                foreach (var x in new[] { -3.9f, 3.9f })
                    ArchiveRow(archive, new Vector3(x, floor, z), 5.8f, rng);
            // A few cartons that didn't make it back onto a shelf, and one that just fell.
            Part("Carton", archive, new Vector3(7.6f, floor + 0.135f, -5.6f), new Vector3(0.38f, 0.27f, 0.3f), "Paper").transform.localRotation = Quaternion.Euler(0, 24f, 0);
            Part("Carton", archive, new Vector3(7.9f, floor + 0.135f, -4.7f), new Vector3(0.38f, 0.27f, 0.3f), "Cream").transform.localRotation = Quaternion.Euler(0, -60f, 0);
            foreach (var p in new[] { new Vector3(7.3f, floor + 0.004f, -5.1f), new Vector3(7.75f, floor + 0.004f, -5.3f), new Vector3(8.2f, floor + 0.004f, -4.9f) })
                Part("Loose Page", archive, p, new Vector3(0.21f, 0.004f, 0.28f), "Paper").transform.localRotation = Quaternion.Euler(0, Lerp(rng, 0f, 180f), 0);
            Part("Carton", archive, new Vector3(-6.2f, floor + 0.135f, -9.0f), new Vector3(0.38f, 0.27f, 0.3f), "Paper");

            Locker(archive, new Vector3(-0.7f, floor, -6.3f), 90f);
            var lockerLamp = new GameObject("Emergency Light");
            lockerLamp.transform.SetParent(archive, false);
            lockerLamp.transform.localPosition = new Vector3(-0.3f, top - 0.35f, -6.1f);
            var ll = lockerLamp.AddComponent<Light>();
            ll.type = LightType.Point;
            ll.color = new Color(1f, 0.25f, 0.18f);
            ll.range = 4f;
            ll.intensity = 0.55f;
            ll.shadows = LightShadows.None;
            Part("Emergency Lamp", archive, new Vector3(-0.3f, top - 0.25f, -6.1f), new Vector3(0.3f, 0.12f, 0.1f), "ExitRed");
            Part("Emergency Lamp Box", archive, new Vector3(-0.3f, top - 0.2f, -6.1f), new Vector3(0.36f, 0.06f, 0.16f), "Metal");
            SoundEmitter(archive, new Vector3(6f, top - 0.3f, -9.5f), "drip_loop", 0.3f, 10f);
            SoundEmitter(archive, new Vector3(-6f, top - 0.3f, -2.5f), "drip_loop", 0.2f, 8f);
            Part("Mop Bucket", archive, new Vector3(-7.4f, floor + 0.15f, -9.6f), new Vector3(0.36f, 0.3f, 0.3f), "Galvanized");
            Round("Mop Handle", archive, new Vector3(-7.4f, floor + 0.8f, -9.75f), 0.03f, 1.4f, "Wood").transform.localRotation = Quaternion.Euler(8f, 0, 0);

            foreach (var x in new[] { -5f, 0f, 5f })
                CagedBulb(archive, new Vector3(x, top - 0.25f, -5.0f), 6.5f, 1.1f, flicker: x == 0f);
            CagedBulb(archive, new Vector3(0f, top - 0.25f, -9.0f), 5.5f, 0.9f);
            CagedBulb(archive, new Vector3(0f, top - 0.25f, -1.9f), 5.5f, 0.9f);
            // The emergency lamp over the boiler door comes on when the building goes dark.
            var em = new GameObject("Emergency Light");
            em.transform.SetParent(archive, false);
            em.transform.localPosition = new Vector3(9.5f, top - 0.3f, -5.0f);
            var emLight = em.AddComponent<Light>();
            emLight.type = LightType.Point;
            emLight.color = new Color(1f, 0.22f, 0.15f);
            emLight.range = 7f;
            emLight.intensity = 0.6f;
            emLight.shadows = LightShadows.None;
            Part("Emergency Lamp", archive, new Vector3(9.95f, top - 0.3f, -5.0f), new Vector3(0.1f, 0.14f, 0.3f), "ExitRed");
            Part("Emergency Lamp Box", archive, new Vector3(9.98f, top - 0.3f, -5.0f), new Vector3(0.06f, 0.2f, 0.36f), "Metal");
            ExitSign(archive, new Vector3(-7.95f, -1.2f, -5.45f), 90f);
            SoundEmitter(archive, new Vector3(1f, top - 0.2f, -6f), "room_tone_loop", 0.06f, 14f);

            AddMarker(archive, "the archive", 10.5f, 2, new Vector3(1f, floor, -6f));
            Zone(archive, "Archive Floor", new Vector3(1f, floor, -6f), new Vector3(18f, 2f, 10f), Audio.Surface.Asphalt, 1);

            TutorialAnchor("ArchiveEntry", new Vector3(-7.0f, floor, -5.0f), Vector3.right);
            TutorialAnchor("DogEast", new Vector3(8.6f, floor, -5.0f), Vector3.left);
            TutorialAnchor("LockerFront", new Vector3(-0.3f, floor, -5.4f), Vector3.left);
            TutorialAnchor("DogWest", new Vector3(-7.3f, floor, -5.0f), Vector3.right);
            TutorialAnchor("ChaseStart", new Vector3(3.0f, floor, -5.0f), Vector3.right);
            TutorialAnchor("BoilerDoor", new Vector3(9.3f, floor, -5.0f), Vector3.right);
        }

        // ------------------------------------------------------------ the boiler room

        private static (TutorialBreaker, World.Door, Renderer) BuildBoilerRoom(Transform boiler, Random rng)
        {
            const float floor = -3.5f, top = -0.9f, bottom = -3.6f;
            string wall = "Concrete";
            Box("Floor", boiler, new Vector3(13f, floor - 0.05f, -5f), new Vector3(6.4f, 0.1f, 6.4f), "Concrete");
            Box("Ceiling", boiler, new Vector3(13f, top + 0.06f, -5f), new Vector3(6.4f, 0.12f, 6.4f), "ConcreteDark");
            WallX(boiler, "Wall East", 16.1f, -8.1f, -1.9f, bottom, top, wall);
            WallZ(boiler, "Wall South", -8.1f, 10.0f, 16.2f, bottom, top, wall);
            WallZ(boiler, "Wall North", -2.1f, 10.0f, 11.55f, bottom, top, wall);
            WallZ(boiler, "Wall North", -2.1f, 12.45f, 16.2f, bottom, top, wall);
            WallZ(boiler, "Lintel", -2.1f, 11.55f, 12.45f, -1.4f, top, wall);
            var closetDoor = TutorialDoor(boiler, "Closet Door", new Vector3(11.55f, floor, -2.1f), 0f);
            // The closet behind it: Teo's, and Biscuit's.
            Box("Closet Floor", boiler, new Vector3(12f, floor - 0.05f, -0.85f), new Vector3(2.4f, 0.1f, 2.5f), "Concrete");
            Box("Closet Ceiling", boiler, new Vector3(12f, top + 0.06f, -0.85f), new Vector3(2.4f, 0.12f, 2.5f), "ConcreteDark");
            WallX(boiler, "Closet Wall", 10.9f, -2.1f, 0.4f, bottom, top, wall);
            WallX(boiler, "Closet Wall", 13.1f, -2.1f, 0.4f, bottom, top, wall);
            WallZ(boiler, "Closet Wall", 0.4f, 10.8f, 13.2f, bottom, top, wall);
            var shelf = Piece("Closet Shelf", boiler, new Vector3(12.6f, floor, 0.05f), 0f, new Vector3(0.9f, 1.8f, 0.3f), FloorY);
            foreach (var y in new[] { 0.5f, 1.0f, 1.5f })
                Part("Board", shelf, new Vector3(0, FloorY + y, 0), new Vector3(0.9f, 0.03f, 0.3f), "Wood");
            foreach (var (x, y, mat) in new[] { (-0.3f, 0.53f, "Mustard"), (0f, 0.53f, "Teal"), (0.25f, 1.03f, "SignWhite"), (-0.2f, 1.03f, "BarnRed") })
                Part("Tin", shelf, new Vector3(x, FloorY + y + 0.1f, 0), new Vector3(0.12f, 0.2f, 0.12f), mat);
            Part("Dog Blanket", boiler, new Vector3(11.4f, floor + 0.04f, -0.6f), new Vector3(0.8f, 0.08f, 0.7f), "Rug");
            Round("Water Bowl", boiler, new Vector3(12.6f, floor + 0.03f, -1.6f), 0.22f, 0.06f, "Galvanized");
            CagedBulb(boiler, new Vector3(12f, top - 0.25f, -0.9f), 4f, 0.8f);

            // The furnace: a tank with a hatch, a slit of fire in it, and a flue up into the ceiling.
            var furnace = Piece("Furnace", boiler, new Vector3(14.6f, floor, -3.1f), 0f, new Vector3(2.0f, 1.9f, 1.5f), FloorY);
            Part("Tank", furnace, new Vector3(0, FloorY + 0.95f, 0), new Vector3(2.0f, 1.9f, 1.5f), "Galvanized");
            Part("Hatch", furnace, new Vector3(0, FloorY + 0.75f, -0.76f), new Vector3(0.7f, 0.6f, 0.04f), "Metal");
            Part("Hatch Hinge", furnace, new Vector3(-0.33f, FloorY + 0.75f, -0.78f), new Vector3(0.03f, 0.5f, 0.02f), "Rust");
            Part("Latch", furnace, new Vector3(0.3f, FloorY + 0.75f, -0.79f), new Vector3(0.05f, 0.14f, 0.03f), "Rust");
            var glow = Part("Fire Slit", furnace, new Vector3(0, FloorY + 0.52f, -0.78f), new Vector3(0.5f, 0.06f, 0.02f), "Bulb");
            Round("Flue", furnace, new Vector3(0.4f, FloorY + 2.3f, 0), 0.3f, 0.8f, "Rust");
            Part("Pipe", furnace, new Vector3(-0.6f, FloorY + 1.6f, 0.4f), new Vector3(0.1f, 0.1f, 2.4f), "Rust");
            Part("Gauge", furnace, new Vector3(-0.5f, FloorY + 1.3f, -0.77f), new Vector3(0.14f, 0.14f, 0.03f), "SignWhite");
            Part("Gauge Rim", furnace, new Vector3(-0.5f, FloorY + 1.3f, -0.78f), new Vector3(0.16f, 0.16f, 0.01f), "Brass");
            SoundEmitter(furnace, new Vector3(0, 1f, 0), "drone_deep_loop", 0.08f, 7f);

            // The breaker panel on the east wall. The big switch is the one that matters.
            var panel = Box("Breaker Panel", boiler, new Vector3(15.92f, -2.2f, -6.0f), new Vector3(0.14f, 0.75f, 0.5f), "Galvanized");
            Box("Panel Door Edge", boiler, new Vector3(15.84f, -2.2f, -6.0f), new Vector3(0.02f, 0.7f, 0.46f), "Metal", collider: false);
            for (int i = 0; i < 6; i++)
                Box("Switch", boiler, new Vector3(15.82f, -2.0f - i * 0.09f, -6.1f), new Vector3(0.02f, 0.05f, 0.03f), "HideBlack", collider: false);
            Box("Main Switch", boiler, new Vector3(15.8f, -2.42f, -5.88f), new Vector3(0.06f, 0.2f, 0.07f), "BarnRed", collider: false);
            Box("Panel Label", boiler, new Vector3(15.84f, -1.92f, -5.86f), new Vector3(0.004f, 0.06f, 0.14f), "Writing", collider: false);
            var breaker = panel.AddComponent<TutorialBreaker>();

            // A ladder against the south wall, a bucket, the work lights.
            var ladder = Group("Ladder", boiler);
            ladder.localPosition = new Vector3(10.6f, floor, -7.4f);
            ladder.localRotation = Quaternion.Euler(-14f, 0, 0);
            foreach (var x in new[] { -0.2f, 0.2f })
                Part("Rail", ladder, new Vector3(x, 1.2f, 0), new Vector3(0.04f, 2.4f, 0.05f), "Wood");
            for (int r = 0; r < 7; r++)
                Part("Rung", ladder, new Vector3(0, 0.25f + r * 0.32f, 0), new Vector3(0.44f, 0.03f, 0.03f), "Wood");
            Part("Bucket", boiler, new Vector3(11.2f, floor + 0.15f, -7.4f), new Vector3(0.3f, 0.3f, 0.3f), "Galvanized");
            Part("Rag", boiler, new Vector3(11.5f, floor + 0.01f, -7.1f), new Vector3(0.3f, 0.02f, 0.25f), "Fabric");
            CagedBulb(boiler, new Vector3(13f, top - 0.25f, -5f), 6f, 1.3f);
            CagedBulb(boiler, new Vector3(15f, top - 0.25f, -6.5f), 4f, 0.8f);

            AddMarker(boiler, "the boiler room", 4.5f, 3, new Vector3(13f, floor, -5f));
            Zone(boiler, "Boiler Floor", new Vector3(13f, floor, -4.5f), new Vector3(6.4f, 2f, 9f), Audio.Surface.Asphalt, 1);
            TutorialAnchor("DogSit", new Vector3(12.6f, floor, -5.3f), Vector3.left);
            TutorialAnchor("TeoOut", new Vector3(12f, floor, -3.6f), Vector3.back);
            return (breaker, closetDoor, glow.GetComponent<Renderer>());
        }

        // ------------------------------------------------------------ outside: the rain, the lamp, the way in

        private static void BuildExterior(Transform exterior, Random rng)
        {
            Box("Lot", exterior, new Vector3(25f, -0.05f, 0f), new Vector3(34f, 0.1f, 34f), "Asphalt");
            Box("Kerb", exterior, new Vector3(9.0f, 0.06f, 0f), new Vector3(1.6f, 0.12f, 34f), "Sidewalk");
            // The building's face: brick over the office wall, a storey above it, dark windows up there.
            Box("Facade Upper", exterior, new Vector3(8.26f, 4.5f, 0f), new Vector3(0.12f, 2.4f, 11.8f), "Brick", collider: false);
            Box("Cornice", exterior, new Vector3(8.4f, 5.75f, 0f), new Vector3(0.4f, 0.14f, 12f), "Stone", collider: false);
            Box("Band", exterior, new Vector3(8.33f, 3.35f, 0f), new Vector3(0.14f, 0.12f, 12f), "Stone", collider: false);
            foreach (var z in new[] { -3.2f, 0f, 3.2f })
            {
                Box("Upper Window", exterior, new Vector3(8.3f, 4.5f, z), new Vector3(0.04f, 1.3f, 1.1f), "WindowDark", collider: false);
                Box("Upper Sill", exterior, new Vector3(8.36f, 3.82f, z), new Vector3(0.2f, 0.07f, 1.3f), "Stone", collider: false);
            }
            // The brick face, built as strips so the three windows are open through it.
            Box("Facade Low", exterior, new Vector3(8.26f, 0.475f, 0f), new Vector3(0.08f, 0.95f, 11.8f), "Brick", collider: false);
            Box("Facade High", exterior, new Vector3(8.26f, 2.875f, 0f), new Vector3(0.08f, 0.85f, 11.8f), "Brick", collider: false);
            foreach (var p in new[] { (-5.9f, -3.85f), (-2.55f, -0.65f), (0.65f, 2.55f), (3.85f, 5.9f) })
                Box("Facade Pier", exterior, new Vector3(8.26f, 1.7f, (p.Item1 + p.Item2) * 0.5f), new Vector3(0.08f, 1.5f, p.Item2 - p.Item1), "Brick", collider: false);
            Box("Side Wall", exterior, new Vector3(0f, 2.9f, 5.72f), new Vector3(16.6f, 5.8f, 0.1f), "Brick", collider: false);
            Box("Side Wall", exterior, new Vector3(0f, 2.9f, -5.72f), new Vector3(16.6f, 5.8f, 0.1f), "Brick", collider: false);
            Box("Roof", exterior, new Vector3(0f, 5.85f, 0f), new Vector3(16.8f, 0.2f, 12f), "Roof", collider: false);

            // One street lamp, a parked cruiser, and the rest of the block as a shape in the dark.
            var lamp = Group("Street Lamp", exterior);
            lamp.localPosition = new Vector3(15.5f, 0f, -1.5f);
            Round("Pole", lamp, new Vector3(0, 2.6f, 0), 0.16f, 5.2f, "Metal");
            Part("Arm", lamp, new Vector3(-0.6f, 5.1f, 0), new Vector3(1.3f, 0.14f, 0.14f), "Metal");
            Part("Head", lamp, new Vector3(-1.2f, 5.0f, 0), new Vector3(0.6f, 0.2f, 0.34f), "Metal");
            Part("Bulb", lamp, new Vector3(-1.2f, 4.88f, 0), new Vector3(0.5f, 0.04f, 0.26f), "Bulb");
            var lightGO = new GameObject("Lamp Light");
            lightGO.transform.SetParent(lamp, false);
            lightGO.transform.localPosition = new Vector3(-1.2f, 4.7f, 0);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.82f, 0.55f);
            light.range = 22f;
            light.intensity = 7f;
            light.shadows = LightShadows.None;
            var car = Group("Cruiser", exterior);
            car.localPosition = new Vector3(14f, 0f, 6f);
            car.localRotation = Quaternion.Euler(0, 8f, 0);
            Part("Body", car, new Vector3(0, 0.55f, 0), new Vector3(1.9f, 0.6f, 4.6f), "HideBlack");
            Part("Cabin", car, new Vector3(0, 1.05f, -0.2f), new Vector3(1.7f, 0.5f, 2.2f), "HideBlack");
            Part("Windows", car, new Vector3(0, 1.08f, -0.2f), new Vector3(1.72f, 0.4f, 2.1f), "WindowDark");
            Part("Light Bar", car, new Vector3(0, 1.36f, -0.3f), new Vector3(1.2f, 0.12f, 0.3f), "Metal");
            foreach (var x in new[] { -0.8f, 0.8f })
                foreach (var z in new[] { -1.5f, 1.5f })
                    Round("Wheel", car, new Vector3(x, 0.32f, z), 0.64f, 0.26f, "HideBlack").transform.localRotation = Quaternion.Euler(0, 0, 90f);
            Box("Block", exterior, new Vector3(40f, 4f, 10f), new Vector3(12f, 8f, 18f), "ConcreteDark", collider: false);
            foreach (var (y, z) in new[] { (2.5f, 4f), (5.5f, 12f), (4f, 16f) })
                Box("Far Window", exterior, new Vector3(33.9f, y, z), new Vector3(0.05f, 1f, 0.8f), "WindowLit", collider: false);
            Box("Block", exterior, new Vector3(20f, 3f, -22f), new Vector3(30f, 6f, 10f), "ConcreteDark", collider: false);

            // Rain: stretched drops falling through the lamplight, outside the glass only.
            var rainGO = new GameObject("Rain");
            rainGO.transform.SetParent(exterior, false);
            rainGO.transform.localPosition = new Vector3(21f, 9f, 0f);
            var ps = rainGO.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.5f;
            main.startSpeed = 0f;
            main.startSize = 0.03f;
            main.startColor = new Color(0.75f, 0.8f, 0.9f, 0.4f);
            main.maxParticles = 5000;
            main.gravityModifier = 2.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            var emission = ps.emission;
            emission.rateOverTime = 1100f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(24f, 0.2f, 22f);
            var renderer = rainGO.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0f;
            renderer.lengthScale = 7f;
            renderer.cameraVelocityScale = 0f;
            renderer.material = RainMaterial();

            TutorialAnchor("CamStart", new Vector3(25f, 2.7f, 1.6f), Vector3.left);
            TutorialAnchor("CamEnd", new Vector3(9.7f, 1.95f, 0.45f), Vector3.left);
            TutorialAnchor("WindowLook", new Vector3(7.2f, 1.7f, 0.5f), Vector3.left);
        }

        private static Material RainMaterial()
        {
            string path = MaterialDir + "/Rain.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            mat.SetColor("_BaseColor", new Color(0.8f, 0.85f, 0.95f, 0.5f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------ Biscuit

        private static TutorialDog BuildDog(Transform parent, Vector3 position)
        {
            var dog = new GameObject("Biscuit");
            dog.transform.SetParent(parent, false);
            dog.transform.position = position;
            var body = Group("Body", dog.transform);
            body.localPosition = new Vector3(0, 0.42f, 0);
            Box("Trunk", body, Vector3.zero, new Vector3(0.3f, 0.28f, 0.6f), "HideBlack", collider: false);
            Box("Chest", body, new Vector3(0, -0.04f, 0.2f), new Vector3(0.26f, 0.24f, 0.22f), "HideBlack", collider: false);
            Box("Collar", body, new Vector3(0, 0.02f, 0.3f), new Vector3(0.3f, 0.05f, 0.05f), "PlainBarnRed", collider: false);
            var head = Group("Head", dog.transform);
            head.localPosition = new Vector3(0, 0.58f, 0.4f);
            Box("Skull", head, Vector3.zero, new Vector3(0.2f, 0.2f, 0.22f), "HideBlack", collider: false);
            Box("Muzzle", head, new Vector3(0, -0.05f, 0.17f), new Vector3(0.12f, 0.1f, 0.16f), "Ash", collider: false);
            Box("Nose", head, new Vector3(0, -0.02f, 0.255f), new Vector3(0.05f, 0.04f, 0.02f), "HideBlack", collider: false);
            foreach (var x in new[] { -0.09f, 0.09f })
                Box("Ear", head, new Vector3(x, 0.08f, -0.03f), new Vector3(0.06f, 0.12f, 0.03f), "HideBlack", collider: false).transform.localRotation = Quaternion.Euler(10f, 0, x < 0 ? 20f : -20f);
            foreach (var x in new[] { -0.065f, 0.065f })
                Box("Eye", head, new Vector3(x, 0.03f, 0.112f), new Vector3(0.035f, 0.03f, 0.012f), "EyeShine", collider: false);
            foreach (var (name, x, z) in new[] { ("Leg FL", 0.1f, 0.22f), ("Leg FR", -0.1f, 0.22f), ("Leg BL", 0.1f, -0.22f), ("Leg BR", -0.1f, -0.22f) })
            {
                var leg = Group(name, dog.transform);
                leg.localPosition = new Vector3(x, 0.3f, z);
                Box("Upper", leg, new Vector3(0, -0.08f, 0), new Vector3(0.08f, 0.2f, 0.09f), "HideBlack", collider: false);
                Box("Lower", leg, new Vector3(0, -0.22f, 0.01f), new Vector3(0.06f, 0.16f, 0.06f), "HideBlack", collider: false);
                Box("Paw", leg, new Vector3(0, -0.29f, 0.03f), new Vector3(0.07f, 0.04f, 0.09f), "HideBlack", collider: false);
            }
            var tail = Group("Tail", dog.transform);
            tail.localPosition = new Vector3(0, 0.5f, -0.3f);
            tail.localRotation = Quaternion.Euler(25f, 0, 0);
            Box("Tail Bone", tail, new Vector3(0, 0, -0.14f), new Vector3(0.05f, 0.05f, 0.28f), "HideBlack", collider: false);
            Box("Tail Tip", tail, new Vector3(0, 0, -0.29f), new Vector3(0.04f, 0.04f, 0.06f), "Ash", collider: false);
            var agent = dog.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.radius = 0.28f;
            agent.height = 0.65f;
            agent.speed = 2.1f;
            agent.acceleration = 10f;
            agent.angularSpeed = 420f;
            agent.stoppingDistance = 0.2f;
            agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            return dog.AddComponent<TutorialDog>();
        }

        // ------------------------------------------------------------ the two people

        private static ResidentProfile TutorialProfile(string name, System.Action<ResidentProfile> fill)
        {
            string path = $"{TutorialProfileDir}/{name}.asset";
            var profile = ScriptableObject.CreateInstance<ResidentProfile>();
            profile.name = name;
            profile.displayName = name;
            fill(profile);
            var existing = AssetDatabase.LoadAssetAtPath<ResidentProfile>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(profile, path);
                return profile;
            }
            EditorUtility.CopySerialized(profile, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(profile);
            return existing;
        }

        private static void AlmaProfile(ResidentProfile p)
        {
            p.occupation = "Desk sergeant";
            p.age = 54;
            p.possessive = "her";
            p.home = "Barlow";
            p.bio = "Thirty-one years on the front desk of the county sheriff's office. Knows every voice in the county by its second word.";
            p.appearance = new ResidentAppearance
            {
                height = 0.97f, build = 1.05f, skin = new Color(0.72f, 0.56f, 0.46f), hairColor = new Color(0.62f, 0.6f, 0.6f), eyeColor = new Color(0.3f, 0.22f, 0.14f),
                shirt = new Color(0.56f, 0.5f, 0.36f), pants = new Color(0.28f, 0.29f, 0.3f), shoes = new Color(0.1f, 0.08f, 0.07f), hair = HairStyle.Bun, glasses = true,
            };
            p.features = new List<FeatureInstance>
            {
                new FeatureInstance("ring", FeatureSide.Left, "Thirty years in May. He's asleep in front of the television right now, I'd put money on it."),
                new FeatureInstance("cheek_scar", FeatureSide.Right, "A drunk with a bottle, 1961. He's dead and I'm not."),
            };
            p.greetings = new[] { "Klein. You're still here.", "Go home, Klein. ...Oh. Right. The letter." };
            p.aboutSelf = "Thirty-one years on this desk. I know every voice in the county by its second word. The sheriff's, the drunks', and the ones that call and don't say anything.";
            p.aboutTown = "Semblance? Two hours north, past Barlow. Farm town: one church, one diner, one road through it. In thirty-one years this office has never had a call from there. Not a stolen truck. Not a drunk. Nothing at all.";
            p.seenAnythingStrange = "Teo's dog got out again. Half blind, that animal, and she goes and sits in the archive facing the wall, like she's waiting for somebody to come through it.";
            p.foundersDay = "";
            p.gossip = new List<DialogueTopic> { new DialogueTopic("About Teodoro Baptiste", "Teo? Clocked out at ten, same as always. Thursdays he's off anyway, so I don't know why he was in at all.") };
        }

        private static void TeoProfile(ResidentProfile p)
        {
            p.occupation = "Custodian";
            p.age = 63;
            p.possessive = "his";
            p.home = "Barlow";
            p.bio = "Has kept the county building clean, warm and lit since 1958. Lives with an old dog called Biscuit.";
            p.appearance = new ResidentAppearance
            {
                height = 0.98f, build = 1.1f, skin = new Color(0.55f, 0.4f, 0.3f), hairColor = new Color(0.7f, 0.7f, 0.68f), eyeColor = new Color(0.2f, 0.14f, 0.1f),
                shirt = new Color(0.42f, 0.44f, 0.47f), pants = new Color(0.36f, 0.38f, 0.42f), shoes = new Color(0.1f, 0.09f, 0.08f), hair = HairStyle.Combover, mustache = true, overalls = true,
            };
            p.features = new List<FeatureInstance>
            {
                new FeatureInstance("limp", FeatureSide.Left, "Fell off that same ladder in '79. The ladder's fine."),
                new FeatureInstance("gold_tooth", FeatureSide.None, "A bet, a long time ago. I won, and this is what I bought with it."),
            };
            p.greetings = new[] { "Evening, Detective. Mind the floor, it's wet.", "Still here? Me too." };
            p.aboutSelf = "Been down here all night with the furnace. She's older than me and twice as mean. Don't tell Alma I'm in; she'll have me fixing the lobby door again.";
            p.aboutTown = "Semblance. Never been. My cousin delivers feed up that way. Says the dogs there don't bark at strangers, and he doesn't like it.";
            p.seenAnythingStrange = "Biscuit won't go past the third row of shelves. Sits and stares at the wall. Dogs know things. Then again, she's half blind.";
            p.foundersDay = "";
            p.gossip = new List<DialogueTopic> { new DialogueTopic("About Alma Reyes", "Alma feeds my dog when she thinks I'm not looking. Thirty years and she thinks I don't know. She's a good one.") };
        }

        private static void PlaceTutorialResident(Transform root, ResidentProfile profile, Vector3 position, float yaw)
        {
            var go = new GameObject(profile.displayName);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = 0.3f;
            capsule.height = 1.75f;
            capsule.center = new Vector3(0, 0.875f, 0);
            var avatar = go.AddComponent<ResidentAvatar>();
            avatar.SetProfile(profile, litShader);
            go.AddComponent<ResidentBehaviour>();
            go.AddComponent<Resident>();
            var agent = go.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.radius = 0.25f;
            agent.height = 1.75f;
            agent.speed = 1.3f;
            agent.angularSpeed = 300f;
            agent.acceleration = 5f;
            agent.stoppingDistance = 0.15f;
            agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            go.AddComponent<ResidentMover>();
        }
    }
}
