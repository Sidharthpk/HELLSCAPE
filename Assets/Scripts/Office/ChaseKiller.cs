using System.Collections.Generic;
using UnityEngine;

// The killer hunting you through the silo complex. There's no NavMesh up there, so he follows your
// footsteps: Arm() starts recording where you walk, Begin() drops him in and he follows the trail.
// Slower than you when he's close, faster when he falls behind, so standing still gets you caught.
// Sits on its own always-on object (it records your steps before he exists) and drives the killer.
public class ChaseKiller : MonoBehaviour
{
    public Transform killer;                // the KillerBoss object (the fight comes later, after the push)
    public Transform player;
    public Animator animator;
    public PlayerHealth health;

    [Header("Chase")]
    public float speed = 4.4f;              // player walks 5, sprints 7
    public float catchUpSpeed = 7.5f;       // when he's far behind
    public float catchUpDistance = 18f;
    public float catchDistance = 1.5f;
    public float ladderSpeed = 1.4f;        // how fast he climbs down after you (slower than you: the ladder buys a lead)
    public float hitDamage = 20f;
    public float stunAfterHit = 1.8f;
    public float trailSpacing = 0.6f;
    [TextArea] public string startLine = "Footsteps. Heavy ones. Behind me.|RUN.";
    [TextArea] public string hitLines = "AGH--!|He's right on me--|MOVE!";

    [Header("Sound")]
    public AudioSource sfx;
    public AudioClip hitClip;               // procedural thud if empty

    private readonly Queue<Vector3> trail = new Queue<Vector3>();
    private Vector3 lastCrumb;
    private bool recording, chasing;
    private float stun;
    private string[] hits;
    private int hitIndex;

    public bool Chasing => chasing;

    void Awake()
    {
        if (player == null) { var p = GameObject.FindGameObjectWithTag("Player"); if (p != null) player = p.transform; }
        if (health == null && player != null) health = player.GetComponent<PlayerHealth>();
        hits = hitLines.Split('|');
    }

    // start recording your path (call on arrival, before he shows up)
    public void Arm()
    {
        trail.Clear();
        recording = true;
        lastCrumb = Feet();
        trail.Enqueue(lastCrumb);
    }

    public void Begin(Transform spawn)
    {
        if (chasing) return;
        if (!recording) Arm();
        killer.SetPositionAndRotation(spawn.position, spawn.rotation);
        killer.gameObject.SetActive(true);
        chasing = true;
        // he picks your trail up where he stands (not back where you first walked in: he'd turn round and leave)
        var crumbs = trail.ToArray();
        int nearest = 0;
        for (int i = 1; i < crumbs.Length; i++)
            if ((crumbs[i] - spawn.position).sqrMagnitude <= (crumbs[nearest] - spawn.position).sqrMagnitude) nearest = i;
        for (int i = 0; i < nearest; i++) trail.Dequeue();
        hitClip = Sounds.Clip("Silo Complex/Killer hits you (chase)", hitClip != null ? hitClip : ProceduralAudio.Thud());
        if (DialogueBox.Instance != null) DialogueBox.Instance.Say(startLine);
    }

    // he holds off this long before coming on (the moment after the shove: time to get your feet under you)
    public void HeadStart(float seconds) => stun = Mathf.Max(stun, seconds);

    // a checkpoint respawn at the complex: he's gone, your steps are recorded afresh, the chase can start again
    public void ResetChase()
    {
        Stop(true);
        stun = 0f;
        Arm();
    }

    // hide: vanish (e.g. the elevator doors close on him)
    public void Stop(bool hide)
    {
        chasing = false;
        recording = false;
        SetSpeed(0f);
        if (hide) killer.gameObject.SetActive(false);
    }

    // the player root sits at the capsule's middle, half its (scaled) height above the floor
    Vector3 Feet() => player.position - Vector3.up * player.lossyScale.y;

    void Update()
    {
        if (player == null) return;

        if (recording && player.gameObject.activeInHierarchy)
        {
            Vector3 f = Feet();
            if ((f - lastCrumb).sqrMagnitude > trailSpacing * trailSpacing)
            {
                trail.Enqueue(f);
                lastCrumb = f;
            }
        }
        if (!chasing) return;

        stun -= Time.deltaTime;
        if (stun > 0f) { SetSpeed(0f); return; }

        Vector3 me = killer.position;
        Vector3 toPlayer = Feet() - me;

        // caught you (not while you're on the rungs: you couldn't do anything about it)
        if (toPlayer.magnitude < catchDistance && !Ladder.Climbing)
        {
            stun = stunAfterHit;
            KillerBoss.Attack(animator);
            Vector3 face = toPlayer; face.y = 0f;
            if (face.sqrMagnitude > 0.001f) killer.rotation = Quaternion.LookRotation(face);
            if (health != null) health.TakeDamage(hitDamage);
            if (sfx != null) sfx.PlayOneShot(hitClip, Sounds.Volume("Silo Complex/Killer hits you (chase)"));
            if (DialogueBox.Instance != null && hits.Length > 0) DialogueBox.Instance.Say(hits[hitIndex++ % hits.Length]);
            var rb = player.GetComponent<Rigidbody>();
            Vector3 shove = toPlayer; shove.y = 0f;
            if (rb != null && shove.sqrMagnitude > 0.01f) rb.AddForce(shove.normalized * 6f + Vector3.up * 2f, ForceMode.VelocityChange);
            return;
        }

        // drop crumbs he's already past, then walk the trail
        while (trail.Count > 1 && (trail.Peek() - me).sqrMagnitude < 0.35f * 0.35f) trail.Dequeue();
        Vector3 target = trail.Count > 0 ? trail.Peek() : Feet();

        float remaining = TrailLength(me);
        float s = remaining > catchUpDistance ? catchUpSpeed : speed;
        Vector3 d = target - me;
        if (Mathf.Abs(d.y) > 0.25f && new Vector2(d.x, d.z).magnitude < 0.6f) s = ladderSpeed;   // on the ladder
        Vector3 step = Vector3.MoveTowards(me, target, s * Time.deltaTime);
        killer.position = step;

        Vector3 flat = target - me; flat.y = 0f;
        if (flat.sqrMagnitude > 0.001f)
            killer.rotation = Quaternion.Slerp(killer.rotation, Quaternion.LookRotation(flat), 10f * Time.deltaTime);
        SetSpeed(s);
    }

    float TrailLength(Vector3 from)
    {
        float d = 0f;
        Vector3 prev = from;
        foreach (var c in trail) { d += Vector3.Distance(prev, c); prev = c; }
        return d + Vector3.Distance(prev, Feet());
    }

    void SetSpeed(float s) { if (animator != null && animator.enabled) animator.SetFloat("Speed", s); }
}
