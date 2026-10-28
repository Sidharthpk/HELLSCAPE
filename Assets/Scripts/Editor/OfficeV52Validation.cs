using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Deterministic component integration checks in the actual scene. Does not simulate user input or assert art quality.
[InitializeOnLoad]
public static class OfficeV52Validation
{
    const string Pending = "HellScape.OfficeV52Tests";
    const string Report = "Library/OfficeV52Validation.txt";
    static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    static readonly StringBuilder log = new StringBuilder();
    static double waitUntil;
    static OfficeV52Sequence seq;
    static bool running;
    static readonly List<string> runtimeErrors = new List<string>();
    static OfficeV52Validation()
    {
        EditorApplication.update += PollRequest;
        EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
            {
                SessionState.EraseBool(Pending);
                running = true; log.Clear(); runtimeErrors.Clear(); stack.Clear(); stack.Push(Checks());
                Application.logMessageReceived += Heard;
                EditorApplication.update += Tick;
            }
        };
    }
    static void Heard(string message, string trace, LogType type)
    {
        if ((type == LogType.Exception || type == LogType.Error) && trace.Contains("Assets/Scripts"))
            runtimeErrors.Add(message);
    }
    static void PollRequest()
    {
        const string request = "Library/OfficeV52Validation.request";
        if (running || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(request)) return;
        string mode = File.ReadAllText(request).Trim();
        File.Delete(request);
        if (mode == "repair")
        {
            var s = RevisedPlot.All<OfficeV52Sequence>(SceneManager.GetActiveScene()).First();
            Undo.RecordObject(s.confrontationArea, "Office V5.2 confrontation visibility");
            Undo.RecordObject(s.confrontationArea.transform, "Office V5.2 confrontation visibility");
            s.confrontationArea.transform.position = new Vector3(998f,4.5f,895f);
            s.confrontationArea.size = new Vector3(4f,3f,3f);
            var exit = s.transform.parent.Find("ExitDoor").GetComponent<Interactable>();
            var gate = exit.gameObject.GetOrAdd<OfficeProgressGate>();
            gate.sequence = s; gate.rule = OfficeProgressGate.Rule.Street;
            gate.unavailable = "Vane is between me and the street. I have to take the silo route.";
            if (s.legacyEncounter.damned != null && s.legacyEncounter.damnedAnim != null)
            {
                Undo.RecordObject(s.legacyEncounter.damned, "Office V5.2 active form animator");
                s.legacyEncounter.damned.animator = s.legacyEncounter.damnedAnim;
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(s.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s.gameObject.scene);
            File.WriteAllText("Library/OfficeV52LayoutRepair.txt", "Confrontation area updated.");
            return;
        }
        if (mode == "alternate") SessionState.SetBool("OfficeV52.AlternateOrder", true);
        Run();
    }
    [MenuItem("HellScape/Tests/Office V5.2 integration")]
    public static void Run()
    {
        if (SceneManager.GetActiveScene().name != "OfficeSilo" || EditorApplication.isPlaying)
            throw new InvalidOperationException("Open OfficeSilo in Edit Mode first.");
        File.WriteAllText(Report, "RUNNING\n");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    static void Check(bool pass, string name)
    {
        log.AppendLine((pass ? "PASS " : "FAIL ") + name);
        File.WriteAllText(Report, log.ToString());
    }
    static IEnumerator Until(Func<bool> condition, float seconds, string name)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (!condition() && EditorApplication.timeSinceStartup < end) yield return null;
        Check(condition(), name);
        if (!condition()) throw new InvalidOperationException("Timed out: " + name);
    }
    static void Warp(Vector3 p)
    {
        seq.player.position = p;
        if (seq.player.TryGetComponent<Rigidbody>(out var rb)) { rb.position = p; rb.linearVelocity = Vector3.zero; }
        Physics.SyncTransforms();
    }
    static T One<T>() where T : Component => UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    static IEnumerator Checks()
    {
        bool cctvOnly = SessionState.GetBool("OfficeV52.CctvOnly",false);
        SessionState.EraseBool("OfficeV52.CctvOnly");
        yield return 1f;
        // Enter via the production arrival flow, rather than the debug wake sequence.
        SleepSequence.WakeInHell = true;
        Teleporter.ArriveAt = "OfficeArrival"; Teleporter.ArriveCheckpoint = "Checkpoint - Office entrance";
        SceneManager.LoadScene("OfficeSilo");
        yield return 4f;
        seq = OfficeV52Sequence.Instance;
        Check(seq != null, "Scene sequence present");
        if (seq == null) yield break;
        DialogueBox.AutoAdvanceAfter = .05f; DialogueBox.Instance.charDelay = 0f;
        DialogueBox.Instance.Clear();
        Check(seq.cctv.feeds.Length == 7 && seq.cctv.feeds.All(c => c != null), "Seven existing CCTV feeds retained");
        Check(seq.officeDoor != null && seq.concealedDoor != null && seq.locker != null && seq.keys != null
            && seq.pistol != null && seq.silo != null && seq.armoryCheckpoint != null, "Required scene references assigned");
        var hunt = VaneHunt.Instance;
        Warp(new Vector3(982f, 4.25f, 892f));
        hunt.RefreshOfficeRoute();
        yield return 2f;
        Check(seq.Opening != null && !hunt.Actor.gameObject.activeSelf, "Vane stays hidden before the electrical-room discovery");
        Check(seq.Objective.Contains("ELECTRICAL ROOM") && !seq.Objective.Contains("MISSING FUSES"), "Opening objective asks for electricity before revealing fuses");
        yield return Until(() => seq.Opening.ControlsShown, 15f, "Palm controls appear as HUD instructions");
        ScreenCapture.CaptureScreenshot("Library/OfficePalmControls.png");
        yield return Until(() => Truth.Has("OfficeOpening.Arrived"), 15f, "Palm controls precede electricity dialogue");
        yield return Until(() => !DialogueBox.Instance.Busy, 10f, "Electricity thought finishes without a talking tutorial");
        Check(!DialogueBox.Instance.CardOpen, "Opening has no soul-conversation card");
        var earlyGhost=RevisedPlot.All<OfficeWitness>(seq.gameObject.scene).First().GetComponent<GhostNPC>();
        RevisedPlotDirector.Instance.SetEcho(true);
        earlyGhost.Talk();
        yield return Until(() => !GhostNPC.Conversing && !DialogueBox.Instance.Busy, 10f, "Optional early worker conversation completes");
        Check(!Truth.Has("Freed"+earlyGhost.ghostName), "Early conversation cannot release a worker before CCTV");
        RevisedPlotDirector.Instance.SetEcho(false);
        var breaker=One<FuseBox>();
        var breakerUse=breaker.GetComponentsInChildren<Interactable>(true).First(i=>i.requiredItem=="Fuse");
        Inventory.Add("SecurityPass");seq.cctv.Watch();
        Check(!seq.cctv.Rolling, "Security pass cannot bypass the missing electrical power");Inventory.Take("SecurityPass");
        DialogueBox.Instance.Clear();
        Warp(new Vector3(1005f,4.25f,903f));
        breakerUse.Use();
        Check(seq.Opening.FusesDiscovered && seq.Objective.Contains("FUSES") && breaker.counter.Count==0,
            "Inspecting the breaker reveals missing fuses without consuming or inserting one");
        yield return Until(() => !DialogueBox.Instance.Busy, 10f, "Missing-fuse discovery finishes");
        Warp(new Vector3(1000.5f,4.25f,898.8f));
        yield return Until(() => seq.Opening.AwaitingFlash, 35f, "Leaving electrical room triggers Vane run-up, accusation and hand discovery");
        yield return .2f;
        ScreenCapture.CaptureScreenshot("Library/OfficeVaneFirstConfrontation.png");
        yield return .2f;
        Check(hunt.OpeningApproached && hunt.Actor.gameObject.activeSelf && hunt.SequenceHeld && !seq.Opening.Cleared,
            "Vane stops for the first playable palm flash");
        var view=seq.player.GetComponentInChildren<Camera>();
        view.transform.rotation=Quaternion.LookRotation(hunt.Actor.position+Vector3.up*1.5f-view.transform.position);
        var palm=seq.player.GetComponentInChildren<PalmLight>(true);
        typeof(PalmLight).GetMethod("Stun",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(palm,null);
        yield return Until(() => seq.Opening.Cleared && !seq.Opening.Busy, 5f, "Actual PalmLight hit releases the confrontation and starts roaming");
        Check(hunt.Report.StartsWith("Blind") && !hunt.SequenceHeld, "First flash blinds Vane through his existing AI");
        yield return Until(() => !DialogueBox.Instance.Busy, 10f, "Post-flash reaction finishes");
        var lockerKey=RevisedPlot.All<Interactable>(seq.gameObject.scene).First(i=>i.giveItem=="LockerKey");
        lockerKey.Use();
        var fuseLocker=RevisedPlot.All<Interactable>(seq.gameObject.scene).First(i=>i.requiredItem=="LockerKey");
        fuseLocker.Use();
        Check(breaker.GetComponentsInChildren<Interactable>(true).First(i=>i.name=="FusePickup 3").Available,
            "Existing boardroom key opens Tomas's locker and reveals the third fuse");
        foreach(var fuse in RevisedPlot.All<Interactable>(seq.gameObject.scene).Where(i=>i.giveItem=="Fuse")) fuse.Use();
        breakerUse.Use();
        Check(Inventory.Has("Fuse") && breaker.counter.Count==0, "Burnt breaker preserves collected fuses until entering Echo");
        RevisedPlotDirector.Instance.SetEcho(true);
        for(int i=0;i<3;i++) {breakerUse.Use();yield return 1.1f;}
        yield return Until(() => breaker.counter.Count==3, 15f, "All three fuse insertion animations complete");
        RevisedPlotDirector.Instance.SetEcho(false);
        Check(seq.Opening.Powered && breaker.counter.Count==3 && !Inventory.Has("Fuse"), "Three collected fuses install through the real breaker and restore power");
        Check(seq.Objective=="WATCH THE SECURITY RECORDINGS", "Security objective appears after restoring power");
        DialogueBox.Instance.Clear();
        Warp(new Vector3(982f,4.25f,892f));
        hunt.PlaceNear(10f);
        Check(hunt.Actor != null && hunt.Actor.gameObject.activeSelf && !hunt.SequenceHeld && !hunt.OfficeForm
            && !hunt.Report.StartsWith("Away"), "Original human Vane owns pre-CCTV patrol");
        var film = seq.cctv.fireTape;
        Check(film != null && film.actors != null && film.cam != null && film.GetComponent<FireTape>().mercer != null,
            "Existing filmed fire recording assigned");
        Check(seq.cctv.recoveredRecordings.Length == 3
            && seq.cctv.recoveredRecordings[0].GetComponent<TomasTape>() != null
            && seq.cctv.recoveredRecordings[1].GetComponent<OfficeEntryTape>() != null
            && seq.cctv.recoveredRecordings[2] == film, "Three recovered recordings have requested order");
        seq.cctv.Watch();
        Check(!seq.cctv.Rolling && !Truth.Has("OfficeV52.CCTV"), "CCTV still requires existing security pass");
        DialogueBox.Instance.Clear();
        Warp(new Vector3(999.5f,4.25f,906.4f));
        Inventory.Add("SecurityPass");
        seq.cctv.Watch();
        var siloTape = seq.cctv.recoveredRecordings[0].GetComponent<TomasTape>();
        var officeEntry = seq.cctv.recoveredRecordings[1].GetComponent<OfficeEntryTape>();
        yield return Until(() => siloTape.ShotFired, 60f, "First recording: Mercer shoots Tomas");
        ScreenCapture.CaptureScreenshot("Library/OfficeCctvSiloShot.png");
        yield return Until(() => siloTape.derekWalker.activeSelf, 30f, "First recording: Mercer starts stair exit");
        yield return 1f;
        ScreenCapture.CaptureScreenshot("Library/OfficeCctvStairExit.png");
        yield return Until(() => seq.cctv.CurrentRecording == 1, 40f, "Second recording follows completed silo recording");
        Check(siloTape.ExitCompleted && siloTape.StairHeightGain > .5f, "Mercer visibly climbs the existing silo stairs before the cut");
        Check(CutsceneHUD.Hidden && !seq.player.GetComponent<FirstPersonController>().enabled, "CCTV playlist keeps gameplay HUD and controls held across recordings");
        Check(!Truth.Has("OfficeV52.CCTV") && !hunt.SequenceHeld, "Red encounter does not begin after first recording");
        yield return Until(() => officeEntry.EnteredOffice, 30f, "Second recording: Mercer crosses Kessler office doorway");
        var entryMaterials = officeEntry.GetComponent<StabCutscene>().actors.GetComponentsInChildren<Renderer>(true)
            .SelectMany(r => r.sharedMaterials).ToArray();
        Check(entryMaterials.All(m => m != null && !AssetDatabase.GetAssetPath(m).Contains("/Burnt/")
            && !m.name.ToLowerInvariant().Contains("burnt")), "Office-entry recording has no burnt materials, including furniture and doorway");
        Check(new[] { "Clean previous office wall", "Clean previous office carpet", "Clean ceiling" }
            .All(n => entryMaterials.Any(m => m != null && m.name == n)), "Office-entry walls, carpet and ceiling use pre-fire materials");
        ScreenCapture.CaptureScreenshot("Library/OfficeCctvKesslerEntry.png");
        yield return Until(() => seq.cctv.CurrentRecording == 2, 20f, "Existing fire recording plays third");
        var vaneRoom = film.actors.transform.Find("Vane office recording geometry");
        Check(vaneRoom != null && vaneRoom.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.name == "Vane desk | Cube.002")
            && vaneRoom.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.name == "Vane executive chair | chair")
            && film.GetComponent<FireTape>().door.name == "Vane office entrance (recording only)", "Vane execution uses his authored office and entrance");
        yield return 2f;
        ScreenCapture.CaptureScreenshot("Library/OfficeCctvVaneDeath.png");
        Check(officeEntry.WalkFinished && Truth.Has("SawMercerEnterKesslerOffice"), "Kessler office entry recording finishes and records evidence");
        Check(seq.cctv.recoveredRecordings[1].recordingStamp.Contains("KESSLER OFFICE"), "Office entry camera retains Kessler identity after legacy migration");
        Check(!Truth.Has("OfficeV52.CCTV") && !hunt.SequenceHeld, "Red encounter waits for the third recording");
        yield return Until(() => Truth.Has("OfficeV52.CCTV"), 60f, "All three recovered recordings complete");
        seq.cctv.Watch(); Check(!seq.cctv.Rolling, "Required CCTV playback is one-time");
        Check(!CutsceneHUD.Hidden, "CCTV playlist restores gameplay HUD");
        if (cctvOnly)
        {
            Check(runtimeErrors.Count == 0, "No game-script errors during CCTV review: " + string.Join("; ",runtimeErrors));
            log.AppendLine("CCTV-only review. Full office flow results are saved separately in docs/validation/OfficeV52PlayMode.txt.");
            yield break;
        }
        yield return Until(() => !seq.InSequence && hunt.SequenceHeld && seq.blockedExit.enabled, 15f, "Red encounter suspends patrol");
        Vector3 east = new Vector3(1002.7f, 4.5f, 906.4f);
        Check(Physics.Raycast(east, Vector3.right, out var block, 2f, ~0, QueryTriggerInteraction.Ignore)
            && block.collider == seq.blockedExit, "East exit physically blocked");
        Check(!Physics.CapsuleCast(new Vector3(992.4f,3.8f,906.4f),new Vector3(992.4f,5f,906.4f),
            .25f,Vector3.left,2.8f,~0,QueryTriggerInteraction.Ignore), "West alternate doorway remains passable");
        Warp(seq.alternateExit.transform.position);
        yield return Until(() => Truth.Has("OfficeV52.RedEncounter"), 5f, "Alternate exit completes encounter");
        Check(!hunt.SequenceHeld && !seq.blockedExit.enabled, "Vane resumes original AI after alternate exit");
        // Prevent unrelated desk scares during deterministic conversation checks.
        One<OfficeColleagues>().enabled = false;
        hunt.HoldForSequence(null); // isolate worker dialogue order from the separately verified roaming threat
        var witnesses = RevisedPlot.All<OfficeWitness>(seq.gameObject.scene);
        foreach (string who in SessionState.GetBool("OfficeV52.AlternateOrder", false)
            ? new[] { "Ben", "Nadia", "Priya", "Omar" } : new[] { "Omar", "Priya", "Nadia", "Ben" })
        {
            var w = witnesses.First(w => w.GetComponent<GhostNPC>().ghostName == who);
            w.gameObject.SetActive(true); DialogueBox.Instance.Clear();
            w.RedTalk();
            yield return Until(() => Truth.Has("Freed" + who), 15f, who + " releases after truth dialogue");
            Check(w.State == OfficeWitness.InvestigationState.Released && !w.gameObject.activeSelf, who + " release persists once");
        }
        Check(Truth.AllFreed, "CCTV truth releases all four in non-default order without mandatory personal clues");
        hunt.ReleaseSequence(false);
        Warp(seq.confrontationArea.transform.position);
        yield return Until(() => Truth.Has("OfficeV52.Transformed") && !seq.InSequence, 25f, "Fourth release enables transformation at authored area");
        Check(hunt.OfficeForm && hunt.Actor == seq.legacyEncounter.damned.transform && !seq.legacyEncounter.Armed,
            "Cry Wolf reuses existing actor without competing legacy AI");
        Check(seq.legacyEncounter.damned.animator == seq.legacyEncounter.damnedAnim
            && seq.legacyEncounter.damnedAnim.isActiveAndEnabled, "Active Cry Wolf animator used by office and tunnel");
        seq.officeDoor.ApplyRealm(false); seq.officeDoor.Unlock();
        Check(seq.officeDoor.blocker.enabled, "Kessler entrance remains shut in Red City");
        RevisedPlotDirector.Instance.SetEcho(true); seq.officeDoor.Unlock();
        yield return 1.6f;
        Check(!seq.officeDoor.blocker.enabled, "Kessler entrance opens in Echo");
        Warp(new Vector3(1005.2f,4.25f,887.8f));
        yield return .3f;
        Check(Truth.Has("OfficeV52.OfficeAccessed"), "Office interior records entry");
        One<EchoVisit>().AdvanceClock(20f);
        yield return 3.5f;
        Check(!RevisedPlotDirector.Instance.Echo && OfficeV52Sequence.Contains(seq.officeInterior, seq.player.position),
            "Echo timeout returns inside office without ejecting player");
        Check(seq.Objective.Contains("CROWBAR") && seq.Objective.Contains("PISTOL"), "Transformation gives pistol goal and cleaner-closet crowbar objective");
        var crowbar=RevisedPlot.All<Interactable>(seq.gameObject.scene).First(i=>i.giveItem=="Crowbar");
        crowbar.Use(); seq.DiscoverCombination();
        yield return Until(() => !DialogueBox.Instance.Busy, 10f, "Crowbar pickup dialogue completes before prying");
        Warp(new Vector3(1007.2f,4.25f,884.9f));
        var pryCamera=seq.player.GetComponentInChildren<Camera>();
        pryCamera.transform.rotation=Quaternion.LookRotation(seq.concealedDoor.transform.position-pryCamera.transform.position);
        seq.concealedDoor.Use();
        Check(!seq.TimerRunning, "Breach timer does not start at pry interaction");
        yield return .5f;
        Check(seq.Opening.PryVisible && seq.Opening.PryProgress>0f && GameObject.Find("Crowbar levering concealed door")!=null,
            "Crowbar mesh and reaching hands animate while the concealed door is pried");
        ScreenCapture.CaptureScreenshot("Library/OfficeCrowbarPry.png");
        yield return Until(() => seq.TimerRunning, 8f, "Breach timer starts after opening completes");
        Check(Checkpoint.Current == seq.armoryCheckpoint, "Armory checkpoint activated");
        seq.locker.Use(); float remaining = seq.Remaining;
        yield return .6f;
        Check(VaultDial.Active && seq.Remaining < remaining, "Locker UI does not pause breach timer");
        seq.locker.Dial(18,-1);seq.locker.Dial(7,1);seq.locker.Dial(31,-1);
        Check(!seq.locker.Solved, "Correct numbers with wrong directions rejected");
        seq.locker.Cancel();
        One<PauseMenu>().Pause(); remaining = seq.Remaining;
        yield return .6f;
        Check(Mathf.Approximately(seq.Remaining,remaining), "Pause menu pauses breach timer");
        One<PauseMenu>().Resume();
        typeof(RevisedPlotDirector).GetMethod("ShowJournal",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
            .Invoke(RevisedPlotDirector.Instance,null);
        remaining = seq.Remaining;
        yield return .6f;
        Check(ChoiceDialogue.Instance.Open && seq.Remaining < remaining
            && EvidenceJournal.Account("Executive storage combination").Contains("L18"), "Existing journal is available while breach timer continues");
        ChoiceDialogue.Instance.Close();
        // Death and actual checkpoint respawn before solving: timer restarts and UI/control locks clear.
        var hp = seq.player.GetComponent<PlayerHealth>(); hp.TakeDamage(hp.maxHealth * 2f);
        yield return .4f;
        One<GameOverScreen>().Restart();
        yield return 3f;
        Check(!hp.IsDead && Checkpoint.Current == seq.armoryCheckpoint && seq.TimerRunning && seq.Remaining > 50f
            && !VaultDial.Active, "Death restores armory checkpoint and running encounter");
        Check(Truth.AllFreed && Truth.Has("OfficeV52.Transformed"), "Retry preserves worker liberation and transformation");
        seq.AdvanceBreach(100f);
        Check(seq.Breached && !seq.TimerRunning && !seq.pistol.gameObject.activeInHierarchy
            && seq.locker.gameObject.activeSelf, "Timeout breaches door; unsolved locker remains available");
        seq.locker.Use();
        seq.locker.Dial(18,1);seq.locker.Dial(7,-1);seq.locker.Dial(31,1);
        Check(seq.locker.Solved && seq.lockerContents.activeSelf && !Inventory.Has("SiloKey")
            && !GunController.Equipped, "L18 R07 L31 opens only locker; items require collection");
        seq.pistol.Use(); seq.keys.Use();
        Check(GunController.Equipped && Inventory.Has("SiloKey") && Truth.Has("OfficeV52.ArmorySuccess"), "Existing gun equip and silo key pickup complete armory");
        int health = seq.legacyEncounter.damned.health;
        DialogueBox.Instance.Clear();
        Warp(hunt.Actor.position + Vector3.right * 2f + Vector3.up * .85f);
        var gun = One<GunController>();
        gun.cam.transform.rotation = Quaternion.LookRotation(hunt.Actor.position + Vector3.up * 1.2f - gun.cam.transform.position);
        typeof(GunController).GetMethod("Fire",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(gun,null);
        string first = hunt.Report;
        float staggerLeft = hunt.StaggerRemaining;
        typeof(GunController).GetMethod("Fire",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(gun,null);
        Check(seq.legacyEncounter.damned.health == health && first.StartsWith("Stagger") && hunt.Report.StartsWith("Stagger"),
            "Office bullets absorb damage and stagger, repeated hit does not defeat Vane");
        Check(Mathf.Approximately(hunt.StaggerRemaining, staggerLeft), "Immune repeated hit does not extend stagger");
        yield return Until(() => !hunt.Report.StartsWith("Stagger"), 8f, "Stagger recovers");
        Check(hunt.BulletImmune, "Stagger recovers during immunity window");
        seq.silo.GetComponent<Interactable>().Use();
        yield return Until(() => Truth.Has("OfficeV52.Completed"), 15f, "Existing silo transition completes office");
        Check(Checkpoint.Current != null && Checkpoint.Current.name == "Checkpoint - Kessler tunnel" && !seq.TimerRunning,
            "Normal tunnel checkpoint retained and office timer stopped");
        Check(!seq.legacyEncounter.damned.GetComponent<OfficeVaneHit>().Hit(), "Office damage override releases shared tunnel actor");
        Check(DialogueCsvCheck.Run().StartsWith("PASS"), "Existing dialogue CSV validation");
        Check(runtimeErrors.Count == 0, "No game-script runtime errors: " + string.Join("; ",runtimeErrors));
        log.AppendLine("NOT RUN: full manual input playthrough, later silo boss/chapters, final asset/animation review.");
    }
    static void Tick()
    {
        if (!running) return;
        if (!EditorApplication.isPlaying) { Finish("Stopped before completion"); return; }
        if (EditorApplication.timeSinceStartup < waitUntil) return;
        try
        {
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                waitUntil = EditorApplication.timeSinceStartup + (top.Current is float seconds ? seconds : 0);
                return;
            }
            Finish(null);
        }
        catch (Exception ex) { Finish(ex.ToString()); }
    }
    static void Finish(string error)
    {
        running = false; EditorApplication.update -= Tick;
        Application.logMessageReceived -= Heard;
        if (error != null) log.AppendLine("FAIL " + error);
        File.WriteAllText(Report, log.ToString());
        DialogueBox.AutoAdvanceAfter = 0f;
        Debug.Log("Office V5.2 validation:\n" + log);
        EditorApplication.isPlaying = false;
    }
}
