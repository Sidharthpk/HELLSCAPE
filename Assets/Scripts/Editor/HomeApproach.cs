using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// HellScape > Prologue: Home Approach. The way from the street to the house's front door was shut: a continuous
// low stone wall along the pavement (fence_part33, one mesh collider for the whole row), then a plaza edge 0.4-0.7 m
// up, both taller than the player steps. This cuts a 5 m gap in that wall (the mesh is copied without the gap's
// triangles, for the picture and the collider both) and puts one broad stone step at the plaza edge in front of it.
// The Prologue scene must be open. Run it again any time (it rebuilds its own pieces).
public static class HomeApproach
{
    const string Dir = "Assets/Generated/HomeApproach";
    const string StepName = "Home approach step";
    // the gap, in world x / z: in front of the front door, clear of the lamp post at x 27.8
    const float GapX0 = 22.0f, GapX1 = 27.2f, GapZ0 = -75.8f, GapZ1 = -70.8f;

    [MenuItem("HellScape/Prologue: Home Approach (wall gap + step)")]
    public static void Menu() => Debug.Log(Apply());

    public static string Apply()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var fence = GameObject.Find("fence_part33");
        var door = GameObject.Find("Front door (go home)");
        if (fence == null || door == null) return "Open the Prologue scene (fence_part33 / Front door (go home) not found).";
        System.IO.Directory.CreateDirectory(Dir);

        // ---- the wall: a copy of its mesh without the gap
        var mf = fence.GetComponent<MeshFilter>(); var mc = fence.GetComponent<MeshCollider>();
        string path = Dir + "/fence_part33_gap.asset";
        const string cityFbx = "Assets/Versatile Studio Assets/Demo City By Versatile Studio/Models/demo_city_by_versatile_studio.fbx";
        var original = AssetDatabase.LoadAllAssetsAtPath(cityFbx).OfType<Mesh>().FirstOrDefault(m => m.name == "fence_part33");
        if (original == null) return "Can't find the wall's original mesh in the city model.";
        var copy = CutGap(original, fence.transform, float.MinValue, out int removed);
        if (removed == 0) return "The gap box held no wall triangles (nothing to cut).";
        var src = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (src == null) { AssetDatabase.CreateAsset(copy, path); src = copy; }
        else { EditorUtility.CopySerialized(copy, src); Object.DestroyImmediate(copy); EditorUtility.SetDirty(src); }
        Undo.RecordObject(mf, "Wall gap"); Undo.RecordObject(mc, "Wall gap");
        mf.sharedMesh = src; mc.sharedMesh = null; mc.sharedMesh = src;

        // the same wall is in the Prologue's simplified city collision too (only what stands above the pavement is cut)
        var city = GameObject.Find("city_part_collider");
        if (city != null)
        {
            var cmc = city.GetComponent<MeshCollider>();
            var cityOriginal = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Generated/DemoCity/PrologueCityCollision.asset");
            if (cityOriginal != null)
            {
                var ccopy = CutGap(cityOriginal, city.transform, 4.07f, out int cremoved);
                string cpath = Dir + "/PrologueCityCollision_gap.asset";
                var csrc = AssetDatabase.LoadAssetAtPath<Mesh>(cpath);
                if (csrc == null) { AssetDatabase.CreateAsset(ccopy, cpath); csrc = ccopy; }
                else { EditorUtility.CopySerialized(ccopy, csrc); Object.DestroyImmediate(ccopy); EditorUtility.SetDirty(csrc); }
                Undo.RecordObject(cmc, "Wall gap");
                cmc.sharedMesh = null; cmc.sharedMesh = csrc;
            }
        }

