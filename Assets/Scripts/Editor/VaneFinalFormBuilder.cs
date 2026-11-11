using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// The escape opening (MainGameScene), 2026-10-29: the office blows up and comes down, and Vane's final form walks
// out of the blast (DemonBoss.Collapse), instead of the damned body changing in the street.
//   HellScape > Finale > Vane Final Form + Office Collapse
//     - the boss model is the user's final form (Assets/Enemies/VaneFinalForm: one FBX per action, same rig),
//       Tillagemon switched off; the boss's state names are kept, so the fight plays the new clips
//     - Building_Fractured (Assets/Perishables, the same office cut into 180 cells) fitted over the office, off
//     - the Particle Pack effects for the blasts, dust, sparks, smoke and fire
public static class VaneFinalFormBuilder
{
    const string Dir = "Assets/Enemies/VaneFinalForm/Boss_";
    const string Ctrl = "Assets/Generated/VaneFinalForm.controller";
    const string Fractured = "Assets/Perishables/Building_Fractured.fbx";
    const string Fx = "Assets/UnityTechnologies/ParticlePack/EffectExamples/";
    const string FormName = "Vane (final form)", BrokenName = "Office (broken, for the collapse)";
    public static float Height = 12f;   // 2x Tillagemon's 6 m (user, 2026-10-30: 24 m was too huge)

