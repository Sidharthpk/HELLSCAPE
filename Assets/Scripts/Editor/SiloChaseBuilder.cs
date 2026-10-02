using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

// HellScape > Office > Extend Silo Chase: the run through the silo complex, longer, and opening on the jump scare.
// Works on the complex OfficeActBuilder made (run it again after a Build Office Act; that menu calls it itself).
//   1. you arrive at the far end of the top corridor (not at the door): a walk in the dark, and the corridor door
//      slams shut behind you
//   2. stepping out onto the first bridge: THE JUMP SCARE. Footsteps, you whip round, Elias's face, he shoves you
//      flat on your back (KillerCutscenes.JumpScare) and the chase starts with him two steps behind
//   3. the old run: bridge, corridor, machinery room, hatch ladder, conveyor walkway, lift (doors slam in his face)
//   4. NEW second leg: halfway across the lower bridge he drops off the roof behind you (Ambush); across the
//      bridge, into the tower, a full turn down the spiral stairs
//   5. the stairs stop over the drop: you jump (SiloEncounter.Leap), instead of being pushed there at the end
// A checkpoint at the bottom of the lift, so dying on the second leg doesn't send you back before it.
public static class SiloChaseBuilder
{
    const string Model = "BloodPool_low";

    [MenuItem("HellScape/Office/Extend Silo Chase")]
    public static void Menu() => Debug.Log(Extend());

    public static string Extend()
    {
        var sb = new StringBuilder();
        var cuts = Object.FindFirstObjectByType<KillerCutscenes>(FindObjectsInactive.Include);
        var chase = Object.FindFirstObjectByType<ChaseKiller>(FindObjectsInactive.Include);
        var enc = Object.FindFirstObjectByType<SiloEncounter>(FindObjectsInactive.Include);
        var elev = Object.FindFirstObjectByType<Elevator>(FindObjectsInactive.Include);
        if (cuts == null || chase == null || enc == null || elev == null) return "No silo complex here (run HellScape > Build Office Act first).";
        var scene = cuts.gameObject.scene;
        Transform c = elev.transform;                       // OfficeAct/SiloComplex
        Transform office = c.parent;
        Transform arrival = office.Find("ComplexArrival");
        var complexCp = Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(k => k.name.Contains("Silo complex"));
        var siloDoor = Object.FindObjectsByType<Teleporter>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "SiloDoor");
        var dialogue = Object.FindObjectsByType<DialogueBox>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(d => d.gameObject.scene == scene);
        Physics.SyncTransforms();

