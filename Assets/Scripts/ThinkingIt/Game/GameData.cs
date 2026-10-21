using System.Collections.Generic;

namespace ThinkingIt
{
    public class Doc { public string title, body; public bool forbidden, terminal; public Doc(string t, bool f, string b) { title = t; forbidden = f; body = b; } }
    public struct Line { public string who, text; public float dur; public Line(string w, string t, float d) { who = w; text = t; dur = d; } }
    public struct Card { public string text; public float dur; public Card(string t, float d) { text = t; dur = d; } }

    // All the writing in the game: documents, tapes, intro cards, intrusive thoughts.
    public static class GameData
    {
        // Anything that names or describes "It" is forbidden. Reading it makes it stronger.
        public static readonly Dictionary<string, Doc> Docs = new Dictionary<string, Doc>
        {
            // the orientation terminal in the lobby: pages are separated by \f and typed in one at a time
            ["tutorial"] = new Doc("HALVARD INSTITUTE // ORIENTATION TERMINAL", false,
                "PROJECT WHITE BEAR — PARTICIPANT ORIENTATION\n\nWelcome, Participant.\n\nThis terminal will explain what you need to know.\nRead carefully. It will not be repeated.\n\nThe Institute has been closed since 1998.\nThe power is off. Please remain calm."
                + "\f[ MOVEMENT ]\n\nW A S D ........ walk\nMOUSE .......... look around\nE .............. use, read, pick up\nP / ESC ........ pause\n\nThings you can use show a prompt\nwhen you look right at them."
                + "\f[ BLINKING ]\n\nHold SPACE (or Q) to close your eyes.\n\nWith your eyes closed you cannot see.\nYou move slowly, and you cannot run.\nBut it can't feed on you if you aren't looking.\n\nIf something is watching you,\nthe safest thing to do is stop watching it."
                + "\f[ LOOKING ]\n\nSome things here are unfinished.\nA pale outline where a door should be.\nA gap where the floor should be.\n\nStare at them. Hold your gaze.\nThe longer you look, the more real they become.\n\nThe building notices when you do this."
                + "\f[ SPRINTING ]\n\nHold SHIFT while walking to run.\n\nRunning is faster, but it is loud.\nThe bar at the bottom of your screen is your breath.\nWhen it runs out you must walk until it returns.\n\nWherever you run, it will come to look."
                + "\f[ IT ]\n\nSubject 14 described a tall shape\nstanding in the corner of the room.\n\nIt does not move while you look at it.\nIt is drawn to the places you keep looking.\n\nIt is made of attention.\nWe do not know what it wants.\nWe know it gets heavier when it is described."
                + "\f[ DOOR CODE ]\n\n3  _  _  _  _"
                + "\f[ END OF ORIENTATION ]\n\nFind the code. Find the way out.\nAnd whatever you do —\n\nDon't think about it.\n\n— Dr. H. Lund")
            { terminal = true },
            ["brochure"] = new Doc("Halvard Institute for Cognitive Research", false,
                "SUMMER VOLUNTEER PROGRAM — 1998\n\nPaid participation for young people aged 13–17.\nRoom and board provided. Six weeks.\n\n\"Learn to quiet your mind.\"\n\n<i>Project White Bear is a study of healthy forgetting.</i>"),
            ["teoNote"] = new Doc("A note, in Teo's handwriting", false,
                "Everything I found is in this building somewhere.\n\nThe doors are open. Something else is, too. It is fast and it cannot turn. Use that.\n\nI'm not crazy.\n\n— T"),
            ["session41"] = new Doc("Session Log 41", true,
                "Subject 14 (M.V., age 14) instructed to suppress target memory (the lake).\n\nSuppression successful.\n\nResidual effect: subject reports \"someone standing in the corner of the room\" whenever she stops trying. It is tall. It does not move while she looks at it.\n\nWe have instructed her not to look at it."),
            ["memo"] = new Doc("Internal Memo — RE: the shape", true,
                "Effective immediately, staff will no longer refer to it by name.\n\nStaff will not describe it in writing. Staff will not draw it.\n\nEvery description we write makes it heavier. It has started standing in the corridors when no subject is present.\n\n— Dr. H. Lund"),
            ["drawing"] = new Doc("A crayon drawing", true,
                "A white bear, standing over blue water.\nSomething small is under the water.\n\nWritten on the back, in pencil:\n<i>\"Found in Subject 14's room. She says she didn't draw it. She says she doesn't remember the lake.\"</i>"),

            ["w_chart"] = new Doc("Ward A — bed chart", false,
                "Bed 1: Participant 03. Discharged.\nBed 2: Participant 07. Moved to the apartment wing.\nBed 3: Subject 14. <i>Lights to remain ON at all times.</i>\nBed 4: Participant 11. Discharged.\nBed 5: —\nBed 6: —\n\nNight staff: do not wake Subject 14. Do not ask her what she is looking at."),
            ["w_nurse"] = new Doc("Night nurse's log", true,
                "02:10 — Bed 3 awake again. Says it is standing at the foot of the bed.\n02:40 — Checked. Nothing there. There was a dent in the mattress at the foot of the bed, as if someone heavy had been sitting.\n03:15 — Bed 3 says it has stopped standing. It sits now. It is learning.\n03:16 — I am requesting a transfer."),
            ["w_code"] = new Doc("Notebook", false, "_  _  _  _  7"),
            ["c1_d2"] = new Doc("Scrap of paper", false, "_  8  _  _  _"),
            ["c1_d3"] = new Doc("Scrap of paper", false, "_  _  4  _  _"),
            ["c1_d4"] = new Doc("Scrap of paper", false, "_  _  _  1  _"),
            ["w_iso"] = new Doc("Scratched into the isolation room door", true,
                "THEY PUT ME IN HERE SO IT WOULD HAVE NOWHERE TO STAND\n\nIT STANDS IN THE CORNER ANYWAY\n\nIF YOU CAN READ THIS IT CAN READ IT TOO\n\n— M"),
            ["w_fuse"] = new Doc("Maintenance note, taped to the breaker", false,
                "Breaker is back on. I don't care what the night staff say.\n\nSomething keeps running the corridors after midnight. Heavy. Fast. Never slows for a corner, just hits the wall and stands there.\n\nDo not run where it can hear you.\n\n— R. (maintenance)"),

            ["diary1"] = new Doc("Participant 07 — diary, day 3", false,
                "They gave me a room to practise in. The same room every day.\n\nDr. Lund says the trick is to notice nothing. Nothing changes here. Nothing is allowed to change.\n\nI counted the things in the room so I would know."),
            ["diary2"] = new Doc("Participant 07 — diary, day 9", false,
                "The clock was wrong today.\n\nI told Dr. Lund. He wrote something down and said: \"Then don't look at it.\"\n\nIt's easier said than done, not looking at a thing."),
            ["diary3"] = new Doc("Participant 07 — diary, day 14", true,
                "There is a tall man in the corner of the photo now. He isn't my father. I don't know whose family that is.\n\nWhen I look at him, he is a little closer to the glass."),
            ["diary4"] = new Doc("Participant 07 — diary, day 20", true,
                "Somebody is running a bath. I don't have a bath.\n\nThere is a girl crying in there. Older than me. She keeps saying she was supposed to be watching him. She keeps saying she only looked away for a second."),
            ["diary5"] = new Doc("Participant 07 — diary, day 31", true,
                "It isn't mine.\n\nIt was never mine. It belongs to the girl from the lake. It only stands in my room because hers is full.\n\nHer initials are on the bathroom door. M.V."),
            ["diary6"] = new Doc("Participant 07 — diary, day 40", true,
                "I stopped looking at it. It is the only thing that works.\n\nYou can't notice it if you don't notice it.\n\nClose your eyes. Count to four. Walk."),

            ["diary7"] = new Doc("Participant 07 — diary, day 47", true,
                "The armchair was facing the wall this morning. Like a child who's being punished.\n\nI turned it back around.\n\nTonight it's facing the wall again, and I don't think it's the chair that's being punished."),
            ["diary8"] = new Doc("Participant 07 — diary, day 52", false,
                "The television turns itself on at 3:33. There's nothing on it. Just snow.\n\nIf you watch the snow long enough there's a shape in it, standing very still.\n\nI've stopped watching. Mostly."),
            ["diary9"] = new Doc("Participant 07 — diary, day 58", true,
                "The radio talks about a lake I've never been to.\n\nIt says her name. Mara. It says she was fourteen. It says she looked away.\n\nI asked Dr. Lund who Mara is. He said I'm not to ask about the other participants."),
            ["diary10"] = new Doc("Participant 07 — diary, day 63", false,
                "Someone played one note on the piano. The same note, over and over, all night.\n\nI don't play the piano. Nobody here plays the piano.\n\nThe stool was in the middle of the room in the morning, like somebody small had dragged it there to sit and listen."),
            ["diary11"] = new Doc("Participant 07 — diary, day 70", true,
                "There is a little red sandal on the shoe rack. It's far too small for me. It's wet.\n\nDr. Lund took it away in a plastic bag.\n\nThis morning it was back. Still wet. It smells like a lake."),

            ["e_trials"] = new Doc("Trial procedure — Rooms A, B, C", false,
                "Each subject will walk from the door to the switch.\n\nA white bear has been placed in each room. The subject will not think of the bear.\n\nIf the subject looks at the bear, the bell will ring and the subject will begin again.\n\nThe chamber is not to be opened until all three switches are thrown.\n\n<i>Hint for staff: the subjects who pass are the ones who stop trying not to look. They just look somewhere else.</i>"),

            ["a_log"] = new Doc("Archive B — access log", false,
                "Most recent entries:\n\nT. Venn — 03:10 — Session tapes, Box 14\nT. Venn — 04:55 — Restricted vault <i>(no key. found another way)</i>\nT. Venn — 05:40 — Wing C\n\nNo entries since."),
            ["a_card"] = new Doc("Card catalogue", false,
                "WHITE BEAR, PROJECT — see: TAPES (restricted vault)\nSUBJECT 14 — see: RESTRICTED\nLAKE INCIDENT, 1998 — see: SUBJECT 14\n\n<i>The card for the next entry has been torn out. Only the first letter is left: \"I\".</i>"),
            ["a_intake"] = new Doc("Intake form — Subject 14", true,
                "Name: Mara Venn. Age: 14.\n\nReason for referral: persistent intrusive memory of the near-drowning of a sibling (T., age 6) at Halvard Lake, July 1998. Subject holds herself responsible: \"I looked away.\"\n\nRecommendation: full suppression protocol. The memory is to be moved, not treated."),
            ["a_p07"] = new Doc("Subject file — Participant 07", true,
                "Status: RELEASED.\n\nNote: her room was reassigned after release. The thing standing in it was not. It does not belong to her and never did.\n\nRecommend the room stays locked until Subject 14 is... resolved."),
            ["a_lund"] = new Doc("The Lund Papers, page 41", true,
                "We are not removing the memories. A memory cannot be destroyed, only moved.\n\nWe have been moving them all into the same place.\n\nIt has begun to have a shape.\n\nIt has begun to stand up."),
            ["a_listen"] = new Doc("Listening room — rules", false,
                "1. Headphones ON at all times.\n2. Do not play session tapes through the speaker.\n3. If a tape plays something that was not recorded, stop it and log the counter number.\n4. Do not log what you heard.\n\n<i>Underneath, in pencil: counter 0412 — breathing. counter 0413 — a bell. counter 0414 — someone counting to four.</i>"),
            ["a_deep"] = new Doc("Box 31 — inventory", true,
                "Contents: every drawing Subject 14 made between July and October 1998.\n\n212 drawings. All of the same figure. Tall. Pale. Standing in water up to the knees, looking at the viewer.\n\nIn the later drawings it is closer. In the last one there is only a face, filling the page.\n\nRecommend incineration. <i>(Denied — H.L.)</i>"),
            ["a_teo"] = new Doc("Teo's notes, pinned to the shelf", false,
                "Tapes 1-3 confirm it. It isn't a monster. It's a room full of what she couldn't hold.\n\nThe lake wing — Wing C — is where her memory is strongest. That's where it lives.\n\nGoing now. If it follows me there, good.\n\nMara, if you're reading this: the door is right behind you."),

            // ---- Chapter 4 ----
            ["c_nurse"] = new Doc("Wing C — night nurse's log", false,
                "02:10  Subject 14 awake again. Says the corridor is wet. It is not wet.\n\n03:30  Water on the floor of the corridor. Maintenance can't find a leak.\n\n04:15  Room 14 door found open. Subject asleep. Floor soaked through. Smells like the lake.\n\n<i>Wing C was closed one week later.</i>"),
            ["c_letter"] = new Doc("A letter, never sent", false,
                "Mara,\n\nThe doctors say it's best if we don't talk about the lake. So I won't.\n\nTeo is fine. He asks about you every day. He has started drawing bears. White ones. I don't know where he gets it from.\n\nCome home soon.\n\nLove, Mum"),
            ["c_schedule"] = new Doc("Halvard Lake — boathouse noticeboard", false,
                "SATURDAY 14 JULY 1998 — INSTITUTE OPEN DAY\n\nSwim period: 2:00 – 3:00 PM\nLifeguard on duty: <i>none (open day)</i>\n\nChildren of staff and families to be supervised by an older sibling.\n\n<i>Someone has written in pencil underneath: \"M. Venn — watching T.\"</i>"),
            ["c_lund"] = new Doc("Dr. Lund — final entry", true,
                "Subject 14 left the facility on her own. It went with her.\n\nI was wrong. You cannot move a memory out of a person. You can only teach them to stop looking at it.\n\nAnd whatever you stop looking at grows.\n\nThe only way to put it down is to look at it until the end."),


            // ---- Chapter 5: the stairwell ----
            ["s_stairs"] = new Doc("Scratched into the paint", false,
                "B1  B2  B3\nB3  B3\n\nSTOP COUNTING\n\nThe numbers on the landings stop making sense after the third floor. It doesn't matter. Keep going down. The door is on the fifth."),
            ["s_maint"] = new Doc("Maintenance log, Sublevel 1", false,
                "Sets are not to be plugged in. I have unplugged them all. They still show snow.\n\nIf you stare at one it starts to climb out. It is made of the picture. It only moves when you move, one step for every step you take, and it never stops following the way you walked.\n\nStand still and it stands still. Lead it under a light. The light takes it apart.\n\n— R. (maintenance)"),
            ["s_noise"] = new Doc("Sublevel 1 — incident report", true,
                "Staff member found standing in front of a television for six hours. Screen showed only snow. He said the snow was looking back.\n\nWe have named it NOISE. It is not the shape. It is what the shape leaves when nobody is watching it properly.\n\nDo not write down what it looks like. Every description makes it heavier."),

            // ---- Chapter 6 ----
            ["e_consent"] = new Doc("Consent form — participant, minor", false,
                "Participant: Mara Venn. Age: 14.\n\nReason given by parent:\n<i>\"She can't stop thinking about the lake. She doesn't sleep. She says it was her fault.\"</i>\n\nPayment: six weeks, room and board.\n\nSigned on behalf of the participant. The participant did not sign."),
            ["e_name"] = new Doc("A page torn from a notebook", true,
                "In a fourteen-year-old's handwriting:\n\nIf I give it a name it will be real.\nIf it's real it can stay.\nIf it can stay I don't have to keep pushing it away.\n\nIts name is <i>(the rest is scratched out so hard the pen went through the paper)</i>"),
            ["e_protocol"] = new Doc("Protocol 0 — the White Bear Room", true,
                "The subject is seated in the chair. The subject is told one thing: <i>do not think of a white bear.</i>\n\nEach time the subject fails, the bell rings.\n\nFor Subject 14 the bell rang 4,112 times in six weeks.\n\nOn day 31 the bell began ringing with nobody in the chair."),
            ["e_teo"] = new Doc("Teo's last note, on the console", false,
                "It let me in. I think it wanted somebody to talk to.\n\nIt's not angry, Mara. It's tired. It's been holding everything for you for twenty years.\n\nWhatever you do in there, don't do it because you're scared.\n\nDo it because you choose to.\n\n— T"),
        };

