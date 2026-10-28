using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > Tests > Checkpoint retry. Plays the main scene, and at every checkpoint kills you and presses Retry,
// then checks you really are back: alive, moving, your own camera on your own head, no cutscene camera left on, the
// screen faded back in, time running. The Kessler tunnel is done properly (through the silo door), with the three
// ways a retry used to go wrong there: restarting in the middle of a door cutscene, dying in one, and dying while
// he beats on the silo door. Result: Library/CheckpointRetry.result (PASS/FAIL per case).
public static class CheckpointRetryTest
{
    const string Key = "HellScape.CheckpointRetry", Result = "Library/CheckpointRetry.result";
    static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    static readonly StringBuilder log = new StringBuilder();
    static readonly List<string> errors = new List<string>();
    static double waitUntil;

    static Transform player, cam, camParent;
    static Vector3 camHome;
    static PlayerHealth hp;
    static FirstPersonController fpc;
    static GameOverScreen over;

    [MenuItem("HellScape/Tests/Checkpoint retry")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
        if (SceneManager.GetActiveScene().name != "MainGameScene")
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/MainGameScene.unity");
        }
        System.IO.File.Delete(Result);
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
            stack.Clear(); log.Clear(); errors.Clear(); waitUntil = 0;
            stack.Push(Main());
            Application.logMessageReceived += Heard;
            EditorApplication.update += Tick;
        };
    }

    static void Heard(string text, string trace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error) errors.Add(text.Split('\n')[0]);
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Heard;
        System.IO.File.WriteAllText(Result, log.ToString());
        Debug.Log("Checkpoint retry:\n" + log);
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { log.AppendLine("FAIL: Play stopped before the end"); Finish(); return; }
        if (EditorApplication.timeSinceStartup < waitUntil) return;
        try
        {
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator inner) { stack.Push(inner); continue; }
                if (top.Current is float f) waitUntil = EditorApplication.timeSinceStartup + f;   // (real seconds: the game is frozen on the death screen)
                return;
            }
        }
        catch (System.Exception e) { log.AppendLine("FAIL: the test itself threw: " + e); }
        Finish();
    }

    static IEnumerator Until(System.Func<bool> done, float timeout, string what)
    {
        double end = EditorApplication.timeSinceStartup + timeout;
        while (!done())
        {
            if (EditorApplication.timeSinceStartup > end) { log.AppendLine("  (timed out waiting for " + what + ")"); yield break; }
            yield return null;
        }
    }

    static T One<T>(string nameHas = null) where T : Object =>
        Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(o => nameHas == null || o.name.Contains(nameHas));

    static IEnumerator Main()
    {
        // past the title: the scene again, waking in hell (as QuickStart does)
        yield return 1f;
        if (TitleScreen.Showing) { SleepSequence.WakeInHell = true; SceneManager.LoadScene("MainGameScene"); }
        yield return 1f;
        yield return Until(() => !TitleScreen.Showing && (fpc = One<FirstPersonController>()) != null && fpc.enabled && Time.timeSinceLevelLoad > 2f, 40f, "the game to start");
        if (fpc == null) { log.AppendLine("FAIL: no player"); yield break; }
        BindPlayer();
        foreach (var name in new[] { "Hell city", "Escape", "Bridge" })
        {
            var cp = One<Checkpoint>(name);
            if (cp == null) { log.AppendLine("FAIL " + name + ": no such checkpoint"); continue; }
            cp.Activate();
            Teleport(cp.spawn);
            yield return 1.5f;
            yield return DieAndRetry(name + ": died", cp);
        }
        // Office checkpoints and their cutscenes belong to OfficeSilo after the scene split.
        Teleporter.ArriveAt = "OfficeArrival";
        Teleporter.ArriveCheckpoint = "Checkpoint - Office entrance";
        SleepSequence.WakeInHell = true;
        SceneManager.LoadScene("OfficeSilo");
        yield return 3f;
        BindPlayer();
        var chase = One<KesslerChase>();
        foreach (var name in new[] { "Office entrance", "Kessler tunnel", "Silo complex", "Lower bridge", "Silo floor" })
        {
            var cp = One<Checkpoint>(name);
            if (cp == null) { log.AppendLine("FAIL " + name + ": no such checkpoint"); continue; }
            if (name == "Kessler tunnel" && chase != null) { yield return Tunnel(cp, chase); continue; }
            cp.Activate(); Teleport(cp.spawn);
            yield return 1.5f;
            yield return DieAndRetry(name + ": died", cp);
        }
    }

    static void BindPlayer()
    {
        fpc = One<FirstPersonController>();
        player = fpc.transform; hp = player.GetComponent<PlayerHealth>(); over = One<GameOverScreen>();
        cam = player.GetComponentInChildren<Camera>(true).transform;
        camParent = cam.parent; camHome = cam.localPosition;
    }

    static IEnumerator Tunnel(Checkpoint cp, KesslerChase chase)
    {
        Inventory.Add("SiloKey");
        var door = One<Teleporter>("SiloDoor");
        if (door == null) { log.AppendLine("FAIL Kessler tunnel: no SiloDoor teleporter"); yield break; }
        door.Go();
        yield return Until(() => Checkpoint.Current == cp, 20f, "arriving in the tunnel");

        yield return Until(() => chase.cuts.Playing, 10f, "the door cutscene");
        yield return 1f;
        var pause = One<PauseMenu>();
        if (pause != null) pause.Restart(); else Checkpoint.RespawnCurrent();
        yield return 3f;
        Verify("Kessler tunnel: restart from the pause menu in the middle of the door cutscene", cp);

        yield return 2f;
        chase.DoorBurst();
        yield return Until(() => chase.cuts.Playing, 5f, "the cutscene of him coming through");
        yield return 1f;
        yield return DieAndRetry("Kessler tunnel: died in the middle of the cutscene of him coming through", cp);

        yield return 2f;
        chase.DoorBurst();
        yield return Until(() => chase.Chasing, 8f, "the chase");
        yield return 1f;
        yield return DieAndRetry("Kessler tunnel: died in the chase", cp);

        // inside the silo block, him beating on the door behind you (it shakes your head with every blow), and you die there
        yield return 2f;
        chase.DoorBurst();
        yield return Until(() => chase.Chasing, 8f, "the chase");
        var complex = One<Checkpoint>("Silo complex");
        Teleport(complex.spawn);
        chase.EnterSilo();
        complex.Activate();
        yield return 3f;
        yield return DieAndRetry("Silo complex: died while Kessler beats on the door", complex, 7f);
    }

    static void Teleport(Transform to)
    {
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;
        player.SetPositionAndRotation(to.position, to.rotation);
        Physics.SyncTransforms();
    }

    static IEnumerator DieAndRetry(string what, Checkpoint cp, float dead = 5.5f)
    {
        errors.Clear();
        hp.TakeDamage(1e6f);
        yield return dead;
        if (!GameOverScreen.Open) log.AppendLine("  (" + what + ": the death screen never came up)");
        over.Restart();
        yield return 3f;
        Verify(what, cp);
    }

    static void Verify(string what, Checkpoint cp)
    {
        var bad = new List<string>();
        var eye = cam.GetComponent<Camera>();
        var car = One<CarInteract>();
        bool driving = car != null && car.InCar;   // (the bridge boss puts you back behind the wheel: the van's camera is yours then)
        if (GameOverScreen.Open) bad.Add("the death screen is still up");
        if (Time.timeScale != 1f) bad.Add("time scale " + Time.timeScale);
        if (hp.IsDead) bad.Add("still dead");
        if (driving)
        {
            if (!Camera.allCameras.Any(c => c.targetTexture == null)) bad.Add("no camera is on");
        }
        else if (!player.gameObject.activeInHierarchy) bad.Add("the player is switched off");
        else if (!fpc.enabled) bad.Add("movement is off");
        else if (cam.parent != camParent) bad.Add("the camera is off your head (parent " + (cam.parent ? cam.parent.name : "none") + ")");
        else if (Vector3.Distance(cam.localPosition, camHome) > 0.12f) bad.Add("the camera sits " + Vector3.Distance(cam.localPosition, camHome).ToString("F2") + " m from where your eyes are");
        if (!driving && !eye.isActiveAndEnabled) bad.Add("your camera is off");
        foreach (var c in Camera.allCameras)
            if (!driving && c != eye && c.targetTexture == null && c.depth >= eye.depth && c.rect.width > 0.9f) bad.Add("camera '" + c.name + "' is drawn over yours");
        foreach (var k in Object.FindObjectsByType<KillerCutscenes>(FindObjectsSortMode.None)) if (k.Playing) bad.Add("a cutscene still counts as playing");
        var fader = cp.fader != null ? cp.fader : One<ScreenFader>();
        if (fader != null && fader.fade.alpha > 0.1f) bad.Add("the screen is still black (" + fader.fade.alpha.ToString("F2") + ")");
        if (CutsceneHUD.Hidden) bad.Add("the HUD is still hidden");
        if (Checkpoint.Current == cp && Vector3.Distance(player.position, cp.spawn.position) > 8f) bad.Add("you are " + Vector3.Distance(player.position, cp.spawn.position).ToString("F0") + " m from the checkpoint");
        foreach (var e in errors.Distinct().Take(3)) bad.Add("error: " + e);
        errors.Clear();
        log.AppendLine((bad.Count == 0 ? "PASS " : "FAIL ") + what + (bad.Count == 0 ? "" : ": " + string.Join("; ", bad)));
    }
}
