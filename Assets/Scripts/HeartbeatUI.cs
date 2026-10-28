using UnityEngine;
using UnityEngine.UI;

// Your health, Resident Evil style: an ECG strip in the corner. The trace sweeps across a dark panel and beats
// faster the more hurt you are; green FINE, yellow CAUTION, red DANGER; it flashes when you're hit and goes flat
// when you die. "+" shows while it slowly heals (PlayerHealth regenerates after a few quiet seconds).
public class HeartbeatUI : MonoBehaviour
{
    public PlayerHealth health;
    public float calmBpm = 62f;             // at full health
    public float panicBpm = 160f;           // at death's door
    [Range(0f, 1f)] public float hurtBelow = 0.66f;
    [Range(0f, 1f)] public float criticalBelow = 0.33f;
    public float sweep = 90f;               // trace speed, texture pixels a second

    const int W = 220, H = 56, Gap = 10;
    static readonly Color Fine = new Color(.35f, 1f, .45f), Caution = new Color(1f, .85f, .2f), Danger = new Color(1f, .18f, .12f);

    Texture2D tex; Color32[] px; RawImage trace; Image panel; TMPro.TextMeshProUGUI label;
    float head, phase, lastY = H * .5f, lastHealth, flinch;
    Color32 clear = new Color32(0, 0, 0, 0);

    void OnEnable()
    {
        if (tex == null) Build();
        if (health != null) lastHealth = health.currentHealth;
    }

    void Build()
    {
        var old = transform.Find("Heart");   // (the anatomical heart this replaced)
        if (old != null) old.gameObject.SetActive(false);
        var rt = (RectTransform)transform;
        rt.sizeDelta = new Vector2(250f, 92f);

        // (not `?? AddComponent`: a missing component is a fake null in the editor)
        panel = GetComponent<Image>(); if (panel == null) panel = gameObject.AddComponent<Image>();
        panel.color = new Color(0f, 0f, 0f, .6f); panel.raycastTarget = false;
        // a thin rule under the label (an Outline on the panel itself would wash the whole panel grey)
        var rule = Child<Image>("Rule", new Vector2(12f, 60f), new Vector2(-12f, -31f));
        rule.color = new Color(1f, 1f, 1f, .15f); rule.raycastTarget = false;

        tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "ECG" };
        px = new Color32[W * H];
        tex.SetPixels32(px); tex.Apply();
        trace = Child<RawImage>("Trace", new Vector2(12f, 6f), new Vector2(-12f, -35f));
        trace.texture = tex; trace.raycastTarget = false;

        label = Child<TMPro.TextMeshProUGUI>("State", new Vector2(12f, 64f), new Vector2(-12f, -4f));
        label.font = GameFont.Main; label.fontSize = 20f; label.characterSpacing = 6f;
        label.alignment = TMPro.TextAlignmentOptions.Left; label.raycastTarget = false;
    }

    T Child<T>(string name, Vector2 min, Vector2 max) where T : Component
    {
        var c = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        c.SetParent(transform, false);
        c.anchorMin = Vector2.zero; c.anchorMax = Vector2.one; c.offsetMin = min; c.offsetMax = max;
        return c.gameObject.AddComponent<T>();
    }

    void Update()
    {
        if (health == null || tex == null) return;
        float k = Mathf.Clamp01(health.currentHealth / Mathf.Max(1f, health.maxHealth));
        bool dead = health.IsDead;
        Color col = dead ? new Color(.5f, .5f, .5f) : k < criticalBelow ? Danger : k < hurtBelow ? Caution : Fine;

        if (health.currentHealth < lastHealth - .01f) flinch = 1f;
        lastHealth = health.currentHealth;
        flinch = Mathf.MoveTowards(flinch, 0f, Time.deltaTime * 2.5f);

        label.text = dead ? "FLATLINE" : (k < criticalBelow ? "DANGER" : k < hurtBelow ? "CAUTION" : "FINE") + (health.Regenerating ? "  +" : "");
        float blink = k < criticalBelow && !dead ? .65f + .35f * Mathf.Abs(Mathf.Sin(Time.time * 4f)) : 1f;
        label.color = new Color(col.r, col.g, col.b, blink);
        panel.color = Color.Lerp(new Color(0f, 0f, 0f, .6f), new Color(.6f, 0f, 0f, .75f), flinch);

        // the sweep: draw the new columns, wipe a gap ahead of them
        float bpm = Mathf.Lerp(calmBpm, panicBpm, Mathf.Pow(1f - k, .8f));
        Color32 c32 = col;
        int from = (int)head;
        head += sweep * Time.deltaTime;
        for (int x = from; x < (int)head; x++)
        {
            phase = Mathf.Repeat(phase + bpm / 60f / sweep, 1f);
            float y = H * .5f + (dead ? 0f : Wave(phase) * H * .4f);
            int col0 = x % W;
            for (int g = 0; g < Gap; g++) Column(((x + g) % W), clear);
            int lo = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(lastY, y)) - 2, 0, H - 1), hi = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(lastY, y)) + 2, 0, H - 1);
            for (int yy = lo; yy <= hi; yy++) px[yy * W + col0] = c32;
            lastY = y;
        }
        head %= W;
        tex.SetPixels32(px); tex.Apply();
    }

    void Column(int x, Color32 c) { for (int y = 0; y < H; y++) px[y * W + x] = c; }

    // one heartbeat: P wave, the QRS spike, T wave
    static float Wave(float p) =>
        .12f * Bump(p, .1f, .03f) - .15f * Bump(p, .2f, .012f) + Bump(p, .23f, .012f) - .3f * Bump(p, .26f, .012f) + .25f * Bump(p, .45f, .05f);

    static float Bump(float x, float at, float width) { float d = (x - at) / width; return Mathf.Exp(-d * d); }
}
