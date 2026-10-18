using System;
using UnityEngine;

namespace Semblance.World
{
    /// <summary>
    /// Lettering painted onto something in the world: a quad carrying a baked, weathered texture of the text, drawn
    /// through the same lit, fogged PS1 shader as everything else (unlike TextMeshPro, which floats unlit and crisp).
    /// Signs whose words change (the population) carry one baked material per variant and swap between them.
    /// </summary>
    public class WorldText : MonoBehaviour
    {
        [SerializeField] private Renderer face;
        [SerializeField] private string[] variants;
        [SerializeField] private Material[] materials;

        public string Current { get; private set; }

        public void Configure(Renderer lettering, string[] names, Material[] baked)
        {
            face = lettering;
            variants = names;
            materials = baked;
            Current = names != null && names.Length > 0 ? names[0] : null;
        }

        /// <summary>Shows one of the baked variants; anything else is ignored (nothing can be painted at runtime).</summary>
        public void Set(string text)
        {
            if (face == null || variants == null) return;
            int i = Array.IndexOf(variants, text);
            if (i < 0 || i >= materials.Length) return;
            face.sharedMaterial = materials[i];
            Current = text;
        }
    }
}
