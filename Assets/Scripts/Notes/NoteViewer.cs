using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Full-screen reading of a ReadableNote, Resident Evil style: the game pauses, the note is held up in front of you
// (drawn from UI pieces in the pixel font - no textures needed), F (or E / Esc / Space / click) puts it down.
// Builds its own overlay canvas the first time it's used, so it needs nothing in the scene.
public class NoteViewer : MonoBehaviour
{
    static NoteViewer instance;
    static bool open;
    static int closedFrame = -10;

    // PlayerInteractor checks this: nothing else gets F while a note is up (or on the frame it was put down)
    public static bool Blocking => open || Time.frameCount <= closedFrame + 1;
    public static bool IsOpen => open;

    // put it down from code (a gamepad button, a test)
    public static void Close() => closeRequested = true;
    static bool closeRequested;

    static readonly KeyCode[] closeKeys = { KeyCode.F, KeyCode.E, KeyCode.Escape, KeyCode.Space, KeyCode.Mouse0 };

    TMP_FontAsset font;
    RectTransform root, item;
    CanvasGroup group;
    Sprite dot, ring;

    public static void Show(ReadableNote note)
    {
        if (open || note == null) return;
        if (instance == null) instance = Build();
        instance.StartCoroutine(instance.Run(note));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; open = false; closedFrame = -10; }

    // ---------------------------------------------------------------- the reading

    IEnumerator Run(ReadableNote note)
    {
        open = true;
        var locked = Lock();

        if (!string.IsNullOrEmpty(note.beforeLine) && DialogueBox.Instance != null)
        {
            DialogueBox.Instance.Say(note.beforeLine);
            yield return null;
            while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) yield return null;
        }

        Draw(note);
        float scale0 = Time.timeScale;
        Time.timeScale = 0f;
        yield return Fade(0f, 1f, 0.15f);

        int openedAt = Time.frameCount;
        bool done = false;
        closeRequested = false;
        while (!done)
        {
            yield return null;
            if (Time.frameCount <= openedAt) continue;   // not the F that opened it
            foreach (var k in closeKeys) if (Input.GetKeyDown(k)) done = true;
            if (closeRequested) done = true;
        }
        closeRequested = false;
        yield return Fade(1f, 0f, 0.12f);
        if (item != null) Destroy(item.gameObject);
        Time.timeScale = scale0 > 0f ? scale0 : 1f;
        foreach (var b in locked) if (b != null) b.enabled = true;
        open = false;
        closedFrame = Time.frameCount;

