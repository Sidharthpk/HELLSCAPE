using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > Play > Car Chase: straight to the escape, for testing it. Enters Play in MainGameScene, skips the
// title and the prologue, and once you've come to on the red street puts you at the garage door with what you'd
// have by then: the van keys, the rifle, and the Escape checkpoint (so dying restarts the chase, not the game).
// Get in the van (F) and Kessler comes through the office doors.
public static class QuickStart
{
    const string Key = "HellScape.QuickStart";
    static int step;

    [MenuItem("HellScape/Play/Car Chase (from the garage)")]
    public static void CarChase()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
        if (SceneManager.GetActiveScene().name != "MainGameScene") { Debug.LogWarning("Open MainGameScene first."); return; }
        SessionState.SetString(Key, "chase");
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s != PlayModeStateChange.EnteredPlayMode || SessionState.GetString(Key, "") == "") return;
            step = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        };
    }

    static void Done()
    {
        SessionState.EraseString(Key);
        EditorApplication.update -= Tick;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Done(); return; }
        if (step == 0)
        {
            // past the title: the scene again, waking in hell
            if (TitleScreen.Showing) { SleepSequence.WakeInHell = true; SceneManager.LoadScene("MainGameScene"); }
            step = 1;
            return;
        }
        if (TitleScreen.Showing) return;
        var fpc = Object.FindFirstObjectByType<FirstPersonController>();
        if (fpc == null || !fpc.enabled || Time.timeSinceLevelLoad < 1f) return;   // still getting up off the street

        var garage = GarageBuilder.Find(fpc.gameObject.scene);
        var spot = garage != null ? garage.Find("Escape respawn") : null;
        if (spot != null)
        {
            var rb = fpc.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = Vector3.zero;
            fpc.transform.SetPositionAndRotation(spot.position, spot.rotation);
            Physics.SyncTransforms();
        }
        Inventory.Add("CarKeys");
        var gun = Object.FindFirstObjectByType<GunController>(FindObjectsInactive.Include);
        if (gun != null && !GunController.Equipped) gun.Equip();
        var escape = Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name.Contains("Escape"));
        if (escape != null) escape.Activate();
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow("(Quick start: the car chase.)|The van's right there. Keys in my hand.");
        Done();
    }
}
