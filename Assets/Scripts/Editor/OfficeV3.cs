using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// The user's smaller office (Assets/New Office/Hellscape_Office_v3_Unity.fbx, ~60 x 44 m, furnished) in place of the
// Kessler & Vane model in MainGameScene. Put in as "Office Ground Floor" (the name code looks for), turned half round
// so its front door is the old one at world (982, 3.4, 870) with the rooms to the north: the plan coordinates of
// NewOfficeFit still work. The Kessler & Vane model is switched off and renamed, not deleted.
//
// Solid: walls, floors and anything 2 m or taller get mesh colliders, furniture a box, nothing low enough to trip
// over (the player is a Rigidbody and cannot step). It has no ceiling of its own: one is added. Its loading bay and
// side doors open onto nothing: invisible walls close them (the silo door stands in the middle bay).
public static partial class NewOfficeFit
{
    const string V3Fbx = "Assets/New Office/Hellscape_Office_v3_Unity.fbx";
    const string V3Dir = GenDir + "/V3";
    static readonly Vector3 V3Door = new Vector3(-29.7f, 0f, 7f);   // the front door in the model's own space

    public static bool IsV3(Transform m) => m != null && m.Find("Continuous structural floor") != null;
    static bool IsOffice(Transform m) => IsKv(m) || IsV3(m);

    // a child of the model by the start of its name: its bounds (the pivots in the file are not where things stand)
    static Bounds Of(string name)
    {
        var rs = model.Cast<Transform>().Where(c => c.name.StartsWith(name)).SelectMany(c => c.GetComponentsInChildren<Renderer>()).ToArray();
        if (rs.Length == 0) { log.AppendLine("no '" + name + "' in the model"); return new Bounds(P(0f, Floor, 20f), Vector3.zero); }
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
    static Vector2 At(string name) { var c = Of(name).center; return new Vector2(c.x - DoorX, c.z - DoorZ); }

    static Transform InstallV3()
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(V3Fbx);
        if (!imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); }   // (mesh colliders on scaled parts need it in a build)

        var old = GameObject.Find(ModelName);
        int sibling = old != null ? old.transform.GetSiblingIndex() : -1;
        if (old != null && IsV3(old.transform)) Undo.DestroyObjectImmediate(old);   // run again: a fresh copy
        else if (old != null)
        {
            Undo.RecordObject(old, "Old office");
            old.name = ModelName + " (Kessler & Vane, old)";
            old.SetActive(false);
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(V3Fbx));
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Undo.RegisterCreatedObjectUndo(go, "Office model");
        go.name = ModelName;
        if (sibling >= 0) go.transform.SetSiblingIndex(sibling);
        go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        go.transform.position = new Vector3(DoorX, Floor, DoorZ) - go.transform.rotation * V3Door;
        go.isStatic = false;

