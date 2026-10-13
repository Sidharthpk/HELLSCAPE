using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Editor-only, Play mode: a shot list (JSON) -> authored camera moves filmed clean (no UI) by TrailerShoot.
//   TrailerShots.Run(@"...\shots.json")   queues the shots; a stage word per shot sets the scene up first.
// Shot: { name, stage, args[], from[], to[], lookFrom[], lookTo[], follow, rel, fov, fovTo, secs, still, roll }
//   positions are world, or relative to 'follow' (a GameObject name, or "van") when rel is true.
//   still: true writes one frame (to shots/_stills/<name>.jpg) instead of a clip, for checking a composition.
public static class TrailerShots
{
    [System.Serializable]
    public class Spec
    {
        public string name = "shot", stage = "", follow = "";
        public float[] args = new float[0], from, to, lookFrom, lookTo;
        public string text = "";
        public bool rel, still, live;   // live: film whatever camera the game itself is using (its cutscene cameras)
        public float fov = 40f, fovTo = 0f, secs = 4f, roll = 0f, settle = 0.6f;
    }
    [System.Serializable] class ShotList { public Spec[] shots; }

    static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    static readonly List<IEnumerator> background = new List<IEnumerator>();
    static Camera rig;
    static string logPath = "Logs/trailer.log";
    public static bool Busy => stack.Count > 0;
    static void Log(string s) { File.AppendAllText(logPath, $"[{Time.time:0.0}s] {s}\n"); }

