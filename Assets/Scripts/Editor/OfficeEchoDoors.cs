using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// The room doors the user placed in the office (scene roots "Door", "Door (1)"... copies of the room-door model, with
// its DoorOpen take) made into working doors: shut and solid, [F] opens them in the Echo only, fused shut again in the
// red office, where they also stop the shadows and cut the walk map. KesslerDoor does the work; OfficeRealmPuzzles
// picks them up by itself. Safe to re-run: it rebuilds each door's blocker.
public static class OfficeEchoDoors
{
    [MenuItem("HellScape/Office/Echo Doors (wire up the placed doors)")]
    public static void Wire()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Stop Play first."); return; }
        var scene = EditorSceneManager.GetActiveScene();
        var taken = Object.FindObjectsByType<KesslerDoor>(FindObjectsSortMode.None).Where(k => k.door != null && k.blocker != null && k.blocker.name != "Echo door blocker").Select(k => k.door).ToList();
        int n = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            var anim = root.GetComponent<Animator>();
            var frame = root.transform.Find("Plane_002");
            if (anim == null || frame == null || frame.Find("Plane_001") == null || taken.Contains(anim)) continue;   // (Vane's door has its own)

            var old = frame.Find("Echo door blocker");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var mb = frame.GetComponent<MeshFilter>().sharedMesh.bounds;   // the frame: the doorway, in its own space
            var block = new GameObject("Echo door blocker").transform;
            block.SetParent(frame, false);
            block.localPosition = mb.center; block.localRotation = Quaternion.identity; block.localScale = Vector3.one;
            var box = block.gameObject.AddComponent<BoxCollider>();
            box.size = mb.size;
            var carve = block.gameObject.AddComponent<NavMeshObstacle>();
            carve.shape = NavMeshObstacleShape.Box; carve.center = Vector3.zero; carve.size = mb.size; carve.carving = true;
            var it = block.gameObject.AddComponent<Interactable>();
            it.prompt = "Open the door"; it.once = false; it.line = "";

            var kd = root.GetComponent<KesslerDoor>(); if (kd == null) kd = root.AddComponent<KesslerDoor>();
            kd.door = anim; kd.speed = 2.5f; kd.latchDelay = .15f;   // ~1 s from [F] to through: you're running
            kd.blocker = box; kd.carve = carve; kd.lockedPrompt = it; kd.echoOnly = true;
            kd.sfx = root.GetComponent<AudioSource>(); if (kd.sfx == null) kd.sfx = root.AddComponent<AudioSource>();
            kd.sfx.playOnAwake = false; kd.sfx.spatialBlend = 1f; kd.sfx.maxDistance = 15f; kd.sfx.rolloffMode = AudioRolloffMode.Linear;
            UnityEventTools.AddVoidPersistentListener(it.onUse, kd.Unlock);
            EditorUtility.SetDirty(kd);
            n++;
            Debug.Log("Echo door: " + root.name + " at " + box.bounds.center + ", doorway " + box.bounds.size);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(n + " echo doors wired");
    }
}
