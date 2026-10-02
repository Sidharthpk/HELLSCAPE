using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// HellScape > UI > Build Modern UI: the in-game UI of every open scene, redone in one clean style.
// Flat dark rounded panels, the pixel font throughout (the PS1 look), amber for anything that is YOU (your lines,
// your answers, the key to press), white for everyone else.
//   - the dialogue box: a subtitle panel with a name plate and accent bar (DialogueBox colours them per speaker),
//     an "E continue" hint, and thin letterbox bars that slide in while someone is talking
//   - the interact prompt: a key chip and a label
//   - the conversation panel (answers 1-4), the health bar and the HUD's labels restyled to match
//   - a pause menu (Esc): resume, restart from checkpoint, controls, volume, sensitivity, quit
// The title screen, the death screen, the end cards and the CCTV overlay keep their own look.
// Safe to run again: it rebuilds what it made and re-applies the styling.
public static class ModernUIBuilder
{
    const string Folder = "Assets/UI/Modern";
    static readonly Color Panel = new Color(0.035f, 0.04f, 0.055f, 0.88f);
    static readonly Color Amber = new Color(1f, 0.80f, 0.36f);
    static readonly Color Muted = new Color(0.62f, 0.65f, 0.70f);
    static readonly Color Soft = new Color(0.80f, 0.82f, 0.86f);
    static readonly Color Red = new Color(0.88f, 0.21f, 0.17f);
    static readonly string[] KeepOwnLook = { "GameOverPanel", "TitleCard", "EndCard_Angel", "EndCard_Continued", "EndCard_Demon", "CctvOverlay", "CarDestroyedMessage", "PauseMenu" };

    static TMP_FontAsset body, head;
    static Material headClean;
    static Sprite round;
    const string H = DialogueBox.HeadFont;

