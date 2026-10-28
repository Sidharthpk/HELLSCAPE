using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Debug: robot pass through the office horror. HellScape > Debug > Test Office Horror (frames in Logs/office/).
public static class OfficeHorrorTest
{
    const string Key = "HellScape.OfficeHorrorTest";
    static string dir => Path.Combine(Application.dataPath, "../Logs/office");
    static int shots;
    static Camera cam;
    static Transform player;
    static StringWriter log = new StringWriter();

    [MenuItem("HellScape/Debug/Test Office Horror")]
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

    static void Shot(string name)
    {
        var rt = new RenderTexture(640, 360, 24);
        var old = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
        RenderTexture.active = rt;
        var tex = new Texture2D(640, 360); tex.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(dir, (shots++).ToString("00") + "_" + name + ".png"), tex.EncodeToPNG());
        Object.Destroy(rt); Object.Destroy(tex);
    }

    static void L(string s) { File.AppendAllText(Path.Combine(dir, "log.txt"), "[" + Time.time.ToString("0.0") + "] " + s + "\n"); }

    // stand where you can see the target: the first of eight directions with a clear line
    static void Look(Vector3 target, float dist, float height = 1f)
    {
        Vector3 best = target + new Vector3(dist, 0f, 0f);
        for (int a = 0; a < 16; a++)
        {
            Vector3 p = target + Quaternion.Euler(0f, a * 22.5f, 0f) * Vector3.forward * dist;
            p.y = target.y;
            if (Physics.CheckCapsule(p + Vector3.up * .5f, p + Vector3.up * 1.5f, .3f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (Physics.Linecast(p + Vector3.up * 1.6f, target + Vector3.up * height, ~0, QueryTriggerInteraction.Ignore)) continue;
            best = p; break;
        }
        FaceFrom(best, target, height);
    }

    static void FaceFrom(Vector3 from, Vector3 target, float height)
    {
        from.y = target.y + 1f;
        var rb = player.GetComponent<Rigidbody>(); if (rb != null) { rb.linearVelocity = Vector3.zero; rb.position = from; }
        player.position = from;
        Vector3 d = target + Vector3.up * height - from; d.y = 0f;
        player.rotation = Quaternion.LookRotation(d.normalized);
        cam.transform.LookAt(target + Vector3.up * height);
        Physics.SyncTransforms();
    }

    static void Face(Vector3 target, float dist, float height = 1.6f)
    {
        Vector3 from = target + new Vector3(dist, 0f, 0f);
        from.y = target.y + 1f;
        var rb = player.GetComponent<Rigidbody>(); if (rb != null) { rb.linearVelocity = Vector3.zero; rb.position = from; }
        player.position = from;
        Vector3 d = target + Vector3.up * height - from; d.y = 0f;
        player.rotation = Quaternion.LookRotation(d.normalized);
        cam.transform.LookAt(target + Vector3.up * (height - .3f));
        Physics.SyncTransforms();
    }

    static IEnumerator Wait(float s) { for (float t = 0f; t < s; t += Time.deltaTime) { typeof(DialogueBox).GetField("AutoAdvanceAfter").SetValue(null, 1f); yield return null; } }

    static IEnumerator Run(EndingSequence e)
    {
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
        shots = 0;
        yield return new WaitForSeconds(9f);   // wake-up on the street
        cam = e.playerCamera; player = cam.transform.root;
        var fpc = player.GetComponentInChildren<FirstPersonController>();
        var plot = RevisedPlotDirector.Instance;
        var horror = Object.FindFirstObjectByType<OfficeHorrorDirector>();
        L("horror director: " + (horror != null));
        if (horror == null || plot == null) yield break;
        var ghosts = Object.FindObjectsByType<GhostNPC>(FindObjectsSortMode.None);
        GhostNPC G(string n) => ghosts.First(g => g.ghostName == n);

        // into the office, red city
        Face(G("Ben").transform.position, 3.2f);
        yield return Wait(1f);
        foreach (var n in new[] { "Ben", "Priya", "Nadia", "Tomas" }) { Look(G(n).transform.position, 2.8f); yield return Wait(.6f); Shot("burnt_" + n); }

        // how you question them changes them
        G("Priya").GetComponent<WitnessLook>().Set(WitnessLook.Mode.Lost);
        G("Nadia").GetComponent<WitnessLook>().Set(WitnessLook.Mode.Damned);
        G("Ben").GetComponent<WitnessLook>().Set(WitnessLook.Mode.Confessed);
        yield return Wait(3f);
        foreach (var n in new[] { "Priya", "Nadia", "Ben" }) { Look(G(n).transform.position, 2.8f); yield return Wait(.5f); Shot("fate_" + n); }

        // the static bodies
        var bodies = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "Burnt body").ToList();
        L("burnt bodies: " + bodies.Count);
        int bi = 0;
        foreach (var b in bodies) { Look(b.position, 3.2f, .4f); yield return Wait(.4f); Shot("body" + bi++); }

        // stage by stage
        var counter = Object.FindFirstObjectByType<FuseBox>().counter;
        Look(G("Omar").transform.position, 6f);
        for (int stage = 1; stage <= 3; stage++)
        {
            counter.Add();
            L("fuse " + stage + " stage=" + horror.Stage);
            for (int i = 0; i < 6; i++) { yield return Wait(1.3f); Shot("stage" + stage + "_" + i); }
        }
        // the Auditor
        L("auditor enabled=" + horror.Auditor.enabled + " active=" + horror.Auditor.Active + " timer=" + horror.Auditor.TimerLeft);
        bool f1 = horror.Auditor.TryFind(14f, true, out var _s1), f2 = horror.Auditor.TryFind(14f, false, out var _s2);
        L("can find spot in view=" + f1 + " behind=" + f2);
        for (int i = 0; i < 12 && !horror.Auditor.Present; i++) yield return Wait(1.5f);
        if (!horror.Auditor.Present && horror.Auditor.TryFind(9f, true, out var showAt)) horror.Auditor.Apparition(showAt, 5f);   // (the natural one waits for you to be idle and unobserved)
        yield return Wait(.3f);
        L("auditor present: " + horror.Auditor.Present + " active=" + horror.Auditor.Active);
        if (horror.Auditor.Present)
            for (int i = 0; i < 4; i++)
            {
                var a = GameObject.Find("The Auditor");
                if (a != null) cam.transform.LookAt(a.transform.position + Vector3.up * 1.4f);
                Shot("auditor" + i); yield return Wait(.5f);
            }
        // the Echo, safe, then not
        plot.SetEcho(true); yield return Wait(1.5f); Shot("echo_clean");
        horror.Advance(4); yield return Wait(2f); Shot("echo_stage4");
        plot.SetEcho(false);
        horror.OnTapeStep(1); yield return Wait(1.2f); horror.OnTapeStep(2); yield return Wait(1f); Shot("cctv_behind");
        horror.OnTapeSolved();
        for (int i = 0; i < 8; i++) { yield return Wait(1.5f); Shot("breakdown" + i); }
        L("done; auditor present=" + horror.Auditor.Present + " stage=" + horror.Stage + " hp=" + player.GetComponentInChildren<PlayerHealth>().currentHealth);
        Debug.Log("[robot] office horror frames in Logs/office");
    }
}
