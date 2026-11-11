using UnityEngine;

// The driver's body in the van (Ch01 "Driving", placed and posed by hand in the Prologue). Shown only while you drive.
// The hands start exactly where they were posed and turn with the steering wheel round its column, as if holding it; two-bone
// IK bends the arms to follow. A hand carried past the top or the bottom of the wheel lets go and takes its posed grip again
// (hand-over-hand), the way you steer a van. The Animator (HellScape > Cars > Seat The Van Driver) turns the head into the turn.
[DefaultExecutionOrder(200)]   // (after CockpitView turns the wheel)
public class VanDriver : MonoBehaviour
{
    public PrometeoCarController car;
    public Transform wheel;                                     // volant.002
    public float regripTime = 0.28f;
    public float maxWrist = 35f;                                // degrees the hand may bend off the forearm

    class Arm
    {
        public Transform up, lo, hand;
        public float side, home, g;            // side: -1 left, +1 right; home: posed grip angle; g: grip angle on the wheel (wheel-fixed degrees)
        public Vector3 elbowHint;              // posed elbow, van space
        public Vector3 restPos;                // posed hand, van space
        public Quaternion restRot;
        public Quaternion upLocal, loLocal, handLocal;   // posed local rotations (IK restarts from these every frame, so no twist builds up)
        public float t = 1f; public Vector3 fromPos; public Quaternion fromRot;   // a regrip in flight
    }

    Animator anim;
    Renderer[] bodies;
    Transform head;
    Arm left, right;
    Vector3 centre, axis, upDir, rightDir;   // the wheel, van space (axis points at the driver)
    Quaternion wheelRest;
    bool? shown;

    void Start()
    {
        anim = GetComponent<Animator>();
        bodies = GetComponentsInChildren<Renderer>(true);
        head = Bone("Head");
        var van = car.transform;
        var b = wheel.GetComponent<MeshFilter>().sharedMesh.bounds;
        centre = van.InverseTransformPoint(wheel.TransformPoint(b.center));
        axis = van.InverseTransformDirection(wheel.forward);
        if (Vector3.Dot(axis, Vector3.forward) > 0f) axis = -axis;
        upDir = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
        rightDir = Vector3.Cross(upDir, axis).normalized;   // (driver's right, +x)
        if (rightDir.x < 0f) rightDir = -rightDir;
        wheelRest = Quaternion.Inverse(van.rotation) * wheel.rotation;
        left = MakeArm("Left", -1f); right = MakeArm("Right", 1f);
    }

    Arm MakeArm(string s, float side)
    {
        var van = car.transform;
        var a = new Arm { up = Bone(s + "Arm"), lo = Bone(s + "ForeArm"), hand = Bone(s + "Hand"), side = side };
        a.elbowHint = van.InverseTransformPoint(a.lo.position);   // (on the straight wheel the arm lands exactly in its posed bend)
        a.restPos = van.InverseTransformPoint(a.hand.position);
        a.restRot = Quaternion.Inverse(van.rotation) * a.hand.rotation;
        a.upLocal = a.up.localRotation; a.loLocal = a.lo.localRotation; a.handLocal = a.hand.localRotation;
        a.home = a.g = Angle(a.restPos - centre);
        return a;
    }

