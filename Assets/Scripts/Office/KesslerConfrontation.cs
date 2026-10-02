using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// After the tape: back to Kessler's ghost. The lost turn into the damned when they're caught and own up to it.
//   Arm()        the CCTV flashback is over: his talk becomes the confrontation (confrontNodes)
//   (talk ends)  he admits it -> "YOU SHOULD BE THANKING ME" -> the blue ghost starts to burn red, shaking itself
//                apart, and the silo key falls out of his coat (user, 2026-10-02: there is no fight here any more).
//                You get a couple of seconds of it, then you're free: grab the key and get out through the silo
//                door while he's still turning. KesslerChase takes it from there (the tunnel).
//   too slow     stay for the whole of it (turnTime) and the damned thing lunges out and hunts you through the
//                rooms (line of sight, else your trail) until you leave. Fists don't put it down.
//   Leave()      you went through the silo door: the office is done with
public class KesslerConfrontation : MonoBehaviour
{
    public GhostNPC ghost;
    public GhostNode[] confrontNodes;
    public Zombie damned;                   // a zombie with its NavMesh AI stripped (no navmesh in the office): walked from here
    public Animator damnedAnim;             // "Speed" float, "Attack" trigger

    [Header("Player")]
    public Transform player;
    public Camera playerCam;
    public FirstPersonController fpc;
    public Behaviour[] lockDuring;
    public ScreenFader fader;
    public AudioSource sfx;

    [Header("The damned")]
    public float speed = 2.6f;
    public float runClipSpeed = 3.6f;       // the run clip's own pace (ZombieAI's)
    public float attackRange = 1.7f;
    public float attackCooldown = 1.6f;
    public int attackDamage = 10;
    public int health = 99999;              // (it isn't beaten here: you run)
    public float noticeRange = 14f;

    [Header("The turn: you run while it happens")]
    public Transform keyPickup;             // the silo key (an Interactable), switched off until it falls out of his coat
    public float watchTime = 2.6f;          // you're held looking at him this long, then you can move
    public float turnTime = 18f;            // how long the whole change takes: your time to grab the key and go

    [Header("Lines ('|' splits)")]
    [TextArea] public string turnLine = "Kessler: Everything I did, I did for you. For you and your brother. To make us RICH.|Kessler: YOU SHOULD BE THANKING ME!";
    [TextArea] public string keyLine = "He said it out loud-- and the city's TAKING him.|Something fell out of his coat. Keys. The SILO key.|Grab it and GO, before he's finished turning!";
    [TextArea] public string afterTurnLine = "Too slow-- he's TURNED. The silo door. NOW!";
    [TextArea] public string defeatedLine = "";
    public UnityEvent onDefeated = new UnityEvent();

    private bool armed, turned, hunting, done;
    private Vector3 turnSpot;
    private Quaternion turnRot;
    private float attackTimer, trailTimer;
    private readonly List<Vector3> trail = new List<Vector3>();

    void Start()
    {
        if (damned != null) damned.gameObject.SetActive(false);
    }

    public void Arm()
    {
        if (armed) return;
        armed = true;
        ghost.Replace(confrontNodes);
        ghost.onFinished.AddListener(() => { if (!turned) StartCoroutine(Turn()); });
    }

    // ---------------------------------------------------------------- the turn

    IEnumerator Turn()
    {
        turned = true;
        while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) yield return null;   // let his last answer land
        Lock(true);
        ghost.enabled = false;   // its own flicker would fight ours
        foreach (var it in ghost.GetComponents<Interactable>()) it.enabled = false;
        foreach (var c in ghost.GetComponents<Collider>()) c.enabled = false;
        Transform body = ghost.body != null ? ghost.body : ghost.transform;
        Vector3 head = ghost.transform.position + Vector3.up * 1.6f;
        yield return LookAt(head, 0.6f);

