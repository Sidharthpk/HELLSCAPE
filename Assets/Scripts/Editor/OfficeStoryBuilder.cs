using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// HellScape > Office > Story 2026-11 (the user's office script), OfficeSilo only. Re-runnable.
//  - Kessler's office door: no longer Echo-only, opens with the keypad (1304), which moves from Vane's door to it
//  - Vane's office door: the crowbar (OfficeV52Sequence.UseVaneDoor)
//  - armory: three lockers, the middle one (Locker 03) has the dial, code 13-4-21 (the stairs tape's stamp 13/04 03:21)
//  - CCTV playlist: Mercer kills Tomas / Mercer at Kessler's keypad / Kessler + Mercer on the stairs / Derek takes the file
public static class OfficeStoryBuilder
{
    const string DerekRun = "Assets/Perishables/Avatar/Meshy_AI_Derek_PS2_Rigged_biped (1)/Derek_Run Trip/Meshy_AI_Derek_PS2_Rigged_biped_Animation_Running_frame_rate_30.fbx";
    public const string StairsStamp = "REC   CAM 04 - SILO TWO   13/04 03:21";
    static readonly int[] LockerCode = { 13, 4, 21 };

    [MenuItem("HellScape/Office/Story 2026-11 (build the office script)")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        if (EditorApplication.isPlaying) return "Stop Play first.";
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "OfficeSilo") return "Open OfficeSilo.";
        var log = new System.Text.StringBuilder();
        var seq = RevisedPlot.All<OfficeV52Sequence>(scene).Single();
        Undo.RecordObject(seq, "Office story");

        // ---- doors and keypad
        var doors = RevisedPlot.All<KesslerDoor>(scene);
        seq.vaneDoor = doors.First(d => d.name == "KesslerDoor");
        seq.officeDoor = doors.First(d => d.name == "Door" && d.transform.parent == null);
        Undo.RecordObject(seq.officeDoor, "Office story"); seq.officeDoor.echoOnly = false;
        foreach (var g in seq.officeDoor.GetComponentsInChildren<OfficeProgressGate>(true))
        { Undo.RecordObject(g, "Office story"); g.sequence = seq; g.rule = OfficeProgressGate.Rule.KesslerDoor; g.unavailable = "Kessler's office. Locked. There's a keypad beside it."; }
        var kp = RevisedPlot.All<Keypad>(scene).Single();
        seq.keypad = kp;
        Undo.RecordObject(kp.transform, "Office story"); Undo.RecordObject(kp, "Office story");
        var vaneBlock = seq.vaneDoor.blocker.bounds.center; var kessBlock = seq.officeDoor.blocker.bounds.center;
        Vector3 off = new Vector3(1003.0f, 5.1f, 896.8f) - new Vector3(1003.16f, 4.7f, 895.48f);
        kp.transform.position = new Vector3(kessBlock.x + off.x, kp.transform.position.y, kessBlock.z + off.z);
        kp.echoOnly = false; kp.code = "1304";
        for (int i = kp.onSolved.GetPersistentEventCount() - 1; i >= 0; i--)
            if (kp.onSolved.GetPersistentTarget(i) is KesslerDoor) UnityEventTools.RemovePersistentListener(kp.onSolved, i);
        var kso = new SerializedObject(kp);
        var calls = kso.FindProperty("onSolved.m_PersistentCalls.m_Calls");
        for (int i = 0; i < calls.arraySize; i++)
        {
            var arg = calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_Arguments.m_StringArgument");
            if (arg.stringValue.Contains("swings open")) arg.stringValue = "*click* The lock gives. The office door swings open.";
        }
        kso.ApplyModifiedProperties();
        foreach (var g in RevisedPlot.All<OfficeProgressGate>(scene).Where(g => g.rule == OfficeProgressGate.Rule.Silo))
        { Undo.RecordObject(g, "Office story"); g.unavailable = "Not yet. Kessler's pistol, the Silo Two keys, and the Lost still need me."; }
        log.AppendLine("keypad moved to Kessler's door at " + kp.transform.position.ToString("F2"));

