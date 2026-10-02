using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > Map > Audit Drive Corridor: what the van can see against what it can hit, along the road.
// A 0.25 m grid over a corridor either side of the drive line, at van height (0.45 - 2.45 m over the road):
//   - "visual" cells: some rendered mesh passes through that height band
//   - "solid" cells: some non-trigger static collider does
//   - CLIP  = a mesh you can drive into with nothing solid there (the van sinks into it)
//   - GHOST = something solid in open air, away from any mesh (an invisible wall)
//   - STEP  = the ground jumps 0.15 - 0.6 m between neighbouring cells (a kerb the van has to climb)
//   - HOLE  = no ground under a cell you can reach
// Writes a picture per run to Logs/map-audit/ and returns a report of the worst spots with the objects behind them.
public static class MapAudit
{
    public const float Cell = 0.25f, BandLo = 0.45f, BandHi = 2.45f;

    public class Blob { public string kind; public Vector3 centre; public int cells; public float size; public string who; }
    public class Result
    {
        public int clip, ghost, step, hole, reachable;
        public List<Blob> blobs = new List<Blob>();
        public float[] stepHistogram = new float[8];   // 0.15..0.6 in 8 bins, metres of kerb
        public string picture;
        public string Report(int top = 40)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"reachable {reachable * Cell * Cell:0} m2 | CLIP {clip} cells ({clip * Cell * Cell:0.0} m2) | GHOST {ghost} cells ({ghost * Cell * Cell:0.0} m2) | STEP {step} cells | HOLE {hole} cells | {picture}");
            sb.AppendLine("kerb heights (m of edge): " + string.Join("  ", stepHistogram.Select((v, i) => $"{0.15f + i * 0.05625f:0.00}:{v:0}")));
            foreach (var b in blobs.OrderByDescending(b => b.cells).Take(top))
                sb.AppendLine($"{b.kind,-5} at ({b.centre.x:0.0}, {b.centre.y:0.0}, {b.centre.z:0.0})  {b.cells * Cell * Cell:0.0} m2, {b.size:0.0} m across  <- {b.who}");
            return sb.ToString();
        }
    }

    [MenuItem("HellScape/Map/Audit Drive Corridor")]
    public static void Menu()
    {
        foreach (var r in AuditOpenScenes()) Debug.Log(r.Key + "\n" + r.Value.Report());
    }

    public static Dictionary<string, Result> AuditOpenScenes()
    {
        var res = new Dictionary<string, Result>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            var line = MapRoutes.DriveLine(s, out var areas);
            if (line == null || line.Length < 2) continue;
            res[s.name] = Run(s, line, 14f, areas, s.name);
        }
        return res;
    }

    // areas: extra discs (x, y = ground height, z, w = radius) to cover as well as the corridor (the boss arena)
    public static Result Run(Scene scene, Vector3[] line, float halfWidth, Vector4[] areas, string tag)
    {
        var res = new Result();
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = false;   // as the van meets them: one-sided
        // the hell city is what you drive through: its props count, the day's don't
        var toggled = new List<(GameObject, bool)>();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "HellWorld" && !root.activeSelf) { toggled.Add((root, false)); root.SetActive(true); }
            if (root.name == "DayWorld" && root.activeSelf) { toggled.Add((root, true)); root.SetActive(false); }
        }
        Physics.SyncTransforms();
        try
        {
            // ---------------------------------------------------------------- the grid
            float minX = line.Min(p => p.x) - halfWidth - 2f, maxX = line.Max(p => p.x) + halfWidth + 2f;
            float minZ = line.Min(p => p.z) - halfWidth - 2f, maxZ = line.Max(p => p.z) + halfWidth + 2f;
            if (areas != null)
                foreach (var a in areas)
                {
                    minX = Mathf.Min(minX, a.x - a.w - 2f); maxX = Mathf.Max(maxX, a.x + a.w + 2f);
                    minZ = Mathf.Min(minZ, a.z - a.w - 2f); maxZ = Mathf.Max(maxZ, a.z + a.w + 2f);
                }
            int w = Mathf.CeilToInt((maxX - minX) / Cell), h = Mathf.CeilToInt((maxZ - minZ) / Cell);
            int n = w * h;
            var inside = new bool[n];
            var yRef = new float[n];
            var visual = new int[n];       // renderer index + 1
            var solid = new int[n];        // collider index + 1
            var ground = new float[n];
            var seeds = new List<int>();
            Vector3 CellCentre(int i) => new Vector3(minX + (i % w + 0.5f) * Cell, 0f, minZ + (i / w + 0.5f) * Cell);

            for (int s = 0; s < line.Length - 1; s++)
            {
                Vector3 a = line[s], b = line[s + 1];
                int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - halfWidth - minX) / Cell)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + halfWidth - minX) / Cell));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.z, b.z) - halfWidth - minZ) / Cell)), z1 = Mathf.Min(h - 1, Mathf.CeilToInt((Mathf.Max(a.z, b.z) + halfWidth - minZ) / Cell));
                Vector2 a2 = new Vector2(a.x, a.z), ab = new Vector2(b.x - a.x, b.z - a.z);
                float len2 = Mathf.Max(ab.sqrMagnitude, 0.0001f);
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int i = z * w + x;
                        Vector2 p = new Vector2(minX + (x + 0.5f) * Cell, minZ + (z + 0.5f) * Cell);
                        float t = Mathf.Clamp01(Vector2.Dot(p - a2, ab) / len2);
                        float d = (a2 + ab * t - p).magnitude;
                        if (d > halfWidth) continue;
                        float y = Mathf.Lerp(a.y, b.y, t);
                        if (!inside[i] || d < 1f) yRef[i] = y;
                        inside[i] = true;
                        if (d < Cell) seeds.Add(i);
                    }
            }
            if (areas != null)
                foreach (var a in areas)
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 c = CellCentre(i);
                        if (inside[i] || (c.x - a.x) * (c.x - a.x) + (c.z - a.z) * (c.z - a.z) > a.w * a.w) continue;
                        inside[i] = true; yRef[i] = a.y;
                    }

            // ---------------------------------------------------------------- what's rendered at van height
            var renderers = new List<MeshRenderer>();
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (mr.gameObject.scene != scene || !mr.enabled) continue;
                if (Skip(mr.transform)) continue;
                var b = mr.bounds;
                if (b.max.x < minX || b.min.x > maxX || b.max.z < minZ || b.min.z > maxZ) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                renderers.Add(mr);
            }
            var poly = new List<Vector3>(8); var tmp = new List<Vector3>(8);
            for (int r = 0; r < renderers.Count; r++)
            {
                var mesh = renderers[r].GetComponent<MeshFilter>().sharedMesh;
                var m = renderers[r].transform.localToWorldMatrix;
                var verts = mesh.vertices;
                var world = new Vector3[verts.Length];
                for (int v = 0; v < verts.Length; v++) world[v] = m.MultiplyPoint3x4(verts[v]);
                var tris = mesh.triangles;
                for (int t = 0; t < tris.Length; t += 3)
                {
                    Vector3 a = world[tris[t]], b = world[tris[t + 1]], c = world[tris[t + 2]];
                    float cx = (a.x + b.x + c.x) / 3f, cz = (a.z + b.z + c.z) / 3f;
                    int gx = Mathf.FloorToInt((cx - minX) / Cell), gz = Mathf.FloorToInt((cz - minZ) / Cell);
                    if (gx < 0 || gz < 0 || gx >= w || gz >= h) continue;
                    int gi = gz * w + gx;
                    if (!inside[gi]) { if (!NearInside(inside, w, h, gx, gz, a, b, c, minX, minZ, out gi)) continue; }
                    float lo = yRef[gi] + BandLo, hi = yRef[gi] + BandHi;
                    if (Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < lo || Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > hi) continue;
                    poly.Clear(); poly.Add(a); poly.Add(b); poly.Add(c);
                    ClipY(poly, tmp, lo, true); ClipY(poly, tmp, hi, false);
                    if (poly.Count < 2) continue;
                    Raster(poly, visual, inside, w, h, minX, minZ, r + 1);
                }
            }

            // ---------------------------------------------------------------- what's solid at van height, and the ground
            var colliders = new List<Collider>();
            var colIndex = new Dictionary<Collider, int>();
            var buf = new Collider[32];
            var hits = new RaycastHit[32];
            Vector3 half = new Vector3(Cell * 0.5f, (BandHi - BandLo) * 0.5f, Cell * 0.5f);
            for (int i = 0; i < n; i++)
            {
                if (!inside[i]) continue;
                Vector3 c = CellCentre(i);
                c.y = yRef[i] + (BandLo + BandHi) * 0.5f;
                int k = Physics.OverlapBoxNonAlloc(c, half, buf, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                for (int j = 0; j < k; j++)
                {
                    var col = buf[j];
                    if (col.gameObject.scene != scene || col.attachedRigidbody != null || Skip(col.transform)) continue;
                    if (!colIndex.TryGetValue(col, out int id)) { id = colliders.Count; colliders.Add(col); colIndex[col] = id; }
                    solid[i] = id + 1;
                    break;
                }
                ground[i] = float.NaN;
                k = Physics.RaycastNonAlloc(new Vector3(c.x, yRef[i] + BandLo, c.z), Vector3.down, hits, 4f, ~0, QueryTriggerInteraction.Ignore);
                for (int j = 0; j < k; j++)
                {
                    var col = hits[j].collider;
                    if (col.gameObject.scene != scene || col.attachedRigidbody != null || Skip(col.transform)) continue;
                    if (float.IsNaN(ground[i]) || hits[j].point.y > ground[i]) ground[i] = hits[j].point.y;
                }
            }

            // ---------------------------------------------------------------- reach
            // where the van can physically get to: its centre can't come within a metre of anything solid
            var fat = Dilate(solid, w, h, 4);
            var fatSolid = new int[n];
            for (int i = 0; i < n; i++) fatSolid[i] = fat[i] ? 1 : 0;
            var reach = Flood(seeds, inside, fatSolid, w, h);
            var nearSolid = Dilate(solid, w, h, 2);
            var nearVisual = Dilate(visual, w, h, 3);
            var kind = new byte[n];   // 1 clip, 2 ghost, 3 step, 4 hole
            for (int i = 0; i < n; i++)
            {
                if (!inside[i]) continue;
                if (reach[i]) res.reachable++;
                if (visual[i] != 0 && !nearSolid[i] && Beside(reach, w, h, i, 5)) { kind[i] = 1; res.clip++; }
                else if (solid[i] != 0 && !nearVisual[i] && Beside(reach, w, h, i, 5)) { kind[i] = 2; res.ghost++; }
                else if (reach[i] && float.IsNaN(ground[i])) { kind[i] = 4; res.hole++; }
                else if (reach[i])
                {
                    int x = i % w, z = i / w;
                    foreach (int j in new[] { x + 1 < w ? i + 1 : -1, z + 1 < h ? i + w : -1 })
                    {
                        if (j < 0 || !reach[j] || float.IsNaN(ground[j])) continue;
                        float d = Mathf.Abs(ground[i] - ground[j]);
                        if (d < 0.15f || d > 0.6f) continue;
                        kind[i] = 3; res.step++;
                        res.stepHistogram[Mathf.Clamp(Mathf.FloorToInt((d - 0.15f) / 0.05625f), 0, 7)] += Cell;
                        break;
                    }
                }
            }

            // ---------------------------------------------------------------- the worst spots, and who's behind them
            var seen = new bool[n];
            var stack = new Stack<int>();
            for (int i = 0; i < n; i++)
            {
                if (kind[i] == 0 || kind[i] == 3 || seen[i]) continue;
                byte k = kind[i];
                var who = new Dictionary<string, int>();
                int cells = 0; Vector3 sum = Vector3.zero; int bx0 = int.MaxValue, bx1 = 0, bz0 = int.MaxValue, bz1 = 0;
                stack.Push(i); seen[i] = true;
                while (stack.Count > 0)
                {
                    int c = stack.Pop(); cells++;
                    int x = c % w, z = c / w;
                    Vector3 p = CellCentre(c); p.y = yRef[c]; sum += p;
                    bx0 = Mathf.Min(bx0, x); bx1 = Mathf.Max(bx1, x); bz0 = Mathf.Min(bz0, z); bz1 = Mathf.Max(bz1, z);
                    string name = k == 1 ? PathOf(renderers[visual[c] - 1].transform) : k == 2 ? PathOf(colliders[solid[c] - 1].transform) + " [" + colliders[solid[c] - 1].GetType().Name + "]" : "no ground";
                    who[name] = who.TryGetValue(name, out int cnt) ? cnt + 1 : 1;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, nz = z + dz;
                            if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                            int j = nz * w + nx;
                            if (seen[j] || kind[j] != k) continue;
                            seen[j] = true; stack.Push(j);
                        }
                }
                if (cells < 3) continue;
                res.blobs.Add(new Blob
                {
                    kind = k == 1 ? "CLIP" : k == 2 ? "GHOST" : "HOLE", cells = cells, centre = sum / cells,
                    size = Mathf.Max(bx1 - bx0 + 1, bz1 - bz0 + 1) * Cell,
                    who = string.Join(", ", who.OrderByDescending(p => p.Value).Take(3).Select(p => p.Key)),
                });
            }

            // ---------------------------------------------------------------- the picture
            var px = new Color32[n];
            for (int i = 0; i < n; i++)
            {
                Color32 c = new Color32(38, 38, 44, 255);
                if (inside[i])
                {
                    bool v = visual[i] != 0, s = solid[i] != 0;
                    c = v && s ? new Color32(235, 235, 235, 255) : v ? new Color32(120, 170, 210, 255) : s ? new Color32(70, 70, 150, 255)
                        : reach[i] ? new Color32(0, 0, 0, 255) : new Color32(18, 18, 22, 255);
                    if (kind[i] == 1) c = new Color32(40, 255, 60, 255);
                    else if (kind[i] == 2) c = new Color32(255, 40, 40, 255);
                    else if (kind[i] == 3) c = new Color32(255, 220, 0, 255);
                    else if (kind[i] == 4) c = new Color32(255, 0, 255, 255);
                }
                px[i] = c;
            }
            foreach (int s in seeds) if (kind[s] == 0 && visual[s] == 0 && solid[s] == 0) px[s] = new Color32(0, 90, 110, 255);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px); tex.Apply();
            Directory.CreateDirectory("Logs/map-audit");
            res.picture = $"Logs/map-audit/{tag}.png  (origin x {minX:0.0}, z {minZ:0.0}, {Cell} m per pixel, {w}x{h})";
            File.WriteAllBytes($"Logs/map-audit/{tag}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        finally
        {
            foreach (var (go, was) in toggled) go.SetActive(was);
            Physics.queriesHitBackfaces = backfaces;
            Physics.SyncTransforms();
        }
        return res;
    }

    // not part of the street: things that move, people, the menu's copy of the city, markers
    static readonly string[] SkipRoots = { "TitleScreen", "StoryCanvas", "Escape Cameras", "Canvas" };
    static readonly Dictionary<Transform, bool> skipCache = new Dictionary<Transform, bool>();
    public static bool Skip(Transform t)
    {
        if (skipCache.TryGetValue(t, out bool s)) return s;
        s = false;
        for (var p = t; p != null; p = p.parent)
        {
            if (p.GetComponent<Rigidbody>() != null || p.GetComponent<Animator>() != null || p.GetComponent<UnityEngine.AI.NavMeshAgent>() != null
                || p.GetComponent<ParticleSystem>() != null || p.GetComponent<Light>() != null && p == t
                || p.name == "Delivery beacon" || p.name == "StreetLights" || p.name == "HellFog"
                || p.parent == null && SkipRoots.Contains(p.name)) { s = true; break; }
        }
        skipCache[t] = s;
        return s;
    }

    static string PathOf(Transform t)
    {
        string p = t.name;
        for (int i = 0; i < 2 && t.parent != null; i++) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    // a triangle whose centre lies just outside the corridor may still reach into it
    static bool NearInside(bool[] inside, int w, int h, int gx, int gz, Vector3 a, Vector3 b, Vector3 c, float minX, float minZ, out int gi)
    {
        foreach (var p in new[] { a, b, c })
        {
            int x = Mathf.FloorToInt((p.x - minX) / Cell), z = Mathf.FloorToInt((p.z - minZ) / Cell);
            if (x < 0 || z < 0 || x >= w || z >= h) continue;
            gi = z * w + x;
            if (inside[gi]) return true;
        }
        gi = 0;
        return false;
    }

    static void ClipY(List<Vector3> poly, List<Vector3> tmp, float y, bool keepAbove)
    {
        tmp.Clear();
        for (int i = 0; i < poly.Count; i++)
        {
            Vector3 a = poly[i], b = poly[(i + 1) % poly.Count];
            bool ia = keepAbove ? a.y >= y : a.y <= y, ib = keepAbove ? b.y >= y : b.y <= y;
            if (ia) tmp.Add(a);
            if (ia != ib) tmp.Add(Vector3.Lerp(a, b, (y - a.y) / (b.y - a.y)));
        }
        poly.Clear(); poly.AddRange(tmp);
    }

    static void Raster(List<Vector3> poly, int[] grid, bool[] inside, int w, int h, float minX, float minZ, int id)
    {
        // the edges (a wall seen from above is only its edges)
        for (int i = 0; i < poly.Count; i++)
        {
            Vector3 a = poly[i], b = poly[(i + 1) % poly.Count];
            float len = Mathf.Sqrt((b.x - a.x) * (b.x - a.x) + (b.z - a.z) * (b.z - a.z));
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / (Cell * 0.5f)));
            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps;
                int x = Mathf.FloorToInt((Mathf.Lerp(a.x, b.x, t) - minX) / Cell), z = Mathf.FloorToInt((Mathf.Lerp(a.z, b.z, t) - minZ) / Cell);
                if (x < 0 || z < 0 || x >= w || z >= h) continue;
                if (inside[z * w + x]) grid[z * w + x] = id;
            }
        }
        if (poly.Count < 3) return;
        // and what's inside them
        float x0 = poly.Min(p => p.x), x1 = poly.Max(p => p.x), z0 = poly.Min(p => p.z), z1 = poly.Max(p => p.z);
        if (x1 - x0 < Cell && z1 - z0 < Cell) return;
        int cx0 = Mathf.Max(0, Mathf.FloorToInt((x0 - minX) / Cell)), cx1 = Mathf.Min(w - 1, Mathf.FloorToInt((x1 - minX) / Cell));
        int cz0 = Mathf.Max(0, Mathf.FloorToInt((z0 - minZ) / Cell)), cz1 = Mathf.Min(h - 1, Mathf.FloorToInt((z1 - minZ) / Cell));
        for (int z = cz0; z <= cz1; z++)
            for (int x = cx0; x <= cx1; x++)
            {
                if (!inside[z * w + x]) continue;
                float px = minX + (x + 0.5f) * Cell, pz = minZ + (z + 0.5f) * Cell;
                bool pos = false, neg = false;
                for (int i = 0; i < poly.Count; i++)
                {
                    Vector3 a = poly[i], b = poly[(i + 1) % poly.Count];
                    float cross = (b.x - a.x) * (pz - a.z) - (b.z - a.z) * (px - a.x);
                    if (cross > 1e-6f) pos = true; else if (cross < -1e-6f) neg = true;
                }
                if (!(pos && neg)) grid[z * w + x] = id;
            }
    }

    static bool[] Flood(List<int> seeds, bool[] inside, int[] blocked, int w, int h)
    {
        var reach = new bool[inside.Length];
        var q = new Queue<int>();
        foreach (int s in seeds) if (blocked[s] == 0 && !reach[s]) { reach[s] = true; q.Enqueue(s); }
        while (q.Count > 0)
        {
            int c = q.Dequeue(); int x = c % w, z = c / w;
            if (x > 0) Try(c - 1); if (x < w - 1) Try(c + 1); if (z > 0) Try(c - w); if (z < h - 1) Try(c + w);
        }
        return reach;
        void Try(int j) { if (inside[j] && blocked[j] == 0 && !reach[j]) { reach[j] = true; q.Enqueue(j); } }
    }

    static bool[] Dilate(int[] grid, int w, int h, int r)
    {
        var o = new bool[grid.Length];
        for (int i = 0; i < grid.Length; i++)
        {
            if (grid[i] == 0) continue;
            int x = i % w, z = i / w;
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx >= 0 && nz >= 0 && nx < w && nz < h) o[nz * w + nx] = true;
                }
        }
        return o;
    }

    // a solid cell the van can actually touch: reachable ground right next to it
    static bool Beside(bool[] reach, int w, int h, int i, int r)
    {
        int x = i % w, z = i / w;
        for (int dz = -r; dz <= r; dz++)
            for (int dx = -r; dx <= r; dx++)
            {
                int nx = x + dx, nz = z + dz;
                if (nx >= 0 && nz >= 0 && nx < w && nz < h && reach[nz * w + nx]) return true;
            }
        return false;
    }

    static bool NextTo(bool[] reach, int w, int h, int i)
    {
        int x = i % w, z = i / w;
        return reach[i] || x > 0 && reach[i - 1] || x < w - 1 && reach[i + 1] || z > 0 && reach[i - w] || z < h - 1 && reach[i + w];
    }
}
