using System.Collections.Generic;
using Semblance.Player;
using UnityEngine;

namespace Semblance.Audio
{
    public enum Surface { Grass, Dirt, Asphalt, Wood, Corn, Concrete }

    /// <summary>Plays footsteps for the player that match what they're walking on.</summary>
    [RequireComponent(typeof(FirstPersonController))]
    public class FootstepAudio : MonoBehaviour
    {
        private FirstPersonController controller;

        private void Awake()
        {
            controller = GetComponent<FirstPersonController>();
            controller.Footstep += OnFootstep;
        }

        private void OnFootstep()
        {
            var surface = Detect(transform.position);
            float volume = controller.IsCrouching ? 0.14f : controller.IsSprinting ? 0.42f : 0.27f;
            if (surface == Surface.Corn)
            {
                GameAudio.Play(GameAudio.Variant("footstep_corn", Variants), volume * 1.3f, Random.Range(0.9f, 1.1f));
                GameAudio.Play(GameAudio.Variant("footstep_dirt", Variants), volume * 0.6f);
                return;
            }
            GameAudio.Play(StepClip(surface), volume, Random.Range(0.92f, 1.08f));
        }

        /// <summary>Recorded steps per surface (footstep_&lt;surface&gt;_1.._6).</summary>
        public const int Variants = 6;

        /// <summary>One random step for a surface (shared with the residents' footsteps).</summary>
        public static AudioClip StepClip(Surface surface)
        {
            string name = surface switch
            {
                Surface.Wood => "footstep_wood", Surface.Asphalt => "footstep_asphalt", Surface.Dirt => "footstep_dirt",
                Surface.Corn => "footstep_corn", Surface.Concrete => "footstep_concrete", _ => "footstep_grass",
            };
            return GameAudio.Variant(name, Variants);
        }

        public static Surface Detect(Vector3 feet)
        {
            if (Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.down, out var hit, 0.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                string n = hit.collider.name;
                // Concrete floors (the tutorial's basement, stairwell and boiler room) and the cellars' packed-earth floors
                // between stone walls sound like a hard, small space, not like boards.
                var rend = hit.collider.GetComponent<Renderer>();
                string mat = rend != null && rend.sharedMaterial != null ? rend.sharedMaterial.name : "";
                if (mat.StartsWith("Concrete") || (n == "Floor" && mat == "Dirt")) return Surface.Concrete;
                if (n == "Floor" || n.StartsWith("Porch") || n.StartsWith("Pew") || n == "Body") return Surface.Wood;
                if (n.StartsWith("Sidewalk")) return Surface.Asphalt;
            }
            return SurfaceZone.TryGet(feet, out var s) ? s : Surface.Grass;
        }
    }
}
