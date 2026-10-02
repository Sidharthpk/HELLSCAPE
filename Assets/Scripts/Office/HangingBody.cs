using UnityEngine;

// A body hung from the tunnel roof (this object is the hook: the rope and the bag hang below it). It turns slowly
// on its rope, and swings when something pushes through it: you, or what's coming after you.
public class HangingBody : MonoBehaviour
{
    public float length = 5f;               // hook to the middle of the body
    public float reach = 0.9f;              // how close something has to pass to set it going
    public float shove = 1.4f;              // how hard (x the passer's speed)
    public float damping = 0.3f;
    public float maxAngle = 38f;
    public Transform[] pushers;             // the player, Kessler

    Vector2 angle, velocity;                // degrees about the world x and z axes
    Vector3[] last;
    float twist, twistSpeed;
    Quaternion rest;

    void Awake()
    {
        rest = transform.rotation;
        twistSpeed = Random.Range(-7f, 7f);
        angle = Random.insideUnitCircle * 3f;
        last = new Vector3[pushers != null ? pushers.Length : 0];
        for (int i = 0; i < last.Length; i++) if (pushers[i] != null) last[i] = pushers[i].position;
    }

    void Update()
    {
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
