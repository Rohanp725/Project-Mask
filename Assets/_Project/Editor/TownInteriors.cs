using System.Collections.Generic;
using Semblance.Interaction;
using Semblance.Rendering;
using UnityEngine;
using Random = System.Random;

namespace Semblance.EditorTools
{
    /// <summary>
    /// Interiors: a small furniture kit (each piece is several low-poly parts with one simple collider, so the
    /// navmesh sees the same plain footprints as before), wall linings, light fixtures, and the furnishing of
    /// every building type, including the story details in the motel's empty rooms and each family's house.
    /// </summary>
    public static partial class TownBuilder
    {
        // ---------------------------------------------------------------- Kit

        /// <summary>A furniture group. Its parts have no colliders; one box collider covers the whole footprint.</summary>
        private static Transform Piece(string name, Transform root, Vector3 localPos, float yaw, Vector3 colliderSize, float colliderBase = 0f)
        {
            var piece = Group(name, root);
            piece.localPosition = localPos;
            piece.localRotation = Quaternion.Euler(0, yaw, 0);
            if (colliderSize != Vector3.zero)
            {
                var box = piece.gameObject.AddComponent<BoxCollider>();
                box.size = colliderSize;
                box.center = new Vector3(0, colliderBase + colliderSize.y * 0.5f, 0);
            }
            return piece;
        }

        private static GameObject Part(string name, Transform piece, Vector3 pos, Vector3 size, string material)
            => Box(name, piece, pos, size, Plain(material), collider: false);

        /// <summary>A round part (six-sided, PS1 style) standing upright: diameter and height in meters.</summary>
        private static GameObject Round(string name, Transform piece, Vector3 pos, float diameter, float height, string material)
            => MeshPart(name, piece, cylinderMesh, pos, new Vector3(diameter, height * 0.5f, diameter), Plain(material));

        private static readonly HashSet<string> SidingPaints = new HashSet<string> { "Teal", "White", "Pink", "Mustard", "Sage", "Blue", "Cream", "BarnRed" };

        /// <summary>
        /// Building paints carry siding and plank textures; on a tin, a toy or a blanket they read as tiny
        /// clapboards. Props get the same colour without the texture.
        /// </summary>
        private static string Plain(string material) => SidingPaints.Contains(material) ? "Plain" + material : material;

        /// <summary>A sheet of paper taped to a wall (facing <paramref name="facing"/>), with crayon figures on it.</summary>
        private static GameObject Drawing(Transform root, Vector3 pos, float facing, Random rng, int figures, bool extraFigure)
        {
            var sheet = Group("Drawing", root);
            sheet.localPosition = pos;
            sheet.localRotation = Quaternion.Euler(0, facing, Lerp(rng, -4f, 4f));
            var paper = Part("Paper", sheet, Vector3.zero, new Vector3(0.3f, 0.22f, 0.004f), "Paper");
            foreach (var cx in new[] { -0.14f, 0.14f })
                Part("Tape", sheet, new Vector3(cx, 0.1f, 0.004f), new Vector3(0.05f, 0.025f, 0.002f), "Tape");
            string[] crayons = { "BarnRed", "Blue", "Mustard", "Sage" };
            Part("House", sheet, new Vector3(0.09f, -0.04f, 0.004f), new Vector3(0.08f, 0.07f, 0.002f), "Mustard");
            Part("Sun", sheet, new Vector3(0.11f, 0.07f, 0.004f), new Vector3(0.03f, 0.03f, 0.002f), "Mustard");
            for (int i = 0; i < figures; i++)
            {
                float x = -0.12f + i * 0.045f, tall = i < 2 ? 0.09f : 0.06f;
                Part("Figure", sheet, new Vector3(x, -0.07f + tall * 0.5f, 0.004f), new Vector3(0.012f, tall, 0.002f), crayons[i % crayons.Length]);
                Part("Head", sheet, new Vector3(x, -0.06f + tall + 0.012f, 0.004f), new Vector3(0.022f, 0.022f, 0.002f), crayons[i % crayons.Length]);
            }
            if (extraFigure)
            {
                // The fifth grown-up, a little behind the others, in grey.
                Part("Figure", sheet, new Vector3(-0.135f, -0.015f, 0.003f), new Vector3(0.014f, 0.12f, 0.002f), "Metal");
                Part("Head", sheet, new Vector3(-0.135f, 0.06f, 0.003f), new Vector3(0.025f, 0.025f, 0.002f), "Metal");
            }
            return paper;
        }

        /// <summary>A shallow glass-fronted insect case on a wall, with pinned moths (or just a pin and a label).</summary>
        private static GameObject MothCase(Transform root, Vector3 pos, float facing, Random rng, int moths)
        {
            var box = Group("Moth Case", root);
            box.localPosition = pos;
            box.localRotation = Quaternion.Euler(0, facing, 0);
            Part("Frame", box, Vector3.zero, new Vector3(0.5f, 0.38f, 0.05f), "Wood");
            var back = Part("Backing", box, new Vector3(0, 0, 0.026f), new Vector3(0.44f, 0.32f, 0.004f), "Cream");
            Part("Glass", box, new Vector3(0, 0, 0.03f), new Vector3(0.44f, 0.32f, 0.002f), "Glass").GetComponent<Renderer>().enabled = moths > 0 && rng.NextDouble() < 0.3;
            for (int m = 0; m < moths; m++)
            {
                float x = -0.15f + (m % 3) * 0.15f, y = moths > 3 ? (m < 3 ? 0.07f : -0.07f) : 0f;
                foreach (var side in new[] { -1f, 1f })
                {
                    var wing = Part("Wing", box, new Vector3(x + side * 0.022f, y, 0.03f), new Vector3(0.04f, 0.028f, 0.003f), "MothWing");
                    wing.transform.localRotation = Quaternion.Euler(0, 0, side * 18f);
                }
                Part("Body", box, new Vector3(x, y, 0.031f), new Vector3(0.008f, 0.03f, 0.004f), "Bark");
                Part("Label", box, new Vector3(x, y - 0.045f, 0.03f), new Vector3(0.04f, 0.012f, 0.002f), "Paper");
            }
            if (moths == 0)
            {
                Part("Pin", box, new Vector3(0, 0.02f, 0.035f), new Vector3(0.004f, 0.004f, 0.02f), "Chrome");
                Part("Label", box, new Vector3(0, -0.06f, 0.03f), new Vector3(0.12f, 0.025f, 0.002f), "Paper");
            }
            return back;
        }

        /// <summary>Makes something inspectable, adding a small trigger if it has no collider of its own.</summary>
        private static void Inspect(GameObject target, string prompt, string text, Vector3? triggerSize = null)
        {
            if (target.GetComponent<Collider>() == null)
            {
                var box = target.AddComponent<BoxCollider>();
                box.isTrigger = true;
                if (triggerSize.HasValue)
                {
                    var s = target.transform.lossyScale;
                    box.size = new Vector3(triggerSize.Value.x / Mathf.Max(s.x, 1e-3f), triggerSize.Value.y / Mathf.Max(s.y, 1e-3f), triggerSize.Value.z / Mathf.Max(s.z, 1e-3f));
                }
            }
            AddInspectable(target, prompt, text);
        }

        private static void Legs(Transform piece, float w, float d, float h, float thick, string material, float inset = 0.04f)
        {
            foreach (var x in new[] { -w * 0.5f + inset, w * 0.5f - inset })
            foreach (var z in new[] { -d * 0.5f + inset, d * 0.5f - inset })
                Part("Leg", piece, new Vector3(x, h * 0.5f, z), new Vector3(thick, h, thick), material);
        }

        // Interior floors are 0.1 m thick boxes, so furniture stands on y = 0.1.
        private const float FloorY = 0.1f;

        private static Transform Table(string name, Transform root, Vector3 pos, float yaw, float w, float d, string top = "Wood", float height = 0.78f)
        {
            var t = Piece(name, root, pos, yaw, new Vector3(w, height, d), FloorY);
            Part("Top", t, new Vector3(0, FloorY + height - 0.025f, 0), new Vector3(w, 0.05f, d), top);
            Part("Apron", t, new Vector3(0, FloorY + height - 0.09f, 0), new Vector3(w - 0.12f, 0.08f, d - 0.12f), "Wood");
            Legs(t, w, d, height - 0.05f, 0.06f, "Wood", 0.08f);
            foreach (var leg in t.GetComponentsInChildren<Transform>())
                if (leg.name == "Leg") leg.localPosition += Vector3.up * FloorY;
            return t;
        }

        private static Transform Chair(Transform root, Vector3 pos, float yaw, string seat = "Wood")
        {
            var c = Piece("Chair", root, pos, yaw, new Vector3(0.45f, 0.9f, 0.45f), FloorY);
            Part("Seat", c, new Vector3(0, FloorY + 0.45f, 0), new Vector3(0.44f, 0.05f, 0.42f), seat);
            foreach (var x in new[] { -0.18f, 0.18f })
            foreach (var z in new[] { -0.17f, 0.17f })
                Part("Leg", c, new Vector3(x, FloorY + 0.22f, z), new Vector3(0.04f, 0.44f, 0.04f), "Wood");
            foreach (var x in new[] { -0.18f, 0.18f })
                Part("Back Post", c, new Vector3(x, FloorY + 0.7f, -0.19f), new Vector3(0.04f, 0.5f, 0.04f), "Wood");
            Part("Back Rail", c, new Vector3(0, FloorY + 0.88f, -0.19f), new Vector3(0.4f, 0.1f, 0.03f), "Wood");
            Part("Back Rail", c, new Vector3(0, FloorY + 0.65f, -0.19f), new Vector3(0.4f, 0.05f, 0.03f), "Wood");
            return c;
        }

        private static Transform Bed(string name, Transform root, Vector3 pos, float yaw, string blanket = "Fabric", bool made = true, float width = 1.4f)
        {
            // Local +z is the foot of the bed; the headboard is at -z.
            var b = Piece(name, root, pos, yaw, new Vector3(width, 0.55f, 2.1f), FloorY);
            Part("Frame", b, new Vector3(0, FloorY + 0.22f, 0), new Vector3(width + 0.05f, 0.18f, 2.12f), "Wood");
            Legs(b, width, 2.05f, 0.14f, 0.07f, "Wood", 0.05f);
            foreach (var leg in b.GetComponentsInChildren<Transform>())
                if (leg.name == "Leg") leg.localPosition += Vector3.up * FloorY;
            Part("Mattress", b, new Vector3(0, FloorY + 0.4f, 0.02f), new Vector3(width - 0.06f, 0.18f, 2.0f), "SignWhite");
            Part("Headboard", b, new Vector3(0, FloorY + 0.55f, -1.06f), new Vector3(width + 0.1f, 1.0f, 0.07f), "Wood");
            Part("Headboard Cap", b, new Vector3(0, FloorY + 1.07f, -1.06f), new Vector3(width + 0.16f, 0.05f, 0.11f), "Wood");
            Part("Footboard", b, new Vector3(0, FloorY + 0.4f, 1.06f), new Vector3(width + 0.1f, 0.5f, 0.06f), "Wood");
            if (made)
            {
                Part("Pillow", b, new Vector3(0, FloorY + 0.54f, -0.78f), new Vector3(width * 0.7f, 0.1f, 0.34f), "SignWhite");
                Part("Blanket", b, new Vector3(0, FloorY + 0.5f, 0.25f), new Vector3(width - 0.02f, 0.05f, 1.45f), blanket);
                Part("Blanket Fold", b, new Vector3(0, FloorY + 0.52f, -0.42f), new Vector3(width - 0.02f, 0.06f, 0.16f), "SignWhite");
            }
            return b;
        }

        private static Transform Nightstand(Transform root, Vector3 pos, float yaw, bool lampLit = false)
        {
            var n = Piece("Nightstand", root, pos, yaw, new Vector3(0.45f, 0.58f, 0.4f), FloorY);
            Part("Body", n, new Vector3(0, FloorY + 0.29f, 0), new Vector3(0.45f, 0.55f, 0.4f), "Wood");
            Part("Top", n, new Vector3(0, FloorY + 0.575f, 0), new Vector3(0.48f, 0.03f, 0.43f), "Wood");
            Part("Drawer", n, new Vector3(0, FloorY + 0.42f, 0.205f), new Vector3(0.38f, 0.14f, 0.015f), "Door");
            Part("Knob", n, new Vector3(0, FloorY + 0.42f, 0.22f), new Vector3(0.04f, 0.03f, 0.03f), "Mustard");
            Round("Lamp Base", n, new Vector3(0.08f, FloorY + 0.65f, -0.04f), 0.1f, 0.12f, "Metal");
            Part("Lamp Stem", n, new Vector3(0.08f, FloorY + 0.77f, -0.04f), new Vector3(0.025f, 0.18f, 0.025f), "Metal");
            Round("Lamp Shade", n, new Vector3(0.08f, FloorY + 0.9f, -0.04f), 0.24f, 0.16f, lampLit ? "LampShade" : "Cream");
            return n;
        }

