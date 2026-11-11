using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > People: Clinic man is the Doctor lab model (Prologue open).
// Perishables/doctor-lab/Doctor.fbx is static meshes with no skeleton. This skins its body + legs onto the skeleton of the
// pack's Character_25_Doctor (each vertex follows its two nearest bone segments), saves that as DoctorLabNpc.prefab, and puts
// it in place of the clinic's man: he keeps Civilian.controller, so he walks with the NPCs' own clips.
public static class DoctorLab
{
    const string Fbx = "Assets/Perishables/doctor-lab/source/Doctor.fbx";
    const string OutDir = "Assets/Generated/DoctorLab";
    const string PrefabPath = OutDir + "/DoctorLabNpc.prefab";
    static readonly string[] Segs =
    {
        "Hips>Spine", "Spine>Spine1", "Spine1>Spine2", "Spine2>Neck", "Neck>Head", "Head>HeadTop_End",
        "LeftShoulder>LeftArm", "LeftArm>LeftForeArm", "LeftForeArm>LeftHand", "LeftHand>LeftHandIndex1",
        "RightShoulder>RightArm", "RightArm>RightForeArm", "RightForeArm>RightHand", "RightHand>RightHandIndex1",
        "LeftUpLeg>LeftLeg", "LeftLeg>LeftFoot", "LeftFoot>LeftToeBase", "LeftToeBase>LeftToe_End",
        "RightUpLeg>RightLeg", "RightLeg>RightFoot", "RightFoot>RightToeBase", "RightToeBase>RightToe_End",
    };

    // The lab coat's skirt was glued to the thighs: as he walked the legs tore it at the waist and the trousers poked through.
    // Below the hips it now hangs from the Hips bone and swings (up to legSwing, reached a third of the way to the knee) with the
    // thigh on its side; the trousers' thighs are tucked in a little under it (TrouserTuck) so the forward leg can't poke through.
    const float legSwing = .85f, tuck = .82f;
    static void CoatHem(Mesh m, string[] bones)
    {
        int Bone(string n) => System.Array.FindIndex(bones, b => b.EndsWith(n));
        var bind = m.bindposes;
        float Y(int i) => bind[i].inverse.GetColumn(3).y;
        int hips = Bone("Hips"), upL = Bone("LeftUpLeg"), upR = Bone("RightUpLeg");
        float hipY = Y(hips), kneeY = (Y(Bone("LeftLeg")) + Y(Bone("RightLeg"))) * .5f;
        float sideL = bind[upL].inverse.GetColumn(3).x, sideR = bind[upR].inverse.GetColumn(3).x;
        var v = m.vertices; var w = m.boneWeights;
        for (int i = 0; i < v.Length; i++)
        {
            if (v[i].y >= hipY) continue;
            float t = legSwing * Mathf.SmoothStep(0f, 1f, (hipY - v[i].y) / ((hipY - kneeY) * .35f));
            int leg = Mathf.Abs(v[i].x - sideL) < Mathf.Abs(v[i].x - sideR) ? upL : upR;
            w[i] = new BoneWeight { boneIndex0 = hips, weight0 = 1f - t, boneIndex1 = leg, weight1 = t };
        }
        m.boneWeights = w;
    }

    // pulls the trouser thighs (hip to knee) in toward the thigh bone
    static void TrouserTuck(Mesh m, string[] bones)
    {
        var bind = m.bindposes;
        Vector3 P(string n) => bind[System.Array.FindIndex(bones, b => b.EndsWith(n))].inverse.GetColumn(3);
        var legs = new[] { (P("LeftUpLeg"), P("LeftLeg")), (P("RightUpLeg"), P("RightLeg")) };
        var v = m.vertices;
        for (int i = 0; i < v.Length; i++)
        {
            var (hip, knee) = Mathf.Abs(v[i].x - legs[0].Item1.x) < Mathf.Abs(v[i].x - legs[1].Item1.x) ? legs[0] : legs[1];
            if (v[i].y > hip.y || v[i].y < knee.y) continue;
            float s = Vector3.Dot(v[i] - hip, knee - hip) / (knee - hip).sqrMagnitude;
            Vector3 axis = hip + (knee - hip) * s;
            float k = Mathf.Lerp(tuck, 1f, Mathf.SmoothStep(0f, 1f, (s - .7f) / .3f));   // full tuck down the thigh, back to normal at the knee
            v[i] = axis + (v[i] - axis) * k;
        }
        m.vertices = v;
        m.RecalculateBounds();
    }

