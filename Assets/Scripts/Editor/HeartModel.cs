using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// The user's anatomical heart ("heart-animated-anatomical-3d-model": one skinned mesh, six blend shapes, a 1.67 s
// beat). Used twice: as Kessler's weak point in the boss fight (HellScape > Finale > Use Animated Heart) and as
// the model the HUD heart is rendered from (HudIconRenderer).
public static class HeartModel
{
    public const string Fbx = "Assets/New Folder/heart-animated-anatomical-3d-model/source/heart_v_1_3.fbx";
    const string Tex = "Assets/New Folder/heart-animated-anatomical-3d-model/textures/";
    const string BossMat = "Assets/Materials/OfficeAct/AnimatedHeart.mat";
    const string Controller = "Assets/Materials/OfficeAct/Anim/HeartBeat.controller";
    // the mesh as it comes: 2.89 units tall, its middle off the pivot, its front towards FrontAxis
    public static readonly Vector3 Centre = new Vector3(0.169f, -0.309f, 0.272f);
    public const float Height = 2.888f;
    public static readonly Vector3 FrontAxis = Vector3.forward;

    // the textures as they should be imported, and the beat set to loop
    public static void Prepare()
    {
        var nimp = AssetImporter.GetAtPath(Tex + "heart_2_heart_nmap.png") as TextureImporter;
        if (nimp != null && nimp.textureType != TextureImporterType.NormalMap) { nimp.textureType = TextureImporterType.NormalMap; nimp.SaveAndReimport(); }
        var imp = AssetImporter.GetAtPath(Fbx) as ModelImporter;
        if (imp == null) return;
        var clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
        if (clips.Any(c => !c.loopTime))
        {
            foreach (var c in clips) c.loopTime = true;
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }
    }

    // wet muscle; with an emission colour it also glows from inside (the boss's weak point has to read at 40 m)
    public static Material Material(string path, Color emission)
    {
        Prepare();
        var mat = path != null ? AssetDatabase.LoadAssetAtPath<Material>(path) : null;
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (path != null) AssetDatabase.CreateAsset(mat, path);
        }
        var col = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "heart_2_heart_color.jpeg");
        var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "heart_2_heart_nmap.png");
        mat.SetTexture("_BaseMap", col); mat.mainTexture = col;
        mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_BumpMap", nrm); mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_Smoothness", 0.72f);
        mat.SetFloat("_Metallic", 0f);
        if (emission.maxColorComponent > 0f)
        {
            mat.SetTexture("_EmissionMap", col);
            mat.SetColor("_EmissionColor", emission);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    public static AnimationClip Beat() =>
        AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__"));

    static RuntimeAnimatorController BeatController()
    {
        var ctl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        if (ctl == null) ctl = AnimatorController.CreateAnimatorControllerAtPath(Controller);
        var sm = ctl.layers[0].stateMachine;
        foreach (var s in sm.states.ToArray()) sm.RemoveState(s.state);
        var beat = sm.AddState("Beat");
        beat.motion = Beat();
        sm.defaultState = beat;
        EditorUtility.SetDirty(ctl);
        return ctl;
    }

    // Kessler's heart: the glowing ball becomes this, beating. The ball's object stays (it's what DemonBoss.heart,
    // the hit test and the pulse light hang off); only its look changes, and its collider grows to cover the model.
    [MenuItem("HellScape/Finale/Use Animated Heart")]
    public static void UseOnBoss()
    {
        var boss = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if (boss == null || boss.heart == null || model == null) { Debug.LogError("Animated heart: open MainGameScene (DemonBoss with a heart) and keep the model at " + Fbx); return; }
        Transform heart = boss.heart;
        var old = heart.Find("Heart model");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var ball = heart.GetComponent<MeshRenderer>();
        if (ball != null) { Undo.RecordObject(ball, "Heart"); ball.enabled = false; }

        const float tall = 1.3f;                                   // metres, on a 9 m demon
        var g = (GameObject)PrefabUtility.InstantiatePrefab(model, heart);
        Undo.RegisterCreatedObjectUndo(g, "Heart");
        g.name = "Heart model";
        float s = tall / Height / Mathf.Max(0.0001f, heart.lossyScale.y);
        // its front out of the chest (the ball's +z points away from the body)
        Quaternion turn = Quaternion.FromToRotation(FrontAxis, Vector3.forward);
        g.transform.localRotation = turn;
        g.transform.localScale = Vector3.one * s;
        g.transform.localPosition = -(turn * Centre) * s;
        var smr = g.GetComponentInChildren<SkinnedMeshRenderer>();
        smr.sharedMaterial = Material(BossMat, new Color(1.5f, 0.16f, 0.08f));
        smr.updateWhenOffscreen = true;
        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var anim = g.GetComponent<Animator>(); if (anim == null) anim = g.AddComponent<Animator>();
        anim.runtimeAnimatorController = BeatController();
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.speed = 1.6f;

        var hit = heart.GetComponent<SphereCollider>();
        if (hit != null) { Undo.RecordObject(hit, "Heart"); hit.radius = tall * 0.42f / Mathf.Max(0.0001f, heart.lossyScale.y); }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
        Debug.Log("Kessler's heart is the animated one now (" + tall + " m).");
    }
}
