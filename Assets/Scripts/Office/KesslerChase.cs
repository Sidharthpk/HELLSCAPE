using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Kessler, turned, coming after you from the office (user, 2026-10-02). The silo door no longer drops you into the
// complex: it lets you out into a service tunnel hung with bodies, and the run to the silo is his chase:
//   Arrive()          the office's silo door (Teleporter.onArrive): you're in the tunnel, its door shut behind you
//   (the door)        a few seconds after you arrive, a cutscene: he's at it already, blows that rock it in its frame
//                     and shake the picture. You get control back with him still hammering at it behind you
//   DoorBurst()       once you've walked on for 'walkTime' seconds: the second cutscene. It comes off its hinges, he
//                     steps through, screams, and comes down the tunnel at the lens at a run
//   (the chase)       he follows your footsteps, fast: you have to sprint. The hanging bodies swing as you and he
//                     push through them (HangingBody)
//   BridgeReached()   a trigger on the footbridge: Tomas's ghost steps into Kessler's way and holds him. It buys
//                     you a few seconds
//   OpenSiloDoor()    a trigger at the far end of the bridge: the door into the silo block swings open for you
//   EnterSilo()       a trigger just inside: it slams behind you. He's at it straight away: loud blows, the
//                     screen shaking, until you open the next door on (StopBanging) - which is where Elias takes over
//   ResetChase()      a checkpoint respawn in the tunnel: all of it again (the cutscene is shortened)
// There's no NavMesh up here, so like Elias's chase he walks the trail of where you've been.
public class KesslerChase : MonoBehaviour
{
    [Header("Player")]
    public Transform player;
    public Transform playerHead;            // the player camera
    public PlayerHealth health;
    public KillerCutscenes cuts;            // its camera, letterbox and player lock run the door cutscene

    [Header("Kessler")]
    public KesslerConfrontation office;     // left behind when you arrive
    public Transform kessler;               // the damned body (the office's)
    public Animator kesslerAnim;
    public string runState = "Run", screamState = "Scream", attackState = "zombieattack", idleState = "Blend Tree";
    public float speed = 6.8f;              // you walk 5 and sprint 7: flat out you only just gain on him
    public float catchUpSpeed = 8.8f;       // when he's fallen this far behind...
    public float catchUpDistance = 9f;
    public float catchDistance = 1.5f;
    public float hitDamage = 25f;
    public float stunAfterHit = 1.6f;
    public float headStart = 0.3f;          // after the cutscene, before he moves again

    [Header("The tunnel door")]
    public Transform tunnelDoor;            // comes off its hinges
    public Transform doorway;               // on the floor in the opening, +z into the tunnel
    public Light doorLight;                 // what's behind him
    public float bangDelay = 3.5f;          // after you arrive, before he's at the door (the cutscene)
    public float walkTime = 5f;             // seconds of walking on, after that, before he's through it

    [Header("The footbridge")]
    public Transform tomas;                 // his ghost, switched off
    public Animator tomasAnim;
    public Transform bridgeMouth;           // where he makes his stand (the tunnel end of the bridge)
    public float holdTime = 7f;

    [Header("Into the silo")]
    public GameObject siloDoor;             // the door at the far end of the bridge
    public float bangEvery = 1.25f;
    public float bangTime = 40f;            // he gives up after this, if you haven't moved on

    [Header("Sound")]
    public AudioSource sfx;

    [Header("Lines ('|' splits)")]
    [TextArea] public string knockLine = "That's HIM. He's at the door already--|It won't hold. MOVE!";
    [TextArea] public string screamLine = "Kessler: YOU AIN'T RUNNING AWAY FROM ME!";
    [TextArea] public string runLine = "He's THROUGH-- RUN!";
    [TextArea] public string hitLines = "AGH--!|He's on me-- MOVE!|Don't stop. Don't STOP!";
    [TextArea] public string tomasLine = "Tomas: Don't stop! Over the bridge-- GO!";
    [TextArea] public string tomasHoldLine = "Tomas: Not this one, Mr. Kessler. You've had enough of us.|Tomas. He's holding him back--";
    [TextArea] public string tomasLostLine = "Kessler: OUT of my WAY, cleaner!";
    [TextArea] public string insideLine = "Shut. It's SHUT.|He's right outside. That door won't hold for long.|Keep moving. There has to be a way on through here.";
    [TextArea] public string quietLine = "...it's stopped.";

