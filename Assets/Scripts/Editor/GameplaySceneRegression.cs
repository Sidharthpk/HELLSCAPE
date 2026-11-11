using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Exercises actual scene doors, state carried through them, and retries in both gameplay scenes.
// Batch: Unity -batchmode -projectPath <project> -executeMethod GameplaySceneRegression.RunBatch
[InitializeOnLoad]
public static class GameplaySceneRegression
{
    const string Key = "HellScape.GameplaySceneRegression", Result = "Library/GameplaySceneRegression.result";
    static readonly List<string> results = new List<string>();
    static readonly Stack<IEnumerator> work = new Stack<IEnumerator>();
    static double deadline;
    static bool failed;

    static GameplaySceneRegression()
    {
        EditorApplication.update += () =>
        {
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Library/CheckpointGameplaySuite.request"))
            {
                File.Delete("Library/CheckpointGameplaySuite.request");
                CheckpointRetryTest.Run();
                return;
            }
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Library/OfficeGameplaySuite.request"))
            {
                File.Delete("Library/OfficeGameplaySuite.request");
                StoryRegression.Run();
                return;
            }
            const string request = "Library/GameplaySceneRegression.request";
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
            File.Delete(request);
            RunBatch();
        };
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            {
                results.Clear(); work.Clear(); failed = false;
                deadline = EditorApplication.timeSinceStartup + 360;
                work.Push(Checks());
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Step;
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key, false))
            {
                SessionState.EraseBool(Key);
                if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
                else EditorSceneManager.OpenScene("Assets/Scenes/MainGameScene.unity");
            }
        };
    }

    [MenuItem("HellScape/Tests/Three scene gameplay")]
    public static void RunBatch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity");
        File.WriteAllText(Result, "RUNNING\n");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Exception) { failed = true; results.Add("EXCEPTION: " + message + "\n" + trace); }
    }
    static void Check(bool ok, string what)
    {
        results.Add((ok ? "PASS: " : "FAIL: ") + what);
        if (!ok) failed = true;
        File.WriteAllText(Result, string.Join("\n", results));
    }
    static void Step()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Three-scene regression timed out");
            if (work.Count > 0)
            {
                var top = work.Peek();
                if (!top.MoveNext()) work.Pop();
                else if (top.Current is IEnumerator inner) work.Push(inner);
                return;
            }
        }
        catch (Exception error) { failed = true; results.Add("FAIL: " + error); }
        EditorApplication.update -= Step;
        Application.logMessageReceived -= OnLog;
        results.Add(failed ? "FAIL: Three scene gameplay" : "PASS: Three scene gameplay");
        File.WriteAllText(Result, string.Join("\n", results));
        EditorApplication.isPlaying = false;
    }

    static T One<T>() where T : Object => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    static Teleporter Door(string name) => Object.FindObjectsByType<Teleporter>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == name);
    static FirstPersonController Player => One<FirstPersonController>();
    static IEnumerator Wait(float seconds)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end) yield return null;
    }
    static IEnumerator Loaded(string scene, string arrival)
    {
        double end = EditorApplication.timeSinceStartup + 45;
        while ((SceneManager.GetActiveScene().name != scene || Teleporter.ArriveAt != null) && EditorApplication.timeSinceStartup < end) yield return null;
        yield return Wait(1.5f);
        Check(SceneManager.GetActiveScene().name == scene, "Loaded " + scene);
        var at = GameObject.Find(arrival);
        Check(at != null && Vector3.Distance(Player.transform.position, at.transform.position) < 4f, "Arrived at " + arrival);
        Check(Player.enabled && Player.playerCanMove && Player.cameraCanMove, scene + " movement and look restored");
        Check(!TitleScreen.Showing && !PauseMenu.Paused && Time.timeScale == 1f, scene + " has live gameplay, no title or pause");
        var fader = One<ScreenFader>();
        Check(fader == null || fader.fade.alpha < .01f, scene + " door fade cleared");
        One<DialogueBox>()?.Clear();
    }
    static void Travel(string name)
    {
        var door = Door(name);
        // Use the authored transition and loading path, with only cinematic waits shortened for the test.
        door.transition = null; door.fadeTime = .05f;
        for (var t = door.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
        door.Go();
    }
    static IEnumerator Retry(Checkpoint checkpoint)
    {
        Check(checkpoint != null, "Retry checkpoint exists");
        checkpoint.Activate();
        var hp = Player.GetComponent<PlayerHealth>();
        hp.TakeDamage(100000f);
        yield return Wait(6f);
        Check(GameOverScreen.Open, checkpoint.title + " death screen opens");
        One<GameOverScreen>().Restart();
        yield return Wait(2f);
        Check(!hp.IsDead && hp.currentHealth > 0f && Player.enabled && Time.timeScale == 1f && !GameOverScreen.Open,
            checkpoint.title + " retry restores life and controls");
        Check(Player.GetComponentInChildren<Camera>().enabled && One<ScreenFader>().fade.alpha < .01f,
            checkpoint.title + " retry restores camera and clears fade");
    }
    static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    static IEnumerator Checks()
    {
        yield return Wait(2f);
        var prologue = One<PrologueDirector>();
        Check(prologue != null && prologue.nextScene == "MainGameScene", "Prologue leads to MainGameScene");
        Check(One<DeliveryRun>() != null && Player != null && One<DialogueBox>() != null, "Prologue delivery, player and dialogue wired");
        Check(new[] { "Prologue", "MainGameScene", "OfficeSilo" }.All(Application.CanStreamedLevelBeLoaded), "All three scenes enabled in build");
        // Start in the red city at a door marker, skipping only the long prologue/wake cinematics.
        SleepSequence.WakeInHell = true;
        Teleporter.ArriveAt = "CityReturnPoint";
        SceneManager.LoadScene("MainGameScene");
        yield return Loaded("MainGameScene", "CityReturnPoint");
        EvidenceJournal.ContainerInspected = true;
        EvidenceJournal.Record("transition-test", "This account must survive the doors.");
        Judgement.Karma = 3;
        Inventory.Add("TravelToken");
        Travel("OfficeDoor");
        yield return Loaded("OfficeSilo", "OfficeArrival");
        Check(Inventory.Has("TravelToken") && Judgement.Karma == 3 && EvidenceJournal.Has("transition-test") && EvidenceJournal.ContainerInspected,
            "Inventory, judgement and prologue evidence survive city to office");
        Check(Checkpoint.Current != null && Checkpoint.Current.name == "Checkpoint - Office entrance", "Office arrival activates its checkpoint");
        yield return Retry(Checkpoint.Current);

        var fuseBox = One<FuseBox>();
        var counter = fuseBox != null ? fuseBox.counter : null;
        Check(counter != null, "Office fuse puzzle exists");
        var pickup = RevisedPlot.All<Interactable>(SceneManager.GetActiveScene()).First(i => i.giveItem == "Fuse");
        pickup.Use();
        string insertedFuse = pickup.name;
        RevisedPlotDirector.Instance.SetEcho(true);
        var breaker = RevisedPlot.All<Interactable>(SceneManager.GetActiveScene()).First(i =>
            Enumerable.Range(0, i.onUse.GetPersistentEventCount()).Any(n => i.onUse.GetPersistentTarget(n) == fuseBox && i.onUse.GetPersistentMethodName(n) == "InsertNext"));
        breaker.Use();
        yield return Wait(1.5f);
        Check((int)Field(counter, "count") == 1, "A collected fuse inserts through the authored breaker interaction");
        Check(fuseBox.fuses.Any(f => f != null && f.gameObject.activeInHierarchy && Vector3.Distance(f.position, fuseBox.seats[0]) < .03f),
            "Inserted fuse model is visible in its authored slot");
        RevisedPlotDirector.Instance.SetEcho(false);
        One<DialogueBox>().Clear();
        pickup = RevisedPlot.All<Interactable>(SceneManager.GetActiveScene()).First(i => i.giveItem == "Fuse" && i.name != insertedFuse);
        pickup.Use();
        string pickupName = pickup.name;
        int fuseCount = (int)Field(counter, "count");
        var witness = RevisedPlot.All<GhostNPC>(SceneManager.GetActiveScene()).First(g => g.ghostName == "Ben");
        witness.fate = GhostNPC.Fate.Confessed;
        EvidenceJournal.Collect(EvidenceJournal.Tape);
        var gun = One<GunController>(); gun.Equip();
        Player.GetComponent<PlayerHealth>().TakeDamage(35);
        Travel("ExitDoor");
        yield return Loaded("MainGameScene", "CityReturnPoint");
        Check(GunController.Equipped && One<GunController>().viewModel.activeSelf, "Weapon remains equipped when leaving the office");
        Check(Inventory.Has("Fuse") && EvidenceJournal.Has(EvidenceJournal.Tape) && Judgement.Karma == 3, "Office items, evidence and choices survive return to city");
        Check(Player.GetComponent<PlayerHealth>().currentHealth < 90f, "A door does not refill player health");
        Check(!InteriorDarkness.Blackout, "Office blackout released in the city");
        Travel("OfficeDoor");
        yield return Loaded("OfficeSilo", "OfficeArrival");
        Check((int)Field(One<PuzzleCounter>(), "count") == fuseCount, "Partial fuse puzzle survives an office revisit");
        pickup = RevisedPlot.All<Interactable>(SceneManager.GetActiveScene()).First(i => i.name == pickupName && i.giveItem == "Fuse");
        Check(!pickup.Available, "Collected pickup cannot be collected again on a revisit");
        fuseBox = One<FuseBox>();
        Check(fuseBox.fuses.Any(f => f != null && f.gameObject.activeInHierarchy && Vector3.Distance(f.position, fuseBox.seats[0]) < .03f),
            "Inserted fuse stays visible in its slot after a revisit");
        Check(RevisedPlot.All<GhostNPC>(SceneManager.GetActiveScene()).First(g => g.ghostName == "Ben").fate == GhostNPC.Fate.Confessed,
            "Witness outcome survives an office revisit");
        Inventory.Add("CarKeys");
        Judgement.KilledKiller = true;
        EvidenceJournal.Record("bridge-test", "The bridge needs the office evidence.");
        Travel("SiloExit");
        yield return Loaded("MainGameScene", "CityReturnPoint");
        Check(Inventory.Has("CarKeys") && Inventory.Has("TravelToken"), "Silo keys survive the escape door");
        Check(Judgement.KilledKiller && Judgement.Karma == 3 && EvidenceJournal.Has("bridge-test") && EvidenceJournal.Has(EvidenceJournal.Tape),
            "Ending judgement and evidence survive the escape door");
        Check(GunController.Equipped && One<GunController>().viewModel.activeSelf, "Silo weapon available for drive-by and bridge fight");
        Check(Checkpoint.Current != null && Checkpoint.Current.name == "Checkpoint - Escape", "Silo return activates escape checkpoint");
        Check(One<CarInteract>().requiredItem == "CarKeys" && Inventory.Has(One<CarInteract>().requiredItem), "Returned player can unlock the van");
        Check(One<DriveBy>() != null && One<DemonBoss>() != null && One<HordeChase>() != null, "City escape, drive-by and bridge boss wired");
        var van = One<CarInteract>();
        typeof(CarInteract).GetMethod("EnterCar", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(van, null);
        yield return Wait(.2f);
        Check(van.InCar && van.carCamera.enabled && !van.playerRoot.activeSelf, "Van entry switches control and camera to the vehicle");
        Check(One<HordeChase>().Running && DemonBoss.Active != null, "Authored van callbacks start the horde and boss escape");
        yield return Retry(Checkpoint.Current);

        // Returning to title must reset the run, including the pending door and weapon.
        NewGame.Reset();
        Check(!Inventory.Has("CarKeys") && !EvidenceJournal.Has(EvidenceJournal.Tape) && Judgement.Karma == 0 && !GunController.Equipped && Teleporter.ArriveAt == null,
            "New game clears carried items, evidence, judgement, weapon and arrival state");
        SceneManager.LoadScene("MainGameScene");
        yield return Wait(2f);
        Check(TitleScreen.Showing, "Quit to title shows the title after travelling through all scenes");
    }
}
