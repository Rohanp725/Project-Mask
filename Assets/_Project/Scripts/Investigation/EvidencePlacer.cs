using System.Linq;
using Semblance.Case;
using Semblance.Core;
using Semblance.Residents;
using UnityEngine;

namespace Semblance.Investigation
{
    /// <summary>
    /// Places the evidence that depends on who the mimic is: its shed skin and practice sketches at its home,
    /// and the real resident, alive, in the cellar (or the church crypt) where it keeps them.
    /// </summary>
    public class EvidencePlacer : MonoBehaviour
    {
        [SerializeField] private Shader shader;

        private void Start()
        {
            SideMysteries.Register();
            var director = CaseDirector.Instance;
            if (director == null) return;
            if (director.IsGenerated) Place();
            else director.CaseGenerated += Place;
        }

        private void Place()
        {
            var director = CaseDirector.Instance;
            var mimic = director.Mimic;
            if (mimic == null) return;
            string place = director.HidingPlace;

            // Where it lives: the family farmhouse, or the church for Father Crane.
            Transform inside, kitchen;
            if (place.StartsWith("Cellar:"))
            {
                string family = place.Substring(7);
                inside = ScheduleSpot.Find($"House:{family}:Inside")?.transform;
                kitchen = ScheduleSpot.Find($"House:{family}:Kitchen")?.transform;
            }
            else
            {
                inside = ScheduleSpot.Find("Church:Pew2")?.transform;
                kitchen = ScheduleSpot.Find("Church:Altar")?.transform;
            }

            if (place == "Cellar:Dobbs")
            {
                var tally = FindObjectsByType<EvidenceItem>(FindObjectsInactive.Include).FirstOrDefault(e => e.Id == "wendell_tally");
                tally?.AppendText(" The last page is in a different hand. Neater. Steadier. It just says: 212.");
            }
            // Whoever tells the Mrs. Calder story this time (it may not be Lyle, if Lyle isn't Lyle).
            var window = FindObjectsByType<EvidenceItem>(FindObjectsInactive.Include).FirstOrDefault(e => e.Id == "diner_window_marks");
            if (window != null)
            {
                var teller = director.Residents.FirstOrDefault(r => r.GetTopics().Any(t => t.title == ResidentProfile.TopicStrange && t.response.Contains("Mrs. Calder")));
                window.ReplaceText("{teller}", teller != null ? teller.FirstName : "Somebody");
            }
            // The Dobbs house stays shut until you've read what Wendell left behind, whoever the mimic is.
            if (Save.SaveSystem.Flags.Contains("evidence:bell_note") || Save.SaveSystem.Flags.Contains("evidence:wendell_tally")) UnlockDobbs();
            Core.GameFlow.GameResumed += () =>
            {
                if (Save.SaveSystem.Flags.Contains("evidence:bell_note") || Save.SaveSystem.Flags.Contains("evidence:wendell_tally")) UnlockDobbs();
            };
            if (inside == null) Debug.LogWarning($"[Semblance] No 'inside' spot for {place}: shed skin not placed.");
            if (kitchen == null) Debug.LogWarning($"[Semblance] No 'kitchen' spot for {place}: sketches not placed.");

            if (inside != null)
            {
                var skin = Prop("Shed Skin", OnFloor(inside.position + inside.right * 1.1f + inside.forward * 0.8f, 0.04f), new Vector3(0.5f, 0.04f, 0.18f), new Color(0.82f, 0.76f, 0.62f));
                skin.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                // Filed under the household: it says which family, not which person.
                skin.AddComponent<EvidenceItem>().Configure("shed_skin", "Shed skin",
                    "A long curl of dry, papery skin, like a snake's, but with a fingerprint whorl still on one end. Whoever lives here sheds.",
                    CaseDirector.Household(mimic), "Examine the floor", 12f, 8f);
            }

            if (kitchen != null && GameSettings.Difficulty != Difficulty.Hard)
            {
                // Spread across the kitchen table (or, for Father Crane, the altar), never floating beside it.
                var papers = Prop("Sketches", OnSurface(kitchen.position, new[] { "Kitchen Table", "Altar" }, 3f, 0.03f, new Vector3(0.35f, 0, 0)), new Vector3(0.4f, 0.03f, 0.3f), new Color(0.88f, 0.86f, 0.78f));
                papers.transform.rotation = Quaternion.Euler(0, Random.Range(-25f, 25f), 0);
                // The drawings never say whose face it is: they describe it, and the detective has to match it to the records.
                papers.AddComponent<EvidenceItem>().Configure("practice_sketches", "Pencil sketches",
                    $"Dozens of pencil drawings of the same face, over and over, more accurate each time. {FaceDescription(mimic)} {DrawingDetail(mimic)}",
                    CaseDirector.Household(mimic), "Look at the papers", 10f, 10f);
            }

            // The real resident.
            var room = CellarRoom.Find(place);
            if (room != null && room.VictimSpot != null)
            {
                SpawnVictim(mimic, room.VictimSpot);
                // "It turns the bulb off when it leaves." The prisoner waits in the dark; bring a flashlight.
                // A couple of empty cellars have dead bulbs too, so darkness alone proves nothing.
                var dark = new System.Collections.Generic.List<CellarRoom> { room };
                var rng = new System.Random((CaseDirector.Instance != null ? CaseDirector.Instance.Seed : 0) * 17 + 5);
                var empty = CellarRoom.All.Where(c => c != room).OrderBy(c => c.Id).ToList();
                for (int i = 0; i < 2 && empty.Count > 0; i++)
                {
                    int k = rng.Next(empty.Count);
                    dark.Add(empty[k]);
                    empty.RemoveAt(k);
                }
                // Padlocked until the body is found: the captive is the last act, not the first evening.
                var clock = GameClock.Instance;
                bool open = Save.SaveSystem.Flags.Contains("padlocks:off") || (clock != null && clock.Day >= StoryBeats.BodyDay);
                foreach (var hatch in FindObjectsByType<CellarHatch>(FindObjectsInactive.Include))
                    if (hatch.GoesDown && hatch.Destination != null && dark.Exists(c => hatch.Destination.IsChildOf(c.transform)))
                    {
                        if (open) hatch.ShowOpenPadlock();
                        else hatch.SetPadlock(true);
                    }
                foreach (var cellar in dark)
                {
                    foreach (var flicker in cellar.GetComponentsInChildren<Rendering.FlickeringLight>(true)) flicker.enabled = false;
                    foreach (var bulb in cellar.GetComponentsInChildren<Light>(true)) bulb.enabled = false;
                    foreach (var r in cellar.GetComponentsInChildren<Renderer>(true))
                        if (r.name == "Bulb")
                        {
                            r.material.SetColor("_EmissionColor", Color.black);
                            r.material.SetColor("_BaseColor", new Color(0.12f, 0.11f, 0.1f));
                        }
                }
            }
        }

