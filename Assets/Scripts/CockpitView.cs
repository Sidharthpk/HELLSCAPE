using UnityEngine;

// First-person driving view for the van (the Prologue only: other scenes keep the chase camera). Sits on the car camera and, every frame
// after CameraFollow, puts it at the driver's eyes, lets the mouse look around a little, and turns the steering wheel with the steering.
[DefaultExecutionOrder(100)]
public class CockpitView : MonoBehaviour
{
    public PrometeoCarController car;
    public Transform wheel;                                     // the steering wheel mesh (volant.002)
    public Vector3 eye = new Vector3(-0.45f, 1.6f, 0.55f);      // the driver's eyes, in car space
    public float fov = 70f, lookYaw = 80f, lookPitch = 35f, wheelTurn = 360f, wheelSpeed = 100f;   // wheelTurn = degrees at full lock, wheelSpeed = degrees per second

    float yaw, pitch, wheelAngle, spin;
    Vector3 pivot, axis;

    void Start()
    {
        if (wheel == null) return;
        var m = wheel.GetComponent<MeshFilter>().sharedMesh.bounds;
        pivot = m.center;
        // (the wheel's disc faces its local z, the thinnest side of the mesh; pointed away from the driver, a positive turn is clockwise = right)
        axis = Vector3.Dot(wheel.TransformDirection(Vector3.forward), car.transform.forward) >= 0f ? Vector3.forward : Vector3.back;
    }

    // (CarInteract turns only the Camera component on and off, so this checks it each frame)
    void LateUpdate()
    {
        var cam = GetComponent<Camera>();
        if (car == null || !cam.enabled) { Straighten(); yaw = pitch = 0f; return; }
        yaw = Mathf.Clamp(yaw + Input.GetAxis("Mouse X") * 2f, -lookYaw, lookYaw);
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2f, -lookPitch, lookPitch);
        if (Input.GetKey(KeyCode.C)) yaw = pitch = 0f;   // (centre the view)
        cam.nearClipPlane = 0.05f; cam.fieldOfView = fov;
        transform.SetPositionAndRotation(car.transform.TransformPoint(eye), car.transform.rotation * Quaternion.Euler(pitch, yaw, 0f));

        if (wheel == null) return;
        float want = car.frontLeftCollider.steerAngle / Mathf.Max(1, car.maxSteeringAngle) * wheelTurn;
        wheelAngle = Mathf.MoveTowards(wheelAngle, -want, wheelSpeed * Time.deltaTime);   // (a hand turns it at a human pace, so a tap doesn't spin it)
        wheel.RotateAround(wheel.TransformPoint(pivot), wheel.TransformDirection(axis), wheelAngle - spin);
        spin = wheelAngle;
    }

    void Straighten()   // (out of the van: the wheel goes back to straight)
    {
        if (wheel != null && spin != 0f) wheel.RotateAround(wheel.TransformPoint(pivot), wheel.TransformDirection(axis), -spin);
        spin = wheelAngle = 0f;
    }
}
