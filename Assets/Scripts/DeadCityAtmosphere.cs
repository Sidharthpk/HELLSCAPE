using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Extends SleepSequence's exterior look. InteriorDarkness, Echo and cinematics retain ownership.
[DisallowMultipleComponent]
public sealed class DeadCityAtmosphere : MonoBehaviour
{
    public SleepSequence look;
    public HellTrees trees;
    public CarInteract vehicle;
    public Camera playerCamera, drivingCamera;
    public float explorationDensity = .008f, drivingDensity = .0035f, denseDensity = .016f;
    public Color explorationFog = new Color(52f / 255, 79f / 255, 81f / 255);
    public Color drivingFog = new Color(41f / 255, 73f / 255, 76f / 255);
    public Color denseFog = new Color(72f / 255, 91f / 255, 89f / 255);
    public Bounds[] denseStreets = new Bounds[0];
    [Range(0f, .15f)] public float scattering = .035f;
    [Range(0f, .8f)] public float anisotropy = .35f;
    public float scatteringDistance = 80f;
    public bool scatteringEnabled = true;
    [Header("Volumetric fog: density and near colour follow RenderSettings, so cutscenes and the blood tide drive it too")]
    public bool volumetricFog = true;
    public float fogStrength = 1f;
    public float fogTop = 50f, fogHeightFalloff = 15f;           // metres: even from the street to the rooftops, gone a little above them
    public float skyBlendStart = 60f, skyBlendEnd = 280f;        // the fog turns from its own colour into the skybox over this range
    public float FogWeight { get; private set; }                 // fades over a second, so doorways don't pop
    public float shadowDistance = 55f;                           // sun shadows stop here while in the city: the fog has taken them by then
    float shadowDistanceWas;
    public static DeadCityAtmosphere Active { get; private set; }
    readonly HashSet<Light> streetlights = new HashSet<Light>();
    InteriorDarkness[] interiors;
    float appliedDensity;
    Color appliedColor;
    bool applied;
    MaterialPropertyBlock mistProperties;
    public bool IsStreetlight(Light light) => streetlights.Contains(light);   // lamps and tree fires: everything that lights the fog
    public string State { get; private set; } = "Suspended";

    // Called only by SleepSequence when its exterior/blood-tide transition finishes.
    public void RestoreExteriorFog()
    {
        applied = false;
        if (look == null || !look.hellWorld.activeInHierarchy) return;
        RenderSettings.fogDensity = look.hellFogDensity;
        RenderSettings.fogColor = look.hellFogColor;
    }

    void OnEnable()
    {
        Active = this;
        RetroLook.SetGrain(PlayerPrefs.GetInt("hs.grain", 1) == 1);
        var pipeline = UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset;   // (shared with the other scenes: OnDisable puts it back)
        if (pipeline != null) { shadowDistanceWas = pipeline.shadowDistance; pipeline.shadowDistance = shadowDistance; }
        streetlights.Clear();
        if (trees != null)
        {
            foreach (var lamp in trees.lamps) if (lamp != null) streetlights.Add(lamp);
            foreach (var fire in trees.lights) if (fire != null) streetlights.Add(fire);
        }
        interiors =FindObjectsByType<InteriorDarkness>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        applied = false;
    }
    void OnDisable()
    {
        if (Active != this) return;
        Active = null;
        RetroLook.SetGrain(PlayerPrefs.GetInt("hs.grain", 1) == 1);
        var pipeline = UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset;
        if (pipeline != null) pipeline.shadowDistance = shadowDistanceWas;
    }

    public bool Allows(Camera camera)
    {
        if (!isActiveAndEnabled || look == null || !look.deadCityAir || !look.hellWorld.activeInHierarchy) return false;
        if (camera != playerCamera && camera != drivingCamera) return false;
        if (TitleScreen.Showing || SleepSequence.Waking || EndingSequence.Playing || FirstSightCutscene.Playing || GameOverScreen.Open) return false;
        var plot = RevisedPlotDirector.Instance;
        if (plot != null && (plot.Echo || plot.InSequence)) return false;
        if (look.bloodVolume != null && look.bloodVolume.weight > .001f) return false;
        // Active is set at the START of BurstOut, before IsChasing becomes true.
        // Yield for the whole encounter, including its opening camera and transformation.
        if (DemonBoss.Active != null) return false;
        // Both interior blends and temporary camera cuts use different ambient/fog modes.
        if (RenderSettings.ambientMode != AmbientMode.Trilight || RenderSettings.fogMode != FogMode.Exponential) return false;
        if (look.sun != null && (Mathf.Abs(look.sun.intensity - look.hellSunIntensity) > .025f ||
            Vector4.Distance(look.sun.color, look.hellSunColor) > .08f)) return false;
        foreach (var zone in interiors)
            if (zone != null && zone.isActiveAndEnabled && zone.zone.Contains(camera.transform.position)) return false;
        return true;
    }

    // Volumetric fog stays on through the chase, the bridge and cutscenes; only the places that aren't the open city end it.
    public bool FogBelongsHere(Camera camera)
    {
        if (!volumetricFog || !isActiveAndEnabled || look == null || !look.deadCityAir || !look.hellWorld.activeInHierarchy || !RenderSettings.fog) return false;
        if (TitleScreen.Showing || EndingSequence.Playing) return false;
        var plot = RevisedPlotDirector.Instance;
        if (plot != null && plot.Echo) return false;
        if (RenderSettings.ambientMode != AmbientMode.Trilight || RenderSettings.fogMode != FogMode.Exponential) return false;
        foreach (var zone in interiors)
            if (zone != null && zone.isActiveAndEnabled && zone.zone.Contains(camera.transform.position)) return false;
        return true;
    }

