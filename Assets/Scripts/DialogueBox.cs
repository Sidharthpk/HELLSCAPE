using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;

// The subtitle panel. Every line shows WHO is talking:
//   - your own lines (plain text in the script): the name plate reads YOU and the text is amber
//   - someone else ("Name: text"): their name on the plate in their own colour, the text in white
//   - a voice with no face ("quoted text": the TV, a radio, a memory): white italics, no name
// While anyone is talking the picture goes letterbox (thin black bars), like a cutscene; not while you're driving
// or in a fight, where the line just appears. Lines type out with a mumble and wait for E.
// Call DialogueBox.Instance.Say("line one|Kessler: line two") from code or a UnityEvent.
public class DialogueBox : MonoBehaviour
{
    public static DialogueBox Instance;

    // (labels inside rich text once switched font here; the whole UI is the one pixel font now)
    public const string HeadFont = "";

    public CanvasGroup group;
    public TextMeshProUGUI text;
    public AudioSource blip;
    public float charDelay = 0.03f;
    public float fadeTime = 0.18f;
    public KeyCode advanceKey = KeyCode.E;       // finish the line / go to the next one (lines wait for it)
    public string advanceHint = "  <size=70%><alpha=#99>[E]";   // only used by the old one-text layout

    [Header("Who's talking")]
    public TextMeshProUGUI nameText;             // the name plate
    public UnityEngine.UI.Graphic accent;        // the bar beside it, in the speaker's colour
    public TextMeshProUGUI hintText;             // "E  continue", blinking once the line is out
    public string playerName = "YOU";
    public Color playerColor = new Color(1f, 0.80f, 0.36f);      // you: amber
    public Color otherColor = Color.white;                       // anyone else: white
    public Color voiceColor = new Color(0.82f, 0.85f, 0.9f);     // a voice off screen
    public Color unknownSpeaker = new Color(0.72f, 0.86f, 1f);   // a name plate nobody gave a colour to

    [Header("Letterbox while someone is talking")]
    public RectTransform barTop, barBottom;
    public float barHeight = 64f;
    public float barSpeed = 7f;

    [Header("Border colour (white by day, red in hell; old layout)")]
    public UnityEngine.UI.Graphic[] border;
    public Color dayBorder = Color.white;
    public Color hellBorder = new Color(0.8f, 0.05f, 0.05f);

    // name plates: who gets which colour (anyone else gets 'unknownSpeaker')
    static readonly Dictionary<string, Color> plates = new Dictionary<string, Color>
    {
        { "kessler", new Color(0.92f, 0.22f, 0.17f) },
        { "the demon king", new Color(1f, 0.42f, 0.1f) },
        { "elias", new Color(0.72f, 0.52f, 1f) },
        { "maya", new Color(0.31f, 0.82f, 0.77f) },
        { "sam", new Color(0.39f, 0.70f, 0.93f) },
        { "derek", new Color(0.55f, 0.85f, 0.55f) },
        { "tomas", new Color(0.62f, 0.78f, 1f) },
        { "priya", new Color(0.62f, 0.78f, 1f) },
        { "ben", new Color(0.62f, 0.78f, 1f) },
        { "nadia", new Color(0.62f, 0.78f, 1f) },
        { "omar", new Color(0.62f, 0.78f, 1f) },
        { "mrs. okafor", new Color(0.96f, 0.68f, 0.33f) },
        { "man at the door", new Color(0.75f, 0.75f, 0.75f) },
    };
    static readonly Regex speakerRx = new Regex(@"^([A-Za-z][A-Za-z .'&\-]{0,24}):\s+(.+)$", RegexOptions.Singleline);

    private readonly Queue<string> queue = new Queue<string>();
    private bool running;
    private bool cutCurrent;               // SayNow: drop the line on screen as well
    private bool urgent;                   // SayNow lines: no letterbox, they're shouted mid-action
    private bool bars;                     // the letterbox wanted for the lines running now
    private float barK;

    public bool Busy => running;           // lines still typing or queued (cutscenes wait on this)

