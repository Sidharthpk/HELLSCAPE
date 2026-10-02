using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Elias, fought on the silo floor. He stalks you and lunges when close, and (user, 2026-10-02):
//   - shoot at him while he's walking and he always DODGES, away from the side the shot comes down (a sidestep, a
//     corkscrew or a roll that way: picked at random), the shot going wide, and then he CHARGES: sidestep it and he
//     runs into the silo wall and is STUNNED; stand there and he takes you off your feet. Running at you, stunned or
//     in the middle of a throw he can't dodge: those are your times to shoot
//   - he leaves a health orb on the floor each time he's lost another quarter of his health
//   - he THROWS KNIVES (three in a fan once he's below half health): step out of the line. The ones that miss stay
//     stuck in the wall; pull one out (F) and throw it back (G): it stuns him wherever he is, mid-charge included
//   - stunned, he's down for stunTime, then gets up (still open to fire while he does)
// Anything he's committed to (a lunge, a throw, a charge) he can't dodge out of, so those are openings too.
// At zero health he drops to his knees instead of dying: SiloEncounter decides what happens next.
// The moves are states on Killer.controller (EliasFightBuilder), cross-faded to by name.
public class KillerBoss : MonoBehaviour
{
    public Transform player;
    public Animator animator;               // humanoid, Killer.controller ("Speed" float, "Attack" trigger, the move states)
    public Transform model;                 // the mesh root, tipped over when kneeling / dead

    [Header("Stats")]
    public float maxHealth = 420f;
    public float walkSpeed = 1.7f;
    public float enragedSpeed = 3.4f;       // below half health
    public float attackRange = 2.1f;
    public float attackDamage = 12f;       // you start the fight on half health
    public float attackCooldown = 1.7f;
    public float lungeDistance = 1.2f;
    public float hitDelay = 0.45f;          // the swing lands this long into the attack animation

    [Header("Arena (silo floor)")]
    public Transform arenaCenter;
    public float arenaRadius = 6f;
    public float wallBeyond = 1.15f;        // the silo wall is this far outside the ring he keeps to

    [Header("Dodging your shots (walking, he always does: user)")]
    [Range(0f, 1f)] public float dodgeChance = 1f;
    public float dodgeDistance = 2.2f;
    public float dodgeCooldown = 0f;        // on his feet again for this long before he can dodge the next burst
    public float aimedWithin = 1.6f;        // a shot passing this close to him counts as aimed at him
    [Range(0f, 1f)] public float chargeAfterDodge = 0.75f;

    [Header("The charge")]
    public float chargeWindup = 0.5f;       // he sets himself: this is when to start moving
    public float chargeSpeed = 7.5f;
    public float chargeDamage = 18f;
    public float chargeHitRadius = 1.15f;
    [Range(0f, 1f)] public float chargeDamageTaken = 1f;     // of your shots' damage while he's running at you (1: all of it)
    public float stunTime = 3f;             // staggering and down, after the wall (or a knife): about 1.2 s of it is the fall
    public float getUpTime = 1.6f;

    [Header("His own moves (a throw or a charge)")]
    public float moveEvery = 3.2f;          // seconds between them (below half health: x0.65)
    [Range(0f, 1f)] public float throwOverCharge = 0.6f;

    [Header("Knives")]
    public GameObject knifeModel;           // the user's knife (EliasFightBuilder hands it over)
    public Material knifeMaterial;
    public Vector3 knifeTip = Vector3.up;   // which way the blade points when the model is dropped into a scene
    public float knifeLength = 0.34f;
    public float knifeSpeed = 17f;
    public float knifeDamage = 10f;
    public float throwRelease = 0.55f;      // seconds into the throw that it leaves his hand
    public float throwTime = 1.5f;          // the whole throw
    public float fanAngle = 13f;            // between the three knives when he's enraged
    public int maxStuck = 5;                // knives left in the wall (the oldest goes)
    public KeyCode throwKey = KeyCode.G;
    public float yourKnifeSpeed = 24f;
    public float yourKnifeDamage = 30f;
    [TextArea] public string knifeLine = "One of his knives. I can throw it back.";

    [Header("Health orbs")]
    public GameObject healthPickup;         // disabled template (the bridge boss's): one is left on the floor each time he loses...
    public int orbs = 3;                    // ...another 1/(orbs+1) of his health

