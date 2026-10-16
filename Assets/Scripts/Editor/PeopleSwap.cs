using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > People: Use PSX Character Pack
// The street people, the delivery customers and the hell city's lost souls were copies of the same three models.
// This gives each of them their own body from the user's PSX character pack (New Folder/characters-psx, 65 people,
// converted to Humanoid), no two alike in a scene. Only the model changes: the root keeps its scripts and pose, the
// new model gets the old one's Animator controller, root motion, culling, local pose and (for ghosts) the ghost
// material; anything in the scene that pointed at the old model's Animator/transform is pointed at the new one.
public static class PeopleSwap
{
    const string Pack = "Assets/New Folder/characters-psx";

    [MenuItem("HellScape/People: Use PSX Character Pack")]
    public static void Run()
    {
        var all = AssetDatabase.FindAssets("t:Model", new[] { Pack }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.Contains("/Male/") || p.Contains("/Female/")).OrderBy(p => p).ToList();
        bool Uniform(string p) => p.Contains("Police") || p.Contains("Firefighter") || p.Contains("Doctor") || p.Contains("_HM");
        var civilians = all.Where(p => !Uniform(p)).ToList();
        var rng = new System.Random(1304);   // the same cast every time it's run
        List<string> Shuffle(IEnumerable<string> l) => l.OrderBy(_ => rng.Next()).ToList();

        // ---------------------------------------------------------------- the prologue: the morning street
        var pro = SceneManager.GetSceneByName("Prologue");
        bool opened = false;
        if (!pro.isLoaded) { pro = EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity", OpenSceneMode.Additive); opened = true; }
        var proRoot = pro.GetRootGameObjects().First(g => g.name == "Prologue").transform;
        var map = new Dictionary<Object, Object>();
        var deck = Shuffle(civilians);
        string Take(System.Func<string, bool> want)
        {
            var p = deck.FirstOrDefault(want) ?? deck.First();
            deck.Remove(p);
            return p;
        }
        var deliveries = proRoot.Find("Deliveries");
        if (deliveries != null)
        {
            var okafor = deliveries.Find("Mrs Okafor");
            if (okafor != null) Swap(okafor, Take(p => p.Contains("/Female/")), null, map);
            var clinic = deliveries.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Man at the door"));
            if (clinic != null) Swap(clinic, all.First(p => p.Contains("Character_25_Doctor")), null, map);   // the clinic's man: a doctor
        }
        foreach (var w in proRoot.GetComponentsInChildren<PavementWalker>(true))
            Swap(w.transform, Take(p => true), null, map);
        Remap(pro, map);
        Bury();
        EditorSceneManager.MarkSceneDirty(pro);
        EditorSceneManager.SaveScene(pro);
        int proCount = map.Count / 3;
        doomed.Clear();

        // ---------------------------------------------------------------- the hell city: the lost souls
        var main = SceneManager.GetSceneByName("MainGameScene");
        map.Clear();
        var lost = main.GetRootGameObjects().First(g => g.name == "HellWorld").transform.Find("LostSouls (City)");
        var souls = Shuffle(all);   // the dead of the whole city: uniforms too
        int i = 0;
        if (lost != null) foreach (Transform soul in lost)   // gone since 10-15: the city's Lost are CityWalkers' floating women
        {
            var old = soul.GetComponentInChildren<Animator>(true);
            if (old == null) continue;
            var ghostMat = old.GetComponentInChildren<Renderer>(true)?.sharedMaterial;
            Swap(soul, souls[i % souls.Count], ghostMat, map);
            i++;
        }
        Remap(main, map);
        Bury();
        EditorSceneManager.MarkSceneDirty(main);
        EditorSceneManager.SaveScene(main);
        if (opened) EditorSceneManager.CloseScene(pro, true);
        Debug.Log($"People swapped: {proCount} in the prologue, {i} lost souls in the hell city.");
    }

    // Mrs. Okafor, the prologue's first drop, was just whichever woman the shuffle dealt: she is one of the pack's grey-haired
    // old ladies now (pink cardigan), a touch shorter, stooped, with a walking stick, and slow on her way back in.
    [MenuItem("HellScape/People: Mrs Okafor is an old lady")]
    public static void OldLady()
    {
        var pro = SceneManager.GetSceneByName("Prologue");
        if (!pro.isLoaded) { Debug.LogError("Open the Prologue scene first."); return; }
        var proRoot = pro.GetRootGameObjects().First(g => g.name == "Prologue").transform;
        var okafor = proRoot.Find("Deliveries/Mrs Okafor");
        if (okafor == null) { Debug.LogError("No 'Mrs Okafor' under Prologue/Deliveries."); return; }
        string path = AssetDatabase.FindAssets("Character_29_Female t:Model", new[] { Pack }).Select(AssetDatabase.GUIDToAssetPath).First(p => p.EndsWith("/Character_29_Female.fbx"));
        var map = new Dictionary<Object, Object>();
        Swap(okafor, path, null, map);
        Remap(pro, map);
        Bury();
        var model = okafor.GetComponentInChildren<Animator>(true);
        model.transform.localScale *= 0.93f;
        if (model.GetComponent<ElderStoop>() == null) model.gameObject.AddComponent<ElderStoop>();
        var run = proRoot.GetComponentInChildren<DeliveryRun>(true);
        if (run != null && run.stops.Length > 0) { run.stops[0].exitSpeed = 0.85f; EditorUtility.SetDirty(run); }
        EditorSceneManager.MarkSceneDirty(pro);
        EditorSceneManager.SaveScene(pro);
        Debug.Log("Mrs. Okafor is an old lady now (" + System.IO.Path.GetFileName(path) + ").");
    }

    // Elias (the prologue's fleeing killer, and the silo's chase / boss / cutscenes) was the Jason model: he is the
    // pack's Character_Killer now, in both scenes. MainGameScene open; the Prologue is opened alongside and saved.
    [MenuItem("HellScape/People: Elias is Character_Killer")]
    public static void Elias() => Debug.Log(EliasSwap());

    public static string EliasSwap()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        string path = AssetDatabase.FindAssets("Character_Killer t:Model", new[] { Pack }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.EndsWith("/Character_Killer.fbx"));
        if (path == null) return "No Character_Killer.fbx in the pack.";
        var mi = (ModelImporter)AssetImporter.GetAtPath(path);
        if (mi.animationType != ModelImporterAnimationType.Human)
        {
            // (a Mixamo skeleton like the rest of the pack: Humanoid, so Killer.controller's clips play on it)
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.SaveAndReimport();
        }
        var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman) return "Character_Killer did not import as a valid Humanoid.";

