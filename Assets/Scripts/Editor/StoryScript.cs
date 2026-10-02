using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape's whole script in one place. Every spoken line and piece of on-screen story text that lives in the
// scenes is listed here; HellScape > Apply Story Script writes them into MainGameScene and Prologue.
// Edit a line here, run the menu again. ('|' starts a new line in the dialogue box. "Name: " makes it a speaker.)
//
// THE STORY, for reference while writing:
//   You drive nights for Kessler & Vane Logistics. Your little brother Derek works there too.
//   Arthur Kessler, the boss, ran unclaimed bodies from the city morgue through Silo 2 at night, cut out the
//   organs to sell, and let the silo crush the rest. On 13 April the night cleaner, Tomas Rourke, opened one of
//   the trucks. Kessler made Derek kill him ("do it or go down with me"), threw him in the silo and had everyone
//   sign an incident report calling it an accident. You signed it without reading it.
//   Tomas's brother Elias worked it out. At 06:00 this morning, with his own face bare, he killed everyone at the
//   office (Kessler, Priya, Ben, Nadia, Omar); Maya and Sam ran for Silo 2. At 06:52 he killed Derek in your flat.
//   At 07:12 you walked in. You chased him to the roof, pushed him off, and he dragged you down with him.
//   You wake in the city as it really is: where the dead wait to be judged.
//     The Lost  - blue ghosts: the dead whose sins are still hidden. They drift and hide.
//     The Damned - the monsters: when a sin comes out, when you SAY it, the city takes you.
//     You, Maya, Sam, Elias still have your bodies: you haven't been judged yet.
//   Maya's secret: Tomas, terrified, told HER what he'd seen in the truck, and she phoned Kessler ("deal with
//   it"). That's why she throws you the rifle: she needs Elias dead before he says who Tomas trusted. Kill him and
//   she keeps her secret (and can leave with you). Spare him and she loses it, says it out loud - and turns.
//   Elias gets up and holds her off ("JUST GO! LEAVE!") while the silo floods over them both.
//   The only way out is the bridge. What waits at the end of it is the DEMON KING, who owns the city. No angel.
//     Damned (more sin than virtue): "you're one of mine" - you walk back into the city as one of them.
//     Otherwise: he can't take the Lost until their sins are spoken, and you made Kessler say his. He marks you
//     as his confessor: make every one of them confess, put down what they turn into, and he'll let you go.
//     TO BE CONTINUED.
public static class StoryScript
{
    // (scene, object path, component, property path, text)
    static readonly (string scene, string path, string comp, string prop, string text)[] Lines =
    {
        // ================================================================ PROLOGUE: the morning
        ("Prologue", "Prologue/Deliveries", "DeliveryRun", "depotObjective",
            "Park the van in the Kessler & Vane garage"),
        ("Prologue", "Prologue/Deliveries", "DeliveryRun", "depotLines",
            "Van's back in the garage.|Office lights are off. Door's locked. At this hour? Kessler's never late.|Keys through the mail slot. They land in the tray on my desk, like every Saturday."),
        ("Prologue", "Prologue/PrologueDirector", "PrologueDirector", "arriveLines",
            "Van's in. Keys are in. Clocked out.|Twelve hours on the road... I'll walk home.|Derek promised breakfast. First time in weeks he's sounded like himself."),
        ("Prologue", "Prologue/PrologueDirector", "PrologueDirector", "doorOpenLines",
            "...our door's open.|Derek never leaves it open. Not since he started locking everything."),
        ("Prologue", "Prologue/PrologueDirector", "PrologueDirector", "bodyLines",
            "Derek...?|No. No, no, no--|DEREK! Come on, wake up! WAKE UP!|...he's cold.|Who did this to you...?"),
        ("Prologue", "Prologue/PrologueDirector", "PrologueDirector", "cameraIdea",
            "The camera. He put one up in the bedroom last month. Said he felt like he was being watched.|If someone did this... it saw them."),
        ("Prologue", "Prologue/PrologueDirector", "PrologueDirector", "afterTape",
            "That face... I've never seen him before in my life.|He was HERE. In our home.|...06:52. That was twenty minutes ago."),
        ("Prologue", "Prologue/PrologueDirector", "PrologueDirector", "footstepLines",
            "...footsteps.|Someone's still in the flat."),
        ("Prologue", "Prologue/PrologueDirector", "PrologueDirector", "escapedLines",
            "The front door--!|He was hiding in here the whole time!"),
        ("Prologue", "Prologue/PrologueDirector", "PrologueDirector", "upLines",
            "He's going UP, not down!|You're not getting away!"),
        ("Prologue", "Prologue/PrologueDirector", "PrologueDirector", "topLines",
            "The roof. There's nowhere left to go.|He's trapped."),
        ("Prologue", "Prologue/PrologueDirector", "PrologueDirector", "confrontLines",
            "Nowhere left to run.|Why him? WHY DEREK?!|Elias: My name is Elias Rourke. Your brother knew why.|Elias: Ask your boss about Tomas.|Rourke...? Tomas Rourke... the cleaner who went missing?|...I don't care. I DON'T CARE.|You're going to pay for this."),
        ("Prologue", "Prologue/Clues/TV", "Interactable", "line",
            "\"...police have called off the search for Tomas Rourke, a night cleaner at the Kessler & Vane depot, missing three weeks. His brother Elias still believes--\"|Kessler & Vane. Derek and I both work there.|Derek hasn't slept properly since that cleaner went missing."),
        ("Prologue", "Prologue/Clues/Photo", "Interactable", "line",
            "Me and Derek. His birthday. He burned the cake and blamed me for it.|My little brother. I was supposed to look out for him."),

        // ================================================================ THE CITY: waking up dead
        ("MainGameScene", "Hell Wake (after the fall)", "SleepSequence", "fallWakeLine",
            "...I fell four floors. I should be dead.|The sky's red. The street's empty. Everything smells like rust.|He fell with me. Where did he go?|'Ask your boss about Tomas.'|Kessler. The office is just down the street. He's going to give me answers."),
        ("MainGameScene", "Prometheus", "CarInteract", "noKeysLine",
            "Locked. My keys are still in my desk.|The office. Kessler & Vane, right next door."),

        // ================================================================ THE OFFICE
        ("MainGameScene", "OfficeAct/OfficeEntrance (City)/OfficeDoor", "Teleporter", "arriveLine",
            "The office. Pitch black.|...hello?|Voices, in the dark. Whispering my name.|These are the people I work with. Worked with."),
        ("MainGameScene", "OfficeAct/Office/SiloDoor", "Interactable", "lockedLine",
            "SILO 2 - AUTHORISED STAFF ONLY.|Locked. Kessler kept the only key in his safe."),
        ("MainGameScene", "OfficeAct/Office/FuseBox (breaker)/BreakerUse", "Interactable", "lockedLine",
            "The fuse box. Three slots, all empty.|Someone pulled them out on purpose."),

        // ---- Priya: the rules of this place
        ("MainGameScene", "OfficeAct/Office/Ghost - Priya", "GhostNPC", "nodes.Array.data[0].line",
            "You came back. Nobody comes back here on purpose."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Priya", "GhostNPC", "nodes.Array.data[0].choices.Array.data[0].reply",
            "Priya: Dead? Since six this morning. It doesn't hurt anymore. The dark does.|Priya: We're the Lost now. The dead who still have secrets. We hide, and we wait."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Priya", "GhostNPC", "nodes.Array.data[0].choices.Array.data[1].reply",
            "Priya: Some of us never left the building. Some are hiding. Listen for them.|Priya: And whatever you do... don't make any of us say our secrets out loud."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Priya", "GhostNPC", "nodes.Array.data[0].choices.Array.data[3].reply",
            "Priya: You are. We all are. This is where the dead wait to be judged.|Priya: But you came down in your own body, like Maya and Sam. That means you haven't been judged yet.|Priya: Be careful. When a sin comes out here - when you SAY it - the city takes you. You become one of the Damned."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Priya", "GhostNPC", "nodes.Array.data[1].line",
            "Elias pulled the fuses when he came for us. He wanted us to die in the dark."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Priya", "GhostNPC", "nodes.Array.data[1].choices.Array.data[0].reply",
            "Priya: He hid them where we used to be. The cake we bought Tomas. Derek's desk. By the bodies at the back."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Priya", "GhostNPC", "nodes.Array.data[1].choices.Array.data[1].reply",
            "Priya: ...thank you. Nobody ever said that.|Priya: Tomas's cake. Derek's desk. The bodies at the back. Go."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Priya", "GhostNPC", "afterLine",
            "Priya: The fuses. Tomas's cake, Derek's desk, the bodies at the back. Hurry."),

        // ---- Ben: Tomas, and the door code
        ("MainGameScene", "OfficeAct/Office/Ghost - Ben", "GhostNPC", "nodes.Array.data[0].line",
            "We bought Tomas a cake, you know. For his last shift. Nobody came."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Ben", "GhostNPC", "nodes.Array.data[0].choices.Array.data[0].reply",
            "Ben: Tomas. The night cleaner. He'd saved enough to go home to his brother, Elias.|Ben: He never made it to the end of that shift."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Ben", "GhostNPC", "nodes.Array.data[0].choices.Array.data[1].reply",
            "Ben: No. You never looked up from your screen. None of us did."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Ben", "GhostNPC", "nodes.Array.data[1].line",
            "Tomas's last night is still circled on the calendar. Kessler used that date for everything after. Doors. Passwords. Like he was proud of it."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Ben", "GhostNPC", "nodes.Array.data[1].choices.Array.data[0].reply",
            "Ben: The day Tomas died. Day first, then the month. Look at the calendar."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Ben", "GhostNPC", "nodes.Array.data[1].choices.Array.data[1].reply",
            "Ben: ...yeah. We should have.|Ben: Kessler's door code is that day. Day first, then the month."),

        // ---- Nadia: what happened this morning
        ("MainGameScene", "OfficeAct/Office/Ghost - Nadia", "GhostNPC", "nodes.Array.data[0].choices.Array.data[0].reply",
            "Nadia: A note. On your desk. It's written in something red."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Nadia", "GhostNPC", "nodes.Array.data[0].choices.Array.data[1].reply",
            "Nadia: At six this morning a man walked in. Grey coat, blood to the elbows. He turned the lights off, one by one.|Nadia: Then he came for us. He knew every one of our names. He had a list."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Nadia", "GhostNPC", "nodes.Array.data[1].choices.Array.data[0].reply",
            "Nadia: The keypad by his door. Ask Ben about the date."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Nadia", "GhostNPC", "nodes.Array.data[1].choices.Array.data[1].reply",
            "Nadia: Maya and Sam ran for Silo 2. They still had their bodies when they went.|Nadia: Please. Get them out."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Nadia", "GhostNPC", "afterLine",
            "Nadia: Maya and Sam. Silo 2. Get them out."),

        // ---- Omar: the trucks, and the tape
        ("MainGameScene", "OfficeAct/Office/Ghost - Omar", "GhostNPC", "nodes.Array.data[0].choices.Array.data[2].reply",
            "Omar: The security terminal in Kessler's wing still has the replay. Four cameras."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Omar", "GhostNPC", "nodes.Array.data[1].choices.Array.data[0].reply",
            "Omar: It's on the cleaning rota in reception. Put his times in order, earliest first."),

        // ---- Kessler's ghost: before the tape
        ("MainGameScene", "OfficeAct/Office/Ghost - Kessler", "GhostNPC", "nodes.Array.data[0].choices.Array.data[3].reply",
            "Kessler: The truth? HERE? Say the wrong thing out loud in this city and it eats you alive.|Kessler: ...no. My mouth stays shut."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Kessler", "GhostNPC", "nodes.Array.data[1].line",
            "Elias is down in Silo 2. He wants everyone whose name is on that report."),

        // ---- Tomas: who did it, and what the bridge is
        ("MainGameScene", "OfficeAct/Office/Ghost - Tomas", "GhostNPC", "nodes.Array.data[0].line",
            "You. Your name's on the last line of my report. You didn't even read it."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Tomas", "GhostNPC", "nodes.Array.data[0].choices.Array.data[0].reply",
            "Tomas: ...I believe you. My brother Elias won't."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Tomas", "GhostNPC", "nodes.Array.data[0].choices.Array.data[2].reply",
            "Tomas: Your brother held the knife. Kessler held your brother.|Tomas: I only opened a truck. I saw what was inside. That's all I ever did."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Tomas", "GhostNPC", "nodes.Array.data[1].line",
            "Elias came down with you. He won't stop until everyone who signed is in the silo."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Tomas", "GhostNPC", "nodes.Array.data[1].choices.Array.data[0].reply",
            "Tomas: You can end him... or you can end it. Those aren't the same thing.|Tomas: Everything you do here is weighed. At the bridge out of the city, all of it gets counted."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Tomas", "GhostNPC", "nodes.Array.data[1].choices.Array.data[1].reply",
            "Tomas: Then go. Before the blood comes.|Tomas: Get them to the bridge. It's the only way out of this city, and it judges everyone who crosses."),
        ("MainGameScene", "OfficeAct/Office/Ghost - Tomas", "GhostNPC", "afterLine",
            "Tomas: Save who you can. The bridge counts everything."),

        // ---- the tape, and Kessler's confession
        ("MainGameScene", "OfficeAct/Flashback (CCTV)/Cutscene - Derek kills Tomas", "StabCutscene", "afterLine",
            "The screen cuts to red.|...Derek. My brother. My little brother did that.|The tape keeps rolling. Kessler walks into frame and points at the silo.|They drag Tomas to the edge... and throw him in.|Kessler didn't just cover it up. He gave the order.|His ghost is still by his door. I want to hear him say it."),
        ("MainGameScene", "OfficeAct/Office/KesslerConfrontation", "KesslerConfrontation", "afterTurnLine",
            "He said it out loud... and the city TOOK him.|That's what the Damned are. People whose sins came out."),
        ("MainGameScene", "OfficeAct/Office/KesslerConfrontation", "KesslerConfrontation", "defeatedLine",
            "...it's over. Whatever was left of him.|Something black is seeping out of the body. It slides under the door... toward the river.|Behind me, in his wing, the safe clicks open."),

        // ================================================================ SILO 2
        ("MainGameScene", "OfficeAct/SiloComplex/KillerCutscenes", "KillerCutscenes", "scareLine",
            "...footsteps. Right behind me."),
        ("MainGameScene", "OfficeAct/SiloComplex/KillerCutscenes", "KillerCutscenes", "getUpLine",
            "That face-- it's HIM. Elias!|GET UP. GET UP! RUN!"),
        ("MainGameScene", "OfficeAct/SiloComplex/KillerCutscenes", "KillerCutscenes", "slamLine",
            "The door-- it shut by itself.|...no way back."),
        ("MainGameScene", "OfficeAct/SiloComplex/KillerCutscenes", "KillerCutscenes", "ambushWarn",
            "Something just hit the roof--"),
        ("MainGameScene", "OfficeAct/SiloComplex/KillerCutscenes", "KillerCutscenes", "ambushLine",
            "He's BEHIND me-- GO! GO!"),
        ("MainGameScene", "OfficeAct/Silo", "SiloEncounter", "leapLine",
            "The stairs just... stop.|No. No no no-- JUMP!"),
        ("MainGameScene", "OfficeAct/Silo", "SiloEncounter", "derekDeathLine",
            "Elias: You pushed me off a roof. We fell together. Did you think that would stop me?|Elias: Kessler gave the order. Your brother held the knife. I took Derek. Kessler's already paid.|Elias: Your name's the last one on the report. Now YOU."),
        ("MainGameScene", "OfficeAct/Silo", "SiloEncounter", "throwLine",
            "Maya: HEY! Down there! Security's rifle-- CATCH!"),
        ("MainGameScene", "OfficeAct/Silo", "SiloEncounter", "defeatedLine",
            "He drops to his knees, blood running down his face.|Elias: Go on. Finish it. You people are good at that."),
        ("MainGameScene", "OfficeAct/Silo", "SiloEncounter", "killLine",
            "...|There's a visitor badge in his coat. ELIAS ROURKE.|Clipped to it, a photo of the night cleaner. TOMAS ROURKE. 'My little brother.'|...he was doing what I'd have done for Derek."),
        // ---- mercy: Elias starts to say who Tomas told, and Maya can't stand it
        ("MainGameScene", "OfficeAct/Silo", "SiloEncounter", "spareLine",
            "Elias: ...you'd let me live? After everything?|Elias: My brother Tomas cleaned your floors for six years. Nobody knew his name.|Elias: One night he opened one of the trucks Kessler ran through Silo 2. That's all. He looked.|Elias: He was scared out of his mind. So he told one person. Someone he trusted."),
        ("MainGameScene", "OfficeAct/Silo", "SiloEncounter", "mayaSnapLine",
            "Maya: What are you DOING?! SHOOT him!|Maya: I threw you that rifle so you'd FINISH it. Not so you'd LISTEN to him!|Maya: He KNOWS. Don't you get it? Tomas came to ME that night!|Maya: He was shaking. He told me what was in that truck. And I picked up the phone and I called Kessler.|Maya: I told him his cleaner had seen everything. I told him to DEAL with it. I KNEW what that meant!"),
        ("MainGameScene", "OfficeAct/Silo", "SiloEncounter", "mayaRealiseLine",
            "Maya: ...no. No, no-- I didn't mean to say it out LOUD--"),
        ("MainGameScene", "OfficeAct/Silo", "SiloEncounter", "mayaTurnedLine",
            "She said it. She SAID it-- and it took her.|...she's coming DOWN!"),
        ("MainGameScene", "OfficeAct/Silo", "SiloEncounter", "eliasSaveLine",
            "Elias: JUST GO! LEAVE!"),
        ("MainGameScene", "OfficeAct/Silo", "SiloEncounter", "eliasKeysLine",
            "Elias: Your keys-- TAKE them! She made the call. She's MINE!|My keys. He's had them the whole time."),
        ("MainGameScene", "OfficeAct/Silo", "SiloEncounter", "twistFloodLine",
            "The grates-- that's BLOOD. It's coming up fast!"),
        // ---- you killed him: her secret is safe, and she only owns up to the small sin
        ("MainGameScene", "OfficeAct/Silo", "SiloEncounter", "confessionLine",
            "Maya: It's done. Good. He can't tell anyone anything now.|Maya: The morgue trucks, what Kessler sold... we all took the bonuses. We all signed his report.|Maya: I'm sorry about Derek. He hated himself for it, every day after.|...I signed it without even reading it.|That's why he came for us. For Derek. For me."),
        ("MainGameScene", "OfficeAct/Silo", "BloodFlood", "clingLineSam",
            "Sam: It's coming up the stairs-- I can't swim in this! Don't let me go under!"),
        ("MainGameScene", "OfficeAct/Silo", "BloodFlood", "damnedLine",
            "Something's dragging itself out of the blood.|...Maya? She confessed down there. She said it out loud...|...and the city TOOK her."),
        ("MainGameScene", "OfficeAct/Silo", "TurningCutscene", "damnedAfter",
            "...and the city TOOK her."),
        ("MainGameScene", "OfficeAct/Silo/SiloExit", "Teleporter", "arriveLine",
            "Air. Real air.|The van's in the garage, just past the office. Then the bridge. It's the only way out."),

        // ================================================================ THE ESCAPE AND THE BRIDGE
        ("MainGameScene", "OfficeAct/Horde (escape)", "HordeChase", "introLine",
            "The engine... every one of them heard it.|The whole city is coming."),
        ("MainGameScene", "OfficeAct/Horde (escape)", "HordeChase", "stopLine",
            "The damned stopped dead. They won't set foot on the bridge.|But HE's still coming."),
        ("MainGameScene", "BridgeEnding/Kessler Demon (bridge boss)", "DemonBoss", "burstLines",
            "Kessler: DRIVER!|Kessler: You read my ledger. You watched my tape.|Kessler: You think you can just DRIVE AWAY from me?!"),
        ("MainGameScene", "BridgeEnding/Kessler Demon (bridge boss)", "DemonBoss", "transformLine",
            "Kessler: Then LOOK at what I really am--"),
        ("MainGameScene", "BridgeEnding/Kessler Demon (bridge boss)", "DemonBoss", "tunnelInLine",
            "It stopped at the tunnel... it's too big to follow me in.|...something's running across the roof."),
        ("MainGameScene", "BridgeEnding/Kessler Demon (bridge boss)", "DemonBoss", "tunnelOutLine",
            "It came off the tunnel roof-- it's RIGHT BEHIND ME!"),
        ("MainGameScene", "BridgeEnding/Kessler Demon (bridge boss)", "DemonBoss", "arriveLines",
            "The bridge just... ends. There's nowhere left to drive.|Kessler: End of the road, driver."),
        ("MainGameScene", "BridgeEnding/Kessler Demon (bridge boss)", "DemonBoss", "nowhereLine",
            "Kessler: NOWHERE LEFT TO RUN, DRIVER!"),
        ("MainGameScene", "BridgeEnding/Kessler Demon (bridge boss)", "DemonBoss", "displayName",
            "THE THING THAT WORE KESSLER"),
        ("MainGameScene", "BridgeEnding/Kessler Demon (bridge boss)", "DemonBoss", "riseLines",
            "Kessler: Did you think that was ME in the office? That was only the coat I wore.|Kessler: Forty years I fed this city the dead. It fed me back.|Kessler: The damned won't follow you here. They know what I am.|Kessler: Nobody leaves my city. Not the Lost. Not the Damned. Not YOU."),
        ("MainGameScene", "BridgeEnding/Kessler Demon (bridge boss)", "DemonBoss", "phase2Lines",
            "Kessler: Every body I sold, I kept a little piece. Come up, all of you. Say hello."),
        ("MainGameScene", "BridgeEnding/Kessler Demon (bridge boss)", "DemonBoss", "phase3Lines",
            "Kessler: Derek BEGGED me for that money!|Kessler: I made you both. I can unmake you."),
        ("MainGameScene", "BridgeEnding/Kessler Demon (bridge boss)", "DemonBoss", "deathLines",
            "Kessler: No... the river... it's pulling me DOWN--|Kessler: They'll judge you too, driver. Your name's on that report. You SIGNED--"),
        ("MainGameScene", "BridgeEnding/Kessler Demon (bridge boss)", "DemonBoss", "afterLines",
            "It's over.|Now there's only the end of the bridge... and whatever's waiting there to judge me."),
        // ---- the Demon King (his verdict itself is built from what you did: EndingSequence.VerdictLines)
        ("MainGameScene", "GameManager", "EndingSequence", "arriveLine",
            "The bridge just... ends. Red fog, black water, and nothing past it.|The sea's moving. Something's coming UP out of it. Something bigger than Kessler."),
        ("MainGameScene", "GameManager", "EndingSequence", "brandLine",
            "My hand-- it's BURNING. There's a mark in it. His mark."),
        ("MainGameScene", "GameManager", "EndingSequence", "boundLine",
            "He's gone. The fog at the end of the bridge never moved.|Behind me the city's still red. Still full of them, hiding what they did.|...fine. Who's next?"),
        ("MainGameScene", "BridgeEnding/Kessler Demon (bridge boss)/Health pickup (template)", "HealthPickup", "line",
            "A little light. Warm. Someone down here is still on my side."),
    };

