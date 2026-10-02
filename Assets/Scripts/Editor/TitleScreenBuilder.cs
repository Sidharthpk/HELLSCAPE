using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// HellScape > Build Title Screen: the start screen (TitleScreen) - a cinematic camera drifting through the hell city
// with the title and START / QUIT over it. Rebuilding replaces the "TitleScreen" root only.
public static class TitleScreenBuilder
{
    const string Root = "TitleScreen";
    const string ShadePath = "Assets/Materials/Title/TitleShade.png";

    [MenuItem("HellScape/Build Title Screen")]
    public static void Build()
    {
        foreach (var old in EditorSceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == Root))
            Object.DestroyImmediate(old);
        var sleep = Object.FindFirstObjectByType<SleepSequence>(FindObjectsInactive.Include);

        var root = new GameObject(Root);
        var ts = root.AddComponent<TitleScreen>();
        ts.sleep = sleep;
        ts.lockDuring = new Behaviour[] { sleep.playerMovementScript, sleep.punchController };
        ts.hideDuring = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.name != "StoryCanvas")
            .ToArray();

        ts.music = root.AddComponent<AudioSource>();
        ts.music.playOnAwake = false; ts.music.spatialBlend = 0f; ts.music.loop = true;
        ts.sfx = root.AddComponent<AudioSource>();
        ts.sfx.playOnAwake = false; ts.sfx.spatialBlend = 0f;

        // the camera: above every other camera while the menu is up
        var camGo = new GameObject("TitleCamera");
        camGo.transform.SetParent(root.transform, false);
        var cam = camGo.AddComponent<Camera>();
        cam.depth = 100f; cam.fieldOfView = 50f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 1500f;
        cam.enabled = false;
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        ts.cam = cam;

        // slow dollies through the city, each cut through black
        ts.shots = new[]
        {
            Shot(new Vector3(-84f, 0f, -89f), new Vector3(-93f, 0.6f, -122f), new Vector3(-104f, -1f, -175f), new Vector3(-107f, -1f, -205f), 11f),   // down the lamp-lit street
            Shot(new Vector3(143f, 15f, -196f), new Vector3(150f, 24f, -170f), new Vector3(250f, 10f, 10f), new Vector3(245f, 6f, 20f), 11f),      // crane up the east side, towards the bridge
            Shot(new Vector3(12f, 24f, 62f), new Vector3(0f, 28f, 44f), new Vector3(-40f, 0f, -80f), new Vector3(-50f, 0f, -90f), 10f),          // over the rooftops from home
            Shot(new Vector3(102f, 60f, -302f), new Vector3(82f, 54f, -280f), new Vector3(60f, 0f, -100f), new Vector3(40f, 0f, -100f), 11f),     // the whole city from above
        };

        // ------------------------------------------------------------------ UI
        var canvasGo = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(root.transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var cutImg = Stretch(Img("Cut", canvasGo.transform, Color.black));
        ts.cut = cutImg.gameObject.AddComponent<CanvasGroup>();
        ts.cut.blocksRaycasts = false;

        var uiGo = Stretch(new GameObject("UI", typeof(RectTransform)).GetComponent<RectTransform>());
        uiGo.SetParent(canvasGo.transform, false);
        ts.ui = uiGo.gameObject.AddComponent<CanvasGroup>();

        // dark falloff behind the text on the left, like a cinematic menu
        var shade = Stretch(Img("Shade", uiGo, Color.white));
        shade.GetComponent<Image>().sprite = ShadeSprite();
        shade.GetComponent<Image>().raycastTarget = false;
        var bottom = Img("BottomBar", uiGo, new Color(0f, 0f, 0f, 0.55f));
        bottom.anchorMin = new Vector2(0f, 0f); bottom.anchorMax = new Vector2(1f, 0f); bottom.pivot = new Vector2(0.5f, 0f);
        bottom.sizeDelta = new Vector2(0f, 64f); bottom.anchoredPosition = Vector2.zero;
        bottom.GetComponent<Image>().raycastTarget = false;

        var hell = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Hell Pixel.asset");
        var dos = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DOS Pixel.asset");

        // the title: a black drop copy behind the red one
        var titleRt = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>();
        titleRt.SetParent(uiGo, false);
        Left(titleRt, new Vector2(140f, 170f), new Vector2(1400f, 240f));
        ts.title = titleRt;
        ts.titleGroup = titleRt.gameObject.AddComponent<CanvasGroup>();
        var drop = Text("Shadow", titleRt, "HELLSCAPE", hell, 190f, new Color(0f, 0f, 0f, 0.85f));
        drop.rectTransform.anchoredPosition = new Vector2(7f, -7f);
        Text("Red", titleRt, "HELLSCAPE", hell, 190f, new Color(0.8f, 0.07f, 0.05f));

        var tag = Text("Tagline", uiGo, "She's gone.  The city is wrong.  Find out why.", dos, 30f, new Color(0.78f, 0.72f, 0.68f, 0.85f));
        Left(tag.rectTransform, new Vector2(150f, 40f), new Vector2(1200f, 50f));

        ts.startButton = MenuButton("Start", uiGo, "START", dos, new Vector2(150f, -110f));
        ts.quitButton = MenuButton("Quit", uiGo, "QUIT", dos, new Vector2(150f, -180f));

        var hint = Text("Hint", uiGo, "[ENTER]  start", dos, 22f, new Color(0.6f, 0.55f, 0.52f, 0.7f));
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0f, 0f);
        hint.rectTransform.pivot = new Vector2(0f, 0.5f);
        hint.rectTransform.anchoredPosition = new Vector2(40f, 32f);
        hint.rectTransform.sizeDelta = new Vector2(600f, 40f);
        var ver = Text("Version", uiGo, "prototype build", dos, 22f, new Color(0.6f, 0.55f, 0.52f, 0.7f));
        ver.alignment = TextAlignmentOptions.MidlineRight;
        ver.rectTransform.anchorMin = ver.rectTransform.anchorMax = new Vector2(1f, 0f);
        ver.rectTransform.pivot = new Vector2(1f, 0.5f);
        ver.rectTransform.anchoredPosition = new Vector2(-40f, 32f);
        ver.rectTransform.sizeDelta = new Vector2(600f, 40f);

        // keep it last under the root list so it's easy to find, then save
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("Title screen built.");
    }

    static TitleScreen.Shot Shot(Vector3 a, Vector3 b, Vector3 la, Vector3 lb, float d) =>
        new TitleScreen.Shot { fromPos = a, toPos = b, fromLook = la, toLook = lb, duration = d };

    static RectTransform Img(string name, Transform parent, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = c;
        return go.GetComponent<RectTransform>();
    }

    static RectTransform Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return r;
    }

    static void Left(RectTransform r, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    static TextMeshProUGUI Text(string name, Transform parent, string s, TMP_FontAsset font, float size, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = s; t.fontSize = size; t.color = c;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        var r = t.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        return t;
    }

    static Button MenuButton(string name, Transform parent, string label, TMP_FontAsset font, Vector2 pos)
    {
        var t = Text(name, parent, label, font, 46f, Color.white);
        Left(t.rectTransform, pos, new Vector2(360f, 60f));
        t.raycastTarget = true;
        t.characterSpacing = 12f;
        var b = t.gameObject.AddComponent<Button>();
        b.targetGraphic = t;
        var cb = b.colors;
        cb.normalColor = new Color(0.82f, 0.78f, 0.74f);
        cb.highlightedColor = new Color(1f, 0.22f, 0.15f);
        cb.selectedColor = cb.normalColor;
        cb.pressedColor = new Color(0.55f, 0.05f, 0.03f);
        cb.fadeDuration = 0.12f;
        b.colors = cb;
        var nav = b.navigation; nav.mode = Navigation.Mode.Vertical; b.navigation = nav;
        return b;
    }

    // black on the left fading out by ~65% of the width
    static Sprite ShadeSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(ShadePath);
        if (existing != null) return existing;
        Directory.CreateDirectory(Path.GetDirectoryName(ShadePath));
        var tex = new Texture2D(256, 4, TextureFormat.RGBA32, false);
        for (int x = 0; x < 256; x++)
        {
            float k = x / 255f;
            float a = Mathf.Clamp01(1f - k / 0.65f);
            a = 0.85f * a * a * (3f - 2f * a);
            for (int y = 0; y < 4; y++) tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
        }
        File.WriteAllBytes(ShadePath, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(ShadePath);
        var imp = (TextureImporter)AssetImporter.GetAtPath(ShadePath);
        imp.textureType = TextureImporterType.Sprite;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(ShadePath);
    }
}
