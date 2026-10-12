using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;

// The subtitle panel. Every line shows WHO is talking:
//   - your own lines (plain text in the script): the name plate reads YOU and the text is bone white
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
        { "vane", new Color(0.92f, 0.22f, 0.17f) },
        { "clinic employee", new Color(.75f, .75f, .75f) },
        { "unseen voice", new Color(.65f, .7f, .8f) },
        { "the demon king", new Color(1f, 0.42f, 0.1f) },
        { "???", new Color(0.55f, 0.55f, 0.6f) },
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
    static readonly Regex speakerRx = new Regex(@"^([A-Za-z?][A-Za-z? .'&\-]{0,24}):\s+(.+)$", RegexOptions.Singleline);

    struct QueuedLine { public string raw, batch; public int generation; }
    private readonly Queue<QueuedLine> queue = new Queue<QueuedLine>();
    private readonly Dictionary<string, int> pendingBatches = new Dictionary<string, int>();
    private int generation;
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
    private DialoguePresentation presentation;

    void Awake()
    {
        Instance = this;
        presentation = DialoguePresentation.Subtitle(this);
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
        Raise();
    }

    // The cutscene letterboxes (LetterboxTop/Bottom) are made after this box, so they drew over the subtitles. Keep the
    // dialogue's own bars and the box above everything else on the canvas, just under the pause menu.
    public void Raise()
    {
        var parent = transform.parent;
        if (parent == null) return;
        if (barTop != null && barTop.parent == parent) barTop.SetAsLastSibling();
        if (barBottom != null && barBottom.parent == parent) barBottom.SetAsLastSibling();
        transform.SetAsLastSibling();
        if (ChoiceDialogue.Instance != null && ChoiceDialogue.Instance.transform.parent == parent) ChoiceDialogue.Instance.transform.SetAsLastSibling();
        var pause = parent.Find("PauseMenu");
        if (pause != null) pause.SetAsLastSibling();
    }

    public void SetHellStyle(bool hell)
    {
        foreach (var g in border)
            if (g != null) g.color = hell ? hellBorder : dayBorder;
    }

    public void Say(string lines)
    {
        if (string.IsNullOrWhiteSpace(lines)) return;
        string batch = lines.Trim();
        // Two collider/event callbacks may request the same speech before it finishes.
        // Deduplicate whole requests only: deliberate repeated words inside a speech remain intact.
        if (pendingBatches.ContainsKey(batch)) return;
        var parts = new List<string>();
        foreach (var l in batch.Split('|')) if (l.Trim().Length > 0) parts.Add(l.Trim());
        if (parts.Count == 0) return;
        pendingBatches[batch] = parts.Count;
        foreach (var l in parts) queue.Enqueue(new QueuedLine { raw = l, batch = batch, generation = generation });
        Raise();   // other scripts add things to the canvas as the game runs
        if (!running) StartCoroutine(Run());
    }

    // time-critical lines (DRIVE!, SWIM!): throw away the backlog and say this straight away
    public void SayNow(string lines)
    {
        if (string.IsNullOrWhiteSpace(lines)) return;
        if (urgent && pendingBatches.ContainsKey(lines.Trim())) return;
        generation++;
        queue.Clear();
        pendingBatches.Clear();
        cutCurrent = running;
        urgent = true;
        Say(lines);
    }

    public void Clear()
    {
        StopAllCoroutines();
        generation++;
        queue.Clear(); pendingBatches.Clear();
        running = cutCurrent = urgent = bars = false;
        if (tutorialCard != null) Destroy(tutorialCard);   // (its coroutine has just been stopped: it would stay up for good)
        if (group != null) group.alpha = 0f;
        if (hintText != null) hintText.alpha = 0f;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // ---------------------------------------------------------------- who is this line?

    public struct Line
    {
        public string speaker, body;
        public Color plate, colour;
        public bool italic, narration;
    }

    public Line Parse(string raw)
    {
        // "*text*": narration, not anyone's speech (white italics with a shadow, no box)
        if (raw.Length > 1 && raw[0] == '*')
            return new Line { speaker = "", body = raw.Trim('*', ' '), plate = Color.white, colour = Color.white, italic = true, narration = true };
        var m = speakerRx.Match(raw);
        if (m.Success && IsName(m.Groups[1].Value.Trim()))
        {
            string who = m.Groups[1].Value.Trim();
            if (who == "You") raw = m.Groups[2].Value.Trim();   // your own line inside someone else's speech (HS.S)
            else return new Line { speaker = who, body = m.Groups[2].Value.Trim(), plate = Plate(who), colour = otherColor };
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
        if (!tutorialShown && InOffice) yield return Tutorial();
        // (outer loop: a line that arrives while the box is fading out is still said, not left stranded in the queue)
        while (queue.Count > 0)
        {
            bars = !urgent && Calm();
            Show(Parse(queue.Peek().raw), 0);
            yield return Fade(1f);

            while (queue.Count > 0)
            {
                cutCurrent = false;
                var queued = queue.Dequeue();
                Line line = Parse(queued.raw);
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
                if (queued.generation == generation && pendingBatches.TryGetValue(queued.batch, out int remaining))
                {
                    if (remaining <= 1) pendingBatches.Remove(queued.batch);
                    else pendingBatches[queued.batch] = remaining - 1;
                }
                yield return null;
            }

            bars = false;
            urgent = false;
            yield return Fade(0f);
        }
        running = false;
    }

    bool Pressed() => !PauseMenu.Paused && !(ChoiceDialogue.Instance != null && ChoiceDialogue.Instance.Open) && (Input.GetKeyDown(advanceKey) || CutsceneSkip.Skipping
                      || (text.maxVisibleCharacters > 9999 && Time.unscaledTime - lineShownAt > (AutoAdvanceAfter > 0f ? AutoAdvanceAfter : InEcho ? 10f : 3f)));   // no key needed: a line stays 3 s (10 s in the Echo realm)

    static bool InEcho => RevisedPlotDirector.Instance != null && RevisedPlotDirector.Instance.Echo;

    // a one-off "how talking works" card before the first line said inside the office, where the conversations are
    // (not at the start of the game: the prologue and the street have nobody to answer)
    static bool InOffice => Camera.main != null && Camera.main.gameObject.scene.name == "MainGameScene" && OfficeHorrorDirector.In(Camera.main.transform.position);
    static bool tutorialShown;
    GameObject tutorialCard;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetTutorial() => tutorialShown = false;

    IEnumerator Tutorial()
    {
        tutorialShown = true;
        var parent = transform.parent;
        if (parent == null || AutoAdvanceAfter > 0f) yield break;
        var panel = tutorialCard = new GameObject("DialogueTutorial", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(parent, false);
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.88f);
        var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(panel.transform, false);
        t.font = text.font; t.fontSize = 30f; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        t.color = DialoguePresentation.Bone; t.lineSpacing = 14f;
        t.text = "<size=150%><color=#C70D0A>TALKING TO THE DEAD</color></size>\n\n" +
                 "<color=#C70D0A>[E]</color>  next line\n" +
                 "<color=#C70D0A>[1-4]</color>  choose your answer\n\n" +
                 "Some of them won't wait for you.\n\n" +
                 "<alpha=#88>[E] to go on";
        var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(160f, 0f); trt.offsetMax = new Vector2(-160f, 0f);
        panel.transform.SetAsLastSibling();
        yield return null;
        while (!Input.GetKeyDown(advanceKey)) yield return null;
        Destroy(panel);
        yield return null;
    }

    void Show(Line line, int visible)
    {
        if (presentation != null) presentation.SetNarration(line.narration);
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
        if (presentation != null) presentation.RefreshLine();
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
