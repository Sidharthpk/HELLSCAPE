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

        string cardMsg = TitleCard(ending);
        EditorUtility.SetDirty(ending);
        EditorSceneManager.MarkSceneDirty(ending.gameObject.scene);
        AssetDatabase.SaveAssets();
        return $"Walker '{walker.name}' ({clip.name}, {clip.length:0.0}s loop) wired. " + cardMsg;
    }

    [MenuItem("HellScape/Finale/End Title Card")]
    public static void CardMenu()
    {
        var ending = Object.FindFirstObjectByType<EndingSequence>(FindObjectsInactive.Include);
        if (EditorApplication.isPlaying || ending == null || ending.continuedCard == null) { Debug.Log("Stop Play / open MainGameScene first."); return; }
        Debug.Log(TitleCard(ending));
        EditorUtility.SetDirty(ending);
        EditorSceneManager.MarkSceneDirty(ending.gameObject.scene);
    }

    // The end screen: HELLSCAPE on the left in the title screen's own lettering (its red face + shadow, bleeding in),
    // TO BE CONTINUED along the bottom in the pixel font, and an [ENTER] hint (EndingSequence shows it at the very
    // end, on whichever end card is up).
    public static string TitleCard(EndingSequence ending)
    {
        var titleScreen = Object.FindFirstObjectByType<TitleScreen>(FindObjectsInactive.Include);
        var faces = titleScreen != null ? titleScreen.GetComponentsInChildren<TextMeshProUGUI>(true).Where(t => t.text == "HELLSCAPE").ToArray() : new TextMeshProUGUI[0];
        if (faces.Length == 0) return "No HELLSCAPE lettering on the title screen to copy.";

        Transform canvas = ending.continuedCard.transform.parent;
        Transform card = canvas.Find("EndCard_Title");
        if (card != null) Object.DestroyImmediate(card.gameObject);
        card = Object.Instantiate(ending.continuedCard, canvas).transform;
        card.name = "EndCard_Title";
        card.SetSiblingIndex(ending.continuedCard.transform.GetSiblingIndex() + 1);
        if (card.GetComponent<CanvasGroup>() == null) card.gameObject.AddComponent<CanvasGroup>();   // (the lettering bleeds in as this fades up)
        var texts = card.GetComponentsInChildren<TextMeshProUGUI>(true);
        var sub = texts.First(t => t.name == "Line2");
        Object.DestroyImmediate(texts.First(t => t.name == "Line1").gameObject);

        foreach (var face in faces)   // (shadow first, as on the title)
        {
            var t = Object.Instantiate(face, card);
            t.name = "Title " + face.name;
            Vector2 nudge = face.rectTransform.anchoredPosition - faces[faces.Length - 1].rectTransform.anchoredPosition;   // the shadow's offset
            t.enableAutoSizing = false; t.fontSize = 130f;
            Left(t, new Vector2(0f, 0.5f), new Vector2(90f, 20f) + nudge * (130f / face.fontSize), new Vector2(1100f, 220f));
        }

        sub.text = "TO BE CONTINUED";
        sub.enableAutoSizing = false; sub.fontSize = 40f; sub.characterSpacing = 12f;
        Left(sub, Vector2.zero, new Vector2(90f, 24f), new Vector2(900f, 60f));   // (under the dialogue box, which is still talking)

        foreach (var c in new[] { card.gameObject, ending.continuedCard, ending.demon.endCard })
        {
            if (c == null) continue;
            Transform old = c.transform.Find("MenuHint");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var hint = Object.Instantiate(sub, c.transform);
            hint.name = "MenuHint";
            hint.text = "[ENTER]  MENU";
            hint.fontSize = 30f; hint.characterSpacing = 0f;
            hint.color = new Color(0.6f, 0.55f, 0.52f, 0.7f);
            Left(hint, new Vector2(1f, 0f), new Vector2(-90f, 24f), new Vector2(500f, 60f));
            hint.alignment = TextAlignmentOptions.Right;
            hint.gameObject.SetActive(false);
            EditorUtility.SetDirty(c);
        }
        card.gameObject.SetActive(false);
        ending.titleCard = card.gameObject;
        return "End title card: HELLSCAPE in the title lettering on the left, TO BE CONTINUED along the bottom, [ENTER] hint on every end card.";
    }

    static void Left(TextMeshProUGUI t, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        t.alignment = TextAlignmentOptions.Left;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = anchor;
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        EditorUtility.SetDirty(t);
    }
}
