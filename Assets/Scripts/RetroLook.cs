using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The game's whole look, PS3-era: full-size, smoothly filtered textures, a filmic tonemap, soft bloom on lamps and
// emissives, a gentle vignette and a faint film grain. Each scene's own volumes (day / hell colour grading) sit on top.
// Depth in the office is InteriorDarkness' fog.
public class RetroLook : MonoBehaviour
{
    const float Grain = 0.15f;

    static FilmGrain grain;

    // pause menu accessibility option: the grain off is a calmer picture
    public static void SetGrain(bool on)
    {
        if (grain != null) grain.intensity.Override(on ? Grain : 0f);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        QualitySettings.globalTextureMipmapLimit = 0;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        var go = new GameObject("Retro look");
        DontDestroyOnLoad(go);
        var v = go.AddComponent<Volume>();
        v.isGlobal = true; v.priority = -10f;
        var p = ScriptableObject.CreateInstance<VolumeProfile>();

        var tone = p.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);

        var bloom = p.Add<Bloom>(true);
        bloom.threshold.Override(1.1f);
        bloom.intensity.Override(0.6f);
        bloom.scatter.Override(0.65f);
        bloom.highQualityFiltering.Override(true);

        var vig = p.Add<Vignette>(true);
        vig.intensity.Override(0.22f);
        vig.smoothness.Override(0.45f);

        var g = grain = p.Add<FilmGrain>(true);
        g.type.Override(FilmGrainLookup.Thin1);
        g.intensity.Override(PlayerPrefs.GetInt("hs.grain", 1) == 1 ? Grain : 0f);
        g.response.Override(0.8f);

        v.sharedProfile = p;
    }
}
