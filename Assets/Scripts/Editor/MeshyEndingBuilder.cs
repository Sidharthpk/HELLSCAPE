using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// The not-damned ending's last shot: the Meshy character walks off toward the city (EndingSequence.WalkAway) with the
// HELLSCAPE / TO BE CONTINUED title down the left side. Sets up the looping walk, the walker and the title card.
// HellScape > Finale > Meshy Walk-Away Ending. (MainGameScene must be open.)
public static class MeshyEndingBuilder
{
    const string Dir = "Assets/Enemies/Meshy_AI_Shadowchain_T_Pose_biped/";
    const string Fbx = Dir + "Meshy_AI_Shadowchain_T_Pose_biped_Animation_01a0f8f0-6e5a-75a4-a63c-8b44556809ac_withSkin.fbx";
    const string ControllerPath = Dir + "MeshyWalk.controller";

    [MenuItem("HellScape/Finale/Meshy Walk-Away Ending")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var ending = Object.FindFirstObjectByType<EndingSequence>(FindObjectsInactive.Include);
        if (ending == null || ending.continuedCard == null) return "No EndingSequence / continued card in the open scenes (run To Be Continued Ending first).";

        // the walk has to loop, and stay on the spot (the Hips carry the forward travel; EndingSequence moves him)
        var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.motionNodeName = "Hips";
        var clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
        foreach (var c in clips) { c.loopTime = true; c.lockRootPositionXZ = true; }
        imp.clipAnimations = clips;
        imp.SaveAndReimport();
        var clip = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).OrderByDescending(c => c.length).First();

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var sm = ctrl.layers[0].stateMachine;
        if (sm.states.Length == 0) sm.defaultState = sm.AddState("Walk");
        sm.defaultState.motion = clip;
        EditorUtility.SetDirty(ctrl);

        Transform parent = ending.walkTarget != null ? ending.walkTarget.parent : ending.transform;
        Transform old = parent.Find("Ending - Walker");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var walker = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx), parent);
        walker.name = "Ending - Walker";
        var anim = walker.GetComponent<Animator>();
        if (anim == null) anim = walker.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.avatar = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Avatar>().FirstOrDefault();
        anim.applyRootMotion = false;
        walker.SetActive(false);
        ending.walker = walker;

        // title card: the continued card's fonts, moved to the left, HELLSCAPE big with TO BE CONTINUED under it
        Transform canvas = ending.continuedCard.transform.parent;
        Transform card = canvas.Find("EndCard_Title");
        if (card != null) Object.DestroyImmediate(card.gameObject);
        card = Object.Instantiate(ending.continuedCard, canvas).transform;
        card.name = "EndCard_Title";
        card.SetSiblingIndex(ending.continuedCard.transform.GetSiblingIndex() + 1);
        var texts = card.GetComponentsInChildren<TextMeshProUGUI>(true);
        var title = texts.First(t => t.name == "Line1");
        var sub = texts.First(t => t.name == "Line2");
        title.text = "HELLSCAPE"; sub.text = "TO BE CONTINUED";
        title.enableAutoSizing = false; title.fontSize = 110f;
        sub.enableAutoSizing = false; sub.fontSize = 40f;
        foreach (var t in new[] { title, sub })
        {
            t.alignment = TextAlignmentOptions.Left;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot = new Vector2(0f, 0.5f);
            r.sizeDelta = new Vector2(900f, t == title ? 140f : 60f);
            r.anchoredPosition = new Vector2(90f, t == title ? 30f : -60f);
            EditorUtility.SetDirty(t);
        }
        card.gameObject.SetActive(false);
        ending.titleCard = card.gameObject;

        EditorUtility.SetDirty(ending);
        EditorSceneManager.MarkSceneDirty(ending.gameObject.scene);
        AssetDatabase.SaveAssets();
        return $"Walker '{walker.name}' ({clip.name}, {clip.length:0.0}s loop) and title card wired.";
    }
}
