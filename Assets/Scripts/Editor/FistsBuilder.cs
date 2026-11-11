using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// The first-person arms: the PSX arms out, Fists 2025 (1Matzh) in. Builds FistsController from the pack's clips and
// puts the model under the player's camera in place of the old one, on the same arms_rig object (PunchController, and
// everything that points at it, stays as it was). Run with MainGameScene open; safe to run again.
public static class FistsBuilder
{
    const string Dir = "Assets/Fists 2025 [First Person Animations] by 1Matzh/";
    const string Model = Dir + "fists_anim.fbx";
    const string Tex = Dir + "textures/arms/FBX/";
    const string ControllerPath = Dir + "FistsController.controller";
    const string MarkedSkin = "Assets/Generated/Ending/arms_marked.png";   // Tools/ending/make_marked_fists.py
    static readonly string[] Loops = { "Idle", "Walk", "Sprint_Type_1", "Sprint_Type_2", "Block_Loop", "Take_Loop" };
    public const float WalkSpeed = 3.2f, SprintSpeed = 7f;   // the player's FirstPersonController speeds: the blend tree's thresholds
    // the pack's framing is all hands: smaller arms, further out and a touch lower, so the forearms show (user, 2026-10-18)
    public const float ArmScale = 0.8f;
    public static readonly Vector3 ArmOffset = new Vector3(0f, -0.04f, 0.16f);

    [MenuItem("HellScape/Fists 2025: Build Arms")]
    public static void Build()
    {
        Import();
        var ctl = Controller();
        Swap(ctl);
    }

    static void Import()
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(Model);
        var clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
        foreach (var c in clips) c.loopTime = Loops.Any(l => c.name == "rig|" + l);
        imp.clipAnimations = clips;
        imp.isReadable = true;   // PalmLight copies the right hand's triangles for the red glow
        imp.SaveAndReimport();

