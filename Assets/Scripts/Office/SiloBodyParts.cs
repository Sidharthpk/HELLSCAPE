using System.Collections.Generic;
using UnityEngine;

// The silo floor's thin layer of blood (on this object) and the severed parts floating on it (its children).
// They are heavy rigidbodies with mesh colliders, held at the surface (or the flood's, once that rises past it) and free
// to be shoved sideways, so wading through them slows you. KillerBoss.Throw picks one up (Take, kinematic while he
// holds it) and hurls it at you; where it lands it floats again (Drop). Built by HellScape > Vane: Silo Blood Pool.
public class SiloBodyParts : MonoBehaviour
{
    public Renderer surface;                // the blood plane (a child, not a part)
    public Transform flood;                 // BloodFlood.surface: they ride it up when it's higher than the pool
    public float bob = 0.025f;
    public float mass = 80f;                // heavy: pushing through them costs you speed
    public float sink = 0.02f;              // how far under the surface their middle sits

    class Part { public Rigidbody rb; public Transform t; public Vector3 home; public Quaternion rest; public float phase, spin, lift; public bool held; }
    readonly List<Part> parts = new List<Part>();

    void Awake()
    {
        foreach (Transform c in transform)
        {
            var r = c.GetComponentInChildren<Renderer>();
            if (r == null || r == surface) continue;
            // lift = how far its pivot is above its lowest point, so it floats on its side, not through the floor
            var rb = c.gameObject.AddComponent<Rigidbody>();
            rb.mass = mass; rb.linearDamping = 4f; rb.angularDamping = 3f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;   // (stays lying on its side)
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            foreach (var mf in c.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; mc.convex = true;
            }
            parts.Add(new Part { rb = rb, t = c, home = c.position, rest = c.rotation, phase = Random.value * 6.3f, spin = Random.Range(-6f, 6f), lift = c.position.y - r.bounds.min.y });
        }
    }

    float SurfaceY() => flood != null && flood.gameObject.activeInHierarchy ? Mathf.Max(transform.position.y, flood.position.y) : transform.position.y;

    void FixedUpdate()
    {
        float y = SurfaceY(), t = Time.time;
        foreach (var p in parts)
        {
            if (p.held) continue;
            Vector3 at = p.rb.position;
            at.y = y + p.lift * 0.6f - sink + Mathf.Sin(t * 1.3f + p.phase) * bob;   // (floats: height is the water's, sideways is physics)
            p.rb.position = at;
            Vector3 v = p.rb.linearVelocity; v.y = 0f; p.rb.linearVelocity = v;
            p.rb.angularVelocity = new Vector3(0f, p.spin * Mathf.Deg2Rad * 0.3f, 0f) + Vector3.up * p.rb.angularVelocity.y * 0.5f;
        }
    }

    // the floating part nearest to him (null when he's holding them all)
    public Transform Take(Vector3 near)
    {
        Part best = null;
        foreach (var p in parts)
            if (!p.held && (best == null || (p.t.position - near).sqrMagnitude < (best.t.position - near).sqrMagnitude)) best = p;
        if (best != null) { best.held = true; best.rb.isKinematic = true; }
        return best != null ? best.t : null;
    }

    // back on the blood where it came down, lying flat-ish again
    public void Drop(Transform part, Vector3 at)
    {
        foreach (var p in parts)
            if (p.t == part)
            {
                part.position = new Vector3(at.x, SurfaceY(), at.z);
                part.rotation = Quaternion.Euler(0f, Random.value * 360f, 0f) * p.rest;
                p.rb.isKinematic = false; p.rb.linearVelocity = Vector3.zero;
                p.held = false;
            }
    }

    // ResetBoss: every part back where the level put it
    public void ResetParts()
    {
        foreach (var p in parts) { p.rb.isKinematic = false; p.rb.linearVelocity = Vector3.zero; p.held = false; p.t.SetPositionAndRotation(p.home, p.rest); }
    }
}