        Transform Fresh(string name)
        {
            var old = c.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var t = new GameObject(name).transform;
            t.SetParent(c, false);
            return t;
        }
        StoryTrigger Trig(string name, Vector3 pos, Vector3 size)
        {
            var t = Fresh(name);
            t.position = pos;
            var box = t.gameObject.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = size;
            var st = t.gameObject.AddComponent<StoryTrigger>();
            st.playerInCar = false;
            return st;
        }
        void Clear(UnityEvent e) { while (e.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(e, 0); }
        void Remove(UnityEvent e, Object target, string method)
        {
            for (int i = e.GetPersistentEventCount() - 1; i >= 0; i--)
                if (e.GetPersistentTarget(i) == target && e.GetPersistentMethodName(i) == method) UnityEventTools.RemovePersistentListener(e, i);
        }

        // ---------------------------------------------------------------- 1. the walk in, and the door behind you
        float corridorY = FloorY(scene, 919f, 1060f, 92f, 90.2f);
        float eye = arrival.position.y - FloorY(scene, arrival.position.x, arrival.position.z, arrival.position.y + 0.5f, corridorY);   // how high the player root sits
        if (eye < 0.5f || eye > 1.5f) eye = 0.95f;
        var door = GameObject.Find(Model + "/door2_SM2");
        bool doorWas = door != null && door.activeSelf;
        if (door != null) door.SetActive(false);
        Physics.SyncTransforms();
        // how far up the corridor can you stand and still walk to the bridge door?
        float north = 1053f;
        for (float z = 1054f; z <= 1085f; z += 0.5f)
        {
            Vector3 feet = new Vector3(919f, corridorY, z);
            bool blocked = Physics.OverlapCapsule(feet + Vector3.up * 0.5f, feet + Vector3.up * 1.5f, 0.3f, ~0, QueryTriggerInteraction.Ignore).Any(k => k.gameObject.scene == scene);
            bool floor = Physics.Raycast(feet + Vector3.up, Vector3.down, 1.6f, ~0, QueryTriggerInteraction.Ignore);
            if (blocked || !floor) break;
            north = z;
        }
        if (door != null) door.SetActive(doorWas);
        float startZ = Mathf.Max(1053f, north - 1.5f);
        bool longWalk = startZ > 1062f;
        if (longWalk)
        {
            arrival.SetPositionAndRotation(new Vector3(919f, corridorY + eye, startZ), Quaternion.Euler(0f, 180f, 0f));
            EditorUtility.SetDirty(arrival);
        }
        sb.AppendLine(longWalk ? $"arrival moved up the corridor to z {startZ:0.0} ({startZ - 1053f:0} m walk to the bridge door)" : $"corridor blocked at z {north:0.0}: arrival left at the door");

        var slamOld = c.Find("CorridorDoorSlam"); if (slamOld != null) Object.DestroyImmediate(slamOld.gameObject);
        if (longWalk && door != null && door.transform.position.z < startZ - 3f)
        {
            Remove(siloDoor.onArrive, door, "SetActive");
            UnityEventTools.AddBoolPersistentListener(siloDoor.onArrive, door.SetActive, false);              // open as you arrive
            var slam = Trig("CorridorDoorSlam", new Vector3(919f, corridorY + 1.2f, door.transform.position.z - 4.5f), new Vector3(3.5f, 3f, 1.2f));
            UnityEventTools.AddObjectPersistentListener(slam.onEnter, cuts.SlamShut, door);                    // ...shut once you're through
            Remove(complexCp.onRespawn, door, "SetActive");
            UnityEventTools.AddBoolPersistentListener(complexCp.onRespawn, door.SetActive, false);            // a respawn starts north of it again
            Remove(complexCp.onRespawn, slam, "Rearm");
            UnityEventTools.AddVoidPersistentListener(complexCp.onRespawn, slam.Rearm);
            sb.AppendLine("corridor door " + door.name + " slams behind you at z " + (door.transform.position.z - 4.5f).ToString("0.0"));
        }
        // just enough light to walk by
        var lamps = Fresh("CorridorLamps");
        if (longWalk)
            for (float z = startZ - 2f; z > 1056f; z -= 8f)
            {
                var l = new GameObject("Lamp").AddComponent<Light>();
                l.transform.SetParent(lamps, false);
                l.transform.position = new Vector3(919f, corridorY + 2.3f, z);
                l.type = LightType.Point; l.color = new Color(1f, 0.16f, 0.07f); l.intensity = 5f; l.range = 9f; l.shadows = LightShadows.None;
            }

        // ---------------------------------------------------------------- 2. the jump scare starts the chase
        var start = c.Find("ChaseStart").GetComponent<StoryTrigger>();
        var spawn = c.Find("KillerSpawn");
        start.transform.position = new Vector3(925.2f, start.transform.position.y, 1052.9f);   // a few steps out on the bridge: room to be thrown
        Clear(start.onEnter);
        UnityEventTools.AddObjectPersistentListener(start.onEnter, cuts.JumpScare, spawn);
        chase.startLine = "";   // (the jump scare says it all)
        EditorUtility.SetDirty(start); EditorUtility.SetDirty(chase);

        // ---------------------------------------------------------------- 4. the second leg: he drops onto the lower bridge
        float lowY = FloorY(scene, 965f, 1101f, 70f, 67.5f);
        var drop = Fresh("KillerDrop (lower bridge)");
        drop.SetPositionAndRotation(new Vector3(963f, lowY, 1101f), Quaternion.Euler(0f, 90f, 0f));
        var oldDread = c.Find("SiloDread"); if (oldDread != null) Object.DestroyImmediate(oldDread.gameObject);
        var ambush = Trig("Ambush (lower bridge)", new Vector3(972f, lowY + 1.3f, 1101f), new Vector3(2f, 3f, 4f));
        UnityEventTools.AddObjectPersistentListener(ambush.onEnter, cuts.Ambush, drop);
        var maya = Trig("MayaCalls (tower door)", new Vector3(991.5f, lowY + 1.3f, 1101f), new Vector3(1.5f, 3f, 4f));
        UnityEventTools.AddStringPersistentListener(maya.onEnter, dialogue.SayNow, "Maya: Up there-- the stairs! DOWN! Don't stop!");

        // a checkpoint at the bottom of the lift
        var cps = complexCp.transform.parent;
        var oldCp = cps.Find("Checkpoint - Lower bridge"); if (oldCp != null) Object.DestroyImmediate(oldCp.gameObject);
        var lower = Object.Instantiate(complexCp.gameObject, cps).GetComponent<Checkpoint>();
        lower.name = "Checkpoint - Lower bridge";
        var respawn = Fresh("LowerRespawn");
        respawn.SetPositionAndRotation(new Vector3(959.5f, lowY + eye, 1101f), Quaternion.Euler(0f, 90f, 0f));
        lower.spawn = respawn;
        Clear(lower.onRespawn);
        UnityEventTools.AddVoidPersistentListener(lower.onRespawn, chase.ResetChase);
        UnityEventTools.AddVoidPersistentListener(lower.onRespawn, ambush.Rearm);
        Remove(elev.onArrive, lower, "Activate");
        for (int i = elev.onArrive.GetPersistentEventCount() - 1; i >= 0; i--)   // (a stale one from an earlier run points at a destroyed checkpoint)
            if (elev.onArrive.GetPersistentTarget(i) == null) UnityEventTools.RemovePersistentListener(elev.onArrive, i);
        UnityEventTools.AddVoidPersistentListener(elev.onArrive, lower.Activate);
        EditorUtility.SetDirty(elev);

        // ---------------------------------------------------------------- 5. the stairs run out: jump
        // the spiral comes down one full turn to a landing under the tower door (west side, 58.3)
        Vector3 centre = enc.siloCenter.position;
        float landY = FloorY(scene, centre.x - 4.1f, centre.z, 60.5f, 58.3f);
        var leap = Trig("LeapTrigger (stairs end)", new Vector3(centre.x - 4.1f, landY + 1.3f, centre.z), new Vector3(2.2f, 2.4f, 2.8f));
        UnityEventTools.AddVoidPersistentListener(leap.onEnter, enc.Leap);
        var push = c.Find("PushTrigger");
        if (push != null && push.gameObject.activeSelf) { push.gameObject.SetActive(false); sb.AppendLine("old PushTrigger switched off (the push is the opening jump scare now; the fall is your own leap)"); }
        sb.AppendLine($"ambush at x 972 on the lower bridge (y {lowY:0.0}), leap from the stairs' last landing (y {landY:0.0})");

        // red tubes round the spiral so the way down reads at a sprint
        var stairLamps = Fresh("StairLamps");
        for (int a = 240; a >= -90; a -= 55)
        {
            float rad = a * Mathf.Deg2Rad;
            Vector3 p = centre + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * 4.6f;
            float y = FloorY(scene, p.x, p.z, 69f, float.NaN);
            if (float.IsNaN(y) || y < 55f) continue;
            var l = new GameObject("Lamp").AddComponent<Light>();
            l.transform.SetParent(stairLamps, false);
            l.transform.position = new Vector3(p.x, y + 2.1f, p.z);
            l.type = LightType.Point; l.color = new Color(1f, 0.2f, 0.08f); l.intensity = 4f; l.range = 7f; l.shadows = LightShadows.None;
        }

        EditorUtility.SetDirty(cuts);
        EditorSceneManager.MarkSceneDirty(scene);
        return sb.ToString();
    }

    // the floor under a point (the highest upward face below 'from'), or 'fallback' if there is none
    static float FloorY(UnityEngine.SceneManagement.Scene scene, float x, float z, float from, float fallback)
    {
        float best = float.NaN;
        foreach (var h in Physics.RaycastAll(new Vector3(x, from, z), Vector3.down, 14f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.gameObject.scene != scene || h.normal.y < 0.5f) continue;
            if (float.IsNaN(best) || h.point.y > best) best = h.point.y;
        }
        return float.IsNaN(best) ? fallback : best;
    }
}
