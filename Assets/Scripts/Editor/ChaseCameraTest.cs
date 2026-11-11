using System.Collections;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > Tests > Van chase camera. Plays the real chase from getting in the van (cutscene sped up), drives the
// road to the bridge and every 2 s saves what the car camera sees to Logs/chase/NN.png, with where Vane is on screen,
// whether he is on the line to the van, and the frame rate. Checks Vane stays in the picture and off the van before the
// bridge, and that he crawls once the van is on it. Result: Logs/chase/result.txt.
public static class ChaseCameraTest
{
    const string Key = "HellScape.ChaseCamera", Dir = "Logs/chase/";
    static IEnumerator run;
    static double waitUntil;
    static readonly StringBuilder log = new StringBuilder();

    [MenuItem("HellScape/Tests/Van chase camera")]
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
        Time.timeScale = 1f;
        System.IO.File.WriteAllText(Dir + "result.txt", log.ToString());
        Debug.Log("[robot] Van chase camera:\n" + log);
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
        var van = boss.carInteract;
        var cams = van.carCamera.GetComponent<FixedCameras>();
        var rb = van.GetComponent<Rigidbody>();
        var cp = Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name.Contains("Escape"));
        if (cp != null) cp.Activate();
        van.SendMessage("EnterCar");
        log.AppendLine("van interpolation " + rb.interpolation);
        Time.timeScale = 5f;
        end = EditorApplication.timeSinceStartup + 60;
        while (!boss.IsChasing && EditorApplication.timeSinceStartup < end) yield return null;
        Time.timeScale = 1f;
        Check(boss.IsChasing, "the chase begins");
        if (!boss.IsChasing) yield break;
        van.GetComponent<CarHealth>().damageScale = 0f;   // (a robot can't dodge: it has to reach the bridge)
        PlaytestDriver.DriveVan(17f, 0f, true);   // (any faster and the robot's simple steering puts it into the wall at the 470 m bend)

        int shots = 0, road = 0, seen = 0, clear = 0;
        float crawlAt = -1f, cutAt = -1f;
        bool crawlAnim = false;
        end = EditorApplication.timeSinceStartup + 120;
        int frames = Time.frameCount; double last = EditorApplication.timeSinceStartup;
        Vector3 lastCam = van.carCamera.transform.position;
        while (boss.IsChasing && EditorApplication.timeSinceStartup < end)
        {
            yield return 2f;
            if (!boss.IsChasing) break;
            float a = cams.Along(rb.position);
            Camera cam = van.carCamera;
            Transform c = cam.transform;
            double now = EditorApplication.timeSinceStartup;
            float fps = (Time.frameCount - frames) / Mathf.Max(.01f, (float)(now - last));
            frames = Time.frameCount; last = now;
            Vector3 bossChest = boss.transform.position + Vector3.up * (boss.Crawling ? 2.5f : 7f);
            Vector3 vb = cam.WorldToViewportPoint(bossChest), vv = cam.WorldToViewportPoint(rb.position + Vector3.up);
            bool inView = vb.z > 0f && vb.x > -.05f && vb.x < 1.05f && vb.y > -.25f && vb.y < 1.1f;
            // is he on the line from the camera to the van, down at his own height?
            Vector3 toVan = rb.position + Vector3.up - c.position;
            float t = Vector3.Dot(bossChest - c.position, toVan.normalized);
            Vector3 onLine = c.position + toVan.normalized * Mathf.Clamp(t, 0f, toVan.magnitude);
            Vector3 miss = onLine - boss.transform.position; miss.y = 0f;
            bool blocks = t > 0f && t < toVan.magnitude && miss.magnitude < 3.5f && onLine.y < boss.transform.position.y + (boss.Crawling ? 5f : 12f);
            float gap = Vector3.Distance(boss.transform.position, rb.position);
            log.AppendLine($"t{shots:00} road {a:0} m, van {rb.position:F0} {rb.linearVelocity.magnitude:0} m/s, Vane {gap:0} m, cam {c.position:F0} fov {cam.fieldOfView:0}, Vane on screen ({vb.x:F2},{vb.y:F2}) z {vb.z:0}, van ({vv.x:F2},{vv.y:F2}) z {vv.z:0}, blocks {blocks}, {fps:0} fps, crawl {boss.Crawling}");
            // (right on the van he's ghosted by BossSeeThrough: only count him when he's further off)
            if (a < 730f && a > 40f) { road++; if (inView) seen++; if (!blocks || gap < 10f) clear++; }
            if (a >= 735f && cutAt < 0f) cutAt = a;
            if (boss.Crawling && crawlAt < 0f) crawlAt = a;
            if (boss.Crawling && boss.animator.GetCurrentAnimatorStateInfo(0).IsName("Stun")) crawlAnim = true;
            Snap(cam, Dir + (shots++).ToString("00") + ".png");
        }
        Check(road > 3 && seen >= road * .8f, $"Vane in the picture before the bridge ({seen}/{road} samples)");
        Check(road > 3 && clear >= road * .9f, $"Vane off the line to the van before the bridge ({clear}/{road} samples)");
        Check(crawlAt >= 730f, "he starts crawling at the bridge (" + crawlAt.ToString("F0") + " m)");
        Check(crawlAnim, "the crawl animation is what plays");
        log.AppendLine("--- driver\n" + PlaytestDriver.Report);
    }

    static void Snap(Camera cam, string file)
    {
        var rt = new RenderTexture(960, 540, 24);
        var was = cam.targetTexture;
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = was;
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 540); tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
    }
}
