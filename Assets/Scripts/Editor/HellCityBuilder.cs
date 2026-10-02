using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// The hell city street you wake up in:
//   HellScape > Hell City > Build Deer Cutscene - pins the feeding zombies to the street (their Animator snaps them
//       to 0,0,0 when the city switches on, so each now sits at local zero inside a holder that carries its place)
//       and puts a cutscene on the Deer Zombie Trigger: it creeps in on the one eating the deer, and it screams at you.
public static class HellCityBuilder
{
    const string FeedController = "Assets/Enemies/SimpleAnimations.controller";
    const string ScreamFbx = "Assets/Enemies/Scary Zombie Pack/zombie scream.fbx";

    [MenuItem("HellScape/Hell City/Build Deer Cutscene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Stop Play first."); return; }
        var sleep = Object.FindFirstObjectByType<SleepSequence>(FindObjectsInactive.Include);
        var hell = sleep.hellWorld.transform;
        var trig = hell.GetComponentsInChildren<StoryTrigger>(true).First(t => t.name == "Deer Zombie Trigger");

        // ---- 1. hold every feeding zombie in place
        var holders = new List<Transform>();
        foreach (var an in hell.GetComponentsInChildren<Animator>(true).Where(a => a.name.StartsWith("Ch10_nonPBR")).ToArray())
        {
            var z = an.transform;
            if (z.parent != null && z.parent.name.StartsWith("Feeding Zombie")) { holders.Add(z.parent); continue; }
            var holder = new GameObject("Feeding Zombie (" + z.name + ")").transform;
            Undo.RegisterCreatedObjectUndo(holder.gameObject, "Deer");
            holder.SetParent(z.parent, false);
            holder.SetPositionAndRotation(z.position + Vector3.down * GroundGap(z), z.rotation);
            Undo.SetTransformParent(z, holder, "Deer");
            Undo.RecordObject(z, "Deer");
            z.localPosition = Vector3.zero;
            z.localRotation = Quaternion.identity;
            holders.Add(holder);
        }

        // ---- 2. a scream on the feeding controller
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(FeedController);
        var sm = ac.layers[0].stateMachine;
        if (!sm.states.Any(s => s.state.name == "Scream"))
        {
            var st = sm.AddState("Scream", new Vector3(300f, 150f, 0f));
            st.motion = AssetDatabase.LoadAllAssetsAtPath(ScreamFbx).OfType<AnimationClip>().First(c => c.name == "ZombieScream");
            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
        }

