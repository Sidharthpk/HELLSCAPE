using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// HellScape > Prologue > Cafe Low Poly: the Soca model (Assets/Perishables/cafe_soca) is ~395k triangles - every floor board
// and grain is modelled. This decimates each mesh by quadric edge collapse (down to 1/Ratio of its triangles, at least
// MinTris), drops the CAD edge-line submeshes, and flat-shades what is left. The result is saved as
// Assets/Generated/Cafe/CafeSocaLowPoly.prefab and swapped into the Prologue in place of "Cafe model (Soca)" (same place).
public static class CafeLowPoly
{
    const string Src = "Assets/Perishables/cafe_soca/scene.gltf", Dir = "Assets/Generated/Cafe", PrefabPath = Dir + "/CafeSocaLowPoly.prefab";
    const int Ratio = 20, MinTris = 300;

    [MenuItem("HellScape/Prologue/Cafe Low Poly")]
    public static void Build()
    {
        var sc = EditorSceneManager.GetSceneByName("Prologue");
        if (!sc.isLoaded) sc = EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity", OpenSceneMode.Additive);
        var old = Placed(sc);
        if (old == null) { Debug.LogError("Cafe low poly: no cafe model in the Prologue"); return; }
        // the place the model stands (it keeps it): deleting the prefab assets below resets a linked instance
        var parent = old.parent; int sib = old.GetSiblingIndex();
        Vector3 pos = old.position; Quaternion rot = old.rotation; Vector3 scale = old.localScale;
        Object.DestroyImmediate(old.gameObject);
        AssetDatabase.DeleteAsset(Dir);
        AssetDatabase.CreateFolder("Assets/Generated", "Cafe");
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(Src);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        long before = 0, after = 0; int i = 0;
        foreach (var f in inst.GetComponentsInChildren<MeshFilter>(true))
        {
            var r = f.GetComponent<Renderer>();
            var mesh = Simplify(f.sharedMesh, r.sharedMaterials, out var mats, ref before, ref after);
            mesh.name = "Cafe " + (i++) + " " + f.name;
            AssetDatabase.CreateAsset(mesh, Dir + "/" + mesh.name + ".asset");
            f.sharedMesh = mesh; r.sharedMaterials = mats;
        }
        foreach (var t in Textures("Assets/Perishables/cafe_soca/textures"))
            if (t.maxTextureSize > 256) { t.maxTextureSize = 256; t.SaveAndReimport(); }
        var prefab = PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
        Object.DestroyImmediate(inst);
        Debug.Log("Cafe low poly: " + before + " -> " + after + " triangles, " + prefab.name);
        var n = (GameObject)PrefabUtility.InstantiatePrefab(prefab, sc);
        n.name = "Cafe model (Soca low poly)";
        n.transform.SetParent(parent, true);
        n.transform.SetPositionAndRotation(pos, rot); n.transform.localScale = scale; n.transform.SetSiblingIndex(sib);
        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
    }