        /// <summary>Three of the face's recorded details, written as a stranger would see them, with no name.</summary>
        private static string FaceDescription(Resident mimic)
        {
            var lines = mimic.Profile.features
                .Where(f => f.inArchive && FeatureCatalog.Get(f.id)?.Physical == true)
                .OrderBy(f => Core.StableHash.Of(f.id, CaseDirector.Instance != null ? CaseDirector.Instance.Seed : 0))
                .Take(3)
                .Select(f => Faceless(FeatureCatalog.Get(f.id).Describe(f.side)))
                .ToList();
            return lines.Count == 0 ? "The face has no features drawn in at all." : "In every one: " + string.Join(" ", lines);
        }

        /// <summary>"Their left eye is..." becomes "The left eye is...", "They're bald." becomes "The head is bald."</summary>
        private static string Faceless(string line)
        {
            if (line.StartsWith("Their ")) return "The " + line.Substring(6);
            if (line.StartsWith("They're ")) return "Whoever it is, they're " + line.Substring(8);
            if (line.StartsWith("They ")) return "Whoever it is, they " + line.Substring(5);
            return line;
        }

        private static string DrawingDetail(Resident mimic)
        {
            var sided = mimic.Profile.features.FirstOrDefault(f => f.side != FeatureSide.None && FeatureCatalog.Get(f.id)?.Physical == true);
            if (sided == null) return "In the early ones the eyes are left blank.";
            var def = FeatureCatalog.Get(sided.id);
            var wrong = sided.side == FeatureSide.Left ? FeatureSide.Right : FeatureSide.Left;
            return $"In the early ones, \"{Faceless(def.Describe(wrong))}\" Somebody has scribbled that out so hard the pencil tore the paper.";
        }

