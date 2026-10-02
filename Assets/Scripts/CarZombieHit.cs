using System.Collections.Generic;
using UnityEngine;

// Running zombies over. The car never physically collides with a zombie (alive or ragdolled): hitting one as a
// solid body bled the car's speed, and the ragdoll left behind snagged the wheels. Instead a trigger box a little
// bigger than the car kills whatever it touches while you're moving - once per zombie - and the car drives on.
public class CarZombieHit : MonoBehaviour
{
    public float killSpeed = 2.5f;          // m/s: slower than this (parked, creeping) doesn't run anyone over
    public float hitImpulse = 800f;

    private PrometeoCarController carController;
    private Rigidbody carRb;
    private readonly HashSet<Zombie> hit = new HashSet<Zombie>();

    static readonly List<Collider> carSolids = new List<Collider>();

    void Awake()
    {
        carController = GetComponent<PrometeoCarController>();
        carRb = GetComponent<Rigidbody>();
        carSolids.Clear();
        // measure the car's footprint with it unrotated, so the box is the car's own shape
        Quaternion rot = transform.rotation;
        transform.rotation = Quaternion.identity;
        Physics.SyncTransforms();
        var bounds = new Bounds(transform.position, Vector3.zero);
        foreach (var c in GetComponentsInChildren<Collider>(true))
        {
            if (c.isTrigger) continue;
            carSolids.Add(c);
            bounds.Encapsulate(c.bounds);
        }
        Vector3 centre = transform.InverseTransformPoint(bounds.center);
        transform.rotation = rot;
        Physics.SyncTransforms();

        // the sweep: the car's footprint, a bit wider and reaching ahead of the bumper
        var sweep = new GameObject("ZombieSweep").AddComponent<BoxCollider>();
        sweep.transform.SetParent(transform, false);
        sweep.isTrigger = true;
        Vector3 size = bounds.size;
        sweep.center = centre + Vector3.forward * 0.4f;
        sweep.size = new Vector3(size.x + 0.6f, Mathf.Max(size.y, 1.5f), size.z + 1.2f);

        foreach (var z in FindObjectsByType<Zombie>(FindObjectsSortMode.None)) IgnoreCar(z);
    }

    // every zombie calls this when it spawns: no solid contact with the car, ever
    public static void IgnoreCar(Zombie z)
    {
        if (carSolids.Count == 0) return;
        foreach (var zc in z.GetComponentsInChildren<Collider>(true))
            foreach (var cc in carSolids)
                if (cc != null && zc != null) Physics.IgnoreCollision(zc, cc, true);
    }

    // (only zombies matter here: Collider.ClosestPoint can't be asked of the map's mesh colliders, and logged an
    // error every time the sweep touched a kerb or a wall)
    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<Zombie>() == null) return;
        bool closest = !(other is MeshCollider mc) || mc.convex;
        TryHit(other, closest ? other.ClosestPoint(transform.position) : other.bounds.ClosestPoint(transform.position));
    }

    // anything that still slips through (a zombie spawned before the car woke up)
    void OnCollisionEnter(Collision col) => TryHit(col.collider, col.contacts[0].point);

    void TryHit(Collider other, Vector3 point)
    {
        var z = other.GetComponentInParent<Zombie>();
        if (z == null || hit.Contains(z)) return;
        if (carRb.linearVelocity.magnitude < killSpeed) return;
        hit.Add(z);
        z.KillInstant(point, carRb.linearVelocity.normalized * hitImpulse);   // Zombie.Die re-applies IgnoreCar to the ragdoll
    }
}
