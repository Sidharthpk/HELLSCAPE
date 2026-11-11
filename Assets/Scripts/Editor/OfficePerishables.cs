using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The user's Assets/Perishables packs (2026-10-21: "use all of the assets and make the office look great and fill
// it up", their floor plan as the reference) cut into single pieces and stood round the v3 office.
//
// Pieces: a pack's merged meshes (the Mersus office's desks, chairs and computers are one mesh each) are cut into
// their loose parts, a furnished room (the private detective's office, the control room) gives whole groups by a
// box round them. Each piece is baked to ONE mesh, foot at its origin, front along +z, saved under
// Generated/Office/Perishables with a prefab. Materials: the v3 model's own when it has the same one (it was built
// from some of these packs), else a copy with the pack's loose textures wired in.
public static partial class NewOfficeFit
{
    const string PerishDir = GenDir + "/Perishables";
    const string Mersus = "Assets/Perishables/mersus-office/source/OfficeforSketchFab.fbx";
    const string Detective = "Assets/Perishables/private-detectives-office/source/private_detective.fbx";
    const string OfficeProps = "Assets/Perishables/office-props/source/Sketchfab_Models.fbx";
    const string Control = "Assets/Perishables/control-room-by-amogusstrikesback2/source/control room 3D Model Avatar of amogusstrikesback2 amogusstrikes.glb";
    const string Loft = "Assets/Perishables/loft-18-mini-office-venvironment/source/17 лофт SAVE с окружением.fbx";
    const string ReceptionDesk = "Assets/Perishables/reception-desk-01/source/Reception Desk_01.fbx";
    const string Barrel = "Assets/Perishables/free-barrel/source/Barrel.fbx.fbx";
    const string Blood = "Assets/Perishables/blood-decals-psx/source/blood decals.glb";

    class Def
    {
        public string key, path;
        public string[] names;          // renderer name prefixes (null: any)
        public Vector3? near;           // cut the loose part nearest this point (source space) out of a merged mesh
        public Bounds? region;          // or: every renderer whose middle is in this box (source space)
        public float yaw, scale = 1f;   // turn so its front is +z; size
    }

    static Def D(string key, string path, string name, float yaw = 0f, float scale = 1f) => new Def { key = key, path = path, names = new[] { name }, yaw = yaw, scale = scale };
    static Def Cut(string key, string path, string name, Vector3 near, float yaw = 0f) => new Def { key = key, path = path, names = new[] { name }, near = near, yaw = yaw };
    static Def Box(string key, string path, Vector3 min, Vector3 max, float yaw = 0f, params string[] names) =>
        new Def { key = key, path = path, names = names.Length > 0 ? names : null, region = new Bounds((min + max) * .5f, max - min), yaw = yaw };

