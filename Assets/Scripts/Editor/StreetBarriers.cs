using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > Map > Build Street Barriers: the street as one clean channel. Wherever the van could leave the road
// between two buildings (side alleys, the ends of a block, beside the tunnel mouth, the bridge approach) a row of
// concrete road barriers closes the gap: something you can see, with a collider to match.
//   - found automatically: walking the drive line, looking sideways; where no wall is in reach, the gap is closed
//     from the last wall before it to the first wall after it
//   - plus hand-placed runs per scene (Extra below) for the spots a sideways look can't find
// Everything lives under the scene root "Street barriers (generated)"; rerun after the map changes.
public static class StreetBarriers
{
    public const string RootName = "Street barriers (generated)";
    const string Folder = "Assets/Generated/MapColliders";
    const float Reach = 26f;          // further than this to a wall = a gap
    const float Height = 1.0f, ColliderHeight = 2.2f;

    // hand-placed runs: scene name -> pairs of ends (x, z)
    static readonly Dictionary<string, Vector2[]> Extra = new Dictionary<string, Vector2[]>
    {
        // the bridge mouth: from the last building each side to where the bridge's own rail begins (the block's edge
        // there drops straight into the river)
        { "MainGameScene", new[] { new Vector2(177.2f, -29.6f), new Vector2(187.9f, -34.1f),   new Vector2(197.7f, -46.2f), new Vector2(195.0f, -54.4f) } },
    };


