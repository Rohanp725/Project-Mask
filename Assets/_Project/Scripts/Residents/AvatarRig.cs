using System.Collections.Generic;
using UnityEngine;

namespace Semblance.Residents
{
    /// <summary>
    /// A low-poly humanoid built from named boxes. Features look parts up by name ("EyeL", "FingerR3", "Nose")
    /// and move, scale, tint or remove them. The character faces +Z; their right side is +X.
    /// Finger indices run 0 = index to 3 = pinky.
    /// </summary>
    public class AvatarRig
    {
        public const float LegLength = 0.84f;

        public Transform Root { get; }
        public ResidentAppearance Look { get; }

        private readonly Dictionary<string, Transform> parts = new Dictionary<string, Transform>();
        private readonly Shader shader;
        private static readonly Dictionary<Color32, Material> MaterialCache = new Dictionary<Color32, Material>();
        private static Mesh cubeMesh;
        private static Mesh softMesh;

        /// <summary>Parts drawn with the bevelled mesh instead of a hard cube (heads, torsos, limbs).</summary>
        private static readonly System.Collections.Generic.HashSet<string> SoftParts = new System.Collections.Generic.HashSet<string>
        {
            "HeadShape", "Jaw", "Torso", "Pelvis", "Neck", "Hump", "LegLMesh", "LegRMesh", "UpperArmL", "UpperArmR",
            "ForearmLMesh", "ForearmRMesh", "FootL", "FootR", "HairTop", "HairBack", "HairBun", "Beard", "PalmL", "PalmR",
        };

        public static readonly Color EyeWhite = new Color(0.85f, 0.83f, 0.78f);
        public static readonly Color MouthColor = new Color(0.28f, 0.1f, 0.1f);

        private AvatarRig(Transform root, ResidentAppearance look, Shader shader)
        {
            Root = root;
            Look = look;
            this.shader = shader;
        }

        // ------------------------------------------------------------ Part access

        public Transform Part(string name) => parts.TryGetValue(name, out var part) && part != null ? part : null;

        /// <summary>"Eye" + Left => "EyeL". Unsided features default to the left.</summary>
        public static string Sided(string baseName, FeatureSide side) => baseName + (side == FeatureSide.Right ? "R" : "L");

        /// <summary>+1 for the character's right (+X), -1 for their left.</summary>
        public static float Sign(FeatureSide side) => side == FeatureSide.Right ? 1f : -1f;

        public void Move(string name, Vector3 delta) { var p = Part(name); if (p) p.localPosition += delta; }
        public void Scale(string name, Vector3 factor) { var p = Part(name); if (p) p.localScale = Vector3.Scale(p.localScale, factor); }
        public void Rotate(string name, Vector3 euler) { var p = Part(name); if (p) p.localRotation *= Quaternion.Euler(euler); }

        public void Tint(string name, Color color)
        {
            var p = Part(name);
            if (p == null) return;
            foreach (var r in p.GetComponentsInChildren<MeshRenderer>(true))
                r.sharedMaterial = GetMaterial(color);
        }

        public void Remove(string name)
        {
            var p = Part(name);
            if (p == null) return;
            foreach (var child in p.GetComponentsInChildren<Transform>(true))
                parts.Remove(child.name);
            DestroySafe(p.gameObject);
        }

        public Transform Pivot(string name, string parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent == null ? Root : Part(parent), false);
            go.transform.localPosition = localPos;
            parts[name] = go.transform;
            return go.transform;
        }

