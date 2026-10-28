using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The prologue's new home (user, 2026-10-17): the Brick kit block that stood across the end of the street
// (Structure_02) is switched off, the street carries on west where it stood (a copy of the home street block, turned
// round the road's end), one row of the street's own buildings is replaced by the user's "old residential building",
// and Flat 2B is the user's "apartment" model, set up away from the map. [F] at the building's door plays the
// Resident Evil door (a copy of MainGameScene's stage) and puts you inside the flat.
// HellScape > Prologue: New Home. Rerunnable: it removes what it made last time first. Prologue.unity is opened
// (and saved) if it isn't loaded; MainGameScene is opened alongside only to copy the door stage.
public static class NewHomeBuilder
{
    const string BuildingFbx = "Assets/Models/old_residential_building/old_residential_building.fbx";
    const string BuildingTex = "Assets/Models/old_residential_building/textures/";
    const string AptFbx = "Assets/Models/apartment/source/6c59c1daa3a143be97b30c2d5e68ac7f.fbx.fbx";
    const string AptTex = "Assets/Models/apartment/textures/";
    const string MatDir = "Assets/Materials/Prologue/NewHome";
    const string HomeStreet = "сити исчерп на скетч";

    public static readonly Vector3 RoadEnd = new Vector3(-2.68f, 0f, 0.6f);      // the street's west end (its edge, measured), on the road's centre line
    public static readonly Vector3 AptAt = new Vector3(-300f, 40f, 300f);         // the flat, away from everything
    static readonly string[] Replaced = { "Plane.014 (1)", "Plane.014", "Plane.016" };   // the north row it stands in (x -6..22)
    const float FrontZ = 10.6f, CentreX = 8f;                                    // its street face, and where along the street

