using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The horde that chases you from the office to the bridge (it replaced the blood orb). Begin() when you drive
// off with the keys (CarInteract.onEnter); Stop() from the StoryTrigger at the bridge; ResetHorde() from the
// Escape checkpoint.
// It opens on a short letterboxed cutscene - the damned pouring down the street behind the car - then it's a
// running fight: packs come up the road behind you and burst out of the side streets ahead. They sprint (no
// navmesh: each one runs the road's own points, then straight at the car once it's close). The ones that reach
// the car grab on and tear at it (CarHealth) until you swerve hard enough to throw them off; stop, or wreck the
// car, and they drag you down. At the bridge they stop dead, as if something out there frightens them.
public class HordeChase : MonoBehaviour
{
    public Transform car;
    public Transform playerRoot;
    public PlayerHealth health;
    public CarHealth carHealth;
    public GameObject zombiePrefab;          // PrimeZombie: its ZombieAI + NavMeshAgent are stripped off
    public Transform[] road;                 // the road points from the office to the bridge, in order
    public Transform bridgeLine;             // they won't come past this (the bridge trigger); +z = onto the bridge

    [Header("The horde")]
    public int maxAlive = 170;               // the most at once when the frame rate allows it
    public int minAlive = 70;                // never fewer than this, however slow the machine
    public float targetFps = 45f;            // below this the cap comes down, well above it goes back up
    public float spawnInterval = 0.45f;      // one pack every this many seconds
    public Vector2Int packSize = new Vector2Int(6, 12);
    public Vector2 runSpeed = new Vector2(0.7f, 1.4f);  // m/s: walkers, a lurching shamble (the van is never outrun by them)
    public Vector2 crawlSpeed = new Vector2(0.45f, 0.9f);
    [Range(0f, 1f)] public float crawlerChance = 0.3f;
    public float noticeDistance = 30f;       // further than this they sway on the spot; closer, they lurch at you
    public float chaseFog = 0.012f;          // fog density while the chase runs (the city's own is too thick to drive in)
    public bool playIntro = false;           // the horde's own cutscene (off: Kessler bursting out of the office opens the escape)
    public float prefillDistance = 260f;     // at the start, the road this far ahead is already full of them
    public Vector2 prefillSpacing = new Vector2(9f, 16f);   // metres between groups along it
    public Vector2Int prefillGroup = new Vector2Int(2, 5);  // how many in each group
    public float roadSpread = 7f;            // how far either side of the road line they run (road and pavements)
    public float behindDistance = 40f;       // packs behind the car come up from this far back
    public Vector2 sideDistance = new Vector2(10f, 26f);   // side-street packs: this far off the road, ahead of you
    public Vector2 aheadDistance = new Vector2(55f, 85f);  // head-on packs: this far up the road, in the fog
    [Range(0f, 1f)] public float aheadChance = 0.35f;
    [Range(0f, 1f)] public float behindChance = 0.3f;     // the rest come out of side streets
    public float despawnDistance = 95f;

    [Header("Grabbing the car")]
    public int maxClingers = 6;
    public float grabDistance = 2.4f;
    public float clingDamage = 3.5f;         // car health per second, per zombie hanging on
    public float swarmDamage = 15f;          // per second when the car is stopped in the middle of them
    public float shakeYawRate = 55f;         // deg/s of swerve that starts throwing them off
    public float shakeTime = 0.35f;          // ...held this long
    public float shedSpeed = 10f;            // m/s: get the van up to this and they can't hold on
    public float shedEvery = 0.6f;           // ...one lets go this often while you stay that fast
    [TextArea] public string clingLine = "They're hanging on to the van!|Floor it, or swerve hard-- shake them off!";
    private float shedTimer;
    private bool clingHinted;
    public float onFootDamage = 14f;
    public float onFootReach = 1.5f;

    [Header("Intro cutscene")]
    public Camera cutsceneCam;
    public RectTransform barTop, barBottom;
    public float barHeight = 90f;
    public Behaviour carControl;
    public ScreenFader fader;
    [TextArea] public string introLine = "Oh God. The whole city's coming--|They heard the engine. ALL of them heard it.";
    public string driveLine = "DRIVE!";
    [TextArea] public string stopLine = "They stopped... they won't set foot on the bridge.|What scares the damned?";
    public string musicArea = "Escape";
    public float musicFadeIn = 2f;
    public float musicFadeOut = 5f;

