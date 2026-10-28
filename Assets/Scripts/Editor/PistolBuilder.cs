using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// The pistol replaces the AK (user, 2026-10-21): the Beretta first-person arms (Assets/Models/Gun/beretta-pistol-
// fps-animation) on the player camera, and the plain Beretta (Gun/pistol) as the thing you pick up: in Kessler's armory
// (NewOfficeFit.Extras) and, if you never opened it, on the silo floor.
//
// The animation file is ONE 16.8 s take with every action in a row, and it says 0 fps (Unity calls that 1 fps). With
// curve resampling on, Unity sampled half the arm bones once a second: it is off. The importer would also round clip
// ends to whole "frames" (= seconds), so the clips are baked here instead, at 30 fps, into Generated/Gun/Pistol_*.anim.
// The actions (seconds), read off the slide kicks, the magazine's keys and a contact sheet of the take:
//   Draw 0-1  Inspect 1-6 (turned over one-handed)  Idle 6.2-7.4  Shoot 7.5-8.1 (slide kick at 7.57)
//   Reload 8.3-10.6 (magazine swap)  [a second shot at 11.4]  ReloadEmpty 12-15.8 (mag flies out, slide release)  Holster 16.2-16.8
// The rig faces +z with the gun held two-handed at shoulder height; the eye is 16 cm above and 30 cm behind its origin.
public static class PistolBuilder
{
    const string ArmsFbx = "Assets/Models/Gun/beretta-pistol-fps-animation/source/pistolanim/pistolanim.fbx";
    public const string PistolFbx = "Assets/Models/Gun/pistol/source/beretta.fbx";
    const string Dir = "Assets/Generated/Gun";
    static readonly Vector3 Eye = new Vector3(0f, .16f, -.30f);

    static readonly (string state, float from, float to, bool loop)[] Clips =
    {
        ("Draw", 0f, 1f, false), ("Inspect", 1f, 6f, false), ("Idle", 6.2f, 7.4f, true), ("Shoot", 7.5f, 8.1f, false),
        ("Reload", 8.3f, 10.6f, false), ("ReloadEmpty", 12f, 15.8f, false), ("Holster", 16.2f, 16.8f, false),
    };

