using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The Kessler & Vane office model (Assets/KesslerVane) put in the scene as "Office Ground Floor", and its blocky
// built-in props swapped for the low-poly office supplies pack.
//
// The model's furniture is merged into one mesh per room and material, so the props are found by splitting those
// meshes into islands and recognising each by its size: a chair is a 0.58 m leather seat, a workstation is a
// 0.51 m screen, a cabinet is a metal box with drawer lines on one face. What is replaced is cut out of the
// mesh (a stripped copy in Generated/Office/KV; both scenes share it) and a supply stands in its place.
// Desk tops, tables, shelving, crates and sofas stay: the pack has nothing to stand in for them.
//
// The model's own origin is its front door. It is turned half round so the plan's north is world +z.
public static partial class NewOfficeFit
{
    const string KvPrefab = "Assets/KesslerVane/Prefabs/Office_Clean.prefab";
    const string KvMats = "Assets/KesslerVane/Materials";
    const string SuppliesFbx = "Assets/KesslerVane/low-poly-office-cubicle-assets/source/Office Supplies.fbx";
    const string KvDir = GenDir + "/KV";
    public const float DoorX = 982f, DoorZ = 870f;   // the front door, in the world

    public static bool IsKv(Transform m) => m != null && m.Find("Floor_arrival") != null;

