using System.Collections.Generic;
using UnityEngine;

// The barrier round Vane while the black hearts seal him (DemonBoss.HeartPhase): two nested, slightly see-through
// spheres of blood - a rippling texture scrolling opposite ways on each, a wobbling outline, a flash where it's shot -
// with severed body parts (AddDebris) floating inside it, dripping. UseOrb swaps the spheres for the bombs' blood orb.
// Pop() bursts it and flings the parts away.
public class BossShield : MonoBehaviour
{
    System.Func<Vector3> centre;
    float radius, born, popped = -1f, flash;
    Material outer, inner;
    Transform innerSphere;
    static Texture2D ripple;
    static readonly Color OuterCol = new Color(0.45f, 0.02f, 0.03f, 0.3f), OuterGlow = new Color(0.3f, 0.01f, 0.01f);
    static readonly Color InnerCol = new Color(0.6f, 0.04f, 0.04f, 0.18f), InnerGlow = new Color(0.18f, 0f, 0f);

    class Bit { public Transform t; public Quaternion tilt; public float speed, phase, spin, rad; }
    readonly List<Bit> bits = new List<Bit>();

    public static BossShield Make(System.Func<Vector3> centre, float radius)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = "Boss shield";
        Destroy(g.GetComponent<Collider>());   // shots go through to the boss: the shield only reacts
        var s = g.AddComponent<BossShield>();
        s.centre = centre; s.radius = radius; s.born = Time.time;
        s.outer = Liquid(OuterCol, OuterGlow, 1.6f);
        s.inner = Liquid(InnerCol, InnerGlow, 2.4f);
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = s.outer; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var inn = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(inn.GetComponent<Collider>());
        inn.transform.SetParent(g.transform, false);
        inn.transform.localScale = Vector3.one * 0.93f;
        var ri = inn.GetComponent<Renderer>();
        ri.sharedMaterial = s.inner; ri.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        s.innerSphere = inn.transform;
        return s;
    }

    // body parts (1 m prefabs, scaled to the bubble) floating inside the blood, each drifting slowly on its own tilted
    // loop at its own depth, tumbling
    public void AddDebris(GameObject[] parts, int count)
    {
        if (parts == null || parts.Length == 0) return;
        float big = Mathf.Max(1f, radius / 5f);
        for (int i = 0; i < count; i++)
        {
            var src = parts[i % parts.Length];
            if (src == null) continue;
            var p = Instantiate(src);
            p.name = "Shield part";
            foreach (var c in p.GetComponentsInChildren<Collider>(true)) Destroy(c);
            p.transform.localScale = src.transform.localScale * Random.Range(0.9f, 1.6f) * big;
            bits.Add(new Bit { t = p.transform, tilt = Quaternion.Euler(Random.Range(-60f, 60f), Random.value * 360f, Random.Range(-60f, 60f)),
                               speed = Random.Range(8f, 20f) * (Random.value < 0.5f ? -1f : 1f), phase = i * 360f / count, spin = Random.Range(15f, 45f),
                               rad = Random.Range(0.3f, 0.8f) });
        }
    }

    // the bombs' blood orb as the shell instead of the two plain spheres, made see-through so he and the parts show
    // inside it
    public void UseOrb(GameObject orbModel)
    {
        if (orbModel == null) return;
        var g = Instantiate(orbModel, transform);
        g.name = "Shield orb";
        g.SetActive(true);
        foreach (var c in g.GetComponentsInChildren<Collider>(true)) Destroy(c);
        var rs = g.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) { Destroy(g); return; }
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        g.transform.localScale *= 1f / Mathf.Max(0.01f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));   // diameter 1: the shield's own scale sizes it
        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        g.transform.position += transform.position - b.center;
        foreach (var r in rs)
            r.sharedMaterials = System.Array.ConvertAll(r.sharedMaterials, m => m == null ? null : SeeThrough(m, orbMats));
        GetComponent<Renderer>().enabled = false;
        innerSphere.GetComponent<Renderer>().enabled = false;
    }

    readonly List<Material> orbMats = new List<Material>();
    static Material SeeThrough(Material src, List<Material> keep)
    {
        var m = new Material(src);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        if (m.HasProperty("_BaseColor")) { var c = m.GetColor("_BaseColor"); c.a = Mathf.Min(c.a, 0.35f); m.SetColor("_BaseColor", c); }
        keep.Add(m);
        return m;
    }

    static Material Liquid(Color col, Color emission, float tiling)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_EMISSION");
        m.renderQueue = 3000;
        m.SetFloat("_Smoothness", 0.95f);
        m.SetColor("_BaseColor", col);
        m.SetColor("_EmissionColor", emission);
        var t = Ripple();
        m.SetTexture("_BaseMap", t); m.mainTexture = t;
        m.mainTextureScale = new Vector2(tiling, tiling);
        return m;
    }

    // a tileable swirl of lighter and clearer patches
    static Texture2D Ripple()
    {
        if (ripple != null) return ripple;
        const int n = 128;
        ripple = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n, tau = Mathf.PI * 2f;
                float a = Mathf.Sin(tau * (3f * u + 1.5f * Mathf.Sin(tau * 2f * v)));
                float b = Mathf.Sin(tau * (2f * v + 1.2f * Mathf.Sin(tau * 3f * u)));
                float h = Mathf.Clamp01(0.5f + 0.25f * (a + b));
                px[y * n + x] = new Color(1f, 1f, 1f, 0.25f + 0.75f * h * h);
            }
        ripple.SetPixels(px); ripple.Apply();
        return ripple;
    }

    public void Flash() => flash = 1f;
    public void Pop()
    {
        if (popped >= 0f) return;
        popped = Time.time;
        // the parts are flung out across the deck
        foreach (var b in bits)
        {
            if (b.t == null) continue;
            var rb = b.t.gameObject.AddComponent<Rigidbody>();
            rb.mass = 20f;
            rb.linearVelocity = ((b.t.position - transform.position).normalized + Vector3.up * 0.5f) * Random.Range(8f, 14f);
            rb.angularVelocity = Random.insideUnitSphere * 8f;
            b.t.gameObject.AddComponent<SphereCollider>().radius = 0.3f;
            Destroy(b.t.gameObject, 6f);
            BloodFx.Spray(b.t.position, Vector3.up, 10, 4f);
        }
        bits.Clear();
    }

    void Update()
    {
        if (centre == null) { Destroy(gameObject); return; }
        float t = Time.time;
        float grow = Mathf.SmoothStep(0f, 1f, (t - born) / 1.4f);
        float burst = popped < 0f ? 0f : (t - popped) / 0.5f;
        if (burst >= 1f) { Destroy(gameObject); return; }
        float d = radius * 2f * grow * (1f + burst * 0.35f);
        // it wobbles like a drop of liquid
        Vector3 c = centre();
        transform.position = c;
        transform.localScale = new Vector3(d * (1f + 0.035f * Mathf.Sin(t * 2.9f)), d * (1f + 0.035f * Mathf.Sin(t * 2.1f + 2f)), d * (1f + 0.03f * Mathf.Sin(t * 3.7f + 1f)));
        transform.Rotate(0f, 8f * Time.deltaTime, 0f);
        innerSphere.localRotation = Quaternion.Euler(0f, -t * 14f, t * 6f);
        flash = Mathf.Max(0f, flash - Time.deltaTime * 3f);
        outer.mainTextureOffset += new Vector2(0.04f, 0.07f) * Time.deltaTime;
        inner.mainTextureOffset -= new Vector2(0.06f, 0.03f) * Time.deltaTime;
        float fade = 1f - burst;
        outer.SetColor("_BaseColor", new Color(OuterCol.r, OuterCol.g, OuterCol.b, (OuterCol.a + flash * 0.3f) * fade));
        outer.SetColor("_EmissionColor", OuterGlow * (1f + flash * 3f + burst * 3f));
        inner.SetColor("_BaseColor", new Color(InnerCol.r, InnerCol.g, InnerCol.b, (InnerCol.a + flash * 0.15f) * fade));

        foreach (var b in bits)
        {
            if (b.t == null) continue;
            float a = (b.phase + t * b.speed) * Mathf.Deg2Rad;
            b.t.position = c + b.tilt * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * grow * b.rad + Vector3.up * Mathf.Sin(t * 0.7f + b.phase) * radius * 0.08f;
            b.t.Rotate(b.spin * Time.deltaTime, b.spin * 0.6f * Time.deltaTime, 0f, Space.Self);
            if (Random.value < 0.4f * Time.deltaTime) BloodFx.Spray(b.t.position, Vector3.down, 4, 1f);   // dripping
        }
    }

    void OnDestroy()
    {
        foreach (var b in bits) if (b.t != null) Destroy(b.t.gameObject);
        if (outer != null) Destroy(outer);
        if (inner != null) Destroy(inner);
        foreach (var m in orbMats) if (m != null) Destroy(m);
    }
}