    enum Phase { Idle, Waiting, Cutscene, Chasing, Held, Banging, Done }
    Phase phase = Phase.Idle;
    readonly Queue<Vector3> trail = new Queue<Vector3>();
    Vector3 lastCrumb, doorPos0;
    Quaternion doorRot0;
    bool doorKnown, bangsSeen, burstSeen, tomasUsed, tomasWaiting, knocking;
    float stun, walked;
    Vector3 lastPos;
    Coroutine knock;
    string animNow;
    string[] hits;
    int hitIndex;
    AudioClip thud;

    public bool Chasing => phase == Phase.Chasing || phase == Phase.Held;

    void Awake()
    {
        hits = hitLines.Split('|');
        if (tunnelDoor != null) { doorPos0 = tunnelDoor.position; doorRot0 = tunnelDoor.rotation; doorKnown = true; }
        if (tomas != null) tomas.gameObject.SetActive(false);
        if (doorLight != null) doorLight.enabled = false;
    }

    // ---------------------------------------------------------------- arriving, and starting over

    public void Arrive()
    {
        if (office != null) office.Leave();
        ResetChase();
    }

    public void ResetChase()
    {
        StopAllCoroutines();
        thud = Sounds.Clip("Silo Complex/Lift doors slam on him", ProceduralAudio.Thud());
        phase = Phase.Waiting;
        stun = 0f;
        tomasUsed = tomasWaiting = false;
        if (kessler != null) kessler.gameObject.SetActive(false);
        if (tomas != null) tomas.gameObject.SetActive(false);
        if (doorLight != null) doorLight.enabled = false;
        if (siloDoor != null) siloDoor.SetActive(true);
        if (doorKnown)
        {
            tunnelDoor.SetPositionAndRotation(doorPos0, doorRot0);
            foreach (var c in tunnelDoor.GetComponentsInChildren<Collider>()) c.enabled = true;
        }
        trail.Clear();
        lastCrumb = Feet();
        trail.Enqueue(lastCrumb);
        walked = 0f;
        knocking = false;
        lastPos = player.position;
        knock = StartCoroutine(DoorBangs());
    }

    // ---------------------------------------------------------------- the door

    // he's at it before you've got your bearings: the first time a cutscene, and then he keeps on while you walk
    IEnumerator DoorBangs()
    {
        if (!bangsSeen)
        {
            yield return new WaitForSeconds(bangDelay);
            bangsSeen = true;
            phase = Phase.Cutscene;
            Vector3 d = doorway.position, into = doorway.forward;
            Vector3 c0 = d + into * 4.6f + doorway.right * 0.8f + Vector3.up * 1.5f;
            cuts.Begin();
            cuts.cam.transform.SetPositionAndRotation(c0, Quaternion.LookRotation(d + Vector3.up * 1.25f - c0));
            if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(knockLine);
            float[] gaps = { 0.5f, 0.8f, 0.55f, 0.4f, 0.3f };
            for (int i = 0; i < gaps.Length; i++)
            {
                yield return new WaitForSeconds(gaps[i]);
                yield return Blow(0.7f + 0.15f * i, c0);
            }
            yield return new WaitForSeconds(0.5f);
            cuts.End();
            phase = Phase.Waiting;
        }
        knocking = true;
        while (phase == Phase.Waiting)
        {
            yield return new WaitForSeconds(Random.Range(0.7f, 1.3f));
            if (phase == Phase.Waiting) yield return Blow(0.9f, null);
        }
    }

