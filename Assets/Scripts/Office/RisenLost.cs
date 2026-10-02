using UnityEngine;

// Someone you let drown in the red city, back as one of the Lost: a blue ghost of them rising out of the blood,
// hanging a few metres over it (over the surface while it's still rising), turning to watch you, flickering.
public class RisenLost : MonoBehaviour
{
    public BloodFlood flood;                // hovers above its surface (optional)
    public Transform watch;                 // the player
    public float above = 3.5f;
    public float riseTime = 5f;
    public Vector2 alphaRange = new Vector2(0.15f, 0.45f);

    private float born, seed, startY;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;

    void Start()
    {
        born = Time.time;
        seed = Random.value * 50f;
        startY = transform.position.y;
        renderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        float k = Mathf.SmoothStep(0f, 1f, (Time.time - born) / riseTime);
        float floor = flood != null ? flood.SurfaceY : startY;
        float y = Mathf.Lerp(startY, Mathf.Max(startY, floor) + above, k) + Mathf.Sin((Time.time + seed) * 0.6f) * 0.3f * k;
        transform.position = new Vector3(transform.position.x, y, transform.position.z);

        if (watch != null)
        {
            Vector3 d = watch.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 0.8f * Time.deltaTime);
        }

        if (block == null) block = new MaterialPropertyBlock();
        float a = Mathf.Lerp(alphaRange.x, alphaRange.y, Mathf.PerlinNoise(seed, Time.time * 1.5f)) * Mathf.Clamp01(k * 2f);
        foreach (var r in renderers)
        {
            if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty("_BaseColor")) continue;
            r.GetPropertyBlock(block);
            var c = r.sharedMaterial.GetColor("_BaseColor");
            c.a = a;
            block.SetColor("_BaseColor", c);
            r.SetPropertyBlock(block);
        }
    }
}
