using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// The user's Meshy cast (Assets/Perishables/Avatar, 2026-10-22). Each animation came as its own FBX of the same
// rigged man (Generic rigs, the same bone paths per character), so one copy of a character plays the clips of its
// other files. The FBXs come untextured: each gets a URP material from its texture_0 (+ normal).
//   HellScape > Prologue: Sofa Shooting   - Derek (run, shot in the back) and Mercer (slow walk, turn into the aim,
//                                           the Beretta in his hand) for PrologueDirector.ShotOnTheSofa (Prologue open)
public static class MeshyCastBuilder
{
    const string Dir = "Assets/Perishables/Avatar/";
    const string Out = "Assets/Generated/Meshy/";
    const string DerekDir = Dir + "Meshy_AI_Derek_PS2_Rigged_biped (1)/Derek_Run Trip/";
    const string DerekRun = DerekDir + "Meshy_AI_Derek_PS2_Rigged_biped_Animation_Running_frame_rate_30.fbx";
    const string DerekShot = Dir + "Meshy_AI_Derek_PS2_Rigged_biped/Derek BackShot/Meshy_AI_Derek_PS2_Rigged_biped_Animation_Hit_in_Back_While_Running_frame_rate_30.fbx";
    public const string DerekWalk = Dir + "Meshy_AI_Derek_PS2_Rigged_biped (2)/Derek_Walking/Meshy_AI_Derek_PS2_Rigged_biped_Animation_Walking_frame_rate_30.fbx";
    public const string MercerDir = Dir + "Meshy_AI_Mercer_PS2_T_Pose_35K_biped/Mercer_Slow Walk/";
    public const string MercerWalk = MercerDir + "Meshy_AI_Mercer_PS2_T_Pose_35K_biped_Animation_Walk_Fight_Forward_withSkin.fbx";
    public const string MercerAim = Dir + "Meshy_AI_Mercer_PS2_T_Pose_35K_biped (1)/Mercer_Right Turn Aim/Meshy_AI_Mercer_PS2_T_Pose_35K_biped_Animation_Rifle_Aim_Turn_Right_withSkin.fbx";
    const string Pistol = "Assets/Models/Gun/pistol/source/beretta.fbx";
    const string PistolTex = "Assets/Models/Gun/pistol/textures/beretta";
    const string Group = "Sofa shooting (generated)";

    public static float MercerHeight = 1.82f, DerekHeight = 1.74f;
    public static Vector3 GripPos = new Vector3(0f, 0.005f, 0.06f), GripRot = new Vector3(0f, 0f, 0f);

    [MenuItem("HellScape/Prologue: Sofa Shooting")]
    public static void SofaMenu() => Debug.Log(SofaShooting());

    public static string SofaShooting()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var d = Object.FindFirstObjectByType<PrologueDirector>(FindObjectsInactive.Include);
        if (d == null) return "Open the Prologue scene.";
        var old = GameObject.Find(Group);
        if (old != null) Object.DestroyImmediate(old);
        var group = new GameObject(Group);
        SceneManager_Move(group, d.gameObject.scene);

        Loop(DerekRun, true); Loop(DerekShot, false); Loop(MercerWalk, true); Loop(MercerAim, false);
        var derek = Man(DerekRun, "Derek (runs, shot)", group.transform, DerekHeight, Mat("Derek", DerekDir + "Meshy_AI_Derek_PS2_Rigged_biped_texture_0.png"),
            Controller("DerekShot", ("Run", Clip(DerekRun)), ("Shot", Clip(DerekShot))));
        // the back-shot clip is a stagger he runs on out of: he goes down as a ragdoll from the stumble (asleep till then)
        Ragdoll(derek);
        var mercer = Man(MercerWalk, "Mercer (walks, aims)", group.transform, MercerHeight, Mat("Mercer", MercerDir + "Meshy_AI_Mercer_PS2_T_Pose_35K_biped_texture_0.png"),
            Controller("MercerAim", ("Walk", Clip(MercerWalk)), ("TurnAim", Clip(MercerAim))));

        // the Beretta in his right hand, fitted in the last frame of the turn: the barrel along where he faces then
        // (a quarter right of his root)
        var muzzle = HandGun(mercer, "RightHand", Clip(MercerAim), Clip(MercerAim).length, () => mercer.transform.right);
        Clip(MercerWalk).SampleAnimation(mercer, 0f);

