using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > Prologue > Build Mercer Walk: Mercer in the user's casual-walk clip (Assets/Perishables/Avatar/Mercer_Walk), for
// the walk from the office to the van (OfficeFlashback.WalkWithMercer). A copy of the Meshy Mercer with its own one-state
// controller, switched off, wired to OfficeOpeningSequence.mercerWalk.
public static class MercerWalkBuilder
{
    const string Walk = "Assets/Perishables/Avatar/Mercer_Walk/Meshy_AI_Mercer_PS2_T_Pose_35K_biped_Animation_Casual_Walk_withSkin.fbx";

    [MenuItem("HellScape/Prologue/Build Mercer Walk")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
        var sc = SceneManager.GetSceneByName("Prologue");
        if (!sc.isLoaded) sc = EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity", OpenSceneMode.Additive);
        var op = sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<OfficeOpeningSequence>(true)).FirstOrDefault();
        if (op == null) { Debug.LogError("Mercer walk: no OfficeOpeningSequence in Prologue"); return; }
        var old = GameObject.Find("Mercer (walks with you)");
        if (old != null) Object.DestroyImmediate(old);
        var oldInactive = sc.GetRootGameObjects().FirstOrDefault(g => g.name == "Mercer (walks with you)");
        if (oldInactive != null) Object.DestroyImmediate(oldInactive);

        MeshyCastBuilder.Loop(Walk, true);
        var clip = MeshyCastBuilder.Clip(Walk);
        var ctrl = MeshyCastBuilder.Controller("MercerCasualWalk", ("Walk", clip));
        var man = MeshyCastBuilder.Man(Walk, "Mercer (walks with you)", null, MeshyCastBuilder.MercerHeight,
            MeshyCastBuilder.Mat("Mercer", MeshyCastBuilder.MercerDir + "Meshy_AI_Mercer_PS2_T_Pose_35K_biped_texture_0.png"), ctrl);
        SceneManager.MoveGameObjectToScene(man, sc);
        man.SetActive(false);
        op.mercerWalk = man.GetComponent<Animator>();
        EditorUtility.SetDirty(op);
        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        Debug.Log("Mercer walk built (clip " + clip.name + ", " + clip.length.ToString("0.0") + " s), Prologue saved");
    }
}
