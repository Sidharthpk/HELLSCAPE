using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// The Eye has you (the other half of OfficeEye: what Catch() starts). It does not just hurt you any more: it hangs
// in front of your face and holds you there. You can't walk, your look is dragged to it (the mouse still pulls a
// little), the walls round you fill with eyes, your sight swims and the whispers start: the statement you signed.
// After a moment BREAK ITS GAZE: hammer [F]. Fill the bar and the marks on your hands go off, it is thrown back and
// gone for a while, and a few eyes stay on the walls until you look at them. Run out of time and it strikes (it
// hurts, it never kills) and lets you go. The palm flash frees you too. Each capture is a little worse than the last.
// Everything it changes on the player is put back by Release(), whatever ends it (death, a cutscene, the Eye or
// this object going, the scene going): you are never left held.
public partial class OfficeEye
{
    [Header("Capture")]
    public float stunTime = 1.5f;           // held in its gaze before you can fight it
    public int pressesNeeded = 8;
    public float struggleTime = 8f;
    public float captureCooldown = 25f;     // it is gone this long afterwards, however it ended
    public float lingerTime = 12f;          // the last few eyes on the walls, once you have broken free
    public float failDamage = 14f;

    [Header("Capture: your sight")]
    [Range(0f, 1f)] public float peripheralWarp = .4f;   // the edges of the picture bending (the middle, and the Eye, stay readable)
    public float wobbleFrequency = .55f;                 // breaths a second
    [Range(0f, 1f)] public float chromatic = .5f;
    [Range(0f, 1f)] public float pulseStrength = .22f;   // the beat of your heart, in the picture
    public float maxIntensity = 1.6f;                    // however many times it has had you
    public float blendTime = .8f;

    // no swimming picture, a slower drag of the look; the struggle itself is the same. ponytail: no menu switch yet, set PlayerPrefs "ReducedMotion" = 1 (add a toggle to the pause menu when there is a settings page)
    public static bool ReducedMotion => PlayerPrefs.GetInt("ReducedMotion", 0) == 1;

    // the statement you signed (the user's lines; no voice recordings yet: a procedural whisper under a subtitle)
    static readonly string[] Whispers =
    {
        "You saw the form.", "You put your name on it.", "You never asked what happened.", "Everybody signed.",
        "Look at what you signed.", "You didn't see him die. You signed anyway.",   // (these two from the second capture on)
    };
    const string LastWhisper = "You can close your eyes. Your name is still there.";

    public int Captures { get; private set; }
    public bool Capturing => state == S.Capture;
    public float Progress { get; private set; }

    FirstPersonController fpc; PlayerInteractor interactor; PalmLight palm;
    bool held, jump0, interact0, teased; float sens0;
    float capT, failAt, fx, dip, pulse, flash, beatIn, whisperIn, whisperShow, eyeIn, linger, panTo;
    int lastLine = -1;
    Volume look; VolumeProfile profile; LensDistortion lens; ChromaticAberration fringe; Vignette edge;
    GameObject panel; Image bar, overlay; TextMeshProUGUI whisperText; Color flashColour;
    AudioSource voice, beat; AudioClip whisperClip, beatClip, thudClip;
    Light burstLight;

