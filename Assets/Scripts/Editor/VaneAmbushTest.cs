using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Debug: HellScape > Debug > Test Vane Ambush. Starts the post-tape ambush and films it (Logs/vane/).
public static class VaneAmbushTest
{
    const string Key = "HellScape.VaneAmbushTest";
    static string dir => Path.Combine(Application.dataPath, "../Logs/vane");
    static int shots;

    [MenuItem("HellScape/Debug/Test Vane Ambush")]
    static void Menu() { SessionState.SetString(Key, "1"); EditorApplication.isPlaying = true; }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged += st =>
        {
            if (st != PlayModeStateChange.EnteredPlayMode || SessionState.GetString(Key, "") == "") return;
            SleepSequence.WakeInHell = true;
            SceneManager.sceneLoaded += OnLoaded;
            SceneManager.LoadScene("OfficeSilo");
        };
    }

    static void OnLoaded(Scene s, LoadSceneMode m)
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SessionState.EraseString(Key);
        var e = Object.FindFirstObjectByType<EndingSequence>();
        if (e != null) e.StartCoroutine(Run(e));
    }

    static void Log(string s) { File.AppendAllText(Path.Combine(dir, "log.txt"), "[" + Time.time.ToString("0.0") + "] " + s + "\n"); }

    static void Shot(Camera cam, string name)
    {
        var rt = new RenderTexture(640, 360, 24);
        var old = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
        RenderTexture.active = rt;
        var tex = new Texture2D(640, 360); tex.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(dir, (shots++).ToString("00") + "_" + name + ".png"), tex.EncodeToPNG());
        Object.Destroy(rt); Object.Destroy(tex);
    }

    static IEnumerator Run(EndingSequence e)
    {
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
        shots = 0;
        yield return new WaitForSeconds(9f);
        var cam = e.playerCamera; var player = cam.transform.root;
        var plot = RevisedPlotDirector.Instance;
        var conf = Object.FindFirstObjectByType<KesslerConfrontation>();
        var vane = Object.FindObjectsByType<GhostNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(g => g.ghostName == "Vane");
        // stand in the hall near where he used to wait, looking away from it
        var ben = Object.FindObjectsByType<GhostNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(g => g.ghostName == "Ben");
        Vector3 at = ben.transform.position + new Vector3(3f, 1f, 0f);   // an open part of the office
        var rb = player.GetComponent<Rigidbody>(); if (rb != null) { rb.linearVelocity = Vector3.zero; rb.position = at; }
        player.position = at; player.rotation = Quaternion.LookRotation(Vector3.right); Physics.SyncTransforms();
        plot.SetEcho(true);
        yield return new WaitForSeconds(1f);
        Log("arming; vane active=" + vane.gameObject.activeSelf + " at " + vane.transform.position + " player " + player.position);
        conf.Arm();
        var auto = typeof(DialogueBox).GetField("AutoAdvanceAfter");
        var select = typeof(ChoiceDialogue).GetMethod("Select", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        bool scared = false; int picks = 0;
        for (float t = 0f; t < 90f; t += 0.5f)
        {
            auto.SetValue(null, 1.2f);
            yield return new WaitForSeconds(.5f);
            bool open = ChoiceDialogue.Instance != null && ChoiceDialogue.Instance.Open;
            string tag = conf.Ambushing ? (vane.gameObject.activeSelf ? "creep" : "wait") : (open ? "choice" : "after");
            if (conf.Ambushing || open || t < 6f) Shot(cam, tag);
            if (conf.Ambushing && !scared) { float d = Vector3.Distance(vane.transform.position, player.position); if (d < 4f) Log("vane dist " + d.ToString("0.0")); }
            if (open) { Log("choices open, picking #" + (picks % 2)); select.Invoke(ChoiceDialogue.Instance, new object[] { 0 }); picks++; }
            if (!conf.Ambushing && picks >= 3 && !open && !DialogueBox.Instance.Busy) { Log("talk finished after " + picks + " picks"); break; }
        }
        Log("done; ambushing=" + conf.Ambushing + " picks=" + picks);
        Debug.Log("[robot] vane ambush frames in Logs/vane");
    }
}
