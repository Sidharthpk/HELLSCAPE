using System.Collections;
using UnityEngine;

// Waking up in the hell city after the fall off the roof: the world turns red, you come to flat on your back in
// the street, and get up. (It used to be a bed you slept in; the prologue replaced that.) Also knows the day and
// hell looks, which the title screen and the ending borrow.
public class SleepSequence : MonoBehaviour
{
    [Header("UI")]
    public ScreenFader fader;

    [Header("Player")]
    public GameObject playerRoot;
    public MonoBehaviour playerMovementScript;
    public MonoBehaviour punchController;
    public Transform wakePoint;          // fallback if there's no fall wake point

    [Header("Worlds")]
    public GameObject dayWorld;          // civilians, bright props, day Global Volume
    public GameObject hellWorld;         // blood, bodies, deer zombie, red Global Volume
    public GateButton garageDoor;        // optional: open during the day, shut when you wake
    public GameObject[] hellOnlyUI;      // HUD, crosshair... hidden during the day, shown when you wake

    [Header("Day Look")]
    public Material daySkybox;
    public Color dayFogColor = new Color(0.75f, 0.82f, 0.9f);
    public float dayFogDensity = 0.005f;

    [Header("Hell Look")]
    public Material hellSkybox;
    public Color hellFogColor = new Color(0.35f, 0.075f, 0.068f);
    public float hellFogDensity = 0.03f;

    [Header("Sun")]
    public Light sun;
    public Color daySunColor = new Color(1f, 0.95f, 0.85f);
    public float daySunIntensity = 1.2f;
    public Color hellSunColor = new Color(0.6f, 0.1f, 0.08f);
    public float hellSunIntensity = 0.4f;

    [Header("Audio / Timing")]
    public AudioSource wakeSound;        // heartbeat as you come to

    public UnityEngine.Events.UnityEvent onWake = new UnityEngine.Events.UnityEvent();   // e.g. the first checkpoint

    [Header("Arriving from the prologue (the fall off the roof)")]
    public Transform fallWakePoint;      // where you come to on the street; wakePoint if empty
    public Transform streetAnchor;       // a street piece the prologue scene has too: you wake where you hit the ground
    [TextArea] public string fallWakeLine = "...I should be dead.|Where... is this?|Why is the sky RED?";

    public static bool WakeInHell;       // set by the prologue before it loads this scene
    public static Vector3? FallSpot;     // where you hit the ground, in the street anchor's space
    public static Vector3 FallFacing;    // which way to face when you get up (same space)

    void Start()
    {
        // first load: the title screen is up, and START plays the prologue (which comes back here with WakeInHell)
        if (!WakeInHell && TitleScreen.Showing) return;
        // from the prologue, or a reload (a game over before the first checkpoint): back on the red street
        WakeInHell = false;
        StartCoroutine(WakeFromFall());
    }

    // straight into hell: flat on your back in the street, looking up at the red sky, then up on your feet
    IEnumerator WakeFromFall()
    {
        playerMovementScript.enabled = false;
        if (punchController != null) punchController.enabled = false;
        Sounds.Apply(wakeSound, "Hell City/Wake heartbeat");
        var fists = punchController as PunchController;
        if (fists != null) fists.HoldDown();   // flat on your back: no guard yet
        if (fader != null) fader.fade.alpha = 1f;
        ApplyLook(true);
        Transform at = fallWakePoint != null ? fallWakePoint : wakePoint;
        if (at != null) playerRoot.transform.SetPositionAndRotation(at.position, at.rotation);
        if (FallSpot.HasValue && streetAnchor != null) PlaceAtFallSpot();
        FallSpot = null;
        yield return null;
        if (fader != null) fader.fade.alpha = 1f;   // the fader's own Start clears it

        var cam = playerRoot.GetComponentInChildren<Camera>();
        if (wakeSound != null) wakeSound.Play();
        yield return new WaitForSeconds(1.5f);
        if (cam != null) cam.transform.localRotation = Quaternion.Euler(-80f, 0f, 12f);
        if (fader != null) yield return fader.FadeTo(0f, 3f);
        yield return new WaitForSeconds(0.8f);
        if (cam != null)
        {
            Quaternion from = cam.transform.localRotation;
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                cam.transform.localRotation = Quaternion.Slerp(from, Quaternion.identity, Mathf.SmoothStep(0f, 1f, t / 2.2f));
                yield return null;
            }
            cam.transform.localRotation = Quaternion.identity;
            if (playerMovementScript is FirstPersonController fpc) fpc.SetPitch(0f);
        }
        if (fists != null) fists.Draw();       // up on your feet: fists up into the guard
        MusicManager.Area("Hell City");
        StartCoroutine(SayAfter(fallWakeLine, 0.3f));
        playerMovementScript.enabled = true;
        if (punchController != null) punchController.enabled = true;
        onWake.Invoke();
    }

    // the same spot on the same street, just... red
    void PlaceAtFallSpot()
    {
        Vector3 p = streetAnchor.TransformPoint(FallSpot.Value);
        Vector3 f = streetAnchor.TransformDirection(FallFacing);
        f.y = 0f;
        float ground = float.MinValue;
        foreach (var h in Physics.RaycastAll(p + Vector3.up * 30f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(playerRoot.transform)) continue;
            if (h.point.y < p.y - 5f || h.point.y > p.y + 2f) continue;   // not roofs, awnings
            ground = Mathf.Max(ground, h.point.y);
        }
        if (ground > float.MinValue) p.y = ground;
        // the hell city has its own walls (the garage gate is where the flats were): step out into the street
        for (int i = 0; i < 12 && f.sqrMagnitude > 0.01f && Blocked(p); i++) p += f.normalized * 0.5f;   // (facing the city: forward is away from the wall)
        playerRoot.transform.SetPositionAndRotation(p + Vector3.up,   // the root sits 1 m up, like FallWakePoint
            f.sqrMagnitude > 0.01f ? Quaternion.LookRotation(f) : playerRoot.transform.rotation);
    }

    bool Blocked(Vector3 feet)
    {
        foreach (var c in Physics.OverlapCapsule(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.6f, 0.35f, ~0, QueryTriggerInteraction.Ignore))
            if (!c.transform.IsChildOf(playerRoot.transform)) return true;
        return false;
    }

    IEnumerator SayAfter(string line, float delay)
    {
        while (TitleScreen.Showing) yield return null;   // the start screen is up: the day hasn't begun yet
        yield return new WaitForSeconds(delay);
        if (!string.IsNullOrEmpty(line) && DialogueBox.Instance != null) DialogueBox.Instance.Say(line);
    }

    void ApplyLook(bool hell)
    {
        if (dayWorld != null) dayWorld.SetActive(!hell);
        if (hellWorld != null) hellWorld.SetActive(hell);
        if (garageDoor != null) garageDoor.SetOpen(!hell);
        foreach (var ui in hellOnlyUI)
            if (ui != null) ui.SetActive(hell);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SetHellStyle(hell);

        Material sky = hell ? hellSkybox : daySkybox;
        if (sky != null) RenderSettings.skybox = sky;
        RenderSettings.fogColor = hell ? hellFogColor : dayFogColor;
        RenderSettings.fogDensity = hell ? hellFogDensity : dayFogDensity;

        if (sun != null)
        {
            sun.color = hell ? hellSunColor : daySunColor;
            sun.intensity = hell ? hellSunIntensity : daySunIntensity;
        }
        DynamicGI.UpdateEnvironment();
    }
}