    [MenuItem("HellScape/People: Doctor coat hangs from the hips")]
    public static void FixCoat()
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var smr = pf.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "body");
        CoatHem(smr.sharedMesh, smr.bones.Select(b => b.name).ToArray());
        EditorUtility.SetDirty(smr.sharedMesh);
        var legs = pf.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "legs");
        var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(legs.sharedMesh));
        if (imp.userData != "tucked")   // (the tuck moves vertices: once, or it shrinks again every run)
        {
            TrouserTuck(legs.sharedMesh, legs.bones.Select(b => b.name).ToArray());
            EditorUtility.SetDirty(legs.sharedMesh);
            AssetDatabase.SaveAssets();   // (before the reimport, or it reloads the untucked mesh)
            imp.userData = "tucked"; imp.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Doctor coat re-weighted.");
    }

    [MenuItem("HellScape/People: Clinic man is the Doctor lab model")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var pro = SceneManager.GetSceneByName("Prologue");
        if (!pro.isLoaded) return "Open the Prologue first.";
        string psx = AssetDatabase.FindAssets("Character_25_Doctor t:Model").Select(AssetDatabase.GUIDToAssetPath).First(p => p.EndsWith("/Character_25_Doctor.fbx"));
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/Generated", "DoctorLab");

        var g = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(psx));
        g.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        PrefabUtility.UnpackPrefabInstance(g, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        g.name = "DoctorLabNpc";
        var old = g.GetComponentInChildren<SkinnedMeshRenderer>(true);
        var bounds = old.bounds;
        var bt = g.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("mixamorig:")).ToDictionary(t => t.name.Substring(10));

        // doctor (stands on the origin, faces +z, arms along x) -> the pack skeleton (arms along the bones' own left/right)
        Vector3 up = Vector3.up, right = (bt["RightArm"].position - bt["LeftArm"].position).normalized;
        right = Vector3.ProjectOnPlane(right, up).normalized;
        var rot = Quaternion.LookRotation(Vector3.Cross(right, up), up);   // doctor +z -> forward = right x up
        var meshes = src.GetComponentsInChildren<Renderer>(true).Select(r => (name: r.name, mesh: r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh, mats: r.sharedMaterials)).Where(m => m.mesh != null && m.mesh.vertexCount > 0).ToArray();
        float top = meshes.Max(m => m.mesh.bounds.max.y);
        float scale = bounds.size.y / top;
        var origin = new Vector3(bt["Hips"].position.x, bounds.min.y, bt["Hips"].position.z);

        var segBones = new List<Transform>(); var segA = new List<Vector3>(); var segB = new List<Vector3>();
        foreach (var s in Segs) { var ab = s.Split('>'); segBones.Add(bt[ab[0]]); segA.Add(bt[ab[0]].position); segB.Add(bt[ab[1]].position); }
        var boneList = segBones.Distinct().ToArray();

        foreach (var mf in meshes)
        {
            var m = Object.Instantiate(mf.mesh); m.name = mf.name + " (skinned)";
            var v = m.vertices; var n = m.normals; var w = new BoneWeight[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var p = origin + rot * (v[i] * scale);
                v[i] = p; if (n.Length == v.Length) n[i] = rot * n[i];
                int b1 = -1, b2 = -1; float d1 = float.MaxValue, d2 = float.MaxValue;
                for (int s = 0; s < segBones.Count; s++)
                {
                    var ab = segB[s] - segA[s];
                    float d = Vector3.Distance(p, segA[s] + ab * Mathf.Clamp01(Vector3.Dot(p - segA[s], ab) / ab.sqrMagnitude));
                    if (d < d1) { b2 = b1; d2 = d1; b1 = s; d1 = d; } else if (d < d2 && segBones[s] != segBones[b1]) { b2 = s; d2 = d; }
                }
                if (segBones[b1] == (b2 >= 0 ? segBones[b2] : null)) b2 = -1;
                float w1 = 1f / (d1 * d1 + 1e-4f), w2 = b2 >= 0 ? 1f / (d2 * d2 + 1e-4f) : 0f;
                w[i] = new BoneWeight { boneIndex0 = System.Array.IndexOf(boneList, segBones[b1]), weight0 = w1 / (w1 + w2), boneIndex1 = b2 >= 0 ? System.Array.IndexOf(boneList, segBones[b2]) : 0, weight1 = w2 / (w1 + w2) };
            }
            m.vertices = v; if (n.Length == v.Length) m.normals = n;
            m.boneWeights = w;
            m.bindposes = boneList.Select(b => b.worldToLocalMatrix).ToArray();
            if (mf.name == "body") CoatHem(m, boneList.Select(b => b.name).ToArray());
            if (mf.name == "legs") TrouserTuck(m, boneList.Select(b => b.name).ToArray());
            m.RecalculateBounds();
            if (m.uv.Length == v.Length && n.Length == v.Length) m.RecalculateTangents();
            AssetDatabase.CreateAsset(m, OutDir + "/" + mf.name + ".asset");
            if (mf.name == "legs") { var imp = AssetImporter.GetAtPath(OutDir + "/legs.asset"); imp.userData = "tucked"; imp.SaveAndReimport(); }

            var go = new GameObject(mf.name); go.transform.SetParent(g.transform, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = m; smr.bones = boneList; smr.rootBone = bt["Hips"];
            smr.sharedMaterials = mf.mats;
            smr.updateWhenOffscreen = true;
        }
        Object.DestroyImmediate(old.gameObject);
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(g, PrefabPath, InteractionMode.AutomatedAction);
        Object.DestroyImmediate(g);

        // ---- the clinic's man
        var proRoot = pro.GetRootGameObjects().First(x => x.name == "Prologue").transform;
        var man = proRoot.Find("Deliveries").Cast<Transform>().First(t => t.name.StartsWith("Man at the door"));
        var map = new Dictionary<Object, Object>();
        PeopleSwap.Swap(man, PrefabPath, null, map, 0f);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(psx).OfType<Avatar>().First();
        var anim = (Animator)map.Values.OfType<Animator>().First();
        anim.avatar = avatar;
        PeopleSwap.Remap(pro, map);
        PeopleSwap.Bury();
        EditorSceneManager.MarkSceneDirty(pro);
        EditorSceneManager.SaveScene(pro);
        AssetDatabase.SaveAssets();
        return $"Clinic man is the Doctor lab model (scale {scale:0.00}, {meshes.Length} meshes skinned).";
    }
}
