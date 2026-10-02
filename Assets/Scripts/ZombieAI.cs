using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The damned on the streets. Each one is a little different: some walk, some drag themselves along the ground,
// some are knelt over a body eating. Most start out lost: shambling slowly round the spot they were left in,
// standing dormant, or feeding, until they see you (in range, clear line of sight, in front of them), hear you
// right beside them, or get hit. Then the ones on their feet stop, turn and SCREAM, and run you down; crawlers
// just come, faster than you'd like. The scream drags the others nearby in. Lose them for long enough and they
// drift off again. While you drive, the car is the target.
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
    public Vector2 crawlSpeedRange = new Vector2(0.18f, 0.3f);
    public Vector2 crawlChaseRange = new Vector2(1.1f, 1.7f);

    [Header("Kinds")]
    [Range(0f, 1f)] public float crawlerChance = 0.2f;
    [Range(0f, 1f)] public float feederChance = 0.25f;         // of the ones on their feet: knelt over a body
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

    [Header("Animation (in-place clips: m/s their feet cover at 1x)")]
    public float walkClipSpeed = 0.41f;
    public float runClipSpeed = 3.67f;
    public float crawlClipSpeed = 0.52f;
    public float crawlRunClipSpeed = 2.67f;
    public float screamLength = 2.8f;

    [Header("Attack")]
    public float attackRange = 1.8f;
    public float attackCooldown = 1.6f;
    public int attackDamage = 10;

    enum Mode { Wander, Dormant, Feed, Scream, Chase }

    static readonly List<ZombieAI> all = new List<ZombieAI>();

    private Mode mode;
    private bool crawler;
    private float pace;                     // this one's animation playback multiplier
    private float walkSpeed, runSpeed;
    private float attackTimer, repathTimer, senseTimer, lostTimer, wanderPause, busyUntil, hitAt = -1f, screamEnds;
    private Vector3 home, lastDest;
    private Transform target;
    private string state = "";

    public bool Chasing => mode == Mode.Chase || mode == Mode.Scream;
    public bool Crawler => crawler;

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

        crawler = Random.value < crawlerChance;
        pace = Random.Range(animVariation.x, animVariation.y);
        walkSpeed = crawler ? Random.Range(crawlSpeedRange.x, crawlSpeedRange.y) : Random.Range(walkSpeedRange.x, walkSpeedRange.y);
        runSpeed = crawler ? Random.Range(crawlChaseRange.x, crawlChaseRange.y) : Random.Range(runSpeedRange.x, runSpeedRange.y);
        home = transform.position;
        wanderPause = Random.Range(0f, 4f);
        senseTimer = Random.Range(0f, 0.25f);   // spread the line-of-sight checks over frames

        if (crawler)
        {
            mode = Mode.Wander;
            // low to the ground: fists and bullets should find it down there
            if (TryGetComponent(out Zombie z) && z.mainCollider is CapsuleCollider cap)
            {
                cap.direction = 2;              // lying along its length
                cap.height = 1.7f;
                cap.center = new Vector3(0f, 0.3f, 0.1f);
                cap.radius = 0.3f;
            }
        }
        else if (Random.value < feederChance) mode = Mode.Feed;
        else mode = Random.value < dormantChance ? Mode.Dormant : Mode.Wander;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        // start somewhere in the cycle, not in step with the one beside it
        Go(mode == Mode.Feed ? "Feed" : crawler ? "Crawl" : "Upright", 0f, Random.value);
    }

    // hit, shot at, or dragged in by another's scream
    public void Alert()
    {
        if (Chasing || !enabled) return;
        lostTimer = 0f;
        repathTimer = 0f;
        if (crawler) { mode = Mode.Chase; }
        else
        {
            // stop, turn to face it, and scream
            mode = Mode.Scream;
            agent.isStopped = true;
            float rate = pace * Random.Range(0.95f, 1.15f);
            Go("Scream", 0.3f);
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
                if (lostTimer > giveUpAfter && d > giveUpDistance) { mode = Mode.Wander; home = transform.position; }
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
            default: agent.isStopped = true; break;   // dormant, feeding
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
        float hear = mode == Mode.Feed ? hearRange * 0.7f : hearRange;          // busy eating
        if (d <= hear) return true;
        float sight = mode == Mode.Feed ? sightRange * 0.5f : sightRange;
        if (d > sight) return false;
        if (t == player && Vector3.Angle(transform.forward, Flat(to)) > sightAngle) return false;
        Vector3 eye = transform.position + Vector3.up * (crawler ? 0.5f : 1.6f);
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
        agent.speed = runSpeed;
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

        // a swipe, a lunge for the neck, or (crawlers) a bite at your legs
        bool neck = !crawler && Random.value < 0.3f;
        string move = crawler ? "CrawlBite" : neck ? "NeckBite" : "Attack";
        float rate = pace * (neck ? 1.7f : crawler ? 1.4f : 1.25f);
        float lands = neck ? 1.1f : crawler ? 0.5f : 0.6f;       // seconds into the clip at 1x
        float hold = neck ? 2.4f : crawler ? 1.3f : 1.9f;
        Go(move, 0.12f, 0f);
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
        if (mode == Mode.Feed) { Go("Feed", 0.3f); animator.speed = pace; return; }

        string posture = crawler ? "Crawl" : "Upright";
        Go(posture, 0.3f);
        float v = agent.isStopped ? 0f : agent.velocity.magnitude;
        float slow = crawler ? crawlClipSpeed : walkClipSpeed, fast = crawler ? crawlRunClipSpeed : runClipSpeed;
        float param, rate;
        if (v < 0.05f) { param = 0f; rate = crawler ? 0.25f * pace : pace; }             // crawlers barely twitch when still
        else if (v < (slow + fast) * 0.3f) { param = slow; rate = Mathf.Clamp(v / slow, 0.35f, 1.3f) * pace; }
        else { param = fast; rate = Mathf.Clamp(v / fast, 0.6f, 1.3f) * pace; }
        animator.SetFloat("Speed", param, 0.15f, Time.deltaTime);
        animator.speed = rate;
    }

    void Go(string next, float fade, float at = -1f)
    {
        if (animator == null || (state == next && at < 0f)) return;
        state = next;
        if (at >= 0f && fade <= 0f) animator.Play(next, 0, at);
        else animator.CrossFadeInFixedTime(next, fade, 0, at >= 0f ? at : 0f);
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