        Texture(Tex + "body_C.tga", 1024, false);
        Texture(Tex + "body_n_normals.TGA", 1024, true);
        Texture(MarkedSkin, 2048, false);   // the skin you wear all game: the sign on the palm wants the detail
    }

    static void Texture(string path, int size, bool normal)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) return;
        if (normal) ti.textureType = TextureImporterType.NormalMap;
        ti.maxTextureSize = size;
        ti.crunchedCompression = true;
        ti.SaveAndReimport();
    }

    static AnimationClip Clip(string name) =>
        AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().First(c => c.name == "rig|" + name);

    static AnimatorController Controller()
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        var ctl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        foreach (var p in new[] { "JabL", "JabR", "Draw" }) ctl.AddParameter(p, AnimatorControllerParameterType.Trigger);
        ctl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var sm = ctl.layers[0].stateMachine;

        var move = ctl.CreateBlendTreeInController("Locomotion", out var tree);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(Clip("Idle"), 0f);
        tree.AddChild(Clip("Walk"), WalkSpeed);
        tree.AddChild(Clip("Sprint_Type_1"), SprintSpeed);
        sm.defaultState = move;

        AnimatorState State(string name, string clip) { var s = sm.AddState(name); s.motion = Clip(clip); return s; }
        void Then(AnimatorState from, AnimatorState to, float exit = 0.9f, float fade = 0.15f)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true; t.exitTime = exit; t.hasFixedDuration = true; t.duration = fade;
        }
        void Any(AnimatorState to, string trigger)
        {
            var t = sm.AddAnyStateTransition(to);
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            t.canTransitionToSelf = false; t.hasFixedDuration = true; t.duration = 0.08f;
        }

        var draw = State("GuardDraw", "Equip");   // (the old state's name: SleepSequence, PalmLight and the ending play it)
        Then(draw, move);
        Any(draw, "Draw");

        var jabL = State("Punch_L", "Punch_L");
        var jabR = State("Punch_R", "Punch_R");
        Then(jabL, move, 0.8f, 0.2f); Then(jabR, move, 0.8f, 0.2f);
        Any(jabL, "JabL"); Any(jabR, "JabR");

        Then(State("Fidget", "Idle_Fidget"), move);
        var take = State("Take", "Take_Start");
        var put = State("TakeStop", "Take_Stop");
        Then(take, put, 0.95f, 0.1f); Then(put, move);
        Then(State("Inspect", "Inspect"), move, 0.95f, 0.3f);   // the mark on your palms (EndingSequence)
        // the palm light (PalmLight): the Take clips on their own states, so F-use keeps its one-shot Take
        var light = State("Light", "Take_Start"); var lightLoop = State("LightLoop", "Take_Loop"); var lightStop = State("LightStop", "Take_Stop");
        Then(light, lightLoop, 0.9f, 0.1f); Then(lightStop, move);
        AddBlock(ctl);

        AssetDatabase.SaveAssets();
        return ctl;
    }

    // the guard while you crouch (PunchController): Block_Start into the held Block_Loop, Block_Stop back to locomotion.
    // Added in place on the existing controller too (a rebuild would give it a new GUID and break the scenes' references).
    [MenuItem("HellScape/Fists 2025: Add Block States")]
    public static void AddBlockToExisting()
    {
        var ctl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctl == null) { Debug.LogError("Fists 2025: no FistsController"); return; }
        AddBlock(ctl);
        AssetDatabase.SaveAssets();
    }

    static void AddBlock(AnimatorController ctl)
    {
        var sm = ctl.layers[0].stateMachine;
        if (sm.states.Any(s => s.state.name == "Block")) return;
        var move = sm.states.First(s => s.state.name == "Locomotion").state;
        AnimatorState State(string name, string clip) { var s = sm.AddState(name); s.motion = Clip(clip); return s; }
        var start = State("Block", "Block_Start"); var loop = State("BlockLoop", "Block_Loop"); var stop = State("BlockStop", "Block_Stop");
        foreach (var (from, to, exit) in new[] { (start, loop, 0.9f), (stop, move, 0.9f) })
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true; t.exitTime = exit; t.hasFixedDuration = true; t.duration = 0.1f;
        }
        EditorUtility.SetDirty(ctl);
    }

    static void Swap(AnimatorController ctl)
    {
        var punch = Object.FindObjectsByType<PunchController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (punch == null) { Debug.LogError("Fists 2025: no PunchController in the open scene (open MainGameScene)"); return; }
        var rig = punch.transform;
        var old = rig.GetComponentInChildren<SkinnedMeshRenderer>(true);
        var shadows = old != null ? old.shadowCastingMode : ShadowCastingMode.Off;
        int layer = old != null ? old.gameObject.layer : rig.gameObject.layer;
        for (int i = rig.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(rig.GetChild(i).gameObject);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
        var tmp = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(tmp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Vector3 eye = tmp.transform.Find("Camera").localPosition;   // the pack's camera: where your eye sits relative to the arms
        foreach (var name in new[] { "rig", "Arms" }) tmp.transform.Find(name).SetParent(rig, false);
        Object.DestroyImmediate(tmp);
        foreach (var t in rig.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        var bg = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Background");
        if (bg != null) Object.DestroyImmediate(bg.gameObject);   // the pack's backdrop card, on the head bone

        rig.localPosition = -eye * ArmScale + ArmOffset; rig.localRotation = Quaternion.identity; rig.localScale = Vector3.one * ArmScale;
        if (rig.GetComponent<ArmSpread>() == null) Undo.AddComponent<ArmSpread>(rig.gameObject);   // hands further apart

        var arms = rig.Find("Arms").GetComponent<SkinnedMeshRenderer>();
        arms.sharedMaterial = Material();
        arms.shadowCastingMode = shadows;
        arms.updateWhenOffscreen = true;   // (bounds from the bones: never culled at the edge of the screen)

        var anim = punch.armsAnimator = rig.GetComponent<Animator>();
        anim.runtimeAnimatorController = ctl;
        anim.avatar = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().FirstOrDefault();
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        punch.jabSpeed = 1f;
        EditorUtility.SetDirty(punch);
        EditorSceneManager.MarkSceneDirty(punch.gameObject.scene);
        EditorSceneManager.SaveScene(punch.gameObject.scene);
        Debug.Log("Fists 2025: arms swapped in " + punch.gameObject.scene.name);
    }

    static Material Material()
    {
        const string path = Dir + "FistsArms.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "body_C.tga"));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "body_n_normals.TGA"));
        m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(m);
        return m;
    }
}
