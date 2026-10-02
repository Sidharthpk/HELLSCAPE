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

    private readonly List<Behaviour> stopped = new List<Behaviour>();
    private float timeScale0 = 1f;
    private CursorLockMode lock0;
    private bool cursor0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => Paused = false;

    void Awake()
    {
        Paused = false;
        Show(false);
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (controlsButton != null) controlsButton.onClick.AddListener(() => controlsPanel.SetActive(!controlsPanel.activeSelf));
        if (titleButton != null) titleButton.onClick.AddListener(ToTitle);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);

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
        !TitleScreen.Showing && !GameOverScreen.Open && !NoteViewer.Blocking && !EndingSequence.Playing && Time.timeScale > 0f;

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
