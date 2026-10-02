using System.Collections.Generic;
using UnityEngine;

// Hides the gameplay HUD (speedometer, car health, crosshair, stamina...) while a cutscene has the letterbox up,
// and puts back exactly what was showing before. The dialogue box, letterbox and fader stay.
public static class CutsceneHUD
{
    static readonly string[] canvases = { "CarCam", "CrosshairAndStamina" };
    static readonly string[] children = { "GameHUD" };
    static readonly List<Behaviour> hiddenCanvases = new List<Behaviour>();
    static readonly List<GameObject> hiddenObjects = new List<GameObject>();

    public static bool Hidden { get; private set; }

    // a reload (restart after dying) brings a fresh HUD: forget what was hidden in the old one
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        Hidden = false;
        hiddenCanvases.Clear();
        hiddenObjects.Clear();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnLoaded;
    }

    static void OnLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
    {
        if (m != UnityEngine.SceneManagement.LoadSceneMode.Single) return;
        Hidden = false;
        hiddenCanvases.Clear();
        hiddenObjects.Clear();
    }

    public static void Hide(bool hide)
    {
        if (hide == Hidden) return;
        Hidden = hide;
        if (hide)
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas || !c.enabled) continue;
                if (System.Array.IndexOf(canvases, c.name) >= 0) { c.enabled = false; hiddenCanvases.Add(c); }
                foreach (var n in children)
                {
                    var t = c.transform.Find(n);
                    if (t != null && t.gameObject.activeSelf) { t.gameObject.SetActive(false); hiddenObjects.Add(t.gameObject); }
                }
            }
        }
        else
        {
            foreach (var c in hiddenCanvases) if (c != null) c.enabled = true;
            foreach (var g in hiddenObjects) if (g != null) g.SetActive(true);
            hiddenCanvases.Clear();
            hiddenObjects.Clear();
        }
    }
}
