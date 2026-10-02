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

    [Header("Speed FOV Kick")]
    public PrometeoCarController carController;
    public float baseFOV = 60f;
    public float maxFOV = 75f;
    public float maxSpeedReference = 150f;
    public float fovLerpSpeed = 3f;

    private Vector3 currentOffset;

    void Start()
    {
        if (carController == null && carTransform != null)
            carController = carTransform.GetComponent<PrometeoCarController>();

        currentOffset = baseOffset;
    }

    // straight to its place behind the car (getting in, a teleport): no swooping in from wherever it was
    public void Snap()
    {
        if (carTransform == null) return;
        currentOffset = baseOffset;
        transform.position = carTransform.TransformPoint(baseOffset);
        Vector3 fwd = carTransform.forward; fwd.y = 0f;
        transform.rotation = Quaternion.LookRotation(carTransform.position + Vector3.up * lookHeightOffset + fwd.normalized * lookAhead - transform.position, Vector3.up);
    }

    void FixedUpdate()
    {
        float speedPercent = 0f;
        if (carController != null)
            speedPercent = Mathf.Clamp01(Mathf.Abs(carController.carSpeed) / maxSpeedReference);

        // Target offset: further back and slightly higher the faster you're going
        Vector3 targetOffset = baseOffset + new Vector3(0f, maxExtraHeight * speedPercent, -maxExtraDistance * speedPercent);
        currentOffset = Vector3.Lerp(currentOffset, targetOffset, distanceLerpSpeed * Time.deltaTime);

        //Look at car, tilted slightly down onto the roofline
        Vector3 fwd = carTransform.forward; fwd.y = 0f;
        Vector3 lookTarget = carTransform.position + Vector3.up * lookHeightOffset + fwd.normalized * lookAhead;
        Vector3 _lookDirection = lookTarget - transform.position;
        Quaternion _rot = Quaternion.LookRotation(_lookDirection, Vector3.up);
        transform.rotation = Quaternion.Lerp(transform.rotation, _rot, lookSpeed * Time.deltaTime);

        //Move to car, using dynamic offset
        Vector3 _targetPos = carTransform.TransformPoint(currentOffset);
        if (avoidWalls)
        {
            // from just above the car out to where the camera wants to be: stop short of anything in the way
            Vector3 from = carTransform.position + Vector3.up * 1.6f;
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
        transform.position = Vector3.Lerp(transform.position, _targetPos, followSpeed * Time.deltaTime);

        //Speed-based FOV kick
        if (cam != null && carController != null)
        {
            float targetFOV = Mathf.Lerp(baseFOV, maxFOV, speedPercent);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, fovLerpSpeed * Time.deltaTime);
        }
    }

}