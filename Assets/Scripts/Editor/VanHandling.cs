using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// How the van drives (both scenes' vans share it): HellScape > Cars > Tune Van Handling writes these numbers onto
// every PrometeoCarController in the open scenes. Playtesters found it slow to turn and slow to stop, and measured
// it was: 68 m to stop from 80 km/h (brake torque 520 on 1.7 t), and the front tyres ran out of grip at 60.
// Measure(...) is the test the numbers were tuned against (Play mode, Prologue): a full stop from 80 km/h and
// 1.2 s of full lock at 60 km/h on the long straight, written to Logs/van-handling.log.
public static class VanHandling
{
    public const int BrakeTorque = 1700;        // per wheel
    public const float ForwardGrip = 1.7f;      // tyre stiffness along the wheel: what the brakes (and the engine) can use
    public const float FrontSideGrip = 1.6f;    // across the wheel: how hard it can turn
    public const float RearSideGrip = 1.5f;     // a touch less than the front, so it turns in rather than ploughing on
    public const int SteeringAngle = 34;

    [MenuItem("HellScape/Cars/Tune Van Handling")]
    public static void TuneAll()
    {
        int n = 0;
        foreach (var c in Object.FindObjectsByType<PrometeoCarController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Apply(c);
            EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            n++;
        }
        Debug.Log("Van handling: " + n + " van(s) tuned.");
    }

    public static void Apply(PrometeoCarController c) => Apply(c, BrakeTorque, ForwardGrip, FrontSideGrip, RearSideGrip, SteeringAngle);

    public static void Apply(PrometeoCarController c, int brake, float forward, float sideFront, float sideRear, int steer)
    {
        c.brakeForce = brake;
        c.maxSteeringAngle = steer;
        Grip(c.frontLeftCollider, forward, sideFront); Grip(c.frontRightCollider, forward, sideFront);
        Grip(c.rearLeftCollider, forward, sideRear); Grip(c.rearRightCollider, forward, sideRear);
        if (!Application.isPlaying) EditorUtility.SetDirty(c);
    }

    static void Grip(WheelCollider w, float forward, float sideways)
    {
        if (w == null) return;
        var f = w.forwardFriction; f.stiffness = forward; w.forwardFriction = f;
        var s = w.sidewaysFriction; s.stiffness = sideways; w.sidewaysFriction = s;
        if (!Application.isPlaying) EditorUtility.SetDirty(w);
    }

    // ---------------------------------------------------------------- the test

    const string Log = "Logs/van-handling.log";
    static readonly Vector3 Start = new Vector3(130.5f, 0.35f, -86f);            // the Prologue's long straight
    static readonly Vector3 Dir = new Vector3(-0.571f, 0f, 0.821f).normalized;

    public static string Measure(string label) => Run(label, null);

    // the same test with these numbers put on the van first (this Play session only)
    public static string Measure(string label, int brake, float forward, float sideFront, float sideRear, int steer) =>
        Run($"{label} [brake {brake}, grip {forward} / {sideFront} / {sideRear}, steer {steer}]", c => Apply(c, brake, forward, sideFront, sideRear, steer));

    static string Run(string label, System.Action<PrometeoCarController> setup)
    {
        if (!EditorApplication.isPlaying) return "enter Play in the Prologue first";
        var van = Object.FindFirstObjectByType<PrologueVan>();
        if (van == null || !van.InVan) return "no van being driven (Prologue, in the van)";
        var ctl = van.controller; var rb = van.body;
        var life = GameObject.Find("StreetLife"); if (life != null) life.SetActive(false);     // an empty street
        var gs = Object.FindFirstObjectByType<GrandmaScare>(); if (gs != null) gs.enabled = false;
        setup?.Invoke(ctl);
        // the test drives the van through the controller's own methods; the controller itself (keys) is off
        ctl.enabled = false;
        ctl.CancelInvoke();
        WheelCollider[] wheels = { ctl.frontLeftCollider, ctl.frontRightCollider, ctl.rearLeftCollider, ctl.rearRightCollider };

        void Place(float kmh)
        {
            foreach (var w in wheels) { w.brakeTorque = 0f; w.motorTorque = 0f; w.steerAngle = 0f; }
            rb.position = Start; rb.rotation = Quaternion.LookRotation(Dir);
            van.transform.SetPositionAndRotation(Start, Quaternion.LookRotation(Dir));
            Physics.SyncTransforms();
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity = Dir * (kmh / 3.6f);
        }
        void Hold(float kmh) => rb.linearVelocity = Dir * (kmh / 3.6f) + Vector3.up * rb.linearVelocity.y;   // while the suspension settles

        int phase = 0; float t0 = 0f, settle = 0f, peakYaw = 0f, yawSum = 0f, minUp = 1f, slew = 0f, h0 = 0f; int yawN = 0; Vector3 p0 = Vector3.zero;
        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= tick; return; }
            float speed = rb.linearVelocity.magnitude;
            switch (phase)
            {
                case 0: Place(80f); settle = Time.time + 0.25f; phase = 1; break;
                case 1: Hold(80f); if (Time.time >= settle) { t0 = Time.time; p0 = rb.position; h0 = rb.rotation.eulerAngles.y; phase = 2; } break;
                case 2:
                    ctl.Brakes();
                    slew = Mathf.Max(slew, Mathf.Abs(Mathf.DeltaAngle(h0, rb.rotation.eulerAngles.y)));
                    if (speed < 0.3f || Time.time - t0 > 15f)
                    {
                        float t = Time.time - t0;
                        File.AppendAllText(Log, $"{label} | stop from 80: {t:F2} s, {Vector3.Distance(p0, rb.position):F1} m ({80f / 3.6f / Mathf.Max(0.01f, t):F1} m/s2), nose swung {slew:F1} deg\n");
                        phase = 3;
                    }
                    break;
                case 3: Place(60f); settle = Time.time + 0.25f; phase = 4; break;
                case 4: Hold(60f); if (Time.time >= settle) { t0 = Time.time; p0 = rb.position; h0 = rb.rotation.eulerAngles.y; phase = 5; } break;
                case 5:
                    ctl.GoForward(); ctl.TurnLeft();
                    float yaw = Mathf.Abs(rb.angularVelocity.y) * Mathf.Rad2Deg;
                    peakYaw = Mathf.Max(peakYaw, yaw);
                    minUp = Mathf.Min(minUp, Vector3.Dot(rb.transform.up, Vector3.up));
                    if (Time.time - t0 > 0.6f) { yawSum += yaw; yawN++; }
                    if (Time.time - t0 > 1.2f)
                    {
                        float turned = Mathf.Abs(Mathf.DeltaAngle(h0, rb.rotation.eulerAngles.y));
                        float slide = Vector3.Angle(rb.linearVelocity, rb.transform.forward);
                        File.AppendAllText(Log, $"{label} | full lock at 60 for 1.2 s: turned {turned:F1} deg, yaw peak {peakYaw:F1} then {(yawN > 0 ? yawSum / yawN : 0f):F1} deg/s, sliding {slide:F1} deg, {speed * 3.6f:F0} km/h, most tilted {Mathf.Acos(Mathf.Clamp01(minUp)) * Mathf.Rad2Deg:F1} deg\n");
                        foreach (var w in wheels) { w.motorTorque = 0f; w.steerAngle = 0f; }
                        ctl.Brakes();
                        phase = 6;
                    }
                    break;
                default:
                    File.AppendAllText(Log, label + " | done\n");
                    EditorApplication.update -= tick;
                    break;
            }
        };
        EditorApplication.update += tick;
        return "measuring: " + label;
    }
}
