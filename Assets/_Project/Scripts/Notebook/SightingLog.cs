using System.Collections.Generic;
using Semblance.Core;
using Semblance.Residents;
using Semblance.World;
using UnityEngine;

namespace Semblance.Notes
{
    /// <summary>
    /// Quietly notes where and when the detective sees each resident (once per place per phase of the day).
    /// These sightings are alibis: someone can't be at the diner and the Thorne farm at the same time.
    /// </summary>
    public class SightingLog : MonoBehaviour
    {
        [SerializeField] private float range = 20f;
        [SerializeField] private float interval = 1.5f;

        public static event System.Action<Resident, string> Sighted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics() => Sighted = null;

        private float nextCheck;

        private void Update()
        {
            if (Time.time < nextCheck || InputLock.IsLocked) return;
            nextCheck = Time.time + interval;
            var cam = Camera.main;
            var notebook = Notebook.Instance;
            var clock = GameClock.Instance;
            if (cam == null || notebook == null || clock == null) return;

            foreach (var r in Resident.All)
            {
                if (!r.isActiveAndEnabled) continue;
                Vector3 head = r.transform.position + Vector3.up * 1.5f;
                if (Vector3.Distance(cam.transform.position, head) > range) continue;
                var vp = cam.WorldToViewportPoint(head);
                if (vp.z <= 0 || vp.x < 0.05f || vp.x > 0.95f || vp.y < 0.05f || vp.y > 0.95f) continue;
                if (Physics.Linecast(cam.transform.position, head, out var hit, ~0, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<Resident>() != r)
                    continue;

                string where = LocationMarker.Describe(r.transform.position);
                string key = $"sighting|{r.DisplayName}|{clock.Day}|{clock.Phase}|{where}";
                if (notebook.Has(key)) continue;
                notebook.Record(NoteKind.Sighting, r.DisplayName, "Seen", $"Seen {where}, {GameClock.FormatHour(clock.Hour)}.", null, key);
                Sighted?.Invoke(r, where);
            }
        }
    }
}
