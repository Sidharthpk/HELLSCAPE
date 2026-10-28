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
        ApplyCleanOffice(entry);
        var second = entry.GetComponent<StabCutscene>();
        var third = seq.cctv.fireTape;
        if (third == null) throw new InvalidOperationException("The existing fire/Vane recording must remain assigned.");
        StageVaneOffice(third);
        Undo.RecordObject(seq.cctv, "Three recovered CCTV recordings");
        seq.cctv.recoveredRecordings = new[] { first, second, third };
        Undo.RecordObject(second, "Kessler office recording identity");
        Undo.RecordObject(third, "Office fire recording identity");
        first.recordingStamp = "REC   CAM 04 - SILO TWO   13/04 03:10";
        second.recordingStamp = "REC   CAM 05 - KESSLER OFFICE   13/04 03:14";
        third.recordingStamp = "REC   CAM 07 - VANE OFFICE   13/04 06:40";
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
            third.overlay = Overlay(first, "Office fire recording stamp", "REC   CAM 07 - VANE OFFICE   13/04 06:40");
        }
        EditorUtility.SetDirty(seq.cctv); EditorUtility.SetDirty(silo); EditorUtility.SetDirty(first);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        return "PASS: 3 recordings assigned in order: Mercer shoots Tomas / stair exit; Mercer enters Kessler office; existing fire / Vane execution. Existing silo camera and exit path retained.";
    }

    static void StageVaneOffice(StabCutscene cut)
    {
        var tape = cut.GetComponent<FireTape>();
        var stage = cut.actors.transform;
        if (stage.Find("Vane office recording geometry") != null)
        {
            PoseClosedDoor(tape.door.gameObject);
            return;
        }
        Vector3 origin = new Vector3(1003.1f,3.4f,895.47f);
        Vector3 offset = stage.position - origin;
        var context = new GameObject("Vane office recording geometry").transform;
        Undo.RegisterCreatedObjectUndo(context.gameObject,"Vane office execution setting");
        context.SetParent(stage,false);
        var region = new Bounds(new Vector3(1007.56f,5f,894.6f),new Vector3(8.55f,3.5f,6.6f));
        foreach (var renderer in GameObject.Find("Office Ground Floor").GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!renderer.gameObject.activeInHierarchy || !renderer.enabled || !renderer.bounds.Intersects(region)
                || !renderer.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) continue;
            var visual = new GameObject(renderer.name);
            visual.transform.SetParent(context,false);
            visual.transform.SetPositionAndRotation(renderer.transform.position+offset,renderer.transform.rotation);
            visual.transform.localScale=renderer.transform.lossyScale;
            visual.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
            var copy=visual.AddComponent<MeshRenderer>();
            copy.sharedMaterials=renderer.sharedMaterials; copy.shadowCastingMode=renderer.shadowCastingMode;
        }
        // Retain the obsolete placeholder objects for recoverability, outside playback.
        foreach (string name in new[]{"Floor","Wall","Door","Fire behind the door"})
        {
            var old=stage.Find(name); if(old!=null) { Undo.RecordObject(old.gameObject,"Replace generic execution backdrop"); old.gameObject.SetActive(false); }
        }
        var sourceDoor=RevisedPlot.All<KesslerDoor>(cut.gameObject.scene)
            .OrderBy(d=>Vector3.Distance(d.door.transform.position,origin)).First();
        var doorCopy=UnityEngine.Object.Instantiate(sourceDoor.door.gameObject,context);
        doorCopy.name="Vane office entrance (recording only)";
        doorCopy.transform.SetPositionAndRotation(sourceDoor.door.transform.position+offset,sourceDoor.door.transform.rotation);
        doorCopy.transform.localScale=sourceDoor.door.transform.lossyScale;
        foreach(var c in doorCopy.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach(var c in doorCopy.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach(var c in doorCopy.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach(var c in doorCopy.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(c);
        var animator=doorCopy.GetComponent<Animator>();
        PoseClosedDoor(doorCopy);
        Undo.RecordObject(tape,"Vane room fire recording"); tape.door=doorCopy.transform;
        var actor=tape.mercer.transform;
        Vector3 oldPosition=actor.position; Quaternion oldRotation=actor.rotation;
        Undo.RecordObject(actor,"Mercer in Vane office");
        actor.SetPositionAndRotation(new Vector3(1005.8f,3.4f,894.9f)+offset,Quaternion.Euler(0,298f,0));
        if(tape.flash!=null)
        {
            Undo.RecordObject(tape.flash.transform,"Execution muzzle light");
            tape.flash.transform.position=actor.position+actor.rotation*Quaternion.Inverse(oldRotation)*(tape.flash.transform.position-oldPosition);
        }
        for(int i=0;i<tape.glow.Length;i++) if(tape.glow[i]!=null)
        {
            Undo.RecordObject(tape.glow[i].transform,"Vane doorway fire glow");
            tape.glow[i].transform.position=new Vector3(1003.55f,4.7f,895.0f+i*.9f)+offset;
        }
        var lamp=stage.Find("Tape light");
        if(lamp!=null) { Undo.RecordObject(lamp,"Office recording light"); lamp.position=new Vector3(1008f,6.1f,894.5f)+offset; }
        Undo.RecordObject(cut.cam.transform,"Vane office fixed CCTV camera");
        cut.cam.transform.position=new Vector3(1010.8f,6.1f,897.45f)+offset;
        cut.cam.transform.rotation=Quaternion.LookRotation(new Vector3(1006f,4.55f,894.7f)+offset-cut.cam.transform.position);
        Undo.RecordObject(cut.cam,"Vane office CCTV lens");cut.cam.fieldOfView=65f;cut.cam.nearClipPlane=.05f;cut.cam.farClipPlane=30f;
    }

    static void PoseClosedDoor(GameObject door)
    {
        var animator=door.GetComponent<Animator>();
        if(animator==null || animator.runtimeAnimatorController==null) return;
        // Sampling works while the recording cast is inactive; Animator.Update does not.
        var clip=animator.runtimeAnimatorController.animationClips.FirstOrDefault();
        if(clip!=null) clip.SampleAnimation(door,0f);
        animator.enabled=false;
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

    // Resolve the current V3 materials as well as the older Echo mappings. Only the
    // isolated second-recording cast is changed; shared source materials stay intact.
    static void ApplyCleanOffice(OfficeEntryTape entry)
    {
        var settings = Resources.Load<EchoOfficeSettings>("EchoOfficeSettings");
        Material Required(string path) => AssetDatabase.LoadAssetAtPath<Material>(path)
            ?? throw new InvalidOperationException("Missing clean CCTV material: " + path);
        Material Clean(Material material)
        {
            if (material == null) throw new InvalidOperationException("Missing office-entry material");
            string path = AssetDatabase.GetAssetPath(material);
            if (path == "Assets/Generated/Office/V3/Wall (old office, burnt).mat")
                return Required("Assets/Generated/PrologueOffice/Clean previous office wall.mat");
            if (path == "Assets/Generated/Office/V3/Floor (old office, burnt).mat")
                return Required("Assets/Generated/PrologueOffice/Clean previous office carpet.mat");
            if (path == "Assets/Generated/Office/V3/Ceiling.mat")
                return Required("Assets/Generated/PrologueOffice/Clean ceiling.mat");
            if (path.StartsWith("Assets/Generated/Burnt/Props/"))
            {
                string name = material.name.Substring("burnt_".Length);
                if (name.StartsWith("sup_")) return Required("Assets/Generated/Office/KV/" + name + ".mat");
                return AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/PrologueOffice/Clean " + name + ".mat")
                    ?? Required("Assets/Generated/Office/Perishables/Materials/" + name + ".mat");
            }
            if (settings != null)
                foreach (var pair in settings.materials)
                    if (pair.burnt == material && pair.before != null) return pair.before;
            return material;
        }
        foreach (var renderer in entry.GetComponent<StabCutscene>().actors.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials.Select(Clean).ToArray();
            if (materials.SequenceEqual(renderer.sharedMaterials)) continue;
            Undo.RecordObject(renderer, "Pre-fire Kessler CCTV materials");
            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(renderer);
        }
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
