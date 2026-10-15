using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Semblance.EditorTools
{
    /// <summary>
    /// Generates small (64x64), point-filtered, mostly-greyscale PS1 textures that materials tint with their colour.
    /// Menu: Semblance > Generate Textures.
    /// </summary>
    public static class TextureSynth
    {
        public const string Dir = "Assets/_Project/Textures";
        private const int S = 64;
        private static System.Random rng;

        [MenuItem("Semblance/Generate Textures")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Dir);
            rng = new System.Random(212);
            Save("grass", Grass());
            Save("dirt", Dirt());
            Save("asphalt", Asphalt());
            Save("planks", Planks(vertical: false));
            Save("planks_vertical", Planks(vertical: true));
            Save("siding", Siding());
            Save("brick", Brick());
            Save("stone", Stone());
            Save("slabs", Slabs());
            Save("shingles", Shingles());
            Save("bark", Bark());
            Save("needles", Needles());
            Save("stalks", Stalks());
            Save("hay", Hay());
            Save("fabric", Fabric());
            AssetDatabase.Refresh();
            foreach (var file in Directory.GetFiles(Dir, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        public static Texture2D Load(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/{name}.png");

        // ------------------------------------------------------------ Helpers

        private static float R() => (float)rng.NextDouble();
        private static float[,] Field(float v) { var f = new float[S, S]; for (int x = 0; x < S; x++) for (int y = 0; y < S; y++) f[x, y] = v; return f; }

        /// <summary>Tileable value noise.</summary>
        private static float Noise(int x, int y, int cells, int seed)
        {
            float fx = x / (float)S * cells, fy = y / (float)S * cells;
            int x0 = (int)fx, y0 = (int)fy;
            float tx = fx - x0, ty = fy - y0;
            float H(int a, int b) { a = ((a % cells) + cells) % cells; b = ((b % cells) + cells) % cells; return (float)new System.Random(a * 73856093 ^ b * 19349663 ^ seed).NextDouble(); }
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);
            return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), tx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), tx), ty);
        }

        private static float Fbm(int x, int y, int seed) => Noise(x, y, 4, seed) * 0.5f + Noise(x, y, 8, seed + 1) * 0.3f + Noise(x, y, 16, seed + 2) * 0.2f;

        private static void Save(string name, Color[] pixels)
        {
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes($"{Dir}/{name}.png", tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static Color[] Gray(Func<int, int, float> value, Func<int, int, Color> tint = null)
        {
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float v = Mathf.Clamp01(value(x, y));
                var c = new Color(v, v, v, 1);
                if (tint != null) c *= tint(x, y);
                c.a = 1;
                px[y * S + x] = c;
            }
            return px;
        }

        // ------------------------------------------------------------ Textures

        private static Color[] Grass()
        {
            var blades = Field(0);
            for (int i = 0; i < 260; i++)
            {
                int x = rng.Next(S), y = rng.Next(S), len = 2 + rng.Next(4);
                for (int k = 0; k < len; k++) blades[(x + (k > 2 ? 1 : 0)) % S, (y + k) % S] += 0.25f;
            }
            return Gray((x, y) => 0.72f + Fbm(x, y, 1) * 0.3f - blades[x, y] * 0.6f + (R() - 0.5f) * 0.08f,
                (x, y) => Color.Lerp(new Color(1f, 1f, 0.9f), new Color(0.85f, 1.05f, 0.85f), Noise(x, y, 4, 9)));
        }

        private static Color[] Dirt()
        {
            return Gray((x, y) =>
            {
                float v = 0.72f + Fbm(x, y, 3) * 0.28f + (R() - 0.5f) * 0.12f;
                if (Noise(x, y, 16, 5) > 0.82f) v -= 0.25f; // pebbles
                return v;
            });
        }

        private static Color[] Asphalt()
        {
            var cracks = Field(0);
            for (int c = 0; c < 3; c++)
            {
                float x = R() * S, y = R() * S, dir = R() * Mathf.PI * 2;
                for (int k = 0; k < 40; k++)
                {
                    dir += (R() - 0.5f) * 0.9f;
                    x += Mathf.Cos(dir); y += Mathf.Sin(dir);
                    cracks[((int)x % S + S) % S, ((int)y % S + S) % S] = 1;
                }
            }
            return Gray((x, y) => 0.78f + (R() - 0.5f) * 0.18f + Fbm(x, y, 7) * 0.15f - cracks[x, y] * 0.4f);
        }

        private static Color[] Planks(bool vertical)
        {
            return Gray((x, y) =>
            {
                int a = vertical ? x : y, b = vertical ? y : x;
                int plank = a / 16;
                float gap = a % 16 == 0 ? 0.45f : 0f;
                float grain = Mathf.Sin((b + plank * 13) * 0.35f + Noise(x, y, 8, plank) * 6f) * 0.06f;
                float knot = Noise(x, y, 16, plank + 40) > 0.88f ? 0.2f : 0f;
                return 0.8f + grain - gap - knot + (plank % 2) * 0.05f + (R() - 0.5f) * 0.05f;
            });
        }

        private static Color[] Siding()
        {
            return Gray((x, y) =>
            {
                int board = y / 8;
                float shadow = y % 8 == 0 ? 0.35f : y % 8 == 1 ? 0.15f : 0f;
                float wear = Fbm(x, y, 20 + board) > 0.72f ? 0.12f : 0f;
                return 0.92f - shadow - wear + (R() - 0.5f) * 0.05f;
            });
        }

        private static Color[] Brick()
        {
            return Gray((x, y) =>
            {
                int row = y / 8;
                int offset = row % 2 == 0 ? 0 : 8;
                bool mortar = y % 8 == 0 || (x + offset) % 16 == 0;
                if (mortar) return 0.95f;
                int brickId = row * 7 + (x + offset) / 16;
                return 0.65f + (float)new System.Random(brickId).NextDouble() * 0.25f + (R() - 0.5f) * 0.08f;
            });
        }

        private static Color[] Stone() => Gray((x, y) => 0.7f + Fbm(x, y, 30) * 0.35f + (R() - 0.5f) * 0.1f - (Noise(x, y, 8, 31) > 0.75f ? 0.15f : 0f));

        private static Color[] Slabs() => Gray((x, y) => (x % 32 == 0 || y % 32 == 0 ? 0.6f : 0.88f) + Fbm(x, y, 33) * 0.12f + (R() - 0.5f) * 0.06f);

        private static Color[] Shingles()
        {
            return Gray((x, y) =>
            {
                int row = y / 8;
                int offset = row % 2 == 0 ? 0 : 5;
                bool edge = y % 8 == 0 || (x + offset) % 10 == 0;
                float v = 0.82f + (float)new System.Random(row * 11 + (x + offset) / 10).NextDouble() * 0.18f;
                return edge ? 0.45f : v - (y % 8) * 0.02f;
            });
        }

        private static Color[] Bark() => Gray((x, y) => 0.75f + Mathf.Sin(x * 0.8f + Noise(x, y, 4, 50) * 8f) * 0.15f + (R() - 0.5f) * 0.1f);

        private static Color[] Needles() => Gray((x, y) => 0.7f + Fbm(x, y, 60) * 0.25f + (((x + y * 3) % 7 == 0) ? -0.25f : 0f) + (R() - 0.5f) * 0.12f);

        private static Color[] Stalks() => Gray((x, y) => 0.75f + Mathf.Sin(x * 1.3f) * 0.12f + Noise(x, y, 16, 70) * 0.15f - (x % 9 == 0 ? 0.2f : 0f));

        private static Color[] Hay() => Gray((x, y) => 0.7f + (((x * 3 + y) % 5 == 0) ? 0.25f : 0f) + Noise(x, y, 16, 80) * 0.2f + (R() - 0.5f) * 0.1f);

        private static Color[] Fabric() => Gray((x, y) => 0.8f + ((x % 4 < 2) ^ (y % 4 < 2) ? 0.08f : -0.04f) + ((x / 16 + y / 16) % 2 == 0 ? 0.08f : 0f) + (R() - 0.5f) * 0.04f);
    }
}
