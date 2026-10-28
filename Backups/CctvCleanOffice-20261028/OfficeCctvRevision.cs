using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Reuses the authored silo shot and makes a visual-only copy of Kessler's office for a second recording.
// No level geometry, gameplay door state, actor controller or original cutscene is rebuilt.
[InitializeOnLoad]
public static class OfficeCctvRevision
{
    const string Request = "Library/OfficeCctvRevision.request";
    const string Result = "Library/OfficeCctvRevision.result";
    static OfficeCctvRevision() => EditorApplication.update += Poll;
    static void Poll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
        string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
        try
        {
            if (command == "apply") File.WriteAllText(Result, Build());
            else if (command == "test") OfficeV52Validation.Run();
            else if (command == "review") { SessionState.SetBool("OfficeV52.CctvOnly",true); OfficeV52Validation.Run(); }
        }
        catch (Exception ex) { File.WriteAllText(Result, "FAIL " + ex); Debug.LogException(ex); }
    }

    [MenuItem("HellScape/Office/CCTV - three recovered recordings")]
    public static void Menu() => Debug.Log(Build());
    public static string Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "OfficeSilo") throw new InvalidOperationException("Open OfficeSilo in Edit Mode.");
        var seq = RevisedPlot.All<OfficeV52Sequence>(scene).Single();
        var silo = RevisedPlot.All<TomasTape>(scene).Single();
        var first = silo.GetComponent<StabCutscene>();
        if (silo.derek == null || silo.tomas == null || silo.derekWalker == null || silo.exit.Length == 0)
            throw new InvalidOperationException("The existing Mercer/Tomas silo cast and stair exit must be assigned.");
        Undo.RecordObject(silo, "Recovered silo recording");
        silo.talk = "A05_tomas_tape"; silo.shot = silo.after = silo.leaving = silo.end = "";
        Undo.RecordObject(first, "Recovered silo recording");
        first.afterLine = "";
        CopyPistol(silo.derek.gameObject, silo.derekWalker);

        var entry = RevisedPlot.All<OfficeEntryTape>(scene).FirstOrDefault();
        if (entry == null) entry = BuildEntry(seq, silo);
        var second = entry.GetComponent<StabCutscene>();
        var third = seq.cctv.fireTape;
        if (third == null) throw new InvalidOperationException("The existing fire/Vane recording must remain assigned.");
        Undo.RecordObject(seq.cctv, "Three recovered CCTV recordings");
        seq.cctv.recoveredRecordings = new[] { first, second, third };
        Undo.RecordObject(second, "Kessler office recording identity");
        Undo.RecordObject(third, "Office fire recording identity");
        first.recordingStamp = "REC   CAM 04 - SILO TWO   13/04 03:10";
        second.recordingStamp = "REC   CAM 05 - KESSLER OFFICE   13/04 03:14";
        third.recordingStamp = "REC   CAM 07 - OFFICE   13/04 06:40";
        foreach (var cut in new[] { first, second, third })
        {
            var stamp = cut.overlay != null ? cut.overlay.GetComponent<TMP_Text>() : null;
            if (stamp == null) continue;
            Undo.RecordObject(stamp, "Recovered camera stamp layout");
            stamp.textWrappingMode = TextWrappingModes.NoWrap;
            stamp.rectTransform.anchorMin = stamp.rectTransform.anchorMax = new Vector2(0f,1f);
            stamp.rectTransform.pivot = new Vector2(0f,1f);
            stamp.rectTransform.anchoredPosition = new Vector2(20f,-12f);
            stamp.rectTransform.sizeDelta = new Vector2(1500f,50f);
        }
        // Labels identify each separate camera; retain the shared original label for the silo recording.
        var label = first.overlay != null ? first.overlay.GetComponent<TMP_Text>() : null;
        if (label != null) { Undo.RecordObject(label, "Silo recording label"); label.text = "REC   CAM 04 - SILO TWO   13/04 03:10"; }
        if (third.overlay == first.overlay)
        {
            Undo.RecordObject(third, "Office fire recording label");
            third.overlay = Overlay(first, "Office fire recording stamp", "REC   CAM 07 - OFFICE   13/04 06:40");
        }
        EditorUtility.SetDirty(seq.cctv); EditorUtility.SetDirty(silo); EditorUtility.SetDirty(first);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        return "PASS: 3 recordings assigned in order: Mercer shoots Tomas / stair exit; Mercer enters Kessler office; existing fire / Vane execution. Existing silo camera and exit path retained.";
    }

    static OfficeEntryTape BuildEntry(OfficeV52Sequence seq, TomasTape silo)
    {
        var reference = silo.GetComponent<StabCutscene>();
        var root = new GameObject("Cutscene - Mercer enters Kessler office");
        Undo.RegisterCreatedObjectUndo(root, "Kessler office CCTV recording");
        root.transform.SetParent(reference.transform.parent, false);
        var cast = new GameObject("Kessler office recording cast").transform;
        cast.SetParent(root.transform, false);
        Vector3 entrance = new Vector3(seq.officeDoor.transform.position.x, 3.4f, seq.officeDoor.transform.position.z);
        cast.position = new Vector3(3200f, -300f, 3000f);
        Vector3 offset = cast.position - entrance;
        var region = new Bounds(new Vector3(1006f, 5f, 887.5f), new Vector3(15f, 6f, 13f));
        var settings = Resources.Load<EchoOfficeSettings>("EchoOfficeSettings");
        var model = GameObject.Find("Office Ground Floor");
        foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!renderer.gameObject.activeInHierarchy || !renderer.enabled || !renderer.bounds.Intersects(region)
                || !renderer.TryGetComponent<MeshFilter>(out var source) || source.sharedMesh == null) continue;
            var visual = new GameObject(renderer.name);
            visual.transform.SetParent(cast, false);
            visual.transform.SetPositionAndRotation(renderer.transform.position + offset, renderer.transform.rotation);
            visual.transform.localScale = renderer.transform.lossyScale;
            visual.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            var copy = visual.AddComponent<MeshRenderer>();
            var materials = (Material[])renderer.sharedMaterials.Clone();
            if (settings != null)
                for (int i = 0; i < materials.Length; i++)
                    foreach (var pair in settings.materials)
                        if (pair.burnt != null && pair.before != null && materials[i] == pair.burnt) { materials[i] = pair.before; break; }
            copy.sharedMaterials = materials;
            copy.shadowCastingMode = renderer.shadowCastingMode;
        }
        // Clone only the door's visual rig. Its game-side KesslerDoor/Interactable/blockers remain untouched.
        var doorCopy = UnityEngine.Object.Instantiate(seq.officeDoor.door.gameObject, cast);
        doorCopy.name = "Kessler doorway (recording only)";
        doorCopy.transform.SetPositionAndRotation(seq.officeDoor.door.transform.position + offset, seq.officeDoor.door.transform.rotation);
        doorCopy.transform.localScale = seq.officeDoor.door.transform.lossyScale;
        foreach (var component in doorCopy.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in doorCopy.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in doorCopy.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var source in doorCopy.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(source);

        var mercer = MeshyCastBuilder.Man(MeshyCastBuilder.MercerWalk, "Mercer (enters Kessler office)", cast,
            MeshyCastBuilder.MercerHeight,
            silo.derek.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMaterial,
            MeshyCastBuilder.Controller("MercerOfficeEntryCctv", ("Walk", MeshyCastBuilder.Clip(MeshyCastBuilder.MercerWalk)),
                ("Idle", MeshyCastBuilder.Clip(MeshyCastBuilder.MercerAim))));
        CopyPistol(silo.derek.gameObject, mercer);
        var tape = root.AddComponent<OfficeEntryTape>();
        tape.mercer = mercer.GetComponent<Animator>(); tape.door = doorCopy.GetComponent<Animator>();
        tape.clipLength = seq.officeDoor.clipLength; tape.openFrom = seq.officeDoor.openFrom; tape.openTo = seq.officeDoor.openTo;
        tape.doorSpeed = seq.officeDoor.speed;
        Vector3[] path = { entrance + Vector3.left * 2.4f, entrance + Vector3.left * .9f,
            entrance + Vector3.right * 1.25f, new Vector3(1005.8f, 3.4f, 887.3f) };
        tape.route = path.Select((p,i) =>
        {
            var point = new GameObject("Mercer entry waypoint " + i).transform;
            point.SetParent(cast, false); point.SetPositionAndRotation(p + offset, Quaternion.LookRotation(Vector3.right));
            return point;
        }).ToArray();
        mercer.transform.SetPositionAndRotation(tape.route[0].position,tape.route[0].rotation);
        var lamp = new GameObject("Recording room light").AddComponent<Light>();
        lamp.transform.SetParent(cast, false); lamp.transform.position = new Vector3(1007f,6.1f,887.5f) + offset;
        lamp.type = LightType.Point; lamp.range = 14f; lamp.intensity = 5f; lamp.color = new Color(.85f,.9f,1f); lamp.shadows = LightShadows.None;
        var camera = new GameObject("CctvCamera - Kessler office entry").AddComponent<Camera>();
        camera.transform.SetParent(root.transform, false);
        camera.transform.position = new Vector3(1009.8f,6.1f,890.6f) + offset;
        camera.transform.rotation = Quaternion.LookRotation(entrance + offset + new Vector3(.8f,1.1f,0f) - camera.transform.position);
        camera.fieldOfView = 60f; camera.nearClipPlane = .05f; camera.farClipPlane = 45f; camera.enabled = false;
        var cut = root.AddComponent<StabCutscene>();
        cut.actors = cast.gameObject; cut.cam = camera; cut.player = reference.player;
        cut.fader = reference.fader; cut.lockDuring = reference.lockDuring; cut.hideActorsAfter = true;
        cut.overlay = Overlay(reference,"Kessler office recording stamp","REC   CAM 05 - KESSLER OFFICE   13/04 03:14");
        cut.soundSlot = "Office/CCTV silo tape"; cut.afterLine = "";
        cast.gameObject.SetActive(false);
        return tape;
    }

    static GameObject Overlay(StabCutscene reference, string name, string text)
    {
        if (reference.overlay == null) return null;
        var overlay = UnityEngine.Object.Instantiate(reference.overlay,reference.overlay.transform.parent,false);
        Undo.RegisterCreatedObjectUndo(overlay,"CCTV camera stamp"); overlay.name = name;
        var label = overlay.GetComponent<TMP_Text>(); if (label != null) label.text = text;
        overlay.SetActive(false); return overlay;
    }
    static void CopyPistol(GameObject source, GameObject target)
    {
        var hand = target.GetComponentsInChildren<Transform>(true).First(t => t.name.EndsWith("RightHand"));
        if (hand.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Pistol")) return;
        var gun = source.GetComponentsInChildren<Transform>(true).First(t => t.name == "Pistol");
        var copy = UnityEngine.Object.Instantiate(gun.gameObject,hand,false);
        Undo.RegisterCreatedObjectUndo(copy,"Mercer carried pistol"); copy.name = "Pistol";
    }
}
