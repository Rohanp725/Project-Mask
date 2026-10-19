using System.Linq;
using System.Text;
using Semblance.Notes;
using Semblance.Save;
using Semblance.UI;
using UnityEngine;

namespace Semblance.Investigation
{
    /// <summary>
    /// The smaller mysteries of Semblance. Each one looks, from the outside, like the thing you came for: a wife
    /// nobody has seen since August, children drawing a fifth grown-up, a husband whose hands are never warm. Most
    /// of them resolve into something human (grief, fear, a lie told for love), and the resolution says so, but it
    /// never swears the person is who they were. One or two are the creature's own tracks.
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
            /// <summary>The person the mystery seems to point at; the resolution leaves the door ajar on them.</summary>
            public string Person;
        }

        public static readonly Mystery[] All =
        {
            new Mystery
            {
                Id = "ruth", Title = "Ruth Calder", Question = "Where is Ruth Calder?", Person = Case.CastNames.Amos,
                Clues = new[] { "grave_headstone", "calder_certificate", "calder_shoes", "calder_knitting" },
                Resolution = "Ruth Calder died of heart failure on the ninth of August. Amos couldn't stand for the town to take her from him, so Doc wrote the certificate and never sent it, and Amos buried her himself in the churchyard at night and chiselled her name off the stone so nobody would come asking. He tells people she's in Tulsa. He still buys her shoes. The knitting that grows by itself is his: Amos is left-handed, and a left-handed man knits a right-handed woman's sleeve backwards, badly at first and then not badly at all, every night, because it's the last thing of hers still being made. This is a man who can't let go. If that's still Amos.",
            },
            new Mystery
            {
                Id = "marsh", Title = "The fifth figure", Question = "Who is the tall grey one the Marsh children keep drawing?", Person = Case.CastNames.June,
                Clues = new[] { "marsh_drawings", "mailbox_Marsh", "marsh_crayons", "marsh_grave" },
                Resolution = "There were five Marsh children. Daniel drowned in the creek in the summer of 1979, aged eight, and June has never once said his name to the four that came after; she tells people there are four of them, four, and she believes it while she's saying it. The eldest girl found his crayons in the attic and has been drawing him the way she imagines him now: grown, grey, standing a little behind the family, holding her mother's hand. 'The other one.' The grey crayon is his. There's nothing in that house but a boy who isn't there and a mother who can't let him be. Unless something has noticed how easily a family makes room for one more.",
            },
            new Mystery
            {
                Id = "hollis", Title = "Harlan's hands", Question = "Why does Harlan Hollis come to bed cold?", Person = Case.CastNames.Harlan,
                Clues = new[] { "hollis_hymnal", "mailbox_Hollis", "hollis_bedroll" },
                Resolution = "Harlan hasn't slept in his bed since September. He sleeps in the end stall of the barn with the shotgun and a thermos, because something walks the corn at night and doesn't trip the wire, and he'd rather meet it than wake to it. He comes in at dawn cold as the pump handle and lies down beside Nell for an hour so she'll think he was there. She knows. She writes 'warm' in her hymnal on the mornings it's true, and in October she stopped writing because there weren't any, and she'd rather not know what the cold ones mean. The little one still posts her teeth to a mailman who stopped coming. Nell leaves the pennies herself. This is a frightened house, not a wrong one. Unless Harlan has stopped being the one who's frightened.",
            },
            new Mystery
            {
                Id = "whitlock", Title = "Mae's breakfast", Question = "What is Ada afraid is happening to her mother?", Person = Case.CastNames.Mae,
                Clues = new[] { "whitlock_suitcase", "mailbox_Whitlock", "whitlock_recipe" },
                Resolution = "Mae stopped sleeping when Earl died and never started again. She walks the house at night, rubs Earl's face off his photograph with her thumb, writes 'cherry' four times under his mother's recipe without knowing she's written it, and stands at the diner window looking at the road. Ada watches all of it from a packed suitcase she bought a ticket for three years ago and has never used, and writes her mother notes about a cat that died, and wrote 'ask Mae what she had for breakfast' on the menu because she needed a stranger to tell her whether her mother was still in there. She is. She eats pie for breakfast and she's still in there. Grief, not the other thing. Though the other thing would look exactly like this from the outside, and it knows that.",
            },
            new Mystery
            {
                Id = "vance", Title = "The porch light", Question = "Why is Irene Vance's mailbox full of dead moths?", Person = Case.CastNames.Irene,
                Clues = new[] { "vance_case", "mailbox_Vance", "vance_card" },
                Resolution = "Carl Vance died in 1979 and Irene has left the porch light on every night since, the way you'd leave it for a man coming home late. Moths come to it and die in the box beneath it, and she keeps every one, and pins the best of them, because it's the only thing that still comes to the house. On the twenty-second of September one came off the pin and looked at her. She's told nobody but you. She isn't lying and she isn't mad; she's a woman who has been alone with a light on for four years. What came off the pin is another matter.",
            },
            new Mystery
            {
                Id = "thorne", Title = "The blurred man", Question = "Why is Silas Thorne a smear in every photograph?", Person = Case.CastNames.Silas,
                Clues = new[] { "thorne_photo", "mailbox_Thorne", "thorne_album" },
                Resolution = "Silas came back from Korea with a tremor he's hidden for thirty years by never holding still: he moves before the shutter, every time, and the family album is full of a man with no face. Bev took the 1979 picture out of her own album and put it in the mailbox where you'd find it, then told you somebody else had, because she's frightened of her husband and couldn't say so: he takes engines apart all night and drinks and doesn't sleep, and she wanted a detective to look at him without her having to ask. He's a man who shakes. That's all the photographs ever showed. Whether it's still all there is to see is your business, not the camera's.",
            },
            new Mystery
            {
                Id = "cattle", Title = "The Kessler cattle", Question = "What's killing Greta's cattle?", Person = Case.CastNames.Greta,
                Clues = new[] { "kessler_carcass", "kessler_vet_report", "kessler_fence", "mailbox_Kessler" },
                Resolution = "Something has been feeding on Greta's herd at night through two neat punctures behind the jaw, taking everything and leaving the bodies standing. It started the week the salesman came, it comes on the nights the bell rings, and it steps over a fence rather than through it. The vet wrote 'undetermined' because the other word would have got him laughed out of Barlow. Greta has notched her rifle fourteen times and shot at it twice from the porch, and it didn't mind. She is the only person in Semblance who has looked straight at it and said what it was, and nobody believed the Dutch widow. It isn't an animal, and fourteen cows won't be enough for it forever.",
            },
            new Mystery
            {
                Id = "bell", Title = "The count", Question = "Who rings the church bell at night, and why?", Person = Case.CastNames.Wendell,
                Clues = new[] { "bell_note", "wendell_tally", "hymn_card" },
                Resolution = "Wendell Dobbs rings the bell at three in the morning and counts who comes running. Every soul comes: 212. But since September someone has been painting 213 on the welcome sign, and putting a red card on the hymn board for a hymn that doesn't exist, and however often Wendell paints the sign back, it comes back. Wendell wrote the letter that brought you here. Whatever came to Semblance isn't content to hide among them. It wants to be counted.",
            },
            new Mystery
            {
                Id = "mirrors", Title = "The missing mirrors", Question = "Who bought every mirror in town?", Person = Case.CastNames.Otis,
                Clues = new[] { "store_rack", "store_receipt" },
                Resolution = "The mirrors were bought in the night with cold exact change and never seen again, along with a quart of red enamel: the red on the welcome sign. Mirrors show things as they are, and something in this town doesn't want to be seen. Mae's is the last mirror left. It might be worth watching who passes in front of it.",
            },
            new Mystery
            {
                Id = "window", Title = "The face at the window", Question = "What did Lyle see at the diner window?", Person = Case.CastNames.Lyle,
                Clues = new[] { "diner_window_marks", "grave_soil" },
                Resolution = "Whatever stood at the diner window wore Ruth Calder's face, seven feet up, and Lyle said her name before he could stop himself. And something has been at the churchyard at night, digging at a fresh grave with very long fingers. It practises faces. It learns them from somewhere.",
            },
            new Mystery
            {
                Id = "room3", Title = "Room 3", Question = "Who checked into Room 3 in June?", Person = null,
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
                int found = m.Clues.Count(c => SaveSystem.Flags.Contains("evidence:" + c));
                if (found == 1 && !SaveSystem.Flags.Contains("opened:" + m.Id))
                {
                    SaveSystem.Flags.Add("opened:" + m.Id);
                    PlayerHUD.Instance?.Toast($"A question for the Case File: <b>{m.Question}</b>");
                }
                if (IsSolved(m) && !SaveSystem.Flags.Contains(flag))
                {
                    SaveSystem.Flags.Add(flag);
                    PlayerHUD.Instance?.Toast($"Side mystery solved: <b>{m.Title}</b>. See the Case File.");
                    Notebook.Instance?.Record(NoteKind.Evidence, Notebook.CaseSubject, "Solved: " + m.Title, ResolutionFor(m), null, "solved|" + m.Id);
                }
            }
        }

        /// <summary>Some resolutions read differently depending on who the mimic is this time. None of them say who it is.</summary>
        private static string ResolutionFor(Mystery m)
        {
            var mimic = Case.CaseDirector.Instance != null ? Case.CaseDirector.Instance.Mimic : null;
            if (m.Id == "mirrors" && (Case.CaseDirector.Instance == null || !Case.CaseDirector.Instance.MimicHasNoReflection))
                return "The mirrors were bought in the night with cold exact change and never seen again, along with a quart of red enamel: the red on the welcome sign. Mirrors show things as they are, and something in this town doesn't want to be seen.";
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
