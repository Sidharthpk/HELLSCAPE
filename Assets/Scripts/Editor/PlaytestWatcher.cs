using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only playtest log: while in Play mode, writes errors/warnings, scene loads, every new dialogue line and a
// heartbeat (where the player is, health, checkpoint, fps) to Logs/playtest.log, plus flags for likely problems
// (falling out of the world, stuck with controls locked, low fps). Delete this file once playtesting is done.
[InitializeOnLoad]
static class PlaytestWatcher
{
    static readonly string LogPath = Path.GetFullPath("Logs/playtest.log");
    static double nextBeat;
    static string lastLine = "";
    static float lockedFor;
    static int frames;
    static double fpsStart;

    static PlaytestWatcher()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode)
            {
                File.WriteAllText(LogPath, "");
                Write("=== PLAY START ===");
                Application.logMessageReceivedThreaded += OnLog;
                SceneManager.sceneLoaded += OnScene;
                EditorApplication.update += Tick;
                fpsStart = EditorApplication.timeSinceStartup;
                frames = 0;
            }
            else if (s == PlayModeStateChange.ExitingPlayMode)
            {
                Write("=== PLAY STOP ===");
                Application.logMessageReceivedThreaded -= OnLog;
                SceneManager.sceneLoaded -= OnScene;
                EditorApplication.update -= Tick;
            }
        };
    }

    static void OnScene(Scene s, LoadSceneMode m) => Write("SCENE " + s.name);

    static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Log) return;
        if (msg.Contains("triangles where the distance")) return;   // the silo model's big triangles: known noise
        string first = stack.Split('\n')[0];
        Write($"{type.ToString().ToUpper()} {msg.Split('\n')[0]}  @ {first}");
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        frames++;

        // each dialogue line, once it has finished typing (logged when the next one replaces it, or the box closes)
        var d = DialogueBox.Instance;
        string t = d != null && d.text != null && d.Busy ? d.text.text : "";
        if (lastLine.Length > 0 && !t.StartsWith(lastLine)) Write("SAY " + lastLine.Replace('\n', ' '));
        lastLine = t;

        if (EditorApplication.timeSinceStartup < nextBeat) return;
        double dt = EditorApplication.timeSinceStartup - fpsStart;
        float fps = (float)(frames / dt);
        frames = 0;
        fpsStart = EditorApplication.timeSinceStartup;
        nextBeat = EditorApplication.timeSinceStartup + 3.0;

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) { Write($"BEAT scene={SceneManager.GetActiveScene().name} no player fps={fps:F0}"); return; }
        var h = p.GetComponentInParent<PlayerHealth>();
        if (h == null) h = p.GetComponentInChildren<PlayerHealth>();
        var fpc = p.GetComponentInParent<FirstPersonController>();
        if (fpc == null) fpc = p.GetComponentInChildren<FirstPersonController>();
        var rb = p.GetComponentInParent<Rigidbody>();
        Vector3 pos = p.transform.position;
        bool canMove = fpc != null && fpc.enabled;

        Write($"BEAT scene={SceneManager.GetActiveScene().name} pos=({pos.x:F1},{pos.y:F1},{pos.z:F1}) hp={(h ? h.currentHealth.ToString("F0") : "-")} " +
              $"move={canMove} cp={(Checkpoint.Current ? Checkpoint.Current.name.Replace("Checkpoint - ", "") : "-")} ts={Time.timeScale:F1} fps={fps:F0}" +
              (TitleScreen.Showing ? " TITLE" : ""));

        // likely problems
        if (rb != null && rb.linearVelocity.y < -25f) Write($"WARN falling fast (vy {rb.linearVelocity.y:F0}) at {pos}");
        if (pos.y < -60f) Write($"WARN below the world at {pos}");
        lockedFor = canMove || TitleScreen.Showing || Time.timeScale == 0f ? 0f : lockedFor + 3f;
        if (lockedFor >= 45f && lockedFor % 15f < 3f) Write($"WARN controls locked for {lockedFor:F0}s (dialogue busy={(d != null && d.Busy)})");
        if (fps < 20f && !TitleScreen.Showing) Write($"WARN low fps {fps:F0} at {pos}");
    }

    static void Write(string s)
    {
        try { File.AppendAllText(LogPath, $"[{System.DateTime.Now:HH:mm:ss}] {s}\n"); } catch { }
    }
}
