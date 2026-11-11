using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// HellScape > Cars > Swap Van To Volkswagen T6: the drivable van (the PrometeoCarController object in each scene) gets the
// user's T6 model instead of the Ducato. The T6 file is a bare mesh dump: no rig, its four wheels are ONE mesh per part,
// and its materials import without textures. So:
//   - Build(): once, writes Assets/Generated/VW/VolkswagenT6.prefab: the body scaled to a real 5 m T6 with its wheels on the
//     ground and the axles centred on the origin, each wheel cut out of the merged wheel meshes into its own object centred
//     on its axle (so it can spin), and clean URP materials (paint, plastic, tyre, glass, ...) made from its textures
//   - Swap(car): the old van's looks go, the prefab goes in; its wheels become the pivots the controller spins/steers,
//     and the wheel colliders, body box, get-in box, exit and chase camera are fitted to it. Physics, story hooks and
//     CarInteract/CarHealth stay on the car as they were.
public static class VolkswagenBuilder
{
    const string Dir = "Assets/Models/volkswagen-van-t6/";
    const string Fbx = Dir + "source/T6_Wolkwagen.fbx";
    const string Out = "Assets/Generated/VW/";
    const string PrefabPath = Out + "VolkswagenT6.prefab";
    const float Length = 5.0f;                  // a real T6 is 4.9 - 5.3 m
    static readonly string[] WheelNames = { "Wheel.Ft.L", "Wheel.Ft.R", "Wheel.Bk.L", "Wheel.Bk.R" };
    static readonly string[] Scenes = { "Assets/Scenes/MainGameScene.unity", "Assets/Scenes/Prologue.unity", "Assets/Scenes/OfficeSilo.unity" };