    [MenuItem("HellScape/Gun: Pistol (replaces the AK)")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Stop Play first."); return; }
        var gun = Object.FindObjectsByType<GunController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (gun == null || gun.cam == null) { Debug.LogError("Open MainGameScene: no GunController on the player camera."); return; }
        Import();
        var ctrl = Controller();

        // the view model: the AK goes
        Undo.RegisterFullObjectHierarchyUndo(gun.cam.gameObject, "Pistol");
        int layer = gun.viewModel != null ? gun.viewModel.layer : gun.cam.gameObject.layer;
        if (gun.viewModel != null) Undo.DestroyObjectImmediate(gun.viewModel);
        var old = gun.cam.transform.Find("Pistol (ViewModel)");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var vm = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ArmsFbx), gun.cam.transform);
        PrefabUtility.UnpackPrefabInstance(vm, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        vm.name = "Pistol (ViewModel)";
        vm.transform.localPosition = -Eye;
        vm.transform.localRotation = Quaternion.identity;
        foreach (var t in vm.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        foreach (var r in vm.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        foreach (var s in vm.GetComponentsInChildren<SkinnedMeshRenderer>(true)) s.updateWhenOffscreen = true;   // (its bounds sit behind the near plane)
        var anim = vm.GetComponent<Animator>();
        if (anim == null) anim = vm.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // the muzzle flash: a lamp a hand's length in front of the gun's bone, riding with it
        AssetDatabase.LoadAssetAtPath<AnimationClip>(Dir + "/Pistol_Idle.anim").SampleAnimation(vm, 0f);
        var arma = vm.GetComponentsInChildren<Transform>(true).First(t => t.name == "ARMA");
        var flash = new GameObject("Muzzle flash").transform;
        flash.SetParent(arma, false);
        flash.position = arma.position + gun.cam.transform.forward * .16f;
        var light = flash.gameObject.AddComponent<Light>();
        light.type = LightType.Point; light.color = new Color(1f, .7f, .3f); light.intensity = 4f; light.range = 6f; light.shadows = LightShadows.None;
        light.enabled = false;
        if (gun.muzzleLight != null && !gun.muzzleLight.transform.IsChildOf(vm.transform)) Undo.DestroyObjectImmediate(gun.muzzleLight.gameObject);
        vm.SetActive(false);

        Undo.RecordObject(gun, "Pistol");
        gun.viewModel = vm;
        gun.animator = anim;
        gun.muzzleLight = light;
        gun.automatic = false;          // a round a click
        gun.fireRate = 4f;
        gun.damage = 24f;               // (the AK did 14 at 6.5 a second: about the same against a boss at a steady click)
        gun.magSize = 15;
        gun.reloadTime = 2.3f;          // the Reload clip
        gun.reloadEmptyTime = 3.8f;     // ReloadEmpty
        gun.kickBack = .03f;
        EditorUtility.SetDirty(gun);

        SiloFallback();
        EditorSceneManager.MarkSceneDirty(gun.gameObject.scene);
        Debug.Log("Pistol on the camera (" + vm.name + "), animator " + AssetDatabase.GetAssetPath(ctrl) + "; the AK view model is gone.");
    }

    static void Import()
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(ArmsFbx);
        mi.resampleCurves = false;
        mi.animationCompression = ModelImporterAnimationCompression.Off;
        mi.clipAnimations = mi.defaultClipAnimations;   // the whole take, cut up in Controller()
        mi.SaveAndReimport();
        // the textures came at 4K: 1024 is plenty for a gun and two hands (and the build stays small)
        foreach (var dir in new[] { "Assets/Models/Gun/beretta-pistol-fps-animation", "Assets/Models/Gun/pistol" })
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (ti.maxTextureSize <= 1024 && ti.crunchedCompression) continue;
                ti.maxTextureSize = 1024; ti.crunchedCompression = true;
                ti.SaveAndReimport();
            }
    }

    static AnimatorController Controller()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Generated", "Gun");
        string path = Dir + "/Pistol.controller";
        AssetDatabase.DeleteAsset(path);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
        var take = AssetDatabase.LoadAllAssetsAtPath(ArmsFbx).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
        var clips = Clips.ToDictionary(c => c.state, c => Cut(take, c.state, c.from, c.to, c.loop));
        var sm = ac.layers[0].stateMachine;
        var idle = sm.AddState("Idle");
        idle.motion = clips["Idle"];
        sm.defaultState = idle;
        foreach (var c in Clips.Where(c => c.state != "Idle"))
        {
            var s = sm.AddState(c.state);
            s.motion = clips[c.state];
            var t = s.AddTransition(idle);
            t.hasExitTime = true; t.exitTime = .95f; t.duration = .08f;
        }
        return ac;
    }

    // one action out of the take, baked: the take is played on a scratch copy of the rig and every animated bone's
    // position, rotation (as a quaternion: the file's Euler curves are in its own rotation order, which a new clip
    // would read wrong) and scale is recorded 30 times a second from `from` to `to`
    static AnimationClip Cut(AnimationClip take, string name, float from, float to, bool loop)
    {
        string path = Dir + "/Pistol_" + name + ".anim";
        AssetDatabase.DeleteAsset(path);
        var rig = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ArmsFbx));
        try
        {
            var bones = AnimationUtility.GetCurveBindings(take).Select(b => b.path).Distinct()
                .Select(p => (p, t: rig.transform.Find(p))).Where(x => x.t != null).ToArray();
            int frames = Mathf.RoundToInt((to - from) * 30f);
            var keys = bones.Select(_ => new Keyframe[10][]).ToArray();
            for (int b = 0; b < bones.Length; b++) for (int c = 0; c < 10; c++) keys[b][c] = new Keyframe[frames + 1];
            var last = new Quaternion[bones.Length];
            for (int i = 0; i <= frames; i++)
            {
                take.SampleAnimation(rig, from + i / 30f);
                for (int b = 0; b < bones.Length; b++)
                {
                    var t = bones[b].t;
                    var q = t.localRotation;
                    if (i > 0 && Quaternion.Dot(q, last[b]) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);   // (the short way round)
                    last[b] = q;
                    float[] v = { t.localPosition.x, t.localPosition.y, t.localPosition.z, q.x, q.y, q.z, q.w, t.localScale.x, t.localScale.y, t.localScale.z };
                    for (int c = 0; c < 10; c++) keys[b][c][i] = new Keyframe(i / 30f, v[c]);
                }
            }
            var clip = new AnimationClip { name = name, frameRate = 30f };
            string[] props = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w", "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };
            for (int b = 0; b < bones.Length; b++)
                for (int c = 0; c < 10; c++)
                    clip.SetCurve(bones[b].p, typeof(Transform), props[c], new AnimationCurve(keys[b][c]));
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
        finally { Object.DestroyImmediate(rig); }
    }

    // the plain Beretta lying flat (barrel along z, its right side up), sized to a real 21.7 cm pistol
    public static GameObject Prop(Transform parent, string name)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PistolFbx));
        PrefabUtility.UnpackPrefabInstance(g, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = Vector3.zero;
        g.transform.localRotation = Quaternion.identity;
        var rs = g.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        g.transform.localScale = Vector3.one * (.217f / Mathf.Max(b.size.z, .01f)) * g.transform.localScale.x;
        foreach (var c in g.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        return g;
    }

    // the silo's floor gun (only if you never opened the armory): the same pistol, not the rifle's boxes
    static void SiloFallback()
    {
        var enc = Object.FindFirstObjectByType<SiloEncounter>(FindObjectsInactive.Include);
        if (enc == null || enc.gunProp == null) return;
        var p = enc.gunProp;
        Undo.RegisterFullObjectHierarchyUndo(p.gameObject, "Silo pistol");
        p.name = "PistolPickup";
        foreach (Transform c in p.Cast<Transform>().ToList()) Object.DestroyImmediate(c.gameObject);
        foreach (var r in p.GetComponents<Renderer>()) r.enabled = false;
        var model = Prop(p, "Beretta");
        // (no turn of its own: the silo rolls the root 90 degrees on z, which lays it on its side)
        var box = p.GetComponent<BoxCollider>();
        if (box != null) { box.center = Vector3.zero; box.size = new Vector3(.6f, .6f, .6f); }   // easy to aim at, on the floor
        var it = p.GetComponent<Interactable>();
        if (it != null) { Undo.RecordObject(it, "Silo pistol"); it.prompt = "Take the pistol"; }
    }
}
