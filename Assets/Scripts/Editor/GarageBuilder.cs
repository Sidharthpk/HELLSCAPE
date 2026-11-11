using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > Map > Build Garage (prologue + main): the Kessler & Vane garage (the user's warehouse model,
// "конт на скетч", placed in the Prologue) is where the van lives, in both scenes. Needs both scenes open.
//   - the model gets colliders (it had none), a painted parking bay inside, sane lamp brightness, and a concrete
//     driveway over the kerb from its open end down to the road
//   - Prologue: the last stop of the delivery round is the bay inside the garage (not the street outside the office)
//   - MainGameScene: a copy of the garage stands on the same spot of the same street (the three buildings the user
//     cleared for it in the prologue are switched off here too), the van starts parked in the bay facing out, and
//     the escape road begins there
public static class GarageBuilder
{
    public const string ParkName = "Garage parking bay";
    public const string GarageName = "конт на скетч";
    const string DrivewayName = "Garage driveway";
    const string Folder = "Assets/Generated/MapColliders";
    static readonly Vector3 BayLocal = new Vector3(-1.2f, 0.02f, 3.4f);      // in the garage's space; the open end is +z
    const float FrontZ = 10.7f, DoorX = -0.25f, DriveWidth = 6.4f;
    static readonly string[] ClearedForGarage = { "Cube.005", "Plane.011", "Cylinder.006", "node_0.008" };

    [MenuItem("HellScape/Map/Build Garage (prologue + main)")]
    public static void Menu() => Debug.Log(Build());

    public static Transform Find(Scene scene) =>
        Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
              .FirstOrDefault(t => t.gameObject.scene == scene && t.name == GarageName && t.Find("Plane") != null);

    public static string Build()
    {
        var pro = SceneManager.GetSceneByName("Prologue");
        var main = SceneManager.GetSceneByName("MainGameScene");
        if (!pro.isLoaded || !main.isLoaded) return "Open MainGameScene and Prologue together first.";
        var sb = new StringBuilder();

        var pg = Find(pro);
        if (pg == null) return "No garage (" + GarageName + ") in the Prologue.";
        if (PrefabUtility.IsPartOfPrefabInstance(pg.gameObject))
            PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(pg.gameObject), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        sb.AppendLine("prologue garage: " + Fit(pg));

        // ---------------------------------------------------------------- the same garage on the same street, in hell
        var p0 = pro.GetRootGameObjects().First(g => g.name == "сити исчерп на скетч").transform;
        var props = main.GetRootGameObjects().First(g => g.name == "Props").transform;
        var m0 = props.Find("сити исчерп на скетч");
        var mg = Find(main);
        if (mg == null)
        {
            var copy = Object.Instantiate(pg.gameObject);
            copy.name = GarageName;
            SceneManager.MoveGameObjectToScene(copy, main);
            mg = copy.transform;
            mg.SetParent(props, true);
        }
        Matrix4x4 N = Matrix4x4.TRS(m0.position, m0.rotation, Vector3.one) * Matrix4x4.TRS(p0.position, p0.rotation, Vector3.one).inverse;
        mg.SetPositionAndRotation(N.MultiplyPoint3x4(pg.position), Quaternion.Euler(0f, pg.eulerAngles.y + m0.eulerAngles.y - p0.eulerAngles.y, 0f));
        foreach (var n in ClearedForGarage) { var t = m0.Find(n); if (t != null && t.gameObject.activeSelf) { t.gameObject.SetActive(false); sb.AppendLine("main: switched off " + n + " (cleared for the garage, as in the prologue)"); } }
        sb.AppendLine("main garage at " + mg.position.ToString("F1") + ": " + Fit(mg));

        // the street's own colliders have to be right before the driveway is measured over them
        sb.AppendLine("prologue streets: " + MapColliders.Build(pro));
        sb.AppendLine("main streets: " + MapColliders.Build(main));
        sb.AppendLine("prologue driveway: " + Driveway(pg, pro));
        sb.AppendLine("main driveway: " + Driveway(mg, main));

        WirePrologue(pro, main, pg, sb);
        WireMain(main, mg, sb);

        EditorSceneManager.MarkSceneDirty(pro);
        EditorSceneManager.MarkSceneDirty(main);
        return sb.ToString();
    }

    // ---------------------------------------------------------------- colliders, the bay, the lamps

    static string Fit(Transform g)
    {
        int cols = 0, junk = 0;
        foreach (Transform c in g)
        {
            var mr = c.GetComponent<MeshRenderer>();
            var mf = c.GetComponent<MeshFilter>();
            if (mr == null || mf == null || mf.sharedMesh == null) continue;
            if (c.name == DrivewayName || c.name == "Floor (collider)") continue;
            // a pile of crates the model left floating 34 m over the roof
            if (g.InverseTransformPoint(mr.bounds.center).y > 20f) { if (c.gameObject.activeSelf) { c.gameObject.SetActive(false); junk++; } continue; }
            if (c.name == "Plane") continue;   // the floor: a box below
            if (c.GetComponent<Collider>() == null) { c.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh; cols++; }
        }
        // the floor: a slab, not a sheet
        var floor = g.Find("Plane");
        Vector3 mn = Vector3.one * 1e9f, mx = Vector3.one * -1e9f;
        foreach (var v in floor.GetComponent<MeshFilter>().sharedMesh.vertices) { Vector3 l = g.InverseTransformPoint(floor.TransformPoint(v)); mn = Vector3.Min(mn, l); mx = Vector3.Max(mx, l); }
        var fc = g.Find("Floor (collider)");
        if (fc == null) { fc = new GameObject("Floor (collider)").transform; fc.SetParent(g, false); }
        fc.localPosition = new Vector3((mn.x + mx.x) * 0.5f, mx.y - 0.5f, (mn.z + mx.z) * 0.5f);
        fc.localRotation = Quaternion.identity;
        var fb = fc.GetComponent<BoxCollider>(); if (fb == null) fb = fc.gameObject.AddComponent<BoxCollider>();
        fb.size = new Vector3(mx.x - mn.x, 1f, mx.z - mn.z);
        foreach (var old in floor.GetComponents<Collider>()) Object.DestroyImmediate(old);

        // the bay: nose in, towards the back wall
        var bay = g.Find(ParkName);
        if (bay == null) { bay = new GameObject(ParkName).transform; bay.SetParent(g, false); }
        bay.localPosition = BayLocal;
        bay.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var lines = bay.Find("Bay lines");
        if (lines == null)
        {
            lines = new GameObject("Bay lines").transform; lines.SetParent(bay, false);
            lines.gameObject.AddComponent<MeshFilter>(); lines.gameObject.AddComponent<MeshRenderer>();
        }
        lines.GetComponent<MeshFilter>().sharedMesh = BayMesh(3.1f, 6.6f, 0.14f);
        lines.GetComponent<MeshRenderer>().sharedMaterial = Paint();
        lines.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // the model's lamps came in blinding (179): a working garage, not a flashbulb
        foreach (var l in g.GetComponentsInChildren<Light>(true)) { l.intensity = Mathf.Min(l.intensity, 14f); l.range = Mathf.Max(l.range, 12f); }
        Physics.SyncTransforms();

        // anything standing in the bay?
        var blockers = Physics.OverlapBox(bay.position + Vector3.up * 1.3f, new Vector3(1.3f, 1f, 3f), bay.rotation, ~0, QueryTriggerInteraction.Ignore)
            .Where(c => c.transform.IsChildOf(g)).Select(c => c.name).Distinct().ToArray();
        return $"{cols} mesh colliders added, {junk} stray pieces hidden, bay at {bay.position:F1}" + (blockers.Length > 0 ? " BLOCKED BY " + string.Join(", ", blockers) : ", clear");
    }

    static Mesh BayMesh(float w, float l, float t)
    {
        var v = new List<Vector3>(); var tr = new List<int>();
        void Quad(float x0, float z0, float x1, float z1)
        {
            int s = v.Count;
            v.Add(new Vector3(x0, 0f, z0)); v.Add(new Vector3(x0, 0f, z1)); v.Add(new Vector3(x1, 0f, z1)); v.Add(new Vector3(x1, 0f, z0));
            tr.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
        }
        float hw = w * 0.5f, hl = l * 0.5f;
        Quad(-hw, -hl, -hw + t, hl); Quad(hw - t, -hl, hw, hl);                 // the sides
        Quad(-hw, hl - t, hw, hl);                                             // the far end (the nose)
        for (int i = 0; i < 5; i++) Quad(-hw + 0.3f + i * 0.55f, -hl, -hw + 0.55f + i * 0.55f, -hl + t);   // a dashed line where you drive in
        var m = new Mesh { name = "Parking bay lines" };
        m.SetVertices(v); m.SetTriangles(tr, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static Material Paint()
    {
        string path = Folder + "/BayPaint.mat";
        Directory.CreateDirectory(Folder);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", new Color(0.9f, 0.68f, 0.08f));
        mat.SetFloat("_Smoothness", 0.1f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.9f, 0.68f, 0.08f) * 0.35f);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------------------------------------------------------------- the driveway

    static float GroundAt(Scene scene, Transform g, Vector3 p, float refY)
    {
        float best = float.NaN;
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, refY + 1.2f, p.z), Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            var c = h.collider;
            if (c.gameObject.scene != scene || c.attachedRigidbody != null || c.transform.IsChildOf(g) || MapAudit.Skip(c.transform)) continue;
            if (c.transform.parent != null && c.transform.parent.name == MapColliders.Container) continue;   // posts and bins aren't ground
            if (float.IsNaN(best) || h.point.y > best) best = h.point.y;
        }
        return best;
    }

    // a concrete apron from the garage's open end over the kerb and down onto the road
    static string Driveway(Transform g, Scene scene)
    {
        var old = g.Find(DrivewayName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        Physics.SyncTransforms();
        float floorY = g.TransformPoint(0f, 0f, 0f).y;
        Vector3 door = g.TransformPoint(new Vector3(DoorX, 0f, FrontZ));
        Vector3 f = g.forward, r = g.right;

        // the ground along three lines out of the door: where the kerb starts and ends, where the road is
        const float Step = 0.25f, Max = 34f;
        int n = Mathf.RoundToInt(Max / Step);
        float kerbFrom = float.NaN, kerbTo = float.NaN, kerbTop = float.MinValue, roadY = float.NaN, verge = float.NaN;
        var centre = new float[n];
        foreach (float off in new[] { 0f, -DriveWidth * 0.5f, DriveWidth * 0.5f })
        {
            var h = new float[n];
            for (int i = 0; i < n; i++) h[i] = GroundAt(scene, g, door + f * (i * Step) + r * off, floorY);
            if (off == 0f) centre = h;
            // the verge: what's just outside the door
            var near = h.Skip(4).Take(8).Where(v => !float.IsNaN(v)).OrderBy(v => v).ToArray();
            if (near.Length == 0) continue;
            float vg = near[near.Length / 2];
            if (off == 0f) verge = vg;
            int a = -1, b = -1; float topHere = float.MinValue;
            for (int i = 8; i < n; i++)
            {
                if (float.IsNaN(h[i])) continue;
                if (a < 0 && h[i] > vg + 0.08f) a = i;
                if (a >= 0 && h[i] > topHere) topHere = h[i];
                if (a >= 0 && h[i] < topHere - 0.25f) { b = i; break; }   // off the kerb, down onto the road
            }
            if (a >= 0 && b >= 0 && topHere > kerbTop) kerbTop = topHere;
            if (a < 0 || b < 0) continue;
            float from = a * Step, to = b * Step;
            if (float.IsNaN(kerbFrom) || from < kerbFrom) kerbFrom = from;
            if (float.IsNaN(kerbTo) || to > kerbTo) kerbTo = to;
            float ry = h.Skip(b).Take(16).Where(v => !float.IsNaN(v)).DefaultIfEmpty(vg - 0.18f).Min();
            if (float.IsNaN(roadY) || ry < roadY) roadY = ry;
        }
        if (float.IsNaN(verge)) verge = floorY;
        bool kerb = !float.IsNaN(kerbFrom);
        if (!kerb) { kerbFrom = 8f; kerbTo = 9f; kerbTop = verge; roadY = verge; }

        // the profile along the drive (distance out of the door, height)
        float top = Mathf.Max(floorY, verge) + 0.03f;
        var prof = new List<Vector2>
        {
            new Vector2(-1.2f, floorY + 0.02f),
            new Vector2(0.4f, top),
            new Vector2(Mathf.Max(0.8f, kerbFrom - 1.6f), top),
            new Vector2(kerbFrom - 0.1f, kerbTop + 0.03f),
            new Vector2(kerbTo - 0.5f, kerbTop + 0.03f),
            new Vector2(kerbTo + 1.6f, roadY + 0.02f),
        };
        float end = prof[prof.Count - 1].x;

        var go = new GameObject(DrivewayName);
        go.transform.SetParent(g, false);
        go.transform.SetPositionAndRotation(new Vector3(door.x, 0f, door.z), g.rotation);
        go.isStatic = true;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
        float hw = DriveWidth * 0.5f;
        // flared where it meets the road, like a real dropped kerb
        float Half(int i) => i >= prof.Count - 2 ? hw + (i == prof.Count - 1 ? 1.2f : 0.5f) : hw;
        for (int i = 0; i < prof.Count; i++)
        {
            float w = Half(i);
            v.Add(new Vector3(-w, prof[i].y, prof[i].x)); v.Add(new Vector3(w, prof[i].y, prof[i].x));
            v.Add(new Vector3(-w, prof[i].y - 0.7f, prof[i].x)); v.Add(new Vector3(w, prof[i].y - 0.7f, prof[i].x));   // skirts
            for (int k = 0; k < 4; k++) uv.Add(new Vector2((k % 2 == 0 ? -w : w) / 3f + (k >= 2 ? 0.13f : 0f), prof[i].x / 3f));
        }
        for (int i = 0; i < prof.Count - 1; i++)
        {
            int a = i * 4, b = a + 4;
            tr.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });             // top
            tr.AddRange(new[] { a + 2, b + 2, a, a, b + 2, b });             // left skirt
            tr.AddRange(new[] { a + 1, b + 1, a + 3, a + 3, b + 1, b + 3 }); // right skirt
        }
        int last = (prof.Count - 1) * 4;
        tr.AddRange(new[] { last, last + 2, last + 1, last + 1, last + 2, last + 3 });   // the lip on the road
        var mesh = new Mesh { name = "Garage driveway" };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tr, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = g.Find("Plane").GetComponent<Renderer>().sharedMaterial;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        Physics.SyncTransforms();

        // street furniture standing in the drive: out of the way
        var moved = new List<string>();
        Vector3 mid = door + f * (end * 0.5f) + Vector3.up * (floorY + 1.5f - door.y);
        foreach (var c in Physics.OverlapBox(mid, new Vector3(hw + 0.6f, 1.2f, end * 0.5f + 0.5f), g.rotation, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.gameObject.scene != scene || c.transform.parent == null || c.transform.parent.name != MapColliders.Container) continue;
            string piece = c.name.Contains(" box") ? c.name.Substring(0, c.name.IndexOf(" box")) : c.name;
            var visual = c.transform.parent.parent.Find(piece);
            // a single bollard or bin can go; a lamp row shares one mesh, so only its post's collider goes
            if (visual != null && visual.name.StartsWith("Cylinder")) visual.gameObject.SetActive(false);
            Object.DestroyImmediate(c.gameObject);
            moved.Add(piece);
        }
        return (kerb ? $"kerb {kerbFrom:0.0}-{kerbTo:0.0} m from the door ({kerbTop - roadY:0.00} m high), " : "no kerb found, ") + $"{end:0.0} m long" + (moved.Count > 0 ? ", cleared: " + string.Join(", ", moved) : "");
    }

    // the two corners of the open end
    public static Vector3[] DoorCorners(Transform g) => new[] { g.TransformPoint(new Vector3(-6.7f, 0f, FrontZ)), g.TransformPoint(new Vector3(6.3f, 0f, FrontZ)) };

    public static Vector3 DrivewayMouth(Transform g)
    {
        var d = g.Find(DrivewayName);
        if (d == null) return g.TransformPoint(new Vector3(DoorX, 0f, FrontZ + 10f));
        var b = d.GetComponent<MeshFilter>().sharedMesh.bounds;
        return d.TransformPoint(new Vector3(0f, b.min.y + 0.7f, b.max.z + 3f));
    }

    // ---------------------------------------------------------------- the prologue: park here

    public static void WirePrologue(Scene pro, Scene main, Transform g, StringBuilder sb)
    {
        var run = Object.FindObjectsByType<DeliveryRun>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(d => d.gameObject.scene == pro);
        if (run == null) { sb.AppendLine("prologue: no DeliveryRun (run Prologue: Build Van Deliveries first)"); return; }
        var bay = g.Find(ParkName);
        var oldBay = run.transform.Find("Depot bay (office)");
        if (oldBay != null) Object.DestroyImmediate(oldBay.gameObject);
        run.depotBay = bay;
        run.depotRadius = 2.6f;
        run.depotObjective = "Park the van in the Kessler & Vane garage";
        run.depotLines = "Van's back in the garage.|Office lights are off. Door's locked. At this hour? Kessler's never late.|Keys through the mail slot. They land in the tray on my desk, like every Saturday.";
        EditorUtility.SetDirty(run);
        TuneVan(run.van != null ? run.van.gameObject : null, sb);

        // the round, as a line the map tools can follow
        var drive = run.transform.Find("Drive line");
        if (drive != null) Object.DestroyImmediate(drive.gameObject);
        drive = new GameObject("Drive line").transform;
        drive.SetParent(run.transform, false);
        var pts = new List<Vector3>();
        if (run.van != null) pts.Add(run.van.transform.position);
        // between the two stops the street bends: follow main's road points, carried over like the stops were
        var between = new List<Vector3>();
        if (main.isLoaded)
        {
            var m1 = main.GetRootGameObjects().First(o => o.name == "Props").transform.Find("сити исчерп на скетч (1)");
            var p1 = pro.GetRootGameObjects().FirstOrDefault(o => o.name == "сити исчерп на скетч (1)");
            if (m1 != null && p1 != null)
            {
                Matrix4x4 M = Matrix4x4.TRS(p1.transform.position, p1.transform.rotation, Vector3.one) * Matrix4x4.TRS(m1.position, m1.rotation, Vector3.one).inverse;
                foreach (var name in new[] { "RoadPoint 00", "RoadPoint 01", "RoadPoint 02" })
                {
                    var rp = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.gameObject.scene == main && t.name == name);
                    if (rp != null) between.Add(M.MultiplyPoint3x4(rp.position));
                }
            }
        }
        for (int i = 0; i < run.stops.Length; i++)
        {
            if (i == 1) pts.AddRange(between);
            if (run.stops[i].bay != null) pts.Add(run.stops[i].bay.position);
        }
        Vector3 mouth = DrivewayMouth(g);
        // from the last stop to the garage the street is the one the morning traffic drives
        var loop = Object.FindObjectsByType<LoopDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(l => l.gameObject.scene == pro && l.path != null && l.path.Length > 4);
        if (loop != null && pts.Count > 0)
        {
            int half = loop.path.Length / 2;   // the way in (the second half is the same street back out)
            int from = 0, to = 0; float bf = float.MaxValue, bt = float.MaxValue;
            for (int i = 0; i < half; i++)
            {
                float df = (loop.path[i] - pts[pts.Count - 1]).sqrMagnitude, dt = (loop.path[i] - mouth).sqrMagnitude;
                if (df < bf) { bf = df; from = i; }
                if (dt < bt) { bt = dt; to = i; }
            }
            // ...and on past the garage down the home street to where the traffic turns round: the van can go there too
            for (int i = from + 6; i < half; i += 8) pts.Add(loop.path[i]);
        }
        pts.Add(mouth);
        pts.Add(g.TransformPoint(new Vector3(DoorX, 0f, FrontZ)));
        pts.Add(bay.position);
        for (int i = 0; i < pts.Count; i++)
        {
            var p = new GameObject($"Drive {i:00}").transform;
            p.SetParent(drive, false);
            p.position = pts[i];
        }
        sb.AppendLine($"prologue: depot is now the garage bay {bay.position:F1}, drive line of {pts.Count} points");
    }

    // ---------------------------------------------------------------- the hell city: the van starts here

    static void WireMain(Scene main, Transform g, StringBuilder sb)
    {
        var bay = g.Find(ParkName);
        var van = main.GetRootGameObjects().FirstOrDefault(o => o.name == "Prometheus");
        if (van == null) { sb.AppendLine("main: no Prometheus"); return; }
        // parked nose out: straight down the drive when Kessler comes through the doors
        Vector3 was = van.transform.position;
        Vector3 at = new Vector3(bay.position.x, g.position.y + 0.02f, bay.position.z);
        van.transform.SetPositionAndRotation(at, Quaternion.LookRotation(g.forward));
        var rb = van.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;
        TuneVan(van, sb);
        EditorUtility.SetDirty(van.transform);

        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.gameObject.scene == main).ToArray();

        // the "get in" prompt floats in the world by the van: bring it along, facing whoever walks in at the door
        var ci = van.GetComponent<CarInteract>();
        if (ci != null && ci.promptUI != null && !ci.promptUI.transform.IsChildOf(van.transform))
        {
            var canvas = ci.promptUI.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                canvas.transform.rotation = Quaternion.LookRotation(-g.forward);
                canvas.transform.position += at + Vector3.up * 2.9f + g.forward * 2.6f - ci.promptUI.transform.position;
                EditorUtility.SetDirty(canvas.transform);
            }
        }

        // dying during the escape puts you back at the garage door, not up the street at the office
        var escape = all.Select(t => t.GetComponent<Checkpoint>()).FirstOrDefault(c => c != null && c.name.Contains("Escape"));
        if (escape != null)
        {
            var respawn = g.Find("Escape respawn");
            if (respawn == null) { respawn = new GameObject("Escape respawn").transform; respawn.SetParent(g, false); }
            respawn.localPosition = new Vector3(DoorX, 1.05f, FrontZ + 3f);
            respawn.localRotation = Quaternion.Euler(0f, 180f, 0f);   // facing in, at the van
            escape.spawn = respawn;
            EditorUtility.SetDirty(escape);
        }

        var start = all.FirstOrDefault(t => t.name == "RoadPoint Start (garage)");
        if (start != null) start.position = at;

        // nothing dropped on the road inside the garage or on its drive
        var spawner = all.Select(t => t.GetComponent<RoadObstacleSpawner>()).FirstOrDefault(s => s != null);
        var zones = all.FirstOrDefault(t => t.name.StartsWith("NoObstacleZones"));
        if (spawner != null && zones != null)
        {
            var zone = zones.Find("Zone garage");
            if (zone == null) { zone = new GameObject("Zone garage").transform; zone.SetParent(zones, false); }
            Vector3 mouth = DrivewayMouth(g);
            Vector3 back = g.TransformPoint(new Vector3(DoorX, 0f, -FrontZ));
            zone.SetPositionAndRotation((mouth + back) * 0.5f + Vector3.up, g.rotation);
            var zb = zone.GetComponent<BoxCollider>(); if (zb == null) zb = zone.gameObject.AddComponent<BoxCollider>();
            zb.isTrigger = true;
            zb.size = new Vector3(18f, 8f, Vector3.Distance(mouth, back) + 6f);
            if (!spawner.noSpawnZones.Contains(zb)) spawner.noSpawnZones = spawner.noSpawnZones.Where(z => z != null).Append(zb).ToArray();
            spawner.keepClearOf = van.transform;
            EditorUtility.SetDirty(spawner);
        }
        sb.AppendLine($"main: van moved {was:F1} -> {at:F1} (in the garage, facing out), road start moved with it");
    }

    // ---------------------------------------------------------------- the van's own body

    // slides along walls instead of gripping them, can't be driven through thin things at speed, rides kerbs
    public static void TuneVan(GameObject van, StringBuilder sb)
    {
        if (van == null) return;
        var rb = van.GetComponent<Rigidbody>();
        if (rb != null) { rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; EditorUtility.SetDirty(rb); }
        var solid = van.transform.Find("Van body collider");
        if (solid != null)
        {
            var box = solid.GetComponent<BoxCollider>();
            string path = Folder + "/VanBody.physicMaterial";
            Directory.CreateDirectory(Folder);
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm == null) { pm = new PhysicsMaterial("VanBody"); AssetDatabase.CreateAsset(pm, path); }
            pm.dynamicFriction = 0.05f; pm.staticFriction = 0.05f; pm.bounciness = 0f;
            pm.frictionCombine = PhysicsMaterialCombine.Minimum; pm.bounceCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(pm);
            box.sharedMaterial = pm;
            // the belly a little higher: kerbs go under it, not into it
            float topY = box.center.y + box.size.y * 0.5f, bottom = Mathf.Max(box.center.y - box.size.y * 0.5f, 0.55f);
            box.center = new Vector3(box.center.x, (topY + bottom) * 0.5f, box.center.z);
            box.size = new Vector3(box.size.x, topY - bottom, box.size.z);
            EditorUtility.SetDirty(box);
        }
        var rec = van.GetComponent<VanRecovery>();
        if (rec == null) rec = van.AddComponent<VanRecovery>();
        rec.body = rb;
        rec.controller = van.GetComponent<PrometeoCarController>();
        EditorUtility.SetDirty(rec);
        sb.AppendLine($"{van.name}: continuous collision, slippery body, recovery (hold T)");
    }
}
