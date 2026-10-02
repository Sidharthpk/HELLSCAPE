using System.Collections;
using System.Linq;
using UnityEngine;

// Elias's big beats in the silo complex:
//   JumpScare(spawn) - the very start, in your own eyes: footsteps, you whip round, his face, he shoves you flat on
//                      the bridge and comes at you. You scramble up and the chase is on.
//   Ambush(spawn)    - after the lift: a bang on the roof, and he drops onto the lower bridge behind you. Run again.
//   SlamShut(door)   - the corridor door crashes shut behind you: no way back.
// and three short letterboxed camera cutscenes:
//   Reveal(spawn)  - (the old chase start, kept for reference) he steps out of the dark corridor behind you.
//   LiftSlam()     - the lift: from inside the car, he sprints at you as the doors grind shut in his face.
//   Defeat()       - the fight is won: a slow low orbit round him on his knees, mask cracked.
// and Maya's two scenes on the silo stairs (run by SiloEncounter):
//   GunThrow()     - she heaves the rifle off the stairs; it tumbles down the shaft and clatters at your feet.
//   MayaTurns()    - you spared Elias: she loses it, says what SHE did, and the city takes her. The thing she
//                    becomes drops down the shaft and comes for you - and Elias gets up and takes it instead.
// One camera does all of them; the player is frozen while it rolls and handed back after.
public class KillerCutscenes : MonoBehaviour
{
    public Camera cam;                      // disabled when idle, above the player camera
    public Light key;                       // on the camera: lights him in the shot
    public RectTransform barTop, barBottom; // letterbox bars (height animated)
    public float barHeight = 90f;
    public Transform player;
    public Transform playerHead;            // the player camera
    public Transform killer;                // KillerBoss root
    public Animator killerAnim;             // Killer.controller: "Speed", "Attack"
    public ChaseKiller chase;
    public Behaviour[] lockDuring;          // movement, fists, rifle, interacting

    [Header("Lift")]
    public Transform liftCamera;            // inside the car, looking out through the top doors
    public Transform liftRunFrom, liftRunTo;// his sprint along the walkway to the doors

    [Header("Sound")]
    public AudioSource sfx;
    public AudioClip stinger, slam;         // procedural stand-ins if empty

    [Header("The opening jump scare")]
    [TextArea] public string scareLine = "...footsteps. Right behind me.";
    [TextArea] public string getUpLine = "Elias--! GET UP. GET UP!|RUN!";
    public float shoveDistance = 3.4f;
    public float shoveDamage = 15f;
    public float headStart = 1.6f;          // seconds before he comes after you once you're up
    [TextArea] public string slamLine = "The door-- it shut by itself.|...no way back.";
    [TextArea] public string ambushWarn = "Something just hit the roof--";
    [TextArea] public string ambushLine = "He's BEHIND me-- GO! GO!";

    private bool revealSeen, scareSeen;
    private bool[] wasOn;
    private Renderer[] hidden;

    public bool Playing { get; private set; }

    void Awake()
    {
        if (cam != null) cam.enabled = false;
        SetBars(0f);
    }

    // ---------------------------------------------------------------- the reveal

    public void Reveal(Transform spawn)
    {
        if (revealSeen || Playing) { chase.Begin(spawn); return; }   // a checkpoint replay goes straight to the chase
        revealSeen = true;
        StartCoroutine(RevealRoutine(spawn));
    }

    IEnumerator RevealRoutine(Transform spawn)
    {
        Begin();
        // he stands in the corridor, facing the way you went (the spawn's forward: down the corridor to the door)
        Vector3 dir = Flat(spawn.forward).normalized;
        killer.SetPositionAndRotation(spawn.position, Quaternion.LookRotation(dir));
        killer.gameObject.SetActive(true);
        Speed(0f);
        Play(stinger, 0.9f);

        // shot 1: down the corridor in front of him, low; he walks out of the dark straight at the lens
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        Vector3 head = killer.position + Vector3.up * 1.6f;
        Vector3 a = killer.position + dir * 5f + side * 0.4f + Vector3.up * 1.3f;
        if (Physics.Linecast(head, a, out RaycastHit block, ~0, QueryTriggerInteraction.Ignore))
            a = block.point - (a - head).normalized * 0.3f;   // never behind a wall
        float t0 = 2.6f;
        Vector3 k0 = killer.position, k1 = killer.position + dir * 2.2f;
        for (float t = 0f; t < t0; t += Time.deltaTime)
        {
            float k = t / t0;
            Speed(1.3f);
            killer.position = Vector3.Lerp(k0, k1, k);
            cam.transform.position = a + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.05f;
            cam.transform.rotation = Quaternion.LookRotation(killer.position + Vector3.up * 1.45f - cam.transform.position);
            yield return null;
        }
        Speed(0f);

        // shot 2: the mask, close; he lifts his arms and swings at the lens
        Vector3 face = killer.position + Vector3.up * 1.65f;
        Vector3 c = face + killer.forward * 1.3f + killer.right * 0.25f - Vector3.up * 0.25f;
        cam.transform.SetPositionAndRotation(c, Quaternion.LookRotation(face - c));
        KillerBoss.Attack(killerAnim);
        yield return Shake(1.4f, 0.03f, face);

        End();
        MusicManager.Area("Silo Chase");
        chase.Begin(killer);   // from where he stands now
    }

