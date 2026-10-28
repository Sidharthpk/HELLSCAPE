using UnityEngine;

// The void's jump-scare (EndingSequence.SoulScare): the shade's head turns to stare straight into the camera, over
// whatever its animation is doing. Meshy rigs have a "headfront" bone at the tip of the nose: the head is turned so
// the line to it points at you. A body without those bones is left alone.
public class ShadeStare : MonoBehaviour
{
    public Transform target;    // the camera
    Transform head, face;

    void Awake()
    {
        foreach (var t in GetComponentsInChildren<Transform>())
        {
            if (t.name == "Head" || t.name.EndsWith(":Head")) head = t;
            if (t.name == "headfront") face = t;
        }
    }

    void LateUpdate()
    {
        if (target == null || head == null || face == null) return;
        Vector3 now = face.position - head.position, want = target.position - head.position;
        if (now.sqrMagnitude > 1e-6f && want.sqrMagnitude > 1e-6f) head.rotation = Quaternion.FromToRotation(now, want) * head.rotation;
    }
}
