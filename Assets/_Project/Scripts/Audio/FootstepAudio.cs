using System.Collections.Generic;
using Semblance.Player;
using UnityEngine;

namespace Semblance.Audio
{
    public enum Surface { Grass, Dirt, Asphalt, Wood, Corn }

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
            switch (surface)
            {
                case Surface.Wood: GameAudio.Play(GameAudio.Variant("footstep_wood", 4), volume, Random.Range(0.92f, 1.08f)); break;
                case Surface.Asphalt: GameAudio.Play(GameAudio.Variant("footstep_asphalt", 4), volume, Random.Range(0.92f, 1.08f)); break;
                case Surface.Dirt: GameAudio.Play(GameAudio.Variant("footstep_dirt", 4), volume, Random.Range(0.92f, 1.08f)); break;
                case Surface.Corn:
                    GameAudio.Play(GameAudio.Variant("footstep_corn", 3), volume * 1.3f, Random.Range(0.9f, 1.1f));
                    GameAudio.Play(GameAudio.Variant("footstep_dirt", 4), volume * 0.6f);
                    break;
                default: GameAudio.Play(GameAudio.Variant("footstep_grass", 4), volume, Random.Range(0.92f, 1.08f)); break;
            }
        }

        public static Surface Detect(Vector3 feet)
        {
            if (Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.down, out var hit, 0.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                string n = hit.collider.name;
                if (n == "Floor" || n.StartsWith("Porch") || n.StartsWith("Pew") || n == "Body") return Surface.Wood;
                if (n.StartsWith("Sidewalk")) return Surface.Asphalt;
            }
            return SurfaceZone.TryGet(feet, out var s) ? s : Surface.Grass;
        }
    }
}