    // ---------------------------------------------------------------- the opening jump scare

    public void JumpScare(Transform spawn)
    {
        if (scareSeen || Playing) { MusicManager.Area("Silo Chase"); chase.Begin(spawn); return; }   // a checkpoint replay goes straight to the chase
        scareSeen = true;
        StartCoroutine(JumpScareRoutine());
    }

    IEnumerator JumpScareRoutine()
    {
        Playing = true;
        wasOn = new bool[lockDuring.Length];
        for (int i = 0; i < lockDuring.Length; i++)
            if (lockDuring[i] != null) { wasOn[i] = lockDuring[i].enabled; lockDuring[i].enabled = false; }
        stinger = Sounds.Clip("Silo Complex/Killer sting", stinger != null ? stinger : ProceduralAudio.Thud());
        slam = Sounds.Clip("Silo Complex/Lift doors slam on him", slam != null ? slam : ProceduralAudio.Thud());
        var rb = player.GetComponent<Rigidbody>();
        var fpc = player.GetComponent<FirstPersonController>();
        var hp = player.GetComponent<PlayerHealth>();
        Transform head = playerHead;
        if (rb != null && !rb.isKinematic) rb.linearVelocity = Vector3.zero;
        StartCoroutine(Bars(1f));

        // two heavy steps in the dark behind you
        Play(slam, 0.3f);
        yield return new WaitForSeconds(0.45f);
        Play(slam, 0.5f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(scareLine);
        yield return new WaitForSeconds(0.65f);

        // he is right there
        Vector3 back = -Flat(player.forward).normalized;
        Vector3 feet = player.position - Vector3.up * player.lossyScale.y;
        Vector3 at = feet + back * 1.15f;
        if (Physics.Raycast(at + Vector3.up, Vector3.down, out RaycastHit floor, 2.5f, ~0, QueryTriggerInteraction.Ignore) && !floor.collider.transform.IsChildOf(player))
            at.y = floor.point.y;
        killer.SetPositionAndRotation(at, Quaternion.LookRotation(-back));
        killer.gameObject.SetActive(true);
        Speed(0f);
        Vector3 Face() => killer.position + Vector3.up * 1.62f * killer.lossyScale.y;

        // whip round, body and head, straight into his face
        Quaternion y0 = player.rotation, y1 = Quaternion.LookRotation(Flat(killer.position - player.position));
        Quaternion p0 = head.localRotation;
        for (float t = 0f; t < 0.22f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.22f);
            player.rotation = Quaternion.Slerp(y0, y1, k);
            head.localRotation = Quaternion.Slerp(p0, PitchTo(head, Face()), k);
            yield return null;
        }
        player.rotation = y1;
        Play(stinger, 1f);
        KillerBoss.Attack(killerAnim);
        Vector3 camHome = head.localPosition;
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            head.localPosition = camHome + Random.insideUnitSphere * 0.05f * (1f - t / 0.4f);
            head.localRotation = PitchTo(head, Face());
            yield return null;
        }
        head.localPosition = camHome;

