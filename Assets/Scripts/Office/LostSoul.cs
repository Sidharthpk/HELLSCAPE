using UnityEngine;

// A lost soul hanging in the air over the hell city: frozen in a floating pose, bobbing, drifting slowly around
// the spot it was placed and turning in the wind, flickering like the office ghosts. Scenery only.
public class LostSoul : MonoBehaviour
{
    public float driftRadius = 7f;
    public float driftSpeed = 0.6f;
    public float bobHeight = 0.5f;
    public float bobSpeed = 0.5f;
    public Vector2 alphaRange = new Vector2(0.12f, 0.4f);

    private Vector3 home;
    private float seed, time;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;

    void Start()
    {
        home = transform.position;
        seed = Random.value * 50f;
        renderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        time += Time.deltaTime * driftSpeed / Mathf.Max(driftRadius, 0.1f);
        Vector3 target = home + new Vector3(Mathf.PerlinNoise(seed, time) - 0.5f, 0f, Mathf.PerlinNoise(time, seed + 7f) - 0.5f) * 2f * driftRadius;
        target.y = home.y + Mathf.Sin((Time.time + seed) * bobSpeed) * bobHeight;
        Vector3 step = Vector3.MoveTowards(transform.position, target, driftSpeed * Time.deltaTime * 2f);
        Vector3 dir = step - transform.position; dir.y = 0f;
        transform.position = step;
        if (dir.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 0.6f * Time.deltaTime);

        if (block == null) block = new MaterialPropertyBlock();
        float a = Mathf.Lerp(alphaRange.x, alphaRange.y, Mathf.PerlinNoise(seed, Time.time * 1.5f));
        foreach (var r in renderers)
        {
            r.GetPropertyBlock(block);
            var c = r.sharedMaterial.GetColor("_BaseColor");
            c.a = a;
            block.SetColor("_BaseColor", c);
            r.SetPropertyBlock(block);
        }
    }
}