        // ---- armory: two more lockers either side of Locker 03, the dial on the middle one
        var root = seq.transform;
        var back = root.Find("Locker 03 back");
        foreach (var t in root.Cast<Transform>().Where(t => t.name.StartsWith("Locker (side)")).ToArray()) Undo.DestroyObjectImmediate(t.gameObject);
        foreach (int side in new[] { -1, 1 })
        {
            var holder = new GameObject("Locker (side) " + (side < 0 ? "left" : "right")).transform;
            Undo.RegisterCreatedObjectUndo(holder.gameObject, "Office story");
            holder.SetParent(root, false);
            foreach (var n in new[] { "Locker 03 back", "Locker 03 floor", "Locker 03 side -1", "Locker 03 side 1", "Locker 03 rotary lock" })
            {
                var src = root.Find(n); if (src == null) continue;
                var c = new GameObject(n.Replace("Locker 03", "Locker 0" + (side < 0 ? 2 : 4)));
                c.transform.SetParent(holder, false);
                c.transform.SetPositionAndRotation(src.position + Vector3.right * side * .64f, src.rotation);
                c.transform.localScale = src.lossyScale;
                c.AddComponent<MeshFilter>().sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
                c.AddComponent<MeshRenderer>().sharedMaterials = src.GetComponent<MeshRenderer>().sharedMaterials;
                c.AddComponent<BoxCollider>();
                if (n.EndsWith("rotary lock"))
                {
                    var use = c.AddComponent<Interactable>();
                    use.prompt = "Locker 0" + (side < 0 ? 2 : 4); use.once = false;
                    use.line = side < 0 ? "Rusted shut. No dial, no keyhole. Nothing in here for me." : "Empty. Someone cleared this one out in a hurry.";
                }
            }
        }
        Undo.RecordObject(seq.locker, "Office story");
        seq.locker.code = LockerCode; seq.locker.requireDirections = false;
        seq.locker.lockedText = "LOCKER 03 - A.K. PERSONAL\nThree numbers. Scratched on the rim: \"the night on the stairs - day, month, minute\".";
        var note = root.Find("Executive storage maintenance note");
        if (note != null) { Undo.RecordObject(note.gameObject, "Office story"); note.gameObject.SetActive(false); }
        log.AppendLine("lockers 02/04 beside Locker 03; dial 13-4-21");

        // ---- the colleagues' spots (the sequence also snaps them to clear floor at runtime)
        foreach (var g in RevisedPlot.All<GhostNPC>(scene))
        {
            Vector3? at = g.ghostName == "Omar" ? seq.omarAt : g.ghostName == "Priya" ? seq.priyaAt : g.ghostName == "Nadia" ? seq.nadiaAt : g.ghostName == "Ben" ? seq.benAt : (Vector3?)null;
            if (at == null) continue;
            Undo.RecordObject(g.transform, "Office story");
            g.transform.position = at.Value;
        }

        // ---- the tapes
        var desk = seq.cctv;
        var tomas = RevisedPlot.All<TomasTape>(scene).Single();
        Undo.RecordObject(tomas, "Office story");
        tomas.end = "A3V6_tomas_realise";
        var first = tomas.GetComponent<StabCutscene>();
        var entry = RevisedPlot.All<OfficeEntryTape>(scene).Single().GetComponent<StabCutscene>();
        var stairs = BuildStairs(first, log);
        var derek = BuildDerek(first, desk, seq.player, log);
        Undo.RecordObject(desk, "Office story");
        desk.recoveredRecordings = new[] { first, entry, stairs, derek };
        desk.officeSequence = seq;