        // the shove: thrown back along the bridge, flat on your back, still staring up at him
        Vector3 dir = Flat(player.position - killer.position).normalized;
        float dist = shoveDistance;
        foreach (var h in Physics.SphereCastAll(player.position, 0.3f, dir, shoveDistance + 0.6f, ~0, QueryTriggerInteraction.Ignore))
            if (!h.collider.transform.IsChildOf(player) && !h.collider.transform.IsChildOf(killer)) dist = Mathf.Min(dist, Mathf.Max(0.6f, h.distance - 0.5f));
        Play(slam, 1f);
        if (rb != null) rb.isKinematic = true;
        Vector3 s = player.position, e = s + dir * dist;
        for (float t = 0f; t < 0.45f; t += Time.deltaTime)
        {
            float k = t / 0.45f;
            player.position = Vector3.Lerp(s, e, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.35f;
            head.localPosition = camHome + Vector3.down * 0.95f * k * k;
            head.localRotation = PitchTo(head, Face());
            yield return null;
        }
        player.position = e;
        if (rb != null) rb.isKinematic = false;
        Physics.SyncTransforms();
        Vector3 low = camHome + Vector3.down * 0.95f;
        head.localPosition = low;
        Play(slam, 0.8f);
        if (hp != null) hp.TakeDamage(Mathf.Min(shoveDamage, hp.currentHealth - 1f));

        // on the floor: he comes at you, slowly, arm coming up
        Vector3 k0 = killer.position, k1 = k0 + dir * Mathf.Max(0.5f, dist - 1.9f);
        for (float t = 0f; t < 1.15f; t += Time.deltaTime)
        {
            Speed(1.3f);
            killer.position = Vector3.Lerp(k0, k1, t / 1.15f);
            head.localPosition = low + Random.insideUnitSphere * 0.012f;
            head.localRotation = PitchTo(head, Face());
            yield return null;
        }
        Speed(0f);
        KillerBoss.Attack(killerAnim);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(getUpLine);

        // scramble up, turning away from him down the bridge
        Quaternion yaw0 = player.rotation, yaw1 = Quaternion.LookRotation(dir);
        Quaternion pr = head.localRotation;
        for (float t = 0f; t < 0.55f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.55f);
            head.localPosition = Vector3.Lerp(low, camHome, k);
            player.rotation = Quaternion.Slerp(yaw0, yaw1, k);
            head.localRotation = Quaternion.Slerp(pr, Quaternion.identity, k);
            yield return null;
        }
        head.localPosition = camHome;
        head.localRotation = Quaternion.identity;
        player.rotation = yaw1;
        if (fpc != null) fpc.SetPitch(0f);