        public static readonly Dictionary<string, string[]> Thoughts = new Dictionary<string, string[]>
        {
            ["low"] = new[] { "don't think about it", "teo?", "you've been here before", "it's just a building" },
            ["mid"] = new[] { "look behind you", "it's in the corner", "you let him go under", "the water was so cold", "don't look for it" },
            ["high"] = new[] { "LOOK BEHIND YOU", "it knows your name", "you looked away", "he's still down there", "you made it", "stop thinking" },
        };

        public static readonly Dictionary<int, Card[]> Cards = new Dictionary<int, Card[]>
        {
            [1] = new[] {
                new Card("In 1987, a psychologist asked people not to think about a white bear.", 4.5f),
                new Card("They could think of nothing else.", 3.5f),
                new Card("1998.\nThe Halvard Institute.\nProject White Bear.", 4.5f),
                new Card("<size=24>[ voice message — Teo ]</size>\n\n<i>\"Mara. It wasn't a study about forgetting.\nThey were growing something.\n\nDon't come here.\n\nAnd Mara... don't think about it.\"</i>", 9f),
                new Card("Present day. 2:14 AM.", 3.5f),
            },
            [2] = new[] {
                new Card("Chapter Two\n<i>The Waiting Room</i>", 4f),
                new Card("Participant 07 was given a room to practise in.\nThe same room. Every day.", 5f),
                new Card("Nothing was allowed to change.", 4f),
            },
            [3] = new[] {
                new Card("Chapter Three\n<i>The Archive</i>", 4f),
                new Card("Every session was recorded.\nEvery word. Every child.", 5f),
                new Card("Somewhere down here is the reason they chose you.", 4.5f),
            },
            [5] = new[] {
                new Card("Chapter Five\n<i>The Stairwell</i>", 4f),
                new Card("The only way down to the White Bear Room.\nThere is no lift.", 5f),
                new Card("Don't count the floors.", 3.5f),
            },
            [6] = new[] {
                new Card("Chapter Six\n<i>The White Bear Room</i>", 4f),
                new Card("One chair. One bell. One instruction.", 4.5f),
                new Card("<i>Don't think of a white bear.</i>", 4f),
            },
            [4] = new[] {
                new Card("Chapter Four\n<i>The Lake House</i>", 4f),
                new Card("Wing C was built on the shore of Halvard Lake.", 4.5f),
                new Card("Every memory they moved went somewhere.\nHers went home.", 5f),
                new Card("This time, don't look away.", 4f),
            },
        };

