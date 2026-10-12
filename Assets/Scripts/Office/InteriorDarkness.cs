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

    [Header("Blackout (the office, until the fuses are in)")]
    public bool blackoutUntilPowered;       // dark: every lamp in the zone is off, only dim red emergency lamps; your hands are the light to see by (PalmLight)
    public Color blackFog = new Color(0.01f, 0.008f, 0.01f);
    public Color emergencyAmbient = new Color(0.14f, 0.025f, 0.02f);   // the red the lamps leave in every corner
    public Color emergencyColor = new Color(1f, 0.06f, 0.04f);
    public float emergencyIntensity = 1.5f, emergencyRange = 14f, emergencyHeight = 1.1f;   // (height above the middle of the zone: just under the ceiling)
    public Vector3[] emergencySpots;        // world, at the ceiling: set = a lamp at each of these only, not one over every room
    public Material emergencyLampMaterial;  // the lamp you see at each spot (none: only its light)
    private GameObject emergency;

    [Header("Fog of war (the office)")]
    public bool fogOfWar;                   // black distance fog, powered or not; the noisy screen fog and the drifting fog particles are off inside
    private Volume noHaze;
    private VolumeProfile noHazeProfile;
    private ParticleSystemRenderer[] mist = new ParticleSystemRenderer[0];

    // the office (the blackout zone) is darker still than its saved dim/lightDim, but never black: ambient and every
    // lamp are scaled by this (the emergency red before the fuses is left as it was). Raise it to lighten the office.
    const float OfficeGloom = 0.65f;
    float Gloom => blackoutUntilPowered ? OfficeGloom : 1f;

    private bool inside, powered;
    private bool blackNow;                  // this zone is blacked out and you're in it
    private readonly System.Collections.Generic.List<Light> switchedOff = new System.Collections.Generic.List<Light>();

    // true while you stand in a blacked-out zone: PalmLight is allowed then
    public static bool Blackout { get; private set; }
    private static int blackZones;

    void Start()
    {
        if (blackoutUntilPowered)
        {
            if (player != null && player.GetComponentInChildren<PunchController>(true) is PunchController pc && pc.GetComponent<PalmLight>() == null) pc.gameObject.AddComponent<PalmLight>();
            Lamps(false);
            // not quite black: a dim red emergency lamp over each room, until the fuses are in
            emergency = new GameObject("Emergency lights");
            emergency.transform.SetParent(transform, false);
            bool spots = emergencySpots != null && emergencySpots.Length > 0;
            int n = spots ? emergencySpots.Length : OfficeMinimap.Rooms.Length;
            for (int i = 0; i < n; i++)
            {
                var l = new GameObject("Emergency light").AddComponent<Light>();
                l.transform.SetParent(emergency.transform, false);
                l.transform.position = spots ? emergencySpots[i] + Vector3.down * 0.35f
                    : new Vector3(zone.center.x + OfficeMinimap.Rooms[i].x, zone.center.y + emergencyHeight, zone.center.z + OfficeMinimap.Rooms[i].z);
                l.type = LightType.Point; l.color = emergencyColor; l.range = emergencyRange; l.intensity = emergencyIntensity; l.shadows = LightShadows.None;
                if (!spots) continue;
                if (i % 2 == 0) l.gameObject.AddComponent<LightFlicker>();   // half of them are failing
                if (emergencyLampMaterial == null) continue;
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(lamp.GetComponent<Collider>());
                lamp.name = "Emergency lamp";
                lamp.transform.SetParent(emergency.transform, false);
                lamp.transform.position = emergencySpots[i] + Vector3.down * 0.06f;
                lamp.transform.localScale = new Vector3(0.34f, 0.12f, 0.16f);
                lamp.GetComponent<Renderer>().sharedMaterial = emergencyLampMaterial;
            }
        }
        if (fogOfWar)
        {
            // the city's screen fog (grey, noisy) is overridden to nothing while you're inside: RenderSettings' fog does the fading
            noHaze = new GameObject("No haze inside").AddComponent<Volume>();
            noHaze.transform.SetParent(transform, false);
            noHaze.isGlobal = true; noHaze.priority = 900f; noHaze.weight = 0f;   // (under the Echo's 1000)
            noHazeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            noHaze.sharedProfile = noHazeProfile;
            var haze = noHazeProfile.Add<PSX.Fog>(true);
            haze.fogDensity.value = 0f; haze.noiseStrength.value = 0f; haze.fogColor.value = Color.black;
            mist = System.Array.ConvertAll(FindObjectsByType<FogFollow>(FindObjectsInactive.Include, FindObjectsSortMode.None), f => f.GetComponent<ParticleSystemRenderer>());
        }
        float lamps = lightDim * Gloom;
        if (Mathf.Approximately(lamps, 1f)) return;
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional || !zone.Contains(l.transform.position)) continue;
            if (player != null && l.transform.IsChildOf(player)) continue;   // not a light carried with the player
            if (l.TryGetComponent(out LightFlicker f)) f.ScaleBase(lamps);
            else l.intensity *= lamps;
        }
    }

    // every lamp in the zone off (or back on, the ones this switched off)
    void Lamps(bool on)
    {
        if (on) { foreach (var l in switchedOff) if (l != null) l.enabled = true; switchedOff.Clear(); return; }
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional || !l.enabled || !zone.Contains(l.transform.position)) continue;
            if (player != null && l.transform.IsChildOf(player)) continue;   // your own light
            if (l.GetComponentInParent<WitnessLook>() != null) continue;     // the burnt dead glow: the only thing to see by
            l.enabled = false;
            switchedOff.Add(l);
        }
    }
    private float k;                        // 0 outside .. 1 fully inside

    // the outside look, shared by every zone
    private static int activeZones;
    private static float sunIntensity, reflection, density;
    private static AmbientMode ambientMode;
    private static Color ambient, fog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { activeZones = 0; blackZones = 0; Blackout = false; }

    public void SetPowered(bool on)
    {
        powered = on;
        if (blackoutUntilPowered && on) Lamps(true);   // the fuses are in: the lights come back
        if (emergency != null) emergency.SetActive(!on);
    }

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
        bool black = blackoutUntilPowered && inside && !powered;
        if (black != blackNow) { blackNow = black; blackZones += black ? 1 : -1; Blackout = blackZones > 0; }

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
        bool blackedOut = blackoutUntilPowered && !powered;
        RenderSettings.ambientLight = Color.Lerp(ambient, blackedOut ? emergencyAmbient : (powered ? poweredAmbient : darkAmbient) * dim * Gloom, k);
        RenderSettings.fogColor = Color.Lerp(fog, blackedOut || fogOfWar ? blackFog : darkFog, k);
        if (noHaze != null) noHaze.weight = k;
        foreach (var m in mist) if (m != null) m.enabled = k <= 0f;
        if (fogDensity >= 0f) RenderSettings.fogDensity = Mathf.Lerp(density, fogDensity, k);
        RenderSettings.reflectionIntensity = Mathf.Lerp(reflection, 0.05f, k);
    }

    void OnDestroy()
    {
        if (noHazeProfile == null) return;
        foreach (var c in noHazeProfile.components) if (c != null) Destroy(c);
        Destroy(noHazeProfile);
    }
}