        var main = SceneManager.GetSceneByName("MainGameScene");
        if (!main.isLoaded) return "Open MainGameScene first.";
        var pro = SceneManager.GetSceneByName("Prologue");
        bool opened = false;
        if (!pro.isLoaded) { pro = EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity", OpenSceneMode.Additive); opened = true; }
        var log = new List<string>();
        foreach (var scene in new[] { main, pro })
        {
            var roots = scene.GetRootGameObjects();
            var who = roots.SelectMany(r => r.GetComponentsInChildren<KillerBoss>(true)).Select(k => k.transform)
                .Concat(roots.SelectMany(r => r.GetComponentsInChildren<FleeingKiller>(true)).Select(k => k.transform)).ToList();
            var map = new Dictionary<Object, Object>();
            foreach (var root in who)
            {
                var was = root.GetComponentInChildren<Animator>(true);
                if (was == null || was.avatar == avatar) continue;   // already him
                Swap(root, path, null, map, 0f);
            }
            Remap(scene, map);
            Bury();
            if (map.Count > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            log.Add($"{scene.name}: {map.Count / 3} of {who.Count} swapped");
        }
        if (opened) EditorSceneManager.CloseScene(pro, true);
        return "Elias is Character_Killer. " + string.Join("; ", log);
    }

    // The prologue's stab through the bedroom door was played by the killer baked into "machete stab.fbx" (a different
    // man from Elias now). This takes that killer's half of the clip as a Humanoid clip (a copy of the FBX, imported
    // Humanoid: the avatar maps its first skeleton, the killer's) and adds it to PrologueKiller.controller as
    // "StabOver", which PrologueDirector plays on Elias himself, stood on the rig's origin.
    const string StabFbx = "Assets/New Folder/jason-voorhees-machete-stab-animation/source/machete stab.fbx";
    const string StabCopy = "Assets/Enemies/Elias Boss/Stab Over Derek.fbx";
    const string PrologueKillerController = "Assets/Materials/Prologue/PrologueKiller.controller";

    [MenuItem("HellScape/People: Elias Stabs Derek Himself")]
    public static void EliasStabMenu() => Debug.Log(EliasStab());

    public static string EliasStab()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(StabCopy) == null && !AssetDatabase.CopyAsset(StabFbx, StabCopy)) return "Could not copy " + StabFbx;
        var mi = (ModelImporter)AssetImporter.GetAtPath(StabCopy);
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.importCameras = false;
        mi.importLights = false;
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            // everything stays in the pose, as it is in the file: where he stands from the rig's origin, which way he faces
            c.name = "EliasStabOver";
            c.loopTime = false;
            c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true;
            c.keepOriginalOrientation = c.keepOriginalPositionY = c.keepOriginalPositionXZ = true;
        }
        mi.clipAnimations = clips;
        mi.SaveAndReimport();
        // (the automatic mapping takes the joint above both skeletons as the hips: it is the killer's pelvis)
        var desc = mi.humanDescription;
        var bones = desc.human;
        for (int i = 0; i < bones.Length; i++)
            if (bones[i].humanName == "Hips" && bones[i].boneName != "pelvis") { bones[i].boneName = "pelvis"; desc.human = bones; mi.humanDescription = desc; mi.SaveAndReimport(); break; }

        var avatar = AssetDatabase.LoadAllAssetsAtPath(StabCopy).OfType<Avatar>().FirstOrDefault();
        var clip = AssetDatabase.LoadAllAssetsAtPath(StabCopy).OfType<AnimationClip>().FirstOrDefault(c => c.name == "EliasStabOver");
        if (avatar == null || !avatar.isValid || !avatar.isHuman || clip == null) return "The stab did not import as a Humanoid clip.";
        string hips = avatar.humanDescription.human.FirstOrDefault(h => h.humanName == "Hips").boneName;
        if (hips == null || hips.EndsWith("_2")) return "The avatar mapped Derek's skeleton (" + hips + "), not the killer's.";

        var ac = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(PrologueKillerController);
        if (ac == null) return "No PrologueKiller.controller.";
        var sm = ac.layers[0].stateMachine;
        var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "StabOver") ?? sm.AddState("StabOver");
        st.motion = clip;
        st.speed = 1f;
        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        return $"Elias's own stab: clip {clip.length:0.00} s on PrologueKiller.controller as StabOver (avatar hips = {hips}).";
    }

    // Derek was the "prep guy" baked into both two-person stab FBXs: the victim in machete stab.fbx (its second
    // skeleton, bones *_2) on the prologue's bedroom floor, and the killer in kitchenknife multistab.fbx (its first
    // skeleton) on the CCTV tape in the silo. He is the pack's Character_20_Police now: his half of each clip is taken
    // as a Humanoid clip (a copy of the FBX whose avatar maps his skeleton) and played on the police model, stood on
    // the rig's origin; the rig's prep-guy meshes are hidden. MainGameScene open; the Prologue is opened alongside.
    const string DerekDir = "Assets/Materials/Derek";
    const string KnifeFbx = "Assets/New Folder/tommy-jarvis-multiple-stab-animation/source/kitchenknife multistab.fbx";

    [MenuItem("HellScape/People: Derek is Character_20_Police")]
    public static void DerekMenu() => Debug.Log(DerekSwap());

    public static string DerekSwap()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        string police = AssetDatabase.FindAssets("Character_20_Police t:Model", new[] { Pack }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.EndsWith("/Male/Character_20_Police.fbx"));
        if (police == null) return "No Male/Character_20_Police.fbx in the pack.";
        var pavatar = AssetDatabase.LoadAllAssetsAtPath(police).OfType<Avatar>().FirstOrDefault();
        if (pavatar == null || !pavatar.isHuman) return "Character_20_Police is not Humanoid.";
        if (!AssetDatabase.IsValidFolder(DerekDir)) AssetDatabase.CreateFolder("Assets/Materials", "Derek");

        var dies = DerekController(StabFbx, "Derek Stabbed", "_2", out string err);   // the prologue: the second skeleton
        if (dies == null) return err;
        var kills = DerekController(KnifeFbx, "Derek Kills Tomas", "", out err);       // the tape: the first skeleton
        if (kills == null) return err;

        var main = SceneManager.GetSceneByName("MainGameScene");
        if (!main.isLoaded) return "Open MainGameScene first.";
        var pro = SceneManager.GetSceneByName("Prologue");
        bool opened = false;
        if (!pro.isLoaded) { pro = EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity", OpenSceneMode.Additive); opened = true; }
        var log = new List<string>();

        // the prologue: PrologueDirector drives derekRig itself (frame 0 behind the door, then the stab): his model
        // takes that job, the old rig goes (its killer half has been Elias's own StabOver for a while)
        var d = pro.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PrologueDirector>(true)).FirstOrDefault();
        if (d != null && d.derekRig != null && d.derekRig.avatar != pavatar)
        {
            var rig = d.derekRig.gameObject;
            var anim = Police(police, pavatar, rig.transform.parent, rig.transform.localPosition, rig.transform.localRotation, dies);
            Undo.RecordObject(d, "Derek");
            d.derekRig = anim;
            d.killerInRig = new GameObject[0];
            EditorUtility.SetDirty(d);
            Undo.DestroyObjectImmediate(rig);
            EditorSceneManager.MarkSceneDirty(pro);
            EditorSceneManager.SaveScene(pro);
            log.Add("prologue swapped");
        }
        else log.Add("prologue: " + (d == null ? "no PrologueDirector" : "already him"));

        // the tape: a child of the rig, so StabCutscene's actors.SetActive shows and hides him; enabling restarts his
        // Animator on its one state in the same frame the rig's own clip is played from 0
        var tape = main.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StabCutscene>(true)).FirstOrDefault(s => s.name == "Cutscene - Derek kills Tomas");
        if (tape != null && tape.actors != null && !tape.actors.GetComponentsInChildren<Animator>(true).Any(a => a.avatar == pavatar))
        {
            foreach (var r in tape.actors.GetComponentsInChildren<Renderer>(true).Where(r => r.name.Contains("Counselor")))
            { Undo.RecordObject(r.gameObject, "Derek"); r.gameObject.SetActive(false); }
            Police(police, pavatar, tape.actors.transform, Vector3.zero, Quaternion.identity, kills);
            EditorSceneManager.MarkSceneDirty(main);
            EditorSceneManager.SaveScene(main);
            log.Add("CCTV tape swapped");
        }
        else log.Add("CCTV tape: " + (tape == null ? "not found" : "already him"));

        DerekAnimationRepair.ConfigureTapeProp();

        if (opened) EditorSceneManager.CloseScene(pro, true);
        return "Derek is Character_20_Police. " + string.Join("; ", log);
    }

    // the police model on the rig's origin, a man's height, playing ctrl's one state
    static Animator Police(string path, Avatar avatar, Transform parent, Vector3 localPos, Quaternion localRot, RuntimeAnimatorController ctrl)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent.gameObject.scene);
        g.name = "Derek (Character_20_Police)";
        var rs = g.GetComponentsInChildren<Renderer>(true);   // measured before parenting: an inactive parent has no bounds
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        float raw = b.size.y;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localRotation = localRot;
        g.transform.localScale = Vector3.one * (1.78f / Mathf.Max(0.01f, raw) / Mathf.Max(0.0001f, parent.lossyScale.y));
        var anim = g.GetComponent<Animator>();
        if (anim == null) anim = g.AddComponent<Animator>();
        anim.avatar = avatar;
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (var smr in g.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
        Undo.RegisterCreatedObjectUndo(g, "Derek");
        return anim;
    }

    // Derek's half of a two-person FBX as a Humanoid clip on a controller with one state, "Stab"
    static RuntimeAnimatorController DerekController(string src, string name, string suffix, out string err)
    {
        err = null;
        string copy = DerekDir + "/" + name + ".fbx";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(copy) == null && !AssetDatabase.CopyAsset(src, copy)) { err = "Could not copy " + src; return null; }
        var mi = (ModelImporter)AssetImporter.GetAtPath(copy);
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.importCameras = false;
        mi.importLights = false;
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.name = name;
            c.loopTime = false;
            c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true;
            c.keepOriginalOrientation = c.keepOriginalPositionY = c.keepOriginalPositionXZ = true;
        }
        mi.clipAnimations = clips;
        mi.SaveAndReimport();
        // the automatic mapping takes the first skeleton (and the joint above both as the hips): his pelvis is the
        // hips, and for the second skeleton every bone points at its name + suffix instead
        {
            var desc = mi.humanDescription;
            var names = new HashSet<string>(desc.skeleton.Select(s => s.name));
            var bones = desc.human;
            bool changed = false;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i].humanName == "Hips" && bones[i].boneName != "pelvis" + suffix) { bones[i].boneName = "pelvis" + suffix; changed = true; }
                else if (suffix != "" && !bones[i].boneName.EndsWith(suffix) && names.Contains(bones[i].boneName + suffix)) { bones[i].boneName += suffix; changed = true; }
            }
            if (changed) { desc.human = bones; mi.humanDescription = desc; mi.SaveAndReimport(); }
        }
        DerekAnimationRepair.ConfigureReferencePose(mi, suffix);
        mi.SaveAndReimport();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(copy).OfType<Avatar>().FirstOrDefault();
        var clip = AssetDatabase.LoadAllAssetsAtPath(copy).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
        if (avatar == null || !avatar.isValid || !avatar.isHuman || clip == null) { err = name + " did not import as a Humanoid clip."; return null; }
        string hips = avatar.humanDescription.human.FirstOrDefault(h => h.humanName == "Hips").boneName;
        if (hips != "pelvis" + suffix) { err = name + ": the avatar mapped the wrong man (hips = " + hips + ")."; return null; }

        string ctrlPath = DerekDir + "/" + name + ".controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        var ac = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        ac.layers[0].stateMachine.AddState("Stab").motion = clip;
        AssetDatabase.SaveAssets();
        return ac;
    }

    // replace the model under root (the child holding the Animator) with the pack model at modelPath
    static void Swap(Transform root, string modelPath, Material overrideMat, Dictionary<Object, Object> map, float vary = 0.08f)
    {
        var oldAnim = root.GetComponentInChildren<Animator>(true);
        if (oldAnim == null) return;
        Transform old = oldAnim.transform;
        while (old.parent != root) old = old.parent;

        var oldRs = old.GetComponentsInChildren<Renderer>(true);
        float height = 1.75f;   // as tall as the one it replaces, if that measures sensibly
        if (oldRs.Length > 0)
        {
            Bounds ob = oldRs[0].bounds; foreach (var r in oldRs) ob.Encapsulate(r.bounds);
            if (ob.size.y > 1.45f && ob.size.y < 2.4f) height = ob.size.y;   // (a child's model gets an adult's height: the pack has no children)
        }
        var shadows = oldRs.Length > 0 ? oldRs[0].shadowCastingMode : UnityEngine.Rendering.ShadowCastingMode.On;
        bool receive = oldRs.Length == 0 || oldRs[0].receiveShadows;

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var g = (GameObject)PrefabUtility.InstantiatePrefab(model, root.gameObject.scene);
        g.transform.SetParent(root, false);
        g.transform.localPosition = old.localPosition;
        g.transform.localRotation = old.localRotation;
        g.transform.SetSiblingIndex(old.GetSiblingIndex());
        g.SetActive(old.gameObject.activeSelf);

        // to a person's height (the pack's are ~4.8 units tall), a touch of variety
        var nrs = g.GetComponentsInChildren<Renderer>(true);
        float raw = 4.8f;
        if (nrs.Length > 0) { Bounds nb = nrs[0].bounds; foreach (var r in nrs) nb.Encapsulate(r.bounds); raw = nb.size.y / Mathf.Max(0.0001f, g.transform.lossyScale.y); }
        float want = height * (1f + (Random.value - 0.5f) * vary);
        g.transform.localScale = Vector3.one * (want / Mathf.Max(0.01f, raw));

        var anim = g.GetComponent<Animator>();
        if (anim == null) anim = g.AddComponent<Animator>();
        anim.runtimeAnimatorController = oldAnim.runtimeAnimatorController;
        anim.avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
        anim.applyRootMotion = oldAnim.applyRootMotion;
        anim.cullingMode = oldAnim.cullingMode;
        anim.updateMode = oldAnim.updateMode;

        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
        {
            if (overrideMat != null) r.sharedMaterials = Enumerable.Repeat(overrideMat, r.sharedMaterials.Length).ToArray();
            r.shadowCastingMode = shadows;
            r.receiveShadows = receive;
        }

        map[oldAnim] = anim;
        map[old] = g.transform;
        map[old.gameObject] = g;
        Undo.RegisterCreatedObjectUndo(g, "People");
        doomed.Add(old.gameObject);   // destroyed after the scene's references are moved over
    }

    static readonly List<GameObject> doomed = new List<GameObject>();

    static void Bury()
    {
        foreach (var g in doomed) if (g != null) Undo.DestroyObjectImmediate(g);
        doomed.Clear();
    }

    // everything in the scene that pointed at an old model now points at its replacement
    static void Remap(Scene scene, Dictionary<Object, Object> map)
    {
        if (map.Count == 0) return;
        var byId = map.ToDictionary(kv => kv.Key.GetInstanceID(), kv => kv.Value);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    int id = it.objectReferenceInstanceIDValue;
                    if (id != 0 && byId.TryGetValue(id, out var to)) { it.objectReferenceValue = to; changed = true; }
                }
                if (changed) so.ApplyModifiedProperties();
            }
    }
}
