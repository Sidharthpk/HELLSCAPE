using UnityEngine;

// Keeps a world-space label (e.g. the car's "Press F" prompt) turned toward the viewer so it never reads backwards.
public class FaceViewer : MonoBehaviour
{
    public Transform viewer;   // the player camera

    void LateUpdate()
    {
        if (viewer == null) return;
        Vector3 away = transform.position - viewer.position;
        away.y = 0f;
        if (away.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(away);
    }
}
