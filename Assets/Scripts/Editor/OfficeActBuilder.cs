using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds the office act into the open scene and wires it to the existing player, car, UI and ending:
//   city entrance (door + beacon, across from the garage) -> the office model (3 puzzles, ghosts with multiple-choice
//   talks, clues; the CCTV replays Derek killing Tomas) -> the silo complex model (chase, elevator, pushed into the
//   silo tower) -> silo floor (Derek's death, boss, choice, flood, colleagues) -> back to the car -> blood wave -> bridge.
// Menu: HellScape > Build Office Act. Re-running deletes the previous "OfficeAct" and builds it fresh.
// The interiors live far off the map (teleport doors), so they never clash with the city.
// Needs the two placed models: the office rooms (OfficeModelName) and the silo complex (ComplexModelName).
public static class OfficeActBuilder
{
    const string MatDir = "Assets/Materials/OfficeAct";
    const string JasonFbx = "Assets/New Folder/jason-manhattan-no-mask/source/Jason_Manhattan.fbx";
    const string AkFbx = "Assets/New Folder/ak74u-free-animation/source/AK74U.fbx";
    const string MacheteFbx = "Assets/New Folder/jason-voorhees-machete-stab-animation/source/machete stab.fbx";          // the killer stabs Derek
    const string KitchenKnifeFbx = "Assets/New Folder/tommy-jarvis-multiple-stab-animation/source/kitchenknife multistab.fbx"; // Derek stabs Tomas
    const string CivilianController = "Assets/Enemies/Civilian.controller";
    const string OfficeModelName = "Sketchfab_2023_11_11_16_15_44";
    const string ComplexModelName = "BloodPool_low";
    static readonly Vector3 SiloOrigin = new Vector3(1000f, 0f, 1100f);   // centre of the complex's silo tower
    // the office door: the glass doors on the rounded corner of the user's office building, found by looking at
    // them from the street where the car is parked
    const string EntranceBuildingName = "NewOfficeBuilding Entrance";
    static readonly Vector3 EntranceProbe = new Vector3(8.9f, -2.3f, -42.5f);
    static readonly Vector3 EntranceDir = new Vector3(0.58f, 0f, -0.81f);
    const float PlayerScale = 0.85f;
    const string FuseBoxModel = "Fuse";                                          // the user's fuse box (root with a PowerBox child)
    const string KesslerDoorModel = "4bfaca4c4a794e9393a413e88dd5611a.fbx";     // the user's door into Kessler's wing (scene root)

    // things inside OfficeAct the user has moved by hand: a rebuild puts them back where they were left
    static readonly string[] KeepPlacement =
    {
        "OfficeEntrance (City)/Sign", "OfficeEntrance (City)/Label",
        "Office/Keypad", "Office/Body#0", "Office/Body#1", "Office/BloodWriting",
        "SiloComplex/PushTrigger",
    };
    const string DoorFbx = "Assets/New Folder/room-door-animation/source/4bfaca4c4a794e9393a413e88dd5611a.fbx.fbx";
    static readonly Vector3 DoorStage = new Vector3(-3000f, -500f, -3000f);   // far off the map, nothing else in view        // ~1.7 m tall: eye level with the office ghosts
    const string AnimDir = MatDir + "/Anim";                                         // humanoid copies of the Mixamo clips
    const string DamnedPrefab = "Assets/Enemies/Scary Zombie Pack/PrimeZombie.prefab";   // what Kessler becomes
    const string ZombieAttackFbx = "Assets/Enemies/Scary Zombie Pack/zombie attack.fbx";
    const string ZombieRunFbx = "Assets/Enemies/Scary Zombie Pack/zombie run.fbx";
    static readonly string[] FloatFbx = { "Assets/New Folder/Floating Pose.fbx", "Assets/New Folder/Floating Pose 2.fbx" };

    const float R = 7f;     // silo tower radius (the model's wall sits at 6.3-8.3)
    const float RigScale = 2.05f / 5.17f;   // the Jason FBXs are ~5 units tall

    static GameObject player;
    static Camera cam;
    static PlayerHealth health;
    static ScreenFader fader;
    static DialogueBox dialog;
    static RectTransform canvas;
    static TMP_FontAsset font;
    static Material mGhost, mWood, mMetal, mScreen, mPaper, mBlood, mFuse, mGold, mRust, mBloodSurface, mWave, mBeacon, mBlack, mTube, mCamButton;

    [MenuItem("HellScape/Build Office Act")]
    public static void Build()
    {
        // the models' walls are one-sided: without this, probes pass through a wall from behind and puzzles
        // end up placed on the far side of it
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        // (the rebuilt complex gets its longer chase back, and the rebuilt UI its modern look)
        try { BuildAll(); SoundManagerMenu.SetMusicAreas(); Debug.Log(SiloChaseBuilder.Extend()); ModernUIBuilder.Menu(); }
        finally { Physics.queriesHitBackfaces = backfaces; }
    }

    static void BuildAll()
    {
        doorCtrl = null;
        FindSceneRefs();
        SizePlayer();
        Materials();

        var old = GameObject.Find("OfficeAct");
        var kept = RecordPlacements(old != null ? old.transform : null);
        if (old != null) Object.DestroyImmediate(old);
        CleanupPlayerAddons();
        EnsureColliders(GameObject.Find(OfficeModelName).transform);
        complexModel = GameObject.Find(ComplexModelName).transform;
        EnsureColliders(complexModel);
        ZombieFreeInteriors();
        var entranceBuilding = GameObject.Find(EntranceBuildingName);
        if (entranceBuilding != null) EnsureColliders(entranceBuilding.transform);
        Physics.SyncTransforms();

        var root = new GameObject("OfficeAct").transform;

        var ui = BuildUI();
        var cityReturn = new GameObject("CityReturnPoint").transform;
        cityReturn.SetParent(root);
        var beacon = BuildCityEntrance(root, cityReturn, out Transform officeArrival, out Teleporter toOffice);
        var complexArrival = new GameObject("ComplexArrival").transform;
        complexArrival.SetParent(root);
        var chase = BuildChaseKiller(root);

        var gun = BuildGun(ui);
        ui.choices.lockDuring = new Behaviour[] { player.GetComponent<FirstPersonController>(), player.GetComponentInChildren<PunchController>(true), gun };
        var lockAll = ui.choices.lockDuring.Append(cam.GetComponent<PlayerInteractor>()).ToArray();
        var flashback = BuildFlashback(root, lockAll);
        BuildOffice(root, officeArrival, cityReturn, complexArrival, chase, flashback);
        toOffice.destination = officeArrival;
        var enc = BuildSilo(root, cityReturn, gun, ui, lockAll);
        BuildComplex(root, complexArrival, enc, chase);
        BuildWaveAndCar(root, beacon);
        BuildTurningCutscene(enc, lockAll);

        BuildLostSouls();

        BuildCheckpoints(root, ui, cityReturn, complexArrival, chase, enc);

        // every doorway plays the Resident Evil door
        var transition = BuildDoorTransition(root, lockAll);
        if (transition != null)
            foreach (var tp in root.GetComponentsInChildren<Teleporter>(true)) tp.transition = transition;

        ApplyPlacements(root, kept);

        EditorSceneManager.MarkSceneDirty(player.scene);
        Debug.Log("HellScape: office act built. Save the scene to keep it.");
    }

    // ---------------------------------------------------------------- scene refs

