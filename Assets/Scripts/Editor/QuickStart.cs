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
    public static void CarChase() => Begin("chase");

    // HellScape > Play > Elias Fight: onto the silo floor, as if you'd just jumped. The landing plays, Elias drops in,
    // Maya throws the rifle down: pick it up (F) and the fight is on.
    [MenuItem("HellScape/Play/Elias Fight (silo floor)")]
    public static void EliasFight() => Begin("fight");

    // HellScape > Play > Kessler Chase: through the office's silo door with the key in your hand, into the tunnel.
    [MenuItem("HellScape/Play/Kessler Chase (tunnel)")]
    public static void KesslerChase() => Begin("tunnel");

    static void Begin(string what)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
        if (SceneManager.GetActiveScene().name != "MainGameScene") { Debug.LogWarning("Open MainGameScene first."); return; }
        SessionState.SetString(Key, what);
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

        if (SessionState.GetString(Key, "") == "tunnel")
        {
            Inventory.Add("SiloKey");
            var door = Object.FindObjectsByType<Teleporter>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "SiloDoor");
            if (door != null) door.Go();
            Done();
            return;
        }

        if (SessionState.GetString(Key, "") == "fight")
        {
            var enc = Object.FindFirstObjectByType<SiloEncounter>(FindObjectsInactive.Include);
            if (enc != null)
            {
                var body = fpc.GetComponent<Rigidbody>();
                if (body != null) body.linearVelocity = Vector3.zero;
                fpc.transform.SetPositionAndRotation(enc.siloCenter.position + new Vector3(0.5f, 1.2f, 2.5f), Quaternion.LookRotation(Vector3.back));
                Physics.SyncTransforms();
            }
            Done();
            return;
        }

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
