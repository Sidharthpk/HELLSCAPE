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
    public int fate;                        // how they answer you: 1 denies it (Lost), 2 justifies it (Damned), 3 owns it (Confessed)
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
public class GhostNPC : MonoBehaviour, ISceneProgress
{
    public enum Fate { Undecided, Lost, Damned, Confessed, Gone }
    public Fate fate;                       // set by the answers you draw out; Gone if the memory fades unanswered
    public float answerSeconds;             // > 0: the question times out and the memory is gone
    public string ghostName = "Priya";
    public GhostNode[] nodes;
    [System.NonSerialized] public string opening;   // said before the first question
    [TextArea] public string afterLine = "...";   // talked to again once the conversation is done
    public UnityEngine.Events.UnityEvent onFinished = new UnityEngine.Events.UnityEvent();   // a conversation has run its course

    [Header("Look")]
    public Transform body;
    public Animator animator;               // Civilian.controller: "Speed" 0 idle .. 1.3 walk
    public Material skin;                   // a model with its own texture: WitnessLook soots this instead of the Burnt materials
    public float hoverHeight = 0f;          // body lifted this far off the floor
    public float bobHeight = 0f;
    public float bobSpeed = 1.3f;
    public float driftRadius = 1.6f;        // wanders this far from where it was placed
    public float driftSpeed = 0.55f;
    public float walkClipSpeed = 1.3f;      // the walk clip's own pace: played slower to match driftSpeed
    public float holdRange = 3.5f;          // stops drifting and faces you inside this
    public Vector2 alphaRange = new Vector2(0.18f, 0.45f);
    public float whisperRange = 12f;

    public bool working;                    // at their desk, bent over it, until the story lets them go
    private Vector3 workLook;
    private Transform spine;

    // stand them at a desk: they stay put and face it until `working` is cleared
    public void WorkAt(Vector3 spot, Vector3 facing)
    {
        transform.SetPositionAndRotation(spot, Quaternion.LookRotation(facing));
        home = spot; workLook = facing; working = true;
    }

