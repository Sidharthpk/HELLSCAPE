using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Canon: HellScape Revised Plot.docx (Sid, 3 October 2026).
// Old component/object names remain stable to preserve scene references and gameplay wiring.
// Apply before Start, including scene reloads and scenes produced by the existing editor builders.
public static class RevisedPlot
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
        Loaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Prologue" && scene.name != "MainGameScene") return;
        ReadableSetup.Apply();
        ApplyText(scene);
        if (All<RevisedPlotDirector>(scene).Length == 0)
        {
            var go = new GameObject("Revised Plot Director");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<RevisedPlotDirector>();
        }
    }

    public static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();

    // Used for old serialized dialogue and UnityEvent string arguments, not IDs, paths or audio keys.
    public static string Migrate(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Replace("Kessler & Vane", "{COMPANY}")
            .Replace("KESSLER & VANE", "{COMPANY_UPPER}")
            .Replace("Arthur Kessler", "Vane").Replace("Kessler", "Vane").Replace("KESSLER", "VANE")
            .Replace("{COMPANY}", "Kessler & Vane").Replace("{COMPANY_UPPER}", "KESSLER & VANE");
    }

    static bool Narrative(FieldInfo f) => f.FieldType == typeof(string) &&
        (f.Name.IndexOf("line", StringComparison.OrdinalIgnoreCase) >= 0 ||
         new[] { "text", "reply", "prompt", "objective", "depotObjective", "displayName", "ghostName", "title", "subtitle", "body", "footer", "scrawl", "dayNote" }.Contains(f.Name));

    static void MigrateFields(object obj, int depth = 0)
    {
        if (obj == null || depth > 3) return;
        foreach (var f in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (Narrative(f)) f.SetValue(obj, Migrate((string)f.GetValue(obj)));
            else if (f.FieldType == typeof(GhostNode[]) || f.FieldType == typeof(GhostChoice[]) || f.FieldType == typeof(DeliveryRun.Stop[]))
                foreach (var child in (IEnumerable)(f.GetValue(obj) ?? Array.CreateInstance(f.FieldType.GetElementType(), 0))) MigrateFields(child, depth + 1);
        }
    }

    public static void ApplyText(Scene scene)
    {
        foreach (var c in All<MonoBehaviour>(scene))
            if (c != null && c.GetType().Assembly == typeof(RevisedPlot).Assembly) MigrateFields(c);
        foreach (var t in All<TextMeshProUGUI>(scene)) t.text = Migrate(t.text);

        foreach (var d in All<DeliveryRun>(scene))
        {
            d.startLines = "*6:38 AM. Two drops left.*|Maya's gone quiet on the radio.|Tunnel, probably.";
            d.clockOutCard = "7:00 AM";
            d.depotLines = "*Vane's car is in the lot. Engine running. Nobody in it.*|A desk phone is ringing inside. Nobody answers.|The mop bucket by the door is pink.|Container in. Keys in the tray, like every Saturday.|Where is everyone?";
            if (d.stops.Length > 0)
                d.stops[0].handOverLines = "Mrs. Okafor: You're Derek's brother. He had this route before you.|Mrs. Okafor: He was here at two this morning. Knocking. Wouldn't sit down.|Mrs. Okafor: He asked if anyone had come looking for him. He looked frightened.|I'll ask him over breakfast.";
            if (d.stops.Length > 1)
            {
                d.stops[1].objective = "Collect the KEEP COLD container - Harlow Clinic";
                d.stops[1].arriveLine = "*6:50 AM. Harlow Clinic, rear entrance.*|A collection, not a drop. KEEP COLD on the lid.";
                d.stops[1].handOverLines = "Clinic employee: Mr. Vane rang at five. It has to be gone before seven.|Clinic employee: His code's on the slip. V-0500. Straight to the depot.|What's in it?|Clinic employee: I only sign for them. Please go.|*Cold through the lid. Heavier than it should be.*";
                if (d.stops[1].customer != null) d.stops[1].customer.prompt = "Collect the sealed container";
            }
        }
        foreach (var d in All<PrologueDirector>(scene))
        {
            d.timeCard = "SATURDAY - 6:38 AM";
            d.arriveLines = "*7:00 AM.*|Van's back. I'm off the clock.|Derek said breakfast. I'll walk.";
            d.homeLines = "Flat 2B. Second floor.";
            d.doorOpenLines = "The door's ajar.|Derek locks that twice. Every time.";
            d.callOut = "Derek? It's me.";
            d.noAnswer = "...Derek?";
            d.settleLines = "Nothing. Shower, probably.|I'll put the news on. Breakfast can wait.";
            d.newsLines = "News: ...a body was found at the Kessler & Vane depot overnight.|News: Named as Tomas Rourke, night cleaner.|News: Police see no suspicious circumstances. The company has no comment.";
            d.suspicionLines = "Tomas. He said goodnight to me on Friday.|...Wait.|Derek said he'd make breakfast. It's past eight.|No pan. No plates. Not even a mug.|Derek?";
            d.letterBefore = "Something in his hand. Paper, curled tight. He wouldn't let go of it.";
            d.letterAfter = "Soaked through. Most of it is gone.|Harlow. A ledger. Tomas saw something.|So did Derek.";
            d.bodyLines = "Derek.|*He's on the floor by the bed. The carpet is dark around him.*|Come on. Look at me.|...He's cold.";
            d.stabLine = "WHY?!";
            d.woundedLines = "Roof. Go.";
            d.upLines = "Up. Keep going.";
            d.topLines = "The roof door. Go.";
            d.confrontLines = "I can't feel my legs. He's in no hurry.|There's no way down past him.|...Come on, then. Closer.";
            d.grabLines = "Got you.|If I'm going, you're coming with me.";
        }
        foreach (var s in All<SleepSequence>(scene))
            s.fallWakeLine = "We fell. I should be dead.|...No pulse. Then what's beating in my hand?|The Demon King: The innocent make poor judges. They pardon too easily.|The Demon King: I wanted a hand that had signed.|The Demon King: Take my mark. Carry Vane's own words across the bridge.|Tomas: Where did you get that mark? ...Never mind. Move.";
        foreach (var car in All<CarInteract>(scene)) car.noKeysLine = "Locked. Keys are in the depot, in my tray.|If they're still there.";

        foreach (var g in All<GhostNPC>(scene)) ConfigureGhost(g);
        foreach (var n in All<ReadableNote>(scene)) ConfigureNote(n);
        foreach (var c in All<KesslerConfrontation>(scene))
        {
            c.confrontNodes = new[] {
                N("Who gave you that tape? ...What did you see?",
                    C("I saw Derek kill Tomas. You gave the order.", "Vane: The cleaner opened our bags. I told your brother it would take you both down.|Vane: Whose van do you think carried them?", 1),
                    C("Omar kept a copy.", "Vane: Omar. I should have put him in with the rest.|Vane: Everyone signed. So did you.", 1),
                    C("Back off, Vane.", "Vane: My office. My Echo. You're here because I allow it.", 1, -1),
                    C("The Harlow manifest has your code.", "Vane: A signature isn't a confession.", 1)),
                N("Derek did as he was told. You all took the bonus.",
                    C("You gave the order. Stop dressing it up.", "Vane: I protected sixty jobs. I'd do it again."),
                    C("You called Kessler at five. He didn't pick up.", "Vane: He'll see I was right. I don't need to confess to cross a bridge."),
                    C("Then tell me the truth.", "Vane: The truth is it worked. Every night. Every bonus.")) };
            c.turnLine = "Vane: I fed you. All of you. I was right.|Tomas: Every excuse makes him bigger. Don't let him go.";
            c.keyLine = "That isn't a confession. It's a wall.|The silo key fell out of his coat. Take it.";
            c.afterTurnLine = "He's gone from the Echo. The service tunnel.";
            c.scareLine = "Vane: That tape was wiped. Nobody was meant to see it.";
        }
        foreach (var c in All<KesslerChase>(scene))
        {
            c.tomasHoldLine = "Tomas: You don't get to hide again, Vane.|*His body flashes red. A Lost soul shouldn't do that.*";
            c.tomasLine = "Tomas: Go. Over the bridge. I'll hold him.";
            c.tomasLostLine = "Vane: Out of my way, cleaner.";
            c.knockLine = "He's at the door.|It won't hold.";
            c.screamLine = "Vane: You're not walking away from me.";
            c.runLine = "He's through. Run.";
            c.hitLines = "Agh--|Keep moving.|Don't stop.";
            c.insideLine = "Shut.|That door won't hold.|There has to be a way through.";
            c.quietLine = "...It's stopped.";
        }
        foreach (var s in All<SiloEncounter>(scene))
        {
            s.pushLine = "Footsteps. Close.";
            s.leapLine = "The stairs stop. Nothing below but dark.|He's behind me. ...Jump.";
            s.landLine = "My leg.";
            s.derekDeathLine = "Elias: Tomas keeps bringing me back here. Where I killed them.|Elias: He had me take your keys, after you fell. So you couldn't leave.|Elias: Your name was the last one. I couldn't, while you held Derek. I can now.";
            s.throwLine = "Maya: That voice. My driver.|Maya: Elias left this rifle. Catch.";
            s.gunLine = "Elias's rifle. Maya took it from the landing.";
            s.defeatedLine = "*He drops to his knees. Blood down his face.*|Elias: Finish it. People like you are good at that.";
            s.killLine = "*The shape tears apart. Elias is gone. No hearing. No way back.*|*His coat falls open.*|My van keys.|*A photograph: TOMAS ROURKE. On the back, in pencil: 'The last name. Stop him leaving.'*";
            s.spareLine = "Elias: I got my brother the cleaner's job. When he died, I could see him.|Elias: He gave me names. I called it justice. It was murder.|Elias: Maya. Sam. Vane. Derek. They were people. I did it. Tomas never held the knife.|*The red is leaving his skin. He's coming back.*";
            s.mayaSnapLine = "Maya: Don't listen to him.|Maya: Tomas told me about the bodies and I phoned Vane. I knew what he'd do.|Maya: I had a job. We all did.|Maya: I won't apologise.";
            s.mayaRealiseLine = "Maya: I was right. Do you hear me? I was right.";
            s.mayaTurnedLine = "*She's holding on to the excuse. It's tearing her apart.*|Elias confessed. Maya won't. That's the difference.";
            s.eliasSaveLine = "Elias: No one else dies for my brother. Go.";
            s.eliasKeysLine = "Elias: The keys. Take them. Get Sam out.|He took them from my desk after the fall. On Tomas's orders.";
            s.keysLine = "My van keys. He had them all along.";
            s.twistFloodLine = "Blood, through the grates.|Rising fast.";
            s.floodLine = "It's filling up.|Swim.";
            s.confessionLine = "Maya: I know your voice. I put your route out on the radio before Sam loaded you.|Sam: Elias came after us with the rifle. Kept saying 'Tomas' to nobody.|Maya: We died on those stairs a little after six. Fear kept us solid.|Sam: That report was a shift statement. I didn't see an accident. I signed.";
        }
        foreach (var f in All<BloodFlood>(scene))
        {
            f.firstLine = "Chest-deep. Kick.";
            f.lowLine = "Maya: Don't stop. Kick.";
            f.lowLineSam = "Sam: Don't stop. Kick.";
            f.clingLine = "Maya: We can't swim in this. Don't let go of us.";
            f.clingLineSam = "Sam: I can't swim. Don't let go.";
            f.mayaDrownLine = "Maya: I can't--|Maya.";
            f.samDrownLine = "Sam: Help me. Please.|Sam.";
            f.lostLine = "He's up there, over the blood. Blue, like the others.|Sam's one of the Lost now. I let go.";
            f.damnedLine = "Maya: I was right. I was right.|*The same excuse. The body growing around it.*|The blood didn't make her confess.";
        }
        foreach (var t in All<TurningCutscene>(scene))
        {
            t.damnedSpeech = "Maya: I phoned Vane. I protected our jobs. I'd do it again.";
            t.damnedAfter = "*Her excuse holds the monstrous shape. A confession could still bring her back.*";
            t.lostSpeech = "Sam: I can't remember my face. I keep trying not to remember what I signed.|Sam: Don't let me forget my name.|*His shape is fading. Denial is taking his memory.*";
        }
        foreach (var h in All<HordeChase>(scene))
        {
            h.introLine = "The engine. They've all heard it.";
            h.stopLine = "They've stopped at the bridge. All but Tomas.|He's dragging the other two.";
            h.clingLine = "They're on the van.|Floor it. Shake them off.";
            h.driveLine = "Go.";
        }
        foreach (var b in All<DemonBoss>(scene))
        {
            b.displayName = "TOMAS - THE AMALGAMATION";
            b.burstLines = "*Tomas steps out of the office. Vane and Maya's ruined bodies lie behind him.*|Tomas: She made the call. He gave the order. I brought them back to where it started.|Tomas: You broke their shapes. You didn't end them.";
            b.driveLine = "Go.";
            b.transformLine = "Tomas: No hearing. No way out. You stay with me.|*The bodies drag across the concrete. Flesh draws tight between all three.*";
            b.riseLines = "Tomas: Your mark opens the way to Kessler. Give it to me.|You sent Elias after us.|Tomas: And now you can finish helping.";
            b.phase2Lines = "Vane: Get him out of me!|Maya: Tomas, let go!|Tomas: You didn't let me go.";
            b.phase3Lines = "Tomas: They made this. Every piece.|You chose what to do to Elias. Look what you're doing now.|Tomas: I am owed this!";
            b.sealedName = "TOMAS - BOUND BY THREE HEARTS";
            b.heartsLines = "Tomas: Three hearts. Three of us. Nothing touches me while they beat.|Vane: Please--|Maya: Tomas, stop--";
            b.heartsHint = "Break the three hearts.";
            b.crashLines = "Tomas: NO--! They're mine!";
            b.deathLines = "*The three necks pull apart. Tomas can't hold them.*|Tomas: No. They're mine.";
            b.afterLines = "*The joined body sinks. Three voices go quiet.*|The bridge is waiting for my account.";
            b.arriveLines = "The bridge just ends.|Tomas: The mark. Then you can go.";
            b.nowhereLine = "Tomas: You don't get to leave your part behind.";
            b.tunnelInLine = "It stopped at the tunnel. Too big to follow.|...Something's on the roof.";
            b.tunnelOutLine = "It's off the roof. Right behind me.";
        }
        foreach (var e in All<EndingSequence>(scene))
        {
            e.arriveLine = "The bridge. Not an escape. A hearing.|The mark's beating harder.";
            e.brandLine = "The mark's burning again. It was his heartbeat all along.|A door to the living world is opening.";
            e.boundLine = "Kessler's alive. The King can't see him.|I can reach the living through the Echo.|*Tomas turned an account into a death list. I know where that ends.*";
            e.deathLines = "The Demon King: You struck that street, driver, and your name was struck from the living.|The Demon King: Not every soul is claimed. Some are marked, and sent back to labour.|The Demon King: Wear my mark. It will walk you among the living.";
            e.deathBrandLine = "There's a mark burned into both my hands.";
        }
        foreach (var c in All<StabCutscene>(scene))
            if (c.gameObject.name.Contains("Derek kills Tomas"))
                c.afterLine = "*Derek catches Tomas with the body bags. Vane steps into frame.*|*Vane points at our van. 'Both brothers go down.'*|Derek held the knife. Vane gave the order.|The statement called it a fall. I signed it.";
        DialogueRewrite.Apply(scene);
    }

    static GhostChoice C(string text, string reply, int next = -1, int karma = 0, int fate = 0) => new GhostChoice { text = text, reply = reply, next = next, karma = karma, fate = fate };
    static GhostNode N(string line, params GhostChoice[] choices) => new GhostNode { line = line, choices = choices };

    static void ConfigureGhost(GhostNPC g)
    {
        switch (g.ghostName)
        {
            case "Priya":
                g.nodes = new[] {
                    N("He came at half past five. Tomas's brother. We were dead by a quarter past six.",
                        C("Why are you blue?", "Priya: I'm losing pieces of myself. The more you hide, the less there is.|Priya: Some of us are going quicker than others.", 1, 0, 1),
                        C("Where's Vane?", "Priya: Hiding in the Echo. We can't follow. You can, with that mark.|Priya: Start at the threshold in reception.", 1),
                        C("I'll help if I can.", "Priya: Thank you.|Priya: Find out what happened to Tomas. Don't just sign another story.", 1, 1, 3),
                        C("Why am I still solid?", "Priya: The mark holds you together. Elias has his one purpose.|Priya: The Damned have their excuses. Hold one tight enough and it grows a body.|Priya: Say what you did, honestly, and the shape can go back.", 1)),
                    N("He pulled the fuses on his way in. Wanted us to die in the dark.",
                        C("Where did he put them?", "Priya: Where we used to be. Tomas's cake. Derek's desk. Back by the bodies."),
                        C("I'm sorry.", "Priya: ...Nobody's said that.|Priya: The cake. Derek's desk. The bodies at the back.", -1, 1),
                        C("I just want my keys.", "Priya: That's what you said about Tomas.|Priya: Find them yourself.", -1, -1)) };
                g.afterLine = "Priya: The cake. Derek's desk. The back, by the bodies.";
                break;
            case "Nadia":
                g.nodes = new[] {
                    N("He knew every name. I heard him ask Tomas which room.",
                        C("Where are my keys?", "Nadia: You left them in the tray at seven. Elias took them from your desk after you fell.|Nadia: Don't ask me why.", 1),
                        C("When did he come?", "Nadia: Half past five. He had a rifle. Maya and Sam ran for Silo 2. Vane went out through the service tunnel.|Nadia: I stopped counting after that.", 1),
                        C("I'm sorry.", "Nadia: I signed it too. I'd like to remember who I was before I did.", 1, 1, 3),
                        C("You signed it too.", "Nadia: It was procedure. Everybody signed. I'm not taking the blame for procedure.", 1, -1, 2)),
                    N("Vane locked himself in his office after the call. Didn't help him.",
                        C("How do I get in?", "Nadia: Keypad by his door. Ask Ben about the date."),
                        C("Did anyone make it out?", "Nadia: Maya and Sam ran for the silo. They still had their bodies then.|Nadia: Get them out.", -1, 1),
                        C("Serves Vane right.", "Nadia: ...He was frightened. We all were.", -1, -1)) };
                g.afterLine = "Nadia: Maya and Sam. Silo 2.";
                break;
            case "Ben":
                g.nodes = new[] {
                    N("We got Tomas a cake. For his last shift. Nobody came.",
                        C("His last shift?", "Ben: He'd saved enough to go home to his brother.|Ben: He never got to finish it.", 1),
                        C("I didn't know there was a cake.", "Ben: No. Nobody looked up.", 1, 0, 3),
                        C("I don't have time.", "Ben: Nobody had time for him either.", -1, -1, 1)),
                    N("Vane used the date for everything after. Codes. Passwords. Like he was proud of it.",
                        C("What date?", "Ben: The day Tomas died. Day first, then month. It's on the calendar."),
                        C("We should have gone to the party.", "Ben: ...Yeah.|Ben: Day first, then month.", -1, 1)) };
                g.afterLine = "Ben: The calendar. Day, then month.";
                break;
            case "Omar":
                g.nodes = new[] {
                    N("I did security. I watched the tape that night. Vane wiped it. Mostly.",
                        C("What did you see?", "Omar: Morgue trucks at Silo 2, three in the morning. No paperwork, no lights. Cool boxes going out the other way.|Omar: And Tomas on the walkway with his mop. Watching.", 1),
                        C("You let him cover it up?", "Omar: I needed the job. So did you.", 1, 0, 2),
                        C("Is there a copy?", "Omar: The terminal in Vane's wing still has a replay. Four cameras.", 1)),
                    N("Tomas walked the same route every night. The tape only plays if you follow him round it.",
                        C("What was his route?", "Omar: The rota's in reception. Put his times in order."),
                        C("Rest now, Omar.", "Omar: ...The rota. Earliest first. Silo last. It was always the silo last.", -1, 1, 3)) };
                g.afterLine = "Omar: Reception, meeting room, closet. Silo last.";
                break;
            case "Vane":
                g.nodes = new[] { N("This is the office as it was. The dead can't get in here. Neither should you.",
                    C("Elias said he killed you.", "Vane: Five past six. The service tunnel.|Vane: Being dead doesn't oblige me to answer to anyone."),
                    C("Why wasn't Kessler at the depot?", "Vane: Listen to the phone. Then leave me be."),
                    C("What happened to Tomas?", "Vane: There's a terminal. Four cameras. The order's on the cleaning rota, if you can read it.")) };
                g.afterLine = "Vane: Watch the tape. I won't say it for you.";
                break;
            case "Tomas":
                g.nodes = new[] {
                    N("That mark goes where I can't. Take me to Vane. I only want to hear him say it.",
                        C("Do you remember your family?", "Tomas: My mother's name. I used to know it.|Tomas: There was a song. ...Gone.|Tomas: I remember the knife. I remember the names.", 1, 1),
                        C("Why can Elias see you?", "Tomas: I'm why he took the job.|Tomas: The living only see the dead they share blame with.", 1),
                        C("Did you tell him to kill?", "Tomas: I told him who signed. What did you think he'd do?", 1)),
                    N("Vane paid the clinic to make me disappear. Filed it as a fall. Collected your signatures.",
                        C("I need proof.", "Tomas: The container. KEEP COLD. His code's on the manifest. Then the ledger. Then the tape."),
                        C("What happens at the bridge?", "Tomas: Only the ones who've confessed get across. Vane thinks he can walk over without.|Tomas: Get me close. I'll make him talk.")) };
                g.afterLine = "Tomas: Keep that mark from the Damned. Find Vane.";
                break;
        }
    }

    static void ConfigureNote(ReadableNote n)
    {
        switch (n.gameObject.name)
        {
            case "Derek's phone": n.title = "3 missed calls from Vane"; n.subtitle = "Vane"; n.afterLine = "Three missed calls and a threat. All before three in the morning.|What did you get into, Derek?"; break;
            case "IncidentReport":
                n.title = "SHIFT WITNESS STATEMENT";
                n.body = "13 APRIL - SILO 2\n\nT. Rourke fell while working unsupervised. Cause recorded by Vane: ACCIDENTAL.\n\nAll night-shift staff, including drivers, must sign. This confirms the account above, not merely attendance.";
                n.footer = "Signed: Maya / Sam / Derek / [your signature]\nFiled by Vane. No further action.";
                n.afterLine = "My name. I never saw a fall. I signed because asking would have cost me.";
                break;
            case "KesslerLedger":
                n.title = "VANE - NIGHT ACCOUNTS";
                n.body = "Unclaimed bodies: city morgue -> Silo 2.\nHarvested material -> Harlow Clinic.\nRemainder: silo disposal.\n\n13 APRIL: T. Rourke. Harlow disposal approved V-0500.\nDriver bonuses included. No questions.";
                n.footer = "The cleaner opened the bags. Derek caught him.\nBoth brothers' van is implicated. Deal with it. - Vane";
                break;
            case "StickyNote": n.afterLine = "Day first, then month. On a sticky note."; break;
            case "YourDesk_Note": n.body = "AFTER THE FALL\n\nTomas says you don't leave.\nI have the keys. Come and get them.\nSILO 2.\n- Elias"; break;
        }
    }
}
