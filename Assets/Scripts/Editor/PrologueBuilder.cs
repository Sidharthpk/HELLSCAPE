using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds the prologue into the apartment-kit scene (Brick Project Studio's Scene_01, saved as Assets/Scenes/Prologue.unity
// so the kit's own demo scene is left alone): night, the player's flat on the 2nd floor, Derek's body, the living-room
// camera and its tape, the killer hiding in the closet, a stairwell extension + bulkhead up to the roof, the push and the fall.
// Menu: HellScape > Build Prologue. Re-running deletes "Prologue" and builds it fresh (the kit's tiles it hides stay hidden).
public static class PrologueBuilder
{
    const string ScenePath = "Assets/Scenes/Prologue.unity";
    const string Dir = "Assets/Materials/Prologue";
    const string OfficeMats = "Assets/Materials/OfficeAct";
    const string JasonFbx = "Assets/New Folder/jason-manhattan-no-mask/source/Jason_Manhattan.fbx";
    const string MacheteFbx = "Assets/New Folder/jason-voorhees-machete-stab-animation/source/machete stab.fbx";
    const string KillerRunFbx = OfficeMats + "/Anim/KillerRun.fbx";
    const string CivilianController = "Assets/Enemies/Civilian.controller";
    const string FpcPrefab = "Assets/ModularFirstPersonController/FirstPersonController/FirstPersonController.prefab";
    const string FootstepDir = "Assets/ModularFirstPersonController/FirstPersonController/";
    const float PlayerScale = 0.85f;
    const float RigScale = 2.05f / 5.17f;   // the Jason FBXs are ~5 units tall

    const string StreetAnchor = "сити исчерп на скетч";
    static readonly Vector3 PlayerStart = new Vector3(71f, 0.2f, 5f);        // outside the office doors, facing home down the street
    const float PlayerStartYaw = -90f;
    static Vector3 BodySpot = new Vector3(2.6f, 4.05f, -0.7f);     // (a rebuild puts him in the master bedroom: MoveDerekToBedroom)
    static Vector3 CctvSpot = new Vector3(5.25f, 7.45f, 1.7f);
    const float Floor2 = 4.05f, TopLanding = 16.05f, RoofY = 16.1f;   // the roof is the user's own (Structure_02/Exterior/Roof)

    static Transform root;
    static TMP_FontAsset font, titleFont;
    static RectTransform canvas;
    static Material mBlood, mDark, mConcrete, mMetal, mScreen, mCamLed, mPaper, mPhone;

    [MenuItem("HellScape/Build Prologue")]
    public static void Build()
    {
        var building = GameObject.Find("Structure_02");
        if (building == null)
        {
            EditorUtility.DisplayDialog("Build Prologue", "Open the apartment scene (Structure_02) first.", "OK");
            return;
        }
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) EditorSceneManager.SaveScene(scene, ScenePath);   // save-as: the kit's demo scene stays as it was
        AddToBuildSettings();

        foreach (var n in new[] { "Prologue", "First Person Player", "Camera", "BA_Simple Cursor" })
            foreach (var g in scene.GetRootGameObjects().Where(r => r.name == n).ToArray())
                Object.DestroyImmediate(g);
        // the kit's demo player (its own camera, mouse-look, Player tag) and cursor, when grouped under another root
        foreach (var t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray())
            if (t.parent != null && (t.name == "First Person Player" || t.name == "BA_Simple Cursor")) t.gameObject.SetActive(false);

        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Materials", "Prologue");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DOS Pixel.asset");
        titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Hell Pixel.asset");
        Materials();

        root = new GameObject("Prologue").transform;
        var moon = Morning();
        var ui = BuildUI();
        var player = BuildPlayer(ui);
        var doors = BuildDoors(building.transform);
        var stairLights = BuildStairwell();
        var derek = BuildDerek(out GameObject[] killerParts, out Transform bodyPoint, out Camera cctvCam);
        var pc = BuildPc();
        var killer = BuildKiller(player.transform, doors);
        BuildClues(building.transform);
        BuildStreet();
        BuildStreetLife(player.transform);

        var d = new GameObject("PrologueDirector").AddComponent<PrologueDirector>();
        d.transform.SetParent(root);
        d.fpc = player;
        d.playerCam = player.playerCamera;
        d.lockDuring = new Behaviour[] { player, player.playerCamera.GetComponent<PlayerInteractor>() };
        d.fader = ui.fader;
        d.objective = ui.objective;
        d.titleCard = ui.titleCard;
        d.barTop = ui.barTop;
        d.barBottom = ui.barBottom;
        d.rage = ui.rage;
        d.pushPrompt = ui.push;
        d.hallway = new Bounds(new Vector3(-3.8f, 5.5f, -4.4f), new Vector3(4.8f, 3f, 6f));
        d.flat = new Bounds(new Vector3(-2.5f, 5.5f, 1f), new Vector3(4.6f, 3f, 4f));
        d.derekRig = derek;
        d.killerInRig = killerParts;
        d.body = bodyPoint;
        d.bloodPool = root.Find("BloodPool")?.gameObject;
        d.pc = pc;
        d.cctvCam = cctvCam;
        d.cctvOverlay = ui.cctv;
        d.cctvClock = ui.cctvClock;
        d.tapeLength = Mathf.Max(6f, derek.runtimeAnimatorController.animationClips[0].length + 0.8f);
        d.killer = killer;
        d.playerBody = BuildFallBody();
        d.closet = doors.FirstOrDefault(x => x.name == "ClosetDoor" && Vector3.Distance(x.transform.position, new Vector3(-1.15f, 5.5f, -8.2f)) < 1f);
        d.stairLights = stairLights;
        d.cine = Cam("CinematicCamera", root, player.playerCamera.depth + 10);
        d.cineLight = Lamp(d.cine.transform, d.cine.transform.position + Vector3.up * 0.3f, new Color(0.75f, 0.8f, 1f), 1.6f, 7f);
        d.moon = moon;
        MorningLines(d);
        var street = GameObject.Find(StreetAnchor);   // the user's street piece, also in MainGameScene: wake where you land
        if (street != null) d.streetAnchor = street.transform;
        d.cctvLook = Look("CctvLook", p =>
        {
            Add<ColorAdjustments>(p, c => { c.saturation.Override(-100f); c.contrast.Override(25f); c.postExposure.Override(0.4f); });
            Add<FilmGrain>(p, c => { c.type.Override(FilmGrainLookup.Large02); c.intensity.Override(1f); });
            Add<Vignette>(p, c => { c.intensity.Override(0.5f); });
        });
        d.hellLook = Look("HellLook", p =>
        {
            Add<ColorAdjustments>(p, c => { c.colorFilter.Override(new Color(1f, 0.32f, 0.26f)); c.saturation.Override(25f); c.contrast.Override(20f); });
            Add<Vignette>(p, c => { c.color.Override(new Color(0.35f, 0f, 0f)); c.intensity.Override(0.5f); });
        });

        // sound
        d.ambience = Source(d.gameObject, AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/New York City Streets Ambience Sound Effect.mp3"), true);
        d.music = Source(d.gameObject, null, false);
        d.sfx = Source(d.gameObject, null, false);
        d.wind = Source(d.gameObject, null, true);
        d.heartbeat = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/WakeHeartbeat.wav");
        d.sting = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/TitleSting.wav");
        d.stabs = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Stabbing sound effect sfx bgm for videos films #copyrightfree #creativecommons.mp3");
        var tvScreen = root.Find("Clues/TV");
        if (tvScreen != null)
        {
            d.tv = tvScreen.gameObject.AddComponent<AudioSource>();
            d.tv.spatialBlend = 1f;
            d.tv.maxDistance = 14f;
            d.tv.volume = 0.35f;
            d.tv.playOnAwake = false;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("HellScape: prologue built and saved to " + ScenePath);
    }

    static void AddToBuildSettings()
    {
        var list = EditorBuildSettings.scenes.ToList();
        if (list.Any(s => s.path == ScenePath)) return;
        list.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ---------------------------------------------------------------- look

    static void Materials()
    {
        mBlood = Mat("Blood", new Color(0.32f, 0.015f, 0.01f), smooth: 0.2f);   // glossier reflects the blue night sky
        mDark = Mat("Dark", new Color(0.05f, 0.05f, 0.05f));
        mConcrete = Mat("Concrete", new Color(0.42f, 0.41f, 0.4f), smooth: 0.1f);
        mMetal = Mat("Metal", new Color(0.3f, 0.3f, 0.32f), metallic: 0.7f, smooth: 0.4f);
        mScreen = Mat("CctvScreen", new Color(0.02f, 0.03f, 0.03f), new Color(0.35f, 0.42f, 0.4f));
        mCamLed = Mat("CamLed", new Color(0.6f, 0f, 0f), new Color(3f, 0.05f, 0.05f));
        mPaper = Mat("Paper", new Color(0.86f, 0.83f, 0.72f));
        mPhone = Mat("PhoneScreen", new Color(0.02f, 0.02f, 0.03f), new Color(0.15f, 0.3f, 0.6f));
    }

    static Material Mat(string name, Color c, Color? emission = null, float metallic = 0f, float smooth = 0.2f)
    {
        string path = Dir + "/" + name + ".mat";
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

    // a dark city night: moonlight, a low blue sky, thin fog. The flat's own lamps light the inside.
    static Volume Look(string name, System.Action<VolumeProfile> fill)
    {
        string path = Dir + "/" + name + ".asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        fill(profile);
        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var v = new GameObject(name).AddComponent<Volume>();
        v.transform.SetParent(root);
        v.isGlobal = true;
        v.priority = 10;
        v.weight = 0f;
        v.sharedProfile = profile;
        return v;
    }

    static void Add<T>(VolumeProfile p, System.Action<T> set) where T : VolumeComponent
    {
        var c = p.Add<T>(true);
        c.name = typeof(T).Name;
        set(c);
    }

    // ---------------------------------------------------------------- UI

    class UI
    {
        public ScreenFader fader;
        public TextMeshProUGUI objective, titleCard, prompt, push, cctvClock;
        public RectTransform barTop, barBottom;
        public CanvasGroup rage;
        public GameObject cctv;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Stretch(string name, Transform parent)
    {
        var rt = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static TextMeshProUGUI Text(string name, Transform parent, string text, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, TMP_FontAsset f = null)
    {
        var t = Rect(name, parent, anchor, anchor, pos, size).gameObject.AddComponent<TextMeshProUGUI>();
        t.font = f != null ? f : font;
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    static Image Img(string name, Transform parent, Color c)
    {
        var i = Stretch(name, parent).gameObject.AddComponent<Image>();
        i.color = c;
        i.raycastTarget = false;
        return i;
    }

    static UI BuildUI()
    {
        var ui = new UI();
        var cg = new GameObject("StoryCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        cg.transform.SetParent(root);
        var cv = cg.GetComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 10;
        var scaler = cg.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvas = (RectTransform)cg.transform;

        // the tape: grey (the CctvLook volume), scanlines, REC + clock
        var cctv = Stretch("CctvOverlay", canvas);
        var lines = Stretch("Scanlines", cctv).gameObject.AddComponent<RawImage>();
        lines.texture = Texture("Scanlines", 1, 4, (x, y) => new Color(0f, 0f, 0f, y == 0 ? 0.35f : 0f), repeat: true);
        lines.uvRect = new Rect(0f, 0f, 1f, 270f);
        lines.raycastTarget = false;
        ui.cctvClock = Text("Clock", cctv, "", new Vector2(0f, 1f), new Vector2(520f, -60f), new Vector2(960f, 50f), 34f);
        ui.cctvClock.alignment = TextAlignmentOptions.Left;
        var play = Text("Play", cctv, "PLAY ▶", new Vector2(1f, 0f), new Vector2(-150f, 60f), new Vector2(260f, 50f), 34f);
        play.alignment = TextAlignmentOptions.Right;
        ui.cctv = cctv.gameObject;
        ui.cctv.SetActive(false);

        ui.objective = Text("Objective", canvas, "", new Vector2(0f, 1f), new Vector2(480f, -45f), new Vector2(880f, 40f), 26f);
        ui.objective.alignment = TextAlignmentOptions.Left;
        ui.objective.color = new Color(0.9f, 0.86f, 0.74f);

        ui.prompt = Text("InteractPrompt", canvas, "", new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(800f, 40f), 26f);
        ui.prompt.gameObject.SetActive(false);

        // rage: red creeping in from the edges
        var rageImg = Stretch("Rage", canvas).gameObject.AddComponent<RawImage>();
        rageImg.texture = Texture("RageVignette", 256, 256, (x, y) =>
        {
            float dx = (x - 127.5f) / 127.5f, dy = (y - 127.5f) / 127.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            return new Color(0.7f, 0f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.25f, r)));
        });
        rageImg.raycastTarget = false;
        ui.rage = rageImg.gameObject.AddComponent<CanvasGroup>();
        ui.rage.alpha = 0f;
        ui.rage.blocksRaycasts = false;

        ui.push = Text("PushPrompt", canvas, "[F]  PUSH HIM", new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(900f, 90f), 64f, TMP_Settings.defaultFontAsset);
        ui.push.color = new Color(0.85f, 0.05f, 0.05f);
        ui.push.color = new Color(0.85f, 0.05f, 0.03f);
        ui.push.gameObject.SetActive(false);

        // inner monologue (the same box as the main game: "* line", typed with a blip)
        var box = Rect("DialogueBox", canvas, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1180f, 150f));
        var group = box.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        Img("Back", box, new Color(0f, 0f, 0f, 0.9f));
        var border = new List<Graphic>();
        foreach (var (a0, a1, off0, off1) in new[]
        {
            (new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -5f), Vector2.zero),
            (new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 5f)),
            (new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(5f, 0f)),
            (new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-5f, 0f), Vector2.zero),
        })
        {
            var e = Rect("Border", box, a0, a1, Vector2.zero, Vector2.zero);
            e.offsetMin = off0;
            e.offsetMax = off1;
            var img = e.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            border.Add(img);
        }
        var line = Text("Text", box, "", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 34f);
        line.rectTransform.anchorMin = Vector2.zero;
        line.rectTransform.anchorMax = Vector2.one;
        line.rectTransform.offsetMin = new Vector2(34f, 20f);
        line.rectTransform.offsetMax = new Vector2(-34f, -20f);
        line.alignment = TextAlignmentOptions.TopLeft;
        var dlg = box.gameObject.AddComponent<DialogueBox>();
        dlg.group = group;
        dlg.text = line;
        dlg.border = border.ToArray();
        dlg.blip = box.gameObject.AddComponent<AudioSource>();
        dlg.blip.playOnAwake = false;
        dlg.blip.volume = 0.35f;
        dlg.blip.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/TextBlip.wav");

        ui.barTop = Rect("LetterboxTop", canvas, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 0f));
        ui.barTop.pivot = new Vector2(0.5f, 1f);
        ui.barTop.gameObject.AddComponent<Image>().color = Color.black;
        ui.barBottom = Rect("LetterboxBottom", canvas, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 0f));
        ui.barBottom.pivot = new Vector2(0.5f, 0f);
        ui.barBottom.gameObject.AddComponent<Image>().color = Color.black;
        box.SetAsLastSibling();   // lines stay readable over the bars

        var black = Img("Fade", canvas, Color.black);
        var fg = black.gameObject.AddComponent<CanvasGroup>();
        fg.blocksRaycasts = false;
        ui.fader = black.gameObject.AddComponent<ScreenFader>();
        ui.fader.fade = fg;

        ui.titleCard = Text("TitleCard", canvas, "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 80f), 44f);
        ui.titleCard.color = new Color(0.85f, 0.82f, 0.75f);
        box.SetAsLastSibling();   // and over the black
        return ui;
    }

    static Texture2D Texture(string name, int w, int h, System.Func<int, int, Color> px, bool repeat = false)
    {
        string path = Dir + "/" + name + ".png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, px(x, y));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        ti.filterMode = repeat ? FilterMode.Point : FilterMode.Bilinear;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------------------------------------------------------------- player

    static FirstPersonController BuildPlayer(UI ui)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FpcPrefab));
        go.name = "Player";
        go.transform.SetParent(root);
        go.transform.SetPositionAndRotation(PlayerStart + Vector3.up * PlayerScale, Quaternion.Euler(0f, PlayerStartYaw, 0f));
        go.transform.localScale = Vector3.one * PlayerScale;
        go.tag = "Player";
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;   // the capsule would show up in the cutscene shots

        var fpc = go.GetComponent<FirstPersonController>();
        fpc.crouchHeight = PlayerScale * 0.65f;
        fpc.walkSpeed = 4.2f;                 // a tired walk home; the chase gives unlimited sprint
        fpc.enableJump = false;
        fpc.enableZoom = false;
        fpc.maxLookAngle = 75f;
        fpc.playerCamera.nearClipPlane = 0.05f;
        var camData = fpc.playerCamera.GetUniversalAdditionalCameraData();
        camData.renderPostProcessing = true;

        var rb = go.GetComponent<Rigidbody>();
        // no interpolation: the controller turns the body by setting its rotation every frame, and an interpolating
        // rigidbody overwrites that with its own smoothed rotation - the two fight and the view jitters as you turn
        rb.interpolation = RigidbodyInterpolation.None;
        var col = go.GetComponent<CapsuleCollider>();
        col.sharedMaterial = Slippery();      // no sticking to walls and stair edges

        go.AddComponent<StickToGround>();

        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.volume = 0.5f;
        var steps = go.AddComponent<PlayerFootStep>();
        steps.footSteps = Footsteps();

        var it = fpc.playerCamera.gameObject.AddComponent<PlayerInteractor>();
        it.promptText = ui.prompt;
        it.range = 2.6f;
        return fpc;
    }

    static AudioClip[] Footsteps() =>
        Enumerable.Range(1, 7).Select(i => AssetDatabase.LoadAssetAtPath<AudioClip>(FootstepDir + "Footstep_" + i + ".mp3")).Where(c => c != null).ToArray();

    static PhysicsMaterial Slippery()
    {
        string path = Dir + "/Slippery.physicMaterial";
        var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (m == null)
        {
            m = new PhysicsMaterial("Slippery") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    static Camera Cam(string name, Transform parent, float depth)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        var c = go.AddComponent<Camera>();
        c.depth = depth;
        c.nearClipPlane = 0.05f;
        c.fieldOfView = 60f;
        c.enabled = false;
        go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        return c;
    }

    static AudioSource Source(GameObject g, AudioClip clip, bool loop, bool spatial = false)
    {
        var a = g.AddComponent<AudioSource>();
        a.clip = clip;
        a.loop = loop;
        a.playOnAwake = false;
        a.spatialBlend = spatial ? 1f : 0f;
        return a;
    }

    // ---------------------------------------------------------------- doors

    // every room/stairwell door and closet in the building: F to open, the kit's click scripts off
    static List<PrologueDoor> BuildDoors(Transform building)
    {
        var result = new List<PrologueDoor>();
        foreach (var mb in building.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string type = mb.GetType().Name;
            bool closet = type == "ClosetopencloseDoor";
            if (type != "opencloseDoor" && !closet) continue;
            string frame = mb.transform.parent != null ? mb.transform.parent.name : "";
            if (!frame.StartsWith("DoorFrame") && !frame.StartsWith("IntExt")) continue;   // not the cupboards and fridges

            var anim = mb.GetComponent<Animator>();
            if (anim == null) continue;
            Undo.RecordObject(mb, "Prologue door");
            mb.enabled = false;
            var so = new SerializedObject(mb);
            var p = so.FindProperty("Player");
            if (p != null) { p.objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo(); }

            foreach (var old in mb.GetComponents<PrologueDoor>()) Object.DestroyImmediate(old);
            foreach (var old in mb.GetComponents<Interactable>()) Object.DestroyImmediate(old);
            foreach (var old in mb.GetComponents<AudioSource>()) Object.DestroyImmediate(old);
            var it = mb.gameObject.AddComponent<Interactable>();
            it.prompt = "Open door";
            it.once = false;
            var d = mb.gameObject.AddComponent<PrologueDoor>();
            d.anim = anim;
            if (closet) { d.openState = "ClosetOpening"; d.closeState = "ClosetClosing"; }
            d.startOpen = mb.transform.parent.name == "DoorFrame_Apt_03" && Mathf.Abs(mb.transform.position.y - 5.5f) < 0.5f;   // the flat's front door
            d.sfx = mb.gameObject.AddComponent<AudioSource>();
            d.sfx.spatialBlend = 1f;
            d.sfx.maxDistance = 25f;
            d.sfx.playOnAwake = false;
            result.Add(d);
        }
        return result;
    }

    // ---------------------------------------------------------------- stairwell and roof

    // invisible slopes over each flight: the rigidbody player glides up instead of catching on step edges
    static LightFlicker[] BuildStairwell()
    {
        var sw = Node(root, "Stairwell", Vector3.zero);
        foreach (float L in new[] { 0f, 4f, 8f, 12f, 16f })
        {
            Ramp(sw, new Vector3(-8.44f, L + 0.08f, -7.47f), new Vector3(-11.25f, L + 2.33f, -7.47f), 2.3f);
            Ramp(sw, new Vector3(-11.1f, L + 2.33f, -5.05f), new Vector3(-8.75f, L + 4.1f, -5.05f), 2.3f);
        }
        var lights = new List<LightFlicker>();
        foreach (float L in new[] { 0f, 4f, 8f, 12f, 16f })
        {
            var l = Lamp(sw, new Vector3(-7.5f, L + 3.4f, -6.2f), new Color(1f, 0.82f, 0.58f), 1.4f, 7f);
            var f = l.gameObject.AddComponent<LightFlicker>();
            f.enabled = false;
            lights.Add(f);
        }
        return lights.ToArray();
    }

    static void Ramp(Transform parent, Vector3 a, Vector3 b, float width)
    {
        var g = new GameObject("Ramp");
        g.transform.SetParent(parent);
        // 10 cm over the step nosings (the kit's steps poke through a flush slope and catch the capsule), and the
        // lower end run 35 cm on down into the landing so there's no lip to walk into
        if (a.y <= b.y) a -= (b - a).normalized * 0.35f;
        else b -= (a - b).normalized * 0.35f;
        a += Vector3.up * 0.1f;
        b += Vector3.up * 0.1f;
        Vector3 dir = b - a;
        var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        g.transform.SetPositionAndRotation((a + b) / 2f - rot * Vector3.up * 0.05f, rot);
        var c = g.AddComponent<BoxCollider>();
        c.size = new Vector3(width, 0.1f, dir.magnitude);
        c.sharedMaterial = Slippery();
    }

    // ---------------------------------------------------------------- Derek

    // the machete-stab rig (the killer and the "prep guy" = Derek) holding its last frame on the living-room floor.
    // The same rig plays from the start on the tape, seen through the ceiling camera.
    static Animator BuildDerek(out GameObject[] killerParts, out Transform bodyPoint, out Camera cctvCam)
    {
        var holder = Node(root, "Derek", Vector3.zero);
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MacheteFbx), holder);
        rig.transform.localPosition = Vector3.zero;
        rig.transform.localRotation = Quaternion.identity;
        rig.transform.localScale = Vector3.one * RigScale;
        var anim = rig.GetComponent<Animator>();
        if (anim == null) anim = rig.AddComponent<Animator>();   // not ??: Unity fake-null
        anim.runtimeAnimatorController = StabController(out AnimationClip clip);
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        RigMaterials(rig);
        foreach (var smr in rig.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;

        var derekR = rig.GetComponentsInChildren<Renderer>().Where(r => r.name.Contains("Counselor")).ToArray();
        var killerR = rig.GetComponentsInChildren<Renderer>().Where(r => !r.name.Contains("Counselor")).ToArray();

        // turn the rig until neither of them ends up inside the furniture, then slide it so Derek lies on the spot
        float bestYaw = 0f; int bestHits = int.MaxValue; Vector3 bestOffset = Vector3.zero;
        Physics.SyncTransforms();
        for (float yaw = 0f; yaw < 360f; yaw += 30f)
        {
            holder.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
            clip.SampleAnimation(rig, clip.length);
            Vector3 off = BodySpot - Floor(Bounds(derekR));
            off.y = Floor2;   // the rig stands on its origin
            int hits = 0;
            foreach (float at in new[] { 0.3f, 0.6f, 1f })
            {
                clip.SampleAnimation(rig, clip.length * at);
                hits += Blocked(Bounds(derekR), off) + Blocked(Bounds(killerR), off);
            }
            if (hits < bestHits) { bestHits = hits; bestYaw = yaw; bestOffset = off; }
        }
        holder.SetPositionAndRotation(bestOffset, Quaternion.Euler(0f, bestYaw, 0f));
        clip.SampleAnimation(rig, clip.length * 0.5f);
        Vector3 midAction = Bounds(rig.GetComponentsInChildren<Renderer>()).center;
        clip.SampleAnimation(rig, clip.length);
        var body = Bounds(derekR);

        bodyPoint = Node(root, "DerekBody", body.center);
        var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pool.name = "BloodPool";
        Object.DestroyImmediate(pool.GetComponent<Collider>());
        pool.transform.SetParent(holder.parent);
        float poolY = Floor2;
        foreach (var h in Physics.RaycastAll(new Vector3(body.center.x, Floor2 + 1.5f, body.center.z), Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore))
            poolY = Mathf.Max(poolY, h.point.y);   // on the rug if he's on the rug
        pool.transform.position = new Vector3(body.center.x, poolY + 0.01f, body.center.z);
        pool.transform.localScale = new Vector3(Mathf.Max(1.2f, body.size.x * 0.8f), 0.004f, Mathf.Max(1.2f, body.size.z * 0.8f));
        pool.GetComponent<Renderer>().sharedMaterial = mBlood;
        // a few spreading lobes so it isn't a perfect disc
        var rnd = new System.Random(7);
        for (int i = 0; i < 4; i++)
        {
            var lobe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(lobe.GetComponent<Collider>());
            lobe.name = "Lobe";
            lobe.transform.SetParent(pool.transform, false);
            float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
            lobe.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.35f, 0.1f * i, Mathf.Sin(a) * 0.35f);
            lobe.transform.localScale = new Vector3(0.5f + (float)rnd.NextDouble() * 0.4f, 1f, 0.35f + (float)rnd.NextDouble() * 0.3f);
            lobe.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 180f, 0f);
            lobe.GetComponent<Renderer>().sharedMaterial = mBlood;
        }

        killerParts = killerR.Select(r => r.gameObject).ToArray();
        // the reading lamp over the spot where it happened: lights the tape, and him on the floor after
        Lamp(root, new Vector3(midAction.x, Floor2 + 2.9f, midAction.z), new Color(0.8f, 0.88f, 1f), 3f, 6.5f);

        // the camera Derek put up last month, in the ceiling corner
        var camProp = Node(root, "CCTV Camera", CctvSpot);
        camProp.rotation = Quaternion.LookRotation(midAction - CctvSpot);
        Box(camProp, "Housing", new Vector3(0f, 0f, -0.1f), new Vector3(0.14f, 0.12f, 0.28f), mDark, false);
        Box(camProp, "Led", new Vector3(0.05f, 0.05f, 0.04f), Vector3.one * 0.025f, mCamLed, false);
        cctvCam = Cam("CctvCamera", camProp, 50f);
        cctvCam.transform.localPosition = new Vector3(0f, -0.05f, 0.1f);
        cctvCam.transform.localRotation = Quaternion.identity;
        cctvCam.fieldOfView = 68f;
        Debug.Log($"Prologue: Derek rig yaw {bestYaw}, overlaps {bestHits}, body at {body.center}");
        return anim;
    }

    // Derek in the master bedroom (the big room with the large bed) instead of the living room, so you walk the flat
    // - past the phone, the TV, the fridge note - looking for him. The whole scene moves: his body, the blood, the
    // lamp over it and the camera that taped it (now in the bedroom's ceiling corner). Into the open Prologue scene.
    [MenuItem("HellScape/Prologue: Move Derek To The Master Bedroom")]
    public static void MoveDerekToBedroom()
    {
        var d = Object.FindFirstObjectByType<PrologueDirector>(FindObjectsInactive.Include);
        if (d == null) { Debug.LogWarning("Open the Prologue scene first."); return; }
        root = d.transform.parent;
        Vector3 near = d.body != null ? d.body.position : BodySpot;

        // the biggest bed on Derek's floor, in this flat
        Bounds? bed = null;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.name.ToLower().Contains("bed") || r.transform.IsChildOf(root)) continue;
            var b = r.bounds;
            if (b.min.y < Floor2 - 0.3f || b.min.y > Floor2 + 0.9f) continue;
            if (Vector3.Distance(new Vector3(b.center.x, 0f, b.center.z), new Vector3(near.x, 0f, near.z)) > 32f) continue;   // the whole floor is the flat
            if (bed == null || b.size.x * b.size.z > bed.Value.size.x * bed.Value.size.z) bed = b;
        }
        if (bed == null) { Debug.LogWarning("HellScape: couldn't find a bed on the second floor near the flat."); return; }
        var bb = bed.Value;

        // beside it, on whichever side has the most floor
        Physics.SyncTransforms();
        Vector3 bestSpot = Vector3.zero; float bestFree = -1f;
        foreach (var dir in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        {
            float half = Mathf.Abs(Vector3.Dot(bb.extents, dir));
            Vector3 edge = new Vector3(bb.center.x, Floor2 + 0.4f, bb.center.z) + dir * (half + 0.05f);
            float free = Physics.Raycast(edge, dir, out RaycastHit h, 3f, ~0, QueryTriggerInteraction.Ignore) ? h.distance : 3f;
            if (free <= bestFree) continue;
            bestFree = free;
            bestSpot = edge + dir * Mathf.Clamp(free * 0.5f, 0.45f, 1.0f);
        }
        BodySpot = new Vector3(bestSpot.x, Floor2, bestSpot.z);

        // the camera: the bedroom ceiling corner furthest from him (the widest view of the room)
        float ceil = Physics.Raycast(BodySpot + Vector3.up * 1f, Vector3.up, out RaycastHit up, 4f, ~0, QueryTriggerInteraction.Ignore) ? up.point.y : Floor2 + 3.4f;
        Vector3 top = new Vector3(BodySpot.x, ceil - 0.25f, BodySpot.z);
        Vector3 bestCorner = top; float far = -1f;
        foreach (var dir in new[] { new Vector3(1f, 0f, 1f), new Vector3(1f, 0f, -1f), new Vector3(-1f, 0f, 1f), new Vector3(-1f, 0f, -1f) })
        {
            var n = dir.normalized;
            float dist = Physics.Raycast(top, n, out RaycastHit h, 8f, ~0, QueryTriggerInteraction.Ignore) ? h.distance : 3f;
            if (dist > far) { far = dist; bestCorner = top + n * Mathf.Max(0.3f, dist - 0.35f); }
        }
        CctvSpot = bestCorner;

        // out with the living-room scene
        Vector3 oldBody = near;
        foreach (var n in new[] { "Derek", "DerekBody", "BloodPool", "CCTV Camera" })
            foreach (var t in root.Cast<Transform>().Where(t => t.name == n).ToArray()) Object.DestroyImmediate(t.gameObject);
        foreach (var t in root.Cast<Transform>().Where(t => t.name == "Light" && Mathf.Abs(t.position.y - (Floor2 + 2.9f)) < 0.1f &&
                                                          Vector3.Distance(new Vector3(t.position.x, 0f, t.position.z), new Vector3(oldBody.x, 0f, oldBody.z)) < 5f).ToArray())
            Object.DestroyImmediate(t.gameObject);   // the reading lamp that hung over him

        Materials();
        var derek = BuildDerek(out GameObject[] killerParts, out Transform bodyPoint, out Camera cctvCam);
        Undo.RecordObject(d, "Derek to the bedroom");
        d.derekRig = derek;
        d.killerInRig = killerParts;
        d.body = bodyPoint;
        d.bloodPool = root.Find("BloodPool")?.gameObject;
        d.cctvCam = cctvCam;
        d.tapeLength = Mathf.Max(6f, derek.runtimeAnimatorController.animationClips[0].length + 0.8f);
        d.cameraIdea = d.cameraIdea.Replace("living room", "bedroom");
        EditorUtility.SetDirty(d);
        EditorSceneManager.MarkSceneDirty(d.gameObject.scene);
        Debug.Log($"HellScape: Derek now lies by the bed at {BodySpot} (bed {bb.center}, {bb.size}); camera at {CctvSpot}. Save the scene to keep it.");
    }

    static int Blocked(Bounds b, Vector3 offset)
    {
        b.center += offset;
        var half = b.extents;
        half.y = Mathf.Max(0.05f, half.y - 0.15f);
        var c = b.center + Vector3.up * 0.15f;   // off the floor
        return Physics.OverlapBox(c, half * 0.8f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
            .Count(col => col.bounds.size.y < 3.5f && col.bounds.min.y > Floor2 - 0.5f);   // furniture, not the floor or the walls
    }

    static Vector3 Floor(Bounds b) => new Vector3(b.center.x, b.min.y, b.center.z);

    // skinned bounds don't follow SampleAnimation in the editor; the bones do
    static Bounds Bounds(IEnumerable<Renderer> rs)
    {
        Bounds? b = null;
        foreach (var r in rs)
        {
            var bones = r is SkinnedMeshRenderer s ? s.bones.Where(x => x != null).Select(x => x.position) : new[] { r.bounds.center };
            foreach (var p in bones)
            {
                if (b == null) b = new Bounds(p, Vector3.zero);
                else { var x = b.Value; x.Encapsulate(p); b = x; }
            }
        }
        var res = b ?? new Bounds();
        res.Expand(0.25f);   // bones are inside the skin
        return res;
    }

    static Bounds RendererBounds(IEnumerable<Renderer> rs)
    {
        Bounds? b = null;
        foreach (var r in rs)
        {
            if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
            if (b == null) b = r.bounds;
            else { var x = b.Value; x.Encapsulate(r.bounds); b = x; }
        }
        return b ?? new Bounds();
    }

    static RuntimeAnimatorController StabController(out AnimationClip clip)
    {
        clip = AssetDatabase.LoadAllAssetsAtPath(MacheteFbx).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
        string path = Dir + "/PrologueStab.controller";
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ac != null) return ac;
        ac = AnimatorController.CreateAnimatorControllerAtPath(path);
        ac.layers[0].stateMachine.AddState("Stab").motion = clip;
        return ac;
    }

    // the FBX comes in untextured: the office act already made textured materials for it
    static void RigMaterials(GameObject rig)
    {
        Material M(string n) => AssetDatabase.LoadAssetAtPath<Material>(OfficeMats + "/" + n + ".mat");
        var map = new Dictionary<string, Material>
        {
            { "M_Jason_V9_skin", M("RigV9_Head") },
            { "M_Jason_V9_UpperBod", M("RigV9_UpperBody") },
            { "M_Jason_V9_LowerBod", M("RigV9_LowerBody") },
            { "M_Jason_V9_Eye_inst", M("RigV9_Eye") },
            { "J9_hair_inst", M("RigV9_Hair") },
            { "M_Jason_V9_MAsk_inst", M("Jason_Mask") },
            { "EyeReflection_0", M("Office_Black") },
            { "HairMaster_Prep_Inst", M("RigPrep_Hair") },
            { "ClothMAster_Prep_Inst", M("RigPrep_Clothes") },
            { "SkinMaster_Prep_Inst", M("RigPrep_Body") },
            { "EyeMAster_Prep_Inst", M("Rig_Eyes") },
            { "jason_machete", M("Rig_Machete") },
        };
        foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = r.sharedMaterials.Select(m => m != null && map.TryGetValue(m.name.Replace(".1", ""), out var t) && t != null ? t : m).ToArray();
    }

    static Interactable BuildPc()
    {
        var furn = GameObject.Find("Structure_02").transform.Find("Interior/2nd Floor/Apartment_01/Furniture/Table_Computer_01_Setup");
        var screen = furn.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name == "Screen");
        var monitor = furn.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name == "Monitor_Apt_01");
        Vector3 at = monitor != null ? monitor.bounds.center : new Vector3(2.19f, 5.72f, -8.1f);

        if (screen != null)
        {
            Undo.RecordObject(screen, "CCTV screen");
            screen.sharedMaterial = mScreen;   // the playback software, glowing grey in the dark
        }
        var pc = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pc.name = "PC (camera playback)";
        pc.transform.SetParent(root);
        pc.transform.position = at;
        pc.transform.localScale = new Vector3(1.05f, 0.75f, 0.3f);
        Object.DestroyImmediate(pc.GetComponent<Renderer>());
        Object.DestroyImmediate(pc.GetComponent<MeshFilter>());
        var it = pc.AddComponent<Interactable>();
        it.prompt = "Watch the camera footage";
        var lbl = Label(root, "CAM 01 ● REC", at + new Vector3(0f, 0.12f, 0.13f), Vector3.back, 1.1f, new Color(0.85f, 0.95f, 0.9f));
        lbl.rectTransform.sizeDelta = new Vector2(0.9f, 0.2f);
        Lamp(root, at + new Vector3(0f, 0f, 0.7f), new Color(0.6f, 0.75f, 0.8f), 0.6f, 2.5f);
        return it;
    }

    // ---------------------------------------------------------------- the killer

    static FleeingKiller BuildKiller(Transform player, List<PrologueDoor> doors)
    {
        var avatar = AssetDatabase.LoadAllAssetsAtPath(JasonFbx).OfType<Avatar>().FirstOrDefault();
        var k = Node(root, "Killer", Vector3.zero);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(JasonFbx), k);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * RigScale;
        var anim = model.GetComponent<Animator>();
        if (anim == null) anim = model.AddComponent<Animator>();
        anim.runtimeAnimatorController = KillerController();
        if (avatar != null && avatar.isHuman) anim.avatar = avatar;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var cap = k.gameObject.AddComponent<CapsuleCollider>();
        cap.center = Vector3.up * 1f;
        cap.height = 2.05f;
        cap.radius = 0.4f;
        cap.isTrigger = true;   // you can't grab him, only chase him

        var f = k.gameObject.AddComponent<FleeingKiller>();
        f.player = player;
        f.animator = anim;
        f.steps = Source(k.gameObject, null, false, spatial: true);
        f.steps.maxDistance = 22f;
        f.steps.rolloffMode = AudioRolloffMode.Linear;
        f.stepClips = Footsteps();

        // hiding in the hall closet next to the PC -> behind your back -> out of the front door
        var route = new List<Vector3>
        {
            new Vector3(-2.6f, Floor2, -7.5f),
            new Vector3(-1.4f, Floor2, -7.5f),
            new Vector3(-0.2f, Floor2, -7.0f),
            new Vector3(0.55f, Floor2, -5.6f),
            new Vector3(0.6f, Floor2, -3.2f),
            new Vector3(0.4f, Floor2, -1.0f),
            new Vector3(-1.0f, Floor2, 0.2f),
            new Vector3(-2.5f, Floor2, 0.1f),
        };
        f.sneakUntil = route.Count - 1;
        route.Add(new Vector3(-2.5f, Floor2, -2.6f));
        route.Add(new Vector3(-4.9f, Floor2, -4.9f));
        route.Add(new Vector3(-7.3f, Floor2, -5.0f));
        foreach (float L in new[] { 4f, 8f, 12f })
        {
            route.Add(new Vector3(-7.4f, L + 0.05f, -7.2f));
            route.Add(new Vector3(-8.4f, L + 0.05f, -7.4f));
            route.Add(new Vector3(-11.3f, L + 2.25f, -7.4f));
            route.Add(new Vector3(-12.6f, L + 2.25f, -6.9f));
            route.Add(new Vector3(-12.6f, L + 2.25f, -5.5f));
            route.Add(new Vector3(-11.2f, L + 2.25f, -5.0f));
            route.Add(new Vector3(-8.6f, L + 4.05f, -5.0f));
        }
        route.Add(new Vector3(-7.2f, TopLanding, -5.0f));
        route.Add(new Vector3(-5.4f, RoofY, -5.0f));    // through the bulkhead door
        route.Add(new Vector3(-3.5f, RoofY, -3.5f));
        route.Add(new Vector3(2.0f, RoofY, -2.0f));
        route.Add(new Vector3(5.9f, RoofY, -2.0f));     // the east edge, over the lobby doors: 16 m down to the street
        f.route = route.ToArray();
        f.doors = doors.Where(d => d.transform.position.x < -1.5f && (d.transform.position.y > 3f && d.transform.position.y < 7f
            || d.transform.position.y > TopLanding - 0.5f && d.transform.position.y < TopLanding + 1f)).ToArray();   // + the roof door

        k.position = route[0];
        k.gameObject.SetActive(false);
        return f;
    }

    // ---------------------------------------------------------------- the fall (the user's Mixamo clips)

    const string FallDir = "Assets/New Folder/FallAnimation";
    const string PoolFbx = FallDir + "/Falling Into Pool.fbx";         // the killer, going over the edge
    const string FallFlatFbx = FallDir + "/Falling Flat Impact.fbx";   // you (the Ch33 body comes with it), hitting the street

    // "Falling Into Pool": only its first 0.9 s (flailing, horizontal) looped; after that it's the splash, which
    // would jump every loop
    static void FallClipImport()
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(PoolFbx);
        if (mi == null) return;
        var clips = mi.clipAnimations.Length > 0 ? mi.clipAnimations : mi.defaultClipAnimations;
        if (clips.Length == 0) return;
        if (Mathf.Approximately(clips[0].lastFrame, 27f) && clips[0].loopTime) return;
        clips[0].firstFrame = 0f;
        clips[0].lastFrame = 27f;
        clips[0].loopTime = true;
        clips[0].name = "Falling Into Pool";
        mi.clipAnimations = clips;
        mi.SaveAndReimport();
    }

    static AnimationClip ClipOf(string fbx) =>
        AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));

    // into the prologue scene as it is (no rebuild)
    [MenuItem("HellScape/Prologue: Fall Animations")]
    public static void ApplyFallAnimations()
    {
        var d = Object.FindFirstObjectByType<PrologueDirector>(FindObjectsInactive.Include);
        if (d == null) { Debug.LogWarning("Open the Prologue scene first."); return; }
        root = d.transform.parent;
        var ac = KillerController() as AnimatorController;   // adds the Fall state
        if (d.killer != null && d.killer.animator != null) d.killer.animator.runtimeAnimatorController = ac;
        var old = root.Find("You (falling)");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        Undo.RecordObject(d, "Fall animations");
        d.playerBody = BuildFallBody();
        EditorUtility.SetDirty(d);
        EditorSceneManager.MarkSceneDirty(d.gameObject.scene);
        Debug.Log("HellScape: the fall now plays Falling Into Pool on the killer and Falling Flat Impact on you.");
    }

    // the killer's "Fall" state (added to his controller if it isn't there yet)
    static void AddFallState(AnimatorController ac)
    {
        FallClipImport();
        var clip = ClipOf(PoolFbx);
        if (clip == null) return;
        var sm = ac.layers[0].stateMachine;
        var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Fall") ?? sm.AddState("Fall", new Vector3(300f, 120f, 0f));
        st.motion = clip;
        EditorUtility.SetDirty(ac);
    }

    // you, for the last second of the fall: the Ch33 body that came with "Falling Flat Impact", playing it
    static Animator BuildFallBody()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FallFlatFbx);
        var clip = ClipOf(FallFlatFbx);
        if (model == null || clip == null) return null;
        string path = Dir + "/PlayerFallFlat.controller";
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ac.layers[0].stateMachine;
        var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "FallFlat") ?? sm.AddState("FallFlat");
        st.motion = clip;
        sm.defaultState = st;
        EditorUtility.SetDirty(ac);

        var body = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
        body.name = "You (falling)";
        var anim = body.GetComponent<Animator>();
        if (anim == null) anim = body.AddComponent<Animator>();
        anim.runtimeAnimatorController = ac;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = true;
        body.SetActive(false);
        return anim;
    }

    // the civilians' idle/walk and the zombie pack's run (the office act's humanoid copy)
    static RuntimeAnimatorController KillerController()
    {
        string path = Dir + "/PrologueKiller.controller";
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ac != null) { AddFallState(ac); return ac; }
        var civ = AssetDatabase.LoadAssetAtPath<AnimatorController>(CivilianController);
        var civTree = (BlendTree)civ.layers[0].stateMachine.states[0].state.motion;
        var run = AssetDatabase.LoadAllAssetsAtPath(KillerRunFbx).OfType<AnimationClip>().First(c => c.name == "KillerRun");
        ac = AnimatorController.CreateAnimatorControllerAtPath(path);
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(civTree.children[0].motion, 0f);
        tree.AddChild(civTree.children[1].motion, 1.3f);
        tree.AddChild(run, 4.5f);
        AddFallState(ac);
        return ac;
    }

    // ---------------------------------------------------------------- the flat, the lobby, the street

    static void BuildClues(Transform building)
    {
        var c = Node(root, "Clues", Vector3.zero);

        // the lobby notice board: why the flat has its own camera
        if (Physics.Raycast(new Vector3(-3.5f, 1.6f, -5f), Vector3.back, out RaycastHit wall, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            var n = Box(c, "Notice", wall.point + wall.normal * 0.02f, new Vector3(0.6f, 0.8f, 0.02f), mPaper);
            n.transform.rotation = Quaternion.LookRotation(-wall.normal);
            var title = Label(c, "NOTICE", n.transform.position + wall.normal * 0.02f + Vector3.up * 0.28f, -wall.normal, 2f, new Color(0.1f, 0.05f, 0.05f));
            title.rectTransform.sizeDelta = new Vector2(0.6f, 0.2f);
            Inter(n, "Read the notice", "\"NOTICE TO RESIDENTS: Building CCTV is out of service until further notice. Please keep your doors locked. - Management\"|Figures. Good thing Derek put up our own camera.");
        }

        // Derek's phone on the dining table
        var phone = Box(c, "Derek's phone", new Vector3(-4.5f, 5.215f, 5.2f), new Vector3(0.08f, 0.012f, 0.16f), mPhone);
        phone.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
        Inter(phone, "Derek's phone",
            "Derek's phone. Three missed calls from \"K\".|One message:|\"Keep your mouth shut about the silo. Or you go in it too.\"|...K? Kessler? Our boss?|What did you get yourself into, Derek...");

        // the TV on the wall, still on
        var tvProp = building.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "TV_Apt_01");
        Vector3 tvFront = new Vector3(-1.0f, 6.1f, -3.2f);
        if (tvProp != null)
        {
            var b = RendererBounds(tvProp.GetComponentsInChildren<Renderer>());
            tvFront = new Vector3(b.max.x + 0.015f, b.center.y, b.center.z);
        }
        var tv = GameObject.CreatePrimitive(PrimitiveType.Quad);
        tv.name = "TV";
        tv.transform.SetParent(c);
        tv.transform.SetPositionAndRotation(tvFront, Quaternion.LookRotation(Vector3.left));
        tv.transform.localScale = new Vector3(1.25f, 0.7f, 1f);
        tv.GetComponent<Renderer>().sharedMaterial = Mat("TvGlow", new Color(0.05f, 0.07f, 0.1f), new Color(0.35f, 0.5f, 0.8f));
        Object.DestroyImmediate(tv.GetComponent<Collider>());
        tv.AddComponent<BoxCollider>().size = new Vector3(1f, 1f, 0.1f);
        Inter(tv, "Watch the news",
            "\"...still no leads in the disappearance of Tomas, a night cleaner at the Kessler & Vane depot, missing now for three weeks...\"|Kessler & Vane. That's where Derek and I work.|Derek hasn't slept right since.");
        var glow = Lamp(c, tvFront + Vector3.right * 0.6f, new Color(0.45f, 0.6f, 1f), 1.2f, 5f);
        var gf = glow.gameObject.AddComponent<LightFlicker>();
        gf.calmTime = new Vector2(0.2f, 1.2f);
        gf.burstTime = new Vector2(0.05f, 0.15f);
        gf.minBrightness = 0.4f;

        // a photo on the side table by the sofa
        var photo = Box(c, "Photo", new Vector3(5.1f, 4.98f, -5.5f), new Vector3(0.03f, 0.2f, 0.26f), mDark);
        photo.transform.rotation = Quaternion.Euler(0f, 0f, -12f);
        Inter(photo, "Look at the photo", "Me and Derek. His birthday.|He burned the cake and blamed me for it.|...he always blamed me for everything.");

        // a note on the fridge
        var note = Box(c, "Fridge note", new Vector3(-0.35f, 5.9f, 7.3f), new Vector3(0.18f, 0.22f, 0.01f), mPaper);
        Inter(note, "Read the note", "\"Bro - rent's due Friday. Don't touch my leftovers. Breakfast's on me when you're back from your run. - D\"|He was waiting up for me.");
    }

    static void BuildStreet()
    {
        var s = Node(root, "Street", Vector3.zero);
        foreach (var p in new[] { new Vector3(9.8f, 0f, 2f), new Vector3(9.8f, 0f, -9f) })
        {
            Box(s, "Lamp post", p + Vector3.up * 2.2f, new Vector3(0.12f, 4.4f, 0.12f), mMetal);
            Box(s, "Lamp head", p + new Vector3(-0.3f, 4.4f, 0f), new Vector3(0.7f, 0.12f, 0.25f), mMetal, false);
            Lamp(s, p + new Vector3(-0.4f, 4.2f, 0f), new Color(1f, 0.72f, 0.4f), 4f, 13f);
        }
        Lamp(s, new Vector3(7.2f, 3.6f, -3.3f), new Color(1f, 0.85f, 0.65f), 1.5f, 6f);   // over the lobby doors
    }

    // ---------------------------------------------------------------- morning (the walk home after a night shift)

    [MenuItem("HellScape/Prologue: Morning + Street Life")]
    static void ApplyMorning()
    {
        var d = Object.FindFirstObjectByType<PrologueDirector>(FindObjectsInactive.Include);
        if (d == null) { Debug.LogError("HellScape: open the Prologue scene first."); return; }
        root = d.transform.parent;
        Undo.RecordObject(d, "Morning");
        d.moon = Morning();
        MorningLines(d);
        EditorUtility.SetDirty(d);
        var p = d.fpc.transform;
        Undo.RecordObject(p, "Morning");
        p.SetPositionAndRotation(PlayerStart + Vector3.up * PlayerScale, Quaternion.Euler(0f, PlayerStartYaw, 0f));
        BuildStreetLife(p);
        EditorSceneManager.MarkSceneDirty(d.gameObject.scene);
    }

    static void MorningLines(PrologueDirector d)
    {
        d.timeCard = "SATURDAY  -  7:12 AM";
        d.arriveLines = "Van's back. Clocked out. Twelve hours on the road.|Left my car keys in my desk again... forget it, I'll walk.|Derek said he'd have breakfast waiting. Like old times.";
        d.tapeStart = "Playback. This morning, 06:52.";
        d.afterTape = "That mask...|He was HERE. In our home.|...wait. 06:52.|That was twenty minutes ago.";
        d.tapeStartSeconds = 6 * 3600 + 52 * 60;
    }

    static Light Morning()
    {
        string skyPath = Dir + "/MorningSky.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (sky == null)
        {
            sky = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(sky, skyPath);
        }
        sky.SetFloat("_SunSize", 0.045f);
        sky.SetFloat("_AtmosphereThickness", 1.05f);
        sky.SetColor("_SkyTint", new Color(0.55f, 0.6f, 0.72f));
        sky.SetColor("_GroundColor", new Color(0.36f, 0.34f, 0.32f));
        sky.SetFloat("_Exposure", 1.15f);
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.6f, 0.64f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.47f, 0.43f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.19f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.74f, 0.75f, 0.78f);
        RenderSettings.fogDensity = 0.006f;

        var sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null)
        {
            Undo.RecordObject(sun, "Sun");
            Undo.RecordObject(sun.transform, "Sun");
            sun.name = "Sun";
            sun.color = new Color(1f, 0.86f, 0.68f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(22f, 255f, 0f);   // low in the east, behind you as you walk home
            RenderSettings.sun = sun;
        }
        DynamicGI.UpdateEnvironment();
        return sun;
    }

    // ---------------------------------------------------------------- street life: traffic and people on the pavements

    const string CivilianControllerPath = "Assets/Enemies/Civilian.controller";
    static readonly (string path, float scale, bool backwards)[] Cars =   // backwards: the nose ends up at the back without a half turn
    {
        ("Assets/Perishables/1976-ford-fiesta-mk1/source/Fiesta.fbx", 1.65f, false),
        ("Assets/Perishables/2004-honda-s2000-ap1/source/S2000.fbx", 1.64f, false),
        ("Assets/Perishables/chevrolet-caprice-police/source/chevrolet caprice police/chevrolet caprice police.fbx", 1f, true),   // a full-size saloon: 5.4 m, the biggest car on the street
    };
    static readonly (string path, float scale)[] People =
    {
        ("Assets/Enemies/MalePop/source/Walking.fbx", 0.89f),
        ("Assets/Enemies/FemalePop/source/WOMAN 128 FINAL.blend", 0.41f),
        ("Assets/Enemies/KidPop/source/Sketchfab_2020_12_11_17_17_43.fbx", 0.64f),
    };

    // the road's centre line, from far down the south road (out of sight) to the flats at the dead end
    static readonly Vector2[] RoadCentre =
    {
        new Vector2(120.5f, -140f), new Vector2(124.5f, -130f), new Vector2(129f, -120f), new Vector2(133f, -110f),
        new Vector2(134f, -100f), new Vector2(129.5f, -90f), new Vector2(122.5f, -80f), new Vector2(115.5f, -70f),
        new Vector2(108.5f, -60f), new Vector2(101.5f, -50f), new Vector2(94.5f, -40f), new Vector2(88f, -30f),
        new Vector2(81.5f, -20f), new Vector2(75f, -10f), new Vector2(68f, -1.5f), new Vector2(60f, 0f),
        new Vector2(40f, 0f), new Vector2(26f, 0f),
    };
    const float LaneOffset = 3f;

    static void BuildStreetLife(Transform player)
    {
        var old = root.Find("StreetLife");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var life = Node(root, "StreetLife", Vector3.zero);
        Undo.RegisterCreatedObjectUndo(life.gameObject, "Street life");
        Physics.SyncTransforms();
        var rnd = new System.Random(7);

        // the loop: in along the right-hand lane, round at the dead end, back out along the other
        var centre = Densify(RoadCentre.Select(v => new Vector3(v.x, 0f, v.y)).ToList(), 1f);
        var inbound = Offset(centre, LaneOffset);
        var back = Enumerable.Reverse(centre).ToList();
        var outbound = Offset(back, LaneOffset);
        var loop = new List<Vector3>(inbound);
        Vector3 turn = centre[centre.Count - 1];
        for (int i = 1; i < 12; i++)
        {
            float a = Mathf.PI * i / 12f;
            loop.Add(turn + new Vector3(-LaneOffset * Mathf.Sin(a), 0f, LaneOffset * Mathf.Cos(a)));
        }
        loop.AddRange(outbound);
        for (int i = 0; i < loop.Count; i++) loop[i] = OnGround(loop[i]);
        float length = 0f;
        for (int i = 1; i < loop.Count; i++) length += Vector3.Distance(loop[i - 1], loop[i]);

        const int movingCars = 6;
        for (int i = 0; i < movingCars; i++)
        {
            var car = Car(life, "Car " + i, Cars[i % Cars.Length]);
            var drv = car.AddComponent<LoopDriver>();
            drv.path = loop.ToArray();
            drv.startAt = length * i / movingCars + (float)rnd.NextDouble() * 12f;
            drv.speed = 7f + (float)rnd.NextDouble() * 3f;
            drv.player = player;
        }

        // parked along both kerbs of the street
        var parked = new[] { new Vector3(31f, 0f, 7.6f), new Vector3(46f, 0f, 7.6f), new Vector3(57f, 0f, 7.6f), new Vector3(37f, 0f, -7.6f), new Vector3(52f, 0f, -7.6f) };
        for (int i = 0; i < parked.Length; i++)
        {
            var car = Car(life, "Parked car " + i, Cars[i % 2]);
            car.transform.SetPositionAndRotation(OnGround(parked[i]), Quaternion.Euler(0f, parked[i].z > 0f ? -90f : 90f, 0f));
        }

        // people on the pavements: both sides of the street, and both sides of the south road down past the office
        var walks = new[]
        {
            (new Vector3(4f, 0f, 10.3f), new Vector3(62f, 0f, 10.3f), 6),
            (new Vector3(0f, 0f, -10.2f), new Vector3(28f, 0f, -10.2f), 3),
            (new Vector3(70.5f, 0f, -20f), new Vector3(83.5f, 0f, -40f), 2),
            (new Vector3(92.5f, 0f, -20f), new Vector3(106f, 0f, -40f), 2),
            (new Vector3(66f, 0f, 9.5f), new Vector3(73.5f, 0f, -6f), 2),
        };
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CivilianControllerPath);
        int n = 0;
        foreach (var (a, b, count) in walks)
            for (int i = 0; i < count; i++, n++)
            {
                var (path, scale) = People[n % People.Length];
                var holder = new GameObject("Pedestrian " + n).transform;
                holder.SetParent(life, false);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), holder);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * scale * (0.94f + (float)rnd.NextDouble() * 0.12f);
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                var anim = model.GetComponentInChildren<Animator>();
                if (anim != null) { anim.runtimeAnimatorController = controller; anim.applyRootMotion = false; }
                var w = holder.gameObject.AddComponent<PavementWalker>();
                Vector3 side = Vector3.Cross(Vector3.up, (b - a).normalized) * ((float)rnd.NextDouble() - 0.5f) * 1.4f;   // not all on one line
                w.a = a + side;
                w.b = b + side;
                w.startAt = (float)rnd.NextDouble();
                w.speed = 1.05f + (float)rnd.NextDouble() * 0.45f;
                w.animator = anim;
                holder.position = OnGround(Vector3.Lerp(w.a, w.b, w.startAt));
            }
    }

    // a car model under a holder whose +z is the car's nose (the models are modelled along x)
    static GameObject Car(Transform parent, string name, (string path, float scale, bool backwards) spec)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(parent, false);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(spec.path), holder.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * spec.scale;
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        var b = RenderBounds(model);
        float yaw = (b.size.x > b.size.z ? -90f : 0f) + (spec.backwards ? 180f : 0f);   // long along x: onto z; then nose to +z
        model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        b = RenderBounds(model);
        model.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);   // centred, wheels on the holder
        b = RenderBounds(model);
        var box = holder.AddComponent<BoxCollider>();
        box.center = b.center - holder.transform.position;
        box.size = b.size;
        var rb = holder.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        if (spec.path.Contains("Fiesta") || spec.path.Contains("S2000")) model.AddComponent<RandomTint>();
        return holder;
    }

    static Bounds RenderBounds(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Vector3 OnGround(Vector3 p)
    {
        var hits = Physics.RaycastAll(new Vector3(p.x, 3f, p.z), Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
        if (hits.Length > 0) p.y = hits.Max(h => h.point.y);
        return p;
    }

    // smooth (Catmull-Rom) and resample every `step` metres
    static List<Vector3> Densify(List<Vector3> pts, float step)
    {
        var smooth = new List<Vector3>();
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 p0 = pts[Mathf.Max(i - 1, 0)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(i + 2, pts.Count - 1)];
            int n = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / step));
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)n, t2 = t * t, t3 = t2 * t;
                smooth.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3));
            }
        }
        smooth.Add(pts[pts.Count - 1]);
        return smooth;
    }

    // shift a line sideways, to the right of its direction of travel
    static List<Vector3> Offset(List<Vector3> pts, float d)
    {
        var o = new List<Vector3>(pts.Count);
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 t = pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)];
            t.y = 0f;
            o.Add(pts[i] + Vector3.Cross(Vector3.up, t.normalized) * d);
        }
        return o;
    }

    // ---------------------------------------------------------------- helpers

    static Transform Node(Transform parent, string name, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent);
        t.position = pos;
        return t;
    }

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m, bool collider = true)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent);
        g.transform.position = pos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static Light Lamp(Transform p, Vector3 pos, Color c, float intensity, float range)
    {
        var g = new GameObject("Light");
        g.transform.SetParent(p);
        g.transform.position = pos;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    static TextMeshPro Label(Transform p, string text, Vector3 pos, Vector3 facing, float size, Color c)
    {
        var g = new GameObject("Label");
        g.transform.SetParent(p, false);
        g.transform.localPosition = pos;
        g.transform.rotation = Quaternion.LookRotation(facing);
        var t = g.AddComponent<TextMeshPro>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = c;
        t.alignment = TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta = new Vector2(3f, 1f);
        return t;
    }

    static Interactable Inter(GameObject g, string prompt, string line)
    {
        var it = g.AddComponent<Interactable>();
        it.prompt = prompt;
        it.line = line;
        it.once = false;
        return it;
    }
}
