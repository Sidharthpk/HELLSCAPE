using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// A private volume blends over the city's existing volumes without modifying their assets.
// Shared material references are swapped, never painted over or cloned each time V is pressed.
public class EchoOfficeMemory : MonoBehaviour
{
    readonly Dictionary<Renderer, Material[]> original = new Dictionary<Renderer, Material[]>();
    readonly Dictionary<Renderer, Material[]> memory = new Dictionary<Renderer, Material[]>();
    EchoOfficeSettings settings;
    Volume volume;
    VolumeProfile profile;
    bool echo;
    UnityEngine.UI.RawImage glow;
    Texture2D tex;
    Material glowMaterial;
    float glowPhase;                      // 0..1 through one breath of the edge glow
    const float GlowPeriod = 3.8f;        // seconds per breath, normally
    const float RushFrom = 2f;            // the last seconds of a visit: the breath speeds up...
    const float RushPeriod = 1.2f, LastPeriod = .35f;   // ...from this to a flicker as it runs out
    EchoVisit visit;
    public int RendererCount => memory.Count;
    public float Weight => volume != null ? volume.weight : 0f;

    void Awake()
    {
        settings = Resources.Load<EchoOfficeSettings>("EchoOfficeSettings");
        if (settings == null) { Debug.LogError("Missing Echo office material settings."); return; }
        // Kessler's door is its own model, outside the office root: it gets the same burnt/clean swap.
        var doorRoots = new List<Transform>();
        foreach (var d in FindObjectsByType<KesslerDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (d.door != null) doorRoots.Add(d.door.transform);
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                // Scope swaps to the authored office, including its inactive children.
                bool inOffice = doorRoots.Exists(t => r.transform.IsChildOf(t));   // (the fuse box model is a scene root of its own: "Fuse")
                for (var p = r.transform; p != null; p = p.parent)
                    if (p.name == "Office Ground Floor" || p.name == "Fuse" && p.parent == null ||
                        p.name == "Office" && p.parent != null && p.parent.name == "OfficeAct") { inOffice = true; break; }
                if (!inOffice) continue;
                var before = r.sharedMaterials;
                var after = (Material[])before.Clone();
                bool changed = false;
                for (int i = 0; i < after.Length; i++)
                    foreach (var pair in settings.materials)
                        if (pair.burnt != null && pair.before != null && after[i] == pair.burnt)
                        { after[i] = pair.before; changed = true; break; }
                if (changed) { original[r] = before; memory[r] = after; }
            }
        var go = new GameObject("Echo memory atmosphere");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true; volume.priority = 1000f; volume.weight = 0f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = profile;
        // No colour grading, bloom or blur: the Echo is only the glowing edge (see BuildGlow); the volume just drives its fade.
        BuildGlow();
    }

    // URP's Vignette only darkens, so the glow is a screen overlay: clear centre, bright blue edges.
    void BuildGlow()
    {
        var canvasGo = new GameObject("Echo glow vignette");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -50;   // under the HUD and dialogue
        // A rounded rectangle follows the screen edges at any aspect ratio. The
        // broad, continuous falloff avoids a hard circular rim or solid side bars.
        const int size = 256;
        tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { name = "Echo soft blue glow", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = Mathf.Abs(2f * x / (size - 1) - 1f);
                float v = Mathf.Abs(2f * y / (size - 1) - 1f);
                float d = Mathf.Pow(u * u * u * u + v * v * v * v, .25f);
                float halo = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.52f, 1.12f, d));
                float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.84f, 1.2f, d));
                Color color = Color.Lerp(new Color(.06f, .24f, 1f), new Color(.22f, .64f, 1f), rim);
                color.a = .36f * halo * halo + .12f * rim;
                pixels[y * size + x] = color;
            }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        glow = new GameObject("Glow").AddComponent<UnityEngine.UI.RawImage>();
        glow.transform.SetParent(canvasGo.transform, false);
        glow.texture = tex; glow.raycastTarget = false;
        var shader = Resources.Load<Shader>("EchoGlow");
        if (shader != null)
        {
            glowMaterial = new Material(shader) { name = "Echo additive glow" };
            glow.material = glowMaterial;
        }
        var rt = glow.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        glow.color = new Color(1f, 1f, 1f, 0f);
        glow.enabled = false;
    }

    public void SetEcho(bool on)
    {
        if (on && !echo && Weight <= 0f) glowPhase = 0f;
        echo = on;
        foreach (var pair in on ? memory : original)
            if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
    }

    void Update()
    {
        if (volume == null) return;
        volume.weight = Mathf.MoveTowards(volume.weight, echo ? 1f : 0f,
            Time.unscaledDeltaTime / Mathf.Max(.1f, settings.transitionSeconds));
        if (glow != null)
        {
            // Start each visit gently; keep the phase continuous during an exit or
            // a quick reversal so toggling realms cannot flash the border.
            // the Echo clock is in the glow: the last two seconds it breathes faster and faster
            if (visit == null) visit = FindFirstObjectByType<EchoVisit>();
            float period = GlowPeriod;
            if (echo && visit != null && !visit.HeldForVane && visit.Remaining <= RushFrom)
                period = Mathf.Lerp(LastPeriod, RushPeriod, visit.Remaining / RushFrom);
            glowPhase = Mathf.Repeat(glowPhase + Time.unscaledDeltaTime / period, 1f);
            float breath = .5f - .5f * Mathf.Cos(glowPhase * 2f * Mathf.PI);
            float pulse = Mathf.Lerp(.06f, .95f, Mathf.SmoothStep(0f, 1f, breath));
            float fade = Mathf.SmoothStep(0f, 1f, volume.weight);
            glow.color = new Color(1f, 1f, 1f, fade * pulse);
            glow.enabled = volume.weight > 0f;
        }
    }

    void OnDisable()
    {
        SetEcho(false);
        if (volume != null) volume.weight = 0f;
        if (glow != null) glow.enabled = false;
    }

    void OnDestroy()
    {
        if (tex != null) Destroy(tex);
        if (glowMaterial != null) Destroy(glowMaterial);
        if (profile == null) return;
        foreach (var component in profile.components) if (component != null) Destroy(component);
        Destroy(profile);
    }
}
