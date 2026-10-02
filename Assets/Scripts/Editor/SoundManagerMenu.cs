using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// HellScape > Sound Manager: opens the Sound Library (making it, or adding any slots it's missing). Every
// area's music plus every sound effect the game plays, each with a note on what plays there now.
static class SoundManagerMenu
{
    const string Path = "Assets/Resources/SoundLibrary.asset";

    // area, slot, what it is now
    static readonly (string area, string slot, string note)[] Registry =
    {
        ("Title", "Start pressed", "Hit when you press START (built-in: a low thud). Title music = this area's Music (built-in: a drone)."),

        ("Prologue", "Heartbeat", "Heartbeat under the prologue from the open flat door onwards (now: WakeHeartbeat)."),
        ("Prologue", "Sting (body, spotted)", "Stinger when you find Derek and when you spot the killer (now: TitleSting)."),
        ("Prologue", "Stabbing on the tape", "The stabbing on the CCTV tape (now: the stabbing sound effect)."),
        ("Prologue", "Street ambience", "The morning street outside the flats (now: New York City Streets Ambience)."),
        ("Prologue", "TV murmur", "The TV murmuring in the living room (built-in: generated murmur)."),
        ("Prologue", "Wind (roof and fall)", "Wind on the roof and during the fall (built-in: generated wind)."),
        ("Prologue", "Whisper (the fall)", "Whisper as the killer goes over and halfway down the fall (built-in: generated whisper)."),
        ("Prologue", "Push", "The shove on the roof (built-in: thud)."),
        ("Prologue", "Impact (hitting the street)", "Hitting the ground at the end of the fall (built-in: thud)."),
        ("Prologue", "Door creak", "Apartment doors opening (built-in: creak)."),
        ("Prologue", "Door flung open", "The killer bursting doors open (built-in: thud)."),
        ("Prologue", "Killer footsteps", "The killer's footsteps while sneaking and fleeing (now: Footstep_1-7)."),
        ("Prologue", "Grandma horn", "The scooter horn as the grandma cuts across the van (built-in: generated beep-beeeep)."),
        ("Prologue", "Grandma scooter", "Her scooter's engine as she crosses, looped (built-in: generated two-stroke buzz)."),

        ("Prologue Chase", "", ""),

        ("Hell City", "Wake heartbeat", "Heartbeat when you come to on the red street (now: WakeHeartbeat)."),
        ("Hell City", "Hell ambience", "The hell city's background (now: D&D Ambience Hell Cries)."),
        ("Hell City", "Eerie music (street trigger)", "Music that starts at the street trigger in the hell city (now: Song of Unhealing)."),
        ("Hell City", "Zombie moans", "The damned moaning (now: Half Life 2 zombie cries reversed). Several clips = variety."),
        ("Hell City", "Zombie scream", "A zombie screaming when it notices you (built-in: its moan, pitched down)."),
        ("Hell City", "Punch hits", "Your fists landing (now: PunchHit_1-3)."),
        ("Hell City", "Car hits a zombie", "The car ploughing into one (now: CarHit_1-3)."),
        ("Hell City", "Car engine", "The car's engine loop (now: CarEngine)."),
        ("Hell City", "Tire screech", "Tyre screech loop (now: TireScreech)."),

        ("Office", "Ghost whispers", "The office ghosts' whispering loop (built-in: generated whisper)."),
        ("Office", "Fuse clunk", "A fuse pushed into the fuse box (built-in: thud)."),
        ("Office", "Kessler door latch", "Kessler's door unlocking (built-in: click)."),
        ("Office", "Kessler door creak", "Kessler's door swinging open (built-in: creak)."),
        ("Office", "Kessler turning (whisper)", "Kessler burning red as he confesses (built-in: whisper)."),
        ("Office", "Kessler becomes the damned", "The flash when he turns (built-in: thud)."),
        ("Office", "CCTV stabbing", "The CCTV tape of Derek stabbing Tomas (the flashback after the camera puzzle)."),
        ("Office", "Kessler lunges", "The damned Kessler lunging at you (built-in: thud)."),

        ("Kessler Fight", "", ""),

        ("Silo Complex", "Killer sting", "Stinger when the killer steps out of the dark / sprints at the lift (built-in: thud)."),
        ("Silo Complex", "Lift doors slam on him", "The lift doors slamming in the killer's face (built-in: thud)."),
        ("Silo Complex", "Lift doors", "The freight lift's doors (built-in: thud)."),
        ("Silo Complex", "Lift ride rumble", "The lift moving (built-in: rumble)."),
        ("Silo Complex", "Ladder rung", "Each rung on the hatch ladder (built-in: click)."),
        ("Silo Complex", "Killer hits you (chase)", "The killer catching you during the chase (built-in: thud)."),

        ("Silo Chase", "", ""),

        ("Silo Fight", "Push and fall thud", "Footsteps behind you, the shove, the landing (built-in: thud)."),
        ("Silo Fight", "Killer hurt", "The killer grunting when you hit him."),
        ("Silo Fight", "Rifle shot", "The rifle firing (built-in: generated gunshot)."),
        ("Silo Fight", "Rifle reload", "Reloading (silent while empty)."),
        ("Silo Fight", "Blood flood rumble", "The silo flooding with blood (built-in: rumble)."),
        ("Silo Fight", "Swim stroke", "Each Space kick while swimming up through the blood."),
        ("Silo Fight", "Drowning", "You going under for good (the air bar ran out)."),
        ("Silo Fight", "Colleague turns into the Lost", "Cutscene: a drowned colleague's ghost rising out of the blood."),
        ("Silo Fight", "Colleague turns into the Damned", "Cutscene: Maya burning into one of the Damned."),
        ("Silo Fight", "Survivor drowning", "Maya or Sam slipping under when you let the air bar run too low."),

        ("Escape", "Blood wave roar", "The blood orb rolling after the car (built-in: rumble)."),

        ("Blood Orb", "", ""),   // music once the orb's cutscene ends (fades in), until the bridge (fades out)

        ("Demon Fight", "", ""),   // music for the bridge fight with Kessler's true form
        ("Demon Fight", "Boss footsteps", "Kessler's monster stomping after you in the chase (built-in: deep bass stomp)."),
        ("Demon Fight", "Boss dragged under", "The drowned dragging it into the water when it dies (built-in: rumble)."),

        ("Ending", "Angel choir", "The angel descending (now: AngelChoir)."),
        ("Ending", "Demon rises", "The demon rising (now: DemonRise)."),

        ("General", "Dialogue blip", "The typing blip of the dialogue box (now: TextBlip)."),
        ("General", "Player footsteps", "Your footsteps (now: Footstep_1-7)."),
        ("General", "Player footsteps (metal)", "Your footsteps in the silo complex: metal stairs and walkways."),
        ("General", "Door transition latch", "Resident Evil door: the latch (built-in: click)."),
        ("General", "Door transition creak", "Resident Evil door: the creak (built-in: creak)."),
        ("General", "Door transition step", "Resident Evil door: the step through (built-in: thud)."),
        ("General", "Door transition (full)", "Resident Evil door: one recording of the whole thing. When set, it replaces latch/creak/step."),
    };

