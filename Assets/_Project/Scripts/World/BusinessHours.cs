using System.Collections.Generic;
using System.Linq;
using Semblance.Core;
using Semblance.Residents;
using UnityEngine;

namespace Semblance.World
{
    /// <summary>
    /// A shop keeps hours. Outside them the door is locked, unless whoever runs the place is still inside,
    /// and anyone already inside can always let themselves out. A night event can hold it open.
    /// </summary>
    public class BusinessHours : MonoBehaviour
    {
        [SerializeField] private float opens = 8f;
        [SerializeField] private float closes = 18f;
        [SerializeField] private string[] keepers;
        [SerializeField, TextArea(1, 3)] private string closedMessage = "Locked. A CLOSED sign hangs in the glass.";
        [SerializeField] private Transform building;
        [SerializeField] private Vector3 halfExtents = new Vector3(5f, 4f, 4f);

        /// <summary>Something tonight wants this door open (the lights in the diner after close).</summary>
        public bool Override { get; set; }

        public string ClosedMessage => closedMessage;

        public void Configure(float openHour, float closeHour, IEnumerable<string> keeperNames, string message, Transform root, Vector3 half)
        {
            opens = openHour;
            closes = closeHour;
            keepers = keeperNames.ToArray();
            closedMessage = message;
            building = root;
            halfExtents = half;
        }

        public bool IsOpenHours
        {
            get
            {
                var clock = GameClock.Instance;
                float h = clock != null ? clock.Hour : 12f;
                if (h >= 24f) h -= 24f; // the clock runs past midnight
                return h >= opens && h < closes;
            }
        }

        public bool Inside(Vector3 worldPosition)
        {
            if (building == null) return false;
            var p = building.InverseTransformPoint(worldPosition);
            return Mathf.Abs(p.x) < halfExtents.x && Mathf.Abs(p.z) < halfExtents.z && p.y > -1f && p.y < halfExtents.y + 1f;
        }

        /// <summary>Whoever runs the place is still in it: the door's open whatever the clock says.</summary>
        public bool KeeperInside =>
            keepers != null && Resident.All.Any(r => r != null && r.isActiveAndEnabled && keepers.Contains(r.DisplayName) && Inside(r.transform.position));

        /// <summary>Shut to somebody standing where they are.</summary>
        public bool ClosedTo(Vector3 playerPosition) => !Override && !IsOpenHours && !KeeperInside && !Inside(playerPosition);
    }
}