    static readonly Def[] Defs =
    {
        // the Mersus office (the v3 model's workstation banks are this pack)
        Cut("swivel chair", Mersus, "Spine_low.001", new Vector3(.64f, .48f, -.19f), 180f),
        Cut("brown chair", Mersus, "BrownChairs", new Vector3(.29f, .37f, 7.13f)),
        Cut("monitors", Mersus, "Computers", new Vector3(.61f, .97f, -1.07f)),
        Cut("pc tower", Mersus, "Computers", new Vector3(2.5f, .9f, -7.12f)),
        Cut("keyboard", Mersus, "Computers", new Vector3(.72f, .76f, -.71f)),
        Cut("big white table", Mersus, "BigWhiteTables", new Vector3(1.32f, .37f, 3.15f)),
        Cut("small white table", Mersus, "SmallWhiteTables", new Vector3(-.43f, .36f, -1.34f), 90f),
        Cut("wooden desk", Mersus, "WoodenDesks", new Vector3(1.65f, .33f, -7.51f), 90f),
        Cut("corner desk", Mersus, "WoodenDesks", new Vector3(-1.91f, .33f, -4.86f)),
        D("meeting table", Mersus, "MeetingTable_New_Tabletop_Joined.001"),
        D("shelving", Mersus, "Shelving"),

        // the private detective's office: whole groups (front = into the room they stood in)
        Box("detective desk", Detective, new Vector3(-1.95f, 0f, -1.2f), new Vector3(-.8f, 2f, 1.45f), -90f),
        Box("detective chair", Detective, new Vector3(-3.3f, 0f, -.7f), new Vector3(-2.2f, 1.5f, .5f), -90f, "chair"),
        Box("leather armchair", Detective, new Vector3(2.5f, 0f, 1.4f), new Vector3(3.6f, 1.2f, 2.4f), 0f, "chairs"),
        Box("leather sofa", Detective, new Vector3(2.8f, 0f, -1.9f), new Vector3(4f, 1.2f, 1f), 90f, "Sofa"),
        Box("sideboard", Detective, new Vector3(-.6f, 0f, -3.05f), new Vector3(1.45f, 2f, -2.2f)),
        Box("old safe", Detective, new Vector3(-1.45f, 0f, -2.95f), new Vector3(-.75f, 1f, -2.25f), 0f, "safe"),
        Box("bookcase", Detective, new Vector3(-3.85f, 0f, 2.55f), new Vector3(-2.3f, 2.3f, 3.0f), 180f),
        Box("low bookshelf", Detective, new Vector3(-1.2f, 0f, 2.55f), new Vector3(.35f, 1.2f, 3.0f), 180f),
        Box("locker row", Detective, new Vector3(-3.97f, 0f, -3.0f), new Vector3(-1.95f, 1.9f, -2.3f), 0f, "locker"),
        Box("small lockers", Detective, new Vector3(-2.2f, 0f, 2.55f), new Vector3(-1.3f, 1.5f, 3.1f), 180f, "locker"),
        Box("work table", Detective, new Vector3(1.8f, 0f, 1.95f), new Vector3(4f, 1.3f, 3.05f), 180f),
        Box("evidence boxes", Detective, new Vector3(.4f, 0f, 2.1f), new Vector3(1.9f, .7f, 3.05f), 180f, "evidence_box"),
        Box("coat stand", Detective, new Vector3(3.05f, 0f, -2.8f), new Vector3(3.7f, 1.9f, -2.1f), 0f, "Cylinder"),
        Box("evidence wall", Detective, new Vector3(-1.25f, .95f, 2.95f), new Vector3(3.35f, 2.5f, 3.1f), 180f),
        Box("map wall", Detective, new Vector3(3.85f, .9f, -1f), new Vector3(4.1f, 2.4f, 1f), 90f),
        D("wall clock", Detective, "clock"),

        // office props (laid out along a wall to the west of them: they face +x)
        D("armchair", OfficeProps, "Chair1", -90f), D("chaise", OfficeProps, "Chaise1", -90f), D("writing desk", OfficeProps, "Desk1", -90f),
        D("fireplace", OfficeProps, "Fireplace1", -90f), D("landscape painting", OfficeProps, "PaintingLandscape1", -90f),
        D("portrait painting", OfficeProps, "PaintingPortret1", -90f), D("side table", OfficeProps, "SideTable1"),
        D("vase", OfficeProps, "Vase3"), D("coffee set", OfficeProps, "CoffeeSet1", -90f), D("bean tin", OfficeProps, "Beans1"),
        Box("books", OfficeProps, new Vector3(-12f, 0f, -7.1f), new Vector3(-11.4f, .5f, -6.6f), -90f, "Book"),

        // the control room (its desk stood on the west wall, the cabinets on the north)
        D("control desk", Control, "WhiteDesk_IS8", -90f), D("drawers", Control, "WhiteDesk_Drawer_2", -90f),
        D("control shelf", Control, "IS4_MDL_SalaControl_Estanteria"), D("wall cabinet", Control, "IS4_SalaControl_ArchivadorT2"),
        D("control sofa", Control, "IS4_MDL_SalaControl_Sofa"), D("filing cabinet", Control, "Office_FilesCabinet_3_IS8", 180f),
        D("wide filing cabinet", Control, "Office_FilesCabinet_IS8", 180f), D("wood table", Control, "OfficeWoodTable"),
        Cut("office chair", Control, "OfficeChair_IS8", new Vector3(2.13f, .51f, -4.05f)),
        D("fuse panel", Control, "IS4_MDL_SalaControl_Interruptor", 180f), D("pinboard", Control, "Office_CubicleBoard", 180f),
        D("crowbar", Control, "Item_Crowbar"), D("telephone", Control, "Telephone_telefono", -90f),
        D("crt monitor", Control, "IS4_SalaControl_MonitorT1", -90f), D("tall monitor", Control, "IS4_SalaControl_MonitorT3_1_SalaControl", -90f),
        D("computer", Control, "IS4_MDL_SalaControl_Computadora", -90f), D("carton", Control, "IS4_MDL_SalaControl_CajaCarton__1__SalaControl"),
        Cut("carton pile", Control, "IS4_MDL_SalaControl_CajaCarton__1__1", new Vector3(1.28f, .22f, -2.93f)),
        D("control panel", Control, "IS4_MDL_SalaControl_PanelControl_SalaControl_Props_Mobile_0.002"),
        D("ceiling lamp", Control, "IS4_SalaControl_LamparaTecho_2"), D("lamp tube", Control, "IS4_SalaControl_LamparaTecho_Luz"),

        // the loft
        Box("bar stool", Loft, new Vector3(1.9f, 0f, -.9f), new Vector3(2.5f, 1f, -.2f), 0f, "node_0"),
        Box("tub chair", Loft, new Vector3(.1f, 0f, 4f), new Vector3(1.2f, 1.3f, 5.1f), 0f, "node_0"),
        Box("loft sofa", Loft, new Vector3(-2.9f, 0f, -.2f), new Vector3(-.8f, 1f, 2.8f), 0f, "node_0"),
        Box("round pouf", Loft, new Vector3(-2.9f, 0f, 2.6f), new Vector3(-1.8f, 1f, 3.7f), 0f, "node_0"),
        Box("corner sofa", Loft, new Vector3(4.6f, 0f, 3.6f), new Vector3(7.1f, 1f, 7f), 90f, "node_0"),
        Box("long table", Loft, new Vector3(-1.6f, 0f, -2.6f), new Vector3(1.6f, 1f, -.4f), 0f, "node_0"),
        Box("ottoman", Loft, new Vector3(2.9f, 0f, -2.8f), new Vector3(4.4f, 1f, -1.3f), 0f, "node_0"),
        Box("big plant", Loft, new Vector3(.7f, 0f, 2.5f), new Vector3(2.4f, 1.2f, 4.3f), 0f, "Plane"),
        Box("bush planter", Loft, new Vector3(23.2f, 0f, .6f), new Vector3(24.2f, 1.2f, 1.7f), 0f, "Cube"),

        D("reception desk", ReceptionDesk, null),
        D("barrel", Barrel, "Barrel", 0f, .19f),
        Box("blood pool", Blood, new Vector3(0f, -1f, -1f), new Vector3(2f, 1f, 1f)), Box("blood spatter", Blood, new Vector3(-2f, -1f, -1f), new Vector3(0f, 1f, 1f)),

        // vehicles for the loading bay
        D("police car", "Assets/Perishables/chevrolet-caprice-police/source/chevrolet caprice police/chevrolet caprice police.fbx", null),
        D("passat", "Assets/Perishables/psx-passat-ls-1978/source/PassatLS1978.glb", null),
        D("vespa rider", "Assets/Perishables/Grandma rides a Vintage Vespa/Grandma rides a Vintage Vespa.fbx", null, 0f, 1.6f),
    };

