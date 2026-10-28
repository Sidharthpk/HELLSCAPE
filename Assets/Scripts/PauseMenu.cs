using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Esc: the game stops (time, sound, controls) and the pause menu comes up: resume, back to the last checkpoint, the
// controls, volume and mouse sensitivity, quit to the title or to the desktop. Built by HellScape > UI > Build
// Modern UI into each scene's story canvas. Not available over the title screen, the death screen, a note or the ending.
public class PauseMenu : MonoBehaviour
{
    public static bool Paused { get; private set; }

    public CanvasGroup group;
    public GameObject controlsPanel;
    public Button resumeButton, restartButton, controlsButton, titleButton, quitButton;
    public Slider volume, sensitivity;
    public string titleScene = "MainGameScene";
    private GameObject settingsPanel;

    private readonly List<Behaviour> stopped = new List<Behaviour>();
    private float timeScale0 = 1f;
    private CursorLockMode lock0;
    private bool cursor0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => Paused = false;

    // ---- display & accessibility: saved in PlayerPrefs, applied at start-up so they hold over the title too
    static UnityEngine.Rendering.Universal.ColorAdjustments exposure;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootDisplay()
    {
        var go = new GameObject("Brightness");
        DontDestroyOnLoad(go);
        var v = go.AddComponent<UnityEngine.Rendering.Volume>();
        v.isGlobal = true; v.priority = 10f;
        var p = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
        exposure = p.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
        v.sharedProfile = p;
        SetBrightness(PlayerPrefs.GetFloat("hs.brightness", 0f));
        if (PlayerPrefs.HasKey("hs.fullscreen")) SetFullscreen(PlayerPrefs.GetInt("hs.fullscreen") == 1);
    }

    static void SetBrightness(float stops) => exposure.postExposure.Override(stops);

    static void SetFullscreen(bool on)
    {
        if (on) Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        else Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
    }

    // a settings button that flips an option and shows its state
    void BuildSettings()
    {
        if (controlsPanel == null || controlsButton == null || volume == null) return;
        var parent = controlsPanel.transform.parent;
        var src = (RectTransform)controlsPanel.transform;
        var panel = Instantiate(controlsPanel, parent);
        panel.name = "Settings";
        foreach (Transform c in panel.transform) Destroy(c.gameObject);   // keep the panel look, drop the controls text
        settingsPanel = panel;
        var label = volume.transform.parent.Find(volume.name + " label");

        void Place(RectTransform rt, float y) { rt.SetParent(panel.transform, false); rt.anchoredPosition = new Vector2(48f, y); }
        TMPro.TMP_Text Label(string text, float y)
        {
            var t = Instantiate(label.gameObject).GetComponent<TMPro.TMP_Text>();
            Place((RectTransform)t.transform, y); t.text = text; return t;
        }
        void Toggle(string name, float y, bool on, System.Action<bool> apply)
        {
            var b = Instantiate(controlsButton);
            Place((RectTransform)b.transform, y);
            var t = b.GetComponentInChildren<TMPro.TMP_Text>();
            void Refresh() => t.text = name + ":  " + (on ? "ON" : "OFF");
            Refresh();
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => { on = !on; apply(on); Refresh(); });
        }

        Label("DISPLAY & ACCESSIBILITY", -40f);
        Toggle("FULLSCREEN", -100f, Screen.fullScreen, on => { SetFullscreen(on); PlayerPrefs.SetInt("hs.fullscreen", on ? 1 : 0); });
        Toggle("FILM GRAIN", -170f, PlayerPrefs.GetInt("hs.grain", 1) == 1, on => { RetroLook.SetGrain(on); PlayerPrefs.SetInt("hs.grain", on ? 1 : 0); });
        Toggle("INVERT LOOK Y", -240f, PlayerPrefs.GetInt("hs.invertY", 0) == 1, on => { ApplyInvert(on); PlayerPrefs.SetInt("hs.invertY", on ? 1 : 0); });