    [Header("UI / Sound")]
    public RectTransform healthFill;        // anchored left, scaled on x
    public GameObject healthBar;
    public AudioSource voice;
    public AudioClip hitClip;
    public UnityEvent onDefeated = new UnityEvent();

    private float health;
    private float cooldown, moveTimer, dodgeReady;
    private bool active, defeated;
    private bool busy, evading, charging, stunned;
    private int held;                       // knives you're carrying
    private bool toldAboutKnives;
    private Camera playerCam;
    private TMPro.TextMeshProUGUI knifeHud;
    private readonly List<GameObject> stuck = new List<GameObject>(), flying = new List<GameObject>();

    public bool Defeated => defeated;
    public bool Stunned => stunned;
    public int KnivesHeld => held;

    static KillerBoss current;

    // GunController, as a shot leaves the barrel (before it lands): his chance to not be there
    public static void ShotFired(Vector3 from, Vector3 dir)
    {
        if (current != null) current.OnShotAt(from, dir);
    }

    void OnEnable() { current = this; }
    void OnDisable() { if (current == this) current = null; }

    void Start()
    {
        health = maxHealth;
        if (player == null) { var p = GameObject.FindGameObjectWithTag("Player"); if (p != null) player = p.transform; }
        if (healthBar != null) healthBar.SetActive(false);
    }

    private Quaternion modelRot0;
    private Vector3 modelPos0;
    private bool posed;

    void Awake()
    {
        Transform m = model != null ? model : transform;
        modelRot0 = m.localRotation;
        modelPos0 = m.localPosition;
        posed = true;
    }

    // a checkpoint respawn on the silo floor: full health, standing, waiting to be activated again
    public void ResetBoss(Vector3 at, Vector3 facing)
    {
        StopAllCoroutines();
        active = false;
        defeated = false;
        busy = evading = charging = stunned = false;
        if (downBody != null) downBody.enabled = false;
        health = maxHealth;
        cooldown = 0f;
        held = 0;
        ClearKnives(true);
        foreach (var o in dropped) if (o != null) Destroy(o);
        dropped.Clear();
        ShowKnifeHud();
        if (healthFill != null) healthFill.localScale = Vector3.one;
        if (healthBar != null) healthBar.SetActive(false);
        Transform m = model != null ? model : transform;
        if (posed) { m.localRotation = modelRot0; m.localPosition = modelPos0; }
        if (animator != null) { animator.enabled = true; animator.Rebind(); }
        facing.y = 0f;
        transform.SetPositionAndRotation(at, facing.sqrMagnitude > 0.001f ? Quaternion.LookRotation(facing) : transform.rotation);
        gameObject.SetActive(true);
    }

    public void Activate()
    {
        if (defeated) return;
        active = true;
        moveTimer = moveEvery * 0.6f;
        if (healthBar != null) healthBar.SetActive(true);
    }

    void Update()
    {
        if (!active || player == null) return;   // ChaseKiller animates him before the fight

        if (held > 0 && Input.GetKeyDown(throwKey) && Time.timeScale > 0f) StartCoroutine(ThrowBack());
        if (busy) return;

        Vector3 to = Flat(player.position - transform.position);
        float dist = to.magnitude;
        Face(to, 5f);

        cooldown -= Time.deltaTime;
        moveTimer -= Time.deltaTime;
        bool enraged = health < maxHealth * 0.5f;
        if (moveTimer <= 0f)
        {
            // a throw or a charge, from wherever he is: too close for either, he hops back first and throws
            moveTimer = moveEvery * (enraged ? 0.65f : 1f) * Random.Range(0.8f, 1.25f);
            bool close = dist < 3f;
            StartCoroutine(close || Random.value < throwOverCharge ? Throw(enraged ? 3 : 1, close) : Charge());
            return;
        }
        if (dist <= attackRange)
        {
            SetSpeed(0f);
            if (cooldown <= 0f) StartCoroutine(Lunge());
            return;
        }

        float speed = enraged ? enragedSpeed : walkSpeed;
        transform.position = InArena(transform.position + to.normalized * speed * Time.deltaTime);
        SetSpeed(speed);
    }

