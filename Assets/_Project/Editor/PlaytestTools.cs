using System.Linq;
using System.Reflection;
using System.Text;
using Semblance.Core;
using Semblance.Dialogue;
using Semblance.Interaction;
using Semblance.Notes;
using Semblance.Residents;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Semblance.EditorTools
{
    /// <summary>
    /// Development helpers for playtesting through the editor bridge: walk somewhere (charging the clock for
    /// the time it would take on foot), look at things, talk, and capture what's on screen.
    /// </summary>
    public static class PlaytestTools
    {
        public const float WalkSpeed = 2.4f;
        public const float SprintSpeed = 4.2f;
        public static float RealSecondsWalked;

        private static Transform Player => GameObject.Find("Player").transform;

        /// <summary>Walks to a point along the navmesh. Returns metres travelled. Advances game time.</summary>
        public static float WalkTo(Vector3 target, bool sprint = true)
        {
            var p = Player;
            float distance = Vector3.Distance(p.position, target);
            var path = new NavMeshPath();
            if (NavMesh.SamplePosition(p.position, out var a, 3f, NavMesh.AllAreas) && NavMesh.SamplePosition(target, out var b, 3f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                distance = 0;
                for (int i = 1; i < path.corners.Length; i++) distance += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }
            float seconds = distance / (sprint ? SprintSpeed : WalkSpeed);
            RealSecondsWalked += seconds;
            var clock = GameClock.Instance;
            if (clock != null) clock.Advance(seconds / 60f);
            Place(target, Quaternion.LookRotation(Vector3.ProjectOnPlane(target - p.position, Vector3.up).sqrMagnitude > 0.01f ? Vector3.ProjectOnPlane(target - p.position, Vector3.up) : p.forward));
            return distance;
        }

        private static double pushUntil;
        private static Vector3 pushDir;
        private static bool pushing;

        /// <summary>Shove the character controller in a direction for a while, as a held key would (editor ticks).</summary>
        private static float pushSpeed = WalkSpeed;

        public static void Push(Vector3 direction, float seconds, float speed = WalkSpeed)
        {
            pushSpeed = speed;
            pushDir = direction.normalized;
            pushUntil = EditorApplication.timeSinceStartup + seconds;
            if (!pushing) { pushing = true; EditorApplication.update += PushStep; }
        }

        private static void PushStep()
        {
            var cc = Player.GetComponent<CharacterController>();
            if (EditorApplication.timeSinceStartup > pushUntil || cc == null || !cc.enabled) { pushing = false; EditorApplication.update -= PushStep; return; }
            cc.Move((pushDir * pushSpeed + Vector3.down * 2f) * 0.016f);
        }

        public static void Place(Vector3 position, Quaternion rotation, float pitch = 0f)
        {
            var p = Player;
            var cc = p.GetComponent<CharacterController>();
            cc.enabled = false;
            p.SetPositionAndRotation(position, rotation);
            cc.enabled = true;
            p.Find("Camera Root").localRotation = Quaternion.Euler(pitch, 0, 0);
            Physics.SyncTransforms();
        }

        /// <summary>Stand in front of a resident and face them.</summary>
        public static Resident Approach(string name, float distance = 1.6f)
        {
            var r = Resident.FindByName(name);
            if (r == null) return null;
            Vector3 front = r.transform.position + r.transform.forward * distance;
            if (!NavMesh.SamplePosition(front, out var hit, 1.5f, NavMesh.AllAreas)) hit.position = front;
            WalkTo(hit.position);
            var look = Vector3.ProjectOnPlane(r.transform.position - hit.position, Vector3.up);
            Place(hit.position, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : -r.transform.forward), 6f);
            return r;
        }

        public static string Talk(string name, params string[] topics)
        {
            var r = Resident.FindByName(name);
            var ui = DialogueUI.Instance;
            ui.Open(r, Player.GetComponent<PlayerInteractor>());
            var sb = new StringBuilder();
            var body = ui.transform.Find("Dialogue Canvas/Dialogue Panel/Line").GetComponent<TMPro.TextMeshProUGUI>();
            sb.AppendLine($"[{r.DisplayName}] {Notebook.StripTags(body.text)}");
            var choose = typeof(DialogueUI).GetMethod("Choose", BindingFlags.NonPublic | BindingFlags.Instance);
            var available = r.GetTopics().Select(t => t.title).ToList();
            foreach (var topic in topics)
            {
                int index = topic == "observe" ? available.Count : available.IndexOf(topic);
                if (index < 0) { sb.AppendLine($"  ({topic}: not available)"); continue; }
                choose.Invoke(ui, new object[] { index });
                sb.AppendLine($"  > {topic}: {Notebook.StripTags(body.text)}");
                GameClock.Instance?.Advance(0.05f); // a conversation beat takes a few minutes
            }
            ui.Close();
            return sb.ToString();
        }

        public static string Topics(string name) => string.Join(" | ", Resident.FindByName(name).GetTopics().Select(t => t.title));

        public static string Shot(string file, int width = 960, int height = 540) => DevCapture.CaptureScreen("Temp/Playtest/" + file + ".png", width, height);

        public static string Status()
        {
            var clock = GameClock.Instance;
            var nerve = Events.Nerve.Instance;
            return $"{clock?.Label} | {World.LocationMarker.Describe(Player.position)} | nerve {(nerve != null ? nerve.Value : 0):F0} | film {Semblance.Player.PhotoCamera.Instance?.Film} | notes {Notebook.Instance?.Entries.Count} | walked {RealSecondsWalked / 60f:F1} real min";
        }
    }
}
