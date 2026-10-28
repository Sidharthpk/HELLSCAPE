using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// The light in your hands. In the blacked-out office (InteriorDarkness.blackoutUntilPowered, before the fuses are in)
// Q plays the arms' Take clips (Take_Start, then the Take_Loop held) with the right hand out, the left hidden: the mark on
// the palm gives off a beam where you look. Q again plays Take_Stop (the hand coming down) and it settles back into the
// idle. The fuses going in lowers it too. Sits next to PunchController (InteriorDarkness adds it); the fists are off while
// the hand is up.
//
// The light itself is free: hold it as long as you like. Only the flash heats your hands (the skin reddens a little,
// then cools). With the hands up, left click (your punch, when they're down) spends it all at once: a strobing flash that freezes
// every Damned who can see you (and sends the Auditor away). One flash, then a long recharge (the blood bar at the
// bottom of the screen). Anywhere in the office, lit or not: the hands come up for the flash as well as the light.
public class PalmLight : MonoBehaviour
{
    public KeyCode key = KeyCode.Q;
    public Color color = new Color(1f, 0.9f, 0.72f);
    public float range = 60f, angle = 105f, intensity = 45f;   // a soft wide glow, strong enough to see the whole room by
    public float beamAhead = 0.5f;          // the beam starts this far in front of the palm, so it never lights your own hand
    [TextArea] public string hint = "It's pitch black.|[Q] raises my hands. The mark lights the way.|[LEFT CLICK] with my hands up: a flash that stuns them. It recharges slowly.";

    [Header("Overuse")]
    public float coolTime = 45f;            // seconds to cool right down again
    public float usableBelow = 0.55f;       // burnt-out hands come back once they are this cool
    public Color burnt = new Color(1f, 0.22f, 0.16f);
    [TextArea] public string warnLine = "My hands are burning. I can't hold this much longer.";
    [TextArea] public string burnLine = "Too hot. I have to let them cool.";

    [Header("Flash")]
    public float flashRange = 24f;
    public float stunTime = 5f;
    public float recharge = 30f;
    public float flashHeat = 0.3f;          // what one flash costs your hands
    [TextArea] public string notReadyLine = "Nothing. It hasn't come back yet.";

    // Fists 2025's Take clips as their own states on FistsController: the right hand reaches out and holds, looping; the left is hidden
    const string Start0 = "Light", Loop = "LightLoop", Stop = "LightStop";

    enum Phase { Down, Raising, Held, Lowering }
    Phase phase;

    PunchController punch;
    Animator arms;
    Transform cam, palmR, palmL, leftArm;
    Vector3 leftScale;
    Light beam, glowR, glowL, burst;
    bool hinted, warned, burntOut, flashing, sawStop;
    float phaseAt, heat, charge = 1f;
    Material[] skin; Color[] skin0;
    Image overlay, chargeBar;
    AudioSource sfx;