        if (!string.IsNullOrEmpty(note.afterLine) && DialogueBox.Instance != null) DialogueBox.Instance.Say(note.afterLine);
    }

    // movement and mouse-look, fists and gun: off while reading (only what was on, and it all comes back)
    static List<Behaviour> Lock()
    {
        var off = new List<Behaviour>();
        void Off(Behaviour b) { if (b != null && b.enabled) { b.enabled = false; off.Add(b); } }
        foreach (var f in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None)) Off(f);
        foreach (var p in FindObjectsByType<PunchController>(FindObjectsSortMode.None)) Off(p);
        foreach (var g in FindObjectsByType<GunController>(FindObjectsSortMode.None)) Off(g);
        foreach (var f in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
            if (f.TryGetComponent(out Rigidbody rb) && !rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        return off;
    }

    IEnumerator Fade(float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            group.alpha = Mathf.Lerp(from, to, k);
            if (item != null) item.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, Mathf.Lerp(from, to, k));
            yield return null;
        }
        group.alpha = to;
        if (item != null) item.localScale = Vector3.one;
    }

    // ---------------------------------------------------------------- the overlay

    static NoteViewer Build()
    {
        var go = new GameObject("NoteViewer");
        var v = go.AddComponent<NoteViewer>();
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;   // over the HUD and the dialogue box
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        v.group = go.AddComponent<CanvasGroup>();
        v.group.alpha = 0f;
        v.group.blocksRaycasts = false;
        v.font = Resources.Load<TMP_FontAsset>("NotePixelFont");
        v.dot = MakeCircle(64, 0f);
        v.ring = MakeCircle(64, 0.16f);

        var dim = v.Box(go.transform, "Dim", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.78f));
        dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one; dim.sizeDelta = Vector2.zero;
        v.root = new GameObject("Root", typeof(RectTransform)).GetComponent<RectTransform>();
        v.root.SetParent(go.transform, false);
        v.root.anchoredPosition = new Vector2(0f, 20f);
        v.root.localScale = Vector3.one * 0.86f;   // the tallest pages (1000) stay clear of the hint
        var hint = v.Text(go.transform, "[F]  Put it down", 26, new Color(0.8f, 0.78f, 0.72f), TextAlignmentOptions.Center,
                          new Vector2(0f, -500f), new Vector2(800f, 40f));
        hint.name = "Hint";
        return v;
    }

    // ---------------------------------------------------------------- drawing each kind

    static readonly Color Ink = new Color(0.12f, 0.16f, 0.4f);
    static readonly Color Black = new Color(0.08f, 0.07f, 0.07f);
    static readonly Color Marker = new Color(0.78f, 0.05f, 0.04f);
    static readonly Color Blood = new Color(0.45f, 0.02f, 0.02f);

    void Draw(ReadableNote n)
    {
        if (item != null) Destroy(item.gameObject);
        item = new GameObject("Note", typeof(RectTransform)).GetComponent<RectTransform>();
        item.SetParent(root, false);
        switch (n.style)
        {
            case ReadableNote.Style.Sticky: Sticky(n, false); break;
            case ReadableNote.Style.BloodySticky: Sticky(n, true); break;
            case ReadableNote.Style.Notice: Notice(n); break;
            case ReadableNote.Style.Phone: Phone(n); break;
            case ReadableNote.Style.Report: Report(n); break;
            case ReadableNote.Style.Ledger: Ledger(n); break;
            case ReadableNote.Style.Calendar: Calendar(n); break;
            default: Paper(n); break;
        }
    }

    void Paper(ReadableNote n)
    {
        Tilt(-2f);
        var sheet = Box(item, "Sheet", Vector2.zero, new Vector2(600f, 700f), new Color(0.94f, 0.91f, 0.82f));
        Shadow(sheet);
        Circle(sheet, new Vector2(0f, 320f), 46f, new Color(0.7f, 0.12f, 0.1f), dot);   // a magnet
        Lines(sheet, 600f, 700f, 46f, new Color(0.55f, 0.65f, 0.85f, 0.35f));
        if (!string.IsNullOrEmpty(n.title)) Text(sheet, n.title, 38, Ink, TextAlignmentOptions.TopLeft, new Vector2(0f, 250f), new Vector2(500f, 60f));
        Text(sheet, n.body, 32, Ink, TextAlignmentOptions.TopLeft, new Vector2(0f, 10f), new Vector2(500f, 440f));
        if (!string.IsNullOrEmpty(n.footer)) Text(sheet, n.footer, 34, Ink, TextAlignmentOptions.Right, new Vector2(0f, -290f), new Vector2(500f, 50f));
        Scrawl(sheet, n.scrawl, new Vector2(0f, -220f), -6f, 40);
    }

    void Sticky(ReadableNote n, bool bloody)
    {
        Tilt(bloody ? 4f : -3f);
        var pad = Box(item, "Sticky", Vector2.zero, new Vector2(520f, 520f), new Color(0.99f, 0.87f, 0.32f));
        Shadow(pad);
        Box(pad, "Glue", new Vector2(0f, 235f), new Vector2(520f, 50f), new Color(0.93f, 0.8f, 0.26f));
        if (bloody)
        {
            // soaked from the bottom corner up, a bloody thumbprint where it was pressed down, splatter, drips
            var wet = new Color(0.55f, 0.02f, 0.03f, 0.95f);
            var dry = new Color(0.36f, 0.01f, 0.02f, 0.9f);
            var rnd = new System.Random(1304);
            // the stains stay on the paper (clipped to it); only the drips hang off the edge
            var stains = new GameObject("Stains", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            stains.SetParent(pad, false);
            stains.sizeDelta = pad.sizeDelta;
            foreach (var (p, s) in new[] { (new Vector2(200f, -190f), 230f), (new Vector2(90f, -230f), 150f), (new Vector2(230f, -60f), 120f), (new Vector2(-20f, -245f), 90f) })
                Circle(stains, p, s, wet, dot);   // the soaked corner
            Circle(stains, new Vector2(-165f, 150f), 96f, dry, dot);    // thumbprint
            Circle(stains, new Vector2(-150f, 172f), 58f, dry, dot);
            for (int i = 0; i < 22; i++)
            {
                float s = 6f + (float)rnd.NextDouble() * 20f;
                Circle(stains, new Vector2((float)rnd.NextDouble() * 480f - 240f, (float)rnd.NextDouble() * 480f - 240f), s, wet, dot);
            }
            for (int i = 0; i < 5; i++)
            {
                float x = 40f + i * 44f + (float)rnd.NextDouble() * 20f, len = 50f + (float)rnd.NextDouble() * 90f;
                Box(pad, "Drip", new Vector2(x, -260f - len * 0.5f), new Vector2(10f + i % 2 * 4f, len), wet);
                Circle(pad, new Vector2(x, -260f - len), 18f, wet, dot);
            }
            Text(pad, n.body, 56, new Color(0.42f, 0.0f, 0.02f), TextAlignmentOptions.Center, new Vector2(-20f, 40f), new Vector2(440f, 300f));   // written in it
        }
        else
            Text(pad, n.body, 34, Black, TextAlignmentOptions.TopLeft, new Vector2(0f, -10f), new Vector2(440f, 400f));
        if (!string.IsNullOrEmpty(n.footer)) Text(pad, n.footer, 34, bloody ? Blood : Black, TextAlignmentOptions.Right, new Vector2(0f, -205f), new Vector2(440f, 50f));
    }

    void Notice(ReadableNote n)
    {
        Tilt(1f);
        var sheet = Box(item, "Sheet", Vector2.zero, new Vector2(760f, 920f), new Color(0.97f, 0.97f, 0.95f));
        Shadow(sheet);
        foreach (var x in new[] { -330f, 330f }) Circle(sheet, new Vector2(x, 420f), 22f, new Color(0.5f, 0.5f, 0.55f), dot);   // drawing pins
        var band = Box(sheet, "Header", new Vector2(0f, 330f), new Vector2(680f, 110f), new Color(0.72f, 0.06f, 0.05f));
        Text(band, string.IsNullOrEmpty(n.title) ? "NOTICE" : n.title, 44, Color.white, TextAlignmentOptions.Center, Vector2.zero, new Vector2(660f, 100f));
        if (!string.IsNullOrEmpty(n.subtitle)) Text(sheet, n.subtitle, 26, new Color(0.35f, 0.35f, 0.35f), TextAlignmentOptions.Center, new Vector2(0f, 240f), new Vector2(680f, 40f));
        Text(sheet, n.body, 34, Black, TextAlignmentOptions.Top, new Vector2(0f, -40f), new Vector2(640f, 480f));
        if (!string.IsNullOrEmpty(n.footer)) Text(sheet, n.footer, 30, Black, TextAlignmentOptions.Right, new Vector2(0f, -380f), new Vector2(640f, 50f));
        Scrawl(sheet, n.scrawl, new Vector2(0f, -300f), -5f, 40);
    }

    void Phone(ReadableNote n)
    {
        var body = Box(item, "Phone", Vector2.zero, new Vector2(470f, 900f), new Color(0.05f, 0.05f, 0.06f));
        Shadow(body);
        var screen = Box(body, "Screen", Vector2.zero, new Vector2(420f, 800f), new Color(0.07f, 0.09f, 0.13f));
        Text(screen, "03:10", 24, new Color(0.8f, 0.8f, 0.85f), TextAlignmentOptions.Center, new Vector2(0f, 372f), new Vector2(400f, 36f));
        Box(screen, "Bar", new Vector2(0f, 345f), new Vector2(420f, 2f), new Color(1f, 1f, 1f, 0.12f));
        if (!string.IsNullOrEmpty(n.title)) Text(screen, n.title, 26, new Color(0.95f, 0.3f, 0.28f), TextAlignmentOptions.Center, new Vector2(0f, 300f), new Vector2(400f, 70f));
        if (!string.IsNullOrEmpty(n.subtitle)) Text(screen, n.subtitle, 30, Color.white, TextAlignmentOptions.Left, new Vector2(10f, 220f), new Vector2(360f, 44f));
        var bubble = Box(screen, "Bubble", new Vector2(-10f, 80f), new Vector2(360f, 220f), new Color(0.2f, 0.22f, 0.27f));
        Text(bubble, n.body, 30, Color.white, TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(320f, 190f));
        if (!string.IsNullOrEmpty(n.footer)) Text(screen, n.footer, 22, new Color(0.6f, 0.62f, 0.68f), TextAlignmentOptions.Left, new Vector2(10f, -58f), new Vector2(360f, 34f));
        Box(body, "Home", new Vector2(0f, -425f), new Vector2(90f, 8f), new Color(0.3f, 0.3f, 0.32f));
    }

    void Report(ReadableNote n)
    {
        Tilt(-1.5f);
        var sheet = Box(item, "Sheet", Vector2.zero, new Vector2(780f, 960f), new Color(0.96f, 0.95f, 0.92f));
        Shadow(sheet);
        Box(sheet, "Clip", new Vector2(0f, 470f), new Vector2(160f, 40f), new Color(0.55f, 0.55f, 0.58f));
        Text(sheet, n.title, 46, Black, TextAlignmentOptions.Center, new Vector2(0f, 380f), new Vector2(700f, 60f));
        if (!string.IsNullOrEmpty(n.subtitle)) Text(sheet, n.subtitle, 24, new Color(0.35f, 0.35f, 0.35f), TextAlignmentOptions.Center, new Vector2(0f, 330f), new Vector2(700f, 36f));
        Box(sheet, "Rule", new Vector2(0f, 300f), new Vector2(700f, 3f), Black);
        Text(sheet, n.body, 30, Black, TextAlignmentOptions.TopLeft, new Vector2(0f, 20f), new Vector2(680f, 520f));
        if (!string.IsNullOrEmpty(n.footer)) Text(sheet, n.footer, 30, Ink, TextAlignmentOptions.TopLeft, new Vector2(0f, -330f), new Vector2(680f, 200f));
        Scrawl(sheet, n.scrawl, new Vector2(80f, -300f), -8f, 44);
    }

    void Ledger(ReadableNote n)
    {
        Tilt(1.5f);
        var cover = Box(item, "Cover", Vector2.zero, new Vector2(860f, 980f), new Color(0.24f, 0.07f, 0.05f));
        Shadow(cover);
        var page = Box(cover, "Page", Vector2.zero, new Vector2(800f, 920f), new Color(0.9f, 0.86f, 0.74f));
        Lines(page, 800f, 920f, 44f, new Color(0.45f, 0.55f, 0.75f, 0.35f));
        Box(page, "Margin", new Vector2(-330f, 0f), new Vector2(3f, 920f), new Color(0.8f, 0.2f, 0.2f, 0.5f));
        Text(page, n.title, 36, new Color(0.35f, 0.04f, 0.03f), TextAlignmentOptions.TopLeft, new Vector2(30f, 400f), new Vector2(680f, 50f));
        if (!string.IsNullOrEmpty(n.subtitle)) Text(page, n.subtitle, 26, new Color(0.35f, 0.04f, 0.03f), TextAlignmentOptions.TopLeft, new Vector2(30f, 356f), new Vector2(680f, 40f));
        Text(page, n.body, 29, Ink, TextAlignmentOptions.TopLeft, new Vector2(30f, 20f), new Vector2(680f, 600f));
        if (!string.IsNullOrEmpty(n.footer)) Text(page, n.footer, 29, Blood, TextAlignmentOptions.TopLeft, new Vector2(30f, -340f), new Vector2(680f, 200f));
    }

    void Calendar(ReadableNote n)
    {
        Tilt(-1f);
        const float W = 960f, H = 1000f;
        var sheet = Box(item, "Calendar", Vector2.zero, new Vector2(W, H), new Color(0.95f, 0.93f, 0.86f));
        Shadow(sheet);
        Circle(sheet, new Vector2(0f, H * 0.5f - 8f), 34f, new Color(0.4f, 0.4f, 0.42f), dot);   // the nail it hangs on
        var band = Box(sheet, "Month", new Vector2(0f, 400f), new Vector2(W - 40f, 140f), new Color(0.62f, 0.05f, 0.05f));
        Text(band, n.month, 80, Color.white, TextAlignmentOptions.Center, Vector2.zero, new Vector2(W - 60f, 130f));

        string[] wd = { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" };
        float gridW = W - 60f, cw = gridW / 7f, top = 300f;
        for (int i = 0; i < 7; i++)
            Text(sheet, wd[i], 24, i >= 5 ? Marker : Black, TextAlignmentOptions.Center, new Vector2(-gridW * 0.5f + cw * (i + 0.5f), top), new Vector2(cw, 36f));
        int rows = Mathf.CeilToInt((n.firstWeekday + n.days) / 7f);
        float gridTop = top - 26f, ch = (gridTop + H * 0.5f - 40f) / rows;
        var lineCol = new Color(0.2f, 0.18f, 0.16f, 0.6f);
        for (int r = 0; r <= rows; r++) Box(sheet, "H", new Vector2(0f, gridTop - r * ch), new Vector2(gridW, 2f), lineCol);
        for (int c = 0; c <= 7; c++) Box(sheet, "V", new Vector2(-gridW * 0.5f + c * cw, gridTop - rows * ch * 0.5f), new Vector2(2f, rows * ch), lineCol);

        for (int d = 1; d <= n.days; d++)
        {
            int cell = n.firstWeekday + d - 1, row = cell / 7, col = cell % 7;
            Vector2 centre = new Vector2(-gridW * 0.5f + cw * (col + 0.5f), gridTop - ch * (row + 0.5f));
            Text(sheet, d.ToString(), 26, col >= 5 ? Marker : Black, TextAlignmentOptions.TopLeft, centre + new Vector2(6f, 4f), new Vector2(cw - 16f, ch - 12f));
            if (d != n.circledDay) continue;
            Circle(sheet, centre + new Vector2(-cw * 0.3f, ch * 0.26f), 66f, Marker, ring);   // circled in red
            // written across the box and the next one (the way people write on calendars), crossed out...
            if (!string.IsNullOrEmpty(n.dayNote))
                Text(sheet, "<s>" + n.dayNote + "</s>", 26, Ink, TextAlignmentOptions.Left, centre + new Vector2(cw * 0.9f + 4f, -ch * 0.12f), new Vector2(cw * 2.8f - 10f, 60f));
            // ...and the word written over it, big, in red marker, under the entry so it stays readable
            if (!string.IsNullOrEmpty(n.scrawl))
            {
                var s = Text(sheet, n.scrawl, 50, Marker, TextAlignmentOptions.Center, centre + new Vector2(cw * 1.1f, -ch * 0.62f), new Vector2(cw * 3.2f, 70f));
                s.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            }
        }
        if (!string.IsNullOrEmpty(n.body)) Text(sheet, n.body, 24, Ink, TextAlignmentOptions.BottomLeft, new Vector2(0f, -H * 0.5f + 34f), new Vector2(gridW, 40f));
    }

    // ---------------------------------------------------------------- pieces

    void Tilt(float z) => item.localRotation = Quaternion.Euler(0f, 0f, z);

    RectTransform Box(Transform parent, string name, Vector2 pos, Vector2 size, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return rt;
    }

    void Shadow(RectTransform r)
    {
        var s = Box(r.parent, r.name + " shadow", r.anchoredPosition + new Vector2(14f, -16f), r.sizeDelta, new Color(0f, 0f, 0f, 0.45f));
        s.SetSiblingIndex(r.GetSiblingIndex());
    }

    void Lines(RectTransform sheet, float w, float h, float gap, Color c)
    {
        for (float y = h * 0.5f - 110f; y > -h * 0.5f + 30f; y -= gap) Box(sheet, "Line", new Vector2(0f, y), new Vector2(w - 40f, 2f), c);
    }

    void Circle(RectTransform parent, Vector2 pos, float size, Color c, Sprite s)
    {
        var r = Box(parent, "Circle", pos, new Vector2(size, size), c);
        r.GetComponent<Image>().sprite = s;
    }

    void Scrawl(RectTransform parent, string text, Vector2 pos, float angle, int size)
    {
        if (string.IsNullOrEmpty(text)) return;
        var t = Text(parent, text, size, Marker, TextAlignmentOptions.Center, pos, new Vector2(parent.sizeDelta.x - 60f, size * 2.4f));
        t.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    TextMeshProUGUI Text(Transform parent, string s, float size, Color c, TextAlignmentOptions align, Vector2 pos, Vector2 box)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = s ?? "";
        t.fontSize = size;
        t.color = c;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.richText = true;
        t.raycastTarget = false;
        t.lineSpacing = 12f;
        return t;
    }

    // a disc (ring == 0) or a ring of that thickness, pixel-hard edges to match the font
    static Sprite MakeCircle(int size, float ring)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                bool on = ring > 0f ? d <= 1f && d >= 1f - ring : d <= 1f;
                tex.SetPixel(x, y, on ? Color.white : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