    public static void Run(string jsonPath)
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("Enter Play first."); return; }
        var list = JsonUtility.FromJson<ShotList>(File.ReadAllText(jsonPath));
        stack.Clear();
        stack.Push(Sequence(list.shots));
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    public static void Cancel() { stack.Clear(); background.Clear(); TrailerShoot.Stop(); TrailerShoot.Override = null; }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Cancel(); EditorApplication.update -= Tick; return; }
        for (int i = background.Count - 1; i >= 0; i--)
        {
            bool alive = true;
            try { alive = background[i].MoveNext(); } catch (System.Exception e) { Log("BG EXCEPTION " + e); alive = false; }
            if (!alive) background.RemoveAt(i);
        }
        if (stack.Count == 0) return;
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
        catch (System.Exception e) { Log("EXCEPTION " + e); stack.Clear(); TrailerShoot.Stop(); TrailerShoot.Override = null; }
    }

    static IEnumerator Wait(float s) { float t0 = Time.time; while (Time.time - t0 < s) yield return null; }

    static Vector3 V(float[] a) => a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;

    static Transform Find(string who)
    {
        if (string.IsNullOrEmpty(who)) return null;
        if (who == "van")
        {
            var pv = Object.FindFirstObjectByType<PrologueVan>(FindObjectsInactive.Include);
            if (pv != null) return pv.transform;
            var ci = Object.FindFirstObjectByType<CarInteract>(FindObjectsInactive.Include);
            return ci != null ? ci.transform : null;
        }
        if (who == "player")
        {
            var d = Object.FindFirstObjectByType<PrologueDirector>();
            return d != null ? d.fpc.transform : null;
        }
        var g = GameObject.Find(who);
        return g != null ? g.transform : null;
    }

    static IEnumerator Sequence(Spec[] shots)
    {
        if (rig == null)
        {
            var go = new GameObject("__trailerRig") { hideFlags = HideFlags.HideAndDontSave };
            rig = go.AddComponent<Camera>();
            rig.enabled = false;
            rig.nearClipPlane = 0.1f; rig.farClipPlane = 2000f;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            // copy the look of the live game camera (renderer, post-processing, background)
            foreach (var c in Camera.allCameras)
                if (c.targetTexture == null && c.enabled && c.GetUniversalAdditionalCameraData().renderType == CameraRenderType.Base)
                {
                    var src = c.GetUniversalAdditionalCameraData();
                    data.SetRenderer(0);
                    data.renderPostProcessing = src.renderPostProcessing;
                    data.antialiasing = src.antialiasing;
                    rig.clearFlags = c.clearFlags; rig.backgroundColor = c.backgroundColor;
                    break;
                }
        }
        foreach (var s in shots)
        {
            Time.timeScale = 1f;
            TrailerShoot.Override = s.live ? null : rig;
            Log($"shot {s.name}: stage '{s.stage}'");
            yield return Stage(s);
            yield return Wait(s.settle);
            bool tracking = true;
            float t0 = Time.time, dur = Mathf.Max(0.05f, s.secs);
            Transform tgt = Find(s.follow);
            Vector3 P(float[] a) => s.rel && tgt != null ? tgt.TransformPoint(V(a)) : V(a);
            if (!s.still) TrailerShoot.Start(s.name, 30, 1920, 1080);
            else { TrailerShoot.Start("_still_" + s.name, 30, 1920, 1080); }
            int frames = 0;
            while (tracking)
            {
                float k = s.still ? 0f : Mathf.Clamp01((Time.time - t0) / dur);
                float e = k * k * (3f - 2f * k);          // ease in and out
                Vector3 pos = Vector3.Lerp(P(s.from), P(s.to != null && s.to.Length >= 3 ? s.to : s.from), e);
                Vector3 look = Vector3.Lerp(P(s.lookFrom), P(s.lookTo != null && s.lookTo.Length >= 3 ? s.lookTo : s.lookFrom), e);
                var tr = rig.transform;
                if (!s.live) tr.position = pos;
                Vector3 fwd = look - pos;
                if (!s.live) tr.rotation = (fwd.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(fwd, Vector3.up) : tr.rotation) * Quaternion.Euler(0, 0, s.roll);
                if (!s.live) rig.fieldOfView = Mathf.Lerp(s.fov, s.fovTo > 0f ? s.fovTo : s.fov, e);
                if (TrailerShoot.Frames > frames) frames = TrailerShoot.Frames;
                if (s.still ? TrailerShoot.Frames >= 2 : (k >= 1f || TrailerShoot.Frames > dur * 30f + 45f)) tracking = false;
                yield return null;
            }
            TrailerShoot.Stop();
            if (s.still)
            {
                var stills = Path.Combine(TrailerShoot.Root, "_stills"); Directory.CreateDirectory(stills);
                string src = Path.Combine(TrailerShoot.Root, "_still_" + s.name, "f00001.jpg");
                if (File.Exists(src)) File.Copy(src, Path.Combine(stills, s.name + ".jpg"), true);
            }
            Log($"shot {s.name} done ({frames} frames)");
        }
        Log("ALL DONE");
        TrailerShoot.Override = null;
    }

    // ------------------------------------------------------------------ staging

    static IEnumerator Stage(Spec s)
    {
        var a = s.args;
        switch (s.stage)
        {
            case "": yield break;
            case "free":      // the prologue's story stopped, the player's eyes unlocked: a clean world to point a camera at
            {
                var d = Object.FindFirstObjectByType<PrologueDirector>();
                if (d != null)
                {
                    d.StopAllCoroutines();
                    if (d.deliveries != null) { d.deliveries.StopAllCoroutines(); if (d.deliveries.beacon != null) d.deliveries.beacon.SetActive(false); }
                    if (d.fader != null) d.fader.fade.alpha = 0f;
                    foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
                }
                yield break;
            }
            case "load":      // text = scene name; MainGameScene comes up in the red city (no title screen)
            {
                if (s.text == "MainGameScene") SleepSequence.WakeInHell = true;
                UnityEngine.SceneManagement.SceneManager.LoadScene(s.text);
                yield return Wait(4f);
                var sleep = Object.FindFirstObjectByType<SleepSequence>();
                if (sleep != null)
                {
                    sleep.StopAllCoroutines();
                    if (sleep.fader != null) sleep.fader.fade.alpha = 0f;
                }
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
                yield break;
            }
            case "wait":
                yield return Wait(a.Length > 0 ? a[0] : 1f);
                yield break;
            case "hide":      // text = object name; args[0] 0 = hide, 1 = show
            {
                var t = Find(s.text);
                if (t != null) t.gameObject.SetActive(a.Length > 0 && a[0] > 0.5f);
                yield break;
            }
            case "put":       // text = object name; x y z yaw
            {
                var t = Find(s.text); if (t == null) { Log("put: no " + s.text); yield break; }
                var rb = t.GetComponent<Rigidbody>(); if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                t.SetPositionAndRotation(V(a), Quaternion.Euler(0, a.Length > 3 ? a[3] : 0f, 0));
                Physics.SyncTransforms();
                yield break;
            }
            case "load_void":   // MainGameScene as if you'd fallen from the prologue: the death vision plays by itself
            {
                SleepSequence.WakeInHell = true;
                SleepSequence.FallSpot = Vector3.zero;
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainGameScene");
                yield return Wait(2f);
                yield break;
            }
            case "call":        // text = "Type.Method": called (no args) on the first such object; routines are started on it
            {
                var parts = s.text.Split('.');
                var type = typeof(PrologueDirector).Assembly.GetType(parts[0]);
                if (type == null) { Log("call: no type " + parts[0]); yield break; }
                var obj = Object.FindFirstObjectByType(type, FindObjectsInactive.Include) as MonoBehaviour;
                var m = type.GetMethod(parts[1], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (obj == null || m == null) { Log("call: missing " + s.text); yield break; }
                var ret = m.Invoke(obj, m.GetParameters().Length == 0 ? null : new object[] { a.Length > 0 ? (object)a[0] : null });
                if (ret is IEnumerator en) obj.StartCoroutine(en);
                yield break;
            }
            case "silo_enter":  // onto the silo floor (the fall and the rifle throw play by themselves); the player can't die
            {
                var enc = Object.FindFirstObjectByType<SiloEncounter>(FindObjectsInactive.Include);
                var fpc = Object.FindFirstObjectByType<FirstPersonController>();
                if (enc == null || fpc == null) { Log("silo_enter: missing"); yield break; }
                foreach (var z in Object.FindObjectsByType<ZombieSpawner>(FindObjectsSortMode.None)) z.enabled = false;
                DialogueBox.AutoAdvanceAfter = 1.2f;
                var player = fpc.transform;
                var rb = player.GetComponent<Rigidbody>(); if (rb != null) rb.linearVelocity = Vector3.zero;
                player.SetPositionAndRotation(enc.siloCenter.position + new Vector3(0.5f, 1.2f, 2.5f), Quaternion.LookRotation(Vector3.back));
                Physics.SyncTransforms();
                var hp = player.GetComponent<PlayerHealth>(); if (hp != null) hp.currentHealth = 99999f;
                yield break;
            }
            case "silo_gun":
            {
                var enc = Object.FindFirstObjectByType<SiloEncounter>(FindObjectsInactive.Include);
                if (enc != null) enc.OnGunTaken();
                yield break;
            }
            case "silo_beat":
            {
                var enc = Object.FindFirstObjectByType<SiloEncounter>(FindObjectsInactive.Include);
                if (enc != null) enc.killer.TakeDamage(100000f, enc.killer.transform.position + Vector3.up, Vector3.forward);
                yield break;
            }
            case "boss_leap":   // the bridge: you on foot at the arena's mouth, the boss comes over the top and lands (its own cutscene)
            {
                var boss = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
                if (boss == null) { Log("boss_leap: no boss"); yield break; }
                typeof(DemonBoss).GetField("chaseBegun", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(boss, true);
                var fpc = Object.FindFirstObjectByType<FirstPersonController>();
                if (fpc != null)
                {
                    var p = fpc.transform;
                    var rb = p.GetComponent<Rigidbody>(); if (rb != null) rb.linearVelocity = Vector3.zero;
                    p.SetPositionAndRotation(new Vector3(325f, -2.5f, 62f), Quaternion.LookRotation(new Vector3(0.78f, 0f, 0.62f)));
                    Physics.SyncTransforms();
                    var hp = p.GetComponent<PlayerHealth>(); if (hp != null) hp.currentHealth = 99999f;
                }
                boss.ArriveAtBridgeEnd();
                yield break;
            }
            case "title":       // the title screen with its own UI drawn in (no "press any button"): canvases go onto the title camera
            {
                TitleScreen.ShowAgain();
                SleepSequence.WakeInHell = false;
                SleepSequence.FallSpot = null;
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainGameScene");
                yield return Wait(0.8f);
                var ts = Object.FindFirstObjectByType<TitleScreen>(FindObjectsInactive.Include);
                if (ts == null) { Log("title: no TitleScreen"); yield break; }
                var canvases = new System.Collections.Generic.HashSet<Canvas>();
                foreach (var c in ts.GetComponentsInChildren<Canvas>(true)) canvases.Add(c.rootCanvas);
                foreach (var cg in new[] { ts.ui, ts.cut }) if (cg != null) canvases.Add(cg.GetComponentInParent<Canvas>().rootCanvas);
                foreach (var c in canvases)
                {
                    c.enabled = true;
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = ts.cam;
                    c.planeDistance = 1f;
                }
                background.Add(HideHint(ts));
                yield break;
            }
            case "boss_follow": // args: distance behind the van (m): the boss runs behind it, for the chase shots
                background.Add(BossFollow(a.Length > 0 ? a[0] : 18f));
                yield break;
            case "van_put":   // x y z yaw
            {
                var van = Find("van"); if (van == null) { Log("no van"); yield break; }
                var rb = van.GetComponent<Rigidbody>();
                if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                van.SetPositionAndRotation(V(a), Quaternion.Euler(0, a.Length > 3 ? a[3] : 0f, 0));
                Physics.SyncTransforms();
                yield break;
            }
            case "van_drive": // speed (m/s)
                background.Add(Autopilot(a.Length > 0 ? a[0] : 10f));
                yield break;
            case "van_stop":
                background.Clear();
                yield break;
            case "player_put": // x y z yaw
            {
                var p = Find("player"); if (p == null) yield break;
                var rb = p.GetComponent<Rigidbody>(); if (rb != null) rb.linearVelocity = Vector3.zero;
                p.SetPositionAndRotation(V(a), Quaternion.Euler(0, a.Length > 3 ? a[3] : 0f, 0));
                Physics.SyncTransforms();
                yield break;
            }
            case "flat_story":  // the flat from the front door on (news, bedroom door, Derek)
            {
                var d = Object.FindFirstObjectByType<PrologueDirector>();
                if (d != null) d.SkipToFlat();
                yield break;
            }
            case "witness":     // the bedroom door comes ajar and Derek is killed behind it
            {
                var d = Object.FindFirstObjectByType<PrologueDirector>();
                if (d == null) yield break;
                var doors = Object.FindObjectsByType<PrologueDoor>(FindObjectsSortMode.None);
                var m = typeof(PrologueDirector).GetMethod("Witness", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                d.StartCoroutine((IEnumerator)m.Invoke(d, new object[] { doors }));
                yield return Wait(0.3f);
                PrologueDoor door = null;
                foreach (var pd in doors)
                    if (pd.transform.parent != null && pd.transform.parent.name == d.bedroomDoorFrame && Mathf.Abs(pd.transform.position.y - d.body.position.y) < 2.5f) door = pd;
                if (door != null) door.GetComponent<Interactable>().Use();   // ease the door: the cutscene runs by itself
                else Log("no bedroom door");
                yield break;
            }
            default: Log("unknown stage " + s.stage); yield break;
        }
    }


    static IEnumerator HideHint(TitleScreen ts)
    {
        while (ts != null)
        {
            if (ts.ui != null)
            {
                var h = ts.ui.transform.Find("Hint");
                if (h != null) { var t = h.GetComponent<TMPro.TMP_Text>(); if (t != null) { t.text = ""; t.alpha = 0f; } }
            }
            yield return null;
        }
    }

    // the boss kept right behind the van while it drives (the chase's pursuit, staged)
    static IEnumerator BossFollow(float dist)
    {
        var boss = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        var van = Find("van");
        if (boss == null || van == null) { Log("boss_follow: missing"); yield break; }
        boss.gameObject.SetActive(true);
        typeof(DemonBoss).GetMethod("SetModelVisible", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(boss, new object[] { true });
        float t = 0f;
        while (boss != null && van != null)
        {
            t += Time.deltaTime;
            Vector3 f = van.forward; f.y = 0f; f.Normalize();
            Vector3 p = van.position - f * (dist + Mathf.Sin(t * 1.3f) * 2f);
            boss.transform.SetPositionAndRotation(p, Quaternion.LookRotation(f));
            yield return null;
        }
    }

    // the van driven along the street by its wheels (the prologue's route: start to the garage mouth)
    static IEnumerator Autopilot(float speed)
    {
        var vt = Find("van"); if (vt == null) yield break;
        var pv = vt.GetComponent<PrologueVan>();
        var pc = vt.GetComponent<PrometeoCarController>();
        var rb = vt.GetComponent<Rigidbody>();
        if (pc == null || rb == null) { Log("van has no controller/body"); yield break; }
        var line = MapRoutes.DriveLine(vt.gameObject.scene, out _);
        if (pv != null) { System.Array.Reverse(line); line = line.Skip(3).Reverse().ToArray(); }
        pc.enabled = false;                 // (it would zero the torque we set every frame)
        rb.isKinematic = false;
        var front = new[] { pc.frontLeftCollider, pc.frontRightCollider };
        var all = new[] { pc.frontLeftCollider, pc.frontRightCollider, pc.rearLeftCollider, pc.rearRightCollider };
        float[] along = new float[line.Length];
        for (int i = 1; i < line.Length; i++) along[i] = along[i - 1] + Vector3.Distance(line[i - 1], line[i]);
        float total = along[line.Length - 1], prog = 0f;
        Vector3 At(float x, out Vector3 dir)
        {
            x = Mathf.Clamp(x, 0f, total);
            int i = 0; while (i < line.Length - 2 && along[i + 1] < x) i++;
            dir = (line[i + 1] - line[i]).normalized;
            return Vector3.Lerp(line[i], line[i + 1], Mathf.InverseLerp(along[i], along[i + 1], x));
        }
        // start from wherever the van is now
        float best = float.MaxValue;
        for (int i = 0; i < line.Length - 1; i++)
        {
            Vector3 ab = line[i + 1] - line[i];
            float t = Mathf.Clamp01(Vector3.Dot(vt.position - line[i], ab) / Mathf.Max(ab.sqrMagnitude, 0.01f));
            float d = (line[i] + ab * t - vt.position).sqrMagnitude;
            if (d < best) { best = d; prog = along[i] + ab.magnitude * t; }
        }
        Log($"autopilot from {prog:0} of {total:0} m at {speed} m/s");
        while (prog < total - 4f)
        {
            // progress: nearest point ahead
            float bd = float.MaxValue, at = prog;
            for (int i = 0; i < line.Length - 1; i++)
            {
                Vector3 ab = line[i + 1] - line[i];
                float t = Mathf.Clamp01(Vector3.Dot(vt.position - line[i], ab) / Mathf.Max(ab.sqrMagnitude, 0.01f));
                float x = along[i] + ab.magnitude * t;
                if (Mathf.Abs(x - prog) > 30f) continue;
                float d = (line[i] + ab * t - vt.position).sqrMagnitude;
                if (d < bd) { bd = d; at = x; }
            }
            prog = at;
            Vector3 target = At(prog + 9f, out _);
            Vector3 local = vt.InverseTransformPoint(target);
            float steer = Mathf.Clamp(Mathf.Atan2(local.x, Mathf.Max(0.5f, local.z)) * Mathf.Rad2Deg, -30f, 30f);
            float v = rb.linearVelocity.magnitude;
            foreach (var w in front) w.steerAngle = steer;
            foreach (var w in all) { w.motorTorque = v < speed ? 700f : 0f; w.brakeTorque = v > speed * 1.2f ? 800f : 0f; }
            yield return null;
        }
        foreach (var w in all) { w.motorTorque = 0f; w.brakeTorque = 1500f; }
        Log("autopilot done");
    }
}
