using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GhostChoice
{
    public string text;
    [TextArea] public string reply;         // the ghost's answer, said through the DialogueBox
    public int next = -1;                   // node to continue with, -1 ends the conversation
    public int karma;                       // +1 kind, -1 cruel: weighs on the bridge judgement
}

[System.Serializable]
public class GhostNode
{
    [TextArea] public string line;
    public GhostChoice[] choices;           // empty: the line is just said and the talk ends
}

// A dead colleague still haunting the office. Press F (Interactable.onUse -> Talk) for a
// multiple-choice conversation: some answers get you puzzle hints, some weigh on your soul.
// They wander slowly around the room they died in (idle / walk on the civilian controller's "Speed"), never
// through a wall, and stand still and turn to you when you come close, so they're easy to talk to.
public class GhostNPC : MonoBehaviour
{
    public string ghostName = "Priya";
    public GhostNode[] nodes;
    [TextArea] public string afterLine = "...";   // talked to again once the conversation is done
    public UnityEngine.Events.UnityEvent onFinished = new UnityEngine.Events.UnityEvent();   // a conversation has run its course

    [Header("Look")]
    public Transform body;
    public Animator animator;               // Civilian.controller: "Speed" 0 idle .. 1.3 walk
    public float hoverHeight = 0f;          // body lifted this far off the floor
    public float bobHeight = 0f;
    public float bobSpeed = 1.3f;
    public float driftRadius = 1.6f;        // wanders this far from where it was placed
    public float driftSpeed = 0.55f;
    public float walkClipSpeed = 1.3f;      // the walk clip's own pace: played slower to match driftSpeed
    public float holdRange = 3.5f;          // stops drifting and faces you inside this
    public Vector2 alphaRange = new Vector2(0.18f, 0.45f);
    public float whisperRange = 12f;

    private Transform player;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private AudioSource whisper;
    private Vector3 basePos, home;
    private float seed, driftTime;
    private bool finished;
    private readonly HashSet<int> judged = new HashSet<int>();   // karma counts once per question

    // A ghost is mid-conversation: its question is up, its answer is still being said, or the next question is on
    // its way. PlayerInteractor shows no "F Talk to ..." (and takes no F) until it's over.
    public static bool Conversing { get; private set; }
    private bool mine, nextComing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => Conversing = false;

    void OnDisable()
    {
        if (mine) { mine = false; Conversing = false; }
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        if (body == null) body = transform;
        basePos = body.localPosition;
        home = transform.position;
        renderers = body.GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        seed = Random.value * 10f;

        whisper = gameObject.AddComponent<AudioSource>();
        whisper.clip = Sounds.Clip("Office/Ghost whispers", null) ?? ProceduralAudio.Whisper(Random.Range(2.5f, 4f));
        whisper.loop = true;
        whisper.spatialBlend = 1f;
        whisper.rolloffMode = AudioRolloffMode.Linear;
        whisper.maxDistance = whisperRange;
        whisper.volume = 0.5f * Sounds.Volume("Office/Ghost whispers");
        whisper.pitch = Random.Range(0.75f, 1f);
        whisper.Play();
    }

    void Update()
    {
        body.localPosition = basePos + Vector3.up * (hoverHeight + Mathf.Sin((Time.time + seed) * bobSpeed) * bobHeight);

        Vector3 toPlayer = player != null ? player.position - transform.position : Vector3.one * 999f;
        toPlayer.y = 0f;
        bool talking = ChoiceDialogue.Instance != null && ChoiceDialogue.Instance.Open;
        if (mine && !talking && !nextComing && (DialogueBox.Instance == null || !DialogueBox.Instance.Busy)) { mine = false; Conversing = false; }
        Vector3 look = toPlayer;
        bool moving = false;
        if (!talking && toPlayer.magnitude > holdRange && driftRadius > 0f)
        {
            // a slow wandering loop around home
            driftTime += Time.deltaTime * driftSpeed / Mathf.Max(driftRadius, 0.1f);
            Vector3 target = home + new Vector3(Mathf.PerlinNoise(seed, driftTime) - 0.5f, 0f, Mathf.PerlinNoise(driftTime, seed + 3f) - 0.5f) * 2f * driftRadius;
            Vector3 step = Vector3.MoveTowards(transform.position, target, driftSpeed * Time.deltaTime);
            Vector3 move = step - transform.position;
            // stand and wait rather than walk through a wall or a desk
            bool blocked = move.sqrMagnitude > 0f && Physics.Raycast(transform.position + Vector3.up * 0.8f, move.normalized, 0.6f, ~0, QueryTriggerInteraction.Ignore);
            if (!blocked && move.sqrMagnitude > 0.0000001f)
            {
                look = move;
                transform.position = step;
                moving = true;
            }
        }
        if (animator != null)
        {
            animator.SetFloat("Speed", moving ? walkClipSpeed : 0f, 0.2f, Time.deltaTime);
            animator.speed = moving ? Mathf.Clamp(driftSpeed / walkClipSpeed, 0.3f, 1f) : 1f;
        }
        if (look.sqrMagnitude > 0.00001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 2f * Time.deltaTime);

        // unsteady, like a bad signal
        float a = Mathf.Lerp(alphaRange.x, alphaRange.y, Mathf.PerlinNoise(seed, Time.time * 2f));
        if (Random.value < 0.01f) a *= 0.2f;
        if (block == null) block = new MaterialPropertyBlock();   // not serialized: gone after a script reload in play mode
        foreach (var r in renderers)
        {
            r.GetPropertyBlock(block);
            var c = r.sharedMaterial.GetColor("_BaseColor");
            c.a = a;
            block.SetColor("_BaseColor", c);
            r.SetPropertyBlock(block);
        }
    }

    public void Talk()
    {
        if (ChoiceDialogue.Instance == null || ChoiceDialogue.Instance.Open) return;
        mine = true;
        Conversing = true;
        if (finished) { Say(afterLine); return; }
        ShowNode(0);
    }

    void ShowNode(int i)
    {
        var node = nodes[i];
        if (node.choices == null || node.choices.Length == 0)
        {
            Say(node.line);
            Finish();
            return;
        }

        var labels = new string[node.choices.Length];
        for (int c = 0; c < labels.Length; c++) labels[c] = node.choices[c].text;

        ChoiceDialogue.Instance.Show(ghostName, node.line, labels, pick =>
        {
            var choice = node.choices[pick];
            if (judged.Add(i)) Judgement.Karma += choice.karma;
            Say(choice.reply);
            if (choice.next >= 0) StartCoroutine(Continue(choice.next));
            else
            {
                Finish();
                if (whisper != null) whisper.volume = 0.15f;
            }
        }, transform);
    }

    void Finish()
    {
        finished = true;
        onFinished.Invoke();
    }

    // a new conversation (the story moved on): talking starts from its first line again
    public void Replace(GhostNode[] conversation)
    {
        nodes = conversation;
        finished = false;
        judged.Clear();
    }

    IEnumerator Continue(int next)
    {
        nextComing = true;
        yield return new WaitForSeconds(0.6f);
        nextComing = false;
        ShowNode(next);
    }

    // the ghost's own lines carry its name, so the box shows who's talking (lines that already name someone are left)
    void Say(string s)
    {
        if (string.IsNullOrEmpty(s) || DialogueBox.Instance == null) return;
        var parts = s.Split('|');
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i].Trim();
            if (p.Length == 0) continue;
            var line = DialogueBox.Instance.Parse(p);
            if (line.speaker == DialogueBox.Instance.playerName) parts[i] = ghostName + ": " + p;
        }
        DialogueBox.Instance.Say(string.Join("|", parts));
    }
}