        // ---- 3. the cutscene, on the trigger
        var deer = hell.Cast<Transform>().Where(t => t.name.StartsWith("model")).OrderBy(t => (t.position - trig.transform.position).sqrMagnitude).First();
        var feeder = holders.OrderBy(h => (h.position - deer.position).sqrMagnitude).First();
        var dc = trig.GetComponent<DeerCutscene>();
        if (dc == null) dc = Undo.AddComponent<DeerCutscene>(trig.gameObject);
        Undo.RecordObject(dc, "Deer");
        dc.feeder = feeder;
        dc.feederAnim = feeder.GetComponentInChildren<Animator>(true);
        dc.deer = deer;
        var player = GameObject.FindGameObjectWithTag("Player");
        dc.player = player != null ? player.transform : null;
        dc.playerCam = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(c => c.name == "PlayerCamera");
        var kc = Object.FindFirstObjectByType<KesslerConfrontation>(FindObjectsInactive.Include);
        dc.lockDuring = kc != null ? kc.lockDuring.Where(b => b != null && (player == null || b.transform.IsChildOf(player.transform))).ToArray() : new Behaviour[0];
        dc.hideDuring = dc.playerCam.transform.Cast<Transform>().Where(t => t.GetComponentInChildren<Renderer>(true) != null).Select(t => t.gameObject).ToArray();
        var story = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(c => c.name == "StoryCanvas").transform;
        dc.letterboxTop = story.Find("LetterboxTop") as RectTransform;
        dc.letterboxBottom = story.Find("LetterboxBottom") as RectTransform;
        dc.fader = Object.FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);

        // what the trigger did (the spawner, the title) now happens when the cutscene ends
        var ev = trig.onEnter;
        bool rewired = false;
        for (int i = ev.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            if (ev.GetPersistentTarget(i) == dc) { rewired = true; continue; }
        }
        if (!rewired)
        {
            Undo.RecordObject(trig, "Deer");
            // copy the old calls across by serialized data, so their arguments (enabled = true) come too
            var soT = new SerializedObject(trig);
            var soD = new SerializedObject(dc);
            var from = soT.FindProperty("onEnter.m_PersistentCalls.m_Calls");
            var to = soD.FindProperty("onDone.m_PersistentCalls.m_Calls");
            to.arraySize = from.arraySize;
            for (int i = 0; i < from.arraySize; i++) CopyCall(from.GetArrayElementAtIndex(i), to.GetArrayElementAtIndex(i));
            soD.ApplyModifiedProperties();
            from.arraySize = 0;
            soT.ApplyModifiedProperties();
            UnityEventTools.AddPersistentListener(trig.onEnter, dc.Play);
        }
        EditorUtility.SetDirty(dc);
        EditorUtility.SetDirty(trig);
        EditorSceneManager.MarkSceneDirty(trig.gameObject.scene);
        Debug.Log("Deer cutscene: " + holders.Count + " feeding zombies pinned, cutscene on " + feeder.name + " by " + deer.name + ", " + dc.onDone.GetPersistentEventCount() + " calls moved to after it.");
    }

    static void CopyCall(SerializedProperty a, SerializedProperty b)
    {
        foreach (var n in new[] { "m_Target", "m_TargetAssemblyTypeName", "m_MethodName", "m_Mode", "m_CallState" })
        {
            var pa = a.FindPropertyRelative(n); var pb = b.FindPropertyRelative(n);
            if (pa == null || pb == null) continue;
            switch (pa.propertyType)
            {
                case SerializedPropertyType.ObjectReference: pb.objectReferenceValue = pa.objectReferenceValue; break;
                case SerializedPropertyType.String: pb.stringValue = pa.stringValue; break;
                default: pb.intValue = pa.intValue; break;
            }
        }
        var aa = a.FindPropertyRelative("m_Arguments"); var ba = b.FindPropertyRelative("m_Arguments");
        ba.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = aa.FindPropertyRelative("m_ObjectArgument").objectReferenceValue;
        ba.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = aa.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue;
        ba.FindPropertyRelative("m_IntArgument").intValue = aa.FindPropertyRelative("m_IntArgument").intValue;
        ba.FindPropertyRelative("m_FloatArgument").floatValue = aa.FindPropertyRelative("m_FloatArgument").floatValue;
        ba.FindPropertyRelative("m_StringArgument").stringValue = aa.FindPropertyRelative("m_StringArgument").stringValue;
        ba.FindPropertyRelative("m_BoolArgument").boolValue = aa.FindPropertyRelative("m_BoolArgument").boolValue;
    }

    // how far its feeding pose hovers over the road (sampled from the clip, lowest vertex vs the ground below)
    static float GroundGap(Transform z)
    {
        var an = z.GetComponent<Animator>();
        if (an == null || an.runtimeAnimatorController == null) return 0f;
        var clip = an.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name.Contains("Feeding"));
        if (clip == null) return 0f;
        var copy = Object.Instantiate(z.gameObject);
        copy.transform.SetPositionAndRotation(z.position, z.rotation);
        float minY = float.MaxValue;
        clip.SampleAnimation(copy, clip.length * 0.5f);
        foreach (var smr in copy.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var m = new Mesh();
            smr.BakeMesh(m, true);
            foreach (var v in m.vertices) minY = Mathf.Min(minY, smr.transform.TransformPoint(v).y);
            Object.DestroyImmediate(m);
        }
        Object.DestroyImmediate(copy);
        bool bf = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        var hit = Physics.RaycastAll(z.position + Vector3.up * 2f, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => !h.collider.transform.IsChildOf(z)).OrderBy(h => h.distance).FirstOrDefault();
        Physics.queriesHitBackfaces = bf;
        if (hit.collider == null || minY == float.MaxValue) return 0f;
        return Mathf.Max(0f, minY - hit.point.y - 0.02f);   // only ever lowered, to just touch the road
    }
}
