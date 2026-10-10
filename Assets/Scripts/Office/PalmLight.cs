using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The light in your hands. In the blacked-out office (InteriorDarkness.blackoutUntilPowered, before the fuses are in)
// Q plays the arms' push (ArmsRig_push.R) and holds it at frame 13, palms raised: the mark on them gives off a beam
// where you look, a flashlight. Q again plays the rest of the clip (hands coming down), then the fists come back up
// into the guard. The fuses going in lowers them too. Sits next to PunchController (InteriorDarkness adds it); the
// fists are off while the hands are up.
//
// The light itself is free: hold it as long as you like. Only the flash heats your hands (the skin reddens a little,
// then cools). With the hands up, left click (your punch, when they're down) spends it all at once: a strobing flash that freezes
// every Damned who can see you (and sends the Auditor away). One flash, then a long recharge (the blood bar at the
// bottom of the screen). Anywhere in the office, lit or not: the hands come up for the flash as well as the light.
public class PalmLight : MonoBehaviour
{
    public KeyCode key = KeyCode.Q;
    public Color color = new Color(1f, 0.8f, 0.58f);
    public float range = 24f, angle = 65f, intensity = 8f;
    [TextArea] public string hint = "It's pitch black.|[Q] raises my hands. The mark lights the way.|[LEFT CLICK] with my hands up: a flash that stuns them. It recharges slowly.";

    [Header("Overuse")]
    public float coolTime = 45f;            // seconds to cool right down again
    public float usableBelow = 0.55f;       // burnt-out hands come back once they are this cool
    public Color burnt = new Color(1f, 0.22f, 0.16f);
    [TextArea] public string warnLine = "My hands are burning. I can't hold this much longer.";
    [TextArea] public string burnLine = "Too hot. I have to let them cool.";

    [Header("Flash")]
    public float flashRange = 15f;
    public float stunTime = 5f;
    public float recharge = 30f;
    public float flashHeat = 0.3f;          // what one flash costs your hands
    [TextArea] public string notReadyLine = "Nothing. It hasn't come back yet.";

    const string Push = "ArmsRig_push_R";     // the user's clip, as a state on ArmsController (30 fps, 23 frames)
    const float ClipFrames = 23f, HoldFrame = 13f;
    const float holdAt = HoldFrame / ClipFrames;   // normalized time of the pause: palms raised

    enum Phase { Down, Raising, Held, Lowering }
    Phase phase;

    PunchController punch;
    Animator arms;
    Transform cam, palmR, palmL;
    Light beam, glowR, glowL, burst;
    bool hinted, warned, burntOut, flashing;
    float phaseAt, heat, charge = 1f;
    Material[] skin; Color[] skin0;
    Image overlay, chargeBar;
    AudioSource sfx;

    // the Damned a flash is holding, and until when
    static readonly Dictionary<ZombieAI, float> stunned = new Dictionary<ZombieAI, float>();
    public static bool Stunned(ZombieAI ai) => ai != null && stunned.ContainsKey(ai);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => stunned.Clear();

    public float Heat => heat;
    public float Surge;   // the marks flaring on their own (the Eye's hold being fought): glow and a tremble, dying away
    Vector3 tremble;
    public float Charge => charge;

    void Start()
    {
        punch = GetComponent<PunchController>();
        if (punch == null) { enabled = false; return; }
        arms = punch.armsAnimator; cam = punch.cam;
        foreach (var t in arms.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "palm.03.R") palmR = t;
            else if (t.name == "palm.03.L") palmL = t;
        }
        beam = MakeLight("Palm beam", LightType.Spot);
        beam.range = range; beam.spotAngle = angle; beam.innerSpotAngle = angle * 0.45f; beam.intensity = intensity;
        beam.shadows = LightShadows.Hard;
        glowR = MakeLight("Palm glow R", LightType.Point); glowR.range = 3f; glowR.intensity = 1.8f;
        glowL = MakeLight("Palm glow L", LightType.Point); glowL.range = 2.5f; glowL.intensity = 1.2f;
        burst = MakeLight("Palm flash", LightType.Point); burst.range = flashRange * 1.6f; burst.intensity = 0f; burst.color = Color.white; burst.enabled = false;
        Show(false);

