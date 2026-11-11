using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Story V4: Vane's second form (THE AMALGAMATION) is the user's "Cry Wolf" model (Models/cry-wolf-game-character,
// a Generic rig with its own clips). It replaces the damned body that turns after the CCTV tape and hunts you down
// the service tunnel: OfficeAct/Office/KesslerConfrontation/Kessler (damned), shared by KesslerConfrontation and
// KesslerChase. Their scripts are unchanged: this controller has the states and parameters they call by name
// (Speed / Attack, "Blend Tree", "Run", "Scream", "zombieattack", "Crawl"). The old body's meshes and Animator are
// switched off, not deleted. Rerun after OfficeActBuilder rebuilds the office (it makes the old body again).
public static class VaneCryWolf
{
    const string Fbx = "Assets/Models/cry-wolf-game-character/source/Cry Wolf SETS.fbx";
    const string ControllerPath = "Assets/Materials/OfficeAct/Anim/VaneCryWolf.controller";
    const string ChildName = "Vane - Cry Wolf (second form)";
    const float Height = 2.4f;   // taller than the man he was (the old damned body was 2.2 m)

    [MenuItem("HellScape/Vane: Cry Wolf Second Form")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if (model == null) return "No " + Fbx;
        var ctrl = Controller();
        if (ctrl == null) return "The Cry Wolf clips did not import.";

        var chase = Object.FindObjectsByType<KesslerChase>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        var conf = Object.FindObjectsByType<KesslerConfrontation>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (chase == null || chase.kessler == null) return "Open MainGameScene (no KesslerChase with a body).";
        Transform body = chase.kessler;

        var old = body.Find(ChildName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        // the old body off (its bones too: KesslerChase looks for a "Head" among the active ones)
        foreach (Transform c in body)
            if (c.GetComponentInChildren<Renderer>(true) != null || c.name.Contains("Hips")) { Undo.RecordObject(c.gameObject, "Cry Wolf"); c.gameObject.SetActive(false); }
        var rootAnim = body.GetComponent<Animator>();
        if (rootAnim != null) { Undo.RecordObject(rootAnim, "Cry Wolf"); rootAnim.enabled = false; }

        var wolf = (GameObject)PrefabUtility.InstantiatePrefab(model, body.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(wolf, "Cry Wolf");
        wolf.name = ChildName;
        wolf.transform.SetParent(body, false);
        wolf.transform.localPosition = Vector3.zero;
        wolf.transform.localRotation = Quaternion.identity;
        // to height, feet on the body's origin (the source file is in its own units)
        var rs = wolf.GetComponentsInChildren<Renderer>(true);
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        float s = Height / Mathf.Max(0.01f, b.size.y);
        wolf.transform.localScale = Vector3.one * s / Mathf.Max(0.0001f, body.lossyScale.y);
        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        wolf.transform.position += Vector3.up * (body.position.y - b.min.y);
        foreach (var r in rs)
        {
            r.enabled = true;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            // the FBX's materials come in untextured: its two texture sets, by the material's name
            string set = r.sharedMaterial != null && r.sharedMaterial.name.Contains("SEC") ? "SEC" : "Main";
            r.sharedMaterial = Material(set);
        }
        foreach (var c in wolf.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c);
        foreach (var l in wolf.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);

        var anim = wolf.GetComponent<Animator>();
        if (anim == null) anim = wolf.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Undo.RecordObject(chase, "Cry Wolf"); chase.kesslerAnim = anim;
        if (conf != null) { Undo.RecordObject(conf, "Cry Wolf"); conf.damnedAnim = anim; }
        var z = body.GetComponent<Zombie>();
        if (z != null) { Undo.RecordObject(z, "Cry Wolf"); z.animator = anim; }

        EditorSceneManager.MarkSceneDirty(body.gameObject.scene);
        EditorSceneManager.SaveScene(body.gameObject.scene);
        return $"Vane's second form is the Cry Wolf: {Height} m tall, controller {ControllerPath}.";
    }

    static Material Material(string set)
    {
        string path = "Assets/Materials/OfficeAct/CryWolf_" + set + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        string tex = "Assets/Models/cry-wolf-game-character/textures/BODY_PAINT_Cry_Wolf_" + set + "_";
        string normal = tex + "Normal.jpeg";
        if (AssetImporter.GetAtPath(normal) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "BaseColor.jpeg"));
        m.SetColor("_BaseColor", Color.white);
        var n = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
        if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
        m.SetFloat("_Smoothness", .35f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------------------------------------------------------------- the silo (story V4: Vane, not Elias)

    const string SiloControllerPath = "Assets/Materials/OfficeAct/Anim/VaneSiloBoss.controller";
    const float SiloHeight = 2.1f;   // (the lift and the tower stairs: a little smaller than the tunnel's)

    // The silo's killer (OfficeAct/Silo/Killer: KillerBoss, ChaseKiller, KillerCutscenes all drive it) becomes Vane:
    // the Cry Wolf body, a controller with the states those scripts cross-fade to, and the V4 fight - no dodging,
    // blood balls to dodge, a rush (the walk at full speed) that staggers him against the wall, his attack up close.
    [MenuItem("HellScape/Vane: Silo Boss (Cry Wolf)")]
    public static void SiloMenu() => Debug.Log(SiloBoss());

    public static string SiloBoss()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        var boss = Object.FindObjectsByType<KillerBoss>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (boss == null) return "Open MainGameScene (no KillerBoss).";
        ImportClips();
        var ctrl = SiloController();
        if (ctrl == null) return "The Cry Wolf clips did not import.";
        Transform body = boss.transform;

        var old = body.Find(ChildName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        foreach (Transform c in body)
            if (c.GetComponentInChildren<Renderer>(true) != null) { Undo.RecordObject(c.gameObject, "Cry Wolf"); c.gameObject.SetActive(false); }

        var wolf = (GameObject)PrefabUtility.InstantiatePrefab(model, body.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(wolf, "Cry Wolf");
        wolf.name = ChildName;
        wolf.transform.SetParent(body, false);
        wolf.transform.localPosition = Vector3.zero;
        wolf.transform.localRotation = Quaternion.identity;
        var rs = wolf.GetComponentsInChildren<Renderer>(true);
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        wolf.transform.localScale = Vector3.one * (SiloHeight / Mathf.Max(0.01f, b.size.y)) / Mathf.Max(0.0001f, body.lossyScale.y);
        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        wolf.transform.position += Vector3.up * (body.position.y - b.min.y);
        foreach (var r in rs)
        {
            r.enabled = true;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            r.sharedMaterial = Material(r.sharedMaterial != null && r.sharedMaterial.name.Contains("SEC") ? "SEC" : "Main");
        }
        foreach (var c in wolf.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c);
        foreach (var l in wolf.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
        var anim = wolf.GetComponent<Animator>();
        if (anim == null) anim = wolf.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Undo.RecordObject(boss, "Cry Wolf");
        boss.animator = anim;
        boss.model = wolf.transform;
        boss.bloodBalls = true;
        boss.dodgeChance = 0f;
        boss.knifeModel = null;
        boss.walkSpeed = 2.2f; boss.enragedSpeed = 3.2f;
        boss.attackRange = 2.6f; boss.attackDamage = 12f; boss.attackCooldown = 1.6f; boss.hitDelay = 0.55f;
        boss.moveEvery = 2.4f; boss.throwOverCharge = 0.4f;
        boss.chargeWindup = 0.5f; boss.chargeSpeed = 10f; boss.chargeDamage = 18f;
        boss.stunTime = 2.2f; boss.getUpTime = 0.6f;
        boss.throwRelease = 0.55f; boss.throwTime = 1.3f;
        boss.ballSpeed = 9f; boss.ballDamage = 9f;
        boss.mouth = new Vector3(0f, 1.6f, 0.8f);
        foreach (var chase in Object.FindObjectsByType<ChaseKiller>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { Undo.RecordObject(chase, "Cry Wolf"); chase.animator = anim; }
        foreach (var cuts in Object.FindObjectsByType<KillerCutscenes>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { Undo.RecordObject(cuts, "Cry Wolf"); cuts.killerAnim = anim; }

        EditorSceneManager.MarkSceneDirty(body.gameObject.scene);
        EditorSceneManager.SaveScene(body.gameObject.scene);
        return $"Vane is the silo boss: Cry Wolf {SiloHeight} m, controller {SiloControllerPath}.";
    }

    static AnimatorController SiloController()
    {
        AnimationClip idle = Clip("IDDLE1"), idle2 = Clip("IDDLE2"), walk = Clip("MOVE_Walk"), atk = Clip("ATK 1"), atk2 = Clip("ATK 2"),
            stagger = Clip("DAMAGE Stagger"), death = Clip("DAMAGE DEATH"), hurt = Clip("DAMAGE 1");
        if (idle == null || walk == null || atk == null) return null;
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(SiloControllerPath) != null) AssetDatabase.DeleteAsset(SiloControllerPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(SiloControllerPath);
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        var sm = ac.layers[0].stateMachine;

        // walking pace follows his speed (the chase runs him at 4-7 m/s: the walk, played faster)
        var tree = new BlendTree { name = "Locomotion", blendParameter = "Speed", blendType = BlendTreeType.Simple1D, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(tree, ac);
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, 1f);
        tree.AddChild(walk, 3.5f);
        tree.AddChild(walk, 8f);
        var kids = tree.children;
        kids[1].timeScale = 0.9f; kids[2].timeScale = 1.8f; kids[3].timeScale = 3f;
        tree.children = kids;
        var loco = sm.AddState("Locomotion");
        loco.motion = tree;
        sm.defaultState = loco;

        AnimatorState State(string n, Motion m, float speed = 1f) { var s = sm.AddState(n); s.motion = m; s.speed = speed; return s; }
        State("Charge", walk, 3.2f);                 // the rush: the walk at full tilt (user)
        State("Stunned", stagger ?? hurt ?? idle);   // into the wall
        State("GetUp", idle2 ?? idle);
        State("Throw", atk2 ?? atk);                 // the heave that spits the blood
        State("DodgeBack", walk, -1.6f);             // (a step back before a volley, when you're too close)
        State("Death", death ?? hurt ?? idle);
        var swing = State("Swing", atk);
        var hit = sm.AddAnyStateTransition(swing);
        hit.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        hit.hasExitTime = false; hit.duration = .08f; hit.canTransitionToSelf = false;
        var back = swing.AddTransition(loco);
        back.hasExitTime = true; back.exitTime = .92f; back.duration = .15f;
        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        return ac;
    }

    static AnimationClip Clip(string name) => AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>()
        .FirstOrDefault(c => c.name == "rig|" + name);

    static AnimatorController Controller()
    {
        ImportClips();
        AnimationClip idle = Clip("IDDLE1"), walk = Clip("MOVE_Walk"), tease = Clip("STRIKE TEASE HIT"), atk = Clip("ATK 1"), low = Clip("IDDLE3");
        if (idle == null || walk == null || atk == null) return null;
        return BuildOfficeController(idle, walk, tease, atk, low);
    }

    // the FBX's clips: no cameras or lights, the idles and the walk loop
    static void ImportClips()
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        bool changed = mi.importCameras || mi.importLights;
        mi.importCameras = false; mi.importLights = false;
        var clips = mi.clipAnimations.Length > 0 ? mi.clipAnimations : mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            bool loop = c.name == "rig|IDDLE1" || c.name == "rig|IDDLE2" || c.name == "rig|IDDLE3" || c.name == "rig|MOVE_Walk";
            if (c.loopTime != loop) { c.loopTime = loop; changed = true; }
        }
        if (changed) { mi.clipAnimations = clips; mi.SaveAndReimport(); }
    }

    static AnimatorController BuildOfficeController(AnimationClip idle, AnimationClip walk, AnimationClip tease, AnimationClip atk, AnimationClip low)
    {

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        var sm = ac.layers[0].stateMachine;

        var tree = new BlendTree { name = "Blend Tree", blendParameter = "Speed", blendType = BlendTreeType.Simple1D, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(tree, ac);
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, 1f);
        var locomotion = sm.AddState("Blend Tree");
        locomotion.motion = tree;
        sm.defaultState = locomotion;

        var run = sm.AddState("Run"); run.motion = walk; run.speed = 2.2f;   // (no run clip: the walk, driven hard)
        var scream = sm.AddState("Scream"); scream.motion = tease ?? idle;
        var crawl = sm.AddState("Crawl"); crawl.motion = low ?? idle;
        var attack = sm.AddState("zombieattack"); attack.motion = atk;

        var hit = locomotion.AddTransition(attack);
        hit.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        hit.hasExitTime = false; hit.duration = .1f;
        foreach (var from in new[] { run, scream, crawl })
        {
            var t = from.AddTransition(attack);
            t.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            t.hasExitTime = false; t.duration = .1f;
        }
        var back = attack.AddTransition(locomotion);
        back.hasExitTime = true; back.exitTime = .95f; back.duration = .15f;

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        return ac;
    }
}
