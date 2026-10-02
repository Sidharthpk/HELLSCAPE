using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// The masked killer, fought on the silo floor. Walks at you, lunges when close, gets faster when hurt.
// At zero health he drops to his knees instead of dying: SiloEncounter decides what happens next.
public class KillerBoss : MonoBehaviour
{
    public Transform player;
    public Animator animator;               // humanoid, Killer.controller ("Speed" float, "Attack" trigger)
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

    [Header("UI / Sound")]
    public RectTransform healthFill;        // anchored left, scaled on x
    public GameObject healthBar;
    public AudioSource voice;
    public AudioClip hitClip;
    public UnityEvent onDefeated = new UnityEvent();

    private float health;
    private float cooldown;
    private bool active, defeated;

    public bool Defeated => defeated;

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
        health = maxHealth;
        cooldown = 0f;
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
        if (healthBar != null) healthBar.SetActive(true);
    }

    void Update()
    {
        if (!active || player == null) return;   // ChaseKiller animates him before the fight

        Vector3 to = player.position - transform.position; to.y = 0f;
        float dist = to.magnitude;
        if (dist > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 5f * Time.deltaTime);

        cooldown -= Time.deltaTime;
        if (dist <= attackRange)
        {
            SetSpeed(0f);
            if (cooldown <= 0f) StartCoroutine(Lunge());
            return;
        }

        float speed = health < maxHealth * 0.5f ? enragedSpeed : walkSpeed;
        Vector3 next = transform.position + to.normalized * speed * Time.deltaTime;
        if (arenaCenter != null)
        {
            Vector3 off = next - arenaCenter.position; off.y = 0f;
            if (off.magnitude > arenaRadius) next = arenaCenter.position + off.normalized * arenaRadius + Vector3.up * (next.y - arenaCenter.position.y);
        }
        transform.position = next;
        SetSpeed(speed);
    }

    IEnumerator Lunge()
    {
        cooldown = attackCooldown;
        Attack(animator);
        yield return new WaitForSeconds(Mathf.Max(0f, hitDelay - 0.18f));
        Vector3 start = transform.position;
        Vector3 end = start + transform.forward * lungeDistance;
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
    }

    public void TakeDamage(float dmg, Vector3 point, Vector3 dir)
    {
        if (defeated) return;
        BloodFx.Spray(point, -dir, 18, 3f);
        var grunt = Sounds.Clip("Silo Fight/Killer hurt", hitClip);
        if (voice != null && grunt != null && !voice.isPlaying) voice.PlayOneShot(grunt, Sounds.Volume("Silo Fight/Killer hurt"));

        // shooting him wakes him up even if the scripted beat hasn't yet
        if (!active) Activate();

        health -= dmg;
        if (healthFill != null) healthFill.localScale = new Vector3(Mathf.Clamp01(health / maxHealth), 1f, 1f);
        if (health > 0f) return;

        defeated = true;
        active = false;
        SetSpeed(0f);
        if (healthBar != null) healthBar.SetActive(false);
        StartCoroutine(Tip(25f, -0.55f, 0.8f));   // drops to his knees
        onDefeated.Invoke();
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

    void SetSpeed(float s) { if (animator != null && animator.enabled) animator.SetFloat("Speed", s); }

    // plays the swing if the controller has one
    public static void Attack(Animator a)
    {
        if (a == null || !a.enabled || a.runtimeAnimatorController == null) return;
        foreach (var p in a.parameters)
            if (p.name == "Attack" && p.type == AnimatorControllerParameterType.Trigger) { a.SetTrigger("Attack"); return; }
    }
}
