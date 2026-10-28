using UnityEngine;

// The imported apartment's furniture is low. Lower the view only while inside its bounds.
public class ApartmentEyeHeight : MonoBehaviour
{
    public Bounds localArea = new Bounds(new Vector3(2.5f, 1.5f, 0f), new Vector3(12f, 5f, 16f));
    public float eyeHeight = 1.30f;
    FirstPersonController player;
    Vector3 original;
    bool inside;

    void Update()
    {
        if (player == null) player = FindFirstObjectByType<FirstPersonController>();
        if (player == null || player.joint == null) return;
        bool next = localArea.Contains(transform.InverseTransformPoint(player.transform.position));
        if (next == inside) return;
        if (next)
        {
            original = player.ViewRestPosition;
            var body = player.GetComponent<CapsuleCollider>();
            if (body == null) return;
            float feet = player.transform.TransformPoint(body.center - Vector3.up * body.height * .5f).y;
            // Measure from the capsule's feet, not a spawn marker that may still be falling.
            float delta = feet + eyeHeight - player.playerCamera.transform.position.y;
            Vector3 offset = player.joint.parent.InverseTransformVector(Vector3.up * delta);
            player.SetViewRestPosition(original + offset);
        }
        else player.SetViewRestPosition(original);
        inside = next;
    }

    void OnDisable()
    {
        if (inside && player != null) player.SetViewRestPosition(original);
        inside = false;
    }
}