    [MenuItem("HellScape/UI/Build Modern UI")]
    public static void Menu()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded) Debug.Log(s.name + ": " + Build(s));
        }
    }

    public static string Build(Scene scene)
    {
        var dialogue = Object.FindObjectsByType<DialogueBox>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(d => d.gameObject.scene == scene);
        if (dialogue == null) return "no dialogue box";
        Assets();
        var canvas = dialogue.transform.parent as RectTransform;
        // (it had been switched off in the prologue while the street was being edited: nothing talks without it)
        if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);

        Dialogue(dialogue, canvas);
        int prompts = 0;
        foreach (var pi in Object.FindObjectsByType<PlayerInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(p => p.gameObject.scene == scene))
            if (Prompt(pi, canvas)) prompts++;
        var choice = Object.FindObjectsByType<ChoiceDialogue>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.gameObject.scene == scene);
        if (choice != null) Choice(choice);
        int texts = Fonts(canvas);
        Health(canvas);
        Speedo(canvas);
        Pause(canvas, scene);

        EditorSceneManager.MarkSceneDirty(scene);
        return $"dialogue box, {prompts} interact prompt(s){(choice != null ? ", conversation panel" : "")}, {texts} HUD texts restyled, pause menu";
    }

    // ---------------------------------------------------------------- shared look

    static void Assets()
    {
        // one face for everything: the pixel font (the PS1 look), in the new layout
        body = head = Font("DOS Pixel");
        headClean = head.material;
        Directory.CreateDirectory(Folder);
        string path = Folder + "/RoundedRect.png";
        if (!File.Exists(path))
        {
            const int S = 64; const float R = 14f;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    // distance outside the rounded outline, for a one-pixel soft edge
                    float cx = Mathf.Clamp(x + 0.5f, R, S - R), cy = Mathf.Clamp(y + 0.5f, R, S - R);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) - R;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d)));
                }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spriteBorder = new Vector4(20f, 20f, 20f, 20f);
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        round = AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static TMP_FontAsset Font(string name) =>
        AssetDatabase.FindAssets("t:TMP_FontAsset " + name).Select(g => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g))).First(f => f.name == name);

    static RectTransform Node(string name, Transform parent, params System.Type[] comps)
    {
        var go = new GameObject(name, new[] { typeof(RectTransform) }.Concat(comps).ToArray());
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static void Place(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
    }

    static Image Rounded(RectTransform rt, Color c)
    {
        var img = rt.GetComponent<Image>(); if (img == null) img = rt.gameObject.AddComponent<Image>();
        img.sprite = round; img.type = Image.Type.Sliced; img.color = c; img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI Text(RectTransform rt, string s, TMP_FontAsset font, float size, Color c, TextAlignmentOptions align, float spacing = 0f)
    {
        var t = rt.GetComponent<TextMeshProUGUI>(); if (t == null) t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.fontSharedMaterial = font == head ? headClean : font.material; t.text = s; t.fontSize = size; t.color = c; t.alignment = align;
        t.characterSpacing = spacing; t.raycastTarget = false; t.enableAutoSizing = false; t.richText = true;
        return t;
    }

    // ---------------------------------------------------------------- the dialogue box

    static void Dialogue(DialogueBox d, RectTransform canvas)
    {
        var box = (RectTransform)d.transform;
        Place(box, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(1180f, 178f));
        foreach (var child in box.Cast<Transform>().ToArray())
            if (child != d.text.transform) Object.DestroyImmediate(child.gameObject);

        var panel = Node("Panel", box); Stretch(panel); Rounded(panel, Panel);
        var accent = Node("Accent", box, typeof(Image));
        Place(accent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(5f, -44f));
        accent.GetComponent<Image>().color = Amber; accent.GetComponent<Image>().raycastTarget = false;
        var name = Node("Name", box);
        Place(name, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42f, -18f), new Vector2(800f, 34f));
        var nameText = Text(name, "YOU", head, 25f, Amber, TextAlignmentOptions.TopLeft, 5f);
        var hint = Node("Hint", box);
        Place(hint, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 14f), new Vector2(300f, 28f));
        var hintText = Text(hint, H + "<color=#" + ColorUtility.ToHtmlStringRGB(Amber) + ">E</color>   continue", body, 24f, Muted, TextAlignmentOptions.BottomRight);

        var textRt = (RectTransform)d.text.transform;
        textRt.SetAsLastSibling();
        Stretch(textRt, 42f, 30f, 44f, 56f);
        Text(textRt, "", body, 31f, Color.white, TextAlignmentOptions.TopLeft);
        d.text.textWrappingMode = TextWrappingModes.Normal;
        d.text.lineSpacing = 6f;
        d.text.margin = Vector4.zero;
        hint.SetAsLastSibling();

        // the letterbox, behind the box
        RectTransform Bar(string n, bool top)
        {
            var old = canvas.Find(n); if (old != null) Object.DestroyImmediate(old.gameObject);
            var b = Node(n, canvas, typeof(Image));
            float y = top ? 1f : 0f;
            Place(b, new Vector2(0f, y), new Vector2(1f, y), new Vector2(0.5f, y), Vector2.zero, Vector2.zero);
            b.GetComponent<Image>().color = Color.black; b.GetComponent<Image>().raycastTarget = false;
            b.SetSiblingIndex(box.GetSiblingIndex());
            return b;
        }
        d.barTop = Bar("DialogueBarTop", true);
        d.barBottom = Bar("DialogueBarBottom", false);
        d.nameText = nameText; d.accent = accent.GetComponent<Image>(); d.hintText = hintText;
        d.border = new Graphic[0];
        d.charDelay = 0.03f;
        d.group.alpha = 0f;
        EditorUtility.SetDirty(d);
    }

    // ---------------------------------------------------------------- "F  Inspect"

    static bool Prompt(PlayerInteractor pi, RectTransform canvas)
    {
        var old = canvas.Find("InteractPrompt");
        int index = old != null ? old.GetSiblingIndex() : canvas.childCount;
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = Node("InteractPrompt", canvas, typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        root.SetSiblingIndex(index);
        Place(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(300f, 52f));
        Rounded(root, Panel);
        var lay = root.GetComponent<HorizontalLayoutGroup>();
        lay.padding = new RectOffset(10, 20, 8, 8); lay.spacing = 14f; lay.childAlignment = TextAnchor.MiddleCenter;
        lay.childControlWidth = lay.childControlHeight = true; lay.childForceExpandWidth = lay.childForceExpandHeight = false;
        var fit = root.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var chip = Node("Key", root, typeof(LayoutElement));
        Rounded(chip, Amber);
        var le = chip.GetComponent<LayoutElement>(); le.minWidth = le.preferredWidth = 38f; le.minHeight = le.preferredHeight = 36f;
        var keyRt = Node("Text", chip); Stretch(keyRt);
        var key = Text(keyRt, pi.key.ToString(), head, 24f, new Color(0.08f, 0.07f, 0.05f), TextAlignmentOptions.Center);
        var labelRt = Node("Label", root);
        var label = Text(labelRt, "Inspect", body, 26f, Color.white, TextAlignmentOptions.MidlineLeft);
        label.textWrappingMode = TextWrappingModes.NoWrap;

        pi.promptText = label; pi.keyText = key; pi.promptRoot = root.gameObject;
        root.gameObject.SetActive(false);
        EditorUtility.SetDirty(pi);
        return true;
    }

    // ---------------------------------------------------------------- conversations

    static void Choice(ChoiceDialogue c)
    {
        var panel = (RectTransform)c.panel.transform;
        Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1040f, 400f));
        Rounded(panel, Panel);
        var t = (RectTransform)c.text.transform;
        Stretch(t, 44f, 30f, 44f, 30f);
        Text(t, "", body, 29f, Color.white, TextAlignmentOptions.TopLeft);
        c.text.lineSpacing = 10f;
        c.text.textWrappingMode = TextWrappingModes.Normal;
        c.answerColor = Amber;
        EditorUtility.SetDirty(c);
    }

    // ---------------------------------------------------------------- the HUD's lettering

    static int Fonts(RectTransform canvas)
    {
        int n = 0;
        // (two texts were never pixel to begin with and stay as the user set them)
        var leave = KeepOwnLook.Concat(new[] { "AirBar", "PushPrompt" }).ToArray();
        foreach (var t in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (t.font == null || t.font == body || t.GetComponentsInParent<Transform>(true).Any(p => leave.Contains(p.name))) continue;
            if (t.font.name != "LiberationSans SDF" && t.font.name != "Oswald Bold SDF") continue;
            t.font = body;
            t.fontSharedMaterial = body.material;
            EditorUtility.SetDirty(t);
            n++;
        }
        return n;
    }

    static Sprite HeartSprite(string name) => Picture("Heart_" + name + ".png", false);   // (a render of the heart model: HudIconRenderer)

    // a HUD picture from the Modern folder: pixel art keeps hard pixels; anything else is smooth, with mipmaps so
    // it stays smooth when it's drawn small or scaled (the beating heart)
    internal static Sprite Picture(string file, bool pixelArt)
    {
        string path = Folder + "/" + file;
        if (AssetImporter.GetAtPath(path) == null && File.Exists(path)) AssetDatabase.ImportAsset(path);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return null;
        var filter = pixelArt ? FilterMode.Point : FilterMode.Trilinear;
        if (imp.textureType != TextureImporterType.Sprite || imp.filterMode != filter || imp.mipmapEnabled == pixelArt || imp.textureCompression != TextureImporterCompression.Uncompressed)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = filter;
            imp.mipmapEnabled = !pixelArt;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    [MenuItem("HellScape/UI/Build Health + Driving HUD")]
    public static void DrivingHudMenu()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            var dialogue = Object.FindObjectsByType<DialogueBox>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(d => d.gameObject.scene == s);
            if (!s.isLoaded || dialogue == null) continue;
            Assets();
            var canvas = dialogue.transform.parent as RectTransform;
            Health(canvas);   // the heart, and the van icon with it
            Debug.Log(s.name + ": driving HUD - " + Speedo(canvas));
            EditorSceneManager.MarkSceneDirty(s);
        }
    }

    // the speed, on a dial: an analogue speedometer bottom right while the van is driven (SpeedometerUI), in place
    // of the old digital read-out. Pictures by Tools/hud/make_driving_hud.py.
    static string Speedo(RectTransform canvas)
    {
        var scene = canvas.gameObject.scene;
        var ctl = Object.FindObjectsByType<PrometeoCarController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.gameObject.scene == scene);
        var dialSprite = Picture("Speedo_dial.png", false);
        var needleSprite = Picture("Speedo_needle.png", false);
        if (ctl == null || dialSprite == null || needleSprite == null) return "no van or no dial pictures, skipped";

        // (under GameHUD, so cutscenes hide it with the rest: CutsceneHUD)
        var hud = canvas.Find("GameHUD") as RectTransform;
        if (hud == null) { hud = Node("GameHUD", canvas); Stretch(hud); hud.SetAsFirstSibling(); }
        var old = hud.Find("Speedometer");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = Node("Speedometer", hud);
        // (up off the bottom edge: the rifle's ammo count sits in that corner)
        Place(root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 88f), new Vector2(256f, 256f));
        var dialRt = Node("Dial", root, typeof(Image)); Stretch(dialRt);
        var dial = dialRt.GetComponent<Image>(); dial.sprite = dialSprite; dial.raycastTarget = false;
        var needleRt = Node("Needle", dialRt, typeof(Image));
        // the picture's hub is 28 px up from its bottom edge: that's what it turns on
        Place(needleRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 28f / 152f), Vector2.zero, new Vector2(40f, 152f));
        var needle = needleRt.GetComponent<Image>(); needle.sprite = needleSprite; needle.raycastTarget = false;

        var sp = root.gameObject.AddComponent<SpeedometerUI>();
        sp.body = ctl.GetComponentInParent<Rigidbody>();
        sp.drivingWhile = ctl;
        sp.dial = dialRt.gameObject;
        sp.needle = needleRt;
        needleRt.localRotation = Quaternion.Euler(0f, 0f, sp.zeroAngle);
        dialRt.gameObject.SetActive(false);

        // the digital read-out it replaces: off, and no longer switched back on when you get in
        if (ctl.carSpeedText != null)
        {
            var txt = ctl.carSpeedText.gameObject;
            foreach (var ui in Object.FindObjectsByType<CarHealthUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (ui.showWhileDriving != null && ui.showWhileDriving.Contains(txt)) { ui.showWhileDriving = ui.showWhileDriving.Where(g => g != txt).ToArray(); EditorUtility.SetDirty(ui); }
            txt.SetActive(false);
            ctl.useUI = false;
            EditorUtility.SetDirty(ctl);
        }
        return "speedometer for " + ctl.name;
    }

    // your health: a pixel heart in the corner that beats faster the more hurt you are (HeartbeatUI). No number.
    static void Health(RectTransform canvas)
    {
        Van(canvas);
        var bar = canvas.Find("GameHUD/PlayerHealthBar") as RectTransform;
        if (bar == null) return;
        var slider = bar.GetComponent<Slider>();
        foreach (var child in bar.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
        Place(bar, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(48f, 56f), new Vector2(126f, 202f));   // (the heart render is tall: 249 x 399)
        bar.localScale = Vector3.one;
        if (slider != null) { slider.fillRect = null; slider.handleRect = null; slider.targetGraphic = null; slider.transition = Selectable.Transition.None; slider.interactable = false; }

        var healthy = HeartSprite("healthy");
        var beatRt = Node("Heart", bar); Stretch(beatRt);
        var glowRt = Node("Glow", beatRt, typeof(Image)); Stretch(glowRt);
        var glow = glowRt.GetComponent<Image>(); glow.sprite = healthy; glow.preserveAspect = true; glow.raycastTarget = false; glow.color = new Color(1f, 0.1f, 0.1f, 0.12f);
        var faceRt = Node("Face", beatRt, typeof(Image)); Stretch(faceRt);
        var face = faceRt.GetComponent<Image>(); face.sprite = healthy; face.preserveAspect = true; face.raycastTarget = false;

        var hb = bar.GetComponent<HeartbeatUI>(); if (hb == null) hb = bar.gameObject.AddComponent<HeartbeatUI>();
        hb.heart = beatRt; hb.face = face; hb.glow = glow;
        hb.healthy = healthy; hb.hurt = HeartSprite("hurt"); hb.critical = HeartSprite("critical");
        foreach (var ui in Object.FindObjectsByType<PlayerHealthUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (ui.healthSlider == slider) { ui.healthText = null; hb.health = ui.playerHealth; EditorUtility.SetDirty(ui); }
        EditorUtility.SetDirty(hb);
    }

    // the van: no bar, no number. Its state is painted on the van itself (VanDamageLook swaps in the more and
    // more wrecked versions of its texture) and shown as a small picture of the van where your heart sits on foot
    // (VanHealthUI, the same five stages; the pictures are renders of the van itself: HudIconRenderer).
    static void Van(RectTransform canvas)
    {
        var gauge = canvas.Find("GameHUD/CarHealthGauge") as RectTransform;
        if (gauge != null)
        {
            var slider = gauge.GetComponent<Slider>();
            foreach (var child in gauge.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            CarHealth car = null;
            if (slider != null)
            {
                slider.fillRect = null; slider.handleRect = null; slider.targetGraphic = null; slider.transition = Selectable.Transition.None; slider.interactable = false;
                foreach (var ui in Object.FindObjectsByType<CarHealthUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (ui.healthSlider == slider) { ui.healthText = null; car = ui.carHealth; EditorUtility.SetDirty(ui); }
            }
            var icons = Enumerable.Range(0, 5).Select(i => Picture("VanIcon_" + i + ".png", false)).Where(s => s != null).ToArray();
            if (icons.Length > 0)
            {
                Place(gauge, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(40f, 56f), new Vector2(238f, 168f));   // (the van render: 388 x 274)
                gauge.localScale = Vector3.one;
                var iconRt = Node("Van", gauge); Stretch(iconRt);
                var faceRt = Node("Face", iconRt, typeof(Image)); Stretch(faceRt);
                var face = faceRt.GetComponent<Image>(); face.sprite = icons[0]; face.preserveAspect = true; face.raycastTarget = false;
                var vh = gauge.GetComponent<VanHealthUI>(); if (vh == null) vh = gauge.gameObject.AddComponent<VanHealthUI>();
                vh.health = car; vh.icon = iconRt; vh.face = face; vh.stages = icons;
                EditorUtility.SetDirty(vh);
            }
        }

        const string dir = "Assets/New Folder/stylized-pixel-fiat-ducato-gen-i-jacex/textures/";
        var clean = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "Kesller.png");
        var cleanImp = AssetImporter.GetAtPath(dir + "Kesller.png") as TextureImporter;
        if (clean == null || cleanImp == null) return;
        var stages = new System.Collections.Generic.List<Texture2D> { clean };
        for (int i = 1; i <= 4; i++)
        {
            string path = dir + "Kesller_damage" + i + ".png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            // imported exactly like the clean paint, so the swap doesn't change how the van is lit or filtered
            if (imp.maxTextureSize != cleanImp.maxTextureSize || imp.filterMode != cleanImp.filterMode || imp.mipmapEnabled != cleanImp.mipmapEnabled || imp.textureCompression != cleanImp.textureCompression)
            {
                imp.maxTextureSize = cleanImp.maxTextureSize; imp.filterMode = cleanImp.filterMode; imp.mipmapEnabled = cleanImp.mipmapEnabled;
                imp.textureCompression = cleanImp.textureCompression; imp.wrapMode = cleanImp.wrapMode; imp.sRGBTexture = cleanImp.sRGBTexture;
                imp.SaveAndReimport();
            }
            stages.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        }
        foreach (var ch in Object.FindObjectsByType<CarHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (ch.gameObject.scene != canvas.gameObject.scene) continue;
            var look = ch.GetComponent<VanDamageLook>(); if (look == null) look = ch.gameObject.AddComponent<VanDamageLook>();
            look.health = ch;
            look.stages = stages.ToArray();
            EditorUtility.SetDirty(look);
        }
    }

    // ---------------------------------------------------------------- the pause menu

    static void Pause(RectTransform canvas, Scene scene)
    {
        var old = canvas.Find("PauseMenu");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = Node("PauseMenu", canvas, typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(PauseMenu));
        Stretch(root);
        var cv = root.GetComponent<Canvas>(); cv.overrideSorting = true; cv.sortingOrder = 900;
        var menu = root.GetComponent<PauseMenu>();
        menu.group = root.GetComponent<CanvasGroup>();
        menu.group.alpha = 0f; menu.group.interactable = false; menu.group.blocksRaycasts = false;

        var dim = Node("Dim", root, typeof(Image)); Stretch(dim);
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.68f);
        var side = Node("Side", root, typeof(Image));
        Place(side, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(640f, 0f));
        side.GetComponent<Image>().color = new Color(0.03f, 0.035f, 0.05f, 0.94f);
        var edge = Node("Edge", side, typeof(Image));
        Place(edge, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(3f, 0f));
        edge.GetComponent<Image>().color = Red; edge.GetComponent<Image>().raycastTarget = false;

        RectTransform At(string n, float x, float y, float w, float h, params System.Type[] comps)
        {
            var rt = Node(n, side, comps);
            Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(w, h));
            return rt;
        }
        Text(At("Game", 92f, -118f, 460f, 28f), "HELLSCAPE", head, 24f, Red, TextAlignmentOptions.TopLeft, 6f);
        Text(At("Title", 90f, -146f, 480f, 100f), "PAUSED", head, 84f, Color.white, TextAlignmentOptions.TopLeft, 2f);

        Button MenuButton(string label, float y)
        {
            var rt = At(label, 80f, y, 480f, 58f, typeof(Image), typeof(Button));
            var hit = rt.GetComponent<Image>(); hit.color = new Color(1f, 1f, 1f, 0f);
            var lrt = Node("Label", rt); Stretch(lrt, 12f);
            var t = Text(lrt, label, head, 30f, Color.white, TextAlignmentOptions.MidlineLeft, 1f);
            var b = rt.GetComponent<Button>();
            b.targetGraphic = t; t.raycastTarget = false;
            var cb = b.colors;
            cb.normalColor = Soft; cb.highlightedColor = Amber; cb.selectedColor = Amber; cb.pressedColor = Color.white; cb.disabledColor = new Color(1f, 1f, 1f, 0.25f);
            cb.fadeDuration = 0.06f; cb.colorMultiplier = 1f;
            b.colors = cb;
            return b;
        }
        menu.resumeButton = MenuButton("RESUME", -290f);
        menu.restartButton = MenuButton("RESTART FROM CHECKPOINT", -354f);
        menu.controlsButton = MenuButton("CONTROLS", -418f);
        menu.titleButton = MenuButton("QUIT TO TITLE", -482f);
        menu.quitButton = MenuButton("QUIT GAME", -546f);

        Slider MenuSlider(string label, float y, float min, float max, float value)
        {
            Text(At(label + " label", 92f, y, 460f, 24f), label, head, 23f, Muted, TextAlignmentOptions.TopLeft, 2f);
            var rt = At(label, 92f, y - 34f, 420f, 22f, typeof(Slider));
            var bg = Node("Background", rt); Place(bg, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 8f));
            Rounded(bg, new Color(1f, 1f, 1f, 0.16f)).raycastTarget = true;
            var area = Node("Fill Area", rt); Place(area, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 8f));
            var fill = Node("Fill", area); Stretch(fill); Rounded(fill, Amber);
            var slide = Node("Handle Slide Area", rt); Stretch(slide, 10f, 0f, 10f, 0f);
            var handle = Node("Handle", slide); Place(handle, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));
            var himg = Rounded(handle, Color.white); himg.raycastTarget = true;
            var s = rt.GetComponent<Slider>();
            s.fillRect = fill; s.handleRect = handle; s.targetGraphic = himg; s.direction = Slider.Direction.LeftToRight;
            s.minValue = min; s.maxValue = max; s.value = value;
            return s;
        }
        menu.volume = MenuSlider("VOLUME", -650f, 0f, 1f, 1f);
        menu.sensitivity = MenuSlider("MOUSE SENSITIVITY", -730f, 0.5f, 5f, 2f);

        var foot = Node("Foot", side);
        Place(foot, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(92f, 56f), new Vector2(460f, 26f));
        Text(foot, H + "<color=#" + ColorUtility.ToHtmlStringRGB(Amber) + ">ESC</color>   resume", body, 24f, Muted, TextAlignmentOptions.BottomLeft);

        // the controls, beside the menu
        var controls = Node("Controls", root);
        Place(controls, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(720f, 0f), new Vector2(860f, 740f));
        Rounded(controls, Panel);
        string a = ColorUtility.ToHtmlStringRGB(Amber), m = ColorUtility.ToHtmlStringRGB(Muted);
        string Row(string key, string what) => $"{H}<color=#{a}>{key}</color><pos=34%>{what}\n";
        string Head(string s) => $"{H}<size=95%><cspace=0.08em><color=#{m}>{s}</color></cspace></size>\n";
        var ct = Node("Text", controls); Stretch(ct, 48f, 36f, 40f, 36f);
        var ctext = Text(ct,
            Head("ON FOOT") + Row("W A S D", "Move") + Row("SHIFT", "Sprint") + Row("SPACE", "Jump") + Row("F", "Use / talk / pick up") + Row("E", "Next line")
            + Row("LEFT MOUSE", "Punch / shoot") + Row("R", "Reload") + "<size=40%>\n</size>"
            + Head("IN THE VAN") + Row("W / S", "Drive / reverse") + Row("A / D", "Steer") + Row("SPACE", "Handbrake") + Row("F", "Get in / out")
            + Row("RIGHT MOUSE", "Lean out and aim") + Row("MIDDLE MOUSE", "Lock on") + Row("T (hold)", "Put the van back on the road"),
            body, 23f, Color.white, TextAlignmentOptions.TopLeft);
        ctext.lineSpacing = 12f;
        ctext.textWrappingMode = TextWrappingModes.NoWrap;
        controls.gameObject.SetActive(false);
        menu.controlsPanel = controls.gameObject;

        menu.titleScene = "MainGameScene";
        root.SetAsLastSibling();
        EditorUtility.SetDirty(menu);
    }
}