        private static Transform Dresser(Transform root, Vector3 pos, float yaw, float width = 1.1f)
        {
            var d = Piece("Dresser", root, pos, yaw, new Vector3(width, 0.8f, 0.45f), FloorY);
            Part("Body", d, new Vector3(0, FloorY + 0.4f, 0), new Vector3(width, 0.78f, 0.45f), "Wood");
            Part("Top", d, new Vector3(0, FloorY + 0.8f, 0), new Vector3(width + 0.04f, 0.03f, 0.48f), "Wood");
            for (int i = 0; i < 3; i++)
            {
                Part("Drawer", d, new Vector3(0, FloorY + 0.16f + i * 0.22f, 0.228f), new Vector3(width - 0.1f, 0.18f, 0.012f), "Door");
                foreach (var x in new[] { -width * 0.25f, width * 0.25f })
                    Part("Handle", d, new Vector3(x, FloorY + 0.16f + i * 0.22f, 0.24f), new Vector3(0.1f, 0.025f, 0.02f), "Mustard");
            }
            return d;
        }

        /// <summary>A wood-cased 1970s television with rabbit ears, standing on something <paramref name="surfaceY"/> high.</summary>
        private static GameObject Television(Transform parent, Vector3 pos, float surfaceY, bool on)
        {
            var tv = Group("Television", parent);
            tv.localPosition = pos;
            Part("Cabinet", tv, new Vector3(0, surfaceY + 0.24f, 0), new Vector3(0.62f, 0.46f, 0.45f), "Wood");
            var screen = Part("Screen", tv, new Vector3(-0.06f, surfaceY + 0.25f, 0.226f), new Vector3(0.42f, 0.33f, 0.01f), on ? "Screen" : "WindowDark");
            foreach (var y in new[] { 0.34f, 0.26f, 0.18f })
                Round("Dial", tv, new Vector3(0.22f, surfaceY + y, 0.228f), 0.05f, 0.02f, "Metal").transform.localRotation = Quaternion.Euler(90f, 0, 0);
            var ear = Part("Antenna", tv, new Vector3(-0.08f, surfaceY + 0.62f, 0), new Vector3(0.012f, 0.4f, 0.012f), "Metal");
            ear.transform.localRotation = Quaternion.Euler(0, 0, 25f);
            ear = Part("Antenna", tv, new Vector3(0.08f, surfaceY + 0.62f, 0), new Vector3(0.012f, 0.4f, 0.012f), "Metal");
            ear.transform.localRotation = Quaternion.Euler(0, 0, -25f);
            return screen;
        }

        private static Transform Couch(Transform root, Vector3 pos, float yaw, string fabric = "Fabric", float width = 2.2f)
        {
            var c = Piece("Couch", root, pos, yaw, new Vector3(width, 0.85f, 0.9f), FloorY);
            Part("Base", c, new Vector3(0, FloorY + 0.2f, 0), new Vector3(width, 0.3f, 0.88f), fabric);
            for (int i = 0; i < 2; i++)
                Part("Cushion", c, new Vector3((i - 0.5f) * (width - 0.4f) * 0.5f, FloorY + 0.43f, 0.06f), new Vector3((width - 0.42f) * 0.5f - 0.02f, 0.16f, 0.7f), fabric);
            Part("Back", c, new Vector3(0, FloorY + 0.6f, -0.34f), new Vector3(width, 0.55f, 0.2f), fabric);
            foreach (var x in new[] { -width * 0.5f + 0.1f, width * 0.5f - 0.1f })
                Part("Arm", c, new Vector3(x, FloorY + 0.48f, 0.02f), new Vector3(0.2f, 0.3f, 0.86f), fabric);
            foreach (var x in new[] { -width * 0.5f + 0.1f, width * 0.5f - 0.1f })
                Part("Foot", c, new Vector3(x, FloorY + 0.03f, 0.35f), new Vector3(0.06f, 0.06f, 0.06f), "Wood");
            return c;
        }

        private static Transform Stove(Transform root, Vector3 pos, float yaw)
        {
            var s = Piece("Stove", root, pos, yaw, new Vector3(0.76f, 0.92f, 0.66f), FloorY);
            Part("Body", s, new Vector3(0, FloorY + 0.45f, 0), new Vector3(0.76f, 0.88f, 0.64f), "Counter");
            Part("Cooktop", s, new Vector3(0, FloorY + 0.9f, 0), new Vector3(0.76f, 0.03f, 0.64f), "Metal");
            foreach (var x in new[] { -0.18f, 0.18f })
            foreach (var z in new[] { -0.14f, 0.14f })
                Round("Burner", s, new Vector3(x, FloorY + 0.925f, z), 0.18f, 0.015f, "WindowDark");
            Part("Backsplash", s, new Vector3(0, FloorY + 1.05f, -0.31f), new Vector3(0.76f, 0.28f, 0.03f), "Counter");
            Part("Oven Door", s, new Vector3(0, FloorY + 0.4f, 0.325f), new Vector3(0.6f, 0.45f, 0.015f), "Metal");
            Part("Oven Window", s, new Vector3(0, FloorY + 0.45f, 0.334f), new Vector3(0.36f, 0.18f, 0.01f), "WindowDark");
            Part("Handle", s, new Vector3(0, FloorY + 0.66f, 0.345f), new Vector3(0.5f, 0.025f, 0.03f), "Metal");
            for (int i = 0; i < 4; i++)
                Round("Knob", s, new Vector3(-0.27f + i * 0.18f, FloorY + 1.05f, -0.285f), 0.045f, 0.02f, "Metal").transform.localRotation = Quaternion.Euler(90f, 0, 0);
            return s;
        }

        private static Transform Cupboard(Transform root, Vector3 pos, float yaw, float width = 2f)
        {
            var c = Piece("Cupboard", root, pos, yaw, new Vector3(width, 2f, 0.5f), FloorY);
            Part("Base Cabinet", c, new Vector3(0, FloorY + 0.45f, 0.02f), new Vector3(width, 0.88f, 0.5f), "Wood");
            Part("Worktop", c, new Vector3(0, FloorY + 0.9f, 0.03f), new Vector3(width + 0.04f, 0.04f, 0.54f), "Counter");
            int doors = Mathf.Max(2, Mathf.RoundToInt(width / 0.5f));
            for (int i = 0; i < doors; i++)
            {
                float x = -width * 0.5f + (i + 0.5f) * width / doors;
                Part("Door", c, new Vector3(x, FloorY + 0.45f, 0.275f), new Vector3(width / doors - 0.05f, 0.76f, 0.012f), "Door");
                Part("Pull", c, new Vector3(x + (i % 2 == 0 ? 0.15f : -0.15f), FloorY + 0.7f, 0.285f), new Vector3(0.025f, 0.08f, 0.02f), "Metal");
            }
            Part("Upper Shelf", c, new Vector3(0, FloorY + 1.55f, -0.12f), new Vector3(width, 0.03f, 0.26f), "Wood");
            Part("Upper Shelf", c, new Vector3(0, FloorY + 1.9f, -0.12f), new Vector3(width, 0.03f, 0.26f), "Wood");
            for (int i = 0; i < Mathf.RoundToInt(width * 3); i++)
            {
                float x = -width * 0.5f + 0.15f + i * 0.33f;
                Part("Plate", c, new Vector3(x, FloorY + 1.66f, -0.18f), new Vector3(0.22f, 0.22f, 0.02f), "SignWhite").transform.localRotation = Quaternion.Euler(-10f, 0, 0);
                Round("Jar", c, new Vector3(x + 0.08f, FloorY + 2.0f, -0.1f), 0.1f, 0.16f, i % 3 == 0 ? "Mustard" : i % 3 == 1 ? "BarnRed" : "Sage");
            }
            return c;
        }

        private static Transform Shelving(string name, Transform root, Vector3 pos, float yaw, float length, float height, int boards, Random rng, bool goods = true, bool doubleSided = false)
        {
            if (name == "Bookshelf") return Bookshelf(root, pos, yaw, length, height, boards, rng);
            float depth = doubleSided ? 0.7f : 0.4f;
            var s = Piece(name, root, pos, yaw, new Vector3(length, height, depth), FloorY);
            foreach (var x in new[] { -length * 0.5f + 0.03f, length * 0.5f - 0.03f })
                Part("Upright", s, new Vector3(x, FloorY + height * 0.5f, 0), new Vector3(0.05f, height, depth), "Wood");
            if (!doubleSided)
                Part("Backboard", s, new Vector3(0, FloorY + height * 0.5f, -depth * 0.5f + 0.01f), new Vector3(length, height, 0.02f), "Wood");
            else
                Part("Spine", s, new Vector3(0, FloorY + height * 0.5f, 0), new Vector3(length, height, 0.03f), "Wood");
            string[] mats = { "Mustard", "BarnRed", "Blue", "Sage", "Cream", "SignWhite", "Teal" };
            for (int b = 0; b < boards; b++)
            {
                float y = FloorY + 0.08f + b * (height - 0.1f) / boards;
                Part("Board", s, new Vector3(0, y, 0), new Vector3(length - 0.06f, 0.03f, depth), "Wood");
                if (!goods) continue;
                foreach (float face in doubleSided ? new[] { -1f, 1f } : new[] { 1f })
                {
                    float z = doubleSided ? face * depth * 0.25f : 0.02f;
                    for (float x = -length * 0.5f + 0.15f; x < length * 0.5f - 0.12f; x += Lerp(rng, 0.14f, 0.3f))
                    {
                        if (rng.NextDouble() < 0.15) { x += 0.15f; continue; } // gaps where stock has run low
                        string mat = mats[rng.Next(mats.Length)];
                        if (rng.NextDouble() < 0.55)
                            Round("Can", s, new Vector3(x, y + 0.08f, z), 0.09f, 0.13f, mat);
                        else
                        {
                            float h = Lerp(rng, 0.14f, 0.26f);
                            Part("Box", s, new Vector3(x, y + 0.015f + h * 0.5f, z), new Vector3(0.12f, h, Lerp(rng, 0.08f, 0.16f)), mat);
                        }
                    }
                }
            }
            return s;
        }

        /// <summary>A bookcase: spines packed tight, a few leaning, the odd gap, a stack lying flat.</summary>
        private static Transform Bookshelf(Transform root, Vector3 pos, float yaw, float length, float height, int boards, Random rng)
        {
            const float depth = 0.32f;
            var s = Piece("Bookshelf", root, pos, yaw, new Vector3(length, height, depth), FloorY);
            foreach (var x in new[] { -length * 0.5f + 0.03f, length * 0.5f - 0.03f })
                Part("Upright", s, new Vector3(x, FloorY + height * 0.5f, 0), new Vector3(0.05f, height, depth), "Wood");
            Part("Backboard", s, new Vector3(0, FloorY + height * 0.5f, -depth * 0.5f + 0.01f), new Vector3(length, height, 0.02f), "Wood");
            Part("Top", s, new Vector3(0, FloorY + height, 0), new Vector3(length + 0.04f, 0.04f, depth + 0.02f), "Wood");
            string[] spines = { "BarnRed", "Blue", "Sage", "Mustard", "Leather", "Cream", "Suit", "Felt" };
            for (int b = 0; b < boards; b++)
            {
                float y = FloorY + 0.04f + b * (height - 0.08f) / boards;
                Part("Board", s, new Vector3(0, y, 0), new Vector3(length - 0.06f, 0.03f, depth), "Wood");
                float x = -length * 0.5f + 0.07f, end = length * 0.5f - 0.07f;
                while (x < end)
                {
                    float w = Lerp(rng, 0.025f, 0.055f), h = Lerp(rng, 0.17f, 0.27f);
                    if (rng.NextDouble() < 0.06) { x += 0.12f; continue; } // a gap
                    if (rng.NextDouble() < 0.05 && x + 0.22f < end)
                    {
                        for (int k = 0; k < 3; k++) // a short stack lying flat
                            Part("Book", s, new Vector3(x + 0.1f, y + 0.035f + k * 0.035f, 0.02f), new Vector3(0.2f, 0.033f, 0.16f), spines[rng.Next(spines.Length)]);
                        x += 0.24f;
                        continue;
                    }
                    if (x + w > end) break;
                    var book = Part("Book", s, new Vector3(x + w * 0.5f, y + 0.015f + h * 0.5f, 0.02f), new Vector3(w, h, Lerp(rng, 0.15f, 0.2f)), spines[rng.Next(spines.Length)]);
                    x += w + 0.003f;
                    if (rng.NextDouble() < 0.04 && x + 0.1f < end)
                    {
                        book.transform.localRotation = Quaternion.Euler(0, 0, -14f); // leaning into the gap
                        book.transform.localPosition += new Vector3(0.025f, -0.008f, 0);
                        x += 0.05f;
                    }
                }
            }
            return s;
        }

        /// <summary>A framed picture on a wall. <paramref name="facing"/> is the yaw the picture faces.</summary>
        private static GameObject Picture(Transform root, Vector3 pos, float facing, Vector2 size, string inner = "Photo")
        {
            var frame = Group("Picture", root);
            frame.localPosition = pos;
            frame.localRotation = Quaternion.Euler(0, facing, 0);
            Part("Frame", frame, Vector3.zero, new Vector3(size.x + 0.06f, size.y + 0.06f, 0.025f), "Wood");
            var picture = Part("Picture", frame, new Vector3(0, 0, 0.014f), new Vector3(size.x, size.y, 0.01f), inner);
            if (inner == "Photo")
            {
                // A suggestion of people in the print: a darker band of figures and a pale sky.
                Part("Sky", frame, new Vector3(0, size.y * 0.3f, 0.02f), new Vector3(size.x * 0.92f, size.y * 0.3f, 0.005f), "Cream");
                for (int i = 0; i < 3; i++)
                    Part("Figure", frame, new Vector3((i - 1) * size.x * 0.25f, -size.y * 0.12f, 0.021f), new Vector3(size.x * 0.12f, size.y * 0.5f, 0.005f), "SignBoard");
            }
            return picture;
        }