        for (int i = 0; i < lockDuring.Length; i++) if (lockDuring[i] != null && wasOn[i]) lockDuring[i].enabled = true;
        StartCoroutine(Bars(0f));
        Playing = false;
        MusicManager.Area("Silo Chase");
        chase.Begin(killer);   // from where he stands: a couple of steps behind you
        chase.HeadStart(headStart);
    }

    // camera-local rotation (pitch only, the body carries the yaw) that looks at a point
    static Quaternion PitchTo(Transform cam, Vector3 at)
    {
        Vector3 d = at - cam.position;
        float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
        return Quaternion.Euler(Mathf.Clamp(pitch, -85f, 85f), 0f, 0f);
    }

    // ---------------------------------------------------------------- the ambush on the lower bridge

    public void Ambush(Transform spawn)
    {
        if (Playing || chase.Chasing) return;
        StartCoroutine(AmbushRoutine(spawn));
    }

    IEnumerator AmbushRoutine(Transform spawn)
    {
        slam = Sounds.Clip("Silo Complex/Lift doors slam on him", slam != null ? slam : ProceduralAudio.Thud());
        // something heavy lands on the roof of the bridge...
        Play(slam, 1f);
        StartCoroutine(HeadShake(0.5f, 0.05f));
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(ambushWarn);
        yield return new WaitForSeconds(0.8f);
        // ...and drops onto the walkway behind you
        Vector3 top = spawn.position + Vector3.up * 2.6f;
        killer.SetPositionAndRotation(top, spawn.rotation);
        killer.gameObject.SetActive(true);
        Speed(0f);
        for (float t = 0f; t < 0.22f; t += Time.deltaTime)
        {
            killer.position = Vector3.Lerp(top, spawn.position, (t / 0.22f) * (t / 0.22f));
            yield return null;
        }
        killer.position = spawn.position;
        Play(slam, 1f);
        Play(stinger != null ? stinger : slam, 0.8f);
        StartCoroutine(HeadShake(0.45f, 0.09f));
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(ambushLine);
        MusicManager.Area("Silo Chase");
        yield return new WaitForSeconds(0.35f);
        chase.Arm();
        chase.Begin(killer);
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

    // ---------------------------------------------------------------- the door behind you

    public void SlamShut(GameObject door)
    {
        if (door == null || door.activeSelf) return;
        door.SetActive(true);
        slam = Sounds.Clip("Silo Complex/Lift doors slam on him", slam != null ? slam : ProceduralAudio.Thud());
        Play(slam, 1f);
        StartCoroutine(HeadShake(0.35f, 0.04f));
        if (DialogueBox.Instance != null) DialogueBox.Instance.Say(slamLine);
    }

    // ---------------------------------------------------------------- the lift doors

    public void LiftSlam()
    {
        if (!Playing) StartCoroutine(LiftRoutine());
    }

    IEnumerator LiftRoutine()
    {
        Begin();
        if (chase != null) chase.Stop(false);
        killer.gameObject.SetActive(true);
        killer.SetPositionAndRotation(liftRunFrom.position, Quaternion.LookRotation(Flat(liftRunTo.position - liftRunFrom.position)));
        cam.transform.SetPositionAndRotation(liftCamera.position, liftCamera.rotation);
        Play(stinger, 0.7f);

        const float run = 1.15f;   // Elevator.doorCloseDelay: he reaches the doors just as they shut
        for (float t = 0f; t < run; t += Time.deltaTime)
        {
            Speed(4.5f);
            killer.position = Vector3.Lerp(liftRunFrom.position, liftRunTo.position, t / run);
            yield return null;
        }
        Speed(0f);
        KillerBoss.Attack(killerAnim);
        Play(slam, 1f);
        yield return Shake(0.7f, 0.06f, null);
        killer.gameObject.SetActive(false);
        End();
    }

    // ---------------------------------------------------------------- on his knees

    public void Defeat()
    {
        if (!Playing) StartCoroutine(DefeatRoutine());
    }

    IEnumerator DefeatRoutine()
    {
        yield return new WaitForSeconds(0.6f);   // let him drop to his knees first
        Begin();
        Vector3 centre = killer.position + Vector3.up * 0.9f;
        Vector3 fwd = Flat(killer.forward).normalized;
        float start = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg - 50f;
        const float time = 3.6f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float ang = (start + 100f * Mathf.SmoothStep(0f, 1f, t / time)) * Mathf.Deg2Rad;
            float r = Mathf.Lerp(2.6f, 1.7f, t / time);
            Vector3 p = centre + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * r - Vector3.up * 0.35f;
            cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(centre + Vector3.up * 0.45f - p));
            yield return null;
        }
        End();
    }

    // ---------------------------------------------------------------- Maya throws the rifle

    private System.Action latePose;         // bone poses must go on after the Animator has written its own
    void LateUpdate() { if (latePose != null) latePose(); }

    public IEnumerator GunThrow(Transform maya, Animator body, Transform gun, Vector3 land, Transform centre, string line)
    {
        Begin();
        var sv = maya.GetComponent<Survivor>();
        if (sv != null) sv.enabled = false;   // (it keeps turning her to face you)
        Vector3 m = maya.position;
        Vector3 inward = Flat(centre.position - m).normalized, side = Vector3.Cross(Vector3.up, inward);
        maya.rotation = Quaternion.LookRotation(inward);
        if (body != null) body.SetFloat("Speed", 0f);

        // she's holding it across her body, both hands
        Vector3 carry = (inward * 0.55f + Vector3.down * 0.85f).normalized;
        Vector3 aim = carry;
        bool held = true;
        latePose = () =>
        {
            Arm(body, true, (aim - side * 0.22f).normalized);
            Arm(body, false, (aim + side * 0.22f).normalized);
            if (held) gun.SetPositionAndRotation(Hands(body, m + Vector3.up * 1.1f + inward * 0.4f), Quaternion.LookRotation(side, Vector3.up));
        };
        gun.gameObject.SetActive(true);

        // shot 1: out in the shaft, looking back at her on the stairs
        Vector3 chest = m + Vector3.up * 1.3f;
        Vector3 c0 = m + inward * 2.7f + side * 1f + Vector3.up * 1.6f, c1 = m + inward * 2f + side * 0.7f + Vector3.up * 1.5f;
        if (DialogueBox.Instance != null && !string.IsNullOrEmpty(line)) DialogueBox.Instance.SayNow(line);
        for (float t = 0f; t < 1.9f; t += Time.deltaTime)
        {
            cam.transform.position = Vector3.Lerp(c0, c1, Mathf.SmoothStep(0f, 1f, t / 1.9f));
            cam.transform.rotation = Quaternion.LookRotation(chest - cam.transform.position);
            yield return null;
        }

        // up over her head...
        Vector3 high = (Vector3.up - inward * 0.22f).normalized;
        for (float t = 0f; t < 0.42f; t += Time.deltaTime)
        {
            aim = Vector3.Slerp(carry, high, Mathf.SmoothStep(0f, 1f, t / 0.42f));
            cam.transform.rotation = Quaternion.LookRotation(chest + Vector3.up * 0.3f - cam.transform.position);
            yield return null;
        }
        // ...and out
        Vector3 fling = (inward * 0.9f + Vector3.up * 0.15f).normalized;
        for (float t = 0f; t < 0.16f; t += Time.deltaTime) { aim = Vector3.Slerp(high, fling, t / 0.16f); yield return null; }
        held = false;
        Play(slam, 0.25f);

        // shot 2: beside her, watching it tumble away down the shaft
        Vector3 p0 = gun.position;
        Vector3 c2 = m + side * 1.35f + Vector3.up * 2.05f - inward * 0.1f;
        Vector3 rest = (inward * 0.7f + Vector3.down * 0.6f).normalized;
        for (float t = 0f; t < 0.95f; t += Time.deltaTime)
        {
            float k = t / 0.95f;
            aim = Vector3.Slerp(fling, rest, Mathf.Clamp01(k * 2.5f));
            gun.position = p0 + inward * (5f * k) + Vector3.up * (1.3f * k - 10f * k * k);
            gun.Rotate(0f, 0f, 620f * Time.deltaTime, Space.Self);
            cam.transform.SetPositionAndRotation(c2, Quaternion.LookRotation(gun.position - c2));
            yield return null;
        }
        latePose = null;

        // shot 3: the silo floor. It comes down out of the dark and lands in front of you, him standing beyond it
        Vector3 toKiller = Flat(killer.position - land).normalized;
        Vector3 c3 = land - toKiller * 2.5f + Vector3.Cross(Vector3.up, toKiller) * 1.3f;
        Vector3 off = Flat(c3 - centre.position);
        if (off.magnitude > 5.2f) c3 = centre.position + off.normalized * 5.2f;
        c3.y = land.y + 0.45f;
        Vector3 q0 = land + Vector3.up * 13f + Flat(m - land).normalized * 2.5f;
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            float k = t / 0.8f;
            gun.position = Vector3.Lerp(q0, land, k * k);
            gun.Rotate(0f, 0f, 620f * Time.deltaTime, Space.Self);
            Vector3 look = Vector3.Lerp(land + Vector3.up * 3f, gun.position, 0.6f);
            cam.transform.SetPositionAndRotation(c3, Quaternion.LookRotation(look - c3));
            yield return null;
        }
        gun.SetPositionAndRotation(land, Quaternion.Euler(0f, Random.Range(0f, 360f), 90f));
        Play(slam, 0.6f);
        Quaternion r0 = cam.transform.rotation, r1 = Quaternion.LookRotation(Vector3.Lerp(land, killer.position + Vector3.up * 1.2f, 0.45f) - c3);
        for (float t = 0f; t < 1.1f; t += Time.deltaTime)
        {
            cam.transform.position = c3 + Random.insideUnitSphere * 0.04f * Mathf.Clamp01(1f - t / 0.3f);
            cam.transform.rotation = Quaternion.Slerp(r0, r1, Mathf.SmoothStep(0f, 1f, t / 1.1f));
            yield return null;
        }

        if (sv != null) sv.enabled = true;
        End();
    }

    // point a humanoid's whole arm along a world direction (rig-agnostic: works from where the bones are)
    static void Arm(Animator a, bool right, Vector3 dir)
    {
        if (a == null || !a.isHuman) return;
        var up = a.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
        var lo = a.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
        var hand = a.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        if (up == null || lo == null || hand == null) return;
        up.rotation = Quaternion.FromToRotation(lo.position - up.position, dir) * up.rotation;
        lo.rotation = Quaternion.FromToRotation(hand.position - lo.position, dir) * lo.rotation;
    }

    static Vector3 Hands(Animator a, Vector3 fallback)
    {
        if (a == null || !a.isHuman) return fallback;
        var l = a.GetBoneTransform(HumanBodyBones.LeftHand);
        var r = a.GetBoneTransform(HumanBodyBones.RightHand);
        return l != null && r != null ? (l.position + r.position) * 0.5f : fallback;
    }

    // ---------------------------------------------------------------- mercy: Maya says it out loud

    public IEnumerator MayaTurns(Survivor maya, RisenDamned risen, KillerBoss boss, Transform centre,
                                 string rant, string realise, string seen, string save, System.Func<float> bloodY)
    {
        Begin();
        Transform mt = maya.transform;
        maya.enabled = false;
        Vector3 m = mt.position;
        Vector3 inward = Flat(centre.position - m).normalized, side = Vector3.Cross(Vector3.up, inward);
        mt.rotation = Quaternion.LookRotation(inward);
        if (maya.animator != null) maya.animator.SetFloat("Speed", 0f);
        Vector3 face = m + Vector3.up * 1.42f;
        var box = DialogueBox.Instance;

        // shot 1: her, on the stairs, shouting down at you. The camera creeps in while she talks herself into it
        Vector3 c0 = m + inward * 2.3f + side * 0.75f + Vector3.up * 1.55f, c1 = m + inward * 1.2f + side * 0.3f + Vector3.up * 1.5f;
        if (box != null) box.SayNow(rant);
        yield return null;
        Vector3 jab = (inward * 0.8f - side * 0.45f + Vector3.down * 0.3f).normalized;   // pointing down at the two of you
        // she stabs her finger at you about once a second, the arm drifting a little between: a new random aim every
        // frame (what this was) made her hand buzz
        latePose = () =>
        {
            float stab = Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * 2.6f)), 6f);
            Vector3 drift = (side * (Mathf.PerlinNoise(Time.time * 0.6f, 3f) - 0.5f) + Vector3.up * (Mathf.PerlinNoise(7f, Time.time * 0.6f) - 0.5f)) * 0.12f;
            Arm(maya.animator, true, (jab + drift + Vector3.up * 0.14f * (1f - stab)).normalized);
        };
        for (float t = 0f; t < 2.5f || (box != null && box.Busy && t < 60f); t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 13f);
            cam.transform.position = Vector3.Lerp(c0, c1, k) + new Vector3(Mathf.PerlinNoise(t * 0.7f, 0f) - 0.5f, Mathf.PerlinNoise(0f, t * 0.7f) - 0.5f, 0f) * 0.05f;
            cam.transform.rotation = Quaternion.LookRotation(face - cam.transform.position);
            yield return null;
        }
        latePose = null;

        // she hears what she just said: the red comes up through her
        if (box != null) box.SayNow(realise);
        Sounds.OneShot(sfx, "Silo Fight/Colleague turns into the Damned", null, 1f);
        var glow = new GameObject("Turning glow").AddComponent<Light>();
        glow.transform.position = m + Vector3.up * 1.1f + inward * 0.4f;
        glow.color = new Color(1f, 0.1f, 0.04f);
        glow.range = 8f;
        glow.intensity = 0f;
        var rends = mt.GetComponentsInChildren<Renderer>();
        var block = new MaterialPropertyBlock();
        Vector3 camAt = cam.transform.position;
        const float burn = 3.4f;
        for (float t = 0f; t < burn; t += Time.deltaTime)
        {
            float k = t / burn;
            Color c = Color.Lerp(Color.white, new Color(1f, 0.05f, 0.02f), k) * (Random.value < 0.08f * k ? 0.3f : 1f);
            c.a = 1f;
            foreach (var r in rends) { r.GetPropertyBlock(block); block.SetColor("_BaseColor", c); r.SetPropertyBlock(block); }
            mt.position = m + Random.insideUnitSphere * 0.06f * k;
            glow.intensity = 10f * k;
            cam.transform.position = camAt + Random.insideUnitSphere * 0.025f * k;
            cam.transform.rotation = Quaternion.LookRotation(face - cam.transform.position);
            yield return null;
        }
        foreach (var r in rends) r.SetPropertyBlock(null);
        mt.position = m;

        // and it has her
        Zombie z = risen.damned;
        Transform zt = z.transform;
        Animator za = risen.anim;
        zt.SetPositionAndRotation(m, Quaternion.LookRotation(inward));
        z.health = risen.health;
        z.gameObject.SetActive(true);
        maya.Turn();
        za.CrossFadeInFixedTime("Scream", 0f, 0);
        z.Scream();
        Play(stinger, 1f);
        Vector3 close = m + inward * 1.25f + side * 0.2f + Vector3.up * 1.5f;
        cam.transform.SetPositionAndRotation(close, Quaternion.LookRotation(face - close));
        yield return Shake(1.6f, 0.06f, face);
        Destroy(glow.gameObject);
        if (box != null) box.SayNow(seen);

        // shot 2: from where you stand, looking up the shaft. It comes down.
        Vector3 land = Landing(centre.position, boss.transform.position);
        boss.StandUp();   // (out of shot: he is getting to his feet)
        Vector3 eye = playerHead.position;
        Vector3 top = land + Vector3.up * 30f;
        zt.SetPositionAndRotation(top, Quaternion.LookRotation(Flat(player.position - land)));
        za.CrossFadeInFixedTime("Upright", 0.1f, 0);
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(top + Vector3.down * 6f - eye));
        yield return new WaitForSeconds(0.35f);
        for (float t = 0f; t < 1.05f; t += Time.deltaTime)
        {
            float k = t / 1.05f;
            zt.position = Vector3.Lerp(top, land, k * k);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(zt.position + Vector3.up - eye), 1f - Mathf.Exp(-14f * Time.deltaTime));
            yield return null;
        }
        zt.position = land;
        Play(slam, 1f);
        za.CrossFadeInFixedTime("Scream", 0.05f, 0);
        z.Scream();
        yield return Shake(1.5f, 0.09f, land + Vector3.up * 1.3f);

        // back behind your own eyes, still frozen: it runs at you
        player.rotation = Quaternion.LookRotation(Flat(land - player.position));
        playerHead.localRotation = PitchTo(playerHead, land + Vector3.up * 1.3f);
        End(false);
        za.CrossFadeInFixedTime("Upright", 0.15f, 0);
        for (float t = 0f; t < 4f; t += Time.deltaTime)
        {
            Vector3 to = Flat(player.position - zt.position);
            if (to.magnitude < 2.6f) break;
            zt.position += to.normalized * 6.5f * Time.deltaTime;
            zt.rotation = Quaternion.LookRotation(to);
            za.SetFloat("Speed", 3.6f);
            AimAt(zt.position + Vector3.up * 1.4f, 9f);
            yield return null;
        }
        za.SetTrigger("Attack");

        // and Elias hits it from the side
        Transform e = boss.transform;
        Vector3 e0 = e.position, run = Flat(zt.position - e0);
        float runTime = Mathf.Clamp(run.magnitude / 15f, 0.16f, 0.5f);
        e.rotation = Quaternion.LookRotation(run);
        for (float t = 0f; t < runTime; t += Time.deltaTime)
        {
            Speed(4.5f);
            e.position = Vector3.Lerp(e0, zt.position - run.normalized * 0.7f, t / runTime);
            AimAt(zt.position + Vector3.up * 1.4f, 9f);
            yield return null;
        }
        Play(slam, 1f);
        Play(stinger, 0.7f);
        StartCoroutine(HeadShake(0.45f, 0.08f));
        KillerBoss.Attack(killerAnim);

        // he carries it off its feet, away from you
        Vector3 toYou = Flat(player.position - zt.position).normalized, perp = Vector3.Cross(Vector3.up, toYou);
        if (Vector3.Dot(perp, run) < 0f) perp = -perp;
        Vector3 z0 = zt.position, z1 = z0 + perp * 2.6f - toYou * 0.8f;
        Vector3 o = Flat(z1 - centre.position);
        if (o.magnitude > 4.6f) z1 = centre.position + o.normalized * 4.6f;
        z1.y = z0.y;
        Vector3 ea = e.position, eb = z1 - perp * 1.1f;
        for (float t = 0f; t < 0.32f; t += Time.deltaTime)
        {
            float k = t / 0.32f;
            zt.position = Vector3.Lerp(z0, z1, k);
            e.position = Vector3.Lerp(ea, eb, k);
            AimAt(zt.position + Vector3.up * 1.3f, 9f);
            yield return null;
        }
        Speed(0f);
        StartCoroutine(Struggle(z, za, e, bloodY));
        if (box != null) box.SayNow(save);
        for (float t = 0f; t < 2.4f; t += Time.deltaTime) { AimAt((zt.position + e.position) * 0.5f + Vector3.up * 1.3f, 5f); yield return null; }

        var fpc = player.GetComponent<FirstPersonController>();
        if (fpc != null) fpc.SetPitch(playerHead.localEulerAngles.x);
        for (int i = 0; i < lockDuring.Length; i++) if (lockDuring[i] != null && wasOn[i]) lockDuring[i].enabled = true;
        Playing = false;
    }

    // somewhere on the floor a few metres in front of you, and not on top of him
    Vector3 Landing(Vector3 centre, Vector3 elias)
    {
        Vector3 best = centre;
        float bestScore = float.MinValue;
        for (int a = 0; a < 12; a++)
        {
            Vector3 p = centre + Quaternion.Euler(0f, a * 30f, 0f) * Vector3.forward * 3.6f;
            float score = -Mathf.Abs(Flat(p - player.position).magnitude - 6.5f) + Mathf.Min(Flat(p - elias).magnitude, 4f) * 0.5f;
            if (score > bestScore) { bestScore = score; best = p; }
        }
        return best;
    }

    // the two of them locked together, turning, until the blood closes over them
    IEnumerator Struggle(Zombie z, Animator za, Transform e, System.Func<float> bloodY)
    {
        Transform zt = z.transform;
        Vector3 mid = (zt.position + e.position) * 0.5f;
        Vector3 d0 = Flat(zt.position - e.position).normalized;
        float ang0 = Mathf.Atan2(d0.x, d0.z) * Mathf.Rad2Deg, next = 0.4f, t = 0f;
        bool his = true;
        while (z != null && z.gameObject.activeSelf && e.gameObject.activeSelf && bloodY() < mid.y + 1.75f)
        {
            t += Time.deltaTime;
            Vector3 d = Quaternion.Euler(0f, ang0 + Mathf.Sin(t * 0.8f) * 40f, 0f) * Vector3.forward * 0.6f;
            Vector3 j = Flat(Random.insideUnitSphere) * 0.025f;
            e.SetPositionAndRotation(mid - d + j, Quaternion.LookRotation(d));
            Speed(0f);
            if (!z.IsDead)
            {
                zt.SetPositionAndRotation(mid + d - j, Quaternion.LookRotation(-d));
                za.SetFloat("Speed", 0f);
            }
            next -= Time.deltaTime;
            if (next <= 0f)
            {
                next = Random.Range(0.8f, 1.4f);
                if (his) KillerBoss.Attack(killerAnim); else if (!z.IsDead) za.SetTrigger("Attack");
                his = !his;
            }
            yield return null;
        }
        yield return new WaitForSeconds(1.2f);
        if (z != null) z.gameObject.SetActive(false);
        e.gameObject.SetActive(false);
    }

    // turn your own view (body yaw, head pitch) toward a point while you're frozen
    void AimAt(Vector3 p, float rate)
    {
        float k = 1f - Mathf.Exp(-rate * Time.deltaTime);
        player.rotation = Quaternion.Slerp(player.rotation, Quaternion.LookRotation(Flat(p - playerHead.position)), k);
        playerHead.localRotation = Quaternion.Slerp(playerHead.localRotation, PitchTo(playerHead, p), k);
    }

    // ---------------------------------------------------------------- shared (KesslerChase's door cutscene uses these too)

    public void Begin()
    {
        Playing = true;
        wasOn = new bool[lockDuring.Length];
        for (int i = 0; i < lockDuring.Length; i++)
            if (lockDuring[i] != null) { wasOn[i] = lockDuring[i].enabled; lockDuring[i].enabled = false; }
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        // your own arms / rifle hang off the player camera: keep them out of the shots
        hidden = player.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        foreach (var r in hidden) r.enabled = false;
        stinger = Sounds.Clip("Silo Complex/Killer sting", stinger != null ? stinger : ProceduralAudio.Thud());
        slam = Sounds.Clip("Silo Complex/Lift doors slam on him", slam != null ? slam : ProceduralAudio.Thud());
        cam.enabled = true;
        if (key != null) key.enabled = true;
        StartCoroutine(Bars(1f));
    }

    // (unlock = false: back to your own eyes, but still frozen; the caller hands control back)
    public void End(bool unlock = true)
    {
        cam.enabled = false;
        if (key != null) key.enabled = false;
        if (hidden != null) foreach (var r in hidden) if (r != null) r.enabled = true;
        StartCoroutine(Bars(0f));
        if (!unlock) return;
        for (int i = 0; i < lockDuring.Length; i++) if (lockDuring[i] != null && wasOn[i]) lockDuring[i].enabled = true;
        Playing = false;
    }

    IEnumerator Shake(float time, float amount, Vector3? lookAt)
    {
        Vector3 p0 = cam.transform.position;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float fade = 1f - t / time;
            cam.transform.position = p0 + Random.insideUnitSphere * amount * fade;
            if (lookAt.HasValue) cam.transform.rotation = Quaternion.LookRotation(lookAt.Value - cam.transform.position);
            yield return null;
        }
        cam.transform.position = p0;
    }

    IEnumerator Bars(float to)
    {
        float from = barTop != null ? barTop.sizeDelta.y / barHeight : 0f;
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            SetBars(Mathf.Lerp(from, to, t / 0.35f));
            yield return null;
        }
        SetBars(to);
    }

    void SetBars(float k)
    {
        foreach (var b in new[] { barTop, barBottom })
            if (b != null) b.sizeDelta = new Vector2(b.sizeDelta.x, barHeight * k);
    }

    void Speed(float s) { if (killerAnim != null && killerAnim.enabled) killerAnim.SetFloat("Speed", s); }
    void Play(AudioClip c, float v) { if (sfx != null && c != null) sfx.PlayOneShot(c, v); }
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v : Vector3.forward; }
}
