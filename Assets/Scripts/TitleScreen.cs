using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Animated kaleidoscope backdrop with the existing title, music and prologue transition.
public class TitleScreen : MonoBehaviour
{
    [System.Serializable]
    public class Shot
    {
        public Vector3 fromPos, toPos;
        public Vector3 fromLook, toLook;
        public float duration = 10f;
    }

    public static bool Showing { get; private set; }
    static bool seen;                       // a scene reload (game over with no checkpoint) skips straight in

    // "quit to title" from the pause menu: the next load of the scene opens on the title again
    public static void ShowAgain() => seen = false;

    [Header("Scene")]
    public SleepSequence sleep;             // knows the day/hell worlds, sky, fog and sun
    public Camera cam;                      // the title camera (above every other camera while showing)
    public Shot[] shots;
    public Animator core;                   // the sci-fi core tunnel: when set, the camera rides down its inside instead of the city shots
    public float coreRideTime = 30f;
    public float coreFogDensity = 0.008f;
    public float menuFogDensity = 0.012f;   // the in-game hell fog is too thick for a city vista
    public Behaviour[] lockDuring;          // player movement, fists
    public Canvas[] hideDuring;             // the HUD canvases (only the Canvas is switched off: scripts still find their UI)

    [Header("UI")]
    public CanvasGroup ui;                  // title + menu
    public CanvasGroup cut;                 // black: shot changes and the hand-over
    public RectTransform title;             // jitters now and then
    public CanvasGroup titleGroup;
    public Button startButton, quitButton;

    [Range(0f, 1f)] public float dim = 0.3f;   // extra dimming behind the title

    [Header("Story")]
    public string prologueScene = "Prologue";   // START plays this first (the flat, Derek, the roof); empty = start here

    [Header("Sound")]
    public AudioSource music;               // a procedural drone if it has no clip
    public AudioSource sfx;
    public float stingHit = 3.43f;          // seconds into the opening sting (Sound Manager: Title/Opening sting) where the beat lands: the logo slams in on it
    public float musicAfterHit = 3.5f;      // the title music fades in this long after the hit, under the sting's tail
    private AudioSource sting;
    private double stingDspStart;
    private float shakeUntil;

    private Material backgroundMaterial;
    private bool starting;
    private Vector2 titleHome;
    private bool[] hudWasOn;
    private TMPro.TMP_Text hint;               // "PRESS ANY BUTTON", fading in and out
    private float hintAt = -1f;                // when the title finished appearing: no prompt (and no start) before that
    private Image logo;                        // the HELLSCAPE title art (Resources/HellscapeUI), in place of the old text

