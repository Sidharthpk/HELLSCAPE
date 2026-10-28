using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Kessler's armory door: a bank-vault combination dial, drawn with IMGUI like LockPick. Three numbers on a 0-39
// dial: A/D (or the arrows, or the mouse wheel) turns it a notch at a time, SPACE (or click) sets the number under
// the mark, BACKSPACE takes the last one back, F or Esc walks away. Kessler gave each colleague one number so no
// one could open it alone; Omar knows the order (OfficeWitness). Use() is what the door's Interactable calls.
public class VaultDial : MonoBehaviour
{
    public static bool Active { get; private set; }
    public static int ClosedFrame { get; private set; } = -1;

    public int[] code = { 18, 7, 31 };
    public int notches = 40;
    // (not "...Line": RevisedPlot.Migrate turns every Kessler in a *line* field into Vane, and this is Kessler's armory)
    [TextArea] public string lockedText = "A vault dial. Three numbers.";
    [TextArea] public string openText = "The bolts slide back.";
    public UnityEvent onSolved = new UnityEvent();

    readonly List<int> set = new List<int>();
    readonly List<Behaviour> held = new List<Behaviour>();
    bool open, solved;
    int value;
    float shown, repeatAt, msgUntil, shake;
    string msg = "";
    AudioSource sfx;
    AudioClip click, thud;
    static Texture2D face;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Active = false; ClosedFrame = -1; }

    public void Use()
    {
        if (solved || Active) return;
        open = Active = true;
        set.Clear();
        held.Clear();
        void Off(Behaviour b) { if (b != null && b.enabled) { b.enabled = false; held.Add(b); } }
        foreach (var f in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
        {
            Off(f);
            if (f.TryGetComponent(out Rigidbody rb) && !rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
        foreach (var p in FindObjectsByType<PunchController>(FindObjectsSortMode.None)) Off(p);
        foreach (var g in FindObjectsByType<GunController>(FindObjectsSortMode.None)) Off(g);
        if (sfx == null)
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false; sfx.spatialBlend = 0f;
            click = ProceduralAudio.Click(); thud = ProceduralAudio.Thud();
        }
        Say(lockedText, 3f);
    }

    void Close()
    {
        open = Active = false;
        ClosedFrame = Time.frameCount;
        foreach (var b in held) if (b != null) b.enabled = true;
        held.Clear();
    }

    void OnDisable() { if (open) Close(); }

    void Update()
    {
        if (!open || PauseMenu.Paused || GameOverScreen.Open) return;
        float now = Time.unscaledTime;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F)) { Close(); return; }

        // turning: a notch per press, then repeating while held; the wheel a notch per click
        int dir = 0;
        bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow), left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) { dir = -1; repeatAt = now + .35f; }
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) { dir = 1; repeatAt = now + .35f; }
        else if ((right || left) && now >= repeatAt) { dir = right ? -1 : 1; repeatAt = now + .06f; }
        float wheel = Input.mouseScrollDelta.y;
        if (wheel > 0f) dir = 1; else if (wheel < 0f) dir = -1;
        if (dir != 0)
        {
            value = (value + dir + notches) % notches;
            sfx.pitch = Random.Range(1.6f, 1.9f);
            sfx.PlayOneShot(click, .7f);
        }
        shown = Mathf.LerpAngle(shown, -value * 360f / notches, 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime));
        shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 2f);

        if (Input.GetKeyDown(KeyCode.Backspace) && set.Count > 0) set.RemoveAt(set.Count - 1);
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
        {
            set.Add(value);
            sfx.pitch = 1.1f;
            sfx.PlayOneShot(click, 1f);
            if (set.Count == code.Length) Try();
        }
    }

    // set one number as if turned to and set by hand (tests, or another input)
    public void Dial(int number)
    {
        if (!open) return;
        value = ((number % notches) + notches) % notches;
        set.Add(value);
        if (set.Count == code.Length) Try();
    }

    void Try()
    {
        bool right = true;
        for (int i = 0; i < code.Length; i++) right &= set[i] == code[i];
        if (right)
        {
            solved = true;
            sfx.pitch = .7f; sfx.PlayOneShot(thud, 1f);
            Close();
            onSolved.Invoke();
            if (DialogueBox.Instance != null && !string.IsNullOrEmpty(openText)) DialogueBox.Instance.Say(openText);
            return;
        }
        sfx.pitch = .5f; sfx.PlayOneShot(thud, .6f);
        shake = 1f;
        set.Clear();
        Say("The handle won't turn. Wrong numbers, or the wrong order.", 2.5f);
    }

    void Say(string s, float seconds) { msg = s; msgUntil = Time.unscaledTime + seconds; }

    // ---------------------------------------------------------------- the dial

    static Texture2D Face()
    {
        if (face != null) return face;
        const int n = 512;
        face = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[n * n];
        float c = (n - 1) * .5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x - c, dy = y - c, r = Mathf.Sqrt(dx * dx + dy * dy) / c;
                float a = Mathf.Repeat(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg, 360f);   // 0 at the top, clockwise
                Color col = Color.clear;
                if (r <= 1f)
                {
                    float grain = Mathf.PerlinNoise(x * .05f, y * .6f) * .06f;
                    col = r > .94f ? new Color(.16f, .15f, .14f) : new Color(.42f + grain, .40f + grain, .37f + grain);   // a dark rim round brushed steel
                    if (r < .3f) col = new Color(.22f, .21f, .2f);                                                         // the hub
                    float tick = Mathf.Repeat(a + 4.5f, 9f) - 4.5f;                                                        // 40 notches, 9 degrees apart
                    bool major = Mathf.RoundToInt(Mathf.Repeat(a, 360f) / 9f) % 5 == 0;
                    if (Mathf.Abs(tick) < (major ? .9f : .5f) && r > (major ? .78f : .84f) && r < .93f) col = new Color(.08f, .08f, .08f);
                    col.a = Mathf.Clamp01((1f - r) * c * .5f);
                }
                px[y * n + x] = col;
            }
        face.SetPixels(px);
        face.Apply();
        return face;
    }

    void OnGUI()
    {
        if (!open || PauseMenu.Paused || GameOverScreen.Open) return;
        var white = Texture2D.whiteTexture;
        float s = Mathf.Min(Screen.width, Screen.height) / 1080f;
        float cx = Screen.width * .5f + Mathf.Sin(Time.unscaledTime * 80f) * 10f * shake * s, cy = Screen.height * .46f, size = 560f * s;
        GUI.color = new Color(0f, 0f, 0f, .82f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);

        // the dial and its numbers turn together
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(shown, new Vector2(cx, cy));
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(cx - size * .5f, cy - size * .5f, size, size), Face());
        var num = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, font = GameFont.Ttf, fontSize = Mathf.RoundToInt(26f * s) };
        num.normal.textColor = new Color(.1f, .09f, .08f);
        for (int i = 0; i < notches; i += 5)
        {
            float a = i * 360f / notches * Mathf.Deg2Rad, r = size * .33f;   // clockwise from the top: turning right (clockwise) brings the lower numbers up
            var p = new Vector2(cx + Mathf.Sin(a) * r, cy - Mathf.Cos(a) * r);
            var mm = GUI.matrix;
            GUIUtility.RotateAroundPivot(i * 360f / notches, p);   // (upright when turned to the mark)
            GUI.Label(new Rect(p.x - 40f * s, p.y - 20f * s, 80f * s, 40f * s), i.ToString(), num);
            GUI.matrix = mm;
        }
        GUI.matrix = m;

        // the mark at the top
        GUI.color = new Color(.75f, .08f, .05f);
        GUI.DrawTexture(new Rect(cx - 4f * s, cy - size * .5f - 34f * s, 8f * s, 40f * s), white);

        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, font = GameFont.Ttf, fontSize = Mathf.RoundToInt(64f * s) };
        style.normal.textColor = new Color(1f, .85f, .55f);
        GUI.color = Color.white;
        GUI.Label(new Rect(0, cy - size * .5f - 120f * s, Screen.width, 80f * s), value.ToString("00"), style);

        // the numbers set so far, and which way to turn next
        style.fontSize = Mathf.RoundToInt(44f * s);
        string slots = "";
        for (int i = 0; i < code.Length; i++) slots += (i < set.Count ? set[i].ToString("00") : "--") + (i < code.Length - 1 ? "   -   " : "");
        style.normal.textColor = new Color(.9f, .88f, .82f);
        GUI.Label(new Rect(0, cy + size * .5f + 20f * s, Screen.width, 60f * s), slots, style);
        style.fontSize = Mathf.RoundToInt(26f * s);
        style.normal.textColor = new Color(.7f, .7f, .7f);
        string turn = set.Count < code.Length ? (set.Count % 2 == 0 ? "TURN RIGHT TO NUMBER " : "TURN LEFT TO NUMBER ") + (set.Count + 1) : "";
        GUI.Label(new Rect(0, cy + size * .5f + 80f * s, Screen.width, 40f * s), turn, style);
        GUI.Label(new Rect(0, Screen.height - 90f * s, Screen.width, 40f * s), "A / D or wheel: turn     SPACE: set the number     BACKSPACE: undo     F: step back", style);
        if (Time.unscaledTime < msgUntil)
        {
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(0, cy + size * .5f + 125f * s, Screen.width, 40f * s), msg, style);
        }
        GUI.color = Color.white;
    }
}
