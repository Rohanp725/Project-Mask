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
            Mat("SteelFrame", 0.2f, 0.22f, 0.22f);
            Mat("Newsprint", 0.62f, 0.6f, 0.55f);  // paper that isn't lit from inside ("Paper" glows, for signs)
            Mat("Sack", 0.52f, 0.45f, 0.34f);
            Mat("Carton", 0.58f, 0.54f, 0.45f);     // archive boxes: matt board, nothing that glows in the dark
            Mat("CartonDark", 0.44f, 0.4f, 0.32f);
            EnsureLayer("NoReflection");
            EnsureLayer("MirrorGhost");
            EnsureLayer("PlayerBody");

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
            var boilerDoor = BuildArchive(archive, rng);
            var (breaker, closetDoor, furnaceGlow) = BuildBoilerRoom(boiler, rng);
            BuildExterior(exterior, rng);

            MarkStatic(env);
            foreach (var door in env.GetComponentsInChildren<World.Door>(true))
                foreach (var t in door.transform.parent.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            BakeNavMesh(env, SceneDir + "/Semblance_Tutorial_NavMesh.asset");

            Group("Lights", null);
            foreach (var lamp in DeferredLamps) lamp();

            // At the middle window, facing out: that's where the camera finds him on the way in. His hat's on the hook.
            var window = TutorialAnchor("Window", new Vector3(7.3f, 0.1f, 0.0f), Vector3.right);
            var playerCamera = BuildPlayer(window.position, hat: false);
            playerCamera.transform.root.rotation = Quaternion.LookRotation(window.forward);
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
            PlaceTutorialResident(residents, alma, new Vector3(-2.3f, 0.1f, -4.68f), 0f);
            TutorialAnchor("Dispatch", new Vector3(-6.97f, 0.1f, -3.1f), Vector3.left); // the seat is behind her here
            PlaceTutorialResident(residents, teo, new Vector3(12f, -3.5f, -0.9f), 180f);
            TutorialAnchor("TeoCloset", new Vector3(12f, -3.5f, -0.9f), Vector3.back);

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
            SetField(stage, "boilerDoor", boilerDoor);
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

        /// <summary>
        /// A wall-hung thing's inspect trigger, moved wholly onto the room side of it (toward <paramref name="room"/>):
        /// 25 cm deep, starting at its face. A trigger that reaches back into the wall can be hit from the next room.
        /// </summary>
        private static void TriggerInFront(GameObject go, Vector3 room)
        {
            var box = go.GetComponent<BoxCollider>();
            if (box == null) return;
            float sign = Mathf.Sign(go.transform.InverseTransformPoint(room).z);
            float sz = Mathf.Max(go.transform.lossyScale.z, 1e-3f);
            box.size = new Vector3(box.size.x, box.size.y, 0.25f / sz);
            box.center = new Vector3(box.center.x, box.center.y, sign * (0.5f + 0.125f / sz));
        }

        private enum DoorStyle { Office, Fire, Plank }

        /// <summary>
        /// A door in a 0.9 m opening. The frame (jambs, head, stops, architrave, threshold) stays put; only the panel
        /// swings, on a hinge at the given corner, and the stops lap its edges so nothing shows round it when it's
        /// shut. Office: a panelled door with a frosted light and a brass kick plate. Fire: painted steel with a
        /// wired-glass vision panel, a closer and a push bar on the way out. Plank: a ledged and braced cupboard door.
        /// </summary>
        private static World.Door TutorialDoor(Transform parent, string name, Vector3 hingePos, float yaw, bool locked = false, string lockedMessage = null,
            bool plate = false, DoorStyle style = DoorStyle.Office, bool lettering = false, float wall = 0.2f)
        {
            // The clear opening is 0.9 x 2.1 (every lintel in the building sits at floor + 2.1). The frame lines it:
            // jambs and head exactly as deep as the wall, so their faces are flush with the plaster; architrave laid
            // flat on each wall face, lapping the jamb so no seam shows; stops on the closing side that lap the panel.
            const float opening = 0.9f, height = 2.1f, pw = 0.81f, jw = 0.04f;
            string jamb = style == DoorStyle.Fire ? "SteelFrame" : "Wood"; // fire doors: a painted pressed-steel frame
            float wallFace = wall * 0.5f;
            var frame = Group(name + " Frame", parent);
            frame.localPosition = hingePos;
            frame.localRotation = Quaternion.Euler(0, yaw, 0);
            foreach (var x in new[] { jw * 0.5f, opening - jw * 0.5f })
                Box("Jamb", frame, new Vector3(x, height * 0.5f, 0), new Vector3(jw, height, wall), jamb, collider: false);
            Box("Head", frame, new Vector3(opening * 0.5f, height - jw * 0.5f, 0), new Vector3(opening, jw, wall), jamb, collider: false);
            const float aw = 0.07f, at = 0.018f;
            foreach (var side in new[] { -1f, 1f })
            {
                float z = side * (wallFace + at * 0.5f);
                foreach (var x in new[] { -aw * 0.5f + 0.01f, opening + aw * 0.5f - 0.01f })
                    Box("Architrave", frame, new Vector3(x, (height + aw - 0.01f) * 0.5f, z), new Vector3(aw, height + aw - 0.01f, at), jamb, collider: false);
                Box("Architrave Head", frame, new Vector3(opening * 0.5f, height + aw * 0.5f - 0.01f, z), new Vector3(opening + aw * 2f - 0.02f, aw, at), jamb, collider: false);
            }
            Box("Threshold", frame, new Vector3(opening * 0.5f, 0.005f, 0), new Vector3(opening - jw * 2f, 0.01f, wall), style == DoorStyle.Fire ? "Metal" : "Wood", collider: false);
            // Stops on the +z side (the door shuts against them and swings to -z), lapping the panel's edges and top.
            const float sw = 0.025f, sd = 0.022f, sz = 0.025f + sd * 0.5f;
            foreach (var x in new[] { jw + sw * 0.5f, opening - jw - sw * 0.5f })
                Box("Stop", frame, new Vector3(x, (height - jw) * 0.5f, sz), new Vector3(sw, height - jw, sd), jamb, collider: false);
            Box("Stop", frame, new Vector3(opening * 0.5f, height - jw - sw * 0.5f, sz), new Vector3(opening - jw * 2f, sw, sd), jamb, collider: false);

            // The hinge knuckles sit on the swing face at the hinge jamb; the panel hangs off them.
            var hinge = Group(name + " Hinge", frame);
            hinge.localPosition = new Vector3(0.045f, 0, -0.03f);
            foreach (var y in new[] { 0.3f, 1.02f, 1.75f })
                Box("Hinge Knuckle", hinge, new Vector3(0, y, 0), new Vector3(0.02f, 0.1f, 0.02f), "Brass", collider: false);
            string skin = style == DoorStyle.Fire ? "PlainSage" : "Door";
            var panel = Box(name, hinge, new Vector3(pw * 0.5f, 1.025f, 0.03f), new Vector3(pw, 2.03f, 0.05f), skin); // 1 cm off the floor, 2 cm under the head
            float cx = pw * 0.5f, front = 0.03f + 0.025f, back = 0.03f - 0.025f;
            if (style == DoorStyle.Office)
            {
                // A frosted light in the top half, a raised panel below, brass furniture.
                Box("Light", hinge, new Vector3(cx, 1.55f, 0.03f), new Vector3(0.56f, 0.72f, 0.058f), "Glass", collider: false);
                foreach (var z in new[] { front + 0.005f, back - 0.005f })
                {
                    Box("Glazing Bar", hinge, new Vector3(cx, 1.55f, z), new Vector3(0.6f, 0.03f, 0.006f), "Wood", collider: false);
                    Box("Glazing Bar", hinge, new Vector3(cx, 1.55f, z), new Vector3(0.03f, 0.76f, 0.006f), "Wood", collider: false);
                    if (lettering) Box("Lettering", hinge, new Vector3(cx, 1.77f, z + (z > 0.03f ? 0.008f : -0.008f)), new Vector3(0.42f, 0.07f, 0.004f), "Writing", collider: false);
                }
                Box("Raised Panel", hinge, new Vector3(cx, 0.52f, 0.03f), new Vector3(0.56f, 0.64f, 0.056f), "Wood", collider: false);
                Box("Kick Plate", hinge, new Vector3(cx, 0.13f, 0.03f), new Vector3(0.7f, 0.2f, 0.054f), "Brass", collider: false);
                foreach (var face in new[] { -1f, 1f })
                {
                    Box("Knob Rose", hinge, new Vector3(pw - 0.09f, 1.0f, 0.03f + face * 0.03f), new Vector3(0.07f, 0.07f, 0.012f), "Brass", collider: false);
                    Box("Knob", hinge, new Vector3(pw - 0.09f, 1.0f, 0.03f + face * 0.062f), new Vector3(0.05f, 0.05f, 0.05f), "Brass", collider: false);
                }
                if (plate)
                    Box("Door Plate", hinge, new Vector3(cx, 1.1f, front + 0.003f), new Vector3(0.26f, 0.07f, 0.005f), "Brass", collider: false);
            }
            else if (style == DoorStyle.Fire)
            {
                // Steel: a wired-glass slot, a closer up top, a push bar on the way out and a lever on the way in.
                Box("Wired Glass", hinge, new Vector3(cx + 0.1f, 1.5f, 0.03f), new Vector3(0.18f, 0.5f, 0.056f), "Glass", collider: false);
                foreach (var z in new[] { front + 0.004f, back - 0.004f })
                    for (int i = -1; i <= 1; i++)
                        Box("Wire", hinge, new Vector3(cx + 0.1f + i * 0.05f, 1.5f, z), new Vector3(0.004f, 0.5f, 0.004f), "Metal", collider: false);
                Box("Kick Plate", hinge, new Vector3(cx, 0.15f, 0.03f), new Vector3(pw - 0.04f, 0.26f, 0.054f), "Galvanized", collider: false);
                Box("Push Bar", hinge, new Vector3(cx, 1.02f, front + 0.05f), new Vector3(pw - 0.2f, 0.05f, 0.04f), "Galvanized", collider: false);
                foreach (var x in new[] { 0.14f, pw - 0.14f })
                    Box("Bar Bracket", hinge, new Vector3(x, 1.02f, front + 0.025f), new Vector3(0.06f, 0.08f, 0.05f), "Metal", collider: false);
                Box("Lever", hinge, new Vector3(pw - 0.12f, 1.02f, back - 0.03f), new Vector3(0.14f, 0.025f, 0.025f), "Metal", collider: false);
                Box("Lever Rose", hinge, new Vector3(pw - 0.09f, 1.02f, back - 0.008f), new Vector3(0.06f, 0.06f, 0.012f), "Metal", collider: false);
                Box("Closer", hinge, new Vector3(pw - 0.25f, 1.96f, back - 0.04f), new Vector3(0.24f, 0.07f, 0.06f), "Metal", collider: false);
                Box("Closer Arm", hinge, new Vector3(pw - 0.1f, 2.0f, back - 0.09f), new Vector3(0.3f, 0.02f, 0.02f), "Metal", collider: false).transform.localRotation = Quaternion.Euler(0, 35f, 0);
            }
            else
            {
                // Ledged and braced: two ledges and a diagonal on the inside, a latch and a ring on the out.
                foreach (var y in new[] { 0.45f, 1.65f })
                    Box("Ledge", hinge, new Vector3(cx, y, back - 0.012f), new Vector3(pw - 0.06f, 0.12f, 0.024f), "Wood", collider: false);
                Box("Brace", hinge, new Vector3(cx, 1.05f, back - 0.012f), new Vector3(0.11f, 1.25f, 0.024f), "Wood", collider: false).transform.localRotation = Quaternion.Euler(0, 0, 32f);
                Box("Latch", hinge, new Vector3(pw - 0.1f, 1.0f, front + 0.012f), new Vector3(0.14f, 0.03f, 0.02f), "Metal", collider: false);
                Box("Ring Pull", hinge, new Vector3(pw - 0.1f, 0.94f, front + 0.02f), new Vector3(0.05f, 0.05f, 0.012f), "Metal", collider: false);
                Box("Knob", hinge, new Vector3(pw - 0.1f, 1.0f, back - 0.03f), new Vector3(0.04f, 0.04f, 0.04f), "Wood", collider: false);
            }
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
            Part("Rose", fixture, new Vector3(0, 0.215f, 0), new Vector3(0.09f, 0.09f, 0.09f), "Metal"); // up into the ceiling (fittings hang 0.25 below it)
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
        /// <summary>
        /// A steel locker with louvred doors: thin panels, so the inside is the back of the outside and you look out
        /// between the slats. The hiding place under the red lamp.
        /// </summary>
        private static void Locker(Transform root, Vector3 localBase, float yaw)
        {
            var w = Piece("Locker", root, localBase, yaw, new Vector3(1.0f, 2.1f, 0.65f));
            Part("Back", w, new Vector3(0, 1.05f, -0.319f), new Vector3(1.0f, 2.1f, 0.012f), "Galvanized");
            foreach (var x in new[] { -0.494f, 0.494f })
                Part("Side", w, new Vector3(x, 1.05f, 0), new Vector3(0.012f, 2.1f, 0.65f), "Galvanized");
            Part("Divider", w, new Vector3(0, 1.05f, -0.05f), new Vector3(0.012f, 2.0f, 0.5f), "Metal");
            Part("Top", w, new Vector3(0, 2.094f, 0), new Vector3(1.0f, 0.012f, 0.65f), "Galvanized");
            Part("Plinth", w, new Vector3(0, 0.03f, 0), new Vector3(1.0f, 0.06f, 0.65f), "HideBlack");
            foreach (var x in new[] { -0.245f, 0.245f })
            {
                LouvredDoor(w, new Vector3(x, 1.07f, 0.33f), 0.49f, 2.04f, "Galvanized", 1.25f, 1.95f);
                Part("Handle", w, new Vector3(x + (x < 0 ? 0.16f : -0.16f), 1.1f, 0.345f), new Vector3(0.02f, 0.12f, 0.02f), "Metal");
                Part("Number Plate", w, new Vector3(x, 2.0f, 0.34f), new Vector3(0.1f, 0.05f, 0.004f), "Writing");
            }
            var inside = Group("Inside", root);
            inside.localPosition = localBase + new Vector3(0, 0.1f, 0);
            inside.localRotation = Quaternion.Euler(0, yaw, 0);
            w.gameObject.AddComponent<Events.HidingSpot>().Configure(inside, "Hide in the locker");
        }

        /// <summary>
        /// A run of archive shelving: a steel frame with five boards, a metre deep and back to back, cartons of files
        /// on whichever faces look onto an aisle (each with its label strip, the odd one pulled out). Length is along
        /// local x; faceA is local -z, faceB local +z.
        /// </summary>
        private static void ArchiveRow(Transform root, Vector3 pos, float yaw, float length, bool faceA, bool faceB, Random rng)
        {
            const float depth = 1.0f;
            var s = Piece("Archive Shelf", root, pos, yaw, new Vector3(length, 2.2f, depth), FloorY);
            foreach (var x in new[] { -length * 0.5f + 0.03f, length * 0.5f - 0.03f })
                Part("Upright", s, new Vector3(x, FloorY + 1.1f, 0), new Vector3(0.05f, 2.2f, depth), "Galvanized");
            Part("Spine", s, new Vector3(0, FloorY + 1.1f, 0), new Vector3(length, 2.2f, 0.03f), "Metal");
            string[] cartons = { "Carton", "CartonDark", "Carton", "Carton" };
            for (int b = 0; b < 5; b++)
            {
                float y = FloorY + 0.06f + b * 0.43f;
                Part("Board", s, new Vector3(0, y, 0), new Vector3(length - 0.06f, 0.03f, depth), "Galvanized");
                foreach (float face in new[] { -1f, 1f })
                {
                    if (face < 0 ? !faceA : !faceB) continue;
                    for (float x = -length * 0.5f + 0.25f; x < length * 0.5f - 0.2f; x += 0.42f)
                    {
                        if (rng.NextDouble() < 0.12) continue;
                        float pull = rng.NextDouble() < 0.1 ? 0.08f : 0f;
                        var box = Part("Carton", s, new Vector3(x, y + 0.16f, face * (0.33f + pull)), new Vector3(0.38f, 0.27f, 0.3f), cartons[rng.Next(cartons.Length)]);
                        box.transform.localRotation = Quaternion.Euler(0, Lerp(rng, -3f, 3f), 0);
                        Part("Label", s, new Vector3(x, y + 0.2f, face * (0.485f + pull)), new Vector3(0.16f, 0.06f, 0.004f), "Writing");
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
            WallZ(office, "Lintel", -5.6f, -6.5f, -5.6f, 2.2f, h, wall);
            TutorialDoor(office, "Lobby Door", new Vector3(-6.5f, 0.1f, -5.6f), 0f, true, "Locked for the night. The lobby's dark, and the sheriff has the only key.", lettering: true);
            // West: the stairwell door, under the exit sign.
            WallX(office, "Wall West", -8.1f, -5.7f, 2.5f, 0, h, wall);
            WallX(office, "Wall West", -8.1f, 3.4f, 5.7f, 0, h, wall);
            WallX(office, "Lintel", -8.1f, 2.5f, 3.4f, 2.2f, h, wall);
            TutorialDoor(office, "Stairwell Door", new Vector3(-8.1f, 0.1f, 2.5f), -90f, style: DoorStyle.Fire);
            ExitSign(office, new Vector3(-7.95f, 2.42f, 2.95f), 90f);
            // East: three windows onto the rain. No glass: the room's open to the night, and the night to it.
            // One window per room: the sheriff's (z -5.5..-2.4), Klein's (-2.4..2.7) and the armory's (2.7..5.5),
            // each well clear of the walls between them.
            float[] windows = { -3.95f, 0f, 4.1f };
            WallX(office, "Wall East Low", 8.1f, -5.7f, 5.7f, 0, 0.95f, wall);
            WallX(office, "Wall East High", 8.1f, -5.7f, 5.7f, 2.45f, h, wall);
            float[][] piers = { new[] { -5.7f, -4.6f }, new[] { -3.3f, -0.65f }, new[] { 0.65f, 3.45f }, new[] { 4.75f, 5.7f } };
            foreach (var p in piers) WallX(office, "Wall East Pier", 8.1f, p[0], p[1], 0.95f, 2.45f, wall);
            foreach (float z in windows)
            {
                // Lining inside the opening (head, jambs) and a sill proud of the wall; no face of the trim sits in
                // the same plane as a face of the wall, or the two flicker through each other.
                Box("Window Head", office, new Vector3(8.1f, 2.42f, z), new Vector3(0.22f, 0.06f, 1.3f), "Trim", collider: false);
                Box("Window Sill", office, new Vector3(8.02f, 0.965f, z), new Vector3(0.36f, 0.06f, 1.46f), "Trim", collider: false);
                foreach (var side in new[] { -1f, 1f })
                    Box("Window Jamb", office, new Vector3(8.1f, 1.695f, z + side * 0.62f), new Vector3(0.22f, 1.39f, 0.06f), "Trim", collider: false);
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
            // (stopping at the lobby and stairwell doors' architraves)
            Part("Skirting", office, new Vector3((-8f + -6.56f) * 0.5f, 0.17f, -5.47f), new Vector3(-6.56f + 8f, 0.14f, 0.03f), "Wood");
            Part("Skirting", office, new Vector3((-5.54f + 8f) * 0.5f, 0.17f, -5.47f), new Vector3(8f + 5.54f, 0.14f, 0.03f), "Wood");
            Part("Skirting", office, new Vector3(-7.97f, 0.17f, (-5.5f + 2.44f) * 0.5f), new Vector3(0.03f, 0.14f, 2.44f + 5.5f), "Wood");
            Part("Skirting", office, new Vector3(-7.97f, 0.17f, (3.46f + 5.5f) * 0.5f), new Vector3(0.03f, 0.14f, 5.5f - 3.46f), "Wood");
            Part("Skirting", office, new Vector3(7.97f, 0.17f, 0), new Vector3(0.03f, 0.14f, 11f), "Wood");
            Part("Picture Rail", office, new Vector3(0, 2.75f, 5.47f), new Vector3(16f, 0.05f, 0.04f), "Wood");

            // Lighting: tubes over the corridors and the block, one of them tired; the lamps make the pools you walk between.
            foreach (var (x, z, flicker) in new[] { (-4.5f, 3.7f, false), (0f, 3.7f, false), (-4.5f, -3.7f, true), (0f, -3.7f, false), (4.5f, -3.7f, false), (-1.6f, 0f, false), (1.6f, 0f, false), (6.2f, 1.2f, false) })
                Fluorescent(office, new Vector3(x, ceiling - 0.3f, z), ceiling, flicker);
            SoundEmitter(office, new Vector3(0, 3f, 0), "lamp_buzz_loop", 0.1f, 10f);

            // Klein's office: the east strip between the sheriff's room and the armory, glazed off from the floor,
            // his desk by the middle window with the lamp still on. The first pool of light.
            BuildKleinsOffice(office, rng);
            var klein = OfficeDesk(office, new Vector3(6.3f, 0, 0.6f), -90f, rng, working: true);
            DeskLamp(office, new Vector3(6.35f, FloorY + 0.76f, -0.1f), "LampShade", new Color(1f, 0.78f, 0.5f), 2.6f);
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
            TutorialAnchor("Desk", new Vector3(5.6f, 0.1f, 1.35f), Vector3.right);

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
            TriggerInFront(cork, new Vector3(0, 1.5f, 0));
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
                    var desk = OfficeDesk(office, new Vector3(cx, 0, row * 0.45f), row > 0 ? 180f : 0f, rng, working: index == 1);
                    float backZ = row * (t * 0.5f + 0.006f);         // the spine's face
                    float sideX = cx - 1f + t * 0.5f + 0.006f;       // the left panel's inner face
                    CubicleStory(office, desk, index, cx, row, backZ, sideX, rng);
                    index++;
                }
            foreach (var (bx, bz) in new[] { (-2.75f, 1.9f), (0.75f, 1.9f), (2.75f, -1.9f) })
            {
                Round("Bin", office, new Vector3(bx, FloorY + 0.16f, bz), 0.28f, 0.32f, "Galvanized");
                Round("Bin Rim", office, new Vector3(bx, FloorY + 0.325f, bz), 0.3f, 0.015f, "Metal");
                Part("Bin Paper", office, new Vector3(bx + 0.03f, FloorY + 0.3f, bz - 0.02f), new Vector3(0.12f, 0.06f, 0.1f), "Paper").transform.localRotation = Quaternion.Euler(20f, 30f, 0);
            }
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
                    Part("Packed Carton", desk, new Vector3(0, top + 0.14f, 0), new Vector3(0.4f, 0.28f, 0.32f), "Carton");
                    Part("Carton Flap", desk, new Vector3(0, top + 0.29f, 0.17f), new Vector3(0.4f, 0.01f, 0.1f), "Carton").transform.localRotation = Quaternion.Euler(-25f, 0, 0);
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

        /// <summary>
        /// Klein's office: the east strip between the sheriff's room and the armory cage, glazed off like the
        /// sheriff's, his desk by the middle window, a visitor's chair, a cabinet, a bookcase, and the door to the floor.
        /// </summary>
        private static void BuildKleinsOffice(Transform office, Random rng)
        {
            const float h = 3.3f, t = 0.12f;
            void Segment(Vector3 a, Vector3 b, bool blindsPositive)
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
                var off = alongX ? new Vector3(0, 0, blindsPositive ? 0.08f : -0.08f) : new Vector3(blindsPositive ? 0.08f : -0.08f, 0, 0);
                Box("Blind", office, new Vector3(c.x, 1.7f, c.z) + off, Vector3.Scale(S(length - 0.13f, 0.02f), new Vector3(1, 1.16f, 1)), "PlainCream", collider: false);
            }
            // West partition with the door in it (door z 0.2..1.1); north partition up to the armory's cage.
            Segment(new Vector3(4.4f, 0, -2.4f), new Vector3(4.4f, 0, 0.2f), true);
            Segment(new Vector3(4.4f, 0, 1.1f), new Vector3(4.4f, 0, 2.7f), true);
            Box("Door Lintel", office, new Vector3(4.4f, (2.2f + h) * 0.5f, 0.65f), new Vector3(t, h - 2.2f, 0.9f), "StoreWall");
            Box("Office Wall", office, new Vector3(6.2f, 1.65f, 2.7f), new Vector3(3.6f + t, h, t), "StoreWall");
            Part("Office Skirting", office, new Vector3(6.2f, 0.17f, 2.62f), new Vector3(3.6f, 0.14f, 0.03f), "Wood");
            Part("Office Skirting", office, new Vector3(6.2f, 0.17f, 2.78f), new Vector3(3.6f, 0.14f, 0.03f), "Wood");
            Box("Corner Post", office, new Vector3(4.4f, 1.65f, 2.7f), new Vector3(t + 0.02f, h, t + 0.02f), "Wood");
            Box("Corner Post", office, new Vector3(4.4f, 1.65f, -2.4f), new Vector3(t + 0.02f, h, t + 0.02f), "Wood");
            TutorialDoor(office, "Klein's Door", new Vector3(4.4f, 0.1f, 0.2f), -90f, plate: true, lettering: true, wall: t);
            // The room: visitor's chair across the desk, cabinet in the corner, the hat and coat by the door, a bookcase.
            var cab = Piece("Filing Cabinet", office, new Vector3(7.6f, 0, -1.9f), 0f, new Vector3(0.5f, 1.32f, 0.62f), FloorY);
            Part("Body", cab, new Vector3(0, FloorY + 0.66f, 0), new Vector3(0.5f, 1.32f, 0.62f), "Galvanized");
            for (int d = 0; d < 3; d++)
                Part("Drawer Pull", cab, new Vector3(0, FloorY + 0.4f + d * 0.42f, -0.325f), new Vector3(0.1f, 0.025f, 0.02f), "Metal");
            Part("Cabinet Radio", office, new Vector3(7.6f, FloorY + 1.4f, -1.9f), new Vector3(0.3f, 0.16f, 0.14f), "Wood");
            Part("Hat Stand", office, new Vector3(4.85f, FloorY + 0.9f, -2.0f), new Vector3(0.04f, 1.8f, 0.04f), "Wood");
            Round("Hat Stand Foot", office, new Vector3(4.85f, FloorY + 0.02f, -2.0f), 0.36f, 0.04f, "Wood");
            Part("Klein's Hat", office, new Vector3(4.85f, FloorY + 1.84f, -2.0f), new Vector3(0.32f, 0.09f, 0.32f), "Suit");
            Part("Klein's Hat Crown", office, new Vector3(4.85f, FloorY + 1.95f, -2.0f), new Vector3(0.18f, 0.14f, 0.18f), "Suit");
            Part("Coat", office, new Vector3(4.93f, FloorY + 1.28f, -1.92f), new Vector3(0.2f, 0.8f, 0.12f), "Suit");
            Shelving("Bookshelf", office, new Vector3(6.6f, 0, 2.4f), 0f, 1.6f, 1.3f, 3, rng);
            Picture(office, new Vector3(4.485f, 1.95f, -1.2f), 90f, new Vector2(0.42f, 0.32f), "Paper");
            Part("Certificate Text", office, new Vector3(4.512f, 1.95f, -1.2f), new Vector3(0.002f, 0.2f, 0.3f), "Writing");
            Part("Rug", office, new Vector3(6.0f, FloorY + 0.006f, 0.2f), new Vector3(2.4f, 0.012f, 3.2f), "Rug");
        }

        /// <summary>The front desk along the south wall, the sergeant behind it, the lobby door at her elbow.</summary>
        private static void BuildFrontDesk(Transform office, Random rng)
        {
            var front = Piece("Front Desk", office, new Vector3(-2.75f, 0, -4.1f), 0f, new Vector3(3.6f, 1.05f, 0.8f), FloorY);
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
            DeskLamp(office, new Vector3(-4.2f, FloorY + 1.03f, -4.2f), "Sage", new Color(0.8f, 1f, 0.75f), 1.8f);
            Chair(office, new Vector3(-2.3f, 0, -5.1f), 0f, "Leather");
            Part("Cardigan", office, new Vector3(-2.3f, FloorY + 0.8f, -5.28f), new Vector3(0.4f, 0.45f, 0.08f), "Sage");
            // The duty roster beside her, where she'd see the swap if it were on it.
            var roster = Group("Duty Roster", office);
            roster.localPosition = new Vector3(-5.0f, 1.9f, -5.465f);
            Part("Roster Board", roster, Vector3.zero, new Vector3(0.9f, 0.7f, 0.03f), "Hay");
            Part("Roster Frame", roster, new Vector3(0, 0.36f, 0), new Vector3(0.94f, 0.03f, 0.04f), "Wood");
            Part("Roster Frame", roster, new Vector3(0, -0.36f, 0), new Vector3(0.94f, 0.03f, 0.04f), "Wood");
            // The sheet: a ruled grid, seven day columns, three rows, names in the cells, Thursday's custodial cell empty.
            Part("Roster Sheet", roster, new Vector3(0, 0.02f, 0.02f), new Vector3(0.62f, 0.44f, 0.004f), "Paper");
            Part("Roster Title", roster, new Vector3(-0.12f, 0.2f, 0.024f), new Vector3(0.3f, 0.04f, 0.002f), "Writing");
            for (int col = 0; col <= 7; col++)
                Part("Rule", roster, new Vector3(-0.31f + col * 0.0886f, -0.02f, 0.024f), new Vector3(0.003f, 0.32f, 0.002f), "HideBlack");
            for (int r = 0; r <= 3; r++)
                Part("Rule", roster, new Vector3(0, 0.14f - r * 0.1067f, 0.024f), new Vector3(0.62f, 0.003f, 0.002f), "HideBlack");
            for (int col = 0; col < 7; col++)
                for (int r = 0; r < 3; r++)
                {
                    bool blank = r == 2 && col == 3; // Thursday, custodial
                    if (blank) { Part("Pencil Mark", roster, new Vector3(-0.31f + (col + 0.5f) * 0.0886f, 0.14f - (r + 0.5f) * 0.1067f, 0.025f), new Vector3(0.02f, 0.03f, 0.002f), "Ash"); continue; }
                    Part("Entry", roster, new Vector3(-0.31f + (col + 0.5f) * 0.0886f, 0.14f - (r + 0.5f) * 0.1067f, 0.025f), new Vector3(0.07f, 0.025f, 0.002f), "Writing");
                }
            Part("Pin", roster, new Vector3(-0.29f, 0.22f, 0.028f), new Vector3(0.016f, 0.016f, 0.01f), "BarnRed");
            Part("Pin", roster, new Vector3(0.29f, 0.22f, 0.028f), new Vector3(0.016f, 0.016f, 0.01f), "BarnRed");
            Part("Notice", roster, new Vector3(0.36f, -0.2f, 0.02f), new Vector3(0.14f, 0.2f, 0.004f), "Paper");
            Part("Notice Text", roster, new Vector3(0.36f, -0.2f, 0.024f), new Vector3(0.1f, 0.12f, 0.002f), "Writing");
            Inspect(roster.GetChild(0).gameObject, "Read the roster",
                "DUTY ROSTER, WEEK OF OCT 10. Nights: REYES. Custodial: T. BAPTISTE, M-W-F. Thursday is blank. Somebody has pencilled a question mark in it and rubbed most of it out again.",
                new Vector3(1f, 0.8f, 0.5f), clue: "tut_roster");
            TriggerInFront(roster.GetChild(0).gameObject, new Vector3(0, 1.5f, 0));
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
                Box("Blind", office, new Vector3(c.x, 1.7f, c.z) + off, Vector3.Scale(S(length - 0.13f, 0.02f), new Vector3(1, 1.16f, 1)), "PlainCream", collider: false);
            }
            // West wall of the office with the door in it; north wall full length.
            Segment(new Vector3(4.4f, 0, -5.5f), new Vector3(4.4f, 0, -3.4f), false);
            Box("Door Post", office, new Vector3(4.4f, 1.65f, -2.45f), new Vector3(t, h, 0.1f), "StoreWall");
            Box("Door Lintel", office, new Vector3(4.4f, (2.2f + h) * 0.5f, -2.95f), new Vector3(t, h - 2.2f, 0.9f), "StoreWall");
            Segment(new Vector3(4.4f, 0, -2.4f), new Vector3(8.0f, 0, -2.4f), false);
            Box("Corner Post", office, new Vector3(4.4f, 1.65f, -2.4f), new Vector3(t + 0.02f, h, t + 0.02f), "Wood");
            TutorialDoor(office, "Sheriff's Door", new Vector3(4.4f, 0.1f, -3.4f), -90f, true, "The sheriff's office. Locked, and his light's off. He went home at six. He has somebody to go home to.", plate: true, lettering: true, wall: t);
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
            Mesh(new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), 0.4f, 1.3f); // the corridor side, with the gate
            var hinge = Group("Armory Gate Hinge", office);
            hinge.localPosition = new Vector3(x0, FloorY, z0 + 1.3f);
            hinge.localRotation = Quaternion.Euler(0, 90f, 0);
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
            Part("Armory Sign", office, new Vector3(x0 - 0.035f, FloorY + h - 0.16f, z0 + 2.1f), new Vector3(0.02f, 0.22f, 0.9f), "SignWhite");
            Part("Sign Stripe", office, new Vector3(x0 - 0.047f, FloorY + h - 0.16f, z0 + 2.1f), new Vector3(0.004f, 0.04f, 0.9f), "PlainBarnRed");
            Part("Sign Letters", office, new Vector3(x0 - 0.047f, FloorY + h - 0.19f, z0 + 2.1f), new Vector3(0.004f, 0.08f, 0.6f), "Writing");
            foreach (var sz in new[] { z0 + 1.7f, z0 + 2.5f })
                Part("Sign Wire", office, new Vector3(x0 - 0.035f, FloorY + h - 0.03f, sz), new Vector3(0.006f, 0.08f, 0.006f), "Metal");
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
            Part("Vest", office, new Vector3(7.9f, FloorY + 1.3f, 3.1f), new Vector3(0.06f, 0.6f, 0.46f), "Suit");
            Part("Vest Hook", office, new Vector3(7.96f, FloorY + 1.65f, 3.1f), new Vector3(0.08f, 0.04f, 0.04f), "Brass");
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
            foreach (var (y, w) in new[] { (0.64f, 1.76f), (-0.64f, 1.76f) })
                Part("Map Frame", map, new Vector3(0, y, 0), new Vector3(w, 0.04f, 0.03f), "Wood");
            foreach (var x in new[] { -0.87f, 0.87f })
                Part("Map Frame", map, new Vector3(x, 0, 0), new Vector3(0.04f, 1.3f, 0.03f), "Wood");
            // Roads: the highway north, the county road across it, farm tracks off both; towns as dots with names.
            void Road(Vector2 a, Vector2 b, float width, string mat)
            {
                var c = (a + b) * 0.5f;
                var d = b - a;
                var r = Part("Map Road", map, new Vector3(c.x, c.y, 0.007f), new Vector3(d.magnitude, width, 0.002f), mat);
                r.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }
            Road(new Vector2(-0.1f, -0.58f), new Vector2(0.05f, 0.1f), 0.014f, "HideBlack");
            Road(new Vector2(0.05f, 0.1f), new Vector2(0.22f, 0.55f), 0.014f, "HideBlack");
            Road(new Vector2(-0.8f, -0.2f), new Vector2(0.75f, -0.05f), 0.012f, "HideBlack");
            Road(new Vector2(-0.6f, 0.4f), new Vector2(0.0f, 0.12f), 0.008f, "Ash");
            Road(new Vector2(0.3f, -0.5f), new Vector2(0.7f, -0.35f), 0.008f, "Ash");
            Road(new Vector2(0.1f, 0.3f), new Vector2(0.6f, 0.45f), 0.008f, "Ash");
            Road(new Vector2(-0.5f, -0.5f), new Vector2(-0.05f, -0.3f), 0.008f, "Ash");
            // A river, and the county line.
            Road(new Vector2(-0.85f, 0.5f), new Vector2(-0.3f, 0.2f), 0.02f, "PlainBlue");
            Road(new Vector2(-0.3f, 0.2f), new Vector2(0.4f, 0.0f), 0.02f, "PlainBlue");
            Road(new Vector2(-0.85f, -0.62f), new Vector2(0.85f, -0.62f), 0.006f, "Rust");
            foreach (var (tx, ty) in new[] { (-0.1f, -0.58f), (0.0f, -0.12f), (-0.45f, -0.35f), (0.55f, -0.4f), (-0.3f, 0.35f), (0.5f, 0.42f), (0.4f, -0.05f) })
            {
                Part("Town Dot", map, new Vector3(tx, ty, 0.008f), new Vector3(0.025f, 0.025f, 0.002f), "HideBlack");
                Part("Town Name", map, new Vector3(tx + 0.07f, ty + 0.02f, 0.008f), new Vector3(0.1f, 0.025f, 0.002f), "Writing");
                Part("Map Pin", map, new Vector3(tx, ty, 0.012f), new Vector3(0.018f, 0.018f, 0.012f), "Brass");
            }
            // Semblance, top of the highway, two hours north: no pin, a pencil circle, and the mileage beside it.
            Part("Town Dot", map, new Vector3(0.22f, 0.55f, 0.008f), new Vector3(0.025f, 0.025f, 0.002f), "HideBlack");
            Part("Town Name", map, new Vector3(0.3f, 0.57f, 0.008f), new Vector3(0.12f, 0.025f, 0.002f), "Writing");
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                var seg = Part("Pencil Circle", map, new Vector3(0.22f + Mathf.Cos(a) * 0.075f, 0.55f + Mathf.Sin(a) * 0.075f, 0.009f), new Vector3(0.05f, 0.006f, 0.002f), "Ash");
                seg.transform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg + 90f);
            }
            Part("Mileage", map, new Vector3(0.36f, 0.5f, 0.009f), new Vector3(0.06f, 0.02f, 0.002f), "Ash");
            Part("Map Legend", map, new Vector3(-0.65f, -0.5f, 0.008f), new Vector3(0.3f, 0.12f, 0.002f), "Writing");
            Inspect(map.GetChild(0).gameObject, "Look at the map", "Barlow County. Pins in every town but one. Somebody has drawn a circle round Semblance in pencil, two hours north, and written the mileage beside it: 94.", new Vector3(1.8f, 1.3f, 0.25f), clue: "tut_map");
            TriggerInFront(map.GetChild(0).gameObject, new Vector3(0, 1.5f, 0));
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
            Chair(office, new Vector3(-6.55f, 0, -3.1f), -90f, "Leather");
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
            wall.transform.localPosition = new Vector3(3.65f, FloorY + 0.9f, z);
            wall.AddComponent<BoxCollider>().size = new Vector3(1.0f, 1.8f, 0.6f);
            int n = 0;
            foreach (var x in new[] { 3.3f, 3.72f, 4.14f })
            {
                int height = x == 3.72f ? 4 : 3;
                for (int i = 0; i < height; i++)
                {
                    var carton = Part("Archive Carton", office, new Vector3(x, FloorY + 0.135f + i * 0.272f, z), new Vector3(0.4f, 0.27f, 0.32f), n++ % 3 == 1 ? "CartonDark" : "Carton");
                    carton.transform.localRotation = Quaternion.Euler(0, Lerp(rng, -4f, 4f), 0);
                    Part("Carton Label", carton.transform, new Vector3(0, 0.03f, -0.163f), new Vector3(0.16f, 0.06f, 0.004f), "Writing");
                }
            }
            // A chair up on the lower stack, and one on the floor in front with a carton on its seat.
            Chair(office, new Vector3(4.14f, 0.82f, z), 90f, "Wood");
            Chair(office, new Vector3(3.5f, 0f, z - 0.62f), 0f, "Wood");
            Part("Archive Carton", office, new Vector3(3.5f, FloorY + 0.61f, z - 0.62f), new Vector3(0.38f, 0.26f, 0.3f), "CartonDark");
            // The folding sign in the aisle, an A-frame.
            var sign = Group("Wet Floor Sign", office);
            sign.localPosition = new Vector3(4.05f, FloorY, 1.45f); // beside the chair, not through it
            foreach (var side in new[] { -1f, 1f })
            {
                var board = Part("Board", sign, new Vector3(0, 0.3f, side * 0.07f), new Vector3(0.3f, 0.62f, 0.015f), "Mustard");
                board.transform.localRotation = Quaternion.Euler(side * -13f, 0, 0);
            }
            var text = Part("Text", sign, new Vector3(0, 0.34f, -0.085f), new Vector3(0.22f, 0.28f, 0.004f), "Writing");
            text.transform.localRotation = Quaternion.Euler(13f, 0, 0);
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
            TutorialDoor(stairwell, "Archive Door", new Vector3(-8.1f, -3.5f, -5.9f), -90f, style: DoorStyle.Fire); // spans z -5.9..-5.0, swings into the archive
            // Handrail down the open side.
            var rail = Box("Handrail", stairwell, new Vector3(-8.35f, -0.8f, -1.5f), new Vector3(0.05f, 0.05f, 7.0f), "Metal", collider: false);
            rail.transform.localRotation = Quaternion.Euler(-31f, 0, 0);
            foreach (var z in new[] { 1.2f, -1.5f, -4.2f })
            {
                float y = 0.1f - 0.18f * ((1.5f - z) / 0.3f) + 0.45f;
                Box("Rail Post", stairwell, new Vector3(-8.35f, y, z), new Vector3(0.04f, 0.9f, 0.04f), "Metal", collider: false);
            }
            CagedBulb(stairwell, new Vector3(-9.2f, 3.2f - 0.25f, 2.6f), 6f, 1.2f);
            // Nothing overhead at the foot of the stairs but the shaft: this one is on a bracket off the south wall.
            CagedBulb(stairwell, new Vector3(-9.2f, -1.3f, -5.95f), 5f, 1.0f, flicker: true);
            Part("Lamp Bracket", stairwell, new Vector3(-9.2f, -1.3f + 0.2f, -6.06f), new Vector3(0.04f, 0.04f, 0.3f), "Metal");
            Part("Bracket Plate", stairwell, new Vector3(-9.2f, -1.3f + 0.2f, -6.19f), new Vector3(0.14f, 0.14f, 0.02f), "Metal");
            SoundEmitter(stairwell, new Vector3(-9.8f, -3f, -5.8f), "drip_loop", 0.25f, 7f);
            ExitSign(stairwell, new Vector3(-8.25f, -1.15f, -5.45f), 90f);
            Zone(stairwell, "Stair Floor", new Vector3(-9.2f, -1.5f, -1f), new Vector3(2.4f, 8f, 11f), Audio.Surface.Asphalt, 2);
            AddMarker(stairwell, "the stairwell", 3.5f, 3, new Vector3(-9.2f, 0, -1f));
        }

        // ------------------------------------------------------------ the archive

        // The archive is a maze of stacks, built off this plan (one cell is 1.2 m; west is the left edge, north the top):
        //   #  shelving          .  aisle                L  the locker (its doors face north, into the alcove)
        //   C  cartons stacked across the aisle, low enough to see over and too much to climb
        //   F  filing cabinets (you can see over them)    S  the evidence cage (bars: you can see through it)
        // The stairwell door opens on the west side of row 3; the boiler room door is on the east side of row 3.
        //
        // The route, and the beats on it: a lobby with a dead end to the south (a carton falls: nothing there), north
        // into the long first aisle (the locker's alcove on the right under a red lamp, where the thing walks
        // towards you and you hide), south at the aisle's end, east again, north up the far block (the carton wall
        // at its bottom lets you see the corridor beyond but not reach it), east under the second red lamp, and then
        // the run: south down the long corridor beside the cage (the EXIT sign visible through the bars, a whole loop
        // away), along the end wall under the third lamp, and up to the door. The chase starts at the top of that run.
        private static readonly string[] ArchivePlan =
        {
            "###############",
            "........##...##",
            ".##.###.##.#.#.",
            "..#L###....#.S.",
            ".######.FF.#.S.",
            ".######....C.S.",
            ".###########.#.",
            "..##########...",
        };
        private const float Cell = 1.2f;
        private static float ArchiveX(int i) => -7.5f + Cell * i;
        private static float ArchiveZ(int j) => -1.8f - Cell * j;
        private static Vector3 ArchiveCell(int i, int j, float y) => new Vector3(ArchiveX(i), y, ArchiveZ(j));

        private static World.Door BuildArchive(Transform archive, Random rng)
        {
            const float floor = -3.5f, top = -0.9f, bottom = -3.6f;
            float pieceY = floor - FloorY; // furniture builders put their parts FloorY above the piece
            string wall = "Concrete";
            Box("Floor", archive, new Vector3(1f, floor - 0.05f, -6f), new Vector3(18.4f, 0.1f, 10.4f), "Concrete");
            Box("Ceiling", archive, new Vector3(1f, top + 0.06f, -6f), new Vector3(18.4f, 0.12f, 10.4f), "ConcreteDark");
            WallX(archive, "Wall West", -8.1f, -11.1f, -6.3f, bottom, top, wall);
            WallX(archive, "Wall East", 10.1f, -11.1f, -5.45f, bottom, top, wall);
            WallX(archive, "Wall East", 10.1f, -4.55f, -1.0f, bottom, top, wall);
            WallX(archive, "Lintel", 10.1f, -5.45f, -4.55f, -1.4f, top, wall);
            WallZ(archive, "Wall South", -11.1f, -8.2f, 10.2f, bottom, top, wall);
            WallZ(archive, "Wall North", -0.9f, -8.2f, 10.2f, bottom, top, wall);
            var boilerDoor = TutorialDoor(archive, "Boiler Door", new Vector3(10.1f, floor, -5.45f), -90f, style: DoorStyle.Fire);
            Part("Door Placard", archive, new Vector3(9.995f, floor + 1.85f, -6.0f), new Vector3(0.008f, 0.14f, 0.4f), "SignWhite");
            Part("Door Placard Text", archive, new Vector3(9.989f, floor + 1.85f, -6.0f), new Vector3(0.004f, 0.06f, 0.32f), "Writing");

            // The stacks, off the plan: runs of shelving along the rows first, then whatever's left runs down the
            // columns. Cartons only go on faces that look onto an aisle.
            int rows = ArchivePlan.Length, cols = ArchivePlan[0].Length;
            var used = new bool[cols, rows];
            System.Func<int, int, char> at = (i, j) => i < 0 || j < 0 || i >= cols || j >= rows ? '#' : ArchivePlan[j][i];
            System.Func<int, int, bool> open = (i, j) => at(i, j) != '#' && at(i, j) != 'L';
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols;)
                {
                    if (at(i, j) != '#') { i++; continue; }
                    int n = 0;
                    while (i + n < cols && at(i + n, j) == '#') n++;
                    if (n >= 2)
                    {
                        bool north = false, south = false;
                        for (int k = 0; k < n; k++) { used[i + k, j] = true; north |= open(i + k, j - 1); south |= open(i + k, j + 1); }
                        ArchiveRow(archive, new Vector3((ArchiveX(i) + ArchiveX(i + n - 1)) * 0.5f, pieceY, ArchiveZ(j)), 0f, n * Cell, south, north, rng);
                    }
                    i += n;
                }
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < rows;)
                {
                    if (at(i, j) != '#' || used[i, j]) { j++; continue; }
                    int n = 0;
                    while (j + n < rows && at(i, j + n) == '#' && !used[i, j + n]) n++;
                    bool west = false, east = false;
                    for (int k = 0; k < n; k++) { used[i, j + k] = true; west |= open(i - 1, j + k); east |= open(i + 1, j + k); }
                    // Butt up against the runs above and below, so no slit of light shows at the joins.
                    float extN = at(i, j - 1) == '#' ? 0.1f : 0f, extS = at(i, j + n) == '#' ? 0.1f : 0f;
                    float zc = (ArchiveZ(j) + ArchiveZ(j + n - 1)) * 0.5f + (extN - extS) * 0.5f;
                    ArchiveRow(archive, new Vector3(ArchiveX(i), pieceY, zc), 90f, n * Cell + extN + extS, west, east, rng);
                    j += n;
                }

            // The locker, in its alcove off the first aisle, under the one lamp still burning down here.
            Locker(archive, new Vector3(ArchiveX(3), floor, ArchiveZ(3) + 0.27f), 0f);
            Box("Locker Backing", archive, new Vector3(ArchiveX(3), (floor + top) * 0.5f, ArchiveZ(3) - 0.33f), new Vector3(1.0f, top - floor, 0.54f), "ConcreteDark");
            RedLamp(archive, new Vector3(ArchiveX(3), top, ArchiveZ(2)), 4.5f, 0.6f);
            // The carton wall at the bottom of the far block: you see the corridor beyond it, and you go round.
            CartonWall(archive, ArchiveCell(11, 5, floor), Cell, 90f, rng);
            // Filing cabinets between the two short aisles: low enough to see across.
            foreach (var (i, j) in new[] { (8, 4), (9, 4) })
                foreach (var dx in new[] { -0.3f, 0.3f })
                    ArchiveCabinet(archive, new Vector3(ArchiveX(i) + dx, pieceY, ArchiveZ(j)), rng);
            // The evidence cage down the far column: bars both sides, tagged property on a table inside. From the
            // long corridor you see the EXIT sign through it, two aisles over and a whole loop away.
            EvidenceCage(archive, ArchiveX(13), floor, top, ArchiveZ(5) - Cell * 0.5f, ArchiveZ(3) + Cell * 0.5f, rng);

            // The plan, pinned on the end of the first stack where you can't miss it coming in. Drawn heads-up: the
            // way you're facing is up the sheet.
            var plan = Box("Archive Plan", archive, new Vector3(ArchiveX(2) - 0.515f, floor + 1.55f, ArchiveZ(3)), new Vector3(0.008f, 0.74f, 0.44f), "Paper");
            Inspect(plan, "Read the plan", "The archive, drawn up when the stacks went in and pinned at the door ever since. The sheet's turned the way you're facing: the boiler room is at the top, past the cage. Somebody's pencilled the way through as far as the far block. After that there's a coffee ring, and you're on your own.", null);
            float px = ArchiveX(2) - 0.521f, py0 = floor + 1.25f, pz0 = ArchiveZ(3) + 0.14f, cs = 0.04f;
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                    if (at(i, j) == '#')
                        Part("Plan Stack", archive, new Vector3(px, py0 + i * cs, pz0 - j * cs), new Vector3(0.004f, cs - 0.006f, cs - 0.006f), "Writing");
            Part("Plan Title", archive, new Vector3(px, floor + 1.87f, ArchiveZ(3)), new Vector3(0.004f, 0.05f, 0.3f), "Writing");
            Part("You Are Here", archive, new Vector3(px - 0.002f, py0 + 0 * cs, pz0 - 3 * cs), new Vector3(0.006f, 0.025f, 0.025f), "ExitRed");
            Part("Plan Boiler", archive, new Vector3(px, py0 + 14.5f * cs, pz0 - 3 * cs), new Vector3(0.004f, 0.03f, 0.12f), "ExitRed");
            foreach (var (a, b) in new[] { ((0, 3), (0, 1)), ((0, 1), (7, 1)), ((7, 1), (7, 3)), ((7, 3), (10, 3)) })
                Part("Pencil Line", archive, new Vector3(px - 0.001f, py0 + (a.Item1 + b.Item1) * 0.5f * cs, pz0 - (a.Item2 + b.Item2) * 0.5f * cs),
                    new Vector3(0.004f, Mathf.Max(0.008f, Mathf.Abs(b.Item1 - a.Item1) * cs), Mathf.Max(0.008f, Mathf.Abs(b.Item2 - a.Item2) * cs)), "HideBlack");
            var ring = Round("Coffee Ring", archive, new Vector3(px - 0.002f, py0 + 12f * cs, pz0 - 2.5f * cs), 0.13f, 0.003f, "Leather");
            ring.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            foreach (var dy in new[] { 0.36f, -0.36f })
                Part("Drawing Pin", archive, new Vector3(px - 0.003f, floor + 1.55f + dy, ArchiveZ(3)), new Vector3(0.008f, 0.014f, 0.014f), "Brass");

            // Her prints in the dust, the way she goes between the closet and the stacks: a line that leads on.
            PawTrail(archive, floor, new[] { (6, 1), (1, 1) }, rng);
            PawTrail(archive, floor, new[] { (7, 1), (7, 3), (10, 3), (10, 1), (12, 1), (12, 7), (14, 7), (14, 4) }, rng);

            // The lobby and its dead end: the hand truck, and round the corner the carton that just fell, the drain
            // it fell beside, the mop nobody's used since the spring.
            var trolley = Group("Hand Truck", archive);
            trolley.localPosition = new Vector3(-7.72f, floor, -6.75f);
            trolley.localRotation = Quaternion.Euler(0, 24f, 0);
            Part("Truck Frame", trolley, new Vector3(0, 0.6f, 0), new Vector3(0.45f, 1.2f, 0.04f), "Metal").transform.localRotation = Quaternion.Euler(-12f, 0, 0);
            Part("Truck Plate", trolley, new Vector3(0, 0.03f, 0.2f), new Vector3(0.45f, 0.02f, 0.4f), "Metal");
            foreach (var wx in new[] { -0.2f, 0.2f })
                Round("Truck Wheel", trolley, new Vector3(wx, 0.12f, -0.05f), 0.24f, 0.05f, "HideBlack").transform.localRotation = Quaternion.Euler(0, 0, 90f);
            Part("Carton", trolley, new Vector3(0, 0.18f, 0.2f), new Vector3(0.38f, 0.27f, 0.3f), "Carton");
            Part("Carton", trolley, new Vector3(0, 0.46f, 0.2f), new Vector3(0.38f, 0.27f, 0.3f), "CartonDark");
            Part("Carton", archive, new Vector3(-6.35f, floor + 0.135f, -10.25f), new Vector3(0.38f, 0.27f, 0.3f), "Carton").transform.localRotation = Quaternion.Euler(0, 24f, 0);
            Part("Carton Lid", archive, new Vector3(-6.0f, floor + 0.01f, -9.9f), new Vector3(0.4f, 0.02f, 0.32f), "Carton").transform.localRotation = Quaternion.Euler(0, -35f, 0);
            foreach (var p in new[] { new Vector3(-6.7f, floor + 0.004f, -9.8f), new Vector3(-6.1f, floor + 0.004f, -10.5f), new Vector3(-5.8f, floor + 0.004f, -10.15f), new Vector3(-6.45f, floor + 0.004f, -9.55f) })
                Part("Loose Page", archive, p, new Vector3(0.21f, 0.004f, 0.28f), "Newsprint").transform.localRotation = Quaternion.Euler(0, Lerp(rng, 0f, 180f), 0);
            Round("Floor Drain", archive, new Vector3(-6.0f, floor + 0.004f, -10.2f), 0.32f, 0.008f, "HideBlack");
            foreach (var dz in new[] { -0.08f, 0f, 0.08f })
                Part("Grate Bar", archive, new Vector3(-6.0f, floor + 0.009f, -10.2f + dz), new Vector3(0.3f, 0.004f, 0.02f), "Metal");
            Round("Puddle", archive, new Vector3(-6.2f, floor + 0.003f, -10.1f), 1.4f, 0.004f, "ConcreteDark");
            MopBucket(archive, new Vector3(-7.6f, floor, -10.6f), new Vector3(-0.25f, 0, -0.25f)); // handle in the corner
            // Cartons that never made it back onto a shelf, in the dead end north of the boiler door.
            Part("Carton", archive, new Vector3(9.5f, floor + 0.135f, -3.9f), new Vector3(0.38f, 0.27f, 0.3f), "Carton").transform.localRotation = Quaternion.Euler(0, 24f, 0);
            Part("Carton", archive, new Vector3(9.1f, floor + 0.135f, -3.75f), new Vector3(0.38f, 0.27f, 0.3f), "CartonDark").transform.localRotation = Quaternion.Euler(0, -60f, 0);
            Part("Carton", archive, new Vector3(9.1f, floor + 0.405f, -3.75f), new Vector3(0.38f, 0.27f, 0.3f), "Carton").transform.localRotation = Quaternion.Euler(0, -52f, 0);

            // Pipes under the ceiling: they run the way you're going, and they knock.
            Round("Pipe", archive, new Vector3(1f, top - 0.18f, ArchiveZ(1) + 0.42f), 0.14f, 18f, "Rust").transform.localRotation = Quaternion.Euler(0, 0, 90f);
            Round("Pipe", archive, new Vector3(ArchiveX(12) + 0.45f, top - 0.18f, -6.3f), 0.12f, 9.4f, "Rust").transform.localRotation = Quaternion.Euler(90f, 0, 0);
            Round("Pipe", archive, new Vector3(ArchiveX(8) + 0.6f, top - 0.22f, ArchiveZ(7) - 0.42f), 0.1f, 15f, "Rust").transform.localRotation = Quaternion.Euler(0, 0, 90f);
            foreach (var x in new[] { -5f, 0f, 5f })
                Part("Pipe Hanger", archive, new Vector3(x, top - 0.08f, ArchiveZ(1) + 0.42f), new Vector3(0.04f, 0.16f, 0.04f), "Metal");
            SoundEmitter(archive, new Vector3(-6.0f, top - 0.3f, -10.2f), "drip_loop", 0.3f, 10f);
            SoundEmitter(archive, new Vector3(9.3f, top - 0.3f, -4.0f), "drip_loop", 0.2f, 8f);
            SoundEmitter(archive, new Vector3(1f, top - 0.2f, -6f), "room_tone_loop", 0.06f, 14f);

            // The bulbs (dead until the breaker), one over each stretch of the route.
            foreach (var (i, j) in new[] { (0, 5), (2, 1), (6, 1), (7, 4), (9, 3), (10, 2), (12, 2), (12, 6), (14, 4) })
                CagedBulb(archive, ArchiveCell(i, j, top - 0.25f), 5.5f, 0.9f, flicker: i == 6);
            // The red lamps come on when the building goes dark: one at each turn, and the EXIT sign over the door.
            RedLamp(archive, new Vector3(ArchiveX(12), top, ArchiveZ(1)), 5f, 0.5f);
            RedLamp(archive, new Vector3(ArchiveX(13), top, ArchiveZ(7)), 5f, 0.5f);
            var em = new GameObject("Emergency Light");
            em.transform.SetParent(archive, false);
            em.transform.localPosition = new Vector3(9.45f, top - 0.6f, -5.0f);
            var emLight = em.AddComponent<Light>();
            emLight.type = LightType.Point;
            emLight.color = new Color(1f, 0.22f, 0.15f);
            emLight.range = 7f;
            emLight.intensity = 0.6f;
            emLight.shadows = LightShadows.None;
            ExitSign(archive, new Vector3(9.6f, top - 0.5f, -5.0f), 180f); // flag-mounted: seen from the far end of the run
            Part("Sign Bracket", archive, new Vector3(9.8f, top - 0.42f, -5.0f), new Vector3(0.4f, 0.03f, 0.03f), "Metal");
            ExitSign(archive, new Vector3(-7.95f, -1.2f, -5.45f), 90f);

            AddMarker(archive, "the archive", 10.5f, 2, new Vector3(1f, floor, -6f));
            Zone(archive, "Archive Floor", new Vector3(1f, floor, -6f), new Vector3(18f, 2f, 10f), Audio.Surface.Asphalt, 1);

            TutorialAnchor("ArchiveEntry", ArchiveCell(0, 3, floor), Vector3.right);
            TutorialAnchor("Thud", ArchiveCell(1, 7, floor), Vector3.left);
            TutorialAnchor("DogEast", ArchiveCell(7, 1, floor), Vector3.left);
            TutorialAnchor("LockerFront", ArchiveCell(3, 1, floor), Vector3.back);
            TutorialAnchor("DogWest", ArchiveCell(0, 2, floor), Vector3.back);
            TutorialAnchor("ChaseStart", ArchiveCell(10, 1, floor), Vector3.right);
            TutorialAnchor("ChaseFrom", ArchiveCell(10, 5, floor), Vector3.forward);
            TutorialAnchor("BoilerDoor", ArchiveCell(14, 4, floor), Vector3.forward);
            return boilerDoor;
        }

        /// <summary>A round galvanised bucket with a mop standing in it, the handle leaning toward <paramref name="lean"/>.</summary>
        private static void MopBucket(Transform root, Vector3 floorPos, Vector3 lean)
        {
            var b = Group("Mop Bucket", root);
            b.localPosition = floorPos;
            Round("Bucket", b, new Vector3(0, 0.15f, 0), 0.34f, 0.3f, "Galvanized");
            Round("Rim", b, new Vector3(0, 0.3f, 0), 0.37f, 0.02f, "Metal");
            Round("Water", b, new Vector3(0, 0.24f, 0), 0.3f, 0.01f, "ConcreteDark");
            Part("Bail", b, new Vector3(0, 0.36f, 0), new Vector3(0.36f, 0.012f, 0.012f), "Metal");
            // The mop: a head of grey strands in the water, the handle up out of it, leaning on the rim.
            Round("Mop Head", b, new Vector3(0.02f, 0.2f, 0.02f), 0.2f, 0.12f, "Fabric");
            var dir = (Vector3.up * 1f + lean).normalized;
            var start = new Vector3(0.02f, 0.18f, 0.02f);
            var handle = Round("Mop Handle", b, start + dir * 0.7f, 0.03f, 1.4f, "Wood");
            handle.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
        }

        /// <summary>
        /// Teo's tool board over the bench: a cork panel in a wooden frame, a shelf along the bottom with jars and a
        /// tin on it, and the tools on their hooks (saw, hammer, screwdrivers, wrench, pliers, a square, a coil of wire).
        /// </summary>
        private static void ToolBoard(Transform root, Vector3 wallFoot)
        {
            var t = Group("Tool Board", root);
            t.localPosition = wallFoot; // on the south wall's face, looking north (+z)
            const float y0 = 1.32f, w = 1.6f, h = 0.8f, face = 0.03f;
            Part("Cork", t, new Vector3(0, y0 + h * 0.5f, 0.015f), new Vector3(w, h, 0.02f), "Hay");
            foreach (var y in new[] { y0, y0 + h })
                Part("Frame", t, new Vector3(0, y, 0.02f), new Vector3(w + 0.06f, 0.04f, 0.035f), "Wood");
            foreach (var x in new[] { -w * 0.5f, w * 0.5f })
                Part("Frame", t, new Vector3(x, y0 + h * 0.5f, 0.02f), new Vector3(0.04f, h + 0.04f, 0.035f), "Wood");
            // The shelf along the bottom, on two brackets.
            Part("Shelf", t, new Vector3(0, y0 - 0.03f, 0.09f), new Vector3(w + 0.06f, 0.025f, 0.15f), "Wood");
            foreach (var x in new[] { -0.6f, 0.6f })
                Part("Shelf Bracket", t, new Vector3(x, y0 - 0.1f, 0.06f), new Vector3(0.025f, 0.12f, 0.1f), "Metal");
            foreach (var (x, d, hh, mat) in new[] { (-0.62f, 0.08f, 0.12f, "Glass"), (-0.5f, 0.08f, 0.1f, "Glass"), (-0.36f, 0.1f, 0.13f, "BarnRed"), (0.55f, 0.09f, 0.11f, "Glass") })
            {
                Round("Jar", t, new Vector3(x, y0 - 0.017f + hh * 0.5f, 0.09f), d, hh, mat);
                if (mat == "Glass") Round("Jar Lid", t, new Vector3(x, y0 - 0.017f + hh + 0.01f, 0.09f), d + 0.005f, 0.02f, "Metal");
            }
            Part("Box of Nails", t, new Vector3(0.38f, y0 + 0.02f, 0.09f), new Vector3(0.14f, 0.07f, 0.09f), "Carton");
            // Hooks, and what hangs on them.
            void Hook(float x, float y) => Part("Hook", t, new Vector3(x, y, face + 0.02f), new Vector3(0.015f, 0.015f, 0.04f), "Brass");
            // Hand saw: blade and handle, hanging by the handle.
            Hook(-0.55f, y0 + 0.66f);
            Part("Saw Blade", t, new Vector3(-0.5f, y0 + 0.36f, face + 0.01f), new Vector3(0.13f, 0.5f, 0.005f), "Galvanized").transform.localRotation = Quaternion.Euler(0, 0, -4f);
            Part("Saw Handle", t, new Vector3(-0.53f, y0 + 0.64f, face + 0.015f), new Vector3(0.11f, 0.12f, 0.025f), "Wood");
            // Hammer, head up.
            Hook(-0.28f, y0 + 0.62f);
            Part("Hammer Handle", t, new Vector3(-0.28f, y0 + 0.45f, face + 0.02f), new Vector3(0.028f, 0.3f, 0.028f), "Wood");
            Part("Hammer Head", t, new Vector3(-0.28f, y0 + 0.62f, face + 0.02f), new Vector3(0.13f, 0.035f, 0.035f), "Metal");
            // Two screwdrivers.
            foreach (var (x, mat) in new[] { (-0.1f, "BarnRed"), (-0.03f, "Mustard") })
            {
                Hook(x, y0 + 0.66f);
                Part("Screwdriver Handle", t, new Vector3(x, y0 + 0.6f, face + 0.02f), new Vector3(0.026f, 0.09f, 0.026f), mat);
                Part("Screwdriver Shaft", t, new Vector3(x, y0 + 0.48f, face + 0.02f), new Vector3(0.008f, 0.15f, 0.008f), "Metal");
            }
            // Wrench and pliers.
            Hook(0.14f, y0 + 0.66f);
            Part("Wrench", t, new Vector3(0.14f, y0 + 0.5f, face + 0.012f), new Vector3(0.035f, 0.28f, 0.01f), "Metal");
            Part("Wrench Jaw", t, new Vector3(0.14f, y0 + 0.65f, face + 0.012f), new Vector3(0.07f, 0.05f, 0.012f), "Metal");
            Hook(0.28f, y0 + 0.66f);
            foreach (var a in new[] { -6f, 6f })
                Part("Pliers", t, new Vector3(0.28f + a * 0.002f, y0 + 0.55f, face + 0.012f), new Vector3(0.014f, 0.2f, 0.01f), "Metal").transform.localRotation = Quaternion.Euler(0, 0, a);
            // A steel square, and a coil of wire on the last hook.
            Hook(0.45f, y0 + 0.66f);
            Part("Square", t, new Vector3(0.45f, y0 + 0.5f, face + 0.01f), new Vector3(0.025f, 0.3f, 0.006f), "Galvanized");
            Part("Square", t, new Vector3(0.52f, y0 + 0.36f, face + 0.01f), new Vector3(0.16f, 0.025f, 0.006f), "Galvanized");
            Hook(0.66f, y0 + 0.66f);
            Round("Wire Coil", t, new Vector3(0.66f, y0 + 0.54f, face + 0.03f), 0.2f, 0.035f, "Rust").transform.localRotation = Quaternion.Euler(90f, 0, 0);
        }

        /// <summary>A red emergency lamp on the ceiling, lit only when the power's off (the stage finds it by name).</summary>
        private static void RedLamp(Transform root, Vector3 ceiling, float range, float intensity)
        {
            var go = new GameObject("Emergency Light");
            go.transform.SetParent(root, false);
            go.transform.localPosition = ceiling + new Vector3(0, -0.4f, 0);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.25f, 0.18f);
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            Part("Emergency Lamp Box", root, ceiling + new Vector3(0, -0.14f, 0), new Vector3(0.36f, 0.08f, 0.16f), "Metal");
            Part("Emergency Lamp", root, ceiling + new Vector3(0, -0.22f, 0), new Vector3(0.3f, 0.1f, 0.1f), "ExitRed");
        }

        /// <summary>A steel filing cabinet, four drawers, with the odd one not quite shut.</summary>
        private static void ArchiveCabinet(Transform root, Vector3 pos, Random rng)
        {
            var c = Piece("Filing Cabinet", root, pos, 0f, new Vector3(0.5f, 1.45f, 0.62f), FloorY);
            Part("Body", c, new Vector3(0, FloorY + 0.725f, 0), new Vector3(0.5f, 1.45f, 0.62f), "Galvanized");
            for (int d = 0; d < 4; d++)
            {
                float y = FloorY + 0.2f + d * 0.34f;
                float pull = rng.NextDouble() < 0.2 ? 0.06f : 0f;
                Part("Drawer", c, new Vector3(0, y, -0.312f - pull), new Vector3(0.46f, 0.3f, 0.012f + pull * 2f), "Galvanized");
                Part("Pull", c, new Vector3(0, y + 0.04f, -0.33f - pull), new Vector3(0.1f, 0.02f, 0.02f), "Metal");
                Part("Label", c, new Vector3(0, y - 0.06f, -0.325f - pull), new Vector3(0.08f, 0.04f, 0.004f), "Writing");
            }
            if (rng.NextDouble() < 0.5)
                Part("Carton", c, new Vector3(0, FloorY + 1.585f, 0), new Vector3(0.38f, 0.27f, 0.3f), "Carton").transform.localRotation = Quaternion.Euler(0, Lerp(rng, -10f, 10f), 0);
        }

        /// <summary>
        /// The evidence cage: a run of bars down both sides of a column of cells, a padlocked gate, and inside it a
        /// table of tagged property. Sightlines cross it; feet don't.
        /// </summary>
        private static void EvidenceCage(Transform root, float x, float floor, float top, float z0, float z1, Random rng)
        {
            float h = top - floor, zc = (z0 + z1) * 0.5f, len = z1 - z0;
            foreach (var side in new[] { -0.5f, 0.5f })
            {
                var wall = Piece("Cage Side", root, new Vector3(x + side, floor, zc), 0f, new Vector3(0.06f, h, len));
                for (float z = -len * 0.5f; z <= len * 0.5f + 0.01f; z += Cell)
                    Part("Post", wall, new Vector3(0, h * 0.5f, z), new Vector3(0.06f, h, 0.06f), "Metal");
                foreach (var y in new[] { 0.08f, h * 0.5f, h - 0.08f })
                    Part("Rail", wall, new Vector3(0, y, 0), new Vector3(0.04f, 0.04f, len), "Metal");
                for (float z = -len * 0.5f + 0.1f; z < len * 0.5f - 0.05f; z += 0.11f)
                    Part("Bar", wall, new Vector3(0, h * 0.5f, z), new Vector3(0.02f, h - 0.1f, 0.02f), "Metal");
            }
            // The gate on the west side, hasp and padlock, and the sign.
            Part("Gate Frame", root, new Vector3(x - 0.52f, floor + 1.05f, zc), new Vector3(0.03f, 2.0f, 0.9f), "Rust");
            Part("Gate Frame Inner", root, new Vector3(x - 0.53f, floor + 1.05f, zc), new Vector3(0.012f, 1.9f, 0.8f), "Metal");
            Part("Hasp", root, new Vector3(x - 0.55f, floor + 1.1f, zc + 0.4f), new Vector3(0.02f, 0.06f, 0.12f), "Metal");
            Part("Padlock", root, new Vector3(x - 0.57f, floor + 1.02f, zc + 0.42f), new Vector3(0.03f, 0.1f, 0.07f), "Brass");
            Part("Cage Sign", root, new Vector3(x - 0.545f, floor + 2.25f, zc), new Vector3(0.008f, 0.14f, 0.5f), "SignWhite");
            Part("Cage Sign Text", root, new Vector3(x - 0.55f, floor + 2.25f, zc), new Vector3(0.004f, 0.06f, 0.4f), "Writing");
            // Inside: a table of bagged and tagged things nobody's come back for.
            var table = Table("Property Table", root, new Vector3(x, floor - FloorY, zc), 90f, 2.6f, 0.6f, "Wood", 0.78f);
            string[] bags = { "CartonDark", "Newsprint", "Fabric", "Galvanized", "Leather" };
            for (int b = 0; b < 7; b++)
            {
                float bx = -1.1f + b * 0.36f;
                var bag = Part("Evidence Bag", table, new Vector3(bx, FloorY + 0.81f, Lerp(rng, -0.15f, 0.15f)), new Vector3(0.26f, 0.06f + (float)rng.NextDouble() * 0.1f, 0.2f), bags[b % bags.Length]);
                bag.transform.localRotation = Quaternion.Euler(0, Lerp(rng, -20f, 20f), 0);
                Part("Tag", bag.transform, new Vector3(0.1f, 0.5f, 0.45f), new Vector3(0.25f, 0.02f, 0.18f), "Writing");
            }
            Round("Rolled Rug", table, new Vector3(0.6f, FloorY + 0.93f, 0), 0.22f, 1.1f, "Rug").transform.localRotation = Quaternion.Euler(0, 0, 90f);
            Part("Typewriter", table, new Vector3(-1.1f, FloorY + 0.9f, 0.05f), new Vector3(0.36f, 0.2f, 0.34f), "HideBlack");
            Part("Bicycle Wheel", root, new Vector3(x + 0.25f, floor + 0.34f, z0 + 0.5f), new Vector3(0.03f, 0.68f, 0.68f), "Metal");
        }

        /// <summary>Paw prints in the dust between the cells given, every half metre, left then right.</summary>
        private static void PawTrail(Transform root, float floor, (int, int)[] cells, Random rng)
        {
            var trail = Group("Paw Prints", root);
            bool left = false;
            for (int c = 0; c + 1 < cells.Length; c++)
            {
                var a = ArchiveCell(cells[c].Item1, cells[c].Item2, floor);
                var b = ArchiveCell(cells[c + 1].Item1, cells[c + 1].Item2, floor);
                var dir = (b - a).normalized;
                var side = Vector3.Cross(Vector3.up, dir);
                float yaw = Quaternion.LookRotation(dir).eulerAngles.y;
                for (float d = 0.3f; d < (b - a).magnitude; d += 0.5f)
                {
                    left = !left;
                    var p = a + dir * d + side * (left ? -0.09f : 0.09f) + new Vector3(Lerp(rng, -0.03f, 0.03f), 0.004f, Lerp(rng, -0.03f, 0.03f));
                    var print = Group("Print", trail);
                    print.localPosition = p;
                    print.localRotation = Quaternion.Euler(0, yaw + Lerp(rng, -14f, 14f), 0);
                    Part("Pad", print, Vector3.zero, new Vector3(0.05f, 0.004f, 0.055f), "ConcreteDark");
                    foreach (var tx in new[] { -0.025f, 0f, 0.025f })
                        Part("Toe", print, new Vector3(tx, 0, 0.045f), new Vector3(0.016f, 0.004f, 0.02f), "ConcreteDark");
                }
            }
        }

        /// <summary>Archive cartons stacked across an aisle, four and five high, with one slab of collider behind them.</summary>
        private static void CartonWall(Transform root, Vector3 pos, float width, float yaw, Random rng)
        {
            var wall = Group("Carton Wall", root);
            wall.localPosition = pos;
            wall.localRotation = Quaternion.Euler(0, yaw, 0);
            var col = wall.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.75f, 0);
            col.size = new Vector3(width, 1.5f, 0.36f);
            int columns = Mathf.Max(1, Mathf.RoundToInt(width / 0.42f));
            float step = width / columns;
            int n = 0;
            for (int c = 0; c < columns; c++)
            {
                int height = 4 + (c % 2 == 0 ? 1 : 0);
                for (int i = 0; i < height; i++)
                {
                    var carton = Part("Carton", wall, new Vector3(-width * 0.5f + step * (c + 0.5f), 0.135f + i * 0.272f, 0), new Vector3(Mathf.Min(0.4f, step - 0.02f), 0.27f, 0.32f), n++ % 4 == 1 ? "CartonDark" : "Carton");
                    carton.transform.localRotation = Quaternion.Euler(0, Lerp(rng, -4f, 4f), 0);
                    Part("Carton Label", carton.transform, new Vector3(0, 0.03f, 0.163f), new Vector3(0.16f, 0.06f, 0.004f), "Writing");
                    Part("Carton Label", carton.transform, new Vector3(0, 0.03f, -0.163f), new Vector3(0.16f, 0.06f, 0.004f), "Writing");
                }
            }
        }

        // ------------------------------------------------------------ the boiler room

        private static (TutorialBreaker, World.Door, Renderer) BuildBoilerRoom(Transform boiler, Random rng)
        {
            const float floor = -3.5f, top = -0.9f, bottom = -3.6f;
            float pieceY = floor - FloorY; // furniture builders put their parts FloorY above the piece
            string wall = "Concrete";
            Box("Floor", boiler, new Vector3(13f, floor - 0.05f, -5f), new Vector3(6.4f, 0.1f, 6.4f), "Concrete");
            Box("Ceiling", boiler, new Vector3(13f, top + 0.06f, -5f), new Vector3(6.4f, 0.12f, 6.4f), "ConcreteDark");
            WallX(boiler, "Wall East", 16.1f, -8.1f, -1.9f, bottom, top, wall);
            WallZ(boiler, "Wall South", -8.1f, 10.0f, 16.2f, bottom, top, wall);
            WallZ(boiler, "Wall North", -2.1f, 10.0f, 11.55f, bottom, top, wall);
            WallZ(boiler, "Wall North", -2.1f, 12.45f, 16.2f, bottom, top, wall);
            WallZ(boiler, "Lintel", -2.1f, 11.55f, 12.45f, -1.4f, top, wall);
            var closetDoor = TutorialDoor(boiler, "Closet Door", new Vector3(11.55f, floor, -2.1f), 0f, true,
                "Held shut from the other side. Something behind it is breathing, and trying not to.", style: DoorStyle.Plank);

            // ---- The closet behind it: Teo's, and Biscuit's. A sink, the mops, her blanket and bowls, his stool.
            Box("Closet Floor", boiler, new Vector3(12f, floor - 0.05f, -0.85f), new Vector3(2.4f, 0.1f, 2.5f), "Concrete");
            Box("Closet Ceiling", boiler, new Vector3(12f, top + 0.06f, -0.85f), new Vector3(2.4f, 0.12f, 2.5f), "ConcreteDark");
            WallX(boiler, "Closet Wall", 10.9f, -2.1f, 0.4f, bottom, top, wall);
            WallX(boiler, "Closet Wall", 13.1f, -2.1f, 0.4f, bottom, top, wall);
            WallZ(boiler, "Closet Wall", 0.4f, 10.8f, 13.2f, bottom, top, wall);
            var shelf = Piece("Closet Shelf", boiler, new Vector3(12.6f, pieceY, 0.15f), 0f, new Vector3(0.9f, 1.8f, 0.3f), FloorY);
            foreach (var x in new[] { -0.43f, 0.43f })
                Part("Upright", shelf, new Vector3(x, FloorY + 0.9f, 0), new Vector3(0.04f, 1.8f, 0.3f), "Metal");
            foreach (var y in new[] { 0.5f, 1.0f, 1.5f })
                Part("Board", shelf, new Vector3(0, FloorY + y, 0), new Vector3(0.9f, 0.03f, 0.3f), "Wood");
            foreach (var (x, y, mat, d, h) in new[] { (-0.3f, 0.5f, "Mustard", 0.12f, 0.2f), (-0.06f, 0.5f, "Teal", 0.12f, 0.2f), (0.25f, 0.5f, "BarnRed", 0.1f, 0.12f),
                (-0.3f, 1.0f, "SignWhite", 0.12f, 0.25f), (-0.13f, 1.0f, "SignWhite", 0.12f, 0.25f), (0.22f, 1.0f, "Teal", 0.14f, 0.28f),
                (-0.25f, 1.5f, "Galvanized", 0.16f, 0.14f), (0.18f, 1.5f, "BarnRed", 0.1f, 0.12f) })
                Round("Tin", shelf, new Vector3(x, FloorY + y + 0.015f + h * 0.5f, 0), d, h, mat);
            Part("Box of Rags", shelf, new Vector3(0.05f, FloorY + 1.6f, 0.02f), new Vector3(0.24f, 0.16f, 0.22f), "Carton");
            Part("Kibble Sack", boiler, new Vector3(12.9f, floor + 0.26f, -0.15f), new Vector3(0.3f, 0.52f, 0.16f), "Sack").transform.localRotation = Quaternion.Euler(-8f, 0, 0);
            Part("Sack Label", boiler, new Vector3(12.9f, floor + 0.3f, -0.235f), new Vector3(0.18f, 0.12f, 0.004f), "Writing").transform.localRotation = Quaternion.Euler(-8f, 0, 0);
            Part("Dog Blanket", boiler, new Vector3(11.3f, floor + 0.04f, -0.75f), new Vector3(0.8f, 0.08f, 0.7f), "Rug");
            Part("Blanket Fold", boiler, new Vector3(11.15f, floor + 0.09f, -0.6f), new Vector3(0.35f, 0.03f, 0.3f), "Rug");
            Part("Chew Toy", boiler, new Vector3(11.6f, floor + 0.03f, -0.42f), new Vector3(0.14f, 0.05f, 0.05f), "PlainCream");
            Round("Water Bowl", boiler, new Vector3(12.6f, floor + 0.03f, -1.6f), 0.22f, 0.06f, "Galvanized");
            Round("Food Bowl", boiler, new Vector3(12.9f, floor + 0.03f, -1.6f), 0.22f, 0.06f, "Galvanized");
            Round("Kibble", boiler, new Vector3(12.9f, floor + 0.065f, -1.6f), 0.14f, 0.01f, "Mustard");
            var sink = Piece("Utility Sink", boiler, new Vector3(11.4f, pieceY, 0.12f), 0f, new Vector3(0.55f, 0.9f, 0.5f), FloorY);
            Part("Basin", sink, new Vector3(0, FloorY + 0.72f, 0), new Vector3(0.55f, 0.3f, 0.5f), "Galvanized");
            Part("Basin Well", sink, new Vector3(0, FloorY + 0.875f, 0), new Vector3(0.47f, 0.02f, 0.42f), "HideBlack");
            foreach (var x in new[] { -0.22f, 0.22f })
                Part("Leg", sink, new Vector3(x, FloorY + 0.28f, -0.18f), new Vector3(0.04f, 0.57f, 0.04f), "Metal");
            Part("Tap Riser", sink, new Vector3(0, FloorY + 1.05f, 0.22f), new Vector3(0.03f, 0.3f, 0.03f), "Brass");
            Part("Tap Spout", sink, new Vector3(0, FloorY + 1.18f, 0.12f), new Vector3(0.03f, 0.03f, 0.2f), "Brass");
            Part("Soap", sink, new Vector3(0.18f, FloorY + 0.9f, 0.2f), new Vector3(0.08f, 0.03f, 0.05f), "PlainCream");
            foreach (var (z, head) in new[] { (-1.55f, "Mustard"), (-1.25f, "Fabric"), (-0.95f, "Mustard") })
            {
                Round("Broom Handle", boiler, new Vector3(11.07f, floor + 0.72f, z), 0.03f, 1.4f, "Wood").transform.localRotation = Quaternion.Euler(0, 0, -4f);
                Part(head == "Fabric" ? "Mop Head" : "Broom Head", boiler, new Vector3(11.1f, floor + 0.05f, z), head == "Fabric" ? new Vector3(0.14f, 0.1f, 0.14f) : new Vector3(0.08f, 0.1f, 0.26f), head);
            }
            var wringer = Piece("Wringer Bucket", boiler, new Vector3(11.1f, pieceY, -1.75f), 0f, new Vector3(0.36f, 0.5f, 0.5f), FloorY);
            Part("Bucket", wringer, new Vector3(0, FloorY + 0.2f, 0), new Vector3(0.34f, 0.4f, 0.48f), "Mustard");
            Part("Wringer", wringer, new Vector3(0, FloorY + 0.47f, -0.12f), new Vector3(0.3f, 0.14f, 0.2f), "Metal");
            Part("Wringer Handle", wringer, new Vector3(0.17f, FloorY + 0.6f, -0.12f), new Vector3(0.03f, 0.25f, 0.03f), "Metal");
            var stool = Piece("Stool", boiler, new Vector3(12.75f, pieceY, -1.05f), 20f, new Vector3(0.32f, 0.5f, 0.32f), FloorY);
            Round("Seat", stool, new Vector3(0, FloorY + 0.48f, 0), 0.3f, 0.04f, "Wood");
            foreach (var a in new[] { 0f, 120f, 240f })
                Part("Leg", stool, Quaternion.Euler(0, a, 0) * new Vector3(0.1f, 0, 0) + new Vector3(0, FloorY + 0.23f, 0), new Vector3(0.03f, 0.46f, 0.03f), "Wood");
            Part("Cap", stool, new Vector3(0.02f, FloorY + 0.53f, 0.02f), new Vector3(0.2f, 0.06f, 0.2f), "HideBlack");
            Part("Cap Peak", stool, new Vector3(0.02f, FloorY + 0.515f, 0.14f), new Vector3(0.16f, 0.015f, 0.08f), "HideBlack");
            Part("Hook", boiler, new Vector3(12.98f, floor + 1.9f, -1.5f), new Vector3(0.04f, 0.04f, 0.08f), "Brass");
            Part("Slicker", boiler, new Vector3(12.93f, floor + 1.45f, -1.5f), new Vector3(0.1f, 0.85f, 0.42f), "Mustard");
            Part("Slicker Hood", boiler, new Vector3(12.93f, floor + 1.92f, -1.5f), new Vector3(0.1f, 0.14f, 0.22f), "Mustard");
            Part("Hook", boiler, new Vector3(11.02f, floor + 1.8f, -0.3f), new Vector3(0.04f, 0.04f, 0.08f), "Brass");
            Part("Leash", boiler, new Vector3(11.04f, floor + 1.45f, -0.3f), new Vector3(0.02f, 0.7f, 0.03f), "Leather");
            Part("Leash Loop", boiler, new Vector3(11.04f, floor + 1.1f, -0.3f), new Vector3(0.02f, 0.03f, 0.1f), "Leather");
            Part("Lost Property", boiler, new Vector3(12.6f, floor + 0.15f, -0.55f), new Vector3(0.5f, 0.3f, 0.36f), "Carton");
            Part("Box Label", boiler, new Vector3(12.6f, floor + 0.17f, -0.735f), new Vector3(0.22f, 0.07f, 0.004f), "Writing");
            Part("Pull Chain", boiler, new Vector3(12.1f, top - 0.65f, -0.9f), new Vector3(0.004f, 0.6f, 0.004f), "Metal");
            CagedBulb(boiler, new Vector3(12f, top - 0.25f, -0.9f), 4f, 0.8f);

            // ---- The furnace against the north wall: a riveted tank with a hatch and a slit of fire, the flue and
            // the duct it feeds across the ceiling, a feed pipe to the wall, gauges, a valve wheel, and the yellow
            // keep-clear line painted round it that nobody keeps.
            var furnace = Piece("Furnace", boiler, new Vector3(14.6f, pieceY, -3.1f), 0f, new Vector3(2.0f, 1.9f, 1.5f), FloorY);
            Part("Tank", furnace, new Vector3(0, FloorY + 0.95f, 0), new Vector3(2.0f, 1.9f, 1.5f), "Galvanized");
            foreach (var y in new[] { 0.4f, 1.2f })
                Part("Band", furnace, new Vector3(0, FloorY + y, 0), new Vector3(2.03f, 0.06f, 1.53f), "Rust");
            Part("Hatch", furnace, new Vector3(0, FloorY + 0.75f, -0.76f), new Vector3(0.7f, 0.6f, 0.04f), "Metal");
            Part("Hatch Hinge", furnace, new Vector3(-0.33f, FloorY + 0.75f, -0.78f), new Vector3(0.03f, 0.5f, 0.02f), "Rust");
            Part("Latch", furnace, new Vector3(0.3f, FloorY + 0.75f, -0.79f), new Vector3(0.05f, 0.14f, 0.03f), "Rust");
            var glow = Part("Fire Slit", furnace, new Vector3(0, FloorY + 0.52f, -0.78f), new Vector3(0.5f, 0.06f, 0.02f), "Bulb");
            Part("Ash Door", furnace, new Vector3(0, FloorY + 0.2f, -0.76f), new Vector3(0.5f, 0.22f, 0.03f), "Metal");
            Round("Flue", furnace, new Vector3(0.4f, FloorY + 2.3f, 0), 0.3f, 0.8f, "Rust");
            Part("Feed Pipe", furnace, new Vector3(-0.6f, FloorY + 1.6f, 0.5f), new Vector3(0.1f, 0.1f, 1.0f), "Rust");
            Part("Gauge", furnace, new Vector3(-0.5f, FloorY + 1.3f, -0.77f), new Vector3(0.14f, 0.14f, 0.03f), "SignWhite");
            Part("Gauge Rim", furnace, new Vector3(-0.5f, FloorY + 1.3f, -0.78f), new Vector3(0.16f, 0.16f, 0.01f), "Brass");
            Part("Gauge Needle", furnace, new Vector3(-0.5f, FloorY + 1.3f, -0.787f), new Vector3(0.06f, 0.008f, 0.004f), "BarnRed").transform.localRotation = Quaternion.Euler(0, 0, 40f);
            Part("Gauge", furnace, new Vector3(0.55f, FloorY + 1.3f, -0.77f), new Vector3(0.12f, 0.12f, 0.03f), "SignWhite");
            Part("Gauge Rim", furnace, new Vector3(0.55f, FloorY + 1.3f, -0.78f), new Vector3(0.14f, 0.14f, 0.01f), "Brass");
            Round("Valve Wheel", furnace, new Vector3(-0.85f, FloorY + 1.0f, -0.78f), 0.16f, 0.03f, "BarnRed").transform.localRotation = Quaternion.Euler(90f, 0, 0);
            SoundEmitter(furnace, new Vector3(0, 1f, 0), "drone_deep_loop", 0.08f, 7f);
            Box("Duct Elbow", boiler, new Vector3(15.0f, top - 0.2f, -3.1f), new Vector3(0.42f, 0.34f, 0.42f), "Galvanized", collider: false);
            Box("Duct", boiler, new Vector3(15.0f, top - 0.2f, -5.0f), new Vector3(0.36f, 0.3f, 3.6f), "Galvanized", collider: false);
            Box("Duct Seam", boiler, new Vector3(15.0f, top - 0.2f, -4.3f), new Vector3(0.4f, 0.34f, 0.04f), "Metal", collider: false);
            Box("Duct Seam", boiler, new Vector3(15.0f, top - 0.2f, -5.9f), new Vector3(0.4f, 0.34f, 0.04f), "Metal", collider: false);
            foreach (var (a, b) in new[] { ((13.3f, -4.25f), (13.3f, -2.1f)), ((13.3f, -4.25f), (15.9f, -4.25f)), ((15.9f, -4.25f), (15.9f, -2.1f)) })
                Part("Keep Clear", boiler, new Vector3((a.Item1 + b.Item1) * 0.5f, floor + 0.004f, (a.Item2 + b.Item2) * 0.5f),
                    new Vector3(Mathf.Max(0.06f, Mathf.Abs(b.Item1 - a.Item1)), 0.004f, Mathf.Max(0.06f, Mathf.Abs(b.Item2 - a.Item2))), "RoadLine");

            // ---- The breaker panel on the east wall, conduit up from it; the big red switch is the one that matters.
            var panel = Box("Breaker Panel", boiler, new Vector3(15.92f, -2.2f, -6.0f), new Vector3(0.14f, 0.75f, 0.5f), "Galvanized");
            Box("Panel Door Edge", boiler, new Vector3(15.84f, -2.2f, -6.0f), new Vector3(0.02f, 0.7f, 0.46f), "Metal", collider: false);
            for (int i = 0; i < 6; i++)
                Box("Switch", boiler, new Vector3(15.82f, -2.0f - i * 0.09f, -6.1f), new Vector3(0.02f, 0.05f, 0.03f), "HideBlack", collider: false);
            Box("Main Switch", boiler, new Vector3(15.8f, -2.42f, -5.88f), new Vector3(0.06f, 0.2f, 0.07f), "BarnRed", collider: false);
            Box("Panel Label", boiler, new Vector3(15.84f, -1.92f, -5.86f), new Vector3(0.004f, 0.06f, 0.14f), "Writing", collider: false);
            Round("Conduit", boiler, new Vector3(15.95f, -1.4f, -6.0f), 0.04f, 0.9f, "Galvanized");
            Round("Conduit", boiler, new Vector3(15.95f, -1.4f, -5.85f), 0.03f, 0.9f, "Galvanized");
            var breaker = panel.AddComponent<TutorialBreaker>();

            // Pipes along the east wall at head height, valve wheels on the drops, the water heater they feed.
            Round("Pipe", boiler, new Vector3(15.95f, floor + 2.05f, -5.1f), 0.09f, 5.8f, "Rust").transform.localRotation = Quaternion.Euler(90f, 0, 0);
            foreach (var z in new[] { -3.5f, -7.0f })
            {
                Round("Pipe", boiler, new Vector3(15.95f, floor + 1.05f, z), 0.07f, 2.0f, "Rust");
                Round("Valve Wheel", boiler, new Vector3(15.82f, floor + 1.55f, z), 0.16f, 0.03f, "BarnRed").transform.localRotation = Quaternion.Euler(0, 0, 90f);
            }
            foreach (var z in new[] { -7.6f, -5.0f, -2.5f })
                Part("Pipe Bracket", boiler, new Vector3(15.99f, floor + 2.05f, z), new Vector3(0.06f, 0.14f, 0.06f), "Metal");
            var heater = Piece("Water Heater", boiler, new Vector3(15.4f, pieceY, -7.35f), 0f, new Vector3(0.7f, 1.8f, 0.7f), FloorY);
            Round("Tank", heater, new Vector3(0, FloorY + 0.9f, 0), 0.64f, 1.7f, "Galvanized");
            Round("Cap", heater, new Vector3(0, FloorY + 1.78f, 0), 0.5f, 0.1f, "Metal");
            Round("Stand", heater, new Vector3(0, FloorY + 0.03f, 0), 0.6f, 0.06f, "HideBlack");
            foreach (var x in new[] { -0.15f, 0.15f })
                Round("Pipe", heater, new Vector3(x, FloorY + 2.2f, 0), 0.06f, 0.9f, "Rust");
            Part("Plate", heater, new Vector3(0, FloorY + 1.1f, -0.325f), new Vector3(0.2f, 0.12f, 0.004f), "Writing");
            Part("Pilot Door", heater, new Vector3(0, FloorY + 0.3f, -0.32f), new Vector3(0.18f, 0.14f, 0.01f), "Metal");

            // ---- Teo's bench against the south wall: vice, oil can, a coffee can of screws, his radio and thermos,
            // the pegboard above with the outlines of tools that aren't on it.
            var bench = Piece("Workbench", boiler, new Vector3(12.9f, pieceY, -7.72f), 0f, new Vector3(1.8f, 0.9f, 0.6f), FloorY);
            Part("Top", bench, new Vector3(0, FloorY + 0.875f, 0), new Vector3(1.8f, 0.05f, 0.6f), "Wood");
            Part("Shelf", bench, new Vector3(0, FloorY + 0.2f, 0.05f), new Vector3(1.7f, 0.03f, 0.5f), "Wood");
            foreach (var x in new[] { -0.82f, 0.82f })
                foreach (var z in new[] { -0.25f, 0.25f })
                    Part("Leg", bench, new Vector3(x, FloorY + 0.43f, z), new Vector3(0.06f, 0.86f, 0.06f), "Wood");
            Part("Vice Jaw", bench, new Vector3(-0.6f, FloorY + 0.98f, -0.22f), new Vector3(0.16f, 0.14f, 0.1f), "Metal");
            Part("Vice Jaw", bench, new Vector3(-0.6f, FloorY + 0.98f, -0.08f), new Vector3(0.16f, 0.14f, 0.1f), "Metal");
            Part("Vice Screw", bench, new Vector3(-0.6f, FloorY + 0.98f, -0.32f), new Vector3(0.03f, 0.03f, 0.14f), "Brass");
            Part("Vice Handle", bench, new Vector3(-0.6f, FloorY + 0.98f, -0.38f), new Vector3(0.2f, 0.02f, 0.02f), "Metal");
            Round("Oil Can", bench, new Vector3(-0.25f, FloorY + 0.98f, 0.1f), 0.12f, 0.16f, "Galvanized");
            Part("Oil Spout", bench, new Vector3(-0.19f, FloorY + 1.1f, 0.14f), new Vector3(0.12f, 0.02f, 0.02f), "Galvanized").transform.localRotation = Quaternion.Euler(0, 0, 35f);
            Round("Coffee Can", bench, new Vector3(0.05f, FloorY + 0.975f, 0.15f), 0.14f, 0.15f, "BarnRed");
            Part("Can Label", bench, new Vector3(0.05f, FloorY + 0.975f, 0.078f), new Vector3(0.09f, 0.07f, 0.004f), "SignWhite");
            Part("Radio", bench, new Vector3(0.45f, FloorY + 0.985f, 0.12f), new Vector3(0.34f, 0.17f, 0.14f), "Leather");
            Part("Radio Grille", bench, new Vector3(0.38f, FloorY + 0.985f, 0.048f), new Vector3(0.16f, 0.12f, 0.004f), "Mustard");
            Part("Radio Dial", bench, new Vector3(0.56f, FloorY + 0.985f, 0.047f), new Vector3(0.07f, 0.07f, 0.004f), "SignWhite");
            Part("Radio Aerial", bench, new Vector3(0.58f, FloorY + 1.25f, 0.12f), new Vector3(0.008f, 0.36f, 0.008f), "Metal").transform.localRotation = Quaternion.Euler(0, 0, -20f);
            Round("Thermos", bench, new Vector3(0.78f, FloorY + 1.05f, -0.12f), 0.1f, 0.3f, "Teal");
            Round("Thermos Cup", bench, new Vector3(0.78f, FloorY + 1.23f, -0.12f), 0.09f, 0.06f, "SignWhite");
            Part("Hammer Handle", bench, new Vector3(0.25f, FloorY + 0.915f, -0.15f), new Vector3(0.28f, 0.025f, 0.025f), "Wood").transform.localRotation = Quaternion.Euler(0, 20f, 0);
            Part("Hammer Head", bench, new Vector3(0.4f, FloorY + 0.92f, -0.2f), new Vector3(0.05f, 0.04f, 0.1f), "Metal").transform.localRotation = Quaternion.Euler(0, 20f, 0);
            Part("Rag", bench, new Vector3(-0.05f, FloorY + 0.91f, -0.22f), new Vector3(0.26f, 0.02f, 0.2f), "Fabric");
            Part("Newspaper", bench, new Vector3(0.15f, FloorY + 0.23f, 0.05f), new Vector3(0.32f, 0.03f, 0.24f), "Newsprint");
            Part("Paint Tin", bench, new Vector3(-0.6f, FloorY + 0.31f, 0.05f), new Vector3(0.2f, 0.2f, 0.2f), "Galvanized");
            ToolBoard(boiler, new Vector3(12.9f, floor, -8.0f));
            CagedBulb(boiler, new Vector3(12.9f, top - 0.25f, -7.3f), 4f, 0.7f);

            // ---- His chair by the furnace, the paper he was reading, the lunch pail; the drain in the middle of the
            // floor and the water that never quite reaches it; the ladder, the salt for the steps.
            Chair(boiler, new Vector3(12.95f, pieceY, -2.85f), 110f, "Wood");
            Part("Newspaper", boiler, new Vector3(12.95f, floor + 0.49f, -2.85f), new Vector3(0.3f, 0.02f, 0.22f), "Newsprint").transform.localRotation = Quaternion.Euler(0, 20f, 0);
            Part("Lunch Pail", boiler, new Vector3(12.55f, floor + 0.1f, -3.15f), new Vector3(0.3f, 0.2f, 0.16f), "HideBlack");
            Part("Pail Handle", boiler, new Vector3(12.55f, floor + 0.22f, -3.15f), new Vector3(0.2f, 0.02f, 0.02f), "Metal");
            Round("Floor Drain", boiler, new Vector3(13.9f, floor + 0.004f, -6.3f), 0.32f, 0.008f, "HideBlack");
            foreach (var dz in new[] { -0.08f, 0f, 0.08f })
                Part("Grate Bar", boiler, new Vector3(13.9f, floor + 0.009f, -6.3f + dz), new Vector3(0.3f, 0.004f, 0.02f), "Metal");
            Round("Puddle", boiler, new Vector3(13.7f, floor + 0.003f, -6.1f), 1.3f, 0.004f, "ConcreteDark");
            MopBucket(boiler, new Vector3(15.55f, floor, -4.9f), new Vector3(0.33f, 0, 0.05f)); // handle resting on the east wall
            var ladder = Group("Ladder", boiler);
            ladder.localPosition = new Vector3(10.6f, floor, -7.4f);
            ladder.localRotation = Quaternion.Euler(-14f, 0, 0);
            foreach (var x in new[] { -0.2f, 0.2f })
                Part("Rail", ladder, new Vector3(x, 1.2f, 0), new Vector3(0.04f, 2.4f, 0.05f), "Wood");
            for (int r = 0; r < 7; r++)
                Part("Rung", ladder, new Vector3(0, 0.25f + r * 0.32f, 0), new Vector3(0.44f, 0.03f, 0.03f), "Wood");
            for (int i = 0; i < 3; i++)
                Part("Salt Bag", boiler, new Vector3(10.75f + (i == 2 ? 0.08f : 0f), floor + 0.1f + i * 0.19f, -6.75f), new Vector3(0.5f, 0.2f, 0.34f), i == 1 ? "CartonDark" : "Sack").transform.localRotation = Quaternion.Euler(0, (i - 1) * 9f, 0);
            Part("Bag Print", boiler, new Vector3(10.83f, floor + 0.49f, -6.575f), new Vector3(0.26f, 0.09f, 0.004f), "Writing").transform.localRotation = Quaternion.Euler(0, 9f, 0);

            // ---- On the walls: the extinguisher inside the door, the coat hooks with Teo's jacket and scarf, the
            // calendar he's a month behind on, and NO SMOKING over the thing that's on fire.
            Part("Extinguisher Bracket", boiler, new Vector3(10.22f, floor + 1.25f, -3.9f), new Vector3(0.04f, 0.1f, 0.14f), "Metal");
            Round("Extinguisher", boiler, new Vector3(10.32f, floor + 1.05f, -3.9f), 0.16f, 0.5f, "BarnRed");
            Round("Extinguisher Neck", boiler, new Vector3(10.32f, floor + 1.34f, -3.9f), 0.06f, 0.08f, "Brass");
            Part("Extinguisher Hose", boiler, new Vector3(10.32f, floor + 1.0f, -3.8f), new Vector3(0.02f, 0.4f, 0.02f), "HideBlack");
            Part("Fire Placard", boiler, new Vector3(10.21f, floor + 1.75f, -3.9f), new Vector3(0.004f, 0.2f, 0.16f), "SignWhite");
            Part("Fire Placard Band", boiler, new Vector3(10.213f, floor + 1.8f, -3.9f), new Vector3(0.004f, 0.05f, 0.14f), "BarnRed");
            Part("Hook Rail", boiler, new Vector3(10.85f, floor + 1.85f, -2.215f), new Vector3(0.6f, 0.06f, 0.03f), "Wood");
            foreach (var x in new[] { 10.65f, 10.85f, 11.05f })
                Part("Hook", boiler, new Vector3(x, floor + 1.83f, -2.25f), new Vector3(0.03f, 0.03f, 0.05f), "Brass");
            Part("Jacket", boiler, new Vector3(10.65f, floor + 1.42f, -2.29f), new Vector3(0.38f, 0.78f, 0.12f), "Suit");
            Part("Jacket Collar", boiler, new Vector3(10.65f, floor + 1.8f, -2.3f), new Vector3(0.2f, 0.08f, 0.14f), "Suit");
            Part("Scarf", boiler, new Vector3(11.05f, floor + 1.5f, -2.26f), new Vector3(0.1f, 0.6f, 0.06f), "BarnRed");
            Part("Calendar", boiler, new Vector3(12.95f, floor + 1.7f, -2.205f), new Vector3(0.3f, 0.42f, 0.01f), "Newsprint");
            Part("Calendar Picture", boiler, new Vector3(12.95f, floor + 1.81f, -2.212f), new Vector3(0.26f, 0.16f, 0.004f), "Teal");
            for (int r = 0; r < 4; r++)
                Part("Calendar Grid", boiler, new Vector3(12.95f, floor + 1.64f - r * 0.05f, -2.212f), new Vector3(0.24f, 0.03f, 0.004f), "Writing");
            Part("Calendar Nail", boiler, new Vector3(12.95f, floor + 1.93f, -2.215f), new Vector3(0.01f, 0.01f, 0.02f), "Metal");
            Part("No Smoking", boiler, new Vector3(15.3f, floor + 2.15f, -2.205f), new Vector3(0.44f, 0.16f, 0.01f), "SignWhite");
            Part("No Smoking Band", boiler, new Vector3(15.3f, floor + 2.15f, -2.212f), new Vector3(0.4f, 0.05f, 0.004f), "BarnRed");
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
            foreach (var z in new[] { -3.95f, 0f, 4.1f })
            {
                Box("Upper Window", exterior, new Vector3(8.3f, 4.5f, z), new Vector3(0.04f, 1.3f, 1.1f), "WindowDark", collider: false);
                Box("Upper Sill", exterior, new Vector3(8.36f, 3.82f, z), new Vector3(0.2f, 0.07f, 1.3f), "Stone", collider: false);
            }
            // The brick face, built as strips so the three windows are open through it.
            Box("Facade Low", exterior, new Vector3(8.26f, 0.475f, 0f), new Vector3(0.08f, 0.95f, 11.8f), "Brick", collider: false);
            Box("Facade High", exterior, new Vector3(8.26f, 2.875f, 0f), new Vector3(0.08f, 0.85f, 11.8f), "Brick", collider: false);
            foreach (var p in new[] { (-5.9f, -4.6f), (-3.3f, -0.65f), (0.65f, 3.45f), (4.75f, 5.9f) })
                Box("Facade Pier", exterior, new Vector3(8.26f, 1.7f, (p.Item1 + p.Item2) * 0.5f), new Vector3(0.08f, 1.5f, p.Item2 - p.Item1), "Brick", collider: false);
            Box("Side Wall", exterior, new Vector3(0.2f, 2.9f, 5.72f), new Vector3(16.2f, 5.8f, 0.1f), "Brick", collider: false);
            Box("Side Wall", exterior, new Vector3(0.2f, 2.9f, -5.72f), new Vector3(16.2f, 5.8f, 0.1f), "Brick", collider: false);
            Box("Roof", exterior, new Vector3(0.2f, 5.85f, 0f), new Vector3(16.4f, 0.2f, 12f), "Roof", collider: false);

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
            main.startLifetime = 0.9f; // dies at the ground: nothing falls on through the floor into the basement
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
            emission.rateOverTime = 1800f;
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
            TutorialAnchor("CamEnd", new Vector3(9.8f, 1.9f, 0.6f), Vector3.left);
            TutorialAnchor("WindowLook", new Vector3(7.3f, 1.45f, 0.05f), Vector3.left);
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
            var touch = dog.AddComponent<BoxCollider>();
            touch.isTrigger = true;
            touch.center = new Vector3(0, 0.42f, 0.05f);
            touch.size = new Vector3(0.4f, 0.6f, 0.95f);
            var biscuit = dog.AddComponent<TutorialDog>();
            dog.AddComponent<DogPet>();
            return biscuit;
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