        /// <summary>A leather case (suitcase, sample case, doctor's bag) lying flat, with a handle and latches. Returns the body.</summary>
        private static GameObject Case(string name, Transform parent, Vector3 pos, Vector3 size, float yaw = 0f)
        {
            var g = Group(name, parent);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            var body = Part(name, g, Vector3.zero, size, "Leather");
            Part("Seam", g, new Vector3(0, size.y * 0.15f, 0), new Vector3(size.x + 0.01f, 0.015f, size.z + 0.01f), "Bark");
            Part("Handle", g, new Vector3(0, size.y * 0.5f + 0.02f, size.z * 0.5f - 0.03f), new Vector3(size.x * 0.3f, 0.04f, 0.025f), "Bark");
            foreach (var x in new[] { -size.x * 0.32f, size.x * 0.32f })
                Part("Latch", g, new Vector3(x, size.y * 0.2f, size.z * 0.5f + 0.006f), new Vector3(0.04f, 0.05f, 0.012f), "Mustard");
            return body;
        }

        private static Transform Crate(Transform root, Vector3 pos, float yaw, float size = 0.6f)
        {
            var c = Piece("Crate", root, pos, yaw, Vector3.one * size, FloorY);
            Part("Box", c, new Vector3(0, FloorY + size * 0.5f, 0), Vector3.one * (size - 0.02f), "Wood");
            foreach (var y in new[] { 0.15f, 0.85f })
                Part("Slat", c, new Vector3(0, FloorY + size * y, 0), new Vector3(size + 0.01f, 0.05f, size + 0.01f), "Bark");
            return c;
        }

        private static Transform Barrel(Transform root, Vector3 pos)
        {
            var b = Piece("Barrel", root, pos, 0f, new Vector3(0.55f, 0.85f, 0.55f), FloorY);
            Round("Staves", b, new Vector3(0, FloorY + 0.42f, 0), 0.55f, 0.84f, "Wood");
            foreach (var y in new[] { 0.12f, 0.72f })
                Round("Hoop", b, new Vector3(0, FloorY + y, 0), 0.58f, 0.05f, "Metal");
            return b;
        }

        private static void CeilingFixture(Transform root, Vector3 lightPos, float ceiling, bool lit, bool pendant)
        {
            if (pendant)
            {
                float top = ceiling - 0.06f;
                Part("Cord", root, new Vector3(lightPos.x, (top + lightPos.y + 0.15f) * 0.5f, lightPos.z), new Vector3(0.015f, Mathf.Max(0.05f, top - lightPos.y - 0.15f), 0.015f), "Metal");
                Round("Shade", root, lightPos + Vector3.up * 0.12f, 0.36f, 0.14f, "Metal");
                Round("Bulb", root, lightPos, 0.12f, 0.1f, lit ? "Bulb" : "SignWhite");
            }
            else
            {
                Round("Fixture", root, new Vector3(lightPos.x, ceiling - 0.13f, lightPos.z), 0.4f, 0.08f, lit ? "Bulb" : "SignWhite");
                Round("Fixture Rim", root, new Vector3(lightPos.x, ceiling - 0.09f, lightPos.z), 0.44f, 0.03f, "Metal");
            }
        }

        private static void InteriorLight(Transform root, Vector3 localPos, float range, float intensity, bool flicker = false, float ceiling = -1f, bool pendant = false)
        {
            var go = new GameObject("Interior Light");
            go.transform.SetParent(root, false);
            go.transform.localPosition = localPos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.74f, 0.45f);
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            if (flicker)
                SetField(go.AddComponent<FlickeringLight>(), "flickerChance", 0.2f);
            if (ceiling > 0f)
                CeilingFixture(root, localPos, ceiling, true, pendant);
        }

        /// <summary>A loop that plays from a spot in the world (TV static, a jukebox), through the game's audio buses.</summary>
        private static void SoundEmitter(Transform parent, Vector3 localPos, string clip, float volume, float maxDistance)
        {
            var go = new GameObject("Sound: " + clip);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var emitter = go.AddComponent<Audio.AmbientEmitter>();
            SetField(emitter, "clipName", clip);
            SetField(emitter, "volume", volume);
            SetField(emitter, "maxDistance", maxDistance);
        }

        private static void Wardrobe(Transform root, Vector3 localBase, float yaw)
        {
            var w = Box("Wardrobe", root, localBase + new Vector3(0, 1.05f, 0), new Vector3(0.65f, 2.1f, 1.0f), "Wood");
            w.transform.localRotation = Quaternion.Euler(0, yaw + 90f, 0);
            // Doors, handles and a cornice, parented to the wardrobe so they follow its rotation.
            var face = Group("Wardrobe Face", root);
            face.localPosition = localBase;
            face.localRotation = Quaternion.Euler(0, yaw, 0);
            foreach (var x in new[] { -0.245f, 0.245f })
            {
                Part("Door", face, new Vector3(x, 1.05f, 0.33f), new Vector3(0.47f, 1.9f, 0.015f), "Door");
                Part("Handle", face, new Vector3(x * 0.15f, 1.1f, 0.345f), new Vector3(0.025f, 0.14f, 0.025f), "Mustard");
            }
            Part("Cornice", face, new Vector3(0, 2.13f, 0), new Vector3(1.06f, 0.06f, 0.7f), "Wood");
            var inside = Group("Inside", root);
            inside.localPosition = localBase + new Vector3(0, 0.1f, 0);
            inside.localRotation = Quaternion.Euler(0, yaw, 0);
            w.AddComponent<Events.HidingSpot>().Configure(inside);
        }

        // ---------------------------------------------------------------- Walls

        /// <summary>Wallpaper and skirting on the inside of the shell, leaving the door gaps open.</summary>
        private static void LineWalls(Transform root, BuildingDef def, List<float> doorXs, Vector2 doorSize, float ceiling)
        {
            string paper = def.Interior switch
            {
                InteriorKind.House => "Wallpaper",
                InteriorKind.Motel => "MotelPaper",
                InteriorKind.MotelOffice => "MotelPaper",
                InteriorKind.Diner => "DinerWall",
                InteriorKind.Store => "StoreWall",
                _ => null,
            };
            if (paper == null) return;
            float w = def.Size.x, d = def.Size.z, t = WallThickness;
            float inX = w * 0.5f - t, inZ = d * 0.5f - t, h = ceiling - 0.12f;
            const float gap = 0.012f, lining = 0.02f;
            Part("Lining Back", root, new Vector3(0, h * 0.5f + 0.1f, -inZ + gap), new Vector3(w - 2 * t, h - 0.1f, lining), paper);
            Part("Lining Left", root, new Vector3(-inX + gap, h * 0.5f + 0.1f, 0), new Vector3(lining, h - 0.1f, d - 2 * t), paper);
            Part("Lining Right", root, new Vector3(inX - gap, h * 0.5f + 0.1f, 0), new Vector3(lining, h - 0.1f, d - 2 * t), paper);
            Part("Skirting Back", root, new Vector3(0, 0.17f, -inZ + 0.03f), new Vector3(w - 2 * t, 0.14f, 0.03f), "Wood");
            Part("Skirting Left", root, new Vector3(-inX + 0.03f, 0.17f, 0), new Vector3(0.03f, 0.14f, d - 2 * t), "Wood");
            Part("Skirting Right", root, new Vector3(inX - 0.03f, 0.17f, 0), new Vector3(0.03f, 0.14f, d - 2 * t), "Wood");
            // Front wall: segments between the doors. Windows poke through the wall a little further than the lining.
            float cursor = -inX;
            foreach (float doorX in doorXs)
            {
                float a = doorX - doorSize.x * 0.5f - 0.04f;
                if (a - cursor > 0.05f)
                {
                    Part("Lining Front", root, new Vector3((cursor + a) * 0.5f, h * 0.5f + 0.1f, inZ - gap), new Vector3(a - cursor, h - 0.1f, lining), paper);
                    Part("Skirting Front", root, new Vector3((cursor + a) * 0.5f, 0.17f, inZ - 0.03f), new Vector3(a - cursor, 0.14f, 0.03f), "Wood");
                }
                // Door casing inside.
                foreach (var side in new[] { -1f, 1f })
                    Part("Casing", root, new Vector3(doorX + side * (doorSize.x * 0.5f + 0.04f), doorSize.y * 0.5f + 0.05f, inZ - 0.02f), new Vector3(0.08f, doorSize.y + 0.1f, 0.04f), "Wood");
                Part("Casing Head", root, new Vector3(doorX, doorSize.y + 0.1f, inZ - 0.02f), new Vector3(doorSize.x + 0.16f, 0.08f, 0.04f), "Wood");
                cursor = doorX + doorSize.x * 0.5f + 0.04f;
            }
            if (inX - cursor > 0.05f)
            {
                Part("Lining Front", root, new Vector3((cursor + inX) * 0.5f, h * 0.5f + 0.1f, inZ - gap), new Vector3(inX - cursor, h - 0.1f, lining), paper);
                Part("Skirting Front", root, new Vector3((cursor + inX) * 0.5f, 0.17f, inZ - 0.03f), new Vector3(inX - cursor, 0.14f, 0.03f), "Wood");
            }
        }