        public static readonly string[] ChapterNames = { "", "Arrival", "The Waiting Room", "The Archive", "The Lake House", "The Stairwell", "The White Bear Room" };
        public static readonly string[] ChapterNumbers = { "", "One", "Two", "Three", "Four", "Five", "Six" };

        public static readonly Line[] TeoRecorder = {
            new Line("TEO", "Mara. If you found this, you didn't listen to me. Of course you didn't.", 5.5f),
            new Line("TEO", "I've been through the data. It wasn't a study about forgetting. They taught kids to push things down...", 6f),
            new Line("TEO", "...and everything they pushed down went somewhere. It all went to one place.", 5f),
            new Line("TEO", "It follows attention. Don't look for it. Don't read about it.", 5f),
            new Line("TEO", "If it gets bad, close your eyes. It can't feed on you if you aren't looking.", 6f),
            new Line("TEO", "I'm going further in. Participant housing. ...I think I know whose it is, Mara.", 6f),
        };

        public class Tape { public string title; public Line[] lines; }
        public static readonly Dictionary<string, Tape> Tapes = new Dictionary<string, Tape>
        {
            ["c1a"] = new Tape { title = "TAPE 12 — night nurse, Ward A", lines = new[] {
                new Line("NURSE ALLEN", "Night log, ward A. Bed three is awake again.", 4),
                new Line("NURSE ALLEN", "Something came past the door at two. Running. It did not slow down.", 5),
                new Line("NURSE ALLEN", "It hit the end wall so hard the lamp fell. Then nothing, for a long time.", 5),
                new Line("NURSE ALLEN", "Then it turned. I heard it turn. Slow. Like it had to think about how.", 5.5f) } },
            ["c1b"] = new Tape { title = "TAPE 13 — Dr. Lund, dictation", lines = new[] {
                new Line("DR. LUND", "Memo to myself. The suppression does not remove the memory. It moves it.", 5.5f),
                new Line("DR. LUND", "Eleven children. Eleven things that should have been grief. They have to go somewhere.", 6),
                new Line("DR. LUND", "The second one is made of flesh. It was the first to learn to hunt.", 5),
                new Line("DR. LUND", "I call it HIM, because I refuse to call it anything worse.", 4.5f) } },
            ["c1c"] = new Tape { title = "TAPE 14 — security desk, 02:14", lines = new[] {
                new Line("GUARD", "Camera four. Hall. Something is standing at the far end.", 4.5f),
                new Line("GUARD", "It's looking at the wall. It's not moving. It's... waiting?", 4.5f),
                new Line("GUARD", "Radio says don't run. Radio says it hears running.", 4),
                new Line("GUARD", "I'm going to walk. I'm going to walk very quietly to the stairs.", 5) } },
            ["c1d"] = new Tape { title = "TAPE 15 — Participant 03, exit interview", lines = new[] {
                new Line("PARTICIPANT 03", "They say I'm cured. I don't feel cured. I feel emptied.", 5),
                new Line("DR. LUND", "That is what cured feels like.", 3),
                new Line("PARTICIPANT 03", "Where did it go, then? What you took out?", 4),
                new Line("DR. LUND", "Somewhere it can't hurt you. Somewhere with a lot of corridors.", 5) } },
            ["c1e"] = new Tape { title = "TAPE 16 — maintenance, a warning", lines = new[] {
                new Line("R. (MAINTENANCE)", "If you're listening to this, the floor in the old corridor already went.", 5),
                new Line("R. (MAINTENANCE)", "Don't cross where it's weak. And if it's chasing you, put a wall between you.", 5.5f),
                new Line("R. (MAINTENANCE)", "It can't steer. It can only go. Let it go. Then walk away.", 5) } },
            ["tape1"] = new Tape { title = "TAPE 1 — orientation, 1998", lines = new[] {
                new Line("DR. LUND", "Project White Bear. Session one. We do not teach them to forget.", 5),
                new Line("DR. LUND", "We teach them to put it somewhere else.", 4.5f),
                new Line("DR. LUND", "The mind is a house. We are simply... closing doors.", 5),
                new Line("DR. LUND", "What happens to the rooms behind the doors is not our concern.", 5.5f) } },
            ["tape2"] = new Tape { title = "TAPE 2 — Session 41, Subject 14", lines = new[] {
                new Line("DR. LUND", "Mara. Tell me what you see.", 4),
                new Line("MARA, AGE 14", "There's someone in the corner. He's really tall.", 5),
                new Line("DR. LUND", "Don't look at him. Look at me. What happened at the lake?", 5),
                new Line("MARA, AGE 14", "...What lake?", 3.5f),
                new Line("DR. LUND", "Good. Very good.", 3),
                new Line("DR. LUND", "...He's closer now, isn't he.", 4.5f) } },
            ["tape3"] = new Tape { title = "TAPE 3 — Teo, last week", lines = new[] {
                new Line("TEO", "Mara. It's me. I found the tapes. I found you.", 5),
                new Line("TEO", "It's made of the lake. Of that afternoon. Everything you weren't allowed to feel, they put in one place... and it grew.", 7),
                new Line("TEO", "I'm going to the lake wing. Wing C. If it's yours, maybe it'll listen to me.", 6),
                new Line("TEO", "And Mara... it wasn't your fault. I was six. I ran off the end of the dock. Not you.", 7) } },
            ["tapeA"] = new Tape { title = "TAPE 7 — Participant 07, session 12", lines = new[] {
                new Line("DR. LUND", "Participant 07. What did you notice in your room today?", 4.5f),
                new Line("PARTICIPANT 07", "Nothing. I noticed nothing.", 3.5f),
                new Line("DR. LUND", "Good. And the corner?", 3),
                new Line("PARTICIPANT 07", "...There's nothing in the corner. It isn't mine.", 4.5f),
                new Line("DR. LUND", "Whose is it?", 2.5f),
                new Line("PARTICIPANT 07", "The girl who cries in the bath. She keeps saying she was only reading.", 5.5f),
                new Line("DR. LUND", "Stop the tape. Stop the—", 3) } },
            ["tapeB"] = new Tape { title = "TAPE 9 — intake interview, Mrs. Venn", lines = new[] {
                new Line("MRS. VENN", "She won't sleep. She won't swim. She won't look at her brother.", 5),
                new Line("DR. LUND", "And Teo? He recovered fully?", 3.5f),
                new Line("MRS. VENN", "He's fine. He's six, he's forgotten already. She's the one who can't.", 5.5f),
                new Line("MRS. VENN", "She keeps saying it's her fault. I gave her the whistle. I told her to watch him.", 6),
                new Line("DR. LUND", "We can make it so she doesn't have to carry it.", 4),
                new Line("MRS. VENN", "...Where does it go? If she doesn't carry it?", 4.5f),
                new Line("DR. LUND", "Somewhere safe.", 3) } },
            ["tapeC"] = new Tape { title = "TAPE 11 — staff meeting, August 1998", lines = new[] {
                new Line("NURSE ALLEN", "It sat on the end of bed three last night. The mattress went down.", 5),
                new Line("DR. LUND", "We agreed not to describe it.", 3),
                new Line("DR. OKAFOR", "Then what do we do? It's getting heavier every session.", 4.5f),
                new Line("DR. LUND", "The bell. We ring the bell whenever a child thinks of it. We train the thought out.", 6),
                new Line("DR. OKAFOR", "And when they can't stop thinking of it?", 3.5f),
                new Line("DR. LUND", "Then we ring it louder.", 3.5f) } },
            ["tape4"] = new Tape { title = "TAPE 4 — Teo, the lake house", lines = new[] {
                new Line("TEO", "Mara. If you made it this far, you already know where you are.", 5),
                new Line("TEO", "This isn't really the house. It's the afternoon. The way you kept it.", 5.5f),
                new Line("TEO", "For twenty years they told you not to look, so you never saw how it ended.", 6),
                new Line("TEO", "Look this time. All of it. My sandal. The boat you made me. Mum's whistle.", 6),
                new Line("TEO", "I'll be at the end of the dock. Where you lost me.", 5) } },
            ["tape5"] = new Tape { title = "TAPE 0 — Dr. Lund, the last recording", lines = new[] {
                new Line("DR. LUND", "This is the room where it started. One chair. One instruction.", 5),
                new Line("DR. LUND", "Do not think of a white bear. Every child failed. That was the point.", 5.5f),
                new Line("DR. LUND", "We never asked what the bear wanted. We only taught them to push.", 5),
                new Line("DR. LUND", "If anyone hears this... the thought was never the enemy. The pushing was.", 6),
                new Line("DR. LUND", "You can't kill it. You can only decide what to do with it.", 5) } },
        };