    static void FindSceneRefs()
    {
        player = GameObject.Find("FirstPersonController");
        cam = player.GetComponentInChildren<Camera>(true);
        health = player.GetComponent<PlayerHealth>();
        fader = Object.FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);
        dialog = Object.FindFirstObjectByType<DialogueBox>(FindObjectsInactive.Include);
        canvas = (RectTransform)GameObject.Find("StoryCanvas").transform;
        font = dialog.text.font;
    }

    // the street spawner follows you everywhere; the office and the silo complex are off limits to it
    static void ZombieFreeInteriors()
    {
        var spawner = Object.FindFirstObjectByType<ZombieSpawner>(FindObjectsInactive.Include);
        if (spawner == null) return;
        Bounds Around(string name)
        {
            var rs = GameObject.Find(name).GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            b.Expand(20f);
            return b;
        }
        Undo.RecordObject(spawner, "Zombie-free interiors");
        spawner.interiors = new[] { Around(OfficeModelName), Around(ComplexModelName) };
        EditorUtility.SetDirty(spawner);
    }

    // "Office/Body#1" = the second child of Office called Body
    static Transform FindKept(Transform root, string path)
    {
        Transform t = root;
        foreach (var part in path.Split('/'))
        {
            if (t == null) return null;
            string name = part; int index = 0;
            int hash = part.IndexOf('#');
            if (hash >= 0) { name = part.Substring(0, hash); index = int.Parse(part.Substring(hash + 1)); }
            Transform found = null; int seen = 0;
            foreach (Transform c in t)
                if (c.name == name && seen++ == index) { found = c; break; }
            t = found;
        }
        return t;
    }

    static System.Collections.Generic.Dictionary<string, (Vector3 pos, Quaternion rot, Vector3 scale)> RecordPlacements(Transform oldRoot)
    {
        var d = new System.Collections.Generic.Dictionary<string, (Vector3, Quaternion, Vector3)>();
        if (oldRoot == null) return d;
        foreach (var path in KeepPlacement)
        {
            var t = FindKept(oldRoot, path);
            if (t != null) d[path] = (t.position, t.rotation, t.localScale);
        }
        return d;
    }

    static void ApplyPlacements(Transform root, System.Collections.Generic.Dictionary<string, (Vector3 pos, Quaternion rot, Vector3 scale)> kept)
    {
        foreach (var kv in kept)
        {
            var t = FindKept(root, kv.Key);
            if (t == null) continue;
            t.SetPositionAndRotation(kv.Value.pos, kv.Value.rot);
            t.localScale = kv.Value.scale;
        }
        // the blood under each body follows it
        var office = root.Find("Office");
        for (int i = 0; i < 2 && office != null; i++)
        {
            var body = FindKept(root, "Office/Body#" + i);
            var pool = FindKept(root, "Office/BodyBlood#" + i);
            if (body != null && pool != null) pool.position = new Vector3(body.position.x, OfficeFloor + 0.01f, body.position.z);
        }
    }

    static GameObject SceneRoot(string name, System.Func<GameObject, bool> match = null) =>
        player.scene.GetRootGameObjects().FirstOrDefault(r => r.name == name && (match == null || match(r)));

    // the room-door model's own take as an "Open" state (shared by Kessler's door and the door transition)
    static RuntimeAnimatorController doorCtrl;
    static float doorClipLength;

    static RuntimeAnimatorController DoorOpenController(out float length)
    {
        // once per build: remaking the asset would break whoever got the first one
        if (doorCtrl != null) { length = doorClipLength; return doorCtrl; }
        var mi = (ModelImporter)AssetImporter.GetAtPath(DoorFbx);
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips) { c.name = "DoorOpen"; c.loopTime = false; }
        mi.clipAnimations = clips;
        mi.importLights = false;            // its hemi light is a directional: it would light the whole world
        mi.importCameras = false;
        mi.SaveAndReimport();
        var clip = AssetDatabase.LoadAllAssetsAtPath(DoorFbx).OfType<AnimationClip>().First(c => c.name == "DoorOpen");
        length = clip.length;
        if (!AssetDatabase.IsValidFolder(AnimDir)) AssetDatabase.CreateFolder(MatDir, "Anim");
        string path = AnimDir + "/DoorOpen.controller";
        AssetDatabase.DeleteAsset(path);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
        ac.layers[0].stateMachine.AddState("Open").motion = clip;
        doorCtrl = ac;
        doorClipLength = length;
        return ac;
    }

    // shorter player: the interact ray starts at eye level with the ghosts and colleagues
    static void SizePlayer()
    {
        Undo.RecordObject(player.transform, "Player height");
        player.transform.localScale = Vector3.one * PlayerScale;
        var fpc = player.GetComponent<FirstPersonController>();
        Undo.RecordObject(fpc, "Player height");
        fpc.crouchHeight = PlayerScale * 0.65f;
        EditorUtility.SetDirty(fpc);
    }

    static void CleanupPlayerAddons()
    {
        foreach (var n in new[] { "AK74U (ViewModel)" })
        {
            var t = cam.transform.Find(n);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        foreach (var c in cam.GetComponents<GunController>()) Object.DestroyImmediate(c);
        foreach (var c in cam.GetComponents<PlayerInteractor>()) Object.DestroyImmediate(c);
        foreach (var c in cam.GetComponents<AudioSource>()) Object.DestroyImmediate(c);
        foreach (var n in new[] { "InteractPrompt", "AmmoText", "BossBar", "ChoicePanel", "InBloodOverlay", "AirBar", "UnderBloodTint", "ChoiceDialogue", "CctvOverlay", "CheckpointNotice", "LetterboxTop", "LetterboxBottom" })
        {
            var t = canvas.Find(n);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
    }

    // ---------------------------------------------------------------- helpers

    static void Materials()
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Materials", "OfficeAct");
        mGhost = GhostMat();
        mWood = Mat("Office_Wood", new Color(0.26f, 0.18f, 0.12f));
        mMetal = Mat("Office_Metal", new Color(0.3f, 0.3f, 0.32f), metallic: 0.7f, smooth: 0.4f);
        mScreen = Mat("Office_Screen", new Color(0.02f, 0.04f, 0.03f), new Color(0.05f, 0.35f, 0.2f));
        mPaper = Mat("Office_Paper", new Color(0.85f, 0.82f, 0.7f));
        mBlood = Mat("Office_Blood", new Color(0.28f, 0f, 0f), smooth: 0.85f);
        mFuse = Mat("Office_Fuse", new Color(0.9f, 0.7f, 0.1f), new Color(0.5f, 0.35f, 0.02f));
        mGold = Mat("Office_Key", new Color(0.85f, 0.65f, 0.2f), new Color(0.35f, 0.25f, 0.05f), metallic: 0.9f, smooth: 0.7f);
        mRust = Mat("Silo_Rust", new Color(0.3f, 0.2f, 0.14f), metallic: 0.5f, smooth: 0.25f);
        mBloodSurface = Mat("Silo_BloodSurface", new Color(0.3f, 0f, 0f), new Color(0.08f, 0f, 0f), smooth: 0.95f);
        mWave = Mat("BloodWave", new Color(0.08f, 0f, 0f), new Color(0.03f, 0f, 0f), smooth: 0.95f);   // near-black blood: reads against the red sky
        mBeacon = Mat("Office_Beacon", new Color(0.5f, 0f, 0f), new Color(2f, 0.05f, 0.05f));
        mBlack = Mat("Office_Black", new Color(0.03f, 0.03f, 0.03f));
        mCamButton = Mat("Office_CamButton", new Color(0.25f, 0.6f, 0.35f), new Color(0.2f, 1.1f, 0.45f));
        mTube = Mat("Silo_RedTube", new Color(0.5f, 0.02f, 0.02f), new Color(6f, 0.15f, 0.08f));
    }

    // see-through, glowing pale blue: the office ghosts
    static Material GhostMat()
    {
        string path = MatDir + "/Ghost.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);   // additive-ish glow
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetColor("_BaseColor", new Color(0.55f, 0.85f, 1f, 0.35f));
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Mat(string name, Color c, Color? emission = null, float metallic = 0f, float smooth = 0.2f)
    {
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smooth);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m, bool collider = true)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static Transform Node(Transform parent, string name, Vector3 pos, float yaw = 0f)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }

    static Light Lamp(Transform p, Vector3 pos, Color c, float intensity, float range, bool flicker = false)
    {
        var g = new GameObject("Light");
        g.transform.SetParent(p, false);
        g.transform.localPosition = pos;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        if (flicker) g.AddComponent<LightFlicker>();
        return l;
    }

    // world text; `facing` is the direction the reader looks when reading it
    static TextMeshPro Label(Transform p, string text, Vector3 pos, Vector3 facing, float size, Color c, float width = 2f)
    {
        var g = new GameObject("Label");
        g.transform.SetParent(p, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.LookRotation(facing);
        var t = g.AddComponent<TextMeshPro>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = c;
        t.alignment = TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta = new Vector2(width, 1f);
        return t;
    }

    static Interactable Inter(GameObject g, string prompt, string line = null, string give = null, string req = null,
                              bool once = true, bool hide = false, string locked = null, bool consume = true)
    {
        var it = g.AddComponent<Interactable>();
        it.prompt = prompt;
        it.line = line;
        it.giveItem = give;
        it.requiredItem = req;
        it.consumeItem = consume;
        it.once = once;
        it.hideOnUse = hide;
        if (locked != null) it.lockedLine = locked;
        return it;
    }

    static void On(UnityEvent e, UnityAction a) => UnityEventTools.AddVoidPersistentListener(e, a);
    static void On(UnityEvent e, UnityAction<string> a, string arg) => UnityEventTools.AddStringPersistentListener(e, a, arg);
    static void On(UnityEvent e, UnityAction<bool> a, bool arg) => UnityEventTools.AddBoolPersistentListener(e, a, arg);
    static void On(UnityEvent e, UnityAction<int> a, int arg) => UnityEventTools.AddIntPersistentListener(e, a, arg);
    static void Say(UnityEvent e, string lines) => On(e, dialog.Say, lines);
    static void SetActive(UnityEvent e, GameObject g, bool on) => On(e, g.SetActive, on);
    static void SetEnabled(UnityEvent e, Behaviour b, bool on) =>
        On(e, (UnityAction<bool>)Delegate.CreateDelegate(typeof(UnityAction<bool>), b, "set_enabled"), on);

    static Teleporter Door(Transform parent, string name, Vector3 pos, Vector3 size, Quaternion rot, Material m, Transform destination, string arriveLine)
    {
        var g = Box(parent, name, pos, size, m);
        g.transform.localRotation = rot;
        var tp = g.AddComponent<Teleporter>();
        tp.player = player.transform;
        tp.destination = destination;
        tp.fader = fader;
        tp.arriveLine = arriveLine;
        return tp;
    }

    static GameObject Prop(Transform parent, string assetPath, Vector3 pos, float scale, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null) return null;
        var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        g.transform.localScale = Vector3.one * scale;
        return g;
    }

    static GameObject CopyOf(string sceneName, Transform parent, Vector3 pos, float yaw)
    {
        // the original (a day-world civilian), never one of the blue ghost / lost-soul copies made from it earlier:
        // copying a copy handed Maya the ghost material and no walk controller
        bool IsGhostCopy(Transform t) =>
            t.GetComponentsInChildren<Renderer>(true).Any(r => r.sharedMaterials.Any(m => m != null && m.name == "Ghost"));
        var src = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.name == sceneName)
            .OrderBy(t => IsGhostCopy(t) ? 1 : 0)
            .FirstOrDefault();
        if (src == null) return null;
        var g = Object.Instantiate(src.gameObject, parent);
        g.name = sceneName;
        g.SetActive(true);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return g;
    }

    // ---------------------------------------------------------------- UI

    class UI
    {
        public TextMeshProUGUI prompt, ammo;
        public GameObject bossBar, choice, inBlood, airBar;
        public RectTransform bossFill, airFill;
        public Image airFillImage;
        public CanvasGroup underBlood;
        public ChoiceDialogue choices;
        public TextMeshProUGUI checkpoint;
    }

    static TextMeshProUGUI UIText(string name, string text, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize)
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(canvas, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var t = g.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    static UI BuildUI()
    {
        var ui = new UI();
        int below = fader.transform.GetSiblingIndex();   // keep all of it under the black fade

        ui.prompt = UIText("InteractPrompt", "[F] Inspect", new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(700f, 40f), 26f);
        ui.prompt.gameObject.SetActive(false);

        ui.ammo = UIText("AmmoText", "30 / ∞", new Vector2(1f, 0f), new Vector2(-120f, 50f), new Vector2(220f, 50f), 34f);
        ui.ammo.alignment = TextAlignmentOptions.Right;
        ui.ammo.gameObject.SetActive(false);

        var bar = UIText("BossBar", "???", new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(640f, 36f), 24f);
        bar.color = new Color(0.9f, 0.2f, 0.15f);
        var bg = new GameObject("Back", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(bar.transform, false);
        var bgr = (RectTransform)bg.transform;
        bgr.anchoredPosition = new Vector2(0f, -30f);
        bgr.sizeDelta = new Vector2(600f, 16f);
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bg.transform, false);
        var fr = (RectTransform)fill.transform;
        fr.anchorMin = new Vector2(0f, 0f);
        fr.anchorMax = new Vector2(1f, 1f);
        fr.pivot = new Vector2(0f, 0.5f);
        fr.offsetMin = new Vector2(2f, 2f);
        fr.offsetMax = new Vector2(-2f, -2f);
        fill.GetComponent<Image>().color = new Color(0.6f, 0.02f, 0.02f);
        ui.bossBar = bar.gameObject;
        ui.bossFill = fr;
        ui.bossBar.SetActive(false);

        var choice = UIText("ChoicePanel", "[LEFT CLICK]  END IT            [Q]  SHOW MERCY", new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(1000f, 50f), 32f);
        ui.choice = choice.gameObject;
        ui.choice.SetActive(false);

        var ov = new GameObject("InBloodOverlay", typeof(RectTransform), typeof(Image));
        ov.transform.SetParent(canvas, false);
        var ovr = (RectTransform)ov.transform;
        ovr.anchorMin = Vector2.zero;
        ovr.anchorMax = Vector2.one;
        ovr.offsetMin = ovr.offsetMax = Vector2.zero;
        var ovi = ov.GetComponent<Image>();
        ovi.color = new Color(0.5f, 0.02f, 0f, 0.1f);   // a light tint: you still need to see the stairs and colleagues
        ovi.raycastTarget = false;
        ui.inBlood = ov;
        ui.inBlood.SetActive(false);
        BuildAirBar(ui);

        // ghost conversations: speaker, line and numbered answers
        var cdGo = new GameObject("ChoiceDialogue", typeof(RectTransform));
        cdGo.transform.SetParent(canvas, false);
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(cdGo.transform, false);
        var pr = (RectTransform)panel.transform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.anchoredPosition = new Vector2(0f, 60f);
        pr.sizeDelta = new Vector2(980f, 300f);
        var pi = panel.GetComponent<Image>();
        pi.color = new Color(0.02f, 0.03f, 0.05f, 0.85f);
        pi.raycastTarget = false;
        var ct = UIText("Text", "", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 26f);
        ct.transform.SetParent(panel.transform, false);
        ct.rectTransform.anchorMin = Vector2.zero;
        ct.rectTransform.anchorMax = Vector2.one;
        ct.rectTransform.offsetMin = new Vector2(28f, 20f);
        ct.rectTransform.offsetMax = new Vector2(-28f, -20f);
        ct.alignment = TextAlignmentOptions.TopLeft;
        ui.choices = cdGo.AddComponent<ChoiceDialogue>();
        ui.choices.panel = panel;
        ui.choices.text = ct;
        ui.choices.blip = dialog.blip;
        panel.SetActive(false);

        ui.checkpoint = UIText("CheckpointNotice", "CHECKPOINT", new Vector2(1f, 1f), new Vector2(-170f, -40f), new Vector2(300f, 40f), 24f);
        ui.checkpoint.alignment = TextAlignmentOptions.Right;
        ui.checkpoint.color = new Color(0.85f, 0.8f, 0.7f);
        ui.checkpoint.gameObject.SetActive(false);

        foreach (var g in new[] { ui.underBlood.gameObject, ui.airBar, ui.inBlood, ui.prompt.gameObject, ui.ammo.gameObject, ui.bossBar, ui.choice, cdGo, ui.checkpoint.gameObject })
            g.transform.SetSiblingIndex(below);

        var interactor = cam.gameObject.AddComponent<PlayerInteractor>();
        interactor.promptText = ui.prompt;
        interactor.range = 3f;
        return ui;
    }

    // the silo flood: "MASH [SPACE]" over a breath bar at the bottom of the screen, and a red that thickens as you go under
    static void BuildAirBar(UI ui)
    {
        var under = new GameObject("UnderBloodTint", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        under.transform.SetParent(canvas, false);
        var ur = (RectTransform)under.transform;
        ur.anchorMin = Vector2.zero;
        ur.anchorMax = Vector2.one;
        ur.offsetMin = ur.offsetMax = Vector2.zero;
        var ui_ = under.GetComponent<Image>();
        ui_.color = new Color(0.32f, 0f, 0f, 1f);
        ui_.raycastTarget = false;
        ui.underBlood = under.GetComponent<CanvasGroup>();
        ui.underBlood.alpha = 0f;
        ui.underBlood.blocksRaycasts = false;

        var label = UIText("AirBar", "MASH  [SPACE]  TO SWIM", new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(700f, 34f), 26f);
        label.font = TMP_Settings.defaultFontAsset;   // plain type: it must read at a glance
        label.color = new Color(0.92f, 0.88f, 0.8f);
        label.fontStyle = FontStyles.Bold;
        var bg = new GameObject("Back", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(label.transform, false);
        var bgr = (RectTransform)bg.transform;
        bgr.anchoredPosition = new Vector2(0f, -32f);
        bgr.sizeDelta = new Vector2(420f, 18f);
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);
        bg.GetComponent<Image>().raycastTarget = false;
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bg.transform, false);
        var fr = (RectTransform)fill.transform;
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = Vector2.one;
        fr.pivot = new Vector2(0f, 0.5f);
        fr.offsetMin = new Vector2(3f, 3f);
        fr.offsetMax = new Vector2(-3f, -3f);
        ui.airFillImage = fill.GetComponent<Image>();
        ui.airFillImage.color = new Color(0.92f, 0.9f, 0.85f);
        ui.airFillImage.raycastTarget = false;
        ui.airBar = label.gameObject;
        ui.airFill = fr;
        ui.airBar.SetActive(false);
    }

    // ---------------------------------------------------------------- city entrance

    static GameObject BuildCityEntrance(Transform root, Transform cityReturn, out Transform officeArrival, out Teleporter toOffice)
    {
        var city = new GameObject("OfficeEntrance (City)").transform;
        city.SetParent(root);

        Vector3 probe = EntranceProbe, dir = EntranceDir.normalized;
        Vector3 doorPos = probe + dir * 6f, normal = -dir;
        var building = GameObject.Find(EntranceBuildingName);
        bool onModel = false;
        foreach (var h in Physics.RaycastAll(probe, dir, 40f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            if (building == null || h.collider.transform.IsChildOf(building.transform))
            {
                doorPos = h.point;
                normal = h.normal; normal.y = 0f; normal.Normalize();
                onModel = building != null;
                break;
            }
        float ground = doorPos.y - 1.4f;
        if (Physics.Raycast(doorPos + normal * 1f + Vector3.up * 0.8f, Vector3.down, out RaycastHit g, 10f, ~0, QueryTriggerInteraction.Ignore)) ground = g.point.y;   // below any awning

        city.position = new Vector3(doorPos.x, ground, doorPos.z) + normal * 0.08f;
        city.rotation = Quaternion.LookRotation(normal);   // local +z points out into the street

        GameObject door;
        if (onModel)
        {
            // the building's own glass doors: an invisible, usable panel in front of them
            door = Box(city, "OfficeDoor", new Vector3(0f, 1.3f, 0.05f), new Vector3(2.8f, 2.6f, 0.12f), mWood);
            door.GetComponent<Renderer>().enabled = false;
        }
        else
        {
            door = Box(city, "OfficeDoor", new Vector3(0f, 1.2f, 0f), new Vector3(1.5f, 2.4f, 0.12f), mWood);
            Box(city, "DoorFrame", new Vector3(0f, 2.5f, 0f), new Vector3(1.9f, 0.2f, 0.2f), mMetal);
        }
        Box(city, "Sign", new Vector3(0f, 3.1f, 0.35f), new Vector3(3.6f, 0.6f, 0.1f), mBlack, false);
        Label(city, "KESSLER & VANE LOGISTICS", new Vector3(0f, 3.1f, 0.42f), Vector3.back, 3f, new Color(0.9f, 0.85f, 0.7f), 3.5f);

        var beacon = new GameObject("OfficeBeacon").transform;
        beacon.SetParent(city, false);
        beacon.localPosition = new Vector3(0f, 3.8f, 0.6f);
        var bl = Lamp(beacon, Vector3.zero, new Color(1f, 0.1f, 0.05f), 25f, 30f, true);
        Box(beacon, "Bulb", Vector3.zero, Vector3.one * 0.3f, mBeacon, false);
        beacon.gameObject.SetActive(false);

        cityReturn.position = city.TransformPoint(new Vector3(0f, 1.1f, 2.2f));
        cityReturn.rotation = city.rotation;

        officeArrival = new GameObject("OfficeArrival").transform;
        officeArrival.SetParent(root);

        toOffice = door.AddComponent<Teleporter>();
        toOffice.player = player.transform;
        toOffice.fader = fader;
        toOffice.arriveLine = "The office. Pitch black.|...hello?|Somewhere in the dark, someone is whispering my name.";
        var it = Inter(door, "Go inside the office");
        it.once = false;
        On(it.onUse, toOffice.Go);
        return beacon.gameObject;
    }

    // ---------------------------------------------------------------- office (the user's office model)
    // Rooms, from the model's floor plan (world coords, floor y 3.4):
    //   reception    x1001-1004.5 z981-998    arrival, breaker box, cleaning rota
    //   meeting room x996-1003.5  z1005-1013  "break room": calendar, cake, fuse
    //   open plan    x998-1012    z1014-1022  your desk, fuse, bodies
    //   closet       x1014.5-1016 z1015-1017.5 "server closet": fuse, body
    //   hub          x1013-1021   z1019-1022  security shutter + keypad into Kessler's wing
    //   Kessler wing x1013-1021   z1023-1032.5 CCTV, safe, report, silo door
    //   east hall    x1017-1021   z980-1017

    const float OfficeFloor = 3.4f;

    static Transform officeModel;

    // first hit on the office model itself (ignores everything we've placed)
    static bool ModelRay(Vector3 from, Vector3 dir, out RaycastHit hit, float dist = 15f)
    {
        var hits = Physics.RaycastAll(from, dir, dist, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits)
            if (h.collider.transform.IsChildOf(officeModel)) { hit = h; return true; }
        hit = default;
        return false;
    }

    // top surface (floor or desk) under x,z
    static Vector3 Top(float x, float z) =>
        ModelRay(new Vector3(x, OfficeFloor + 1.5f, z), Vector3.down, out var h) ? h.point : new Vector3(x, OfficeFloor, z);

    // a soft light in front of a puzzle item so it reads in the dark
    static void Glow(Transform p, Vector3 pos, Vector3 facing, Color c) => Lamp(p, pos - facing * 0.6f + Vector3.up * 0.3f, c, 0.7f, 2.2f);

    // wall point found by looking from `from` along `dir`; `facing` = the direction a reader looks at it
    static Vector3 WallAt(Vector3 from, Vector3 dir, out Vector3 facing)
    {
        facing = dir;
        return ModelRay(from, dir, out var h) ? h.point : from + dir * 1.5f;
    }

    static void BuildOffice(Transform root, Transform arrival, Transform cityReturn, Transform complexArrival, ChaseKiller chase, StabCutscene flashback)
    {
        officeModel = GameObject.Find(OfficeModelName).transform;
        var o = new GameObject("Office").transform;
        o.SetParent(root);

        // lighting: red emergency lamps now, flickering fluorescents once the fuses are in
        var rooms = new[] { new Vector3(1002.7f, 0, 984f), new Vector3(1002.7f, 0, 993f), new Vector3(999.5f, 0, 1009f), new Vector3(1008f, 0, 1009.5f),
                            new Vector3(1009.5f, 0, 1000f), new Vector3(996f, 0, 999f), new Vector3(1002f, 0, 1018f), new Vector3(1009f, 0, 1018f),
                            new Vector3(1017f, 0, 1020.5f), new Vector3(1016f, 0, 1026f), new Vector3(1017f, 0, 1030.5f), new Vector3(1019f, 0, 988f),
                            new Vector3(1019f, 0, 1004f), new Vector3(1015.2f, 0, 1016.2f) };
        var emergency = Node(o, "EmergencyLights", Vector3.zero);
        var power = Node(o, "PowerLights", Vector3.zero);
        for (int i = 0; i < rooms.Length; i++)
        {
            var r = rooms[i];
            // emergency lamps alternate blood red and sodium orange: an orange-red mix that still lights the rooms
            Lamp(emergency, r + Vector3.up * (OfficeFloor + 3.3f), i % 2 == 0 ? new Color(1f, 0.18f, 0.06f) : new Color(1f, 0.45f, 0.12f), 2.4f, 9f, i % 3 == 0);
            Lamp(power, r + Vector3.up * (OfficeFloor + 3.5f), new Color(1f, 0.62f, 0.38f), 2.4f, 10f, true);   // sickly warm tubes, not clean white
        }
        power.gameObject.SetActive(false);

        // ---- RECEPTION: arrival, way out, breaker box (puzzle 1), cleaning rota (clue for puzzle 3)
        arrival.position = new Vector3(1002.7f, OfficeFloor + 1.1f, 982.6f);
        arrival.rotation = Quaternion.identity;

        var exitPos = WallAt(new Vector3(1002.7f, OfficeFloor + 1.2f, 983f), Vector3.back, out var exitFacing);
        var exit = Door(o, "ExitDoor", exitPos - exitFacing * 0.06f, new Vector3(1.4f, 2.4f, 0.1f), Quaternion.LookRotation(exitFacing), mWood, cityReturn, null);
        On(Inter(exit.gameObject, "Leave the office", once: false).onUse, exit.Go);

        var keypad = BuildKeypad(o);
        var cctv = BuildCctv(o, complexArrival, chase, flashback);

        // ---- the fuse box (the user's model): its three loose fuses are the pickups; each one put back slides
        // into its own slot. The loose fuses are modelled in their slots, so "seated" = the model's own pivot.
        var fuseModel = SceneRoot(FuseBoxModel, r => r.transform.Find("PowerBox") != null);
        var pbBounds = fuseModel.transform.Find("PowerBox").GetComponent<Renderer>().bounds;
        var bFacing = Vector3.right;   // you face the wall to use it
        var boxNode = Node(o, "FuseBox (breaker)", Vector3.zero);
        var fb = boxNode.gameObject.AddComponent<FuseBox>();
        var seated = fuseModel.transform.Find("Fuse5");
        fb.seatedPos = fuseModel.transform.position;
        fb.seatedRot = seated != null ? seated.rotation : fuseModel.transform.Find("Fuse1").rotation;
        fb.outward = -bFacing;
        fb.sfx = boxNode.gameObject.AddComponent<AudioSource>();
        fb.sfx.playOnAwake = false;
        string[] fuseLines =
        {
            "A fuse. Pushed into the old cake like a candle.",
            "A fuse. Taped under Derek's keyboard.",
            "A fuse, on a crate by the body. Still warm.",
        };
        var fuseList = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < 3; i++)
        {
            var f = fuseModel.transform.Find("Fuse" + (i + 1));
            if (f == null) continue;
            int slot = fuseList.Count;
            fuseList.Add(f);
            var fBounds = f.GetComponent<Renderer>().bounds;
            var pick = new GameObject("FusePickup " + (i + 1));
            pick.transform.SetParent(boxNode, false);
            pick.transform.position = fBounds.center;
            var pc = pick.AddComponent<BoxCollider>();
            pc.isTrigger = true;
            pc.size = Vector3.one * 0.6f;
            var take = Inter(pick, "Take the fuse", fuseLines[i], give: "Fuse", hide: true);
            On(take.onUse, fb.PickUp, slot);
            Lamp(pick.transform, Vector3.up * 0.5f, new Color(1f, 0.7f, 0.2f), 0.5f, 1.4f);   // faint glow so it can be found
        }
        fb.fuses = fuseList.ToArray();

        var breaker = new GameObject("BreakerUse");
        breaker.transform.SetParent(boxNode, false);
        breaker.transform.position = pbBounds.center;
        var bc = breaker.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = pbBounds.size + new Vector3(0.3f, 0.2f, 0.2f);
        Label(o, "BREAKER", new Vector3(pbBounds.min.x - 0.03f, pbBounds.max.y + 0.15f, pbBounds.center.z), bFacing, 2f, Color.white);
        Glow(o, new Vector3(pbBounds.min.x, pbBounds.center.y, pbBounds.center.z), bFacing, new Color(1f, 0.75f, 0.4f));
        var counter = breaker.AddComponent<PuzzleCounter>();
        counter.required = fuseList.Count;
        counter.progressLine = "*clunk*  {0} of {1} fuses in.";
        fb.counter = counter;
        var bi = Inter(breaker, "Insert a fuse", req: "Fuse", once: false,
                       locked: "The fuse box. Three slots empty.|Someone pulled them out on purpose.");
        On(bi.onUse, fb.InsertNext);   // it slides in, then counts
        SetActive(counter.onSolved, power.gameObject, true);

        // real darkness inside: no sun or sky light through the model's one-sided walls and roof
        var dark = o.gameObject.AddComponent<InteriorDarkness>();
        dark.player = player.transform;
        var modelRs = officeModel.GetComponentsInChildren<Renderer>();
        var ob = modelRs[0].bounds;
        foreach (var r in modelRs) ob.Encapsulate(r.bounds);
        ob.Expand(new Vector3(3f, 6f, 3f));
        dark.zone = ob;
        var sunGo = GameObject.Find("Sun");
        dark.sun = sunGo != null ? sunGo.GetComponent<Light>() : null;
        On(counter.onSolved, dark.SetPowered, true);

        // the ceiling panels stay dead until the fuses are in
        var panelRs = modelRs.Where(r => r.sharedMaterials.Any(m => m != null && m.name == "glsl_emit")).ToArray();
        if (panelRs.Length > 0)
        {
            var panels = o.gameObject.AddComponent<PanelLights>();
            panels.renderers = panelRs;
            panels.glowing = panelRs[0].sharedMaterials.First(m => m != null && m.name == "glsl_emit");
            panels.off = Mat("Office_PanelOff", new Color(0.3f, 0.3f, 0.28f), smooth: 0.5f);
            On(counter.onSolved, panels.SetPowered, true);
        }
        On(counter.onSolved, keypad.SetPowered, true);
        On(counter.onSolved, cctv.SetPowered, true);
        // on the bare band of wall between the filing cabinets' tops and the cornice (lower down, the cabinets and
        // the partition screens stand in front of it), east of the column that stands off the wall at x 1003,
        // one line, in the dripping font
        var wPos = WallAt(new Vector3(1008.1f, OfficeFloor + 3.18f, 1018f), Vector3.forward, out var wFacing);
        // 6 cm off the plaster: it runs across the orange poster, which hangs a little proud of the wall
        var writing = Label(o, "WHO SIGNED IT?", wPos - wFacing * 0.06f, wFacing, 9f, new Color(0.55f, 0f, 0f), 7f);
        writing.name = "BloodWriting";
        var bloodFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Nosifer Blood SDF.asset");
        if (bloodFont != null) writing.font = bloodFont;
        writing.textWrappingMode = TextWrappingModes.NoWrap;
        writing.enableAutoSizing = true; writing.fontSizeMin = 2f; writing.fontSizeMax = 9f;   // as big as fits 7 x 1 m
        var bloodWriting = writing.gameObject;
        bloodWriting.SetActive(false);
        SetActive(counter.onSolved, bloodWriting, true);
        Say(counter.onSolved, "The lights stutter on.|...there's something written over the desks. In blood.");

        var rPos = WallAt(new Vector3(1002.7f, OfficeFloor + 1.6f, 990f), Vector3.right, out var rFacing);
        var rota = Box(o, "CleaningRota", rPos - rFacing * 0.02f, new Vector3(0.35f, 0.45f, 0.02f), mPaper);
        rota.transform.rotation = Quaternion.LookRotation(rFacing);
        rota.GetComponent<BoxCollider>().size = new Vector3(1.5f, 1.5f, 4f);
        Glow(o, rPos, rFacing, new Color(1f, 0.85f, 0.6f));
        Inter(rota, "Read the cleaning rota",
              "NIGHT CLEANING ROTA - T. ROURKE|03:10  Silo 2 walkway.  00:30  Reception.|02:15  Server closet.  01:00  Meeting room.|Someone wrote underneath in marker: 'he never finished his route.'", once: false);

        // ---- MEETING ROOM ("break room"): cake, fuse, calendar (clue for puzzle 2)
        // (the cake and its fuse are the user's models now)
        var cPos = WallAt(new Vector3(999.5f, OfficeFloor + 1.6f, 1010.5f), Vector3.left, out var cFacing);
        var cal = Box(o, "Calendar", cPos - cFacing * 0.02f, new Vector3(0.5f, 0.6f, 0.02f), mPaper);
        cal.transform.rotation = Quaternion.LookRotation(cFacing);
        cal.GetComponent<BoxCollider>().size = new Vector3(1.4f, 1.4f, 4f);
        Label(o, "APRIL", cPos - cFacing * 0.04f + Vector3.up * 0.2f, cFacing, 1.5f, Color.black, 0.5f);
        Glow(o, cPos, cFacing, new Color(1f, 0.85f, 0.6f));
        Inter(cal, "Look at the calendar",
              "APRIL. The 13th is circled in red.|'Tomas - last shift! cake??'|Someone crossed it out and wrote ACCIDENT over it.", once: false);

        // ---- SERVER CLOSET: fuse by a body
        // a crate for the user's closet fuse to sit on, as tall as the fuse is high
        var f3 = fuseModel.transform.Find("Fuse3");
        if (f3 != null)
        {
            var f3b = f3.GetComponent<Renderer>().bounds;
            float h = f3b.min.y - OfficeFloor;
            if (h > 0.15f) Box(o, "Crate", new Vector3(f3b.center.x, OfficeFloor + h * 0.5f, f3b.center.z), new Vector3(0.5f, h, 0.5f), mWood);
        }
        Prop(o, "Assets/Enemies/body-bag/source/Body.fbx", new Vector3(1015.2f, OfficeFloor, 1016.6f), 0.9f, 0f);
        BloodPool(o, new Vector3(1015.2f, OfficeFloor + 0.01f, 1016.2f), 1.2f, "BodyBlood");

        // ---- OPEN PLAN: your desk, fuse, bodies
        var myDesk = Box(o, "YourDesk_Note", Top(1004.5f, 1020f) + Vector3.up * 0.01f, new Vector3(0.12f, 0.01f, 0.12f), mBlood);
        myDesk.GetComponent<BoxCollider>().size = new Vector3(8f, 30f, 8f);   // easy to aim at a tiny note
        Inter(myDesk, "Check your desk",
              "My desk. My keys should be right... here.|Gone.|There's a sticky note, wet with blood: 'COME GET THEM. SILO 2.'");
        Label(o, "YOU", Top(1004.5f, 1020f) + new Vector3(0f, 0.5f, -0.3f), Vector3.forward, 1.5f, new Color(0.5f, 0f, 0f), 0.6f);
        Prop(o, "Assets/Enemies/body-bag/source/Body.fbx", new Vector3(1000.5f, OfficeFloor, 1016.5f), 1f, 90f);
        BloodPool(o, new Vector3(1000.8f, OfficeFloor + 0.01f, 1016.5f), 1.3f, "BodyBlood");
        BloodPool(o, new Vector3(1002.6f, OfficeFloor + 0.01f, 996f), 0.8f);

        // ---- KESSLER'S WING: the incident report
        var report = Box(o, "IncidentReport", Top(1019f, 1031.8f) + Vector3.up * 0.01f, new Vector3(0.3f, 0.01f, 0.4f), mPaper);
        report.GetComponent<BoxCollider>().size = new Vector3(3f, 40f, 3f);
        Lamp(o, report.transform.position + Vector3.up * 0.8f, new Color(1f, 0.85f, 0.6f), 0.6f, 1.8f);
        Inter(report, "Read the report",
              "INCIDENT REPORT - 13/04|'Night cleaner T. Rourke fell into Silo 2 while working unsupervised. Accidental.'|Witness signatures: D. Hale. M. Ortiz. S. Park.|...and mine.|I never read it. I just signed what Kessler put in front of me.", once: false);

        // ---- ...and why: Kessler's second set of books, the smuggling Tomas walked in on
        var ledgerAt = Top(1017.8f, 1031.8f);
        var ledger = Box(o, "KesslerLedger", ledgerAt + Vector3.up * 0.03f, new Vector3(0.28f, 0.05f, 0.36f), mBlood);
        ledger.GetComponent<BoxCollider>().size = new Vector3(3f, 12f, 3f);
        Lamp(o, ledgerAt + Vector3.up * 0.8f, new Color(1f, 0.6f, 0.35f), 0.6f, 1.8f);
        Inter(ledger, "Read Kessler's ledger",
              "A second ledger, hidden under the real books.|'SILO 2 - NIGHT DELIVERIES FROM CITY MORGUE. NO MANIFEST.'|'K x2 - 40,000. L x1 - 150,000. CORNEAS x2 - ...'|...kidneys. Livers. These were people.|The last page, 12/04: 'The cleaner opened a truck. Handle it. D. does it or D. goes down with me.'|The next day, Tomas was 'an accident'.", once: false);

        BuildKesslerConfrontation(o, BuildGhosts(o), flashback);
    }

    static void Fuse(Transform p, Vector3 pos, string line)
    {
        var f = Box(p, "Fuse", pos, new Vector3(0.1f, 0.1f, 0.25f), mFuse);
        f.GetComponent<BoxCollider>().size = new Vector3(4f, 4f, 2f);
        Inter(f, "Take the fuse", line, give: "Fuse", hide: true);
        Lamp(f.transform, Vector3.up * 2f, new Color(1f, 0.7f, 0.2f), 0.4f, 1.2f);   // faint glow so it can be found
    }

    static void BloodPool(Transform p, Vector3 pos, float size, string name = "BloodPool")
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        g.transform.SetParent(p, false);
        g.transform.localPosition = pos;
        g.transform.localScale = new Vector3(size, 0.005f, size * 0.8f);
        g.GetComponent<Renderer>().sharedMaterial = mBlood;
        Object.DestroyImmediate(g.GetComponent<Collider>());
    }

    // puzzle 2: Kessler's door (the user's door model), locked until the code is in; keypad on the wall beside it
    static Keypad BuildKeypad(Transform o)
    {
        var pad = new GameObject("Keypad").transform;
        pad.SetParent(o, false);
        pad.SetPositionAndRotation(new Vector3(1018.23f, OfficeFloor + 1.65f, 1021.91f), Quaternion.identity);   // where the user put it
        pad.localScale = Vector3.one * 0.9f;
        Lamp(pad, new Vector3(0f, 0.3f, -0.6f), new Color(1f, 0.3f, 0.2f), 0.7f, 2.2f);
        Box(pad, "Panel", Vector3.zero, new Vector3(0.42f, 0.62f, 0.04f), mMetal);
        var kp = pad.gameObject.AddComponent<Keypad>();
        kp.code = "1304";
        kp.display = Label(pad, "", new Vector3(0f, 0.22f, -0.03f), Vector3.forward, 1.2f, Color.red, 0.4f);
        kp.wrongLine = "The keypad buzzes. Wrong.";
        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "", "0", "" };
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] == "") continue;
            var b = Box(pad, "Key " + keys[i], new Vector3((i % 3 - 1) * 0.12f, 0.08f - (i / 3) * 0.1f, -0.03f), new Vector3(0.09f, 0.075f, 0.02f), mBlack);
            Label(pad, keys[i], new Vector3((i % 3 - 1) * 0.12f, 0.08f - (i / 3) * 0.1f, -0.045f), Vector3.forward, 0.6f, Color.white, 0.1f);
            On(Inter(b, "Press " + keys[i], once: false).onUse, kp.Press, keys[i]);
        }
        var note = Box(pad, "StickyNote", new Vector3(-0.35f, 0.15f, -0.02f), new Vector3(0.1f, 0.1f, 0.02f), mFuse);
        note.GetComponent<BoxCollider>().size = new Vector3(3f, 3f, 4f);
        Inter(note, "Read the sticky note", "'Door code = THE DAY. Day first, then month. Don't write it down! - K'|...thanks, Kessler.", once: false);

        var doorModel = SceneRoot(KesslerDoorModel);
        if (doorModel == null)
        {
            Debug.LogWarning("HellScape: Kessler's door model (" + KesslerDoorModel + ") is missing: the wing is left open.");
            return kp;
        }
        var frame = doorModel.transform.Find("Plane_002").GetComponent<Renderer>().bounds;
        Label(o, "K. KESSLER - AUTHORISED ONLY", new Vector3(frame.center.x, frame.max.y + 0.35f, frame.min.z - 0.08f), Vector3.forward, 2.2f, new Color(0.9f, 0.7f, 0.1f), 4f);

        var anim = doorModel.GetComponent<Animator>();
        if (anim == null) anim = doorModel.AddComponent<Animator>();   // not ??: Unity fake-null
        anim.runtimeAnimatorController = DoorOpenController(out float clipLength);
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        EditorUtility.SetDirty(doorModel);

        var kd = Node(o, "KesslerDoor", Vector3.zero).gameObject.AddComponent<KesslerDoor>();
        kd.door = anim;
        kd.clipLength = clipLength;
        kd.sfx = kd.gameObject.AddComponent<AudioSource>();
        kd.sfx.playOnAwake = false;
        kd.sfx.spatialBlend = 1f;
        kd.transform.position = frame.center;
        var blocker = new GameObject("DoorBlocker");
        blocker.transform.SetParent(kd.transform, false);
        blocker.transform.position = new Vector3(frame.center.x, OfficeFloor + 1.3f, frame.center.z);
        var bcol = blocker.AddComponent<BoxCollider>();
        bcol.size = new Vector3(frame.size.x * 0.9f, 2.6f, 0.3f);
        kd.blocker = bcol;
        kd.lockedPrompt = Inter(blocker, "Try the door", "Locked. There's a keypad on the wall beside it.", once: false);

        On(kp.onSolved, kd.Unlock);
        Say(kp.onSolved, "*click* The lock gives. Kessler's door swings open.");
        return kp;
    }

    // puzzle 3: replay the cleaner's last route on the CCTV terminal to open the safe
    static SequencePuzzle BuildCctv(Transform o, Transform complexArrival, ChaseKiller chase, StabCutscene flashback)
    {
        var tWall = WallAt(new Vector3(1015.5f, OfficeFloor + 1.5f, 1030f), Vector3.forward, out var tFacing);
        var term = new GameObject("CCTV").transform;
        term.SetParent(o, false);
        term.SetPositionAndRotation(new Vector3(tWall.x, OfficeFloor, tWall.z) - tFacing * 0.1f, Quaternion.LookRotation(tFacing));
        Box(term, "Desk", new Vector3(0f, 0.4f, -0.4f), new Vector3(1.6f, 0.8f, 0.8f), mMetal);
        Box(term, "Screen", new Vector3(0f, 1.55f, -0.05f), new Vector3(1.1f, 0.7f, 0.08f), mScreen);
        var seq = term.gameObject.AddComponent<SequencePuzzle>();
        seq.order = new[] { "Reception", "Meeting Room", "Server Closet", "Silo" };
        seq.display = Label(term, "", new Vector3(0f, 1.55f, -0.1f), Vector3.forward, 1.4f, new Color(0.4f, 1f, 0.6f), 1f);
        seq.noPowerLine = "Security terminal. The screen is dead.";
        string[] shuffled = { "Server Closet", "Silo", "Reception", "Meeting Room" };
        for (int i = 0; i < 4; i++)
        {
            float x = -0.54f + i * 0.36f;
            var b = Box(term, "Cam " + shuffled[i], new Vector3(x, 0.84f, -0.5f), new Vector3(0.22f, 0.06f, 0.14f), mCamButton);   // lit: easy to find on the dark desk
            b.GetComponent<BoxCollider>().size = new Vector3(1.2f, 3f, 2f);
            Label(term, "CAM\n" + shuffled[i].ToUpper(), new Vector3(x, 1.08f, -0.1f), Vector3.forward, 0.6f, Color.white, 0.34f);
            On(Inter(b, "Replay CAM: " + shuffled[i], once: false).onUse, seq.Press, shuffled[i]);
        }

        // the safe with the silo key
        var sWall = WallAt(new Vector3(1018f, OfficeFloor + 0.5f, 1029.8f), Vector3.right, out var sFacing);
        var safe = new GameObject("Safe").transform;
        safe.SetParent(o, false);
        safe.SetPositionAndRotation(new Vector3(sWall.x, OfficeFloor, sWall.z) - sFacing * 0.4f, Quaternion.LookRotation(sFacing));
        Box(safe, "Body", new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0.7f), mMetal);
        var safeDoor = Box(safe, "Door", new Vector3(0f, 0.5f, -0.37f), new Vector3(0.9f, 0.9f, 0.05f), mBlack);
        var key = Box(safe, "SiloKey", new Vector3(0f, 0.55f, -0.45f), new Vector3(0.05f, 0.02f, 0.14f), mGold);
        key.GetComponent<BoxCollider>().size = new Vector3(8f, 10f, 4f);
        Inter(key, "Take the silo key", "SILO 2 - MASTER KEY.", give: "SiloKey", hide: true);
        key.SetActive(false);
        safeDoorObj = safeDoor;
        siloKeyObj = key;
        On(seq.onSolved, flashback.Play);   // the tape: Derek and Tomas on the silo floor
        flashback.afterLine = "The screen cuts to red.|...Derek. My brother. My little brother did that.|The tape keeps rolling. Kessler walks into frame and points at the silo.|They drag Tomas to the edge... and throw him in.|Kessler. He was there. He TOLD him to.|His ghost is still out by his door. He's going to say it to my face.";

        // the silo door (east wall of the wing): into the silo complex
        var dWall = WallAt(new Vector3(1019f, OfficeFloor + 1.2f, 1025.5f), Vector3.right, out var dFacing);
        var sd = Door(o, "SiloDoor", dWall - dFacing * 0.06f, new Vector3(1.4f, 2.4f, 0.1f), Quaternion.LookRotation(dFacing), mRust, complexArrival,
            "Silo 2's service block. Tomas's route ended out here.|...something is breathing in the dark.");
        Label(o, "SILO 2", dWall - dFacing * 0.12f + Vector3.up * 1.45f, dFacing, 3f, new Color(0.9f, 0.7f, 0.1f), 1.5f);
        var sdi = Inter(sd.gameObject, "Open the silo door", req: "SiloKey", consume: false,
            locked: "SILO 2 - AUTHORISED STAFF ONLY.|Locked. Kessler kept the key in his safe... and the safe won't open.");
        On(sdi.onUse, sd.Go);
        On(sd.onArrive, chase.Arm);
        return seq;
    }

    // ---------------------------------------------------------------- ghosts (dead colleagues, multiple-choice talks)

    static GameObject safeDoorObj, siloKeyObj;   // BuildCctv -> BuildKesslerConfrontation

    // back to Kessler after the tape: he admits it, and the lost ghost turns into one of the damned
    static void BuildKesslerConfrontation(Transform o, GhostNPC kessler, StabCutscene flashback)
    {
        var kc = new GameObject("KesslerConfrontation").AddComponent<KesslerConfrontation>();
        kc.transform.SetParent(o, false);
        kc.ghost = kessler;
        kc.player = player.transform;
        kc.playerCam = cam;
        kc.fpc = player.GetComponent<FirstPersonController>();
        kc.lockDuring = flashback.lockDuring;
        kc.fader = fader;
        kc.sfx = kc.gameObject.AddComponent<AudioSource>();
        kc.sfx.playOnAwake = false;
        kc.confrontNodes = new[]
        {
            N("You watched the tape. I can see it on your face.",
                C("You made my brother a murderer.", "Kessler: I made your brother RICH. There's a difference.", 1),
                C("What was really in those trucks?", "Kessler: ...you want to know? Fine. You've earned it.", 1),
                C("Tomas had a brother too, Kessler.", "Kessler: Everybody has a brother.", 1, 1)),
            N("The morgue sent us the ones nobody claimed. Before they went into Silo 2, we took what sells. Kidneys. Livers. Eyes.|The silo crushed what was left. Tomas opened the wrong truck.",
                C("Say it. Say you killed him.", "Kessler: FINE! I killed him! Derek held the knife, but it was ME!"),
                C("You're a monster.", "Kessler: I'm a businessman. You cashed the bonuses. You never asked where they came from."),
                C("Why Derek? Why my brother?", "Kessler: Because he needed the money. You both did. And he did what he was told.")),
        };

        // the damned: a zombie from the pack, its NavMesh AI stripped (there's no navmesh up here)
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DamnedPrefab);
        var z = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(z, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        z.name = "Kessler (damned)";
        z.transform.SetParent(kc.transform, true);
        z.transform.position = kessler.transform.position;
        foreach (var ai in z.GetComponentsInChildren<ZombieAI>(true)) Object.DestroyImmediate(ai);
        foreach (var ag in z.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) Object.DestroyImmediate(ag);
        kc.damned = z.GetComponent<Zombie>();
        kc.damnedAnim = kc.damned.animator != null ? kc.damned.animator : z.GetComponentInChildren<Animator>();
        Lamp(z.transform, Vector3.up * 1.4f, new Color(1f, 0.1f, 0.05f), 1.2f, 4f);   // it glows red where the ghost glowed blue
        z.SetActive(false);

        On(flashback.onFinished, kc.Arm);
        if (safeDoorObj != null) SetActive(kc.onDefeated, safeDoorObj, false);
        if (siloKeyObj != null) SetActive(kc.onDefeated, siloKeyObj, true);
    }

    static GhostChoice C(string text, string reply, int next = -1, int karma = 0) =>
        new GhostChoice { text = text, reply = reply, next = next, karma = karma };

    static GhostNode N(string line, params GhostChoice[] choices) => new GhostNode { line = line, choices = choices };

    // ---------------------------------------------------------------- checkpoints (GameOverScreen.Restart comes back here)

    static Checkpoint MakeCheckpoint(Transform parent, string name, Transform spawn, float health, UI ui)
    {
        var cp = new GameObject("Checkpoint - " + name).AddComponent<Checkpoint>();
        cp.transform.SetParent(parent, false);
        cp.title = "Checkpoint";
        cp.spawn = spawn;
        cp.health = health;
        cp.player = player.transform;
        cp.fader = fader;
        cp.notice = ui.checkpoint;
        cp.restorePoses = new Transform[0];
        return cp;
    }

    static void BuildCheckpoints(Transform root, UI ui, Transform cityReturn, Transform complexArrival, ChaseKiller chase, SiloEncounter enc)
    {
        var cps = Node(root, "Checkpoints", Vector3.zero);
        Teleporter Tp(string n) => root.GetComponentsInChildren<Teleporter>(true).First(t => t.name == n);

        // 1. waking up in hell (zombies on the streets)
        var sleep = Object.FindFirstObjectByType<SleepSequence>(FindObjectsInactive.Include);
        var wakeSpot = sleep != null ? (sleep.fallWakePoint != null ? sleep.fallWakePoint : sleep.wakePoint) : null;
        if (wakeSpot != null)
        {
            var wake = MakeCheckpoint(cps, "Hell city", wakeSpot, 100f, ui);
            Undo.RecordObject(sleep, "Checkpoint wiring");
            while (sleep.onWake.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(sleep.onWake, 0);   // stale ones from an earlier build
            On(sleep.onWake, wake.Activate);
            EditorUtility.SetDirty(sleep);
        }

        // 2. the office entrance, once you've been inside
        var office = MakeCheckpoint(cps, "Office entrance", cityReturn, 100f, ui);
        On(Tp("OfficeDoor").onArrive, office.Activate);
        var kc = root.GetComponentInChildren<KesslerConfrontation>(true);
        if (kc != null) On(office.onRespawn, kc.ResetFight);

        // 3. the silo complex: the chase starts over
        var complex = MakeCheckpoint(cps, "Silo complex", complexArrival, 100f, ui);
        On(Tp("SiloDoor").onArrive, complex.Activate);
        On(complex.onRespawn, chase.ResetChase);
        var startTrig = root.GetComponentsInChildren<StoryTrigger>(true).First(t => t.name == "ChaseStart");
        On(complex.onRespawn, startTrig.Rearm);

        // 4. the silo floor: the fight starts over, on the half health the fall left you
        var floorSpawn = Node(enc.transform, "FloorRespawn", new Vector3(0f, PlayerScale + 0.15f, 3.5f), 180f);
        var floor = MakeCheckpoint(cps, "Silo floor", floorSpawn, 50f, ui);
        floor.clearZombiesRadius = 0f;
        On(enc.onLanded, floor.Activate);
        On(floor.onRespawn, enc.RespawnFight);
        if (enc.flood != null) On(floor.onRespawn, enc.flood.ResetFlood);   // drowned: the blood drains and comes again

        // 5. out with the keys: the drive from the blood wave starts over, car back where it was parked
        var car = Object.FindFirstObjectByType<CarInteract>(FindObjectsInactive.Include);
        var wave = root.GetComponentInChildren<BloodWave>(true);
        var escape = MakeCheckpoint(cps, "Escape", cityReturn, 100f, ui);
        escape.restorePoses = new[] { car.transform };
        On(Tp("SiloExit").onArrive, escape.Activate);
        On(escape.onRespawn, wave.ResetWave);
        var carHealth = car.GetComponent<CarHealth>();
        if (carHealth != null) On(escape.onRespawn, carHealth.Repair);
    }

    // ---------------------------------------------------------------- door transition (the user's room-door model)

    static DoorTransition BuildDoorTransition(Transform root, Behaviour[] lockAll)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorFbx);
        if (prefab == null) return null;
        var ac = DoorOpenController(out float clipLen);

        var stage = Node(root, "DoorTransition (stage)", DoorStage);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, stage);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * 100f;   // the FBX is in metres/100: door 2.06 m
        foreach (var l in model.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
        var wall = Mat("DoorStage_Wall", new Color(0.13f, 0.11f, 0.1f), smooth: 0.1f);
        var floor = Mat("DoorStage_Floor", new Color(0.07f, 0.06f, 0.055f), smooth: 0.3f);
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = r.sharedMaterials.Select(m => m != null && m.name == "unnamed" ? (r.name == "Plane" ? floor : wall) : m).ToArray();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        var anim = model.GetComponent<Animator>();
        if (anim == null) anim = model.AddComponent<Animator>();   // not ??: Unity fake-null
        anim.runtimeAnimatorController = ac;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // one warm lamp over your shoulder on the door; beyond it, nothing
        var lampGo = new GameObject("DoorLamp");
        lampGo.transform.SetParent(stage, false);
        lampGo.transform.localPosition = new Vector3(0.4f, 2.7f, -2.4f);
        lampGo.transform.LookAt(stage.TransformPoint(new Vector3(0f, 1.1f, 0f)));
        var lamp = lampGo.AddComponent<Light>();
        lamp.type = LightType.Spot;
        lamp.color = new Color(1f, 0.78f, 0.55f);
        lamp.intensity = 4.5f;
        lamp.range = 9f;
        lamp.spotAngle = 55f;
        lamp.shadows = LightShadows.Soft;

        var camGo = new GameObject("DoorCamera");
        camGo.transform.SetParent(stage, false);
        var dcam = camGo.AddComponent<Camera>();
        dcam.clearFlags = CameraClearFlags.SolidColor;
        dcam.backgroundColor = Color.black;
        dcam.fieldOfView = 60f;
        dcam.nearClipPlane = 0.05f;
        dcam.farClipPlane = 20f;
        dcam.depth = cam.depth + 10;
        dcam.enabled = false;
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

        var start = Node(stage, "CamStart", new Vector3(0f, 1.45f, -2.5f));
        start.LookAt(stage.TransformPoint(new Vector3(0f, 1.1f, 0.4f)));
        var end = Node(stage, "CamEnd", new Vector3(-0.08f, 1.5f, 1.3f));
        end.LookAt(stage.TransformPoint(new Vector3(-0.08f, 1.45f, 4f)));

        var dt = stage.gameObject.AddComponent<DoorTransition>();
        dt.cam = dcam;
        dt.door = anim;
        dt.camStart = start;
        dt.camEnd = end;
        dt.fader = fader;
        dt.lockDuring = lockAll.Append(player.GetComponent<FirstPersonController>()).Distinct().ToArray();
        dt.clipLength = clipLen;
        dt.sfx = stage.gameObject.AddComponent<AudioSource>();
        dt.sfx.playOnAwake = false;
        dt.sfx.spatialBlend = 0f;
        return dt;
    }

    // ---------------------------------------------------------------- humanoid clips (Mixamo FBXs retargeted)

    // imports a Mixamo animation FBX as Humanoid so it plays on any humanoid (Jason, the civilians). With `copyAs`
    // the FBX is copied first, leaving the original's Generic setup (the zombies use it) untouched.
    static AnimationClip HumanoidClip(string fbx, string clipName, bool loop, string copyAs = null)
    {
        string path = fbx;
        if (copyAs != null)
        {
            if (!AssetDatabase.IsValidFolder(AnimDir)) AssetDatabase.CreateFolder(MatDir, "Anim");
            path = AnimDir + "/" + copyAs + ".fbx";
            if (AssetDatabase.LoadAssetAtPath<Object>(path) == null) AssetDatabase.CopyAsset(fbx, path);
        }
        var mi = (ModelImporter)AssetImporter.GetAtPath(path);
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.motionNodeName = "";
        mi.importCameras = false;
        mi.importLights = false;
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.name = clipName;
            c.loopTime = loop;
            c.lockRootRotation = true;          // stay facing where the script points them
            c.lockRootHeightY = true;
            c.keepOriginalOrientation = true;
            c.keepOriginalPositionY = true;
        }
        mi.clipAnimations = clips;
        mi.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => c.name == clipName);
    }

    static RuntimeAnimatorController[] floatControllers;

    // the ghosts: one held floating pose each (the user's Floating Pose 1 and 2)
    static RuntimeAnimatorController FloatController(int i)
    {
        if (floatControllers == null)
        {
            floatControllers = new RuntimeAnimatorController[FloatFbx.Length];
            for (int k = 0; k < FloatFbx.Length; k++)
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(FloatFbx[k]) == null) continue;
                var clip = HumanoidClip(FloatFbx[k], "FloatingPose" + (k + 1), true);
                string path = AnimDir + "/GhostFloat" + (k + 1) + ".controller";
                if (!AssetDatabase.IsValidFolder(AnimDir)) AssetDatabase.CreateFolder(MatDir, "Anim");
                AssetDatabase.DeleteAsset(path);
                var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
                ac.layers[0].stateMachine.AddState("Float").motion = clip;
                floatControllers[k] = ac;
            }
        }
        return floatControllers[i % floatControllers.Length];
    }


    // lost souls hanging in the air over the hell city (under HellWorld, so they only exist after you wake up)
    [MenuItem("HellScape/Rebuild Lost Souls (city)")]
    static void RebuildLostSouls()
    {
        Materials();
        BuildLostSouls();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    static void BuildLostSouls()
    {
        var hell = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "HellWorld" && g.scene.IsValid());
        if (hell == null) return;
        var oldSouls = hell.transform.Find("LostSouls (City)");
        if (oldSouls != null) Object.DestroyImmediate(oldSouls.gameObject);
        var souls = new GameObject("LostSouls (City)").transform;
        souls.SetParent(hell.transform, false);

        // spread along the streets you actually walk and drive: the zombie spawn points and the road
        var spots = Resources.FindObjectsOfTypeAll<Transform>()
            .Where(t => t.gameObject.scene.IsValid() && t.parent != null && t.parent.name == "ZombieSpawnPoints")
            .Select(t => t.position).ToList();
        var road = Object.FindFirstObjectByType<RoadObstacleSpawner>(FindObjectsInactive.Include);
        if (road != null) spots.AddRange(road.roadPoints.Where(r => r != null).Select(r => r.position));
        var rnd = new System.Random(1304);
        // a few more around each spot, so they gather in loose crowds over the streets rather than one per block
        var around = new System.Collections.Generic.List<Vector3>();
        foreach (var p in spots)
            for (int k = 0; k < 3; k++)
                around.Add(p + new Vector3((float)rnd.NextDouble() * 24f - 12f, 0f, (float)rnd.NextDouble() * 24f - 12f));
        spots.AddRange(around);
        var chosen = new System.Collections.Generic.List<Vector3>();
        foreach (var p in spots.OrderBy(_ => rnd.Next()))
        {
            if (chosen.Any(c => Vector3.Distance(c, p) < 9f)) continue;   // spread out
            chosen.Add(p);
            if (chosen.Count >= 60) break;
        }

        string[] models = { "Walking", "WOMAN 128 FINAL", "Sketchfab_2020_12_11_17_17_43" };   // men, women, children
        float[] scales = { 0.9f, 0.41f, 0.64f };
        for (int i = 0; i < chosen.Count; i++)
        {
            Vector3 at = chosen[i];
            if (Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
            var root = new GameObject("Lost Soul " + (i + 1)).transform;
            root.SetParent(souls, false);
            root.position = at + Vector3.up * (4f + (float)rnd.NextDouble() * 14f);   // some low over your head, some high up
            root.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            int m = i % models.Length;
            var body = CopyOf(models[m], root, Vector3.zero, 0f);
            if (body == null) { Object.DestroyImmediate(root.gameObject); continue; }
            body.transform.localScale = Vector3.one * scales[m];
            foreach (var c in body.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(c);   // wander scripts etc.
            foreach (var c in body.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) Object.DestroyImmediate(c);
            foreach (var c in body.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var r in body.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterials = Enumerable.Repeat(mGhost, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var anim = body.GetComponentInChildren<Animator>();
            var fc = FloatController(i);
            if (anim != null && fc != null)
            {
                anim.runtimeAnimatorController = fc;
                anim.applyRootMotion = false;
            }
            if (i % 3 == 0) Lamp(root, Vector3.up * 1.2f, new Color(0.5f, 0.85f, 1f), 1.2f, 6f);   // one in three glows: 60 lights is too many
            var soul = root.gameObject.AddComponent<LostSoul>();
            soul.driftRadius = 5f + (float)rnd.NextDouble() * 6f;
            soul.driftSpeed = 0.4f + (float)rnd.NextDouble() * 0.5f;
        }
        floatControllers = null;
    }

    static GhostNPC Ghost(Transform o, string name, string model, float scale, float x, float z, string afterLine, params GhostNode[] nodes)
    {
        const float hover = 0f;      // on the floor: they idle and wander (the floating ones are out over the city)
        var root = new GameObject("Ghost - " + name).transform;
        root.SetParent(o, false);
        root.position = new Vector3(x, OfficeFloor, z);
        var body = CopyOf(model, root, Vector3.zero, 0f);
        body.transform.localScale = Vector3.one * scale;
        var anim = body.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CivilianController);
            anim.applyRootMotion = false;
        }
        foreach (var r in body.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterials = Enumerable.Repeat(mGhost, r.sharedMaterials.Length).ToArray();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        foreach (var c in body.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        Lamp(root, Vector3.up * (1.2f + hover), new Color(0.5f, 0.85f, 1f), 0.8f, 3.5f);

        // trigger: talkable, but you walk through them. Reaches down to the floor so looking at them from any
        // height (even straight ahead under a floating ghost) finds them
        var cap = root.gameObject.AddComponent<CapsuleCollider>();
        cap.isTrigger = true;
        cap.center = Vector3.up * (1.9f + hover) * 0.5f;
        cap.height = 1.9f + hover;
        cap.radius = 0.6f;

        var g = root.gameObject.AddComponent<GhostNPC>();
        g.hoverHeight = hover;
        g.animator = anim;
        g.ghostName = name;
        g.body = body.transform;
        g.afterLine = afterLine;
        g.nodes = nodes;
        On(Inter(root.gameObject, "Talk to " + name, once: false).onUse, g.Talk);
        return g;
    }

    static GhostNPC BuildGhosts(Transform o)
    {
        floatControllers = null;
        const string man = "Walking", woman = "WOMAN 128 FINAL";

        Ghost(o, "Priya", woman, 0.41f, 1003.3f, 992.5f, "Priya: The fuses. Where we used to sit. Hurry.",
            N("You came back. Why would anyone come back here?",
                C("I left my keys. Priya... are you-", "Priya: Dead? Yes. It doesn't hurt anymore. The dark does.", 1),
                C("Where is everyone else?", "Priya: Some of us never left. Some are still hiding. Listen for them.", 1),
                C("(Back away slowly)", "Priya: ...everyone backs away. Even now."),
                C("I fell. Off a roof. I should be dead.", "Priya: You are. We all are.|Priya: This is where we go when nobody lets us rest.", 1)),
            N("The power's dead. He pulled the fuses so nobody could see what he did.",
                C("Where did he hide them?", "Priya: Where we used to sit. The meeting room. Your desks. The little closet by Kessler's door."),
                C("I'm sorry this happened to you.", "Priya: ...thank you. Nobody ever said that.|Priya: The fuses are where we used to sit. Go.", -1, 1),
                C("I don't care. I just want my keys.", "Priya: That's what you said about Tomas too.|Priya: Find the fuses yourself.", -1, -1)));

        Ghost(o, "Ben", man, 0.9f, 1001.5f, 1010f, "Ben: The calendar. Day first, then the month.",
            N("We got him a cake, you know. For his last shift. Nobody came.",
                C("Whose last shift?", "Ben: Tomas. The cleaner. He was going home to his brother.", 1),
                C("I didn't know there was a cake.", "Ben: No. You never looked up from your screen.", 1),
                C("I don't have time for this.", "Ben: Nobody ever had time for him either.", -1, -1)),
            N("The date's still on the calendar. Kessler used it for everything after. Doors. Passwords. Guilt.",
                C("What date?", "Ben: The day he died. Day first, then the month. Look at the calendar."),
                C("We should have gone to his party.", "Ben: ...yeah. We should have.|Ben: It's on the calendar. Kessler's door still remembers the day.", -1, 1)));

        Ghost(o, "Nadia", woman, 0.4f, 1010f, 1018.6f, "Nadia: Maya and Sam. The silo. Get them out.",
            N("Your desk is still there. He left something on it for you.",
                C("What did he leave?", "Nadia: A note. It's on your desk. Don't touch the red part."),
                C("Nadia, what happened here?", "Nadia: He came at six, while you were still out on the road. Lights off, one by one.", 1)),
            N("Kessler locked himself in his office. It didn't save him.",
                C("How do I get in there?", "Nadia: Kessler's code. Ask Ben what day it was."),
                C("Did anyone make it out?", "Nadia: Maya and Sam ran for the silo.|Nadia: Please. Get them out.", -1, 1),
                C("Serves Kessler right.", "Nadia: ...he was scared. We all were. You too.", -1, -1)));

        Ghost(o, "Omar", man, 0.88f, 1019f, 1004f, "Omar: Reception. Meeting room. Closet. Silo. Always the silo last.",
            N("I worked security. I watched the tape the night it happened. Then Kessler wiped it... mostly.",
                C("What did you see?", "Omar: Trucks from the city morgue at Silo 2, three in the morning. No lights, no paperwork. Cool boxes going out the other way.|Omar: And Tomas on the walkway with his mop. Watching.", 1),
                C("You let him cover it up?", "Omar: I needed the job. So did you.", 1),
                C("Is there a copy?", "Omar: The terminal in his wing still has the replay. Four cameras.", 1)),
            N("Tomas walked the same route every night. The tape only plays back if you follow him.",
                C("What was his route?", "Omar: It's on the cleaning rota in reception. Put the times in order."),
                C("Rest now, Omar. I'll finish it.", "Omar: ...the rota in reception. Earliest first. The silo last. Always the silo last.", -1, 1)));

        var kessler = Ghost(o, "Kessler", man, 0.93f, 1015f, 1019.6f, "Kessler: My safe stays shut. Whatever you think you know... prove it.",
            N("Don't look at me like that. I kept this company alive.",
                C("You made us sign a lie.", "Kessler: I made you comfortable. You signed without reading. That's on you.", 1),
                C("What did Tomas see?", "Kessler: Nothing. He saw nothing. He fell.|Kessler: It's all in the report. The one you signed.", 1, -1),
                C("Open your door, Kessler.", "Kessler: The code is the day it stopped mattering. Figure it out.", 1),
                C("It's not too late to tell the truth.", "Kessler: ...it is for me. Maybe not for you.", 1, 1)),
            N("He's waiting in the silo. He wants everyone who signed.",
                C("Then I'll face him.", "Kessler: Brave. Or stupid.|Kessler: The silo key stays in my safe. Nobody goes down there."),
                C("You deserved what you got.", "Kessler: ...maybe. So do you.", -1, -1),
                C("Help me get the others out.", "Kessler: Maya and Sam ran for the silo.|Kessler: ...I'm sorry about Derek. He was a good worker.", -1, 1)));

        Ghost(o, "Tomas", man, 0.86f, 1019.4f, 1024.2f, "Tomas: Go. Before the blood comes.",
            N("You. You signed on the last line. You didn't even read it.",
                C("I'm sorry, Tomas. I should have read it.", "Tomas: ...I believe you. My brother won't.", 1, 1),
                C("It wasn't my fault.", "Tomas: It's never anyone's fault. That's how I stayed down there for three days.", 1, -1),
                C("Who did this to you?", "Tomas: Your brother held the knife. Kessler held your brother.|Tomas: I only opened a truck. That's all I ever did. I saw what was inside.", 1)),
            N("Elias won't stop. Not until the silo is full.",
                C("Can I stop him?", "Tomas: You can end him... or you can end it. Those aren't the same thing."),
                C("Then I'll get the others out first.", "Tomas: Then go. Before the blood comes.", -1, 1)));
        return kessler;
    }

    // ---------------------------------------------------------------- silo complex (the user's BloodPool_low model): the chase
    // Route (world coords): the user's "Entrance Door" off corridor x919 (y 90.2) -> bridge z1053 -> corridor x942 ->
    // machinery room z1065 (y 89.6) -> hatch ladder down to the conveyor walkway x956 (y 81.3) -> lift switch ->
    // freight elevator (81.3 -> 67.5) -> bridge z1101 into the silo tower -> shoved off the end, 67 m down into the silo.

    static readonly string[] RouteDoors = { "Entrance Door", "Rollup_door_SM1", "Rollup_door_SM2" };

    static void EnsureColliders(Transform model)
    {
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.name.Contains("Glass") || mf.GetComponent<Collider>() != null) continue;
            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }
    }

    static float FloorY(float x, float z, float from, float fallback)
    {
        var hits = Physics.RaycastAll(new Vector3(x, from, z), Vector3.down, 15f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MinValue;
        foreach (var h in hits) if (h.collider.transform.IsChildOf(complexModel) && h.point.y > best) best = h.point.y;
        return best > float.MinValue ? best : fallback;
    }

    static Transform complexModel;

    static ChaseKiller BuildChaseKiller(Transform root)
    {
        var ck = new GameObject("KillerChase").AddComponent<ChaseKiller>();
        ck.transform.SetParent(root);
        ck.player = player.transform;
        ck.health = health;
        ck.sfx = ck.gameObject.AddComponent<AudioSource>();
        ck.sfx.playOnAwake = false;
        return ck;
    }

    static void BuildComplex(Transform root, Transform arrival, SiloEncounter enc, ChaseKiller chase)
    {
        complexModel = GameObject.Find(ComplexModelName).transform;
        var c = new GameObject("SiloComplex").transform;
        c.SetParent(root);

        chase.killer = enc.killer.transform;
        chase.animator = enc.killer.animator;

        // arrival: in the corridor, in front of the user's "Entrance Door", facing through it onto the bridge
        var entranceDoor = GameObject.Find(ComplexModelName + "/Entrance Door");
        Vector3 doorAt = entranceDoor != null ? entranceDoor.GetComponentInChildren<Renderer>().bounds.center : new Vector3(920.6f, 91.4f, 1052.9f);
        float corridorY = FloorY(doorAt.x - 1.6f, doorAt.z, doorAt.y + 1f, 90.17f);
        arrival.position = new Vector3(doorAt.x - 1.6f, corridorY + PlayerScale + 0.1f, doorAt.z);
        arrival.rotation = Quaternion.Euler(0f, 90f, 0f);

        // the doors on the route swing open when you get here (they're closed in the model)
        var doors = RouteDoors.Select(n => GameObject.Find(ComplexModelName + "/" + n)).Where(g => g != null).ToArray();
        var officeSiloDoor = Object.FindObjectsByType<Teleporter>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "SiloDoor");
        foreach (var d in doors) SetActive(officeSiloDoor.onArrive, d, false);

        // he steps out of the corridor behind you once you're through the door
        var spawn = Node(c, "KillerSpawn", new Vector3(doorAt.x - 1.6f, corridorY, doorAt.z - 9f), 0f);
        var startTrig = Trigger(c, "ChaseStart", new Vector3(doorAt.x + 2.5f, corridorY + 1.2f, doorAt.z), new Vector3(1.5f, 3f, 4f));
        var cuts = BuildKillerCutscenes(c, chase, enc);
        UnityEventTools.AddObjectPersistentListener<Transform>(startTrig.onEnter, cuts.Reveal, spawn);   // cutscene, then the chase

        // the hatch ladder (the user's "Ladder" mesh): through the floor of the machinery room, down the wall to
        // the conveyor walkway. The rungs hang on the wall at x~955.4; you climb facing it (-x).
        const float ladderX = 955.85f, ladderZ = 1067.9f;
        float hatchY = FloorY(ladderX - 1.2f, ladderZ, 91f, 89.56f), walkY = FloorY(ladderX + 0.9f, ladderZ, 83f, 81.32f);
        var lad = Node(c, "Ladder (hatch)", new Vector3(ladderX, walkY, ladderZ));
        var ladder = lad.gameObject.AddComponent<Ladder>();
        ladder.player = player.transform;
        ladder.faceDirection = Vector3.left;
        ladder.path = new[]
        {
            Node(lad, "Hatch", new Vector3(0f, hatchY - walkY + PlayerScale + 0.1f, 0f)),
            Node(lad, "Bottom", new Vector3(0f, PlayerScale + 0.1f, 0f)),
            Node(lad, "Off", new Vector3(0.8f, PlayerScale + 0.1f, 0.6f)),
        };
        ladder.sfx = lad.gameObject.AddComponent<AudioSource>();
        ladder.sfx.playOnAwake = false;
        var grab = new GameObject("LadderHatch");
        grab.transform.SetParent(c, false);
        grab.transform.position = new Vector3(ladderX - 0.5f, hatchY + 0.4f, ladderZ);
        var gc = grab.AddComponent<BoxCollider>();
        gc.isTrigger = true;   // aimable, doesn't block
        gc.size = new Vector3(1.6f, 0.8f, 1.4f);
        On(Inter(grab, "Climb down the ladder", once: false).onUse, ladder.Climb);
        var ladLabel = Label(c, "LADDER", new Vector3(ladderX - 1.3f, hatchY + 0.02f, ladderZ), Vector3.down, 1.6f, new Color(0.95f, 0.75f, 0.1f), 1f);
        ladLabel.transform.rotation = Quaternion.Euler(90f, 90f, 0f);   // painted on the floor, read walking up to the hatch
        Lamp(c, new Vector3(ladderX - 0.5f, hatchY + 1.6f, ladderZ), new Color(1f, 0.6f, 0.3f), 3f, 5f);
        Lamp(c, new Vector3(ladderX + 0.4f, walkY + 2.2f, ladderZ), new Color(1f, 0.6f, 0.3f), 2f, 4f);   // the bottom of the shaft

        // freight elevator: the lift switch beside the top doors opens it; step in and it drops 13.8 m,
        // shutting in the killer's face
        var carGo = GameObject.Find(ComplexModelName + "/Elevator_SM");
        var carRb = carGo.GetComponent<Rigidbody>();
        if (carRb == null) carRb = carGo.AddComponent<Rigidbody>();
        carRb.isKinematic = true;
        carRb.interpolation = RigidbodyInterpolation.Interpolate;
        var elev = c.gameObject.AddComponent<Elevator>();
        elev.car = carRb;
        elev.drop = 13.8f;
        elev.player = player.transform;
        elev.inside = new Bounds(new Vector3(956.15f, 82.6f, 1108.9f), new Vector3(3.6f, 3.4f, 3.4f));
        elev.topDoors = new[] { GameObject.Find(ComplexModelName + "/elevator_door_SM1"), GameObject.Find(ComplexModelName + "/elevator_door_SM2") };
        elev.bottomDoors = new[] { GameObject.Find(ComplexModelName + "/elevator_door_SM3"), GameObject.Find(ComplexModelName + "/elevator_door_SM4") };
        elev.requireSwitch = true;
        elev.sfx = c.gameObject.AddComponent<AudioSource>();
        elev.sfx.playOnAwake = false;
        foreach (var old in carGo.GetComponentsInChildren<Transform>(true).Where(t => t.name == "DownButton").ToArray())
            Object.DestroyImmediate(old.gameObject);
        // the user's "LiftSwitch" mesh holds three switch plates; the lift's is the one beside the top doors
        Vector3 switchAt = new Vector3(954.93f, 82.76f, 1105.79f);
        var sw = new GameObject("LiftSwitch (use)");
        sw.transform.SetParent(c, false);
        sw.transform.position = switchAt + Vector3.back * 0.1f;
        var swc = sw.AddComponent<BoxCollider>();
        swc.isTrigger = true;
        swc.size = new Vector3(0.9f, 0.9f, 0.6f);
        On(Inter(sw, "Hit the lift switch", once: false).onUse, elev.Open);
        Label(c, "LIFT", switchAt + new Vector3(0f, 0.45f, -0.06f), Vector3.forward, 1.6f, new Color(0.95f, 0.75f, 0.1f), 0.6f);
        var swLight = Lamp(c, switchAt + new Vector3(0f, 0.3f, -0.5f), new Color(1f, 0.2f, 0.05f), 2.5f, 3f, true);
        SetActive(elev.onOpen, swLight.gameObject, false);
        On(elev.onDepart, chase.Stop, false);
        elev.doorCloseDelay = 1.15f;                 // he's still sprinting at you when they start to shut
        On(elev.onDepart, cuts.LiftSlam);
        On(enc.killer.onDefeated, cuts.Defeat);
        Say(elev.onDepart, "The doors grind shut-- his hand slams into them--|...|He's gone quiet. Where did he go?");
        SetActive(elev.onArrive, enc.killer.gameObject, false);

        // walking the last bridge into the tower... and then he's behind you
        var dread = Trigger(c, "SiloDread", new Vector3(975f, 68.8f, 1101f), new Vector3(2f, 3f, 4f));
        Say(dread.onEnter, "The silo. I can't even see the bottom.|Maya's voice, way down there. 'Hello?! Is someone up there?!'");
        var push = Trigger(c, "PushTrigger", new Vector3(994.6f, 68.8f, 1101f), new Vector3(1.4f, 3f, 4f));
        On(push.onEnter, enc.Push);

        // red tube lights along the ceilings of the whole run, so it reads at a sprint
        // the complex and the silo tower: no sun through the one-sided roofs, a dim warm fill and thinner haze,
        // so the route reads at a run (the red tubes do the rest)
        var zoneRs = complexModel.GetComponentsInChildren<Renderer>();
        var zb = zoneRs[0].bounds;
        foreach (var r in zoneRs) zb.Encapsulate(r.bounds);
        zb.Expand(10f);
        var light = c.gameObject.AddComponent<InteriorDarkness>();
        light.player = player.transform;
        light.zone = zb;
        var sunGo = GameObject.Find("Sun");
        light.sun = sunGo != null ? sunGo.GetComponent<Light>() : null;
        light.darkAmbient = light.poweredAmbient = new Color(0.32f, 0.17f, 0.12f);
        light.darkFog = new Color(0.26f, 0.1f, 0.07f);   // a bit lighter and thinner: you need to see where you're running
        light.fogDensity = 0.008f;

        var tubes = Node(c, "RedTubeLights", Vector3.zero);
        TubeLights(tubes, 90.2f, new Vector3(doorAt.x + 0.8f, 0f, 1053f), new Vector3(942f, 0f, 1053f), new Vector3(942f, 0f, 1065f), new Vector3(954.5f, 0f, 1065f));
        TubeLights(tubes, 81.3f, new Vector3(956.6f, 0f, 1069f), new Vector3(956.6f, 0f, 1104f));
        TubeLights(tubes, 67.5f, new Vector3(958f, 0f, 1101f), new Vector3(991f, 0f, 1101f));
    }

    // emissive red tubes every few metres under the ceiling along a path (floor near `floorY`), every second one
    // with a real light
    static void TubeLights(Transform parent, float floorY, params Vector3[] path)
    {
        const float spacing = 3.5f;
        int n = 0;
        for (int i = 0; i + 1 < path.Length; i++)
        {
            Vector3 a = path[i], b = path[i + 1], dir = (b - a).normalized;
            float len = Vector3.Distance(a, b);
            for (float d = i == 0 ? 0f : spacing * 0.5f; d <= len; d += spacing, n++)
            {
                Vector3 p = a + dir * d;
                float floor = FloorY(p.x, p.z, floorY + 1.5f, floorY);
                float y = floor + 3f;
                var up = Physics.RaycastAll(new Vector3(p.x, floor + 0.5f, p.z), Vector3.up, 6f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => h.collider.transform.IsChildOf(complexModel)).OrderBy(h => h.distance).ToArray();
                if (up.Length > 0) y = Mathf.Min(up[0].point.y - 0.12f, floor + 4.5f);
                var tube = Box(parent, "Tube", new Vector3(p.x, y, p.z), new Vector3(0.07f, 0.07f, 1.3f), mTube, false);
                tube.transform.rotation = Quaternion.LookRotation(dir);
                tube.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (n % 2 == 0) Lamp(parent, new Vector3(p.x, y - 0.25f, p.z), new Color(1f, 0.16f, 0.07f), 6f, 12f, n % 6 == 0);
                // the model's glTF materials ignore ambient light: only real lights show the walls, so a warm fill
                else Lamp(parent, new Vector3(p.x, y - 0.6f, p.z), new Color(1f, 0.5f, 0.3f), 5.5f, 12f);   // (Forward+ renderer: every lamp reaches the big model meshes)
            }
        }
    }

    // ---------------------------------------------------------------- killer cutscenes (reveal, lift, defeat)

    static RectTransform Letterbox(string name, bool top)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(canvas, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 0f);
        var img = g.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        rt.SetSiblingIndex(fader.transform.GetSiblingIndex());   // under the black fade
        return rt;
    }

    static KillerCutscenes BuildKillerCutscenes(Transform c, ChaseKiller chase, SiloEncounter enc)
    {
        var node = Node(c, "KillerCutscenes", Vector3.zero);
        var ks = node.gameObject.AddComponent<KillerCutscenes>();

        var camGo = new GameObject("CutsceneCamera");
        camGo.transform.SetParent(node, false);
        ks.cam = camGo.AddComponent<Camera>();
        ks.cam.depth = cam.depth + 5;
        ks.cam.fieldOfView = 50f;
        ks.cam.nearClipPlane = 0.05f;
        ks.cam.enabled = false;
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        ks.key = camGo.AddComponent<Light>();   // the silo's materials only answer to real lights: light him for the camera
        ks.key.type = LightType.Point;
        ks.key.color = new Color(1f, 0.55f, 0.4f);
        ks.key.intensity = 6f;
        ks.key.range = 14f;
        ks.key.shadows = LightShadows.None;
        ks.key.enabled = false;

        ks.barTop = Letterbox("LetterboxTop", true);
        ks.barBottom = Letterbox("LetterboxBottom", false);
        ks.player = player.transform;
        ks.playerHead = cam.transform;
        ks.killer = enc.killer.transform;
        ks.killerAnim = enc.killer.animator;
        ks.chase = chase;
        ks.lockDuring = new Behaviour[] { player.GetComponent<FirstPersonController>(), player.GetComponentInChildren<PunchController>(true),
                                          cam.GetComponent<GunController>(), cam.GetComponent<PlayerInteractor>() };

        // the lift: from inside the car, looking out through the top doors down the walkway he sprints along
        float walkY = FloorY(956.3f, 1100f, 83f, 81.32f);
        ks.liftCamera = Node(node, "LiftShot", new Vector3(956.15f, walkY + 1.55f, 1110.3f), 180f);
        ks.liftRunFrom = Node(node, "LiftRunFrom", new Vector3(956.3f, walkY, 1099.5f));
        ks.liftRunTo = Node(node, "LiftRunTo", new Vector3(956.3f, walkY, 1105.4f));

        ks.sfx = node.gameObject.AddComponent<AudioSource>();
        ks.sfx.playOnAwake = false;
        ks.sfx.spatialBlend = 0f;
        return ks;
    }

    static StoryTrigger Trigger(Transform parent, string name, Vector3 pos, Vector3 size)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        var bc = g.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = size;
        var st = g.AddComponent<StoryTrigger>();
        st.playerInCar = false;
        return st;
    }

    // ---------------------------------------------------------------- gun (on the player camera)

    static GunController BuildGun(UI ui)
    {
        // idle should loop; the rest play once
        var mi = (ModelImporter)AssetImporter.GetAtPath(AkFbx);
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips) c.loopTime = c.name.EndsWith("IDLE");
        mi.clipAnimations = clips;
        mi.SaveAndReimport();

        string ctrlPath = MatDir + "/AK74U.controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var all = AssetDatabase.LoadAllAssetsAtPath(AkFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
        AnimationClip Clip(string n) => all.First(c => c.name.EndsWith("|" + n));
        var sm = ac.layers[0].stateMachine;
        var idle = sm.AddState("Idle");
        idle.motion = Clip("IDLE");
        sm.defaultState = idle;
        foreach (var (state, clip) in new[] { ("Shoot", "SHOOT"), ("Reload", "RELOAD1"), ("Draw", "DRAW") })
        {
            var s = sm.AddState(state);
            s.motion = Clip(clip);
            var t = s.AddTransition(idle);
            t.hasExitTime = true;
            t.exitTime = 0.95f;
            t.duration = 0.05f;
        }

        var vm = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AkFbx), cam.transform);
        vm.name = "AK74U (ViewModel)";
        vm.transform.localPosition = new Vector3(0.05f, -1.63f, 0.28f);   // rig's eye point sits just above/behind the rifle
        cam.nearClipPlane = 0.05f;                                          // 0.3 slices the hands off
        vm.transform.localRotation = Quaternion.identity;
        foreach (var r in vm.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var anim = vm.GetComponent<Animator>();
        if (anim == null) anim = vm.AddComponent<Animator>();   // not ??: Unity fake-null
        anim.runtimeAnimatorController = ac;
        anim.applyRootMotion = false;
        var muzzle = Lamp(vm.transform, new Vector3(0.07f, 1.52f, 0.8f), new Color(1f, 0.7f, 0.3f), 4f, 7f);
        muzzle.enabled = false;
        vm.SetActive(false);

        var gun = cam.gameObject.AddComponent<GunController>();
        gun.cam = cam;
        gun.viewModel = vm;
        gun.animator = anim;
        var punch = player.GetComponentInChildren<PunchController>(true);
        gun.fistsRig = punch != null ? punch.gameObject : null;
        gun.source = cam.gameObject.AddComponent<AudioSource>();
        gun.source.playOnAwake = false;
        gun.muzzleLight = muzzle;
        gun.ammoText = ui.ammo;
        return gun;
    }

    // ---------------------------------------------------------------- silo

    static SiloEncounter BuildSilo(Transform root, Transform cityReturn, GunController gun, UI ui, Behaviour[] lockAll)
    {
        // Everything happens inside the complex's own silo tower (BloodPool_low): its floor, the spiral stairs that
        // cling to the wall near the top (from the doorway ledge at 67.5 round and down to a dead end at ~58.3),
        // and the doorway onto the tower bridge. Nothing is built around it.
        var s = new GameObject("Silo").transform;
        s.SetParent(root);
        float floor = FloorY(SiloOrigin.x, SiloOrigin.z, 5f, 0.37f);
        s.position = new Vector3(SiloOrigin.x, floor, SiloOrigin.z);
        Vector3 Stair(float x, float z, float from) => new Vector3(x, FloorY(SiloOrigin.x + x, SiloOrigin.z + z, from, from - 1.2f) - floor, z);
        Vector3 stairEnd = Stair(-4f, 0f, 60f);        // where the stairs stop, 58 m up
        Vector3 ledge = Stair(-5f, 0f, 69f);           // the doorway ledge

        // lights
        Lamp(s, new Vector3(0f, 4f, 0f), new Color(1f, 0.45f, 0.22f), 9f, 20f);         // the arena: steady, so the fight reads
        foreach (float a in new[] { 0f, 120f, 240f })                                     // fill round the wall, no dark corners
            Lamp(s, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 2.5f, R - 1.5f), new Color(1f, 0.55f, 0.3f), 3f, 9f);
        Lamp(s, new Vector3(3f, 2f, -3f), new Color(1f, 0.3f, 0.12f), 2f, 9f, true);
        // the whole shaft, floor to doorway: the model's glTF walls only answer to real lights, so a bright warm lamp
        // up the middle every 8 m and a ring of three near the wall (where the stairs and ledges are) at each level
        for (float y = 6f; y <= 72f; y += 8f)
        {
            Lamp(s, new Vector3(0f, y, 0f), new Color(1f, 0.82f, 0.62f), 16f, 26f, y > 60f);
            for (int k = 0; k < 3; k++)
            {
                float a = (k * 120f + y * 9f) * Mathf.Deg2Rad;
                Lamp(s, new Vector3(Mathf.Sin(a) * (R - 1.8f), y + 1.5f, Mathf.Cos(a) * (R - 1.8f)), new Color(1f, 0.72f, 0.5f), 10f, 12f);
            }
        }
        Lamp(s, new Vector3(994.5f, 70.3f, 1100.5f) - s.position, new Color(1f, 0.55f, 0.3f), 6f, 12f);   // the doorway onto the tower
        Lamp(s, stairEnd + new Vector3(2f, 3f, 3f), new Color(1f, 0.6f, 0.4f), 1.5f, 9f); // the colleagues on the stairs
        Lamp(s, ledge + new Vector3(1f, 2.5f, 0f), new Color(1f, 0.3f, 0.1f), 1.5f, 8f, true);

        // encounter
        var enc = s.gameObject.AddComponent<SiloEncounter>();
        enc.player = player.transform;
        enc.movement = player.GetComponent<FirstPersonController>();
        enc.health = health;
        enc.gun = gun;
        enc.fader = fader;
        enc.siloCenter = Node(s, "SiloCenter", Vector3.zero);
        enc.killerLandPoint = Node(s, "KillerLandPoint", new Vector3(0f, 0f, -3.5f));
        enc.sfx = s.gameObject.AddComponent<AudioSource>();
        enc.sfx.playOnAwake = false;
        enc.gunThrowFrom = Node(s, "GunThrowFrom", Stair(-2f, 6f, 64.5f) + Vector3.up * 1.4f);
        enc.choicePanel = ui.choice;

        // the push happens on the tower bridge (BuildComplex); landing on the floor any other way plays the same beat
        var fall = new GameObject("FloorTrigger");
        fall.transform.SetParent(s, false);
        fall.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        var fc = fall.AddComponent<BoxCollider>();
        fc.isTrigger = true;
        fc.size = new Vector3(11f, 3f, 11f);
        var ft = fall.AddComponent<StoryTrigger>();
        ft.playerInCar = false;
        On(ft.onEnter, enc.Push);

        // the rifle pickup
        var gp = Node(s, "RiflePickup", Vector3.zero);
        Box(gp, "Body", Vector3.zero, new Vector3(0.08f, 0.14f, 0.7f), mBlack, false);
        Box(gp, "Mag", new Vector3(0f, -0.12f, 0.08f), new Vector3(0.06f, 0.2f, 0.1f), mBlack, false);
        Box(gp, "Stock", new Vector3(0f, -0.03f, -0.45f), new Vector3(0.06f, 0.12f, 0.25f), mWood, false);
        var gpc = gp.gameObject.AddComponent<BoxCollider>();
        gpc.size = new Vector3(0.9f, 0.9f, 1.2f);
        var gpi = Inter(gp.gameObject, "Pick up the rifle", hide: true);
        On(gpi.onUse, enc.OnGunTaken);
        enc.gunProp = gp;

        // the car keys (dropped by the killer)
        var keys = Node(s, "CarKeys", Vector3.zero);
        Box(keys, "Key", Vector3.zero, new Vector3(0.05f, 0.02f, 0.12f), mGold, false);
        Box(keys, "Fob", new Vector3(0f, 0f, -0.09f), new Vector3(0.06f, 0.03f, 0.07f), mBlack, false);
        Lamp(keys, Vector3.up * 0.4f, new Color(1f, 0.8f, 0.4f), 0.6f, 1.5f);
        var kc = keys.gameObject.AddComponent<BoxCollider>();
        kc.size = new Vector3(0.8f, 0.8f, 0.8f);
        On(Inter(keys.gameObject, "Take your car keys", give: "CarKeys", hide: true).onUse, enc.OnKeysTaken);
        enc.keysPickup = keys.gameObject;

        enc.killer = BuildKiller(s, enc, ui);

        // flood: carries you 58 m up to where the stairs stop
        var surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
        surface.name = "BloodSurface";
        surface.transform.SetParent(s, false);
        surface.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        surface.transform.localScale = new Vector3(1.6f, 1f, 1.6f);
        surface.GetComponent<Renderer>().sharedMaterial = mBloodSurface;
        Object.DestroyImmediate(surface.GetComponent<Collider>());
        var flood = s.gameObject.AddComponent<BloodFlood>();
        flood.surface = surface.transform;
        flood.startY = floor + 0.3f;
        flood.endY = floor + stairEnd.y + 2f;   // floating at the surface puts your feet just above the last step
        flood.riseTime = 50f;
        flood.radius = R + 1f;
        flood.player = player.GetComponent<Rigidbody>();
        flood.head = cam.transform;   // eyes kept above the blood so you can see
        Lamp(surface.transform, new Vector3(0f, 2.8f, 0f), new Color(1f, 0.35f, 0.18f), 7f, 13f);   // rises with the blood: lights the wall round you
        flood.inBloodOverlay = ui.inBlood;
        flood.airBar = ui.airBar;
        flood.airFill = ui.airFill;
        flood.airFillImage = ui.airFillImage;
        flood.underTint = ui.underBlood;
        flood.health = player.GetComponent<PlayerHealth>();
        flood.rumble = s.gameObject.AddComponent<AudioSource>();
        flood.rumble.playOnAwake = false;
        enc.flood = flood;

        // colleagues hiding on the stairs (civilian models from the day world)
        var maya = Colleague(s, "Maya", "WOMAN 128 FINAL", Stair(-2f, 6f, 64.5f), 0.41f, "Maya: I've got you. Let's GO.", flood);
        var sam = Colleague(s, "Sam", "Walking", Stair(2f, 6f, 64.5f), 0.88f, "Sam: Thank you. Thank you, thank you.", flood);
        // die again in the red city and you become the Lost - or the Damned, if you've owned up to something rotten
        // (Maya confessed to signing Kessler's report down here)
        maya.GetComponent<Survivor>().ifKilled = Survivor.Fate.Damned;
        sam.GetComponent<Survivor>().ifKilled = Survivor.Fate.Lost;
        flood.ghostMaterial = mGhost;
        flood.risenDamned = BuildRisenDamned(s);

        // way out: the doorway onto the tower bridge (the route you came in by)
        var exit = new GameObject("SiloExit");
        exit.transform.SetParent(s, false);
        exit.transform.localPosition = ledge + new Vector3(-1.6f, 1.2f, 0f);
        var ec = exit.AddComponent<BoxCollider>();
        ec.isTrigger = true;   // aimable, doesn't block the doorway
        ec.size = new Vector3(0.4f, 2.4f, 2.6f);
        var tp = exit.AddComponent<Teleporter>();
        tp.player = player.transform;
        tp.destination = cityReturn;
        tp.fader = fader;
        tp.arriveLine = "Air. Real air.|The car. Get to the car.";
        var exitIt = Inter(exit, "Get out of the silo", req: "CarKeys", consume: false, once: false, locked: "Not without my keys.");
        On(exitIt.onUse, tp.Go);
        return enc;
    }

    // ---------------------------------------------------------------- stab cutscenes (the two-person FBXs)

    // the CCTV replay: Derek and Tomas on the silo floor, seen from a security camera up the wall
    static StabCutscene BuildFlashback(Transform root, Behaviour[] lockAll)
    {
        var stage = Node(root, "Flashback (CCTV)", SiloOrigin + Vector3.up * FloorY(SiloOrigin.x, SiloOrigin.z, 5f, 0.37f));
        var fb = BuildRig(stage, "Cutscene - Derek kills Tomas", KitchenKnifeFbx, "TomasDeath", new Vector3(2f, 0f, 1.5f), 200f);
        fb.player = player.transform;
        fb.fader = fader;
        fb.lockDuring = lockAll;
        fb.hideActorsAfter = true;

        var camGo = new GameObject("CctvCamera");
        camGo.transform.SetParent(stage, false);
        // aim at the two of them mid-clip (the characters don't stand on the rig's origin)
        var clip = fb.animator.runtimeAnimatorController.animationClips[0];
        fb.actors.SetActive(true);
        clip.SampleAnimation(fb.actors, clip.length * 0.5f);
        var rs = fb.actors.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        fb.actors.SetActive(false);
        camGo.transform.position = b.center + new Vector3(-2.2f, 2.3f, -2.2f);   // high on the wall, looking down like a security camera
        camGo.transform.LookAt(b.center);
        fb.cam = camGo.AddComponent<Camera>();
        fb.cam.depth = cam.depth + 5;
        fb.cam.fieldOfView = 55f;
        fb.cam.nearClipPlane = 0.05f;
        fb.cam.enabled = false;
        var data = camGo.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        Lamp(stage, stage.InverseTransformPoint(b.center) + new Vector3(0.5f, 2.5f, 0.5f), new Color(0.75f, 0.9f, 1f), 6f, 9f);   // the night lights on the tape

        var rec = UIText("CctvOverlay", "<color=#ff2a2a>● REC</color>   CAM 04 - SILO 2   13/04   03:10", new Vector2(0f, 1f), new Vector2(330f, -40f), new Vector2(620f, 40f), 26f);
        rec.alignment = TextAlignmentOptions.Left;
        rec.transform.SetSiblingIndex(fader.transform.GetSiblingIndex());
        fb.overlay = rec.gameObject;
        rec.gameObject.SetActive(false);
        return fb;
    }

    static StabCutscene BuildRig(Transform parent, string name, string fbx, string clipName, Vector3 localPos, float yaw)
    {
        // the clip should play once and hold its last frame (the body stays down)
        var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips) { c.loopTime = false; c.name = clipName; }
        mi.clipAnimations = clips;
        mi.importCameras = false;   // the Cinema 4D export carries its editor camera: it would render over the game
        mi.importLights = false;
        mi.SaveAndReimport();
        var clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().First(c => c.name == clipName);

        string ctrlPath = MatDir + "/" + clipName + ".controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var st = ac.layers[0].stateMachine.AddState("Stab");
        st.motion = clip;

        var holder = Node(parent, name, localPos, yaw);
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx), holder);
        rig.transform.localPosition = Vector3.zero;
        rig.transform.localRotation = Quaternion.identity;
        rig.transform.localScale = Vector3.one * RigScale;
        var anim = rig.GetComponent<Animator>();
        if (anim == null) anim = rig.AddComponent<Animator>();   // not ??: Unity fake-null
        anim.runtimeAnimatorController = ac;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        RigMaterials(rig);
        foreach (var smr in rig.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;

        var sc = holder.gameObject.AddComponent<StabCutscene>();
        sc.actors = rig;
        sc.animator = anim;
        sc.length = clip.length;
        rig.SetActive(false);
        return sc;
    }

    // both FBXs come in untextured: map each embedded material (by name) to its texture set
    static void RigMaterials(GameObject rig)
    {
        const string mj = "Assets/New Folder/jason-voorhees-machete-stab-animation/textures/";
        const string kk = "Assets/New Folder/tommy-jarvis-multiple-stab-animation/textures/";
        Material Tex(string matName, string diffuse, string normal = null, float smooth = 0.15f)
        {
            if (normal != null && AssetImporter.GetAtPath(normal) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            var m = Mat(matName, Color.white, smooth: smooth);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diffuse));
            var n = normal != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(normal) : null;
            if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
            else m.DisableKeyword("_NORMALMAP");
            return m;
        }
        var eyes = Mat("Rig_Eyes", new Color(0.12f, 0.1f, 0.08f), smooth: 0.8f);
        var map = new System.Collections.Generic.Dictionary<string, Material>
        {
            // machete stab: Jason V9 (the killer) and the prep guy (Derek)
            { "M_Jason_V9_skin", Tex("RigV9_Head", mj + "T_Jason_V9_Head_D.jpeg", mj + "T_Jason_V9_Head_N.jpeg") },
            { "M_Jason_V9_UpperBod", Tex("RigV9_UpperBody", mj + "T_Jason_V9_UpperBody_D.jpeg", mj + "T_Jason_V9_UpperBody_N.jpeg") },
            { "M_Jason_V9_LowerBod", Tex("RigV9_LowerBody", mj + "T_Jason_V9_LowerBody_D.jpeg", mj + "T_Jason_V9_LowerBody_N.jpeg") },
            { "M_Jason_V9_Eye_inst", Tex("RigV9_Eye", mj + "T_Jason_V9_Eye_D.jpeg") },
            { "J9_hair_inst", Tex("RigV9_Hair", mj + "T_Jason_V9_Hair_D.jpeg") },
            { "M_Jason_V9_MAsk_inst", Mat("Jason_Mask", new Color(0.78f, 0.74f, 0.64f), smooth: 0.45f) },
            { "EyeReflection_0", mBlack },
            { "HairMaster_Prep_Inst", Tex("RigPrep_Hair", mj + "T_Prep_Guy_Hair_D.jpeg") },
            { "ClothMAster_Prep_Inst", Tex("RigPrep_Clothes", mj + "T_Prep_Guy_Clothes_D.jpeg", mj + "T_Prep_Guy_Clothes_N.jpeg") },
            { "SkinMaster_Prep_Inst", Tex("RigPrep_Body", mj + "T_Prep_Guy_Body_D.jpeg", mj + "T_Prep_Guy_Body_N.jpeg") },
            { "EyeMAster_Prep_Inst", eyes },
            { "jason_machete", Tex("Rig_Machete", mj + "machete_source.jpeg", mj + "machete_normal.jpeg", 0.5f) },
            // kitchen knife: the prep guy (Derek) and Jason V3 (Tomas)
            { "SkinMaster_JasonV3_Inst", Tex("RigV3_Head", kk + "Jason_V3_Head_Diffuse.png", kk + "Jason_V3_Head_Normal.png") },
            { "ClothMAster_JasonV3_Arms_Inst", Tex("RigV3_Torso", kk + "Jason_V3_Torso_Diffuse.png", kk + "Jason_V3_Torso_Normal.png") },
            { "ClothMAster_JasonV3_Inst", Tex("RigV3_Pants", kk + "Jason_V3_Pants_Diffuse.png", kk + "Jason_V3_Pants_Normal.png") },
            { "EyeMaster_JasonV3_Inst", Tex("RigV3_Eyes", kk + "Jason_V3_Eyes_Diffuse.png") },
            { "M_Prep_Hair_Inst", Tex("RigPrep_Hair2", kk + "T_Prep_Guy_Hair_D.png") },
            { "M_Prep_Clothes_Inst", Tex("RigPrep_Clothes2", kk + "T_Prep_Guy_Clothes_D.png", kk + "T_Prep_Guy_Clothes_N.png") },
            { "M_Prep_Body_Inst", Tex("RigPrep_Body2", kk + "T_Prep_Guy_Body_D.png", kk + "T_Prep_Guy_Body_N.png") },
            { "M_Weapon_Hatchet_01", Tex("Rig_KitchenKnife", kk + "T_Weapon_KitchenKnife_01_D.png", kk + "T_Weapon_KitchenKnife_01_N.png", 0.6f) },
        };
        foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = r.sharedMaterials.Select(m => m != null && map.TryGetValue(m.name.Replace(".1", ""), out var t) ? t : m).ToArray();
    }

    static Interactable Colleague(Transform s, string name, string model, Vector3 pos, float scale, string joinLine, BloodFlood flood)
    {
        var root = Node(s, name, pos, Quaternion.LookRotation(new Vector3(-pos.x, 0f, -pos.z)).eulerAngles.y);   // facing into the silo
        var body = CopyOf(model, root, Vector3.zero, 0f);
        if (body != null) body.transform.localScale = Vector3.one * scale;
        var cap = root.gameObject.AddComponent<CapsuleCollider>();
        cap.center = Vector3.up * 0.9f;
        cap.height = 1.8f;
        cap.radius = 0.35f;
        var sv = root.gameObject.AddComponent<Survivor>();
        sv.displayName = name;
        sv.player = player.transform;
        sv.animator = root.GetComponentInChildren<Animator>();
        if (sv.animator != null)
        {
            // the copied scene model may have come with no controller (or a ghost's float pose): walk like a civilian
            sv.animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CivilianController);
            sv.animator.applyRootMotion = false;
        }
        if (body != null) LivingLook(body, name, name == "Maya" ? new Color(1f, 0.86f, 0.76f) : new Color(0.96f, 0.88f, 0.78f));
        sv.joinLine = joinLine;
        var it = Inter(root.gameObject, "Take " + name + " with you");
        On(it.onUse, sv.Join);
        it.enabled = false;                       // only once the silo floods
        SetEnabled(flood.onBegin, it, true);
        root.gameObject.SetActive(false);         // not there at all during the chase and the fight: they appear with the blood
        SetActive(flood.onBegin, root.gameObject, true);
        return it;
    }

    // the transformations in the silo (a drowned colleague coming back as the Lost / the Damned)
    static void BuildTurningCutscene(SiloEncounter enc, Behaviour[] lockAll)
    {
        var tc = enc.flood.gameObject.AddComponent<TurningCutscene>();
        tc.flood = enc.flood;
        tc.player = player.transform;
        tc.playerCam = cam;
        tc.fpc = player.GetComponent<FirstPersonController>();
        tc.lockDuring = lockAll.Append(player.GetComponent<FirstPersonController>()).Distinct().ToArray();
        tc.fader = fader;
        tc.letterboxTop = canvas.Find("LetterboxTop") as RectTransform;
        tc.letterboxBottom = canvas.Find("LetterboxBottom") as RectTransform;
        tc.sfx = enc.flood.gameObject.AddComponent<AudioSource>();
        tc.sfx.playOnAwake = false;
        tc.sfx.spatialBlend = 0f;
        enc.flood.cutscene = tc;
    }

    // the Damned a drowned colleague comes back as (a zombie from the pack, NavMesh AI stripped: no navmesh in the silo)
    static RisenDamned BuildRisenDamned(Transform s)
    {
        var rd = new GameObject("Risen Damned").AddComponent<RisenDamned>();
        rd.transform.SetParent(s, false);
        var z = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DamnedPrefab));
        PrefabUtility.UnpackPrefabInstance(z, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        z.name = "Maya (damned)";
        z.transform.SetParent(rd.transform, false);
        foreach (var ai in z.GetComponentsInChildren<ZombieAI>(true)) Object.DestroyImmediate(ai);
        foreach (var ag in z.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) Object.DestroyImmediate(ag);
        rd.damned = z.GetComponent<Zombie>();
        rd.anim = rd.damned.animator != null ? rd.damned.animator : z.GetComponentInChildren<Animator>();
        rd.player = player.transform;
        Lamp(z.transform, Vector3.up * 1.4f, new Color(1f, 0.1f, 0.05f), 1.2f, 4f);
        z.SetActive(false);
        return rd;
    }

    // the living look alive: their own warm-tinted materials, shadows, a warm light on them - never the Lost's
    // blue, though the Lost over the city and the office ghosts are copies of the same two models
    static void LivingLook(GameObject body, string name, Color tint)
    {
        const string dir = "Assets/Materials/Survivors";
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Materials", "Survivors");
        foreach (var r in body.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string path = dir + "/" + name + (mats.Length > 1 ? "_" + i : "") + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(mats[i]);
                    AssetDatabase.CreateAsset(m, path);
                }
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);   // solid, never see-through
                EditorUtility.SetDirty(m);
                mats[i] = m;
            }
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        Lamp(body.transform.parent, Vector3.up * 2.2f, new Color(1f, 0.72f, 0.5f), 0.9f, 3.5f);
    }

    static KillerBoss BuildKiller(Transform s, SiloEncounter enc, UI ui)
    {
        // Jason's skeleton is a standard biped; as Humanoid he can use the civilians' walk
        var mi = (ModelImporter)AssetImporter.GetAtPath(JasonFbx);
        if (mi.animationType != ModelImporterAnimationType.Human || mi.importCameras)
        {
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importCameras = false;   // the Cinema 4D export carries its editor camera: it would render over the game
            mi.SaveAndReimport();
        }
        JasonMaterials(mi);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(JasonFbx).OfType<Avatar>().FirstOrDefault();

        var root = Node(s, "Killer", new Vector3(0f, 0f, -4f));
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(JasonFbx), root);
        model.transform.localPosition = Vector3.zero;
        model.transform.localScale = Vector3.one * (2.05f / 5.17f);
        var anim = model.GetComponent<Animator>();
        if (anim == null) anim = model.AddComponent<Animator>();   // not ??: Unity fake-null
        anim.runtimeAnimatorController = KillerController();
        if (avatar != null && avatar.isHuman) anim.avatar = avatar;
        anim.applyRootMotion = false;

        var cap = root.gameObject.AddComponent<CapsuleCollider>();
        cap.center = Vector3.up * 1f;
        cap.height = 2.05f;
        cap.radius = 0.45f;

        var k = root.gameObject.AddComponent<KillerBoss>();
        k.player = player.transform;
        k.animator = anim;
        k.model = model.transform;
        k.arenaCenter = enc.siloCenter;
        k.arenaRadius = R - 1.2f;
        k.healthBar = ui.bossBar;
        k.healthFill = ui.bossFill;
        k.voice = root.gameObject.AddComponent<AudioSource>();
        k.voice.spatialBlend = 1f;
        k.voice.playOnAwake = false;
        On(k.onDefeated, enc.OnKillerDefeated);
        return k;
    }

    // the killer: the civilians' idle/walk, the zombie pack's run and attack swipe (retargeted to humanoid)
    static RuntimeAnimatorController KillerController()
    {
        var civ = AssetDatabase.LoadAssetAtPath<AnimatorController>(CivilianController);
        var civTree = (BlendTree)civ.layers[0].stateMachine.states[0].state.motion;
        var run = HumanoidClip(ZombieRunFbx, "KillerRun", true, "KillerRun");
        var attack = HumanoidClip(ZombieAttackFbx, "KillerAttack", false, "KillerAttack");

        string path = AnimDir + "/Killer.controller";
        AssetDatabase.DeleteAsset(path);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        var loco = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(civTree.children[0].motion, 0f);     // idle
        tree.AddChild(civTree.children[1].motion, 1.3f);   // walk
        tree.AddChild(run, 4.5f);                          // the chase

        var sm = ac.layers[0].stateMachine;
        var swing = sm.AddState("Attack");
        swing.motion = attack;
        swing.speed = 1.5f;
        var into = loco.AddTransition(swing);
        into.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        into.hasExitTime = false;
        into.duration = 0.1f;
        var back = swing.AddTransition(loco);
        back.hasExitTime = true;
        back.exitTime = 0.85f;
        back.duration = 0.2f;
        EliasFightBuilder.AddStates(ac);   // his fight moves: dodges, the charge, the throw, stunned, getting up
        return ac;
    }

    // the FBX's embedded materials come in untextured; remap them to textured ones (the mask has no texture: stained white)
    static void JasonMaterials(ModelImporter mi)
    {
        const string tex = "Assets/New Folder/jason-manhattan-no-mask/textures/T_Jason_V8_";
        Material Textured(string part)
        {
            var normalPath = tex + part + "_N.jpeg";
            var ti = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            var m = Mat("Jason_" + part, Color.white, smooth: 0.15f);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + part + "_D.jpeg"));
            var n = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
            return m;
        }
        var map = new System.Collections.Generic.Dictionary<string, Material>
        {
            { "M_Jason_V8_Head", Textured("Head") },
            { "M_Jason_V8_Torso", Textured("Torso") },
            { "M_Jason_V8_Pants", Textured("Pants") },
            { "M_Jason_V8_Eye", Textured("Eye") },
            { "M_Jason_V8_Mask", Mat("Jason_Mask", new Color(0.78f, 0.74f, 0.64f), smooth: 0.45f) },
            { "jason_mask_eyehole", mBlack },
            { "EyeReflection_0", mBlack },
        };
        foreach (var kv in map)
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
        mi.SaveAndReimport();
    }

    // ---------------------------------------------------------------- car, wave, bridge

    // ---------------------------------------------------------------- the giant blood orb (the user's BloodOrb.blend)

    const string OrbModel = "Assets/New Folder/BloodOrb.blend";
    const float OrbDiameter = 26f;   // eight storeys: fills the street without cutting through the buildings either side

    // the orb takes the old wall's place on the BloodWave: a pivot that rises and rolls, the model spinning inside it
    public static void BuildBloodOrb(BloodWave bw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrbModel);
        if (prefab == null) { Debug.LogWarning("HellScape: no " + OrbModel); return; }
        var wave = bw.transform;
        var old = wave.Find("Orb");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // its spin (the "Scene" take) on a loop
        var mi = (ModelImporter)AssetImporter.GetAtPath(OrbModel);
        var takes = mi.clipAnimations.Length > 0 ? mi.clipAnimations : mi.defaultClipAnimations;
        if (takes.Length > 0 && !takes[0].loopTime)
        {
            takes[0].loopTime = true;
            mi.clipAnimations = takes;
            mi.SaveAndReimport();
        }
        var spin = AssetDatabase.LoadAllAssetsAtPath(OrbModel).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));

        var pivot = new GameObject("Orb").transform;
        pivot.SetParent(wave, false);
        float radius = OrbDiameter * 0.5f;
        pivot.localPosition = Vector3.up * radius * 0.92f;
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pivot);
        model.transform.localPosition = Vector3.zero;
        var b = new Bounds(); bool any = false;
        foreach (var r in model.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
        float size = any ? Mathf.Max(b.size.x, b.size.y, b.size.z) : 2.3f;
        model.transform.localScale = Vector3.one * (OrbDiameter / Mathf.Max(0.01f, size));
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // the imported materials render back faces only (a hollow ring from outside): the orb gets its own copies
            // that draw the front, with a faint glow of its own so it reads through the red fog
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string path = "Assets/Materials/OfficeAct/BloodOrb_" + mats[i].name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(mats[i]); AssetDatabase.CreateAsset(m, path); }
                else if (mats[i] != m) m.CopyPropertiesFromMaterial(mats[i]);
                m.SetFloat("_Cull", 2f);   // back faces culled: the front drawn
                Color c = m.GetColor("_BaseColor");
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(c.r * 0.35f, c.g * 0.2f, c.b * 0.2f));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(m);
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
        if (spin != null)
        {
            string path = "Assets/Materials/OfficeAct/BloodOrb.controller";
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ac.layers[0].stateMachine;
            var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Spin") ?? sm.AddState("Spin");
            st.motion = spin;
            sm.defaultState = st;
            EditorUtility.SetDirty(ac);
            var anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = ac;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        // a red glow under it (lights the street as it comes), and the old wall hidden
        var glow = new GameObject("OrbGlow").AddComponent<Light>();
        glow.transform.SetParent(pivot, false);
        glow.transform.localPosition = Vector3.down * radius * 0.6f;
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.12f, 0.05f);
        glow.intensity = 8f;
        glow.range = 60f;
        glow.shadows = LightShadows.None;

        Undo.RecordObject(bw, "Blood orb");
        bw.orb = pivot;
        bw.orbRadius = radius;
        bw.orbGlow = glow;
        bw.hideWhenOrb = wave.Cast<Transform>().Where(t => t != pivot).Select(t => t.gameObject).ToArray();   // the wall, its crest, its lamp
        bw.startDistance = 70f;
        bw.minStartDistance = 40f;
        bw.catchDistance = radius + 2f;
        bw.closeDistance = 60f;
        bw.musicArea = "Blood Orb";
        EditorUtility.SetDirty(bw);
    }

    [MenuItem("HellScape/Blood Orb Escape (into this scene)")]
    static void ApplyBloodOrb()
    {
        var bw = Object.FindFirstObjectByType<BloodWave>(FindObjectsInactive.Include);
        if (bw == null) { Debug.LogWarning("No BloodWave in this scene."); return; }
        if (bw.fader == null) bw.fader = Object.FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);
        BuildBloodOrb(bw);
        EditorSceneManager.MarkSceneDirty(bw.gameObject.scene);
        Debug.Log("HellScape: the blood wave is now the giant blood orb.");
    }

    static void BuildWaveAndCar(Transform root, GameObject beacon)
    {
        var car = Object.FindFirstObjectByType<CarInteract>(FindObjectsInactive.Include);
        var bridgeZone = GameObject.Find("NoZombieZone (Bridge)");

        var wave = new GameObject("BloodWave").transform;
        wave.SetParent(root);
        Box(wave, "Wall", new Vector3(0f, 16f, 0f), new Vector3(80f, 34f, 10f), mWave, false);         // towers over the rooftops
        var crest = Box(wave, "Crest", new Vector3(0f, 32.5f, 4f), new Vector3(80f, 3f, 6f), mWave, false);
        crest.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
        crest.GetComponent<Renderer>().sharedMaterial = Mat("BloodWave_Crest", new Color(0.6f, 0.03f, 0.02f), new Color(2.2f, 0.08f, 0.04f), smooth: 0.95f);   // a glowing lip
        Lamp(wave, new Vector3(0f, 6f, 8f), new Color(1f, 0.1f, 0.05f), 6f, 40f);
        var bw = wave.gameObject.AddComponent<BloodWave>();
        bw.car = car.transform;
        bw.playerRoot = player.transform;
        // spawn behind the car: opposite the first stretch of road you drive off along (not away from the bridge,
        // which can put it on the road ahead once the road bends)
        var road = Object.FindFirstObjectByType<RoadObstacleSpawner>().roadPoints;
        int near = System.Array.FindIndex(road, p => p == road.OrderBy(q => Vector3.Distance(q.position, car.transform.position)).First());
        bw.awayFrom = road[Mathf.Min(near + 2, road.Length - 1)];
        bw.health = health;
        bw.roar = wave.gameObject.AddComponent<AudioSource>();
        bw.roar.playOnAwake = false;
        bw.roar.spatialBlend = 0.6f;
        bw.roar.maxDistance = 200f;

        // its intro cutscene: a camera of its own (not under the wave, which scales as it rises), the letterbox
        // bars the killer cutscenes use, and the car frozen while it rolls
        var waveCam = new GameObject("WaveCutsceneCamera");
        waveCam.transform.SetParent(root, false);
        bw.cutsceneCam = waveCam.AddComponent<Camera>();
        bw.cutsceneCam.depth = 50;
        bw.cutsceneCam.fieldOfView = 60f;
        bw.cutsceneCam.farClipPlane = 600f;
        bw.cutsceneCam.enabled = false;
        waveCam.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        bw.barTop = canvas.Find("LetterboxTop") as RectTransform;
        bw.barBottom = canvas.Find("LetterboxBottom") as RectTransform;
        bw.carControl = car.carController;
        bw.fader = fader;
        BuildBloodOrb(bw);
        wave.gameObject.SetActive(false);

        // the wave breaks where the bridge starts
        var stop = new GameObject("BloodWaveStop (Bridge)");
        stop.transform.SetParent(root);
        stop.transform.position = bridgeZone != null ? bridgeZone.transform.position : new Vector3(79f, -3f, 366f);
        var sc = stop.AddComponent<BoxCollider>();
        sc.isTrigger = true;
        sc.size = new Vector3(40f, 14f, 20f);
        var st = stop.AddComponent<StoryTrigger>();
        On(st.onEnter, bw.Stop);

        // the car needs its keys now; driving off with them unleashes the wave
        Undo.RecordObject(car, "Office act car wiring");
        car.requiredItem = "CarKeys";
        car.showWhenKeysMissing = new[] { beacon };
        car.onEnter = new UnityEvent();
        On(car.onEnter, bw.Begin);
        EditorUtility.SetDirty(car);
    }
}
