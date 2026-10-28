using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > Map > Rebuild Escape Route (MainGameScene open). The demo city replaced the old hell city, and the bridge
// the user placed is out west along the highway, but the escape road, the horde line and the bridge fight were still
// out in the empty east. This
//   - moves the bridge fight (BridgeEnding, the horde's stop line, the no-zombie zone) as one rigid block onto the
//     plaza at the end of the bridge: the trigger at the plaza's mouth, the arena square on its floor
//   - finds a drivable route from the van to that trigger (A* over the real colliders, van-sized clearance) and
//     rebuilds the road points from it; the spawner, the horde, Kessler's chase and the cameras are pointed at them
//   - adds the route's markers (EscapeRoute); the map and the next-stop marker come with them
// Safe to run again: the bridge fight is only moved once, the route is rebuilt each time.
public static class EscapeRouteBuilder
{
    static readonly Vector3 PlazaCentre = new Vector3(-842f, 0f, -165f);   // the arena's middle (y found on the floor)
    const float S = 3f;                                                     // route search cell, metres

    [MenuItem("HellScape/Map/Rebuild Escape Route (office to the bridge)")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        var scene = SceneManager.GetSceneByName("MainGameScene");
        if (!scene.isLoaded) return "Open MainGameScene first.";
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.gameObject.scene == scene).ToArray();
        Transform Named(string n) => all.FirstOrDefault(t => t != null && t.name == n);
        var boss = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        var horde = Object.FindFirstObjectByType<HordeChase>(FindObjectsInactive.Include);
        var spawner = Object.FindFirstObjectByType<RoadObstacleSpawner>(FindObjectsInactive.Include);
        var cams = Object.FindFirstObjectByType<FixedCameras>(FindObjectsInactive.Include);
        var car = Object.FindFirstObjectByType<CarInteract>(FindObjectsInactive.Include);
        var bridge = Named("BridgeEnding"); var arena = Named("Boss arena"); var trigger = Named("Boss Trigger (bridge)");
        if (boss == null || horde == null || spawner == null || car == null || bridge == null || arena == null || trigger == null) return "Missing: DemonBoss / HordeChase / RoadObstacleSpawner / CarInteract / BridgeEnding / Boss arena / Boss Trigger.";
        var log = new StringBuilder();
        Physics.SyncTransforms();

        // the walls and barriers of the fight are real colliders in the editor: out of the way while the route is found
        var wasOn = new List<(GameObject, bool)>();
        foreach (var w in boss.fightWalls) if (w != null) { wasOn.Add((w, w.activeSelf)); w.SetActive(false); }
        var barrier = Named("DeadEndBarrier"); if (barrier != null) { wasOn.Add((barrier.gameObject, barrier.gameObject.activeSelf)); barrier.gameObject.SetActive(false); }

