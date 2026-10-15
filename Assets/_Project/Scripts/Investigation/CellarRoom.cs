using System.Collections.Generic;
using UnityEngine;

namespace Semblance.Investigation
{
    /// <summary>An underground room ("Cellar:Hollis", "Crypt"). One of them holds the real resident.</summary>
    public class CellarRoom : MonoBehaviour
    {
        [SerializeField] private string id;
        [SerializeField] private Transform victimSpot;

        private static readonly List<CellarRoom> Rooms = new List<CellarRoom>();
        public static IReadOnlyList<CellarRoom> All => Rooms;

        public string Id => id;
        public Transform VictimSpot => victimSpot;

        public void Configure(string roomId, Transform spot)
        {
            id = roomId;
            victimSpot = spot;
        }

        private void OnEnable() => Rooms.Add(this);
        private void OnDisable() => Rooms.Remove(this);

        public static CellarRoom Find(string roomId) => Rooms.Find(r => r.id == roomId);
    }
}
