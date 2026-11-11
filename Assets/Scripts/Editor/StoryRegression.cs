using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Runs against disposable Play-mode state; never saves or changes the authored scene.
[InitializeOnLoad]
public static class StoryRegression
{
    const string Request = "Library/StoryRegression.request", Result = "Library/StoryRegression.result", Key = "HellScape.StoryRegression";
    static readonly List<string> results = new List<string>();
    static double deadline;
    static readonly List<string> runtimeErrors = new List<string>();
    static void OnLog(string message, string stack, LogType type) { if (type == LogType.Exception) runtimeErrors.Add(message + "\n" + stack); }
    static StoryRegression()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            {
                runtimeErrors.Clear(); Application.logMessageReceived += OnLog;
                deadline = EditorApplication.timeSinceStartup + 180;
                EditorApplication.update += Watch;
                EditorApplication.delayCall += () =>
                {
                    var box = Object.FindFirstObjectByType<DialogueBox>(FindObjectsInactive.Include);
                    if (box == null) { Finish("FAIL: No dialogue component"); return; }
                    Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include).StartCoroutine(Guard(SessionState.GetBool("HellScape.EchoConversationOnly", false) ? EchoConversationChecks(box) : Checks(box)));
                };
            }
        };
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            Run();
        };
    }

    [MenuItem("HellScape/Tests/Story regression (Play mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "OfficeSilo")
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/OfficeSilo.unity");
        }
        Directory.CreateDirectory("Library/GameplayTestArtifacts/Story/office");
        Directory.CreateDirectory("Library/GameplayTestArtifacts/Story/echo");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    [MenuItem("HellScape/Tests/Echo conversation timer (Play mode)")]
    public static void RunEchoConversation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool("HellScape.EchoConversationOnly", true);
        Run();
    }

    static IEnumerator EchoConversationChecks(DialogueBox box)
    {
        yield return null;
        yield return null;
        var director = Object.FindFirstObjectByType<RevisedPlotDirector>();
        Check(director != null, "Story director initialized");
        director.enabled = false; // Keep this isolated check at its chosen realm.
        var visit = director.GetComponent<EchoVisit>();
        var choices = ChoiceDialogue.Instance;
        box.Clear(); choices.Close();
        foreach (var g in RevisedPlot.All<GhostNPC>(director.gameObject.scene)) g.CancelConversation();
        director.SetEcho(true); box.Clear();
        visit.AdvanceClock(2f);
        float remaining = visit.Remaining;
        Check(Mathf.Approximately(remaining, 4f), "Exploration consumes Echo time");
        var speaker = System.Array.Find(RevisedPlot.All<GhostNPC>(director.gameObject.scene), g => g.ghostName == "Tomas");
        Check(speaker != null, "Non-Vane conversation participant available");
        speaker.gameObject.SetActive(true);
        speaker.opening = "You: Tell me what happened.";
        speaker.Talk();
        yield return null;
        Check(GhostNPC.Conversing && box.Busy && box.PlayerSpeaking, "Conversation includes the player's spoken reply");
        visit.AdvanceClock(20f);
        Check(visit.HeldForConversation && Mathf.Approximately(visit.Remaining, remaining), "Player reply holds remaining Echo time");
        box.Clear();
        visit.AdvanceClock(20f);
        Check(Mathf.Approximately(visit.Remaining, remaining), "Gap between conversation lines holds Echo time");
        choices.Show("Tomas", "What do you want to know?", new[] { "Continue" }, _ => { });
        visit.AdvanceClock(20f);
        Check(Mathf.Approximately(visit.Remaining, remaining), "Dialogue choices hold Echo time");
        speaker.CancelConversation(); choices.Close();
        box.Say("Tomas: This is my final reply.");
        yield return null;
        visit.AdvanceClock(20f);
        Check(box.Busy && Mathf.Approximately(visit.Remaining, remaining), "Final NPC reply holds Echo time after conversation callback");
        box.Clear();
        visit.AdvanceClock(.5f);
        Check(Mathf.Approximately(visit.Remaining, remaining - .5f), "Countdown resumes from remaining time, without resetting");
        visit.AdvanceClock(20f);
        choices.Show("Tomas", "Still speaking at the deadline.", new[] { "Continue" }, _ => { });
        yield return null;
        Check(director.Echo && !visit.Returning, "Zero-time deadline cannot interrupt an open conversation");
        choices.Close();
        director.SetEcho(false);
    }

    static void Watch()
    {
        if (EditorApplication.timeSinceStartup > deadline) Finish("FAIL: Regression timed out");
    }
    static void Finish(string status)
    {
        Application.logMessageReceived -= OnLog;
        if (runtimeErrors.Count > 0) status = "FAIL: Runtime exceptions\n" + string.Join("\n", runtimeErrors);
        File.WriteAllText(Result, string.Join("\n", results) + "\n" + status);
        SessionState.SetBool(Key, false);
        SessionState.EraseBool("HellScape.EchoConversationOnly");
        EditorApplication.update -= Watch;
        EditorApplication.isPlaying = false;
    }
    static IEnumerator Guard(IEnumerator test)
    {
        while (true)
        {
            object next;
            try { if (!test.MoveNext()) break; next = test.Current; }
            catch (Exception e) { Finish("FAIL: " + e); yield break; }
            yield return next;
        }
        Finish("PASS: All story regression checks completed");
    }
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        results.Add("PASS: " + message);
    }
    static object Field(object obj, string name) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
    static int Pending(DialogueBox box) => ((IDictionary)Field(box, "pendingBatches")).Count;
    // each office worker from over their shoulder: at a desk, and (in the Echo) as a soul
    static void DeskShots(string realm)
    {
        foreach (var w in Object.FindObjectsByType<OfficeWitness>(FindObjectsSortMode.None))
        {
            var g = w.GetComponent<GhostNPC>(); var t = w.transform;
            // (no "starts at a desk" check any more: the witnesses walk the office in pain, PainWalk clears `working`)
            var go = new GameObject("Desk verification camera"); var c = go.AddComponent<Camera>(); c.enabled = false; c.nearClipPlane = .05f; c.fieldOfView = 60f;
            go.transform.position = t.position + t.right * 2.2f - t.forward * .4f + Vector3.up * 1.4f;
            go.transform.LookAt(t.position + t.forward * .3f + Vector3.up * 1f);
            Capture(c, "desk-" + realm + "-" + g.ghostName); Object.Destroy(go);
        }
    }

    static void EchoShot(string name)
    {
        var arrival = GameObject.Find("OfficeArrival");
        if (arrival == null) return;
        var go = new GameObject("Echo verification camera");
        var camera = go.AddComponent<Camera>();
        camera.enabled = false;
        camera.fieldOfView = 65f; camera.nearClipPlane = .1f; camera.farClipPlane = 150f;
        camera.allowHDR = true;
        go.transform.SetPositionAndRotation(arrival.transform.position + Vector3.up * .7f + arrival.transform.forward * 3f, arrival.transform.rotation);
        var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        var target = new RenderTexture(1280, 720, 24);
        var previous = RenderTexture.active;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            Directory.CreateDirectory("Library/GameplayTestArtifacts/Story/echo");
            File.WriteAllBytes("Library/GameplayTestArtifacts/Story/echo/" + name + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(go);
        }
    }

    static IEnumerator Checks(DialogueBox box)
    {
        yield return null;
        yield return null;
        Time.timeScale = 1f;
        results.Clear();
        var choices = ChoiceDialogue.Instance;
        Check(choices != null, "Main scene choice UI exists");
        Check(RevisedPlotDirector.Instance != null, "Revised story initializes in MainGameScene");
        choices.Close(); box.Clear(); box.charDelay = 0f; box.fadeTime = 0f;
        // (the one-off "how dialogue works" card waits for a key: nobody is here to press it)
        typeof(DialogueBox).GetField("tutorialShown", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        DialogueBox.AutoAdvanceAfter = 0f;
        box.Say("Audit A|Audit B"); box.Say("Audit A|Audit B");
        Check(Pending(box) == 1, "Duplicate pending speech is suppressed");
        box.SayNow("Audit A|Audit B");
        yield return null; yield return null; yield return null;
        Check(Pending(box) == 1, "Interrupting identical text preserves the replacement request");
        DialogueBox.AutoAdvanceAfter = .02f;
        while (box.Busy) yield return null;
        Check(Pending(box) == 0, "Speech drains and clears deduplication state");
        box.Say("Audit A|Audit B");
        Check(box.Busy && Pending(box) == 1, "Completed speech can be requested again intentionally");
        box.Clear();
        Check(!box.Busy && Pending(box) == 0, "Checkpoint dialogue clear cancels the queue");

        var go = new GameObject("Regression ghost");
        var ghost = go.AddComponent<GhostNPC>(); ghost.enabled = false;
        ghost.nodes = new[] {
            new GhostNode { line = "Question one", choices = new[] { new GhostChoice { text = "Answer", reply = "A reply that must finish first.", next = 1 } } },
            new GhostNode { line = "Question two", choices = new[] { new GhostChoice { text = "Finish", reply = "Done." } } }
        };
        DialogueBox.AutoAdvanceAfter = 0f;
        ghost.Talk();
        Set(choices, "openedAt", Time.unscaledTime - 1f);
        typeof(ChoiceDialogue).GetMethod("Select", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(choices, new object[] { 0 });
        yield return new WaitForSecondsRealtime(.85f);
        Check(box.Busy && !choices.Open, "Next question waits for the previous reply");
        ghost.Talk();
        Check(!choices.Open, "Repeated interaction cannot restart an active conversation");
        DialogueBox.AutoAdvanceAfter = .02f;
        while (!choices.Open) yield return null;
        Check(!box.Busy, "Next question opens after reply completion");
        choices.Close(); ghost.CancelConversation(); Object.Destroy(go); box.Clear();

        var lockObject = new GameObject("Regression puzzle");
        var puzzle = lockObject.AddComponent<TapLock>();
        var unrelated = new GameObject("Inactive puzzle").AddComponent<TapLock>();
        puzzle.Begin(Array.Empty<Behaviour>(), 15f, null, null);
        unrelated.enabled = false;
        Check(TapLock.Active, "Disabling another puzzle does not close the active puzzle");
        var pause = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
        Check(pause != null, "Pause controller exists");
        float before = (float)Field(puzzle, "timeLeft");
        pause.Pause();
        yield return new WaitForSecondsRealtime(.3f);
        Check(Mathf.Approximately(before, (float)Field(puzzle, "timeLeft")), "Puzzle timer freezes while paused");
        pause.Resume(); puzzle.Cancel();
        Check(!TapLock.Active, "Checkpoint cancellation releases the lock puzzle");
        Object.Destroy(lockObject); Object.Destroy(unrelated.gameObject);

        var director = RevisedPlotDirector.Instance;
        // Real imported doors have a 100x scale and rotated local axes. Their clearance stays in metres.
        var clearanceScene = UnityEngine.SceneManagement.SceneManager.CreateScene("Door clearance regression");
        var clearanceRoot = new GameObject("Scaled doorway");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clearanceRoot, clearanceScene);
        clearanceRoot.transform.localScale = Vector3.one * 100f;
        clearanceRoot.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var clearanceBox = clearanceRoot.AddComponent<BoxCollider>(); clearanceBox.size = new Vector3(.01f, .003f, .03f);
        var clearanceDoor = clearanceRoot.AddComponent<KesslerDoor>(); clearanceDoor.blocker = clearanceBox;
        var clearance = clearanceRoot.AddComponent<OfficeRealmPuzzles>(); clearance.Initialize();
        var probe = new GameObject("Player clearance probe");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(probe, clearanceScene);
        probe.transform.position = Vector3.zero;
        Check(!clearance.CanSwitch(probe.transform, false), "Scaled doorway prevents switching inside its blocker");
        probe.transform.position = Vector3.forward * 3f;
        Check(clearance.CanSwitch(probe.transform, false), "Scaled doorway allows switching three metres away");
        UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(clearanceScene);
        var ghosts = RevisedPlot.All<GhostNPC>(director.gameObject.scene);
        var states = new Dictionary<GhostNPC, bool>();
        foreach (var g in ghosts) if (g != null && g.ghostName != "Vane") states[g] = g.gameObject.activeSelf;
        var memory = director.GetComponent<EchoOfficeMemory>();
        Check(memory != null && memory.RendererCount > 0, "Authored office has matching before/after material pairs");
        var originals = new Dictionary<Renderer, Material[]>();
        foreach (var r in RevisedPlot.All<Renderer>(director.gameObject.scene)) originals[r] = r.sharedMaterials;
        yield return new WaitForSeconds(.5f); DeskShots("red");
        EchoShot("red-city");
        director.SetEcho(true);
        yield return new WaitForSecondsRealtime(1f);
        EchoShot("echo"); DeskShots("echo");
        Check(memory.Weight > .99f, "Echo atmosphere blends fully in");
        int changed = 0;
        foreach (var pair in originals)
        {
            var current = pair.Key.sharedMaterials;
            for (int i = 0; i < current.Length; i++) if (current[i] != pair.Value[i]) changed++;
        }
        Check(changed >= 4, "Echo replaces the wall, floor, ceiling and furniture materials");
        director.SetEcho(false);
        yield return new WaitForSecondsRealtime(1f);
        Check(memory.Weight < .01f, "Red city removes the memory post effects");
        foreach (var pair in originals)
        {
            var current = pair.Key.sharedMaterials;
            for (int i = 0; i < current.Length; i++)
                if (current[i] != pair.Value[i]) throw new Exception("Material not restored: " + pair.Key.name);
        }
        Check(true, "Leaving Echo restores every original material slot");
        foreach (var trigger in RevisedPlot.All<StoryTrigger>(director.gameObject.scene))
        {
            if (trigger.name == "PushTrigger") Check(!trigger.gameObject.activeSelf, "Obsolete early shove trigger is disabled");
            if (trigger.name == "LeapTrigger (stairs end)") Check(trigger.gameObject.activeInHierarchy, "Staircase leap remains active");
        }
        box.Clear();
        foreach (var pair in states) Check(pair.Key.gameObject.activeSelf == pair.Value, "Echo restores active state: " + pair.Key.name);
        Check(RevisedPlot.All<KesslerConfrontation>(director.gameObject.scene)[0].ghost.ghostName == "Vane", "Office antagonist uses revised identity");
        // Realm puzzles must remain consistent if switched during their opening animation.
        var door = RevisedPlot.All<KesslerDoor>(director.gameObject.scene)[0];
        director.SetEcho(true); box.Clear(); door.Unlock();
        yield return new WaitForSeconds(.15f); director.SetEcho(false);
        yield return new WaitForSeconds(2.7f);
        Check(door.blocker.enabled, "Door stays closed in Red City after interrupted opening");
        director.SetEcho(true); box.Clear();
        Check(!door.blocker.enabled, "Unlocked door restores open in Echo");
        var pad = RevisedPlot.All<Keypad>(director.gameObject.scene)[0];
        int solvedCount = 0; pad.onSolved.AddListener(() => solvedCount++);
        pad.SetPowered(true); pad.ApplyRealm(false); foreach (char digit in pad.code) pad.Press(digit.ToString());
        Check(solvedCount == 0, "Burnt keypad cannot solve"); box.Clear();
        pad.ApplyRealm(true); pad.Press(pad.code[0].ToString()); pad.ApplyRealm(false); pad.ApplyRealm(true);
        foreach (char digit in pad.code.Substring(1)) pad.Press(digit.ToString());
        Check(solvedCount == 1, "Powered Echo keypad solves exactly once");
        pad.ApplyRealm(false); Check(pad.display.text == "BURNT", "Solved keypad is still burnt outside Echo");
        box.Clear();
        var counterObject = new GameObject("Regression counter"); var counter = counterObject.AddComponent<PuzzleCounter>();
        int counts = 0; counter.onSolved.AddListener(() => counts++); for (int n = 0; n < 6; n++) counter.Add();
        Check(counts == 1, "Fuse completion cannot fire twice"); Object.Destroy(counterObject); box.Clear();

        var witnesses = RevisedPlot.All<OfficeWitness>(director.gameObject.scene);
        Check(witnesses.Length == 4, "All four optional witnesses have realm-specific conversations");
        foreach (var w in witnesses) Check(w.gameObject.activeInHierarchy && w.GetComponent<WitnessLook>().Echo, "Human witness remains present in Echo: " + w.name);
        var optional = witnesses[0].GetComponent<GhostNPC>();
        int completed = 0; optional.onFinished.AddListener(() => completed++);
        optional.answerSeconds = .6f; optional.opening = null; optional.Talk();
        Check(choices.Open && choices.SecondsLeft > 0f, "Echo questioning has a visible deadline");
        pause.Pause(); float seconds = choices.SecondsLeft;
        yield return new WaitForSecondsRealtime(.8f);
        Check(choices.Open && Mathf.Approximately(seconds, choices.SecondsLeft), "Witness deadline freezes during pause");
        pause.Resume(); yield return new WaitForSeconds(.9f);
        Check(optional.fate == GhostNPC.Fate.Gone && completed == 0, "Timeout loses memory without firing confession completion");
        optional.CancelConversation(); box.Clear();
        var choose = typeof(ChoiceDialogue).GetMethod("Select", BindingFlags.Instance | BindingFlags.NonPublic);
        for (int w = 1; w < witnesses.Length; w++)
        {
            var npc = witnesses[w].GetComponent<GhostNPC>(); npc.answerSeconds = 18f;
            npc.Talk();
            for (float wait = 0f; !choices.Open && wait < 8f; wait += Time.deltaTime) yield return null;   // (the shared opening is spoken first)
            Check(choices.Open, "First question arrives after the opening: " + npc.ghostName);
            Set(choices, "openedAt", Time.unscaledTime - 1f); choose.Invoke(choices, new object[] { 0 });
            yield return null;

            for (float wait = 0f; !choices.Open && wait < 6f; wait += Time.deltaTime) yield return null;
            Check(choices.Open, "Follow-up arrives after testimony: " + npc.ghostName);
            Set(choices, "openedAt", Time.unscaledTime - 1f); choose.Invoke(choices, new object[] { w - 1 });
            Check(npc.fate == (w == 1 ? GhostNPC.Fate.Confessed : w == 2 ? GhostNPC.Fate.Damned : GhostNPC.Fate.Lost), "Reply determines witness fate: " + npc.ghostName);
            npc.CancelConversation(); box.Clear();
        }
        var layout = director.GetComponent<OfficeEvidenceLayout>();
        Check(layout.Placed.Count >= 6, "All six investigation props receive surface placement");
        foreach (var item in layout.Placed)
        {
            var bounds = item.GetComponent<Renderer>().bounds;
            bool supported = false;
            foreach (var hit in Physics.RaycastAll(new Vector3(bounds.center.x, bounds.min.y + .01f, bounds.center.z), Vector3.down, .12f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(item) && hit.normal.y > .9f) supported = true;
            if (item.name != "CleaningRota") Check(supported, "Evidence has a support surface: " + item.name);   // (the rota is pinned to a wall: OfficeEvidenceLayout)
            var cameraObject = new GameObject("Evidence verification"); var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.nearClipPlane = .05f;
            var target = bounds.center;
            Vector3 from = target + new Vector3(0f, 1.1f, -1.6f);
            for (int angle = 0; angle < 16; angle++) {
                Vector3 candidate = target + Quaternion.Euler(0f, angle * 22.5f, 0f) * new Vector3(0f, 1.1f, -1.6f);
                bool clear = true;
                foreach (var hit in Physics.RaycastAll(candidate, target - candidate, Vector3.Distance(candidate, target), ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(item)) clear = false;
                if (clear) { from = candidate; break; }
            }
            cameraObject.transform.SetPositionAndRotation(from, Quaternion.LookRotation(target - from));
            Capture(camera, item.name.Replace("'", "").Replace(" ", "-")); Object.Destroy(cameraObject);
        }
        // Force the real ambush at a walkable office location, including a missing optional fader.
        var conf = RevisedPlot.All<KesslerConfrontation>(director.gameObject.scene)[0];
        var fpc = conf.fpc;
        var arrival = GameObject.Find("OfficeArrival").transform;
        fpc.transform.position = arrival.position;
        // The old five-metre offset is inside a fused doorway in the rebuilt office.
        // Exercise a legal realm change, with the same clearance rule the player uses.
        for (int offset = 0; offset < 8 && !director.GetComponent<OfficeRealmPuzzles>().CanSwitch(fpc.transform, false); offset++)
            fpc.transform.position = arrival.position + arrival.forward * (offset + 1f);
        Check(director.GetComponent<OfficeRealmPuzzles>().CanSwitch(fpc.transform, false), "Blink test stands clear of fused doorways");
        fpc.transform.rotation = arrival.rotation;
        if (fpc.TryGetComponent<Rigidbody>(out var body)) { body.position = fpc.transform.position; body.linearVelocity = Vector3.zero; }
        fpc.enabled = true; Physics.SyncTransforms();
        var title = Object.FindFirstObjectByType<TitleScreen>(FindObjectsInactive.Include);
        if (title != null) typeof(TitleScreen).GetMethod("Finish", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(title, null);
        yield return null; yield return null;
        var horror = director.GetComponent<OfficeHorrorDirector>();
        var blink = director.GetComponent<RealmBlink>();
        horror.RequestSwitch(false, () => director.SetEcho(false));
        yield return new WaitForSeconds(.1f);
        Check(blink.Active && blink.Closed > 0f && blink.Closed < 1f && director.Echo, "Eyelids close before the realm changes (active=" + blink.Active + ", closure=" + blink.Closed + ", echo=" + director.Echo + ")");
        var eyelids = (UnityEngine.UI.Graphic)Field(blink, "lids");
        Check(eyelids != null && eyelids.GetComponent<CanvasRenderer>() != null, "Eyelid mesh has a UI renderer");
        Check(!fpc.enabled, "Blink holds movement during the realm swap");
        pause.Pause(); float closure = blink.Closed;
        yield return new WaitForSecondsRealtime(.3f);
        Check(Mathf.Approximately(closure, blink.Closed), "Realm blink freezes during pause");
        pause.Resume();
        Time.timeScale = 0f;
        yield return null; yield return null;
        ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("Library/GameplayTestArtifacts/Story/office/realm-blink.png"));
        yield return null; yield return null; Time.timeScale = 1f;
        bool switchedBehindLids = false;
        for (float wait = 0f; horror.Switching && wait < 5f; wait += Time.deltaTime) {
            if (!director.Echo && blink.Closed > .99f) switchedBehindLids = true;
            yield return null;
        }
        Check(switchedBehindLids && !director.Echo && !blink.Active && fpc.enabled,
            "Realm swaps behind closed eyelids and restores controls (echo=" + director.Echo + ", active=" + blink.Active + ", movement=" + fpc.enabled + ", behindLids=" + switchedBehindLids + ")");
        horror.RequestSwitch(true, () => director.SetEcho(true)); yield return new WaitForSeconds(.08f);
        horror.CancelSwitch();
        Check(!horror.Switching && !blink.Active && blink.Closed == 0f && fpc.enabled, "Interrupted blink clears the overlay and restores movement");
        var visit = director.GetComponent<EchoVisit>();
        director.SetEcho(true); box.Clear();
        Check(Mathf.Approximately(visit.Remaining, 6f), "Every Echo visit starts with six seconds");
        pause.Pause(); visit.AdvanceClock(2f);
        Check(Mathf.Approximately(visit.Remaining, 6f), "Pause holds the Echo clock"); pause.Resume();
        Set(conf.ghost, "mine", true); visit.AdvanceClock(7f);
        Check(visit.HeldForVane && Mathf.Approximately(visit.Remaining, 6f), "Vane conversation holds the Echo clock beyond six seconds");
        Set(conf.ghost, "mine", false);
        for (float wait = 0f; (director.Echo || horror.Switching) && wait < 12f; wait += Time.deltaTime) yield return null;
        Check(!director.Echo && !blink.Active && fpc.enabled && visit.Remaining == 0f, "Six-second expiry blinks back to Red City and restores movement");
        director.SetEcho(true); box.Clear();
        Check(Mathf.Approximately(visit.Remaining, 6f), "Re-entry resets the Echo budget");
        conf.creepTimeout = 2f;
        // Walk the real CCTV route; its serialized flashback callback must arm Vane.
        var tape = RevisedPlot.All<SequencePuzzle>(director.gameObject.scene)[0]; tape.SetPowered(true); tape.ApplyRealm(true);
        for (int step = 0; step < tape.order.Length; step++) {
            tape.Press(tape.order[step]);
            if (step == 0) { tape.ApplyRealm(false); tape.ApplyRealm(true); }
            if (step < 3) {
                var cg = new GameObject("CCTV verification"); var cc = cg.AddComponent<Camera>(); cc.enabled = false; cc.nearClipPlane = .05f;
                Vector3 focus = tape.transform.position + Vector3.up * 1.55f;
                cg.transform.SetPositionAndRotation(focus - tape.transform.forward * 1.5f, Quaternion.LookRotation(tape.transform.forward));
                Capture(cc, "cctv-" + (step + 1)); Object.Destroy(cg);
            }
            box.Clear();
        }
        yield return new WaitForSeconds(4f); ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("Library/GameplayTestArtifacts/Story/office/tomas-tape.png"));
        for (float wait = 0f; wait < 35f && !conf.Armed; wait += Time.deltaTime) yield return null;
        Check(conf.Armed, "Solving CCTV plays the murder footage and arms Vane");
        Check(director.GetComponent<OfficeHorrorDirector>().Stage == 5, "CCTV reaches the final horror stage");
        conf.Arm();
        bool captured = false;
        for (float wait = 0f; wait < 30f && !choices.Open; wait += Time.deltaTime)
        {
            if (conf.ScareVisible && !captured) { yield return new WaitForSeconds(.25f); Capture(conf.playerCam, "vane-ambush"); captured = true; }
            yield return null;
        }
        Check(captured, "Vane ambush reaches a valid visible scare position");
        Check(choices.Open && !conf.Ambushing, "Vane scare hands control to conversation");
        conf.watchTime = .2f; conf.turnTime = 2f;
        for (float wait = 0f; wait < 15f && !director.VaneReleased; wait += Time.deltaTime) {
            if (choices.Open) { Set(choices, "openedAt", Time.unscaledTime - 1f); choose.Invoke(choices, new object[] { 0 }); }
            yield return null;
        }
        Check(director.VaneReleased, "Vane's justification triggers his transformation");
        yield return new WaitForSeconds(4f);
        Check(conf.keyPickup != null && conf.keyPickup.gameObject.activeInHierarchy, "Transformation drops the escape key");
        Check(fpc.enabled, "Transformation restores movement for the escape");
        Check(conf.damned.gameObject.activeSelf, "Vane becomes the pursuing Damned after the grace period");
        Check(!door.blocker.enabled && !door.echoOnly, "Final collapse preserves a usable escape door");
        conf.Leave();
        Check(conf.Done && !conf.Ambushing && !conf.ghost.gameObject.activeSelf, "Office exit cancels all Vane encounter activity");
        DialogueBox.AutoAdvanceAfter = 0f;
    }
    static void Capture(Camera camera, string name)
    {
        var target = new RenderTexture(1280, 720, 24); var old = camera.targetTexture; var active = RenderTexture.active;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try { camera.targetTexture = target; camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
            Directory.CreateDirectory("Library/GameplayTestArtifacts/Story/office"); File.WriteAllBytes("Library/GameplayTestArtifacts/Story/office/" + name + ".png", pixels.EncodeToPNG()); }
        finally { camera.targetTexture = old; RenderTexture.active = active; Object.Destroy(target); Object.Destroy(pixels); }
    }
}