        /// <summary>Wendell's door comes unbarred once you've read his note or his tally book.</summary>
        public static void UnlockDobbs()
        {
            var inside = ScheduleSpot.Find("House:Dobbs:Inside");
            if (inside == null) return;
            foreach (var door in inside.transform.parent.GetComponentsInChildren<World.Door>(true))
                door.SetLocked(false);
        }

        /// <summary>The top of a named surface near a point (a table, the altar), or the floor below if there's none.</summary>
        private static Vector3 OnSurface(Vector3 around, string[] names, float radius, float thickness, Vector3 offset)
        {
            Collider best = null;
            foreach (var c in Physics.OverlapSphere(around, radius, ~0, QueryTriggerInteraction.Ignore))
                if (System.Array.IndexOf(names, c.name) >= 0 && (best == null || Vector3.Distance(c.bounds.center, around) < Vector3.Distance(best.bounds.center, around)))
                    best = c;
            if (best == null) return OnFloor(around, thickness);
            var b = best.bounds;
            var p = b.center + best.transform.rotation * offset;
            return new Vector3(p.x, b.max.y + thickness * 0.5f + 0.002f, p.z);
        }

        /// <summary>Drops a point onto whatever solid surface is below it, ignoring people standing there.</summary>
        private static Vector3 OnFloor(Vector3 p, float thickness)
        {
            var hits = Physics.RaycastAll(new Vector3(p.x, p.y + 1.2f, p.z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<Resident>() != null || hit.collider.GetComponentInParent<CharacterController>() != null) continue;
                return hit.point + Vector3.up * (thickness * 0.5f + 0.002f);
            }
            return p;
        }

        private GameObject Prop(string name, Vector3 position, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = size;
            var mat = new Material(shader != null ? shader : Shader.Find("Semblance/PS1 Lit"));
            mat.SetColor("_BaseColor", color);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2.2f, 8f, 2.6f);
            return go;
        }

        private void SpawnVictim(Resident mimic, Transform spot)
        {
            var go = new GameObject("The Real " + mimic.DisplayName);
            go.SetActive(false);
            go.transform.SetPositionAndRotation(spot.position, spot.rotation * Quaternion.Euler(0, 0, 90f));
            var avatar = go.AddComponent<ResidentAvatar>();
            avatar.SetProfile(mimic.Profile, shader);
            var mods = new RunModifications();
            mods.AddedFeatures.Add(new FeatureInstance("pale"));
            mods.AddedFeatures.Add(new FeatureInstance("insomnia"));
            mods.AddedFeatures.Add(new FeatureInstance("dark_stains"));
            avatar.ApplyRunModifications(mods);
            go.SetActive(true);

            var trigger = new GameObject("Victim Trigger");
            trigger.transform.SetParent(spot, false);
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            // Fit to the body: lying on its side it runs along the spot's -x from the hips. The prompt should only
            // appear when you're actually looking at them, not at the floor beside them.
            box.size = new Vector3(1.9f, 0.8f, 0.9f);
            box.center = new Vector3(-0.8f, 0.2f, 0);
            trigger.AddComponent<MissingPerson>().Configure(mimic);
            if (Save.SaveSystem.Flags.Contains("found:missing"))
                EndingDirector.MissingPersonFound = true;
        }
    }
}
