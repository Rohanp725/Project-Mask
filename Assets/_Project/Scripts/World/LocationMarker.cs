using System.Collections.Generic;
using UnityEngine;

namespace Semblance.World
{
    /// <summary>A named place ("Mae's Diner", "Hollis Farm"). Used to describe where notes, photos and sightings happened.</summary>
    public class LocationMarker : MonoBehaviour
    {
        [SerializeField] private string displayName = "Somewhere";
        [SerializeField] private float radius = 12f;
        [Tooltip("Markers with higher priority win when areas overlap (a building inside a farm).")]
        [SerializeField] private int priority;

        private static readonly List<LocationMarker> Markers = new List<LocationMarker>();

        public string DisplayName => displayName;
        public float Radius => radius;
        public int Priority => priority;
        public static IReadOnlyList<LocationMarker> All => Markers;

        private void OnEnable() => Markers.Add(this);
        private void OnDisable() => Markers.Remove(this);

        public void Configure(string name, float r, int p)
        {
            displayName = name;
            radius = r;
            priority = p;
        }

        public static LocationMarker At(Vector3 position)
        {
            LocationMarker best = null;
            foreach (var m in Markers)
            {
                float d = Vector3.Distance(new Vector3(position.x, 0, position.z), new Vector3(m.transform.position.x, 0, m.transform.position.z));
                if (d > m.radius) continue;
                if (best == null || m.priority > best.priority) best = m;
            }
            return best;
        }

        public static string Describe(Vector3 position)
        {
            var m = At(position);
            if (m != null) return "at " + m.displayName;
            if (Mathf.Abs(position.x) < 6f) return "on the highway";
            if (Mathf.Abs(position.z) < 6f) return "on the county road";
            return "out in the dark fields";
        }

        public static LocationMarker Find(string name) => Markers.Find(m => m.displayName == name);
    }
}
