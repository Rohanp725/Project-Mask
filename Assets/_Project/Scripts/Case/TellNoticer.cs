using System.Collections.Generic;
using System.Linq;
using Semblance.Core;
using Semblance.Notes;
using Semblance.Player;
using Semblance.Residents;
using Semblance.UI;
using UnityEngine;

namespace Semblance.Case
{
    /// <summary>
    /// Behaviour tells (never blinking, not breathing, a head that turns too far, no reflection) are hard to see at
    /// this resolution. If the detective studies someone closely (holding right-click to focus, up close) for long
    /// enough, they consciously notice. It's the player's choice of whom to study that matters, so it never fires
    /// on its own, and it can be wrong: one nervous innocent per case reads just as strange.
    /// Hard needs a longer, steadier look.
    /// </summary>
    public class TellNoticer : MonoBehaviour
    {
        [SerializeField] private float secondsNeeded = 18f;
        [SerializeField] private float hardSecondsNeeded = 32f;
        [SerializeField] private float range = 4f;

        private readonly Dictionary<Resident, float> watched = new Dictionary<Resident, float>();
        private readonly HashSet<string> noticed = new HashSet<string>();
        private FirstPersonController controller;
        private float mirrorWatch, focusTime;
        private Rendering.PlanarMirror[] mirrors;

        private void Update()
        {
            var director = CaseDirector.Instance;
            var cam = Camera.main;
            if (director == null || cam == null || director.Mimic == null) return;
            if (controller == null) controller = cam.GetComponentInParent<FirstPersonController>();
            if (controller == null || !controller.IsFocusing || InputLock.IsLocked) return;

            CheckMirror(director, cam);

            // Whoever is in the middle of the focused view, close enough to study.
            Resident target = null;
            float best = 0.97f;
            foreach (var r in Resident.All)
            {
                Vector3 face = r.transform.position + Vector3.up * 1.6f;
                Vector3 to = face - cam.transform.position;
                if (to.magnitude > range) continue;
                float dot = Vector3.Dot(cam.transform.forward, to.normalized);
                if (dot > best) { best = dot; target = r; }
            }
            if (target == null) return;
            focusTime += Time.unscaledDeltaTime;
            if (focusTime > 2.5f && !Save.SaveSystem.Flags.Contains("hint:focus"))
            {
                Save.SaveSystem.Flags.Add("hint:focus");
                PlayerHUD.Instance?.SetHint($"Keep studying {target.FirstName}.\n<size=75%>Hold your look on someone for a while (twenty seconds or so) and you may notice what a glance would miss.</size>", 8f);
            }
            if (target != director.Mimic && (director.Noise == null || target != director.Noise.NoticerDecoy)) return;

            watched.TryGetValue(target, out float seconds);
            seconds += Time.unscaledDeltaTime;
            watched[target] = seconds;
            float needed = GameSettings.Difficulty == Difficulty.Hard ? hardSecondsNeeded : secondsNeeded;
            if (seconds < needed) return;
            watched[target] = 0f;

            string text = target == director.Mimic ? MimicLine(target) : DecoyLine(target);
            if (text == null) return;
            Record(target, text);
        }

        private string MimicLine(Resident mimic)
        {
            string obj = mimic.Profile.possessive switch { "his" => "him", "her" => "her", _ => "them" };
            foreach (var feature in mimic.Avatar.ActiveFeatures)
            {
                string text = feature.id switch
                {
                    "never_blinks" => $"I've been watching {mimic.FirstName}'s face a long while. I don't think I've seen {obj} blink. Not once. I could be wrong.",
                    "no_breathing" => $"Standing this close, I can't hear {mimic.FirstName} breathe. I don't think the chest moved. I could be imagining it.",
                    "head_snaps" => $"When I moved, {mimic.FirstName}'s head followed. Too quick, I thought. Too far round. Maybe.",
                    _ => null,
                };
                if (text != null && noticed.Add("mimic|" + feature.id)) return text;
            }
            return null;
        }

        /// <summary>The misfire: someone nervous or twitchy reads just as wrong when you stare at them long enough.</summary>
        private string DecoyLine(Resident r)
        {
            if (!noticed.Add("decoy|" + r.DisplayName)) return null;
            string obj = r.Profile.possessive switch { "his" => "him", "her" => "her", _ => "them" };
            string[] lines =
            {
                $"I've been watching {r.FirstName} a long while. Something about the way {obj} holds still bothers me. I could be wrong.",
                $"When I moved, {r.FirstName}'s eyes followed. Too quick, I thought. Maybe it's just nerves.",
                $"I don't think I saw {r.FirstName} blink for a long time there. Then {r.FirstName} did. I think.",
            };
            return StableHash.Pick(lines, r.DisplayName, CaseDirector.Instance.Seed);
        }

        /// <summary>No reflection: caught when the mimic stands in front of Mae's mirror and you're looking at the glass.</summary>
        private void CheckMirror(CaseDirector director, Camera cam)
        {
            if (!director.MimicHasNoReflection || noticed.Contains("mirror")) return;
            var mimic = director.Mimic;
            if (!mimic.isActiveAndEnabled) return;
            if (mirrors == null) mirrors = FindObjectsByType<Rendering.PlanarMirror>(FindObjectsInactive.Exclude);
            foreach (var mirror in mirrors)
            {
                Vector3 m = mirror.transform.position;
                Vector3 to = m - cam.transform.position;
                if (to.magnitude > 9f || Vector3.Dot(cam.transform.forward, to.normalized) < 0.9f) continue;
                if (Vector3.Distance(mimic.transform.position, m) > 6f) continue;
                mirrorWatch += Time.unscaledDeltaTime;
                if (mirrorWatch < 4f) return;
                noticed.Add("mirror");
                Record(mimic, $"In Mae's mirror I can see the stools, the counter, the coffee pot. I can't see {mimic.FirstName}. {mimic.FirstName} is sitting right there.");
                return;
            }
        }

        private static void Record(Resident r, string text)
        {
            Notebook.Instance?.Record(NoteKind.Observation, r.DisplayName, "Something I noticed", text, null, "noticed|" + r.DisplayName + "|" + StableHash.Of(text));
            PlayerHUD.Instance?.Toast($"<i>Something about {r.FirstName}... You note it down.</i>");
            Audio.GameAudio.Play("sting_soft", 0.25f);
        }
    }
}