    [MenuItem("HellScape/Office/Install Kessler && Vane Office (replaces the model)")]
    public static void Reinstall()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Stop Play first."); return; }
        log.Clear();
        var m = Install(true);
        EditorSceneManager.MarkSceneDirty(m.gameObject.scene);
        Debug.Log("Kessler & Vane office installed.\n" + log);
    }

    // The office in the open scene: burnt in the hell city (the scene with the OfficeAct), clean in the Prologue.
    public static Transform Install(bool force)
    {
        var old = GameObject.Find(ModelName);
        if (old != null && IsKv(old.transform) && !force) return old.transform;
        bool burnt = GameObject.Find("OfficeAct") != null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KvPrefab));
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Undo.RegisterCreatedObjectUndo(go, "Office model");
        go.name = ModelName;
        go.GetComponent<KesslerVane.OfficeStoryState>().SetBurned(burnt);
        go.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * go.transform.rotation;
        go.transform.position = new Vector3(DoorX, Floor, DoorZ);
        if (old != null) { go.transform.SetSiblingIndex(old.transform.GetSiblingIndex()); Undo.DestroyObjectImmediate(old); }
        Physics.SyncTransforms();
        Dress(go.transform, burnt);
        Doors(go.transform, burnt ? new[] { "D17" } : new[] { "D17", "D05", "D06" });   // (before the fire nothing off the lobby is locked)
        if (burnt) EchoPairs();
        Cubicles();
        Logo();
        return go.transform;
    }

    // The Prologue's office is the one the user dresses by hand. This makes the hell city's office the same: every
    // material changed on the model there is worn here in its burnt twin (the Echo pairs say which, so the Echo
    // shows it clean again), and Supplies is replaced by a burnt copy of the Prologue's as it stands. The doors and
    // everything the act put in the rooms stay this scene's own: Fit To New Office runs afterwards, so what lies on
    // the desks finds room among the moved things and the walk map is baked again.
    [MenuItem("HellScape/Office/Copy The Prologue's Office Here")]
    public static void CopyPrologue()
    {
        var here = GameObject.Find(ModelName);
        if (EditorApplication.isPlaying || here == null || !IsKv(here.transform) || GameObject.Find("OfficeAct") == null) { Debug.LogError("Open MainGameScene (not in Play) with the Kessler & Vane office in it."); return; }
        var settings = Resources.Load<EchoOfficeSettings>("EchoOfficeSettings");
        var notes = new System.Text.StringBuilder();
        Material Burnt(Material clean)
        {
            if (clean == null) return null;
            string path = AssetDatabase.GetAssetPath(clean);
            var twin = AssetDatabase.LoadAssetAtPath<Material>(path.Replace("/Clean/KV_Clean_", "/Burned/KV_Burned_").Replace("/sup_", "/burnt_sup_"));
            if (twin != null && twin != clean) return twin;
            foreach (var p in settings.materials) if (p.before == clean && p.burnt != null) return p.burnt;
            if (!notes.ToString().Contains(clean.name)) notes.AppendLine("no burnt twin for " + clean.name + ": worn as it is");
            return clean;
        }
        int changed = 0, pieces = 0;
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity", OpenSceneMode.Additive);
        try
        {
            var there = scene.GetRootGameObjects().FirstOrDefault(g => g.name == ModelName);
            var kit = there != null ? there.transform.Find("Supplies") : null;
            if (kit == null) { Debug.LogError("The Prologue has no Kessler & Vane office with supplies."); return; }
            foreach (var r in there.GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform.IsChildOf(kit)) continue;
                string path = r.name;
                for (var t = r.transform.parent; t != there.transform; t = t.parent) path = t.name + "/" + path;
                var mine = here.transform.Find(path);
                var worn = r.sharedMaterials.Select(Burnt).ToArray();
                if (mine == null || mine.GetComponent<Renderer>() == null || worn.SequenceEqual(mine.GetComponent<Renderer>().sharedMaterials)) continue;
                Undo.RecordObject(mine.GetComponent<Renderer>(), "Prologue materials");
                mine.GetComponent<Renderer>().sharedMaterials = worn;
                changed++;
            }
            var old = here.transform.Find("Supplies");
            var copy = Object.Instantiate(kit.gameObject, here.transform);
            copy.name = "Supplies";
            Undo.RegisterCreatedObjectUndo(copy, "Prologue supplies");
            foreach (var r in copy.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials = r.sharedMaterials.Select(Burnt).ToArray();
            if (old != null) { copy.transform.SetSiblingIndex(old.GetSiblingIndex()); Undo.DestroyObjectImmediate(old.gameObject); }
            pieces = copy.transform.childCount;
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
        Physics.SyncTransforms();
        Run();
        Report = "copied from the Prologue: " + changed + " surfaces re-dressed, " + pieces + " supplies" + System.Environment.NewLine + notes + Report;
        Debug.Log(Report);
    }

    // Cubicles round the eighteen single desks of the open-plan floor, each laid out the way the user set one up by
    // hand: the monitor in the middle of the desk facing the chair, keyboard and mouse in front of it, the tower and
    // the papers on the far side, the chair in the cubicle's doorway. The offsets are metres east and north of the
    // desk's middle for a desk whose chair is to the east; the other column of each bank is that turned half round.
    // The cubicle is 2.42 m deep and the desks stand 2.09 m apart, so it is squeezed to fit between its neighbours.
    // Works on the office as it stands (no reinstall): a desk that has its cubicle is left alone.
    [MenuItem("HellScape/Office/Cubicles On The Open-Plan Desks")]
    public static void Cubicles()
    {
        var mo = GameObject.Find(ModelName);
        if (mo == null || !IsKv(mo.transform) || mo.transform.Find("Supplies") == null) { Debug.LogError("Needs the Kessler & Vane office with its supplies in the open scene."); return; }
        var m = model = mo.transform;
        supplies = m.Find("Supplies");
        pack = AssetDatabase.LoadAssetAtPath<GameObject>(SuppliesFbx);
        dressBurnt = GameObject.Find("OfficeAct") != null;
        Physics.SyncTransforms();
        var kit = supplies.Cast<Transform>().ToList();
        Bounds Of(Transform t) { var rs = t.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
        void Pose(Transform t, Vector3 at, Vector3 front)   // (keeps its height)
        {
            if (t == null) return;
            t.rotation = Quaternion.LookRotation(front);
            var b = Of(t);
            t.position += new Vector3(at.x - b.center.x, 0f, at.z - b.center.z);
        }
        Transform Nearest(string name, Vector3 to, float reach) =>
            kit.Where(t => t.name == name && Flat(Of(t).center, to) < reach).OrderBy(t => Flat(Of(t).center, to)).FirstOrDefault();
        Transform Walls(Transform piece)   // (you walk round it and into it: the mesh, not a box)
        {
            Object.DestroyImmediate(piece.GetComponent<BoxCollider>());
            foreach (var mf in piece.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            return piece;
        }

        int made = 0;
        foreach (var top in Islands(m).Where(i => i.room == "open-office" && i.mat == "Wood" && i.Is(1.72f, .09f, 1.87f)))
        {
            var d = top.c;
            if (kit.Any(t => t.name == "Cubicle" && Over(Of(t), d, 0f))) continue;
            var chair = Nearest("Chair", d, 1.8f);
            if (chair == null) continue;
            var o = Snap(Of(chair).center - d);
            if (o.x == 0f) continue;   // (these desks have their chair to the east or to the west)
            // one the user stood there by hand (a piece of the pack dropped in the scene): the squeezed one takes its place
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                if (mf.sharedMesh != null && mf.sharedMesh.name == "Cubicle" && !mf.transform.IsChildOf(supplies) && Over(mf.GetComponent<Renderer>().bounds, d, 0f)) mf.gameObject.SetActive(false);
            Vector3 At(float e, float n) => new Vector3(d.x + o.x * e, m.position.y, d.z + o.x * n);
            foreach (var (name, e, n) in new[] { ("Monitor", -.005f, -.25f), ("Keyboard", .615f, -.22f), ("Mouse", .62f, -.635f), ("PC tower", -.07f, .35f), ("Papers", .505f, .305f), ("Coffee cup", .135f, -.7f) })
                Pose(kit.FirstOrDefault(t => t.name == name && Over(top.b, Of(t).center, .05f)), At(e, n), o);
            var bin = Nearest("Waste basket", Of(chair).center, 1.3f);
            Pose(chair, At(.96f, -.255f), -o);
            Pose(bin, At(1.3f, .72f), o);
            Walls(Put("Cubicle", At(.4275f, .0755f), Vector3.Cross(o, Vector3.up), new Vector3(1f, 1f, .856f), false, "Cubicle"));
            made++;
        }

        // ---- the security room's fourth workstation stands on air in the model: the pack's corner desk goes under
        // it, along the south wall, and its things are set out on that
        var lone = Nearest("Monitor", P(45.9f, 0f, 21f), .6f);
        if (lone != null && !kit.Any(t => t.name == "Corner desk"))
        {
            var size = Size("Cubicle Desk");
            float top = Of(lone).min.y, south = Wall(46.2f, .5f, 22f, Vector3.back, "security desk").z + .03f, deep = size.z * 1.3f;
            var chair = Nearest("Chair", Of(lone).center, 2.4f);
            Vector3 On(float e, float n) => new Vector3(DoorX + e, m.position.y, south + n);
            Walls(Put("Corner desk", On(46.2f, deep * .5f), Vector3.left, new Vector3(1.3f, (top - m.position.y) / size.y, 1f), false, "Cubicle Desk"));
            Pose(Nearest("Keyboard", Of(lone).center, .9f), On(46.4f, .67f), Vector3.forward);
            Pose(Nearest("Mouse", Of(lone).center, .9f), On(46.77f, .67f), Vector3.forward);
            Pose(Nearest("Coffee cup", Of(lone).center, .9f), On(46.95f, .3f), Vector3.forward);
            Pose(Nearest("Papers", Of(lone).center, .9f), On(45.36f, 1.3f), Vector3.forward);
            Pose(lone, On(46.4f, .35f), Vector3.forward);
            Pose(chair, On(46.4f, 1.5f), Vector3.back);
            made++;
        }
        Physics.SyncTransforms();
        if (dressBurnt && made > 0) EchoPairs();   // (the Echo shows the new pieces unburnt too)
        EditorSceneManager.MarkSceneDirty(mo.scene);
        log.AppendLine("cubicles and desks added: " + made);
        Debug.Log("Open-plan cubicles: " + made + " added.");
    }

    // the company's sign (the scene's "Plane" with the CompanyLogo material) hangs in the lobby, facing the front doors
    public static void Logo()
    {
        var m = GameObject.Find(ModelName);
        if (m == null) return;
        model = m.transform;
        var sign = m.scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Plane" && g.GetComponent<Renderer>() != null
                                                                  && g.GetComponent<Renderer>().sharedMaterial != null && g.GetComponent<Renderer>().sharedMaterial.name == "CompanyLogo");
        if (sign == null) return;
        Physics.SyncTransforms();
        sign.SetActive(true);
        sign.transform.SetPositionAndRotation(Wall(-2.3f, 1.75f, 14.5f, Vector3.forward, "company logo") + Vector3.back * .02f, Quaternion.Euler(90f, 180f, 0f));
    }

    // The model's doors are separate leaves and most stand a hand's width ajar: nobody gets past one. Those are
    // swung right open (taken away where a wall or a cabinet is in the way of that). The ones that are shut stay
    // shut: they are the locked doors of the floor plan, but for the `unlocked` (the conference room's always: the
    // locker key is in there).
    // The front doors are pushed to: they are the way out.
    static void Doors(Transform m, string[] unlocked)
    {
        foreach (var mf in m.GetComponentsInChildren<MeshFilter>().Where(x => x.name.StartsWith("DoorLeaf_")))
        {
            var leaf = mf.transform.parent; var b = mf.GetComponent<Renderer>().bounds;   // (each leaf hangs on a hinge object of its own)
            if (leaf == m) { log.AppendLine(mf.name + ": no hinge, left as it is"); continue; }
            Vector3 p = b.size.x > b.size.z ? Vector3.forward : Vector3.right;
            bool shut = Mathf.Min(b.size.x, b.size.z) < .2f;
            if (shut && !unlocked.Any(id => mf.name.StartsWith("DoorLeaf_" + id))) continue;
            Vector3 hinge = leaf.position, f = (b.center - hinge) * 2f;
            hinge.y = b.center.y; f.y = 0f;
            if (mf.name.StartsWith("DoorLeaf_D01"))
            {
                var along = Vector3.Cross(Vector3.up, p);
                leaf.RotateAround(hinge, Vector3.up, Vector3.SignedAngle(f, along * Mathf.Sign(Vector3.Dot(f, along)), Vector3.up));
                continue;
            }
            float len = f.magnitude; bool done = false;
            foreach (float side in shut ? new[] { 1f, -1f } : new[] { Mathf.Sign(Vector3.Dot(f, p)) })
            {
                var t = p * side;
                var hits = Physics.OverlapBox(hinge + t * (len * .5f + .12f), new Vector3(.04f, 1f, len * .5f - .15f), Quaternion.LookRotation(t), ~0, QueryTriggerInteraction.Ignore);
                if (hits.Any(c => c.transform != mf.transform)) continue;
                leaf.RotateAround(hinge, Vector3.up, Vector3.SignedAngle(f, t, Vector3.up));
                done = true; break;
            }
            if (!done) { log.AppendLine(leaf.name + ": no room to open, taken off its hinges"); Object.DestroyImmediate(leaf.gameObject); }
            Physics.SyncTransforms();
        }
    }

    // the Echo shows the office as it was: every burnt material of the model has its clean twin
    static void EchoPairs()
    {
        var settings = Resources.Load<EchoOfficeSettings>("EchoOfficeSettings");
        if (settings == null) { log.AppendLine("no Resources/EchoOfficeSettings"); return; }
        // (the last office's pairs go: nothing wears those materials now, and Resources would ship them)
        var pairs = settings.materials.Where(p => p.burnt != null && p.before != null && !AssetDatabase.GetAssetPath(p.before).Contains("unity-export-groundfloor")).ToList();
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { KvMats + "/Burned", KvDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var burnt = AssetDatabase.LoadAssetAtPath<Material>(path);
            var clean = AssetDatabase.LoadAssetAtPath<Material>(path.Replace("/Burned/KV_Burned_", "/Clean/KV_Clean_").Replace("/burnt_sup_", "/sup_"));
            if (clean == null || clean == burnt || pairs.Any(p => p.burnt == burnt)) continue;
            pairs.Add(new EchoOfficeSettings.MaterialPair { burnt = burnt, before = clean });
        }
        settings.materials = pairs.ToArray();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    // ---------------------------------------------------------------- islands

    class Island
    {
        public string room, mat; public MeshFilter mf; public Bounds b; public List<int> tris = new List<int>(); public bool gone;
        public Vector3 c => b.center;
        public bool Is(float a, float h, float d, float tol = .045f) =>
            Mathf.Abs(b.size.y - h) < tol && (Mathf.Abs(b.size.x - a) < tol && Mathf.Abs(b.size.z - d) < tol || Mathf.Abs(b.size.x - d) < tol && Mathf.Abs(b.size.z - a) < tol);
    }

    static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
    static Vector3 Snap(Vector3 v) => Mathf.Abs(v.x) >= Mathf.Abs(v.z) ? new Vector3(Mathf.Sign(v.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(v.z));
    static bool Over(Bounds b, Vector3 p, float grow) => Mathf.Abs(p.x - b.center.x) < b.extents.x + grow && Mathf.Abs(p.z - b.center.z) < b.extents.z + grow;

    static List<Island> Islands(Transform m)
    {
        var all = new List<Island>();
        foreach (var mf in m.GetComponentsInChildren<MeshFilter>(true))
        {
            var part = mf.name.Split('_');
            if (part.Length != 3 || part[0] != "Furniture") continue;
            var mesh = mf.sharedMesh; var v = mesh.vertices; var t = mesh.triangles; var l2w = mf.transform.localToWorldMatrix;
            var p = new int[v.Length]; var first = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = l2w.MultiplyPoint3x4(v[i]);
                var k = Vector3Int.RoundToInt(v[i] * 500f);
                if (first.TryGetValue(k, out int f)) p[i] = f; else { first[k] = i; p[i] = i; }
            }
            int Find(int i) { while (p[i] != i) { p[i] = p[p[i]]; i = p[i]; } return i; }
            for (int i = 0; i < t.Length; i += 3) { int a = Find(t[i]); p[Find(t[i + 1])] = a; p[Find(t[i + 2])] = a; }
            var by = new Dictionary<int, Island>();
            for (int i = 0; i < t.Length; i += 3)
            {
                int r = Find(t[i]);
                if (!by.TryGetValue(r, out var isl)) { isl = by[r] = new Island { room = part[1], mat = part[2], mf = mf, b = new Bounds(v[t[i]], Vector3.zero) }; all.Add(isl); }
                for (int k = 0; k < 3; k++) { isl.b.Encapsulate(v[t[i + k]]); isl.tris.Add(t[i + k]); }
            }
        }
        return all;
    }

    // ---------------------------------------------------------------- the supplies

    static Transform supplies; static GameObject pack; static bool dressBurnt;
    static readonly Quaternion Upright = Quaternion.LookRotation(Vector3.right, Vector3.up);   // the pack's pieces face -x...
    static readonly Quaternion Lying = Quaternion.LookRotation(Vector3.right, Vector3.back);   // ...and the flat ones stand on edge to be looked at

    static Material SupplyMat(Material src)
    {
        if (src == null || !AssetDatabase.GetAssetPath(src).StartsWith("Assets/")) return src;
        string key = string.Concat(src.name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        string path = KvDir + "/sup_" + key + ".mat";
        var clean = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (clean == null)
        {
            clean = new Material(src) { name = "sup_" + key };
            if (src.name == "screen") { clean.SetColor("_BaseColor", new Color(.02f, .03f, .03f)); clean.SetFloat("_Smoothness", .8f); }   // (the pack's is a green placeholder)
            if (src.name == "Aglaonema Leaf" || src.name == "Mesh Basket" || src.name == "Steam") { clean.SetFloat("_AlphaClip", 1f); clean.EnableKeyword("_ALPHATEST_ON"); clean.SetFloat("_Cull", 0f); clean.renderQueue = 2450; }
            AssetDatabase.CreateAsset(clean, path);
        }
        if (!dressBurnt) return clean;
        path = KvDir + "/burnt_sup_" + key + ".mat";
        var burnt = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (burnt == null)
        {
            burnt = new Material(clean) { name = "burnt_sup_" + key };
            var c = clean.GetColor("_BaseColor");
            burnt.SetColor("_BaseColor", new Color(c.r * .36f, c.g * .32f, c.b * .3f, c.a));
            burnt.SetFloat("_Smoothness", .05f);
            var soot = AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/Burnt/ember_soot.png");
            if (soot != null) { burnt.SetTexture("_DetailAlbedoMap", soot); burnt.SetFloat("_DetailAlbedoMapScale", 1f); burnt.EnableKeyword("_DETAIL_MULX2"); }
            AssetDatabase.CreateAsset(burnt, path);
        }
        return burnt;
    }

    // A piece of the pack (one or more of its objects, kept as they stand to each other) with the middle of its
    // footprint at `at`, its foot at at.y, facing `front`. `scale` is across, up, front to back.
    static Transform Put(string name, Vector3 at, Vector3 front, Vector3 scale, bool lying, params string[] parts)
    {
        var holder = new GameObject(name).transform;
        holder.SetParent(supplies, false);
        var pivot = new GameObject("pivot").transform;
        pivot.SetParent(holder, false);
        foreach (var part in parts)
        {
            var src = pack.transform.Find(part);
            if (src == null) { log.AppendLine("no '" + part + "' in the supplies pack"); continue; }
            var g = Object.Instantiate(src.gameObject, pivot);
            g.name = part;
            g.transform.localPosition = src.localPosition; g.transform.localRotation = src.localRotation; g.transform.localScale = src.localScale;
            foreach (var r in g.GetComponentsInChildren<Renderer>()) r.sharedMaterials = r.sharedMaterials.Select(SupplyMat).ToArray();
        }
        pivot.localRotation = lying ? Lying : Upright;
        holder.rotation = Quaternion.LookRotation(front);
        holder.localScale = scale;
        var rs = holder.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return holder;
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        holder.position += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
        b.center += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
        var box = holder.gameObject.AddComponent<BoxCollider>();
        box.center = holder.InverseTransformPoint(b.center);
        var s = holder.InverseTransformVector(b.size);
        box.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        return holder;
    }

    static Vector3 Size(params string[] parts)
    {
        var rs = parts.Select(p => pack.transform.Find(p).GetComponent<Renderer>()).ToArray();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return b.size;   // (the pack as it lies in the file: x front to back, z across)
    }

    // room to put something down: nothing solid in the box but the floor under it
    static bool Free(Vector3 foot, Vector3 size) =>
        !Physics.CheckBox(foot + Vector3.up * (size.y * .5f + .06f), new Vector3(size.x * .5f, size.y * .5f - .03f, size.z * .5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

    static void Dress(Transform m, bool burnt)
    {
        if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Generated", "Office");
        if (!AssetDatabase.IsValidFolder(KvDir)) AssetDatabase.CreateFolder(GenDir, "KV");
        pack = AssetDatabase.LoadAssetAtPath<GameObject>(SuppliesFbx);
        dressBurnt = burnt;
        supplies = new GameObject("Supplies").transform;
        supplies.SetParent(m, false);
        supplies.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var all = Islands(m);
        var todo = new List<System.Action>();
        var count = new Dictionary<string, int>();
        void Did(string what) { count.TryGetValue(what, out int n); count[what] = n + 1; }
        Vector3 Centre(string room) { var f = m.Find("Floor_" + room); return f != null ? f.GetComponent<Renderer>().bounds.center : m.position; }
        // the top of a desk or a table that is staying, under a point
        bool OnTop(Vector3 p, float y, float margin) =>
            all.Any(i => !i.gone && i.mat != "Foliage" && Mathf.Abs(i.b.max.y - y) < .03f && Over(i.b, p, -margin));

        // ---- the store room's north shelving stands right across its door to the open-plan floor: out it goes
        var across = new Bounds(new Vector3(DoorX + 20.6f, Floor + 1f, DoorZ + 23.9f), new Vector3(5.6f, 3f, 2f));
        foreach (var i in all.Where(i => i.room == "storage" && across.Contains(i.c))) i.gone = true;

        // ---- plants: one per pot, about as big as the one it replaces
        foreach (var pot in all.Where(i => i.mat == "Pot"))
        {
            float s = Mathf.Clamp(pot.b.size.x / .3f, 1.2f, 2.2f);
            var at = new Vector3(pot.c.x, pot.b.min.y, pot.c.z);
            todo.Add(() => Put("Potted plant", at, Vector3.forward, Vector3.one * s, false, "Potted Plant")); Did("plants");
        }
        foreach (var i in all.Where(i => i.mat == "Pot" || i.mat == "Foliage")) i.gone = true;

        // ---- workstations: screen, keyboard, the mug and the paper beside them
        var stations = new List<(Vector3 at, Vector3 front, Vector3 right)>();
        foreach (var screen in all.Where(i => i.mat == "Screen" && i.Is(.51f, .27f, .01f)))
        {
            Island Near(string mat, float a, float h, float d, float reach) =>
                all.Where(i => !i.gone && i.room == screen.room && i.mat == mat && i.Is(a, h, d) && Flat(i.c, screen.c) < reach).OrderBy(i => Flat(i.c, screen.c)).FirstOrDefault();
            var body = Near("Hardware", .60f, .38f, .16f, .3f);
            var stand = Near("Hardware", .30f, .035f, .22f, .3f);
            var keys = Near("Hardware", .52f, .03f, .18f, .6f);
            var paper = Near("Paper", .30f, .01f, .38f, .9f);
            var mug = Near("Ceramic", .11f, .10f, .11f, .9f);
            if (body == null || keys == null) { log.AppendLine("a screen without a monitor or a keyboard at " + screen.c); continue; }
            var front = Snap(keys.c - body.c); var right = Vector3.Cross(Vector3.up, front);
            float y = body.b.min.y;
            foreach (var i in new[] { screen, body, stand, keys, paper, mug }) if (i != null) i.gone = true;
            var at = new Vector3(body.c.x, y, body.c.z); var kat = new Vector3(keys.c.x, y, keys.c.z);
            stations.Add((at, front, right));
            todo.Add(() => Put("Monitor", at, front, Vector3.one, false, "Computer Monitor"));
            todo.Add(() => Put("Keyboard", kat, front, Vector3.one, true, "Keyboard"));
            todo.Add(() => Put("Mouse", kat + right * .37f, front, Vector3.one, true, "mouse"));
            if (paper != null) { var pat = new Vector3(paper.c.x, y, paper.c.z); todo.Add(() => Put("Papers", pat, front, Vector3.one, false, "Stack of Papers 1")); }
            if (mug != null) { var mat = new Vector3(mug.c.x, y, mug.c.z); todo.Add(() => Put("Coffee cup", mat, front, Vector3.one, false, "Coffee Cup")); }
            Did("workstations");
        }

        // ---- Vane's assistant works across his desk from him: her computer (the Prologue seats Maya at it)
        {
            Vector3 front = Vector3.forward, right = Vector3.Cross(Vector3.up, front);
            var at = new Vector3(DoorX + 67.5f, 0f, DoorZ + 36.9f);
            var top = all.Where(i => !i.gone && i.room == "vane" && Over(i.b, at, 0f) && i.b.max.y < m.position.y + 1f).OrderByDescending(i => i.b.max.y).FirstOrDefault();
            if (top != null)
            {
                at.y = top.b.max.y;
                var kat = new Vector3(at.x, 0f, DoorZ + 37.27f);
                kat.y = all.Where(i => !i.gone && i.room == "vane" && Over(i.b, kat, 0f) && i.b.max.y < m.position.y + 1f).Select(i => i.b.max.y).DefaultIfEmpty(at.y).Max();
                stations.Add((at, front, right));
                todo.Add(() => Put("Monitor", at, front, Vector3.one, false, "Computer Monitor"));
                todo.Add(() => Put("Keyboard", kat, front, Vector3.one, true, "Keyboard"));
                todo.Add(() => Put("Mouse", kat + right * .37f, front, Vector3.one, true, "mouse"));
                Did("workstations");
            }
        }

        // ---- chairs: a seat, its back, the base under it. Each faces its work: the monitor if it has one, or the
        // table it is drawn up to (the model's backs are no guide: half of them are on the desk side)
        var chairs = new List<(Vector3 at, Vector3 front, string room)>();
        foreach (var seat in all.Where(i => i.mat == "Leather" && i.Is(.58f, .13f, .58f)))
        {
            var back = all.Where(i => i != seat && i.mat == "Leather" && i.b.size.y > .38f && i.b.size.y < .66f && Mathf.Min(i.b.size.x, i.b.size.z) < .16f && Flat(i.c, seat.c) < .45f)
                          .OrderBy(i => Flat(i.c, seat.c)).FirstOrDefault();
            var front = Snap(back != null ? seat.c - back.c : Centre(seat.room) - seat.c);
            var table = all.Where(i => !i.gone && i.mat != "Leather" && Mathf.Abs(i.b.max.y - m.position.y - .83f) < .12f && i.b.size.x * i.b.size.z > 1f)
                           .Select(i => i.b.ClosestPoint(new Vector3(seat.c.x, i.b.center.y, seat.c.z))).OrderBy(q => Flat(q, seat.c)).FirstOrDefault();
            if (table != default && Flat(table, seat.c) < 1.3f && Flat(table, seat.c) > .01f) front = Snap(table - seat.c);
            var screen = stations.OrderBy(st => Flat(st.at, seat.c)).FirstOrDefault();
            if (screen.front != default && Flat(screen.at, seat.c) < 2.2f) front = -screen.front;
            seat.gone = true; if (back != null) back.gone = true;
            foreach (var h in all.Where(i => i.room == seat.room && i.mat == "Hardware" && Flat(i.c, seat.c) < .42f && (i.Is(.45f, .12f, .45f) || i.Is(.08f, .32f, .08f) || i.Is(.07f, .07f, .45f)))) h.gone = true;
            var at = new Vector3(seat.c.x, seat.b.min.y - .42f, seat.c.z);
            chairs.Add((at, front, seat.room));
            todo.Add(() => Put("Chair", at, front, Vector3.one * .8f, false, "Chair")); Did("chairs");
        }

        // ---- the security console's three screens: the pack's CCTV sets
        foreach (var tv in all.Where(i => i.mat == "Screen" && i.Is(.82f, .08f, .60f)))
        {
            tv.gone = true;
            var at = new Vector3(tv.c.x, tv.b.min.y - .03f, tv.c.z); var front = Snap(Centre(tv.room) - tv.c);
            todo.Add(() => Put("CCTV set", at, front, Vector3.one, false, "Panasonic VHS TV")); Did("CCTV sets");
        }

        // ---- cabinets: a metal box with drawer lines on its front, filled with a row of filing cabinets
        var tops = new List<(Vector3 at, Vector3 front, float h, string room)>();
        foreach (var box in all.Where(i => i.mat == "Metal" && i.room != "kitchen" && i.b.min.y < m.position.y + .05f && i.b.size.y > .75f && i.b.size.y < 1.95f && Mathf.Max(i.b.size.x, i.b.size.z) < 4.7f))
        {
            var line = all.FirstOrDefault(i => i.room == box.room && i.mat == "Hardware" && i.b.size.y < .03f && Mathf.Max(i.b.size.x, i.b.size.z) > .7f && Over(box.b, i.c, .15f) && i.c.y < box.b.max.y);
            if (line == null) continue;
            var front = Snap(line.c - box.c); var right = Vector3.Cross(Vector3.up, front);
            box.gone = true;
            foreach (var i in all.Where(i => i.room == box.room && (i.mat == "Hardware" && i.b.size.y < .05f || i.mat == "Paper" && i.b.size.y < .06f) && Over(box.b, i.c, .15f) && i.c.y < box.b.max.y)) i.gone = true;
            bool tall = box.b.size.y > 1.05f;
            string piece = tall ? "4 Drawer Filing Cabinet" : "2 Drawer Filing Cabinet";
            var size = Size(piece);
            float width = Mathf.Abs(Vector3.Dot(box.b.size, right)), depth = Mathf.Abs(Vector3.Dot(box.b.size, front)), up = box.b.size.y / size.y;
            int n = Mathf.Max(1, Mathf.RoundToInt(width / (size.z * up)));
            for (int k = 0; k < n; k++)
            {
                var at = new Vector3(box.c.x, box.b.min.y, box.c.z) + right * ((k + .5f) / n - .5f) * width;
                var scale = new Vector3(width / n / size.z, up, depth / size.x);
                todo.Add(() => Put(piece, at, front, scale, false, piece)); Did("filing cabinets");
            }
            tops.Add((new Vector3(box.c.x, box.b.max.y, box.c.z), front, box.b.size.y, box.room));
        }

        // ---- cut what was replaced out of the model's meshes
        foreach (var g in all.GroupBy(i => i.mf))
        {
            if (g.All(i => i.gone)) { Object.DestroyImmediate(g.Key.gameObject); continue; }
            if (!g.Any(i => i.gone)) continue;
            string path = KvDir + "/" + g.Key.name + ".asset";
            var cut = Object.Instantiate(g.Key.sharedMesh);
            cut.name = g.Key.name;
            cut.triangles = g.Where(i => !i.gone).SelectMany(i => i.tris).ToArray();
            cut.RecalculateBounds();
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(cut, path); saved = cut; }
            else { EditorUtility.CopySerialized(cut, saved); Object.DestroyImmediate(cut); }   // (in place: the other scene's office uses it too)
            g.Key.sharedMesh = saved;
            var col = g.Key.GetComponent<MeshCollider>();
            if (col != null) { col.sharedMesh = null; col.sharedMesh = saved; }
        }
        foreach (var put in todo) put();
        Physics.SyncTransforms();

        // ---- and what the pack has that the model never had
        // on the desk beside each monitor: the tower, the telephone
        foreach (var (at, front, right) in stations)
        {
            var tower = at + right * .52f - front * .02f;
            if (OnTop(tower, at.y, .12f) && Free(tower, new Vector3(.2f, .44f, .2f))) { Put("PC tower", tower, front, Vector3.one, false, "PC Tower"); Did("PC towers"); }
            var phone = at - right * .5f + front * .12f;
            if (OnTop(phone, at.y, .1f) && Free(phone, new Vector3(.16f, .1f, .16f))) { Put("Telephone", phone, front, Vector3.one, true, "Body", "Phone handle"); Did("telephones"); }
        }
        // a bin on the floor by every desk chair
        foreach (var (at, front, room) in chairs.Where(c => c.room != "conference" && c.room != "kitchen"))
            foreach (float side in new[] { .74f, -.74f })
            {
                var bin = at + Vector3.Cross(Vector3.up, front) * side - front * .1f;
                if (!Free(bin, new Vector3(.4f, .46f, .4f))) continue;
                Put("Waste basket", bin, front, Vector3.one, false, "Trash Cam"); Did("waste baskets");
                Physics.SyncTransforms();
                break;
            }
        // on the cabinets: a printer on the chest-high ones, binders on the low ones
        foreach (var g in tops.GroupBy(t => t.room))
        {
            var t = g.OrderBy(x => Mathf.Abs(x.h - 1.15f)).First();
            if (t.h > .9f && t.h < 1.3f) { Put("Printer", t.at, t.front, Vector3.one, false, "Printer", "Tray 1", "Tray 2"); Did("printers"); }
            foreach (var low in g.Where(x => x.h <= .9f)) { Put("Binders", low.at, low.front, Vector3.one, false, "Binders"); Did("binders"); }
        }
        Physics.SyncTransforms();
        log.AppendLine("supplies: " + string.Join(", ", count.Select(c => c.Value + " " + c.Key)));
    }
}