    IEnumerator Lunge()
    {
        busy = true;
        cooldown = attackCooldown;
        Attack(animator);
        yield return new WaitForSeconds(Mathf.Max(0f, hitDelay - 0.18f));
        Vector3 start = transform.position;
        Vector3 end = InArena(start + transform.forward * lungeDistance);
        for (float t = 0f; t < 0.18f; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(start, end, t / 0.18f);
            yield return null;
        }
        Vector3 to = player.position - transform.position; to.y = 0f;
        if (!defeated && to.magnitude <= attackRange + 0.4f)
        {
            var ph = player.GetComponentInParent<PlayerHealth>();
            if (ph != null) ph.TakeDamage(attackDamage);
        }
        yield return new WaitForSeconds(0.25f);
        busy = false;
    }

    // ---------------------------------------------------------------- dodge, charge, stun

    void OnShotAt(Vector3 from, Vector3 dir)
    {
        if (!active || defeated || busy || Time.time < dodgeReady || Random.value > dodgeChance) return;
        // is it coming anywhere near him?
        Vector3 chest = transform.position + Vector3.up * 1.1f, rel = chest - from;
        float along = Vector3.Dot(rel, dir);
        Vector3 miss = dir * along - rel;   // from his chest to where the shot passes him
        if (along < 0f || miss.magnitude > aimedWithin) return;
        // he goes the other way from the side it's coming down (dead centre: either)
        float across = Vector3.Dot(miss, Vector3.Cross(Vector3.up, Flat(player.position - transform.position).normalized));
        StartCoroutine(Dodge(Mathf.Abs(across) < 0.08f ? 0f : -Mathf.Sign(across)));
    }

    static readonly string[] DodgeStates = { "DodgeLeft", "DodgeRight", "DodgeBack", "Corkscrew", "RollToRun" };
    // how long each takes as EliasFightBuilder plays it (clip length / state speed): the move, and how long shots go wide
    static readonly float[] DodgeTimes = { 0.72f, 0.72f, 0.9f, 1.02f, 0.8f };

    // side: +1 = to his right (as he faces you), -1 = to his left, 0 = whichever
    IEnumerator Dodge(float side)
    {
        busy = true;
        evading = true;
        Vector3 fwd = Flat(player.position - transform.position).normalized;
        if (fwd == Vector3.zero) fwd = transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        if (side == 0f) side = Random.value < 0.5f ? 1f : -1f;
        // away from the shot: the plain sidestep that way, the corkscrew or the roll
        Vector3 Step(int which, float s)
        {
            switch (which)
            {
                case 0: return -right;
                case 1: return right;
                case 2: return -fwd * 0.8f;
                case 3: return right * s * 1.2f;
                default: return right * s * 0.85f + fwd * 0.45f;   // the roll comes in at an angle
            }
        }
        int[] ways = { side > 0f ? 1 : 0, 3, 4 };
        int pick = ways[Random.Range(0, ways.Length)];
        // (the wall's in the way on that side: he ducks back instead, and if that's wall too, the other side)
        Vector3 from = transform.position, move = Step(pick, side) * dodgeDistance;
        bool Room(Vector3 step) => Flat(InArena(from + step) - from).magnitude >= step.magnitude * 0.6f;
        if (!Room(move))
        {
            pick = 2;
            move = Step(pick, side) * dodgeDistance;
            if (!Room(move)) { side = -side; pick = side > 0f ? 1 : 0; move = Step(pick, side) * dodgeDistance; }
        }
        Vector3 end = InArena(from + move);
        Go(DodgeStates[pick], 0.05f);
        float dodgeTime = DodgeTimes[pick];
        for (float t = 0f; t < dodgeTime; t += Time.deltaTime)
        {
            float k = t / dodgeTime;
            transform.position = Vector3.Lerp(from, end, 1f - (1f - k) * (1f - k));   // quick off the mark
            Face(Flat(player.position - transform.position), 9f);
            yield return null;
        }
        transform.position = end;
        evading = false;
        dodgeReady = Time.time + dodgeCooldown;

        // (the roll comes up running: straight into the charge)
        if (pick == 4 || Random.value < chargeAfterDodge) { yield return Charge(pick == 4 ? 0.12f : chargeWindup); yield break; }
        Go("Locomotion", 0.15f);
        yield return new WaitForSeconds(0.2f);
        busy = false;
    }