        // Chapter 4: the three things from that afternoon, and what she remembers when she finally looks at them
        public static readonly Dictionary<string, Line[]> Memories = new Dictionary<string, Line[]>
        {
            ["sandal"] = new[] { new Line("MARA", "His red sandal. He always kicked them off on the porch.", 5), new Line("MARA", "He was so proud of them. Red, like a fire engine.", 4.5f) },
            ["boat"] = new[] { new Line("MARA", "The paper boat. I folded it for him so he'd sit still.", 5), new Line("MARA", "\"Stay where I can see you, Teo.\"", 4) },
            ["magazine"] = new[] { new Line("MARA", "The magazine. A quiz on the back page. 'Which summer are you?'", 5), new Line("MARA", "I was on question four when it went quiet.", 4.5f) },
            ["ring"] = new[] { new Line("MARA", "His swim ring. Yellow, with ducks. He wouldn't wear it.", 5), new Line("MARA", "\"I'm a big boy, Mara.\" I let him leave it on the dock.", 5) },
            ["camera"] = new[] { new Line("MARA", "Dad's camera. The last picture on the roll is of the two of us.", 5), new Line("MARA", "We never got it developed. Nobody wanted to see that day.", 5) },
            ["whistle"] = new[] { new Line("MARA", "Mum's whistle. \"If anything happens, you blow it.\"", 5), new Line("MARA", "I put it in my pocket. Then I picked up a magazine.", 5) },
        };