        private static void AddReverb(Transform root, InteriorKind kind, Vector3 size)
        {
            var preset = kind switch
            {
                InteriorKind.Church => AudioReverbPreset.Auditorium,
                InteriorKind.Barn => AudioReverbPreset.Hangar,
                InteriorKind.Motel => AudioReverbPreset.Room,
                InteriorKind.MotelOffice => AudioReverbPreset.Room,
                InteriorKind.Diner => AudioReverbPreset.Livingroom,
                InteriorKind.Store => AudioReverbPreset.Room,
                InteriorKind.House => AudioReverbPreset.Livingroom,
                _ => AudioReverbPreset.Room,
            };
            var go = new GameObject("Reverb");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0, 1.5f, 0);
            var zone = go.AddComponent<AudioReverbZone>();
            zone.reverbPreset = preset;
            zone.minDistance = Mathf.Min(size.x, size.z) * 0.45f;
            zone.maxDistance = Mathf.Max(size.x, size.z) * 0.6f;
        }

        // ---------------------------------------------------------------- Furnishing

        private static void Furnish(Transform root, BuildingDef def, List<float> doorXs, float ceiling, bool anyLit, Random rng)
        {
            float w = def.Size.x, d = def.Size.z;
            float inX = w * 0.5f - WallThickness, inZ = d * 0.5f - WallThickness; // interior half-extents
            var doorSize = def.DoorSize == Vector2.zero ? new Vector2(1.4f, 2.2f) : def.DoorSize;
            LineWalls(root, def, doorXs, doorSize, ceiling);
            if (def.Interior != InteriorKind.Empty)
                AddReverb(root, def.Interior, def.Size);
            switch (def.Interior)
            {
                case InteriorKind.Diner: FurnishDiner(root, inX, inZ, ceiling, rng); break;
                case InteriorKind.Store: FurnishStore(root, inX, inZ, ceiling, rng); break;
                case InteriorKind.Church: FurnishChurch(root, inX, inZ, ceiling); break;
                case InteriorKind.House: FurnishHouse(root, def, inX, inZ, ceiling, anyLit, rng); break;
                case InteriorKind.Barn: FurnishBarn(root, inX, inZ, ceiling, rng); break;
                case InteriorKind.Motel: FurnishMotel(root, def, doorXs, inX, inZ, ceiling); break;
                case InteriorKind.MotelOffice: FurnishMotelOffice(root, inX, inZ, ceiling); break;
            }
        }

        private static void FurnishDiner(Transform root, float inX, float inZ, float ceiling, Random rng)
        {
            // Counter: chrome-edged top, panelled front, kick plate.
            var counter = Piece("Counter", root, new Vector3(1f, 0, -1.9f), 0f, new Vector3(7f, 1.05f, 0.6f), FloorY);
            Part("Front", counter, new Vector3(0, FloorY + 0.48f, 0.27f), new Vector3(7f, 0.9f, 0.06f), "Vinyl");
            Part("Body", counter, new Vector3(0, FloorY + 0.48f, -0.02f), new Vector3(6.96f, 0.9f, 0.52f), "Wood");
            Part("Top", counter, new Vector3(0, FloorY + 0.95f, 0.02f), new Vector3(7.1f, 0.05f, 0.7f), "Counter");
            Part("Trim", counter, new Vector3(0, FloorY + 0.915f, 0.37f), new Vector3(7.1f, 0.03f, 0.02f), "Metal");
            Part("Kick", counter, new Vector3(0, FloorY + 0.05f, 0.29f), new Vector3(7f, 0.1f, 0.04f), "Metal");
            for (float x = -2f; x <= 4.1f; x += 1.4f)
            {
                var stool = Piece("Stool", root, new Vector3(x, 0, -1.15f), 0f, new Vector3(0.4f, 0.72f, 0.4f), FloorY);
                Round("Foot", stool, new Vector3(0, FloorY + 0.02f, 0), 0.34f, 0.04f, "Metal");
                Round("Post", stool, new Vector3(0, FloorY + 0.33f, 0), 0.07f, 0.62f, "Metal");
                Round("Ring", stool, new Vector3(0, FloorY + 0.25f, 0), 0.3f, 0.02f, "Metal");
                Round("Seat", stool, new Vector3(0, FloorY + 0.68f, 0), 0.4f, 0.09f, "Vinyl");
            }
            // Pies under a cloche on the counter. One has been dug into.
            foreach (var x in new[] { -2.1f, -1.5f })
            {
                Round("Cake Stand", root, new Vector3(x, FloorY + 1.0f, -1.9f), 0.36f, 0.04f, "Counter");
                Round("Crust", root, new Vector3(x, FloorY + 1.05f, -1.9f), 0.3f, 0.06f, "Hay");
                Round("Filling", root, new Vector3(x, FloorY + 1.075f, -1.9f), 0.24f, 0.02f, "BarnRed");
            }
            // Four long grooves dug through the filling, too far apart for a hand.
            GameObject gouged = null;
            for (int g = 0; g < 4; g++)
            {
                var groove = Part("Groove", root, new Vector3(-1.56f + g * 0.04f, FloorY + 1.087f, -1.9f), new Vector3(0.018f, 0.012f, 0.22f), "Coffee");
                groove.transform.localRotation = Quaternion.Euler(0, 6f - g * 3f, 0);
                if (g == 1) gouged = groove;
            }
            Inspect(gouged, "Look at the pie", "A cherry pie with the middle clawed out. Not cut, dug: four long grooves through the filling, set too far apart for any hand you know. Mae's put it out anyway.", new Vector3(0.4f, 0.3f, 0.4f));
            // Free county maps in a little wire tray by the napkins (fills in the notebook's map page).
            var maps = Box("County Maps", root, new Vector3(1.1f, FloorY + 0.97f, -1.8f), new Vector3(0.34f, 0.05f, 0.24f), "MapPaper"); // the green county-map covers
            maps.transform.localRotation = Quaternion.Euler(0, 12f, 0);
            maps.AddComponent<World.TownMap>();
            // A glass carafe on the warmer: coffee in the bottom, a black lid and handle.
            var coffee = Round("Coffee Pot", root, new Vector3(3.6f, FloorY + 1.05f, -1.9f), 0.18f, 0.11f, "Coffee");
            coffee.AddComponent<BoxCollider>();
            coffee.AddComponent<World.DinerCoffee>();
            Round("Carafe Glass", root, new Vector3(3.6f, FloorY + 1.15f, -1.9f), 0.16f, 0.09f, "Glass");
            Round("Collar", root, new Vector3(3.6f, FloorY + 1.21f, -1.9f), 0.13f, 0.04f, "HideBlack");
            Round("Lid", root, new Vector3(3.6f, FloorY + 1.24f, -1.9f), 0.1f, 0.025f, "HideBlack");
            Part("Handle", root, new Vector3(3.71f, FloorY + 1.13f, -1.9f), new Vector3(0.03f, 0.15f, 0.03f), "HideBlack");
            Part("Handle Top", root, new Vector3(3.68f, FloorY + 1.2f, -1.9f), new Vector3(0.07f, 0.025f, 0.03f), "HideBlack");
            Part("Warmer", root, new Vector3(3.6f, FloorY + 0.985f, -1.9f), new Vector3(0.3f, 0.02f, 0.3f), "WindowDark");
            foreach (var x in new[] { 0.3f, 2.0f })
            {
                Round("Napkins", root, new Vector3(x, FloorY + 1.03f, -1.85f), 0.1f, 0.12f, "Metal");
                Round("Ketchup", root, new Vector3(x + 0.15f, FloorY + 1.06f, -1.85f), 0.06f, 0.18f, "BarnRed");
            }

            // Back bar: shelves of cups and bottles, the mirror above.
            var backBar = Shelving("Back Shelf", root, new Vector3(1f, 0, -inZ + 0.22f), 0f, 8f, 1.8f, 3, rng, goods: false);
            for (int b = 0; b < 3; b++)
                for (float x = -3.7f; x < 3.8f; x += 0.32f)
                {
                    float y = FloorY + 0.08f + b * 1.7f / 3f;
                    if (b == 0) Round("Cup", backBar, new Vector3(x, y + 0.07f, 0.02f), 0.1f, 0.1f, "SignWhite");
                    else if (b == 1) Round(x % 0.64f < 0.32f ? "Bottle" : "Jar", backBar, new Vector3(x, y + 0.12f, 0.02f), 0.08f, 0.22f, x % 0.96f < 0.32f ? "Sage" : "Mustard");
                    else Part("Plate", backBar, new Vector3(x, y + 0.11f, -0.08f), new Vector3(0.2f, 0.2f, 0.02f), "SignWhite").transform.localRotation = Quaternion.Euler(-12f, 0, 0);
                }
            Mirror(root, new Vector3(1f, 2.45f, -inZ + 0.04f), 0f, new Vector2(4f, 0.9f),
                "Earl's mirror. The only one left in town, the way people talk. Mae won't take it down, whatever anybody says.");

            // Booths along the front windows: padded benches with backs, a table on a pedestal.
            foreach (float x in new[] { -4.2f, 3.6f })
            {
                float z = inZ - 0.75f;
                var table = Piece("Booth Table", root, new Vector3(x, 0, z), 0f, new Vector3(1.3f, 0.75f, 0.8f), FloorY);
                Part("Top", table, new Vector3(0, FloorY + 0.73f, 0), new Vector3(1.3f, 0.05f, 0.8f), "Counter");
                Part("Edge", table, new Vector3(0, FloorY + 0.705f, 0), new Vector3(1.32f, 0.02f, 0.82f), "Metal");
                Round("Pedestal", table, new Vector3(0, FloorY + 0.36f, 0), 0.1f, 0.7f, "Metal");
                Part("Base", table, new Vector3(0, FloorY + 0.02f, 0), new Vector3(0.5f, 0.04f, 0.4f), "Metal");
                Round("Sugar", table, new Vector3(0.4f, FloorY + 0.82f, 0.2f), 0.08f, 0.12f, "SignWhite");
                foreach (var side in new[] { -1f, 1f })
                {
                    var seat = Piece("Booth Seat", root, new Vector3(x + side * 1.05f, 0, z), side > 0 ? -90f : 90f, new Vector3(0.9f, 1.05f, 0.55f), FloorY);
                    Part("Base", seat, new Vector3(0, FloorY + 0.2f, 0.02f), new Vector3(0.9f, 0.3f, 0.5f), "Wood");
                    Part("Cushion", seat, new Vector3(0, FloorY + 0.4f, 0.04f), new Vector3(0.88f, 0.12f, 0.48f), "Vinyl");
                    Part("Back", seat, new Vector3(0, FloorY + 0.72f, -0.22f), new Vector3(0.88f, 0.6f, 0.12f), "Vinyl");
                    Part("Back Frame", seat, new Vector3(0, FloorY + 0.6f, -0.27f), new Vector3(0.9f, 1.0f, 0.04f), "Wood");
                }
            }

            // Jukebox, glowing, playing the town's one song very quietly.
            var juke = Piece("Jukebox", root, new Vector3(-inX + 0.45f, 0, -2.4f), 90f, new Vector3(0.9f, 1.55f, 0.6f), FloorY);
            Part("Cabinet", juke, new Vector3(0, FloorY + 0.65f, 0), new Vector3(0.9f, 1.3f, 0.6f), "Wood");
            Round("Crown", juke, new Vector3(0, FloorY + 1.3f, 0), 0.9f, 0.5f, "SignNeon").transform.localRotation = Quaternion.Euler(90f, 0, 0);
            Part("Window", juke, new Vector3(0, FloorY + 1.0f, 0.305f), new Vector3(0.6f, 0.3f, 0.01f), "Bulb");
            Part("Grille", juke, new Vector3(0, FloorY + 0.45f, 0.305f), new Vector3(0.62f, 0.45f, 0.01f), "Metal");
            foreach (var x in new[] { -0.4f, 0.4f })
                Part("Light Bar", juke, new Vector3(x, FloorY + 0.7f, 0.29f), new Vector3(0.06f, 1.1f, 0.04f), "SignNeon");
            SoundEmitter(juke, new Vector3(0, 1f, 0), "jukebox_loop", 0.07f, 9f);

            Picture(root, new Vector3(-inX + 0.03f, 1.9f, 1.2f), 90f, new Vector2(0.5f, 0.38f));
            var earl = Picture(root, new Vector3(inX - 0.03f, 1.8f, -0.5f), -90f, new Vector2(0.4f, 0.5f));
            Inspect(earl, "Look at the photo", "Earl Whitlock outside the diner the year it opened, squinting into the sun. Somebody has rubbed at his face with a thumb, over and over, until the print has worn white.", new Vector3(0.6f, 0.7f, 0.3f));
            var menu = Part("Menu Board", root, new Vector3(-3.6f, 2.2f, -inZ + 0.05f), new Vector3(1.6f, 0.7f, 0.04f), "SignBoard");
            AddText(root, "MEATLOAF  1.95\nCHERRY PIE  .75\nCOFFEE  .35\n<size=60%>ASK ABOUT TODAY'S PIE</size>", new Vector3(-3.6f, 2.2f, -inZ + 0.075f), new Vector2(1.45f, 0.6f), new Color(0.85f, 0.8f, 0.65f));
            foreach (var x in new[] { -2.5f, 2.5f })
            {
                var lightPos = new Vector3(x, ceiling - 0.7f, 0.2f);
                InteriorLight(root, lightPos, 9f, 3f, ceiling: ceiling, pendant: true);
            }
        }

        private static void FurnishStore(Transform root, float inX, float inZ, float ceiling, Random rng)
        {
            foreach (float x in new[] { -4f, -2f })
                Shelving("Shelf", root, new Vector3(x, 0, 0.2f), 90f, 4.4f, 1.8f, 4, rng, goods: true, doubleSided: true);
            Shelving("Back Shelf", root, new Vector3(2.5f, 0, -inZ + 0.25f), 0f, 5f, 2f, 4, rng);

            var counter = Piece("Counter", root, new Vector3(3f, 0, -1.4f), 0f, new Vector3(3f, 0.9f, 0.7f), FloorY);
            Part("Body", counter, new Vector3(0, FloorY + 0.44f, 0), new Vector3(3f, 0.86f, 0.66f), "Wood");
            Part("Top", counter, new Vector3(0, FloorY + 0.885f, 0), new Vector3(3.1f, 0.05f, 0.74f), "Counter");
            for (int i = 0; i < 4; i++)
                Part("Panel", counter, new Vector3(-1.1f + i * 0.73f, FloorY + 0.44f, 0.335f), new Vector3(0.6f, 0.6f, 0.012f), "Door");
            var register = Box("Register", root, new Vector3(2.3f, FloorY + 1.05f, -1.4f), new Vector3(0.45f, 0.28f, 0.4f), "Metal");
            register.AddComponent<World.FilmCounter>();
            Part("Register Keys", root, new Vector3(2.3f, FloorY + 1.1f, -1.18f), new Vector3(0.4f, 0.12f, 0.08f), "SignWhite").transform.localRotation = Quaternion.Euler(30f, 0, 0);
            Part("Register Display", root, new Vector3(2.3f, FloorY + 1.27f, -1.45f), new Vector3(0.3f, 0.14f, 0.06f), "WindowDark");
            for (int i = 0; i < 3; i++)
                Round("Candy Jar", root, new Vector3(3.3f + i * 0.22f, FloorY + 1.0f, -1.3f), 0.16f, 0.22f, i == 0 ? "BarnRed" : i == 1 ? "Mustard" : "Sage");
            // Film and batteries, the only things you'll buy.
            for (int i = 0; i < 5; i++)
                Part("Film Box", root, new Vector3(4.0f, FloorY + 0.95f + i * 0.06f, -1.55f), new Vector3(0.12f, 0.055f, 0.08f), "Mustard");

            // The mirror rack: a pegboard of empty hooks.
            var rack = Box("Empty Mirror Rack", root, new Vector3(inX - 0.08f, 1.4f, 1.5f), new Vector3(0.05f, 1.4f, 1.6f), "Wood");
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    Part("Hook", root, new Vector3(inX - 0.15f, 0.95f + r * 0.32f, 0.95f + c * 0.36f), new Vector3(0.1f, 0.02f, 0.02f), "Metal");
            Part("Price Tag", root, new Vector3(inX - 0.2f, 0.9f, 2.03f), new Vector3(0.01f, 0.06f, 0.09f), "SignWhite");
            AddInspectable(rack, "Inspect rack", "A rack of empty hooks. A price tag still hangs from one: MIRROR, HAND, $2.49.");

            Barrel(root, new Vector3(-inX + 0.45f, 0, inZ - 0.5f));
            for (int i = 0; i < 3; i++)
            {
                var sack = Piece("Feed Sack", root, new Vector3(-inX + 1.2f + i * 0.55f, 0, inZ - 0.4f), i * 20f, new Vector3(0.5f, 0.4f, 0.35f), FloorY);
                Part("Sack", sack, new Vector3(0, FloorY + 0.2f, 0), new Vector3(0.5f, 0.38f, 0.33f), "Hay");
            }
            AddText(root, "WE DO NOT SELL MIRRORS", new Vector3(0, 2.45f, -inZ + 0.05f), new Vector2(4.4f, 0.55f), new Color(0.35f, 0.08f, 0.06f));
            InteriorLight(root, new Vector3(-1.5f, ceiling - 0.6f, 0), 8f, 2.6f, ceiling: ceiling, pendant: true);
            InteriorLight(root, new Vector3(2.5f, ceiling - 0.6f, -1f), 7f, 2.2f, flicker: true, ceiling: ceiling, pendant: true);
        }

        private static void FurnishChurch(Transform root, float inX, float inZ, float ceiling)
        {
            // Pews face the altar (-z): seat, a back on the door side, carved ends, a hymnal shelf.
            for (float z = 4.5f; z >= -3.6f; z -= 1.6f)
                foreach (float x in new[] { -2.3f, 2.3f })
                {
                    var pew = Piece("Pew", root, new Vector3(x, 0, z), 0f, new Vector3(3.4f, 1.0f, 0.62f), FloorY);
                    Part("Pew Seat", pew, new Vector3(0, FloorY + 0.42f, -0.02f), new Vector3(3.3f, 0.06f, 0.46f), "Wood");
                    Part("Pew Back", pew, new Vector3(0, FloorY + 0.68f, 0.24f), new Vector3(3.3f, 0.62f, 0.06f), "Wood");
                    Part("Pew Rail", pew, new Vector3(0, FloorY + 1.0f, 0.24f), new Vector3(3.34f, 0.05f, 0.1f), "Wood");
                    Part("Hymnal Shelf", pew, new Vector3(0, FloorY + 0.75f, 0.3f), new Vector3(3.2f, 0.04f, 0.12f), "Wood");
                    foreach (var end in new[] { -1.68f, 1.68f })
                        Part("Pew End", pew, new Vector3(end, FloorY + 0.48f, 0.02f), new Vector3(0.06f, 0.96f, 0.58f), "Wood");
                    for (int i = 0; i < 3; i++)
                        Part("Hymnal", pew, new Vector3(-1f + i * 1f, FloorY + 0.8f, 0.31f), new Vector3(0.14f, 0.08f, 0.1f), "BarnRed");
                }

            var altar = Piece("Altar", root, new Vector3(0, 0, -inZ + 1.4f), 0f, new Vector3(2.4f, 1f, 1f), FloorY);
            Part("Altar Body", altar, new Vector3(0, FloorY + 0.47f, 0), new Vector3(2.3f, 0.94f, 0.9f), "Wood");
            Part("Cloth", altar, new Vector3(0, FloorY + 0.955f, 0), new Vector3(2.44f, 0.03f, 1.02f), "SignWhite");
            Part("Runner", altar, new Vector3(0, FloorY + 0.7f, 0.515f), new Vector3(0.5f, 0.55f, 0.01f), "Rug");
            foreach (var x in new[] { -0.8f, 0.8f })
            {
                Round("Candlestick", altar, new Vector3(x, FloorY + 1.1f, -0.2f), 0.06f, 0.26f, "Mustard");
                Round("Candle", altar, new Vector3(x, FloorY + 1.3f, -0.2f), 0.04f, 0.14f, "SignWhite");
                Part("Flame", altar, new Vector3(x, FloorY + 1.4f, -0.2f), new Vector3(0.025f, 0.05f, 0.025f), "Bulb");
            }
            Part("Bible", altar, new Vector3(0, FloorY + 0.985f, 0.1f), new Vector3(0.36f, 0.04f, 0.26f), "BarnRed");

            var pulpit = Piece("Pulpit", root, new Vector3(2.6f, 0, -inZ + 2.4f), 0f, new Vector3(0.8f, 1.2f, 0.8f), FloorY);
            Part("Column", pulpit, new Vector3(0, FloorY + 0.55f, 0), new Vector3(0.6f, 1.1f, 0.6f), "Wood");
            Part("Desk", pulpit, new Vector3(0, FloorY + 1.15f, 0.05f), new Vector3(0.8f, 0.05f, 0.6f), "Wood").transform.localRotation = Quaternion.Euler(18f, 0, 0);
            Part("Step", pulpit, new Vector3(0, FloorY + 0.08f, -0.5f), new Vector3(0.7f, 0.16f, 0.3f), "Wood");

            Box("Cross Upright", root, new Vector3(0, 4.2f, -inZ + 0.06f), new Vector3(0.2f, 2.4f, 0.08f), "Wood", collider: false);
            Box("Cross Bar", root, new Vector3(0, 4.7f, -inZ + 0.06f), new Vector3(1.3f, 0.2f, 0.08f), "Wood", collider: false);

            // The hymn board.
            var board = Part("Hymn Board", root, new Vector3(-2.8f, 2.2f, -inZ + 0.05f), new Vector3(0.7f, 0.9f, 0.04f), "Wood");
            AddText(root, "4\n46\n ", new Vector3(-2.8f, 2.2f, -inZ + 0.075f), new Vector2(0.55f, 0.8f), new Color(0.85f, 0.82f, 0.7f));
            // The third number is on a fresh white card, in red paint.
            Part("Hymn Card", root, new Vector3(-2.8f, 1.9f, -inZ + 0.074f), new Vector3(0.48f, 0.22f, 0.006f), "Paper");
            AddText(root, "213", new Vector3(-2.8f, 1.9f, -inZ + 0.08f), new Vector2(0.44f, 0.2f), new Color(0.55f, 0.08f, 0.06f), wear: 0.04f);
            Inspect(board, "Read the hymn board", "Hymns 4, 46 and 213. The hymnals in the pews only go up to 180. The 213 is on a fresh white card, in the same red paint as the sign on the highway.", new Vector3(0.8f, 1f, 0.3f));

            // The bell rope hangs just inside the door, under the steeple.
            // A real rope: from a brass-bound hole in the ceiling (the belfry is above) down to a striped woollen
            // grip, with the tail end coiled on a cleat on the wall.
            var ropePos = new Vector3(inX - 0.7f, 0, inZ - 1.0f);
            float ropeTop = ceiling - 0.12f, ropeBottom = 0.75f;
            Round("Bell Rope", root, new Vector3(ropePos.x, (ropeTop + ropeBottom) * 0.5f, ropePos.z), 0.05f, ropeTop - ropeBottom, "Rope");
            Round("Rope Hole", root, new Vector3(ropePos.x, ropeTop - 0.01f, ropePos.z), 0.22f, 0.03f, "Brass");
            string[] sally = { "BarnRed", "Paper", "Blue", "BarnRed", "Paper", "Blue" };
            for (int i = 0; i < sally.Length; i++)
                Round("Sally", root, new Vector3(ropePos.x, 1.0f + i * 0.1f, ropePos.z), 0.1f, 0.1f, sally[i]);
            Round("Rope Tail", root, new Vector3(ropePos.x, ropeBottom - 0.15f, ropePos.z), 0.035f, 0.3f, "Rope");
            var ropeTrigger = new GameObject("Bell Rope Trigger");
            ropeTrigger.transform.SetParent(root, false);
            ropeTrigger.transform.localPosition = new Vector3(ropePos.x, 1.6f, ropePos.z);
            var ropeBox = ropeTrigger.AddComponent<BoxCollider>();
            ropeBox.isTrigger = true;
            ropeBox.size = new Vector3(0.5f, 2.6f, 0.5f);
            ropeTrigger.AddComponent<Case.BellRope>();
            Part("Cleat", root, new Vector3(inX - 0.04f, 1.2f, inZ - 1.0f), new Vector3(0.06f, 0.06f, 0.3f), "Metal");
            for (int c = 0; c < 3; c++)
                Round("Coil", root, new Vector3(inX - 0.09f, 1.05f - c * 0.05f, inZ - 1.0f), 0.22f - c * 0.02f, 0.035f, "Rope").transform.localRotation = Quaternion.Euler(0, 0, 90f);

            var cabinet = Box("Parish Records", root, new Vector3(-inX + 0.4f, 0.85f, -inZ + 2.6f), new Vector3(0.65f, 1.5f, 0.9f), "Metal");
            cabinet.AddComponent<Archive.ArchiveCabinet>();
            for (int i = 0; i < 3; i++)
            {
                Box("Drawer Front", root, new Vector3(-inX + 0.74f, 0.45f + i * 0.45f, -inZ + 2.6f), new Vector3(0.02f, 0.36f, 0.75f), "Wood", collider: false);
                Box("Drawer Pull", root, new Vector3(-inX + 0.76f, 0.55f + i * 0.45f, -inZ + 2.6f), new Vector3(0.03f, 0.03f, 0.18f), "Mustard", collider: false);
            }
            // A green-shaded reading lamp on top of the records cabinet.
            Round("Lamp Base", root, new Vector3(-inX + 0.4f, 1.615f, -inZ + 2.3f), 0.14f, 0.03f, "Brass");
            Part("Lamp Stem", root, new Vector3(-inX + 0.4f, 1.73f, -inZ + 2.3f), new Vector3(0.02f, 0.2f, 0.02f), "Brass");
            Part("Lamp Shade", root, new Vector3(-inX + 0.4f, 1.84f, -inZ + 2.38f), new Vector3(0.12f, 0.06f, 0.24f), "Sage");
            Part("Lamp Glow", root, new Vector3(-inX + 0.4f, 1.805f, -inZ + 2.38f), new Vector3(0.1f, 0.01f, 0.2f), "Bulb");

            // Candle stands by the walls.
            foreach (var z in new[] { 3.5f, -1.5f })
                foreach (var x in new[] { -inX + 0.3f, inX - 0.3f })
                {
                    Round("Candle Stand", root, new Vector3(x, FloorY + 0.6f, z), 0.06f, 1.2f, "Metal");
                    Round("Candle", root, new Vector3(x, FloorY + 1.27f, z), 0.06f, 0.14f, "SignWhite");
                    Part("Flame", root, new Vector3(x, FloorY + 1.37f, z), new Vector3(0.03f, 0.05f, 0.03f), "Bulb");
                }

            InteriorLight(root, new Vector3(-inX + 1.2f, 2.2f, -inZ + 2.6f), 4f, 1.6f);
            InteriorLight(root, new Vector3(0, 1.8f, -inZ + 1.5f), 9f, 3.5f, flicker: true);
            InteriorLight(root, new Vector3(0, 4f, 2f), 9f, 1.2f, ceiling: ceiling, pendant: true);
        }

        private static void FurnishHouse(Transform root, BuildingDef def, float inX, float inZ, float ceiling, bool anyLit, Random rng)
        {
            string family = def.AnchorKey != null && def.AnchorKey.StartsWith("House:") ? def.AnchorKey.Substring(6) : "";

            // Kitchen (back left): table and chairs, stove, cupboard.
            Table("Kitchen Table", root, new Vector3(-2.6f, 0, -1.4f), 0f, 1.6f, 1f);
            Chair(root, new Vector3(-3.75f, 0, -1.4f), 90f);
            Chair(root, new Vector3(-1.45f, 0, -1.4f), -90f);
            Chair(root, new Vector3(-2.6f, 0, -2.25f), 0f);
            Stove(root, new Vector3(-inX + 0.45f, 0, -inZ + 0.4f), 0f);
            Cupboard(root, new Vector3(-2.2f, 0, -inZ + 0.3f), 0f);
            Round("Kettle", root, new Vector3(-inX + 0.35f, FloorY + 1.0f, -inZ + 0.3f), 0.18f, 0.16f, "Metal");

            // Sitting room (back right): couch, rug, side table with a radio, a lamp.
            Couch(root, new Vector3(2.4f, 0, -inZ + 0.6f), 0f);
            Part("Rug", root, new Vector3(2.4f, FloorY + 0.01f, -0.6f), new Vector3(2.6f, 0.02f, 1.8f), "Rug");
            Part("Rug Border", root, new Vector3(2.4f, FloorY + 0.005f, -0.6f), new Vector3(2.8f, 0.01f, 2.0f), "Cream");
            var side = Table("Side Table", root, new Vector3(4.0f, 0, -inZ + 0.5f), 0f, 0.5f, 0.45f, height: 0.6f);
            Part("Radio", side, new Vector3(0, FloorY + 0.7f, 0), new Vector3(0.36f, 0.2f, 0.18f), "Wood");
            Part("Radio Dial", side, new Vector3(0, FloorY + 0.7f, 0.095f), new Vector3(0.2f, 0.07f, 0.01f), "Mustard");
            var lamp = Piece("Floor Lamp", root, new Vector3(-0.75f, 0, -inZ + 0.4f), 0f, new Vector3(0.35f, 1.6f, 0.35f), FloorY);
            Round("Base", lamp, new Vector3(0, FloorY + 0.03f, 0), 0.3f, 0.05f, "Metal");
            Part("Pole", lamp, new Vector3(0, FloorY + 0.75f, 0), new Vector3(0.03f, 1.4f, 0.03f), "Metal");
            Round("Shade", lamp, new Vector3(0, FloorY + 1.5f, 0), 0.4f, 0.28f, anyLit ? "LampShade" : "Cream");

            // Bedroom corner (front right): bed head against the front wall, nightstand, wardrobe.
            Bed("Bed", root, new Vector3(inX - 0.8f, 0, inZ - 1.1f), 180f, family == "Marsh" ? "Blue" : "Fabric");
            Nightstand(root, new Vector3(inX - 1.85f, 0, inZ - 0.35f), 180f);
            Wardrobe(root, new Vector3(inX - 0.4f, 0f, -0.9f), -90f);

            // By the door: a coat rack and a mat.
            var rack = Piece("Coat Rack", root, new Vector3(-1.2f, 0, inZ - 0.35f), 0f, new Vector3(0.3f, 1.8f, 0.3f), FloorY);
            Round("Stand", rack, new Vector3(0, FloorY + 0.85f, 0), 0.06f, 1.7f, "Wood");
            Part("Coat", rack, new Vector3(0, FloorY + 1.3f, 0.08f), new Vector3(0.38f, 0.7f, 0.14f), rng.NextDouble() < 0.5 ? "Fabric" : "Blue");
            Part("Mat", root, new Vector3(0, FloorY + 0.005f, inZ - 0.6f), new Vector3(1.2f, 0.01f, 0.6f), "Hay");

            if (family != "Marsh" && family != "Thorne")
                Picture(root, new Vector3(2.4f, 1.75f, -inZ + 0.04f), 0f, new Vector2(0.5f, 0.4f));
            if (family != "Hollis" && family != "Pruitt" && family != "Vance" && family != "Kessler")
                Picture(root, new Vector3(-inX + 0.04f, 1.6f, 0.4f), 90f, new Vector2(0.4f, 0.5f));
            Shelving("Bookshelf", root, new Vector3(-inX + 0.22f, 0, 1.9f), 90f, 1.2f, 1.6f, 4, rng);

            FamilyDetails(root, family, inX, inZ, rng);

            if (anyLit)
                InteriorLight(root, new Vector3(0, ceiling - 0.5f, -0.5f), 7f, 3f, flicker: rng.NextDouble() < 0.3, ceiling: ceiling, pendant: true);
            else
                CeilingFixture(root, new Vector3(0, ceiling - 0.5f, -0.5f), ceiling, false, true);
        }

        /// <summary>The small things that make each house somebody's: red herrings, real threads, and a little dread.</summary>
        private static void FamilyDetails(Transform root, string family, float inX, float inZ, Random rng)
        {
            switch (family)
            {
                case "Hollis":
                {
                    var gunRack = Part("Gun Rack", root, new Vector3(-inX + 0.04f, 1.7f, -0.6f), new Vector3(0.04f, 0.5f, 1.2f), "Bark");
                    // The double-barrel on two pegs: side-by-side barrels, a wooden fore-end and stock.
                    foreach (var pz in new[] { -0.95f, -0.3f })
                        Part("Peg", root, new Vector3(-inX + 0.1f, 1.66f, pz), new Vector3(0.12f, 0.025f, 0.025f), "Wood");
                    foreach (var dy in new[] { -0.015f, 0.015f })
                        Round("Barrel", root, new Vector3(-inX + 0.12f, 1.7f + dy, -0.38f), 0.028f, 0.72f, "Metal").transform.localRotation = Quaternion.Euler(90f, 0, 0);
                    Part("Fore-end", root, new Vector3(-inX + 0.12f, 1.68f, -0.6f), new Vector3(0.04f, 0.04f, 0.22f), "Wood");
                    Part("Action", root, new Vector3(-inX + 0.12f, 1.69f, -0.8f), new Vector3(0.045f, 0.07f, 0.12f), "Metal");
                    var stock = Part("Stock", root, new Vector3(-inX + 0.12f, 1.65f, -1.02f), new Vector3(0.045f, 0.1f, 0.34f), "Wood");
                    stock.transform.localRotation = Quaternion.Euler(-8f, 0, 0);
                    Inspect(gunRack, "Look at the gun rack", "Harlan's double-barrel. Loaded. There are fresh scratches on the stock where it's been leaned against the back door every night.", new Vector3(0.3f, 0.6f, 1.3f));
                    var hymnal = Part("Hymnal", root, new Vector3(-2.9f, FloorY + 0.8f, -1.3f), new Vector3(0.18f, 0.04f, 0.24f), "BarnRed");
                    Inspect(hymnal, "Read the hymnal", "Nell's hymnal, swollen with use. A ribbon marks 'Abide With Me'. In the margin, in her hand: 'Harlan's hands were warm this morning. Thank you, Lord.' The same line, every morning of September. October's margins are blank.", new Vector3(0.4f, 0.3f, 0.4f));
                    break;
                }
                case "Marsh":
                {
                    // Crayon drawings taped up in a row (no frames); the newest has a fifth grown-up in it.
                    for (int i = 0; i < 4; i++)
                        Drawing(root, new Vector3(-0.2f + i * 0.42f, 1.3f + (i % 2) * 0.1f, -inZ + 0.03f), 0f, rng, 6, i >= 2);
                    var drawing = Drawing(root, new Vector3(1.55f, 1.25f, -inZ + 0.03f), 0f, rng, 6, true);
                    Inspect(drawing, "Look at the drawings", "Crayon drawings taped to the wall: Mama, Daddy, four kids, the house. In every one since September there's a fifth grown-up standing a little behind the others. In the newest, it's holding Mama's hand.", new Vector3(2.2f, 0.6f, 0.3f));
                    foreach (var p in new[] { new Vector3(0.6f, FloorY + 0.08f, 0.8f), new Vector3(1.4f, FloorY + 0.06f, 1.2f) })
                        Part("Toy Block", root, p, new Vector3(0.12f, 0.12f, 0.12f), rng.NextDouble() < 0.5 ? "BarnRed" : "Blue");
                    break;
                }
                case "Pruitt":
                {
                    var files = Box("Surgery Files", root, new Vector3(-inX + 0.35f, 0.75f, 0.2f), new Vector3(0.55f, 1.3f, 0.6f), "Metal");
                    for (int i = 0; i < 3; i++)
                        Box("Drawer Front", root, new Vector3(-inX + 0.64f, 0.35f + i * 0.4f, 0.2f), new Vector3(0.02f, 0.32f, 0.5f), "Wood", collider: false);
                    AddInspectable(files, "Look through the files", "Doc's surgery files, forty years of the town in carbon copies. One folder has been put back facing the wrong way: every birth he recorded in 1945, the year the Founders' Day pageant started. The pages are soft, as if somebody has read them over and over.");
                    var bag = Case("Doctor's Bag", root, new Vector3(2.0f, FloorY + 0.125f, 0.6f), new Vector3(0.45f, 0.25f, 0.22f), -30f);
                    Inspect(bag, "Look in the bag", "Doc's old bag. Stethoscope, tongue depressors, a reflex hammer. A blank death certificate pad with the top sheet torn off.", new Vector3(0.6f, 0.4f, 0.4f));
                    break;
                }
                case "Calder":
                {
                    var rocker = Piece("Rocking Chair", root, new Vector3(1.4f, 0, 1.4f), 210f, new Vector3(0.55f, 1.1f, 0.7f), FloorY);
                    Part("Seat", rocker, new Vector3(0, FloorY + 0.42f, 0), new Vector3(0.5f, 0.05f, 0.48f), "Wood");
                    Part("Back", rocker, new Vector3(0, FloorY + 0.82f, -0.24f), new Vector3(0.5f, 0.8f, 0.05f), "Wood");
                    foreach (var x in new[] { -0.24f, 0.24f })
                    {
                        Part("Rocker", rocker, new Vector3(x, FloorY + 0.04f, 0), new Vector3(0.04f, 0.06f, 0.75f), "Wood");
                        foreach (var z in new[] { -0.2f, 0.2f })
                            Part("Leg", rocker, new Vector3(x, FloorY + 0.235f, z), new Vector3(0.04f, 0.37f, 0.04f), "Wood");
                        Part("Arm", rocker, new Vector3(x, FloorY + 0.64f, 0.02f), new Vector3(0.05f, 0.035f, 0.46f), "Wood");
                        Part("Arm Post", rocker, new Vector3(x, FloorY + 0.53f, 0.2f), new Vector3(0.035f, 0.2f, 0.035f), "Wood");
                    }
                    // The half-finished sleeve, two needles still in it, a ball of the same green wool.
                    var knitting = Part("Knitting", rocker, new Vector3(0, FloorY + 0.46f, 0.05f), new Vector3(0.26f, 0.03f, 0.22f), "Sage");
                    foreach (var a in new[] { -25f, 25f })
                        Part("Needle", rocker, new Vector3(0, FloorY + 0.48f, 0.1f), new Vector3(0.008f, 0.008f, 0.3f), "Chrome").transform.localRotation = Quaternion.Euler(0, a, 0);
                    Round("Wool", rocker, new Vector3(0.17f, FloorY + 0.49f, -0.12f), 0.1f, 0.09f, "Sage");
                    Inspect(knitting, "Look at the knitting", "Ruth's knitting, a half-finished sleeve. Somebody has kept adding to it since August. The new rows are perfectly even, machine-even, and they're knitted backwards.", new Vector3(0.5f, 0.4f, 0.5f));
                    break;
                }
                case "Kessler":
                {
                    var bills = Part("Bills", root, new Vector3(-2.3f, FloorY + 0.79f, -1.5f), new Vector3(0.3f, 0.02f, 0.22f), "SignWhite");
                    Inspect(bills, "Read the bills", "Vet bills, feed bills, a letter from the bank. Greta has added the losses up in the margin: fourteen head. Underneath, smaller: 'Never at the new moon. Always when the bell rings.'", new Vector3(0.4f, 0.3f, 0.4f));
                    // A bolt-action rifle laid across two nails: wooden stock (notched), steel barrel and bolt.
                    foreach (var nz in new[] { -0.95f, -0.25f })
                        Part("Nail", root, new Vector3(-inX + 0.06f, 2.0f, nz), new Vector3(0.08f, 0.012f, 0.012f), "Metal");
                    var rifle = Part("Rifle Stock", root, new Vector3(-inX + 0.1f, 2.04f, -0.85f), new Vector3(0.045f, 0.09f, 0.55f), "Wood");
                    Round("Rifle Barrel", root, new Vector3(-inX + 0.1f, 2.06f, -0.3f), 0.022f, 0.6f, "Metal").transform.localRotation = Quaternion.Euler(90f, 0, 0);
                    Part("Bolt", root, new Vector3(-inX + 0.13f, 2.08f, -0.58f), new Vector3(0.05f, 0.015f, 0.015f), "Metal");
                    for (int n = 0; n < 14; n++) // one for every cow
                        Part("Notch", root, new Vector3(-inX + 0.124f, 2.04f, -1.1f + n * 0.022f), new Vector3(0.003f, 0.06f, 0.006f), "Paper");
                    Inspect(rifle, "Look at the rifle", "A bolt-action rifle on two nails. Greta's notched the stock fourteen times. One for every cow, not for anything she shot.", new Vector3(0.3f, 0.4f, 1.1f));
                    break;
                }
                case "Thorne":
                {
                    // The family in front of the barn: Bev, two boys, and Silas a pale smear from the shoulders up.
                    var photo = Picture(root, new Vector3(0.2f, 1.7f, -inZ + 0.04f), 0f, new Vector2(0.6f, 0.45f), "Cream");
                    var print = photo.transform.parent;
                    Part("Barn", print, new Vector3(0.1f, 0.05f, 0.02f), new Vector3(0.36f, 0.22f, 0.004f), "BarnRed");
                    float[] heights = { 0.24f, 0.17f, 0.15f, 0.26f };
                    for (int f = 0; f < 4; f++)
                    {
                        float fx = -0.2f + f * 0.12f, fh = heights[f];
                        Part("Figure", print, new Vector3(fx, -0.2f + fh * 0.5f, 0.024f), new Vector3(0.06f, fh, 0.004f), "SignBoard");
                        if (f < 3) Part("Head", print, new Vector3(fx, -0.2f + fh + 0.025f, 0.024f), new Vector3(0.04f, 0.045f, 0.004f), "Photo");
                    }
                    Part("Smear", print, new Vector3(0.16f, 0.1f, 0.025f), new Vector3(0.12f, 0.1f, 0.004f), "Smear");
                    Inspect(photo, "Look at the photo", "The Thorne family, 1979, in front of the barn. Bev, the two boys, Silas. Silas is a smear from the shoulders up, like he turned his head too fast. Everyone else is pin-sharp. Silas says he never moves in photos. 1979: years before anything came to Semblance.", new Vector3(0.8f, 0.7f, 0.3f));
                    var engine = Piece("Engine Parts", root, new Vector3(1.8f, 0, 1.8f), 0f, new Vector3(1.0f, 0.75f, 0.6f), FloorY);
                    Part("Bench", engine, new Vector3(0, FloorY + 0.7f, 0), new Vector3(1f, 0.05f, 0.6f), "Wood");
                    Legs(engine, 1f, 0.6f, 0.7f, 0.05f, "Wood");
                    Part("Carburettor", engine, new Vector3(-0.2f, FloorY + 0.82f, 0), new Vector3(0.25f, 0.2f, 0.2f), "Metal");
                    Round("Gear", engine, new Vector3(0.25f, FloorY + 0.74f, 0.05f), 0.2f, 0.04f, "Metal");
                    break;
                }
                case "Vance":
                {
                    // Irene's moths, pinned in glass-fronted cases; one case holds only a pin and a label.
                    for (int i = 0; i < 6; i++)
                    {
                        float z = -1.9f + i * 0.55f;
                        MothCase(root, new Vector3(-inX + 0.03f, 1.5f + (i % 2) * 0.5f, z), 90f, rng, 3 + rng.Next(4));
                    }
                    var empty = MothCase(root, new Vector3(inX - 0.03f, 1.6f, 0.9f), -90f, rng, 0);
                    Inspect(empty, "Look at the case", "A display case, empty except for one pin and a label in Irene's spidery hand: 'Sept. 22nd. Large. Grey. It would not hold still, and it would not die.'", new Vector3(0.3f, 0.6f, 0.7f));
                    break;
                }
                case "Whitlock":
                {
                    var suitcase = Case("Suitcase", root, new Vector3(inX - 1.9f, FloorY + 0.12f, inZ - 2.4f), new Vector3(0.6f, 0.22f, 0.4f), 8f);
                    Inspect(suitcase, "Look at the suitcase", "Ada's suitcase, still packed. A Greyhound ticket to Tulsa is tucked under the strap, dated April 1980. Never punched.", new Vector3(0.8f, 0.5f, 0.6f));
                    // A stack of pie tins and the recipe card beside them.
                    for (int t = 0; t < 3; t++)
                        Round("Pie Tin", root, new Vector3(-2.3f + t * 0.01f, FloorY + 0.79f + t * 0.025f, -1.6f), 0.26f, 0.025f, "Galvanized");
                    var tins = Part("Recipe Card", root, new Vector3(-2.0f, FloorY + 0.783f, -1.45f), new Vector3(0.13f, 0.004f, 0.08f), "Paper");
                    tins.transform.localRotation = Quaternion.Euler(0, 20f, 0);
                    Part("Card Line", root, new Vector3(-2.0f, FloorY + 0.786f, -1.45f), new Vector3(0.1f, 0.002f, 0.006f), "BarnRed").transform.localRotation = Quaternion.Euler(0, 20f, 0);
                    Inspect(tins, "Look at the recipe", "Pie tins and a recipe card in Mae's handwriting: 'Cherry. Earl's mother's.' Somebody has written underneath, in pencil, very carefully: 'cherry cherry cherry cherry'.", new Vector3(0.4f, 0.3f, 0.4f));
                    break;
                }
            }
        }

        private static void FurnishBarn(Transform root, float inX, float inZ, float ceiling, Random rng)
        {
            for (int i = 0; i < 9; i++)
            {
                float x = Lerp(rng, -inX + 0.8f, inX - 0.8f);
                float z = Lerp(rng, -inZ + 0.8f, 0f);
                int stack = rng.Next(1, 3);
                var bale = Piece("Hay Bale", root, new Vector3(x, 0, z), Lerp(rng, -12f, 12f), new Vector3(1.2f, 0.6f * stack, 0.6f), FloorY);
                for (int s = 0; s < stack; s++)
                {
                    Part("Bale", bale, new Vector3(0, FloorY + 0.3f + s * 0.6f, 0), new Vector3(1.18f, 0.58f, 0.58f), "Hay");
                    foreach (var bx in new[] { -0.3f, 0.3f })
                        Part("Twine", bale, new Vector3(bx, FloorY + 0.3f + s * 0.6f, 0), new Vector3(0.02f, 0.6f, 0.6f), "Bark");
                }
            }
            for (float x = -inX + 2.5f; x < inX; x += 2.5f)
            {
                var stall = Piece("Stall Divider", root, new Vector3(x, 0, -inZ + 1.5f), 0f, new Vector3(0.12f, 1.4f, 3f), FloorY);
                for (int r = 0; r < 4; r++)
                    Part("Board", stall, new Vector3(0, FloorY + 0.2f + r * 0.35f, 0), new Vector3(0.06f, 0.22f, 3f), "Wood");
                Part("Post", stall, new Vector3(0, FloorY + 0.7f, 1.45f), new Vector3(0.12f, 1.4f, 0.12f), "Wood");
            }
            // Hay loft over the back with a ladder up to it.
            float loftY = Mathf.Min(3.2f, ceiling - 1.5f);
            Box("Loft", root, new Vector3(0, loftY, -inZ + 1.6f), new Vector3(inX * 2f - 0.1f, 0.12f, 3.2f), "Wood");
            for (float x = -inX + 0.5f; x < inX; x += 2.4f)
                Part("Loft Post", root, new Vector3(x, loftY * 0.5f, -inZ + 3.15f), new Vector3(0.14f, loftY, 0.14f), "Wood");
            var ladder = Group("Ladder", root);
            ladder.localPosition = new Vector3(inX - 1.2f, 0, -inZ + 3.5f);
            ladder.localRotation = Quaternion.Euler(-12f, 0, 0);
            foreach (var x in new[] { -0.22f, 0.22f })
                Part("Rail", ladder, new Vector3(x, loftY * 0.5f, 0), new Vector3(0.05f, loftY + 0.4f, 0.05f), "Wood");
            for (float y = 0.3f; y < loftY; y += 0.35f)
                Part("Rung", ladder, new Vector3(0, y, 0), new Vector3(0.44f, 0.04f, 0.04f), "Wood");
            // Tools, a trough, a lantern.
            // Pitchfork (tines up), shovel (blade down), rake (head up), leaning on the wall.
            for (int i = 0; i < 3; i++)
            {
                var tool = Group(i == 0 ? "Pitchfork" : i == 1 ? "Shovel" : "Rake", root);
                tool.localPosition = new Vector3(-inX + 0.12f, FloorY, 1.5f + i * 0.45f);
                tool.localRotation = Quaternion.Euler(0, 0, -6f);
                Part("Handle", tool, new Vector3(0, 0.85f, 0), new Vector3(0.035f, 1.6f, 0.035f), "Wood");
                if (i == 0)
                {
                    Part("Crossbar", tool, new Vector3(0, 1.66f, 0), new Vector3(0.03f, 0.03f, 0.22f), "Metal");
                    for (int t = 0; t < 4; t++)
                        Part("Tine", tool, new Vector3(0, 1.8f, -0.09f + t * 0.06f), new Vector3(0.012f, 0.28f, 0.012f), "Metal");
                }
                else if (i == 1)
                {
                    Part("Blade", tool, new Vector3(0, 0.12f, 0), new Vector3(0.02f, 0.28f, 0.22f), "Metal");
                    Part("D-Grip", tool, new Vector3(0, 1.68f, 0), new Vector3(0.03f, 0.08f, 0.12f), "Wood");
                    tool.localPosition += Vector3.up * 0.15f;
                }
                else
                {
                    Part("Rake Head", tool, new Vector3(0, 1.66f, 0), new Vector3(0.04f, 0.04f, 0.4f), "Metal");
                    for (int t = 0; t < 8; t++)
                        Part("Tooth", tool, new Vector3(0.03f, 1.62f, -0.18f + t * 0.05f), new Vector3(0.01f, 0.07f, 0.01f), "Metal");
                }
            }
            var trough = Piece("Trough", root, new Vector3(inX - 0.5f, 0, 1.2f), 90f, new Vector3(2f, 0.6f, 0.6f), FloorY);
            Part("Trough", trough, new Vector3(0, FloorY + 0.3f, 0), new Vector3(2f, 0.55f, 0.55f), "Wood");
            Part("Water", trough, new Vector3(0, FloorY + 0.5f, 0), new Vector3(1.9f, 0.02f, 0.45f), "WindowDark");
            Crate(root, new Vector3(-inX + 0.6f, 0, 3.5f), 10f);
            Crate(root, new Vector3(-inX + 0.7f, 0, 4.2f), -5f, 0.5f);
            Barrel(root, new Vector3(inX - 0.5f, 0, 3.2f));
            Part("Lantern Hook", root, new Vector3(0, ceiling - 1.3f, 2.5f), new Vector3(0.02f, 0.4f, 0.02f), "Metal");
            // A hurricane lantern: tin base and cap, a glass globe between, a wire bail.
            Round("Lantern Base", root, new Vector3(0, ceiling - 1.74f, 2.5f), 0.14f, 0.05f, "Rust");
            Round("Lantern Globe", root, new Vector3(0, ceiling - 1.63f, 2.5f), 0.11f, 0.17f, "Glass");
            Round("Lantern Cap", root, new Vector3(0, ceiling - 1.52f, 2.5f), 0.1f, 0.05f, "Rust");
            Part("Bail", root, new Vector3(0, ceiling - 1.48f, 2.5f), new Vector3(0.14f, 0.01f, 0.01f), "Metal");
        }

        private static void FurnishMotel(Transform root, BuildingDef def, List<float> doorXs, float inX, float inZ, float ceiling)
        {
            float d = def.Size.z;
            for (int i = 0; i < doorXs.Count; i++)
            {
                float roomX = doorXs[i];
                int roomNumber = i + 2;
                if (i < doorXs.Count - 1)
                {
                    float wallX = (doorXs[i] + doorXs[i + 1]) * 0.5f;
                    Box("Partition", root, new Vector3(wallX, ceiling * 0.5f, 0), new Vector3(WallThickness, ceiling, d - 2 * WallThickness), "MotelPaper");
                    Part("Skirting", root, new Vector3(wallX - 0.12f, 0.17f, 0), new Vector3(0.03f, 0.14f, d - 2 * WallThickness), "Wood");
                    Part("Skirting", root, new Vector3(wallX + 0.12f, 0.17f, 0), new Vector3(0.03f, 0.14f, d - 2 * WallThickness), "Wood");
                }

                bool yours = i == doorXs.Count - 1;

                // A standard room: bed against the back wall, nightstand, a dresser with the television on it,
                // a wardrobe, a chair by the window.
                var bed = Bed("Bed", root, new Vector3(roomX - 0.35f, 0, -inZ + 1.1f), 0f, roomNumber == 5 ? "SignWhite" : "Rug", made: roomNumber != 5);
                Nightstand(root, new Vector3(roomX + 0.7f, 0, -inZ + 0.3f), 0f, lampLit: yours);
                Dresser(root, new Vector3(roomX - 1.38f, 0, 0.2f), 90f, 1.0f);
                var screen = Television(root, new Vector3(roomX - 1.38f, 0, 0.2f), FloorY + 0.82f, on: roomNumber == 4);
                screen.transform.parent.localRotation = Quaternion.Euler(0, 90f, 0);
                Wardrobe(root, new Vector3(roomX + 1.15f, 0f, -0.3f), -90f);
                Chair(root, new Vector3(roomX + 1.1f, 0, inZ - 0.9f), 200f, "Fabric");
                Part("Carpet", root, new Vector3(roomX, FloorY + 0.006f, 0), new Vector3(3.1f, 0.012f, 2 * inZ - 0.2f), roomNumber % 2 == 0 ? "Rug" : "Fabric");

                if (yours)
                {
                    bed.gameObject.AddComponent<World.MotelBed>();
                    var wake = Group("Wake Spot", root);
                    wake.localPosition = new Vector3(roomX + 0.2f, 0.15f, -0.9f);
                    Anchors["WakeSpot"] = wake;
                    InteriorLight(root, new Vector3(roomX, ceiling - 0.6f, -0.5f), 5f, 2.5f, ceiling: ceiling);
                    Case("Suitcase", root, new Vector3(roomX + 0.4f, FloorY + 0.12f, 0.8f), new Vector3(0.6f, 0.22f, 0.4f), 20f);
                }
                else
                    CeilingFixture(root, new Vector3(roomX, ceiling - 0.6f, -0.5f), ceiling, false, false);

                switch (roomNumber)
                {
                    case 3:
                    {
                        // Room 3: H. Semple, brushes and notions. Checked in on June 2nd. Never checked out.
                        // On the floor at the foot of the bed, where he set it down on June 2nd.
                        var sampleCase = Case("Sample Case", root, new Vector3(roomX - 0.35f, FloorY + 0.08f, -inZ + 2.5f), new Vector3(0.55f, 0.16f, 0.4f), 12f);
                        Evidence(EvidenceInspect(sampleCase), "semple_case", "A salesman's sample case",
                            "Fuller Brush. Every brush still in its slot, every one still in its wrapper. Not one sale. The name stencilled on the lid is H. SEMPLE.", null, "Open the sample case");
                        var suitRoot = Group("Suit", root);
                        suitRoot.localPosition = new Vector3(roomX + 1.5f, 0, 1.4f);
                        suitRoot.localRotation = Quaternion.Euler(0, -90f, 0);
                        Part("Hook", suitRoot, new Vector3(0, 2.02f, -0.12f), new Vector3(0.03f, 0.03f, 0.12f), "Metal");
                        Part("Hanger", suitRoot, new Vector3(0, 1.93f, 0), new Vector3(0.44f, 0.03f, 0.03f), "Wood");
                        var suit = Part("Jacket", suitRoot, new Vector3(0, 1.62f, 0), new Vector3(0.44f, 0.6f, 0.1f), "Suit");
                        Part("Lapels", suitRoot, new Vector3(0, 1.78f, 0.052f), new Vector3(0.16f, 0.26f, 0.01f), "SignWhite");
                        Part("Tie", suitRoot, new Vector3(0, 1.72f, 0.058f), new Vector3(0.05f, 0.34f, 0.01f), "BarnRed");
                        foreach (var x in new[] { -0.26f, 0.26f })
                            Part("Sleeve", suitRoot, new Vector3(x, 1.55f, 0), new Vector3(0.09f, 0.62f, 0.09f), "Suit");
                        for (int b = 0; b < 3; b++)
                            Part("Button", suitRoot, new Vector3(0, 1.62f - b * 0.12f, 0.055f), new Vector3(0.025f, 0.025f, 0.01f), "Mustard");
                        foreach (var x in new[] { -0.1f, 0.1f })
                            Part("Trouser Leg", suitRoot, new Vector3(x, 0.95f, 0), new Vector3(0.17f, 0.75f, 0.07f), "Suit");
                        Evidence(EvidenceInspect(suit, new Vector3(0.7f, 1.2f, 0.4f)), "semple_suit", "A brown suit",
                            "A brown suit on a hook, buttoned all the way up, as if something were still wearing it. The pockets are full of dry, fine dirt, like the dirt from a grave. Or a cellar.", null, "Look at the suit");
                        var dent = Part("Dent", root, new Vector3(roomX - 0.35f, FloorY + 0.531f, -inZ + 1.3f), new Vector3(0.6f, 0.01f, 1.5f), "Fabric");
                        Evidence(EvidenceInspect(dent, new Vector3(1.2f, 0.6f, 2f)), "semple_bed", "The bed in Room 3",
                            "The bedspread is perfectly smooth, but there's a long, deep dent in the mattress under it, the shape of someone lying flat on their back with their arms at their sides. The dust on it is months old. Whatever lay here didn't toss or turn. It lay still, all summer, with its ear to the wall.", null, "Look at the bed", 6f);
                        break;
                    }
                    case 2:
                    {
                        // Room 2: made up and never used. A stack of towels nobody has unfolded, the chair turned to the wall.
                        for (int t = 0; t < 4; t++)
                            Part("Towel", root, new Vector3(roomX - 1.38f, FloorY + 0.86f + t * 0.05f, 0.2f), new Vector3(0.34f, 0.045f, 0.24f), "PlainWhite");
                        var card = Part("Card", root, new Vector3(roomX + 0.7f, FloorY + 0.6f, -inZ + 0.33f), new Vector3(0.12f, 0.004f, 0.08f), "Paper");
                        Inspect(card, "Read the card", "A card on the nightstand in the motel's handwriting: WELCOME TO THE PALE MOON. The date line has been filled in and crossed out eleven times. Every date is a Sunday.", new Vector3(0.3f, 0.3f, 0.3f));
                        break;
                    }
                    case 4:
                    {
                        SoundEmitter(root, new Vector3(roomX - 1.2f, 1.2f, 0.2f), "static_loop", 0.06f, 6f);
                        // Left open: red cover, two pages fanned up from the spine.
                        var bible = Part("Bible", root, new Vector3(roomX + 0.6f, FloorY + 0.596f, -inZ + 0.3f), new Vector3(0.26f, 0.012f, 0.18f), "BarnRed");
                        foreach (var side in new[] { -1f, 1f })
                            Part("Pages", root, new Vector3(roomX + 0.6f + side * 0.062f, FloorY + 0.608f, -inZ + 0.3f), new Vector3(0.12f, 0.012f, 0.17f), "Paper").transform.localRotation = Quaternion.Euler(0, 0, side * -5f);
                        Inspect(bible, "Read the Bible", "A Gideon Bible left open at Matthew 13: 'Let both grow together until the harvest.' Somebody has written in the margin in pencil, very small: which is which", new Vector3(0.35f, 0.3f, 0.4f));
                        var ashtray = Round("Ashtray", root, new Vector3(roomX + 0.84f, FloorY + 0.605f, -inZ + 0.44f), 0.15f, 0.03f, "Glass");
                        Round("Ash", root, new Vector3(roomX + 0.84f, FloorY + 0.618f, -inZ + 0.44f), 0.08f, 0.006f, "Ash");
                        // Every butt the same length, put out in a perfect ring, filters pointing inward.
                        for (int c = 0; c < 10; c++)
                        {
                            float a = c * 36f;
                            var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                            var butt = Part("Cigarette", root, new Vector3(roomX + 0.84f, FloorY + 0.625f, -inZ + 0.44f) + dir * 0.048f, new Vector3(0.011f, 0.011f, 0.032f), "Paper");
                            butt.transform.localRotation = Quaternion.Euler(0, a, 0);
                            Part("Filter", root, new Vector3(roomX + 0.84f, FloorY + 0.625f, -inZ + 0.44f) + dir * 0.028f, new Vector3(0.012f, 0.012f, 0.012f), "Mustard").transform.localRotation = Quaternion.Euler(0, a, 0);
                        }
                        Inspect(ashtray, "Look at the ashtray", "Full. Every cigarette smoked down to exactly the same length, then put out in a perfect ring.", new Vector3(0.3f, 0.3f, 0.3f));
                        break;
                    }
                    case 5:
                    {
                        var patch = Part("Mirror Patch", root, new Vector3(roomX + 1.62f, 1.6f, 1.6f), new Vector3(0.01f, 0.9f, 0.6f), "SignWhite");
                        Inspect(patch, "Look at the wall", "A clean pale rectangle in the wallpaper where a mirror used to hang. The four screws have been lined up on the nightstand: perfectly straight, perfectly spaced.", new Vector3(0.3f, 1f, 0.8f));
                        for (int s = 0; s < 4; s++)
                            Part("Screw", root, new Vector3(roomX + 0.58f + s * 0.06f, FloorY + 0.6f, -inZ + 0.38f), new Vector3(0.012f, 0.012f, 0.04f), "Metal");
                        break;
                    }
                }
            }
            Mirror(root, new Vector3(inX - 0.03f, 1.6f, -1.6f), -90f, new Vector2(0.7f, 1f), "A small mirror screwed to the wall. Somebody has scratched a line across it at exactly the height of your eyes.");
            // The scratch, at eye height, right across the glass.
            Part("Scratch", root, new Vector3(inX - 0.036f, 1.6f, -1.6f), new Vector3(0.003f, 0.008f, 0.66f), "Smear").transform.localRotation = Quaternion.Euler(2f, 0, 0);
        }

        /// <summary>The motel office. Nobody works here. Somebody keeps the books anyway.</summary>
        private static void FurnishMotelOffice(Transform root, float inX, float inZ, float ceiling)
        {
            const float roomX = 0f;
            // The picture window beside the door, through the front wall, so the desk can be seen from the lot.
            float winX = -1.7f;
            Part("Office Window", root, new Vector3(winX, 1.45f, inZ + 0.1f), new Vector3(1.7f, 1.3f, 0.34f), "WindowLit");
            Part("Window Frame", root, new Vector3(winX, 2.13f, inZ + 0.1f), new Vector3(1.8f, 0.07f, 0.4f), "Wood");
            Part("Window Frame", root, new Vector3(winX, 0.77f, inZ + 0.1f), new Vector3(1.8f, 0.07f, 0.4f), "Wood");
            foreach (var fx in new[] { -0.9f, 0.9f })
                Part("Window Frame", root, new Vector3(winX + fx, 1.45f, inZ + 0.1f), new Vector3(0.07f, 1.4f, 0.4f), "Wood");
            Part("Window Sill", root, new Vector3(winX, 0.74f, inZ + 0.3f), new Vector3(1.9f, 0.06f, 0.2f), "Wood");
            Part("Blind", root, new Vector3(winX, 1.95f, inZ - 0.14f), new Vector3(1.65f, 0.35f, 0.02f), "PlainCream");
            var vacancy = Part("Vacancy Sign", root, new Vector3(winX - 0.4f, 1.1f, inZ - 0.12f), new Vector3(0.7f, 0.22f, 0.03f), "SignNeon");
            AddText(root, "VACANCY", new Vector3(winX - 0.4f, 1.1f, inZ - 0.14f), new Vector2(0.65f, 0.2f), new Color(0.95f, 0.9f, 0.8f), Quaternion.Euler(0, 180f, 0), glow: true, wear: 0.03f);
            var desk = Table("Front Desk", root, new Vector3(roomX, 0, 0.3f), 0f, 1.8f, 0.7f, "Wood", 1.0f);
            Part("Desk Front", desk, new Vector3(0, FloorY + 0.5f, 0.33f), new Vector3(1.8f, 0.95f, 0.04f), "Door");
            var register = Part("Guest Register", root, new Vector3(roomX - 0.3f, FloorY + 1.016f, 0.3f), new Vector3(0.45f, 0.03f, 0.32f), "SignWhite");
            Evidence(EvidenceInspect(register, new Vector3(0.6f, 0.4f, 0.5f)), "motel_register", "The motel register",
                "Pale Moon Motel guest register. June 2nd: H. Semple, Rm 3, 'brushes & notions'. No check-out. Then nothing for four months. October 15th: Rm 6, PAID IN ADVANCE, CASH, EXACT CHANGE. No name. That entry isn't in the same handwriting as the others. It's in yours.", null, "Read the register", 6f);
            // A chrome service bell.
            Round("Bell Base", root, new Vector3(roomX + 0.5f, FloorY + 1.008f, 0.45f), 0.11f, 0.015f, "Wood");
            MeshPart("Bell", root, domeMesh, new Vector3(roomX + 0.5f, FloorY + 1.015f, 0.45f), new Vector3(0.09f, 0.045f, 0.09f), "Chrome");
            Part("Plunger", root, new Vector3(roomX + 0.5f, FloorY + 1.065f, 0.45f), new Vector3(0.012f, 0.02f, 0.012f), "Chrome");
            // Hooks for Rooms 2 to 6. Keys on 2, 4 and 5; Room 3's hook has a paper tag; Room 6's key is in your door.
            var keys = Part("Key Board", root, new Vector3(roomX, 1.7f, -inZ + 0.05f), new Vector3(0.9f, 0.5f, 0.03f), "Wood");
            for (int k = 0; k < 5; k++)
            {
                // The building faces the other way round, so room 2 is on the viewer's left at +x.
                float kx = roomX + 0.32f - k * 0.16f;
                AddText(root, (k + 2).ToString(), new Vector3(kx, 1.87f, -inZ + 0.07f), new Vector2(0.1f, 0.08f), new Color(0.85f, 0.75f, 0.45f));
                Part("Hook", root, new Vector3(kx, 1.78f, -inZ + 0.08f), new Vector3(0.012f, 0.012f, 0.04f), "Brass");
                if (k == 0 || k == 2 || k == 3)
                {
                    Part("Key", root, new Vector3(kx, 1.72f, -inZ + 0.075f), new Vector3(0.025f, 0.07f, 0.008f), "Brass");
                    Part("Key Fob", root, new Vector3(kx, 1.64f, -inZ + 0.075f), new Vector3(0.05f, 0.08f, 0.01f), "BarnRed");
                }
                else if (k == 1)
                    Part("Tag", root, new Vector3(kx, 1.69f, -inZ + 0.072f), new Vector3(0.09f, 0.11f, 0.004f), "Paper");
            }
            Inspect(keys, "Look at the key board", "Hooks for Rooms 2 to 6. Every key is hanging up except yours and Room 3's. Under Room 3's empty hook, a paper tag: DO NOT DISTURB. HE IS STILL LISTENING.", new Vector3(1f, 0.7f, 0.3f));
            Chair(root, new Vector3(roomX, 0, -0.6f), 0f);
            Crate(root, new Vector3(-inX + 0.5f, 0, -inZ + 0.5f), 0f, 0.55f);
            // A rack of brochures nobody takes, a calendar still on June, a coffee ring on the desk.
            Part("Brochure Rack", root, new Vector3(inX - 0.2f, 1.2f, 0.4f), new Vector3(0.05f, 0.5f, 0.5f), "Wood");
            for (int b = 0; b < 3; b++)
                Part("Brochure", root, new Vector3(inX - 0.24f, 1.03f + b * 0.17f, 0.25f + (b % 2) * 0.25f), new Vector3(0.012f, 0.14f, 0.1f), b == 1 ? "PlainBlue" : "PlainMustard");
            var calendar = Part("Calendar", root, new Vector3(1.4f, 1.75f, -inZ + 0.05f), new Vector3(0.3f, 0.42f, 0.01f), "Paper");
            Inspect(calendar, "Look at the calendar", "A feed-store calendar, still on June. The 2nd is circled. Nothing's been written on it since.", new Vector3(0.4f, 0.5f, 0.3f));
            Round("Coffee Ring", root, new Vector3(roomX + 0.6f, FloorY + 1.012f, 0.15f), 0.09f, 0.002f, "Coffee");
            InteriorLight(root, new Vector3(roomX, ceiling - 0.6f, 0), 6f, 2.2f, flicker: true, ceiling: ceiling, pendant: true);
        }

        /// <summary>Wraps a decorative part so it can carry an EvidenceItem (which needs a trigger collider).</summary>
        private static GameObject EvidenceInspect(GameObject target, Vector3? triggerSize = null)
        {
            var box = target.GetComponent<BoxCollider>();
            if (box == null) box = target.AddComponent<BoxCollider>();
            box.isTrigger = true;
            var s = target.transform.lossyScale;
            var size = triggerSize ?? new Vector3(0.6f, 0.5f, 0.6f);
            box.size = new Vector3(Mathf.Max(1f, size.x / Mathf.Max(s.x, 1e-3f)), Mathf.Max(1f, size.y / Mathf.Max(s.y, 1e-3f)), Mathf.Max(1f, size.z / Mathf.Max(s.z, 1e-3f)));
            return target;
        }
    }
}
