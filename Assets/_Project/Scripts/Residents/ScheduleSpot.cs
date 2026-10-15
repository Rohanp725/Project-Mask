using System.Collections.Generic;
using UnityEngine;

namespace Semblance.Residents
{
    /// <summary>A named place a resident can be scheduled to stand ("Diner:Counter", "House:Hollis:Porch").</summary>
    public class ScheduleSpot : MonoBehaviour
    {
        [SerializeField] private string id;

        private static readonly Dictionary<string, ScheduleSpot> Spots = new Dictionary<string, ScheduleSpot>();

        public string Id => id;

        public void Configure(string spotId) => id = spotId;

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(id))
                Spots[id] = this;
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(id) && Spots.TryGetValue(id, out var s) && s == this)
                Spots.Remove(id);
        }

        public static ScheduleSpot Find(string spotId) => !string.IsNullOrEmpty(spotId) && Spots.TryGetValue(spotId, out var s) ? s : null;
        public static IEnumerable<ScheduleSpot> All => Spots.Values;
    }
}
