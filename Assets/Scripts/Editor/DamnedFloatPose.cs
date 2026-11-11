using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class DamnedFloatPose
{
    const string Source = "Assets/Models/Floating Pose 2.fbx";
    const string Copy = "Assets/Enemies/Dammed/Floating Pose 2.fbx";
    const string Controller = "Assets/Resources/OfficeHorror/DamnedFloat.controller";

    [MenuItem("HellScape/Build Damned Float Pose")]
    public static void Build()
    {
        if (AssetDatabase.LoadMainAssetAtPath(Copy) == null) AssetDatabase.CopyAsset(Source, Copy);
        var imp = (ModelImporter)AssetImporter.GetAtPath(Copy);
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.motionNodeName = "";
        var clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.loopTime = true; c.loopPose = true;
            c.lockRootRotation = true; c.keepOriginalOrientation = true;
            c.lockRootHeightY = true; c.lockRootPositionXZ = true;
        }
        imp.clipAnimations = clips;
        imp.SaveAndReimport();
        var pose = AssetDatabase.LoadAllAssetsAtPath(Copy).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        if (ac == null) ac = AnimatorController.CreateAnimatorControllerAtPath(Controller);
        var sm = ac.layers[0].stateMachine;
        foreach (var st in sm.states.ToArray()) sm.RemoveState(st.state);
        var state = sm.AddState("Float", new Vector3(300, 0));
        state.motion = pose; state.iKOnFeet = false;
        sm.defaultState = state;
        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        Debug.Log("Dammed floating pose rebuilt for the humanoid demon rig.");
    }
}