    void LateUpdate()
    {
        if (!working || mine || animator == null) return;
        if (spine == null) spine = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Spine) : null;
        if (spine == null || spine.localRotation == leaned) return;   // (a culled animator hasn't reposed it: leaning again would stack)
        spine.rotation = Quaternion.AngleAxis(leanAngle, transform.right) * spine.rotation;   // after the idle clip, so it holds
        leaned = spine.localRotation;
    }
    public float leanAngle = 24f;
    private Quaternion leaned;

    private Transform player;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private AudioSource whisper;
    private Vector3 basePos, home;
    private float seed, driftTime;
    private static RuntimeAnimatorController floatPose;
    private bool finished;
    private readonly HashSet<int> judged = new HashSet<int>();   // karma counts once per question

    public object CaptureProgress() => (finished, fate, new List<int>(judged).ToArray(), afterLine);
    public void RestoreProgress(object state)
    {
        var saved = ((bool finished, Fate fate, int[] questions, string afterLine))state;
        finished = saved.finished; fate = saved.fate; afterLine = saved.afterLine;
        judged.Clear(); foreach (int question in saved.questions) judged.Add(question);
        if (TryGetComponent<WitnessLook>(out var look) && fate != Fate.Undecided)
            look.Set(fate == Fate.Damned ? WitnessLook.Mode.Damned : fate == Fate.Confessed ? WitnessLook.Mode.Confessed : WitnessLook.Mode.Lost);
    }

    // A ghost is mid-conversation: its question is up, its answer is still being said, or the next question is on
    // its way. PlayerInteractor shows no "F Talk to ..." (and takes no F) until it's over.
    public static bool Conversing { get; private set; }
    private bool mine, nextComing;
    public bool IsTalking => mine;
    public void ExpireEcho()
    {
        CancelConversation();
        if (!finished && fate == Fate.Undecided) Fade();
    }
    private int choiceRevision = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => Conversing = false;

    public void CancelConversation()
    {
        StopAllCoroutines();
        if (mine && ChoiceDialogue.Instance != null && ChoiceDialogue.Instance.Revision == choiceRevision) ChoiceDialogue.Instance.Close();
        choiceRevision = -1;
        nextComing = false;
        if (mine) { mine = false; Conversing = false; }
    }

    void OnDisable() => CancelConversation();
    void OnDestroy() => CancelConversation();

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
        if (floatPose == null) floatPose = Resources.Load<RuntimeAnimatorController>("OfficeHorror/DamnedFloat");   // HellScape > Build Damned Float Pose

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
        bool lost = fate == Fate.Lost || fate == Fate.Gone;   // the Lost hang in the floating pose, they never walk again
        if (lost && animator != null && animator.isHuman && floatPose != null && animator.runtimeAnimatorController != floatPose)   // (a Generic Meshy rig keeps its own idle)
        {
            animator.runtimeAnimatorController = floatPose;
            animator.applyRootMotion = false;
            animator.speed = 1f;
            working = false;
        }
        if (lost) body.localPosition += Vector3.up * (0.35f + Mathf.Sin((Time.time + seed) * 0.6f) * 0.15f);
        if (working && !mine) look = workLook;
        if (!lost && !working && !talking && toPlayer.magnitude > holdRange && driftRadius > 0f)
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
        if (animator != null && !lost)
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
        if (ChoiceDialogue.Instance == null || ChoiceDialogue.Instance.Open || Conversing || nextComing || mine) return;
        mine = true;
        Conversing = true;
        if (TryGetComponent<OfficeWitness>(out var witness) && witness.RedTalk()) return;
        if (fate == Fate.Gone) { Say(HS.S("240", ghostName)); return; }
        if (finished) { Say(afterLine); return; }
        if (string.IsNullOrEmpty(opening)) ShowNode(0);
        else { Say(opening); StartCoroutine(Continue(0)); }   // what is said before the first question
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
            choiceRevision = -1;
            ApplyFate(choice.fate);
            Say(choice.reply);
            if (choice.next >= 0) StartCoroutine(Continue(choice.next));
            else
            {
                Finish();
                if (whisper != null) whisper.volume = 0.15f;
            }
        }, transform);
        choiceRevision = ChoiceDialogue.Instance.Revision;
        if (answerSeconds > 0f) ChoiceDialogue.Instance.SetDeadline(answerSeconds, Fade);   // (the Echo clock waits while a question is open: EchoVisit)
    }

    void Fade()
    {
        choiceRevision = -1;
        fate = Fate.Gone;
        if (TryGetComponent<WitnessLook>(out var faded)) faded.Set(WitnessLook.Mode.Lost);
        Say(HS.S("240", ghostName));
        // A timeout is not a confession and must never invoke a story completion event.
        finished = true;
        if (TryGetComponent<OfficeWitness>(out var witness)) witness.Resolved(fate);
    }

    void ApplyFate(int f)
    {
        if (f == 0 || fate == Fate.Gone || fate == Fate.Confessed) return;   // (once they have owned it, nothing undoes it)
        fate = f == 1 ? Fate.Lost : f == 2 ? Fate.Damned : Fate.Confessed;
        var look = GetComponent<WitnessLook>();
        if (TryGetComponent<OfficeWitness>(out var witness)) witness.Resolved(fate);
        if (look == null) return;
        look.Set(fate == Fate.Lost ? WitnessLook.Mode.Lost : fate == Fate.Damned ? WitnessLook.Mode.Damned : WitnessLook.Mode.Confessed);
        if (fate == Fate.Damned) look.Flash();
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
        while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) yield return null;
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
            if (line.speaker == DialogueBox.Instance.playerName && !p.StartsWith("You: ")) parts[i] = ghostName + ": " + p;
        }
        DialogueBox.Instance.Say(string.Join("|", parts));
    }
}