    // ---------------------------------------------------------------- baking

    static readonly Dictionary<string, GameObject> pieces = new Dictionary<string, GameObject>();

    static GameObject Piece(string key)
    {
        if (pieces.TryGetValue(key, out var have) && have != null) return have;
        var def = Defs.First(d => d.key == key);
        string path = PerishDir + "/" + key + ".prefab";
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) go = Bake(def);
        pieces[key] = go;
        return go;
    }

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int cut = path.LastIndexOf('/');
        Folder(path.Substring(0, cut));
        AssetDatabase.CreateFolder(path.Substring(0, cut), path.Substring(cut + 1));
    }

    static GameObject Bake(Def def)
    {
        Folder(PerishDir);
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(def.path);
        var inst = (GameObject)Object.Instantiate(src);
        try
        {
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
            var byMat = new Dictionary<Material, List<int>>();
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || mf.sharedMesh == null) continue;
                if (def.names != null && !def.names.Any(n => n == null || mf.name.StartsWith(n))) continue;
                if (def.region is Bounds box && !box.Contains(r.bounds.center)) continue;
                var mesh = mf.sharedMesh; var l2w = mf.transform.localToWorldMatrix;
                var v = mesh.vertices.Select(p => l2w.MultiplyPoint3x4(p)).ToArray();
                var nrm = mesh.normals; var uv = mesh.uv;
                HashSet<int> keep = def.near is Vector3 near ? Part(mesh, v, near) : null;
                int baseIndex = verts.Count;
                verts.AddRange(v);
                norms.AddRange(nrm.Length == v.Length ? nrm.Select(n => l2w.MultiplyVector(n).normalized) : v.Select(_ => Vector3.up));
                uvs.AddRange(uv.Length == v.Length ? uv : new Vector2[v.Length]);
                for (int s = 0; s < mesh.subMeshCount && s < r.sharedMaterials.Length; s++)
                {
                    var mat = PieceMat(r.sharedMaterials[s], def.path);
                    if (!byMat.TryGetValue(mat, out var tris)) byMat[mat] = tris = new List<int>();
                    var t = mesh.GetTriangles(s);
                    for (int i = 0; i < t.Length; i += 3)
                        if (keep == null || keep.Contains(t[i])) { tris.Add(baseIndex + t[i]); tris.Add(baseIndex + t[i + 1]); tris.Add(baseIndex + t[i + 2]); }
                }
            }
            byMat = byMat.Where(p => p.Value.Count > 0).ToDictionary(p => p.Key, p => p.Value);
            if (byMat.Count == 0) { log.AppendLine("PIECE " + def.key + ": nothing matched in " + def.path); return null; }

            // foot in the middle of its footprint, front along +z, scaled
            var used = byMat.Values.SelectMany(t => t).Distinct().ToList();
            var b = new Bounds(verts[used[0]], Vector3.zero);
            foreach (int i in used) b.Encapsulate(verts[i]);
            var pivot = new Vector3(b.center.x, b.min.y, b.center.z);
            var turn = Quaternion.Euler(0f, def.yaw, 0f);
            var remap = new Dictionary<int, int>();
            var outV = new List<Vector3>(); var outN = new List<Vector3>(); var outUv = new List<Vector2>();
            foreach (int i in used.OrderBy(i => i)) { remap[i] = outV.Count; outV.Add(turn * (verts[i] - pivot) * def.scale); outN.Add(turn * norms[i]); outUv.Add(uvs[i]); }
            var baked = new Mesh { name = def.key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            baked.SetVertices(outV); baked.SetNormals(outN); baked.SetUVs(0, outUv);
            var mats = byMat.Keys.ToArray();
            baked.subMeshCount = mats.Length;
            for (int s = 0; s < mats.Length; s++) baked.SetTriangles(byMat[mats[s]].Select(i => remap[i]).ToArray(), s);
            baked.RecalculateBounds();
            baked.RecalculateTangents();
            AssetDatabase.CreateAsset(baked, PerishDir + "/" + def.key + ".asset");

            var go = new GameObject(def.key);
            go.AddComponent<MeshFilter>().sharedMesh = baked;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PerishDir + "/" + def.key + ".prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }
        finally { Object.DestroyImmediate(inst); }
    }

    // the loose part of a merged mesh nearest `near`: welded vertices joined, parts whose boxes touch kept together
    static HashSet<int> Part(Mesh mesh, Vector3[] v, Vector3 near)
    {
        var t = mesh.triangles;
        var p = new int[v.Length]; var first = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < v.Length; i++) { var k = Vector3Int.RoundToInt(v[i] * 500f); if (first.TryGetValue(k, out int f)) p[i] = f; else { first[k] = i; p[i] = i; } }
        int Find(int i) { while (p[i] != i) { p[i] = p[p[i]]; i = p[i]; } return i; }
        for (int i = 0; i < t.Length; i += 3) { int a = Find(t[i]); p[Find(t[i + 1])] = a; p[Find(t[i + 2])] = a; }
        var boxes = new Dictionary<int, Bounds>();
        foreach (int i in t) { int r = Find(i); if (boxes.TryGetValue(r, out var b)) { b.Encapsulate(v[i]); boxes[r] = b; } else boxes[r] = new Bounds(v[i], Vector3.zero); }
        var groups = boxes.Select(kv => (box: kv.Value, roots: new List<int> { kv.Key })).ToList();
        for (bool merged = true; merged;)
        {
            merged = false;
            for (int i = 0; i < groups.Count && !merged; i++)
                for (int j = i + 1; j < groups.Count; j++)
                {
                    var a = groups[i].box; a.Expand(.03f);
                    if (!a.Intersects(groups[j].box)) continue;
                    var b = groups[i].box; b.Encapsulate(groups[j].box);
                    groups[i].roots.AddRange(groups[j].roots);
                    groups[i] = (b, groups[i].roots);
                    groups.RemoveAt(j); merged = true; break;
                }
        }
        var best = groups.OrderBy(g => (g.box.center - near).sqrMagnitude).First();
        var roots = new HashSet<int>(best.roots);
        var keep = new HashSet<int>();
        for (int i = 0; i < v.Length; i++) if (roots.Contains(Find(i))) keep.Add(i);
        return keep;
    }

    static readonly Dictionary<Material, Material> pieceMats = new Dictionary<Material, Material>();

    // the v3 model's material of the same name if it has a texture; else a copy with the pack's own texture wired in
    static Material PieceMat(Material src, string packPath)
    {
        if (src == null) return null;
        if (pieceMats.TryGetValue(src, out var done)) return done;
        Material result = src;
        var v3 = AssetDatabase.LoadAllAssetsAtPath(V3Fbx).OfType<Material>().FirstOrDefault(m => m.name == src.name && m.mainTexture != null);
        if (v3 != null) result = v3;
        else if (src.mainTexture == null || !AssetDatabase.GetAssetPath(src).EndsWith(".mat"))
        {
            string pack = packPath.Substring(0, packPath.IndexOf('/', "Assets/Perishables/".Length));
            var tex = Tex(pack, src.name, "BaseColor") ?? (src.name.StartsWith("_Veneer") ? Tex(pack, "Laminate_A02_Diff", "") : null);
            string key = string.Concat(src.name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
            string path = PerishDir + "/Materials/" + key + ".mat";
            var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (copy == null)
            {
                Folder(PerishDir + "/Materials");
                bool urp = src.shader.name.StartsWith("Universal Render Pipeline");
                copy = urp ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                copy.name = key;
                if (!urp)   // (glTF shader graph: carry its colour and texture over by hand)
                {
                    var c = src.HasProperty("baseColorFactor") ? src.GetColor("baseColorFactor") : src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.white;
                    copy.SetColor("_BaseColor", c);
                    var t = src.HasProperty("baseColorTexture") ? src.GetTexture("baseColorTexture") : src.mainTexture;
                    var et = src.HasProperty("emissiveTexture") ? src.GetTexture("emissiveTexture") : null;
                    // the control room's colours are a lighting bake in the emissive slot: as the colour they take the
                    // office's own light (glowing, they would light up the blackout); only its lamp tubes glow
                    bool glow = src.name.Contains("Luz") || src.name.Contains("Emision");
                    if (t == null && et != null && !glow) { t = et; copy.SetColor("_BaseColor", Color.white); }
                    if (t != null) copy.SetTexture("_BaseMap", t);
                    copy.SetFloat("_Smoothness", .25f);
                    if (src.renderQueue > 2500 || src.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") || src.IsKeywordEnabled("_ALPHATEST_ON"))
                    { copy.SetFloat("_AlphaClip", 1f); copy.SetFloat("_Cutoff", .35f); copy.EnableKeyword("_ALPHATEST_ON"); copy.renderQueue = 2450; }
                    if (glow && et != null) { copy.EnableKeyword("_EMISSION"); copy.SetColor("_EmissionColor", Color.white * 1.5f); copy.SetTexture("_EmissionMap", et); }
                }
                if (tex != null) { copy.SetTexture("_BaseMap", tex); copy.SetColor("_BaseColor", Color.white); }
                var bm = copy.GetTexture("_BaseMap");   // (leaves and decals are cut out of their texture)
                if (bm != null && UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(bm.graphicsFormat) && (src.name.Contains("растит") || packPath.Contains("blood")))
                { copy.SetFloat("_AlphaClip", 1f); copy.SetFloat("_Cutoff", .35f); copy.EnableKeyword("_ALPHATEST_ON"); copy.SetFloat("_Cull", 0f); copy.renderQueue = 2450; }
                var nrm = Tex(pack, src.name, "Normal");
                if (nrm != null && urp) { copy.SetTexture("_BumpMap", nrm); copy.EnableKeyword("_NORMALMAP"); }
                AssetDatabase.CreateAsset(copy, path);
            }
            result = copy;
        }
        pieceMats[src] = result;
        return result;
    }

    // a texture in the pack whose file name has "_<material>_<kind>" (Substance export names), or is the name itself
    static Texture2D Tex(string pack, string mat, string kind)
    {
        foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { pack }))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var f = System.IO.Path.GetFileNameWithoutExtension(p);
            if (kind == "" ? f == mat : f.IndexOf("_" + mat + "_" + kind, System.StringComparison.OrdinalIgnoreCase) >= 0) return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        return null;
    }

    // every piece in rows south of the office, named, to look at from above (Tools: see what each one is and which
    // way it faces)
    [MenuItem("HellScape/Office/Perishables/Rebake Pieces + Showroom")]
    public static void Showroom()
    {
        log.Clear();
        pieces.Clear(); pieceMats.Clear();
        if (AssetDatabase.IsValidFolder(PerishDir)) foreach (var g in AssetDatabase.FindAssets("", new[] { PerishDir })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));
        var old = GameObject.Find("Perishables showroom");
        if (old != null) Object.DestroyImmediate(old);
        var room = new GameObject("Perishables showroom").transform;
        int i = 0;
        foreach (var d in Defs)
        {
            var pf = Piece(d.key);
            if (pf == null) continue;
            var t = ((GameObject)PrefabUtility.InstantiatePrefab(pf, room)).transform;
            t.position = new Vector3(DoorX - 30f + (i % 10) * 6f, Floor, DoorZ - 12f - (i / 10) * 6f);
            var label = new GameObject("label").AddComponent<TextMesh>();
            label.transform.SetParent(t, false);
            label.transform.localPosition = new Vector3(0f, .05f, -1.8f);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            label.text = d.key; label.characterSize = .12f; label.fontSize = 40; label.anchor = TextAnchor.MiddleCenter; label.color = Color.yellow;
            var arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);   // which way +z (the front) points
            Object.DestroyImmediate(arrow.GetComponent<Collider>());
            arrow.transform.SetParent(t, false);
            arrow.transform.localPosition = new Vector3(0f, .02f, 1.6f); arrow.transform.localScale = new Vector3(.15f, .02f, .6f);
            i++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Showroom: " + i + " pieces\n" + log);
    }
}
