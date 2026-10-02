using System.Collections.Generic;
using UnityEngine;

// The van's safety net. While you're driving it remembers where it was last rolling along happily; if it ends up on
// its roof or side, wedged somewhere with the throttle held and going nowhere, or off the map, it is put back there
// (a few metres up the road, upright, stopped). Hold the reset key to do it yourself.
public class VanRecovery : MonoBehaviour
{
    public Rigidbody body;
    public Behaviour controller;            // the car controller: only watches while it's being driven
    public KeyCode resetKey = KeyCode.T;
    public float holdToReset = 0.6f;
    public float flippedAfter = 1.6f;       // seconds on its side or roof
    public float stuckAfter = 2.8f;         // seconds of throttle with no movement
    public float fallDepth = 12f;           // this far under the last good spot = fell out of the world

    struct Pose { public Vector3 pos; public Quaternion rot; }
    readonly List<Pose> good = new List<Pose>();
    WheelCollider[] wheels;
    float flipped, stuck, held, nextSample;

    public System.Action onRecover;

    void Awake()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        wheels = GetComponentsInChildren<WheelCollider>();
    }

    void Update()
    {
        if (body == null || controller == null || !controller.enabled || body.isKinematic) { flipped = stuck = held = 0f; return; }
        float speed = body.linearVelocity.magnitude;
        bool upright = Vector3.Dot(transform.up, Vector3.up) > 0.45f;

        // somewhere worth coming back to: rolling, upright, all four wheels down
        if (Time.time >= nextSample)
        {
            nextSample = Time.time + 0.5f;
            bool grounded = wheels.Length > 0;
            foreach (var w in wheels) grounded &= w.isGrounded;
            if (grounded && upright && speed > 3f)
            {
                good.Add(new Pose { pos = transform.position, rot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) });
                if (good.Count > 8) good.RemoveAt(0);
            }
        }

        flipped = upright ? 0f : flipped + Time.deltaTime;
        bool pushing = Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.3f && !Input.GetKey(KeyCode.Space);   // (not just sat on the handbrake)
        stuck = pushing && speed < 0.35f ? stuck + Time.deltaTime : 0f;
        held = Input.GetKey(resetKey) ? held + Time.deltaTime : 0f;
        bool fell = good.Count > 0 && transform.position.y < good[good.Count - 1].pos.y - fallDepth;

        if (flipped > flippedAfter || stuck > stuckAfter || held > holdToReset || fell) Recover();
    }

    public void Recover()
    {
        flipped = stuck = held = 0f;
        // a couple of seconds back, not the spot that just got it stuck
        Pose p = good.Count > 0 ? good[Mathf.Max(0, good.Count - 4)] : new Pose { pos = transform.position, rot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) };
        if (good.Count > 4) good.RemoveRange(good.Count - 3, 3);
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(p.pos + Vector3.up * 0.35f, p.rot);
        Physics.SyncTransforms();
        onRecover?.Invoke();
    }
}
