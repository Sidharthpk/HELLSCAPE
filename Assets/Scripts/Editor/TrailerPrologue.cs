using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Editor-only: robot pass over the normal-city prologue that films clean (no UI) footage for the trailer.
//   TrailerPrologue.Start("scout", 12, 640, 360)  low-res film of the whole run to find the shots (marks in Logs/trailer.log)
// Takes: names in 'plan' -> (seconds) start filming at the named mark.
public static class TrailerPrologue
{
    const string Key = "HellScape.Trailer.Prologue";
    static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    static string logPath = "Logs/trailer.log";

    static void Mark(string s)
    {
        File.AppendAllText(logPath, $"[{Time.time:0.0}s f{TrailerShoot.Frames}] {s}\n");
    }

    public static void Start(string take, int fps, int w, int h)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
        if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty) { Debug.LogWarning("Save the open scene first."); return; }
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity");
        SessionState.SetString(Key, $"{take}|{fps}|{w}|{h}");
        File.WriteAllText(logPath, "");
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged += st =>
        {
            string arg = SessionState.GetString(Key, "");
            if (st != PlayModeStateChange.EnteredPlayMode || arg == "") return;
            SessionState.EraseString(Key);
            var a = arg.Split('|');
            stack.Clear();
            stack.Push(Scout(a[0], int.Parse(a[1]), int.Parse(a[2]), int.Parse(a[3])));
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        };
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { stack.Clear(); TrailerShoot.Stop(); EditorApplication.update -= Tick; return; }
        if (stack.Count == 0) { EditorApplication.update -= Tick; return; }
        try
        {
            for (int guard = 0; guard < 8 && stack.Count > 0; guard++)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator inner) { stack.Push(inner); continue; }
                break;
            }
        }
        catch (System.Exception e) { Mark("EXCEPTION " + e); stack.Clear(); }
    }

    static IEnumerator Wait(float s) { float t0 = Time.time; while (Time.time - t0 < s) yield return null; }

    static IEnumerator Scout(string take, int fps, int w, int h)
    {
        IEnumerator Idle() { yield return null; while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) yield return null; }
        var dir = Object.FindFirstObjectByType<PrologueDirector>();
        while (dir == null) { dir = Object.FindFirstObjectByType<PrologueDirector>(); yield return null; }
        TrailerShoot.Start(take, fps, w, h);
        Mark("film started");
        var run = dir.deliveries;
        var van = run.van;
        DialogueBox.AutoAdvanceAfter = 1.2f;
        yield return Wait(6f);

        for (int i = 0; i < run.stops.Length; i++)
        {
            var s = run.stops[i];
            float tw = Time.time;
            while (!ObjectiveUI.Current.Contains(s.objective) && Time.time - tw < 90f) yield return null;
            Mark($"stop {i} objective up");
            if (i == 0)
            {
                // a real drive down the street first
                Mark("drive begins");
                PlaytestDriver.DriveVan(10f, 0f, true);
                float td0 = Time.time;
                while (PlaytestDriver.Busy && Time.time - td0 < 45f) yield return null;
                PlaytestDriver.Stop();
                Mark("drive ends");
            }
            van.body.position = s.bay.position + Vector3.up * 0.5f; van.body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            yield return Wait(0.5f);
            van.Exit();
            float t0 = Time.time;
            while (s.customer != null && !s.customer.enabled && Time.time - t0 < 10f) yield return null;
            Mark($"stop {i} customer ready");
            if (s.customer != null && s.customer.enabled) s.customer.Use();
            yield return Wait(1f);
            yield return Idle();
            yield return Wait(2f);
            Mark($"stop {i} done");
        }

        float tdw = Time.time;
        while (!ObjectiveUI.Current.Contains(run.depotObjective) && Time.time - tdw < 60f) yield return null;
        van.body.position = run.depotBay.position + Vector3.up * 0.5f; van.body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        yield return Wait(0.5f);
        van.Exit();
        float td = Time.time;
        while (!run.Done && Time.time - td < 60f) yield return null;
        Mark("depot done");
        yield return Wait(3f);

        var player = dir.fpc.transform; var rb = player.GetComponent<Rigidbody>();
        void Put(Vector3 p, Vector3 look) { if (rb != null) rb.linearVelocity = Vector3.zero; player.SetPositionAndRotation(p, Quaternion.LookRotation(look)); Physics.SyncTransforms(); }
        float th = Time.time;
        while (!ObjectiveUI.Current.Contains(dir.goHome) && Time.time - th < 90f) yield return null;
        Mark("walk home up");
        Put(dir.hallway.center, Vector3.forward); yield return Wait(3f);
        while (DialogueBox.Instance.Busy) yield return null;
        th = Time.time;
        while (!ObjectiveUI.Current.Contains(dir.findDerek) && Time.time - th < 20f) { Put(dir.flat.center, dir.body.position - dir.flat.center); yield return null; }
        Mark($"in the flat, objective '{ObjectiveUI.Current}'");
        yield return Wait(60f);   // let the news / witness play on its own
        Mark("end of scout");
        DialogueBox.AutoAdvanceAfter = 0f;
        TrailerShoot.Stop();
    }
}
