using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Editor-only robot playtester (Play mode, no keyboard): runs a scripted pass and writes what happened to a log.
//   PlaytestDriver.DriveVan(speed, weave)  the van driven along the escape line by its wheels (no chase started):
//                                          kerbs, barriers, tunnel, bridge. 'weave' swings it side to side so it
//                                          mounts the pavements and scrapes the walls on purpose.
//   PlaytestDriver.RunSilo()               the silo complex start to finish: walk in, jump scare, chase, ladder,
//                                          lift, ambush, the stairs, the leap, the landing.
//   PlaytestDriver.StartSiloFight(kill, dir)  from the editor (not playing, MainGameScene open): enters Play, skips
//                                          to the silo floor and plays the fight's story beats: the fall, Maya's
//                                          rifle throw, Elias beaten, the choice (kill / mercy), the flood. Frames of
//                                          the cutscenes are written to 'dir'.
//   PlaytestDriver.Report                  the log so far;  PlaytestDriver.Busy  still running
public static class PlaytestDriver
{
    static readonly StringBuilder log = new StringBuilder();
    static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();   // (nested steps: run by hand, like coroutines)
    public static string Report => log.ToString();
    public static bool Busy => stack.Count > 0;

    static void Say(string s) { log.AppendLine($"[{Time.time:0.0}] {s}"); }

