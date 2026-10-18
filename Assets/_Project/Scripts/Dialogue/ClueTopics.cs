using System;
using System.Collections.Generic;
using System.Linq;
using Semblance.Core;
using Semblance.Notes;
using Semblance.Residents;
using Semblance.Save;

namespace Semblance.Dialogue
{
    /// <summary>
    /// A question the detective can only ask once they've seen or heard the thing it's about: a note on a door, a
    /// death certificate, a mailbox, a drawing on a wall. Some open a line of questioning (a follow-up that appears
    /// after the first answer, to the same person). Answers never depend on who the mimic is: it has the person's
    /// memories, so the knowledge is the same. What differs is what each person knows, cares about, or hides.
    /// </summary>
    public sealed class ClueQuestion
    {
        public string Key;
        public string Question;
        /// <summary>A follow-up: offered only after the parent key has been asked of this same person.</summary>
        public string Parent;
        /// <summary>First names (or "Father Crane") who get this question; null = anyone with an answer.</summary>
        public string[] Only;
        public Func<bool> Available;
        /// <summary>What anyone not listed in Answers says; null = only the listed people can be asked.</summary>
        public string Default;
        public Dictionary<string, string> Answers = new Dictionary<string, string>();

        public bool OfferedTo(Resident r)
        {
            string name = r.FirstName;
            if (Only != null && Array.IndexOf(Only, name) < 0 && Array.IndexOf(Only, r.LastName) < 0) return false;
            return Answers.ContainsKey(name) || Default != null;
        }

        public string AnswerFor(Resident r) => Answers.TryGetValue(r.FirstName, out var a) ? a : Default;
    }

    public static class ClueTopics
    {
        private static readonly List<ClueQuestion> All = new List<ClueQuestion>();

        private static Func<bool> Flag(string flag) => () => SaveSystem.Flags.Contains(flag);
        private static Func<bool> Seen(string clue) => Flag("seen:" + clue);
        private static Func<bool> Evidence(params string[] ids) => () => ids.Any(id => SaveSystem.Flags.Contains("evidence:" + id));
        private static Func<bool> Any(params Func<bool>[] conditions) => () => conditions.Any(c => c());

        private static ClueQuestion Add(string key, Func<bool> available, string question, string fallback = null, string parent = null, params (string who, string says)[] answers)
        {
            var q = new ClueQuestion { Key = key, Question = question, Available = available, Default = fallback, Parent = parent };
            foreach (var (who, says) in answers) q.Answers[who] = says;
            All.Add(q);
            return q;
        }

        public static string AskedFlag(Resident r, string key) => $"asked|{r.DisplayName}|{key}";

        /// <summary>The questions this person can be asked right now, follow-ups included.</summary>
        public static List<ClueQuestion> For(Resident r)
        {
            Ensure();
            var list = new List<ClueQuestion>();
            foreach (var q in All)
            {
                if (!q.OfferedTo(r) || !q.Available()) continue;
                if (q.Parent != null && !SaveSystem.Flags.Contains(AskedFlag(r, q.Parent))) continue;
                list.Add(q);
            }
            return list;
        }

        private static bool built;

