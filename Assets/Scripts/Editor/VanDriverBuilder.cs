using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the Animator for the Ch01 "Driving" body in the Prologue van around the pose it was given by hand (nothing in the scene
// is moved). A blend tree on Steer holds five poses - Left, Small Left, Straight, Small Right, Right - that only turn the neck
// and head into the turn (Assets/Generated/Driver). The hands follow the wheel through VanDriver (IK) at runtime.
public static class VanDriverBuilder
{
    const string Dir = "Assets/Generated/Driver";

    static readonly string[] Bones = { "Neck", "Head" };
    static readonly float[] Yaw = { 6f, 12f };   // degrees into the turn at full lock

    [MenuItem("HellScape/Cars/Seat The Van Driver")]
    public static void Seat()
    {
        var body = GameObject.Find("Ch01_nonPBR@Driving");
        var cockpit = Object.FindFirstObjectByType<CockpitView>(FindObjectsInactive.Include);
        if (body == null || cockpit == null || cockpit.wheel == null) { Debug.LogError("Seat The Van Driver: open the Prologue (needs Ch01_nonPBR@Driving and the van's CockpitView)."); return; }
        var van = cockpit.car.transform;
        var t = body.transform;
        Undo.RegisterFullObjectHierarchyUndo(body, "Seat The Van Driver");
        if (t.parent != van) Undo.SetTransformParent(t, van, "Seat The Van Driver");

        var anim = body.GetComponent<Animator>();
        if (anim == null) anim = Undo.AddComponent<Animator>(body);
        anim.runtimeAnimatorController = BuildController(t);
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var drv = body.GetComponent<VanDriver>();
        if (drv == null) drv = Undo.AddComponent<VanDriver>(body);
        drv.car = cockpit.car; drv.wheel = cockpit.wheel;

        EditorSceneManager.MarkSceneDirty(body.scene);
        Debug.Log("Seat The Van Driver: animator rebuilt around the posed body");
    }

    // The VW badge on the steering wheel hub is part of Circle.005 (black backing) and Circle.006 (chrome) - one mesh each with every
    // badge on the van - so it stood still while the wheel turned. This cuts the triangles sitting on the steering column (within a
    // few cm of its axis, in front of the wheel) out into their own pieces parented to volant.002, and gives the two badge objects
    // copies without them (Assets/Generated/VW).
    [MenuItem("HellScape/Cars/Spin The Wheel Badge")]
    public static void SpinBadge()
    {
        var cockpit = Object.FindFirstObjectByType<CockpitView>(FindObjectsInactive.Include);
        if (cockpit == null || cockpit.wheel == null) { Debug.LogError("Spin The Wheel Badge: open the Prologue (needs the van's CockpitView)."); return; }
        var wheel = cockpit.wheel;
        if (wheel.Find("Steering badge Circle.006") != null) { Debug.Log("Spin The Wheel Badge: already done"); return; }
        var van = cockpit.car.transform;
        var c = wheel.TransformPoint(wheel.GetComponent<MeshFilter>().sharedMesh.bounds.center);
        var axis = wheel.forward; if (Vector3.Dot(axis, van.forward) > 0f) axis = -axis;   // (toward the driver)
        int moved = 0;
        foreach (var n in new[] { "Circle.005", "Circle.006" })
        {
            var mf = wheel.parent.Find(n)?.GetComponent<MeshFilter>();
            if (mf == null) continue;
            var src = mf.sharedMesh;
            var keep = new List<int>(); var badge = new List<int>();
            using (var data = Mesh.AcquireReadOnlyMeshData(src))
            {
                var d = data[0];
                var v = new NativeArray<Vector3>(d.vertexCount, Allocator.Temp); d.GetVertices(v);
                for (int s = 0; s < d.subMeshCount; s++)
                {
                    var idx = new NativeArray<int>(d.GetSubMesh(s).indexCount, Allocator.Temp); d.GetIndices(idx, s);
                    for (int i = 0; i < idx.Length; i += 3)
                    {
                        bool onHub = true;
                        for (int k = 0; k < 3; k++)
                        {
                            var off = mf.transform.TransformPoint(v[idx[i + k]]) - c;
                            float along = Vector3.Dot(off, axis);
                            if (along < 0.05f || along > 0.3f || (off - axis * along).magnitude > 0.05f) { onHub = false; break; }
                        }
                        (onHub ? badge : keep).AddRange(new[] { idx[i], idx[i + 1], idx[i + 2] });
                    }
                    idx.Dispose();
                }
                v.Dispose();
                if (badge.Count == 0) continue;

                var rest = Copy(src, d, keep, src.name + " (no wheel badge)");
                var hub = Copy(src, d, badge, src.name + " (wheel badge)");
                Save(rest); Save(hub);
                Undo.RecordObject(mf, "Spin The Wheel Badge");
                mf.sharedMesh = rest;
                var go = new GameObject("Steering badge " + n);
                Undo.RegisterCreatedObjectUndo(go, "Spin The Wheel Badge");
                go.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
                go.transform.localScale = mf.transform.lossyScale;
                go.transform.SetParent(wheel, true);   // (turns with the wheel)
                go.AddComponent<MeshFilter>().sharedMesh = hub;
                go.AddComponent<MeshRenderer>().sharedMaterials = mf.GetComponent<MeshRenderer>().sharedMaterials;
                moved += badge.Count / 3;
            }
        }
        EditorSceneManager.MarkSceneDirty(wheel.gameObject.scene);
        Debug.Log($"Spin The Wheel Badge: {moved} triangles now turn with the wheel");
    }

    // Muffled rain and engine in the cab (VanCabinAudio on the van). The rain shared the PrologueDirector object with the heartbeat
    // and stings, and a low-pass filter muffles every source on its object, so the rain moves to its own child, "Street ambience".
    [MenuItem("HellScape/Cars/Van Cab Audio")]
    public static void CabAudio()
    {
        var van = Object.FindFirstObjectByType<PrologueVan>(FindObjectsInactive.Include);
        var d = Object.FindFirstObjectByType<PrologueDirector>(FindObjectsInactive.Include);
        if (van == null || d == null || d.ambience == null) { Debug.LogError("Van Cab Audio: open the Prologue (needs PrologueVan and PrologueDirector.ambience)."); return; }
        if (d.ambience.GetComponents<AudioSource>().Length > 1)
        {
            var go = new GameObject("Street ambience");
            Undo.RegisterCreatedObjectUndo(go, "Van Cab Audio");
            go.transform.SetParent(d.transform, false);
            var src = go.AddComponent<AudioSource>();
            EditorUtility.CopySerialized(d.ambience, src);
            var old = d.ambience;
            Undo.RecordObject(d, "Van Cab Audio");
            d.ambience = src;
            Undo.DestroyObjectImmediate(old);
        }
        var cab = van.GetComponent<VanCabinAudio>();
        if (cab == null) cab = Undo.AddComponent<VanCabinAudio>(van.gameObject);
        cab.van = van; cab.radio = van.GetComponent<VanRadio>(); cab.rain = d.ambience;
        EditorSceneManager.MarkSceneDirty(van.gameObject.scene);
        Debug.Log("Van Cab Audio: rain and engine muffle in the cab and duck under the radio");
    }

    // a copy of src (all its vertex data) drawing only these triangles
    static Mesh Copy(Mesh src, Mesh.MeshData d, List<int> tris, string name)
    {
        var arr = Mesh.AllocateWritableMeshData(1);
        var o = arr[0];
        o.SetVertexBufferParams(d.vertexCount, src.GetVertexAttributes());
        for (int s = 0; s < d.vertexBufferCount; s++) o.GetVertexData<byte>(s).CopyFrom(d.GetVertexData<byte>(s));
        o.SetIndexBufferParams(tris.Count, IndexFormat.UInt32);
        var ix = o.GetIndexData<uint>();
        for (int i = 0; i < tris.Count; i++) ix[i] = (uint)tris[i];
        o.subMeshCount = 1;
        o.SetSubMesh(0, new SubMeshDescriptor(0, tris.Count));
        var m = new Mesh { name = name };
        Mesh.ApplyAndDisposeWritableMeshData(arr, m);
        m.RecalculateBounds();
        return m;
    }

    static void Save(Mesh m)
    {
        string path = "Assets/Generated/VW/" + m.name + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
    }

    static AnimatorController BuildController(Transform root)
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Generated", "Driver");
        var poses = new (string name, float steer)[] { ("Left", -1f), ("Small Left", -0.35f), ("Straight", 0f), ("Small Right", 0.35f), ("Right", 1f) };
        string path = Dir + "/VanDriver.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Steer", AnimatorControllerParameterType.Float);
        var tree = new BlendTree { name = "Steering", blendParameter = "Steer", blendType = BlendTreeType.Simple1D, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(tree, ctrl);
        foreach (var p in poses)
        {
            var clip = Pose(root, p.name, p.steer);
            AssetDatabase.AddObjectToAsset(clip, ctrl);
            tree.AddChild(clip, p.steer);
        }
        var state = ctrl.layers[0].stateMachine.AddState("Driving");
        state.motion = tree;
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    // one held pose, baked from the scene pose with the neck and head turned for this much steering (-1 left .. +1 right)
    static AnimationClip Pose(Transform root, string name, float steer)
    {
        var van = root.parent;
        var saved = new Quaternion[Bones.Length];
        for (int i = 0; i < Bones.Length; i++) saved[i] = Bone(root, Bones[i]).localRotation;
        for (int i = 0; i < Bones.Length; i++)
        {
            var bn = Bone(root, Bones[i]);
            bn.rotation = Quaternion.AngleAxis(Yaw[i] * steer, van.up) * bn.rotation;
        }
        var clip = new AnimationClip { name = name };
        foreach (var n in Bones)
        {
            var bn = Bone(root, n);
            string p = AnimationUtility.CalculateTransformPath(bn, root);
            var lr = bn.localRotation;
            Curve(clip, p, "localRotation.x", lr.x); Curve(clip, p, "localRotation.y", lr.y);
            Curve(clip, p, "localRotation.z", lr.z); Curve(clip, p, "localRotation.w", lr.w);
        }
        for (int i = 0; i < Bones.Length; i++) Bone(root, Bones[i]).localRotation = saved[i];
        var s = AnimationUtility.GetAnimationClipSettings(clip); s.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, s);
        return clip;
    }

    static void Curve(AnimationClip clip, string path, string prop, float v) =>
        clip.SetCurve(path, typeof(Transform), prop, AnimationCurve.Constant(0f, 1f, v));

    static Transform Bone(Transform root, string n)
    {
        foreach (var x in root.GetComponentsInChildren<Transform>(true)) if (x.name.EndsWith(":" + n)) return x;
        return null;
    }
}