        // the arms' own materials (instances: RevisedPlotDirector paints the mark on the same ones)
        var mats = new List<Material>();
        foreach (var r in arms.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            foreach (var m in r.materials) if (m.HasProperty("_BaseColor")) { m.EnableKeyword("_EMISSION"); mats.Add(m); }
        skin = mats.ToArray(); skin0 = new Color[skin.Length];
        for (int i = 0; i < skin.Length; i++) skin0[i] = skin[i].GetColor("_BaseColor");

        var canvas = new GameObject("Palm light HUD", typeof(Canvas)).GetComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 390;
        var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        overlay = new GameObject("Flash", typeof(RectTransform)).AddComponent<Image>();
        overlay.transform.SetParent(canvas.transform, false);
        overlay.rectTransform.anchorMin = Vector2.zero; overlay.rectTransform.anchorMax = Vector2.one; overlay.rectTransform.offsetMin = overlay.rectTransform.offsetMax = Vector2.zero;
        overlay.color = Color.clear; overlay.raycastTarget = false;
        chargeBar = BloodBar.Make(canvas.transform, "Flash recharge", new Vector2(240f, 28f));
        var track = (RectTransform)chargeBar.transform.parent;
        track.anchorMin = track.anchorMax = new Vector2(.5f, 0f); track.pivot = new Vector2(0f, .5f); track.anchoredPosition = new Vector2(-120f, 54f);
        track.gameObject.SetActive(false);

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false; sfx.spatialBlend = 0f;
    }

