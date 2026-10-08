using UnityEngine;

// The bubble round Kessler / Tomas while the black hearts seal him (DemonBoss.HeartPhase): two nested, slightly
// see-through spheres of purple "liquid" - a rippling texture scrolling opposite ways on each, a wobbling outline,
// a flash where it's shot. Pop() bursts it.
public class BossShield : MonoBehaviour
{
    System.Func<Vector3> centre;
    float radius, born, popped = -1f, flash;
    Material outer, inner;
    Transform innerSphere;
    static Texture2D ripple;

    public static BossShield Make(System.Func<Vector3> centre, float radius)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = "Boss shield";
        Destroy(g.GetComponent<Collider>());   // shots go through to the boss: the shield only reacts
        var s = g.AddComponent<BossShield>();
        s.centre = centre; s.radius = radius; s.born = Time.time;
        s.outer = Liquid(new Color(0.5f, 0.2f, 1f, 0.22f), new Color(0.25f, 0.05f, 0.55f), 1.6f);
        s.inner = Liquid(new Color(0.8f, 0.3f, 1f, 0.12f), new Color(0.2f, 0.02f, 0.4f), 2.4f);
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
    public void Pop() { if (popped < 0f) popped = Time.time; }

    void Update()
    {
        if (centre == null) { Destroy(gameObject); return; }
        float t = Time.time;
        float grow = Mathf.SmoothStep(0f, 1f, (t - born) / 1.4f);
        float burst = popped < 0f ? 0f : (t - popped) / 0.5f;
        if (burst >= 1f) { Destroy(gameObject); return; }
        float d = radius * 2f * grow * (1f + burst * 0.35f);
        // it wobbles like a drop of liquid
        transform.position = centre();
        transform.localScale = new Vector3(d * (1f + 0.035f * Mathf.Sin(t * 2.9f)), d * (1f + 0.035f * Mathf.Sin(t * 2.1f + 2f)), d * (1f + 0.03f * Mathf.Sin(t * 3.7f + 1f)));
        transform.Rotate(0f, 8f * Time.deltaTime, 0f);
        innerSphere.localRotation = Quaternion.Euler(0f, -t * 14f, t * 6f);
        flash = Mathf.Max(0f, flash - Time.deltaTime * 3f);
        outer.mainTextureOffset += new Vector2(0.04f, 0.07f) * Time.deltaTime;
        inner.mainTextureOffset -= new Vector2(0.06f, 0.03f) * Time.deltaTime;
        float fade = 1f - burst;
        outer.SetColor("_BaseColor", new Color(0.5f, 0.2f, 1f, (0.22f + flash * 0.3f) * fade));
        outer.SetColor("_EmissionColor", new Color(0.25f, 0.05f, 0.55f) * (1f + flash * 3f + burst * 3f));
        inner.SetColor("_BaseColor", new Color(0.8f, 0.3f, 1f, (0.12f + flash * 0.15f) * fade));
    }

    void OnDestroy()
    {
        if (outer != null) Destroy(outer);
        if (inner != null) Destroy(inner);
    }
}