    // scripted playtests (no keyboard): lines move on by themselves after this many seconds. 0 = wait for the key.
    public static float AutoAdvanceAfter;
    private float lineShownAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => AutoAdvanceAfter = 0f;

    private AudioClip[] voice;             // the Sound Library's mumbles: one picked at random per syllable
    private CarInteract car;
    private PrologueVan van;
    private ChaseKiller chase;
    private GameObject[] fightBars;

    void Awake()
    {
        Instance = this;
        Sounds.Apply(blip, "General/Dialogue blip");
        voice = Sounds.Clips("General/Dialogue blip", null);
        group.alpha = 0f;
        group.blocksRaycasts = false;
        if (hintText != null) hintText.alpha = 0f;
        car = FindFirstObjectByType<CarInteract>(FindObjectsInactive.Include);
        van = FindFirstObjectByType<PrologueVan>(FindObjectsInactive.Include);
        chase = FindFirstObjectByType<ChaseKiller>(FindObjectsInactive.Include);
        var canvas = GetComponentInParent<Canvas>();
        var found = new List<GameObject>();
        if (canvas != null)
            foreach (var n in new[] { "BossBar", "DemonHealthBar", "AirBar" }) { var t = canvas.transform.Find(n); if (t != null) found.Add(t.gameObject); }
        fightBars = found.ToArray();
        SetBars(0f);
    }

    public void SetHellStyle(bool hell)
    {
        foreach (var g in border)
            if (g != null) g.color = hell ? hellBorder : dayBorder;
    }

    public void Say(string lines)
    {
        foreach (var l in lines.Split('|'))
            if (l.Trim().Length > 0) queue.Enqueue(l.Trim());
        if (!running) StartCoroutine(Run());
    }

    // time-critical lines (DRIVE!, SWIM!): throw away the backlog and say this straight away
    public void SayNow(string lines)
    {
        queue.Clear();
        cutCurrent = running;
        urgent = true;
        Say(lines);
    }

    // ---------------------------------------------------------------- who is this line?

    public struct Line
    {
        public string speaker, body;
        public Color plate, colour;
        public bool italic;
    }

    public Line Parse(string raw)
    {
        var m = speakerRx.Match(raw);
        if (m.Success && IsName(m.Groups[1].Value.Trim()))
        {
            string who = m.Groups[1].Value.Trim();
            return new Line { speaker = who, body = m.Groups[2].Value.Trim(), plate = Plate(who), colour = otherColor };
        }
        if (raw.Length > 0 && (raw[0] == '"' || raw[0] == '“'))
            return new Line { speaker = "", body = raw, plate = voiceColor, colour = voiceColor, italic = true };
        return new Line { speaker = playerName, body = raw, plate = playerColor, colour = playerColor };
    }

    public Color Plate(string who)
    {
        string key = who.ToLowerInvariant();
        foreach (var p in plates) if (key == p.Key || key.StartsWith(p.Key + " ") || key.EndsWith(" " + p.Key)) return p.Value;
        return unknownSpeaker;
    }

    // "Kessler", "Mrs. Okafor": a name. "The note says": the start of one of your own sentences.
    static bool IsName(string s)
    {
        if (plates.ContainsKey(s.ToLowerInvariant())) return true;
        string[] words = s.Split(' ');
        if (words.Length > 3) return false;
        foreach (var w in words) if (w.Length == 0 || !char.IsUpper(w[0])) return false;
        return true;
    }

    // ---------------------------------------------------------------- saying it