        // Chapter 4 finale: what she sees while she finally looks at it
        public static readonly (float at, Line line)[] Finale = {
            (0.08f, new Line("MARA", "I looked away. Just for a second.", 4.5f)),
            (0.24f, new Line("MARA", "When I looked up, the end of the dock was empty.", 5)),
            (0.42f, new Line("MARA", "I didn't blow the whistle. I just jumped.", 4.5f)),
            (0.60f, new Line("MARA", "The water was so cold. I couldn't see anything.", 4.5f)),
            (0.78f, new Line("MARA", "Then I felt his hand. I pulled. I didn't let go.", 5)),
            (0.93f, new Line("MARA", "He was coughing. He was crying. He was alive.", 5)),
        };

        // Chapter 5: what Mara remembers while she finally looks at all of it
        public static readonly (float at, Line line)[] Remember = {
            (0.06f, new Line("MARA", "It's not a monster. It's everything I wouldn't let myself feel.", 5)),
            (0.22f, new Line("MARA", "The lake. The magazine. The second I looked away.", 4.5f)),
            (0.40f, new Line("MARA", "Twenty years of not thinking about it. And it was right here, holding it for me.", 6)),
            (0.58f, new Line("MARA", "I'm sorry. I'm taking it back. All of it.", 4.5f)),
            (0.76f, new Line("TEO", "Mara... I can see you.", 3.5f)),
            (0.92f, new Line("MARA", "I'm here. I'm looking. I'm not going to look away again.", 5)),
        };

