using UnityEngine;

// Hold SPACE during a cutscene to fast-forward it (ponytail: a speed-up, not a true skip: every cutscene is its own
// coroutine, so racing through keeps all their state changes intact). Only touches Time.timeScale when it's normal
// (1), so the game-over slow-mo, pause and the boss slow-mo are left alone.
public class CutsceneSkip : MonoBehaviour
{
    const float Speed = 8f;
    bool ours;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Make() { DontDestroyOnLoad(new GameObject("CutsceneSkip", typeof(CutsceneSkip))); }

    static bool InCutscene()
    {
        if (EndingSequence.Playing || OfficeFlashback.Playing) return true;
        foreach (var k in FindObjectsByType<KillerCutscenes>(FindObjectsSortMode.None)) if (k.Playing) return true;
        foreach (var s in FindObjectsByType<StabCutscene>(FindObjectsSortMode.None)) if (s.Playing) return true;
        foreach (var t in FindObjectsByType<TurningCutscene>(FindObjectsSortMode.None)) if (t.Playing) return true;
        return false;
    }

    void Update()
    {
        bool want = Input.GetKey(KeyCode.Space) && !TitleScreen.Showing && (ours || Time.timeScale == 1f) && InCutscene();
        if (want) { Time.timeScale = Speed; ours = true; }
        else if (ours) { if (Time.timeScale == Speed) Time.timeScale = 1f; ours = false; }
    }

    void OnDisable() { if (ours && Time.timeScale == Speed) Time.timeScale = 1f; }
}