    // one blow on the tunnel door: it jumps in its frame. In a cutscene the picture shakes (camHome: where the
    // cutscene camera sits); otherwise your head does, harder the nearer the door you still are
    IEnumerator Blow(float power, Vector3? camHome)
    {
        Play(thud, Mathf.Clamp01(0.45f + 0.5f * power));
        if (camHome == null) StartCoroutine(HeadShake(0.4f, 0.02f + 0.07f * power * Mathf.Clamp01(1f - Vector3.Distance(player.position, doorPos0) / 40f)));
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            // knocked in, and ringing in its frame after (slow enough to read as a shudder, not a flicker)
            float k = power * Mathf.Exp(-7f * t), jolt = k * Mathf.Cos(t * 38f);
            tunnelDoor.SetPositionAndRotation(doorPos0 + doorway.forward * 0.1f * jolt, Quaternion.AngleAxis(3f * jolt, doorway.right) * doorRot0);
            if (camHome != null) cuts.cam.transform.position = camHome.Value + Random.insideUnitSphere * 0.09f * k;
            yield return null;
        }
        tunnelDoor.SetPositionAndRotation(doorPos0, doorRot0);
        if (camHome != null) cuts.cam.transform.position = camHome.Value;
    }

    public void DoorBurst()
    {
        if (phase != Phase.Waiting) return;
        if (knock != null) StopCoroutine(knock);
        knocking = false;
        tunnelDoor.SetPositionAndRotation(doorPos0, doorRot0);
        StartCoroutine(burstSeen ? QuickBurst() : Burst());
    }

    IEnumerator Burst()
    {
        phase = Phase.Cutscene;
        burstSeen = true;
        Camera cam = cuts.cam;
        Vector3 d = doorway.position, into = doorway.forward, side = doorway.right;
        cuts.Begin();

        // shot 1: back down the tunnel at the door. Two more blows: the last one it doesn't survive
        Vector3 c0 = d + into * 8.5f + side * 1.7f + Vector3.up * 1.45f, look = d + Vector3.up * 1.3f;
        cam.transform.SetPositionAndRotation(c0, Quaternion.LookRotation(look - c0));
        yield return new WaitForSeconds(0.35f);
        yield return Blow(1.2f, c0);
        yield return new WaitForSeconds(0.3f);
        yield return Blow(1.5f, c0);
        yield return new WaitForSeconds(0.25f);

        // it comes off its hinges, and he's standing in the hole
        Play(thud, 1f);
        PlaceKessler(d - into * 0.5f, into);
        if (doorLight != null) doorLight.enabled = true;
        StartCoroutine(DoorFlies(0.85f));
        Vector3 k0 = kessler.position, k1 = d + into * 2.4f;
        kesslerAnim.SetFloat("Speed", 1.3f);
        Anim(idleState, 0f);
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            float k = t / 1.5f;
            kessler.position = Vector3.Lerp(k0, k1, k);
            cam.transform.position = Vector3.Lerp(c0, c0 - into * 0.8f, k) + Random.insideUnitSphere * 0.05f * Mathf.Clamp01(1f - t / 0.5f);
            cam.transform.rotation = Quaternion.LookRotation(kessler.position + Vector3.up * 1.4f - cam.transform.position);
            yield return null;
        }
        kesslerAnim.SetFloat("Speed", 0f);

        // shot 2: his face (wherever the scream throws his head). The scream
        Transform head = Find(kessler, "Head");
        Vector3 Face() => head != null ? head.position + Vector3.up * 0.08f : kessler.position + Vector3.up * 1.7f;
        Anim(screamState, 0.05f);
        var z = kessler.GetComponent<Zombie>();
        if (z != null) z.Scream();
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(screamLine);
        yield return null;
        Vector3 c1 = Face() + into * 1.5f + side * 0.3f + Vector3.up * 0.1f;
        for (float t = 0f; t < 2.4f; t += Time.deltaTime)
        {
            cam.transform.position = c1 - into * 0.3f * (t / 2.4f) + Random.insideUnitSphere * 0.04f;
            // (a little above his face: the dialogue box is along the bottom of the picture)
            cam.transform.rotation = Quaternion.LookRotation(Face() - Vector3.up * 0.22f - cam.transform.position);
            yield return null;
        }

        // shot 3: low, further down the tunnel, on the way you went. He comes at the lens flat out
        PickUpTrail();
        Vector3 c2 = kessler.position, last = kessler.position;
        float along = 0f;
        foreach (var crumb in trail)
        {
            along += Vector3.Distance(last, crumb);
            last = c2 = crumb;
            if (along > 11f) break;
        }
        c2 += Vector3.up * 0.75f;
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(runLine);
        for (float t = 0f; t < 1.15f && Flat(kessler.position - c2).magnitude > 1.6f; t += Time.deltaTime)
        {
            RunTrail(catchUpSpeed);
            cam.transform.position = c2 + Random.insideUnitSphere * 0.05f;
            cam.transform.rotation = Quaternion.LookRotation(Face() - Vector3.up * 0.3f - cam.transform.position);
            yield return null;
        }

        // back in your own eyes, with him already coming
        cuts.End();
        StartChase();
    }

    // a retry: no cutscene, the door just goes
    IEnumerator QuickBurst()
    {
        phase = Phase.Cutscene;
        Play(thud, 1f);
        PlaceKessler(doorway.position + doorway.forward * 0.6f, doorway.forward);
        if (doorLight != null) doorLight.enabled = true;
        yield return DoorFlies(0.7f);
        var z = kessler.GetComponent<Zombie>();
        if (z != null) z.Scream();
        StartChase();
    }

    void PlaceKessler(Vector3 at, Vector3 facing)
    {
        kessler.SetPositionAndRotation(at, Quaternion.LookRotation(Flat(facing)));
        kessler.gameObject.SetActive(true);
        animNow = null;
    }

    IEnumerator DoorFlies(float time)
    {
        foreach (var c in tunnelDoor.GetComponentsInChildren<Collider>()) c.enabled = false;
        Vector3 into = doorway.forward, to = doorPos0 + into * 5.5f + doorway.right * 1.1f - Vector3.up * (doorPos0.y - doorway.position.y - 0.12f);
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            tunnelDoor.position = Vector3.Lerp(doorPos0, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.9f;
            tunnelDoor.rotation = Quaternion.AngleAxis(450f * k, doorway.right) * doorRot0;
            yield return null;
        }
        tunnelDoor.SetPositionAndRotation(to, Quaternion.AngleAxis(450f, doorway.right) * doorRot0);
        Play(thud, 0.5f);
    }

    void StartChase()
    {
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(runLine);
        MusicManager.Area("Silo Chase");
        PickUpTrail();
        stun = headStart;
        phase = Phase.Chasing;
    }

    // he picks your trail up from where he stands
    void PickUpTrail()
    {
        var crumbs = trail.ToArray();
        int nearest = 0;
        for (int i = 1; i < crumbs.Length; i++)
            if ((crumbs[i] - kessler.position).sqrMagnitude <= (crumbs[nearest] - kessler.position).sqrMagnitude) nearest = i;
        for (int i = 0; i < nearest; i++) trail.Dequeue();
    }

    // a frame of his run, along it
    void RunTrail(float s)
    {
        Vector3 me = kessler.position;
        while (trail.Count > 1 && (trail.Peek() - me).sqrMagnitude < 0.2f) trail.Dequeue();
        Vector3 target = trail.Count > 0 ? trail.Peek() : Feet();
        kessler.position = Vector3.MoveTowards(me, target, s * Time.deltaTime);
        Vector3 flat = Flat(target - me);
        if (flat.sqrMagnitude > 0.001f) kessler.rotation = Quaternion.Slerp(kessler.rotation, Quaternion.LookRotation(flat), 10f * Time.deltaTime);
        Anim(runState, 0.12f);
    }

    // ---------------------------------------------------------------- the chase

    Vector3 Feet() => player.position - Vector3.up * player.lossyScale.y;

    void Update()
    {
        if (phase == Phase.Idle || phase == Phase.Done || player == null) return;

        if (player.gameObject.activeInHierarchy && phase != Phase.Banging)
        {
            Vector3 f = Feet();
            if ((f - lastCrumb).sqrMagnitude > 0.36f) { trail.Enqueue(f); lastCrumb = f; }
        }
        // the door holds for as long as it takes you to walk on a little way
        if (phase == Phase.Waiting && knocking && Time.deltaTime > 0f)
        {
            if (Flat(player.position - lastPos).magnitude / Time.deltaTime > 1.5f) walked += Time.deltaTime;
            if (walked >= walkTime) DoorBurst();
        }
        lastPos = player.position;
        if (phase != Phase.Chasing) return;

        stun -= Time.deltaTime;
        if (stun > 0f) return;

        Vector3 me = kessler.position, toPlayer = Feet() - me;
        if (toPlayer.magnitude < catchDistance)
        {
            // got you: a blow that throws you on down the tunnel
            stun = stunAfterHit;
            Anim(attackState, 0.05f);
            if (Flat(toPlayer).sqrMagnitude > 0.001f) kessler.rotation = Quaternion.LookRotation(Flat(toPlayer));
            if (health != null) health.TakeDamage(hitDamage);
            Play(thud, 0.9f);
            if (DialogueBox.Instance != null && hits.Length > 0) DialogueBox.Instance.Say(hits[hitIndex++ % hits.Length]);
            var rb = player.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) rb.AddForce(Flat(toPlayer).normalized * 7f + Vector3.up * 2f, ForceMode.VelocityChange);
            return;
        }

        float behind = 0f;
        Vector3 prev = me;
        foreach (var c in trail) { behind += Vector3.Distance(prev, c); prev = c; }
        RunTrail(behind > catchUpDistance ? catchUpSpeed : speed);
    }

    // ---------------------------------------------------------------- Tomas, on the footbridge

    // a trigger near the end of the tunnel: he's standing at its mouth as you come round the last of the bend
    public void TomasWaits()
    {
        if (phase != Phase.Chasing || tomasUsed || tomasWaiting || tomas == null) return;
        tomasWaiting = true;
        Vector3 away = bridgeMouth.forward;   // (+z: on along the bridge, toward the silo)
        Vector3 at = bridgeMouth.position - away * 2.8f + bridgeMouth.right * 1.05f;
        tomas.SetPositionAndRotation(at, Quaternion.LookRotation(-away));
        tomas.gameObject.SetActive(true);
        if (tomasAnim != null) tomasAnim.SetFloat("Speed", 0f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(tomasLine);
    }

    // a trigger on the bridge: you're past him, and he steps into Kessler's way
    public void BridgeReached()
    {
        if (phase != Phase.Chasing || tomasUsed || tomas == null) return;
        if (!tomasWaiting) TomasWaits();
        tomasUsed = true;
        StartCoroutine(TomasHolds());
    }

    IEnumerator TomasHolds()
    {
        // between you and him: in the tunnel's mouth, or right in front of him if he's already past it
        Vector3 away = bridgeMouth.forward, stand = bridgeMouth.position - away * 1.6f;
        if (Vector3.Dot(kessler.position - stand, away) > -1.5f) stand = kessler.position + Flat(player.position - kessler.position).normalized * 1.4f;
        Vector3 from = tomas.position;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            tomas.position = Vector3.Lerp(from, stand, t / 0.3f);
            tomas.rotation = Quaternion.LookRotation(Flat(kessler.position - tomas.position));
            yield return null;
        }
        tomas.SetPositionAndRotation(stand, Quaternion.LookRotation(Flat(kessler.position - stand)));

        // he runs straight into him
        for (float t = 0f; t < 5f && phase == Phase.Chasing && Flat(kessler.position - stand).magnitude > 1.5f; t += Time.deltaTime) yield return null;
        if (phase != Phase.Chasing) { tomas.gameObject.SetActive(false); yield break; }
        phase = Phase.Held;
        Play(thud, 0.8f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(tomasHoldLine);
        Vector3 hold = kessler.position;
        float swing = 0f;
        for (float t = 0f; t < holdTime && phase == Phase.Held; t += Time.deltaTime)
        {
            // straining against each other
            kessler.position = hold + Random.insideUnitSphere * 0.03f;
            kessler.rotation = Quaternion.LookRotation(Flat(stand - hold));
            tomas.rotation = Quaternion.LookRotation(Flat(hold - stand));
            tomas.position = stand + Random.insideUnitSphere * 0.02f + Flat(stand - hold).normalized * 0.25f * Mathf.Clamp01(t / holdTime);
            swing -= Time.deltaTime;
            if (swing <= 0f) { swing = 1.3f; animNow = null; Anim(attackState, 0.05f); }
            yield return null;
        }
        if (phase != Phase.Held) yield break;   // (you got inside: EnterSilo has taken over)

        // and through him
        Play(thud, 1f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(tomasLostLine);
        tomas.gameObject.SetActive(false);
        kessler.position = hold;
        stun = 0.4f;
        phase = Phase.Chasing;
    }

    void LateUpdate()
    {
        // Tomas, arms out against him (after his animator has posed him)
        if (phase != Phase.Held || tomas == null || !tomas.gameObject.activeInHierarchy || tomasAnim == null || !tomasAnim.isHuman) return;
        Vector3 dir = (kessler.position + Vector3.up * 1.4f - (tomas.position + Vector3.up * 1.4f)).normalized;
        Arm(tomasAnim, true, dir);
        Arm(tomasAnim, false, dir);
    }

    static void Arm(Animator a, bool right, Vector3 dir)
    {
        var up = a.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
        var lo = a.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
        var hand = a.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        if (up == null || lo == null || hand == null) return;
        up.rotation = Quaternion.FromToRotation(lo.position - up.position, dir) * up.rotation;
        lo.rotation = Quaternion.FromToRotation(hand.position - lo.position, dir) * lo.rotation;
    }

    // ---------------------------------------------------------------- the silo door

    public void OpenSiloDoor()
    {
        if (siloDoor == null || !siloDoor.activeSelf || phase == Phase.Banging || phase == Phase.Done) return;
        siloDoor.SetActive(false);
        Play(ProceduralAudio.Click(), 1f);
    }

    public void EnterSilo()
    {
        if (phase == Phase.Banging || phase == Phase.Done || phase == Phase.Idle) return;
        StopAllCoroutines();
        bool hunted = phase == Phase.Chasing || phase == Phase.Held;
        // (if Tomas has him, or he's still back in the tunnel, it's a few seconds before the first blow lands)
        float reach = phase == Phase.Held ? 3f : Mathf.Clamp(Vector3.Distance(kessler.position, player.position) / catchUpSpeed, 0.6f, 4f);
        phase = Phase.Banging;
        if (tomas != null) tomas.gameObject.SetActive(false);
        if (siloDoor != null) siloDoor.SetActive(true);
        Play(thud, 1f);
        StartCoroutine(HeadShake(0.35f, 0.05f));
        MusicManager.Area("Silo Complex");
        if (!hunted) { phase = Phase.Done; if (kessler != null) kessler.gameObject.SetActive(false); return; }
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(insideLine);
        StartCoroutine(Bangs(reach));
    }

    // he's at the door: every blow shakes the room (harder the nearer you stand to it)
    IEnumerator Bangs(float reach)
    {
        Vector3 door = siloDoor.transform.position, doorHome = door;
        var rend = siloDoor.GetComponent<Renderer>();
        if (rend != null) door = rend.bounds.center;
        Vector3 outside = Flat(kessler.position - door).normalized;
        yield return new WaitForSeconds(reach);
        kessler.SetPositionAndRotation(new Vector3(door.x, kessler.position.y, door.z) + outside * 1.1f, Quaternion.LookRotation(-outside));
        for (float t = 0f; t < bangTime && phase == Phase.Banging; )
        {
            animNow = null; Anim(attackState, 0.05f);
            yield return new WaitForSeconds(0.35f);
            Play(thud, 1f);
            float near = Mathf.Clamp01(1f - Vector3.Distance(player.position, door) / 22f);
            StartCoroutine(HeadShake(0.4f, 0.02f + 0.07f * near));
            for (float s = 0f; s < 0.5f; s += Time.deltaTime)
            {
                siloDoor.transform.position = doorHome - outside * 0.06f * Mathf.Exp(-7f * s) * Mathf.Cos(s * 38f);
                yield return null;
            }
            siloDoor.transform.position = doorHome;
            float wait = bangEvery * Random.Range(0.75f, 1.3f);
            yield return new WaitForSeconds(wait);
            t += wait + 0.85f;
        }
        siloDoor.transform.position = doorHome;
        if (phase == Phase.Banging && DialogueBox.Instance != null) DialogueBox.Instance.Say(quietLine);
        phase = Phase.Done;
        kessler.gameObject.SetActive(false);
    }

    // the next door on is open: whatever is ahead has your attention now
    public void StopBanging()
    {
        if (phase == Phase.Banging) phase = Phase.Done;
    }

    IEnumerator HeadShake(float time, float amount)
    {
        if (playerHead == null) yield break;
        Vector3 p0 = playerHead.localPosition;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            playerHead.localPosition = p0 + Random.insideUnitSphere * amount * (1f - t / time);
            yield return null;
        }
        playerHead.localPosition = p0;
    }

    // ---------------------------------------------------------------- helpers

    void Anim(string state, float fade)
    {
        if (kesslerAnim == null || !kesslerAnim.isActiveAndEnabled || state == animNow) return;
        animNow = state;
        if (kesslerAnim.HasState(0, Animator.StringToHash(state))) kesslerAnim.CrossFadeInFixedTime(state, fade, 0);
    }

    // a bone by the end of its name ("mixamorig5:Head")
    static Transform Find(Transform under, string endsWith)
    {
        foreach (var t in under.GetComponentsInChildren<Transform>())
            if (t.name.EndsWith(endsWith)) return t;
        return null;
    }

    void Play(AudioClip clip, float volume) { if (sfx != null && clip != null) sfx.PlayOneShot(clip, volume); }
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
