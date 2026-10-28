using UnityEngine;

// A rain curtain that rides above the active camera and stops under a roof.
[RequireComponent(typeof(ParticleSystem))]
public class RainFollow : MonoBehaviour
{
    public float height = 16f;
    public float roofCheck = 30f;
    ParticleSystem ps;

    void Awake() { ps = GetComponent<ParticleSystem>(); }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null || !cam.isActiveAndEnabled)   // (the van's camera is untagged)
        {
            cam = null;
            foreach (var c in Camera.allCameras) if (c.targetTexture == null && (cam == null || c.depth > cam.depth)) cam = c;
        }
        if (cam == null) return;
        var p = cam.transform.position;
        transform.position = p + Vector3.up * height;
        var em = ps.emission;
        em.enabled = !Physics.Raycast(p, Vector3.up, roofCheck, ~0, QueryTriggerInteraction.Ignore);
    }
}