    [Header("Sound")]
    public AudioSource[] crowd;              // looping moans from the pack (made if empty)

    class Runner
    {
        public Zombie z;
        public Transform t;
        public Animator anim;
        public float speed, side, groundY, nextGround, attackAt;
        public int seg;
        public bool clinging, held, crawler;
        public Vector3 clingLocal;
        public Vector3 holdAt;
    }

    readonly List<Runner> runners = new List<Runner>();
    readonly RaycastHit[] hits = new RaycastHit[8];
    GameObject template;
    bool running, intro, stopped;
    float spawnTimer, lastYaw, shaking, carSpeed;
    int carSeg;
    Vector3 lastCar;
    float fog0 = -1f;
    float fpsSmooth = 60f, cap;

    public bool Running => running;

    // ---------------------------------------------------------------- start

    public void Begin()
    {
        if (running || stopped) return;   // (getting back in the car at the bridge doesn't start it again)
        running = true;
        stopped = false;
        gameObject.SetActive(true);
        MakeTemplate();
        MakeCrowd();
        lastCar = Flat(car.position);
        lastYaw = car.eulerAngles.y;
        carSeg = NearestSeg(car.position);
        cap = maxAlive;
        if (fog0 < 0f) fog0 = RenderSettings.fogDensity;
        RenderSettings.fogDensity = Mathf.Min(fog0, chaseFog);
        if (playIntro) StartCoroutine(Intro());
        else Prefill();
    }