        EditorUtility.SetDirty(seq);
        EditorSceneManager.MarkSceneDirty(scene);
        return log.ToString();
    }

    static StabCutscene BuildStairs(StabCutscene tomasTape, System.Text.StringBuilder log)
    {
        var parent = tomasTape.transform.parent;
        var old = parent.Find("Cutscene - Kessler and Mercer on the stairs");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var go = Object.Instantiate(tomasTape.gameObject, parent);
        go.name = "Cutscene - Kessler and Mercer on the stairs";
        Undo.RegisterCreatedObjectUndo(go, "Office story");
        Object.DestroyImmediate(go.GetComponent<TomasTape>());
        var cut = go.GetComponent<StabCutscene>();
        var cast = cut.actors.transform;
        cast.gameObject.name = "Stairs tape cast";
        Animator mercer = null;
        foreach (Transform c in cast.Cast<Transform>().ToArray())
        {
            var a = c.GetComponent<Animator>();
            if (c.name == "Mercer" && a != null) mercer = a;
            else if (c.GetComponentInChildren<Light>(true) == null || a != null) Object.DestroyImmediate(c.gameObject);   // (the tape's night light stays)
        }
        if (mercer == null) throw new System.Exception("no Mercer in the Tomas tape cast");
        // on the landing, facing the lens: Kessler stands just below the camera, out of shot
        var cam = tomasTape.cam.transform.position;
        Vector3 face = cam - mercer.transform.position; face.y = 0f;
        mercer.transform.rotation = Quaternion.LookRotation(face);
        mercer.gameObject.SetActive(true);
        var tape = go.AddComponent<StairsTape>(); tape.mercer = mercer;
        cut.recordingStamp = StairsStamp;
        cut.hideAfter = new GameObject[0]; cut.showAfter = null; cut.afterLine = ""; cut.soundSlot = "-"; cut.sound = null;
        log.AppendLine("stairs tape: Mercer at " + mercer.transform.position.ToString("F2"));
        return cut;
    }

    static StabCutscene BuildDerek(StabCutscene template, CctvDesk desk, Transform player, System.Text.StringBuilder log)
    {
        var parent = template.transform.parent;
        var old = parent.Find("Cutscene - Derek takes the file");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var go = new GameObject("Cutscene - Derek takes the file");
        Undo.RegisterCreatedObjectUndo(go, "Office story");
        go.transform.SetParent(parent, false);
        var cast = new GameObject("Derek tape cast").transform; cast.SetParent(go.transform, false);

        // the security room: in from the maintenance side, to the CCTV desk, and back out
        Vector3[] inPts = { new Vector3(1005.3f, 3.4f, 901.9f), new Vector3(1005.1f, 3.4f, 904.7f), new Vector3(1004.0f, 3.4f, 906.2f), new Vector3(1002.6f, 3.4f, 905.6f), new Vector3(1001.5f, 3.4f, 904.5f), new Vector3(1001.7f, 3.4f, 902.4f), new Vector3(1002.25f, 3.4f, 901.6f) };
        Vector3[] outPts = { new Vector3(1001.7f, 3.4f, 902.4f), new Vector3(1001.5f, 3.4f, 904.5f), new Vector3(1002.6f, 3.4f, 905.6f), new Vector3(1004.0f, 3.4f, 906.2f), new Vector3(1005.1f, 3.4f, 904.7f), new Vector3(1005.3f, 3.4f, 901.6f), new Vector3(1004.2f, 3.4f, 899.6f) };
        Transform[] Markers(string n, Vector3[] pts) => pts.Select((p, i) =>
        {
            var m = new GameObject(n + " " + i).transform; m.SetParent(go.transform, false);
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var h, 3f, ~0, QueryTriggerInteraction.Ignore)) p.y = h.point.y;
            m.position = p; return m;
        }).ToArray();

        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Meshy/Derek.mat");
        var walker = MeshyCastBuilder.Man(MeshyCastBuilder.DerekWalk, "Derek (walks in)", cast, MeshyCastBuilder.DerekHeight, mat,
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Generated/Meshy/DerekMemoryWalk.controller"));
        var runner = MeshyCastBuilder.Man(DerekRun, "Derek (runs out)", cast, MeshyCastBuilder.DerekHeight, mat,
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Generated/Meshy/DerekShot.controller"));
        foreach (var c in walker.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        foreach (var c in runner.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        foreach (var rb in runner.GetComponentsInChildren<Rigidbody>()) Object.DestroyImmediate(rb);

        // the file on the desk
        var file = GameObject.CreatePrimitive(PrimitiveType.Cube);
        file.name = "Derek's file"; Object.DestroyImmediate(file.GetComponent<Collider>());
        file.transform.SetParent(cast, false);
        file.transform.localScale = new Vector3(.24f, .03f, .32f);
        Vector3 fileAt = new Vector3(1002.95f, 5.5f, 902.0f);
        if (Physics.Raycast(fileAt, Vector3.down, out var top, 3f, ~0, QueryTriggerInteraction.Ignore)) fileAt = top.point + Vector3.up * .02f;
        file.transform.position = fileAt;
        var fm = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Meshy/DerekFile.mat");
        if (fm == null) { fm = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.75f, .62f, .38f) }; AssetDatabase.CreateAsset(fm, "Assets/Generated/Meshy/DerekFile.mat"); }
        file.GetComponent<Renderer>().sharedMaterial = fm;

        // a camera in the room's far corner, looking at the desk and the door
        var camGo = new GameObject("CctvCamera (security room)");
        camGo.transform.SetParent(go.transform, false);
        camGo.transform.position = new Vector3(996.6f, 6.0f, 903.8f);
        camGo.transform.rotation = Quaternion.LookRotation(new Vector3(1002.6f, 4.3f, 903.2f) - camGo.transform.position);
        var cam = camGo.AddComponent<Camera>(); cam.fieldOfView = 70f; cam.depth = template.cam.depth; cam.enabled = false;
        var light = new GameObject("Tape light").AddComponent<Light>();
        light.transform.SetParent(go.transform, false); light.transform.position = new Vector3(1000.5f, 6.2f, 904f);
        light.type = LightType.Point; light.range = 9f; light.intensity = 2.2f; light.color = new Color(.85f, .9f, 1f); light.shadows = LightShadows.None;
        light.transform.SetParent(cast, true);

        var cut = go.AddComponent<StabCutscene>();
        cut.actors = cast.gameObject; cut.cam = cam; cut.overlay = template.overlay; cut.player = template.player;
        cut.lockDuring = template.lockDuring; cut.fader = template.fader; cut.soundSlot = "-";
        cut.recordingStamp = "REC   CAM 07 - SECURITY   13/04 05:02";
        cut.hideActorsAfter = true;
        var tape = go.AddComponent<DerekTape>();
        tape.walker = walker.GetComponent<Animator>(); tape.runner = runner.GetComponent<Animator>();
        tape.walkIn = Markers("In", inPts); tape.runOut = Markers("Out", outPts);
        tape.file = file; tape.player = player;
        walker.transform.position = tape.walkIn[0].position;
        runner.SetActive(false);
        cast.gameObject.SetActive(false);
        log.AppendLine("derek tape built; file at " + fileAt.ToString("F2"));
        return cut;
    }
}