        public Transform Box(string name, string parent, Vector3 localPos, Vector3 size, Color color, Vector3 euler = default)
        {
            if (cubeMesh == null)
                cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent == null ? Root : Part(parent), false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = size;
            go.GetComponent<MeshFilter>().sharedMesh = SoftParts.Contains(name) ? SoftMesh : cubeMesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetMaterial(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            parts[name] = go.transform;
            return go.transform;
        }

        /// <summary>A unit cube with bevelled edges and corners, flat-shaded for a faceted PS1 look.</summary>
        private static Mesh SoftMesh
        {
            get
            {
                if (softMesh != null) return softMesh;
                const float r = 0.16f;
                float[] grid = { -0.5f, -0.5f + r, 0.5f - r, 0.5f };
                var vertices = new System.Collections.Generic.List<Vector3>();
                var triangles = new System.Collections.Generic.List<int>();
                // For each cube face, a 3x3 grid of quads pushed onto a rounded box.
                Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                foreach (var n in normals)
                {
                    Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                    Vector3 v = Vector3.Cross(n, u);
                    for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                    {
                        Vector3 Corner(int a, int b) => Round(n * 0.5f + u * grid[a] + v * grid[b], r);
                        var p0 = Corner(i, j); var p1 = Corner(i + 1, j); var p2 = Corner(i + 1, j + 1); var p3 = Corner(i, j + 1);
                        AddTri(vertices, triangles, p0, p2, p1, n);
                        AddTri(vertices, triangles, p0, p3, p2, n);
                    }
                }
                softMesh = new Mesh { name = "Soft Cube", hideFlags = HideFlags.HideAndDontSave };
                softMesh.SetVertices(vertices);
                softMesh.SetTriangles(triangles, 0);
                softMesh.RecalculateNormals();
                softMesh.RecalculateBounds();
                return softMesh;
            }
        }

        private static Vector3 Round(Vector3 p, float r)
        {
            var inner = new Vector3(Mathf.Clamp(p.x, -0.5f + r, 0.5f - r), Mathf.Clamp(p.y, -0.5f + r, 0.5f - r), Mathf.Clamp(p.z, -0.5f + r, 0.5f - r));
            var d = p - inner;
            return d.sqrMagnitude < 1e-8f ? p : inner + d.normalized * r;
        }

        private static void AddTri(System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 faceNormal)
        {
            // Keep outward winding (clockwise seen from outside in Unity).
            var normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, faceNormal + (a + b + c) / 3f) < 0f) { var t = b; b = c; c = t; }
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }

        public Material GetMaterial(Color color)
        {
            Color32 key = color;
            if (MaterialCache.TryGetValue(key, out var material) && material != null)
                return material;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, name = "Resident " + key };
            material.SetColor("_BaseColor", color);
            material.enableInstancing = true;
            MaterialCache[key] = material;
            return material;
        }

        public static void DestroySafe(Object obj)
        {
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }

        // ------------------------------------------------------------ Construction

        public static AvatarRig Create(Transform parent, ResidentAppearance look, Shader shader)
        {
            var rootGO = new GameObject("Rig");
            rootGO.transform.SetParent(parent, false);
            rootGO.transform.localScale = Vector3.one * (0.95f * look.height);
            var rig = new AvatarRig(rootGO.transform, look, shader);
            rig.BuildBody();
            return rig;
        }

        private void BuildBody()
        {
            var look = Look;
            float b = look.build;
            var pants = look.overalls ? new Color(0.22f, 0.3f, 0.45f) : look.pants;
            var shirt = look.clericalCollar ? new Color(0.07f, 0.07f, 0.08f) : look.shirt;
            var skinShade = look.skin * 0.88f;
            skinShade.a = 1f;

            // Legs and hips
            Pivot("Hips", null, new Vector3(0, LegLength, 0));
            foreach (var side in new[] { FeatureSide.Left, FeatureSide.Right })
            {
                float s = Sign(side);
                string leg = Sided("Leg", side);
                Pivot(leg, "Hips", new Vector3(s * 0.09f * b, 0, 0));
                Box(leg + "Mesh", leg, new Vector3(0, -LegLength * 0.5f, 0), new Vector3(0.15f * b, LegLength, 0.17f), pants);
                Box(Sided("Foot", side), leg, new Vector3(0, -LegLength + 0.04f, 0.04f), new Vector3(0.13f, 0.08f, 0.26f), look.shoes);
            }
            Box("Pelvis", "Hips", new Vector3(0, 0.02f, 0), new Vector3(0.34f * b, 0.16f, 0.22f * b), pants);

            // Torso
            Pivot("Spine", "Hips", new Vector3(0, 0.08f, 0));
            Box("Torso", "Spine", new Vector3(0, 0.26f, 0), new Vector3(0.4f * b, 0.52f, 0.24f * b), shirt);

            // Arms and hands
            foreach (var side in new[] { FeatureSide.Left, FeatureSide.Right })
            {
                float s = Sign(side);
                string arm = Sided("Arm", side), forearm = Sided("Forearm", side), hand = Sided("Hand", side);
                Pivot(arm, "Spine", new Vector3(s * (0.2f * b + 0.065f), 0.48f, 0));
                Box(Sided("UpperArm", side), arm, new Vector3(0, -0.15f, 0), new Vector3(0.11f, 0.3f, 0.11f), shirt);
                Pivot(forearm, arm, new Vector3(0, -0.3f, 0));
                Box(forearm + "Mesh", forearm, new Vector3(0, -0.135f, 0), new Vector3(0.1f, 0.27f, 0.1f), shirt);
                Pivot(hand, forearm, new Vector3(0, -0.27f, 0));
                Box(Sided("Palm", side), hand, new Vector3(0, -0.045f, 0), new Vector3(0.04f, 0.09f, 0.09f), look.skin);
                for (int i = 0; i < 4; i++)
                {
                    float z = 0.03f - i * 0.02f; // index finger at the front, pinky at the back
                    Box(Sided("Finger", side) + i, hand, new Vector3(0, -0.12f, z), new Vector3(0.022f, 0.06f, 0.016f), look.skin);
                }
                Box(Sided("Thumb", side), hand, new Vector3(-s * 0.012f, -0.06f, 0.055f), new Vector3(0.02f, 0.045f, 0.02f), look.skin, new Vector3(-25f, 0, 0));
            }

            // Neck and head
            Pivot("NeckPivot", "Spine", new Vector3(0, 0.52f, 0));
            Box("Neck", "NeckPivot", new Vector3(0, 0.05f, 0), new Vector3(0.1f, 0.1f, 0.1f), look.skin);
            var head = Pivot("Head", "NeckPivot", new Vector3(0, 0.1f, 0));
            head.localScale = Vector3.one * look.headSize;
            Box("HeadShape", "Head", new Vector3(0, 0.15f, 0), new Vector3(0.22f, 0.25f, 0.25f), look.skin);
            Box("Jaw", "Head", new Vector3(0, 0.04f, 0.01f), new Vector3(0.19f, 0.07f, 0.22f), look.skin);

            foreach (var side in new[] { FeatureSide.Left, FeatureSide.Right })
            {
                float s = Sign(side);
                string eye = Sided("Eye", side);
                Pivot(eye, "Head", new Vector3(s * 0.05f, 0.16f, 0.126f));
                Box(eye + "White", eye, Vector3.zero, new Vector3(0.045f, 0.026f, 0.008f), EyeWhite);
                Box(Sided("Pupil", side), eye, new Vector3(0, 0, 0.005f), new Vector3(0.018f, 0.02f, 0.006f), look.eyeColor);
                Box(Sided("Brow", side), "Head", new Vector3(s * 0.05f, 0.192f, 0.128f), new Vector3(0.055f, 0.012f, 0.01f), look.hairColor);
                Box(Sided("Ear", side), "Head", new Vector3(s * 0.118f, 0.14f, 0), new Vector3(0.02f, 0.06f, 0.04f), skinShade);
            }
            Pivot("Nose", "Head", new Vector3(0, 0.125f, 0.125f));
            Box("NoseMesh", "Nose", new Vector3(0, 0, 0.018f), new Vector3(0.035f, 0.055f, 0.04f), skinShade);
            Box("Mouth", "Head", new Vector3(0, 0.07f, 0.126f), new Vector3(0.07f, 0.012f, 0.008f), MouthColor);

            BuildHair();
            BuildAccessories(shirt);
        }

        private void BuildHair()
        {
            var look = Look;
            var c = look.hairColor;
            Pivot("Hair", "Head", Vector3.zero);
            switch (look.hair)
            {
                case HairStyle.Short:
                    Box("HairTop", "Hair", new Vector3(0, 0.285f, -0.005f), new Vector3(0.235f, 0.05f, 0.26f), c);
                    Box("HairBack", "Hair", new Vector3(0, 0.21f, -0.125f), new Vector3(0.235f, 0.14f, 0.03f), c);
                    break;
                case HairStyle.Long:
                    Box("HairTop", "Hair", new Vector3(0, 0.285f, -0.005f), new Vector3(0.24f, 0.05f, 0.265f), c);
                    Box("HairBack", "Hair", new Vector3(0, 0.12f, -0.13f), new Vector3(0.24f, 0.34f, 0.04f), c);
                    Box("HairSideL", "Hair", new Vector3(-0.12f, 0.15f, -0.03f), new Vector3(0.02f, 0.27f, 0.18f), c);
                    Box("HairSideR", "Hair", new Vector3(0.12f, 0.15f, -0.03f), new Vector3(0.02f, 0.27f, 0.18f), c);
                    break;
                case HairStyle.Bun:
                    Box("HairTop", "Hair", new Vector3(0, 0.282f, -0.005f), new Vector3(0.235f, 0.045f, 0.26f), c);
                    Box("HairBack", "Hair", new Vector3(0, 0.2f, -0.125f), new Vector3(0.235f, 0.16f, 0.03f), c);
                    Box("HairBun", "Hair", new Vector3(0, 0.3f, -0.12f), new Vector3(0.1f, 0.09f, 0.09f), c);
                    break;
                case HairStyle.Combover:
                    Box("HairTop", "Hair", new Vector3(0.01f, 0.277f, 0), new Vector3(0.23f, 0.02f, 0.2f), c, new Vector3(0, 0, -4f));
                    Box("HairBack", "Hair", new Vector3(0, 0.18f, -0.125f), new Vector3(0.235f, 0.1f, 0.03f), c);
                    break;
                case HairStyle.Wild:
                    Box("HairTop", "Hair", new Vector3(0, 0.3f, -0.01f), new Vector3(0.29f, 0.09f, 0.3f), c, new Vector3(0, 8f, 3f));
                    Box("HairBack", "Hair", new Vector3(0, 0.18f, -0.14f), new Vector3(0.28f, 0.22f, 0.05f), c);
                    Box("HairSideL", "Hair", new Vector3(-0.13f, 0.2f, -0.02f), new Vector3(0.04f, 0.16f, 0.2f), c, new Vector3(0, 0, 10f));
                    Box("HairSideR", "Hair", new Vector3(0.13f, 0.2f, -0.02f), new Vector3(0.04f, 0.16f, 0.2f), c, new Vector3(0, 0, -10f));
                    break;
            }
            if (look.beard)
                Box("Beard", "Head", new Vector3(0, 0.035f, 0.1f), new Vector3(0.2f, 0.1f, 0.08f), c);
            if (look.mustache)
                Box("Mustache", "Head", new Vector3(0, 0.088f, 0.131f), new Vector3(0.085f, 0.016f, 0.014f), c);
        }

        private void BuildAccessories(Color shirt)
        {
            var look = Look;
            float b = look.build;
            var dark = new Color(0.08f, 0.08f, 0.08f);

            if (look.glasses)
            {
                // Wire frames: four thin bars around each lens so the eyes stay visible.
                Pivot("Glasses", "Head", new Vector3(0, 0.16f, 0.136f));
                foreach (var side in new[] { FeatureSide.Left, FeatureSide.Right })
                {
                    string lens = Sided("Glasses", side);
                    Pivot(lens, "Glasses", new Vector3(Sign(side) * 0.05f, 0, 0));
                    Box(lens + "Top", lens, new Vector3(0, 0.02f, 0), new Vector3(0.06f, 0.005f, 0.004f), dark);
                    Box(lens + "Bottom", lens, new Vector3(0, -0.02f, 0), new Vector3(0.06f, 0.005f, 0.004f), dark);
                    Box(lens + "Inner", lens, new Vector3(-Sign(side) * 0.0275f, 0, 0), new Vector3(0.005f, 0.04f, 0.004f), dark);
                    Box(lens + "Outer", lens, new Vector3(Sign(side) * 0.0275f, 0, 0), new Vector3(0.005f, 0.04f, 0.004f), dark);
                }
                Box("GlassesBridge", "Glasses", new Vector3(0, 0.008f, 0), new Vector3(0.04f, 0.005f, 0.004f), dark);
            }
            switch (look.hat)
            {
                case HatStyle.Cap:
                    Box("HatCrown", "Head", new Vector3(0, 0.3f, 0), new Vector3(0.245f, 0.075f, 0.27f), look.hatColor);
                    Box("HatBill", "Head", new Vector3(0, 0.272f, 0.17f), new Vector3(0.2f, 0.015f, 0.11f), look.hatColor * 0.8f);
                    break;
                case HatStyle.Fedora:
                    Box("HatBrim", "Head", new Vector3(0, 0.285f, 0), new Vector3(0.37f, 0.015f, 0.37f), look.hatColor);
                    Box("HatCrown", "Head", new Vector3(0, 0.335f, 0), new Vector3(0.23f, 0.1f, 0.23f), look.hatColor);
                    break;
                case HatStyle.Bonnet:
                    Box("HatCrown", "Head", new Vector3(0, 0.2f, -0.03f), new Vector3(0.27f, 0.25f, 0.24f), look.hatColor);
                    break;
            }
            if (look.apron)
                Box("Apron", "Spine", new Vector3(0, 0.12f, 0.123f * b), new Vector3(0.36f * b, 0.55f, 0.015f), new Color(0.82f, 0.8f, 0.74f));
            if (look.overalls)
            {
                var denim = new Color(0.22f, 0.3f, 0.45f);
                Box("OverallBib", "Spine", new Vector3(0, 0.25f, 0.123f * b), new Vector3(0.26f * b, 0.24f, 0.015f), denim);
                Box("OverallStrapL", "Spine", new Vector3(-0.09f * b, 0.44f, 0.123f * b), new Vector3(0.04f, 0.16f, 0.015f), denim);
                Box("OverallStrapR", "Spine", new Vector3(0.09f * b, 0.44f, 0.123f * b), new Vector3(0.04f, 0.16f, 0.015f), denim);
            }
            if (look.clericalCollar)
                Box("Collar", "NeckPivot", new Vector3(0, 0.03f, 0.052f), new Vector3(0.05f, 0.03f, 0.008f), Color.white);
        }
    }
}