    // straight at where you're standing when he sets off: step aside and it's the wall that stops him
    IEnumerator Charge(float windup = -1f)
    {
        busy = true;
        SetSpeed(0f);
        if (windup < 0f) { windup = chargeWindup; Go("Locomotion", 0.15f); }
        for (float t = 0f; t < windup; t += Time.deltaTime)
        {
            Face(Flat(player.position - transform.position), 10f);
            yield return null;
        }
        Vector3 dir = Flat(player.position - transform.position).normalized;
        if (dir == Vector3.zero) dir = transform.forward;
        transform.rotation = Quaternion.LookRotation(dir);
        charging = true;
        Go("Charge", 0.08f);
        bool hit = false, wall = false;
        for (float t = 0f; t < 4f && !wall; t += Time.deltaTime)
        {
            Vector3 next = transform.position + dir * chargeSpeed * Time.deltaTime;
            Vector3 kept = InArena(next);
            wall = Flat(kept - next).sqrMagnitude > 0.0001f;
            transform.position = kept;
            if (!hit && Flat(player.position - transform.position).magnitude < chargeHitRadius)
            {
                hit = true;
                var ph = player.GetComponentInParent<PlayerHealth>();
                if (ph != null) ph.TakeDamage(chargeDamage);
                var body = player.GetComponent<Rigidbody>();
                if (body != null && !body.isKinematic) body.AddForce(dir * 7f + Vector3.up * 2.5f, ForceMode.VelocityChange);
                break;   // he's got you: he pulls up
            }
            yield return null;
        }
        charging = false;
        if (wall && !hit)
        {
            Say(ProceduralAudio.Thud(), 1f);
            BloodFx.Spray(transform.position + Vector3.up * 1.5f, -dir, 10, 2.5f);
            yield return Stun();
            yield break;
        }
        Go("Locomotion", 0.2f);
        yield return new WaitForSeconds(0.45f);
        moveTimer = Mathf.Max(moveTimer, 1.2f);
        busy = false;
    }

    // he staggers and goes down flat (wall or knife), lies there, then gets back on his feet; open to fire the
    // whole time. Lying down he's nowhere near his standing collider, so a box follows his body for your shots.
    IEnumerator Stun()
    {
        busy = true;
        stunned = true;
        evading = charging = false;
        SetSpeed(0f);
        Go("Stunned", 0.1f);
        Say(Sounds.Clip("Silo Fight/Killer hurt", hitClip), 1f);
        if (downBody == null) downBody = gameObject.AddComponent<BoxCollider>();
        downBody.enabled = true;
        for (float t = 0f; t < stunTime + getUpTime; t += Time.deltaTime)
        {
            if (t >= stunTime && t - Time.deltaTime < stunTime) Go("GetUp", 0.15f);
            FitDownBody();
            yield return null;
        }
        downBody.enabled = false;
        stunned = false;
        Go("Locomotion", 0.2f);
        yield return new WaitForSeconds(0.2f);
        moveTimer = 1f;
        busy = false;
    }

    BoxCollider downBody;
    static readonly HumanBodyBones[] BodyEnds = { HumanBodyBones.Head, HumanBodyBones.Hips, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftHand, HumanBodyBones.RightHand };

