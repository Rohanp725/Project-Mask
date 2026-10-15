using System.Collections.Generic;
using UnityEngine;

namespace Semblance.Audio
{
    /// <summary>An area of the ground that sounds different (roads, driveways, cornfields). Placed by the town builder.</summary>
    public class SurfaceZone : MonoBehaviour
    {
        [SerializeField] private Surface surface;
        [SerializeField] private Vector3 size = Vector3.one;
        [SerializeField] private int priority;

        private static readonly List<SurfaceZone> Zones = new List<SurfaceZone>();

        public void Configure(Surface s, Vector3 boxSize, int p)
        {
            surface = s;
            size = boxSize;
            priority = p;
        }

        private void OnEnable() => Zones.Add(this);
        private void OnDisable() => Zones.Remove(this);

        public static bool TryGet(Vector3 position, out Surface surface)
        {
            surface = Surface.Grass;
            int best = int.MinValue;
            foreach (var z in Zones)
            {
                var local = z.transform.InverseTransformPoint(position);
                if (Mathf.Abs(local.x) > z.size.x * 0.5f || Mathf.Abs(local.z) > z.size.z * 0.5f) continue;
                if (z.priority <= best) continue;
                best = z.priority;
                surface = z.surface;
            }
            return best != int.MinValue;
        }

        public static bool InCorn(Vector3 position) => TryGet(position, out var s) && s == Surface.Corn;
    }
}
