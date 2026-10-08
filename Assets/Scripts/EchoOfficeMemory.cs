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
                bool inOffice = doorRoots.Exists(t => r.transform.IsChildOf(t));
                for (var p = r.transform; p != null; p = p.parent)
                    if (p.name == "Sketchfab_2023_11_11_16_15_44" ||
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
        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.value = settings.exposure;
        color.colorFilter.value = settings.tint;
        color.contrast.value = -18f; color.saturation.value = -22f;
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.value = .85f; bloom.threshold.value = .65f;
        bloom.scatter.value = .75f; bloom.tint.value = new Color(.7f, .85f, 1f);
        var blur = profile.Add<DepthOfField>(true);
        blur.mode.value = DepthOfFieldMode.Gaussian;
        blur.gaussianStart.value = 1.5f; blur.gaussianEnd.value = 11f;
        blur.gaussianMaxRadius.value = 1.35f; blur.highQualitySampling.value = true;
        var vignette = profile.Add<Vignette>(true);
        vignette.color.value = new Color(.23f, .4f, .65f);
        vignette.intensity.value = .22f; vignette.smoothness.value = .7f;
        var lift = profile.Add<LiftGammaGain>(true);
        lift.lift.value = new Vector4(.88f, .94f, 1f, .07f);
        // Suppress the city fog pass while the blue lifted shadows supply the memory haze.
        var haze = profile.Add<PSX.Fog>(true);
        haze.fogColor.value = new Color(.56f, .73f, .94f);
        haze.ambientColor.value = Color.white;
        haze.fogDensity.value = 0f; haze.fogNear.value = 2f; haze.fogFar.value = 35f;
        haze.noiseStrength.value = 0f;
    }

    public void SetEcho(bool on)
    {
        echo = on;
        foreach (var pair in on ? memory : original)
            if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
    }

    void Update()
    {
        if (volume == null) return;
        volume.weight = Mathf.MoveTowards(volume.weight, echo ? 1f : 0f,
            Time.unscaledDeltaTime / Mathf.Max(.1f, settings.transitionSeconds));
    }

    void OnDisable()
    {
        SetEcho(false);
        if (volume != null) volume.weight = 0f;
    }

    void OnDestroy()
    {
        if (profile == null) return;
        foreach (var component in profile.components) if (component != null) Destroy(component);
        Destroy(profile);
    }
}