        var renderers = body.GetComponentsInChildren<Renderer>();
        var block = new MaterialPropertyBlock();
        var glow = ghost.GetComponentInChildren<Light>();
        var whisper = ghost.GetComponent<AudioSource>();
        Vector3 body0 = body.localPosition;
        Color blue = renderers.Length > 0 ? renderers[0].sharedMaterial.GetColor("_BaseColor") : Color.cyan;

        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(turnLine);
        Play(Sounds.Clip("Office/Kessler turning (whisper)", null) ?? ProceduralAudio.Whisper(3f), 0.8f * Sounds.Volume("Office/Kessler turning (whisper)"));
        bool free = false;
        for (float t = 0f; t < turnTime; t += Time.deltaTime)
        {
            // blue -> blood red, ever more solid, shaking itself apart
            float k = Mathf.Clamp01(t / turnTime);
            Color c = Color.Lerp(blue, new Color(0.8f, 0.03f, 0.02f), k);
            c.a = Mathf.Lerp(0.4f, 0.95f, k) * (Random.value < 0.08f ? 0.2f : 1f);
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", c);
                r.SetPropertyBlock(block);
            }
            body.localPosition = body0 + Random.insideUnitSphere * (0.02f + 0.07f * k);
            if (glow != null) { glow.color = Color.Lerp(new Color(0.5f, 0.85f, 1f), Color.red, k); glow.intensity = 0.8f + 4f * k; }
            if (whisper != null) whisper.pitch = Mathf.Lerp(0.9f, 0.45f, k);
            if (!free && t >= watchTime)
            {
                // you've seen enough: the key drops, and you can move
                free = true;
                DropKey();
                Lock(false);
                if (DialogueBox.Instance != null) DialogueBox.Instance.Say(keyLine);
            }
            if (!free) Aim(head);
            yield return null;
        }

        // still here: the lost becomes the damned, and it comes for you
        Play(Sounds.Clip("Office/Kessler becomes the damned", null) ?? ProceduralAudio.Thud(), Sounds.Volume("Office/Kessler becomes the damned"));
        if (fader != null) yield return fader.FadeTo(1f, 0.08f);
        turnSpot = ghost.transform.position;
        turnRot = Quaternion.LookRotation(Flat(player.position - turnSpot));
        ghost.gameObject.SetActive(false);
        damned.transform.SetPositionAndRotation(turnSpot, turnRot);
        damned.health = health;
        damned.gameObject.SetActive(true);
        yield return new WaitForSeconds(0.25f);
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 0.2f));
        if (damnedAnim != null) damnedAnim.SetTrigger("Attack");
        Play(Sounds.Clip("Office/Kessler lunges", null) ?? ProceduralAudio.Thud(), Sounds.Volume("Office/Kessler lunges"));
        yield return Shake(0.5f, 0.06f);

        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(afterTurnLine);
        MusicManager.Area("Kessler Fight");
        Lock(false);
        hunting = true;
    }

    // the silo key, on the floor between him and you, lit so you can't miss it
    void DropKey()
    {
        if (keyPickup == null) return;
        Vector3 at = ghost.transform.position + Flat(player.position - ghost.transform.position).normalized * 0.9f;
        if (Physics.Raycast(at + Vector3.up, Vector3.down, out RaycastHit floor, 3f, ~0, QueryTriggerInteraction.Ignore)) at.y = floor.point.y;
        keyPickup.SetParent(transform, true);   // (out of the safe it used to sit in)
        keyPickup.position = at + Vector3.up * 0.05f;
        keyPickup.gameObject.SetActive(true);
        Play(ProceduralAudio.Click(), 0.8f);
    }

    // through the silo door (KesslerChase.Arrive): whatever state he was in here, it's over
    public void Leave()
    {
        StopAllCoroutines();
        Lock(false);
        hunting = false;
        done = true;
        if (ghost != null) ghost.gameObject.SetActive(false);
        if (damned != null) damned.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- too slow: it hunts you

    void Update()
    {
        if (!hunting || done || damned == null) return;
        if (damned.IsDead) { StartCoroutine(Defeated()); return; }

        Transform z = damned.transform;
        Vector3 eye = z.position + Vector3.up * 1.5f;
        Vector3 toPlayer = player.position - z.position;
        bool sees = toPlayer.magnitude < noticeRange && Clear(eye, player.position + Vector3.up * 0.5f);

        // keep your trail while it can't see you: it follows you through doorways
        trailTimer -= Time.deltaTime;
        if (sees) trail.Clear();
        else if (trailTimer <= 0f) { trailTimer = 0.3f; trail.Add(player.position); if (trail.Count > 60) trail.RemoveAt(0); }

        attackTimer -= Time.deltaTime;
        if (sees && Flat(toPlayer).magnitude <= attackRange)
        {
            Speed(0f);
            z.rotation = Quaternion.Slerp(z.rotation, Quaternion.LookRotation(Flat(toPlayer)), 10f * Time.deltaTime);
            if (attackTimer <= 0f)
            {
                attackTimer = attackCooldown;
                if (damnedAnim != null) damnedAnim.SetTrigger("Attack");
                StartCoroutine(HitSoon());
            }
            return;
        }

        Vector3 target;
        if (sees) target = player.position;
        else if (trail.Count > 0)
        {
            target = trail[0];
            if (Flat(target - z.position).magnitude < 0.5f) { trail.RemoveAt(0); return; }
        }
        else { Speed(0f); return; }   // lost you completely: wait for you to come back into view

        Vector3 dir = Flat(target - z.position).normalized;
        Vector3 step = dir * speed * Time.deltaTime;
        if (Free(z.position + Vector3.up * 0.8f, dir)) z.position += step;
        z.rotation = Quaternion.Slerp(z.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
        Speed(speed);
    }

    // nothing in the way but its own body or the player (who it's allowed to walk into)
    bool Free(Vector3 from, Vector3 dir)
    {
        foreach (var h in Physics.RaycastAll(from, dir, 0.5f, ~0, QueryTriggerInteraction.Ignore))
            if (!h.collider.transform.IsChildOf(damned.transform) && !h.collider.transform.IsChildOf(player)) return false;
        return true;
    }

    bool Clear(Vector3 from, Vector3 to)
    {
        foreach (var h in Physics.RaycastAll(from, to - from, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
            if (!h.collider.transform.IsChildOf(damned.transform) && !h.collider.transform.IsChildOf(player)) return false;
        return true;
    }

    IEnumerator HitSoon()
    {
        yield return new WaitForSeconds(0.4f);
        if (damned == null || damned.IsDead) yield break;
        if (Flat(player.position - damned.transform.position).magnitude > attackRange + 0.4f) yield break;   // you stepped back in time
        var ph = player.GetComponentInParent<PlayerHealth>();
        if (ph != null) ph.TakeDamage(attackDamage);
    }

    IEnumerator Defeated()
    {
        done = true;
        hunting = false;
        yield return new WaitForSeconds(1.5f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.Say(defeatedLine);
        MusicManager.Area("Office");
        onDefeated.Invoke();
    }

    // a checkpoint respawn mid-fight: it's back where it turned, whole again, waiting
    public void ResetFight()
    {
        if (!turned || done || damned == null) return;
        StopAllCoroutines();
        Lock(false);
        trail.Clear();
        damned.transform.SetPositionAndRotation(turnSpot, turnRot);
        damned.health = health;
        Speed(0f);
        hunting = true;
    }

    // ---------------------------------------------------------------- helpers

    void Lock(bool on)
    {
        foreach (var b in lockDuring) if (b != null) b.enabled = !on;
        if (on && player.TryGetComponent(out Rigidbody rb) && !rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    IEnumerator LookAt(Vector3 point, float time)
    {
        Quaternion y0 = player.rotation, p0 = playerCam.transform.localRotation;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            Vector3 d = point - playerCam.transform.position;
            player.rotation = Quaternion.Slerp(y0, Quaternion.LookRotation(Flat(d)), k);
            playerCam.transform.localRotation = Quaternion.Slerp(p0, Pitch(d), k);
            yield return null;
        }
        Aim(point);
    }

    void Aim(Vector3 point)
    {
        Vector3 d = point - playerCam.transform.position;
        player.rotation = Quaternion.LookRotation(Flat(d));
        playerCam.transform.localRotation = Pitch(d);
        if (fpc != null) fpc.SetPitch(playerCam.transform.localEulerAngles.x);
    }

    static Quaternion Pitch(Vector3 d) => Quaternion.Euler(Mathf.Clamp(-Mathf.Atan2(d.y, Flat(d).magnitude) * Mathf.Rad2Deg, -80f, 80f), 0f, 0f);

    IEnumerator Shake(float time, float amount)
    {
        Vector3 p0 = playerCam.transform.localPosition;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            playerCam.transform.localPosition = p0 + Random.insideUnitSphere * amount * (1f - t / time);
            yield return null;
        }
        playerCam.transform.localPosition = p0;
    }

    void Speed(float v)
    {
        if (damnedAnim == null) return;
        damnedAnim.SetFloat("Speed", v, 0.15f, Time.deltaTime);
        damnedAnim.speed = v < 0.05f ? 1f : Mathf.Clamp(v / runClipSpeed, 0.8f, 2.2f);
    }

    void Play(AudioClip c, float v) { if (sfx != null && c != null) sfx.PlayOneShot(c, v); }
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v : Vector3.forward; }
}
