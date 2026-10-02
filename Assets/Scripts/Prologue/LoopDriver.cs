using System.Collections.Generic;
using UnityEngine;

// Morning traffic in the prologue: a car that drives a fixed loop of road points (in off the far end of the
// south road, down the street to the flats, round, and back out), jumping back to the start once it's out of
// sight. It brakes for the player and keeps its distance from the car in front. No navmesh needed.
public class LoopDriver : MonoBehaviour
{
    public Vector3[] path;                  // evenly spaced points, closed by a jump from the last back to the first
    public float speed = 8f;
    public float startAt;                   // metres along the path
    public float modelYaw;                  // turns the model so its nose points along the path
    public Transform player;
    public float brakeDistance = 9f;
    public float carGap = 10f;

    static readonly List<LoopDriver> all = new List<LoopDriver>();

    private float[] along;                  // distance at each point
    private float length, s, v;

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    void Start()
    {
        along = new float[path.Length];
        for (int i = 1; i < path.Length; i++) along[i] = along[i - 1] + Vector3.Distance(path[i - 1], path[i]);
        length = along[path.Length - 1];
        s = Mathf.Repeat(startAt, length);
        v = speed;
        if (player == null) { var p = GameObject.FindGameObjectWithTag("Player"); if (p != null) player = p.transform; }
        Place(true);
    }

    void Update()
    {
        float target = speed;
        Vector3 fwd = transform.rotation * Quaternion.Euler(0f, -modelYaw, 0f) * Vector3.forward;

        // someone in the road ahead
        if (player != null)
        {
            Vector3 to = player.position - transform.position;
            to.y = 0f;
            float ahead = Vector3.Dot(to, fwd);
            if (ahead > 0f && ahead < brakeDistance && Vector3.Cross(fwd, to).magnitude < 2.4f) target = 0f;
        }
        // the car in front
        foreach (var o in all)
        {
            if (o == this || o.length <= 0f) continue;
            float gap = Mathf.Repeat(o.s - s, length);
            if (gap < carGap) target = Mathf.Min(target, Mathf.Max(0f, (gap - carGap * 0.6f) * 1.5f));
        }

        v = Mathf.MoveTowards(v, target, (target < v ? 12f : 4f) * Time.deltaTime);
        s += v * Time.deltaTime;
        bool wrapped = s >= length;
        if (wrapped) s -= length;
        Place(wrapped);
    }

    void Place(bool snap)
    {
        int i = System.Array.BinarySearch(along, s);
        if (i < 0) i = ~i;
        i = Mathf.Clamp(i, 1, path.Length - 1);
        float t = Mathf.InverseLerp(along[i - 1], along[i], s);
        Vector3 pos = Vector3.Lerp(path[i - 1], path[i], t);
        // look a few metres ahead so it steers through the curves instead of snapping at each point
        int j = Mathf.Min(i + 3, path.Length - 1);
        Vector3 dir = path[j] - pos;
        dir.y = 0f;
        Quaternion rot = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) * Quaternion.Euler(0f, modelYaw, 0f) : transform.rotation;
        transform.position = pos;
        transform.rotation = snap ? rot : Quaternion.Slerp(transform.rotation, rot, 6f * Time.deltaTime);
    }
}
