using System.Collections;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > Tests > Office collapse. Plays MainGameScene from the Escape checkpoint, starts the chase
// (DemonBoss.BeginChase) and saves what the cutscene camera sees every second to Logs/collapse/NN.png; then checks
// the chase began, the office is down, and that the Escape retry (ResetChase) stands it up again with the rubble gone.
// Result: Logs/collapse/result.txt.
public static class OfficeCollapseTest
{
    const string Key = "HellScape.OfficeCollapse", Dir = "Logs/collapse/";
    static IEnumerator run;
    static double waitUntil;
    static readonly StringBuilder log = new StringBuilder();

    [MenuItem("HellScape/Tests/Office collapse")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
        if (SceneManager.GetActiveScene().name != "MainGameScene") EditorSceneManager.OpenScene("Assets/Scenes/MainGameScene.unity");
        System.IO.Directory.CreateDirectory(Dir);
        foreach (var f in System.IO.Directory.GetFiles(Dir)) System.IO.File.Delete(f);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            SessionState.EraseBool(Key);
            log.Clear(); waitUntil = 0; run = Main();
            Application.logMessageReceived += Heard;
            EditorApplication.update += Tick;
        };
    }

    static void Heard(string text, string trace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error) log.AppendLine("ERROR: " + text.Split('\n')[0]);
    }

    static void Tick()
    {
        if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup < waitUntil) return;
        bool more = false;
        try { more = EditorApplication.isPlaying && run.MoveNext(); }
        catch (System.Exception e) { log.AppendLine("FAIL: the test threw: " + e); }
        if (more) { if (run.Current is float f) waitUntil = EditorApplication.timeSinceStartup + f; return; }
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Heard;
        System.IO.File.WriteAllText(Dir + "result.txt", log.ToString());
        Debug.Log("Office collapse:\n" + log);
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
    }

    static void Check(bool ok, string what) => log.AppendLine((ok ? "PASS " : "FAIL ") + what);

    static IEnumerator Main()
    {
        yield return 1f;
        if (TitleScreen.Showing) { SleepSequence.WakeInHell = true; SceneManager.LoadScene("MainGameScene"); }
        double end = EditorApplication.timeSinceStartup + 40;
        while ((TitleScreen.Showing || Time.timeSinceLevelLoad < 3f) && EditorApplication.timeSinceStartup < end) yield return null;
        var boss = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        if (boss == null || boss.brokenOffice == null) { Check(false, "a DemonBoss with the broken office (run the builder)"); yield break; }
        var cp = Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name.Contains("Escape"));
        if (cp != null) cp.Activate();
        Vector3 signHome = boss.kvSign != null ? boss.kvSign.position : Vector3.zero;
        boss.BeginChase();
        end = EditorApplication.timeSinceStartup + 60;
        for (int i = 0; !boss.IsChasing && EditorApplication.timeSinceStartup < end; i++)
        {
            Snap(boss.cutsceneCam, Dir + i.ToString("00") + ".png");
            yield return 0.4f;
        }
        Check(boss.IsChasing, "the chase begins after the cutscene");
        Check(boss.officeBuilding.GetComponentsInChildren<Renderer>().All(r => !r.enabled), "the whole office is hidden");
        var rubble = GameObject.Find("Office (coming down)");
        Check(rubble != null, "the rubble is there");
        if (rubble != null)
        {
            float top = rubble.GetComponentsInChildren<Renderer>().Max(r => r.bounds.max.y);
            log.AppendLine("  rubble top " + top.ToString("F1") + " m (the office stood to " + boss.officeBuilding.GetComponentsInChildren<Renderer>().Max(r => r.bounds.max.y).ToString("F1") + ")");
        }
        if (boss.kvSign != null) Check(Vector3.Distance(boss.kvSign.position, signHome) > 20f, "the sign was thrown (landed " + boss.kvSign.position + ", van " + boss.carRb.position + ")");
        boss.ResetChase();
        yield return 0.5f;
        if (boss.kvSign != null) Check(Vector3.Distance(boss.kvSign.position, signHome) < 0.01f, "the Escape retry puts the sign back");
        Check(boss.officeBuilding.GetComponentsInChildren<Renderer>().All(r => r.enabled) && GameObject.Find("Office (coming down)") == null, "the Escape retry stands the office up again, rubble gone");
    }

    static void Snap(Camera cam, string file)
    {
        if (cam == null || !cam.enabled) return;
        var rt = new RenderTexture(960, 540, 24);
        var was = cam.targetTexture;
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = was;
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
    }
}
