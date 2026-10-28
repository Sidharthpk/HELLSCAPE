using System.Collections.Generic;
using UnityEngine;

// Morning traffic in the prologue: a car that drives a fixed route of road points (in off the far end of the south road,
// down the street to the flats, round, and back out), jumping back to the start once it's out of sight. No navmesh.
//
// It drives like a car, not a bead on a string: the route is smoothed and resampled, a speed is worked out for every
// metre of it from how tight the bend is (and braked for in good time), and a kinematic bicycle model - steered by pure
// pursuit - turns the front wheels, so the car really swings round a corner about its rear axle. Acceleration is
// smoothed (no snap stops), the body rolls out of a turn and dips under braking, the wheels spin and steer. It keeps a
// time gap behind the car in front and stops short of the player and the van.
public class LoopDriver : MonoBehaviour
{
    public Vector3[] path;                  // road points; the route jumps from the last back to the first
    public float speed = 8f;                // cruising speed on the straights (m/s)
    public float startAt;                   // metres along the route
    public float modelYaw;                  // an old model whose nose isn't +z (the pack cars need 0)
    public Transform player;
    public float brakeDistance = 9f;        // stops this far short of the player
    public float carGap = 10f;              // the gap kept behind the car in front at a crawl; grows with speed

    [Header("Driving feel")]
    public float cornerGrip = 2.8f;         // sideways acceleration it will accept in a bend (m/s^2): lower = slower corners
    public float accel = 2.4f;              // m/s^2
    public float comfortBrake = 3.2f;       // braking it plans bends with
    public float hardBrake = 7f;            // the most it will brake for something in the way
    public float maxSteer = 34f;            // front wheel lock (degrees)

    static readonly List<LoopDriver> all = new List<LoopDriver>();

    public float Speed => v;
    public float Progress => s;

    PackCar car;
    PrologueVan[] vans = new PrologueVan[0];
    Vector3[] pts;                          // the smoothed route, 1 m apart
    float[] limit;                          // speed limit at each metre
    float length;
    float s, v, steer, acc, yaw, roll, pitch, y0, stuck;
    Renderer rend;
    float wheelbase = 2.7f, rearAxle = -1.3f, halfLength = 2.2f;
    Vector3 rear;                           // the rear axle's position on the road

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    void Start()
    {
        car = GetComponentInChildren<PackCar>();
        rend = car != null && car.body != null ? car.body.GetComponent<Renderer>() : GetComponentInChildren<Renderer>();
        if (car != null) { wheelbase = car.wheelbase; rearAxle = car.rearAxle; halfLength = car.size.z * 0.5f; }
        else { var box = GetComponent<BoxCollider>(); if (box != null) halfLength = Mathf.Max(halfLength, Mathf.Max(box.size.x, box.size.z) * 0.5f); }
        if (player == null) { var p = GameObject.FindGameObjectWithTag("Player"); if (p != null) player = p.transform; }
        vans = FindObjectsByType<PrologueVan>(FindObjectsSortMode.None);
        Build();
        s = Mathf.Repeat(startAt, length);
        PlaceOn(s);
    }

    // ---------------------------------------------------------------- the route

    void Build()
    {
        // round the corners off (Chaikin), then lay the points 1 m apart
        var p = new List<Vector3>(path);
        for (int pass = 0; pass < 3; pass++)
        {
            var q = new List<Vector3> { p[0] };
            for (int i = 0; i < p.Count - 1; i++) { q.Add(Vector3.Lerp(p[i], p[i + 1], 0.25f)); q.Add(Vector3.Lerp(p[i], p[i + 1], 0.75f)); }
            q.Add(p[p.Count - 1]);
            p = q;
        }
        var r = new List<Vector3> { p[0] };
        float done = 0f, next = 1f;                  // metres walked so far, and where the next point goes
        for (int i = 1; i < p.Count; i++)
        {
            float d = Vector3.Distance(p[i - 1], p[i]);
            while (done + d >= next && d > 1e-5f) { r.Add(Vector3.Lerp(p[i - 1], p[i], (next - done) / d)); next += 1f; }
            done += d;
        }
        r.Add(p[p.Count - 1]);
        pts = r.ToArray();
        length = pts.Length - 1;

        // a speed for every metre: the bend's speed (from its curvature), then braked back into it
        limit = new float[pts.Length];
        for (int i = 0; i < pts.Length; i++)
        {
            float k = Curvature(i);
            limit[i] = Mathf.Clamp(Mathf.Sqrt(cornerGrip / Mathf.Max(k, 1e-4f)), 2.5f, speed);
        }
        for (int i = limit.Length - 2; i >= 0; i--)
            limit[i] = Mathf.Min(limit[i], Mathf.Sqrt(limit[i + 1] * limit[i + 1] + 2f * comfortBrake));
        for (int i = 1; i < limit.Length; i++)    // and eased out of it: no launching off the apex
            limit[i] = Mathf.Min(limit[i], Mathf.Sqrt(limit[i - 1] * limit[i - 1] + 2f * accel * 1.5f));
    }