    [MenuItem("HellScape/Apply Story Script")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
        int done = 0;
        foreach (var sceneName in Lines.Select(l => l.scene).Distinct())
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            bool opened = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene($"Assets/Scenes/{sceneName}.unity", OpenSceneMode.Additive);
                opened = true;
            }
            foreach (var l in Lines.Where(x => x.scene == sceneName))
            {
                var t = Find(scene, l.path);
                if (t == null) { Debug.LogWarning($"Story script: no '{l.path}' in {sceneName}"); continue; }
                var c = t.GetComponents<MonoBehaviour>().FirstOrDefault(m => m != null && m.GetType().Name == l.comp);
                if (c == null) { Debug.LogWarning($"Story script: no {l.comp} on '{l.path}'"); continue; }
                var so = new SerializedObject(c);
                var p = so.FindProperty(l.prop);
                if (p == null || p.propertyType != SerializedPropertyType.String) { Debug.LogWarning($"Story script: no text '{l.prop}' on {l.comp} '{l.path}'"); continue; }
                if (p.stringValue == l.text) continue;
                p.stringValue = l.text;
                so.ApplyModifiedProperties();
                done++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        Debug.Log($"Story script applied: {done} lines changed.");
    }

    static Transform Find(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;
            Transform t = root.transform;
            for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
            if (t != null) return t;
        }
        return null;
    }
}
