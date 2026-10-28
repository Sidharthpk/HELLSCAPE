using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Dedicated humanoid copies leave the original generic clips available to older cutscenes.
public static class DamnedAnimatorBuilder
{
    const string Source = "Assets/Enemies/Scary Zombie Pack/";
    const string Dir = "Assets/Enemies/Dammed/";
    const string Model = "Assets/Models/Dammed.fbx";
    const string SadWalk = "Assets/Models/Sad Walk (1).fbx";
    public const float WalkSpeed = 0.7f, RunSpeed = 3.67f, SneakSpeed = 0.7f;

    static void ImportHuman(string path, bool loop)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(path);
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.optimizeGameObjects = false;
        imp.motionNodeName = "";
        var clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.loopTime = loop;
            c.loopPose = loop;
            c.lockRootRotation = true;
            c.keepOriginalOrientation = true;
            c.lockRootHeightY = true;
            c.heightFromFeet = true;
            c.lockRootPositionXZ = true;
        }
        imp.clipAnimations = clips;
        imp.SaveAndReimport();
    }

    static AnimationClip Clip(string name) => AssetDatabase.LoadAllAssetsAtPath(Dir + name + ".fbx")
        .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));

    [MenuItem("HellScape/Build Damned Animator")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before rebuilding enemies.");
        if (!AssetDatabase.IsValidFolder(Dir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Enemies", "Dammed");
        ImportHuman(Model, false);
        ImportHuman(SadWalk, true);
        foreach (var name in new[] { "zombie idle", "zombie run", "zombie scream", "zombie attack", "Sad Walk", "Sneak Walk" })
        {
            var path = Dir + name + ".fbx";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                AssetDatabase.CopyAsset(name == "Sad Walk" ? SadWalk : Source + name + ".fbx", path);
            ImportHuman(path, name.EndsWith("Walk") || name.Contains("idle") || name.Contains("run"));
        }

        // Preserve the controller GUID and all callers that already reference it.
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(Source + "DamnedController.controller");
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(ac)).Where(o => o != ac).ToArray())
            UnityEngine.Object.DestroyImmediate(o, true);
        var sm = new AnimatorStateMachine { name = "Base Layer" };
        AssetDatabase.AddObjectToAsset(sm, ac);
        ac.layers = new[] { new AnimatorControllerLayer { name = "Base Layer", defaultWeight = 1f, stateMachine = sm } };
        ac.parameters = new[] {
            new AnimatorControllerParameter { name = "Speed", type = AnimatorControllerParameterType.Float },
            new AnimatorControllerParameter { name = "Attack", type = AnimatorControllerParameterType.Trigger }
        };
        AnimatorState Tree(string name, params (AnimationClip clip, float speed)[] children)
        {
            var st = sm.AddState(name, new Vector3(300, name == "Upright" ? 0 : name == "Sneak" ? 180 : 360, 0));
            var tree = new BlendTree { name = name, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, ac);
            foreach (var c in children) tree.AddChild(c.clip, c.speed);
            st.motion = tree;
            st.iKOnFeet = true;
            return st;
        }
        var upright = Tree("Upright", (Clip("zombie idle"), 0), (Clip("Sad Walk"), WalkSpeed), (Clip("zombie run"), RunSpeed));
        Tree("Sneak", (Clip("zombie idle"), 0), (Clip("Sneak Walk"), SneakSpeed), (Clip("zombie run"), RunSpeed));
        // Older cutscenes can still call these state names without playing crawl/bite motions.
        Tree("Crawl", (Clip("zombie idle"), 0), (Clip("Sneak Walk"), SneakSpeed), (Clip("zombie run"), RunSpeed));
        sm.defaultState = upright;
        int row = 0;
        foreach (var pair in new[] { ("Feed", "Sneak Walk"), ("FeedDown", "Sneak Walk"), ("Scream", "zombie scream"), ("Attack", "zombie attack"), ("NeckBite", "zombie attack"), ("CrawlBite", "zombie attack") })
        {
            var st = sm.AddState(pair.Item1, new Vector3(600, -180 + row++ * 80, 0));
            st.motion = Clip(pair.Item2);
            if (pair.Item1.StartsWith("Feed")) { st.speed = pair.Item1 == "Feed" ? 0.65f : 0.9f; continue; }
            var back = st.AddTransition(upright);
            back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.18f;
            if (pair.Item1 == "Attack")
            {
                var any = sm.AddAnyStateTransition(st);
                any.AddCondition(AnimatorConditionMode.If, 0, "Attack");
                any.duration = 0.1f; any.canTransitionToSelf = false;
            }
        }
        EditorUtility.SetDirty(ac);
        var pathOld = Source + "PrimeZombie.prefab";
        var root = PrefabUtility.LoadPrefabContents(pathOld);
        try
        {
            if (!root.GetComponent<Animator>().isHuman) ReplaceModel(root, ac);
            root.GetComponent<Animator>().runtimeAnimatorController = ac;
            var ai = root.GetComponent<ZombieAI>();
            ai.walkClipSpeed = WalkSpeed; ai.runClipSpeed = RunSpeed; ai.sneakClipSpeed = SneakSpeed;
            ai.sneakChance = 0.55f; ai.sneakSpeedRange = new Vector2(0.35f, 0.8f);
            PrefabUtility.SaveAsPrefabAsset(root, pathOld);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        // The named prefab inherits future fixes from the compatibility prefab.
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(pathOld));
        try { instance.name = "Dammed"; instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); PrefabUtility.SaveAsPrefabAsset(instance, Dir + "Dammed.prefab"); }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
        AssetDatabase.SaveAssets();
        Debug.Log("Dammed: humanoid controller, looped locomotion, prefab and ragdoll rebuilt.");
    }

    public static void ReplaceModel(GameObject root, RuntimeAnimatorController controller)
    {
        foreach (Transform t in root.transform.Cast<Transform>().Where(t => t.GetComponentInChildren<SkinnedMeshRenderer>(true) != null || t.name.EndsWith(":Hips")).ToArray())
            UnityEngine.Object.DestroyImmediate(t.gameObject);
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
        foreach (Transform t in model.transform.Cast<Transform>().ToArray()) t.SetParent(root.transform, false);
        var avatar = model.GetComponent<Animator>().avatar;
        UnityEngine.Object.DestroyImmediate(model);
        var animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
        animator.avatar = avatar;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        if (!animator.isHuman || !avatar.isValid) throw new InvalidOperationException("Dammed humanoid avatar is invalid.");
        var ai = root.GetComponent<ZombieAI>();
        if (ai != null)
        {
            ai.animator = animator; ai.walkClipSpeed = WalkSpeed; ai.runClipSpeed = RunSpeed;
            ai.sneakClipSpeed = SneakSpeed;
        }
        BuildRagdoll(root, animator);
    }

    static void BuildRagdoll(GameObject root, Animator animator)
    {
        var bones = new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg };
        var ends = new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Neck, HumanBodyBones.LastBone,
            HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot };
        var bodies = new Rigidbody[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            var t = animator.GetBoneTransform(bones[i]);
            var rb = t.gameObject.AddComponent<Rigidbody>();
            rb.mass = i == 0 ? 14f : i < 3 ? 10f : i == 3 ? 5f : i >= 8 ? 6f : 2.5f;
            rb.isKinematic = true; rb.useGravity = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.solverIterations = 12; rb.solverVelocityIterations = 4;
            bodies[i] = rb;
            var c = t.gameObject.AddComponent<CapsuleCollider>();
            Vector3 delta = ends[i] == HumanBodyBones.LastBone ? new Vector3(0, 0.18f, 0) : t.InverseTransformPoint(animator.GetBoneTransform(ends[i]).position);
            var abs = new Vector3(Mathf.Abs(delta.x), Mathf.Abs(delta.y), Mathf.Abs(delta.z));
            c.direction = abs.x > abs.y && abs.x > abs.z ? 0 : abs.z > abs.y ? 2 : 1;
            c.center = delta * 0.5f;
            c.radius = i == 0 ? 0.13f : i < 3 ? 0.14f : i == 3 ? 0.105f : i >= 8 ? 0.075f : 0.05f;
            c.height = Mathf.Max(delta.magnitude, c.radius * 2);
            c.enabled = false;
        }
        for (int i = 1; i < bodies.Length; i++)
        {
            var t = bodies[i].transform.parent;
            while (t != null && t.GetComponent<Rigidbody>() == null) t = t.parent;
            var j = bodies[i].gameObject.AddComponent<CharacterJoint>();
            j.connectedBody = t.GetComponent<Rigidbody>();
            j.axis = Vector3.right; j.swingAxis = Vector3.forward;
            j.lowTwistLimit = new SoftJointLimit { limit = -25 };
            j.highTwistLimit = new SoftJointLimit { limit = 25 };
            j.swing1Limit = new SoftJointLimit { limit = 40 };
            j.swing2Limit = new SoftJointLimit { limit = 25 };
            j.enableProjection = true; j.enablePreprocessing = false;
        }
        var z = root.GetComponent<Zombie>();
        if (z != null) { z.animator = animator; z.mainCollider = root.GetComponent<Collider>(); z.ragdollBones = bodies; }
    }
}