    Light MakeLight(string name, LightType type)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.type = type; l.color = color;
        return l;
    }

    void Show(bool v) { beam.enabled = glowR.enabled = glowL.enabled = v; }

    void Update()
    {
        bool live = Time.timeScale > 0f && !PauseMenu.Paused && !GunController.Equipped;
        bool allowed = (InteriorDarkness.Blackout || (cam != null && OfficeHorrorDirector.In(cam.position))) && live && !burntOut;
        if (InteriorDarkness.Blackout && live && !hinted && DialogueBox.Instance != null) { hinted = true; DialogueBox.Instance.Say(hint); }
        if (allowed && Input.GetKeyDown(key)) Set(phase == Phase.Down);
        else if (!allowed && (phase == Phase.Raising || phase == Phase.Held)) Set(false);

        Overuse();
        Flash(live);

        var st = arms.GetCurrentAnimatorStateInfo(0);
        bool inPush = st.IsName(Push) && !arms.IsInTransition(0);
        switch (phase)
        {
            case Phase.Raising:
                // (timeout: if the clip never reports in, e.g. another state took over, still raise the light)
                if ((inPush && st.normalizedTime >= holdAt) || Time.time - phaseAt > 0.6f)
                {
                    arms.Play(Push, 0, holdAt);   // frame 13, exactly
                    arms.speed = 0f;              // ...and paused there
                    phase = Phase.Held;
                    Show(true);
                }
                break;
            case Phase.Held:
                // steady while it's cool; guttering as it nears the end
                float gutter = heat > 0.75f ? 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 14f, 0f) : 1f;
                beam.intensity = intensity * gutter * (0.94f + 0.06f * Mathf.Sin(Time.time * 17f) * Mathf.Sin(Time.time * 5.3f));
                break;
            case Phase.Lowering:
                if ((inPush && st.normalizedTime >= 0.98f) || Time.time - phaseAt > 0.8f)
                {
                    arms.speed = 1f;
                    arms.Play("GuardDraw", 0, 0f);   // hands down, then the fists back up into the guard
                    punch.enabled = true;
                    phase = Phase.Down;
                }
                break;
        }
    }

    // ---------------------------------------------------------------- overuse: the red creeping up your arms

    void Overuse()
    {
        heat = Mathf.Clamp01(heat - Time.deltaTime / coolTime);   // holding the light costs nothing now: only a flash heats the hands
        Surge = Mathf.MoveTowards(Surge, 0f, Time.deltaTime * 2.5f);
        if (heat > 0.7f && !warned && phase == Phase.Held) { warned = true; Say(warnLine); }
        if (heat >= 1f && !burntOut) { burntOut = true; Say(burnLine); Set(false); }
        if (burntOut && heat < usableBelow) { burntOut = false; warned = false; }

        // skin: nothing for the first moments, then the red comes in and finally glows
        float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 1f, heat));
        float throb = heat > 0.75f ? 0.75f + 0.25f * Mathf.Sin(Time.time * 7f) : 1f;
        for (int i = 0; i < skin.Length; i++)
        {
            if (skin[i] == null) continue;
            skin[i].SetColor("_BaseColor", Color.Lerp(skin0[i], skin0[i] * burnt, k));
            skin[i].SetColor("_EmissionColor", burnt * (k * k * 0.9f * throb + Surge));
        }
        Color c = Color.Lerp(color, burnt, k * 0.65f);
        beam.color = glowR.color = glowL.color = c;
    }

    // ---------------------------------------------------------------- the flash

    void Flash(bool live)
    {
        // whoever the last flash caught comes round
        if (stunned.Count > 0)
        {
            List<ZombieAI> free = null;
            foreach (var kv in stunned) if (kv.Key == null || Time.time >= kv.Value) (free ??= new List<ZombieAI>()).Add(kv.Key);
            if (free != null) foreach (var ai in free) Release(ai);
        }

        if (charge < 1f) charge = Mathf.Min(1f, charge + Time.deltaTime / recharge);
        bool here = cam != null && OfficeHorrorDirector.In(cam.position);
        var track = chargeBar.transform.parent.gameObject;
        bool show = charge < 1f && here && !TitleScreen.Showing && !GameOverScreen.Open;
        if (track.activeSelf != show) track.SetActive(show);
        chargeBar.fillAmount = charge;
        chargeBar.color = BloodBar.Red * Mathf.Lerp(.55f, 1f, charge);

        if (!live || phase != Phase.Held || flashing || !Input.GetMouseButtonDown(0)) return;   // only with the palms up: the click that would have been a punch
        if (RevisedPlotDirector.Instance != null && RevisedPlotDirector.Instance.Echo) return;   // the memory has nothing to burn
        if (GhostNPC.Conversing || NoteViewer.Blocking || (ChoiceDialogue.Instance != null && ChoiceDialogue.Instance.Open)) return;
        if (charge < 1f || burntOut) { Say(notReadyLine); return; }
        StartCoroutine(Burst());
    }

    IEnumerator Burst()
    {
        flashing = true;
        charge = 0f;
        heat = Mathf.Min(1f, heat + flashHeat);
        Stun();
        sfx.pitch = 1.6f; sfx.PlayOneShot(ProceduralAudio.Thud(), 1f);
        sfx.PlayOneShot(ProceduralAudio.Click(), 1f);

        // strobe: hard white frames against black ones, a few of each, then the white dies away
        burst.enabled = true;
        const float strobe = 0.5f, fade = 0.45f;
        for (float t = 0f; t < strobe; t += Time.unscaledDeltaTime)
        {
            bool on = (int)(t * 22f) % 2 == 0;
            burst.transform.position = cam.position + cam.forward * 0.4f;
            burst.intensity = on ? 90f : 0f;
            overlay.color = on ? new Color(1f, 1f, 1f, 0.92f) : new Color(0f, 0f, 0f, 0.82f);   // the impact frames
            yield return null;
        }
        for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / fade;
            burst.transform.position = cam.position + cam.forward * 0.4f;
            burst.intensity = 40f * k * k;
            overlay.color = new Color(1f, 0.93f, 0.85f, 0.6f * k * k);
            yield return null;
        }
        burst.intensity = 0f; burst.enabled = false;
        overlay.color = Color.clear;
        flashing = false;
    }

    // every Damned in range with a clear line to you stops dead where it stands
    void Stun()
    {
        Vector3 eye = cam.position;
        foreach (var ai in FindObjectsByType<ZombieAI>(FindObjectsSortMode.None))
        {
            if (!ai.TryGetComponent(out Zombie z) || z.IsDead) continue;
            Vector3 to = ai.transform.position + Vector3.up * 1.1f - eye;
            if (to.magnitude > flashRange || !Clear(eye, to, ai.transform)) continue;
            stunned[ai] = Time.time + stunTime;
            ai.enabled = false;
            if (ai.agent != null && ai.agent.isOnNavMesh) { ai.agent.isStopped = true; ai.agent.velocity = Vector3.zero; }
            if (ai.animator != null) ai.animator.speed = 0f;   // caught mid-step
        }
        var horror = FindFirstObjectByType<OfficeHorrorDirector>();
        if (horror != null && horror.Auditor != null && horror.Auditor.Present && Vector3.Distance(horror.Auditor.Position, eye) < flashRange) horror.Auditor.Vanish();
        if (horror != null && horror.Eye != null) horror.Eye.Flashed(eye, flashRange, stunTime);   // the Eye is blinded, not stopped for good
    }

    bool Clear(Vector3 eye, Vector3 to, Transform target)
    {
        foreach (var h in Physics.RaycastAll(eye, to.normalized, to.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (!h.transform.IsChildOf(transform.root) && !h.transform.IsChildOf(target)) return false;
        return true;
    }

    void Release(ZombieAI ai)
    {
        stunned.Remove(ai);
        if (ai == null || (ai.TryGetComponent(out Zombie z) && z.IsDead)) return;
        if (ai.animator != null) ai.animator.speed = 1f;
        ai.enabled = true;
        if (ai.agent != null && ai.agent.isOnNavMesh) ai.agent.isStopped = false;
    }

    static void Say(string line) { if (!string.IsNullOrEmpty(line) && DialogueBox.Instance != null) DialogueBox.Instance.Say(line); }

    // true: raise the hands; false: let the clip play out and bring them down
    void Set(bool up)
    {
        if (up)
        {
            if (phase != Phase.Down) return;
            punch.enabled = false;                 // no punching with your hands up
            arms.speed = 1f;
            arms.CrossFadeInFixedTime(Push, 0.1f, 0, 0f);
            phase = Phase.Raising;
            phaseAt = Time.time;
        }
        else
        {
            if (phase != Phase.Raising && phase != Phase.Held) return;
            Show(false);
            arms.speed = 1f;                       // (resumes from the pause)
            phase = Phase.Lowering;
            phaseAt = Time.time;
        }
    }

    void LateUpdate()
    {
        // (added on top of wherever the arms are, and taken off again: nothing else that moves them is disturbed)
        Vector3 shake = Surge > 0f ? Random.insideUnitSphere * (.012f * Mathf.Min(1f, Surge)) : Vector3.zero;
        arms.transform.localPosition += shake - tremble; tremble = shake;
        if (phase != Phase.Held) return;
        // the beam leaves the palm and goes where you look; the palms glow
        Vector3 from = (palmR != null ? palmR.position : cam.position + cam.forward * 0.5f) + cam.forward * 0.12f;
        beam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(cam.forward));
        if (palmR != null) glowR.transform.position = palmR.position;
        if (palmL != null) glowL.transform.position = palmL.position;
    }

    void OnDestroy()
    {
        foreach (var l in new[] { beam, glowR, glowL, burst }) if (l != null) Destroy(l.gameObject);
        if (skin != null) foreach (var m in skin) if (m != null) Destroy(m);
    }
}
