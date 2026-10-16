using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Debug: HellScape > Debug > Start In Office. Plays MainGameScene awake in the red city (no prologue, no void) and
// drops you just inside the office front door (OfficeArrival) once you have control.
public static class StartInOffice
{
    const string Key = "HellScape.StartInOffice";

    [MenuItem("HellScape/Debug/Start In Office")]
    static void Menu() { SessionState.SetString(Key, "1"); EditorApplication.isPlaying = true; }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged += st =>
        {
            if (st != PlayModeStateChange.EnteredPlayMode || SessionState.GetString(Key, "") == "") return;
            SessionState.EraseString(Key);
            SleepSequence.WakeInHell = true;
            SceneManager.sceneLoaded += OnLoaded;
            SceneManager.LoadScene("MainGameScene");
        };
    }

    static void OnLoaded(Scene s, LoadSceneMode m)
    {
        SceneManager.sceneLoaded -= OnLoaded;
        var e = Object.FindFirstObjectByType<EndingSequence>();
        if (e != null) e.StartCoroutine(Drop(e));
    }

    static IEnumerator Drop(EndingSequence e)
    {
        var player = e.playerCamera.transform.root;
        var fpc = player.GetComponentInChildren<FirstPersonController>();
        // wait for the wake-up (SleepSequence.Start switches you off and lays you in the street) to hand you control
        yield return null; yield return null;
        for (float t = 0f; t < 30f && (fpc == null || !fpc.enabled || EndingSequence.Playing || TitleScreen.Showing); t += Time.deltaTime) yield return null;
        var arrival = GameObject.Find("OfficeArrival");
        Vector3 at = arrival != null ? arrival.transform.position + arrival.transform.forward * 1.5f : new Vector3(982.5f, 4.5f, 874f);
        Vector3 fwd = arrival != null ? arrival.transform.forward : Vector3.forward;
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.position = at; }
        player.SetPositionAndRotation(at, Quaternion.LookRotation(fwd));
        Physics.SyncTransforms();
        Debug.Log("[StartInOffice] dropped at " + at);
    }
}