    void FitDownBody()
    {
        if (animator == null || !animator.isHuman) { downBody.center = Vector3.up * 0.5f; downBody.size = new Vector3(2f, 1f, 2f); return; }
        Bounds b = new Bounds(transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Hips).position), Vector3.zero);
        foreach (var bone in BodyEnds) { var t = animator.GetBoneTransform(bone); if (t != null) b.Encapsulate(transform.InverseTransformPoint(t.position)); }
        b.Expand(0.5f);
        downBody.center = b.center;
        downBody.size = b.size;
    }

    // ---------------------------------------------------------------- knives

    IEnumerator Throw(int count, bool hopBack = false)
    {
        busy = true;
        SetSpeed(0f);
        if (hopBack)
        {
            // room to throw: a step back out of your reach
            Vector3 from0 = transform.position, away = -Flat(player.position - from0).normalized;
            Vector3 to0 = InArena(from0 + away * dodgeDistance);
            if (Flat(to0 - from0).magnitude < dodgeDistance * 0.6f) to0 = InArena(from0 + Vector3.Cross(Vector3.up, away) * dodgeDistance);   // back to the wall: sideways
            Go("DodgeBack", 0.05f);
            for (float t = 0f; t < DodgeTimes[2]; t += Time.deltaTime)
            {
                float k = t / DodgeTimes[2];
                transform.position = Vector3.Lerp(from0, to0, 1f - (1f - k) * (1f - k));
                Face(Flat(player.position - transform.position), 9f);
                yield return null;
            }
        }
        Go("Throw", 0.1f);
        Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        GameObject inHand = hand != null ? MakeKnife() : null;
        if (inHand != null) { inHand.transform.SetParent(hand, false); inHand.transform.localPosition = Vector3.zero; inHand.transform.localRotation = Quaternion.identity; inHand.transform.localScale = Vector3.one / Mathf.Max(0.001f, hand.lossyScale.x); }
        for (float t = 0f; t < throwRelease; t += Time.deltaTime)
        {
            Face(Flat(player.position - transform.position), 8f);
            yield return null;
        }
        Vector3 from = hand != null ? hand.position : transform.position + Vector3.up * 1.5f + transform.forward * 0.4f;
        if (inHand != null) Destroy(inHand);
        Vector3 aim = (Target() - from).normalized;
        for (int i = 0; i < count; i++)
            StartCoroutine(Fly(from, Quaternion.AngleAxis((i - (count - 1) * 0.5f) * fanAngle, Vector3.up) * aim, false));
        Say(ProceduralAudio.Click(), 0.7f);
        yield return new WaitForSeconds(Mathf.Max(0f, throwTime - throwRelease));
        Go("Locomotion", 0.2f);
        busy = false;
    }

    // your chest, roughly: where his knives are thrown at
    Vector3 Target()
    {
        if (playerCam == null) playerCam = player.GetComponentInChildren<Camera>();
        return playerCam != null ? playerCam.transform.position - Vector3.up * 0.3f : player.position + Vector3.up * 0.6f;
    }

    // you, throwing one back: it stuns him if it finds him (he can't dodge these), and is there to pull out again if it doesn't
    IEnumerator ThrowBack()
    {
        held--;
        ShowKnifeHud();
        if (playerCam == null) playerCam = player.GetComponentInChildren<Camera>();
        Transform eye = playerCam != null ? playerCam.transform : player;
        yield return Fly(eye.position + eye.forward * 0.5f - eye.up * 0.15f, eye.forward, true);
    }

    IEnumerator Fly(Vector3 from, Vector3 dir, bool yours)
    {
        var knife = MakeKnife();
        if (knife == null) yield break;
        flying.Add(knife);
        Transform k = knife.transform, blade = k.GetChild(0);
        Quaternion blade0 = blade.localRotation;
        k.SetPositionAndRotation(from, Quaternion.LookRotation(dir));
        float speed = yours ? yourKnifeSpeed : knifeSpeed, spun = 0f;
        Vector3 stickAt = Vector3.zero; bool landed = false;
        for (float t = 0f; t < 4f && knife != null && !landed; t += Time.deltaTime)
        {
            Vector3 p = k.position, step = dir * speed * Time.deltaTime, next = p + step;
            spun += 900f * Time.deltaTime;
            blade.localRotation = Quaternion.Euler(spun, 0f, 0f) * blade0;

            if (yours)
            {
                // anywhere on him, head to foot
                Vector3 feet = transform.position, rel = next - feet;
                if (!defeated && gameObject.activeInHierarchy && Flat(rel).magnitude < 0.7f && rel.y > -0.2f && rel.y < 2.2f)
                {
                    TakeDamage(yourKnifeDamage, next, dir);
                    if (!defeated && !stunned) { StopMoves(); StartCoroutine(Stun()); }
                    flying.Remove(knife); Destroy(knife);
                    yield break;
                }
            }
            else
            {
                Vector3 you = Target();
                if (Flat(next - you).magnitude < 0.5f && Mathf.Abs(next.y - you.y) < 0.9f)
                {
                    var ph = player.GetComponentInParent<PlayerHealth>();
                    if (ph != null) ph.TakeDamage(knifeDamage);
                    flying.Remove(knife); Destroy(knife);
                    yield break;
                }
            }

            // the wall (what's really there, or failing that the ring just outside his arena), or the floor
            foreach (var h in Physics.RaycastAll(p, dir, step.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(transform) || h.collider.transform.IsChildOf(player.root) || h.collider.GetComponentInParent<Interactable>() != null) continue;
                if (!landed || (h.point - p).sqrMagnitude < (stickAt - p).sqrMagnitude) { landed = true; stickAt = h.point; }
            }
            if (!landed && arenaCenter != null)
            {
                if (Flat(next - arenaCenter.position).magnitude > arenaRadius + wallBeyond) { landed = true; stickAt = next; }
                else if (next.y < arenaCenter.position.y + 0.03f) { landed = true; stickAt = next; }
            }
            k.position = landed ? stickAt : next;
            yield return null;
        }
        if (knife == null) yield break;
        flying.Remove(knife);
        if (!landed) { Destroy(knife); yield break; }

        // in the wall to half its blade, there to be pulled out
        blade.localRotation = blade0;
        k.position = stickAt - dir * knifeLength * 0.3f;
        Say(ProceduralAudio.Click(), 0.5f);
        var grip = knife.AddComponent<BoxCollider>();
        grip.size = new Vector3(0.3f, 0.3f, knifeLength + 0.2f);
        var take = knife.AddComponent<Interactable>();
        take.prompt = "Pull the knife out";
        take.hideOnUse = true;
        take.onUse.AddListener(() => PickUp(knife));
        stuck.Add(knife);
        while (stuck.Count > maxStuck) { if (stuck[0] != null) Destroy(stuck[0]); stuck.RemoveAt(0); }
    }

    void PickUp(GameObject knife)
    {
        stuck.Remove(knife);
        Destroy(knife);
        held++;
        ShowKnifeHud();
        if (!toldAboutKnives && DialogueBox.Instance != null && !string.IsNullOrEmpty(knifeLine)) { toldAboutKnives = true; DialogueBox.Instance.Say(knifeLine); }
    }

    // a knife pointing down its own +z, the right size, whatever the model's own axes and scale are
    GameObject MakeKnife()
    {
        if (knifeModel == null) return null;
        var root = new GameObject("Elias's knife");
        var body = Instantiate(knifeModel, root.transform);
        foreach (var c in body.GetComponentsInChildren<Collider>()) Destroy(c);
        var rs = body.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) { Destroy(root); return null; }
        if (knifeMaterial != null) foreach (var r in rs) r.sharedMaterial = knifeMaterial;
        body.transform.rotation = Quaternion.FromToRotation(knifeTip.normalized, Vector3.forward) * body.transform.rotation;
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        float scale = knifeLength / Mathf.Max(0.0001f, Mathf.Max(b.size.x, b.size.y, b.size.z));
        body.transform.localScale *= scale;
        body.transform.localPosition = -b.center * scale;   // (root is at the origin here: its middle on the pivot)
        return root;
    }

    void ClearKnives(bool stuckToo)
    {
        foreach (var k in flying) if (k != null) Destroy(k);
        flying.Clear();
        if (!stuckToo) return;
        foreach (var k in stuck) if (k != null) Destroy(k);
        stuck.Clear();
    }

    // "[G] KNIFE x2" over the ammo count (a copy of it, so it's in the HUD's own font)
    void ShowKnifeHud()
    {
        if (knifeHud == null)
        {
            if (held <= 0) return;
            var gun = FindFirstObjectByType<GunController>(FindObjectsInactive.Include);
            if (gun == null || gun.ammoText == null) return;
            knifeHud = Instantiate(gun.ammoText, gun.ammoText.transform.parent);
            knifeHud.name = "KnifeCount";
            knifeHud.rectTransform.anchoredPosition += Vector2.up * (gun.ammoText.rectTransform.rect.height + 8f);
        }
        knifeHud.gameObject.SetActive(held > 0 && !defeated);
        knifeHud.text = "[" + throwKey + "] KNIFE x" + held;
    }

    // ---------------------------------------------------------------- damage

    public void TakeDamage(float dmg, Vector3 point, Vector3 dir)
    {
        if (defeated || evading) return;   // (evading: that's the shot he just got out of the way of)
        if (charging) dmg *= chargeDamageTaken;
        BloodFx.Spray(point, -dir, 18, 3f);
        var grunt = Sounds.Clip("Silo Fight/Killer hurt", hitClip);
        if (voice != null && grunt != null && !voice.isPlaying) voice.PlayOneShot(grunt, Sounds.Volume("Silo Fight/Killer hurt"));

        // shooting him wakes him up even if the scripted beat hasn't yet
        if (!active) Activate();

        health -= dmg;
        if (healthFill != null) healthFill.localScale = new Vector3(Mathf.Clamp01(health / maxHealth), 1f, 1f);
        while (health > 0f && dropped.Count < orbs && health <= maxHealth * (1f - (dropped.Count + 1f) / (orbs + 1f))) DropOrb();
        if (health > 0f) return;

        defeated = true;
        active = false;
        StopMoves();
        ClearKnives(false);
        ShowKnifeHud();
        SetSpeed(0f);
        if (healthBar != null) healthBar.SetActive(false);
        StartCoroutine(Tip(25f, -0.55f, 0.8f));   // drops to his knees
        onDefeated.Invoke();
    }

    readonly List<GameObject> dropped = new List<GameObject>();

    // a health orb on the silo floor, somewhere away from him
    void DropOrb()
    {
        GameObject orb = null;
        if (healthPickup != null && arenaCenter != null)
        {
            Vector3 p = arenaCenter.position;
            for (int i = 0; i < 12; i++)
            {
                Vector2 r = Random.insideUnitCircle * (arenaRadius - 1f);
                p = arenaCenter.position + new Vector3(r.x, 0f, r.y);
                if (Flat(p - transform.position).magnitude > 3f) break;
            }
            orb = Instantiate(healthPickup, p + Vector3.up * 0.6f, Quaternion.identity);
            var hp = orb.GetComponent<HealthPickup>();
            if (hp != null) hp.line = "";   // (the template's line belongs to the bridge)
            orb.SetActive(true);
        }
        dropped.Add(orb);   // (counted even with no template, so the thresholds move on)
    }

    // whatever he was in the middle of: over
    void StopMoves()
    {
        StopAllCoroutines();
        busy = evading = charging = stunned = false;
        if (downBody != null) downBody.enabled = false;
        ClearKnives(false);
        if (animator != null) foreach (var t in animator.GetComponentsInChildren<Transform>()) if (t.name == "Elias's knife") Destroy(t.gameObject);   // one still in his hand
    }

    // beaten, spared - and getting up anyway (not to fight you: he stays defeated)
    public void StandUp(float time = 0.45f)
    {
        StopAllCoroutines();
        StartCoroutine(Untip(time));
    }

    IEnumerator Untip(float time)
    {
        Transform m = model != null ? model : transform;
        Quaternion r0 = m.localRotation;
        Vector3 p0 = m.localPosition;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            m.localRotation = Quaternion.Slerp(r0, modelRot0, k);
            m.localPosition = Vector3.Lerp(p0, modelPos0, k);
            yield return null;
        }
        m.localRotation = modelRot0;
        m.localPosition = modelPos0;
    }

    // the kill shot: he falls on his back
    public void FallDead() => StartCoroutine(Tip(-85f, -0.9f, 0.6f));

    IEnumerator Tip(float pitch, float drop, float time)
    {
        Transform m = model != null ? model : transform;
        Quaternion r0 = m.localRotation, r1 = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 p0 = m.localPosition, p1 = new Vector3(p0.x, drop, p0.z);
        // settle into the idle pose (switching the animator off froze him mid-swing, arms out); the root it
        // leaves alone, so the tip still applies
        if (animator != null && animator.enabled)
        {
            animator.SetFloat("Speed", 0f);
            animator.Play("Locomotion", 0, 0f);
        }
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            m.localRotation = Quaternion.Slerp(r0, r1, k);
            m.localPosition = Vector3.Lerp(p0, p1, k);
            yield return null;
        }
    }

    // ---------------------------------------------------------------- helpers

    void SetSpeed(float s) { if (animator != null && animator.enabled) animator.SetFloat("Speed", s); }

    // cross-fade to a state, if this controller has it (an old controller: he just doesn't play the move)
    void Go(string state, float fade)
    {
        if (animator == null || !animator.enabled || !animator.HasState(0, Animator.StringToHash(state))) return;
        animator.CrossFadeInFixedTime(state, fade, 0);
    }

    void Face(Vector3 flat, float speed)
    {
        if (flat.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), speed * Time.deltaTime);
    }

    // the same spot, pulled back inside his ring of the silo floor
    Vector3 InArena(Vector3 p)
    {
        if (arenaCenter == null) return p;
        Vector3 off = Flat(p - arenaCenter.position);
        return off.magnitude > arenaRadius ? arenaCenter.position + off.normalized * arenaRadius + Vector3.up * (p.y - arenaCenter.position.y) : p;
    }

    void Say(AudioClip clip, float volume) { if (voice != null && clip != null) voice.PlayOneShot(clip, volume); }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // plays the swing if the controller has one
    public static void Attack(Animator a)
    {
        if (a == null || !a.enabled || a.runtimeAnimatorController == null) return;
        foreach (var p in a.parameters)
            if (p.name == "Attack" && p.type == AnimatorControllerParameterType.Trigger) { a.SetTrigger("Attack"); return; }
    }
}
