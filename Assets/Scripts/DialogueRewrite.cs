using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// The wording of every line that isn't a story beat in RevisedPlot: props, cutscene one-liners, small reactions.
// Style: short, flat, specific. People under stress say less, not more.
// Fields: "Type.field" -> text for every instance. Exact: old text -> new text for the shared Interactable/Teleporter fields.
public static class DialogueRewrite
{
    static readonly Dictionary<string, string> Fields = new Dictionary<string, string>
    {
        { "DeerCutscene.feedLine", "Where am I?" },
        { "DeerCutscene.afterLine", "None of them look at me." },
        { "GateCutscene.line", "Something's moving down there.|...What is that?" },
        { "GateButton.openedLine", "Where am I?" },
        { "Elevator.notInsideLine", "I need to be inside first." },
        { "Elevator.openLine", "*The lift doors grind open.*|Get in." },
        { "BodyBagScare.afterLine", "It moved.|...No. It's a body. It's just a body." },
        { "BodyBagScare.twitchLine", "Did that one just move?" },
        { "ChaseKiller.startLine", "Footsteps. Heavy.|Run." },
        { "ChaseKiller.hitLines", "Agh--|He's on me.|Move." },
        { "KillerCutscenes.scareLine", "Footsteps. Close behind me." },
        { "KillerCutscenes.getUpLine", "That's him. Elias.|Get up. Get up and run." },
        { "KillerCutscenes.slamLine", "The door shut behind me. By itself.|...There's no going back that way." },
        { "KillerCutscenes.ambushWarn", "Something just landed on the roof." },
        { "KillerCutscenes.ambushLine", "He's behind me. Go. Go." },
        { "GrandmaScare.lines", "Jesus--|...Where did she come from?|\"Watch where you're going, love!\"" },
        { "PuzzleCounter.progressLine", "{0} of {1}." },
        { "SequencePuzzle.stepLine", "CAM: {0}. He was here." },
        { "Survivor.turnedLine", "" },
    };

    static readonly Dictionary<string, string> Exact = new Dictionary<string, string>
    {
        { "Maya: I've got you. Let's GO.", "Maya: Okay. I'm with you." },
        { "Sam: Thank you. Thank you, thank you.", "Sam: Thank you. God, thank you." },
        { "A fuse, on a crate by the body. Still warm.", "A fuse on a crate beside the body. Still warm." },
        { "A fuse. Pushed into the old cake like a candle.", "A fuse, pushed into the cake like a candle.|Somebody thought that was funny." },
        { "A fuse. Taped under Derek's keyboard.", "A fuse, taped under Derek's keyboard.|He always did hide things badly." },
        { "SILO 2 - AUTHORISED STAFF ONLY.|Locked. Vane keeps the only key on him.", "SILO 2. AUTHORISED STAFF ONLY.|Deadbolt. Vane wore the key round his neck." },
        { "The fuse box. Three slots, all empty.|Someone pulled them out on purpose.", "Three slots. All empty.|Somebody took them out on purpose." },
        { "The office. Pitch black.|...hello?|Voices, in the dark. Whispering my name.|These are the people I work with. Worked with.", "*The office. No lights. Wet carpet and rust.*|...Hello?|*Voices, somewhere in the dark. Low. Saying my name.*|I know those voices. I work with these people." },
        { "A service tunnel, under Silo 2. ...what IS that, hanging from the roof?", "*A service tunnel under Silo 2. Cold air, moving.*|...What's hanging from the ceiling?" },
        { "Air. Real air.|The van's in the garage, just past the office. Then the bridge. It's the only way out.", "Air.|The van's in the garage past the office. After that, the bridge. It's the only way out." },
        { "Not without my keys.", "Not without the keys." },
        { "The silo key. GO!", "The silo key. Go." },
        { "Locked. There's a keypad on the wall beside it.", "Locked. Keypad on the wall beside it." },
        { "\"...police have called off the search for Tomas Rourke, a night cleaner at the Kessler & Vane depot, missing three weeks. His brother Elias still believes--\"|Kessler & Vane. Derek and I both work there.|Derek hasn't slept properly since that cleaner went missing.", "\"...police have scaled back the search for Tomas Rourke, a night cleaner at the Kessler & Vane depot. Missing three weeks. His brother, Elias, says he won't stop looking--\"|Kessler & Vane. My employer.|Derek stopped sleeping around the time that cleaner disappeared." },
        { "Me and Derek. His birthday. He burned the cake and blamed me for it.|My little brother. I was supposed to look out for him.", "Me and Derek. His birthday. He burned the cake and swore it was my fault.|I was supposed to look out for him." },
    };

    public static void Apply(Scene scene)
    {
        foreach (var c in RevisedPlot.All<MonoBehaviour>(scene))
        {
            if (c == null || c.GetType().Assembly != typeof(DialogueRewrite).Assembly) continue;
            string type = c.GetType().Name;
            foreach (var f in c.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType != typeof(string)) continue;
                if (Fields.TryGetValue(type + "." + f.Name, out var text)) f.SetValue(c, text);
                else if (Exact.TryGetValue((string)f.GetValue(c) ?? "", out var swap)) f.SetValue(c, swap);
            }
        }
    }
}