        private static void Ensure()
        {
            if (built) return;
            built = true;

            // ---------------------------------------------------------------- Town centre
            Add("breakfast", Seen("breakfast"), "Somebody wrote 'ask Mae what she had for breakfast' on the diner menu.",
                "Mae? Coffee and a slice of her own pie, same as every morning since Earl. Whoever wrote that wants you to look at her. I'd look at whoever wrote it.",
                null,
                ("Mae", "Coffee. And a slice of yesterday's cherry. Since Earl, that's breakfast. ...Ada wrote that, didn't she. She thinks I'm wasting away. Maybe I am. I'm still me."),
                ("Ada", "That was me. Ask her. Watch her face when she answers. She doesn't eat, Detective. She doesn't sleep. She stands at that window at night looking at the road."),
                ("Otis", "Th-that's Ada's writing. She worries about her m-mother. We all do. Mae's been d-different since Earl. Different isn't wrong."));
            Add("breakfast_ada", Seen("breakfast"), "Why did you want a stranger to ask her?", null, "breakfast",
                ("Ada", "Because if I ask, she tells me I'm being silly, and I believe her. If you ask, and she can't say what she ate... then I'll know I wasn't being silly. That's all. That's all it is."));

            Add("pie", Seen("pie"), "There's a pie on the diner counter with the middle clawed out.",
                "Mae bakes one every night. Something has at it before dawn. She puts it out anyway.",
                null,
                ("Mae", "Every night I bake it, and every dawn it's been at it. Four grooves. I put it out so it knows I saw. That's all I've got, Detective. Letting it know I saw."),
                ("Ada", "She won't throw it out. She says if she throws it out it'll start on something else."));

            Add("earlphoto", Seen("earlphoto"), "The photograph of Earl in the diner. His face is rubbed white.",
                "Thirty years of Mae's thumb. She touches it every night before she locks up.",
                null,
                ("Mae", "Thirty years. You rub a thing enough, it goes. I'd know his face in the dark. I don't need a picture to."),
                ("Ada", "Every night, before the lights. Thumb on his face. She doesn't know she does it."));

            Add("mirrors", Any(Evidence("store_rack", "store_receipt"), Seen("storenotice")), "Every mirror in town was bought up in one night, for cold exact change.",
                "I sold mine before I knew what it was for. Two forty-nine. Everybody did. It's only glass. That's what I told myself.",
                null,
                ("Otis", "L-left on the counter overnight. Fourteen hand, two wall. The coins were c-cold as a well, in August. I put them in the register and I w-washed my hands."),
                ("Mae", "Not mine. Earl's mirror stays where Earl hung it. Whatever wants it can come and ask me to my face."),
                ("Lyle", "Pa says mirrors are vanity. I think whatever bought them doesn't want to be looked at twice."),
                ("Wendell", "Mirrors show what's there. Something doesn't want to be shown. You'd think that would be enough for people."));
            Add("mirrors_otis", Seen("storenotice"), "Why the sign? 'Please don't ask.'", null, "mirrors",
                ("Otis", "Because after that night, people c-came in asking for one. All of them. Like they'd all had the same thought the same m-morning. I didn't want to have to say no sixteen times."));

            Add("windowmarks", Evidence("diner_window_marks"), "The marks on the diner window. Seven feet up.",
                "Something looks in at Mae's at night. Tall. Nobody says what. We walk on the other side of the road after dark.",
                null,
                ("Mae", "I washed them off for a month. They came back by morning, every morning. Now I let them stay. I don't want to know what's on the other side of the glass while I'm scrubbing."),
                ("Lyle", "I said Mrs. Calder. I shouldn't have. Ruth Calder was five foot one, and she's in the ground. I don't know why I said it. It was the first name in my mouth."),
                ("Ada", "Seven feet. I measured. Mama doesn't know I measured."));

            Add("payphone", Seen("payphone"), "The payphone by the road. There's someone breathing under the dial tone.",
                "Don't use that phone. Drive to Barlow if you need to call somebody.",
                null,
                ("Otis", "Line's been c-crossed with something since June. Barlow says there's nothing wrong with it. They tested it from their end. From their end it's f-fine."),
                ("Wendell", "I called the city from that phone. I heard it listening. It let me finish."));

            Add("semple", Evidence("semple_case", "semple_suit", "semple_bed"), "H. Semple. Room 3 at the motel. Checked in June 2nd and never checked out.",
                "The brush salesman. Nobody saw him eat. Nobody saw him leave. The door's been shut since.",
                null,
                ("Otis", "Fuller Brush. He came in once and didn't b-buy anything and didn't sell anything. Just stood there. Looking at how the shelves were l-laid out."),
                ("Father Crane", "He came to one service. Sat at the back by the door. Didn't sing, didn't kneel. Watched our mouths while we sang. I thought he was deaf."),
                ("Mae", "He ate here once. Ordered the special and didn't touch it. Paid in exact change. Cold coins. I remember thinking, where's he been keeping those."),
                ("Wendell", "That's when the count went wrong. June. Everybody looks at September because of the sign. Look at June."));
            Add("semple_crane", Evidence("semple_case", "semple_suit", "semple_bed"), "He watched your mouths?", null, "semple",
                ("Father Crane", "Like a man learning the words. I've had converts do it. He wasn't converting. He left before the blessing and I never saw him again, and I thanked God for it, which I've since confessed."));

            Add("register", Evidence("motel_register"), "The motel register says my room was paid for in advance. In my handwriting.",
                "Then somebody wanted you here badly enough to learn your hand, Detective. That's not a town secret. That's yours.",
                null,
                ("Wendell", "I wrote to the city. I didn't pay for anything. I haven't got it to pay. Somebody read my letter before it went, is what that means."),
                ("Doc", "In your hand. Hm. Then it's seen your hand. Where did you sign your name last, before you came here?"));

            Add("keyboard", Seen("keyboard"), "Room 3's key is gone from the office board. Under the hook it says he's still listening.",
                "Nobody's had that key since June. Nobody's wanted it.",
                null,
                ("Wendell", "I wrote the tag. I sat in the room next door in July. You could hear him. Not moving. Listening. I wanted somebody to know before they took the room."));

            Add("calendar", Seen("calendar"), "The office calendar is still on June. The 2nd is circled.",
                "That's the day the salesman came. Nobody's touched the office since.",
                null,
                ("Wendell", "I circled it. After. When I'd worked out when it started."));

            Add("ashtray", Seen("ashtray"), "Room 4: every cigarette smoked to the same length and put out in a perfect ring.",
                "The motel's been empty since June. Nobody smokes in there that I know of.",
                null,
                ("Wendell", "That was me. July. I sat in Room 4 with my ear to the wall and I smoked, and when I was done I counted them. Twenty. I'd have known if it was nineteen."));

            Add("bible", Seen("bible"), "A Bible in Room 4, open at the wheat and the tares. 'Which is which' in the margin.",
                "Father Crane preached that one in June. Let both grow together until the harvest. Nobody liked it much.",
                null,
                ("Father Crane", "Matthew thirteen. I preached it the Sunday after the salesman came, and I didn't know why I'd chosen it. Somebody listened harder than I did."),
                ("Wendell", "Mine. The question's the whole thing, isn't it. Which is which."));

            Add("mirrorpatch", Seen("mirrorpatch"), "The mirror in Room 5 was taken down. The screws are lined up on the nightstand, perfectly spaced.",
                "Everybody's mirrors went. Why not the motel's.",
                null,
                ("Otis", "P-perfectly spaced. That's how it left the coins. In a row. Like it was showing me it could."));

            Add("mirrorscratch", Seen("mirrorscratch"), "There's a line scratched across the mirror in my room. Exactly at the height of my eyes.",
                "I'd sleep somewhere else, Detective. There isn't anywhere else.",
                null,
                ("Doc", "Five eleven, I'd say, looking at you. So the line was made for you. Before you got here. Think about what that means about when it knew you were coming."),
                ("Mae", "Measuring. That's what that is. It measured Earl's window the same way, with its hands."));

            Add("welcomesign", Seen("welcomesign"), "The population on the welcome sign has been painted over. Twice now.",
                "It says what it says. Wendell fights it with a brush. The paint wins.",
                null,
                ("Wendell", "Two hundred and twelve. I've painted it back nine times. It paints it over by morning, and it's never once been wrong by more than it is. It knows the count better than I do."),
                ("Father Crane", "Two hundred and twelve souls, when I count the pews. The sign has never once agreed with me since September."));

            Add("churchboard", Seen("churchboard"), "'Count your neighbours.' Who added that to the church board?",
                "Wendell. In different letters, in the night. Father Crane left it up.",
                null,
                ("Father Crane", "Wendell. I left it. It's the best advice anyone's given this town in a year, and it came from the man we all think is cracked."),
                ("Wendell", "Me. It's not advice. It's instructions."));

            Add("hymnboard", Seen("hymnboard"), "The hymn board says 213. The hymnals only go up to 180.",
                "Father Crane preaches 213 every Sunday now. He doesn't know what it is either.",
                null,
                ("Father Crane", "I don't put that card up. I take it down. It's back by the service, in the same red as the sign. I've stopped taking it down. I announce it. God forgive me, I announce it."),
                ("Nell", "I looked for it the first Sunday. Turned every page. Harlan told me to stop looking and sing."));

            Add("bellnote", Evidence("bell_note"), "A note on the back pew: 'I ring it at three and count who comes running.'",
                "Wendell Dobbs. He rings the bell at three in the morning and counts us in our nightclothes. We've stopped complaining. We just come.",
                null,
                ("Wendell", "Mine. Two hundred and twelve, every time. Then why does the sign say more? Tell me that, Detective, and I'll stop ringing it."),
                ("Father Crane", "He sits by the rope. I don't stop him anymore. The first time I did, he looked at me like I was the one who'd gone wrong."));
            Add("bellnote_wendell", Evidence("bell_note"), "They all come? Every one?", null, "bellnote",
                ("Wendell", "Every one. Children carried. Old Doc with his stick. I know them by their walk before I see their faces. All two hundred and twelve, and something standing among them with a face I counted. That's the part I can't get past."));

            Add("tally", Evidence("wendell_tally"), "Your tally book. 'IT WANTS TO BE COUNTED.'",
                "He writes everything down. Has for years. Being odd doesn't make a man wrong.",
                null,
                ("Wendell", "Then you know what I know. Something wants to be counted that isn't on my page. I don't know which line it's on. I've looked at every line."),
                ("Greta", "I read it once, on his porch. I didn't laugh. I'd lost nine cows by then."));

            Add("grave", Evidence("grave_headstone", "grave_soil"), "Somebody chiselled the name off a fresh grave in the churchyard.",
                "Ruth Calder's. Amos did it himself, the week after. He couldn't stand to read it.",
                null,
                ("Amos", "That's mine. Leave it."),
                ("Father Crane", "Amos. He couldn't bear to see it written, so he unwrote it. I let him. There are things a man is owed."),
                ("Doc", "Ruth's. August. I was there. Heart. There was nothing to be done and nothing wrong about it, whatever else is wrong here."));
            Add("grave_soil", Evidence("grave_soil"), "The earth on it's been dug at. Fingers.", null, "grave",
                ("Amos", "...I go out there at night. I sit. I don't dig. Who told you I dig?"),
                ("Father Crane", "Dogs, I tell people. I've never seen a dog in that yard."),
                ("Doc", "I've seen it. I rake it back over when Amos isn't looking. The marks are too long for a hand, Detective. I'm a doctor. I know how long a hand is."));

            Add("certificate", Evidence("calder_certificate"), "Ruth Calder's death certificate. Filled out and never filed.",
                "Ruth died in the summer. Amos buried her himself and Doc looked the other way. We all did. It's that kind of town.",
                null,
                ("Doc", "You've been in Amos's house. ...She died in August. Heart. Amos asked me to wait before I sent it, and I waited, and then it was September and the sign had changed and I didn't want the county here. I should have signed it."),
                ("Amos", "You've been in my house. She's in the ground where she belongs, beside where I'll be. The county doesn't need a paper to tell it that."));
            Add("certificate_doc", Evidence("calder_certificate"), "Why wait?", null, "certificate",
                ("Doc", "Because a man I've known fifty years asked me to. And because the county sends someone, and someone asks questions, and nobody here wanted anyone sent. We've managed our own dead a long time. We were wrong to, this once."));
            Add("certificate_amos", Evidence("calder_certificate"), "Who else knows?", null, "certificate",
                ("Amos", "Doc. The Father. Everyone, if they've got eyes. She hasn't been to church since August. Nobody asks after her. Nobody wants to be the one."));

            Add("shoes", Evidence("calder_shoes"), "Three pairs of new women's shoes beside Amos's bed. Tags still on.",
                "He buys her things. He stopped at flowers for a while. Then he started on shoes.",
                null,
                ("Amos", "Her size. Six. I drive to Barlow and I buy them and I don't know why. I think if I stop buying them she's gone."),
                ("Mae", "He came in here with a shoebox in September and sat with it on the stool next to him. Didn't say a word. I didn't either."));

            Add("knitting", Seen("knitting"), "Ruth's knitting has been kept up since August. The new rows are perfect, and backwards.",
                "Amos sits up with it at night. Ask him. Don't ask him.",
                null,
                ("Amos", "I don't touch it. I can't. I come down in the mornings and there's more of it, and I tell myself I did it in my sleep. I've never held a needle in my life."),
                ("Nell", "Backwards? Ruth couldn't purl backwards to save her life. I taught her."));

            Add("cattle", Evidence("kessler_carcass", "kessler_vet_report", "kessler_fence"), "Greta's cattle. Two holes behind the jaw, and no blood anywhere.",
                "Everyone knows about Greta's cows. Nobody goes out past the Kessler place after dark. Nobody says why.",
                null,
                ("Greta", "Fourteen. Never at the new moon. Always after the bell. The vet wrote 'undetermined' and drove away faster than he came."),
                ("Doc", "Not my patients, but I looked. Not a drop of blood in them, Detective. Not a drop. Where does a cow's blood go?"),
                ("Hank", "Something's eating from the inside. I've seen a heifer stand dead. I don't drive past there at night. I go round by the church."),
                ("Harlan", "She's lost half her herd and she won't sell the rest. Dutch. Stubborn. I'd have sold."));
            Add("cattle_greta", Evidence("kessler_carcass", "kessler_vet_report", "kessler_fence"), "'After the bell.' Every time?", null, "cattle",
                ("Greta", "Every time he rings it at three, I lose one by morning. I told Wendell. He said that proves it. Proves what, I said. He didn't know. He just wrote it down."));

            Add("rifle", Seen("rifle"), "Fourteen notches on your rifle stock.",
                null, null,
                ("Greta", "One for every cow. Not for anything I shot. I've shot at it, Detective. Twice, from the porch. It doesn't mind."));
            Add("bills", Seen("bills"), "'Never at the new moon. Always when the bell rings.' In the margin of your bills.",
                null, null,
                ("Greta", "Write it down, then. Nobody believes the Dutch widow. Maybe they'll believe the detective."));

            Add("gunrack", Seen("gunrack"), "Harlan's shotgun is leaned against the back door every night.",
                "Harlan sits up. Everybody with a field does, now.",
                null,
                ("Harlan", "Since September. Something walks the corn at night and it doesn't trip the wire. I sit with the gun. I haven't fired it. I'd like to know what I'm firing at first."),
                ("Nell", "He sits up. I pretend to sleep so he thinks one of us is. Neither of us is."));
            Add("hymnal", Seen("hymnal"), "Your hymnal. 'Harlan's hands were warm this morning. Thank you, Lord.' Every morning in September.",
                null, null,
                ("Nell", "I thank the Lord for small things. Some mornings he's cold as the pump handle, from sitting up. I started writing down the warm ones. ...I stopped in October. I don't want to talk about why I stopped."),
                ("Harlan", "She writes in church. I don't read it. Whatever she wrote, she meant kindly."));
            Add("hymnal_nell", Seen("hymnal"), "Why did you stop?", null, "hymnal",
                ("Nell", "Because there weren't any warm ones to write. Because I'd have been writing the cold ones. Because I'm his wife, Detective, and I know what I know, and I'm asking you not to make me say it."));

            Add("drawings", Seen("drawings"), "The children's drawings. Since September there's a fifth grown-up standing behind the family.",
                "Kids see things. Mine saw an old man in the barn for a year. There wasn't one. I checked.",
                null,
                ("June", "Children draw things. There are four of us in this house. Four. I've asked her who the tall one is and she says 'the other one'. I've stopped asking."),
                ("Hank", "Which one of us is she drawing it behind? ...Don't tell June I asked that."));
            Add("drawings_june", Seen("drawings"), "'The other one.'", null, "drawings",
                ("June", "That's what she says. 'The other one, Mama.' Like there's a one and an other one. She's five. She can't count past twelve and she's never once drawn it with a face."));

            Add("thornephoto", Seen("thornephoto"), "The family photograph from 1979. Silas is a smear from the shoulders up.",
                "Silas never did hold still. Ask anyone who's tried to take his picture.",
                null,
                ("Silas", "I don't hold still for cameras. Never have. You'll find every picture of me the same. Nineteen seventy-nine, Detective. Think about that date before you think about anything else."),
                ("Bev", "He moved. He always moves. I've got a box of pictures with no husband in them. It used to be funny."));

            Add("mothcase", Seen("mothcase"), "An empty display case. 'It would not hold still, and it would not die.'",
                "Irene and her moths. She's the only one in the county who'd notice one acting strange.",
                null,
                ("Irene", "September twenty-second. Grey, big as my hand, and it came off the pin. Moths don't come off the pin. I'd put it through the thorax. It walked to the edge of the case and it looked at me, and I'm aware of how that sounds."),
                ("Doc", "She brought it to me in a jar. I said it was dead. It was dead. The next morning the jar was empty and the lid was on."));

            Add("suitcase", Seen("suitcase"), "There's a packed suitcase in the Whitlock house. A ticket to Tulsa from 1980, never used.",
                "Ada was going to leave after her father died. She didn't. She keeps the case packed.",
                null,
                ("Ada", "I didn't go. Mama needed me after Daddy. I keep it packed so I remember I could. That was three years ago, Detective. I could still go. Any day."),
                ("Mae", "She stays for me. I never once asked her to. I never once told her not to, either."));
            Add("recipe", Seen("recipe"), "A recipe card in Mae's kitchen. 'Cherry. Earl's mother's.' And underneath, 'cherry cherry cherry cherry'.",
                null, null,
                ("Mae", "Earl's mother's. Somebody's been in my kitchen. ...Or I did it. I don't sleep. People do things when they don't sleep."),
                ("Ada", "Mama writes when she can't sleep. Lists, mostly. That one scared me. I don't know why that one."));

            // Mailboxes: the family's own business, mostly.
            Add("mailbox:Hollis", Seen("mailbox:Hollis"), "There's a baby tooth in your mailbox.", null, null,
                ("Harlan", "Grandbabies. They play post office. It's a game."),
                ("Nell", "The little one's. She puts them in for the mailman. He leaves her a penny. ...He hasn't left a penny since September. I keep putting pennies in myself."));
            Add("mailbox:Marsh", Seen("mailbox:Marsh"), "The drawing in your mailbox. A family of four, and a fifth one added in a different crayon.", null, null,
                ("June", "She mails us things. We're the only address she knows. The tall one wasn't there when she drew it. I'd swear to that. It was added."),
                ("Hank", "Different crayon. We don't own a grey crayon. I've looked."));
            Add("mailbox:Pruitt", Seen("mailbox:Pruitt"), "Everything in your mailbox is addressed to 'Occupant'.", null, null,
                ("Doc", "Forty years I've doctored this county and the seed company calls me Occupant. There's your mystery, Detective."),
                ("Lyle", "Nobody writes to us. Nobody writes to anybody. Wendell's the only man in town who's sent a letter since spring."),
                ("Otis", "Pa says it's the m-modern world. I think the modern world doesn't know we're here. I'm n-not sure I mind."));
            Add("mailbox:Calder", Seen("mailbox:Calder"), "Letters to Ruth Calder, all stamped return to sender.", null, null,
                ("Amos", "Her sister in Tulsa. I wrote and said Ruth was poorly. She wrote back. I didn't answer. The rest came back on their own. You can't write to a woman who isn't there to read it."));
            Add("mailbox:Whitlock", Seen("mailbox:Whitlock"), "A note in your mailbox: 'Gone to Mae's. Back by dark. Don't let it in.'", null, null,
                ("Ada", "I leave Mama notes. She doesn't read them. 'It' is the cat. We don't have a cat anymore. I keep writing it anyway."),
                ("Mae", "Ada leaves me notes like I'm a child. 'Don't let it in.' I've never let anything in. Things come in on their own."));
            Add("mailbox:Dobbs", Seen("mailbox:Dobbs"), "Your mailbox has been cleaned. Carefully.", null, null,
                ("Wendell", "I didn't clean it. Somebody wanted to know what I'd sent. I'd already sent it. That's the only reason you're standing here."));
            Add("mailbox:Kessler", Seen("mailbox:Kessler"), "The vet's invoice. Fourteen head, cause of death undetermined.", null, null,
                ("Greta", "He wrote undetermined because the other word would have got him laughed out of Barlow. He said it to me, though, in the yard. He said 'drained'."));
            Add("mailbox:Thorne", Seen("mailbox:Thorne"), "The photograph in your mailbox. 1979, and the father's face is blurred.", null, null,
                ("Bev", "Somebody put that in the box. We didn't. It's ours, it was in the album. Somebody took it out of my album and put it where you'd find it."),
                ("Silas", "Somebody wants you looking at me. Nineteen seventy-nine, Detective. Ask yourself who in this town wants you looking at the one man with an alibi four years long."));
            Add("mailbox:Vance", Seen("mailbox:Vance"), "Your mailbox is full of dead moths.", null, null,
                ("Irene", "They come to the box. Not the porch light, the box. I don't know why. I've kept every one. I've stopped pinning them."));

            // What the household's evidence says, to the household.
            Add("skin", Evidence("shed_skin"), "I found a curl of shed skin on a floor in this town. With a fingerprint still on it.",
                "Snake. They get in under the boards. ...A fingerprint. No. I don't want to hear it.",
                null,
                ("Doc", "Skin doesn't shed in a sheet with a print on it. Not human skin. Not any skin I've opened. Where did you find it? Don't tell me. I don't want to know whose floor."));
            Add("sketches", Evidence("practice_sketches"), "Pencil drawings of the same face, over and over, more accurate each time.",
                "Somebody practising a face. Whose? ...Whose, Detective?",
                null,
                ("Irene", "Practising. Like you'd practise a moth, from the pin, until you could draw it with your eyes shut. Until you could draw it moving."));
        }
    }
}
