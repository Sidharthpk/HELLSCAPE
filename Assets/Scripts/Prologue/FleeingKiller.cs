using UnityEngine;

// The killer in the prologue. He never fights: he runs a fixed route (out of the flat, up the stairwell, onto
// the roof) and keeps just ahead of you, so you can always see him and never quite catch him.
//   Sneak()  - creep along the first leg (from his hiding place to the flat door), trying not to be heard
//   Flee()   - run the rest: faster when you're on his heels, stopping to look back when you fall behind
// He flings open any door on the route as he reaches it. At the last point he turns and faces you (Cornered).
public class FleeingKiller : MonoBehaviour
{
    public Transform player;
    public Animator animator;               // Speed: 0 idle, ~1.3 walk, 4.5 run
    public Vector3[] route;                 // feet positions
    public int sneakUntil = 3;              // Sneak() stops at this route point (the flat door)

    [Header("Speeds")]
    public float sneakSpeed = 1.1f;
    public float runSpeed = 5.2f;           // the player walks 5, sprints 7
    public float panicSpeed = 7.4f;         // you're right behind him
    public float panicDistance = 4.5f;
    public float waitDistance = 13f;        // this far ahead he stops and looks back...
    public float resumeDistance = 8f;       // ...until you're this close again

    [Header("Doors on the route")]
    public PrologueDoor[] doors;
    public float doorReach = 1.8f;

    [Header("Footsteps")]
    public AudioSource steps;               // 3D, on him
    public AudioClip[] stepClips;

    enum Mode { Still, Sneak, Flee }
    private Mode mode;
    private int next;
    private bool waiting;
    private float stepTimer;

    public bool Cornered { get; private set; }
    public bool AtFlatDoor => mode == Mode.Still && next > sneakUntil && !Cornered;
    public Vector3 Chest => transform.position + Vector3.up * 1.45f;

    public void Place()
    {
        transform.position = route[0];
        if (route.Length > 1) Face(route[1] - route[0]);
        next = 1;
        gameObject.SetActive(true);
    }

    public void Sneak() { if (!gameObject.activeSelf) Place(); mode = Mode.Sneak; }

    public void Flee()
    {
        if (!gameObject.activeSelf) Place();
        mode = Mode.Flee;
    }

    public void Stop() { mode = Mode.Still; Speed(0f); }

    void Update()
    {
        if (mode == Mode.Still || next >= route.Length)
        {
            Speed(0f);
            if (Cornered && player != null) Face(player.position - transform.position, 5f);   // backed up to the edge, watching you come
            return;
        }

        float toPlayer = player != null ? Vector3.Distance(transform.position, player.position) : 0f;
        float speed;
        if (mode == Mode.Sneak) speed = sneakSpeed;
        else
        {
            if (waiting && toPlayer < resumeDistance) waiting = false;
            else if (!waiting && toPlayer > waitDistance) waiting = true;
            speed = waiting ? 0f : toPlayer < panicDistance ? panicSpeed : runSpeed;
        }

        if (speed <= 0f)
        {
            // looking back down the stairs at you
            if (player != null) Face(player.position - transform.position, 6f);
            Speed(0f);
            return;
        }

        Vector3 target = route[next];
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        Face(target - transform.position, 12f);
        Speed(speed);
        Footsteps(speed);

        foreach (var d in doors)
            if (d != null && !d.IsOpen && Vector3.Distance(d.transform.position, transform.position + Vector3.up) < doorReach)
                d.Burst();

        if ((transform.position - target).sqrMagnitude < 0.0025f)
        {
            next++;
            if (mode == Mode.Sneak && next > sneakUntil) Stop();
            else if (next >= route.Length)
            {
                Stop();
                Cornered = true;
            }
        }
    }

    void Footsteps(float speed)
    {
        if (steps == null || stepClips == null || stepClips.Length == 0) return;
        var clips = Sounds.Clips("Prologue/Killer footsteps", stepClips);
        stepTimer -= Time.deltaTime;
        if (stepTimer > 0f) return;
        stepTimer = Mathf.Lerp(0.75f, 0.3f, speed / panicSpeed);
        steps.pitch = Random.Range(0.85f, 1f);
        steps.PlayOneShot(clips[Random.Range(0, clips.Length)], (mode == Mode.Sneak ? 0.45f : 1f) * Sounds.Volume("Prologue/Killer footsteps"));
    }

    void Face(Vector3 dir, float rate = 0f)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        var q = Quaternion.LookRotation(dir);
        transform.rotation = rate <= 0f ? q : Quaternion.Slerp(transform.rotation, q, rate * Time.deltaTime);
    }

    void Speed(float s)
    {
        if (animator != null && animator.isActiveAndEnabled) animator.SetFloat("Speed", s);
    }
}
