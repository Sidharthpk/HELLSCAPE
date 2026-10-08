using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// HellScape > Build Damned Float Pose: the zombie pack's rig is Generic, so the (Humanoid) Floating Pose 2 can't play on it.
// A Generic import of the same fbx, its curve paths renamed mixamorig: -> mixamorig5:, in a one-state controller the
// fusion cutscene loads from Resources.
static class DamnedFloatPose
{
    const string Src = "Assets/New Folder/Floating Pose 2.fbx", Copy = "Assets/New Folder/Floating Pose 2 (Generic).fbx";
    const string Crawl = "Assets/Enemies/Scary Zombie Pack/zombie crawl.fbx";
    const string Out = "Assets/Resources/OfficeHorror/DamnedFloat";

    [MenuItem("HellScape/Build Damned Float Pose")]
    static void Build()
    {
        if (!File.Exists(Copy)) AssetDatabase.CopyAsset(Src, Copy);
        var imp = (ModelImporter)AssetImporter.GetAtPath(Copy);
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.SaveAndReimport();

        AnimationClip First(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
        var pose = First(Copy);
        // whatever sits above the hips in the zombie clips' paths (an armature root, or nothing)
        string hipPath = AnimationUtility.GetCurveBindings(First(Crawl)).Select(b => b.path).First(p => p.Contains("mixamorig5:Hips"));
        string prefix = hipPath.Substring(0, hipPath.IndexOf("mixamorig5:Hips"));

        var clip = new AnimationClip { name = "DamnedFloat", frameRate = pose.frameRate };
        foreach (var b in AnimationUtility.GetCurveBindings(pose))
        {
            string p = b.path.Replace("mixamorig:", "mixamorig5:");
            int h = p.IndexOf("mixamorig5:Hips");
            if (h < 0) continue;
            var nb = b; nb.path = prefix + p.Substring(h);
            AnimationUtility.SetEditorCurve(clip, nb, AnimationUtility.GetEditorCurve(pose, b));
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, settings);

        Directory.CreateDirectory(Path.GetDirectoryName(Out));
        AssetDatabase.DeleteAsset(Out + ".controller");
        var ac = AnimatorController.CreateAnimatorControllerAtPath(Out + ".controller");
        AssetDatabase.AddObjectToAsset(clip, ac);
        ac.layers[0].stateMachine.AddState("Float").motion = clip;
        EditorUtility.SetDirty(ac); AssetDatabase.SaveAssets();
        Debug.Log("Damned float pose built: " + clip.length + "s, " + AnimationUtility.GetCurveBindings(clip).Length + " curves, prefix='" + prefix + "'");
    }
}