    [MenuItem("HellScape/Prologue: New Home (street, building, flat)")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var pro = SceneManager.GetSceneByName("Prologue");
        bool opened = false;
        if (!pro.isLoaded) { pro = EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity", OpenSceneMode.Additive); opened = true; }
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Materials/Prologue", "NewHome");

        var log = new List<string>();
        var roots = pro.GetRootGameObjects();
        GameObject Root(string n) => roots.FirstOrDefault(g => g.name == n);
        foreach (var old in new[] { "New Home (generated)", "Street west (generated)" }) { var g = pro.GetRootGameObjects().FirstOrDefault(x => x.name == old); if (g != null) Object.DestroyImmediate(g); }
        roots = pro.GetRootGameObjects();

        var home = new GameObject("New Home (generated)");
        SceneManager.MoveGameObjectToScene(home, pro);

        // 1. the old block off
        var block = Root("Structure_02");
        if (block != null) { block.SetActive(false); log.Add("old block off"); }

        // 2. the building, in the row it replaces (x -6..22), its door on the pavement
        if (Root(HomeStreet) != null)
            foreach (var n in Replaced) { var t = Root(HomeStreet).transform.Find(n); if (t != null) t.gameObject.SetActive(false); }
        var bld = Building(home.transform);
        TryBounds(bld, out var bldBounds);
        log.Add("building at " + bld.position);

        // 3. the street goes on: the home street block again, turned half round the road's end
        var street = Root(HomeStreet);
        if (street != null)
        {
            var west = Object.Instantiate(street);
            west.name = "Street west (generated)";
            SceneManager.MoveGameObjectToScene(west, pro);
            west.transform.SetPositionAndRotation(street.transform.position, street.transform.rotation);
            west.transform.RotateAround(RoadEnd, Vector3.up, 180f);
            // a copied building that lands in one of the originals (they overlap a little past the road's end) goes
            int dropped = 0;
            var originals = Blocks(street.transform).ToList();
            foreach (Transform c in west.transform)
            {
                if (!TryBounds(c, out var b) || b.size.y < 3f) continue;
                if (Overlap(b, bldBounds) > .3f || originals.Any(o => Overlap(b, o) > 6f)) { c.gameObject.SetActive(false); dropped++; }
            }
            // its far end leads nowhere (the original goes on into the next block): a wall in the fog
            if (TryBounds(west.transform, out var wb))
            {
                var wall = new GameObject("Street west end (wall)").AddComponent<BoxCollider>();
                wall.transform.SetParent(west.transform, true);
                wall.transform.position = new Vector3(wb.center.x, 5f, wb.max.z - 1f);
                wall.size = new Vector3(wb.size.x, 12f, 2f);
            }
            log.Add($"street continues west ({dropped} overlapping buildings dropped)");
        }

        // 4. the flat, away from the map
        var apt = Apartment(home.transform);
        log.Add("flat at " + apt.position);
        if (Root(HomeStreet) != null) log.Add(DropBoxes(Root(HomeStreet).transform) + " street boxes of the replaced row off");

        // 5. the story moves in: doors, its places, Derek's bedroom, the way in from the street
        Physics.SyncTransforms();
        WireStory(pro, home.transform, bld, apt, log);

        EditorSceneManager.MarkSceneDirty(pro);
        EditorSceneManager.SaveScene(pro);
        if (opened) EditorSceneManager.CloseScene(pro, true);
        return "New home: " + string.Join("; ", log);
    }

    // ---------------------------------------------------------------- the red city (MainGameScene)

    // The same street in the red city, so you wake outside the home you died in: MainGameScene has the same street
    // block (SleepSequence's streetAnchor), so the prologue's generated street and building are mapped across through
    // the two copies of that block. Run the prologue's New Home first. MainGameScene must be open; saved.
    [MenuItem("HellScape/Red City: New Home (street, building)")]
    public static void RedCityMenu() => Debug.Log(RedCity());

    public static string RedCity()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var main = SceneManager.GetSceneByName("MainGameScene");
        if (!main.isLoaded) return "Open MainGameScene first.";
        var pro = SceneManager.GetSceneByName("Prologue");
        bool opened = false;
        if (!pro.isLoaded) { pro = EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity", OpenSceneMode.Additive); opened = true; }
        try
        {
            var proRoots = pro.GetRootGameObjects();
            var pStreet = proRoots.FirstOrDefault(g => g.name == HomeStreet);
            var pWest = proRoots.FirstOrDefault(g => g.name == "Street west (generated)");
            var pHome = proRoots.FirstOrDefault(g => g.name == "New Home (generated)");
            var pBld = pHome != null ? pHome.transform.Find("Old residential building (home)") : null;
            if (pStreet == null || pWest == null || pBld == null) return "Run HellScape > Prologue: New Home first.";
            var all = main.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            var mStreet = all.FirstOrDefault(t => t.name == HomeStreet);
            if (mStreet == null) return "MainGameScene has no " + HomeStreet;
            Matrix4x4 M = mStreet.localToWorldMatrix * pStreet.transform.worldToLocalMatrix;
            Quaternion R = M.rotation;

            foreach (var t in all.Where(t => t != null && (t.name == "New Home (generated)" || t.name == "Street west (generated)")).ToList())
                if (t != null) Object.DestroyImmediate(t.gameObject);
            var log = new List<string>();
            var block = all.FirstOrDefault(t => t != null && t.name == "Structure_02");
            if (block != null) { block.gameObject.SetActive(false); log.Add("old block off"); }
            foreach (var n in Replaced) { var t = mStreet.Find(n); if (t != null) t.gameObject.SetActive(false); }

            // the street goes on: the red city's own copy of the block, posed as the prologue's continuation is
            var west = Object.Instantiate(mStreet.gameObject, mStreet.parent);
            west.name = "Street west (generated)";
            west.transform.SetPositionAndRotation(M.MultiplyPoint3x4(pWest.transform.position), R * pWest.transform.rotation);
            int off = 0;
            foreach (Transform c in west.transform)
            {
                var twin = pWest.transform.Find(c.name);
                if (twin != null && !twin.gameObject.activeSelf && c.gameObject.activeSelf) { c.gameObject.SetActive(false); off++; }
            }
            var pWall = pWest.transform.Find("Street west end (wall)");
            if (pWall != null)
            {
                var wall = Object.Instantiate(pWall.gameObject, west.transform);
                wall.name = pWall.name;
                wall.transform.localPosition = pWall.localPosition;
                wall.transform.localRotation = pWall.localRotation;
            }
            log.Add($"street continues ({off} buildings dropped as in the prologue)");

            // the building, where the prologue has it
            var home = new GameObject("New Home (generated)");
            home.transform.SetParent(mStreet.parent, false);
            var g = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BuildingFbx), main);
            g.name = pBld.name;
            g.transform.SetParent(home.transform, false);
            g.transform.SetPositionAndRotation(M.MultiplyPoint3x4(pBld.position), R * pBld.rotation);
            Dress(g);
            BurntHomeBuilder.Apply(g.transform);
            log.Add("building at " + g.transform.position);
            log.Add(DropBoxes(mStreet) + DropBoxes(west.transform) + " street boxes of switched-off buildings off");

            EditorSceneManager.MarkSceneDirty(main);
            EditorSceneManager.SaveScene(main);
            return "Red city: " + string.Join("; ", log);
        }
        finally { if (opened) EditorSceneManager.CloseScene(pro, true); }
    }

    // Act 2's red flat: Flat 2B again in MainGameScene (same place, away from the map), with the prologue flat's doors,
    // fridge note, clock and lamps copied over, the building's door going in, the flat's front door coming out, and
    // RedFlat (the scene's runtime) wired. Run the prologue's New Home and the Red City one first. MainGameScene saved.
    [MenuItem("HellScape/Red City: Red Flat (Act 2)")]
    public static void RedFlatMenu() => Debug.Log(BuildRedFlat());

    public static string BuildRedFlat()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var main = SceneManager.GetSceneByName("MainGameScene");
        if (!main.isLoaded) return "Open MainGameScene first.";
        var pro = SceneManager.GetSceneByName("Prologue");
        bool opened = false;
        if (!pro.isLoaded) { pro = EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity", OpenSceneMode.Additive); opened = true; }
        try
        {
            var proRoots = pro.GetRootGameObjects();
            var pStreet = proRoots.FirstOrDefault(g => g.name == HomeStreet);
            var pHome = proRoots.FirstOrDefault(g => g.name == "New Home (generated)");
            var pMarks = pHome != null ? pHome.transform.Find("Flat 2B (story places)") : null;
            var pEntry = pHome != null ? pHome.transform.Find("Old residential building (home)/Front door (go home)") : null;
            if (pStreet == null || pMarks == null || pEntry == null) return "Run HellScape > Prologue: New Home first.";
            var all = main.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            var mStreet = all.FirstOrDefault(t => t.name == HomeStreet);
            var mBld = all.FirstOrDefault(t => t.name == "Old residential building (home)");
            if (mStreet == null || mBld == null) return "Run HellScape > Red City: New Home first.";
            Matrix4x4 M = mStreet.localToWorldMatrix * pStreet.transform.worldToLocalMatrix;
            foreach (var g in main.GetRootGameObjects().Where(g => g.name == "Red Flat (generated)")) Object.DestroyImmediate(g);
            var old = mBld.Find("Front door (go home)");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            all = all.Where(t => t != null).ToList();
            var log = new List<string>();

            var root = new GameObject("Red Flat (generated)");
            SceneManager.MoveGameObjectToScene(root, main);
            var apt = Apartment(root.transform);
            var marks = Object.Instantiate(pMarks.gameObject, pMarks.position, pMarks.rotation);
            SceneManager.MoveGameObjectToScene(marks, main);
            marks.name = pMarks.name;
            marks.transform.SetParent(root.transform, true);
            Transform Find(string n) => marks.transform.Find(n);
            var note = Find("Fridge note");
            if (note != null) note.name = "Fridge note (blank)";   // (not ReadableSetup's: RedFlat gives it its line)

            var fpc = all.Select(t => t.GetComponent<FirstPersonController>()).FirstOrDefault(c => c != null);
            var fader = all.Select(t => t.GetComponent<SleepSequence>()).Where(s => s != null).Select(s => s.fader).FirstOrDefault(f => f != null);
            var stage = all.Select(t => t.GetComponent<DoorTransition>()).FirstOrDefault(d => d != null);

            // the way in: the building's door, where the prologue has it
            var entry = new GameObject("Front door (go home)");
            entry.transform.SetParent(mBld, false);
            entry.transform.SetPositionAndRotation(M.MultiplyPoint3x4(pEntry.position), M.rotation * pEntry.rotation);
            var pBox = pEntry.GetComponent<BoxCollider>();
            var box = entry.AddComponent<BoxCollider>();
            box.size = pBox.size; box.center = pBox.center;
            var use = entry.AddComponent<Interactable>();
            use.prompt = "Go home - Flat 2B"; use.once = false;
            var way = entry.AddComponent<Teleporter>();
            way.player = fpc != null ? fpc.transform : null; way.destination = Find("Arrive (through the front door)"); way.fader = fader; way.transition = stage;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(use.onUse, way.Go);

            // the way out: back on the pavement, facing the street
            var outside = new GameObject("Outside Flat 2B (street)").transform;
            outside.SetParent(root.transform, false);
            Physics.SyncTransforms();
            Vector3 step = entry.transform.position + entry.transform.forward * 2f;
            // (the prologue is open too, its street at the same place: only this scene's ground counts)
            var ground = Physics.RaycastAll(step + Vector3.up * 3f, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.gameObject.scene == main).OrderBy(h => h.distance).FirstOrDefault();
            if (ground.collider != null) step = ground.point;
            outside.SetPositionAndRotation(step + Vector3.up, Quaternion.LookRotation(entry.transform.forward));
            var exit = root.AddComponent<Teleporter>();
            exit.player = way.player; exit.destination = outside; exit.fader = fader; exit.transition = stage;

            var flat = root.AddComponent<RedFlat>();
            flat.table = Find("Mugs (kitchen island)");
            flat.sofaSeat = Find("Sofa seat");
            flat.photoSpot = Find("Photo (coffee table)");
            flat.fridgeNote = note != null ? note.GetComponent<Interactable>() : null;
            flat.frontDoor = Find("Front door") != null ? Find("Front door").GetComponentInChildren<PrologueDoor>(true) : null;
            flat.bathroomDoor = Find("Bathroom door") != null ? Find("Bathroom door").GetComponentInChildren<PrologueDoor>(true) : null;
            flat.way = way; flat.exit = exit; flat.fader = fader;
            flat.lamps = marks.GetComponentsInChildren<Light>(true);
            var derek = all.FirstOrDefault(t => t.name == "Derek (Character_20_Police)");
            flat.derek = derek != null ? derek.gameObject : null;
            BurntHomeBuilder.Apply(root.transform);
            log.Add($"flat at {apt.position}, door at {entry.transform.position}, out at {outside.position}, {flat.lamps.Length} lamps, derek {(derek != null)}, transition {(stage != null)}, fader {(fader != null)}");

            EditorSceneManager.MarkSceneDirty(main);
            EditorSceneManager.SaveScene(main);
            return "Red flat: " + string.Join("; ", log);
        }
        finally { if (opened) EditorSceneManager.CloseScene(pro, true); }
    }

    // ---------------------------------------------------------------- the building

    static Transform Building(Transform parent)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BuildingFbx), parent.gameObject.scene);
        g.name = "Old residential building (home)";
        g.transform.SetParent(parent, false);
        // modelled with its entrance on +z: turned round to face the street (-z)
        g.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        TryBounds(g.transform, out var b0);
        g.transform.position += new Vector3(CentreX - b0.center.x, -b0.min.y - 0.1f, FrontZ - b0.min.z);
        Dress(g);
        return g.transform;
    }

    // its own textures, no lights, solid walls and steps
    static void Dress(GameObject g)
    {
        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = r.sharedMaterials.Select(m => m == null ? null : BuildingMat(m.name)).ToArray();
        foreach (var l in g.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
        foreach (var n in new[] { "zgrada_base_uv_main_building", "b1_staircase", "b1_bunker_entrance" })
        {
            var t = g.transform.Find(n);
            if (t != null && t.GetComponent<MeshFilter>() != null) t.gameObject.AddComponent<MeshCollider>();
        }
    }

    static Material BuildingMat(string name)
    {
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BuildingTex + name + "_albedo.jpg"));
        m.SetColor("_BaseColor", Color.white);
        var n = Normal(BuildingTex + name + "_normal.png");
        if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
        // no AO map: normal map + occlusion map together compile to a missing variant (solid magenta)
        m.SetTexture("_OcclusionMap", null); m.DisableKeyword("_OCCLUSIONMAP");
        m.SetFloat("_Smoothness", .2f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Texture2D Normal(string path)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------------------------------------------------------------- the flat

    static Transform Apartment(Transform parent)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AptFbx), parent.gameObject.scene);
        g.name = "Flat 2B (apartment)";
        g.AddComponent<ApartmentEyeHeight>();
        g.transform.SetParent(parent, false);
        g.transform.position = AptAt;
        var textures = AssetDatabase.FindAssets("t:Texture2D", new[] { AptTex.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath).ToList();
        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
        {
            string tex = AptTexture(r.name, textures);
            if (r.name != "SM_BG" && tex != null) r.sharedMaterials = r.sharedMaterials.Select(_ => AptMat(r.name, tex)).ToArray();
            if (r.name == "SM_Doors") r.enabled = false;   // one mesh for every door: working doors go in their place
            bool solid = r.name.StartsWith("SM_") || r.name.StartsWith("polySurface") || r.name.StartsWith("ceiling");
            if (solid && r.name != "SM_BG" && r.name != "SM_Doors" && r.name != "SM_CeilingLights" && r.GetComponent<MeshFilter>() != null) r.gameObject.AddComponent<MeshCollider>();
        }
        foreach (var l in g.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
        foreach (var c in g.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
        return g.transform;
    }

    // Every mesh has its own baked texture, "baked-<mesh>Shape.tga.png", with a trailing number moved after "Shape"
    // (SM_WallLiving_Q2_3 -> baked-SM_WallLiving_Q2_Shape3) and long names cut short.
    static string AptTexture(string mesh, List<string> textures)
    {
        string File(string p) => System.IO.Path.GetFileName(p);
        if (mesh.StartsWith("ceiling")) mesh = "SM_Ceiling";
        string digits = new string(mesh.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        string stem = mesh.Substring(0, mesh.Length - digits.Length);
        var wants = new List<string> { "baked-" + mesh + "Shape.", "baked-" + stem + "Shape" + digits + "." };
        if (stem.Length > 12) wants.Add("baked-" + stem + "Shape");   // (not plain "polySurface": any of them would match)
        foreach (var want in wants)
        {
            var hit = textures.FirstOrDefault(p => File(p).StartsWith(want));
            if (hit != null) return hit;
        }
        string full = "baked-" + mesh + "Shape";
        return textures.Where(p => File(p).Length > 30 && full.StartsWith(System.IO.Path.GetFileNameWithoutExtension(p).Replace(".tga", ""))).OrderByDescending(p => p.Length).FirstOrDefault();
    }

    static Material AptMat(string mesh, string tex)
    {
        string path = MatDir + "/Flat_" + mesh.Replace(":", "_") + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", .15f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------------------------------------------------------------- the story, moved into the flat

    // Places in the flat's own space (metres from its origin; the floor is at 0). Measured from a collider scan
    // at waist height: vestibule x 1.8..4.2 z 5.2..7.0 (front door on its end wall), kitchen-living x -2.2..7.4
    // z -0.2..5.2, bathroom door in the bath's east wall, corridor x 2..3 z -0.6..-6.6, bedroom door off its end.
    static readonly Vector3 Arrive = new Vector3(3.0f, 0f, 6.2f);
    static readonly Vector3 FrontDoorAt = new Vector3(2.95f, 0f, 6.93f);     // in front of the end wall (it never opens past the chain)
    static readonly Vector3 BathDoorAt = new Vector3(0.3f, 0f, -4.2f);
    static readonly Vector3 BedDoorAt = new Vector3(3.3f, 0f, -6.0f);
    static readonly Vector3 SofaAt = new Vector3(5.6f, 0f, 3.45f);
    static readonly Vector3 TvAt = new Vector3(5.6f, 1.45f, -0.72f);
    static readonly Vector3 MugsAt = new Vector3(0.2f, 0f, 3.6f);
    static readonly Vector3 PhotoAt = new Vector3(5.6f, 0f, 1.35f);
    static readonly Vector3 FridgeNoteAt = new Vector3(-1.38f, 1.45f, 0.5f);
    static readonly Vector3 RigAt = new Vector3(6.0f, 0f, -5.3f);           // Mercer and the body, across the room from the bedroom door
    const float RigYaw = 270f;
    const float DoorHeight = 2.05f;
    const string KitDoor = "Structure_02/Interior/2nd Floor/Apartment_01/Doors Frames/DoorFrame_Apt_01 (3)";

    static void WireStory(Scene pro, Transform home, Transform bld, Transform apt, List<string> log)
    {
        var dir = pro.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PrologueDirector>(true)).FirstOrDefault();
        if (dir == null) { log.Add("no PrologueDirector"); return; }
        Undo.RecordObject(dir, "New home");
        var marks = new GameObject("Flat 2B (story places)").transform;
        marks.SetParent(home, false);
        Vector3 W(Vector3 local) => apt.TransformPoint(local);
        Transform Mark(string n, Vector3 local, float yaw)
        {
            var t = new GameObject(n).transform;
            t.SetParent(marks, false);
            t.SetPositionAndRotation(W(local), Quaternion.Euler(0f, yaw, 0f));
            return t;
        }
        // a table top under a spot: the first thing a ray down from head height meets
        Vector3 OnTop(Vector3 local)
        {
            Vector3 from = W(local + Vector3.up * 1.4f);
            var hit = Physics.RaycastAll(from, Vector3.down, 2f).Where(h => h.collider.transform.IsChildOf(apt)).OrderBy(h => h.distance).FirstOrDefault();
            return hit.collider != null ? hit.point : W(local + Vector3.up * .75f);
        }

        // where you come in, and what counts as "home" for the story
        var arrive = Mark("Arrive (through the front door)", Arrive + Vector3.up * 1.0f, 180f);
        dir.flat = new Bounds(W(new Vector3(3.0f, 1.2f, 6.0f)), new Vector3(2.4f, 3f, 2.2f));
        dir.hallway = dir.flat;
        dir.sofaSeat = Mark("Sofa seat", SofaAt, 180f);
        var tv = Mark("TV screen", TvAt, 0f);
        tv.localScale = new Vector3(1.45f, .82f, 1f);
        dir.tvScreen = tv;
        dir.mugSpot = Mark("Mugs (kitchen island)", MugsAt, 0f);
        dir.mugSpot.position = OnTop(MugsAt);
        dir.photoSpot = Mark("Photo (coffee table)", PhotoAt, 0f);
        dir.photoSpot.position = OnTop(PhotoAt);
        log.Add("mugs on " + (dir.mugSpot.position.y - apt.position.y).ToString("0.00") + " m, photo on " + (dir.photoSpot.position.y - apt.position.y).ToString("0.00") + " m");

        // the doors: copies of the old kit's working door, fitted to the flat's openings
        Transform kit = null;
        var block = pro.GetRootGameObjects().FirstOrDefault(g => g.name == "Structure_02");
        if (block != null) kit = block.transform.Find(KitDoor.Substring("Structure_02/".Length));
        if (kit != null)
        {
            dir.frontDoor = FitDoor(kit, marks, "Front door", W(FrontDoorAt), apt, -90f, 1.0f);
            dir.bathroomDoor = FitDoor(kit, marks, "Bathroom door", W(BathDoorAt), apt, 0f, .72f);
            dir.bedroomDoor = FitDoor(kit, marks, "Bedroom door", W(BedDoorAt), apt, 180f, .98f);
            log.Add("3 doors fitted");
        }
        else log.Add("no kit door to copy: doors missing");

        // the fridge note (ReadableSetup makes it readable by its name)
        var note = GameObject.CreatePrimitive(PrimitiveType.Cube);
        note.name = "Fridge note";
        note.transform.SetParent(marks, false);
        note.transform.SetPositionAndRotation(W(FridgeNoteAt), Quaternion.Euler(0f, 90f, 4f));
        note.transform.localScale = new Vector3(.18f, .22f, .01f);
        note.GetComponent<Renderer>().sharedMaterial = Paper();
        note.AddComponent<Interactable>().prompt = "Read the note";

        // the clock by the front door: 07:19 when he kills you (the voicemail on your phone says 07:24 - V4's replay clue)
        Vector3 wallAt = W(new Vector3(4.2f, 2.0f, 6.3f));
        var wallHit = Physics.RaycastAll(W(new Vector3(3.0f, 2.0f, 6.3f)), Vector3.right, 3f).Where(h => h.collider.transform.IsChildOf(apt)).OrderBy(h => h.distance).FirstOrDefault();
        if (wallHit.collider != null) wallAt = wallHit.point;
        var clock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        clock.name = "Wall clock (07:19)";
        Object.DestroyImmediate(clock.GetComponent<Collider>());
        clock.transform.SetParent(marks, false);
        clock.transform.SetPositionAndRotation(wallAt + Vector3.left * .025f, Quaternion.LookRotation(Vector3.left));
        clock.transform.localScale = new Vector3(.34f, .13f, .04f);
        clock.GetComponent<Renderer>().sharedMaterial = Dark();
        var digits = new GameObject("Time").AddComponent<TMPro.TextMeshPro>();
        digits.transform.SetParent(marks, false);
        digits.transform.SetPositionAndRotation(wallAt + Vector3.left * .05f, Quaternion.LookRotation(Vector3.right));
        digits.rectTransform.sizeDelta = new Vector2(.32f, .12f);
        digits.text = "07:19";
        digits.fontSize = 1f;
        digits.alignment = TMPro.TextAlignmentOptions.Center;
        digits.color = new Color(1f, .15f, .1f);

        // the balcony glass has no collider: a wall so nobody walks off it, 40 m up
        var rail = new GameObject("Balcony (wall)").AddComponent<BoxCollider>();
        rail.transform.SetParent(marks, false);
        rail.transform.position = W(new Vector3(7.55f, 1.5f, 2.4f));
        rail.size = new Vector3(.1f, 3f, 5.8f);

        // lamps (the model's are only meshes)
        foreach (var at in new[] { new Vector3(5f, 2.3f, 2.5f), new Vector3(0.2f, 2.3f, 2.5f), new Vector3(3f, 2.3f, 6f), new Vector3(2.5f, 2.3f, -3.5f), new Vector3(5.4f, 2.3f, -4.5f), new Vector3(-1f, 2.3f, -2.5f) })
        {
            var l = new GameObject("Lamp").AddComponent<Light>();
            l.transform.SetParent(marks, false);
            l.transform.position = W(at);
            l.type = LightType.Point; l.range = 6f; l.intensity = 1.6f; l.color = new Color(1f, .9f, .78f); l.shadows = LightShadows.None;
        }

        // Derek's bedroom: the stab rig, the body, the blood and its lamp keep their places round the rig
        var pRoot = pro.GetRootGameObjects().FirstOrDefault(g => g.name == "Prologue");
        if (dir.derekRig != null && pRoot != null)
        {
            Transform rig = dir.derekRig.transform.parent != null && dir.derekRig.transform.parent.parent == pRoot.transform ? dir.derekRig.transform.parent : dir.derekRig.transform;
            Vector3 old = rig.position;
            Quaternion turn = Quaternion.Euler(0f, RigYaw - rig.eulerAngles.y, 0f);
            Vector3 now = W(RigAt);
            void Bring(Transform t)
            {
                if (t == null) return;
                Undo.RecordObject(t, "New home");
                t.SetPositionAndRotation(now + turn * (t.position - old), turn * t.rotation);
            }
            Vector3 bodyAt = dir.body != null ? dir.body.position : old;
            var lamp = pRoot.transform.Cast<Transform>().FirstOrDefault(t => t.name == "Light" && (t.position - bodyAt).sqrMagnitude < 9f);
            foreach (var t in new[] { dir.body, dir.bloodPool != null ? dir.bloodPool.transform : null, lamp, dir.killer != null ? dir.killer.transform : null }) Bring(t);
            Bring(rig);
            log.Add("Derek's bedroom moved in");
        }
        if (pRoot != null)
        {
            // the TV's sound to the new TV; what belonged to the old flat (the camera tape, its PC, the stairwell) off
            var tvSound = pRoot.transform.Find("Clues/TV");
            if (tvSound != null) tvSound.position = tv.position;
            foreach (Transform t in pRoot.transform)
                if (t.name == "PC (camera playback)" || t.name == "Label" || t.name == "CCTV Camera" || t.name == "Stairwell" || (t.name == "Light" && Mathf.Abs(t.position.x - 2.19f) < .5f && Mathf.Abs(t.position.z + 7.4f) < .5f))
                    t.gameObject.SetActive(false);
            var clues = pRoot.transform.Find("Clues");
            if (clues != null)
                foreach (Transform c in clues)
                    if (c.name != "TV" && c.name != "Derek's phone") { c.gameObject.SetActive(false); log.Add("off: Clues/" + c.name); }
        }

        // the way in: [F] at the building's door, the Resident Evil door, then you're inside
        var handles = bld.Find("b1_door_handles");
        Vector3 door = handles != null ? handles.GetComponent<Renderer>().bounds.center : bld.position;
        var entry = new GameObject("Front door (go home)");
        entry.transform.SetParent(bld, true);
        entry.transform.SetPositionAndRotation(door - bld.forward * .05f, bld.rotation);
        var box = entry.AddComponent<BoxCollider>();
        box.size = new Vector3(1.4f, 2.3f, .25f);
        box.center = new Vector3(0f, .05f, 0f);
        var use = entry.AddComponent<Interactable>();
        use.prompt = "Go home - Flat 2B";
        use.once = false;
        var tp = entry.AddComponent<Teleporter>();
        tp.player = dir.fpc != null ? dir.fpc.transform : null;
        tp.destination = arrive;
        tp.fader = dir.fader;
        tp.transition = DoorStage(pro, dir, log);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(use.onUse, tp.Go);
        dir.homeLookAt = door + Vector3.up * 5f;
        dir.homeCamAt = new Vector3(34f, 6f, 1f);
        log.Add("door at " + door);
        EditorUtility.SetDirty(dir);
    }

    // a copy of the kit's door (frame and leaf), turned (yaw, relative to the kit's) and scaled to the opening's
    // width, its leaf centred on the opening, on the floor
    static PrologueDoor FitDoor(Transform kit, Transform parent, string name, Vector3 at, Transform apt, float yaw, float width)
    {
        var g = Object.Instantiate(kit.gameObject, parent);
        g.name = name;
        g.SetActive(true);
        var leaf = g.GetComponentInChildren<PrologueDoor>(true);
        var col = leaf.GetComponentInChildren<Collider>();
        g.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * kit.rotation;
        Physics.SyncTransforms();
        Vector3 size = col.bounds.size;
        float s = width / Mathf.Max(.1f, Mathf.Max(size.x, size.z));
        g.transform.localScale = kit.lossyScale * s;
        Physics.SyncTransforms();
        // then stretched back up to a door's height (the kit's doors are wide: scaled to a narrow opening they'd be short)
        var sc = g.transform.localScale;
        sc.y *= DoorHeight / Mathf.Max(.1f, col.bounds.size.y);
        g.transform.localScale = sc;
        Physics.SyncTransforms();
        var b = col.bounds;
        float floor = apt.position.y;
        g.transform.position += new Vector3(at.x - b.center.x, floor + .01f - b.min.y, at.z - b.center.z);
        foreach (var i in g.GetComponentsInChildren<Interactable>(true)) i.enabled = true;
        return leaf;
    }

    // MainGameScene's Resident Evil door, copied over (its stage stands far below the map)
    static DoorTransition DoorStage(Scene pro, PrologueDirector dir, List<string> log)
    {
        var have = pro.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DoorTransition>(true)).FirstOrDefault();
        if (have != null) return Hook(have, dir);
        var main = SceneManager.GetSceneByName("MainGameScene");
        bool opened = false;
        if (!main.isLoaded) { main = EditorSceneManager.OpenScene("Assets/Scenes/MainGameScene.unity", OpenSceneMode.Additive); opened = true; }
        var src = main.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DoorTransition>(true)).FirstOrDefault();
        DoorTransition copy = null;
        if (src != null)
        {
            var g = Object.Instantiate(src.gameObject);
            g.name = "DoorTransition (stage)";
            SceneManager.MoveGameObjectToScene(g, pro);
            g.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
            copy = g.GetComponent<DoorTransition>();
            log.Add("door transition copied");
        }
        else log.Add("no door transition in MainGameScene: plain fade");
        if (opened) EditorSceneManager.CloseScene(main, true);
        return copy != null ? Hook(copy, dir) : null;
    }

    static DoorTransition Hook(DoorTransition dt, PrologueDirector dir)
    {
        dt.fader = dir.fader;
        dt.lockDuring = dir.lockDuring.Where(b => b != null).ToArray();
        if (dt.cam != null && dir.playerCam != null) dt.cam.depth = dir.playerCam.depth + 10;
        EditorUtility.SetDirty(dt);
        return dt;
    }

    static Material Dark()
    {
        string path = MatDir + "/ClockBody.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", new Color(.05f, .05f, .06f));
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material Paper()
    {
        string path = MatDir + "/FridgeNote.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", new Color(.93f, .9f, .8f));
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ---------------------------------------------------------------- helpers

    // a building switched off keeps its street collider (MapColliders' "<name> box"): an invisible wall, so it goes too
    public static int DropBoxes(Transform block)
    {
        var boxes = block.Find(MapColliders.Container);
        if (boxes == null) return 0;
        int n = 0;
        foreach (Transform c in block)
            if (!c.gameObject.activeSelf)
                foreach (Transform box in boxes)
                    if (box.gameObject.activeSelf && box.name.StartsWith(c.name + " box")) { box.gameObject.SetActive(false); n++; }
        return n;
    }

    static IEnumerable<Bounds> Blocks(Transform street)
    {
        foreach (Transform c in street)
            if (c.gameObject.activeInHierarchy && TryBounds(c, out var b) && b.size.y >= 3f) yield return b;
    }

    // how far two boxes run into each other on the ground (the smaller of the x and z overlaps, metres)
    static float Overlap(Bounds a, Bounds b) => Mathf.Min(Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x), Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z));

    public static bool TryBounds(Transform t, out Bounds b)
    {
        var rs = t.GetComponentsInChildren<Renderer>(true);
        b = default;
        if (rs.Length == 0) return false;
        b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return true;
    }
}