    void BuildCapture()
    {
        fpc = player.GetComponent<FirstPersonController>();
        interactor = player.GetComponentInChildren<PlayerInteractor>(true);
        palm = player.GetComponentInChildren<PalmLight>(true);

        // your sight: URP's own lens distortion, colour fringe and vignette on a volume of its own (no new shader)
        look = new GameObject("Eye capture look").AddComponent<Volume>();
        look.transform.SetParent(transform, false);
        look.isGlobal = true; look.priority = 60f; look.weight = 0f;
        look.profile = profile = ScriptableObject.CreateInstance<VolumeProfile>();
        lens = profile.Add<LensDistortion>(true); fringe = profile.Add<ChromaticAberration>(true); edge = profile.Add<Vignette>(true);
        edge.color.value = new Color(.35f, 0f, 0f); edge.smoothness.value = .6f;

        var canvas = new GameObject("Eye capture", typeof(Canvas), typeof(CanvasScaler));
        canvas.transform.SetParent(transform, false);
        var c = canvas.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 180;   // over the HUD and the map, under the objective and the story canvas
        var s = canvas.GetComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1920f, 1080f); s.matchWidthOrHeight = 1f;
        overlay = new GameObject("Flash", typeof(RectTransform)).AddComponent<Image>();
        overlay.transform.SetParent(canvas.transform, false);
        overlay.rectTransform.anchorMin = Vector2.zero; overlay.rectTransform.anchorMax = Vector2.one; overlay.rectTransform.offsetMin = overlay.rectTransform.offsetMax = Vector2.zero;
        overlay.color = Color.clear; overlay.raycastTarget = false;
        whisperText = Text("Whisper", canvas.transform, 32f, new Color(.96f, .8f, .76f), Vector2.zero);
        whisperText.outlineWidth = .25f; whisperText.outlineColor = Color.black;   // (to read over the red)
        whisperText.alpha = 0f;
        panel = new GameObject("Struggle", typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        Text("Title", panel.transform, 54f, new Color(.95f, .12f, .08f), new Vector2(0f, -130f)).text = "BREAK ITS GAZE";
        Text("Keys", panel.transform, 28f, new Color(.9f, .86f, .74f), new Vector2(0f, -190f)).text = "PRESS [F] REPEATEDLY";
        bar = BloodBar.Make(panel.transform, "Resistance", new Vector2(420f, 30f));
        ((RectTransform)bar.transform.parent).anchoredPosition = new Vector2(-210f, -245f);   // (the bar's pivot is its left end; all of it clear of the dialogue box)
        panel.SetActive(false);

        voice = Source("Eye whispers"); beat = Source("Eye heartbeat");
        whisperClip = ProceduralAudio.Whisper(2.4f);
        thudClip = ProceduralAudio.Thud();
        beatClip = Sounds.Clip("Prologue/Heartbeat", null) ?? thudClip;
        burstLight = MakeLight("Hand-mark burst", LightType.Point);
        burstLight.transform.SetParent(transform, false); burstLight.range = 18f; burstLight.color = new Color(1f, .75f, .7f);
    }

    AudioSource Source(string name)
    {
        var a = new GameObject(name).AddComponent<AudioSource>();
        a.transform.SetParent(transform, false); a.playOnAwake = false; a.spatialBlend = 0f;
        return a;
    }

    TextMeshProUGUI Text(string name, Transform parent, float size, Color colour, Vector2 at)
    {
        var t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        var font = GameFont.Main;
        if (font != null) t.font = font;
        t.fontSize = size; t.color = colour; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        t.rectTransform.sizeDelta = new Vector2(1100f, 80f); t.rectTransform.anchoredPosition = at;
        return t;
    }

    // ---------------------------------------------------------------- caught, and let go

    // testing: it has you, now, wherever it was (and one press of [F] from code)
    [ContextMenu("Capture Now")]
    public void CaptureNow()
    {
        if (state == S.Capture || !shown()) return;
        root.gameObject.SetActive(true);
        if (NavMesh.SamplePosition(player.position + player.forward * 1.5f, out var h, 3f, NavMesh.AllAreas)) agent.Warp(h.position);
        BeginCapture();
    }
    public void Press() => pressNow = true;
    bool pressNow;

    void BeginCapture()
    {
        state = S.Capture; Captures++;
        capT = failAt = 0f; Progress = 0f; whisperIn = .8f; eyeIn = 0f; linger = 0f;
        Hold(true);
        if (!held && fpc != null)
        {
            held = true; sens0 = fpc.mouseSensitivity; jump0 = fpc.enableJump; interact0 = interactor != null && interactor.enabled;
            fpc.playerCanMove = false; fpc.enableJump = false; fpc.mouseSensitivity = sens0 * .25f;   // trapped, not frozen
            if (interactor != null) interactor.enabled = false;   // [F] is the struggle now, not the nearest drawer
            if (player.TryGetComponent(out Rigidbody rb) && !rb.isKinematic) rb.linearVelocity = Vector3.zero;
        }
        Sting(.5f);
        EyeWalls(Mathf.Min(60, 20 + 10 * Captures), wallEyesTime);
    }

    // how: 0 something cut it short, 1 you broke its gaze, 2 it struck
    void Release(int how)
    {
        if (held)
        {
            held = false;
            if (fpc != null) { fpc.playerCanMove = true; fpc.enableJump = jump0; fpc.mouseSensitivity = sens0; }
            if (interactor != null) interactor.enabled = interact0;
        }
        if (panel != null) panel.SetActive(false);
        if (voice != null) voice.Stop();   // the whispers stop dead
        whisperShow = 0f;
        CloseEyes(how == 1 ? 8 : 0);
        linger = how == 1 ? lingerTime : 0f;
        if (state == S.Capture) { state = S.Away; timer = captureCooldown; }
    }