    void MakeTemplate()
    {
        if (template != null) return;
        // made under a switched-off parent so its NavMeshAgent never wakes up off the navmesh
        var hold = new GameObject("hold"); hold.SetActive(false);
        template = Instantiate(zombiePrefab, new Vector3(0f, -500f, 0f), Quaternion.identity, hold.transform);
        template.SetActive(false);
        template.transform.SetParent(transform, true);
        Destroy(hold);
        template.name = "Runner (template)";
        var ai = template.GetComponent<ZombieAI>(); if (ai != null) DestroyImmediate(ai);
        var ag = template.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag != null) DestroyImmediate(ag);
        // a crowd this size: no animating off screen, cheaper skinning, no shadows
        foreach (var a in template.GetComponentsInChildren<Animator>(true)) a.cullingMode = AnimatorCullingMode.CullCompletely;
        foreach (var r in template.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            r.quality = SkinQuality.Bone2;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.skinnedMotionVectors = false;
        }
    }

    void MakeCrowd()
    {
        if (crowd != null && crowd.Length > 0) return;
        var moans = Sounds.Clips("Hell City/Zombie moans", null);
        if (moans == null || moans.Length == 0) return;
        crowd = new AudioSource[3];
        for (int i = 0; i < crowd.Length; i++)
        {
            var s = new GameObject("Crowd " + i).AddComponent<AudioSource>();
            s.transform.SetParent(transform, false);
            s.clip = moans[i % moans.Length];
            s.loop = true;
            s.pitch = 0.62f + 0.12f * i;
            s.spatialBlend = 0.6f;
            s.volume = 0f;
            s.maxDistance = 80f;
            crowd[i] = s;
        }
    }

    IEnumerator Intro()
    {
        intro = true;
        bool hadControl = carControl != null && carControl.enabled;
        if (carControl != null) carControl.enabled = false;
        var carRb = car.GetComponent<Rigidbody>();
        if (carRb != null) { carRb.linearVelocity = Vector3.zero; carRb.angularVelocity = Vector3.zero; carRb.isKinematic = true; }
        MusicManager.Stop(1f);

        // the first wave: a wall of them down the road behind you
        Vector3 back = RoadBack(car.position, 70f, out int backSeg);
        for (int i = 0; i < 60; i++) Spawn(back + Random.insideUnitSphere.WithY(0f) * 11f - Dir(backSeg) * Random.Range(0f, 30f), backSeg, 0.8f);
        foreach (var s in crowd ?? new AudioSource[0]) if (s != null) { s.time = Random.Range(0f, s.clip.length * 0.8f); s.Play(); }

        if (fader != null) yield return fader.FadeTo(1f, 0.3f);
        if (cutsceneCam != null) cutsceneCam.enabled = true;
        SetBars(1f);
        RenderSettings.fogDensity = Mathf.Min(fog0, 0.006f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(introLine);

        Vector3 c = car.position;
        Vector3 toThem = Flat(back - c).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, toThem);

        // shot 1: low beside the car, looking back up the street: they come round the corner
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 0.35f));
        Vector3 s1 = SafeCam(c + Vector3.up * 1.5f, c + side * 3f - toThem * 1f + Vector3.up * 1f);
        for (float t = 0f; t < 3f; t += Time.deltaTime)
        {
            Place(s1 + Random.insideUnitSphere * 0.02f, Centroid() + Vector3.up * 1.2f);
            yield return null;
        }
        Screams(4);

        // shot 2: down among them, the pack running straight at the camera
        for (float t = 0f; t < 2.6f; t += Time.deltaTime)
        {
            Vector3 front = Front();
            Vector3 cam = front + Flat(c - front).normalized * 7f + side * 1.5f + Vector3.up * 1.1f;
            Place(cam, front + Vector3.up * 1.4f);
            yield return null;
        }

        // shot 3: behind the car, them filling the road beyond it
        Vector3 s3 = SafeCam(c + Vector3.up * 1.5f, c - toThem * 5.5f + side * 0.8f + Vector3.up * 1.8f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(driveLine);
        for (float t = 0f; t < 1.8f; t += Time.deltaTime)
        {
            Place(s3, Centroid() + Vector3.up * 1f);
            yield return null;
        }

        if (fader != null) yield return fader.FadeTo(1f, 0.3f);
        if (cutsceneCam != null) cutsceneCam.enabled = false;
        SetBars(0f);
        RenderSettings.fogDensity = Mathf.Min(fog0, chaseFog);
        if (carRb != null) carRb.isKinematic = false;
        if (carControl != null && hadControl) carControl.enabled = true;
        MusicManager.Area(musicArea, musicFadeIn);
        foreach (var r in runners) r.speed = Random.Range(runSpeed.x, runSpeed.y);   // off the leash
        if (fader != null) yield return fader.FadeTo(0f, 0.5f);
        intro = false;
    }

    // ---------------------------------------------------------------- spawning

    // the streets ahead already full of them when you set off: clusters every few metres, across the whole road
    void Prefill()
    {
        foreach (var s in crowd ?? new AudioSource[0]) if (s != null) { s.time = Random.Range(0f, s.clip.length * 0.8f); s.Play(); }
        for (float d = 35f; d < prefillDistance; d += Random.Range(prefillSpacing.x, prefillSpacing.y))
        {
            Vector3 p = RoadAhead(car.position, d, out int sg);
            if (bridgeLine != null && bridgeLine.InverseTransformPoint(p).z > -8f) break;
            Vector3 street = Vector3.Cross(Vector3.up, Dir(sg + 1));
            int n = Random.Range(prefillGroup.x, prefillGroup.y + 1);
            for (int i = 0; i < n; i++) Spawn(p + street * Random.Range(-roadSpread, roadSpread) + Dir(sg + 1) * Random.Range(-4f, 4f), sg + 1);
        }
    }

    void Spawn(Vector3 at, int seg, float speedScale = 1f)
    {
        if (runners.Count >= Mathf.Max(minAlive, (int)cap)) return;
        at.y = Ground(at, at.y + 3f);
        var g = Instantiate(template, at, Quaternion.LookRotation(Flat(car.position - at)), transform);
        g.name = "Runner";
        g.SetActive(true);
        var r = new Runner
        {
            z = g.GetComponent<Zombie>(),
            t = g.transform,
            anim = g.GetComponentInChildren<Animator>(),
            crawler = Random.value < crawlerChance,
            side = Random.Range(-roadSpread, roadSpread),
            seg = Mathf.Clamp(seg, 0, road.Length - 1),
            groundY = at.y,
            nextGround = Time.time + Random.value * 0.3f,
        };
        r.speed = (r.crawler ? Random.Range(crawlSpeed.x, crawlSpeed.y) : Random.Range(runSpeed.x, runSpeed.y)) * speedScale;
        if (r.crawler && r.z.mainCollider is CapsuleCollider low)
        {
            low.direction = 2; low.height = 1.7f; low.center = new Vector3(0f, 0.3f, 0.1f); low.radius = 0.3f;   // low to the ground
        }
        if (r.anim != null)
        {
            r.anim.applyRootMotion = false;
            r.anim.Play(r.crawler ? "Crawl" : "Upright", 0, Random.value);
            r.anim.SetFloat("Speed", 0f);
        }
        runners.Add(r);
    }

    void SpawnPack()
    {
        int n = Random.Range(packSize.x, packSize.y + 1);
        float roll = Random.value;
        if (roll < aheadChance && carSeg < road.Length - 2)
        {
            // up the road ahead, out of the fog, coming straight at you: spread across the whole street
            Vector3 p = RoadAhead(car.position, Random.Range(aheadDistance.x, aheadDistance.y), out int s);
            if (bridgeLine != null && bridgeLine.InverseTransformPoint(p).z > -8f) return;   // never on the bridge
            Vector3 street = Vector3.Cross(Vector3.up, Dir(s));
            for (int i = 0; i < n; i++) Spawn(p + street * Random.Range(-roadSpread, roadSpread) + Dir(s) * Random.Range(-6f, 6f), s + 1);
            if (Random.value < 0.4f) Screams(1);
            return;
        }
        if (roll < aheadChance + behindChance)
        {
            // up the road behind you
            Vector3 p = RoadBack(car.position, behindDistance + Random.Range(0f, 20f), out int s);
            for (int i = 0; i < n; i++) Spawn(p + Random.insideUnitSphere.WithY(0f) * 5f, s);
            return;
        }
        // out of a side street ahead: off the road, with a clear run to it
        int ahead = Mathf.Min(road.Length - 1, carSeg + Random.Range(1, 3));
        Vector3 roadAt = road[ahead].position;
        Vector3 across = Vector3.Cross(Vector3.up, Dir(ahead)) * (Random.value < 0.5f ? 1f : -1f);
        for (int tries = 0; tries < 4; tries++)
        {
            Vector3 p = roadAt + across * Random.Range(sideDistance.x, sideDistance.y) + Dir(ahead) * Random.Range(-8f, 8f);
            if (!UnityEngine.AI.NavMesh.SamplePosition(p, out var nh, 4f, UnityEngine.AI.NavMesh.AllAreas)) { across = -across; continue; }
            if (Physics.Linecast(nh.position + Vector3.up * 1.2f, roadAt + Vector3.up * 1.2f, out var block, ~0, QueryTriggerInteraction.Ignore)
                && block.collider.GetComponentInParent<Zombie>() == null && !block.collider.transform.IsChildOf(car)) { across = -across; continue; }
            for (int i = 0; i < n; i++) Spawn(nh.position + Random.insideUnitSphere.WithY(0f) * 3f, ahead);
            Screams(1);
            return;
        }
    }

    // ---------------------------------------------------------------- the chase

    Transform Target() => playerRoot != null && playerRoot.gameObject.activeInHierarchy ? playerRoot : car;

    void Update()
    {
        if (!running && !stopped) return;
        runners.RemoveAll(r => r.z == null || r.t == null);

        Vector3 carFlat = Flat(car.position);
        if (Time.deltaTime > 0f) carSpeed = Mathf.Lerp(carSpeed, Vector3.Distance(carFlat, lastCar) / Time.deltaTime, 5f * Time.deltaTime);
        lastCar = carFlat;
        carSeg = Mathf.Max(carSeg, NearestSeg(car.position));
        Transform target = Target();
        bool onFoot = target != car;

        if (running && !intro)
        {
            spawnTimer -= Time.deltaTime;
            int live = 0; foreach (var r in runners) if (!r.z.IsDead) live++;
            // keep the frame rate: fewer of them on a machine that's struggling
            if (Time.unscaledDeltaTime > 0f) fpsSmooth = Mathf.Lerp(fpsSmooth, 1f / Time.unscaledDeltaTime, 2f * Time.unscaledDeltaTime);
            float rate = fpsSmooth < targetFps - 5f ? -40f : fpsSmooth > targetFps + 12f ? 15f : 0f;
            cap = Mathf.Clamp(cap + rate * Time.unscaledDeltaTime, minAlive, maxAlive);
            if (spawnTimer <= 0f && live < cap) { spawnTimer = spawnInterval * Random.Range(0.7f, 1.3f); SpawnPack(); }
            Swerve();
        }

        int clingers = 0, around = 0;
        foreach (var r in runners) if (r.clinging && !r.z.IsDead) clingers++;

        for (int i = runners.Count - 1; i >= 0; i--)
        {
            var r = runners[i];
            if (r.z.IsDead) { if (r.clinging) Unhook(r); continue; }
            if (r.clinging) { Cling(r); continue; }

            Vector3 to = Flat(target.position - r.t.position);
            float dist = to.magnitude;

            if (!intro && dist > despawnDistance && r.seg < carSeg) { Destroy(r.t.gameObject); runners.RemoveAt(i); continue; }

            if (r.held) { Hold(r); continue; }

            // grab on
            if (!intro && !onFoot && dist < grabDistance && clingers < maxClingers && carSpeed < 4f && !r.crawler)
            {
                Hook(r); clingers++; continue;
            }
            if (!intro && dist < 5f) around++;

            // on foot: close enough to tear at you
            if (onFoot && dist < onFootReach)
            {
                Face(r.t, to, 10f);
                Pose(r, "Attack", 0f);
                if (Time.time >= r.attackAt)
                {
                    r.attackAt = Time.time + Random.Range(0.8f, 1.3f);
                    if (health != null) health.TakeDamage(onFootDamage);
                }
                continue;
            }

            // too far to notice you: swaying where they stand
            if (dist > noticeDistance && !stopped) { Pose(r, r.crawler ? "Crawl" : "Upright", 0f); continue; }

            // where to go: the road's points up to where the car is, then straight at it
            Vector3 goal;
            if (dist < 28f || r.seg > carSeg + 1) goal = target.position;
            else
            {
                goal = road[r.seg].position + Vector3.Cross(Vector3.up, Dir(r.seg)) * r.side;
                if (Flat(goal - r.t.position).magnitude < 5f && r.seg < road.Length - 1) r.seg++;
            }

            // nothing crosses onto the bridge
            if (bridgeLine != null)
            {
                Vector3 local = bridgeLine.InverseTransformPoint(goal);
                if (local.z > -2f) goal = bridgeLine.TransformPoint(new Vector3(Mathf.Clamp(local.x, -15f, 15f), 0f, -4f));
            }

            Vector3 step = Flat(goal - r.t.position);
            float spd = r.speed;
            if (step.magnitude < 0.6f) spd = 0f;
            Vector3 move = step.normalized * spd * Time.deltaTime;
            if (move.sqrMagnitude > step.sqrMagnitude) move = step;
            Vector3 p = r.t.position + move;
            if (Time.time >= r.nextGround) { r.nextGround = Time.time + 0.25f; r.groundY = Ground(p, r.groundY + 2f); }
            p.y = Mathf.Lerp(p.y, r.groundY, 12f * Time.deltaTime);
            r.t.position = p;
            if (step.sqrMagnitude > 0.01f) Face(r.t, step, 4f);
            Pose(r, r.crawler ? "Crawl" : "Upright", spd);
        }

        if (!intro)
        {
            if (!onFoot && carHealth != null)
            {
                float dmg = clingers * clingDamage;
                if (carSpeed < 2.5f && around >= 5) dmg += swarmDamage;   // stopped in the middle of them
                if (dmg > 0f) carHealth.TakeDamage(dmg * Time.deltaTime);
            }
        }
        Crowd();
    }

    // hanging onto the car, clawing at it
    void Hook(Runner r)
    {
        r.clinging = true;
        Vector3 local = car.InverseTransformPoint(r.t.position);
        // round the body (sized from the car's solid box collider): the sides, the back
        Vector3 half = new Vector3(1f, 1f, 2.3f), mid = Vector3.zero;
        foreach (var bc in car.GetComponentsInChildren<BoxCollider>())
            if (!bc.isTrigger) { half = Vector3.Scale(bc.size, bc.transform.lossyScale) * 0.5f; mid = car.InverseTransformPoint(bc.transform.TransformPoint(bc.center)); break; }
        float x = Mathf.Sign(local.x == 0f ? Random.value - 0.5f : local.x) * (half.x + Random.Range(0.05f, 0.2f));
        float z = Mathf.Clamp(local.z, mid.z - half.z + 0.3f, mid.z + half.z - 0.6f);
        if (local.z < mid.z - half.z + 0.4f) { x = Random.Range(-half.x * 0.6f, half.x * 0.6f); z = mid.z - half.z - 0.2f; }
        r.clingLocal = new Vector3(x, 0.15f, z);
        r.t.SetParent(car, true);
        if (r.z.mainCollider != null) r.z.mainCollider.enabled = false;   // don't let the car's sweep run it over
        Screams(1, r.z);
        // the first time: say what's happening and how to get rid of them
        if (!clingHinted && DialogueBox.Instance != null) { clingHinted = true; DialogueBox.Instance.Say(clingLine); }
    }

    void Cling(Runner r)
    {
        r.t.localPosition = Vector3.Lerp(r.t.localPosition, r.clingLocal, 10f * Time.deltaTime);
        Vector3 inward = car.TransformDirection(Mathf.Abs(r.clingLocal.x) < 0.9f ? Vector3.forward : new Vector3(-r.clingLocal.x, 0f, 0f));
        Face(r.t, Flat(inward), 12f);
        Pose(r, "Attack", 0f);
    }

    void Unhook(Runner r)
    {
        r.clinging = false;
        if (r.t != null && r.t.parent == car) r.t.SetParent(transform, true);
    }

    // swerve hard (or hit something) and the ones hanging on fly off
    void Swerve()
    {
        float yaw = car.eulerAngles.y;
        float rate = Time.deltaTime > 0f ? Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)) / Time.deltaTime : 0f;
        lastYaw = yaw;
        shaking = rate > shakeYawRate && carSpeed > 5f ? shaking + Time.deltaTime : Mathf.Max(0f, shaking - Time.deltaTime * 2f);
        // at speed they lose their grip by themselves, one after another
        shedTimer = carSpeed > shedSpeed ? shedTimer + Time.deltaTime : 0f;
        bool shed = shedTimer >= shedEvery;
        if (shed) shedTimer = 0f;
        if (shaking < shakeTime && !shed) return;
        if (shaking >= shakeTime) shaking = 0f;
        // one at a time, the one on the outside of the turn first
        foreach (var r in runners)
            if (r.clinging && !r.z.IsDead)
            {
                Vector3 outward = car.TransformDirection(new Vector3(r.clingLocal.x, 0.4f, 0f)).normalized;
                Unhook(r);
                if (r.z.mainCollider != null) r.z.mainCollider.enabled = true;
                r.z.KillInstant(r.t.position + Vector3.up, (outward + car.forward * 0.3f) * 900f);
                return;
            }
    }

    void Hold(Runner r)
    {
        Vector3 step = Flat(r.holdAt - r.t.position);
        if (step.magnitude > 0.5f)
        {
            r.t.position += step.normalized * Mathf.Min(step.magnitude, r.speed * 0.5f * Time.deltaTime);
            Face(r.t, step, 6f);
            Pose(r, "Upright", r.speed * 0.5f);
        }
        else
        {
            Face(r.t, Flat(Target().position - r.t.position), 3f);
            Pose(r, "Upright", 0f);
        }
    }

    void Pose(Runner r, string state, float speed)
    {
        if (r.anim == null) return;
        var info = r.anim.GetCurrentAnimatorStateInfo(0);
        if (!info.IsName(state) && !r.anim.IsInTransition(0)) r.anim.CrossFadeInFixedTime(state, 0.2f);
        else if (state == "Attack" && info.normalizedTime > 0.95f) r.anim.Play("Attack", 0, 0f);   // keep clawing
        if (state == "Upright" || state == "Crawl")
        {
            float clip = state == "Crawl" ? 0.52f : 0.41f;   // metres the feet cover per second at 1x
            r.anim.SetFloat("Speed", speed < 0.05f ? 0f : clip);
            r.anim.speed = speed < 0.05f ? 1f : Mathf.Clamp(speed / clip, 0.8f, 2.6f);
        }
        else r.anim.speed = 1.2f;
    }

    // ---------------------------------------------------------------- stop / reset

    // at the bridge: they pull up short at its mouth and stand there, watching you go
    public void Stop()
    {
        if (!running) return;
        running = false;
        stopped = true;
        MusicManager.Stop(musicFadeOut);
        if (DialogueBox.Instance != null) DialogueBox.Instance.Say(stopLine);
        foreach (var r in runners)
        {
            if (r.z == null || r.z.IsDead) continue;
            if (r.clinging)
            {
                Unhook(r);
                if (r.z.mainCollider != null) r.z.mainCollider.enabled = true;
                r.z.KillInstant(r.t.position + Vector3.up, (car.right * (r.clingLocal.x > 0 ? 1f : -1f) + Vector3.up * 0.3f) * 700f);
                continue;
            }
            r.held = true;
            Vector3 at = bridgeLine != null ? bridgeLine.TransformPoint(new Vector3(Random.Range(-14f, 14f), 0f, Random.Range(-18f, -5f))) : r.t.position;
            r.holdAt = at;
        }
        Screams(3);
        StartCoroutine(FadeCrowd(4f));
    }

    public void ResetHorde()
    {
        StopAllCoroutines();
        if (intro)
        {
            if (cutsceneCam != null) cutsceneCam.enabled = false;
            SetBars(0f);
            if (carControl != null) carControl.enabled = false;   // CarInteract turns it back on when you get in
            var carRb = car.GetComponent<Rigidbody>();
            if (carRb != null) carRb.isKinematic = false;
            if (fader != null) fader.fade.alpha = 0f;
        }
        if (fog0 > 0f) RenderSettings.fogDensity = fog0;
        intro = false;
        running = false;
        stopped = false;
        foreach (var r in runners) if (r.t != null) Destroy(r.t.gameObject);
        runners.Clear();
        foreach (var s in crowd ?? new AudioSource[0]) if (s != null) s.Stop();
        if (MusicManager.CurrentArea == musicArea) MusicManager.Stop(1f);
    }

    // ---------------------------------------------------------------- helpers

    void Crowd()
    {
        if (crowd == null || crowd.Length == 0) return;
        int near = 0; Vector3 sum = Vector3.zero;
        Vector3 me = Target().position;
        foreach (var r in runners)
            if (!r.z.IsDead && (r.t.position - me).sqrMagnitude < 45f * 45f) { near++; sum += r.t.position; }
        float vol = stopped ? crowd[0].volume : Mathf.Clamp01(near / 25f) * 0.9f;
        for (int i = 0; i < crowd.Length; i++)
        {
            if (crowd[i] == null) continue;
            if (near > 0) crowd[i].transform.position = sum / near;   // the moaning comes from the middle of the pack
            if (!stopped) crowd[i].volume = Mathf.MoveTowards(crowd[i].volume, vol, Time.deltaTime);
        }
    }

    IEnumerator FadeCrowd(float time)
    {
        if (crowd == null) yield break;
        float v0 = crowd.Length > 0 && crowd[0] != null ? crowd[0].volume : 0f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            foreach (var s in crowd) if (s != null) s.volume = v0 * (1f - t / time);
            yield return null;
        }
        foreach (var s in crowd) if (s != null) s.Stop();
    }

    void Screams(int n, Zombie who = null)
    {
        if (who != null) { who.Scream(); return; }
        for (int i = 0; i < n && runners.Count > 0; i++)
        {
            var r = runners[Random.Range(0, runners.Count)];
            if (r.z != null && !r.z.IsDead) r.z.Scream();
        }
    }

    float Ground(Vector3 p, float fromY)
    {
        int n = Physics.RaycastNonAlloc(new Vector3(p.x, fromY, p.z), Vector3.down, hits, 12f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MinValue;
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (c.GetComponentInParent<Zombie>() != null || c.transform.IsChildOf(car)) continue;
            if (hits[i].point.y > best) best = hits[i].point.y;
        }
        return best > float.MinValue ? best : p.y;
    }

    int NearestSeg(Vector3 p)
    {
        int best = 0; float bd = float.MaxValue;
        for (int i = 0; i < road.Length; i++)
        {
            float d = (Flat(road[i].position) - Flat(p)).sqrMagnitude;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    Vector3 Dir(int seg)
    {
        seg = Mathf.Clamp(seg, 0, road.Length - 1);
        Vector3 a = road[Mathf.Max(0, seg - 1)].position, b = road[Mathf.Min(road.Length - 1, Mathf.Max(1, seg))].position;
        return Flat(b - a).normalized;
    }

    // a point this far back along the road from p (or straight back past the first point); seg = the point to run to
    Vector3 RoadBack(Vector3 p, float distance, out int seg)
    {
        int i = NearestSeg(p);
        Vector3 at = road[i].position;
        float left = distance - Flat(p - at).magnitude;
        while (left > 0f && i > 0)
        {
            float d = Flat(road[i].position - road[i - 1].position).magnitude;
            if (d >= left) { seg = i; return Vector3.Lerp(road[i].position, road[i - 1].position, left / d); }
            left -= d; i--;
        }
        seg = Mathf.Min(road.Length - 1, i + 1);
        return road[i].position - Dir(1) * Mathf.Max(0f, left);
    }

    // a point this far on along the road from p; seg = the road point just before it
    Vector3 RoadAhead(Vector3 p, float distance, out int seg)
    {
        int i = NearestSeg(p);
        Vector3 at = Flat(p) + Vector3.up * road[i].position.y;
        while (i < road.Length - 1)
        {
            float d = Flat(road[i + 1].position - at).magnitude;
            if (d >= distance) { seg = i; return at + Flat(road[i + 1].position - at).normalized * distance; }
            distance -= d; at = road[i + 1].position; i++;
        }
        seg = road.Length - 1;
        return road[road.Length - 1].position;
    }

    Vector3 Centroid()
    {
        Vector3 s = Vector3.zero; int n = 0;
        foreach (var r in runners) if (r.t != null) { s += r.t.position; n++; }
        return n > 0 ? s / n : car.position;
    }

    Vector3 Front()
    {
        Vector3 best = car.position; float bd = float.MaxValue;
        foreach (var r in runners)
        {
            if (r.t == null) continue;
            float d = (r.t.position - car.position).sqrMagnitude;
            if (d < bd) { bd = d; best = r.t.position; }
        }
        return best;
    }

    Vector3 SafeCam(Vector3 anchor, Vector3 want)
    {
        Vector3 d = want - anchor;
        float nearest = float.MaxValue;
        foreach (var h in Physics.SphereCastAll(anchor, 0.35f, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(car) || h.distance <= 0f || h.collider.GetComponentInParent<Zombie>() != null) continue;
            if (h.distance < nearest) nearest = h.distance;
        }
        return nearest < float.MaxValue ? anchor + d.normalized * Mathf.Max(0.3f, nearest - 0.3f) : want;
    }

    void Place(Vector3 pos, Vector3 look)
    {
        if (cutsceneCam == null) return;
        cutsceneCam.transform.position = pos;
        if ((look - pos).sqrMagnitude > 0.01f) cutsceneCam.transform.rotation = Quaternion.LookRotation(look - pos);
    }

    void SetBars(float k)
    {
        CutsceneHUD.Hide(k > 0f);
        foreach (var b in new[] { barTop, barBottom })
            if (b != null) b.sizeDelta = new Vector2(b.sizeDelta.x, barHeight * k);
    }

    static void Face(Transform t, Vector3 dir, float rate)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(dir), rate * Time.deltaTime);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}

static class HordeVec
{
    public static Vector3 WithY(this Vector3 v, float y) { v.y = y; return v; }
}