        d.derekRuns = derek.GetComponent<Animator>();
        d.mercerAims = mercer.GetComponent<Animator>();
        d.muzzle = muzzle;
        derek.SetActive(false); mercer.SetActive(false);
        EditorUtility.SetDirty(d);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(d.gameObject.scene);
        EditorSceneManager.SaveScene(d.gameObject.scene);
        return "Sofa shooting: Derek + Mercer under '" + Group + "'.";
    }

    // the Beretta in the hand bone whose name ends with `hand`, fitted with the man posed at `at` seconds into `clip`:
    // the barrel along `facing` (asked after the pose), held out of his fist. Returns the muzzle (the end of its
    // longest side). The hand bone is scaled ~100: placed in world metres, then parented keeping the pose.
    public static Transform HandGun(GameObject man, string hand, AnimationClip clip, float at, System.Func<Vector3> facing)
    {
        var bone = man.GetComponentsInChildren<Transform>(true).First(t => t.name.EndsWith(hand));
        var gun = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Pistol));
        PrefabUtility.UnpackPrefabInstance(gun, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        gun.name = "Pistol";
        var gunMat = Mat("Beretta", PistolTex + "Color.png", PistolTex + "Normal.png");
        foreach (var r in gun.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials = Enumerable.Repeat(gunMat, r.sharedMaterials.Length).ToArray();
        var gb = Bounds(gun);
        int axis = gb.size.x >= gb.size.y && gb.size.x >= gb.size.z ? 0 : gb.size.y >= gb.size.z ? 1 : 2;
        gun.transform.localScale *= 0.21f / gb.size[axis];   // a Beretta is 21 cm long
        gb = Bounds(gun);
        var muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(gun.transform, false);
        Vector3 tip = gb.center; tip[axis] = gb.max[axis];
        muzzle.position = tip;
        clip.SampleAnimation(man, at);
        Vector3 f = facing();
        gun.transform.SetPositionAndRotation(bone.position + f * GripPos.z + Vector3.up * GripPos.y, Quaternion.LookRotation(f, Vector3.up) * Quaternion.Euler(GripRot));
        gun.transform.SetParent(bone, true);
        return muzzle;
    }

    // a plain ragdoll on Derek's rig: a capsule from each bone to the next, a joint to the body above; asleep (kinematic)
    static void Ragdoll(GameObject man)
    {
        var bones = man.GetComponentsInChildren<Transform>(true);
        Transform B(string n) => bones.First(t => t.name == n);
        Rigidbody Part(string bone, string to, float radius, float mass, string parent, float swing)
        {
            Transform t = B(bone), end = B(to);
            Vector3 local = t.InverseTransformPoint(end.position);
            int axis = Mathf.Abs(local.x) > Mathf.Abs(local.y) ? (Mathf.Abs(local.x) > Mathf.Abs(local.z) ? 0 : 2) : (Mathf.Abs(local.y) > Mathf.Abs(local.z) ? 1 : 2);
            var cap = t.gameObject.AddComponent<CapsuleCollider>();
            cap.direction = axis; cap.center = local * .5f;
            cap.radius = radius / t.lossyScale.x;
            cap.height = Mathf.Abs(local[axis]) + cap.radius * 2f;
            var rb = t.gameObject.AddComponent<Rigidbody>();
            rb.mass = mass; rb.isKinematic = true;   // (no interpolation while asleep: it would pin the bones against the animation)
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            if (parent != null)
            {
                var j = t.gameObject.AddComponent<CharacterJoint>();
                j.connectedBody = B(parent).GetComponent<Rigidbody>();
                j.enableProjection = true;
                j.lowTwistLimit = new SoftJointLimit { limit = -20f }; j.highTwistLimit = new SoftJointLimit { limit = 20f };
                j.swing1Limit = new SoftJointLimit { limit = swing }; j.swing2Limit = new SoftJointLimit { limit = swing * .5f };
            }
            return rb;
        }
        Part("Hips", "Spine01", .14f, 12f, null, 0f);
        Part("Spine01", "neck", .15f, 12f, "Hips", 25f);
        Part("Head", "head_end", .11f, 5f, "Spine01", 35f);
        foreach (var s in new[] { "Left", "Right" })
        {
            Part(s + "UpLeg", s + "Leg", .08f, 7f, "Hips", 50f);
            Part(s + "Leg", s + "Foot", .06f, 4f, s + "UpLeg", 70f);
            Part(s + "Arm", s + "ForeArm", .05f, 3f, "Spine01", 80f);
            Part(s + "ForeArm", s + "Hand", .045f, 2f, s + "Arm", 70f);
        }
    }

    // ---------------------------------------------------------------- the soul void (MainGameScene)

    const string KingDir = Dir + "Meshy_AI_Crimson_Hellscape_Dem_biped/Idle Shout/";
    const string KingShout = KingDir + "Meshy_AI_Crimson_Hellscape_Dem_biped_Animation_Idle_03_withSkin.fbx";
    const string KingIdle = Dir + "Meshy_AI_Crimson_Hellscape_Dem_biped (1)/Demon King Idle/Meshy_AI_Crimson_Hellscape_Dem_biped_Animation_Idle_11_withSkin.fbx";
    const string KingName = "Demon King (Crimson)", ShadeName = "Void shade (Derek)";

    //   HellScape > Void: Meshy Cast   - the Demon King is the crimson demon (idles; "Appear" = the shout, then idle),
    //                                    the old model switched off; the void's shadow people are Derek, dark red, red eyes
    [MenuItem("HellScape/Void: Meshy Cast")]
    public static void VoidMenu() => Debug.Log(VoidCast());

    public static string VoidCast()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var e = Object.FindFirstObjectByType<EndingSequence>(FindObjectsInactive.Include);
        if (e == null || e.demon == null || e.demon.root == null) return "Open MainGameScene (no EndingSequence with a Demon King).";
        Transform root = e.demon.root.transform;

        // the King: as tall as the old one, which goes off
        float height = 0f;
        var oldKing = root.Find(KingName);
        if (oldKing != null) Object.DestroyImmediate(oldKing.gameObject);
        foreach (Transform c in root)
            if (c.GetComponentInChildren<Renderer>(true) != null)
            {
                c.gameObject.SetActive(true);
                height = Mathf.Max(height, Bounds(c.gameObject).size.y);
                c.gameObject.SetActive(false);
            }
        Loop(KingShout, false); Loop(KingIdle, true);
        string ctrlPath = Out + "DemonKingCrimson.controller";
        if (!AssetDatabase.IsValidFolder("Assets/Generated/Meshy")) AssetDatabase.CreateFolder("Assets/Generated", "Meshy");
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) ?? AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var sm = ac.layers[0].stateMachine;
        foreach (var s in sm.states) sm.RemoveState(s.state);
        foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
        if (!ac.parameters.Any(p => p.name == e.demon.animTrigger)) ac.AddParameter(e.demon.animTrigger, AnimatorControllerParameterType.Trigger);
        var idle = sm.AddState("Idle"); idle.motion = Clip(KingIdle);
        var shout = sm.AddState("Shout"); shout.motion = Clip(KingShout);
        sm.defaultState = idle;
        var go = sm.AddAnyStateTransition(shout);
        go.AddCondition(AnimatorConditionMode.If, 0f, e.demon.animTrigger); go.duration = .2f; go.canTransitionToSelf = false;
        var back = shout.AddTransition(idle); back.hasExitTime = true; back.exitTime = .92f; back.duration = .4f;
        EditorUtility.SetDirty(ac);

        var king = Man(KingShout, KingName, root, height > .1f ? height : 30f, Mat("DemonKingCrimson", KingDir + "Meshy_AI_Crimson_Hellscape_Dem_biped_texture_0.png"), ac);
        e.demon.animator = king.GetComponent<Animator>();
        e.kingHeadHeight = .75f;   // looking at his chest: his head and arms in the picture, the floor at his waist
        e.kingHeadFov = 34f;

        // the shadow people: Derek walking, his own texture gone dark red, two red eyes
        var oldShade = e.transform.Find(ShadeName);
        if (oldShade != null) Object.DestroyImmediate(oldShade.gameObject);
        Loop(DerekWalk, true); Loop(DerekRun, true);
        var shadeMat = AssetDatabase.LoadAssetAtPath<Material>(Out + "VoidShade.mat");
        if (shadeMat == null) { shadeMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(shadeMat, Out + "VoidShade.mat"); }
        shadeMat.SetTexture("_BaseMap", null);   // a silhouette: no face, no clothes, just the shape of him
        shadeMat.SetColor("_BaseColor", Color.black);
        EditorUtility.SetDirty(shadeMat);
        var eyeMat = AssetDatabase.LoadAssetAtPath<Material>(Out + "VoidShadeEyes.mat");
        if (eyeMat == null) { eyeMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(eyeMat, Out + "VoidShadeEyes.mat"); }
        eyeMat.SetColor("_BaseColor", new Color(1f, .04f, .02f));
        EditorUtility.SetDirty(eyeMat);
        var shade = Man(DerekWalk, ShadeName, e.transform, DerekHeight, shadeMat, Controller("VoidShadeWalk", ("Walk", Clip(DerekWalk)), ("Run", Clip(DerekRun))));
        var bones = shade.GetComponentsInChildren<Transform>(true);
        Transform head = bones.First(t => t.name == "Head"), face = bones.First(t => t.name == "headfront");
        foreach (float side in new[] { -1f, 1f })
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(eye.GetComponent<Collider>());
            eye.name = "Red eye";
            eye.GetComponent<Renderer>().sharedMaterial = eyeMat;
            eye.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            eye.transform.position = face.position + shade.transform.right * side * EyeApart + Vector3.up * EyeUp;
            eye.transform.localScale = Vector3.one * .016f;
            eye.transform.SetParent(head, true);
        }
        foreach (var r in shade.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shade.SetActive(false);
        e.shadeModel = shade;

        EditorUtility.SetDirty(e);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(e.gameObject.scene);
        EditorSceneManager.SaveScene(e.gameObject.scene);
        return "Void: crimson Demon King (" + height.ToString("F1") + " m like the old one), shadow people = Derek.";
    }

    public static float EyeApart = .03f, EyeUp = .06f;    // from the "headfront" bone (the tip of his nose)

    static void SceneManager_Move(GameObject g, UnityEngine.SceneManagement.Scene s) => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g, s);

    public static Bounds Bounds(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>(true);
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    public static void Loop(string fbx, bool loop)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
        var clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
        if (clips.All(c => c.loopTime == loop)) return;
        foreach (var c in clips) c.loopTime = loop;
        imp.clipAnimations = clips;
        imp.SaveAndReimport();
    }

    public static AnimationClip Clip(string fbx) =>
        AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).OrderByDescending(c => c.length).First();

    public static Material Mat(string name, string baseMap, string normal = null)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Generated/Meshy")) AssetDatabase.CreateFolder("Assets/Generated", "Meshy");
        string path = Out + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseMap));
        m.SetColor("_BaseColor", Color.white);
        normal ??= baseMap.Replace(".png", "_normal.png");
        if (AssetImporter.GetAtPath(normal) is TextureImporter ti)
        {
            if (ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            m.EnableKeyword("_NORMALMAP");
        }
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", .2f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // the states in order; each one-shot runs on into the next (the last one holds, or loops if its clip does)
    public static AnimatorController Controller(string name, params (string state, AnimationClip clip)[] states)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Generated/Meshy")) AssetDatabase.CreateFolder("Assets/Generated", "Meshy");
        string path = Out + name + ".controller";
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ac.layers[0].stateMachine;
        foreach (var old in sm.states) sm.RemoveState(old.state);   // (rebuilt in place: the scene keeps its reference)
        foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
        AnimatorState prev = null;
        foreach (var (state, clip) in states)
        {
            var s = sm.AddState(state);
            s.motion = clip;
            if (prev != null && prev.motion is AnimationClip pc && !pc.isLooping)
            {
                var t = prev.AddTransition(s);
                t.hasExitTime = true; t.exitTime = .92f; t.duration = .25f; t.hasFixedDuration = true;
            }
            prev = s;
        }
        sm.defaultState = sm.states[0].state;
        return ac;
    }

    // one copy of a character: its material, its controller, to height (Meshy puts the feet on the origin)
    public static GameObject Man(string fbx, string name, Transform parent, float height, Material mat, RuntimeAnimatorController ctrl)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
        PrefabUtility.UnpackPrefabInstance(g, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        g.name = name;
        g.transform.SetParent(parent, false);
        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
        }
        var anim = g.GetComponent<Animator>();
        if (anim == null) anim = g.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // measured in the first frame of its first clip (the bind pose of these files is not always to scale)
        var first = ((AnimatorController)ctrl).layers[0].stateMachine.defaultState.motion as AnimationClip;
        if (first != null) first.SampleAnimation(g, 0f);
        var b = Bounds(g);
        g.transform.localScale *= height / Mathf.Max(.01f, b.size.y);
        return g;
    }
}