    void Awake()
    {
        if (seen || SleepSequence.WakeInHell) { Finish(); return; }   // a reload, or arriving from the prologue
        Showing = true;
        foreach (var b in lockDuring) if (b != null) b.enabled = false;
        hudWasOn = new bool[hideDuring.Length];
        for (int i = 0; i < hideDuring.Length; i++)
            if (hideDuring[i] != null) { hudWasOn[i] = hideDuring[i].enabled; hideDuring[i].enabled = false; }
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0;
            cam.enabled = true;
        }
        if (core != null) core.gameObject.SetActive(false);
        CreateBackground();
        if (cut != null) cut.alpha = 1f;
        if (ui != null) { ui.alpha = 0f; ui.interactable = false; }
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);
        if (title != null) titleHome = title.anchoredPosition;
        var art = Resources.Load<Sprite>("HellscapeUI/HellScape Title");
        if (title != null && art != null)
        {
            foreach (var tx in title.GetComponentsInChildren<TMPro.TMP_Text>()) tx.gameObject.SetActive(false);
            logo = new GameObject("Logo", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            logo.transform.SetParent(title, false);
            var lr = logo.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
            logo.sprite = art; logo.preserveAspect = true; logo.raycastTarget = false; logo.color = new Color(1f, 1f, 1f, 0f);
            // UI mesh effects follow the logo's alpha and scale, including the music-drop reveal.
            var outline = logo.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.015f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = true;
            var shadow = logo.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(10f, -14f);
            shadow.useGraphicAlpha = true;
            title.sizeDelta = new Vector2(1400f, 1400f * art.rect.height / art.rect.width);
        }
        if (ui != null) { var h = ui.transform.Find("Hint"); if (h != null) { hint = h.GetComponent<TMPro.TMP_Text>(); if (hint != null) { hint.text = "PRESS ANY BUTTON"; hint.fontSize = 34f; } } }
        if (ui != null && dim > 0f)
        {
            var go = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(ui.transform, false);
            go.transform.SetAsFirstSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, dim);
            img.raycastTarget = false;
        }
    }

    IEnumerator Start()
    {
        if (!Showing) yield break;
        yield return null;
        foreach (var b in lockDuring) if (b != null) b.enabled = false;

        if (sleep != null && sleep.dayWorld != null) sleep.dayWorld.SetActive(false);
        StartCoroutine(Fade(cut, 0f, 1.2f));
        // the opening sting first (the logo lands on its beat), then the title music under its tail
        var stingClip = Sounds.Clip("Title/Opening sting", null);
        if (stingClip != null) StartCoroutine(StingIntro(stingClip));
        else
        {
            StartMusic();
            if (logo != null) StartCoroutine(LogoIn(1.4f, 2.2f));
            else if (title != null) StartCoroutine(TitleLetters(1.4f, 0.28f, 0.7f));
            else hintAt = Time.unscaledTime;
        }
        yield return new WaitForSeconds(1.2f);
        yield return Fade(ui, 1f, 2f);
        if (ui != null) ui.interactable = true;
    }

    void CreateBackground()
    {
        if (ui == null) return;
        var go = new GameObject("Kaleidoscope Background", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(ui.transform.parent, false);
        go.transform.SetAsFirstSibling();
        var image = go.GetComponent<RawImage>();
        var rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        image.raycastTarget = false;
        image.color = Color.black;
        var shader = Resources.Load<Shader>("TitleKaleidoscope");
        if (shader == null) return;
        backgroundMaterial = new Material(shader);
        image.material = backgroundMaterial;
        image.color = Color.white;
    }

    void OnDestroy()
    {
        if (backgroundMaterial != null) Destroy(backgroundMaterial);
    }

    void StartMusic()
    {
        if (MusicManager.HasMusic("Title")) MusicManager.Area("Title");   // your own title music (Sound Manager)
        else if (music != null)
        {
            if (music.clip == null) music.clip = ProceduralAudio.Drone();
            music.loop = true;
            music.volume = 0f;
            music.Play();
            StartCoroutine(Volume(music, 0.55f, 3f));
        }
    }

    IEnumerator StingIntro(AudioClip clip)
    {
        // the first frames of the title stall (shaders warming up): the sting would run on through the
        // stall while the logo can't move, and land late. Wait for a few smooth frames first (3 s at most).
        int smooth = 0;
        for (float waited = 0f; smooth < 5 && waited < 3f; waited += Time.unscaledDeltaTime)
        {
            smooth = Time.unscaledDeltaTime < .05f ? smooth + 1 : 0;
            yield return null;
        }
        if (starting) yield break;
        sting = gameObject.AddComponent<AudioSource>();
        sting.clip = clip; sting.volume = Sounds.Volume("Title/Opening sting");
        sting.spatialBlend = 0f; sting.playOnAwake = false; sting.loop = false;
        // Share the audio engine clock with the reveal; frame stalls cannot shift the beat.
        stingDspStart = AudioSettings.dspTime + 0.1;
        sting.PlayScheduled(stingDspStart);
        StartCoroutine(MusicAfter(stingHit + musicAfterHit));
        if (logo != null) StartCoroutine(LogoOnBeat());
        else hintAt = Time.unscaledTime;
    }

    IEnumerator MusicAfter(float seconds)
    {
        while (!starting && AudioSettings.dspTime - stingDspStart < seconds) yield return null;
        if (!starting) StartMusic();
    }

    // with the sting: the logo creeps up out of the dark through the whole build-up, then BOOM on the beat
    IEnumerator LogoOnBeat()
    {
        float hit = Mathf.Max(0.01f, stingHit);
        while (!starting)
        {
            float t = Mathf.Max(0f, (float)(AudioSettings.dspTime - stingDspStart));
            if (t >= hit) break;
            logo.color = new Color(1f, 1f, 1f, .22f * Mathf.Pow(t / hit, 1.6f));
            yield return null;
        }
        if (starting) yield break;
        // Full opacity on the first rendered frame of the drop, with no extra fade delay.
        logo.color = Color.white;
        if (ui != null) ui.alpha = 1f;
        if (titleGroup != null) titleGroup.alpha = 1f;
        hintAt = Time.unscaledTime;
        shakeUntil = Time.unscaledTime + .35f;
        for (float t = 0f; t < .6f && !starting; t += Time.unscaledDeltaTime)
        {
            float k = 1f - (1f - t / .6f) * (1f - t / .6f);   // ease out
            logo.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, k);   // the punch
            yield return null;
        }
        logo.color = Color.white;
        logo.rectTransform.localScale = Vector3.one;
    }

    // the title art fades up out of the dark
    IEnumerator LogoIn(float delay, float fade)
    {
        for (float t = 0f; t < delay + fade && !starting; t += Time.deltaTime)
        {
            logo.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, (t - delay) / fade));
            yield return null;
        }
        logo.color = Color.white;
        hintAt = Time.unscaledTime;
    }

    // the title appears one letter at a time: each fades in `fade` seconds after the one before, `step` apart
    IEnumerator TitleLetters(float delay, float step, float fade)
    {
        var texts = title.GetComponentsInChildren<TMPro.TMP_Text>();
        for (float t = 0f; !starting; t += Time.deltaTime)
        {
            bool done = true;
            foreach (var tx in texts)
            {
                tx.ForceMeshUpdate();   // back to the base colours, then scale each letter's alpha
                var info = tx.textInfo;
                for (int i = 0; i < info.characterCount; i++)
                {
                    var ch = info.characterInfo[i];
                    if (!ch.isVisible) continue;
                    float a = Mathf.Clamp01((t - delay - i * step) / fade);
                    if (a < 1f) done = false;
                    var cols = info.meshInfo[ch.materialReferenceIndex].colors32;
                    for (int v = 0; v < 4; v++) cols[ch.vertexIndex + v].a = (byte)(cols[ch.vertexIndex + v].a * a);
                }
                tx.UpdateVertexData(TMPro.TMP_VertexDataUpdateFlags.Colors32);
            }
            if (done) { hintAt = Time.unscaledTime; yield break; }
            yield return null;
        }
    }

    void Update()
    {
        if (backgroundMaterial != null)
        {
            backgroundMaterial.SetFloat("_MotionTime", Time.unscaledTime);
            backgroundMaterial.SetFloat("_Aspect", Screen.width / (float)Mathf.Max(1, Screen.height));
        }
        if (!Showing) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // the title glitches now and then: a sideways jolt and a dropout
        if (title != null && titleGroup != null)
        {
            bool glitch = hintAt >= 0f && Time.unscaledTime > hintAt + 1f && Mathf.PerlinNoise(Time.time * 0.9f, 3.3f) > 0.72f && Random.value < 0.35f;
            title.anchoredPosition = titleHome + (glitch ? new Vector2(Random.Range(-14f, 14f), Random.Range(-3f, 3f)) : Vector2.zero);
            // the beat: a short, hard shake that dies away
            float shake = Mathf.Clamp01((shakeUntil - Time.unscaledTime) / .35f);
            if (shake > 0f) title.anchoredPosition += Random.insideUnitCircle * 22f * shake;
            titleGroup.alpha = glitch && Random.value < 0.4f ? 0.35f : 1f;
        }

        // the prompt breathes: fades in and out, forever (from nothing, once the whole title is up)
        if (hint != null) hint.alpha = hintAt < 0f ? 0f : Mathf.Lerp(0.12f, 1f, 0.5f - 0.5f * Mathf.Cos((Time.unscaledTime - hintAt) * 2.4f)) * Mathf.Clamp01(Time.unscaledTime - hintAt);

        if (ui != null && ui.interactable && hintAt >= 0f && Input.anyKeyDown && !Input.GetKeyDown(KeyCode.Escape))
            StartGame();
    }

    public void StartGame()
    {
        if (starting || !Showing) return;
        starting = true;
        if (ui != null) ui.interactable = false;
        StartCoroutine(Begin());
    }

    IEnumerator Begin()
    {
        Sounds.OneShot(sfx, "Title/Start pressed", ProceduralAudio.Thud(), 1f);
        MusicManager.Stop(2f);
        StartCoroutine(Fade(ui, 0f, 0.8f));
        if (music != null) StartCoroutine(Volume(music, 0f, 2f));
        if (sting != null) StartCoroutine(Volume(sting, 0f, 0.8f));
        yield return Fade(cut, 1f, 1.2f);
        yield return new WaitForSeconds(0.6f);

        if (!string.IsNullOrEmpty(prologueScene) && Application.CanStreamedLevelBeLoaded(prologueScene))
        {
            seen = true;
            Showing = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene(prologueScene);
            yield break;
        }

        // no prologue scene in the build: straight to waking up on the red street
        seen = true;
        Showing = false;
        SleepSequence.WakeInHell = SleepSequence.FromStory = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
    }

    void Finish()
    {
        Showing = false;
        if (cam != null) cam.enabled = false;
        if (core != null) core.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static IEnumerator Fade(CanvasGroup g, float to, float time)
    {
        if (g == null) yield break;
        float from = g.alpha;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            g.alpha = Mathf.Lerp(from, to, t / time);
            yield return null;
        }
        g.alpha = to;
    }

    static IEnumerator Volume(AudioSource a, float to, float time)
    {
        float from = a.volume;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            a.volume = Mathf.Lerp(from, to, t / time);
            yield return null;
        }
        a.volume = to;
    }
}
