using System.Collections;
using UnityEngine;

// The prologue's one cheap shot: on the long straight back to the depot a grandma on a Vespa shoots out of the
// building line, over the zebra crossing right across your bonnet, and is gone on the other side.
// This object sits in the middle of that crossing, facing the way you drive. Fires once, set off by the van's
// own speed so that she is in your lane a few metres ahead of you however fast you come. She has no collider,
// so the worst case is a ghost, not a wreck.
public class GrandmaScare : MonoBehaviour
{
    public PrologueVan van;
    public Transform grandma;               // the rider (child, switched off until it happens); +z is the scooter's nose
    public AudioSource sound;               // on her: engine loop + the horn
    public float minVanSpeed = 5f;          // m/s: any slower and it isn't a scare
    public float speed = 14f;               // hers
    public float clearance = 5f;            // road left between your bonnet and her as she cuts through your lane
    public float maxSide = 16f;             // how far out she starts/ends when there's no wall to come out of
    public float slowMo = 0.4f;             // the moment she's in front of you drags
    public float slowMoTime = 0.6f;         // real seconds
    [Header("The shot: from behind the van she'd be a speck, so it cuts to the bonnet for the second she crosses")]
    public Camera scareCam;                 // off until then; drawn over the van's camera
    public Vector3 camOnVan = new Vector3(0f, 1.5f, 2.2f);   // van space: over the bonnet
    public float cutLead = 6f;              // cut when she's this far from your lane
    public float cutBack = 4.5f;            // and back once she's this far past it
    [Range(0f, 1f)] public float follow = 0.55f;   // how much the shot turns after her
    [TextArea] public string lines = "WHOA--! Where did SHE come from?!|\"Watch the road, sonny!\"";

    public bool Fired { get; private set; }

    private Vector3 side;                   // from the crossing's middle towards where she comes out
    private float near, far;                // metres from the middle to her start and to where she vanishes

    void Start()
    {
        if (grandma != null) grandma.gameObject.SetActive(false);
        // out of whichever building line is nearer, so she's on you sooner
        Vector3 c = transform.position + Vector3.up * 1.2f;
        float r = Wall(c, transform.right), l = Wall(c, -transform.right);
        side = r <= l ? transform.right : -transform.right;
        near = Mathf.Min(r, l);
        far = Mathf.Max(r, l);
    }

    void Update()
    {
        if (Fired || van == null || !van.InVan || van.body == null) return;
        Vector3 vel = Flat(van.body.linearVelocity);
        float v = vel.magnitude;
        if (v < minVanSpeed || Vector3.Dot(vel / v, transform.forward) < 0.8f) return;
        Vector3 to = Flat(transform.position - van.transform.position);
        float ahead = Vector3.Dot(to, transform.forward);                 // road left to the crossing
        if (ahead > 90f || Mathf.Abs(Vector3.Dot(to, transform.right)) > 10f) return;
        // she needs this long to reach your lane: go when you're that far out, plus the clearance
        float reach = (near + Vector3.Dot(to, side)) / speed;
        if (ahead > v * reach + clearance || ahead < v * reach + 1.5f) return;   // (too late = she'd come through the van)
        Fired = true;
        StartCoroutine(Cross());
    }

    IEnumerator Cross()
    {
        Vector3 from = transform.position + side * near;
        float total = near + far;
        grandma.gameObject.SetActive(true);
        Quaternion heading = Quaternion.LookRotation(-side);
        if (sound != null)
        {
            sound.clip = Sounds.Clip("Prologue/Grandma scooter", ProceduralAudio.ScooterEngine());
            sound.loop = true;
            sound.volume = Sounds.Volume("Prologue/Grandma scooter");
            sound.Play();
        }

        int shot = 0;   // 0 not yet, 1 on the bonnet, 2 back behind the van
        for (float s = 0f; s < total; s += speed * Time.deltaTime)
        {
            Vector3 p = from - side * s;
            p.y = Ground(p);
            // a scooter flat out on small wheels: it weaves a little
            grandma.SetPositionAndRotation(p, heading * Quaternion.Euler(0f, Mathf.Sin(s * 1.9f) * 3f, Mathf.Sin(s * 2.7f) * 4f));
            float toLane = Vector3.Dot(Flat(p - van.transform.position), side);   // + while she's still coming at your lane
            if (shot == 0 && toLane < cutLead)
            {
                // the cut, the horn and the world slowing all land on the same frame
                shot = 1;
                Shot(true);
                Sounds.OneShot(sound, "Prologue/Grandma horn", ProceduralAudio.ScooterHorn(), 1f);
                if (DialogueBox.Instance != null && !string.IsNullOrEmpty(lines)) DialogueBox.Instance.SayNow(lines);
                StartCoroutine(Drag());
            }
            else if (shot == 1 && toLane < -cutBack) { shot = 2; Shot(false); }
            yield return null;
        }
        Shot(false);
        if (sound != null) sound.Stop();
        grandma.gameObject.SetActive(false);
    }

    void Shot(bool on)
    {
        if (scareCam == null || scareCam.enabled == on) return;
        scareCam.enabled = on;
        CutsceneHUD.Hide(on);
        if (on) LateUpdate();
    }

    void LateUpdate()
    {
        if (scareCam == null || !scareCam.enabled) return;
        Transform v = van.transform;
        Vector3 at = v.TransformPoint(camOnVan);
        Quaternion ahead = Quaternion.LookRotation(Flat(v.forward));
        Quaternion atHer = Quaternion.LookRotation(grandma.position + Vector3.up * 0.9f - at);
        scareCam.transform.SetPositionAndRotation(at, Quaternion.Slerp(ahead, atHer, follow));
    }

    IEnumerator Drag()
    {
        Time.timeScale = slowMo;
        yield return new WaitForSecondsRealtime(slowMoTime);
        if (!PauseMenu.Paused) Time.timeScale = 1f;
    }

    // metres to the nearest thing that isn't the van, a little short of it (so she starts just clear of the wall)
    float Wall(Vector3 from, Vector3 dir)
    {
        float best = maxSide + 0.8f;
        foreach (var h in Physics.RaycastAll(from, dir, maxSide + 0.8f, ~0, QueryTriggerInteraction.Ignore))
            if (!Ignored(h.collider) && h.distance < best) best = h.distance;
        return Mathf.Max(3f, best - 0.8f);
    }

    float Ground(Vector3 p)
    {
        float y = float.MinValue;
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, transform.position.y + 1.5f, p.z), Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
            if (!Ignored(h.collider) && h.point.y > y) y = h.point.y;
        return y > float.MinValue ? y : transform.position.y;
    }

    // things that move through the street aren't walls or ground
    bool Ignored(Collider c) =>
        (van != null && c.transform.IsChildOf(van.transform)) || c.GetComponentInParent<PavementWalker>() != null || c.GetComponentInParent<LoopDriver>() != null;

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position - transform.right * maxSide + Vector3.up, transform.position + transform.right * maxSide + Vector3.up);
        Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 8f);
    }
}