        try
        {
            // ------------------------------------------------------------ 1. the bridge fight onto the plaza
            Vector3 centre = PlazaCentre; centre.y = FloorAt(centre.x, centre.z, 13.3f);
            if (Flat(arena.position - centre) > 8f)
            {
                Quaternion turn = Quaternion.Euler(0f, 270f - arena.eulerAngles.y, 0f);
                Vector3 o = arena.position;
                Vector3 Map(Vector3 p) => centre + turn * (p - o);   // (the height comes with it: the arena's old floor is the plaza's)
                // (the office door Kessler bursts out of is in the same group, but it is in the city: it stays)
                var door = boss.officeDoor; Vector3 doorAt = door != null ? door.position : default; Quaternion doorRot = door != null ? door.rotation : default;
                foreach (var t in new[] { bridge, Named("Horde line (bridge mouth)"), Named("HordeStop (Bridge)"), Named("NoZombieZone (Bridge)") })
                {
                    if (t == null) continue;
                    Undo.RecordObject(t, "Escape route");
                    t.SetPositionAndRotation(Map(t.position), turn * t.rotation);
                }
                if (door != null) door.SetPositionAndRotation(doorAt, doorRot);
                var water = Named("WaterBlock_50m");
                if (water != null) boss.waterY = water.position.y + 0.4f;
                EditorUtility.SetDirty(boss);
                log.AppendLine($"bridge fight moved: arena now {arena.position:F0}, trigger {trigger.position:F0}, horde line {Named("Horde line (bridge mouth)").position:F0}");
            }
            else log.AppendLine("bridge fight already on the plaza");
            Physics.SyncTransforms();

            // ------------------------------------------------------------ 2. the route: van -> the trigger
            Vector3 start = car.transform.position;
            Vector3 goal = trigger.position; goal.y = FloorAt(goal.x, goal.z, trigger.position.y);
            var raw = FindRoute(start, goal, log);
            if (raw == null) return log + "NO ROUTE FOUND";
            var pts = Simplify(raw, 3f);
            pts = Subdivide(pts, 45f);
            pts[0] = start; pts[0] = new Vector3(start.x, SurfaceNear(start.x, start.z, start.y), start.z);
            pts[pts.Count - 1] = goal;

            // the points
            var parent = spawner.transform;
            for (int i = parent.childCount - 1; i >= 0; i--)
                if (parent.GetChild(i).name.StartsWith("RoadPoint")) Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
            var made = new List<Transform>();
            for (int i = 0; i < pts.Count; i++)
            {
                var g = new GameObject(i == 0 ? "RoadPoint Start (garage)" : "RoadPoint " + i.ToString("00"));
                g.transform.SetParent(parent, false);
                g.transform.position = pts[i];
                made.Add(g.transform);
            }
            var road = made.Skip(1).ToArray();
            spawner.roadPoints = made.ToArray();
            horde.road = road;
            if (cams != null) cams.road = road;
            boss.chaseRoad = road;
            boss.tunnelFrom = -1; boss.tunnelTo = -1;   // (no tunnel on this road: it follows you all the way)
            var wave = Object.FindFirstObjectByType<BloodWave>(FindObjectsInactive.Include);
            if (wave != null) { wave.awayFrom = road[0]; EditorUtility.SetDirty(wave); }
            EditorUtility.SetDirty(spawner); EditorUtility.SetDirty(horde); EditorUtility.SetDirty(boss); if (cams != null) EditorUtility.SetDirty(cams);
            float len = 0; for (int i = 1; i < pts.Count; i++) len += Vector3.Distance(pts[i - 1], pts[i]);
            log.AppendLine($"route: {pts.Count} points, {len:F0} m, from {pts[0]:F0} to {pts[pts.Count - 1]:F0}");

            // ------------------------------------------------------------ 3. the markers
            var old = Named("Escape route markers"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("Escape route markers");
            var er = go.AddComponent<EscapeRoute>();
            er.road = road; er.boss = boss; er.car = car;
            er.player = car.playerRoot != null ? car.playerRoot.transform : null;
            er.glow = MarkerMaterial();
            EditorUtility.SetDirty(er);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        finally { foreach (var (g, on) in wasOn) if (g != null) g.SetActive(on); }

        // ------------------------------------------------------------ 4. the cameras along the new road
        try { log.AppendLine(EscapeCameras.Place()); } catch (System.Exception e) { log.AppendLine("cameras: " + e.Message); }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return log.ToString();
    }

    // ---------------------------------------------------------------- the search

    static Dictionary<(int, int), float[]> surf = new Dictionary<(int, int), float[]>();
    static float[] Surf(int ix, int iz)
    {
        if (surf.TryGetValue((ix, iz), out var v)) return v;
        var ys = Physics.RaycastAll(new Vector3(ix * S, 90f, iz * S), Vector3.down, 130f, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => h.normal.y > 0.85f && h.collider.attachedRigidbody == null).Select(h => h.point.y).OrderBy(y => y).ToList();
        var o = new List<float>();
        foreach (var y in ys) if (o.Count == 0 || y - o[o.Count - 1] > 0.3f) o.Add(y);
        return surf[(ix, iz)] = o.ToArray();
    }

    static bool Free(float x, float y, float z, float half) =>
        !Physics.CheckBox(new Vector3(x, y + 1.5f, z), new Vector3(half, 0.8f, half), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

    class Node { public int ix, iz; public float y, g, f; public int parent = -1; public bool closed; }

    static List<Vector3> FindRoute(Vector3 start, Vector3 goal, StringBuilder log)
    {
        surf.Clear();
        var nodes = new List<Node>(); var index = new Dictionary<(int, int, int), int>(); var open = new List<int>();
        (int, int, int) K(int ix, int iz, float y) => (ix, iz, Mathf.RoundToInt(y / 0.4f));
        int sx = Mathf.RoundToInt(start.x / S), sz = Mathf.RoundToInt(start.z / S);
        var sy = Surf(sx, sz); if (sy.Length == 0) { log.AppendLine("no ground under the van"); return null; }
        float y0 = sy.OrderBy(y => Mathf.Abs(y - start.y)).First();
        int gx = Mathf.RoundToInt(goal.x / S), gz = Mathf.RoundToInt(goal.z / S);
        float H(int ix, int iz) => Mathf.Sqrt((ix - gx) * (ix - gx) + (iz - gz) * (iz - gz)) * S;
        nodes.Add(new Node { ix = sx, iz = sz, y = y0, f = H(sx, sz) }); index[K(sx, sz, y0)] = 0; open.Add(0);
        var dirs = new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };
        int expanded = 0, found = -1;
        while (open.Count > 0 && expanded < 200000)
        {
            int bi = 0; for (int i = 1; i < open.Count; i++) if (nodes[open[i]].f < nodes[open[bi]].f) bi = i;
            int cur = open[bi]; open[bi] = open[open.Count - 1]; open.RemoveAt(open.Count - 1);
            var c = nodes[cur]; if (c.closed) continue; c.closed = true; expanded++;
            if (Mathf.Abs(c.ix - gx) <= 1 && Mathf.Abs(c.iz - gz) <= 1 && Mathf.Abs(c.y - goal.y) < 2f) { found = cur; break; }
            foreach (var (dx, dz) in dirs)
            {
                int nx = c.ix + dx, nz = c.iz + dz;
                float stepLen = (dx != 0 && dz != 0 ? 1.414f : 1f) * S;
                foreach (var ny in Surf(nx, nz))
                {
                    if (Mathf.Abs(ny - c.y) > 0.65f * stepLen / S) continue;     // a van's gradient
                    var key = K(nx, nz, ny);
                    bool known = index.TryGetValue(key, out int ei);
                    if (known && nodes[ei].closed) continue;
                    if (!Free(nx * S, ny, nz * S, 1.5f)) continue;               // the van fits
                    float g = c.g + stepLen + Mathf.Abs(ny - c.y) * 0.5f + (Free(nx * S, ny, nz * S, 3.6f) ? 0f : 1.5f);   // (and prefers the middle of the road)
                    if (known) { if (g >= nodes[ei].g) continue; var n = nodes[ei]; n.g = g; n.f = g + H(nx, nz); n.parent = cur; n.y = ny; open.Add(ei); }
                    else { nodes.Add(new Node { ix = nx, iz = nz, y = ny, g = g, f = g + H(nx, nz), parent = cur }); index[key] = nodes.Count - 1; open.Add(nodes.Count - 1); }
                }
            }
        }
        if (found < 0)
        {
            var best = nodes.Where(n => n.closed).OrderBy(n => H(n.ix, n.iz)).First();
            log.AppendLine($"closest the van can get: {new Vector3(best.ix * S, best.y, best.iz * S):F0}, {H(best.ix, best.iz):F0} m short ({expanded} cells searched)");
            return null;
        }
        var path = new List<Vector3>();
        for (int i = found; i >= 0; i = nodes[i].parent) path.Add(new Vector3(nodes[i].ix * S, nodes[i].y, nodes[i].iz * S));
        path.Reverse();
        log.AppendLine($"search: {expanded} cells, {path.Count} steps");
        return path;
    }

    // Douglas-Peucker on the ground plane (and the height): the straight runs become one segment
    static List<Vector3> Simplify(List<Vector3> p, float tol)
    {
        var keep = new bool[p.Count]; keep[0] = keep[p.Count - 1] = true;
        var stack = new Stack<(int, int)>(); stack.Push((0, p.Count - 1));
        while (stack.Count > 0)
        {
            var (a, b) = stack.Pop();
            float worst = 0f; int wi = -1;
            for (int i = a + 1; i < b; i++)
            {
                float t = Mathf.Clamp01(Vector3.Dot(Flat3(p[i] - p[a]), Flat3(p[b] - p[a])) / Mathf.Max(0.001f, Flat3(p[b] - p[a]).sqrMagnitude));
                Vector3 on = Vector3.Lerp(p[a], p[b], t);
                float d = Mathf.Max(Flat(p[i] - on), Mathf.Abs(p[i].y - on.y) * 0.5f);
                if (d > worst) { worst = d; wi = i; }
            }
            if (worst > tol) { keep[wi] = true; stack.Push((a, wi)); stack.Push((wi, b)); }
        }
        return p.Where((v, i) => keep[i]).ToList();
    }

    static List<Vector3> Subdivide(List<Vector3> p, float maxLen)
    {
        var o = new List<Vector3> { p[0] };
        for (int i = 1; i < p.Count; i++)
        {
            int n = Mathf.CeilToInt(Vector3.Distance(p[i - 1], p[i]) / maxLen);
            for (int k = 1; k <= n; k++) o.Add(Vector3.Lerp(p[i - 1], p[i], (float)k / n));
        }
        return o;
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
    static Vector3 Flat3(Vector3 v) { v.y = 0f; return v; }

    static float SurfaceNear(float x, float z, float y)
    {
        var ys = Physics.RaycastAll(new Vector3(x, y + 6f, z), Vector3.down, 14f, ~0, QueryTriggerInteraction.Ignore).Where(h => h.normal.y > 0.7f && h.collider.attachedRigidbody == null).Select(h => h.point.y).ToList();
        return ys.Count > 0 ? ys.OrderBy(v => Mathf.Abs(v - y)).First() : y;
    }

    static float FloorAt(float x, float z, float near) => SurfaceNear(x, z, near);

    static Material MarkerMaterial()
    {
        const string path = "Assets/Materials/Escape_Marker.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.SetFloat("_Surface", 1f); m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_ZWrite", 0); m.renderQueue = 3000; m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetColor("_BaseColor", new Color(1f, 0.78f, 0.35f, 0.4f));
        EditorUtility.SetDirty(m);
        return m;
    }
}
