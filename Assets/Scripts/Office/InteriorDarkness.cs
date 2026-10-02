using UnityEngine;
using UnityEngine.Rendering;

// Interior lighting. The models' walls and roofs are one-sided, so the sun shines straight through them and the
// sky's ambient light fills every room. While the player is inside `zone` this kills the sun and sets the zone's
// own ambient light, haze colour and fog density (the office: a dim orange-red; the silo: brighter, thinner haze
// so you can see where you're running); stepping out puts the outside look back exactly as it was.
// Several zones can exist: the outside look is captured once, shared, while no zone is active.
// With `power` on (the office fuses are in) the powered ambient is used.
public class InteriorDarkness : MonoBehaviour
{
    public Transform player;
    public Bounds zone;                     // world space
    public Light sun;
    public Color darkAmbient = new Color(0.3f, 0.13f, 0.05f);        // ember glow, power off
    public Color poweredAmbient = new Color(0.38f, 0.19f, 0.08f);    // after the fuses: warmer, a bit brighter
    public Color darkFog = new Color(0.34f, 0.14f, 0.05f);           // orange-red haze
    public float fogDensity = -1f;          // < 0: leave the outside density alone
    public float blend = 0.4f;              // seconds to settle
    [Range(0.3f, 1.5f)] public float dim = 0.85f;        // the zone's ambient light, scaled (lower = darker)
    [Range(0.3f, 1.5f)] public float lightDim = 0.85f;   // every lamp inside the zone, scaled once at the start

    private bool inside, powered;

    void Start()
    {
        if (Mathf.Approximately(lightDim, 1f)) return;
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional || !zone.Contains(l.transform.position)) continue;
            if (player != null && l.transform.IsChildOf(player)) continue;   // not a light carried with the player
            if (l.TryGetComponent(out LightFlicker f)) f.ScaleBase(lightDim);
            else l.intensity *= lightDim;
        }
    }
    private float k;                        // 0 outside .. 1 fully inside

    // the outside look, shared by every zone
    private static int activeZones;
    private static float sunIntensity, reflection, density;
    private static AmbientMode ambientMode;
    private static Color ambient, fog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => activeZones = 0;

    public void SetPowered(bool on) => powered = on;

    void Update()
    {
        if (player == null) return;
        bool now = player.gameObject.activeInHierarchy && zone.Contains(player.position);
        if (now && !inside && k <= 0f)
        {
            if (activeZones == 0) Capture();
            activeZones++;
        }
        inside = now;

        float target = inside ? 1f : 0f;
        if (Mathf.Approximately(k, target) && !inside) return;
        k = Mathf.MoveTowards(k, target, Time.deltaTime / Mathf.Max(0.01f, blend));
        Apply();
        if (k <= 0f && !inside) activeZones = Mathf.Max(0, activeZones - 1);
    }

    // remember the outside look (it changes between day, hell and the ending)
    static void Capture(Light sun)
    {
        sunIntensity = sun != null ? sun.intensity : 0f;
        ambientMode = RenderSettings.ambientMode;
        ambient = RenderSettings.ambientLight;
        fog = RenderSettings.fogColor;
        density = RenderSettings.fogDensity;
        reflection = RenderSettings.reflectionIntensity;
    }

    void Capture() => Capture(sun);

    void Apply()
    {
        if (sun != null) sun.intensity = Mathf.Lerp(sunIntensity, 0f, k);
        RenderSettings.ambientMode = k > 0f ? AmbientMode.Flat : ambientMode;
        RenderSettings.ambientLight = Color.Lerp(ambient, (powered ? poweredAmbient : darkAmbient) * dim, k);
        RenderSettings.fogColor = Color.Lerp(fog, darkFog, k);
        if (fogDensity >= 0f) RenderSettings.fogDensity = Mathf.Lerp(density, fogDensity, k);
        RenderSettings.reflectionIntensity = Mathf.Lerp(reflection, 0.05f, k);
    }
}
