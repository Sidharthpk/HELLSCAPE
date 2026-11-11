using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// The other end of a Teleporter that loads a scene (city <-> office + silo): once the new scene is up, stand the player at
// Teleporter.ArriveAt, start that door's music and activate its checkpoint.
public static class SceneArrival
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
    }

    static void OnLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Teleporter.ArriveAt == null) return;
        var players = RevisedPlot.All<FirstPersonController>(scene);
        if (players.Length > 0) players[0].StartCoroutine(Place(players[0].transform));
    }

    static IEnumerator Place(Transform player)
    {
        string at = Teleporter.ArriveAt, cp = Teleporter.ArriveCheckpoint, music = Teleporter.ArriveMusic, line = Teleporter.ArriveLine;
        var fader = Object.FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);
        if (fader != null) fader.fade.alpha = 1f;
        yield return null; yield return null;   // every Start has run (SleepSequence reads ArriveAt in its own)
        Teleporter.ArriveAt = Teleporter.ArriveCheckpoint = Teleporter.ArriveMusic = Teleporter.ArriveLine = null;
        SceneProgress.Restore(player.gameObject.scene, player);
        var to = GameObject.Find(at);
        if (to == null) Debug.LogError("[SceneArrival] no object called " + at);
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = false; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        if (to != null) player.SetPositionAndRotation(to.transform.position, to.transform.rotation);
        Physics.SyncTransforms();
        MusicManager.Area(music);
        if (!string.IsNullOrEmpty(cp))
            foreach (var c in Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.name == cp) { c.Activate(); break; }
        // Returning through the office's front door must also leave a usable retry point in the city.
        if (Checkpoint.Current == null)
            foreach (var c in RevisedPlot.All<Checkpoint>(player.gameObject.scene))
                if (c.name == "Checkpoint - Hell city") { c.Activate(); break; }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (fader != null) yield return fader.FadeTo(0f, .8f);
        if (!string.IsNullOrEmpty(line) && DialogueBox.Instance != null) DialogueBox.Instance.Say(line);
    }
}
