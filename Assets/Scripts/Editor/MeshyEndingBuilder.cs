using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// The not-damned ending's last scene (EndingSequence.WalkAway): the confessor (the user's Meshy survivor, Assets/New
// Folder/Confessor_*) stands looking at the marks on his hands, then limps off toward the city with the HELLSCAPE /
// TO BE CONTINUED title down the left side. The two animations came as two FBXs of the same man whose armatures are
// set up differently (one in centimetres), so each clip plays on its own copy and the scene cuts between them.
// HellScape > Finale > Meshy Walk-Away Ending. (MainGameScene must be open.)
public static class MeshyEndingBuilder
{
    const string CursedDir = "Assets/Models/Confessor_Cursed/Meshy_AI_rigged_survivor_biped/";
    const string WalkDir = "Assets/Models/Confessor_Walk/Meshy_AI_rigged_survivor_biped/";
    const string CursedFbx = CursedDir + "Meshy_AI_rigged_survivor_biped_Animation_01a1215f-d21e-7463-8c18-e22d7848dcd5_withSkin.fbx";
    const string WalkFbx = WalkDir + "Meshy_AI_rigged_survivor_biped_Animation_Injured_Walk_withSkin.fbx";

    [MenuItem("HellScape/Finale/Meshy Walk-Away Ending")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var ending = Object.FindFirstObjectByType<EndingSequence>(FindObjectsInactive.Include);
        if (ending == null || ending.continuedCard == null) return "No EndingSequence / continued card in the open scenes (run To Be Continued Ending first).";

        // the FBXs come without their texture on the material
        string matPath = WalkDir + "Confessor.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(WalkDir + "Meshy_AI_rigged_survivor_biped_texture_0.png");
        mat.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(mat);

        Transform parent = ending.walkTarget != null ? ending.walkTarget.parent : ending.transform;
        ending.cursed = Man(parent, "Ending - Confessor (his hands)", CursedFbx, CursedDir + "ConfessorCursed.controller", false, mat);
        ending.walker = Man(parent, "Ending - Walker", WalkFbx, WalkDir + "ConfessorWalk.controller", true, mat);
        ending.walkerSpeed = 0.33f;   // measured: his planted foot slides back 2.88 m in the 8.67 s clip

        string cardMsg = TitleCard(ending);
        EditorUtility.SetDirty(ending);
        EditorSceneManager.MarkSceneDirty(ending.gameObject.scene);
        AssetDatabase.SaveAssets();
        return "Confessor wired: '" + ending.cursed.name + "' then '" + ending.walker.name + "'. " + cardMsg;
    }

    // one copy of the man playing one clip (the longest in the file), switched off until the ending
    static GameObject Man(Transform parent, string name, string fbx, string controllerPath, bool loop, Material mat)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        var clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
        foreach (var c in clips) c.loopTime = loop;
        imp.clipAnimations = clips;
        imp.SaveAndReimport();
        var clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).OrderByDescending(c => c.length).First();

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var sm = ctrl.layers[0].stateMachine;
        if (sm.states.Length == 0) sm.defaultState = sm.AddState("Play");
        sm.defaultState.motion = clip;
        EditorUtility.SetDirty(ctrl);

        Transform old = parent.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var man = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx), parent);
        man.name = name;
        var anim = man.GetComponent<Animator>();
        if (anim == null) anim = man.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
        anim.applyRootMotion = false;
        foreach (var r in man.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
        man.SetActive(false);
        return man;
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