    // (CCTV and other render-texture cameras stay clean; the review captures borrow the player camera, so it is always in)
    public bool DrawsFog(Camera camera) => FogWeight > .001f && (camera.targetTexture == null || camera == playerCamera || camera == drivingCamera);

    void LateUpdate()
    {
        var camera = vehicle != null && vehicle.InCar ? drivingCamera : playerCamera;
        if (camera != null) FogWeight = Mathf.MoveTowards(FogWeight, FogBelongsHere(camera) ? 1f : 0f, Time.unscaledDeltaTime);
        if (camera != null) UpdateMist(camera);
        if (camera != null && UpdateChaseVisibility()) return;
        if (camera == null || !Allows(camera)) { State = "Suspended"; return; }
        // Only change values we own. A cutscene changing fog immediately takes precedence.
        bool baseLook = Mathf.Abs(RenderSettings.fogDensity - look.hellFogDensity) < .00005f &&
            Vector4.Distance(RenderSettings.fogColor, look.hellFogColor) < .002f;
        bool ownLook = applied && Mathf.Abs(RenderSettings.fogDensity - appliedDensity) < .00005f &&
            Vector4.Distance(RenderSettings.fogColor, appliedColor) < .002f;
        if (!baseLook && !ownLook) { State = "External fog owner"; return; }
        bool driving = vehicle != null && vehicle.InCar;
        bool dense = false;
        if (!driving) foreach (var zone in denseStreets) if (zone.Contains(camera.transform.position)) dense = true;
        State = driving ? "Driving" : dense ? "Dense horror" : "Exploration";
        float target = driving ? drivingDensity : dense ? denseDensity : explorationDensity;
        Color color = driving ? drivingFog : dense ? denseFog : explorationFog;
        float blend = 1f - Mathf.Exp(-Time.deltaTime * .8f);
        appliedDensity = Mathf.Lerp(RenderSettings.fogDensity, target, blend);
        appliedColor = Color.Lerp(RenderSettings.fogColor, color, blend);
        RenderSettings.fogDensity = appliedDensity; RenderSettings.fogColor = appliedColor;
        applied = true;
    }

    bool UpdateChaseVisibility()
    {
        var boss = DemonBoss.Active;
        if (boss == null || !boss.IsChasing || vehicle == null ||
            (boss.cutsceneCam != null && boss.cutsceneCam.isActiveAndEnabled) ||
            TitleScreen.Showing || GameOverScreen.Open || EndingSequence.Playing || FirstSightCutscene.Playing ||
            RenderSettings.ambientMode != AmbientMode.Trilight || RenderSettings.fogMode != FogMode.Exponential) return false;
        var plot = RevisedPlotDirector.Instance;
        if (plot != null && (plot.Echo || plot.InSequence)) return false;
        bool baseDensity = Mathf.Abs(RenderSettings.fogDensity - look.hellFogDensity) < .00005f;
        bool ownDensity = applied && Mathf.Abs(RenderSettings.fogDensity - appliedDensity) < .00005f;
        if (!baseDensity && !ownDensity) return false;
        // Navigation visibility only. Crimson colour, sky, sun, grade and arena/cutscene fog stay with the encounter.
        float target = vehicle.InCar ? drivingDensity : explorationDensity;
        appliedDensity = Mathf.Lerp(RenderSettings.fogDensity, target, 1f - Mathf.Exp(-Time.deltaTime * .8f));
        RenderSettings.fogDensity = appliedDensity;
        appliedColor = RenderSettings.fogColor; applied = true;
        State = vehicle.InCar ? "Danger driving" : "Danger on foot";
        return true;
    }

    void UpdateMist(Camera camera)
    {
        if (look == null || look.mist == null) return;
        // (the volumetric fog replaces the particle mist, which has no depth of its own and paints over the sky)
        bool hidden = volumetricFog || TitleScreen.Showing || EndingSequence.Playing || HellMist.Strength <= .001f || RenderSettings.ambientMode != AmbientMode.Trilight;
        var plot = RevisedPlotDirector.Instance;
        hidden |= plot != null && plot.Echo;
        foreach (var zone in interiors)
            if (zone != null && zone.isActiveAndEnabled && zone.zone.Contains(camera.transform.position)) hidden = true;
        if (mistProperties == null) mistProperties = new MaterialPropertyBlock();
        float blood = look.bloodVolume != null ? look.bloodVolume.weight : 0f;
        foreach (var mist in look.mist)
        {
            if (mist == null) continue;
            float distance = Vector3.Distance(camera.transform.position, mist.transform.position);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(85f, 120f, distance));
            mist.enabled = !hidden && fade > .001f;
            Color tint = Color.Lerp(look.mistColor, look.bloodMistColor, blood);
            tint.a *= fade * HellMist.Strength;
            mist.GetPropertyBlock(mistProperties); mistProperties.SetColor("_BaseColor", tint); mist.SetPropertyBlock(mistProperties);
        }
    }
}
