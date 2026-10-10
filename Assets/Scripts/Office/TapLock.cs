using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The silo door's jammed knob, drawn in code (no prefab): tap A and D, left-right-left-right, to wind it round. A tap
// that alternates adds turn, the same key twice just rattles it, and the knob slips back whenever you ease off. The
// clock is Tomas's strength: he's holding Kessler off the other side and hasn't much left. Out of time = he's
// through (failed(true)). Esc walks away (failed(false)). Movement/look are held off meanwhile.
// Look: a realistic brass knob on the left, and a modern flat panel in the pixel
// font on the right (like the rest of the UI), clear of the dialogue box along the bottom.
public class TapLock : MonoBehaviour
{
    public static bool Active { get; private set; }

    public float perTap = 0.06f;    // ~17 alternating taps to open it
    public float slip = 0.14f;      // per second, when you stop
    public Action beat;             // a blow on the other side, faster as Tomas tires

    static readonly Color Gold = new Color(0.93f, 0.72f, 0.2f);
    static readonly Color Amber = new Color(1f, 0.78f, 0.3f), PanelCol = new Color(0.05f, 0.05f, 0.06f, 0.88f), Track = new Color(0.16f, 0.16f, 0.18f);
    const float BarW = 380f, Flash = 0.15f;

    bool ownsSession;
    public static int ClosedFrame { get; private set; } = -1;
    public void Cancel() { if (ownsSession) Finish(null); }

    Behaviour[] held; bool[] was;
    Action solved; Action<bool> failed;
    float total, timeLeft, nextBeat, progress, shown, rattle, flashA, flashD;
    int lastKey;                    // -1 = A, +1 = D
    GameObject canvas;
    Image dim, timeFill, turnFill, capA, capD;
    RectTransform slot;
    TextMeshProUGUI timerText, tomasLabel;
    static Sprite rounded, brass, brassPlate;

    public void Begin(Behaviour[] lockDuring, float seconds, Action onSolved, Action<bool> onFailed)
    {
        if (Active) return;
        solved = onSolved; failed = onFailed;
        held = lockDuring ?? new Behaviour[0];
        was = new bool[held.Length];
        for (int i = 0; i < held.Length; i++) if (held[i] != null) { was[i] = held[i].enabled; held[i].enabled = false; }
        total = timeLeft = nextBeat = Mathf.Max(5f, seconds);
        progress = shown = rattle = flashA = flashD = 0f; lastKey = 0;
        Build();
        ownsSession = Active = true;
    }

    void Finish(Action then)
    {
        if (!ownsSession) return;
        ownsSession = Active = false;
        ClosedFrame = Time.frameCount;
        if (canvas != null) Destroy(canvas);
        for (int i = 0; i < held.Length; i++) if (held[i] != null && was[i]) held[i].enabled = true;
        then?.Invoke();
    }

    void OnDisable() { if (ownsSession) Finish(null); }