    // each frame it has you
    void Captured(float dist)
    {
        if (PauseMenu.Paused) return;
        capT += Time.deltaTime;
        float hold = stunTime + (Captures >= 3 ? .5f : 0f);   // the third time it looks a little longer first
        float left = Mathf.Clamp01((capT - hold) / struggleTime);

        // it hangs in front of your face (nearer each time), and your look is dragged to it
        Turn(player.position, 6f);
        Vector3 gap = Flat(player.position - root.position);
        if (agent.isOnNavMesh) agent.Move(gap.normalized * Mathf.Clamp(gap.magnitude - Mathf.Max(1.05f, 1.6f - .2f * Captures), -1f, 1f) * 2f * Time.deltaTime);
        Vector3 to = root.position - cam.transform.position;
        float drag = (ReducedMotion ? 1.2f : 2.6f) * Time.deltaTime;
        player.rotation = Quaternion.Slerp(player.rotation, Quaternion.LookRotation(Flat(to)), drag);
        if (fpc != null) fpc.SetPitch(Mathf.Lerp(Mathf.DeltaAngle(0f, cam.transform.localEulerAngles.x), -Mathf.Atan2(to.y, Flat(to).magnitude) * Mathf.Rad2Deg, drag));

        // more eyes the longer it lasts, and the voices
        eyeIn -= Time.deltaTime;
        if (eyeIn <= 0f && walls != null && walls.particleCount < Mathf.Min(MaxWallEyes, wallEyes + 25 * (Captures - 1))) { eyeIn = .3f; EyeWalls(2, wallEyesTime); }
        whisperIn -= Time.deltaTime;
        if (whisperIn <= 0f) { whisperIn = Mathf.Lerp(3.2f, 1.5f, left) * UnityEngine.Random.Range(.8f, 1.2f); Whisper(Mathf.Lerp(.45f, .9f, left)); }

        if (failAt > 0f)   // out of time: the glare (Look), then the blow
        {
            if (capT < failAt) return;
            if (health != null && !health.IsDead)
            {
                float dmg = Mathf.Min(failDamage, health.currentHealth - 12f);   // like the Auditor: it hurts, it does not finish you
                if (dmg > 0f) health.TakeDamage(dmg);
            }
            flash = 1f; flashColour = new Color(.55f, 0f, 0f);
            Sting(.5f);
            Release(2);
            return;
        }
        if (capT < hold) return;

        if (!panel.activeSelf) panel.SetActive(true);
        if (Input.GetKeyDown(KeyCode.F) || pressNow)
        {
            pressNow = false;
            Progress = Mathf.Min(1f, Progress + 1f / Mathf.Max(1, pressesNeeded));
            dip = 1f;                              // your sight clears for a blink
            level = 1.1f;                          // its glow falters
            CloseEyes(0, 4);
            if (agent.isOnNavMesh) agent.Move(-gap.normalized * .3f);   // and it gives a little
            if (palm != null) palm.Surge = 1f;
            beat.pitch = 1.5f; beat.PlayOneShot(beatClip, .5f);
        }
        bar.fillAmount = Progress;
        if (Progress >= .999f)
        {
            // the marks on your hands go off: a dedicated burst, the flash's charge is not touched
            if (palm != null) palm.Surge = 2.5f;
            flash = 1f; flashColour = new Color(1f, .82f, .78f);
            Sting(1.6f);
            Release(1);
            beat.pitch = 1.6f; beat.PlayOneShot(thudClip, 1f);
            Whisper(.5f, LastWhisper);
        }
        else if (left >= 1f) { failAt = capT + .7f; panel.SetActive(false); Sting(1.9f); }
    }

    void Whisper(float volume, string line = null)
    {
        if (line == null)
        {
            int n = Captures >= 2 ? Whispers.Length : Whispers.Length - 2, i = UnityEngine.Random.Range(0, n);
            if (i == lastLine) i = (i + 1) % n;
            lastLine = i; line = Whispers[i];
        }
        float pan = UnityEngine.Random.Range(-1f, 1f);
        panTo = UnityEngine.Random.value < .4f ? -pan : pan;   // some of them cross from one ear to the other
        voice.panStereo = pan; voice.pitch = UnityEngine.Random.Range(.75f, 1.1f);
        voice.PlayOneShot(whisperClip, volume);
        whisperText.text = line;
        whisperText.rectTransform.anchoredPosition = new Vector2(pan * 430f, UnityEngine.Random.Range(-90f, 190f));   // on the side it came from
        whisperShow = 2.6f;
    }

    // ---------------------------------------------------------------- every frame, whatever it is doing

