using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The start screen: a slow cinematic drift through the hell city (the red sky, the lamps, the lost souls in the air)
// with the HELLSCAPE title and a minimal menu over it. Nothing of the game runs behind it. The city shown is a
// visual-only copy of HellWorld (every script, collider and agent stripped), so the real HellWorld never wakes
// early. START fades to black and loads the prologue.
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

    [Range(0f, 1f)] public float dim = 0.3f;   // the city behind the title, a little darker

    [Header("Story")]
    public string prologueScene = "Prologue";   // START plays this first (the flat, Derek, the roof); empty = start here

    [Header("Sound")]
    public AudioSource music;               // a procedural drone if it has no clip
    public AudioSource sfx;

    private GameObject city;
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
        if (cam != null) cam.enabled = true;
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

        if (sleep.hellSkybox != null) RenderSettings.skybox = sleep.hellSkybox;
        RenderSettings.fogColor = sleep.hellFogColor;
        RenderSettings.fogDensity = menuFogDensity;
        if (sleep.sun != null) { sleep.sun.color = sleep.hellSunColor; sleep.sun.intensity = sleep.hellSunIntensity; }
        if (sleep.dayWorld != null) sleep.dayWorld.SetActive(false);
        if (core != null) RenderSettings.fogDensity = coreFogDensity;
        else city = VisualCopy(sleep.hellWorld);
        DynamicGI.UpdateEnvironment();

        if (MusicManager.HasMusic("Title")) MusicManager.Area("Title");   // your own title music (Sound Manager)
        else if (music != null)
        {
            if (music.clip == null) music.clip = ProceduralAudio.Drone();
            music.loop = true;
            music.volume = 0f;
            music.Play();
            StartCoroutine(Volume(music, 0.55f, 3f));
        }
        StartCoroutine(core != null ? CoreRide() : Shots());
        if (logo != null) StartCoroutine(LogoIn(1.4f, 2.2f));
        else if (title != null) StartCoroutine(TitleLetters(1.4f, 0.28f, 0.7f));
        else hintAt = Time.unscaledTime;
        yield return new WaitForSeconds(1.2f);
        yield return Fade(ui, 1f, 2f);
        if (ui != null) ui.interactable = true;
    }

    // HellWorld's look without any of its behaviour: only visuals, the hell volume, and the harmless ambient movers
    static GameObject VisualCopy(GameObject source)
    {
        if (source == null) return null;
        var copy = Instantiate(source);   // inactive, like the original: nothing has woken up yet
        copy.name = "HellWorld (Title)";
        foreach (var mb in copy.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(mb is UnityEngine.Rendering.Volume) && !(mb is LostSoul) && !(mb is LampFlicker) && !(mb is LightFlicker))
                DestroyImmediate(mb);
        foreach (var a in copy.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) DestroyImmediate(a);
        foreach (var j in copy.GetComponentsInChildren<Joint>(true)) DestroyImmediate(j);
        foreach (var r in copy.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(r);
        foreach (var c in copy.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        copy.SetActive(true);
        return copy;
    }

    IEnumerator Shots()
    {
        if (shots == null || shots.Length == 0 || cam == null) yield break;
        for (int i = 0; !starting; i = (i + 1) % shots.Length)
        {
            var s = shots[i];
            float d = Mathf.Max(2f, s.duration);
            StartCoroutine(Fade(cut, 0f, 1.2f));
            for (float t = 0f; t < d && !starting; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / d);
                Vector3 p = Vector3.Lerp(s.fromPos, s.toPos, k);
                p += new Vector3(Mathf.PerlinNoise(t * 0.3f, 1f) - 0.5f, Mathf.PerlinNoise(2f, t * 0.3f) - 0.5f, 0f) * 0.25f;   // handheld drift
                cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(Vector3.Lerp(s.fromLook, s.toLook, k) - p));
                if (t > d - 1.2f && cut != null && cut.alpha < 0.01f) StartCoroutine(Fade(cut, 1f, 1.2f));
                yield return null;
            }
        }
    }

    // inside the tunnel: the clip twists the hex tube while the camera flies down its middle, swaying and rolling a little
    IEnumerator CoreRide()
    {
        if (cam == null) yield break;
        var lightGo = new GameObject("CoreGlow", typeof(Light));
        lightGo.transform.SetParent(cam.transform, false);
        var glow = lightGo.GetComponent<Light>();
        glow.type = LightType.Point; glow.color = new Color(1f, 0.35f, 0.15f); glow.range = 40f; glow.intensity = 6f;

        var root = core.transform;
        core.speed = 10.4f / coreRideTime;                           // the clip is ~10.4s: stretch it over the whole ride
        while (!starting)
        {
            core.Play(0, 0, 0f);
            StartCoroutine(Fade(cut, 0f, 1.2f));
            for (float t = 0f; t < coreRideTime && !starting; t += Time.deltaTime)
            {
                float k = t / coreRideTime;
                float z = Mathf.Lerp(50f, -105f, k);
                var local = new Vector3(Mathf.Sin(t * 0.5f) * 0.8f, 3.6f + Mathf.Sin(t * 0.37f) * 0.6f, z);
                cam.transform.SetPositionAndRotation(root.TransformPoint(local),
                    root.rotation * Quaternion.Euler(Mathf.Sin(t * 0.3f) * 3f, 180f + Mathf.Sin(t * 0.21f) * 6f, Mathf.Sin(t * 0.17f) * 12f + t * 1.5f));
                cam.fieldOfView = 68f + Mathf.Sin(t * 0.6f) * 5f;
                if (t > coreRideTime - 1.2f && cut != null && cut.alpha < 0.01f) StartCoroutine(Fade(cut, 1f, 1.2f));
                yield return null;
            }
        }
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
        if (!Showing) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // the title glitches now and then: a sideways jolt and a dropout
        if (title != null && titleGroup != null)
        {
            bool glitch = Mathf.PerlinNoise(Time.time * 0.9f, 3.3f) > 0.72f && Random.value < 0.35f;
            title.anchoredPosition = titleHome + (glitch ? new Vector2(Random.Range(-14f, 14f), Random.Range(-3f, 3f)) : Vector2.zero);
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
