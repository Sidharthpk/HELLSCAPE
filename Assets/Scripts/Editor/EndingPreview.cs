using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Debug: jump straight to the bridge judgement with a forced verdict, to watch either ending (the Demon King
// binding you = "to be continued", or the damned one).
public static class EndingPreview
{
    const string Key = "HellScape.EndingPreview";   // "bound" / "demon" while a preview is starting

    [MenuItem("HellScape/Debug/Play Bound Ending (to be continued)")] static void Bound() => Begin("bound");
    [MenuItem("HellScape/Debug/Play Demon Ending")] static void Demon() => Begin("demon");

    static void Begin(string which)
    {
        SessionState.SetString(Key, which);
        if (EditorApplication.isPlaying) Load();
        else
        {
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { SessionState.EraseString(Key); return; }
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/MainGameScene.unity");
            EditorApplication.isPlaying = true;
        }
    }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(Key, "") != "") Load();
        };
    }

    // skip the title screen: arrive in the hell city the way the prologue does, then run the ending
    static void Load()
    {
        SleepSequence.WakeInHell = true;
        SceneManager.sceneLoaded += OnLoaded;
        SceneManager.LoadScene("MainGameScene");
    }

    static void OnLoaded(Scene s, LoadSceneMode m)
    {
        SceneManager.sceneLoaded -= OnLoaded;
        var e = Object.FindFirstObjectByType<EndingSequence>();
        if (e != null) e.StartCoroutine(Run(e, SessionState.GetString(Key, "bound") == "demon"));
        SessionState.EraseString(Key);
    }

    static IEnumerator Run(EndingSequence e, bool damned)
    {
        yield return new WaitForSeconds(9f);   // let the wake-up on the street finish (it moves the player and camera)

        // (Judgement resets on scene load, so the verdict is forced here)
        Judgement.KilledKiller = damned; Judgement.SparedKiller = !damned; Judgement.Karma = damned ? -3 : 3;
        var rescued = typeof(Survivor).GetProperty("Rescued");
        foreach (var sv in Survivor.All) if (sv != null) rescued.SetValue(sv, !damned);

        // stand on the bridge, 14 m short of where the being lands, facing it
        var being = e.demon;
        Transform player = e.playerCamera.transform.root;
        Vector3 land = being.endPoint.position;
        Vector3 away = player.position - land; away.y = 0f;
        Vector3 pos = land + away.normalized * 14f;
        if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out var hit, 200f)) pos = hit.point + Vector3.up * 1.1f;
        var cc = player.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
        player.SetPositionAndRotation(pos, Quaternion.LookRotation(-away.normalized));
        if (cc) cc.enabled = true;

        e.PlayEnding();
    }
}