    static void Run(IEnumerator r)
    {
        log.Clear();
        stack.Clear();
        stack.Push(r);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { stack.Clear(); EditorApplication.update -= Tick; return; }
        if (stack.Count == 0) { EditorApplication.update -= Tick; return; }
        try
        {
            // one step of the innermost routine per frame; a step that hands back another routine runs that first
            for (int guard = 0; guard < 8 && stack.Count > 0; guard++)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator inner) { stack.Push(inner); continue; }
                break;
            }
        }
        catch (System.Exception e) { Say("EXCEPTION " + e); stack.Clear(); }
    }

    public static void Stop() { stack.Clear(); }

    // ---------------------------------------------------------------- the silo fight (from a cold start)

    const string Key = "HellScape.Playtest";

    public static void StartSiloFight(bool kill, string shotDir)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
        SessionState.SetString(Key, (kill ? "kill" : "mercy") + "|" + shotDir);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged += st =>
        {
            if (st != PlayModeStateChange.EnteredPlayMode || SessionState.GetString(Key, "") == "") return;
            string[] arg = SessionState.GetString(Key, "").Split('|');
            SessionState.EraseString(Key);
            Run(SiloFight(arg[0] == "kill", arg[1]));
        };
    }

    static IEnumerator SiloFight(bool kill, string dir)
    {
        IEnumerator Wait(float s) { float t0 = Time.time; while (Time.time - t0 < s) yield return null; }
        // past the title, awake on the red street
        yield return null;
        if (TitleScreen.Showing) { SleepSequence.WakeInHell = true; UnityEngine.SceneManagement.SceneManager.LoadScene("MainGameScene"); yield return null; yield return null; }
        FirstPersonController fpc = null;
        while (fpc == null || !fpc.enabled || Time.timeSinceLevelLoad < 1f || TitleScreen.Showing) { fpc = Object.FindFirstObjectByType<FirstPersonController>(); yield return null; }
        yield return Wait(1f);

        var player = fpc.transform;
        var hp = player.GetComponent<PlayerHealth>();
        var enc = Object.FindFirstObjectByType<SiloEncounter>(FindObjectsInactive.Include);
        var cuts = Object.FindFirstObjectByType<KillerCutscenes>(FindObjectsInactive.Include);
        var flood = enc.flood;
        var playerCam = player.GetComponentInChildren<Camera>();
        foreach (var z in Object.FindObjectsByType<ZombieSpawner>(FindObjectsSortMode.None)) z.enabled = false;
        DialogueBox.AutoAdvanceAfter = 1.6f;
        string Who()
        {
            var sb = new StringBuilder();
            foreach (var sv in Object.FindObjectsByType<Survivor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.Append($"{sv.displayName}[{(sv.gameObject.activeInHierarchy ? "here" : "gone")}{(sv.Turned ? ",TURNED" : "")}{(sv.Clinging ? ",clinging" : "")}{(sv.Drowned ? ",drowned" : "")}] ");
            var rz = flood.risenDamned != null ? flood.risenDamned.damned : null;
            sb.Append($"elias[{(enc.killer.gameObject.activeInHierarchy ? "here" : "gone")}] damned[{(rz != null && rz.gameObject.activeInHierarchy ? "here" : "gone")}]");
            return sb.ToString();
        }

        // onto the silo floor: the floor trigger plays the fall
        Vector3 c = enc.siloCenter.position;
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;
        player.SetPositionAndRotation(c + new Vector3(0.5f, 1.2f, 2.5f), Quaternion.LookRotation(Vector3.back));
        Physics.SyncTransforms();
        Say($"on the silo floor, hp {hp.currentHealth:0}");
        float t = Time.time;
        while (!cuts.Playing && Time.time - t < 40f) yield return null;
        if (!cuts.Playing) { Say("the rifle throw never started; " + Who()); yield break; }
        Say($"RIFLE THROW cutscene started after {Time.time - t:0.0} s; " + Who());
        DevShots.Film(cuts.cam, dir + "/throw_", 16, 0.33f);
        t = Time.time;
        while (cuts.Playing && Time.time - t < 30f) yield return null;
        Say($"throw over after {Time.time - t:0.0} s: rifle at {enc.gunProp.position:F1} (player {player.position:F1}), movement {fpc.enabled}");

        yield return Wait(1f);
        enc.OnGunTaken();
        yield return Wait(2f);
        Say($"rifle taken: equipped {GunController.Equipped}; fight on, hp {hp.currentHealth:0}");
        enc.killer.TakeDamage(100000f, enc.killer.transform.position + Vector3.up, Vector3.forward);
        Say("Elias beaten: " + enc.killer.Defeated);
        t = Time.time;
        while ((enc.choicePanel == null || !enc.choicePanel.activeSelf) && Time.time - t < 30f) yield return null;
        Say($"choice panel up after {Time.time - t:0.0} s");
        yield return Wait(0.6f);
        enc.Choose(kill);
        Say(kill ? "chose: END HIM" : "chose: MERCY");

        if (kill)
        {
            yield return Wait(7f);
            Say($"keys out: {enc.keysPickup.activeSelf}; " + Who());
            enc.keysPickup.GetComponent<Interactable>().Use();
            yield return Wait(1f);
            Say($"keys taken: have CarKeys {Inventory.Has("CarKeys")}");
        }
        else
        {
            t = Time.time;
            while (!cuts.Playing && Time.time - t < 60f) yield return null;
            Say($"MAYA TURNS started after {Time.time - t:0.0} s; " + Who());
            DevShots.Film(cuts.cam, dir + "/turn_", 60, 0.6f);
            t = Time.time;
            while (cuts.cam.enabled && Time.time - t < 90f) yield return null;
            Say($"back in first person after {Time.time - t:0.0} s; " + Who());
            DevShots.Film(playerCam, dir + "/charge_", 12, 0.3f);
            t = Time.time;
            while (cuts.Playing && Time.time - t < 30f) yield return null;
            Say($"cutscene over after another {Time.time - t:0.0} s: movement {fpc.enabled}, hp {hp.currentHealth:0}; " + Who());
        }

        // the flood
        t = Time.time;
        while (!flood.surface.gameObject.activeSelf && Time.time - t < 40f) yield return null;
        Say($"flood {(flood.surface.gameObject.activeSelf ? "began" : "NEVER began")} after {Time.time - t:0.0} s; have CarKeys {Inventory.Has("CarKeys")}; spared {Judgement.SparedKiller} killed {Judgement.KilledKiller}; " + Who());
        DevShots.Film(playerCam, dir + "/flood_", 6, 2.5f);
        t = Time.time;
        float kick = 0f, lastLog = 0f;
        while (Time.time - t < flood.riseTime + 12f && !hp.IsDead)
        {
            if (Time.time > kick) { kick = Time.time + 0.28f; flood.Kick(); }
            if (Time.time - lastLog > 10f) { lastLog = Time.time; Say($"  +{Time.time - t:0} s: blood at {flood.SurfaceY:0.0}, air {flood.Air:0.00}, player y {player.position.y:0.0}; " + Who()); }
            yield return null;
        }
        Say($"end of the swim: dead {hp.IsDead}, player {player.position:F1}, blood at {flood.SurfaceY:0.0}; " + Who());
        Say("verdict would be: damned=" + Judgement.IsDamned());
        Say("VERDICT LINES: " + EndingSequence.VerdictLines(Judgement.IsDamned()));
        DialogueBox.AutoAdvanceAfter = 0f;
        Say("DONE");
    }

    // ---------------------------------------------------------------- the van

    public static void DriveVan(float speed = 13f, float weave = 0f, bool clearRoad = true) => Run(Drive(speed, weave, clearRoad));

    static IEnumerator Drive(float speed, float weave, bool clearRoad)
    {
        var ci = Object.FindFirstObjectByType<CarInteract>(FindObjectsInactive.Include);
        var pv = Object.FindFirstObjectByType<PrologueVan>(FindObjectsInactive.Include);
        GameObject van = ci != null ? ci.gameObject : pv != null ? pv.gameObject : null;
        if (van == null) { Say("no van"); yield break; }
        var pc = van.GetComponent<PrometeoCarController>();
        var rb = van.GetComponent<Rigidbody>();
        var line = MapRoutes.DriveLine(van.scene, out _);
        if (pv != null) { System.Array.Reverse(line); line = line.Skip(3).Reverse().ToArray(); }   // the prologue: street only, start to garage mouth
        if (clearRoad)
        {
            foreach (var o in Object.FindObjectsByType<RoadObstacle>(FindObjectsSortMode.None)) o.gameObject.SetActive(false);
            foreach (var z in Object.FindObjectsByType<ZombieSpawner>(FindObjectsSortMode.None)) z.enabled = false;
            foreach (var z in Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None)) z.gameObject.SetActive(false);
            foreach (var l in Object.FindObjectsByType<LoopDriver>(FindObjectsSortMode.None)) l.gameObject.SetActive(false);
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.parent != null && t.parent.name == "HellWorld" && (t.name.StartsWith("bus_") || t.name.StartsWith("Body"))) t.gameObject.SetActive(false);
        }
        rb.isKinematic = false;
        var front = new[] { pc.frontLeftCollider, pc.frontRightCollider };
        var all = new[] { pc.frontLeftCollider, pc.frontRightCollider, pc.rearLeftCollider, pc.rearRightCollider };

        float[] along = new float[line.Length];
        for (int i = 1; i < line.Length; i++) along[i] = along[i - 1] + Vector3.Distance(line[i - 1], line[i]);
        float total = along[line.Length - 1];
        Vector3 At(float a, out Vector3 dir)
        {
            a = Mathf.Clamp(a, 0f, total);
            int i = 0; while (i < line.Length - 2 && along[i + 1] < a) i++;
            dir = (line[i + 1] - line[i]).normalized;
            return Vector3.Lerp(line[i], line[i + 1], Mathf.InverseLerp(along[i], along[i + 1], a));
        }
        float Progress(Vector3 p, float near)
        {
            float best = float.MaxValue, at = near;
            for (int i = 0; i < line.Length - 1; i++)
            {
                Vector3 ab = line[i + 1] - line[i];
                float t = Mathf.Clamp01(Vector3.Dot(p - line[i], ab) / ab.sqrMagnitude);
                float a = along[i] + ab.magnitude * t;
                if (Mathf.Abs(a - near) > 40f) continue;
                float d = (line[i] + ab * t - p).sqrMagnitude;
                if (d < best) { best = d; at = a; }
            }
            return at;
        }

        Say($"driving {van.name} along {total:0} m at {speed:0.0} m/s, weave {weave:0.0} m");
        float prog = 0f, stuckFor = 0f, started = Time.time, topSpeed = 0f, maxOff = 0f, lastT = Time.time;
        int stuck = 0, flips = 0, falls = 0;
        while (prog < total - 6f && Time.time - started < 400f)
        {
            float dt = Time.time - lastT; lastT = Time.time;
            prog = Progress(van.transform.position, prog);
            Vector3 centre = At(prog, out Vector3 dirNow);
            Vector3 target = At(prog + 10f, out Vector3 dir);
            target += Vector3.Cross(Vector3.up, dir) * weave * Mathf.Sin(prog / 7f);
            Vector3 local = van.transform.InverseTransformPoint(target);
            float steer = Mathf.Clamp(Mathf.Atan2(local.x, Mathf.Max(0.5f, local.z)) * Mathf.Rad2Deg, -32f, 32f);
            float v = rb.linearVelocity.magnitude;
            topSpeed = Mathf.Max(topSpeed, v);
            Vector3 off = van.transform.position - centre; off.y = 0f;
            maxOff = Mathf.Max(maxOff, off.magnitude);
            foreach (var w in front) w.steerAngle = steer;
            foreach (var w in all) { w.motorTorque = v < speed ? 700f : 0f; w.brakeTorque = v > speed * 1.25f ? 800f : 0f; }

            bool recover = false; string why = "";
            stuckFor = v < 0.4f ? stuckFor + dt : 0f;
            if (stuckFor > 3f) { stuck++; why = "STUCK"; recover = true; }
            if (Vector3.Dot(van.transform.up, Vector3.up) < 0.35f) { flips++; why = "FLIPPED"; recover = true; }
            if (van.transform.position.y < centre.y - 5f) { falls++; why = "FELL OFF"; recover = true; }
            if (recover)
            {
                Say($"{why} at {van.transform.position:F1} ({prog:0} m along, {off.magnitude:0.0} m off the line)");
                prog += 7f; stuckFor = 0f;
                Vector3 p = At(prog, out Vector3 d2);
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                van.transform.SetPositionAndRotation(p + Vector3.up * 0.4f, Quaternion.LookRotation(d2));
                Physics.SyncTransforms();
            }
            yield return null;
        }
        foreach (var w in all) { w.motorTorque = 0f; w.brakeTorque = 1500f; }
        Say($"DONE: {prog:0}/{total:0} m in {Time.time - started:0} s, top speed {topSpeed * 3.6f:0} km/h, furthest off the line {maxOff:0.0} m; stuck {stuck}, flipped {flips}, fell {falls}");
    }

    // ---------------------------------------------------------------- the silo complex

    public static void RunSilo() => Run(Silo());

    static IEnumerator Silo()
    {
        var fpc = Object.FindFirstObjectByType<FirstPersonController>();
        var player = fpc.transform;
        var rb = player.GetComponent<Rigidbody>();
        var hp = player.GetComponent<PlayerHealth>();
        var cuts = Object.FindFirstObjectByType<KillerCutscenes>(FindObjectsInactive.Include);
        var chase = Object.FindFirstObjectByType<ChaseKiller>(FindObjectsInactive.Include);
        var enc = Object.FindFirstObjectByType<SiloEncounter>(FindObjectsInactive.Include);
        var elev = Object.FindFirstObjectByType<Elevator>(FindObjectsInactive.Include);
        var ladder = Object.FindFirstObjectByType<Ladder>(FindObjectsInactive.Include);
        var door = Object.FindObjectsByType<Teleporter>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "SiloDoor");
        var arrival = elev.transform.parent.Find("ComplexArrival");
        var scene = fpc.gameObject.scene;
        DialogueBox.AutoAdvanceAfter = 0.8f;

        float Floor(float x, float z, float from)
        {
            float best = float.NaN;
            foreach (var h in Physics.RaycastAll(new Vector3(x, from, z), Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.gameObject.scene == scene && h.normal.y > 0.5f && !h.collider.transform.IsChildOf(player) && (float.IsNaN(best) || h.point.y > best)) best = h.point.y;
            return best;
        }
        float eye = arrival.position.y - Floor(arrival.position.x, arrival.position.z, arrival.position.y + 0.3f);
        float lastHp = hp.currentHealth;
        Vector3 Feet() => player.position - Vector3.up * eye;
        IEnumerator Wait(float s) { float t0 = Time.time; while (Time.time - t0 < s) yield return null; }
        // walk (teleport-step) to a spot on the floor; holds still while a cutscene has the controls
        IEnumerator Go(float x, float z, float speed, string what)
        {
            float y0 = Feet().y;
            float fy = Floor(x, z, y0 + 1.1f);
            if (float.IsNaN(fy)) { Say($"NO FLOOR at ({x:0.0}, {z:0.0}) from y {y0 + 1.1f:0.0}  [{what}]"); fy = y0; }
            Vector3 target = new Vector3(x, fy, z);
            // is the way actually open to a body this size?
            Vector3 from = Feet(), leg = target - from;
            foreach (var h in Physics.CapsuleCastAll(from + Vector3.up * 0.55f, from + Vector3.up * 1.4f, 0.25f, leg.normalized, leg.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.gameObject.scene == scene && !h.collider.transform.IsChildOf(player) && h.distance > 0.05f && h.collider.attachedRigidbody == null)
                { Say($"BLOCKED on the way to {what}: {h.collider.name} at {h.point:F1}"); break; }
            float last = Time.time;
            while ((Feet() - target).magnitude > 0.15f)
            {
                float dt = Time.time - last; last = Time.time;
                if (hp.IsDead) { Say($"DIED on the way to {what} at {Feet():F1}"); yield break; }
                if (Checkpoint.Current != null && Checkpoint.Current.name.Contains("Silo floor")) yield break;   // already jumped: stay down there
                if (cuts.Playing || !fpc.enabled) { yield return null; continue; }
                if (rb != null) rb.isKinematic = true;   // (physics would push the robot back out of a rail it brushes)
                Vector3 step = Vector3.MoveTowards(Feet(), target, speed * dt);
                player.position = step + Vector3.up * eye;
                Vector3 look = target - step; look.y = 0f;
                if (look.sqrMagnitude > 0.01f) player.rotation = Quaternion.LookRotation(look);
                yield return null;
            }
            if (rb != null && fpc.enabled && !cuts.Playing) rb.isKinematic = false;
            if (hp.currentHealth != lastHp) { Say($"  hp {lastHp:0} -> {hp.currentHealth:0} by {what}"); lastHp = hp.currentHealth; }
        }
        string K() => chase.killer.gameObject.activeInHierarchy ? $"killer {Vector3.Distance(chase.killer.position, player.position):0.0} m away" : "killer hidden";

        // through the office's silo door
        player.SetPositionAndRotation(arrival.position, arrival.rotation);
        Physics.SyncTransforms();
        door.onArrive.Invoke();
        Say($"arrived at {arrival.position:F1}, hp {hp.currentHealth:0}, checkpoint {(Checkpoint.Current ? Checkpoint.Current.name : "-")}");
        yield return Wait(0.5f);

        yield return Go(919f, 1053f, 4f, "the bridge door");
        Say("at the bridge door; " + K());
        yield return Go(926.2f, 1052.9f, 4f, "out on the bridge (jump scare)");
        yield return Wait(0.3f);
        Say($"jump scare {(cuts.Playing ? "PLAYING" : "NOT playing")}; " + K());
        float t0 = Time.time;
        while (cuts.Playing && Time.time - t0 < 15f) yield return null;
        Say($"jump scare over after {Time.time - t0:0.0} s: player {Feet():F1}, hp {hp.currentHealth:0}, chasing {chase.Chasing}; " + K());

        yield return Go(941.6f, 1053f, 6.5f, "the end of the bridge");
        yield return Go(942f, 1064.6f, 6.5f, "the corridor");
        yield return Go(953.6f, 1065f, 6.5f, "the machinery room");
        yield return Go(954.6f, 1067.9f, 6.5f, "the hatch");
        Say("at the hatch; " + K());
        ladder.Climb();
        yield return Wait(0.2f);
        t0 = Time.time;
        while (Ladder.Climbing && Time.time - t0 < 20f) yield return null;
        Say($"off the ladder after {Time.time - t0:0.0} s at {Feet():F1}; " + K());

        yield return Go(956.6f, 1090f, 6.5f, "the walkway");
        yield return Go(956.2f, 1103.6f, 6.5f, "the lift switch");
        Say("at the lift switch; " + K());
        elev.Open();
        yield return Wait(0.7f);
        yield return Go(956.15f, 1108.9f, 5f, "the lift car");
        t0 = Time.time;
        while ((player.position.y > 70.5f || cuts.Playing) && Time.time - t0 < 25f) yield return null;
        yield return Wait(1.2f);
        Say($"lift down after {Time.time - t0:0.0} s at {Feet():F1}, chasing {chase.Chasing}, checkpoint {(Checkpoint.Current ? Checkpoint.Current.name : "-")}; " + K());

        yield return Go(956.6f, 1103.5f, 5f, "out of the lift");
        yield return Go(959.5f, 1101f, 5f, "the lower bridge");
        yield return Go(973f, 1101f, 5f, "the ambush");
        yield return Wait(1.8f);
        Say($"ambush: chasing {chase.Chasing}; " + K());
        yield return Go(992f, 1101f, 6.5f, "the tower door");
        Vector3 c = enc.siloCenter.position;
        yield return Go(c.x - 4.6f, c.z + 0.6f, 6.5f, "the top of the stairs");
        Say($"top of the stairs at {Feet():F1}; " + K());
        // down the spiral: west -> south -> east -> north -> west again
        float lastR = 4.6f;
        for (int a = 255; a >= -75 && fpc.enabled && !hp.IsDead; a -= 15)
        {
            float rad = a * Mathf.Deg2Rad, y = Feet().y, bestR = float.NaN;
            for (float r = 3.2f; r <= 5.7f; r += 0.2f)
            {
                float f = Floor(c.x + Mathf.Sin(rad) * r, c.z + Mathf.Cos(rad) * r, y + 0.5f);
                if (float.IsNaN(f) || f < y - 1.8f) continue;
                if (float.IsNaN(bestR) || Mathf.Abs(r - lastR) < Mathf.Abs(bestR - lastR)) bestR = r;
            }
            if (float.IsNaN(bestR)) { Say($"no tread found at {a} deg from y {y:0.0}"); continue; }
            lastR = bestR;
            yield return Go(c.x + Mathf.Sin(rad) * bestR, c.z + Mathf.Cos(rad) * bestR, 6.5f, "the stairs at " + a);
        }
        Say($"bottom of the stairs at {Feet():F1}; " + K());
        yield return Go(c.x - 4.1f, c.z, 5f, "the last landing (leap)");
        t0 = Time.time;
        while (player.position.y > c.y + 4f && Time.time - t0 < 20f) yield return null;
        yield return Wait(3f);
        Say($"after the leap ({Time.time - t0:0.0} s): player {Feet():F1}, hp {hp.currentHealth:0}, checkpoint {(Checkpoint.Current ? Checkpoint.Current.name : "-")}, music {MusicManager.CurrentArea}");
        yield return Wait(6f);
        Say($"on the silo floor: movement {fpc.enabled}, killer active {enc.killer.gameObject.activeInHierarchy} at {enc.killer.transform.position:F1}");
        DialogueBox.AutoAdvanceAfter = 0f;
        Say("DONE");
    }
}
