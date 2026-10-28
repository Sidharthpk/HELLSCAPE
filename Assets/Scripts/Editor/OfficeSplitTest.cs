using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Debug: robot playthrough of the office + silo being its own scene. HellScape > Debug > Test Office Split.
// Checks in OfficeSilo: hell look and HUD, the Judgement mark on the hands, the Echo swapping the burnt materials back and
// forth, the Echo doors opening, no corridor loop. Then the doors between scenes: office exit -> city, city door -> office,
// silo exit -> city (escape checkpoint). Log: Logs/office/split.txt, frames beside it. Runs on EditorApplication.update so
// it survives the scene loads.
public static class OfficeSplitTest
{
    const string Key = "HellScape.OfficeSplitTest";
    static string dir => Path.Combine(Application.dataPath, "../Logs/office");
    static readonly StringBuilder log = new StringBuilder();
    static int shots, fails;
    static IEnumerator run; static double until;

    [MenuItem("HellScape/Debug/Test Office Split")]
    static void Menu() { SessionState.SetString(Key, "1"); EditorApplication.isPlaying = true; }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged += st =>
        {
            if (st != PlayModeStateChange.EnteredPlayMode || SessionState.GetString(Key, "") == "") return;
            SessionState.EraseString(Key);
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
            log.Clear(); stack.Clear(); shots = fails = 0;
            SleepSequence.WakeInHell = true;
            SceneManager.LoadScene("OfficeSilo");
            run = Run(); until = 0;
            EditorApplication.update += Step;
        };
    }

    static void Step()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < until) return;
        bool more;
        try { more = Advance(); } catch (System.Exception ex) { L("EXCEPTION " + ex); fails++; more = false; }
        if (more) return;
        EditorApplication.update -= Step;
        L("DONE fails=" + fails);
        File.WriteAllText(Path.Combine(dir, "split.txt"), log.ToString());
        Debug.Log("[split] DONE fails=" + fails);
        EditorApplication.isPlaying = false;
    }

    static readonly System.Collections.Generic.Stack<IEnumerator> stack = new System.Collections.Generic.Stack<IEnumerator>();

    // runs nested iterators (yield return WaitScene(...)) and float waits; false when everything has finished
    static bool Advance()
    {
        if (stack.Count == 0) stack.Push(run);
        while (stack.Count > 0)
        {
            var top = stack.Peek();
            if (!top.MoveNext()) { stack.Pop(); continue; }
            if (top.Current is IEnumerator inner) { stack.Push(inner); continue; }
            if (top.Current is float s) until = EditorApplication.timeSinceStartup + s;
            return true;
        }
        return false;
    }

    static void L(string s) { log.AppendLine(s); Debug.Log("[split] " + s); }
    static void Check(bool ok, string what) { if (!ok) fails++; L((ok ? "PASS " : "FAIL ") + what); }

    static void Shot(string name)
    {
        var cam = Camera.main; if (cam == null) return;
        var rt = new RenderTexture(640, 360, 24);
        var old = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
        RenderTexture.active = rt;
        var tex = new Texture2D(640, 360); tex.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(dir, (shots++).ToString("00") + "_" + name + ".png"), tex.EncodeToPNG());
        Object.Destroy(rt); Object.Destroy(tex);
    }

    static Transform Player() { var f = Object.FindFirstObjectByType<FirstPersonController>(); return f != null ? f.transform.root : null; }
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    static void Teleport(Vector3 p, float yaw)
    {
        var player = Player();
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.position = p; }
        player.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
        Physics.SyncTransforms();
    }

    static bool Marked()
    {
        var player = Player(); var ending = Object.FindFirstObjectByType<EndingSequence>(FindObjectsInactive.Include);
        if (player == null || ending == null || ending.markedSkin == null) return false;
        var arms = player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.ToLower().Contains("arm")).ToList();
        return arms.Count > 0 && arms.All(r => r.material.mainTexture == ending.markedSkin);
    }

    // renderers of the office still wearing a burnt material that the Echo swaps back
    static int BurntCount()
    {
        var set = Resources.Load<EchoOfficeSettings>("EchoOfficeSettings");
        var burnt = set.materials.Where(p => p.burnt != null && p.before != null).Select(p => p.burnt).ToHashSet();
        return Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(r => r.sharedMaterials.Any(m => m != null && burnt.Contains(m)));
    }

    static void HellLook(string where)
    {
        var sleep = Object.FindFirstObjectByType<SleepSequence>();
        Check(sleep != null && RenderSettings.skybox == sleep.hellSkybox, where + ": hell skybox (fog " + RenderSettings.fogColor + ", density " + RenderSettings.fogDensity + ")");
        Check(sleep != null && sleep.hellOnlyUI.All(u => u == null || u.activeInHierarchy), where + ": HUD (hell-only UI) is showing");
        Check(!TitleScreen.Showing, where + ": no title screen");
        Check(Marked(), where + ": the Sign of Judgement is on the hands");
        var fists = Object.FindFirstObjectByType<PunchController>();
        Check(fists != null && fists.enabled, where + ": fists are up and enabled");
        var fpc = Object.FindFirstObjectByType<FirstPersonController>();
        Check(fpc != null && fpc.enabled, where + ": the player can move");
    }

    static IEnumerator WaitScene(string name, string arrive)
    {
        float t = 0f;
        while (SceneManager.GetActiveScene().name != name && t < 20f) { t += .1f; yield return .1f; }
        yield return 5f;   // wake/arrival settles
        Check(SceneManager.GetActiveScene().name == name, "loaded " + name);
        var at = GameObject.Find(arrive); var p = Player();
        float d = at != null && p != null ? Vector3.Distance(p.position, at.transform.position) : 999f;
        Check(d < 4f, "standing at " + arrive + " (" + d.ToString("0.0") + " m)");
    }

    static IEnumerator Run()
    {
        yield return 6f;
        Check(SceneManager.GetActiveScene().name == "OfficeSilo", "OfficeSilo is the scene (not the Prologue)");
        Check(System.Type.GetType("OfficeMemoryLoop") == null, "the corridor-looping teleport is gone");
        var arrival = GameObject.Find("OfficeArrival");
        Check(Vector3.Distance(Player().position, arrival.transform.position) < 4f, "spawn at the office reception");
        Player().GetComponentInChildren<FirstPersonController>().cameraCanMove = false;
        HellLook("OfficeSilo");
        Shot("hands_office");

        // Q: palms up, wide apart, a soft glow that doesn't blind
        var palm = Object.FindFirstObjectByType<PalmLight>();
        var leftArm = Player().GetComponentsInChildren<Transform>(true).First(t => t.name == "ORG-shoulder.L");
        var arms = palm.GetComponent<PunchController>().armsAnimator;
        float bestYaw = 0f, bestD = 99f;   // face the nearest wall so the glow has something to fall on
        for (int a = 0; a < 16; a++)
        {
            var o = Player().position + Vector3.up * .5f; var dir = Quaternion.Euler(0f, a * 22.5f, 0f) * Vector3.forward;
            if (Physics.Raycast(o, dir, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore) && hit.distance < bestD) { bestD = hit.distance; bestYaw = a * 22.5f; }
        }
        Teleport(Player().position, bestYaw);
        L("wall " + bestD.ToString("0.0") + " m away at yaw " + bestYaw);
        palm.SendMessage("Set", true); yield return 3f;
        Check(arms.GetCurrentAnimatorStateInfo(0).IsName("LightLoop"), "the Take loop is playing for the light");
        Check(leftArm.localScale == Vector3.zero, "the left hand is hidden while the light is up");
        var handGlow = Player().GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Right hand glow");
        Check(handGlow == null, "the hand does not glow (only the light comes out of it)");
        Shot("palms_up");
        palm.SendMessage("Set", false); yield return 3f;
        Check(leftArm.localScale != Vector3.zero, "the left hand is back after lowering");
        Check(palm.GetComponent<PunchController>().enabled, "fists work again after lowering");

        // the Echo: burnt things go back to how they were, and come back
        var plot = RevisedPlotDirector.Instance; var horror = Object.FindFirstObjectByType<OfficeHorrorDirector>(); var memory = Object.FindFirstObjectByType<EchoOfficeMemory>();
        Check(plot != null && horror != null && memory != null, "story director, horror director and Echo memory exist");
        Check(memory.RendererCount > 0, "Echo has " + memory.RendererCount + " renderers to restore");
        int burntBefore = BurntCount();
        Check(burntBefore > 0, "red office: " + burntBefore + " renderers burnt");
        plot.SetEcho(true); yield return 3f;
        int burntEcho = BurntCount();
        Check(burntEcho < burntBefore, "in the Echo the materials are back to the original look (" + burntEcho + " burnt left)");
        Check(memory.Weight > .9f, "Echo atmosphere faded in (" + memory.Weight.ToString("0.00") + ")");
        Shot("echo_office");

        // the Echo doors open, and walking through a room doesn't loop you back
        var doors = Object.FindObjectsByType<KesslerDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(k => k.echoOnly && k.blocker != null).ToList();
        Check(doors.Count >= 5, doors.Count + " Echo doors in the office");
        var d0 = doors.OrderBy(k => Vector3.Distance(k.transform.position, arrival.transform.position)).First();
        d0.Unlock(); yield return 3f;
        Check(!d0.blocker.enabled, "an Echo door opens with [F]");
        horror.Advance(4); yield return 1f;
        Vector3 start = d0.blocker.bounds.center - d0.blocker.transform.forward * 3f; start.y = arrival.transform.position.y;
        Teleport(start, 0f);
        float furthestBack = 0f; Vector3 last = Player().position;
        for (int i = 0; i < 90; i++)   // ~18 m of walking, 0.2 m a step, 9 s: the old loop fired after 12 m
        {
            Teleport(Player().position + (d0.blocker.transform.forward) * .2f, 0f);
            yield return .1f;
            furthestBack = Mathf.Max(furthestBack, Vector3.Distance(last, Player().position) > 3f ? 1f : 0f);
            last = Player().position;
        }
        Check(furthestBack == 0f, "walking 18 m through a room: no teleport back");
        plot.SetEcho(false); yield return 3f;
        Check(BurntCount() >= burntBefore, "leaving the Echo burns the office again (" + BurntCount() + ")");
        Shot("back_in_red");

        // door to the city and back
        var exit = Object.FindObjectsByType<Teleporter>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "ExitDoor");
        Teleport(arrival.transform.position, 0f);
        exit.Go();
        yield return WaitScene("MainGameScene", "CityReturnPoint");
        HellLook("city");
        Shot("hands_city");
        var inDoor = Object.FindObjectsByType<Teleporter>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "OfficeDoor");
        inDoor.Go();
        yield return WaitScene("OfficeSilo", "OfficeArrival");
        HellLook("OfficeSilo again");
        Check(Checkpoint.Current != null && Checkpoint.Current.name == "Checkpoint - Office entrance", "office entrance checkpoint active (" + (Checkpoint.Current != null ? Checkpoint.Current.name : "none") + ")");

        // out of the silo into the escape
        var siloExit = Object.FindObjectsByType<Teleporter>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "SiloExit");
        for (var t = siloExit.transform; t != null; t = t.parent) t.gameObject.SetActive(true);   // (the silo is switched on by the story; the robot skips ahead)
        siloExit.Go();
        yield return WaitScene("MainGameScene", "CityReturnPoint");
        Check(Checkpoint.Current != null && Checkpoint.Current.name == "Checkpoint - Escape", "escape checkpoint active (" + (Checkpoint.Current != null ? Checkpoint.Current.name : "none") + ")");
        HellLook("city after the silo");
    }
}