    void CaptureFrame()
    {
        if (look == null) return;
        float dist = Flat(player.position - root.position).magnitude;
        // anything that takes you out of its hands lets you go
        if (state == S.Capture && (!shown() || (health != null && health.IsDead) || GameOverScreen.Open || (!act() && !PauseMenu.Paused) || dist > 6f)) Release(0);

        // seen and hunted: a little of it already. Held: all of it, worse each time and worse as the bar stays empty
        float want = state == S.Capture ? 1f : state == S.Chase ? .22f : 0f;
        fx = Mathf.MoveTowards(fx, want, Time.unscaledDeltaTime / Mathf.Max(.05f, blendTime));
        dip = Mathf.MoveTowards(dip, 0f, Time.deltaTime * 4f);
        pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 3f);
        flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 1.8f);
        float worse = state == S.Capture ? 1f + .2f * (Captures - 1) + .5f * Mathf.Clamp01((capT - stunTime) / struggleTime) * (1f - Progress) : 1f;
        float k = fx * Mathf.Min(maxIntensity, worse) * (1f - .7f * dip);
        float t = Time.time * wobbleFrequency * 6.283f, motion = ReducedMotion ? 0f : 1f;
        look.weight = fx > 0f ? 1f : 0f;
        lens.intensity.value = -peripheralWarp * k * motion * (.6f + .4f * Mathf.Sin(t));   // the room breathing
        lens.center.value = new Vector2(.5f + .05f * k * motion * Mathf.Sin(t * .63f), .5f + .05f * k * motion * Mathf.Cos(t * .41f));
        fringe.intensity.value = Mathf.Clamp01(chromatic * k + pulseStrength * pulse * fx);
        edge.intensity.value = Mathf.Clamp01((.34f * k + pulseStrength * pulse) * Mathf.Clamp01(fx * 3f));

        // your heart: slow while it hunts you, racing while it has you
        if (fx > .05f && want > 0f && Time.timeScale > 0f)
        {
            beatIn -= Time.deltaTime;
            if (beatIn <= 0f)
            {
                beatIn = state == S.Capture ? Mathf.Lerp(.8f, .42f, Mathf.Clamp01(capT / (stunTime + struggleTime))) : 1.05f * UnityEngine.Random.Range(.85f, 1.2f);   // (irregular while it hunts)
                pulse = 1f;
                beat.pitch = .7f; beat.PlayOneShot(beatClip, state == S.Capture ? .9f : .4f);
            }
        }
        voice.panStereo = Mathf.MoveTowards(voice.panStereo, panTo, Time.deltaTime * .9f);
        whisperShow -= Time.deltaTime;
        whisperText.alpha = Mathf.MoveTowards(whisperText.alpha, whisperShow > 0f ? .85f : 0f, Time.deltaTime * 2.5f);

        overlay.color = new Color(flashColour.r, flashColour.g, flashColour.b, flash * flash * .9f);
        burstLight.transform.position = cam.transform.position;
        burstLight.intensity = flash * flash * 70f;

        // the eyes it left on the walls: shut the ones you look at, then the rest
        if (linger > 0f)
        {
            linger -= Time.deltaTime;
            if (linger <= 0f) CloseEyes(0);
            else if (walls != null)
            {
                int n = walls.GetParticles(buf);
                for (int i = 0; i < n; i++)
                    if (Vector3.Angle(cam.transform.forward, buf[i].position - cam.transform.position) < 9f) Shut(ref buf[i]);
                walls.SetParticles(buf, n);
            }
        }
    }

    // ---------------------------------------------------------------- the eyes on the walls (the particles of EyeWalls)

    const int MaxWallEyes = 150;
    static readonly ParticleSystem.Particle[] buf = new ParticleSystem.Particle[MaxWallEyes];

    static bool Shut(ref ParticleSystem.Particle p)
    {
        if (p.remainingLifetime <= .5f) return false;
        p.startLifetime = 2f; p.remainingLifetime = .5f;   // (the last quarter of a life is the fade: half a second of it)
        return true;
    }

    // all but the first `keep` close, `only` of them at most
    void CloseEyes(int keep, int only = int.MaxValue)
    {
        if (walls == null) return;
        int n = walls.GetParticles(buf);
        for (int i = keep; i < n && only > 0; i++) if (Shut(ref buf[i])) only--;
        walls.SetParticles(buf, n);
    }

    void OnDisable()
    {
        Release(0);
        if (look != null) look.weight = 0f;
        if (overlay != null) overlay.color = Color.clear;
        if (whisperText != null) whisperText.alpha = 0f;
        if (burstLight != null) burstLight.intensity = 0f;
        fx = flash = 0f;
    }
}