    [MenuItem("HellScape/Sound Manager")]
    static void Open()
    {
        var lib = Sync();
        Selection.activeObject = lib;
        EditorGUIUtility.PingObject(lib);
    }

    public static SoundLibrary Sync()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var lib = AssetDatabase.LoadAssetAtPath<SoundLibrary>(Path);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<SoundLibrary>();
            AssetDatabase.CreateAsset(lib, Path);
        }
        foreach (var (area, slot, note) in Registry)
        {
            var a = lib.areas.FirstOrDefault(x => x.name == area);
            if (a == null) { a = new SoundLibrary.Area { name = area }; lib.areas.Add(a); }
            if (string.IsNullOrEmpty(slot)) continue;   // an area that's only music
            var s = a.sounds.FirstOrDefault(x => x.name == slot);
            if (s == null) a.sounds.Add(new SoundLibrary.Slot { name = slot, note = note });
            else if (string.IsNullOrEmpty(s.note)) s.note = note;
        }
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        return lib;
    }

    // the scene's own audio sources that play by themselves: route them through their slots
    // where each doorway takes you, musically (also run by the office act builder)
    public static void SetMusicAreas()
    {
        foreach (var tp in Object.FindObjectsByType<Teleporter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string area = tp.name switch
            {
                "OfficeDoor" => "Office",
                "ExitDoor" => "Hell City",
                "SiloDoor" => "Silo Complex",
                "SiloExit" => "Escape",
                _ => tp.musicArea,
            };
            if (area == tp.musicArea) continue;
            Undo.RecordObject(tp, "Music area");
            tp.musicArea = area;
            EditorUtility.SetDirty(tp);
        }
    }

    [MenuItem("HellScape/Sound Manager - hook up scene sources")]
    static void HookScene()
    {
        Sync();
        void Hook(string path, string slot)
        {
            var go = GameObject.Find(path);
            if (go == null || go.GetComponent<AudioSource>() == null) { Debug.LogWarning("HellScape sound: no AudioSource at " + path); return; }
            var s = go.GetComponent<SoundSlotSource>() ?? Undo.AddComponent<SoundSlotSource>(go);
            s.slot = slot;
            EditorUtility.SetDirty(s);
        }
        if (GameObject.Find("HellWorld") != null || Resources.FindObjectsOfTypeAll<GameObject>().Any(g => g.name == "HellWorld" && g.scene.IsValid()))
        {
            var bg = Resources.FindObjectsOfTypeAll<AudioSource>().FirstOrDefault(a => a.gameObject.scene.IsValid() && a.name == "HorrorBg");
            if (bg != null) { var s = bg.GetComponent<SoundSlotSource>() ?? Undo.AddComponent<SoundSlotSource>(bg.gameObject); s.slot = "Hell City/Hell ambience"; EditorUtility.SetDirty(s); }
            Hook("Sounds/Car Engine Sound", "Hell City/Car engine");
            Hook("Sounds/Tire Screech Sound", "Hell City/Tire screech");
        }
        SetMusicAreas();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("HellScape: scene audio hooked up to the Sound Library.");
    }
}