    IEnumerator Run()
    {
        running = true;
        // (outer loop: a line that arrives while the box is fading out is still said, not left stranded in the queue)
        while (queue.Count > 0)
        {
            bars = !urgent && Calm();
            Show(Parse(queue.Peek()), 0);
            yield return Fade(1f);

            while (queue.Count > 0)
            {
                cutCurrent = false;
                Line line = Parse(queue.Dequeue());
                Show(line, 0);
                text.ForceMeshUpdate();
                int count = text.textInfo.characterCount;
                if (hintText != null) hintText.alpha = 0f;

                for (int i = 0; i < count; i++)
                {
                    if (Pressed() || cutCurrent) break;
                    text.maxVisibleCharacters = i + 1;
                    char c = text.textInfo.characterInfo[i].character;
                    if (blip != null && char.IsLetterOrDigit(c) && i % 3 == 0) Mumble();
                    float wait = charDelay * (c == '.' || c == '?' || c == '!' || c == ',' ? 6f : 1f);
                    yield return new WaitForSeconds(wait);
                }
                text.maxVisibleCharacters = 99999;
                lineShownAt = Time.unscaledTime;
                yield return null;   // so the press that finished the typing doesn't also skip the line

                // stay on this line until the player presses E (blinking prompt)
                for (float t = 0f; !Pressed() && !cutCurrent; t += Time.unscaledDeltaTime)
                {
                    if (hintText != null) hintText.alpha = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * 2.6f));
                    else text.text = Old(line) + ((t % 1f) < 0.6f ? advanceHint : "");
                    yield return null;
                }
                if (hintText != null) hintText.alpha = 0f; else text.text = Old(line);
                yield return null;
            }

            bars = false;
            urgent = false;
            yield return Fade(0f);
        }
        running = false;
    }

    bool Pressed() => !PauseMenu.Paused && (Input.GetKeyDown(advanceKey)
                      || (AutoAdvanceAfter > 0f && text.maxVisibleCharacters > 9999 && Time.unscaledTime - lineShownAt > AutoAdvanceAfter));

    void Show(Line line, int visible)
    {
        if (nameText != null)
        {
            nameText.text = line.speaker.ToUpperInvariant();
            nameText.color = line.plate;
            if (accent != null) accent.color = line.plate;
            text.color = line.colour;
            text.text = line.italic ? "<i>" + line.body + "</i>" : line.body;
        }
        else text.text = Old(line);
        text.maxVisibleCharacters = visible;
    }

    // the old single-text box: the name goes in front of the line
    string Old(Line line)
    {
        string body = "<color=#" + ColorUtility.ToHtmlStringRGB(line.colour) + ">" + (line.italic ? "<i>" + line.body + "</i>" : line.body) + "</color>";
        return line.speaker.Length == 0 ? body : "<color=#" + ColorUtility.ToHtmlStringRGB(line.plate) + ">" + line.speaker.ToUpperInvariant() + "</color>  " + body;
    }

    // letterbox only when nothing else needs your eyes: on foot, nobody chasing you, no boss on screen
    bool Calm()
    {
        if (car != null && car.InCar) return false;
        if (van != null && van.InVan) return false;
        if (chase != null && chase.Chasing) return false;
        foreach (var g in fightBars) if (g != null && g.activeInHierarchy) return false;
        return Time.timeScale > 0.5f;
    }

    void Update()
    {
        bool want = (running && bars) || (ChoiceDialogue.Instance != null && ChoiceDialogue.Instance.Open);
        float to = want ? 1f : 0f;
        if (Mathf.Approximately(barK, to)) return;
        barK = Mathf.MoveTowards(barK, to, barSpeed * Time.unscaledDeltaTime);
        SetBars(Mathf.SmoothStep(0f, 1f, barK));
    }

    void SetBars(float k)
    {
        foreach (var b in new[] { barTop, barBottom })
            if (b != null) b.sizeDelta = new Vector2(b.sizeDelta.x, barHeight * k);
    }

    // a low mumbled syllable (not a beep): a random one of the library's, slightly varied in pitch
    void Mumble()
    {
        blip.pitch = Random.Range(0.9f, 1.04f);
        if (voice != null && voice.Length > 0)
            blip.PlayOneShot(voice[Random.Range(0, voice.Length)], 0.7f * Sounds.Volume("General/Dialogue blip"));
        else
            blip.Play();
    }

    IEnumerator Fade(float target)
    {
        float start = group.alpha;
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, t / fadeTime);
            yield return null;
        }
        group.alpha = target;
    }
}
