using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > Map > Rebuild Street Colliders: what the van (and you) can hit along the street, made to match what
// you can see. For every street block (the "сити исчерп на скетч" pieces) in the open scenes:
//   - ground: the road's paper-thin box is given depth, the dirt verges (no collider: wheels sank to road level)
//     get one, and the kerbs' vertical faces become ramps in the collider so the van rides up instead of stopping dead
//   - everything standing at van height (buildings, lamp posts, bollards, bins): the loose mesh-bounds boxes are
//     replaced by boxes fitted to the footprint at street level, one per separate piece, turned to fit
// The boxes live under each block's "Colliders (generated)" child; rerun after moving buildings.
public static class MapColliders
{
    public const string Container = "Colliders (generated)";
    const string MeshFolder = "Assets/Generated/MapColliders";
    const float BandLo = 0.3f, BandHi = 2.6f;       // above the road: where the van's body is
    const float RampDrop = 0.6f, RampRun = 1.0f;    // kerb faces lean out this much (31 degrees)

    [MenuItem("HellScape/Map/Rebuild Street Colliders")]
    public static void Menu()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded) Debug.Log(s.name + ": " + Build(s));
        }
    }

    public static IEnumerable<Transform> Blocks(Scene scene) =>
        Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
              .Where(t => t.gameObject.scene == scene && t.name.StartsWith("сити исчерп на скетч") && t.GetComponentInChildren<MeshFilter>() != null);

    public static string Build(Scene scene)
    {
        int ground = 0, ramps = 0, boxes = 0, pieces = 0;
        foreach (var block in Blocks(scene).ToArray())
        {
            var old = block.Find(Container);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var holder = new GameObject(Container).transform;
            holder.SetParent(block, false);

            var road = block.Find("Plane");
            float roadY = road != null && road.GetComponent<Renderer>() != null ? road.GetComponent<Renderer>().bounds.max.y : block.position.y - 0.08f;

            foreach (Transform child in block)
            {
                if (child == holder || !child.gameObject.activeSelf) continue;
                var mf = child.GetComponent<MeshFilter>();
                var mr = child.GetComponent<MeshRenderer>();
                if (mf == null || mr == null || mf.sharedMesh == null) continue;
                Bounds b = mr.bounds;
                if (b.max.y < roadY + 0.75f) { if (Ground(child, mf.sharedMesh, ref ramps)) ground++; }
                else if (b.min.y < roadY + BandHi)
                {
                    int n = Solid(child, mf.sharedMesh, roadY, holder);
                    if (n > 0) { boxes += n; pieces++; }
                }
            }
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Physics.SyncTransforms();
        return $"{ground} ground pieces fixed ({ramps} kerbs ramped), {boxes} fitted boxes on {pieces} street pieces";
    }

    // ---------------------------------------------------------------- the ground

    static bool Ground(Transform t, Mesh mesh, ref int ramps)
    {
        var col = t.GetComponent<Collider>();
        if (col == null)
        {
            t.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            return true;
        }
        if (col is BoxCollider box)
        {
            // a sheet of paper for a road: give it two metres of depth, top face where it was
            Vector3 up = t.InverseTransformDirection(Vector3.up);
            int axis = Mathf.Abs(up.x) > Mathf.Abs(up.y) ? (Mathf.Abs(up.x) > Mathf.Abs(up.z) ? 0 : 2) : (Mathf.Abs(up.y) > Mathf.Abs(up.z) ? 1 : 2);
            float scale = Mathf.Abs(t.lossyScale[axis]);
            if (box.size[axis] * scale > 0.5f) return false;
            Bounds mb = mesh.bounds;
            float depth = 2f / scale, sign = Mathf.Sign(up[axis]);
            float top = sign > 0f ? mb.max[axis] : mb.min[axis];
            Vector3 size = box.size, centre = box.center;
            size[axis] = depth; centre[axis] = top - sign * depth * 0.5f;
            box.size = size; box.center = centre;
            EditorUtility.SetDirty(box);
            return true;
        }
        if (col is MeshCollider mc)
        {
            var ramp = RampMesh(mesh, t);
            if (ramp == null) { if (mc.sharedMesh != mesh) mc.sharedMesh = mesh; return false; }
            mc.sharedMesh = ramp;
            EditorUtility.SetDirty(mc);
            ramps++;
            return true;
        }
        return false;
    }

    // the same mesh with every vertical face (a kerb) leaned outward at the bottom: a ramp the wheels can take
    static Mesh RampMesh(Mesh src, Transform t)
    {
        float scale = Mathf.Abs(t.lossyScale.x);
        string path = $"{MeshFolder}/{Safe(src.name)}_{src.vertexCount}v_ramp_{Mathf.RoundToInt(scale * 100f)}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

        Vector3 up = t.InverseTransformDirection(Vector3.up).normalized;
        var v = src.vertices;
        var tris = src.triangles;
        var moved = new Dictionary<Vector3Int, List<Vector3>>();   // low vertex (welded) -> the outward normals of its kerb faces
        var topOf = new Dictionary<Vector3Int, float>();           // ...and the height of the kerb top above it
        Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 2000f), Mathf.RoundToInt(p.y * 2000f), Mathf.RoundToInt(p.z * 2000f));
        bool any = false;
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            if (Mathf.Abs(Vector3.Dot(n, up)) > 0.2f) continue;
            n = (n - up * Vector3.Dot(n, up)).normalized;
            float ha = Vector3.Dot(a, up), hb = Vector3.Dot(b, up), hc = Vector3.Dot(c, up);
            float top = Mathf.Max(ha, Mathf.Max(hb, hc));
            if ((top - Mathf.Min(ha, Mathf.Min(hb, hc))) * scale < 0.1f) continue;
            foreach (var (p, h) in new[] { (a, ha), (b, hb), (c, hc) })
            {
                if ((top - h) * scale < 0.05f) continue;   // on the kerb's top edge: stays
                var k = Key(p);
                if (!moved.TryGetValue(k, out var list)) { list = new List<Vector3>(); moved[k] = list; topOf[k] = top; }
                if (!list.Any(o => Vector3.Angle(o, n) < 5f)) list.Add(n);
                topOf[k] = Mathf.Max(topOf[k], top);
                any = true;
            }
        }
        if (!any) return null;
        if (existing != null) return existing;

        float drop = RampDrop / scale, run = RampRun / scale;
        var nv = (Vector3[])v.Clone();
        for (int i = 0; i < nv.Length; i++)
        {
            var k = Key(v[i]);
            if (!moved.TryGetValue(k, out var normals)) continue;
            Vector3 push;
            if (normals.Count == 2) push = (normals[0] + normals[1]) / Mathf.Max(0.3f, 1f + Vector3.Dot(normals[0], normals[1]));   // a corner: mitre
            else { push = Vector3.zero; foreach (var n in normals) push += n; push = push.normalized; }
            float h = Vector3.Dot(v[i], up);
            nv[i] = v[i] + up * (topOf[k] - drop - h) + push * run;
        }
        var mesh = new Mesh { name = src.name + " (kerb ramps)" };
        mesh.vertices = nv;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        Directory.CreateDirectory(MeshFolder);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static string Safe(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

    // ---------------------------------------------------------------- things standing in the street

    const float Coarse = 0.5f;

    static int Solid(Transform t, Mesh mesh, float roadY, Transform holder)
    {
        // the mesh's outline at van height, in world space
        var m = t.localToWorldMatrix;
        var v = mesh.vertices;
        var world = new Vector3[v.Length];
        for (int i = 0; i < v.Length; i++) world[i] = m.MultiplyPoint3x4(v[i]);
        var tris = mesh.triangles;
        float lo = roadY + BandLo, hi = roadY + BandHi;
        var cells = new Dictionary<Vector2Int, List<Vector2>>();
        var poly = new List<Vector3>(8); var tmp = new List<Vector3>(8);
        void Add(Vector3 p)
        {
            var k = new Vector2Int(Mathf.FloorToInt(p.x / Coarse), Mathf.FloorToInt(p.z / Coarse));
            if (!cells.TryGetValue(k, out var list)) { list = new List<Vector2>(4); cells[k] = list; }
            if (list.Count < 24) list.Add(new Vector2(p.x, p.z));
        }
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = world[tris[i]], b = world[tris[i + 1]], c = world[tris[i + 2]];
            if (Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < lo || Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > hi) continue;
            poly.Clear(); poly.Add(a); poly.Add(b); poly.Add(c);
            ClipY(poly, tmp, lo, true); ClipY(poly, tmp, hi, false);
            if (poly.Count < 2) continue;
            for (int e = 0; e < poly.Count; e++)
            {
                Vector3 p = poly[e], q = poly[(e + 1) % poly.Count];
                float len = Mathf.Sqrt((q.x - p.x) * (q.x - p.x) + (q.z - p.z) * (q.z - p.z));
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / (Coarse * 0.5f)));
                for (int s = 0; s <= steps; s++) Add(Vector3.Lerp(p, q, (float)s / steps));
            }
        }
        if (cells.Count == 0) return 0;

        foreach (var c in t.GetComponents<Collider>()) Object.DestroyImmediate(c);

        // separate pieces (three lamp posts in one mesh) each get their own box
        float top = Mathf.Min(t.GetComponent<Renderer>().bounds.max.y, roadY + 40f), bottom = roadY - 0.5f;
        var seen = new HashSet<Vector2Int>();
        var stack = new Stack<Vector2Int>();
        int count = 0;
        foreach (var start in cells.Keys.ToArray())
        {
            if (!seen.Add(start)) continue;
            var pts = new List<Vector2>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                pts.AddRange(cells[c]);
                for (int dz = -2; dz <= 2; dz++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        var k = new Vector2Int(c.x + dx, c.y + dz);
                        if (cells.ContainsKey(k) && seen.Add(k)) stack.Push(k);
                    }
            }
            MinRect(pts, out Vector2 centre, out Vector2 size, out float yaw);
            // a piece that only reaches into the band (a post) is as tall as what's standing there
            var go = new GameObject(t.name + (count > 0 ? " box " + count : " box"));
            go.transform.SetParent(holder, false);
            go.transform.SetPositionAndRotation(new Vector3(centre.x, (top + bottom) * 0.5f, centre.y), Quaternion.Euler(0f, yaw, 0f));
            Vector3 ls = holder.lossyScale;
            go.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
            go.isStatic = true;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(Mathf.Max(size.x, 0.45f), top - bottom, Mathf.Max(size.y, 0.45f));
            count++;
        }
        return count;
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

    // the smallest rectangle (any turn) around the points
    public static void MinRect(List<Vector2> pts, out Vector2 centre, out Vector2 size, out float yaw)
    {
        var hull = Hull(pts);
        float best = float.MaxValue; centre = hull[0]; size = Vector2.one * 0.45f; yaw = 0f;
        if (hull.Count < 3)
        {
            Vector2 a = hull[0], b = hull[hull.Count - 1];
            centre = (a + b) * 0.5f; size = new Vector2(0.45f, Mathf.Max(0.45f, (b - a).magnitude));
            yaw = (b - a).sqrMagnitude > 1e-6f ? Mathf.Atan2(b.x - a.x, b.y - a.y) * Mathf.Rad2Deg : 0f;
            return;
        }
        for (int i = 0; i < hull.Count; i++)
        {
            Vector2 d = (hull[(i + 1) % hull.Count] - hull[i]);
            if (d.sqrMagnitude < 1e-8f) continue;
            d.Normalize();
            Vector2 n = new Vector2(-d.y, d.x);
            float minD = float.MaxValue, maxD = float.MinValue, minN = float.MaxValue, maxN = float.MinValue;
            foreach (var p in hull)
            {
                float pd = Vector2.Dot(p, d), pn = Vector2.Dot(p, n);
                minD = Mathf.Min(minD, pd); maxD = Mathf.Max(maxD, pd); minN = Mathf.Min(minN, pn); maxN = Mathf.Max(maxN, pn);
            }
            float area = (maxD - minD) * (maxN - minN);
            if (area >= best) continue;
            best = area;
            centre = d * ((minD + maxD) * 0.5f) + n * ((minN + maxN) * 0.5f);
            // box z along d, x along n
            size = new Vector2(maxN - minN, maxD - minD);
            yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
        }
    }

    static List<Vector2> Hull(List<Vector2> pts)
    {
        var p = pts.Distinct().OrderBy(a => a.x).ThenBy(a => a.y).ToList();
        if (p.Count < 3) return p;
        float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        var h = new List<Vector2>();
        foreach (var q in p) { while (h.Count >= 2 && Cross(h[h.Count - 2], h[h.Count - 1], q) <= 0f) h.RemoveAt(h.Count - 1); h.Add(q); }
        int lower = h.Count + 1;
        for (int i = p.Count - 2; i >= 0; i--) { var q = p[i]; while (h.Count >= lower && Cross(h[h.Count - 2], h[h.Count - 1], q) <= 0f) h.RemoveAt(h.Count - 1); h.Add(q); }
        h.RemoveAt(h.Count - 1);
        return h;
    }
}
