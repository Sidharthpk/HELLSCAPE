using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{

    public Transform carTransform;
    public Camera cam; // drag the Camera component on this same object

    [Range(1, 10)]
    public float followSpeed = 6;
    [Range(1, 10)]
    public float lookSpeed = 8;

    [Header("Base Offset")]
    public Vector3 baseOffset = new Vector3(0f, 1.4f, -4.5f); // low, close chase position at rest
    public float lookHeightOffset = 0.8f;
    public float lookAhead = 0f;          // look this far in front of the car (a high camera sees the road coming)

    [Header("Walls")]
    public bool avoidWalls = true;        // pulled in under ceilings and in front of walls (tunnels)
    public float wallPadding = 0.4f;

    [Header("Speed Pull-Back")]
    public float maxExtraDistance = 2.5f; // how much further back it goes at top speed
    public float maxExtraHeight = 0.6f;   // slight extra lift at top speed
    public float distanceLerpSpeed = 2f;  // how smoothly it pulls back/returns
    public float maxPullBack = 1.5f;      // the pull-back never goes past this, whatever maxExtraDistance says

    [Header("Stay Close")]
    public float maxLag = 1.2f;           // the camera never trails its place behind the car by more than this
    public float yawFollowSpeed = 8f;     // how fast the camera swings round behind the car (pitch and roll of the body are ignored)

    [Header("Speed FOV Kick")]
    public PrometeoCarController carController;
    public float baseFOV = 60f;
    public float maxFOV = 75f;
    public float maxSpeedReference = 150f;
    public float fovLerpSpeed = 3f;

    private Vector3 currentOffset;
    private float yaw;

    void Start()
    {
        if (carController == null && carTransform != null)
            carController = carTransform.GetComponent<PrometeoCarController>();

        currentOffset = baseOffset;
        yaw = carTransform != null ? carTransform.eulerAngles.y : 0f;
        // drawn between physics steps, or the car (and so the view) judders at speed
        if (carTransform != null)
        {
            var rb = carTransform.GetComponent<Rigidbody>();
            if (rb != null && rb.interpolation == RigidbodyInterpolation.None) rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    // straight to its place behind the car (getting in, a teleport): no swooping in from wherever it was
    public void Snap()
    {
        if (carTransform == null) return;
        currentOffset = baseOffset;
        yaw = carTransform.eulerAngles.y;
        transform.position = carTransform.TransformPoint(baseOffset);
        Vector3 fwd = carTransform.forward; fwd.y = 0f;
        transform.rotation = Quaternion.LookRotation(carTransform.position + Vector3.up * lookHeightOffset + fwd.normalized * lookAhead - transform.position, Vector3.up);
    }

    void LateUpdate()
    {
        if (carTransform == null) return;
        float dt = Time.deltaTime;
        float speedPercent = 0f;
        if (carController != null)
            speedPercent = Mathf.Clamp01(Mathf.Abs(carController.carSpeed) / maxSpeedReference);

        // Target offset: a little further back and higher the faster you're going (capped, so it never drifts far)
        float extra = Mathf.Min(maxExtraDistance, maxPullBack);
        Vector3 targetOffset = baseOffset + new Vector3(0f, maxExtraHeight * speedPercent, -extra * speedPercent);
        currentOffset = Vector3.Lerp(currentOffset, targetOffset, 1f - Mathf.Exp(-distanceLerpSpeed * dt));

        // follow the car's heading only: bumps, pitch and body roll don't reach the camera
        yaw = Mathf.LerpAngle(yaw, carTransform.eulerAngles.y, 1f - Mathf.Exp(-yawFollowSpeed * dt));
        Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
        Vector3 carPos = carTransform.position;

        Vector3 fwd = carTransform.forward; fwd.y = 0f;
        Vector3 lookTarget = carPos + Vector3.up * lookHeightOffset + fwd.normalized * lookAhead;

        Vector3 _targetPos = carPos + heading * currentOffset;
        if (avoidWalls)
        {
            // from just above the car out to where the camera wants to be: stop short of anything in the way
            Vector3 from = carPos + Vector3.up * 1.6f;
            Vector3 d = _targetPos - from;
            foreach (var h in Physics.SphereCastAll(from, 0.35f, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(carTransform) || h.distance <= 0f) continue;
                if (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic) continue;   // not zombies/ragdolls
                if (h.collider.GetComponentInParent<Zombie>() != null || h.collider.GetComponentInParent<DemonBoss>() != null) continue;
                float cut = Mathf.Max(1f, h.distance - wallPadding);
                if (cut < d.magnitude) d = d.normalized * cut;
            }
            _targetPos = from + d;
        }

        // smooth, then never let it fall further behind its place than maxLag: at speed it stays with the car
        Vector3 pos = Vector3.Lerp(transform.position, _targetPos, 1f - Mathf.Exp(-followSpeed * dt));
        Vector3 lag = pos - _targetPos;
        if (lag.magnitude > maxLag) pos = _targetPos + lag.normalized * maxLag;
        transform.position = pos;

        Quaternion _rot = Quaternion.LookRotation(lookTarget - pos, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, _rot, 1f - Mathf.Exp(-lookSpeed * dt));

        //Speed-based FOV kick
        if (cam != null && carController != null)
        {
            float targetFOV = Mathf.Lerp(baseFOV, maxFOV, speedPercent);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, 1f - Mathf.Exp(-fovLerpSpeed * dt));
        }
    }

}