        // ---- the step at the plaza edge, in front of the gap
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == StepName).ToArray()) Object.DestroyImmediate(t.gameObject);
        Physics.SyncTransforms();
        float cx = (GapX0 + GapX1) * 0.5f, width = GapX1 - GapX0;
        float edge = float.NaN, plaza = 0f;
        for (float z = -68f; z < -58f; z += 0.05f)
            if (Physics.Raycast(new Vector3(cx, 12f, z), Vector3.down, out var h, 20f, ~0, QueryTriggerInteraction.Ignore) && h.collider.name == "basement_mid" && h.point.y > 4f) { edge = z; plaza = h.point.y; break; }
        if (float.IsNaN(edge)) return "Wall gap cut; couldn't find the plaza edge for the step.";
        Physics.Raycast(new Vector3(cx, 12f, edge - 0.7f), Vector3.down, out var low, 20f, ~0, QueryTriggerInteraction.Ignore);
        float top = (low.point.y + plaza) * 0.5f;            // half way: two equal rises
        var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
        step.name = StepName;
        step.transform.SetParent(door.transform.root, true);
        float depth = 1.2f, bottom = low.point.y - 0.8f;
        step.transform.position = new Vector3(cx, (top + bottom) * 0.5f, edge - depth * 0.5f);
        step.transform.localScale = new Vector3(width, top - bottom, depth);
        var mr = step.GetComponent<MeshRenderer>(); mr.sharedMaterial = fence.GetComponent<MeshRenderer>().sharedMaterial;
        GameObjectUtility.SetStaticEditorFlags(step, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
        Undo.RegisterCreatedObjectUndo(step, "Home approach step");
        EditorSceneManager.MarkSceneDirty(fence.scene);
        return $"Wall gap cut at x {GapX0}..{GapX1}; step {width:F1} m wide at z {edge:F1} (ground {low.point.y:F2} -> step {top:F2} -> plaza {plaza:F2}).";
    }

    // Sutherland-Hodgman against one plane x = edge: side -1 keeps x <= edge, side +1 keeps x >= edge. The polygon is a list of
    // vertex indices into the growing lists; new vertices are interpolated (position, normal, uv) and appended.
    static List<int> Clip(List<int> poly, List<Vector3> v, List<Vector3> n, List<Vector2> u, Transform t, float edge, int side)
    {
        var res = new List<int>();
        for (int i = 0; i < poly.Count; i++)
        {
            int p = poly[i], q = poly[(i + 1) % poly.Count];
            float dp = side * (t.TransformPoint(v[p]).x - edge), dq = side * (t.TransformPoint(v[q]).x - edge);
            bool inP = dp >= 0f, inQ = dq >= 0f;
            if (inP) res.Add(p);
            if (inP != inQ)
            {
                float f = dp / (dp - dq);
                v.Add(Vector3.Lerp(v[p], v[q], f)); n.Add(Vector3.Lerp(n[p], n[q], f).normalized); u.Add(Vector2.Lerp(u[p], u[q], f));
                res.Add(v.Count - 1);
            }
        }
        return res;
    }

    // the wall's mesh with the gap clipped out of it (world space box)
    static Mesh CutGap(Mesh m, Transform t, float aboveY, out int removed)
    {
        removed = 0;
        var arr = Mesh.AcquireReadOnlyMeshData(m);
        var d = arr[0];
        var verts = new Vector3[d.vertexCount]; var nrm = new Vector3[d.vertexCount]; var uv = new Vector2[d.vertexCount];
        using (var na = new NativeArray<Vector3>(d.vertexCount, Allocator.Temp)) { d.GetVertices(na); na.CopyTo(verts); d.GetNormals(na); na.CopyTo(nrm); }
        if (d.HasVertexAttribute(VertexAttribute.TexCoord0)) using (var ua = new NativeArray<Vector2>(d.vertexCount, Allocator.Temp)) { d.GetUVs(0, ua); ua.CopyTo(uv); }
        var vl = new List<Vector3>(verts); var nl = new List<Vector3>(nrm); var ul = new List<Vector2>(uv);
        var keptBySub = new List<int[]>();
        for (int s = 0; s < d.subMeshCount; s++)
        {
            var sm = d.GetSubMesh(s);
            using (var ia = new NativeArray<int>(sm.indexCount, Allocator.Temp))
            {
                d.GetIndices(ia, s);
                var kept = new List<int>();
                for (int i = 0; i < ia.Length; i += 3)
                {
                    int a = ia[i], b = ia[i + 1], c = ia[i + 2];
                    Vector3 wa = t.TransformPoint(verts[a]), wb = t.TransformPoint(verts[b]), wc = t.TransformPoint(verts[c]);
                    float minX = Mathf.Min(wa.x, wb.x, wc.x), maxX = Mathf.Max(wa.x, wb.x, wc.x), minZ = Mathf.Min(wa.z, wb.z, wc.z), maxZ = Mathf.Max(wa.z, wb.z, wc.z);
                    float maxY = Mathf.Max(wa.y, wb.y, wc.y);
                    bool touches = maxX > GapX0 && minX < GapX1 && maxZ > GapZ0 && minZ < GapZ1 && maxY > aboveY;
                    if (!touches) { kept.Add(a); kept.Add(b); kept.Add(c); continue; }
                    removed++;
                    if (minZ < GapZ0 || maxZ > GapZ1) continue;   // (sticks out of the gap's depth: drop it whole)
                    // keep what lies outside the gap along x: the part left of it and the part right of it
                    var poly = new List<int> { a, b, c };
                    foreach (var side in new[] { -1, 1 })
                    {
                        var piece = Clip(poly, vl, nl, ul, t, side < 0 ? GapX0 : GapX1, side);
                        for (int k = 1; k + 1 < piece.Count; k++) { kept.Add(piece[0]); kept.Add(piece[k]); kept.Add(piece[k + 1]); }
                    }
                }
                keptBySub.Add(kept.ToArray());
            }
        }
        arr.Dispose();
        verts = vl.ToArray(); nrm = nl.ToArray(); uv = ul.ToArray();
        var copy = new Mesh { name = m.name + "_gap" };
        copy.indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        copy.vertices = verts; copy.normals = nrm; copy.uv = uv;
        copy.subMeshCount = keptBySub.Count;
        for (int s = 0; s < keptBySub.Count; s++) copy.SetTriangles(keptBySub[s], s);
        copy.RecalculateBounds();
        return copy;
    }
}