    float Curvature(int i)
    {
        int a = Mathf.Max(0, i - 4), b = Mathf.Min(pts.Length - 1, i + 4);
        if (b - a < 2) return 0f;
        Vector3 pa = pts[a], pb = pts[i], pc = pts[b];
        float ab = Flat(pb - pa).magnitude, bc = Flat(pc - pb).magnitude, ca = Flat(pa - pc).magnitude;
        float area2 = Mathf.Abs(Cross(Flat(pb - pa), Flat(pc - pa)));   // twice the area
        return 2f * area2 / Mathf.Max(ab * bc * ca, 1e-4f);
    }

    static Vector3 Flat(Vector3 d) { d.y = 0f; return d; }
    static float Cross(Vector3 a, Vector3 b) => a.x * b.z - a.z * b.x;
    Vector3 At(float dist) { float f = Mathf.Clamp(dist, 0f, length); int i = Mathf.Min((int)f, pts.Length - 2); return Vector3.Lerp(pts[i], pts[i + 1], f - i); }
    float LimitAt(float dist) => limit[Mathf.Clamp(Mathf.RoundToInt(dist), 0, limit.Length - 1)];

    // ---------------------------------------------------------------- driving

    void PlaceOn(float dist)
    {
        rear = At(dist);
        Vector3 dir = Flat(At(dist + 3f) - rear);
        yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        v = Mathf.Min(speed, LimitAt(dist)); steer = 0f; acc = 0f;
        y0 = rear.y;
        Apply(0f, true);
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;
        Vector3 fwd = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
        Vector3 centre = rear - fwd * rearAxle;

        // where on the route we are (search a little ahead of last time)
        int lo = Mathf.Max(0, (int)s - 4), hi = Mathf.Min(pts.Length - 1, (int)s + 24);
        float best = float.MaxValue; int bi = lo;
        for (int i = lo; i <= hi; i++) { float d = Flat(pts[i] - rear).sqrMagnitude; if (d < best) { best = d; bi = i; } }
        s = Mathf.Max(s, bi);

        // ---- steering: pure pursuit on a point ahead of the rear axle
        float look = Mathf.Clamp(3.5f + 0.55f * v, 3.5f, 14f);
        Vector3 target = At(s + look);
        Vector3 to = Flat(target - rear);
        float alpha = Mathf.Atan2(Vector3.Dot(to, right), Vector3.Dot(to, fwd));   // + = the target is to the right
        float want = Mathf.Atan(2f * wheelbase * Mathf.Sin(alpha) / Mathf.Max(look, 0.1f)) * Mathf.Rad2Deg;
        want = Mathf.Clamp(want, -maxSteer, maxSteer);
        float rate = Mathf.Lerp(150f, 60f, Mathf.Clamp01(v / 12f));            // the wheel turns slower at speed
        steer = Mathf.MoveTowards(steer, want, rate * dt);

        // ---- speed: the bend ahead, braked for early enough, then whatever is in the way
        float horizon = v * v / (2f * comfortBrake) + 6f;
        float target_v = speed;
        for (float d = 0f; d <= horizon; d += 2f) target_v = Mathf.Min(target_v, LimitAt(s + d));
        target_v = Mathf.Min(target_v, LimitAt(s));
        target_v = Mathf.Min(target_v, Obstacles(centre, fwd, right));
        float cmd = Mathf.Clamp((target_v - v) * 1.4f, -hardBrake, accel);
        acc = Mathf.MoveTowards(acc, cmd, (cmd < acc ? 14f : 5f) * dt);       // brakes bite faster than the throttle builds
        v = Mathf.Max(0f, v + acc * dt);
        if (target_v <= 0.01f && v < 0.15f) { v = 0f; acc = 0f; }

        // held up for good (behind the parked van, say): once nobody can see it, it quietly rejoins at the start of the route
        stuck = v < 0.15f && target_v < 0.1f ? stuck + dt : 0f;
        if (stuck > 8f && (rend == null || !rend.isVisible) && StartIsClear()) { stuck = 0f; s = 0f; PlaceOn(0f); return; }

        // ---- the bicycle model, about the rear axle
        float tan = Mathf.Tan(steer * Mathf.Deg2Rad);
        yaw += v / wheelbase * tan * Mathf.Rad2Deg * dt;
        fwd = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));
        rear += fwd * v * dt;

        // ---- out of the route: back to the start (it's out of sight)
        if (s >= length - 1.5f)
        {
            if (!StartIsClear() && rend != null && rend.isVisible) { v = 0f; acc = 0f; return; }   // (wait if seen and the start is occupied)
            s = 0f; PlaceOn(0f); return;
        }

        float latAcc = v * v * tan / wheelbase;                                 // + = swinging right
        Apply(dt, false, latAcc);
    }

    bool StartIsClear()
    {
        foreach (var o in all) if (o != this && o.pts != null && Flat(o.rear - pts[0]).sqrMagnitude < 14f * 14f) return false;
        return true;
    }

    // the speed the road ahead allows: <= 0 means stop
    float Obstacles(Vector3 centre, Vector3 fwd, Vector3 right)
    {
        float result = float.MaxValue;
        // the player and the vans: stand still, so stop a few metres short
        if (player != null) result = Mathf.Min(result, Stop(player.position, 0.7f, 0f, brakeDistance * 0.35f, centre, fwd, right));
        foreach (var van in vans) if (van != null) result = Mathf.Min(result, Stop(van.transform.position, 1.2f, 2.6f, 3f, centre, fwd, right));
        // the car in front: keep a time gap
        foreach (var o in all)
        {
            if (o == this || !o.isActiveAndEnabled || o.pts == null) continue;
            Vector3 rel = Flat(o.Centre - centre);
            float ahead = Vector3.Dot(rel, fwd), side = Mathf.Abs(Vector3.Dot(rel, right));
            if (ahead <= 0f || ahead > 40f || side > 2.3f) continue;
            float gap = ahead - halfLength - o.halfLength;
            float want = 2.5f + 1.2f * v;                                       // a metre-and-a-bit per m/s, and a car's length at a stop
            float vs = o.v + (gap - want) * 0.7f;
            if (gap < 1.5f) vs = 0f;
            result = Mathf.Min(result, Mathf.Max(0f, vs));
        }
        return result;
    }

    Vector3 Centre => rear - new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad)) * rearAxle;

    // something standing still ahead: the speed from which we can still stop `margin` metres short of it
    float Stop(Vector3 thing, float radius, float halfDepth, float margin, Vector3 centre, Vector3 fwd, Vector3 right)
    {
        Vector3 rel = Flat(thing - centre);
        float ahead = Vector3.Dot(rel, fwd), side = Mathf.Abs(Vector3.Dot(rel, right));
        if (ahead <= 0f || ahead > 45f || side > 1.1f + radius + 0.4f) return float.MaxValue;
        float room = ahead - halfLength - halfDepth - margin - 2f;
        if (room <= 0f) return 0f;
        return Mathf.Sqrt(2f * hardBrake * 0.6f * room);
    }

    // put the car on the road: position, heading, grade, body lean, wheels
    void Apply(float dt, bool snap, float latAcc = 0f)
    {
        Vector3 fwd = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));
        float ground = At(s).y;
        y0 = snap ? ground : Mathf.Lerp(y0, ground, 1f - Mathf.Exp(-10f * Mathf.Max(dt, 0.001f)));
        Vector3 c = rear - fwd * rearAxle; c.y = y0;
        // the road's grade under the car's length
        float slope = Mathf.Atan2(At(s + wheelbase * 0.6f).y - At(s - wheelbase * 0.4f).y, wheelbase) * Mathf.Rad2Deg;
        transform.SetPositionAndRotation(c, Quaternion.Euler(-slope, yaw + modelYaw, 0f));
        if (car == null) return;
        float k = 1f - Mathf.Exp(-6f * Mathf.Max(dt, 0.001f));
        roll = Mathf.Lerp(roll, Mathf.Clamp(-latAcc * 0.5f, -3.5f, 3.5f), k);       // leans out of the turn
        pitch = Mathf.Lerp(pitch, Mathf.Clamp(-acc * 0.4f, -2.5f, 2.5f), k);         // dips on the brakes, squats on the throttle
        car.Lean(roll, pitch);
        car.Roll(v * dt, steer);
    }
}
