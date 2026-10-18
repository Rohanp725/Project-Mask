using System.Linq;
using System.Text;
using Semblance.Notes;
using Semblance.Save;
using Semblance.UI;
using UnityEngine;

namespace Semblance.Investigation
{
    /// <summary>
    /// The smaller mysteries of Semblance. Each resolves once all its clues are found, usually into something
    /// human (grief, fear, obsession), sometimes into something that points back at the creature.
    /// </summary>
    public static class SideMysteries
    {
        public sealed class Mystery
        {
            public string Id;
            public string Title;
            public string Question;
            public string[] Clues;
            public string Resolution;
        }

        public static readonly Mystery[] All =
        {
            new Mystery
            {
                Id = "ruth", Title = "Ruth Calder", Question = "Where is Ruth Calder?",
                Clues = new[] { "grave_headstone", "calder_shoes", "calder_certificate" },
                Resolution = "Ruth Calder died of heart failure in August. Amos couldn't stand for the town to take her from him, so he buried her himself in the churchyard at night and chiselled her name off the stone so nobody would come asking. He still buys her shoes. He isn't what you're hunting. He's just a man who can't let go.",
            },
            new Mystery
            {
                Id = "cattle", Title = "The Kessler cattle", Question = "What's killing Greta's cattle?",
                Clues = new[] { "kessler_carcass", "kessler_vet_report", "kessler_fence" },
                Resolution = "Something has been feeding on Greta's herd at night through two neat punctures behind the jaw, taking everything and leaving the bodies standing. It started in June. It isn't an animal, and fourteen cows won't be enough for it forever. If those two holes turn up on anything else, you'll know it's stopped practising.",
            },
            new Mystery
            {
                Id = "bell", Title = "The count", Question = "Who rings the church bell at night, and why?",
                Clues = new[] { "bell_note", "wendell_tally" },
                Resolution = "Wendell Dobbs rings the bell at three in the morning and counts who comes running. Every soul comes: 212. But since September someone has been painting 213 on the welcome sign at night, and however often Wendell paints it back, it comes back. Wendell wrote the letter that brought you here. Whatever came to Semblance isn't content to hide among them. It wants to be counted.",
            },
            new Mystery
            {
                Id = "mirrors", Title = "The missing mirrors", Question = "Who bought every mirror in town?",
                Clues = new[] { "store_rack", "store_receipt" },
                Resolution = "The mirrors were bought in the night with exact change and never seen again, along with a quart of red enamel: the red on the welcome sign. Mirrors show things as they are, and something in this town doesn't want to be seen. Mae's is the last mirror left. It might be worth watching who passes in front of it.",
            },
            new Mystery
            {
                Id = "window", Title = "The face at the window", Question = "What did Lyle see at the diner window?",
                Clues = new[] { "diner_window_marks", "grave_soil" },
                Resolution = "Whatever stood at the diner window wore Ruth Calder's face, seven feet up. And somebody, or something, has been at the churchyard at night, digging at a fresh grave with very long fingers. It practises faces. It learns them from somewhere.",
            },
            new Mystery
            {
                Id = "room3", Title = "Room 3", Question = "Who checked into Room 3 in June?",
                Clues = new[] { "motel_register", "semple_case", "semple_bed" },
                Resolution = "On June 2nd a brush salesman called H. Semple took Room 3 at the Pale Moon and never checked out. He never sold a brush. The cattle started dying that week. Whatever lay in that bed all summer, perfectly still, was listening to the town through the wall, learning how people talk. By autumn it was ready to be one of them. And it was the one that paid for your room.",
            },
        };

        public static void Register()
        {
            NotebookUI.CaseFileSections.Add(BuildSection);
        }

        public static bool IsSolved(Mystery m) => m.Clues.All(c => SaveSystem.Flags.Contains("evidence:" + c));

        public static void OnClueFound(string clueId)
        {
            foreach (var m in All)
            {
                if (!m.Clues.Contains(clueId)) continue;
                string flag = "solved:" + m.Id;
                if (IsSolved(m) && !SaveSystem.Flags.Contains(flag))
                {
                    SaveSystem.Flags.Add(flag);
                    PlayerHUD.Instance?.Toast($"Side mystery solved: <b>{m.Title}</b>. See the Case File.");
                    Notebook.Instance?.Record(NoteKind.Evidence, Notebook.CaseSubject, "Solved: " + m.Title, ResolutionFor(m), null, "solved|" + m.Id);
                }
            }
        }

        /// <summary>Some resolutions read differently depending on who the mimic is this time.</summary>
        private static string ResolutionFor(Mystery m)
        {
            var mimic = Case.CaseDirector.Instance != null ? Case.CaseDirector.Instance.Mimic : null;
            if (m.Id == "mirrors" && (Case.CaseDirector.Instance == null || !Case.CaseDirector.Instance.MimicHasNoReflection))
                return "The mirrors were bought in the night with exact change and never seen again, along with a quart of red enamel: the red on the welcome sign. Mirrors show things as they are, and something in this town doesn't want to be seen.";
            if (m.Id == "bell" && mimic != null && mimic.DisplayName == Case.CastNames.Wendell)
                return m.Resolution + " The bell hasn't rung at three in the morning for over a week. Wendell, or whatever comes down from the Dobbs place now, has stopped counting.";
            return m.Resolution;
        }

        /// <summary>
        /// Nothing is listed until you've touched a piece of it: just how many there are. Once a clue turns up, the
        /// question appears with the evidence found so far, and the answer once the last piece is in.
        /// </summary>
        private static string BuildSection()
        {
            int solved = All.Count(IsSolved);
            int opened = All.Count(m => m.Clues.Any(c => SaveSystem.Flags.Contains("evidence:" + c)));
            var sb = new StringBuilder($"<size=110%><b>Side mysteries</b></size>  <size=80%>{solved}/{All.Length} solved, {opened} open</size>\n");
            if (opened == 0)
            {
                sb.AppendLine("<i>Nothing yet. There's more wrong with this town than the one thing you came for.</i>");
                return sb.ToString();
            }
            var notebook = Notebook.Instance;
            foreach (var m in All)
            {
                var found = m.Clues.Where(c => SaveSystem.Flags.Contains("evidence:" + c)).ToList();
                if (found.Count == 0) continue;
                bool done = IsSolved(m);
                sb.AppendLine();
                sb.AppendLine($"<b>{m.Question}</b>  <size=80%><color=#00000088>{found.Count}/{m.Clues.Length}{(done ? ", solved" : "")}</color></size>");
                foreach (var id in found)
                {
                    var entry = notebook?.Entries.FirstOrDefault(e => e.key == "evidence|" + id);
                    sb.AppendLine($"   - {(entry != null ? entry.title : id.Replace('_', ' '))}");
                }
                if (done)
                    sb.AppendLine($"   <color=#2f4f2a>{ResolutionFor(m)}</color>");
            }
            return sb.ToString();
        }
    }
}
