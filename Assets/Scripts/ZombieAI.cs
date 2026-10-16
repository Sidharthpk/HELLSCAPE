using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

// The damned wander with Sad Walk or Sneak Walk at individual speeds. Alert enemies stalk from farther away,
// then run inside runDistance and keep running until beyond runReleaseDistance. While driving, the car is the target.
// Animation: DamnedController (HellScape > Build Damned Animator), driven by state name; each zombie plays
// its clips at its own pace, from its own point in the cycle.
public class ZombieAI : MonoBehaviour
{
    public Transform player;
    public Transform car;
    public Animator animator;
    public NavMeshAgent agent;

    [Header("Speeds (randomized per zombie)")]
    public Vector2 walkSpeedRange = new Vector2(0.18f, 0.32f);   // lost: a slow, dragging shamble
    public Vector2 runSpeedRange = new Vector2(2.6f, 4.2f);
    [FormerlySerializedAs("crawlSpeedRange")] public Vector2 sneakSpeedRange = new Vector2(0.35f, 0.8f);

    [Header("Kinds")]
    [FormerlySerializedAs("crawlerChance"), Range(0f, 1f)] public float sneakChance = 0.55f;
    [Range(0f, 1f)] public float dormantChance = 0.25f;        // of the rest: stands swaying until something gets close
    public Vector2 animVariation = new Vector2(0.85f, 1.18f);  // each zombie's own playback pace

    [Header("Senses")]
    public float sightRange = 18f;
    public float sightAngle = 75f;          // half-angle in front of it
    public float hearRange = 5f;            // right beside it: noticed from any side
    public float callRange = 9f;            // a scream drags the ones this close in with it
    public float giveUpAfter = 7f;          // seconds out of sight (and far enough away) before it drifts off
    public float giveUpDistance = 28f;
    public float wanderRadius = 7f;
    public float runDistance = 8f;
    public float runReleaseDistance = 10f;

    [Header("Animation (in-place clips: m/s their feet cover at 1x)")]
    public float walkClipSpeed = 0.41f;
    public float runClipSpeed = 3.67f;
    public float sneakClipSpeed = 0.7f;
    public float screamLength = 2.8f;

    [Header("Attack")]
    public float attackRange = 1.8f;
    public float attackCooldown = 1.6f;
    public int attackDamage = 10;

    enum Mode { Wander, Dormant, Scream, Chase }

    static readonly List<ZombieAI> all = new List<ZombieAI>();

    private Mode mode;
    private bool sneaking, running;
    private float pace;                     // this one's animation playback multiplier
    private float walkSpeed, runSpeed;
    private float attackTimer, repathTimer, senseTimer, lostTimer, wanderPause, busyUntil, hitAt = -1f, screamEnds;
    private Vector3 home, lastDest;
    private Transform target;
    private string state = "";

    public bool Chasing => mode == Mode.Chase || mode == Mode.Scream;
    public bool Running => running;

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    void Start()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        agent.angularSpeed = Mathf.Max(agent.angularSpeed, 300f);
        agent.acceleration = Mathf.Max(agent.acceleration, 10f);
        agent.autoBraking = true;
        // one city-sized navmesh: give the pathfinder enough work per frame that long paths actually finish
        NavMesh.pathfindingIterationsPerFrame = Mathf.Max(NavMesh.pathfindingIterationsPerFrame, 2000);

        sneaking = Random.value < sneakChance;
        pace = Random.Range(animVariation.x, animVariation.y);
        walkSpeed = sneaking ? Random.Range(sneakSpeedRange.x, sneakSpeedRange.y) : Random.Range(walkSpeedRange.x, walkSpeedRange.y);
        runSpeed = Random.Range(runSpeedRange.x, runSpeedRange.y);
        home = transform.position;
        wanderPause = Random.Range(0f, 4f);
        senseTimer = Random.Range(0f, 0.25f);   // spread the line-of-sight checks over frames

