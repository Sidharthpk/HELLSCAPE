using UnityEngine;

// Keeps the rigidbody player on the stairs: sprinting up a steep flight carries enough upward speed to launch
// them off the top of it. Whenever the feet come a little way off the ground (not a real drop), pull them back down.
// The gap is measured with the capsule's own bottom sphere, not a ray from its middle: on a slope a round-bottomed
// capsule rests a few cm above the point straight below its centre, and a ray read that as "airborne" and pulled
// the player down every step, so they couldn't climb the stairs at all.
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class StickToGround : MonoBehaviour
{
    public float snapDistance = 0.6f;       // gaps bigger than this are real falls: leave those to gravity
    public float pull = 3f;
    public float tolerance = 0.06f;         // closer than this counts as standing on it

    private Rigidbody rb;
    private CapsuleCollider cap;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cap = GetComponent<CapsuleCollider>();
    }

    void FixedUpdate()
    {
        if (rb.isKinematic) return;
        float scaleY = transform.lossyScale.y;
        float radius = cap.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) * 0.95f;
        Vector3 centre = transform.TransformPoint(cap.center);
        float toBottomSphere = Mathf.Max(0f, cap.height * 0.5f * scaleY - radius);   // centre -> centre of the bottom sphere
        float gap = float.MaxValue;
        foreach (var hit in Physics.SphereCastAll(centre, radius, Vector3.down, toBottomSphere + snapDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.distance <= 0f) { gap = 0f; break; }          // already touching something
            gap = Mathf.Min(gap, hit.distance - toBottomSphere);
        }
        if (gap == float.MaxValue || gap < tolerance) return;    // a real drop, or standing on the ground/slope
        var v = rb.linearVelocity;
        v.y = Mathf.Min(v.y, -pull);
        rb.linearVelocity = v;
    }
}