        int mesh = 0, box = 0;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.GetComponent<Collider>() != null) continue;
            var b = mf.GetComponent<Renderer>().bounds;
            if (b.size.y >= 2f || b.max.y - Floor < .05f) { mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh; mesh++; }   // walls, tall things, the floors
            else if (b.max.y - Floor > .3f) { mf.gameObject.AddComponent<BoxCollider>(); box++; }
        }
        Physics.SyncTransforms();
        model = go.transform;

        // the ceiling, on top of the walls
        var all = go.GetComponentsInChildren<Renderer>();
        float top = all.Where(r => r.bounds.size.y >= 2f).Max(r => r.bounds.max.y);
        var mb = ModelBounds();
        var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Ceiling (generated)";
        ceiling.transform.SetParent(go.transform, true);
        ceiling.transform.SetPositionAndRotation(new Vector3(mb.center.x, top + .05f, mb.center.z), Quaternion.identity);
        ceiling.transform.localScale = Vector3.one;
        ceiling.transform.localScale = new Vector3(mb.size.x + 1f, .1f, mb.size.z + 1f);
        ceiling.GetComponent<Renderer>().sharedMaterial = CeilingMat(mb.size);
        Retexture(go.transform);

        // the outside walls have bays and doors onto nothing: an invisible wall a hand's width outside each one (the
        // silo door and the exit stand in their doorways, inside it). The slanted corner pieces have no openings.
        var seals = new GameObject("Seals (invisible walls)").transform;
        seals.SetParent(go.transform, false);
        seals.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (Transform c in go.transform)
        {
            if (!c.name.StartsWith("Exterior ") || c.GetComponent<Renderer>() == null) continue;
            var b = c.GetComponent<Renderer>().bounds;
            bool alongX = b.size.z < .5f, alongZ = b.size.x < .5f;
            if (alongX == alongZ) continue;
            var outward = alongX ? Vector3.forward * Mathf.Sign(b.center.z - mb.center.z) : Vector3.right * Mathf.Sign(b.center.x - mb.center.x);
            var t = new GameObject(c.name).transform;
            t.SetParent(seals, false);
            t.position = new Vector3(b.center.x, Floor + 1.6f, b.center.z) + outward * (Mathf.Min(b.size.x, b.size.z) * .5f + .3f);
            t.gameObject.AddComponent<BoxCollider>().size = alongX ? new Vector3(b.size.x + .6f, 3.2f, .3f) : new Vector3(.3f, 3.2f, b.size.z + .6f);
        }
        log.AppendLine("v3 office: " + mesh + " mesh colliders, " + box + " boxes, ceiling at " + (top - Floor).ToString("F2") + " m");
        return go.transform;
    }

    // ---------------------------------------------------------------- the old office's surfaces

    // The user's own burnt carpet floor and burnt plaster-over-panelling wall from the Kessler & Vane office (user,
    // 2026-10-21), on copies so that office and the Prologue keep theirs. The model's UVs are 0..1 on every face
    // whatever its size (and the main slab has none), so each wall and floor gets a mesh of its own with UVs in metres:
    // a wall is one texture high (the panelling at its foot), a floor repeats every 2.5 m.
    const string OldFloor = "Assets/Models/lowpoly-mobile-cartoon-office-rooms/textures/glsl_floor1 1.mat";
    const string OldWall = "Assets/Models/lowpoly-mobile-cartoon-office-rooms/textures/glsl_wall1 1.mat";
    static readonly string[] FloorMats = { "Service floor", "Office floor - charcoal", "Concrete floor" };
    const string WallMat = "Warm grey plaster";

    [MenuItem("HellScape/Office/Old Office Floor And Wall Textures")]
    static void RetextureMenu()
    {
        var m = GameObject.Find(ModelName);
        if (EditorApplication.isPlaying || m == null || !IsV3(m.transform)) { Debug.LogError("Needs the v3 '" + ModelName + "' in the open scene, out of Play."); return; }
        Undo.RegisterFullObjectHierarchyUndo(m, "Old office textures");
        model = m.transform;
        Retexture(model);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(m.scene);
    }

    static void Retexture(Transform m)
    {
        if (!AssetDatabase.IsValidFolder(V3Dir)) AssetDatabase.CreateFolder(GenDir, "V3");
        const string meshDir = V3Dir + "/Meshes";
        AssetDatabase.DeleteAsset(meshDir);
        AssetDatabase.CreateFolder(V3Dir, "Meshes");
        Material Copy(string src, string name)
        {
            string path = V3Dir + "/" + name + ".mat";
            AssetDatabase.DeleteAsset(path);
            var mat = new Material(AssetDatabase.LoadAssetAtPath<Material>(src)) { name = name, mainTextureScale = Vector2.one, mainTextureOffset = Vector2.zero };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
        var floor = Copy(OldFloor, "Floor (old office, burnt)");
        var wall = Copy(OldWall, "Wall (old office, burnt)");
        float height = model.Find("Ceiling (generated)") is Transform c ? c.GetComponent<Renderer>().bounds.min.y - Floor : 3.4f;
        int n = 0;
        foreach (var mf in m.GetComponentsInChildren<MeshFilter>(true))
        {
            var r = mf.GetComponent<Renderer>();
            if (r == null || mf.sharedMesh == null) continue;
            var mats = r.sharedMaterials;
            bool any = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                if (mats[i].name == WallMat) { mats[i] = wall; any = true; }
                else if (System.Array.IndexOf(FloorMats, mats[i].name) >= 0) { mats[i] = floor; any = true; }
            }
            if (!any) continue;
            var mesh = Object.Instantiate(mf.sharedMesh);
            mesh.name = mf.sharedMesh.name + " (metre UVs)";
            if (mesh.normals.Length == 0) mesh.RecalculateNormals();
            var v = mesh.vertices; var nor = mesh.normals; var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = mf.transform.TransformPoint(v[i]), d = mf.transform.TransformDirection(nor[i]);
                if (Mathf.Abs(d.y) > .7f) uv[i] = new Vector2(p.x, p.z) / 2.5f;
                else uv[i] = new Vector2((Mathf.Abs(d.x) > Mathf.Abs(d.z) ? p.z : p.x) / height, (p.y - Floor) / height);
            }
            mesh.uv = uv;
            AssetDatabase.CreateAsset(mesh, meshDir + "/" + (n++) + " " + string.Concat(mf.name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')) + ".asset");
            mf.sharedMesh = mesh;   // (its collider keeps the file's mesh: same shape)
            r.sharedMaterials = mats;
        }
        log.AppendLine("old office floor + wall on " + n + " meshes");
    }

    static Material CeilingMat(Vector3 size)
    {
        if (!AssetDatabase.IsValidFolder(V3Dir)) AssetDatabase.CreateFolder(GenDir, "V3");
        const string path = V3Dir + "/Ceiling.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        var src = GameObject.Find("OfficeAct") != null
            ? Resources.FindObjectsOfTypeAll<Renderer>().FirstOrDefault(r => r.name == "Ceiling_open-office" && r.gameObject.scene.IsValid())?.sharedMaterial
            : null;
        m = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.name = "V3 Ceiling";
        m.mainTextureScale = new Vector2(size.x / 2f, size.z / 2f);
        if (src == null) m.SetColor("_BaseColor", new Color(.55f, .53f, .5f));
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ---------------------------------------------------------------- loose props (Rigidbody: they can be shoved)

    // rooms of the new floor, plan rectangles e0, e1, n0, n1 (also the minimap's)
    internal static readonly (string room, float e0, float e1, float n0, float n1)[] V3Rooms =
    {
        ("ENTRANCE LOBBY", -7.9f, 8f, .2f, 5.6f), ("RECEPTION", -7.6f, 7.8f, 10.2f, 15.2f), ("LOUNGE", -29.6f, -20.8f, 7f, 15.2f),
        ("ARMORY", 21.3f, 29.8f, 7.1f, 13.1f), ("KESSLER'S OFFICE", 21.3f, 29.8f, 13.4f, 21f), ("VANE'S OFFICE", 21.3f, 29.8f, 21.3f, 27.9f),
        ("MAINTENANCE", 21.9f, 29.8f, 31.8f, 38.4f), ("BOARDROOM", -29.6f, -20.8f, 15.3f, 30.3f), ("SECURITY / SERVER", 14f, 21.7f, 30.1f, 38.4f),
        ("ARCHIVE", 9.3f, 13.7f, 30.1f, 34.6f), ("PANTRY", -12.6f, 3.2f, 30.1f, 35.6f), ("LOADING / TO SILO", -12.6f, 3.9f, 35.9f, 44.2f),
        ("RESTROOMS", -29.6f, -16.5f, 30.5f, 36.5f), ("OPEN OFFICE", -20.8f, 21.3f, 15.2f, 30f), ("MAIN HALL", -20.8f, 21.3f, 6f, 15.2f),
        ("WEST CORRIDOR", 21.3f, 29.8f, 28f, 31.8f),
    };

    static Transform props;

    static void Props()
    {
        pack = AssetDatabase.LoadAssetAtPath<GameObject>(SuppliesFbx);
        dressBurnt = false;   // (the main scene's office is clean now)
        if (!AssetDatabase.IsValidFolder(KvDir)) AssetDatabase.CreateFolder(GenDir, "KV");
        var old = model.Find("Props (physics)");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        props = new GameObject("Props (physics)").transform;
        props.SetParent(model, false);
        props.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        supplies = props;
        var rng = new System.Random(7);
        var keep = office.GetComponentsInChildren<Transform>().Where(t => t.GetComponent<Interactable>() != null || t.GetComponent<GhostNPC>() != null || t.name.StartsWith("Body bag"))
                         .Select(t => t.position).ToList();
        keep.Add(P(0f, Floor, 2.6f));   // (where you come in)
        int floor = 0, desk = 0;

        // against the walls: (pieces, how many, rooms, mass, scale)
        var wall = new (string[] parts, int n, string[] rooms, float mass, float scale)[]
        {
            (new[] { "Trash Cam" }, 9, new[] { "OPEN OFFICE", "RECEPTION", "PANTRY", "KESSLER'S OFFICE", "VANE'S OFFICE", "BOARDROOM", "MAINTENANCE", "MAIN HALL", "LOUNGE" }, 2f, 1f),
            (new[] { "Potted Plant" }, 9, new[] { "ENTRANCE LOBBY", "ENTRANCE LOBBY", "MAIN HALL", "OPEN OFFICE", "OPEN OFFICE", "BOARDROOM", "KESSLER'S OFFICE", "VANE'S OFFICE", "LOUNGE" }, 6f, 1.9f),
            (new[] { "Water Cooler", "Water Jug" }, 3, new[] { "PANTRY", "MAIN HALL", "OPEN OFFICE" }, 14f, 1f),
            (new[] { "2 Drawer Filing Cabinet" }, 3, new[] { "OPEN OFFICE", "ARCHIVE", "MAINTENANCE" }, 22f, 1f),
            (new[] { "4 Drawer Filing Cabinet" }, 3, new[] { "OPEN OFFICE", "KESSLER'S OFFICE", "SECURITY / SERVER" }, 35f, 1f),
            (new[] { "Printer" }, 1, new[] { "OPEN OFFICE" }, 9f, 1f),
            (new[] { "Panasonic VHS TV" }, 1, new[] { "MAINTENANCE" }, 9f, 1f),
        };
        foreach (var kind in wall)
            for (int i = 0; i < kind.n; i++)
            {
                var r = V3Rooms.First(x => x.room == kind.rooms[i % kind.rooms.Length]);
                var size = Size(kind.parts) * kind.scale;   // (x front to back, z across)
                for (int tries = 0; tries < 80; tries++)
                {
                    float e = Mathf.Lerp(r.e0, r.e1, (float)rng.NextDouble()), n = Mathf.Lerp(r.n0, r.n1, (float)rng.NextDouble());
                    if (!AgainstWall(e, n, size, keep, out var foot, out var face)) continue;
                    var t = Put(kind.parts[0], foot, face, Vector3.one * kind.scale, false, kind.parts);
                    Loose(t, kind.mass);
                    keep.Add(foot);
                    floor++;
                    break;
                }
            }

        // on the tables and desks
        var tops = new (string table, string[] parts, float mass)[]
        {
            ("Reception desk 27.5", new[] { "Coffee Cup" }, .3f), ("Reception desk 31", new[] { "Stack of Papers 1" }, .5f), ("Reception desk 31", new[] { "Stapler" }, .3f),
            ("Kessler desk", new[] { "Binders" }, .8f), ("Kessler desk", new[] { "Coffee Cup" }, .3f),
            ("Vane desk", new[] { "Stack of Papers 2" }, .5f), ("Vane desk", new[] { "Tray 1" }, .4f),
            ("Pantry table 1", new[] { "Coffee Cup" }, .3f), ("Pantry table 2", new[] { "Coffee Cup" }, .3f), ("Pantry table 3", new[] { "Stack of Papers 1" }, .5f),
            ("Extended blue conference table", new[] { "Binders" }, .8f), ("Extended blue conference table", new[] { "Stack of Papers 2" }, .5f), ("Extended blue conference table", new[] { "Coffee Cup" }, .3f),
            ("Maintenance bench", new[] { "Tray 2" }, .4f), ("Control desk and drawers", new[] { "Coffee Cup" }, .3f),
        };
        foreach (var (table, parts, mass) in tops)
        {
            var c = At(table);
            var s = Size(parts);
            var on = Desk(c.x, c.y, parts[0] + " on " + table, Mathf.Max(s.x, s.z) * .5f + .05f, null, 1.4f);
            if (Mathf.Abs(Top(on).y - on.y) > .05f) continue;   // (no free top: Desk has logged it)
            var t = Put(parts[0], on + Vector3.up * .005f, Quaternion.Euler(0f, rng.Next(360), 0f) * Vector3.forward, Vector3.one, false, parts);
            Loose(t, mass);
            Physics.SyncTransforms();
            desk++;
        }
        log.AppendLine("props: " + floor + " against walls, " + desk + " on tables");
    }

    static void Loose(Transform t, float mass)
    {
        var rb = t.gameObject.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = mass < 1f ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.Discrete;
        Physics.SyncTransforms();
    }

    // a spot by a solid wall (not a window, not a doorway, nothing else there) for a piece `size` (x deep, z wide):
    // its foot on the floor, `face` away from the wall
    static bool AgainstWall(float e, float n, Vector3 size, List<Vector3> keep, out Vector3 foot, out Vector3 face)
    {
        foot = face = default;
        var at = P(e, Floor, n);
        if (Mathf.Abs(Top(at).y - Floor) > .05f) return false;
        foreach (var k in keep) if ((k - at).sqrMagnitude < 2.2f * 2.2f) return false;
        foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
        {
            if (!Ray(at + Vector3.up * 1.2f, dir, out var h, 1.6f) || Vector3.Dot(h.normal, dir) > -.95f || h.collider is BoxCollider) continue;   // (a wall: mesh collider, square on)
            var across = Vector3.Cross(Vector3.up, dir);
            bool solid = true;   // the wall goes on past both sides of it, low and high: no doorway or window beside
            foreach (float u in new[] { -1f, 0f, 1f })
                foreach (float y in new[] { .4f, 1.9f })
                    if (!Ray(h.point - dir * .3f + across * u * (size.z * .5f + .9f) + Vector3.up * (y - 1.2f), dir, out var w, .6f)) solid = false;
            if (!solid) continue;
            var c = new Vector3(h.point.x, Floor, h.point.z) - dir * (size.x * .5f + .04f);
            var rot = Quaternion.LookRotation(dir);
            // room for it, and a clear 0.9 m in front of it so it never narrows a way through
            if (Blocked(c + Vector3.up * (size.y * .5f + .05f), new Vector3(size.z * .5f + .1f, size.y * .5f, size.x * .5f + .02f), rot, null)) continue;
            if (Blocked(c - dir * (size.x * .5f + .5f) + Vector3.up * 1f, new Vector3(size.z * .5f + .3f, .7f, .45f), rot, null)) continue;
            if (!NavMesh.SamplePosition(c - dir * (size.x * .5f + .5f), out _, .5f, NavMesh.AllAreas)) continue;
            foot = c; face = -dir;
            return true;
        }
        return false;
    }

    // InteriorDarkness hangs its red emergency lamps here: one under the ceiling in the middle of each room
    static void EmergencySpots()
    {
        var dark = office.GetComponent<InteriorDarkness>();
        float under = model.Find("Ceiling (generated)").GetComponent<Renderer>().bounds.min.y;
        dark.emergencySpots = V3Rooms.Where(r => r.room != "RESTROOMS" && r.room != "LOUNGE")
                                     .Select(r => P((r.e0 + r.e1) * .5f, under, (r.n0 + r.n1) * .5f)).ToArray();
        EditorUtility.SetDirty(dark);
    }

    // ---------------------------------------------------------------- checks

    // every room reachable on the walk map from the front door, and nothing outside the building
    static void Reach()
    {
        var path = new NavMeshPath();
        if (!NavMesh.SamplePosition(P(0f, Floor, 2.6f), out var from, 2f, NavMesh.AllAreas)) { log.AppendLine("REACH: no walk map at the front door"); return; }
        int ok = 0;
        foreach (var r in V3Rooms)
        {
            var c = FreeFloor((r.e0 + r.e1) * .5f, (r.n0 + r.n1) * .5f, .45f, r.room + " (reach test)", 4f);   // (bare floor: not a table top)
            if (!NavMesh.SamplePosition(c, out var to, .5f, NavMesh.AllAreas)) { log.AppendLine("REACH: no walk map in " + r.room); continue; }
            if (NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) ok++;
            else log.AppendLine("REACH: cannot walk to " + r.room);
        }
        // outside: a ring round the building, and the four corners its walls leave bare (floored, but out of doors)
        var b = ModelBounds();
        var outside = new List<Vector3> { P(-20f, Floor, 3f), P(20f, Floor, 3f), P(22f, Floor, 42f), P(-24f, Floor, 41f), P(8f, Floor, 43f) };
        for (float x = b.min.x - 1.5f; x <= b.max.x + 1.5f; x += 1f) { outside.Add(new Vector3(x, Floor, b.min.z - 1.5f)); outside.Add(new Vector3(x, Floor, b.max.z + 1.5f)); }
        for (float z = b.min.z - 1.5f; z <= b.max.z + 1.5f; z += 1f) { outside.Add(new Vector3(b.min.x - 1.5f, Floor, z)); outside.Add(new Vector3(b.max.x + 1.5f, Floor, z)); }
        int leaks = 0;
        foreach (var p in outside)
        {
            if (!NavMesh.SamplePosition(p, out var o, .6f, NavMesh.AllAreas)) continue;
            if (NavMesh.CalculatePath(from.position, o.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) { leaks++; if (leaks < 6) log.AppendLine("LEAK: walkable outside at " + o.position); }
        }
        log.AppendLine("reach: " + ok + "/" + V3Rooms.Length + " rooms (the armory and Vane's office count as shut), " + leaks + " walkable spots outside");

        // the locked doors as they are once open: the player's capsule (0.3 m, 1.7 m tall) goes through
        var armory = office.Find("Armory door");
        if (armory != null) Through("armory doorway", armory.position, Vector3.back, armory);
        var blocker = office.Find("KesslerDoor/DoorBlocker");
        if (blocker != null) Through("Vane's doorway", blocker.position, Vector3.right, blocker);
    }

    static void Through(string what, Vector3 centre, Vector3 dir, Transform open)
    {
        var off = open.GetComponentsInChildren<Collider>().Where(c => c.enabled).ToList();
        foreach (var c in off) c.enabled = false;
        Physics.SyncTransforms();
        var a = new Vector3(centre.x, Floor, centre.z) - dir * 1.2f;
        bool hit = Physics.CapsuleCast(a + Vector3.up * .35f, a + Vector3.up * 1.4f, .3f, dir, 2.4f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var c in off) c.enabled = true;
        Physics.SyncTransforms();
        log.AppendLine(what + (hit ? ": TOO TIGHT for the player" : ": the player fits through"));
    }
}