        mode = Random.value < dormantChance ? Mode.Dormant : Mode.Wander;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        // start somewhere in the cycle, not in step with the one beside it
        Go(sneaking ? "Sneak" : "Upright", 0f, Random.value);
    }

    // hit, shot at, or dragged in by another's scream
    public void Alert()
    {
        if (Chasing || !enabled) return;
        lostTimer = 0f;
        repathTimer = 0f;
        target = PickTarget();
        if (target != null && Flat(target.position - transform.position).magnitude <= runDistance)
        { mode = Mode.Chase; running = true; }
        else
        {
            // stop, turn to face it, and scream
            mode = Mode.Scream;
            agent.isStopped = true;
            float rate = pace * Random.Range(0.95f, 1.15f);
            Go("Scream", 0.3f, 0f);
            animator.speed = rate;
            screamEnds = Time.time + screamLength * 0.85f / rate;   // off before the last beat of the clip
            if (TryGetComponent(out Zombie z)) z.Scream();
        }
        foreach (var o in all)
            if (o != this && !o.Chasing && (o.transform.position - transform.position).sqrMagnitude < callRange * callRange)
                o.Invoke(nameof(Alert), Random.Range(0.4f, 1.4f));   // a ripple, not all at once
    }

    void Update()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        target = PickTarget();
        // Proximity interrupts the scream so nearby enemies start pursuing immediately.
        if (target != null && mode != Mode.Chase && Flat(target.position - transform.position).magnitude <= runDistance && Notices(target))
        { mode = Mode.Chase; running = true; }

        senseTimer -= Time.deltaTime;
        if (senseTimer <= 0f)
        {
            senseTimer = 0.25f;
            bool noticed = target != null && Notices(target);
            if (!Chasing && noticed) Alert();
            else if (mode == Mode.Chase)
            {
                lostTimer = noticed ? 0f : lostTimer + 0.25f;
                float d = target != null ? Vector3.Distance(transform.position, target.position) : float.MaxValue;
                if (lostTimer > giveUpAfter && d > giveUpDistance) { mode = Mode.Wander; running = false; home = transform.position; }
            }
        }

        switch (mode)
        {
            case Mode.Scream:
                Face(target, 4f);
                if (Time.time >= screamEnds) { mode = Mode.Chase; Go("Upright", 0.35f); }
                break;
            case Mode.Chase: Chase(); break;
            case Mode.Wander: Wander(); break;
            default: agent.isStopped = true; break;   // dormant
        }

        // the swing lands (or whiffs) partway through
        if (hitAt >= 0f && Time.time >= hitAt)
        {
            hitAt = -1f;
            if (target == player && player != null && Flat(player.position - transform.position).magnitude <= attackRange + 0.5f)
            {
                PlayerHealth ph = player.GetComponentInParent<PlayerHealth>();
                if (ph != null) ph.TakeDamage(attackDamage);
            }
        }

        Animate();
    }

    bool Notices(Transform t)
    {
        Vector3 to = t.position - transform.position;
        float d = to.magnitude;
        float hear = hearRange;
        if (d <= hear) return true;
        float sight = sightRange;
        if (d > sight) return false;
        if (t == player && Vector3.Angle(transform.forward, Flat(to)) > sightAngle) return false;
        Vector3 eye = transform.position + Vector3.up * 1.6f;
        Vector3 aim = t.position + Vector3.up * 0.5f;
        foreach (var h in Physics.RaycastAll(eye, aim - eye, Vector3.Distance(eye, aim), ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform) || h.collider.transform.IsChildOf(t)) continue;
            if (h.collider.GetComponentInParent<Zombie>() != null) continue;   // the others don't block the view
            return false;
        }
        return true;
    }

    void Chase()
    {
        if (target == null) { agent.isStopped = true; return; }
        if (Time.time < busyUntil) { agent.isStopped = true; Face(target, 6f); return; }   // mid-swing
        float dist = Flat(target.position - transform.position).magnitude;
        if (dist <= attackRange) { Attack(); return; }

        agent.isStopped = false;
        running = dist <= (running ? Mathf.Max(runDistance, runReleaseDistance) : runDistance);
        agent.speed = running ? runSpeed : walkSpeed;
        // re-path a few times a second, and only when the target has actually moved: a path request every
        // frame never finishes on a navmesh this size and the zombie just stands there
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f || (target.position - lastDest).sqrMagnitude > 4f)
        {
            repathTimer = 0.4f;
            lastDest = target.position;
            agent.SetDestination(target.position);
        }
    }

    void Wander()
    {
        agent.speed = walkSpeed;
        if (agent.pathPending) return;
        if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.3f)
        {
            agent.isStopped = true;
            wanderPause -= Time.deltaTime;
            if (wanderPause > 0f) return;
            wanderPause = Random.Range(3f, 9f);
            Vector2 r = Random.insideUnitCircle * wanderRadius;
            if (NavMesh.SamplePosition(home + new Vector3(r.x, 0f, r.y), out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
            }
        }
    }

    void Attack()
    {
        agent.isStopped = true;
        Face(target, 10f);
        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f) return;

        float rate = pace * 1.25f;
        float lands = 0.6f, hold = 1.9f;
        Go("Attack", 0.12f, 0f);
        animator.speed = rate;
        hitAt = Time.time + lands / rate;
        busyUntil = Time.time + hold / rate;
        attackTimer = attackCooldown + hold / rate;
    }

    // one clip family at a time, played at the pace the feet actually travel (times this zombie's own pace)
    void Animate()
    {
        if (animator == null || mode == Mode.Scream) return;
        if (Time.time < busyUntil) return;      // an attack is playing out
        string posture = mode == Mode.Chase || sneaking ? "Sneak" : "Upright";
        Go(posture, 0.3f);
        float v = agent.isStopped ? 0f : agent.velocity.magnitude;
        float slow = posture == "Sneak" ? sneakClipSpeed : walkClipSpeed, fast = runClipSpeed;
        float param, rate;
        if (v < 0.05f) { param = 0f; rate = pace; }
        else if (!running) { param = slow; rate = Mathf.Clamp(v / slow, 0.25f, 1.5f); }
        else { param = fast; rate = Mathf.Clamp(v / fast, 0.6f, 1.3f) * pace; }
        animator.SetFloat("Speed", param, 0.15f, Time.deltaTime);
        animator.speed = rate;
    }

    // at < 0: a loop, joined somewhere in its cycle (a crowd that changes state together mustn't step in time)
    void Go(string next, float fade, float at = -1f)
    {
        if (animator == null || (state == next && at < 0f)) return;
        state = next;
        if (at >= 0f && fade <= 0f) animator.Play(next, 0, at);
        else FadeTo(animator, next, fade, at >= 0f ? at : Random.value);
    }

    // CrossFade with the start point normalized (CrossFadeInFixedTime's offset is in seconds)
    public static void FadeTo(Animator a, string next, float fade, float at)
    {
        float len = Mathf.Max(a.GetCurrentAnimatorStateInfo(0).length, 0.01f);
        a.CrossFade(next, fade / len, 0, at);
    }

    void Face(Transform t, float rate)
    {
        if (t == null) return;
        Vector3 to = Flat(t.position - transform.position);
        if (to.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), rate * Time.deltaTime);
    }

    // on foot: you. Driving (the player object is switched off): the car. Never an empty parked car.
    Transform PickTarget()
    {
        bool playerActive = player != null && player.gameObject.activeInHierarchy;
        return playerActive ? player : car;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
