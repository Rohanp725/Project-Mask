using System;
using System.Collections.Generic;
using Semblance.Interaction;
using Semblance.Player;
using Semblance.Rendering;
using Semblance.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.AI.Navigation;
using UnityEngine.AI;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Semblance.EditorTools
{
    /// <summary>
    /// Generates the greybox town of Semblance: a small lit crossroads (church, store, diner) and
    /// farms spread far apart along dark country roads, each with a lit gate and a long driveway.
    /// Menu: Semblance > Build Town. Re-running overwrites the scene and generated meshes.
    /// </summary>
    public static partial class TownBuilder
    {
        private const string RootDir = "Assets/_Project";
        private const string MaterialDir = RootDir + "/Materials";
        private const string MeshDir = RootDir + "/Meshes";
        private const string GeneratedMeshDir = MeshDir + "/Generated";
        private const string SceneDir = RootDir + "/Scenes";
        public const string ScenePath = SceneDir + "/Semblance_Town.unity";

        private const float RoadHalfWidth = 3.5f;
        private const float MainRoadHalfLength = 260f;   // north-south highway (asphalt)
        private const float CrossRoadHalfLength = 210f;  // east-west county road (dirt)
        private const float CenterHalfLength = 45f;      // lit town center along the highway
        private const float SidewalkOuterEdge = 6.5f;
        private const float BuildingSetback = 7.5f;
        private const float WorldHalfSize = 280f;

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly List<Rect> TreeExclusions = new List<Rect>();
        private static readonly List<Action> DeferredLamps = new List<Action>();
        private static readonly List<Vector3> ExtraTrees = new List<Vector3>();
        private static Shader litShader;
        private static Mesh cubeMesh, quadMesh, prismMesh, coneMesh, pyramidMesh, cylinderMesh, domeMesh, bellMesh;
        private static int generatedMeshCount;

        // ---------------------------------------------------------------- Definitions

        private struct BuildingDef
        {
            public string Name;
            public string Sign;
            public string SignMaterial;
            public Vector3 Size; // width (along front), height, depth
            public string Material;
            public bool Pitched;
            public int Doors;
            public Vector2 DoorSize;
            public float LitWindowChance;
            public string DoorPrompt;
            public string DoorDescription;
            public InteriorKind Interior;
            public bool Locked;
            public string LockedMessage;
            public string AnchorKey;  // residents are placed relative to anchored buildings
        }

        private enum InteriorKind { Empty, Diner, Store, Church, House, Motel, Barn }

        /// <summary>Building roots by key ("Diner", "House:Hollis", ...), used to place residents.</summary>
        public static readonly Dictionary<string, Transform> Anchors = new Dictionary<string, Transform>();

        private struct FarmDef
        {
            public string Family;
            public Vector3 GatePosition;
            public float Yaw;            // local +Z points from the gate up the driveway
            public float DriveLength;
            public float HouseOffsetX;
            public string HouseMaterial;
            public bool Barn;
            public bool Silo;
            public int CornSide;         // -1 left of the driveway, +1 right, 0 none
            public bool FlickeringGate;
            public string Mailbox;
            public bool LockedHouse;
            public string LockedMessage;
        }

        private static FarmDef OnHighway(float z, bool east) => new FarmDef
        {
            GatePosition = new Vector3((east ? 1 : -1) * (RoadHalfWidth + 2.5f), 0, z),
            Yaw = east ? 90f : -90f,
        };

        private static FarmDef OnCountyRoad(float x, bool north) => new FarmDef
        {
            GatePosition = new Vector3(x, 0, (north ? 1 : -1) * (RoadHalfWidth + 2.5f)),
            Yaw = north ? 0f : 180f,
        };

        // ---------------------------------------------------------------- Entry points

        [MenuItem("Semblance/Build Town")]
        private static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Build();
        }

        public static void Build()
        {
            Materials.Clear();
            TreeExclusions.Clear();
            DeferredLamps.Clear();
            ExtraTrees.Clear();
            Anchors.Clear();
            FarmFrames.Clear();
            CellarSites.Clear();
            generatedMeshCount = 0;
            BatchedMeshes.Clear();

            litShader = Shader.Find("Semblance/PS1 Lit");
            if (litShader == null)
                throw new InvalidOperationException("Shader 'Semblance/PS1 Lit' not found.");

            EnsureFolder(MaterialDir);
            EnsureFolder(MeshDir);
            EnsureFolder(SceneDir);
            if (AssetDatabase.IsValidFolder(GeneratedMeshDir))
                AssetDatabase.DeleteAsset(GeneratedMeshDir);
            EnsureFolder(GeneratedMeshDir);

            cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            quadMesh = GetOrCreateMesh("QuadUp", CreateQuadUp);
            prismMesh = GetOrCreateMesh("Prism", CreatePrism);
            coneMesh = GetOrCreateMesh("PineCone", () => CreateCone(7, 0f, 1f));
            pyramidMesh = GetOrCreateMesh("Pyramid", () => CreateCone(4, 45f, 1f));
            domeMesh = GetOrCreateMesh("SiloCap", () => CreateCone(8, 0f, 1f));
            cylinderMesh = GetOrCreateMesh("Cylinder6", () => CreateCylinder(6));
            bellMesh = GetOrCreateMesh("Bell", CreateBell);
            if (TextureSynth.Load("grass") == null)
                TextureSynth.GenerateAll();
            MenuBuilder.EnsureFontAsset();
            DefineMaterials();

            EnsureLayer("NoReflection");
            EnsureLayer("MirrorGhost");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var environment = new GameObject("Environment").transform;
            BuildGroundAndRoads(environment);
            BuildTownCenter(environment);
            BuildGraveyard(environment);
            BuildMotelAndEntrance(environment);
            BuildFarms(environment);
            BuildProps(environment);
            BuildForest(environment);
            BuildBoundaries(environment);
            MarkStatic(environment);
            // Doors swing, so they can't be static-batched.
            foreach (var door in environment.GetComponentsInChildren<World.Door>(true))
                foreach (var t in door.transform.parent.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            BuildScheduleSpots(environment);
            BakeNavMesh(environment);

            // Lamps are built last so they stay non-static (they flicker at runtime).
            Group("Lights", null);
            foreach (var lamp in DeferredLamps)
                lamp();

            var playerCamera = BuildPlayer(new Vector3(1.8f, 0.05f, -240f));
            BuildSkyAndAtmosphere(playerCamera);

            var systemsRoot = BuildGameSystems();
            var placer = systemsRoot.gameObject.AddComponent<Investigation.EvidencePlacer>();
            SetField(placer, "shader", litShader);
            BuildUnderground();
            BuildCar(new Vector3(5.2f, 0f, -213f), -30f);
            BuildEvidence();
            ResidentCast.EnsureProfiles(overwrite: false);
            ResidentCast.PlaceResidents(litShader);
            new GameObject("Case Director").AddComponent<Case.CaseDirector>();

            TrimShadowCasters();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuBuilder.ScenePath) != null)
                MenuBuilder.SetBuildOrder();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Semblance] Town built: {ScenePath} ({generatedMeshCount} batched meshes)");
        }

        // ---------------------------------------------------------------- Roads

        private static void BuildGroundAndRoads(Transform parent)
        {
            var root = Group("Ground & Roads", parent);

            // Flat surfaces are built from small tiles: huge polygons wobble and z-fight with PS1 vertex snapping.
            float groundSize = WorldHalfSize * 2 + 40;
            var surfaces = new MeshBatcher("Surfaces");
            surfaces.AddTiledQuad(null, "Grass", Vector3.zero, groundSize, groundSize, 10f);
            surfaces.AddTiledQuad(null, "Asphalt", new Vector3(0, 0.03f, 0), RoadHalfWidth * 2, MainRoadHalfLength * 2, 4f);
            surfaces.AddTiledQuad(null, "Dirt", new Vector3(0, 0.02f, 0), CrossRoadHalfLength * 2, RoadHalfWidth * 2 - 1f, 4f);
            for (float z = -MainRoadHalfLength + 2f; z < MainRoadHalfLength; z += 6f)
            {
                if (Mathf.Abs(z) < RoadHalfWidth + 1f)
                    continue; // keep the crossroads clear
                surfaces.AddTiledQuad(null, "RoadLine", new Vector3(0, 0.04f, z), 0.15f, 2.2f, 4f);
            }
            surfaces.Build(root);
            Collider(root, "Ground Collider", new Vector3(0, -0.5f, 0), Quaternion.identity, new Vector3(groundSize, 1, groundSize));
            Zone(root, "Highway", Vector3.zero, new Vector3(RoadHalfWidth * 2, 1, MainRoadHalfLength * 2), Audio.Surface.Asphalt, 2);
            Zone(root, "County Road", Vector3.zero, new Vector3(CrossRoadHalfLength * 2, 1, RoadHalfWidth * 2 - 1f), Audio.Surface.Dirt, 1);

            float sidewalkWidth = SidewalkOuterEdge - RoadHalfWidth;
            float sidewalkX = RoadHalfWidth + sidewalkWidth * 0.5f;
            const float slabLength = 5f;
            foreach (float side in new[] { -1f, 1f })
            foreach (float half in new[] { -1f, 1f })
            for (float d = RoadHalfWidth; d < CenterHalfLength - 0.01f; d += slabLength)
            {
                float length = Mathf.Min(slabLength, CenterHalfLength - d);
                Box("Sidewalk", root, new Vector3(side * sidewalkX, 0.075f, half * (d + length * 0.5f)), new Vector3(sidewalkWidth, 0.15f, length - 0.05f), "Sidewalk");
            }

            TreeExclusions.Add(Rect.MinMaxRect(-10f, -MainRoadHalfLength - 20f, 10f, MainRoadHalfLength + 20f));
            TreeExclusions.Add(Rect.MinMaxRect(-CrossRoadHalfLength - 20f, -9f, CrossRoadHalfLength + 20f, 9f));
        }

        // ---------------------------------------------------------------- Town center

        private static void BuildTownCenter(Transform parent)
        {
            var root = Group("Town Center", parent);
            var rng = new Random(1985);

            BuildRoadsideBuilding(root, rng, west: true, z: 28f, new BuildingDef
            {
                Name = "Church", Size = new Vector3(10, 8, 16), Material = "White", Pitched = true, Doors = 1,
                DoorSize = new Vector2(1.6f, 2.8f), LitWindowChance = 0.6f, Interior = InteriorKind.Church, AnchorKey = "Church",
                DoorPrompt = "Inspect door", DoorDescription = "First Church of Semblance. The board reads: \"WE ARE MADE IN HIS IMAGE.\"",
            }, steeple: true);

            BuildRoadsideBuilding(root, rng, west: false, z: 16f, new BuildingDef
            {
                Name = "General Store", Sign = "SEMBLANCE GENERAL", Size = new Vector3(12, 5, 9), Material = "Mustard", Doors = 1,
                LitWindowChance = 0.5f, Interior = InteriorKind.Store, AnchorKey = "Store",
                DoorPrompt = "Read notice", DoorDescription = "A hand-lettered sign in the window: \"WE DO NOT SELL MIRRORS. PLEASE DON'T ASK.\"",
            });

            BuildRoadsideBuilding(root, rng, west: false, z: -18f, new BuildingDef
            {
                Name = "Diner", Sign = "MAE'S DINER", SignMaterial = "SignNeon", Size = new Vector3(12, 4, 9), Material = "Teal", Doors = 1,
                LitWindowChance = 1f, Interior = InteriorKind.Diner, AnchorKey = "Diner",
                DoorPrompt = "Read menu", DoorDescription = "Today's special: meatloaf. Someone has written underneath in pencil: \"Ask Mae what she had for breakfast.\"",
            });

            // 1980s phone booth on the west sidewalk: aluminium frame, glass on three sides, the folding door
            // standing half open towards the road, a lit TELEPHONE header, the phone on the back wall.
            var booth = Group("Payphone", root);
            booth.position = new Vector3(-SidewalkOuterEdge + 0.8f, 0.15f, -12f);
            booth.rotation = Quaternion.Euler(0, 90f, 0); // local +z (the open front) faces the road
            Box("Plinth", booth, new Vector3(0, 0.03f, 0), new Vector3(1.04f, 0.06f, 1.04f), "Galvanized", collider: false);
            foreach (var x in new[] { -0.49f, 0.49f })
            foreach (var z in new[] { -0.49f, 0.49f })
                Box("Post", booth, new Vector3(x, 1.12f, z), new Vector3(0.06f, 2.2f, 0.06f), "Chrome");
            Box("Roof", booth, new Vector3(0, 2.28f, 0), new Vector3(1.1f, 0.1f, 1.1f), "Chrome", collider: false);
            foreach (var (pos, size) in new[] { (new Vector3(0, 2.13f, 0.53f), new Vector3(0.98f, 0.2f, 0.02f)), (new Vector3(0, 2.13f, -0.53f), new Vector3(0.98f, 0.2f, 0.02f)),
                                                (new Vector3(0.53f, 2.13f, 0), new Vector3(0.02f, 0.2f, 0.98f)), (new Vector3(-0.53f, 2.13f, 0), new Vector3(0.02f, 0.2f, 0.98f)) })
                Box("Header", booth, pos, size, "PhoneSign", collider: false);
            AddText(booth, "TELEPHONE", new Vector3(0, 2.13f, 0.545f), new Vector2(0.9f, 0.16f), new Color(0.1f, 0.14f, 0.2f));
            AddText(booth, "TELEPHONE", new Vector3(0.545f, 2.13f, 0), new Vector2(0.9f, 0.16f), new Color(0.1f, 0.14f, 0.2f), Quaternion.Euler(0, -90f, 0));
            AddText(booth, "TELEPHONE", new Vector3(-0.545f, 2.13f, 0), new Vector2(0.9f, 0.16f), new Color(0.1f, 0.14f, 0.2f), Quaternion.Euler(0, 90f, 0));
            // Back and side walls: a metal kick panel, glass above.
            Box("Kick Panel", booth, new Vector3(0, 0.3f, -0.48f), new Vector3(0.92f, 0.48f, 0.03f), "Galvanized");
            Box("Glass", booth, new Vector3(0, 1.3f, -0.48f), new Vector3(0.92f, 1.5f, 0.02f), "Glass");
            foreach (var x in new[] { -0.48f, 0.48f })
            {
                Box("Kick Panel", booth, new Vector3(x, 0.3f, 0), new Vector3(0.03f, 0.48f, 0.92f), "Galvanized");
                Box("Glass", booth, new Vector3(x, 1.3f, 0), new Vector3(0.02f, 1.5f, 0.92f), "Glass");
                Box("Rail", booth, new Vector3(x, 0.55f, 0), new Vector3(0.04f, 0.03f, 0.92f), "Chrome", collider: false);
            }
            // The bi-fold door, folded back against the right-hand post.
            var fold = Group("Folding Door", booth);
            fold.localPosition = new Vector3(0.46f, 0, 0.5f);
            fold.localRotation = Quaternion.Euler(0, -70f, 0);
            Box("Door Glass", fold, new Vector3(-0.22f, 1.15f, 0), new Vector3(0.42f, 1.9f, 0.02f), "Glass", collider: false);
            Box("Door Frame", fold, new Vector3(-0.22f, 2.1f, 0), new Vector3(0.44f, 0.04f, 0.03f), "Chrome", collider: false);
            Box("Door Frame", fold, new Vector3(-0.22f, 0.2f, 0), new Vector3(0.44f, 0.04f, 0.03f), "Chrome", collider: false);
            // The phone: a chrome box with a dial, a coin slot and a black handset on its hook.
            var phone = Box("Phone", booth, new Vector3(0, 1.35f, -0.4f), new Vector3(0.3f, 0.48f, 0.12f), "Chrome");
            Box("Dial Plate", booth, new Vector3(0.03f, 1.33f, -0.338f), new Vector3(0.16f, 0.16f, 0.01f), "Metal", collider: false);
            MeshPart("Dial", booth, cylinderMesh, new Vector3(0.03f, 1.33f, -0.33f), new Vector3(0.13f, 0.005f, 0.13f), "Chrome").transform.localRotation = Quaternion.Euler(90f, 0, 0);
            Box("Coin Slot", booth, new Vector3(0.08f, 1.52f, -0.338f), new Vector3(0.05f, 0.012f, 0.01f), "Metal", collider: false);
            Box("Coin Return", booth, new Vector3(0.06f, 1.17f, -0.338f), new Vector3(0.07f, 0.05f, 0.01f), "Metal", collider: false);
            Box("Handset", booth, new Vector3(-0.17f, 1.38f, -0.36f), new Vector3(0.06f, 0.26f, 0.06f), "HideBlack", collider: false);
            foreach (var y in new[] { 1.27f, 1.49f })
                Box("Handset End", booth, new Vector3(-0.17f, y, -0.33f), new Vector3(0.07f, 0.07f, 0.06f), "HideBlack", collider: false);
            Box("Cord", booth, new Vector3(-0.14f, 1.12f, -0.38f), new Vector3(0.015f, 0.3f, 0.015f), "HideBlack", collider: false);
            Box("Directory Shelf", booth, new Vector3(0, 0.95f, -0.33f), new Vector3(0.5f, 0.03f, 0.26f), "Chrome", collider: false);
            Box("Directory", booth, new Vector3(0.05f, 0.98f, -0.33f), new Vector3(0.24f, 0.04f, 0.18f), "PlainMustard", collider: false);
            Box("Booth Light", booth, new Vector3(0, 2.21f, 0), new Vector3(0.3f, 0.03f, 0.3f), "Bulb", collider: false);
            AddInspectable(phone, "Pick up phone", "There's a dial tone. Underneath it, very faintly, someone is breathing.");

            // Streetlamps only in the center.
            int index = 0;
            for (float z = -38f; z <= 42f; z += 16f, index++)
            {
                float side = index % 2 == 0 ? 1f : -1f;
                var position = new Vector3(side * (RoadHalfWidth + 0.7f), 0, z);
                var rotation = Quaternion.Euler(0, side > 0 ? -90f : 90f, 0);
                QueueLamp(root, "Street Lamp " + index, position, rotation, flicker: index == 3);
            }
            QueueLamp(root, "Street Lamp West", new Vector3(-20f, 0, RoadHalfWidth + 0.7f), Quaternion.Euler(0, 180f, 0), flicker: false);
            QueueLamp(root, "Street Lamp East", new Vector3(20f, 0, -RoadHalfWidth - 0.7f), Quaternion.identity, flicker: false);

            TreeExclusions.Add(Rect.MinMaxRect(-40f, -50f, 40f, 70f));
            AddMarker(root, "the town square", 48f, 0);
        }

        private static void BuildRoadsideBuilding(Transform parent, Random rng, bool west, float z, BuildingDef def, bool steeple = false)
        {
            float side = west ? -1f : 1f;
            var position = new Vector3(side * (BuildingSetback + def.Size.z * 0.5f), 0, z);
            var building = BuildBuilding(parent, def, position, west ? 90f : -90f, rng);
            if (steeple)
            {
                float front = def.Size.z * 0.5f;
                // Tower, an open belfry with the bell hanging in it, then the spire.
                float h = def.Size.y, sz = front - 1.8f;
                Box("Steeple", building, new Vector3(0, h + 1.25f, sz), new Vector3(2.6f, 4.5f, 2.6f), def.Material, collider: false);
                Box("Belfry Floor", building, new Vector3(0, h + 3.55f, sz), new Vector3(2.8f, 0.12f, 2.8f), "Wood", collider: false);
                foreach (var x in new[] { -1.15f, 1.15f })
                foreach (var zz in new[] { -1.15f, 1.15f })
                    Box("Belfry Post", building, new Vector3(x, h + 4.6f, sz + zz), new Vector3(0.3f, 2.0f, 0.3f), def.Material, collider: false);
                foreach (var face in new[] { 0f, 90f, 180f, 270f })
                {
                    var rail = Group("Belfry Rail", building);
                    rail.localPosition = new Vector3(0, 0, sz);
                    rail.localRotation = Quaternion.Euler(0, face, 0);
                    Box("Rail", rail, new Vector3(0, h + 4.0f, 1.2f), new Vector3(2.1f, 0.08f, 0.08f), def.Material, collider: false);
                    Box("Arch", rail, new Vector3(0, h + 5.45f, 1.2f), new Vector3(2.1f, 0.3f, 0.12f), def.Material, collider: false);
                }
                Box("Belfry Cap", building, new Vector3(0, h + 5.7f, sz), new Vector3(2.9f, 0.2f, 2.9f), def.Material, collider: false);
                Box("Headstock", building, new Vector3(0, h + 5.4f, sz), new Vector3(2.3f, 0.14f, 0.16f), "Wood", collider: false);
                Box("Yoke", building, new Vector3(0, h + 5.25f, sz), new Vector3(0.3f, 0.18f, 0.22f), "Wood", collider: false);
                MeshPart("Bell", building, bellMesh, new Vector3(0, h + 4.1f, sz), new Vector3(1.25f, 1.15f, 1.25f), "Bronze");
                Box("Clapper", building, new Vector3(0, h + 4.4f, sz), new Vector3(0.05f, 0.7f, 0.05f), "Metal", collider: false);
                MeshPart("Clapper Ball", building, cylinderMesh, new Vector3(0, h + 4.02f, sz), new Vector3(0.14f, 0.07f, 0.14f), "Metal");
                Box("Wheel", building, new Vector3(1.05f, h + 5.25f, sz), new Vector3(0.06f, 0.7f, 0.7f), "Wood", collider: false);
                MeshPart("Spire", building, pyramidMesh, new Vector3(0, h + 5.8f, sz), new Vector3(3.2f, 4.5f, 3.2f), "Roof");
            }
        }

        private static void BuildGraveyard(Transform parent)
        {
            var root = Group("Graveyard", parent);
            var rng = new Random(666);
            var batch = new MeshBatcher("Graveyard");
            const float minX = -25f, maxX = -9f, minZ = 38f, maxZ = 60f;

            for (float z = minZ + 3f; z <= maxZ - 3f; z += 3.5f)
            for (float x = maxX - 2f; x >= minX + 2f; x -= 2.4f)
            {
                if (rng.NextDouble() < 0.15)
                    continue;
                float height = Mathf.Lerp(0.6f, 1.1f, (float)rng.NextDouble());
                var tilt = Quaternion.Euler(Lerp(rng, -8f, 8f), Lerp(rng, -6f, 6f), Lerp(rng, -6f, 6f));
                var position = new Vector3(x + Lerp(rng, -0.3f, 0.3f), height * 0.5f - 0.1f, z);
                bool cross = rng.NextDouble() < 0.2;
                if (cross)
                {
                    batch.AddBox("Stone", position + Vector3.up * 0.2f, new Vector3(0.12f, height + 0.4f, 0.12f), tilt);
                    batch.AddBox("Stone", position + tilt * new Vector3(0, height * 0.35f, 0), new Vector3(0.6f, 0.12f, 0.12f), tilt);
                }
                else
                {
                    batch.AddBox("Stone", position, new Vector3(0.6f, height, 0.15f), tilt);
                }
            }

            // Wrought-iron fence with an opening facing the road.
            void FenceLine(Vector3 a, Vector3 b)
            {
                float length = Vector3.Distance(a, b);
                var direction = (b - a).normalized;
                var rotation = Quaternion.LookRotation(direction);
                for (float d = 0; d <= length; d += 0.9f)
                    batch.AddBox("Metal", a + direction * d + Vector3.up * 0.6f, new Vector3(0.05f, 1.2f, 0.05f), Quaternion.identity);
                batch.AddBox("Metal", (a + b) * 0.5f + Vector3.up * 1.1f, new Vector3(0.05f, 0.05f, length), rotation);
                Collider(root, "Fence Collider", (a + b) * 0.5f + Vector3.up * 0.6f, rotation, new Vector3(0.2f, 1.2f, length));
            }
            FenceLine(new Vector3(minX, 0, minZ), new Vector3(minX, 0, maxZ));
            FenceLine(new Vector3(minX, 0, maxZ), new Vector3(maxX, 0, maxZ));
            FenceLine(new Vector3(minX, 0, minZ), new Vector3(maxX, 0, minZ));
            FenceLine(new Vector3(maxX, 0, minZ), new Vector3(maxX, 0, 47f));
            FenceLine(new Vector3(maxX, 0, 51f), new Vector3(maxX, 0, maxZ));
            batch.Build(root);

            // The fresh grave.
            AddMarker(root, "the churchyard", 12f, 1, new Vector3(-17f, 0, 49f));
            var grave = Group("Fresh Grave", root);
            grave.position = new Vector3(-21.5f, 0, 57f);
            grave.rotation = Quaternion.Euler(0, 90f, 0);
            Box("Mound", grave, new Vector3(0, 0.12f, 0), new Vector3(1.1f, 0.25f, 2.2f), "Dirt", collider: false);
            // Clawed at from the top: long thin furrows in groups of four, and the soil thrown out round the edge.
            var graveRng = new Random(9);
            for (int g = 0; g < 3; g++)
            {
                // One hand's four fingers dragged together: parallel, evenly spaced, a slight curve between groups.
                float gx = -0.3f + g * 0.3f, gz = -0.6f + g * 0.45f, angle = Lerp(graveRng, -10f, 10f);
                var hand = Quaternion.Euler(0, angle, 0);
                for (int f = 0; f < 4; f++)
                {
                    var furrow = Box("Furrow", grave, new Vector3(gx, 0.252f, gz) + hand * new Vector3(f * 0.055f, 0, 0), new Vector3(0.025f, 0.012f, 0.55f), "DirtDark", collider: false);
                    furrow.transform.localRotation = hand;
                }
            }
            for (int c = 0; c < 9; c++)
            {
                var clod = Box("Clod", grave, new Vector3(Lerp(graveRng, -0.8f, 0.8f), 0.04f, Lerp(graveRng, -1.3f, 1.0f)), Vector3.one * Lerp(graveRng, 0.06f, 0.12f), "Dirt", collider: false);
                if (Mathf.Abs(clod.transform.localPosition.x) < 0.6f) clod.transform.localPosition += new Vector3(Mathf.Sign(clod.transform.localPosition.x) * 0.5f, 0, 0);
                clod.transform.localRotation = Quaternion.Euler(Lerp(graveRng, 0f, 40f), Lerp(graveRng, 0f, 90f), 0);
            }
            var stone = Box("Headstone", grave, new Vector3(0, 0.5f, 1.2f), new Vector3(0.7f, 1f, 0.18f), "Stone");
            // Where the name was: a pale, chewed-up patch, with the ghost of a capital R.
            Box("Chisel Scar", grave, new Vector3(0, 0.66f, 1.105f), new Vector3(0.5f, 0.16f, 0.012f), "StoneScar", collider: false);
            AddText(grave, "R", new Vector3(-0.19f, 0.66f, 1.097f), new Vector2(0.12f, 0.15f), new Color(0.35f, 0.35f, 0.33f, 0.6f), Quaternion.identity);
            AddText(grave, "1983", new Vector3(0, 0.45f, 1.097f), new Vector2(0.4f, 0.1f), new Color(0.3f, 0.3f, 0.28f), Quaternion.identity);
            AddInspectable(stone, "Read headstone", "A fresh grave. There was a name carved here, but it has been chiselled away.");

            ExtraTrees.Add(new Vector3(-27f, 0, 62f));
            ExtraTrees.Add(new Vector3(-12f, 0, 63f));
        }

        private static void BuildMotelAndEntrance(Transform parent)
        {
            var root = Group("Town Entrance", parent);
            var rng = new Random(6);

            BuildRoadsideBuilding(root, rng, west: false, z: -205f, new BuildingDef
            {
                Name = "Motel", Sign = "PALE MOON MOTEL", SignMaterial = "SignNeon", Size = new Vector3(18, 4, 8), Material = "Pink", Doors = 5,
                LitWindowChance = 0.15f, Interior = InteriorKind.Motel, AnchorKey = "Motel",
                DoorPrompt = "Inspect room 6", DoorDescription = "Room 6. Your room. The key was already in the door when you arrived.",
            });
            QueueLamp(root, "Motel Lamp", new Vector3(RoadHalfWidth + 0.7f, 0, -200f), Quaternion.Euler(0, -90f, 0), flicker: false);
            TreeExclusions.Add(Rect.MinMaxRect(-12f, -225f, 30f, -185f));

            // A signpost in the lot, so a new arrival knows which way town is.
            var post = Group("Town Signpost", root);
            post.position = new Vector3(4.6f, 0, -219f);
            Box("Post", post, new Vector3(0, 1.1f, 0.1f), new Vector3(0.14f, 2.2f, 0.14f), "Wood");
            Box("Board", post, new Vector3(0, 2.0f, 0), new Vector3(2.2f, 0.75f, 0.08f), "SignWhite", collider: false);
            AddText(post, "SEMBLANCE  1/4 MI NORTH\n<size=70%>MAE'S DINER  /  GENERAL STORE  /  CHURCH</size>", new Vector3(0, 2.0f, -0.05f), new Vector2(2.05f, 0.6f), new Color(0.12f, 0.12f, 0.12f), Quaternion.identity);

            var sign = Group("Welcome Sign", root);
            sign.position = new Vector3(-5.5f, 0, -228f);
            sign.rotation = Quaternion.Euler(0, 180f, 0);
            Box("Post L", sign, new Vector3(-1.5f, 1f, -0.15f), new Vector3(0.18f, 2f, 0.18f), "Wood");
            Box("Post R", sign, new Vector3(1.5f, 1f, -0.15f), new Vector3(0.18f, 2f, 0.18f), "Wood");
            var board = Box("Board", sign, new Vector3(0, 2.1f, 0), new Vector3(3.8f, 1.5f, 0.12f), "SignWhite");
            AddInspectable(board, "Read sign", "Someone has painted 213 right over the old population. The paint looks fresh.");
            var ink = new Color(0.12f, 0.12f, 0.12f);
            AddText(sign, "WELCOME TO", new Vector3(0, 2.6f, 0.07f), new Vector2(2.4f, 0.28f), ink);
            AddText(sign, "SEMBLANCE", new Vector3(0, 2.2f, 0.07f), new Vector2(3.3f, 0.5f), ink);
            AddText(sign, "POP.", new Vector3(0.42f, 1.7f, 0.07f), new Vector2(0.7f, 0.3f), ink);
            // The old number, sun-faded, with the new one painted on top of it by hand.
            // (The sign is rotated 180 degrees, so negative local X is the viewer's right.)
            var faded = AddText(sign, "212", new Vector3(-0.3f, 1.7f, 0.07f), new Vector2(0.7f, 0.3f), new Color(0.12f, 0.12f, 0.12f, 0.4f));
            var fresh = AddText(sign, "213", new Vector3(-0.38f, 1.76f, 0.075f), new Vector2(0.78f, 0.36f), new Color(0.48f, 0.07f, 0.05f),
                Quaternion.Euler(0, 180f, -6f));
            fresh.fontStyle = FontStyles.Bold | FontStyles.Italic;
            sign.gameObject.AddComponent<World.SignPopulation>().Configure(faded, fresh, board.GetComponent<InspectableObject>());
        }

        // ---------------------------------------------------------------- Farms

        private static void BuildFarms(Transform parent)
        {
            var root = Group("Farms", parent);
            var farms = new List<FarmDef>();

            FarmDef Farm(FarmDef f, string family, float drive, float houseX, string houseMat, bool barn, bool silo, int corn, string mailbox, bool flicker = false)
            {
                f.Family = family; f.DriveLength = drive; f.HouseOffsetX = houseX; f.HouseMaterial = houseMat;
                f.Barn = barn; f.Silo = silo; f.CornSide = corn; f.Mailbox = mailbox; f.FlickeringGate = flicker;
                return f;
            }

            // North highway
            farms.Add(Farm(OnHighway(85f, east: true), "Hollis", 40f, -3f, "Cream", barn: true, silo: false, corn: 1,
                "The flag is up, but the box is empty except for a single baby tooth."));
            farms.Add(Farm(OnHighway(155f, east: false), "Marsh", 50f, 4f, "Blue", barn: false, silo: false, corn: -1,
                "A child's drawing is taped inside: a family of four. Someone has added a fifth figure in a different crayon."));
            farms.Add(Farm(OnHighway(228f, east: true), "Pruitt", 35f, 2f, "Sage", barn: true, silo: true, corn: 0,
                "Seed catalogues and a feed bill. All addressed to \"Occupant\"."));
            // South highway
            farms.Add(Farm(OnHighway(-95f, east: false), "Calder", 45f, -2f, "White", barn: false, silo: false, corn: 0,
                "A bundle of letters stamped RETURN TO SENDER, all addressed to Ruth Calder.", flicker: true));
            farms.Add(Farm(OnHighway(-150f, east: true), "Whitlock", 38f, 3f, "Cream", barn: true, silo: false, corn: -1,
                "A note: \"Gone to Mae's. Back by dark. Don't let it in.\""));
            // East county road
            var dobbs = Farm(OnCountyRoad(90f, north: true), "Dobbs", 42f, 0f, "Blue", barn: false, silo: false, corn: 1,
                "Empty. Someone has cleaned the inside very, very carefully.");
            dobbs.LockedHouse = true;
            dobbs.LockedMessage = "Locked. Something heavy has been pushed against the other side.";
            farms.Add(dobbs);
            farms.Add(Farm(OnCountyRoad(165f, north: false), "Kessler", 55f, -4f, "Sage", barn: true, silo: true, corn: 1,
                "A vet's invoice: 14 cattle. Cause of death: \"undetermined\"."));
            // West county road
            farms.Add(Farm(OnCountyRoad(-92f, north: false), "Thorne", 40f, 2f, "Cream", barn: true, silo: false, corn: -1,
                "A family photograph, dated 1979. The father's face is blurred as if he moved. Everyone else is perfectly still."));
            farms.Add(Farm(OnCountyRoad(-168f, north: true), "Vance", 48f, 0f, "White", barn: false, silo: true, corn: 0,
                "The mailbox is full of dead moths."));

            var rng = new Random(1979);
            foreach (var farm in farms)
                BuildFarm(root, farm, rng);
        }

        /// <summary>A ranch gate leaf: five rails between two stiles and a diagonal brace (one plain collider for the lot).</summary>
        private static void GatePanel(Transform parent, string name, Vector3 center)
        {
            var solid = Box(name, parent, center, new Vector3(2.3f, 1.1f, 0.08f), "Wood");
            solid.GetComponent<Renderer>().enabled = false;
            for (int r = 0; r < 5; r++)
                Box("Rail", parent, center + new Vector3(0, -0.48f + r * 0.24f, 0), new Vector3(2.3f, 0.1f, 0.05f), "Wood", collider: false);
            foreach (var x in new[] { -1.1f, 1.1f })
                Box("Stile", parent, center + new Vector3(x, 0, 0), new Vector3(0.1f, 1.12f, 0.07f), "Wood", collider: false);
            var brace = Box("Brace", parent, center + new Vector3(0, 0, 0.05f), new Vector3(2.3f, 0.09f, 0.04f), "Wood", collider: false);
            brace.transform.localRotation = Quaternion.Euler(0, 0, 24f);
        }

        private static void BuildFarm(Transform parent, FarmDef def, Random rng)
        {
            var frame = Group(def.Family + " Farm", parent);
            frame.position = def.GatePosition;
            frame.rotation = Quaternion.Euler(0, def.Yaw, 0);
            FarmFrames[def.Family] = (frame, def);
            var batch = new MeshBatcher(def.Family + "Farm");
            float drive = def.DriveLength;
            AddMarker(frame, def.Family + " Farm", drive * 0.5f + 26f, 1, new Vector3(0, 0, drive * 0.5f + 6f));

            // Frontage fence along the road, broken by the gate.
            FenceRun(batch, frame, new Vector3(-32f, 0, 0), new Vector3(-2.9f, 0, 0), rng);
            FenceRun(batch, frame, new Vector3(2.9f, 0, 0), new Vector3(32f, 0, 0), rng);

            // Ranch gate with the family name.
            Box("Gate Post L", frame, new Vector3(-2.6f, 1.6f, 0), new Vector3(0.35f, 3.2f, 0.35f), "Wood");
            Box("Gate Post R", frame, new Vector3(2.6f, 1.6f, 0), new Vector3(0.35f, 3.2f, 0.35f), "Wood");
            Box("Gate Beam", frame, new Vector3(0, 3.1f, 0), new Vector3(6.3f, 0.3f, 0.3f), "Wood", collider: false);
            Box("Name Board", frame, new Vector3(0, 2.55f, 0), new Vector3(3.2f, 0.6f, 0.1f), "SignBoard", collider: false);
            AddText(frame, def.Family.ToUpperInvariant(), new Vector3(0, 2.55f, -0.06f), new Vector2(3f, 0.5f),
                new Color(0.82f, 0.76f, 0.6f), Quaternion.identity);
            GatePanel(frame, "Gate Panel (closed)", new Vector3(-1.3f, 0.75f, 0));
            var hinge = Group("Gate Panel Hinge", frame);
            hinge.localPosition = new Vector3(2.45f, 0, 0);
            hinge.localRotation = Quaternion.Euler(0, -105f, 0);
            GatePanel(hinge, "Gate Panel (open)", new Vector3(-1.2f, 0.75f, 0));

            // Mailbox on the road side.
            Box("Mailbox Post", frame, new Vector3(4.2f, 0.55f, -1.1f), new Vector3(0.1f, 1.1f, 0.1f), "Wood");
            // A rural mailbox: flat sides, a rounded top, the door facing the road, a red flag on the side.
            var mailbox = Box("Mailbox", frame, new Vector3(4.2f, 1.17f, -1.1f), new Vector3(0.3f, 0.2f, 0.52f), "Galvanized");
            MeshPart("Mailbox Top", frame, cylinderMesh, new Vector3(4.2f, 1.27f, -1.1f), new Vector3(0.3f, 0.26f, 0.3f), "Galvanized").transform.localRotation = Quaternion.Euler(90f, 0, 0);
            Box("Mailbox Door", frame, new Vector3(4.2f, 1.2f, -1.365f), new Vector3(0.3f, 0.3f, 0.015f), "Metal", collider: false);
            Box("Flag Pivot", frame, new Vector3(4.36f, 1.15f, -1.0f), new Vector3(0.02f, 0.04f, 0.04f), "Metal", collider: false);
            var flag = Group("Flag", frame);
            flag.localPosition = new Vector3(4.37f, 1.15f, -1.0f);
            flag.localRotation = def.Family == "Hollis" ? Quaternion.identity : Quaternion.Euler(90f, 0, 0); // the Hollis flag is up
            Box("Flag Arm", flag, new Vector3(0, 0.12f, 0), new Vector3(0.015f, 0.24f, 0.025f), "PlainBarnRed", collider: false);
            Box("Flag Plate", flag, new Vector3(0, 0.22f, 0.04f), new Vector3(0.015f, 0.07f, 0.1f), "PlainBarnRed", collider: false);
            AddInspectable(mailbox, "Check mailbox", def.Family.ToUpperInvariant() + ". " + def.Mailbox);
            // Generous trigger around the mailbox so it's easy to target (local units are scaled by the box).
            var mailboxTrigger = mailbox.GetComponent<BoxCollider>();
            mailboxTrigger.isTrigger = true;
            mailboxTrigger.size = new Vector3(3f, 5.5f, 1.9f);
            mailboxTrigger.center = new Vector3(0, -0.9f, 0);

            // The only light on the property: a lamp at the gate, angled over the entrance.
            QueueLamp(frame, "Gate Lamp", frame.TransformPoint(new Vector3(-4.3f, 0, -0.9f)),
                frame.rotation * Quaternion.Euler(0, 75f, 0), def.FlickeringGate);

            // Long dirt driveway up to the house.
            batch.AddTiledQuad(frame, "Dirt", new Vector3(0, 0.015f, drive * 0.5f + 0.5f), 3.2f, drive + 1f, 4f);
            Zone(frame, "Driveway", new Vector3(0, 0, drive * 0.5f + 0.5f), new Vector3(3.2f, 1, drive + 1f), Audio.Surface.Dirt, 1);

            var house = new BuildingDef
            {
                Name = "Farmhouse", Size = new Vector3(10, 5.5f, 8), Material = def.HouseMaterial, Pitched = true, Doors = 1,
                LitWindowChance = 0.45f, Interior = InteriorKind.House, AnchorKey = "House:" + def.Family,
                Locked = def.LockedHouse, LockedMessage = def.LockedMessage,
            };
            float houseZ = drive + 2f + house.Size.z * 0.5f;
            var houseRoot = BuildBuilding(parent, house, frame.TransformPoint(new Vector3(def.HouseOffsetX, 0, houseZ)), def.Yaw + 180f, rng);
            houseRoot.SetParent(frame, true);
            float porchFront = house.Size.z * 0.5f;
            Box("Porch Deck", houseRoot, new Vector3(0, 0.15f, porchFront + 1f), new Vector3(6f, 0.3f, 2f), "Wood");
            Box("Porch Roof", houseRoot, new Vector3(0, 2.75f, porchFront + 1f), new Vector3(6.4f, 0.15f, 2.2f), "Roof", collider: false);
            Box("Porch Post L", houseRoot, new Vector3(-2.8f, 1.4f, porchFront + 1.9f), new Vector3(0.15f, 2.6f, 0.15f), "Wood");
            Box("Porch Post R", houseRoot, new Vector3(2.8f, 1.4f, porchFront + 1.9f), new Vector3(0.15f, 2.6f, 0.15f), "Wood");
            Box("Chimney", houseRoot, new Vector3(3f, house.Size.y + 2f, -1f), new Vector3(0.8f, 3f, 0.8f), "Brick", collider: false);
            CellarSites.Add(("Cellar:" + def.Family, houseRoot, house.Size.z * 0.5f));
            var porchBulb = Box("Porch Bulb", houseRoot, new Vector3(1.2f, 2.55f, porchFront + 0.15f), new Vector3(0.14f, 0.14f, 0.14f), "Bulb", collider: false);
            var porchLightGO = new GameObject("Porch Light");
            porchLightGO.transform.SetParent(houseRoot, false);
            porchLightGO.transform.localPosition = new Vector3(1.2f, 2.35f, porchFront + 0.6f);
            var porchLight = porchLightGO.AddComponent<Light>();
            porchLight.type = LightType.Point;
            porchLight.color = new Color(1f, 0.72f, 0.42f);
            porchLight.range = 5f;
            porchLight.intensity = 1.8f;
            porchLight.shadows = LightShadows.None;
            porchLightGO.AddComponent<LampSwitch>().Configure(porchLight, porchBulb.GetComponent<Renderer>());
            porchLightGO.AddComponent<Audio.LampBuzz>();

            float outbuildingSide = def.CornSide != 0 ? -def.CornSide : (def.HouseOffsetX >= 0 ? 1f : -1f);
            if (def.Barn)
            {
                var barn = new BuildingDef
                {
                    Name = "Barn", Size = new Vector3(10, 7, 14), Material = "BarnRed", Pitched = true, Doors = 1,
                    DoorSize = new Vector2(3.2f, 4f), LitWindowChance = 0f, Interior = InteriorKind.Barn, AnchorKey = "Barn:" + def.Family,
                };
                // Barn sits beside the house, its big doors facing the driveway.
                var barnPos = frame.TransformPoint(new Vector3(outbuildingSide * 17f, 0, drive - 2f));
                var barnRoot = BuildBuilding(parent, barn, barnPos, def.Yaw + (outbuildingSide > 0 ? -90f : 90f), rng);
                barnRoot.SetParent(frame, true);
            }
            if (def.Silo)
            {
                var siloPos = frame.TransformPoint(new Vector3(outbuildingSide * 26f, 0, drive + 6f));
                var silo = Group("Silo", frame);
                silo.position = siloPos;
                MeshPart("Silo Body", silo, cylinderMesh, new Vector3(0, 5.5f, 0), new Vector3(3.6f, 5.5f, 3.6f), "Galvanized");
                MeshPart("Silo Cap", silo, domeMesh, new Vector3(0, 11f, 0), new Vector3(3.8f, 1.6f, 3.8f), "Roof");
                Collider(silo, "Silo Collider", siloPos + Vector3.up * 5.5f, Quaternion.identity, new Vector3(3.2f, 11f, 3.2f));
            }
            if (def.CornSide != 0)
            {
                Cornfield(batch, frame, def.CornSide * 5f, def.CornSide * 24f, 5f, drive - 1f, rng);
                Zone(frame, "Cornfield", new Vector3(def.CornSide * 14.5f, 0, (4f + drive) * 0.5f), new Vector3(20f, 1, drive - 4f), Audio.Surface.Corn, 2);
            }

            // A few trees around the house.
            for (int i = 0; i < 4; i++)
                ExtraTrees.Add(frame.TransformPoint(new Vector3(def.HouseOffsetX + Lerp(rng, -14f, 14f), 0, houseZ + Lerp(rng, 9f, 16f))));

            batch.Build(frame.parent);

            // Keep the forest off the property.
            var corners = new[] { new Vector3(-34, 0, -4), new Vector3(34, 0, -4), new Vector3(-34, 0, drive + 22f), new Vector3(34, 0, drive + 22f) };
            var bounds = new Rect(frame.TransformPoint(corners[0]).x, frame.TransformPoint(corners[0]).z, 0, 0);
            foreach (var corner in corners)
            {
                var world = frame.TransformPoint(corner);
                bounds.xMin = Mathf.Min(bounds.xMin, world.x); bounds.xMax = Mathf.Max(bounds.xMax, world.x);
                bounds.yMin = Mathf.Min(bounds.yMin, world.z); bounds.yMax = Mathf.Max(bounds.yMax, world.z);
            }
            TreeExclusions.Add(bounds);
        }

        // ---------------------------------------------------------------- Props

        private static void BuildProps(Transform parent)
        {
            var root = Group("Props", parent);
            var batch = new MeshBatcher("Props");

            // Power line along the east side of the highway: poles every 40 m, sagging wires between.
            Vector3? previousTop = null;
            for (float z = -250f; z <= 250f; z += 40f)
            {
                if (Mathf.Abs(z) < 6f) continue;
                var pole = new Vector3(9.5f, 0, z);
                batch.Add(cylinderMesh, "Bark", Matrix4x4.TRS(pole + Vector3.up * 3.75f, Quaternion.identity, new Vector3(0.3f, 3.75f, 0.3f)));
                batch.AddBox("Wood", pole + Vector3.up * 7f, new Vector3(1.8f, 0.12f, 0.12f), Quaternion.identity);
                var top = pole + Vector3.up * 7.05f;
                if (previousTop.HasValue)
                    foreach (float side in new[] { -0.8f, 0.8f })
                    {
                        Vector3 a = previousTop.Value + Vector3.right * side, b = top + Vector3.right * side;
                        Vector3 mid = (a + b) * 0.5f - Vector3.up * 1.2f;
                        foreach (var (p, q) in new[] { (a, mid), (mid, b) })
                            batch.AddBox("Metal", (p + q) * 0.5f, new Vector3(0.03f, 0.03f, Vector3.Distance(p, q)), Quaternion.LookRotation(q - p));
                    }
                Collider(root, "Pole Collider", pole + Vector3.up * 3.75f, Quaternion.identity, new Vector3(0.35f, 7.5f, 0.35f));
                previousTop = top;
            }

            // Water tower behind the store.
            var tower = Group("Water Tower", root);
            tower.position = new Vector3(28f, 0, 40f);
            foreach (var x in new[] { -2f, 2f })
            foreach (var z in new[] { -2f, 2f })
                Box("Leg", tower, new Vector3(x, 5f, z), new Vector3(0.25f, 10f, 0.25f), "Metal");
            MeshPart("Tank", tower, cylinderMesh, new Vector3(0, 12.5f, 0), new Vector3(6f, 2.5f, 6f), "White");
            MeshPart("Tank Roof", tower, domeMesh, new Vector3(0, 15f, 0), new Vector3(6.4f, 1.6f, 6.4f), "Roof");
            // The tank is six-sided: the lettering sits on the flat face that looks down the road (2.6 m out).
            AddText(tower, "SEMBLANCE", new Vector3(0, 12.6f, -2.62f), new Vector2(2.9f, 0.8f), new Color(0.2f, 0.18f, 0.17f), Quaternion.identity);
            TreeExclusions.Add(Rect.MinMaxRect(22f, 34f, 34f, 46f));

            // Church notice board by the road.
            var board = Group("Church Board", root);
            board.position = new Vector3(-6.8f, 0, 21f);
            board.rotation = Quaternion.Euler(0, 90f, 0);
            Box("Post L", board, new Vector3(-0.8f, 0.7f, 0), new Vector3(0.1f, 1.4f, 0.1f), "Wood");
            Box("Post R", board, new Vector3(0.8f, 0.7f, 0), new Vector3(0.1f, 1.4f, 0.1f), "Wood");
            var sign = Box("Board", board, new Vector3(0, 1.5f, 0), new Vector3(2f, 0.9f, 0.08f), "SignWhite");
            AddText(board, "FIRST CHURCH OF SEMBLANCE\n<size=80%>WE ARE MADE IN HIS IMAGE\nCOUNT YOUR NEIGHBOURS</size>", new Vector3(0, 1.5f, 0.05f), new Vector2(1.95f, 0.95f), new Color(0.12f, 0.12f, 0.12f));
            AddInspectable(sign, "Read the board", "WE ARE MADE IN HIS IMAGE. COUNT YOUR NEIGHBOURS. The second line was added in different letters.");

            var rng = new Random(77);
            foreach (var pair in FarmFrames)
            {
                var (frame, def) = pair.Value;
                // Scarecrow watching over each cornfield.
                if (def.CornSide != 0)
                {
                    var crow = Group("Scarecrow", frame);
                    crow.localPosition = new Vector3(def.CornSide * 14f, 0, def.DriveLength * 0.5f);
                    crow.localRotation = Quaternion.Euler(0, 180f + Lerp(rng, -30f, 30f), 0);
                    Box("Post", crow, new Vector3(0, 1.3f, 0), new Vector3(0.1f, 2.6f, 0.1f), "Wood", collider: false);
                    Box("Arms", crow, new Vector3(0, 2.0f, 0), new Vector3(1.6f, 0.08f, 0.08f), "Wood", collider: false);
                    Box("Shirt", crow, new Vector3(0, 1.85f, 0), new Vector3(0.55f, 0.65f, 0.25f), "Fabric", collider: false);
                    Box("Sleeves", crow, new Vector3(0, 1.98f, 0), new Vector3(1.3f, 0.18f, 0.2f), "Fabric", collider: false);
                    Box("Head", crow, new Vector3(0, 2.45f, 0), new Vector3(0.32f, 0.36f, 0.3f), "Hay", collider: false);
                    Box("Hat Brim", crow, new Vector3(0, 2.64f, 0), new Vector3(0.5f, 0.03f, 0.5f), "Felt", collider: false);
                    Box("Hat Crown", crow, new Vector3(0, 2.73f, 0), new Vector3(0.28f, 0.16f, 0.28f), "Felt", collider: false);
                    foreach (var hx in new[] { -0.8f, 0.8f })
                        Box("Straw Hand", crow, new Vector3(hx, 1.96f, 0), new Vector3(0.12f, 0.16f, 0.08f), "Hay", collider: false);
                    Box("Eye", crow, new Vector3(-0.07f, 2.48f, 0.155f), new Vector3(0.05f, 0.05f, 0.01f), "Metal", collider: false);
                    Box("Eye", crow, new Vector3(0.07f, 2.48f, 0.155f), new Vector3(0.05f, 0.05f, 0.01f), "Metal", collider: false);
                }
                // Rusted pickup by the barn.
                if (def.Barn && rng.NextDouble() < 0.7)
                {
                    float side = def.CornSide != 0 ? -def.CornSide : (def.HouseOffsetX >= 0 ? 1f : -1f);
                    var truck = Group("Rusted Truck", frame);
                    truck.localPosition = new Vector3(side * 8f, 0, def.DriveLength - 8f);
                    truck.localRotation = Quaternion.Euler(0, Lerp(rng, 0f, 360f), 0);
                    Box("Bed", truck, new Vector3(0, 0.75f, -0.9f), new Vector3(1.8f, 0.7f, 2.2f), "Rust");
                    Box("Cab", truck, new Vector3(0, 1.1f, 0.9f), new Vector3(1.8f, 1.3f, 1.4f), "Rust");
                    Box("Hood", truck, new Vector3(0, 0.8f, 2.1f), new Vector3(1.8f, 0.7f, 1.1f), "Rust", collider: false);
                    Box("Grille", truck, new Vector3(0, 0.8f, 2.66f), new Vector3(1.4f, 0.4f, 0.03f), "Galvanized", collider: false);
                    Box("Bumper", truck, new Vector3(0, 0.45f, 2.7f), new Vector3(1.9f, 0.12f, 0.1f), "Galvanized", collider: false);
                    foreach (var hx in new[] { -0.65f, 0.65f })
                        Box("Headlamp", truck, new Vector3(hx, 0.92f, 2.67f), new Vector3(0.2f, 0.2f, 0.03f), "Glass", collider: false);
                    Box("Windshield", truck, new Vector3(0, 1.45f, 1.61f), new Vector3(1.6f, 0.5f, 0.04f), "WindowDark", collider: false);
                    foreach (var x in new[] { -0.95f, 0.95f })
                    foreach (var z in new[] { -1.2f, 1.9f })
                        MeshPart("Wheel", truck, cylinderMesh, new Vector3(x, 0.35f, z), new Vector3(0.7f, 0.12f, 0.7f), "HideBlack").transform.localRotation = Quaternion.Euler(0, 0, 90f);
                }
                // Hay rolls in the open fields.
                if (def.CornSide == 0)
                    for (int i = 0; i < 4; i++)
                    {
                        float side = def.HouseOffsetX >= 0 ? -1f : 1f;
                        var roll = MeshPart("Hay Roll", frame, cylinderMesh, new Vector3(side * Lerp(rng, 8f, 22f), 0.75f, Lerp(rng, 6f, def.DriveLength - 4f)), new Vector3(1.5f, 0.6f, 1.5f), "Hay");
                        roll.transform.localRotation = Quaternion.Euler(0, Lerp(rng, 0f, 180f), 90f);
                        roll.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 1f);
                    }
            }

            // Washing line at the Marsh farm: sheets hanging in the dark.
            if (FarmFrames.TryGetValue("Marsh", out var marsh))
            {
                var (frame, def) = marsh;
                var line = Group("Washing Line", frame);
                line.localPosition = new Vector3(6f, 0, def.DriveLength + 2f);
                Box("Pole", line, new Vector3(-3f, 1.1f, 0), new Vector3(0.08f, 2.2f, 0.08f), "Wood");
                Box("Pole", line, new Vector3(3f, 1.1f, 0), new Vector3(0.08f, 2.2f, 0.08f), "Wood");
                Box("Line", line, new Vector3(0, 2.1f, 0), new Vector3(6f, 0.02f, 0.02f), "Metal", collider: false);
                for (int i = 0; i < 3; i++)
                    Box("Sheet", line, new Vector3(-2f + i * 2f, 1.45f, 0), new Vector3(1.6f, 1.3f, 0.02f), "SignWhite", collider: false);
            }

            batch.Build(root);
        }

        private static readonly List<(string id, Transform house, float halfDepth)> CellarSites = new List<(string, Transform, float)>();

        // ---------------------------------------------------------------- Cellars, crypt & evidence

        /// <summary>The detective's car, parked in the motel lot.</summary>
        private static void BuildCar(Vector3 position, float yaw)
        {
            var car = new GameObject("Detective's Car").transform;
            car.position = position;
            car.rotation = Quaternion.Euler(0, yaw, 0);
            Box("Body", car, new Vector3(0, 0.6f, 0), new Vector3(1.9f, 0.6f, 4.4f), "CarPaint", collider: false);
            Box("Cabin", car, new Vector3(0, 1.15f, -0.3f), new Vector3(1.75f, 0.55f, 2.1f), "CarPaint", collider: false);
            Box("Windshield", car, new Vector3(0, 1.15f, 0.77f), new Vector3(1.6f, 0.45f, 0.04f), "WindowDark", collider: false);
            Box("Rear Window", car, new Vector3(0, 1.15f, -1.37f), new Vector3(1.6f, 0.45f, 0.04f), "WindowDark", collider: false);
            Box("Side Windows L", car, new Vector3(-0.88f, 1.15f, -0.3f), new Vector3(0.03f, 0.42f, 1.9f), "WindowDark", collider: false);
            Box("Side Windows R", car, new Vector3(0.88f, 1.15f, -0.3f), new Vector3(0.03f, 0.42f, 1.9f), "WindowDark", collider: false);
            Box("Bumper", car, new Vector3(0, 0.38f, 2.22f), new Vector3(1.95f, 0.15f, 0.08f), "Metal", collider: false);
            foreach (var x in new[] { -0.92f, 0.92f })
            foreach (var z in new[] { -1.35f, 1.35f })
                MeshPart("Wheel", car, cylinderMesh, new Vector3(x, 0.33f, z), new Vector3(0.66f, 0.12f, 0.66f), "Metal").transform.localRotation = Quaternion.Euler(0, 0, 90f);
            var lights = new List<Light>();
            foreach (var x in new[] { -0.6f, 0.6f })
            {
                Box("Headlamp", car, new Vector3(x, 0.62f, 2.21f), new Vector3(0.3f, 0.18f, 0.04f), "Bulb", collider: false);
                var lg = new GameObject("Headlight");
                lg.transform.SetParent(car, false);
                lg.transform.localPosition = new Vector3(x, 0.65f, 2.3f);
                lg.transform.localRotation = Quaternion.Euler(6f, 0, 0);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Spot;
                l.range = 38f;
                l.spotAngle = 55f;
                l.intensity = 30f;
                l.color = new Color(1f, 0.93f, 0.75f);
                l.shadows = LightShadows.None;
                l.enabled = false;
                lights.Add(l);
            }
            var seat = Group("Seat", car);
            seat.localPosition = new Vector3(-0.4f, 0.88f, -0.2f);
            var box = car.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0.75f, 0);
            box.size = new Vector3(1.9f, 1.3f, 4.4f);
            car.gameObject.AddComponent<World.DetectiveCar>().Configure(seat, lights.ToArray());
        }

        private static void BuildUnderground()
        {
            var underground = new GameObject("Underground").transform;
            foreach (var (id, house, halfDepth) in CellarSites)
            {
                var hatchPos = new Vector3(-3f, 0.06f, -halfDepth - 1.4f);
                var surfaceReturn = Group("Cellar Exit", house);
                surfaceReturn.localPosition = new Vector3(-3f, 0.1f, -halfDepth - 2.6f);
                surfaceReturn.localRotation = Quaternion.Euler(0, 180f, 0);
                BuildCellar(underground, id, house.position + Vector3.down * 40f, house.rotation, house, hatchPos, surfaceReturn, "Open the cellar hatch");
            }
            if (Anchors.TryGetValue("Church", out var church))
            {
                var exit = Group("Crypt Exit", church);
                exit.localPosition = new Vector3(-1.4f, 0.15f, -5.4f);
                BuildCellar(underground, "Crypt", church.position + Vector3.down * 40f, church.rotation, church, new Vector3(0f, 0.13f, -7.2f), exit, "Open the crypt hatch behind the altar");
            }
        }

        private static void BuildCellar(Transform parent, string id, Vector3 position, Quaternion rotation, Transform surface, Vector3 hatchLocal, Transform surfaceReturn, string hatchPrompt)
        {
            var room = Group(id, parent);
            room.position = position;
            room.rotation = rotation;
            const float w = 6f, d = 5f, h = 2.5f;
            Box("Floor", room, new Vector3(0, -0.1f, 0), new Vector3(w, 0.2f, d), "Dirt");
            Box("Ceiling", room, new Vector3(0, h + 0.1f, 0), new Vector3(w, 0.2f, d), "Wood");
            Box("Wall", room, new Vector3(0, h * 0.5f, d * 0.5f), new Vector3(w, h, 0.2f), "Stone");
            Box("Wall", room, new Vector3(0, h * 0.5f, -d * 0.5f), new Vector3(w, h, 0.2f), "Stone");
            Box("Wall", room, new Vector3(w * 0.5f, h * 0.5f, 0), new Vector3(0.2f, h, d), "Stone");
            Box("Wall", room, new Vector3(-w * 0.5f, h * 0.5f, 0), new Vector3(0.2f, h, d), "Stone");
            for (int i = 0; i < 3; i++)
            {
                Box("Shelf", room, new Vector3(-0.5f + i * 1.6f, 0.5f + (i % 2) * 0.6f, d * 0.5f - 0.3f), new Vector3(1.4f, 0.06f, 0.4f), "Wood", collider: false);
                foreach (var px in new[] { -0.65f, 0.65f })
                    Box("Shelf Post", room, new Vector3(-0.5f + i * 1.6f + px, (0.53f + (i % 2) * 0.6f) * 0.5f, d * 0.5f - 0.3f), new Vector3(0.05f, 0.53f + (i % 2) * 0.6f, 0.05f), "Wood", collider: false);
                for (int j = 0; j < 4; j++)
                    Box("Jar", room, new Vector3(-1f + i * 1.6f + j * 0.3f, 0.65f + (i % 2) * 0.6f, d * 0.5f - 0.3f), new Vector3(0.14f, 0.22f, 0.14f), j % 2 == 0 ? "Mustard" : "BarnRed", collider: false);
            }
            var ladder = Box("Ladder", room, new Vector3(-w * 0.5f + 0.5f, h * 0.5f, -d * 0.5f + 0.2f), new Vector3(0.6f, h, 0.1f), "Wood");
            ladder.GetComponent<Renderer>().enabled = false;
            foreach (var lx in new[] { -0.26f, 0.26f })
                Box("Ladder Rail", room, new Vector3(-w * 0.5f + 0.5f + lx, h * 0.5f, -d * 0.5f + 0.2f), new Vector3(0.05f, h, 0.05f), "Wood", collider: false);
            for (float ly = 0.3f; ly < h; ly += 0.32f)
                Box("Ladder Rung", room, new Vector3(-w * 0.5f + 0.5f, ly, -d * 0.5f + 0.2f), new Vector3(0.52f, 0.04f, 0.04f), "Wood", collider: false);
            Box("Hatch Light", room, new Vector3(-w * 0.5f + 0.5f, h + 0.08f, -d * 0.5f + 0.5f), new Vector3(0.9f, 0.04f, 0.7f), "Wood", collider: false);
            foreach (var (cx, cz, cs) in new[] { (2.3f, -1.6f, 0.6f), (2.4f, -0.9f, 0.45f), (-1.6f, 1.6f, 0.5f) })
            {
                Box("Crate", room, new Vector3(cx, cs * 0.5f, cz), Vector3.one * cs, "Wood");
                Box("Crate Slat", room, new Vector3(cx, cs * 0.8f, cz), new Vector3(cs + 0.01f, 0.05f, cs + 0.01f), "Bark", collider: false);
            }
            Box("Chain", room, new Vector3(w * 0.5f - 0.12f, 0.9f, 0.6f), new Vector3(0.04f, 0.9f, 0.04f), "Metal", collider: false);
            Box("Ring Bolt", room, new Vector3(w * 0.5f - 0.1f, 1.35f, 0.6f), new Vector3(0.06f, 0.1f, 0.1f), "Metal", collider: false);
            ladder.AddComponent<Investigation.CellarHatch>().Configure(surfaceReturn, "Climb back up", false);

            var arrive = Group("Arrival", room);
            arrive.localPosition = new Vector3(-w * 0.5f + 0.8f, 0.1f, -d * 0.5f + 0.9f);
            arrive.localRotation = Quaternion.Euler(0, 30f, 0);
            var victim = Group("Victim Spot", room);
            victim.localPosition = new Vector3(1.6f, 0.25f, 0.6f);
            victim.localRotation = Quaternion.Euler(0, -90f, 0);
            room.gameObject.AddComponent<Investigation.CellarRoom>().Configure(id, victim);

            var bulbGO = new GameObject("Bare Bulb");
            bulbGO.transform.SetParent(room, false);
            bulbGO.transform.localPosition = new Vector3(0.5f, h - 0.4f, 0);
            Box("Bulb", room, new Vector3(0.5f, h - 0.32f, 0), new Vector3(0.08f, 0.1f, 0.08f), "Bulb", collider: false);
            Box("Bulb Cord", room, new Vector3(0.5f, h - 0.13f, 0), new Vector3(0.012f, 0.28f, 0.012f), "Metal", collider: false);
            var reverb = bulbGO.AddComponent<AudioReverbZone>();
            reverb.reverbPreset = AudioReverbPreset.StoneCorridor;
            reverb.minDistance = 3f;
            reverb.maxDistance = 6f;
            var light = bulbGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 1.4f;
            light.color = new Color(1f, 0.8f, 0.55f);
            SetField(bulbGO.AddComponent<FlickeringLight>(), "flickerChance", 0.25f);

            var hatch = Box("Hatch", surface, hatchLocal, new Vector3(1.2f, 0.1f, 1.0f), "Wood");
            var trigger = hatch.GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.4f, 14f, 1.6f);
            // Two leaves in a timber frame, iron strap hinges, a ring pull, and a hasp for the padlock.
            var leaves = Group("Hatch Details", surface);
            leaves.localPosition = hatchLocal;
            foreach (var (pos, size) in new[] { (new Vector3(0, 0.02f, 0.55f), new Vector3(1.4f, 0.12f, 0.12f)), (new Vector3(0, 0.02f, -0.55f), new Vector3(1.4f, 0.12f, 0.12f)),
                                                (new Vector3(0.66f, 0.02f, 0), new Vector3(0.12f, 0.12f, 1.0f)), (new Vector3(-0.66f, 0.02f, 0), new Vector3(0.12f, 0.12f, 1.0f)) })
                Box("Hatch Frame", leaves, pos, size, "Bark", collider: false);
            Box("Seam", leaves, new Vector3(0, 0.052f, 0), new Vector3(0.025f, 0.01f, 0.98f), "WindowDark", collider: false);
            foreach (var x in new[] { -0.35f, 0.35f })
            foreach (var zz in new[] { -0.3f, 0.3f })
                Box("Strap", leaves, new Vector3(x, 0.055f, zz), new Vector3(0.5f, 0.012f, 0.06f), "Metal", collider: false);
            MeshPart("Ring Pull", leaves, cylinderMesh, new Vector3(-0.12f, 0.06f, 0), new Vector3(0.1f, 0.01f, 0.1f), "Metal");
            Box("Hasp", leaves, new Vector3(0.06f, 0.06f, 0), new Vector3(0.14f, 0.012f, 0.05f), "Metal", collider: false);
            var locked = Group("Padlock", leaves);
            locked.localPosition = new Vector3(0.09f, 0.09f, 0);
            Box("Lock Body", locked, new Vector3(0, 0, 0), new Vector3(0.06f, 0.05f, 0.03f), "Brass", collider: false).transform.localRotation = Quaternion.Euler(90f, 0, 0);
            Box("Shackle", locked, new Vector3(-0.035f, 0, 0), new Vector3(0.03f, 0.012f, 0.035f), "Chrome", collider: false);
            locked.gameObject.SetActive(false);
            var opened = Group("Padlock (open)", leaves);
            opened.localPosition = new Vector3(0.45f, 0.0f, -0.75f); // dropped in the dirt beside the hatch
            opened.localRotation = Quaternion.Euler(0, 35f, 0);
            Box("Lock Body", opened, new Vector3(0, 0.03f, 0), new Vector3(0.06f, 0.03f, 0.05f), "Brass", collider: false);
            Box("Shackle", opened, new Vector3(0, 0.03f, 0.045f), new Vector3(0.04f, 0.012f, 0.05f), "Chrome", collider: false);
            opened.gameObject.SetActive(false);
            var hatchComp = hatch.AddComponent<Investigation.CellarHatch>();
            hatchComp.Configure(arrive, hatchPrompt, true);
            SetField(hatchComp, "padlockVisual", locked.gameObject);
            SetField(hatchComp, "openPadlockVisual", opened.gameObject);
        }

        private static void Evidence(GameObject target, string id, string title, string text, string subject = null, string prompt = null, float shock = 0f)
        {
            foreach (var ins in target.GetComponents<InspectableObject>())
                Object.DestroyImmediate(ins);
            target.AddComponent<Investigation.EvidenceItem>().Configure(id, title, text, subject, prompt, shock);
        }

        private static GameObject EvidenceProp(Transform parent, string name, Vector3 localPos, Vector3 size, string material)
        {
            var go = Box(name, parent, localPos, size, Plain(material));
            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(Mathf.Max(1f, 0.6f / size.x), Mathf.Max(1f, 0.8f / size.y), Mathf.Max(1f, 0.6f / size.z));
            return go;
        }

        private static void BuildEvidence()
        {
            // Ruth Calder
            var headstone = GameObject.Find("Fresh Grave/Headstone");
            if (headstone != null)
                Evidence(headstone, "grave_headstone", "Chiselled headstone", "A fresh grave. A name was carved here but it's been chiselled away. Under the chisel marks you can just make out a capital R.", "Ruth Calder", "Read headstone");
            var mound = GameObject.Find("Fresh Grave/Mound");
            if (mound != null)
            {
                var box = mound.AddComponent<BoxCollider>();
                box.isTrigger = true;
                Evidence(mound, "grave_soil", "Disturbed earth", "The soil on the grave has been dug at with bare hands, from the top, again and again. Long, thin finger marks. Too long.", "Ruth Calder", "Examine the grave", 6f);
            }
            if (Anchors.TryGetValue("House:Calder", out var calder))
            {
                var shoes = EvidenceProp(calder, "Shoes", new Vector3(2.65f, 0.16f, 2.3f), new Vector3(0.8f, 0.12f, 0.3f), "PlainBarnRed");
                shoes.GetComponent<Renderer>().enabled = false; // the trigger; the shoes themselves are below
                string[] shoeColours = { "PlainBarnRed", "HideBlack", "PlainCream" };
                for (int pair = 0; pair < 3; pair++)
                    foreach (var dx in new[] { -0.055f, 0.055f })
                    {
                        float sx = 2.35f + pair * 0.3f + dx;
                        Box("Sole", calder, new Vector3(sx, FloorY + 0.012f, 2.3f), new Vector3(0.085f, 0.02f, 0.24f), "HideBlack", collider: false);
                        Box("Heel", calder, new Vector3(sx, FloorY + 0.04f, 2.2f), new Vector3(0.04f, 0.06f, 0.04f), "HideBlack", collider: false);
                        Box("Upper", calder, new Vector3(sx, FloorY + 0.075f, 2.24f), new Vector3(0.08f, 0.05f, 0.12f), shoeColours[pair], collider: false);
                        Box("Toe", calder, new Vector3(sx, FloorY + 0.045f, 2.36f), new Vector3(0.075f, 0.035f, 0.1f), shoeColours[pair], collider: false);
                        if (dx > 0) Box("Tag", calder, new Vector3(sx + 0.05f, FloorY + 0.07f, 2.22f), new Vector3(0.005f, 0.05f, 0.035f), "Paper", collider: false);
                    }
                Evidence(shoes,
                    "calder_shoes", "Women's shoes", "Three pairs of women's shoes, brand new, lined up neatly beside the bed. Size 6. The tags are still on.", "Amos Calder");
                Evidence(EvidenceProp(calder, "Certificate", new Vector3(-2.6f, 0.89f, -1.4f), new Vector3(0.3f, 0.02f, 0.4f), "SignWhite"),
                    "calder_certificate", "Unfiled death certificate", "Filled out in Doc Pruitt's handwriting: Ruth Ann Calder, died August 9th, 1983, heart failure. Never filed. Never signed.", "Ruth Calder", null, 6f);
            }

            // The cattle
            if (FarmFrames.TryGetValue("Kessler", out var kessler))
            {
                var (frame, def) = kessler;
                for (int i = 0; i < 3; i++)
                {
                    var cow = Group("Dead Cow", frame);
                    cow.localPosition = new Vector3(-9f - i * 3f, 0, def.DriveLength * (0.25f + i * 0.1f));
                    cow.localRotation = Quaternion.Euler(0, 30f + i * 50f, 0);
                    string hide = i == 0 ? "HideBrown" : i == 1 ? "HideWhite" : "HideBlack";
                    var body = Box("Body", cow, new Vector3(0, 1.1f, 0), new Vector3(0.9f, 0.85f, 2f), hide);
                    Box("Neck", cow, new Vector3(0, 1.2f, 1.0f), new Vector3(0.42f, 0.5f, 0.4f), hide, collider: false);
                    Box("Head", cow, new Vector3(0, 1.25f, 1.35f), new Vector3(0.4f, 0.45f, 0.55f), hide, collider: false);
                    Box("Muzzle", cow, new Vector3(0, 1.12f, 1.66f), new Vector3(0.32f, 0.24f, 0.12f), "HidePink", collider: false);
                    foreach (var x in new[] { -0.26f, 0.26f })
                        Box("Ear", cow, new Vector3(x, 1.42f, 1.2f), new Vector3(0.16f, 0.06f, 0.1f), hide, collider: false);
                    Box("Tail", cow, new Vector3(0, 0.95f, -1.03f), new Vector3(0.05f, 0.65f, 0.05f), hide, collider: false);
                    Box("Tail Tuft", cow, new Vector3(0, 0.6f, -1.03f), new Vector3(0.09f, 0.12f, 0.09f), "HideBlack", collider: false);
                    if (i == 1)
                        foreach (var (pp, ps) in new[] { (new Vector3(0.455f, 1.2f, 0.3f), new Vector3(0.01f, 0.4f, 0.6f)), (new Vector3(-0.455f, 1.05f, -0.4f), new Vector3(0.01f, 0.5f, 0.7f)),
                                                        (new Vector3(0.1f, 1.53f, -0.5f), new Vector3(0.5f, 0.01f, 0.6f)), (new Vector3(0, 1.48f, 1.35f), new Vector3(0.3f, 0.01f, 0.25f)) })
                            Box("Patch", cow, pp, ps, "HideBlack", collider: false);
                    foreach (var x in new[] { -0.3f, 0.3f })
                    foreach (var z in new[] { -0.7f, 0.7f })
                    {
                        Box("Leg", cow, new Vector3(x, 0.38f, z), new Vector3(0.15f, 0.66f, 0.15f), hide, collider: false);
                        Box("Hoof", cow, new Vector3(x, 0.04f, z), new Vector3(0.17f, 0.08f, 0.17f), "HideBlack", collider: false);
                    }
                    if (i == 0)
                        foreach (var hz in new[] { 1.05f, 1.11f })
                            Box("Puncture", cow, new Vector3(0.214f, 1.08f, hz), new Vector3(0.012f, 0.03f, 0.025f), "HideBlack", collider: false);
                    if (i == 0)
                        Evidence(body, "kessler_carcass", "Dead heifer", "Dead, but still standing on locked legs. Two neat holes behind the jaw, a finger's width apart. There's no blood on the ground. There's no blood anywhere.", "Greta Kessler", "Examine the cow", 10f);
                }
                var wireRight = Box("Bent Wire", frame, new Vector3(-5.72f, 1.04f, 0.1f), new Vector3(0.6f, 0.03f, 0.03f), "Galvanized", collider: false);
                wireRight.transform.localRotation = Quaternion.Euler(0, 0, -20f);
                var wireLeft = EvidenceProp(frame, "Bent Wire", new Vector3(-6.28f, 1.04f, 0.1f), new Vector3(0.6f, 0.03f, 0.03f), "Galvanized");
                wireLeft.transform.localRotation = Quaternion.Euler(0, 0, 20f);
                Evidence(wireLeft,
                    "kessler_fence", "Bent fence wire", "The top wire is bent upward from the inside, as if something stepped over it rather than through it. Something with very long legs.", "Greta Kessler");
            }
            if (Anchors.TryGetValue("Barn:Kessler", out var kBarn))
            {
                Crate(kBarn, new Vector3(0.6f, 0, 3f), 15f);
                var report = EvidenceProp(kBarn, "Vet Report", new Vector3(0.6f, FloorY + 0.615f, 3f), new Vector3(0.22f, 0.012f, 0.3f), "Paper");
                report.transform.localRotation = Quaternion.Euler(0, -10f, 0);
                Evidence(report,
                    "kessler_vet_report", "Vet's report", "Barlow County Veterinary: 'Exsanguination, total. Cause undetermined. Strongly recommend children are kept away from the herd.' Someone has underlined 'total' twice.", "Greta Kessler");
            }

            // The bell
            if (Anchors.TryGetValue("Church", out var church))
            {
                // Folded on the seat of the back pew, nearest the bell rope, where only the bell-ringer sits.
                var note = EvidenceProp(church, "Folded Note", new Vector3(3.3f, 0.561f, 4.45f), new Vector3(0.12f, 0.02f, 0.18f), "SignWhite");
                note.transform.localRotation = Quaternion.Euler(0, 23f, 0);
                Evidence(note, "bell_note", "Note on the back pew",
                    "'I ring it at three and count who comes running. They all come. Every one of them. 212. Then why does the sign say 213? Somebody paints it at night. I paint it back. It comes back.' Signed W.D.", "Wendell Dobbs");
            }
            var treeline = new GameObject("Tally Stump");
            var stump = Cylinder("Stump", treeline.transform, new Vector3(-31.6f, 0.25f, 39.2f), new Vector3(0.6f, 0.25f, 0.6f), "Bark");
            var tally = EvidenceProp(treeline.transform, "Tally Book", new Vector3(-31.6f, 0.53f, 39.2f), new Vector3(0.18f, 0.04f, 0.25f), "BarnRed");
            Evidence(tally, "wendell_tally", "Water-stained tally book", "Sundays going back years, every soul counted: 212, 212, 212... In September a new column: SIGN. 213, 213, 213, every morning, however many times he painted it back. Then a page in capitals: IT WANTS TO BE COUNTED. I HAVE WRITTEN TO THE CITY. The last line is pressed so hard it tore: WHICH ONE.", "Wendell Dobbs");
            treeline.transform.SetParent(GameObject.Find("Environment").transform, true);

            // The mirrors
            var rack = GameObject.Find("Empty Mirror Rack");
            if (rack != null)
                Evidence(rack, "store_rack", "Empty mirror rack", "A rack of empty hooks. A price tag still hangs from one: MIRROR, HAND, $2.49. Every one of them sold the same night.", null, "Inspect rack");
            if (Anchors.TryGetValue("Store", out var store))
                Evidence(EvidenceProp(store, "Receipt Book", new Vector3(2.95f, 1.025f, -1.45f), new Vector3(0.22f, 0.03f, 0.3f), "Paper"),
                    "store_receipt", "Receipt book", "In Otis's careful capitals: '14 HAND MIRRORS, 2 WALL MIRRORS, 1 QT. RED ENAMEL. 3 BRASS PADLOCKS. PAID CASH, EXACT CHANGE. LEFT ON COUNTER OVERNIGHT.' No name. The coins were cold, he's written. In August.", "Otis Pruitt");

            // The window
            if (Anchors.TryGetValue("Diner", out var diner))
            {
                for (int f = 0; f < 4; f++)
                {
                    var streak = Box("Finger Streak", diner, new Vector3(-4.75f + f * 0.09f, 1.95f - Mathf.Abs(f - 1.5f) * 0.03f, 4.545f), new Vector3(0.025f, 0.32f, 0.004f), "Smear", collider: false);
                    streak.transform.localRotation = Quaternion.Euler(0, 0, -8f + f * 4f);
                }
                Box("Palm Smudge", diner, new Vector3(-4.62f, 1.76f, 4.545f), new Vector3(0.16f, 0.1f, 0.004f), "Smear", collider: false);
                var marks = EvidenceProp(diner, "Finger Marks", new Vector3(-4.6f, 1.9f, 4.56f), new Vector3(0.5f, 0.5f, 0.02f), "Smear");
                marks.GetComponent<Renderer>().enabled = false;
                Evidence(marks,
                    "diner_window_marks", "Marks on the glass", "Long greasy finger marks on the outside of the window, seven feet up, as if someone very tall leaned close to look in. {teller} said Mrs. Calder. Ruth Calder was five foot one.", null, "Examine the window", 8f);
            }
        }

        private static readonly Dictionary<string, (Transform frame, FarmDef def)> FarmFrames = new Dictionary<string, (Transform, FarmDef)>();

        // ---------------------------------------------------------------- Schedules & navigation

        private static void Spot(Transform parent, string id, Vector3 localPosition, float localYaw)
        {
            var go = new GameObject("Spot " + id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0, localYaw, 0);
            go.AddComponent<Residents.ScheduleSpot>().Configure(id);
        }

        /// <summary>Named places residents can be scheduled at. Ids are referenced by profiles' schedules.</summary>
        private static void BuildScheduleSpots(Transform environment)
        {
            var spots = Group("Schedule Spots", environment);
            if (Anchors.TryGetValue("Diner", out var diner))
            {
                Spot(diner, "Diner:Counter", new Vector3(2f, 0.1f, -3.0f), 0f);
                Spot(diner, "Diner:Booth", new Vector3(-2.9f, 0.1f, 2.3f), 150f);
                Spot(diner, "Diner:Stool", new Vector3(0.8f, 0.1f, -0.7f), 180f);
                Spot(diner, "Diner:Window", new Vector3(3.6f, 0.1f, 2.6f), 200f);
            }
            if (Anchors.TryGetValue("Store", out var store))
            {
                Spot(store, "Store:Counter", new Vector3(3f, 0.1f, -2.5f), 0f);
                Spot(store, "Store:Aisle", new Vector3(-3f, 0.1f, 1.0f), 90f);
            }
            if (Anchors.TryGetValue("Church", out var church))
            {
                Spot(church, "Church:Altar", new Vector3(0f, 0.1f, -5.2f), 0f);
                Spot(church, "Church:Pew", new Vector3(-2.3f, 0.1f, 2.3f), 180f);
                Spot(church, "Church:Pew2", new Vector3(2.3f, 0.1f, -0.9f), 180f);
                int seat = 0;
                foreach (float z in new[] { 3.6f, 2.0f, 0.4f, -1.2f })
                foreach (float x in new[] { -3.2f, -2.3f, -1.4f, 1.4f, 2.3f, 3.2f })
                    Spot(church, "Church:Seat" + seat++, new Vector3(x, 0.1f, z), 180f);
            }
            if (Anchors.TryGetValue("Motel", out var motel))
                Spot(motel, "Motel:Lot", new Vector3(-5f, 0.05f, 7f), 0f);
            Spot(spots, "Churchyard", new Vector3(-19.5f, 0f, 55.2f), -53f);
            Spot(spots, "Treeline:Church", new Vector3(-31f, 0f, 38f), 90f);
            Spot(spots, "Square", new Vector3(-5.2f, 0.15f, 8f), 90f);
            Spot(spots, "Highway:South", new Vector3(-2.2f, 0.03f, -170f), 180f);

            foreach (var pair in FarmFrames)
            {
                string family = pair.Key;
                var (frame, def) = pair.Value;
                if (Anchors.TryGetValue("House:" + family, out var house))
                {
                    Spot(house, $"House:{family}:Porch", new Vector3(1.5f, 0.3f, 4.9f), 0f);
                    Spot(house, $"House:{family}:Inside", new Vector3(-1f, 0.1f, 0.2f), 0f);
                    Spot(house, $"House:{family}:Kitchen", new Vector3(-2.6f, 0.1f, -0.5f), 180f);
                }
                if (Anchors.TryGetValue("Barn:" + family, out var barn))
                    Spot(barn, $"Barn:{family}:Inside", new Vector3(0f, 0.1f, 2f), 0f);
                float side = def.CornSide != 0 ? def.CornSide : (def.HouseOffsetX >= 0 ? -1f : 1f);
                Vector3 field = def.CornSide != 0 ? new Vector3(def.CornSide * 3.6f, 0f, def.DriveLength * 0.55f) : new Vector3(side * 9f, 0f, def.DriveLength * 0.6f);
                Spot(frame, $"Field:{family}", field, 180f);
                Spot(frame, $"Gate:{family}", new Vector3(1.6f, 0f, 2.5f), 180f);
            }
        }

        private static void BakeNavMesh(Transform environment)
        {
            // Agents slimmer than Unity's default so they fit through 1.2 m doorways.
            var navSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
            if (navSettings.Length > 0)
            {
                var so = new SerializedObject(navSettings[0]);
                var settings = so.FindProperty("m_Settings");
                if (settings != null && settings.arraySize > 0)
                {
                    var agent = settings.GetArrayElementAtIndex(0);
                    agent.FindPropertyRelative("agentRadius").floatValue = 0.25f;
                    agent.FindPropertyRelative("agentHeight").floatValue = 1.75f;
                    agent.FindPropertyRelative("agentClimb").floatValue = 0.4f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // Doors are walked through (they open for residents); triggers aren't obstacles.
            foreach (var door in environment.GetComponentsInChildren<World.Door>(true))
                door.transform.parent.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            foreach (var col in environment.GetComponentsInChildren<Collider>(true))
                if (col.isTrigger && col.GetComponent<NavMeshModifier>() == null)
                    col.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            var surface = environment.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.12f;
            surface.overrideTileSize = true;
            surface.tileSize = 128;
            surface.BuildNavMesh();

            string path = SceneDir + "/Semblance_Town_NavMesh.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);
        }

        private static void FenceRun(MeshBatcher batch, Transform frame, Vector3 localA, Vector3 localB, Random rng)
        {
            float length = Vector3.Distance(localA, localB);
            var direction = (localB - localA).normalized;
            var railRotation = Quaternion.LookRotation(direction);
            for (float d = 0; d <= length + 0.01f; d += 2.5f)
            {
                var lean = Quaternion.Euler(Lerp(rng, -4f, 4f), 0, Lerp(rng, -4f, 4f));
                batch.AddBox(frame, "Wood", localA + direction * d + Vector3.up * 0.6f, new Vector3(0.14f, 1.2f, 0.14f), lean);
            }
            var center = (localA + localB) * 0.5f;
            batch.AddBox(frame, "Wood", center + Vector3.up * 0.95f, new Vector3(0.06f, 0.12f, length), railRotation);
            batch.AddBox(frame, "Wood", center + Vector3.up * 0.5f, new Vector3(0.06f, 0.12f, length), railRotation);
            Collider(frame, "Fence Collider", frame.TransformPoint(center + Vector3.up * 0.6f), frame.rotation * railRotation, new Vector3(0.2f, 1.2f, length));
        }

        private static void Cornfield(MeshBatcher batch, Transform frame, float xA, float xB, float zMin, float zMax, Random rng)
        {
            float xMin = Mathf.Min(xA, xB), xMax = Mathf.Max(xA, xB);
            for (float x = xMin; x <= xMax; x += 1.1f)
            for (float z = zMin; z <= zMax; z += 0.75f)
            {
                if (rng.NextDouble() < 0.12)
                    continue;
                float height = Lerp(rng, 1.7f, 2.4f);
                var position = new Vector3(x + Lerp(rng, -0.15f, 0.15f), height * 0.5f, z + Lerp(rng, -0.1f, 0.1f));
                var tilt = Quaternion.Euler(Lerp(rng, -6f, 6f), 0, Lerp(rng, -6f, 6f));
                batch.AddBox(frame, "Corn", position, new Vector3(0.08f, height, 0.08f), tilt);
                var leaf = Quaternion.Euler(Lerp(rng, -20f, 20f), Lerp(rng, 0f, 180f), Lerp(rng, -25f, 25f));
                batch.AddBox(frame, "CornLeaf", position + Vector3.up * Lerp(rng, -0.2f, 0.4f), new Vector3(0.7f, 0.03f, 0.12f), leaf);
            }
        }

        // ---------------------------------------------------------------- Forest

        private static void BuildForest(Transform parent)
        {
            var root = Group("Forest", parent);
            var colliders = Group("Tree Colliders", root);
            var rng = new Random(213);
            var chunks = new Dictionary<Vector2Int, MeshBatcher>();
            const float chunkSize = 80f;

            void AddTree(Vector3 position, float scale)
            {
                var key = new Vector2Int(Mathf.FloorToInt(position.x / chunkSize), Mathf.FloorToInt(position.z / chunkSize));
                if (!chunks.TryGetValue(key, out var batch))
                    chunks[key] = batch = new MeshBatcher($"Forest_{key.x}_{key.y}");
                var yaw = Quaternion.Euler(0, Lerp(rng, 0f, 360f), 0);
                batch.Add(cylinderMesh, "Bark", Matrix4x4.TRS(position + Vector3.up * scale, yaw, new Vector3(0.35f, 2f, 0.35f) * scale));
                batch.Add(coneMesh, "Pine", Matrix4x4.TRS(position + Vector3.up * 1.2f * scale, yaw, new Vector3(2.8f, 3.4f, 2.8f) * scale));
                batch.Add(coneMesh, "Pine", Matrix4x4.TRS(position + Vector3.up * 3.3f * scale, yaw, new Vector3(2f, 2.8f, 2f) * scale));

                var colliderGO = new GameObject("Tree", typeof(CapsuleCollider));
                colliderGO.transform.SetParent(colliders, false);
                colliderGO.transform.position = position + Vector3.up * 1.5f;
                var capsule = colliderGO.GetComponent<CapsuleCollider>();
                capsule.radius = 0.25f * scale;
                capsule.height = 3f;
            }

            // Clumped forest: dense where the noise is high, open meadows where it's low.
            for (int i = 0; i < 6000; i++)
            {
                float x = Lerp(rng, -WorldHalfSize, WorldHalfSize);
                float z = Lerp(rng, -WorldHalfSize, WorldHalfSize);
                float density = Mathf.PerlinNoise(x * 0.012f + 31.7f, z * 0.012f + 8.3f);
                if (density < 0.5f || rng.NextDouble() > (density - 0.5f) * 2.2f)
                    continue;
                if (IsExcluded(x, z))
                    continue;
                AddTree(new Vector3(x, 0, z), Lerp(rng, 0.8f, 1.7f));
            }
            // Tree lines along the roads, set back from the verge.
            for (int i = 0; i < 500; i++)
            {
                bool highway = rng.NextDouble() < 0.55;
                float along = Lerp(rng, highway ? -MainRoadHalfLength : -CrossRoadHalfLength, highway ? MainRoadHalfLength : CrossRoadHalfLength);
                float offset = (rng.NextDouble() < 0.5 ? -1f : 1f) * Lerp(rng, 11f, 18f);
                float x = highway ? offset : along;
                float z = highway ? along : offset;
                if (IsExcluded(x, z, ignoreRoads: true))
                    continue;
                AddTree(new Vector3(x, 0, z), Lerp(rng, 0.9f, 1.6f));
            }
            foreach (var position in ExtraTrees)
                AddTree(position, Lerp(rng, 1f, 1.5f));

            foreach (var batch in chunks.Values)
                batch.Build(root);
        }

        private static bool IsExcluded(float x, float z, bool ignoreRoads = false)
        {
            for (int i = ignoreRoads ? 2 : 0; i < TreeExclusions.Count; i++)
                if (TreeExclusions[i].Contains(new Vector2(x, z)))
                    return true;
            return false;
        }

        private static void BuildBoundaries(Transform parent)
        {
            var root = Group("Boundaries (invisible)", parent);
            float edge = WorldHalfSize - 5f;
            Collider(root, "North", new Vector3(0, 5, edge), Quaternion.identity, new Vector3(edge * 2, 10, 1));
            Collider(root, "South", new Vector3(0, 5, -edge), Quaternion.identity, new Vector3(edge * 2, 10, 1));
            Collider(root, "West", new Vector3(-edge, 5, 0), Quaternion.identity, new Vector3(1, 10, edge * 2));
            Collider(root, "East", new Vector3(edge, 5, 0), Quaternion.identity, new Vector3(1, 10, edge * 2));
        }

        // ---------------------------------------------------------------- Buildings

        private const float WallThickness = 0.2f;

        private static string PlaceName(string anchorKey)
        {
            switch (anchorKey)
            {
                case "Diner": return "Mae's Diner";
                case "Store": return "Semblance General";
                case "Church": return "First Church of Semblance";
                case "Motel": return "Pale Moon Motel";
            }
            if (anchorKey.StartsWith("House:")) return "the " + anchorKey.Substring(6) + " farmhouse";
            if (anchorKey.StartsWith("Barn:")) return "the " + anchorKey.Substring(5) + " barn";
            return anchorKey;
        }

        private static void AddMarker(Transform parent, string name, float radius, int priority, Vector3 localOffset = default)
        {
            var go = new GameObject("Location: " + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localOffset;
            go.AddComponent<World.LocationMarker>().Configure(name, radius, priority);
        }

        /// <summary>
        /// Builds an enterable building: hollow walls with door openings, floor, ceiling, working doors,
        /// windows that show on both sides, an interior light if anyone's home, and furniture.
        /// Local frame: +Z is the front, X runs along the front.
        /// </summary>
        private static Transform BuildBuilding(Transform parent, BuildingDef def, Vector3 position, float yaw, Random rng)
        {
            float width = def.Size.x, height = def.Size.y, depth = def.Size.z;
            const float t = WallThickness;
            var doorSize = def.DoorSize == Vector2.zero ? new Vector2(1.4f, 2.2f) : def.DoorSize;
            float ceiling = def.Interior == InteriorKind.Church || def.Interior == InteriorKind.Barn ? height : Mathf.Min(height, 3.2f);

            var root = Group(def.Name, parent);
            root.position = position;
            root.rotation = Quaternion.Euler(0, yaw, 0);
            if (!string.IsNullOrEmpty(def.AnchorKey))
            {
                Anchors[def.AnchorKey] = root;
                AddMarker(root, PlaceName(def.AnchorKey), Mathf.Max(width, depth) * 0.75f + 2f, 2);
            }

            // Shell
            float front = depth * 0.5f;
            float frontWallZ = front - t * 0.5f;
            Box("Wall Back", root, new Vector3(0, height * 0.5f, -front + t * 0.5f), new Vector3(width, height, t), def.Material);
            Box("Wall Left", root, new Vector3(-width * 0.5f + t * 0.5f, height * 0.5f, 0), new Vector3(t, height, depth - 2 * t), def.Material);
            Box("Wall Right", root, new Vector3(width * 0.5f - t * 0.5f, height * 0.5f, 0), new Vector3(t, height, depth - 2 * t), def.Material);

            var doorXs = new List<float>();
            for (int i = 0; i < def.Doors; i++)
                doorXs.Add(def.Doors == 1 ? 0f : Mathf.Lerp(-width * 0.5f + 2f, width * 0.5f - 2f, i / (float)(def.Doors - 1)));

            float cursor = -width * 0.5f;
            foreach (float doorX in doorXs)
            {
                float gapStart = doorX - doorSize.x * 0.5f, gapEnd = doorX + doorSize.x * 0.5f;
                if (gapStart - cursor > 0.01f)
                    Box("Wall Front", root, new Vector3((cursor + gapStart) * 0.5f, height * 0.5f, frontWallZ), new Vector3(gapStart - cursor, height, t), def.Material);
                Box("Lintel", root, new Vector3(doorX, doorSize.y + (height - doorSize.y) * 0.5f, frontWallZ), new Vector3(doorSize.x, height - doorSize.y, t), def.Material);
                cursor = gapEnd;
            }
            if (width * 0.5f - cursor > 0.01f)
                Box("Wall Front", root, new Vector3((cursor + width * 0.5f) * 0.5f, height * 0.5f, frontWallZ), new Vector3(width * 0.5f - cursor, height, t), def.Material);

            Box("Floor", root, new Vector3(0, 0.05f, 0), new Vector3(width - 2 * t, 0.1f, depth - 2 * t), def.Interior == InteriorKind.Barn ? "Dirt" : "Floor");
            Box("Ceiling", root, new Vector3(0, ceiling - 0.06f, 0), new Vector3(width - 2 * t, 0.12f, depth - 2 * t), "Ceiling", collider: false);

            if (def.Pitched)
                MeshPart("Roof", root, prismMesh, new Vector3(0, height, 0), new Vector3(width + 0.6f, Mathf.Min(depth * 0.45f, 5f), depth + 0.6f), "Roof");
            else
                Box("Roof", root, new Vector3(0, height + 0.15f, 0), new Vector3(width + 0.4f, 0.3f, depth + 0.4f), "Roof", collider: false);

            // Doors
            for (int i = 0; i < doorXs.Count; i++)
            {
                var hinge = Group("Door Hinge", root);
                hinge.localPosition = new Vector3(doorXs[i] - doorSize.x * 0.5f, 0, frontWallZ);
                var panel = Box("Door", hinge, new Vector3(doorSize.x * 0.5f, doorSize.y * 0.5f, 0), new Vector3(doorSize.x - 0.04f, doorSize.y - 0.02f, 0.07f), "Door");
                // A knob on each face, on the swinging side.
                foreach (var face in new[] { -1f, 1f })
                {
                    Box("Knob Rose", hinge, new Vector3(doorSize.x - 0.13f, 1.0f, face * 0.04f), new Vector3(0.07f, 0.07f, 0.012f), "Brass", collider: false);
                    Box("Knob", hinge, new Vector3(doorSize.x - 0.13f, 1.0f, face * 0.07f), new Vector3(0.05f, 0.05f, 0.05f), "Brass", collider: false);
                }
                if (def.Interior == InteriorKind.Motel && i == doorXs.Count - 1)
                {
                    // Room 6: "The key was already in the door when you arrived."
                    Box("Key", hinge, new Vector3(doorSize.x - 0.13f, 1.09f, 0.06f), new Vector3(0.012f, 0.05f, 0.02f), "Brass", collider: false);
                    Box("Key Tag", hinge, new Vector3(doorSize.x - 0.13f, 1.0f, 0.08f), new Vector3(0.05f, 0.08f, 0.008f), "PlainBarnRed", collider: false);
                }
                if (def.Interior == InteriorKind.Motel && i == 1)
                {
                    // Room 3: a DO NOT DISTURB card hung on the outside knob.
                    Box("DND Card", hinge, new Vector3(doorSize.x - 0.13f, 0.9f, 0.09f), new Vector3(0.1f, 0.17f, 0.005f), "Paper", collider: false);
                    var dnd = AddText(hinge, "DO NOT\nDISTURB", new Vector3(doorSize.x - 0.13f, 0.88f, 0.094f), new Vector2(0.09f, 0.1f), new Color(0.5f, 0.1f, 0.08f));
                    dnd.transform.SetParent(panel.transform, true);
                }
                var door = panel.AddComponent<World.Door>();
                SetField(door, "hinge", hinge);
                if (def.Locked)
                {
                    SetField(door, "locked", true);
                    SetField(door, "lockedMessage", def.LockedMessage ?? "It's locked.");
                }
                if (def.Interior == InteriorKind.Motel && i == 1)
                {
                    // Room 3 stays shut until the morning someone goes missing.
                    SetField(door, "locked", true);
                    SetField(door, "lockedMessage", "A card on the handle: DO NOT DISTURB. It's locked, and Room 3's key isn't on the board in the office.");
                    var gate = panel.AddComponent<World.DayLockedDoor>();
                    SetField(gate, "daysBeforeEnd", 2);
                    SetField(gate, "unlockToast", "Somebody hung Room 3's key back on the motel board in the night.");
                }
                if (def.Interior == InteriorKind.Motel)
                {
                    // Room number on the door itself, so it swings with it.
                    var number = AddText(hinge, (i + 2).ToString(), new Vector3(doorSize.x * 0.5f, 1.6f, 0.04f), new Vector2(0.4f, 0.3f), new Color(0.85f, 0.7f, 0.3f));
                    number.transform.SetParent(panel.transform, true);
                }
            }
            if (!string.IsNullOrEmpty(def.DoorDescription))
            {
                float plaqueX = doorXs[doorXs.Count - 1] + doorSize.x * 0.5f + 0.45f;
                var plaque = Box("Notice", root, new Vector3(plaqueX, 1.5f, front + 0.02f), new Vector3(0.45f, 0.55f, 0.04f), "SignWhite");
                AddInspectable(plaque, def.DoorPrompt, def.DoorDescription);
            }

            // Windows go right through the wall so they read from inside and out.
            bool anyLit = false;
            int floors = height >= 5.5f ? 2 : 1;
            for (int floor = 0; floor < floors; floor++)
            {
                float y = 1.6f + floor * 2.6f;
                if (y + 0.6f > height - 0.2f && !def.Pitched)
                    break;
                for (float x = -width * 0.5f + 1.4f; x <= width * 0.5f - 1.4f; x += 2.4f)
                {
                    if (floor == 0 && doorXs.Exists(d => Mathf.Abs(d - x) < doorSize.x * 0.5f + 0.8f))
                        continue;
                    bool lit = rng.NextDouble() < def.LitWindowChance;
                    anyLit |= lit && floor == 0;
                    Box("Window", root, new Vector3(x, y, frontWallZ), new Vector3(1f, 1.1f, t + 0.06f), lit ? "WindowLit" : "WindowDark", collider: false);
                    Box("Mullion", root, new Vector3(x, y, frontWallZ), new Vector3(0.06f, 1.1f, t + 0.1f), "Wood", collider: false);
                    Box("Transom", root, new Vector3(x, y + 0.05f, frontWallZ), new Vector3(1f, 0.06f, t + 0.1f), "Wood", collider: false);
                    Box("Sill", root, new Vector3(x, y - 0.58f, frontWallZ + 0.06f), new Vector3(1.15f, 0.07f, t + 0.16f), "Wood", collider: false);
                    if (lit && rng.NextDouble() < 0.5)
                        Box("Curtain", root, new Vector3(x + (rng.NextDouble() < 0.5 ? -0.3f : 0.3f), y, frontWallZ), new Vector3(0.38f, 1.06f, t + 0.08f), "Curtain", collider: false);
                }
            }

            if (!string.IsNullOrEmpty(def.Sign))
            {
                float boardWidth = Mathf.Min(width - 1f, 8f);
                Box("Sign", root, new Vector3(0, height - 0.7f, front + 0.1f), new Vector3(boardWidth, 0.9f, 0.12f), def.SignMaterial ?? "SignBoard", collider: false);
                AddText(root, def.Sign, new Vector3(0, height - 0.7f, front + 0.17f), new Vector2(boardWidth - 0.3f, 0.75f), new Color(0.9f, 0.85f, 0.7f));
            }

            Furnish(root, def, doorXs, ceiling, anyLit, rng);
            return root;
        }

        // ---------------------------------------------------------------- Lights, player, atmosphere

        /// <summary>Queues a lamp post whose arm points along the rotation's forward axis.</summary>
        private static void QueueLamp(Transform parent, string name, Vector3 position, Quaternion rotation, bool flicker)
        {
            DeferredLamps.Add(() =>
            {
                var lamp = Group(name, GameObject.Find("Lights").transform);
                lamp.position = position;
                lamp.rotation = rotation;
                Cylinder("Pole", lamp, new Vector3(0, 2.2f, 0), new Vector3(0.16f, 2.2f, 0.16f), "Metal");
                Box("Arm", lamp, new Vector3(0, 4.3f, 0.6f), new Vector3(0.08f, 0.08f, 1.2f), "Metal", collider: false);
                Box("Hood", lamp, new Vector3(0, 4.26f, 1.15f), new Vector3(0.4f, 0.14f, 0.62f), "Metal", collider: false);
                Box("Hood Lip", lamp, new Vector3(0, 4.17f, 1.15f), new Vector3(0.44f, 0.04f, 0.66f), "Metal", collider: false);
                var head = Box("Lamp Head", lamp, new Vector3(0, 4.14f, 1.15f), new Vector3(0.3f, 0.05f, 0.46f), "Bulb", collider: false);

                var lightGO = new GameObject("Light");
                lightGO.transform.SetParent(lamp, false);
                lightGO.transform.localPosition = new Vector3(0, 3.9f, 1.15f);
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.78f, 0.52f);
                light.range = 13f;
                light.intensity = 10f;
                light.shadows = LightShadows.None;

                if (flicker)
                {
                    var flickering = lightGO.AddComponent<FlickeringLight>();
                    SetField(flickering, "bulbRenderer", head.GetComponent<Renderer>());
                    SetField(flickering, "flickerChance", 0.35f);
                }
                lamp.gameObject.AddComponent<LampSwitch>().Configure(light, head.GetComponent<Renderer>());
                lamp.gameObject.AddComponent<Audio.LampBuzz>();
            });
        }

        /// <summary>Scene-wide systems: clock, notebook, records, UI screens. Later steps add more here.</summary>
        private static Transform BuildGameSystems()
        {
            new GameObject("Scene Boot").AddComponent<Core.SceneBoot>();
            var systems = new GameObject("Game Systems").transform;
            systems.gameObject.AddComponent<Core.GameClock>();
            systems.gameObject.AddComponent<Core.UIRoot>();
            systems.gameObject.AddComponent<Notes.Notebook>();
            systems.gameObject.AddComponent<Notes.NotebookUI>();
            var studio = systems.gameObject.AddComponent<Archive.PortraitStudio>();
            SetField(studio, "residentShader", litShader);
            systems.gameObject.AddComponent<Archive.ArchiveUI>();
            systems.gameObject.AddComponent<Case.AccusationUI>();
            systems.gameObject.AddComponent<Case.MimicAwareness>();
            systems.gameObject.AddComponent<Notes.SightingLog>();
            systems.gameObject.AddComponent<Audio.AmbientAudio>();
            systems.gameObject.AddComponent<Events.Nerve>();
            systems.gameObject.AddComponent<Events.EventDirector>();
            systems.gameObject.AddComponent<Case.TellNoticer>();
            systems.gameObject.AddComponent<Case.StoryBeats>();
            systems.gameObject.AddComponent<Core.ScreenFader>();
            systems.gameObject.AddComponent<UI.PauseMenu>();
            systems.gameObject.AddComponent<Core.IntroSequence>().Configure(GameObject.Find("Player").transform);
            systems.gameObject.AddComponent<Core.TutorialDirector>();
            var flow = systems.gameObject.AddComponent<Core.GameFlow>();
            Anchors.TryGetValue("WakeSpot", out var wakeSpot);
            flow.Configure(wakeSpot, GameObject.Find("Player").transform);
            systems.gameObject.AddComponent<Case.EndingDirector>();
            return systems;
        }

        private static Camera BuildPlayer(Vector3 position)
        {
            var player = new GameObject("Player");
            player.transform.position = position;
            player.tag = "Player";

            var controller = player.AddComponent<CharacterController>();
            controller.radius = 0.35f;
            controller.height = 1.75f;
            controller.center = new Vector3(0, 0.875f, 0);
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 45f;

            var cameraRoot = new GameObject("Camera Root").transform;
            cameraRoot.SetParent(player.transform, false);
            cameraRoot.localPosition = new Vector3(0, 1.6f, 0);

            var cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            cameraGO.transform.SetParent(cameraRoot, false);
            var camera = cameraGO.AddComponent<Camera>();
            camera.fieldOfView = 70f;
            int ghostLayer = LayerMask.NameToLayer("MirrorGhost");
            if (ghostLayer >= 0) camera.cullingMask = ~(1 << ghostLayer);
            camera.nearClipPlane = 0.05f;
            cameraGO.AddComponent<AudioListener>();
            var cameraData = cameraGO.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            var ps1 = cameraGO.AddComponent<PS1ScreenEffect>();
            SetField(ps1, "screenShader", Shader.Find("Semblance/PS1 Screen"));

            var fps = player.AddComponent<FirstPersonController>();
            SetField(fps, "cameraRoot", cameraRoot);

            var hudGO = new GameObject("HUD");
            hudGO.transform.SetParent(player.transform, false);
            var hud = hudGO.AddComponent<PlayerHUD>();
            hudGO.AddComponent<Dialogue.DialogueUI>();

            player.AddComponent<Audio.FootstepAudio>();
            player.AddComponent<Flashlight>();
            var photoCamera = player.AddComponent<PhotoCamera>();
            SetField(photoCamera, "viewCamera", camera);

            var interactor = player.AddComponent<PlayerInteractor>();
            SetField(interactor, "viewCamera", camera);
            SetField(interactor, "hud", hud);
            SetField(interactor, "controller", fps);
            return camera;
        }

        private static void BuildSkyAndAtmosphere(Camera playerCamera)
        {
            var moonGO = new GameObject("Moonlight");
            moonGO.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            var moon = moonGO.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.6f, 0.66f, 0.8f);
            moon.intensity = 0.1f;
            moon.shadows = LightShadows.None;
            RenderSettings.sun = moon;

            var atmosphereGO = new GameObject("Atmosphere");
            var atmosphere = atmosphereGO.AddComponent<SceneAtmosphere>();
            SetField(atmosphere, "targetCamera", playerCamera);
            atmosphere.Apply();
            atmosphereGO.AddComponent<DayNightCycle>().Configure(moon, playerCamera);
        }

        // ---------------------------------------------------------------- Materials

        private static void DefineMaterials()
        {
            Mat("Grass", 0.2f, 0.23f, 0.16f);
            Mat("Asphalt", 0.15f, 0.15f, 0.16f, depthOffset: -1f);
            Mat("Dirt", 0.3f, 0.25f, 0.19f, depthOffset: -1f);
            Mat("Sidewalk", 0.42f, 0.41f, 0.38f);
            Mat("RoadLine", 0.62f, 0.54f, 0.22f, depthOffset: -2f);
            Mat("Brick", 0.4f, 0.21f, 0.17f);
            Mat("Stone", 0.46f, 0.46f, 0.44f);
            Mat("Teal", 0.33f, 0.5f, 0.48f);
            Mat("White", 0.72f, 0.72f, 0.68f);
            Mat("Pink", 0.62f, 0.44f, 0.44f);
            Mat("Mustard", 0.56f, 0.46f, 0.25f);
            Mat("Sage", 0.4f, 0.46f, 0.36f);
            Mat("Blue", 0.3f, 0.36f, 0.46f);
            Mat("Cream", 0.64f, 0.58f, 0.46f);
            Mat("BarnRed", 0.42f, 0.14f, 0.11f);
            Mat("Roof", 0.17f, 0.14f, 0.13f);
            Mat("Door", 0.22f, 0.13f, 0.09f);
            Mat("WindowDark", 0.05f, 0.06f, 0.07f);
            Mat("WindowLit", 0.1f, 0.09f, 0.06f, new Color(1f, 0.72f, 0.38f) * 1.1f, fogPierce: 0.35f);
            Mat("SignBoard", 0.14f, 0.13f, 0.12f);
            Mat("SignNeon", 0.2f, 0.1f, 0.12f, new Color(0.9f, 0.25f, 0.35f) * 0.9f, fogPierce: 0.5f);
            Mat("SignWhite", 0.75f, 0.73f, 0.66f, new Color(0.22f, 0.22f, 0.2f));
            Mat("Wood", 0.34f, 0.24f, 0.15f);
            Mat("Metal", 0.12f, 0.12f, 0.13f);
            Mat("Bulb", 0.2f, 0.2f, 0.2f, new Color(1f, 0.82f, 0.58f) * 2f, fogPierce: 0.7f);
            Mat("Bark", 0.2f, 0.14f, 0.1f);
            Mat("Pine", 0.09f, 0.17f, 0.12f);
            Mat("Corn", 0.42f, 0.36f, 0.2f);
            Mat("CornLeaf", 0.33f, 0.31f, 0.17f);
            Mat("Floor", 0.26f, 0.18f, 0.12f);
            Mat("Ceiling", 0.32f, 0.3f, 0.27f);
            Mat("Counter", 0.7f, 0.68f, 0.6f);
            Mat("Fabric", 0.32f, 0.17f, 0.14f);
            Mat("Rug", 0.36f, 0.11f, 0.1f);
            Mat("Hay", 0.55f, 0.45f, 0.2f);
            Mat("CarPaint", 0.22f, 0.26f, 0.3f);
            Mat("Wallpaper", 0.4f, 0.38f, 0.3f);
            Mat("Vinyl", 0.46f, 0.13f, 0.12f);
            Mat("Curtain", 0.5f, 0.44f, 0.34f);
            Mat("MotelPaper", 0.38f, 0.31f, 0.27f);
            Mat("DinerWall", 0.55f, 0.53f, 0.46f);
            Mat("StoreWall", 0.46f, 0.42f, 0.32f);
            Mat("Screen", 0.08f, 0.1f, 0.11f, new Color(0.5f, 0.6f, 0.66f) * 0.7f, fogPierce: 0.2f);
            Mat("LampShade", 0.5f, 0.42f, 0.3f, new Color(1f, 0.72f, 0.42f) * 0.55f, fogPierce: 0.3f);
            Mat("Photo", 0.42f, 0.36f, 0.27f);
            Mat("Leather", 0.3f, 0.19f, 0.11f);
            Mat("Suit", 0.27f, 0.2f, 0.15f);
            // Plain (untextured) paints for small props: siding and plank textures belong on buildings, not tins and toys.
            Mat("PlainTeal", 0.33f, 0.5f, 0.48f);
            Mat("PlainWhite", 0.72f, 0.72f, 0.68f);
            Mat("PlainPink", 0.62f, 0.44f, 0.44f);
            Mat("PlainMustard", 0.56f, 0.46f, 0.25f);
            Mat("PlainSage", 0.4f, 0.46f, 0.36f);
            Mat("PlainBlue", 0.3f, 0.36f, 0.46f);
            Mat("PlainCream", 0.64f, 0.58f, 0.46f);
            Mat("PlainBarnRed", 0.42f, 0.14f, 0.11f);
            Mat("Glass", 0.15f, 0.2f, 0.23f, new Color(0.04f, 0.05f, 0.06f));
            Mat("Chrome", 0.6f, 0.61f, 0.63f, new Color(0.08f, 0.08f, 0.09f));
            Mat("Galvanized", 0.45f, 0.46f, 0.47f);
            Mat("Brass", 0.62f, 0.5f, 0.22f, new Color(0.1f, 0.08f, 0.03f));
            Mat("Bronze", 0.5f, 0.38f, 0.2f, new Color(0.08f, 0.06f, 0.03f));
            Mat("Rope", 0.52f, 0.43f, 0.28f);
            Mat("HideBrown", 0.33f, 0.2f, 0.13f);
            Mat("HideWhite", 0.74f, 0.72f, 0.67f);
            Mat("HideBlack", 0.08f, 0.07f, 0.07f);
            Mat("HidePink", 0.58f, 0.4f, 0.38f);
            Mat("Rust", 0.38f, 0.19f, 0.11f);
            Mat("Felt", 0.13f, 0.11f, 0.1f);
            Mat("Paper", 0.8f, 0.78f, 0.72f, new Color(0.2f, 0.2f, 0.18f));
            Mat("Ash", 0.36f, 0.35f, 0.34f);
            Mat("Smear", 0.6f, 0.6f, 0.56f, new Color(0.12f, 0.12f, 0.12f));
            Mat("Coffee", 0.12f, 0.07f, 0.04f);
            Mat("MapPaper", 0.44f, 0.56f, 0.42f);
            Mat("StoneScar", 0.64f, 0.63f, 0.6f);
            Mat("DirtDark", 0.14f, 0.11f, 0.08f, depthOffset: -1f);
            Mat("MothWing", 0.47f, 0.41f, 0.33f);
            Mat("Tape", 0.75f, 0.7f, 0.5f);
            Mat("PhoneSign", 0.75f, 0.82f, 0.86f, new Color(0.5f, 0.62f, 0.72f) * 0.7f, fogPierce: 0.3f);
        }

        /// <summary>Which generated texture each material uses, and how many tiles per meter.</summary>
        private static readonly Dictionary<string, (string texture, float scale)> TextureMap = new Dictionary<string, (string, float)>
        {
            { "Grass", ("grass", 0.25f) }, { "Asphalt", ("asphalt", 0.3f) }, { "Dirt", ("dirt", 0.3f) }, { "Sidewalk", ("slabs", 0.5f) },
            { "Brick", ("brick", 0.5f) }, { "Stone", ("stone", 0.5f) },
            { "Teal", ("siding", 0.28f) }, { "White", ("siding", 0.28f) }, { "Pink", ("siding", 0.28f) }, { "Mustard", ("siding", 0.28f) },
            { "Sage", ("siding", 0.28f) }, { "Blue", ("siding", 0.28f) }, { "Cream", ("siding", 0.28f) }, { "BarnRed", ("planks_vertical", 0.5f) },
            { "Roof", ("shingles", 0.4f) }, { "Door", ("planks_vertical", 0.9f) }, { "Wood", ("planks", 0.9f) }, { "Floor", ("planks", 0.7f) },
            { "Ceiling", ("planks", 0.5f) }, { "Bark", ("bark", 1.2f) }, { "Pine", ("needles", 0.8f) }, { "Corn", ("stalks", 1.5f) },
            { "CornLeaf", ("stalks", 1.5f) }, { "Hay", ("hay", 1f) }, { "Fabric", ("fabric", 2f) }, { "Rug", ("fabric", 1.5f) }, { "Curtain", ("fabric", 3f) },
        };

        private static void Mat(string name, float r, float g, float b, Color? emission = null, float fogPierce = 0f, float depthOffset = 0f)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(litShader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = litShader;
            material.SetColor("_BaseColor", new Color(r, g, b));
            material.SetColor("_EmissionColor", emission ?? Color.black);
            material.SetFloat("_FogPierce", fogPierce);
            // Negative offset pulls ground decals (roads, lines) toward the camera so they never z-fight the grass.
            material.SetFloat("_OffsetFactor", depthOffset);
            material.SetFloat("_OffsetUnits", depthOffset * 2f);
            material.enableInstancing = true;
            if (TextureMap.TryGetValue(name, out var mapping) && TextureSynth.Load(mapping.texture) is Texture2D tex)
            {
                material.SetTexture("_BaseMap", tex);
                material.SetFloat("_WorldUV", 1f);
                material.SetFloat("_WorldScale", mapping.scale);
                // Textured surfaces are the big ones (walls, floors, ground): affine warping on a 10 m quad makes the
                // siding bend as you look up and down. The PS1 look comes from the vertex snap, dither and low res.
                material.SetFloat("_AffineStrength", 0f);
                // Textures average a bit below white; lift the tint so the palette stays the same.
                material.SetColor("_BaseColor", new Color(r, g, b) * 1.18f);
            }
            else
            {
                material.SetTexture("_BaseMap", null);
                material.SetFloat("_WorldUV", 0f);
                material.SetFloat("_AffineStrength", 1f);
            }
            EditorUtility.SetDirty(material);
            Materials[name] = material;
        }

        // ---------------------------------------------------------------- Mesh batching

        /// <summary>Collects many small parts (fence posts, corn, tombstones, trees) and merges them into one mesh per material.</summary>
        private sealed class MeshBatcher
        {
            private readonly string name;
            private readonly Dictionary<string, List<CombineInstance>> parts = new Dictionary<string, List<CombineInstance>>();

            public MeshBatcher(string name) => this.name = name;

            public void Add(Mesh mesh, string material, Matrix4x4 worldMatrix)
            {
                if (!parts.TryGetValue(material, out var list))
                    parts[material] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh, transform = worldMatrix });
            }

            public void AddBox(string material, Vector3 worldCenter, Vector3 size, Quaternion worldRotation)
                => Add(cubeMesh, material, Matrix4x4.TRS(worldCenter, worldRotation, size));

            public void AddBox(Transform frame, string material, Vector3 localCenter, Vector3 size, Quaternion localRotation)
                => Add(cubeMesh, material, frame.localToWorldMatrix * Matrix4x4.TRS(localCenter, localRotation, size));

            /// <summary>Adds a flat, upward-facing rectangle split into tiles of at most <paramref name="tile"/> meters.</summary>
            public void AddTiledQuad(Transform frame, string material, Vector3 center, float sizeX, float sizeZ, float tile)
            {
                int countX = Mathf.Max(1, Mathf.CeilToInt(sizeX / tile));
                int countZ = Mathf.Max(1, Mathf.CeilToInt(sizeZ / tile));
                float cellX = sizeX / countX, cellZ = sizeZ / countZ;
                var toWorld = frame != null ? frame.localToWorldMatrix : Matrix4x4.identity;
                for (int i = 0; i < countX; i++)
                for (int j = 0; j < countZ; j++)
                {
                    var local = center + new Vector3(-sizeX * 0.5f + (i + 0.5f) * cellX, 0, -sizeZ * 0.5f + (j + 0.5f) * cellZ);
                    Add(quadMesh, material, toWorld * Matrix4x4.TRS(local, Quaternion.identity, new Vector3(cellX, 1, cellZ)));
                }
            }

            public void Build(Transform parent)
            {
                foreach (var pair in parts)
                {
                    var mesh = new Mesh { name = name + "_" + pair.Key, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                    mesh.RecalculateBounds();
                    BatchedMeshes.Add(mesh);
                    AssetDatabase.CreateAsset(mesh, $"{GeneratedMeshDir}/{mesh.name}.asset");
                    generatedMeshCount++;

                    var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(parent, true);
                    go.transform.position = Vector3.zero;
                    go.transform.rotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterial = Materials[pair.Key];
                }
            }
        }

        // ---------------------------------------------------------------- Object helpers

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, string material, bool collider = true)
            => Primitive(PrimitiveType.Cube, name, parent, localPos, size, material, collider);

        private static GameObject Cylinder(string name, Transform parent, Vector3 localPos, Vector3 scale, string material, bool collider = true)
            => Primitive(PrimitiveType.Cylinder, name, parent, localPos, scale, material, collider);

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, string material, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Materials[material];
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject MeshPart(string name, Transform parent, Mesh mesh, Vector3 localPos, Vector3 scale, string material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = Materials[material];
            return go;
        }

        private static void EnsureLayer(string name)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == name)
                    return;
            for (int i = 8; i < layers.arraySize; i++)
            {
                var element = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(element.stringValue)) continue;
                element.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
        }

        private static void Mirror(Transform parent, Vector3 localPosition, float localYaw, Vector2 size, string inspectText)
        {
            var mirrorRoot = Group("Mirror", parent);
            mirrorRoot.localPosition = localPosition;
            mirrorRoot.localRotation = Quaternion.Euler(0, localYaw, 0);
            Box("Mirror Frame", mirrorRoot, new Vector3(0, 0, -0.02f), new Vector3(size.x + 0.12f, size.y + 0.12f, 0.03f), "Wood", collider: false);
            var glass = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glass.name = "Mirror Glass";
            glass.transform.SetParent(mirrorRoot, false);
            glass.transform.localRotation = Quaternion.Euler(0, 180f, 0); // quads face -Z; turn to face the room
            glass.transform.localScale = new Vector3(size.x, size.y, 1f);
            var collider = glass.GetComponent<Collider>();
            Object.DestroyImmediate(collider);
            var box = glass.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 1f, 0.05f);
            if (!string.IsNullOrEmpty(inspectText))
                AddInspectable(glass, "Look in the mirror", inspectText);
            mirrorRoot.gameObject.AddComponent<PlanarMirror>().Configure(glass.GetComponent<Renderer>(), size);
        }

        private static void Zone(Transform parent, string name, Vector3 localCenter, Vector3 size, Audio.Surface surface, int priority)
        {
            var go = new GameObject("Surface: " + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.AddComponent<Audio.SurfaceZone>().Configure(surface, size, priority);
        }

        private static void Collider(Transform parent, string name, Vector3 worldCenter, Quaternion worldRotation, Vector3 size)
        {
            var go = new GameObject(name, typeof(BoxCollider));
            go.transform.SetParent(parent, false);
            go.transform.position = worldCenter;
            go.transform.rotation = worldRotation;
            go.GetComponent<BoxCollider>().size = size;
        }

        /// <param name="localRotation">Defaults to facing local +Z (TMP text faces -Z).</param>
        private static TextMeshPro AddText(Transform parent, string text, Vector3 localPos, Vector2 size, Color color, Quaternion? localRotation = null)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRotation ?? Quaternion.Euler(0, 180f, 0);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f;
            tmp.fontSizeMax = 12f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            // Painted lettering never runs to the edge of its board: leave a margin inside the given box.
            tmp.rectTransform.sizeDelta = new Vector2(size.x * 0.9f, size.y * 0.7f);
            return tmp;
        }

        private static void AddInspectable(GameObject target, string prompt, string description)
        {
            var inspectable = target.AddComponent<InspectableObject>();
            SetField(inspectable, "prompt", prompt);
            SetField(inspectable, "description", description);
        }

        private static void SetField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
                throw new ArgumentException($"Field '{field}' not found on {target.GetType().Name}");
            switch (value)
            {
                case Object obj: property.objectReferenceValue = obj; break;
                case string s: property.stringValue = s; break;
                case float f: property.floatValue = f; break;
                case int i: property.intValue = i; break;
                case bool b: property.boolValue = b; break;
                default: throw new ArgumentException("Unsupported value type " + value?.GetType());
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Ground, fields and small props don't need to cast the sun's shadow: nobody sees a jar's shadow through PS1 fog,
        /// and each caster is drawn again into every shadow cascade.
        /// </summary>
        private static readonly List<Mesh> BatchedMeshes = new List<Mesh>();

        private static void TrimShadowCasters()
        {
            // Batched meshes are render-only (no colliders or scripts read them, and the navmesh is already baked),
            // so the player doesn't need to keep a CPU copy of their vertices.
            foreach (var mesh in BatchedMeshes)
            {
                if (mesh == null) continue;
                var serialized = new SerializedObject(mesh);
                serialized.FindProperty("m_IsReadable").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(mesh);
            }
            BatchedMeshes.Clear();

            int trimmed = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                if (r.shadowCastingMode == ShadowCastingMode.Off) continue;
                var size = r.bounds.size;
                bool small = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < 0.3f;
                if (small || r.name.StartsWith("Surfaces_") || r.name.Contains("_Corn"))
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    trimmed++;
                }
            }
            Debug.Log($"[Semblance] Shadow casting turned off on {trimmed} small or flat renderers.");
        }

        private static void MarkStatic(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    // Not BatchingStatic: the GPU Resident Drawer instances the thousands of shared cubes better, and
                    // static batching would bypass it and copy every vertex into combined buffers at load.
                    StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        private static float Lerp(Random rng, float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());

        // ---------------------------------------------------------------- Procedural meshes

        private static Mesh GetOrCreateMesh(string name, Func<Mesh> create)
        {
            string path = MeshDir + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;
            mesh = create();
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>1x1 quad on the XZ plane, facing up.</summary>
        private static Mesh CreateQuadUp()
        {
            return BuildFlatMesh(new List<Vector3[]>
            {
                new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) },
            });
        }

        /// <summary>Gable roof: triangular prism with its ridge along X, base 1x1 at y=0, apex at y=1.</summary>
        private static Mesh CreatePrism()
        {
            var a = new Vector3(-0.5f, 0, -0.5f); var b = new Vector3(-0.5f, 0, 0.5f); var c = new Vector3(-0.5f, 1, 0);
            var d = new Vector3(0.5f, 0, -0.5f); var e = new Vector3(0.5f, 0, 0.5f); var f = new Vector3(0.5f, 1, 0);
            return BuildFlatMesh(new List<Vector3[]>
            {
                new[] { a, b, c },          // left gable
                new[] { d, f, e },          // right gable
                new[] { a, c, f, d },       // back slope
                new[] { b, e, f, c },       // front slope
                new[] { a, d, e, b },       // bottom
            });
        }

        /// <summary>Low-poly cone with base radius 0.5 at y=0 and tip at y=height.</summary>
        private static Mesh CreateCone(int segments, float angleOffset, float height)
        {
            var faces = new List<Vector3[]>();
            var tip = new Vector3(0, height, 0);
            var ring = Ring(segments, angleOffset, 0f);
            for (int i = 0; i < segments; i++)
                faces.Add(new[] { ring[i], tip, ring[(i + 1) % segments] });
            faces.Add(ring);
            return BuildFlatMesh(faces);
        }

        /// <summary>Low-poly cylinder, radius 0.5, from y=-1 to y=1 (matches Unity's cylinder primitive).</summary>
        private static Mesh CreateCylinder(int segments)
        {
            var faces = new List<Vector3[]>();
            var bottom = Ring(segments, 0f, -1f);
            var top = Ring(segments, 0f, 1f);
            for (int i = 0; i < segments; i++)
            {
                int n = (i + 1) % segments;
                faces.Add(new[] { bottom[i], top[i], top[n], bottom[n] });
            }
            var topCap = new Vector3[segments];
            for (int i = 0; i < segments; i++)
                topCap[i] = top[segments - 1 - i];
            faces.Add(topCap);
            faces.Add(bottom);
            return BuildFlatMesh(faces);
        }

        /// <summary>A church bell turned on a lathe: crown, waist, flared lip. 1 m across the mouth, 1 m tall, pivot at the lip.</summary>
        private static Mesh CreateBell()
        {
            var profile = new[] { (0.5f, 0f), (0.47f, 0.1f), (0.38f, 0.25f), (0.31f, 0.5f), (0.29f, 0.78f), (0.24f, 0.93f), (0.12f, 1f), (0f, 1.02f) };
            const int segments = 10;
            var faces = new List<Vector3[]>();
            Vector3[] RingAt(float r, float y)
            {
                var ring = Ring(segments, 0f, y);
                for (int i = 0; i < segments; i++) ring[i] = new Vector3(ring[i].x * r * 2f, y, ring[i].z * r * 2f);
                return ring;
            }
            for (int k = 0; k < profile.Length - 1; k++)
            {
                var lower = RingAt(profile[k].Item1, profile[k].Item2);
                var upper = RingAt(Mathf.Max(profile[k + 1].Item1, 0.001f), profile[k + 1].Item2);
                for (int i = 0; i < segments; i++)
                {
                    int n = (i + 1) % segments;
                    faces.Add(new[] { lower[i], upper[i], upper[n], lower[n] });
                }
            }
            // The mouth, seen from below: a dark disc a little way up inside.
            var mouth = RingAt(0.46f, 0.08f);
            faces.Add(mouth);
            return BuildFlatMesh(faces);
        }

        private static Vector3[] Ring(int segments, float angleOffset, float y)
        {
            var ring = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = (angleOffset + i * 360f / segments) * Mathf.Deg2Rad;
                ring[i] = new Vector3(Mathf.Cos(angle) * 0.5f, y, Mathf.Sin(angle) * 0.5f);
            }
            return ring;
        }

        /// <summary>Builds a flat-shaded mesh from convex polygons wound clockwise when seen from outside.</summary>
        private static Mesh BuildFlatMesh(List<Vector3[]> faces)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            foreach (var face in faces)
            {
                var normal = Vector3.Cross(face[1] - face[0], face[2] - face[0]).normalized;
                int start = vertices.Count;
                foreach (var v in face)
                {
                    vertices.Add(v);
                    normals.Add(normal);
                    uvs.Add(new Vector2(v.x + v.z, v.y));
                }
                for (int i = 1; i < face.Length - 1; i++)
                {
                    triangles.Add(start);
                    triangles.Add(start + i);
                    triangles.Add(start + i + 1);
                }
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path || s.path.EndsWith("Semblance_Street.unity") || s.path.EndsWith("SampleScene.unity"));
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