    // the Damned a flash is holding, and until when
    static readonly Dictionary<ZombieAI, float> stunned = new Dictionary<ZombieAI, float>();
    public static float FlashedAt = -99f;   // when the last flash went off (Vane's choke breaks on it)
    public static bool FlashAnytime;        // Vane has you by the throat: the flash needs no raised hands
    public bool Held => phase == Phase.Held;
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
            if (t.name == "DEF-palm.03.R") palmR = t;
            else if (t.name == "DEF-palm.03.L") palmL = t;
            else if (t.name == "ORG-shoulder.L") leftArm = t;
        }
        if (leftArm != null) leftScale = leftArm.localScale;
        beam = MakeLight("Palm beam", LightType.Spot);
        beam.range = range; beam.spotAngle = angle; beam.innerSpotAngle = angle * 0.15f; beam.intensity = intensity;   // (a soft edge: it glows off into the dark)
        beam.shadows = LightShadows.Soft;
        // the palm glows are faint and sit out in front of the hands: close to the camera a bright light on the skin blinds
        glowR = MakeLight("Palm glow R", LightType.Point); glowR.range = 10f; glowR.intensity = 0.2f;
        glowL = MakeLight("Palm glow L", LightType.Point); glowL.range = 16f; glowL.intensity = .5f;
        burst = MakeLight("Palm flash", LightType.Point); burst.range = flashRange * 1.6f; burst.intensity = 0f; burst.color = Color.white; burst.enabled = false;
        Show(false);

        // the arms' own materials (instances: RevisedPlotDirector paints the mark on the same ones)
        var mats = new List<Material>();
        foreach (var r in arms.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            foreach (var m in r.materials) if (m.HasProperty("_BaseColor")) { m.DisableKeyword("_EMISSION"); mats.Add(m); }   // (the hands never glow: only the light does)
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

    void Show(bool v) { beam.enabled = v; glowR.enabled = glowL.enabled = false; }   // (just the beam: point lights by the palm lit the skin like a glow)

    public void LowerForAction()
    {
        Show(false);phase=Phase.Down;
        if(leftArm!=null) leftArm.localScale=leftScale;
        if(arms!=null) arms.speed=1f;
    }
    public void PrepareOpeningFlash()
    {
        charge=1f;heat=0f;burntOut=false;
    }

    void Update()
    {
        bool live = Time.timeScale > 0f && !PauseMenu.Paused && !GunController.Equipped;
        bool allowed = (InteriorDarkness.Blackout || (cam != null && OfficeHorrorDirector.In(cam.position))) && live && !burntOut;
        if (OfficeV52Sequence.Instance != null && OfficeV52Sequence.Instance.Opening != null)
        {
            hinted=true; // OfficeOpening presents controls as HUD instructions, never spoken dialogue.
            if(OfficeV52Sequence.Instance.Opening.Busy && !OfficeV52Sequence.Instance.Opening.AwaitingFlash) live=allowed=false;
        }
        else if (InteriorDarkness.Blackout && live && !hinted && DialogueBox.Instance != null) { hinted = true; DialogueBox.Instance.Say(hint); }
        if (allowed && Input.GetKeyDown(key)) Set(phase == Phase.Down);
        else if (!allowed && (phase == Phase.Raising || phase == Phase.Held)) Set(false);

        Overuse();
        Flash(live);

        var st = arms.GetCurrentAnimatorStateInfo(0);
        switch (phase)
        {
            case Phase.Raising:
                // (timeout: if the clip never reports in, e.g. another state took over, still raise the light)
                if (st.IsName(Loop) || Time.time - phaseAt > 1.2f)
                {
                    arms.speed = 1f;   // the hand holds in the Take loop
                    phase = Phase.Held;
                    Show(true);
                }
                break;
            case Phase.Held:
                // steady while it's cool; guttering as it nears the end
                float gutter = heat > 0.75f ? 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 14f, 0f) : 1f;
                // a slow breath in and out (~3 s): the glow is in the light, never on the skin
                float breathe = 0.65f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.time * 2.1f));
                beam.intensity = intensity * gutter * breathe;
                break;
            case Phase.Lowering:
                // (the state's own exit takes it back into the idle)
                sawStop |= st.IsName(Stop);
                if ((sawStop && (!st.IsName(Stop) || st.normalizedTime > .55f)) || Time.time - phaseAt > 1.5f)
                {
                    arms.speed = 1f;
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
        for (int i = 0; i < skin.Length; i++)
        {
            if (skin[i] == null) continue;
            skin[i].SetColor("_BaseColor", Color.Lerp(skin0[i], skin0[i] * burnt, k));
        }
        Color c = Color.Lerp(color, burnt, k * 0.65f);
        beam.color = glowL.color = c;
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

        if (!live || (phase != Phase.Held && !FlashAnytime) || flashing || !Input.GetMouseButtonDown(0)) return;   // only with the palms up: the click that would have been a punch
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
            burst.intensity = on ? 260f : 0f;
            overlay.color = on ? new Color(1f, 1f, 1f, 0.92f) : new Color(0f, 0f, 0f, 0.82f);   // the impact frames
            yield return null;
        }
        for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / fade;
            burst.transform.position = cam.position + cam.forward * 0.4f;
            burst.intensity = 120f * k * k;
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
        FlashedAt = Time.time;
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
        if (VaneHunt.Instance != null) VaneHunt.Instance.OnFlash(eye, cam.forward, flashRange);
        var horror = FindFirstObjectByType<OfficeHorrorDirector>();
        if (horror != null && horror.Auditor != null && horror.Auditor.Present && Vector3.Distance(horror.Auditor.Position, eye) < flashRange) horror.Auditor.Vanish();
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
            arms.speed = 1.4f;                     // (the light is wanted now: the hand comes up quickly)
            arms.CrossFadeInFixedTime(Start0, 0.1f, 0, 0f);
            phase = Phase.Raising;
            phaseAt = Time.time;
        }
        else
        {
            if (phase != Phase.Raising && phase != Phase.Held) return;
            Show(false);
            arms.speed = 1.4f;
            arms.CrossFadeInFixedTime(Stop, 0.1f, 0, 0f);   // the hand comes back down
            phase = Phase.Lowering; sawStop = false;
            phaseAt = Time.time;
        }
    }

    void LateUpdate()
    {
        // (added on top of wherever the arms are, and taken off again: nothing else that moves them is disturbed)
        Vector3 shake = Surge > 0f ? Random.insideUnitSphere * (.012f * Mathf.Min(1f, Surge)) : Vector3.zero;
        arms.transform.localPosition += shake - tremble; tremble = shake;
        if (leftArm != null) leftArm.localScale = phase == Phase.Down ? leftScale : Vector3.zero;   // only the right hand carries the light
        if (phase != Phase.Held) return;
        // the beam leaves the right palm and goes where you look; the palm glows
        Vector3 from = palmR != null ? palmR.position : cam.position + cam.forward * 0.5f;
        beam.transform.SetPositionAndRotation(from + cam.forward * beamAhead, Quaternion.LookRotation(cam.forward));
        glowR.transform.position = from + cam.forward * .35f;
    }

    void OnDestroy()
    {
        foreach (var l in new[] { beam, glowR, glowL, burst }) if (l != null) Destroy(l.gameObject);
        if (skin != null) foreach (var m in skin) if (m != null) Destroy(m);
    }
}
