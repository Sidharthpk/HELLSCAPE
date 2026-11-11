using System.Collections.Generic;
using UnityEngine;

// Recolours a Particle Pack effect as it is spawned: every colour in it is moved onto one hue, keeping how bright and how
// see-through it was. The particles' start colour carries the tint (dark or light); the materials take only the hue (so
// the two multiplied together don't darken it twice); colour-over-life is left as brightness. Used to turn the pack's
// ice, fire and gas into blood for Vane's attacks (DemonBossBlood). Materials are copied once per colour and shared.
public static class BloodTint
{
    static readonly Dictionary<(Material, Color), Material> mats = new Dictionary<(Material, Color), Material>();

    // once: the pack's effects loop; off here so each is one bang. life > 0: destroyed after that many seconds.
    public static GameObject Spawn(GameObject prefab, Vector3 at, Quaternion rot, Color tint, float scale = 1f, float life = 0f, bool once = true)
    {
        var g = Object.Instantiate(prefab, at, rot);
        g.transform.localScale = prefab.transform.localScale * scale;
        Color hue = Hue(tint);
        // plain meshes riding along in the pack's prefabs (IceLance's "IceBall" sphere) show up as a ball: particles only
        foreach (var mr in g.GetComponentsInChildren<MeshRenderer>(true)) Object.Destroy(mr.gameObject);
        foreach (var ps in g.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.name == "Shockwave") { Object.Destroy(ps.gameObject); continue; }   // its distortion sphere renders as a white bubble
            var m = ps.main;
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            if (once) m.loop = false;
            m.startColor = Tint(m.startColor, tint);
            var col = ps.colorOverLifetime;
            if (col.enabled) col.color = Tint(col.color, Color.white);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r != null) r.sharedMaterials = System.Array.ConvertAll(r.sharedMaterials, x => Mat(x, hue));
        }
        foreach (var l in g.GetComponentsInChildren<Light>(true)) l.color = hue;
        if (life > 0f) Object.Destroy(g, life);
        return g;
    }

    // a copy of the material with every colour on it moved to this colour (brightness kept). The IceLance's shader
    // graphs (Ice Shapes, Ice Ball) are glassy and refract: they read as ice whatever colour they get, so those become
    // a wet, faintly glowing URP Lit blood instead.
    public static Material Mat(Material src, Color tint)
    {
        if (src == null) return null;
        if (mats.TryGetValue((src, tint), out var m) && m != null) return m;
        if (src.shader.name.StartsWith("Shader Graphs/Ice"))
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = src.name + " (blood)" };
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", 0.6f);
            m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");   // (no reflections in the city: a mirror finish went black)
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", tint * 1.4f);   // the bridge has no lamps: it has to carry its own light
            mats[(src, tint)] = m;
            return m;
        }
        m = new Material(src) { name = src.name + " (blood)" };
        // lit particles (rock, smoke, debris, cracks) have no light to catch on the lampless bridge and render as black
        // cut-outs; unlit shares their property names, so the texture, blend and colours carry over
        if (src.shader.name == "Universal Render Pipeline/Particles/Lit")
        {
            var unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (unlit != null) { m.shader = unlit; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * new Color(0.6f, 0.6f, 0.6f, 1f)); }
        }
        var sh = m.shader;
        for (int i = 0; i < sh.GetPropertyCount(); i++)
            if (sh.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Color)
            {
                string n = sh.GetPropertyName(i);
                m.SetColor(n, On(m.GetColor(n), tint));
            }
        mats[(src, tint)] = m;
        return m;
    }

    static Color Hue(Color c)
    {
        float v = Mathf.Max(0.0001f, Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
        return new Color(c.r / v, c.g / v, c.b / v, 1f);
    }

    static Color On(Color c, Color tint)
    {
        float v = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        return new Color(tint.r * v, tint.g * v, tint.b * v, c.a);
    }

    static Gradient Tint(Gradient g, Color t)
    {
        var n = new Gradient { mode = g.mode };
        n.SetKeys(System.Array.ConvertAll(g.colorKeys, k => new GradientColorKey(On(k.color, t), k.time)), g.alphaKeys);
        return n;
    }

    static ParticleSystem.MinMaxGradient Tint(ParticleSystem.MinMaxGradient g, Color t)
    {
        switch (g.mode)
        {
            case ParticleSystemGradientMode.Color: return new ParticleSystem.MinMaxGradient(On(g.color, t));
            case ParticleSystemGradientMode.TwoColors: return new ParticleSystem.MinMaxGradient(On(g.colorMin, t), On(g.colorMax, t));
            case ParticleSystemGradientMode.TwoGradients: return new ParticleSystem.MinMaxGradient(Tint(g.gradientMin, t), Tint(g.gradientMax, t));
            case ParticleSystemGradientMode.RandomColor:
                var r = new ParticleSystem.MinMaxGradient(Tint(g.gradient, t)); r.mode = ParticleSystemGradientMode.RandomColor; return r;
            default: return new ParticleSystem.MinMaxGradient(Tint(g.gradient, t));
        }
    }
}