    [MenuItem("HellScape/Map/Build Street Barriers")]
    public static void Menu()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded) Debug.Log(s.name + ": " + Build(s));
        }
    }

    public static string Build(Scene scene)
    {
        var line = MapRoutes.StreetLine(scene);
        if (line == null || line.Length < 2) return "no drive line";
        var oldRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (oldRoot != null) Object.DestroyImmediate(oldRoot);
        Physics.SyncTransforms();
        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        var mat = Material();

        var runs = new List<(Vector3 a, Vector3 b, string name)>();
        var garage = GarageBuilder.Find(scene);
        // ---------------------------------------------------------------- gaps either side of the drive line
        for (int side = -1; side <= 1; side += 2)
        {
            // (bridge: it has rails of its own, its mouth is closed by hand below. garage: its open door isn't a gap)
            bool inGap = false, had = false, lastSkip = false, fromSkip = false; Vector3 last = Vector3.zero, gapFrom = Vector3.zero;
            for (int i = 0; i < line.Length - 1; i++)
            {
                Vector3 a = line[i], b = line[i + 1];
                float len = Vector3.Distance(a, b);
                Vector3 dir = (b - a).normalized, sd = Vector3.Cross(Vector3.up, dir) * side;
                for (float t = 0f; t < len; t += 1f)
                {
                    Vector3 p = a + dir * t + Vector3.up * 1.2f;
                    bool hit = Wall(scene, p, sd, Reach, out Vector3 at, out Collider col);
                    bool skip = hit && (col.GetComponentsInParent<Transform>().Any(x => x.name.StartsWith("TEXTURES_FOR_SKETCH")) || garage != null && col.transform.IsChildOf(garage));
                    if (!hit && !inGap) { inGap = true; gapFrom = last; fromSkip = lastSkip; }
                    else if (hit && inGap)
                    {
                        inGap = false;
                        if (had && !skip && !fromSkip && Vector3.Distance(gapFrom, at) < 45f) runs.Add((gapFrom, at, "Barrier"));
                    }
                    if (hit) { last = at; had = true; lastSkip = skip; }
                }
            }
        }
        // the hell city: the street is closed just up the road from the garage, so the only way out of it is the
        // escape route. From the garage's corner across to the far side, with room to walk round the end of it.
        var corners = garage != null ? GarageBuilder.DoorCorners(garage) : new Vector3[0];
        if (garage != null && Object.FindObjectsByType<RoadObstacleSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(r => r.gameObject.scene == scene) && line.Length > 1)
        {
            Vector3 up = Flat(line[0] - line[1]).normalized;                    // back up the street, away from the escape
            Vector3 p = line[0] + up * 7f + Vector3.up * 1.2f;
            Vector3 away = Vector3.Cross(Vector3.up, up);
            if (Vector3.Dot(away, garage.position - p) > 0f) away = -away;
            Vector3 corner = corners.OrderByDescending(c => Vector3.Dot(c - garage.position, up)).First();
            if (Wall(scene, p, away, 30f, out Vector3 far, out Collider _)) runs.Add((corner, far, "Barrier (road block)"));
        }
        if (Extra.TryGetValue(scene.name, out var ends))
            for (int i = 0; i + 1 < ends.Length; i += 2)
                runs.Add((new Vector3(ends[i].x, 0f, ends[i].y), new Vector3(ends[i + 1].x, 0f, ends[i + 1].y), "Barrier (placed)"));

        int n = 0;
        foreach (var (ra, rb, name) in runs)
        {
            Vector3 a = Ground(scene, ra, line), b = Ground(scene, rb, line);
            Vector3 d = Flat(b - a);
            if (d.magnitude < 0.6f) continue;
            // reach a little into the walls at both ends so no sliver is left open...
            a -= d.normalized * 0.6f; b += d.normalized * 0.6f;
            // ...except at the garage's own corners: a gap a person fits through and the van doesn't
            foreach (var c in corners)
            {
                if (Flat(a - c).magnitude < 1.6f) a += d.normalized * 2.1f;
                if (Flat(b - c).magnitude < 1.6f) b -= d.normalized * 2.1f;
            }
            float length = Flat(b - a).magnitude;
            var go = new GameObject($"{name} {n++}");
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(a, Quaternion.LookRotation(Flat(b - a).normalized));
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = Mesh(length, b.y - a.y);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, ColliderHeight * 0.5f + Mathf.Min(0f, b.y - a.y) * 0.5f - 0.2f, length * 0.5f);
            box.size = new Vector3(0.6f, ColliderHeight + Mathf.Abs(b.y - a.y) + 0.4f, length);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Physics.SyncTransforms();
        return $"{n} barrier runs ({runs.Count(r => r.name == "Barrier")} gaps closed, {runs.Count(r => r.name != "Barrier")} placed)";
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    static bool Wall(Scene scene, Vector3 from, Vector3 dir, float reach, out Vector3 at, out Collider col)
    {
        float best = reach; at = from + dir * reach; bool any = false; col = null;
        foreach (var h in Physics.RaycastAll(from, dir, reach, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.gameObject.scene != scene || h.collider.attachedRigidbody != null || MapAudit.Skip(h.collider.transform)) continue;
            if (h.distance < best) { best = h.distance; at = h.point; any = true; col = h.collider; }
        }
        return any;
    }

    // the ground under a point (the highest thing near road level)
    static Vector3 Ground(Scene scene, Vector3 p, Vector3[] line)
    {
        float yRef = line.OrderBy(l => Flat(l - p).sqrMagnitude).First().y;
        float best = float.NaN;
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, yRef + 0.6f, p.z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.gameObject.scene != scene || h.collider.attachedRigidbody != null || MapAudit.Skip(h.collider.transform)) continue;
            if (Mathf.Abs(h.point.y - yRef) > 1f) continue;   // not the sea under the bridge approach
            if (float.IsNaN(best) || h.point.y > best) best = h.point.y;
        }
        return new Vector3(p.x, float.IsNaN(best) ? yRef : best, p.z);
    }

    // a road barrier's section pushed along z, the far end raised by 'rise'
    static Mesh Mesh(float length, float rise)
    {
        // half profile, mirrored: foot, shoulder, neck, top
        var prof = new[] { new Vector2(-0.3f, -0.4f), new Vector2(-0.3f, 0.16f), new Vector2(-0.13f, 0.48f), new Vector2(-0.09f, Height), new Vector2(0.09f, Height), new Vector2(0.13f, 0.48f), new Vector2(0.3f, 0.16f), new Vector2(0.3f, -0.4f) };
        float[] vAt = new float[prof.Length];
        for (int i = 1; i < prof.Length; i++) vAt[i] = vAt[i - 1] + (prof[i] - prof[i - 1]).magnitude;
        float vLen = vAt[prof.Length - 1];
        int segs = Mathf.Max(1, Mathf.RoundToInt(length / 2f));
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        for (int s = 0; s <= segs; s++)
        {
            float t = (float)s / segs;
            for (int i = 0; i < prof.Length; i++)
            {
                verts.Add(new Vector3(prof[i].x, prof[i].y + rise * t, length * t));
                // the texture: v up the face (foot at the bottom on both sides), u one block every 2 m
                float v = i < 4 ? vAt[i] / vAt[3] : (vLen - vAt[i]) / (vLen - vAt[4]);
                uvs.Add(new Vector2(s, i == 3 || i == 4 ? 1f : Mathf.Clamp01(v)));
            }
        }
        for (int s = 0; s < segs; s++)
            for (int i = 0; i < prof.Length - 1; i++)
            {
                int a = s * prof.Length + i, b = a + 1, c = a + prof.Length, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);
            }
        // end caps
        foreach (int s in new[] { 0, segs })
        {
            int start = verts.Count;
            for (int i = 0; i < prof.Length; i++)
            {
                verts.Add(new Vector3(prof[i].x, prof[i].y + rise * (s == 0 ? 0f : 1f), s == 0 ? 0f : length));
                uvs.Add(new Vector2(prof[i].x + 0.5f, Mathf.Clamp01((prof[i].y + 0.4f) / (Height + 0.4f)) * 0.6f));
            }
            for (int i = 1; i < prof.Length - 1; i++)
            {
                if (s == 0) { tris.Add(start); tris.Add(start + i); tris.Add(start + i + 1); }
                else { tris.Add(start); tris.Add(start + i + 1); tris.Add(start + i); }
            }
        }
        var m = new Mesh { name = $"Road barrier {length:0.0} m" };
        m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // weathered concrete with a red and white warning band along the top, small and unfiltered like the rest of the city
    static Material Material()
    {
        string matPath = Folder + "/RoadBarrier.mat", texPath = Folder + "/RoadBarrier.png";
        Directory.CreateDirectory(Folder);
        if (!File.Exists(texPath))
        {
            const int W = 64, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            var rnd = new System.Random(1304);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float n = 0.5f + ((float)rnd.NextDouble() - 0.5f) * 0.16f + Mathf.PerlinNoise(x * 0.11f, y * 0.13f) * 0.12f;
                    Color c = new Color(n, n * 0.98f, n * 0.94f);
                    float v = (float)y / H;
                    if (v < 0.22f) c *= Mathf.Lerp(0.55f, 1f, v / 0.22f);                 // grime at the foot
                    if (v > 0.72f && v < 0.94f)                                            // the warning band
                    {
                        bool red = ((x + y) / 8) % 2 == 0;
                        c = (red ? new Color(0.62f, 0.1f, 0.08f) : new Color(0.82f, 0.8f, 0.74f)) * (0.8f + (float)rnd.NextDouble() * 0.25f);
                    }
                    if (x == 0 || x == W - 1) c *= 0.6f;                                   // the joint between blocks
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.filterMode = FilterMode.Point; imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.05f);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
