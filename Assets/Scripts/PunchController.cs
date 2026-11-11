using UnityEngine;

// The fists (Fists 2025, FistsController: Tools > HellScape > Fists 2025). Idle / walk / sprint blend on how fast you
// move, jabs on the left click, a fidget when you stand still a while, a reach when you use something (Take) and the
// Inspect clip for looking at the mark on your palms (EndingSequence, PalmLight).
public class PunchController : MonoBehaviour
{
    public Animator armsAnimator;
    public Transform cam;
    public float cooldown = 0.55f;          // seconds between jabs: a fist has to come back before the next goes out
    public float jabSpeed = 1f;             // the jab clips' playback (1 = as authored)
    public float fidgetAfter = 9f;          // standing still this long: the hands fidget
    private bool useLeftNext = true;
    private float nextPunch, stillFor;
    private Rigidbody body;
    private FirstPersonController fpc;
    private bool blocking;
    static readonly int SpeedId = Animator.StringToHash("Speed");

    void Update()
    {
        if (GunController.Equipped) return;
        // (back to the normal pace once the jab is over, unless something else has taken the arms: PalmLight pauses them)
        if (Time.time >= nextPunch && Mathf.Approximately(armsAnimator.speed, jabSpeed)) armsAnimator.speed = 1f;
        Move();
        Guard();
        if (blocking) return;   // crouched: hands up in the block, no jabs
        if (Input.GetMouseButtonDown(0) && Time.time >= nextPunch)
        {
            nextPunch = Time.time + cooldown;
            armsAnimator.speed = jabSpeed;
            Punch();
        }
    }

    // idle / walk / sprint follow the body's own speed (FistsController's blend tree is in m/s)
    void Move()
    {
        if (body == null) body = GetComponentInParent<Rigidbody>();
        Vector3 v = body != null ? body.linearVelocity : Vector3.zero; v.y = 0f;
        float speed = v.magnitude;
        armsAnimator.SetFloat(SpeedId, speed, 0.12f, Time.deltaTime);
        stillFor = speed < 0.2f && armsAnimator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion") ? stillFor + Time.deltaTime : 0f;
        if (stillFor > fidgetAfter) { stillFor = -Random.Range(0f, fidgetAfter); armsAnimator.CrossFadeInFixedTime("Fidget", 0.25f); }
    }

    // crouching brings the fists up into the block (Block / BlockLoop / BlockStop: FistsBuilder); standing lowers them
    void Guard()
    {
        if (fpc == null) fpc = GetComponentInParent<FirstPersonController>();
        bool want = fpc != null && fpc.IsCrouched;
        if (want != blocking)
        {
            blocking = want;
            armsAnimator.CrossFadeInFixedTime(want ? "Block" : "BlockStop", 0.12f);
        }
        // something else took the arms meanwhile (a reach, the palm light): back into the block once it lets go
        else if (blocking && armsAnimator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion") && !armsAnimator.IsInTransition(0))
            armsAnimator.CrossFadeInFixedTime("BlockLoop", 0.15f);
    }

    void OnDisable()
    {
        blocking = false;
        if (armsAnimator == null) return;
        if (Mathf.Approximately(armsAnimator.speed, jabSpeed)) armsAnimator.speed = 1f;
        armsAnimator.SetFloat(SpeedId, 0f);   // (switched off mid-run, e.g. a cutscene: don't keep sprinting)
    }

    // you use something (PlayerInteractor): a hand reaches out for it
    public void Take()
    {
        if (!isActiveAndEnabled || armsAnimator == null || armsAnimator.speed == 0f) return;
        armsAnimator.CrossFadeInFixedTime("Take", 0.1f);
    }

    // fists down, out of the way (lying in the street after the fall): the first frame of the draw, held
    public void HoldDown()
    {
        if (armsAnimator == null) return;
        armsAnimator.Play("GuardDraw", 0, 0f);
        armsAnimator.Update(0f);
        armsAnimator.speed = 0f;
    }

    // back on your feet: fists come up into the guard
    public void Draw()
    {
        if (armsAnimator == null) return;
        armsAnimator.speed = 1f;
        armsAnimator.Play("GuardDraw", 0, 0f);
    }

    void Punch()
    {
        // one trigger at a time: a leftover one would fire a second jab on its own
        armsAnimator.ResetTrigger(useLeftNext ? "JabR" : "JabL");
        armsAnimator.SetTrigger(useLeftNext ? "JabL" : "JabR");
        useLeftNext = !useLeftNext;

        // nearest hit that isn't our own body (the camera sits at the edge of the player's capsule)
        var hits = Physics.SphereCastAll(cam.position, 0.4f, cam.forward, 1.5f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(cam.root)) continue;

            GateButton button = hit.collider.GetComponentInParent<GateButton>();
            if (button != null)
            {
                button.OnPunched();
                return;
            }

            DemonBoss demon = hit.collider.GetComponentInParent<DemonBoss>();
            if (demon != null)
            {
                demon.TakeDamage(15f, hit.point, cam.forward, hit.collider);
                return;
            }

            Zombie z = hit.collider.GetComponentInParent<Zombie>();
            if (z != null)
                z.TakeDamage(15, hit.point, cam.forward * 300f);
            return;
        }
    }
}
