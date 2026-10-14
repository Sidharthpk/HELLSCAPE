using UnityEngine;

// A body hung from the tunnel roof (this object is the hook: the rope and the bag hang below it). It is a heavy
// rigidbody on a ball joint at the hook: it blocks you, and gives slowly when you lean on it. (physical = false: the
// old fake swing instead.) Every third one also drops a pile of bodies across the floor under it that you have to
// jump up onto - Kessler is right behind you.
public class HangingBody : MonoBehaviour
{
    public float length = 5f;               // hook to the middle of the body
    public float reach = 0.9f;              // how close something has to pass to set it going
    public float shove = 1.4f;              // how hard (x the passer's speed)
    public float damping = 0.3f;
    public float maxAngle = 38f;
    public Transform[] pushers;             // the player, Kessler
    public bool physical = true;
    public float mass = 45f;
    const int floorPileEvery = 0;           // 0: none. (every Nth hanging body would drop a heap across the floor; too hard. A const, because the scene had 3 saved into it)

    Vector2 angle, velocity;                // degrees about the world x and z axes
    Vector3[] last;
    float twist, twistSpeed;
    Quaternion rest;

    void Awake()
    {
        if (physical) { MakePhysical(); return; }
        rest = transform.rotation;
        twistSpeed = Random.Range(-7f, 7f);
        angle = Random.insideUnitCircle * 3f;
        last = new Vector3[pushers != null ? pushers.Length : 0];
        for (int i = 0; i < last.Length; i++) if (pushers[i] != null) last[i] = pushers[i].position;
    }