        public static readonly Card[] EndForget = {
            new Card("Mara closes her eyes, and pushes, the way they taught her.", 5f),
            new Card("The bell stops ringing.\nThe institute goes quiet.", 4.5f),
            new Card("She drives until the sun comes up.\nShe doesn't remember why she came.", 5f),
            new Card("She doesn't remember Teo's message.", 4f),
            new Card("On the back seat, very still, something tall is sitting with its head against the roof.", 6f),
        };
        public static readonly Card[] EndRemember = {
            new Card("The room is quiet.", 3.5f),
            new Card("Teo is sitting on the floor next to the chair, shaking, alive.", 5f),
            new Card("They walk out together, into the morning.", 4.5f),
            new Card("Mara still sees it sometimes. At the edge of things.\nTall. Patient.", 5.5f),
            new Card("She doesn't look away anymore.", 4.5f),
        };
        public static readonly Card[] EndThinkIt = {
            new Card("Mara says its name.", 4f),
            new Card("It doesn't shrink. It doesn't leave.\nIt sits down on the floor beside her.", 5.5f),
            new Card("Teo walks out of the White Bear Room on his own.", 4.5f),
            new Card("Mara stays a while longer.", 4f),
            new Card("Some nights, now, she thinks about it on purpose.", 5f),
        };

        public static readonly Line[] Ending = {
            new Line("TEO, AGE 6", "You found me.", 3.5f),
            new Line("MARA", "Teo...?", 3f),
            new Line("TEO", "Mara. Not here. I'm not at the lake.", 4.5f),
            new Line("TEO", "It took me when I tried to talk to it. Back to where they started.", 5.5f),
            new Line("TEO", "The White Bear Room. Please. I can't hold on much longer.", 5),
        };
    }
}