    [MenuItem("HellScape/Finale/Vane Final Form + Office Collapse")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var boss = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        if (boss == null || boss.model == null) return "Open MainGameScene (no DemonBoss with a model).";
        var office = GameObject.Find("NewOfficeBuilding Entrance");
        if (office == null) return "No 'NewOfficeBuilding Entrance' in the scene.";

        // ---- the final form
        var looping = new[] { "Idle", "Walk", "Run", "Crawl" };
        foreach (var n in new[] { "Idle", "Walk", "Run", "Punch", "Roar", "Throw", "Crawl", "Arms_Up", "Die" })
            MeshyCastBuilder.Loop(Dir + n + ".fbx", looping.Contains(n));
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(Ctrl) ?? AnimatorController.CreateAnimatorControllerAtPath(Ctrl);
        var sm = ac.layers[0].stateMachine;
        foreach (var s in sm.states) sm.RemoveState(s.state);
        foreach (var (state, clip) in new[] { ("Idle", "Idle"), ("Walk", "Walk"), ("Run", "Run"), ("Slam", "Punch"), ("Roar", "Roar"),
                                              ("Throw", "Throw"), ("Stun", "Crawl"), ("Hurt", "Arms_Up"), ("Die", "Die") })
        {
            var st = sm.AddState(state);
            st.motion = MeshyCastBuilder.Clip(Dir + clip + ".fbx");
            if (state == "Idle") sm.defaultState = st;
        }
        EditorUtility.SetDirty(ac);

        Undo.RegisterFullObjectHierarchyUndo(boss.gameObject, "Vane final form");
        foreach (var c in boss.model.Cast<Transform>().ToList())
            if (c.name == FormName) Object.DestroyImmediate(c.gameObject);
            else c.gameObject.SetActive(false);   // (Tillagemon: kept, off)
        var g = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "Idle.fbx"));
        PrefabUtility.UnpackPrefabInstance(g, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        g.name = FormName;
        g.transform.SetParent(boss.model, false);
        var anim = g.GetComponent<Animator>();
        if (anim == null) anim = g.AddComponent<Animator>();
        anim.runtimeAnimatorController = ac;
        Skin(g);
        MeshyCastBuilder.Clip(Dir + "Idle.fbx").SampleAnimation(g, 0f);
        FinaleBuilder.FitModel(boss, g, Height);
        HeartModel.UseOnBoss();   // FitModel makes the heart a plain ball again: the beating heart goes back in it
        boss.idleState = "Idle"; boss.moveState = "Walk"; boss.chargeState = "Run"; boss.slamState = "Slam";
        boss.roarState = "Roar"; boss.throwState = "Throw"; boss.stunState = "Stun"; boss.dieState = "Die";
        boss.moveClipSpeed = boss.walkSpeed * Height / 6f;   // a stride covers 4x the ground at 4x the size: slow, heavy steps

        // ---- the broken office, cell for cell over the whole one
        var old = office.transform.parent != null ? office.transform.parent.Find(BrokenName) : GameObject.Find(BrokenName)?.transform;
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var broken = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fractured));
        PrefabUtility.UnpackPrefabInstance(broken, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        broken.name = BrokenName;
        broken.transform.SetParent(office.transform.parent, true);
        float yaw = Fit(broken, office);
        broken.SetActive(false);
        boss.officeBuilding = office;
        var sign = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t.gameObject.scene.isLoaded && t.name.StartsWith("Meshy_AI_KV_Logistics"));
        if (sign != null) boss.kvSign = sign;
        boss.brokenOffice = broken;

        GameObject P(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(Fx + path + ".prefab");
        boss.smallBlast = P("Fire & Explosion Effects/Prefabs/SmallExplosion");
        boss.bigBlast = P("Fire & Explosion Effects/Prefabs/BigExplosion");
        boss.dustBlast = P("Fire & Explosion Effects/Prefabs/DustExplosion");
        boss.fireBall = P("Fire & Explosion Effects/Prefabs/FireBall");
        boss.flames = P("Fire & Explosion Effects/Prefabs/LargeFlames");
        boss.smoke = P("Smoke & Steam Effects/Prefabs/SmokeEffect");
        boss.sparks = P("Misc Effects/Prefabs/SparksEffect");
        WireBlood(boss);

        EditorUtility.SetDirty(boss);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
        EditorSceneManager.SaveScene(boss.gameObject.scene);
        return $"Vane's final form is the boss ({Height} m); broken office fitted over the office (yaw {yaw:F1}), collapse cutscene on.";
    }

    // The bridge fight's blood attacks (DemonBossBlood): the Particle Pack effects they recolour, and the severed
    // Meshy parts (Assets/Floating) as 1 m prefabs with their textures, for the bombs and the shield.
    [MenuItem("HellScape/Finale/Blood Attacks (wire effects + body parts)")]
    public static void BloodMenu()
    {
        if (EditorApplication.isPlaying) { Debug.Log("Stop Play mode first."); return; }
        var boss = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        if (boss == null) { Debug.Log("Open MainGameScene (no DemonBoss)."); return; }
        Undo.RecordObject(boss, "Blood attacks");
        WireBlood(boss);
        EditorUtility.SetDirty(boss);
        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
        EditorSceneManager.SaveScene(boss.gameObject.scene);
        Debug.Log($"Blood attacks wired: lance {boss.iceLance != null}, shatter {boss.earthShatter != null}, gas {boss.poisonGas != null}, {boss.bodyParts.Length} body parts, blast {boss.bigBlast != null}, orb {boss.orbModel != null}.");
    }

    const string PartsDir = "Assets/Generated/BodyParts";

    static void WireBlood(DemonBoss boss)
    {
        GameObject P(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(Fx + path + ".prefab");
        boss.iceLance = P("Magic Effects/Prefabs/IceLance");
        boss.earthShatter = P("Magic Effects/Prefabs/EarthShatter");
        boss.poisonGas = P("Smoke & Steam Effects/Prefabs/PoisonGas");
        boss.goop = P("Goop Effects/Prefabs/GoopSprayEffect");
        if (boss.bigBlast == null) boss.bigBlast = P("Fire & Explosion Effects/Prefabs/BigExplosion");

        if (!AssetDatabase.IsValidFolder(PartsDir)) AssetDatabase.CreateFolder("Assets/Generated", "BodyParts");
        var parts = new List<GameObject>();
        foreach (var path in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Floating" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
        {
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            string key = new[] { "arm", "foot", "hand", "head", "leg", "torso" }.FirstOrDefault(k => file.Contains("_" + k + "_")) ?? file;
            var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(System.IO.Path.ChangeExtension(path, ".mat"));   // (SiloBloodPool made these)
            if (mat != null) foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Object.DestroyImmediate(go); continue; }
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            // pivot in the middle, longest side 1 m
            var root = new GameObject("Body part - " + key);
            go.transform.SetParent(root.transform, true);
            go.transform.position -= b.center;
            root.transform.localScale = Vector3.one / Mathf.Max(0.01f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
            parts.Add(PrefabUtility.SaveAsPrefabAsset(root, PartsDir + "/" + key + ".prefab"));
            Object.DestroyImmediate(root);
        }
        boss.bodyParts = parts.ToArray();
    }

    // The FBXs come untextured, and Meshy's texture set beside them belongs to the static T-pose model
    // (_Source/VaneOriginal.fbx), whose UVs are not the rigged mesh's: put straight on, it lands scrambled. So it is
    // baked across (BakeTexture) onto the rigged mesh's own UVs. Only the colour: the normal map is tangent-space to
    // the old UVs, and the glTF metallic/roughness maps don't fit URP's packing. The mouth keeps its own material.
    const string Tex = "Assets/Enemies/VaneFinalForm/Meshy_AI_Mutated_Monster_T_Pos_1029063436_image-to-3d-texture.png";
    const string Original = "Assets/Enemies/VaneFinalForm/_Source/VaneOriginal.fbx";
    const string Baked = "Assets/Enemies/VaneFinalForm/Vane_baked_color.png";
    const int BakeSize = 2048;

    static void Skin(GameObject g)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(Baked) == null) Debug.Log(BakeTexture());
        var mat = MeshyCastBuilder.Mat("VaneFinalForm", Baked, "none");
        mat.SetTexture("_BumpMap", null); mat.DisableKeyword("_NORMALMAP");
        mat.SetFloat("_Smoothness", .35f);   // a little wet
        foreach (var r in g.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (r.name == "Boss_Mesh") r.sharedMaterial = mat;
    }

    [MenuItem("HellScape/Finale/Bake Vane Texture")]
    public static void BakeMenu() => Debug.Log(BakeTexture());

    // For every texel of the rigged mesh's UV layout: the point on its surface (bind pose = the T-pose), the nearest
    // point on the original's surface, and the original texture's colour there. Then the islands are bled outwards
    // so mip-maps don't pull black into the seams.
    public static string BakeTexture()
    {
        var srcGo = AssetDatabase.LoadAssetAtPath<GameObject>(Original);
        if (srcGo == null) return "Bake: missing " + Original + " (Meshy's T-pose FBX for this texture).";
        var src = new Texture2D(2, 2);
        src.LoadImage(System.IO.File.ReadAllBytes(Tex));   // (full 4K, not the import)
        int sw = src.width, sh = src.height;
        var sp = src.GetPixels32();

        var o = (GameObject)Object.Instantiate(srcGo);
        var mf = o.GetComponentInChildren<MeshFilter>();
        var oMesh = mf.sharedMesh;
        var ov = oMesh.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray();
        var ouv = oMesh.uv; var ot = oMesh.triangles;
        var bGo = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "Idle.fbx"));
        var smr = bGo.GetComponentsInChildren<SkinnedMeshRenderer>().First(s => s.name == "Boss_Mesh");
        var bv = smr.sharedMesh.vertices.Select(v => smr.transform.TransformPoint(v)).ToArray();
        var buv = smr.sharedMesh.uv; var bt = smr.sharedMesh.triangles;
        Object.DestroyImmediate(o); Object.DestroyImmediate(bGo);
        Bounds ob = Box(ov), bb = Box(bv);
        Vector3 shift = ob.center - bb.center;   // same model, the rigged one sits on its feet

        // the original's triangles in a grid
        const float cell = 0.03f;
        Vector3 g0 = ob.min - Vector3.one * cell;
        int nx = Mathf.CeilToInt(ob.size.x / cell) + 3, ny = Mathf.CeilToInt(ob.size.y / cell) + 3, nz = Mathf.CeilToInt(ob.size.z / cell) + 3;
        var grid = new Dictionary<int, List<int>>();
        int Key(int x, int y, int z) => (x * ny + y) * nz + z;
        Vector3Int C(Vector3 p) => new Vector3Int(Mathf.Clamp((int)((p.x - g0.x) / cell), 0, nx - 1), Mathf.Clamp((int)((p.y - g0.y) / cell), 0, ny - 1), Mathf.Clamp((int)((p.z - g0.z) / cell), 0, nz - 1));
        for (int t = 0; t < ot.Length; t += 3)
        {
            Vector3 a = ov[ot[t]], b = ov[ot[t + 1]], c = ov[ot[t + 2]];
            var lo = C(Vector3.Min(a, Vector3.Min(b, c))); var hi = C(Vector3.Max(a, Vector3.Max(b, c)));
            for (int x = lo.x; x <= hi.x; x++) for (int y = lo.y; y <= hi.y; y++) for (int z = lo.z; z <= hi.z; z++)
            {
                int k = Key(x, y, z);
                if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
                l.Add(t);
            }
        }

        Color32 Sample(Vector2 uv)
        {
            float fx = Mathf.Repeat(uv.x, 1f) * sw - .5f, fy = Mathf.Repeat(uv.y, 1f) * sh - .5f;
            int x0 = Mathf.Clamp((int)fx, 0, sw - 1), y0 = Mathf.Clamp((int)fy, 0, sh - 1), x1 = Mathf.Min(x0 + 1, sw - 1), y1 = Mathf.Min(y0 + 1, sh - 1);
            float tx = Mathf.Clamp01(fx - x0), ty = Mathf.Clamp01(fy - y0);
            Color a = Color.Lerp(sp[y0 * sw + x0], sp[y0 * sw + x1], tx), b = Color.Lerp(sp[y1 * sw + x0], sp[y1 * sw + x1], tx);
            return Color.Lerp(a, b, ty);
        }

        Color32 Nearest(Vector3 p)
        {
            var c = C(p);
            float best = float.MaxValue; int bestT = -1; Vector3 bestBary = default;
            for (int ring = 1; ring <= 3 && bestT < 0; ring++)
                for (int x = c.x - ring; x <= c.x + ring; x++) for (int y = c.y - ring; y <= c.y + ring; y++) for (int z = c.z - ring; z <= c.z + ring; z++)
                {
                    if (x < 0 || y < 0 || z < 0 || x >= nx || y >= ny || z >= nz || !grid.TryGetValue(Key(x, y, z), out var l)) continue;
                    foreach (int t in l)
                    {
                        var bary = ClosestBary(p, ov[ot[t]], ov[ot[t + 1]], ov[ot[t + 2]]);
                        Vector3 q = ov[ot[t]] * bary.x + ov[ot[t + 1]] * bary.y + ov[ot[t + 2]] * bary.z;
                        float d = (q - p).sqrMagnitude;
                        if (d < best) { best = d; bestT = t; bestBary = bary; }
                    }
                }
            if (bestT < 0) return new Color32(0, 0, 0, 0);
            var s = Sample(ouv[ot[bestT]] * bestBary.x + ouv[ot[bestT + 1]] * bestBary.y + ouv[ot[bestT + 2]] * bestBary.z);
            s.a = 255;
            return s;
        }

        // rasterise the rigged mesh's UV triangles
        int N = BakeSize;
        var outPx = new Color32[N * N];
        for (int t = 0; t < bt.Length; t += 3)
        {
            if (t % 3000 == 0 && EditorUtility.DisplayCancelableProgressBar("Baking Vane's texture", "triangle " + t / 3 + " / " + bt.Length / 3, (float)t / bt.Length)) break;
            Vector2 ua = buv[bt[t]] * N, ub = buv[bt[t + 1]] * N, uc = buv[bt[t + 2]] * N;
            Vector3 pa = bv[bt[t]] + shift, pb = bv[bt[t + 1]] + shift, pc = bv[bt[t + 2]] + shift;
            float area = (ub.x - ua.x) * (uc.y - ua.y) - (uc.x - ua.x) * (ub.y - ua.y);
            if (Mathf.Abs(area) < 1e-8f) continue;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ua.x, Mathf.Min(ub.x, uc.x)))), x1 = Mathf.Min(N - 1, Mathf.CeilToInt(Mathf.Max(ua.x, Mathf.Max(ub.x, uc.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ua.y, Mathf.Min(ub.y, uc.y)))), y1 = Mathf.Min(N - 1, Mathf.CeilToInt(Mathf.Max(ua.y, Mathf.Max(ub.y, uc.y))));
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                if (outPx[y * N + x].a != 0) continue;
                Vector2 q = new Vector2(x + .5f, y + .5f);
                float w1 = ((ub.x - q.x) * (uc.y - q.y) - (uc.x - q.x) * (ub.y - q.y)) / area;
                float w2 = ((uc.x - q.x) * (ua.y - q.y) - (ua.x - q.x) * (uc.y - q.y)) / area;
                float w3 = 1f - w1 - w2;
                const float e = -0.02f;   // a hair over the edge: no gaps between neighbours
                if (w1 < e || w2 < e || w3 < e) continue;
                outPx[y * N + x] = Nearest(pa * w1 + pb * w2 + pc * w3);
            }
        }
        EditorUtility.ClearProgressBar();

        // bleed the islands out a few texels
        for (int pass = 0; pass < 8; pass++)
        {
            var next = (Color32[])outPx.Clone();
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                if (outPx[y * N + x].a != 0) continue;
                int r = 0, gg = 0, b = 0, n = 0;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = x + dx, yy = y + dy;
                    if (xx < 0 || yy < 0 || xx >= N || yy >= N) continue;
                    var c = outPx[yy * N + xx];
                    if (c.a == 0) continue;
                    r += c.r; gg += c.g; b += c.b; n++;
                }
                if (n > 0) next[y * N + x] = new Color32((byte)(r / n), (byte)(gg / n), (byte)(b / n), 255);
            }
            outPx = next;
        }

        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.SetPixels32(outPx); tex.Apply();
        System.IO.File.WriteAllBytes(Baked, tex.EncodeToPNG());
        Object.DestroyImmediate(tex); Object.DestroyImmediate(src);
        AssetDatabase.ImportAsset(Baked);
        if (AssetImporter.GetAtPath(Baked) is TextureImporter ti) { ti.maxTextureSize = BakeSize; ti.crunchedCompression = true; ti.alphaSource = TextureImporterAlphaSource.None; ti.SaveAndReimport(); }
        return "Vane's texture baked onto the rigged mesh's UVs: " + Baked;
    }

    // barycentric coordinates of the point on triangle abc nearest to p (Ericson, Real-Time Collision Detection 5.1.5)
    static Vector3 ClosestBary(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a, ap = p - a;
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f) return new Vector3(1, 0, 0);
        Vector3 bp = p - b;
        float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3) return new Vector3(0, 1, 0);
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f) { float v = d1 / (d1 - d3); return new Vector3(1 - v, v, 0); }
        Vector3 cp = p - c;
        float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6) return new Vector3(0, 0, 1);
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f) { float w = d2 / (d2 - d6); return new Vector3(1 - w, 0, w); }
        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) { float w = (d4 - d3) / ((d4 - d3) + (d5 - d6)); return new Vector3(0, 1 - w, w); }
        float denom = 1f / (va + vb + vc);
        float v2 = vb * denom, w2 = vc * denom;
        return new Vector3(1 - v2 - w2, v2, w2);
    }

    static Bounds Box(Vector3[] a)
    {
        var b = new Bounds(a[0], Vector3.zero);
        foreach (var v in a) b.Encapsulate(v);
        return b;
    }

    // turns the broken copy about the vertical until its cells sit on the office's own vertices (both are the same
    // model, exported differently), bounds centred on it, feet on its floor. Returns the yaw it took.
    static float Fit(GameObject broken, GameObject office)
    {
        var target = Verts(office, 4000);
        Quaternion r0 = broken.transform.rotation;
        Bounds ob = Bounds(office);
        float bestYaw = 0f, best = float.MaxValue;
        var tries = new List<float>();
        for (int i = 0; i < 360; i += 90) { tries.Add(i); tries.Add(i + office.transform.eulerAngles.y % 90f); }
        foreach (float y in tries)
        {
            Place(broken, r0, y, ob);
            var probe = Verts(broken, 300);
            float err = probe.Sum(p => target.Min(q => (p - q).sqrMagnitude));
            if (err < best) { best = err; bestYaw = y; }
        }
        Place(broken, r0, bestYaw, ob);
        return bestYaw;
    }

    static void Place(GameObject g, Quaternion r0, float yaw, Bounds target)
    {
        g.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * r0;
        var b = Bounds(g);
        g.transform.position += new Vector3(target.center.x - b.center.x, target.min.y - b.min.y, target.center.z - b.center.z);
    }

    static List<Vector3> Verts(GameObject g, int count)
    {
        var all = new List<Vector3>();
        foreach (var mf in g.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh != null)
                foreach (var v in mf.sharedMesh.vertices) all.Add(mf.transform.TransformPoint(v));
        int step = Mathf.Max(1, all.Count / count);
        return all.Where((_, i) => i % step == 0).ToList();
    }

    static Bounds Bounds(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>(true);
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
