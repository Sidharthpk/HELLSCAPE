using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Disposable Play-mode verification. Does not save the scene or alter authored assets.
[InitializeOnLoad]
public static class AmalgamationRegression
{
    const string Key = "HellScape.AmalgamationRegression";
    static readonly List<string> log = new List<string>();
    static double deadline;
    static AmalgamationRegression()
    {
        EditorApplication.delayCall += () => {
            if (!File.Exists("Library/AmalgamationRegression.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete("Library/AmalgamationRegression.request"); Run();
        };
        EditorApplication.playModeStateChanged += s => {
            if (s != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            log.Clear(); deadline = EditorApplication.timeSinceStartup + 220;
            EditorApplication.update += Watch;
            Application.logMessageReceived += Error;
            EditorApplication.delayCall += () => Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include).StartCoroutine(Guard(Checks()));
        };
    }
    [MenuItem("HellScape/Tests/Tomas amalgamation (Play mode)")]
    public static void Run() { if (EditorApplication.isPlayingOrWillChangePlaymode) return; SessionState.SetBool(Key, true); EditorApplication.isPlaying = true; }
    static void Watch() { if (EditorApplication.timeSinceStartup > deadline) Finish("FAIL: timed out"); }
    static void Error(string message, string stack, LogType type) { if (type == LogType.Exception) log.Add("EXCEPTION: " + message + "\n" + stack); }
    static void Finish(string status)
    {
        Application.logMessageReceived -= Error; EditorApplication.update -= Watch;
        if (log.Exists(l => l.StartsWith("EXCEPTION:"))) status = "FAIL: runtime exception";
        File.WriteAllText("Library/AmalgamationRegression.result", string.Join("\n", log) + "\n" + status);
        SessionState.SetBool(Key, false); EditorApplication.isPlaying = false;
    }
    static IEnumerator Guard(IEnumerator checks)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(checks);
        while (stack.Count > 0)
        {
            object next;
            try { var current = stack.Peek(); if (!current.MoveNext()) { stack.Pop(); continue; } next = current.Current; }
            catch (Exception e) { Finish("FAIL: " + e); yield break; }
            if (next is IEnumerator child) stack.Push(child); else yield return next;
        }
        Finish("PASS: All amalgamation checks completed");
    }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); log.Add("PASS: " + label); }
    static object Field(object o, string n) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
    static IEnumerator Call(object o, string n) => (IEnumerator)o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, null);

    static IEnumerator Checks()
    {
        yield return null; yield return null;
        var plot = RevisedPlotDirector.Instance; var scene = plot.gameObject.scene;
        var title = Object.FindFirstObjectByType<TitleScreen>(FindObjectsInactive.Include);
        if (title != null) title.GetType().GetMethod("Finish", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(title, null);
        var box = DialogueBox.Instance; box.Clear(); box.charDelay = 0; box.fadeTime = 0; DialogueBox.AutoAdvanceAfter = .03f;
        var bosses = Object.FindObjectsByType<DemonBoss>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        log.Add("Scene=" + scene.name + "; bosses=" + bosses.Length + "; roots=" + string.Join(",", scene.GetRootGameObjects().Select(g => g.name)));
        Check(bosses.Length > 0, "Authored final boss exists in the loaded scene");
        var boss = bosses[0];
        var fusion = boss.GetComponent<TomasAmalgamation>();
        Check(fusion != null && fusion.Prepared, "Tomas, Vane and Maya visual sources survive independently of the original bodies");
        Check(boss.displayName == "TOMAS - THE AMALGAMATION", "Final boss HUD names Tomas");
        var silo = RevisedPlot.All<SiloEncounter>(scene)[0];
        var maya = RevisedPlot.All<Survivor>(scene).First(s => s.displayName == "Maya");
        var sam = RevisedPlot.All<Survivor>(scene).First(s => s.displayName == "Sam");
        silo.player.position = silo.killer.transform.position + Vector3.forward * 6f;
        if (silo.player.TryGetComponent<Rigidbody>(out var rb)) { rb.position = silo.player.position; rb.linearVelocity = Vector3.zero; }
        silo.health.maxHealth = 100000; silo.health.Revive(100000);
        typeof(SiloEncounter).GetMethod("ShowColleagues", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(silo, null);
        Judgement.KilledKiller = true;
        yield return Call(silo, "Flood");
        Check(maya.Turned && !maya.Rescued, "Ending Elias still turns Maya before escape");
        Check(!sam.Turned, "Sam remains eligible for rescue");
        silo.flood.ResetFlood(); silo.flood.risenDamned.ResetRisen(); box.Clear();
        plot.SetEcho(false);
        boss.player.position = boss.carRb.position + Vector3.up * 2f;
        if (rb != null) { rb.position = boss.player.position; rb.linearVelocity = Vector3.zero; }
        bool carWas = boss.carInteract.carController.enabled;
        boss.BeginChase(); boss.BeginChase();
        bool captured = false; float formedFor = 0f;
        for (float t = 0; t < 65f && !(bool)Field(boss, "chasing"); t += Time.deltaTime)
        {
            if (fusion.Absorbing && !captured) { yield return new WaitForSeconds(1.8f); Capture(boss.cutsceneCam, "tomas-fusion"); captured = true; }
            if (fusion.Formed && boss.cutsceneCam.enabled) { formedFor += Time.deltaTime; if (formedFor > 1.9f) CaptureOnce(boss.cutsceneCam); }
            yield return null;
        }
        Check(captured && fusion.Formed && !fusion.Absorbing, "Two bodies visibly merge into Tomas and leave no loose cutscene props");
        Check((bool)Field(boss, "chasing") && !boss.cutsceneCam.enabled, "Fusion hands control to the existing van chase");
        Check(boss.carInteract.carController.enabled == carWas && !boss.carRb.isKinematic, "Cutscene restores vehicle physics and prior controls");
        Check(EvidenceJournal.TomasDamned, "Tomas's choice to consume the bodies is recorded");
        boss.StopAllCoroutines(); Set(boss, "chasing", false);
        boss.transform.position = boss.standAt.position;
        boss.onDefeated = new UnityEngine.Events.UnityEvent(); bool ended = false; boss.onDefeated.AddListener(() => ended = true);
        boss.StartCoroutine(Call(boss, "Die"));
        var choices = ChoiceDialogue.Instance;
        var select = choices.GetType().GetMethod("Select", BindingFlags.Instance | BindingFlags.NonPublic);
        for (float t = 0; t < 85f && !ended; t += Time.deltaTime)
        {
            if (choices.Open) { Set(choices, "openedAt", Time.unscaledTime - 1f); select.Invoke(choices, new object[] { 0 }); }
            yield return null;
        }
        Check(ended && boss.Defeated, "Defeating the amalgamation reaches the existing judgement callback");
        Check(EvidenceJournal.VaneConfessed && EvidenceJournal.Has("Tomas's amalgamation") && !plot.InSequence, "Final accounts separate the three souls without the old Tomas escape");
        DialogueBox.AutoAdvanceAfter = 0;
    }
    static bool monsterCaptured;
    static void CaptureOnce(Camera camera) { if (monsterCaptured) return; monsterCaptured = true; Capture(camera, "tomas-three-heads"); }
    static void Capture(Camera camera, string name)
    {
        var rt = new RenderTexture(1280, 720, 24); var old = camera.targetTexture; var active = RenderTexture.active;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply(); Directory.CreateDirectory("docs/media/finale"); File.WriteAllBytes("docs/media/finale/" + name + ".png", pixels.EncodeToPNG()); }
        finally { camera.targetTexture = old; RenderTexture.active = active; Object.Destroy(rt); Object.Destroy(pixels); }
    }
}