    void Update()
    {
        if (!ownsSession || PauseMenu.Paused || GameOverScreen.Open) return;
        float dt = Time.unscaledDeltaTime;
        if (Input.GetKeyDown(KeyCode.Escape)) { Finish(() => failed?.Invoke(false)); return; }

        timeLeft -= dt;
        if (timeLeft <= nextBeat - Mathf.Lerp(0.8f, 2.2f, timeLeft / total)) { nextBeat = timeLeft; beat?.Invoke(); }
        if (timeLeft <= 0f) { Finish(() => failed?.Invoke(true)); return; }

        int key = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) ? 1 : 0;
        if (key != 0) Tap(key);
        else progress -= slip * dt;
        progress = Mathf.Clamp01(progress);
        rattle = Mathf.Max(0f, rattle - dt);
        flashA = Mathf.Max(0f, flashA - dt); flashD = Mathf.Max(0f, flashD - dt);
        shown = Mathf.Lerp(shown, progress, 20f * dt);
        if (progress >= 1f) { Finish(solved); return; }
        Draw();
    }

    // a key press, -1 = A, +1 = D (public so the robot playtester can tap)
    public void Tap(int key)
    {
        if (key != lastKey) progress += perTap; else rattle = 0.25f;
        lastKey = key;
        if (key < 0) flashA = Flash; else flashD = Flash;
    }

    // ---------------------------------------------------------------- the picture

    void Draw()
    {
        float panic = 1f - Mathf.Clamp01(timeLeft / total);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (4f + 10f * panic));
        dim.color = new Color(0.12f * panic * (0.4f + pulse), 0f, 0f, 0.88f);   // black going blood red, throbbing
        slot.localRotation = Quaternion.Euler(0f, 0f, -(shown * 270f + Mathf.Sin(Time.unscaledTime * 80f) * 6f * (rattle / 0.25f)));
        Color alarm = Color.Lerp(new Color(0.45f, 0.75f, 1f), new Color(1f, 0.25f, 0.2f), panic);
        timeFill.color = alarm;
        timeFill.rectTransform.sizeDelta = new Vector2(BarW * Mathf.Clamp01(timeLeft / total), 16f);
        turnFill.rectTransform.sizeDelta = new Vector2(BarW * shown, 16f);
        timerText.text = Mathf.Max(0f, timeLeft).ToString("0.0");
        timerText.color = alarm;
        timerText.fontSize = 60f + 8f * pulse * panic;
        tomasLabel.color = Color.Lerp(new Color(0.8f, 0.8f, 0.8f), alarm, panic);
        capA.color = Cap(flashA, -1); capD.color = Cap(flashD, 1);
    }

    // a keycap: dark, amber when just pressed, red when it's the key you mashed twice
    Color Cap(float flash, int key)
    {
        var c = Color.Lerp(new Color(0.14f, 0.14f, 0.16f), Amber, flash / Flash);
        return rattle > 0f && lastKey == key ? Color.Lerp(c, new Color(0.9f, 0.2f, 0.15f), rattle / 0.25f) : c;
    }

    void Build()
    {
        if (rounded == null) rounded = Rounded();
        var font = Resources.Load<TMP_FontAsset>("NotePixelFont");
        canvas = new GameObject("Knob puzzle", typeof(Canvas), typeof(CanvasScaler));
        var c = canvas.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 500;
        var sc = canvas.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920f, 1080f);
        var root = (RectTransform)canvas.transform;

        dim = Pic(root, null, Color.black, Vector2.zero, Vector2.zero);
        dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one; dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;

        // the knob, left of centre: a smooth brass ball on a bevelled plate, with a slot that turns as you tap
        if (brass == null) { brass = Sphere(192, false); brassPlate = Sphere(192, true); }
        Pic(root, brassPlate, Color.white, new Vector2(-250f, 40f), new Vector2(380f, 380f));
        Pic(root, brass, Color.white, new Vector2(-250f, 40f), new Vector2(270f, 270f));
        slot = Pic(root, rounded, new Color(0.18f, 0.1f, 0.02f, 0.85f), new Vector2(-250f, 40f), new Vector2(14f, 190f)).rectTransform;
        slot.GetComponent<Image>().type = Image.Type.Sliced;

        // the side panel: flat, dark, rounded, pixel font, amber accent
        var panel = Pic(root, rounded, PanelCol, new Vector2(520f, 40f), new Vector2(500f, 540f));
        panel.type = Image.Type.Sliced;
        var accent = Pic(panel.transform, null, Amber, new Vector2(0f, 255f), new Vector2(440f, 5f));
        Words(panel.transform, font, "JAMMED DOOR", 34f, Amber, new Vector2(0f, 215f));
        tomasLabel = Words(panel.transform, font, "TOMAS CAN'T HOLD HIM", 24f, Color.white, new Vector2(0f, 150f));
        timerText = Words(panel.transform, font, "", 60f, Color.white, new Vector2(0f, 95f));
        Bar(panel.transform, new Vector2(0f, 40f), out timeFill);
        Words(panel.transform, font, "LOCK", 24f, new Color(0.8f, 0.8f, 0.8f), new Vector2(0f, -10f));
        Bar(panel.transform, new Vector2(0f, -50f), out turnFill);
        turnFill.color = Gold;
        capA = Key(panel.transform, font, "A", new Vector2(-70f, -125f));
        capD = Key(panel.transform, font, "D", new Vector2(70f, -125f));
        Words(panel.transform, font, "TAP  A  D  A  D", 26f, Color.white, new Vector2(0f, -195f));
        Words(panel.transform, font, "ESC: BACK OFF", 20f, new Color(0.55f, 0.55f, 0.55f), new Vector2(0f, -235f));
        Draw();
    }

    static RectTransform Anchored(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    static Image Pic(Transform parent, Sprite s, Color col, Vector2 pos, Vector2 size)
    {
        var img = Anchored(parent, "pic", pos, size).gameObject.AddComponent<Image>();
        img.sprite = s; img.color = col; img.raycastTarget = false;
        return img;
    }

    // a bar that fills from its left end (the track, and the fill in 'fill')
    static void Bar(Transform parent, Vector2 pos, out Image fill)
    {
        var track = Pic(parent, rounded, Track, pos, new Vector2(BarW, 16f));
        track.type = Image.Type.Sliced;
        fill = Pic(parent, rounded, Color.white, pos - new Vector2(BarW * 0.5f, 0f), new Vector2(0f, 16f));
        fill.type = Image.Type.Sliced;
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
    }

    static Image Key(Transform parent, TMP_FontAsset font, string letter, Vector2 pos)
    {
        var cap = Pic(parent, rounded, Color.gray, pos, new Vector2(92f, 92f));
        cap.type = Image.Type.Sliced;
        Words(cap.transform, font, letter, 48f, Color.white, Vector2.zero);
        return cap;
    }

    static TextMeshProUGUI Words(Transform parent, TMP_FontAsset font, string s, float size, Color col, Vector2 pos)
    {
        var t = Anchored(parent, "words", pos, new Vector2(460f, 70f)).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = s; t.fontSize = size; t.color = col; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    // a flat rounded rectangle, smooth edge (like ModernUIBuilder's panels), to slice
    static Sprite Rounded()
    {
        const int n = 32; const float rad = 10f;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Max(rad - x - 0.5f, x + 0.5f - (n - rad)), dy = Mathf.Max(rad - y - 0.5f, y + 0.5f - (n - rad));
                float d = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f)) - rad;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d)));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
    }

    // a lit brass ball (plate == false), or a flat bevelled brass disc (plate == true): diffuse, a sharp highlight, a
    // sky/floor reflection and a darker rim, all from one light at the top left
    static Sprite Sphere(int n, bool plate)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var L = new Vector3(-0.5f, 0.6f, 0.62f).normalized;
        var H = (L + Vector3.forward).normalized;
        var brassCol = plate ? new Vector3(0.5f, 0.36f, 0.1f) : new Vector3(0.95f, 0.74f, 0.22f);
        float r = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - r) / r, dy = (y + 0.5f - r) / r, d = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((1f - d) * r);
                if (alpha <= 0f) { tex.SetPixel(x, y, Color.clear); continue; }
                Vector3 N;
                if (plate) { float bevel = Mathf.Clamp01((d - 0.84f) / 0.16f); N = new Vector3(dx / Mathf.Max(d, 0.001f) * bevel * 0.9f, dy / Mathf.Max(d, 0.001f) * bevel * 0.9f, 1f).normalized; }
                else N = new Vector3(dx, dy, Mathf.Sqrt(Mathf.Max(0f, 1f - d * d)));
                float diff = Mathf.Max(0f, Vector3.Dot(N, L));
                float spec = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(N, H)), plate ? 25f : 70f) * (plate ? 0.35f : 1f);
                Vector3 R = new Vector3(0f, 0f, 1f) - 2f * N.z * N;   // the view ray reflected
                float sky = Mathf.SmoothStep(-0.25f, 0.35f, R.y);
                Vector3 env = Vector3.Lerp(new Vector3(0.22f, 0.12f, 0.04f), new Vector3(1f, 0.92f, 0.75f), sky);
                float rim = plate ? 1f : 1f - 0.45f * Mathf.Pow(1f - N.z, 2f);
                Vector3 col = (brassCol * (0.22f + 0.72f * diff) + Vector3.Scale(brassCol, env) * (plate ? 0.1f : 0.4f)) * rim + Vector3.one * spec * 0.9f;
                tex.SetPixel(x, y, new Color(Mathf.Min(1f, col.x), Mathf.Min(1f, col.y), Mathf.Min(1f, col.z), alpha));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