    Transform Bone(string n)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name.EndsWith(":" + n)) return t;
        return null;
    }

    // a van-space direction's angle round the wheel, degrees clockwise as the driver sees it, 0 = 12 o'clock
    float Angle(Vector3 v) => Mathf.Atan2(Vector3.Dot(v, rightDir), Vector3.Dot(v, upDir)) * Mathf.Rad2Deg;

    // the wheel's turn now
    float WheelAngle()
    {
        var q = Quaternion.Inverse(car.transform.rotation) * wheel.rotation * Quaternion.Inverse(wheelRest);
        return Angle(q * upDir);
    }

    void LateUpdate()
    {
        bool driving = car != null && car.enabled;
        if (driving != shown)
        {
            shown = driving;
            foreach (var r in bodies) r.enabled = driving;
            if (head != null) head.localScale = driving ? Vector3.one * 0.001f : Vector3.one;   // (the camera sits inside the head)
        }
        if (!driving || wheel == null) return;

        if (anim != null) anim.SetFloat("Steer", car.frontLeftCollider.steerAngle / Mathf.Max(1, car.maxSteeringAngle), 0.15f, Time.deltaTime);
        float w = WheelAngle();
        // (only one hand lets go at a time)
        Hold(left, w, right.t >= 1f);
        Hold(right, w, left.t >= 1f);
    }

    void Hold(Arm a, float w, bool mayLetGo)
    {
        var van = car.transform;
        float turned = Mathf.DeltaAngle(0f, a.g + w - a.home);   // how far the wheel has carried this hand from its posed grip
        float at = a.home + turned;
        // carried over the top (past 12) or round the bottom (past 6): let go and take the posed grip again
        bool over = a.side < 0 ? (at > 15f || at < -195f) : (at < -15f || at > 195f);
        // wheel back to straight but the hand isn't on its posed grip (left there by a regrip): take the posed grip again
        bool straight = Mathf.Abs(Mathf.DeltaAngle(0f, w)) < 5f && Mathf.Abs(turned) > 3f;
        if (((over && mayLetGo) || straight) && a.t >= 1f)
        {
            a.t = 0f; a.fromPos = a.hand.position; a.fromRot = a.hand.rotation;
            a.g = a.home - w;
            turned = 0f;
        }

        // the posed hand, turned rigidly with the wheel round its column
        var spin = Quaternion.AngleAxis(turned, axis);   // (Unity: positive about an axis pointing at the driver = clockwise to them)
        var pos = van.TransformPoint(centre + spin * (a.restPos - centre));
        var rot = van.rotation * spin * a.restRot;
        if (a.t < 1f)
        {
            a.t = Mathf.Min(1f, a.t + Time.deltaTime / regripTime);
            float e = Mathf.SmoothStep(0f, 1f, a.t);
            pos = Vector3.Lerp(a.fromPos, pos, e) + van.TransformDirection(axis) * (0.07f * Mathf.Sin(a.t * Mathf.PI));   // (lifted off the rim on the way)
            rot = Quaternion.Slerp(a.fromRot, rot, e);
        }
        a.up.localRotation = a.upLocal; a.lo.localRotation = a.loLocal; a.hand.localRotation = a.handLocal;
        Solve(a, pos, van.TransformPoint(a.elbowHint));
        // the wrist bends at most maxWrist away from where the forearm carries it (more and the skin wraps into a balloon)
        a.hand.rotation = Quaternion.RotateTowards(a.hand.rotation, rot, maxWrist);
    }

    // two-bone IK: shoulder-elbow-wrist reach the target, the elbow bends toward the hint
    static void Solve(Arm a, Vector3 target, Vector3 hint)
    {
        Vector3 s = a.up.position;
        float l1 = Vector3.Distance(s, a.lo.position), l2 = Vector3.Distance(a.lo.position, a.hand.position);
        Vector3 toT = target - s;
        float d = Mathf.Clamp(toT.magnitude, 0.01f, (l1 + l2) * 0.999f);
        Vector3 dir = toT.normalized;
        Vector3 pole = Vector3.ProjectOnPlane(hint - s, dir).normalized;
        float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
        Vector3 elbow = s + (dir * cosA + pole * Mathf.Sqrt(1f - cosA * cosA)) * l1;
        a.up.rotation = Quaternion.FromToRotation(a.lo.position - s, elbow - s) * a.up.rotation;
        a.lo.rotation = Quaternion.FromToRotation(a.hand.position - a.lo.position, target - a.lo.position) * a.lo.rotation;
    }
}
