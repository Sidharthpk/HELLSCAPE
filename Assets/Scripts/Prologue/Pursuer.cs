using System.Collections.Generic;
using UnityEngine;

// Elias, coming for you (the prologue's new flat sequence). On the same object as FleeingKiller, which it switches off:
//   Approach(target)   walks straight at you, footstep by footstep, and stops 1.3 m away
//   Chase(target)      follows the exact path you walked (a trail of your footprints): up stairs, round corners,
//                      through the doors you burst. He is a little slower than your sprint and quicker than your walk,
//                      so you have to run; stop for a couple of seconds and he has you (Caught)
//   Hold()             stands still
public class Pursuer : MonoBehaviour
{
    public Transform target;
    public FleeingKiller flee;
    public Animator animator;               // PrologueKiller.controller: Speed, "Stab"
    public float approachSpeed = 1.7f;
    public float chaseSpeed = 4.4f;
    public float catchDistance = 1.05f;
    public float grace = 2f;                // seconds after Chase() starts in which he can't catch you

    enum Mode { Hold, Approach, Chase }
    Mode mode;
    readonly List<Vector3> trail = new List<Vector3>();
    int at;
    float stepTimer, chaseStarted, closeFor;

    public bool Caught { get; private set; }
    public Vector3 Chest => transform.position + Vector3.up * 1.45f;
    public float Distance => target != null ? Flat(target.position - transform.position).magnitude : 999f;

    void Awake() { if (flee == null) flee = GetComponent<FleeingKiller>(); }

    public void Place(Vector3 feet, Vector3 look)
    {
        if (flee != null) flee.enabled = false;
        gameObject.SetActive(true);
        transform.position = feet;
        look.y = 0f;
        if (look.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(look);
    }

    public void Approach(Transform who) { target = who; mode = Mode.Approach; }
    public void Hold() { mode = Mode.Hold; Speed(0f); }

    // (via: somewhere to go first, a doorway between him and you)
    public void Chase(Transform who, Vector3? via = null)
    {
        target = who;
        trail.Clear();
        if (via != null) trail.Add(via.Value);
        trail.Add(who.position);
        at = 0;
        Caught = false; closeFor = 0f;
        chaseStarted = Time.time;
        mode = Mode.Chase;
    }

    public void StabAnim()
    {
        if (animator != null && animator.isActiveAndEnabled && animator.HasState(0, Animator.StringToHash("Stab"))) animator.CrossFadeInFixedTime("Stab", 0.06f);
    }
    public void Locomotion()
    {
        if (animator != null && animator.isActiveAndEnabled && animator.HasState(0, Animator.StringToHash("Locomotion"))) animator.CrossFadeInFixedTime("Locomotion", 0.15f);
    }

    void Update()
    {
        if (target == null) return;
        switch (mode)
        {
            case Mode.Approach: Walk(target.position, approachSpeed, 1.3f); break;
            case Mode.Chase: Follow(); break;
            default: Speed(0f); break;
        }
    }

    void Walk(Vector3 to, float speed, float stopAt)
    {
        Vector3 d = Flat(to - transform.position);
        if (d.magnitude <= stopAt) { Speed(0f); return; }
        Step(d.normalized, speed);
    }

    void Follow()
    {
        if (trail.Count == 0 || Vector3.Distance(trail[trail.Count - 1], target.position) > .35f) trail.Add(target.position);
        float dist = Distance;
        if (at < trail.Count)
        {
            Vector3 p = trail[at];
            Vector3 d = Flat(p - transform.position);
            if (d.magnitude < .35f) at++;
            else
            {
                // he never falls far behind, and never quite catches a runner
                float speed = chaseSpeed * (dist > 9f ? 1.35f : dist < 2.2f ? .9f : 1f);
                Step(d.normalized, speed);
            }
        }
        else Speed(0f);
        // caught: right on you and you've stopped running
        // (flat distance alone caught you from the flight above/below him on the stairs: they must be on the same level)
        bool sameLevel = Mathf.Abs(target.position.y - transform.position.y) < 1.1f;
        if (Time.time - chaseStarted > grace && dist < catchDistance && sameLevel) closeFor += Time.deltaTime; else closeFor = Mathf.Max(0f, closeFor - Time.deltaTime);
        if (closeFor > .5f) Caught = true;
    }

    void Step(Vector3 dir, float speed)
    {
        Vector3 next = transform.position + dir * speed * Time.deltaTime;
        // feet on whatever is under him: stairs, landings
        if (Physics.Raycast(next + Vector3.up * 1.2f, Vector3.down, out var h, 2.6f, ~0, QueryTriggerInteraction.Ignore) && !h.collider.transform.IsChildOf(target) && !h.collider.transform.IsChildOf(transform))
            next.y = Mathf.MoveTowards(transform.position.y, h.point.y, 8f * Time.deltaTime);
        transform.position = next;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
        Speed(speed);
        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f && flee != null && flee.steps != null && flee.stepClips != null && flee.stepClips.Length > 0)
        {
            stepTimer = Mathf.Lerp(.75f, .3f, speed / 6f);
            flee.steps.pitch = Random.Range(.85f, 1f);
            var clips = Sounds.Clips("Prologue/Killer footsteps", flee.stepClips);   // (the Sound Library may hand back a different number)
            if (clips != null && clips.Length > 0) flee.steps.PlayOneShot(clips[Random.Range(0, clips.Length)], Sounds.Volume("Prologue/Killer footsteps"));
        }
    }

    void Speed(float s)
    {
        if (animator != null && animator.isActiveAndEnabled) animator.SetFloat("Speed", s);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
