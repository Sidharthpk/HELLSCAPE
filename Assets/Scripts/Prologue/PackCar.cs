using UnityEngine;

// A car from the generic passenger car pack (built by HellScape > Cars > Build Car Pack Prefabs): nose along +z, origin
// on the ground at the middle of the car, the four wheels as pivots at their axles so they can spin and steer.
public class PackCar : MonoBehaviour
{
    public Transform body;                  // the bodywork + glass (rolls and pitches on its own)
    public Transform frontLeft, frontRight, rearLeft, rearRight;
    public float wheelRadius = 0.35f;
    public float wheelbase = 2.6f;          // axle to axle
    public float rearAxle = -1.3f;          // z of the rear axle: the car turns about this point
    public Vector3 size = new Vector3(1.8f, 1.4f, 4.4f);   // width, height, length

    float spin;
    Quaternion bodyRest = Quaternion.identity;   // the body's modelled orientation (the lean is applied on top of it)

    void Awake() { if (body != null) bodyRest = body.localRotation; }

    // distance: metres rolled this frame; steer: front wheel angle in degrees
    public void Roll(float distance, float steer)
    {
        spin = Mathf.Repeat(spin + distance / Mathf.Max(0.05f, wheelRadius) * Mathf.Rad2Deg, 360f);
        var roll = Quaternion.Euler(spin, 0f, 0f);
        if (frontLeft != null) frontLeft.localRotation = Quaternion.Euler(0f, steer, 0f) * roll;
        if (frontRight != null) frontRight.localRotation = Quaternion.Euler(0f, steer, 0f) * roll;
        if (rearLeft != null) rearLeft.localRotation = roll;
        if (rearRight != null) rearRight.localRotation = roll;
    }

    // body lean: roll about the long axis (positive = leans right), pitch about the cross axis (positive = nose down)
    public void Lean(float rollDeg, float pitchDeg)
    {
        if (body != null) body.localRotation = Quaternion.Euler(pitchDeg, 0f, -rollDeg) * bodyRest;
    }
}