    [MenuItem("HellScape/Cars/Swap Van To Volkswagen T6")]
    public static void Menu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
        string made = Build();
        if (made != null) { Debug.LogWarning(made); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        int n = 0;
        var open = new HashSet<string>();
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var s = EditorSceneManager.GetSceneAt(i);
            open.Add(s.path);
            foreach (var pc in Object.FindObjectsByType<PrometeoCarController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.gameObject.scene == s)) { Swap(pc, prefab); n++; EditorSceneManager.MarkSceneDirty(s); }
        }
        // the other scenes are opened beside, swapped and saved (the open ones are only marked dirty: saving them is yours to do)
        foreach (var path in Scenes.Where(p => !open.Contains(p) && System.IO.File.Exists(p)))
        {
            var s = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            foreach (var pc in Object.FindObjectsByType<PrometeoCarController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.gameObject.scene == s)) { Swap(pc, prefab); n++; }
            EditorSceneManager.SaveScene(s);
            EditorSceneManager.CloseScene(s, true);
        }
        Debug.Log("Volkswagen T6: " + n + " van(s) swapped. Scenes that were open are changed but not saved.");
    }

    // ------------------------------------------------------------------ the prefab

    public static string Build()
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if (src == null) return "Missing " + Fbx;
        if (!AssetDatabase.IsValidFolder("Assets/Generated/VW")) AssetDatabase.CreateFolder("Assets/Generated", "VW");
        var mats = Materials();

        var holder = new GameObject("VW holder");
        var body = (GameObject)PrefabUtility.InstantiatePrefab(src, holder.transform);
        PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        body.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        var wheelParts = body.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.StartsWith("Cylinder.")).ToArray();
        var tyreFilter = wheelParts.FirstOrDefault(f => f.GetComponent<Renderer>().sharedMaterial.name == "pneu");
        if (wheelParts.Length == 0 || tyreFilter == null) { Object.DestroyImmediate(holder); return "The T6 has no Cylinder.* wheel meshes."; }

        // scale to a real van (the model's own length, wheels included)
        Bounds all = default; bool first = true;
        foreach (var f in body.GetComponentsInChildren<MeshFilter>(true))
        {
            var b = BoundsIn(holder.transform, f);
            if (first) { all = b; first = false; } else all.Encapsulate(b);
        }
        body.transform.localScale = Vector3.one * (Length / all.size.z);

        // where the four wheels are: the tyre triangles by quadrant (front/back about the middle of all four, left/right about the model's axis)
        var tyre = Triangles(holder.transform, tyreFilter).ToList();
        Bounds tb = new Bounds(tyre[0].centre, Vector3.zero);
        foreach (var t in tyre) { tb.Encapsulate(t.a); tb.Encapsulate(t.b); tb.Encapsulate(t.c); }
        var centres = new Bounds[4];
        for (int q = 0; q < 4; q++)
        {
            var mine = tyre.Where(t => Quadrant(t.centre, tb.center) == q).ToList();
            centres[q] = new Bounds(mine[0].a, Vector3.zero);
            foreach (var t in mine) { centres[q].Encapsulate(t.a); centres[q].Encapsulate(t.b); centres[q].Encapsulate(t.c); }
        }
        Vector3 offset = new Vector3(-tb.center.x, -tb.min.y, -tb.center.z);   // the axles' middle on the origin, the tyres on the road

        var root = new GameObject("Volkswagen T6");
        for (int q = 0; q < 4; q++)
        {
            var mesh = new Mesh { name = "VW " + WheelNames[q] };
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
            var subs = new List<int>[wheelParts.Length];
            for (int p = 0; p < wheelParts.Length; p++)
            {
                subs[p] = new List<int>();
                var f = wheelParts[p]; var m = f.sharedMesh; var mv = m.vertices; var mn = m.normals; var mu = m.uv; var map = new Dictionary<int, int>();
                int Take(int i)
                {
                    if (map.TryGetValue(i, out int k)) return k;
                    verts.Add(holder.transform.InverseTransformPoint(f.transform.TransformPoint(mv[i])) - centres[q].center);
                    norms.Add(holder.transform.InverseTransformDirection(f.transform.TransformDirection(mn[i])).normalized);
                    uvs.Add(mu.Length > i ? mu[i] : Vector2.zero);
                    return map[i] = verts.Count - 1;
                }
                var tri = m.triangles;
                for (int i = 0; i < tri.Length; i += 3)
                {
                    Vector3 c = (holder.transform.InverseTransformPoint(f.transform.TransformPoint(mv[tri[i]])) + holder.transform.InverseTransformPoint(f.transform.TransformPoint(mv[tri[i + 1]])) + holder.transform.InverseTransformPoint(f.transform.TransformPoint(mv[tri[i + 2]]))) / 3f;
                    if (Quadrant(c, tb.center) != q) continue;
                    subs[p].Add(Take(tri[i])); subs[p].Add(Take(tri[i + 1])); subs[p].Add(Take(tri[i + 2]));
                }
            }
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs);
            mesh.subMeshCount = wheelParts.Length;
            for (int p = 0; p < wheelParts.Length; p++) mesh.SetTriangles(subs[p], p);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, Out + mesh.name + ".asset");
            var w = new GameObject(WheelNames[q]);
            w.transform.SetParent(root.transform, false);
            w.transform.localPosition = centres[q].center + offset;
            w.AddComponent<MeshFilter>().sharedMesh = mesh;
            var wr = w.AddComponent<MeshRenderer>();
            wr.sharedMaterials = wheelParts.Select(f => Mat(mats, f.GetComponent<Renderer>().sharedMaterial.name, f.GetComponent<Renderer>().sharedMaterial)).ToArray();
            wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        foreach (var f in wheelParts) Object.DestroyImmediate(f.gameObject);   // (the merged wheels: replaced by the four above)
        foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = r.sharedMaterials.Select(m => Mat(mats, m != null ? m.name : "", m)).ToArray();
        body.name = "VW body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = offset;
        Object.DestroyImmediate(holder);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return null;
    }

    struct Tri { public Vector3 a, b, c, centre; }

    // (left/right about the axis x, front/back about the middle of the wheelbase: 0 FL, 1 FR, 2 RL, 3 RR)
    static int Quadrant(Vector3 p, Vector3 mid) => (p.z > mid.z ? 0 : 2) + (p.x > mid.x ? 1 : 0);

    static IEnumerable<Tri> Triangles(Transform space, MeshFilter f)
    {
        var m = f.sharedMesh; var v = m.vertices; var tri = m.triangles;
        Vector3 P(int i) => space.InverseTransformPoint(f.transform.TransformPoint(v[i]));
        for (int i = 0; i < tri.Length; i += 3)
        {
            Vector3 a = P(tri[i]), b = P(tri[i + 1]), c = P(tri[i + 2]);
            yield return new Tri { a = a, b = b, c = c, centre = (a + b + c) / 3f };
        }
    }

    static Bounds BoundsIn(Transform space, MeshFilter f)
    {
        var b = f.sharedMesh.bounds; Bounds r = default; bool first = true;
        for (int i = 0; i < 8; i++)
        {
            var c = space.InverseTransformPoint(f.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1) * 2f - Vector3.one)));
            if (first) { r = new Bounds(c, Vector3.zero); first = false; } else r.Encapsulate(c);
        }
        return r;
    }

    // ------------------------------------------------------------------ the materials

    static Material Mat(Dictionary<string, Material> mats, string name, Material fallback) => mats.TryGetValue(name, out var m) ? m : fallback;

    static Dictionary<string, Material> Materials()
    {
        var d = new Dictionary<string, Material>();
        // (name, base, normal, colour, metallic, smoothness)
        Make(d, "metal_blanc", System.IO.File.Exists(Dir + "textures/" + KvPaint + ".png") ? KvPaint : "metal_blanc_BaseColor", "metal_blanc_Normal", Color.white, .15f, .78f);       // the white paint
        Make(d, "metal_gris", "metal_gris_BaseColor", "metal_gris_Normal", Color.white, .85f, .6f);          // bumpers, trim, rims
        Make(d, "plastique_noir", "plastique_noir_BaseColor", "plastique_noir_Normal", Color.white, 0f, .35f);
        Make(d, "pneu", "pneu_BaseColor", "pneu_Normal", Color.white, 0f, .15f);
        Make(d, "phare", "phare_BaseColor", "phare_Normal", Color.white, .3f, .9f);
        Make(d, "mirroir", null, null, new Color(.78f, .8f, .82f), 1f, .96f);
        Make(d, "tissu", null, null, new Color(.17f, .16f, .16f), 0f, .1f);                                   // the seats
        Make(d, "verre_noir", null, null, new Color(.02f, .02f, .025f), 0f, .92f);
        var glass = Make(d, "verre", null, null, new Color(.5f, .6f, .65f, .22f), 0f, .97f);
        glass.SetFloat("_Surface", 1f); glass.SetFloat("_Blend", 0f); glass.SetFloat("_ZWrite", 0f);
        glass.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); glass.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glass.SetOverrideTag("RenderType", "Transparent"); glass.renderQueue = 3000;
        glass.SetShaderPassEnabled("ShadowCaster", false);
        EditorUtility.SetDirty(glass);
        return d;
    }

    static Material Make(Dictionary<string, Material> d, string name, string baseTex, string normalTex, Color colour, float metallic, float smooth)
    {
        string path = Out + "VW " + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", colour); m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smooth);
        m.SetTexture("_BaseMap", baseTex != null ? Tex(baseTex, false) : null);
        var n = normalTex != null ? Tex(normalTex, true) : null;
        m.SetTexture("_BumpMap", n); if (n != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(m);
        return d[name] = m;
    }

    // 1024 and crunched, like every other texture in the build; the normal maps marked as normal maps
    static Texture2D Tex(string file, bool normal)
    {
        string path = Dir + "textures/" + file + ".png";
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp != null && file != KvPaint && (imp.maxTextureSize != 1024 || !imp.crunchedCompression || (normal && imp.textureType != TextureImporterType.NormalMap)))
        {
            if (normal) imp.textureType = TextureImporterType.NormalMap;
            imp.maxTextureSize = 1024; imp.crunchedCompression = true; imp.compressionQuality = 70;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ------------------------------------------------------------------ the Kessler & Vane livery, painted into the van's paint texture

    // HellScape > Cars > Bake Kessler Vane Livery Into Van Paint: the paint texture (metal_blanc) is one flat grey, and the flanks of the
    // body and doors do not share UV space, so the livery is painted straight into a copy of it: every flank triangle is rasterised in UV,
    // each texel looked up by its car-space (z, y) and given the livery colour there. The logo sits top left of the cargo panel, a red +
    // grey stripe runs along the sill and three diagonal bands climb the rear, all kept clear of the wheel arches. Left reads towards the
    // rear, right towards the front. Tune the Logo* / Lv* numbers and run the menu again.
    const string KvPaint = "metal_blanc_BaseColor_KV";
    const int BakePx = 2048;
    const float LvY0 = 0.35f, LvY1 = 1.95f;
    const float LogoZ = -0.1f, LogoW = 1.25f, LogoY = 1.22f, LogoCrop = 1300f / 1551f;   // (the logo file's right edge carries stripe scraps: cropped)
    static readonly Color Red = new Color(.72f, .11f, .17f), Grey = new Color(.22f, .24f, .28f);

    static float Cov(float d, float pm) => Mathf.Clamp01(.5f + d / pm);
    static void Over(ref Color c, Color s, float a) { a *= s.a; float o = a + c.a * (1f - a); if (o <= 0f) return; c = new Color((s.r * a + c.r * c.a * (1f - a)) / o, (s.g * a + c.g * c.a * (1f - a)) / o, (s.b * a + c.b * c.a * (1f - a)) / o, o); }

    // the livery at car-space (z along the van, y up) on one flank (side -1 left, +1 right): colour + coverage, pm = metres per texel
    static Color LiveryAt(float side, float z, float y, float pm, Texture2D logo, float hl)
    {
        var c = new Color(0, 0, 0, 0);
        float clip = Mathf.Min(Mathf.Min(z + 2.25f, 0.85f - z), Mathf.Min(y - LvY0 - .05f, 1.9f - y));
        Over(ref c, Grey, Cov(Mathf.Min(Mathf.Min(y - .50f, .66f - y), clip), pm));   // sill stripe, from the cab door's foot
        Over(ref c, Red, Cov(Mathf.Min(Mathf.Min(y - .70f, .76f - y), clip), pm));
        float s = z + (y - .9f) * .95f, dc = Mathf.Min(clip, y - .78f);                // the rear diagonals
        Over(ref c, Red, Cov(Mathf.Min(Mathf.Min(s + 1.40f, -1.05f - s) * .7f, dc), pm));
        Over(ref c, Grey, Cov(Mathf.Min(Mathf.Min(s + 1.85f, -1.40f - s) * .7f, dc), pm));
        Over(ref c, Red, Cov(Mathf.Min(Mathf.Min(s + 2.00f, -1.92f - s) * .7f, dc), pm));
        foreach (float az in new[] { -1.52f, 1.52f }) c.a *= Cov(Mathf.Sqrt((z - az) * (z - az) + (y - .375f) * (y - .375f)) - .6f, pm);   // (the wheel arches)
        float a = side < 0 ? (LogoZ - z) / LogoW : (z - (LogoZ - LogoW)) / LogoW, b = (y - LogoY) / hl;
        if (a >= 0f && a <= 1f && b >= 0f && b <= 1f) Over(ref c, logo.GetPixelBilinear(a * LogoCrop, b), 1f);
        return c;
    }

    [MenuItem("HellScape/Cars/Bake Kessler Vane Livery Into Van Paint")]
    public static void LiveryMenu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
        string made = BakeLivery();
        if (made != null) { Debug.LogWarning(made); return; }
        // the floating decal quads of the first attempt go from every scene
        var open = new HashSet<string>();
        for (int i = 0; i < EditorSceneManager.sceneCount; i++) { var s = EditorSceneManager.GetSceneAt(i); open.Add(s.path); if (DropQuads(s) > 0) EditorSceneManager.MarkSceneDirty(s); }
        foreach (var path in Scenes.Where(p => !open.Contains(p) && System.IO.File.Exists(p)))
        {
            var s = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (DropQuads(s) > 0) EditorSceneManager.SaveScene(s);
            EditorSceneManager.CloseScene(s, true);
        }
        Debug.Log("Kessler & Vane livery baked into " + Dir + "textures/" + KvPaint + ".png. Scenes that were open are changed but not saved.");
    }

    static int DropQuads(UnityEngine.SceneManagement.Scene s)
    {
        int n = 0;
        foreach (var pc in Object.FindObjectsByType<PrometeoCarController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.gameObject.scene == s))
        {
            var van = pc.transform.Find("Volkswagen T6"); if (van == null) continue;
            foreach (var name in new[] { "KV Decal L", "KV Decal R" }) { var o = van.Find(name); if (o != null) { Object.DestroyImmediate(o.gameObject); n++; } }
        }
        return n;
    }

    public static string BakeLivery()
    {
        var car = Object.FindObjectsByType<PrometeoCarController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.transform.Find("Volkswagen T6") != null);
        if (car == null) return "Open a scene with the Volkswagen van first.";
        var van = car.transform.Find("Volkswagen T6");

        var logo = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        logo.LoadImage(System.IO.File.ReadAllBytes(Dir + "textures/Kessler_Vane_Logistics_Logo_Transparent.png"));
        float hl = LogoW * logo.height / (logo.width * LogoCrop);
        var flat = new Texture2D(2, 2, TextureFormat.RGBA32, false);                   // (the paint as it was: one flat grey)
        flat.LoadImage(System.IO.File.ReadAllBytes(Dir + "textures/metal_blanc_BaseColor.png"));
        Color paint = flat.GetPixel(8, 8); paint.a = 1f; Object.DestroyImmediate(flat);
        var px = new Color[BakePx * BakePx];
        for (int i = 0; i < px.Length; i++) px[i] = paint;

        const float pm = .008f;   // (a texel is about 0.8 cm on the van)
        int tris = 0;
        foreach (var mf in van.GetComponentsInChildren<MeshFilter>())
        {
            var mats = mf.GetComponent<Renderer>().sharedMaterials; var me = mf.sharedMesh;
            var v = me.vertices; var nm = me.normals; var uv = me.uv;
            for (int sm = 0; sm < me.subMeshCount; sm++)
            {
                if (mats[Mathf.Min(sm, mats.Length - 1)] == null || !mats[Mathf.Min(sm, mats.Length - 1)].name.Contains("metal_blanc")) continue;
                var tr = me.GetTriangles(sm);
                for (int t = 0; t < tr.Length; t += 3)
                {
                    int i0 = tr[t], i1 = tr[t + 1], i2 = tr[t + 2];
                    Vector3 p0 = car.transform.InverseTransformPoint(mf.transform.TransformPoint(v[i0])), p1 = car.transform.InverseTransformPoint(mf.transform.TransformPoint(v[i1])), p2 = car.transform.InverseTransformPoint(mf.transform.TransformPoint(v[i2]));
                    Vector3 n = car.transform.InverseTransformDirection(mf.transform.TransformDirection(nm[i0] + nm[i1] + nm[i2]));
                    float cx = (p0.x + p1.x + p2.x) / 3f;
                    float side = n.x < -.5f * n.magnitude && cx < -.6f ? -1f : n.x > .5f * n.magnitude && cx > .6f ? 1f : 0f;   // (outward faces of the two flanks only)
                    if (side == 0f) continue;
                    tris++;
                    Vector2 a = uv[i0] * BakePx, b = uv[i1] * BakePx, c = uv[i2] * BakePx;
                    float den = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y); if (Mathf.Abs(den) < 1e-6f) continue;
                    int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))) - 1), x1 = Mathf.Min(BakePx - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))) + 1);
                    int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))) - 1), y1 = Mathf.Min(BakePx - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))) + 1);
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            float px_ = x + .5f, py_ = y + .5f;
                            float l0 = ((b.y - c.y) * (px_ - c.x) + (c.x - b.x) * (py_ - c.y)) / den, l1 = ((c.y - a.y) * (px_ - c.x) + (a.x - c.x) * (py_ - c.y)) / den, l2 = 1f - l0 - l1;
                            if (l0 < -.05f || l1 < -.05f || l2 < -.05f) continue;   // (a little past the edge, so no seam shows)
                            l0 = Mathf.Max(0f, l0); l1 = Mathf.Max(0f, l1); l2 = Mathf.Max(0f, l2); float sum = l0 + l1 + l2; l0 /= sum; l1 /= sum; l2 /= sum;
                            Vector3 p = p0 * l0 + p1 * l1 + p2 * l2;
                            var lc = LiveryAt(side, p.z, p.y, pm, logo, hl);
                            if (lc.a > 0f) Over(ref px[y * BakePx + x], new Color(lc.r, lc.g, lc.b, 1f), lc.a);
                        }
                }
            }
        }
        Object.DestroyImmediate(logo);
        if (tris == 0) return "No flank triangles found on the van's paint meshes.";

        string path = Dir + "textures/" + KvPaint + ".png";
        var tex = new Texture2D(BakePx, BakePx, TextureFormat.RGBA32, false); tex.SetPixels(px);
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.maxTextureSize = 2048; imp.crunchedCompression = false; imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.mipmapEnabled = true; imp.anisoLevel = 4; imp.alphaSource = TextureImporterAlphaSource.None; imp.SaveAndReimport();

        var kv = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        foreach (var mp in new[] { Dir + "textures/metal_blanc 1.mat", Out + "VW metal_blanc.mat" })
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp); if (m == null) continue;
            m.SetTexture("_BaseMap", kv); EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
        return null;
    }

    // ------------------------------------------------------------------ the swap

    public static void Swap(PrometeoCarController pc, GameObject prefab)
    {
        var car = pc.gameObject; var rb = car.GetComponent<Rigidbody>();
        Undo.RegisterFullObjectHierarchyUndo(car, "Volkswagen");

        // the old van's looks go (and its wheel meshes, which sit under the pivots)
        foreach (var n in new[] { "PixelDucatoJacex", "Volkswagen T6", "VolkswagenT6" }) { var o = car.transform.Find(n); if (o != null) Object.DestroyImmediate(o.gameObject); }
        var body = car.transform.Find("Body"); if (body != null) body.gameObject.SetActive(false);
        var oldMeshes = car.transform.Find("Wheels/Meshes"); if (oldMeshes != null) oldMeshes.gameObject.SetActive(false);
        var pivots = car.transform.Find("Wheels/Van wheel pivots");
        if (pivots == null) { pivots = new GameObject("Van wheel pivots").transform; pivots.SetParent(car.transform.Find("Wheels"), false); }
        foreach (Transform p in pivots) for (int k = p.childCount - 1; k >= 0; k--) Object.DestroyImmediate(p.GetChild(k).gameObject);

        var van = (GameObject)PrefabUtility.InstantiatePrefab(prefab, car.transform);
        PrefabUtility.UnpackPrefabInstance(van, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);   // (the wheels move to the pivots)
        van.name = "Volkswagen T6";
        van.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        var cols = new[] { pc.frontLeftCollider, pc.frontRightCollider, pc.rearLeftCollider, pc.rearRightCollider };
        var pivotT = new Transform[4];
        float radius = 0f;
        for (int i = 0; i < 4; i++)
        {
            var w = van.transform.Find(WheelNames[i]);
            var p = pivots.Find(WheelNames[i]);
            if (p == null) { p = new GameObject(WheelNames[i]).transform; p.SetParent(pivots, false); }
            p.SetPositionAndRotation(w.position, car.transform.rotation);
            w.SetParent(p, true);
            pivotT[i] = p;
            float r = w.GetComponent<Renderer>().bounds.extents.y; radius += r / 4f;
            cols[i].transform.localPosition = car.transform.InverseTransformPoint(p.position);
        }
        foreach (var c in cols) c.radius = radius;
        pc.frontLeftMesh = pivotT[0].gameObject; pc.frontRightMesh = pivotT[1].gameObject;
        pc.rearLeftMesh = pivotT[2].gameObject; pc.rearRightMesh = pivotT[3].gameObject;

        // skid marks and tyre smoke at the rear wheels
        var fx = car.transform.Find("Effects");
        if (fx != null)
            foreach (Transform e in fx)
            {
                Vector3 l = car.transform.InverseTransformPoint((e.name.StartsWith("Left") ? pivotT[2] : pivotT[3]).position);
                e.localPosition = new Vector3(l.x, e.localPosition.y, l.z);
            }

        // body collider: the shell measured unrotated, lifted clear of the road
        Quaternion rot = car.transform.rotation;
        car.transform.rotation = Quaternion.identity;
        var shell = van.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
        Vector3 min = car.transform.InverseTransformPoint(shell.min), max = car.transform.InverseTransformPoint(shell.max);
        car.transform.rotation = rot;
        min.y = Mathf.Max(min.y, radius * 1.1f);
        Vector3 shellCentre = (min + max) * 0.5f, shellSize = max - min;
        var solidT = car.transform.Find("Van body collider");
        if (solidT == null) { solidT = new GameObject("Van body collider").transform; solidT.SetParent(car.transform, false); }
        var solid = solidT.GetComponent<BoxCollider>();
        if (solid == null) solid = solidT.gameObject.AddComponent<BoxCollider>();
        solid.center = shellCentre; solid.size = shellSize;
        var box = car.GetComponent<BoxCollider>();   // CarInteract's get-in trigger: the van plus a step all round (the prologue's van has none)
        if (box != null) { box.center = shellCentre; box.size = shellSize + new Vector3(2.4f, 0.4f, 2.4f); EditorUtility.SetDirty(box); }

        rb.mass = 1700f;
        pc.bodyMassCenter = new Vector3(0f, 0.45f, 0.1f);
        pc.maxSpeed = 150; pc.accelerationMultiplier = 8;
        VanHandling.Apply(pc);

        var exit = car.transform.Find("Exit");   // driver's side, by the door
        if (exit != null) exit.localPosition = new Vector3(min.x - 1.1f, 1.1f, max.z - 1.8f);
        var ci = car.GetComponent<CarInteract>();
        var follow = ci != null && ci.carCamera != null ? ci.carCamera.GetComponent<CameraFollow>() : null;
        if (follow != null) { Undo.RecordObject(follow, "Van cam"); follow.baseOffset = new Vector3(0f, 2.6f, -6.8f); follow.lookHeightOffset = 1.4f; EditorUtility.SetDirty(follow); }

        EditorUtility.SetDirty(pc); EditorUtility.SetDirty(rb); EditorUtility.SetDirty(solid);
        Debug.Log("Volkswagen T6 fitted to " + car.name + " (" + car.scene.name + "): wheel radius " + radius.ToString("0.00") + ", body " + shellSize.ToString("F2"));
    }
}