    static Transform Placed(UnityEngine.SceneManagement.Scene sc) =>
        sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name.StartsWith("Cafe model (Soca"));

    static IEnumerable<TextureImporter> Textures(string dir) =>
        AssetDatabase.FindAssets("t:Texture2D", new[] { dir }).Select(g => AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(g)) as TextureImporter).Where(t => t != null);

    static Mesh Simplify(Mesh m, Material[] mats, out Material[] outMats, ref long before, ref long after)
    {
        var v = m.vertices; var uv = m.uv;
        var ids = new List<int>(); var sub = new List<int>(); var keep = new List<Material>();
        for (int s = 0; s < m.subMeshCount; s++)
        {
            if (m.GetTopology(s) != MeshTopology.Triangles) continue;
            var t = m.GetTriangles(s);
            ids.AddRange(t);
            for (int k = 0; k < t.Length; k += 3) sub.Add(keep.Count);
            keep.Add(mats[Mathf.Min(s, mats.Length - 1)]);
        }
        int total = ids.Count / 3;
        before += total;
        var pos = new Vector3[ids.Count]; var cuv = new Vector2[ids.Count];
        for (int k = 0; k < ids.Count; k++) { pos[k] = v[ids[k]]; cuv[k] = uv.Length > ids[k] ? uv[ids[k]] : Vector2.zero; }
        var q = new Qem(pos, cuv, sub.ToArray(), m.bounds.size.magnitude);
        q.Run(Mathf.Min(total, Mathf.Max(MinTris, total / Ratio)));
        var vs = new List<Vector3>(); var us = new List<Vector2>(); var ns = new List<Vector3>();
        var idx = keep.Select(_ => new List<int>()).ToList();
        q.Emit((s, a, b, c, ua, ub, uc) =>
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            idx[s].Add(vs.Count); idx[s].Add(vs.Count + 1); idx[s].Add(vs.Count + 2);
            vs.Add(a); vs.Add(b); vs.Add(c); us.Add(ua); us.Add(ub); us.Add(uc); ns.Add(n); ns.Add(n); ns.Add(n);
        });
        after += vs.Count / 3;
        var mesh = new Mesh { indexFormat = vs.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        mesh.SetVertices(vs); mesh.SetUVs(0, us); mesh.SetNormals(ns); mesh.subMeshCount = idx.Count;
        for (int s = 0; s < idx.Count; s++) mesh.SetTriangles(idx[s], s);
        mesh.RecalculateBounds();
        outMats = keep.ToArray();
        return mesh;
    }

    // Garland-Heckbert quadric edge collapse. Positions are welded (UVs stay on the triangle corners, so seams survive),
    // border edges get a stiff plane so the outline holds, and a collapse that would flip a neighbouring face is refused.
    sealed class Qem
    {
        readonly Vector3[] p; readonly int[] f; readonly Vector2[] fuv; readonly int[] fsub; readonly bool[] dead;
        readonly double[][] q; readonly List<int>[] vf; readonly int[] ver;
        readonly List<(int a, int b, int va, int vb, Vector3 at)> edges = new List<(int, int, int, int, Vector3)>();
        readonly SortedSet<(double cost, int id)> heap = new SortedSet<(double, int)>();
        int alive;

        public Qem(Vector3[] corners, Vector2[] cuv, int[] sub, float size)
        {
            fuv = cuv; fsub = sub; alive = sub.Length; dead = new bool[alive];
            f = new int[corners.Length];
            var weld = new Dictionary<(int, int, int), int>(); var pl = new List<Vector3>();
            float inv = 1f / Mathf.Max(1e-6f, size * 1e-6f);
            for (int k = 0; k < corners.Length; k++)
            {
                var key = (Mathf.RoundToInt(corners[k].x * inv), Mathf.RoundToInt(corners[k].y * inv), Mathf.RoundToInt(corners[k].z * inv));
                if (!weld.TryGetValue(key, out int id)) { id = pl.Count; weld[key] = id; pl.Add(corners[k]); }
                f[k] = id;
            }
            p = pl.ToArray(); q = new double[p.Length][]; vf = new List<int>[p.Length]; ver = new int[p.Length];
            for (int i = 0; i < p.Length; i++) { q[i] = new double[10]; vf[i] = new List<int>(); }
            var edgeFaces = new Dictionary<long, int>();
            for (int t = 0; t < dead.Length; t++)
            {
                int a = f[t * 3], b = f[t * 3 + 1], c = f[t * 3 + 2];
                if (a == b || b == c || a == c) { dead[t] = true; continue; }
                vf[a].Add(t); vf[b].Add(t); vf[c].Add(t);
                Vector3 n = Vector3.Cross(p[b] - p[a], p[c] - p[a]); double area = n.magnitude * .5;
                for (int e = 0; e < 3; e++) { long k = Key(f[t * 3 + e], f[t * 3 + (e + 1) % 3]); edgeFaces[k] = edgeFaces.TryGetValue(k, out int cnt) ? cnt + 1 : 1; }
                if (area < 1e-12) continue;
                Plane(new[] { a, b, c }, n.normalized, p[a], area);
            }
            alive = dead.Count(d => !d);
            for (int t = 0; t < dead.Length; t++)
            {
                if (dead[t]) continue;
                Vector3 n = Vector3.Cross(p[f[t * 3 + 1]] - p[f[t * 3]], p[f[t * 3 + 2]] - p[f[t * 3]]).normalized;
                for (int e = 0; e < 3; e++)
                {
                    int a = f[t * 3 + e], b = f[t * 3 + (e + 1) % 3];
                    if (edgeFaces[Key(a, b)] != 1) continue;
                    Vector3 d = p[b] - p[a]; Vector3 bn = Vector3.Cross(d, n);
                    if (bn.sqrMagnitude < 1e-12f) continue;
                    Plane(new[] { a, b }, bn.normalized, p[a], 50.0 * d.sqrMagnitude);
                }
            }
            var seen = new HashSet<long>();
            for (int t = 0; t < dead.Length; t++)
                if (!dead[t]) for (int e = 0; e < 3; e++) { int a = f[t * 3 + e], b = f[t * 3 + (e + 1) % 3]; if (seen.Add(Key(a, b))) Push(a, b); }
        }

        static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        void Plane(int[] vs, Vector3 n, Vector3 on, double w)
        {
            double a = n.x, b = n.y, c = n.z, d = -Vector3.Dot(n, on);
            double[] pp = { a * a, a * b, a * c, a * d, b * b, b * c, b * d, c * c, c * d, d * d };
            foreach (int v in vs) for (int i = 0; i < 10; i++) q[v][i] += w * pp[i];
        }

        static double Cost(double[] m, Vector3 v)
        {
            double x = v.x, y = v.y, z = v.z;
            return m[0] * x * x + 2 * m[1] * x * y + 2 * m[2] * x * z + 2 * m[3] * x + m[4] * y * y + 2 * m[5] * y * z + 2 * m[6] * y + m[7] * z * z + 2 * m[8] * z + m[9];
        }

        void Push(int a, int b)
        {
            if (a == b) return;
            var s = new double[10]; for (int i = 0; i < 10; i++) s[i] = q[a][i] + q[b][i];
            Vector3 best = p[a]; double bc = Cost(s, best);
            foreach (var c in new[] { p[b], (p[a] + p[b]) * .5f }) { double k = Cost(s, c); if (k < bc) { bc = k; best = c; } }
            edges.Add((a, b, ver[a], ver[b], best));
            heap.Add((bc, edges.Count - 1));
        }

        public void Run(int target)
        {
            while (alive > target && heap.Count > 0)
            {
                var top = heap.Min; heap.Remove(top);
                var (a, b, va, vb, at) = edges[top.id];
                if (ver[a] != va || ver[b] != vb) continue;
                if (Flips(a, b, at)) continue;
                p[a] = at; for (int i = 0; i < 10; i++) q[a][i] += q[b][i];
                ver[b] = -1; ver[a]++;
                foreach (int t in vf[b])
                {
                    if (dead[t]) continue;
                    for (int k = 0; k < 3; k++) if (f[t * 3 + k] == b) f[t * 3 + k] = a;
                    int x = f[t * 3], y = f[t * 3 + 1], z = f[t * 3 + 2];
                    if (x == y || y == z || x == z) { dead[t] = true; alive--; } else vf[a].Add(t);
                }
                vf[b].Clear();
                vf[a].RemoveAll(t => dead[t]);
                var nb = new HashSet<int>();
                foreach (int t in vf[a]) for (int k = 0; k < 3; k++) if (f[t * 3 + k] != a) nb.Add(f[t * 3 + k]);
                foreach (int n in nb) Push(a, n);
            }
        }

        bool Flips(int a, int b, Vector3 at)
        {
            foreach (int t in vf[a].Concat(vf[b]))
            {
                if (dead[t]) continue;
                int x = f[t * 3], y = f[t * 3 + 1], z = f[t * 3 + 2];
                if ((x == a || y == a || z == a) && (x == b || y == b || z == b)) continue;   // goes away with the edge
                Vector3 nb = Vector3.Cross(p[y] - p[x], p[z] - p[x]);
                Vector3 px = x == a || x == b ? at : p[x], py = y == a || y == b ? at : p[y], pz = z == a || z == b ? at : p[z];
                Vector3 na = Vector3.Cross(py - px, pz - px);
                if (na.sqrMagnitude < 1e-14f || Vector3.Dot(nb.normalized, na.normalized) < .2f) return true;
            }
            return false;
        }

        public void Emit(System.Action<int, Vector3, Vector3, Vector3, Vector2, Vector2, Vector2> add)
        {
            for (int t = 0; t < dead.Length; t++)
            {
                if (dead[t]) continue;
                add(fsub[t], p[f[t * 3]], p[f[t * 3 + 1]], p[f[t * 3 + 2]], fuv[t * 3], fuv[t * 3 + 1], fuv[t * 3 + 2]);
            }
        }
    }
}