        Label("BRIGHTNESS", -330f);
        var s = Instantiate(volume);
        Place((RectTransform)s.transform, -366f);
        s.minValue = -1f; s.maxValue = 2f;
        s.SetValueWithoutNotify(PlayerPrefs.GetFloat("hs.brightness", 0f));
        s.onValueChanged.RemoveAllListeners();
        s.onValueChanged.AddListener(v => { SetBrightness(v); PlayerPrefs.SetFloat("hs.brightness", v); });
        panel.SetActive(false);
    }

    static void ApplyInvert(bool on)
    {
        foreach (var f in FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) f.invertCamera = on;
    }

    void Awake()
    {
        Paused = false;
        ApplyTypography();
        Show(false);
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        BuildSettings();
        if (controlsButton != null) controlsButton.onClick.AddListener(() =>
        {
            controlsPanel.SetActive(!controlsPanel.activeSelf);
            if (settingsPanel != null) settingsPanel.SetActive(false);
        });
        if (titleButton != null) titleButton.onClick.AddListener(ToTitle);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);
        MakeRoomForSettings();
        ApplyInvert(PlayerPrefs.GetInt("hs.invertY", 0) == 1);

        AudioListener.volume = PlayerPrefs.GetFloat("hs.volume", 1f);
        if (volume != null)
        {
            volume.SetValueWithoutNotify(AudioListener.volume);
            volume.onValueChanged.AddListener(v => { AudioListener.volume = v; PlayerPrefs.SetFloat("hs.volume", v); });
        }
        float sens = PlayerPrefs.GetFloat("hs.sensitivity", 2f);
        ApplySensitivity(sens);
        if (sensitivity != null)
        {
            sensitivity.SetValueWithoutNotify(sens);
            sensitivity.onValueChanged.AddListener(v => { ApplySensitivity(v); PlayerPrefs.SetFloat("hs.sensitivity", v); });
        }
    }

    // a SETTINGS button after CONTROLS: the buttons below it and the two sliders move down one row
    void MakeRoomForSettings()
    {
        if (settingsPanel == null || titleButton == null || quitButton == null) return;
        var sb = Instantiate(controlsButton, controlsButton.transform.parent);
        sb.name = "SETTINGS";
        sb.GetComponentInChildren<TMPro.TMP_Text>().text = "SETTINGS";
        sb.onClick.RemoveAllListeners();
        sb.onClick.AddListener(() =>
        {
            settingsPanel.SetActive(!settingsPanel.activeSelf);
            controlsPanel.SetActive(false);
        });
        var rt = (RectTransform)sb.transform;
        var drop = new Vector2(0f, -64f);
        rt.anchoredPosition = ((RectTransform)controlsButton.transform).anchoredPosition + drop;
        foreach (Transform t in new[] { titleButton.transform, quitButton.transform, volume.transform, sensitivity.transform,
            volume.transform.parent.Find(volume.name + " label"), sensitivity.transform.parent.Find(sensitivity.name + " label") })
            if (t != null) ((RectTransform)t).anchoredPosition += drop;
    }

    public void ApplyTypography()
    {
        var font = GameFont.Main;
        if (font == null) return;
        foreach (var text in GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
        }
    }

    void OnDestroy()
    {
        // a scene change while paused (restart, quit to title) must not leave the next scene frozen and silent
        if (Paused) { Paused = false; Time.timeScale = 1f; AudioListener.pause = false; }
    }

    static void ApplySensitivity(float v)
    {
        foreach (var f in FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) f.mouseSensitivity = v;
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (Paused) Resume();
        else if (CanPause()) Pause();
    }

    static bool CanPause() =>
        !TapLock.Active && !LockPick.Active && !VaultDial.Active && VaultDial.ClosedFrame != Time.frameCount && TapLock.ClosedFrame != Time.frameCount && LockPick.ClosedFrame != Time.frameCount && !TitleScreen.Showing && !GameOverScreen.Open && !NoteViewer.Blocking && !EndingSequence.Playing && Time.timeScale > 0f;

    public void Pause()
    {
        if (Paused) return;
        Paused = true;
        timeScale0 = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        lock0 = Cursor.lockState; cursor0 = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // whatever reads the keys and the mouse: off until we're back (the look, the fists, the gun, the wheel)
        stopped.Clear();
        foreach (var b in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
        {
            if (!b.enabled) continue;
            if (b is FirstPersonController || b is PunchController || b is GunController || b is PrometeoCarController || b is DriveBy
                || b is CarInteract || b is PrologueVan || b is VanRecovery)
            { b.enabled = false; stopped.Add(b); }
        }

        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.transform.SetParent(transform.root, false);
        }
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        Show(true);
        if (resumeButton != null) EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
    }

    public void Resume()
    {
        if (!Paused) return;
        Show(false);
        foreach (var b in stopped) if (b != null) b.enabled = true;
        stopped.Clear();
        Time.timeScale = timeScale0 > 0f ? timeScale0 : 1f;
        AudioListener.pause = false;
        Cursor.lockState = lock0;
        Cursor.visible = cursor0;
        Paused = false;
    }

    // back to the last checkpoint; from the top of the scene if none was reached yet
    public void Restart()
    {
        Resume();
        if (Checkpoint.RespawnCurrent()) return;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // a new game from the title: everything the run remembered is forgotten, as on a fresh start of the program
    public void ToTitle()
    {
        Resume();
        NewGame.Reset();
        SceneManager.LoadScene(titleScene);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void Show(bool on)
    {
        if (group == null) return;
        group.alpha = on ? 1f : 0f;
        group.interactable = on;
        group.blocksRaycasts = on;
    }
}

// Wipes what a run keeps in statics (inventory, judgement, checkpoint, the title's "already seen"...) by running
// every [RuntimeInitializeOnLoadMethod(SubsystemRegistration)] reset in the game again, exactly as at start-up.
public static class NewGame
{
    public static void Reset()
    {
        foreach (var m in Object.FindObjectsByType<MusicManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.Destroy(m.gameObject);
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        foreach (var type in typeof(NewGame).Assembly.GetTypes())
            foreach (var method in type.GetMethods(flags))
            {
                if (method.GetParameters().Length != 0) continue;
                var attrs = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if (attrs.Length == 0 || ((RuntimeInitializeOnLoadMethodAttribute)attrs[0]).loadType != RuntimeInitializeLoadType.SubsystemRegistration) continue;
                try { method.Invoke(null, null); } catch (System.Exception e) { Debug.LogWarning("New game reset: " + type.Name + "." + method.Name + ": " + e.Message); }
            }
        TitleScreen.ShowAgain();
        SleepSequence.WakeInHell = false;
        SleepSequence.FallSpot = null;
        Time.timeScale = 1f;
    }
}