    void MakePhysical()
    {
        // the pile first, so its ray down doesn't meet our own collider
        if (floorPileEvery > 0 && transform.parent != null)
        {
            int n = 0;
            foreach (Transform sib in transform.parent)
            {
                if (sib == transform) break;
                if (sib.GetComponent<HangingBody>() != null) n++;
            }
            if (n % floorPileEvery == 1) DropPile();
        }

        var rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.linearDamping = 0.4f; rb.angularDamping = 1.2f;
        rb.centerOfMass = Vector3.down * length;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        var col = gameObject.AddComponent<CapsuleCollider>();
        col.direction = 1; col.radius = 0.3f; col.height = 1.9f;
        col.center = Vector3.down * length;
        var j = gameObject.AddComponent<ConfigurableJoint>();   // (to the world, at the hook)
        j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
        j.angularXMotion = ConfigurableJointMotion.Limited;
        j.angularZMotion = ConfigurableJointMotion.Limited;
        j.angularYMotion = ConfigurableJointMotion.Free;
        j.lowAngularXLimit = new SoftJointLimit { limit = -45f };
        j.highAngularXLimit = new SoftJointLimit { limit = 45f };
        j.angularZLimit = new SoftJointLimit { limit = 45f };
        rb.angularVelocity = new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(-0.3f, 0.3f), Random.Range(-0.1f, 0.1f));
    }

    // a heap of bags across the walkway under this hook, a bit too tall to walk into
    void DropPile()
    {
        var bag = transform.Find("Body bag");
        if (bag == null || !Physics.Raycast(transform.position, Vector3.down, out var floor, 14f, ~0, QueryTriggerInteraction.Ignore)) return;
        // which way the tunnel runs: toward the next hanging body
        bool seen = false; Vector3 next = default;
        foreach (Transform sib in transform.parent)
        {
            if (seen && sib.GetComponent<HangingBody>() != null) { next = sib.position; break; }
            if (sib == transform) seen = true;
        }
        Vector3 along = next != default ? next - transform.position : transform.forward;
        along.y = 0f;
        if (along.sqrMagnitude < 0.01f) along = Vector3.forward;
        along.Normalize();
        Vector3 across = Vector3.Cross(Vector3.up, along);

        // how wide the walkway is here
        Vector3 at = floor.point + Vector3.up * 0.5f;
        float l = Reach(at, -across), r = Reach(at, across);
        float width = l + r;
        if (width < 1f) return;
        Vector3 mid = at + across * (r - l) * 0.5f;

        // the height to aim for: what a jump of yours clears, with room to spare
        float h = 0.7f;
        var fpc = Object.FindFirstObjectByType<FirstPersonController>();
        var prb = fpc != null ? fpc.GetComponent<Rigidbody>() : null;
        if (fpc != null && prb != null) { float v = fpc.jumpPower / prb.mass; h = Mathf.Clamp(v * v / (2f * 9.81f) * 0.6f, 0.35f, 0.9f); }

        var pile = new GameObject("Bodies on the floor").transform;
        pile.SetParent(transform.parent, false);
        pile.SetPositionAndRotation(new Vector3(mid.x, floor.point.y, mid.z), Quaternion.LookRotation(along));
        Quaternion lie = Quaternion.FromToRotation(Vector3.down, across) * bag.rotation;   // long axis across the way
        int count = Mathf.CeilToInt(width / 1.6f);
        float top = 0f;
        for (int layer = 0; layer < 2; layer++)
            for (int i = 0; i < count; i++)
            {
                var b = Instantiate(bag.gameObject, pile);
                b.name = "Dead body";
                foreach (var c in b.GetComponentsInChildren<Collider>()) Destroy(c);
                b.transform.rotation = Quaternion.AngleAxis(Random.Range(-12f, 12f), Vector3.up) * lie;
                float step = width / count;
                float x = -width * 0.5f + step * (i + 0.5f) + (layer == 1 ? step * 0.5f : 0f);
                Vector3 want = pile.position + across * Mathf.Clamp(x, -width * 0.5f + 0.4f, width * 0.5f - 0.4f);
                b.transform.position = want;
                var rs = b.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                Bounds bb = rs[0].bounds; foreach (var rr in rs) bb.Encapsulate(rr.bounds);
                // rest it on the floor (or on the layer below), centred where it was put
                float baseY = layer == 0 ? pile.position.y : pile.position.y + top * 0.5f;
                b.transform.position += new Vector3(want.x - bb.center.x, baseY - bb.min.y, want.z - bb.center.z);
                if (layer == 0) top = Mathf.Max(top, bb.size.y);
            }
        var box = pile.gameObject.AddComponent<BoxCollider>();
        float thick = Mathf.Max(0.6f, top * 1.2f);
        float height = Mathf.Max(0.5f, Mathf.Min(h, top * 1.5f));   // (what the heap looks like, but never more than a jump clears)
        box.center = new Vector3(0f, height * 0.5f, 0f);
        // (the pile faces along the tunnel, so the box is wide in x, which is `across`)
        box.size = new Vector3(width, height, thick);
    }

    static float Reach(Vector3 from, Vector3 dir)
        => Physics.Raycast(from, dir, out var h, 8f, ~0, QueryTriggerInteraction.Ignore) ? h.distance : 4f;

    void Update()
    {
        if (physical) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;
        Vector3 body = transform.position - transform.up * length;
        for (int i = 0; i < last.Length; i++)
        {
            var p = pushers[i];
            if (p == null || !p.gameObject.activeInHierarchy) continue;
            Vector3 v = (p.position - last[i]) / dt; v.y = 0f;
            last[i] = p.position;
            Vector3 off = body - p.position; off.y = 0f;
            if (off.magnitude > reach || v.magnitude < 0.3f || v.magnitude > 30f) continue;   // (> 30: a teleport, not a push)
            // carried the way they're going, and knocked aside
            Vector3 push = v + off.normalized * v.magnitude * 0.6f;
            velocity += new Vector2(-push.z, push.x) * (shove * dt / length * Mathf.Rad2Deg);
        }
        // a damped pendulum
        velocity -= angle * (9.81f / length) * dt;
        velocity *= Mathf.Exp(-damping * dt);
        angle = Vector2.ClampMagnitude(angle + velocity * dt, maxAngle);
        twist += twistSpeed * dt;
        transform.rotation = Quaternion.Euler(angle.x, 0f, angle.y) * rest * Quaternion.Euler(0f, twist, 0f);
    }
}
