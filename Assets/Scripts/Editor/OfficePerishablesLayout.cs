using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Where the Perishables pieces stand in the v3 office, room by room after the user's floor plan (pods of four desks
// round the open office, seating in the lobby, hall and lounge, a reception desk, a security desk, cars in the
// loading bay...). All of it under "Office Ground Floor/Perishables": run again and it is put back from scratch.
// Also: every box collider on the model's furniture becomes a mesh collider (a box round a whole workstation bank
// sealed the gaps between its desks), the lamps get fittings, and a few battery lamps (exit signs, the security
// screens) stay lit in the blackout.
//
// Plan coordinates (metres east, north of the front door); facings are compass degrees: 0 north, 90 east.
// A floor piece that would stand in anything, or within reach of a story object, is left out and logged.
public static partial class NewOfficeFit
{
    static Transform dressing;
    static readonly List<(Vector3 at, float r)> keepOut = new List<(Vector3, float)>();
    static readonly System.Random dressRng = new System.Random(21);

    [MenuItem("HellScape/Office/Perishables/Dress The Office")]
    public static void DressPerishables()
    {
        var m = GameObject.Find(ModelName);
        var a = GameObject.Find("OfficeAct");
        if (EditorApplication.isPlaying || m == null || !IsV3(m.transform) || a == null || a.transform.Find("Office") == null)
        { Debug.LogError("Needs the v3 '" + ModelName + "' and OfficeAct/Office in the open scene, out of Play."); return; }
        model = m.transform; act = a.transform; office = act.Find("Office");
        log.Clear(); pieces.Clear(); pieceMats.Clear();
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        try
        {
            Solidify();
            CoffeeTables();
            var old = model.Find("Perishables");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            dressing = new GameObject("Perishables").transform;
            dressing.SetParent(model, false);
            dressing.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Undo.RegisterCreatedObjectUndo(dressing.gameObject, "Perishables");
            Physics.SyncTransforms();
            KeepOut();
            foreach (var t in office.Cast<Transform>().Where(t => t.name.StartsWith("Battery")).ToList()) Object.DestroyImmediate(t.gameObject);
            OpenOffice(); Hall(); Lobby(); Reception(); Lounge(); Boardroom(); Pantry(); Loading(); Security(); Archive();
            Kessler(); Vane(); Maintenance(); Corridors(); BloodStains();
            Lamps();
            Physics.SyncTransforms();
            BakeNavMesh();
            Reach();
        }
        finally { Physics.queriesHitBackfaces = backfaces; }
        EditorSceneManager.MarkSceneDirty(m.scene);
        Report = log.ToString();
        Debug.Log("Office dressed with the Perishables packs: " + dressing.childCount + " pieces.\n" + Report);
    }

    // a box round a whole bank of desks stops you between them: the model's furniture gets its own shape
    static void Solidify()
    {
        int n = 0;
        var files = new HashSet<string>();
        foreach (var box in model.GetComponentsInChildren<BoxCollider>(true))
        {
            var t = box.transform;
            if (t.GetComponentInParent<Rigidbody>() != null || t.name.StartsWith("Ceiling") || t.parent.name.StartsWith("Seals") || t.parent.name == "Perishables") continue;
            if (box.isTrigger) continue;
            // (a supply piece holds its meshes in children, the box on the holder round all of them)
            var own = t.GetComponent<MeshFilter>();   // (not `is`: the editor hands back a fake null that passes it)
            var mfs = own != null ? new[] { own } : t.GetComponentsInChildren<MeshFilter>(true);
            if (mfs.Length == 0 || mfs.Any(f => f.sharedMesh == null || f.GetComponent<Collider>() != null && f.transform != t)) continue;
            Object.DestroyImmediate(box);
            foreach (var f in mfs) { f.gameObject.AddComponent<MeshCollider>().sharedMesh = f.sharedMesh; files.Add(AssetDatabase.GetAssetPath(f.sharedMesh)); }
            n++;
        }
        // (a mesh collider on a scaled part needs its mesh readable in a build)
        foreach (var path in files)
            if (AssetImporter.GetAtPath(path) is ModelImporter imp && !imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); log.AppendLine("  " + path + " made readable"); }
        Physics.SyncTransforms();
        log.AppendLine("furniture: " + n + " box colliders -> mesh colliders");
    }

    // the lounge coffee tables came in with a blank default material: the Mersus pack has their own wood
    static void CoffeeTables()
    {
        var tex = Tex("Assets/Perishables/mersus-office", "ComicTable", "BaseColor");
        if (tex == null) return;
        string path = PerishDir + "/Materials/Coffee table.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Folder(PerishDir + "/Materials");
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Coffee table" };
            mat.SetTexture("_BaseMap", tex); mat.SetFloat("_Smoothness", .3f);
            AssetDatabase.CreateAsset(mat, path);
        }
        foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            if (r.sharedMaterials.Length == 1 && r.sharedMaterial != null && r.sharedMaterial.name == "Lit" && AssetDatabase.GetAssetPath(r.sharedMaterial).StartsWith("Packages")) r.sharedMaterial = mat;
    }

    static void KeepOut()
    {
        keepOut.Clear();
        foreach (var t in office.GetComponentsInChildren<Transform>())
        {
            bool story = t.GetComponent<Interactable>() != null || t.GetComponent<GhostNPC>() != null || t.GetComponent<BodyBagScare>() != null
                      || t.parent != null && t.parent.name == "Damned spawners" || t.name == "Keypad" || t.name == "KesslerDoor" || t.name == "CCTV"
                      || t.name == "Safe" || t.name == "Tomas's locker" || t.name.StartsWith("Breaker");
            if (!story) continue;
            var r = t.GetComponentInChildren<Renderer>();
            keepOut.Add((r != null ? r.bounds.center : t.position, 1.1f));
        }
        var eye = Root("EyeBalls");
        if (eye != null) keepOut.Add((eye.transform.position, 1.6f));
    }

    // ---------------------------------------------------------------- putting pieces down

    static Bounds PieceSize(string key) => Piece(key) is GameObject g ? g.GetComponent<MeshFilter>().sharedMesh.bounds : new Bounds();

    static Transform Stand(string room, string key, Vector3 foot, float face, bool solid)
    {
        var pf = Piece(key);
        if (pf == null) return null;
        var t = ((GameObject)PrefabUtility.InstantiatePrefab(pf, dressing)).transform;
        t.name = room + " - " + key;
        t.SetPositionAndRotation(foot, Quaternion.Euler(0f, face, 0f));
        if (solid) t.gameObject.AddComponent<MeshCollider>().sharedMesh = t.GetComponent<MeshFilter>().sharedMesh;
        Physics.SyncTransforms();
        return t;
    }

    // room for it: nothing solid where it would stand (floors aside; a loose prop of Props() in the way is taken
    // away), no story object within reach unless `story` allows it
    static bool Free(string room, string key, Vector3 foot, float face, float grow = 0f, bool story = false)
    {
        var b = PieceSize(key);
        var rot = Quaternion.Euler(0f, face, 0f);
        var half = new Vector3(Mathf.Max(.03f, b.extents.x - .06f + grow), Mathf.Max(.03f, b.extents.y - .06f), Mathf.Max(.03f, b.extents.z - .06f + grow));
        var centre = foot + rot * b.center + Vector3.up * .04f;
        var loose = new List<GameObject>();
        foreach (var c in Physics.OverlapBox(centre, half, rot, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.bounds.size.y < .12f && c.bounds.max.y < Floor + .1f) continue;   // a floor
            if (c.attachedRigidbody != null && c.transform.IsChildOf(model)) { loose.Add(c.attachedRigidbody.gameObject); continue; }
            log.AppendLine("  skipped " + room + " - " + key + ": in " + c.name);
            return false;
        }
        float reach = Mathf.Max(b.extents.x, b.extents.z) * .7f;
        if (!story)
            foreach (var (at, r) in keepOut)
                if (new Vector2(at.x - centre.x, at.z - centre.z).magnitude < r + reach) { log.AppendLine("  skipped " + room + " - " + key + ": by a story object at " + (at - new Vector3(DoorX, 0f, DoorZ)).ToString("F1")); return false; }
        foreach (var g in loose.Distinct()) { log.AppendLine("  (took away a loose " + g.name + " for " + room + " - " + key + ")"); Object.DestroyImmediate(g); }
        if (loose.Count > 0) Physics.SyncTransforms();
        return true;
    }

    // on the floor
    static Transform F(string room, string key, float e, float n, float face, bool solid = true, bool story = false)
    {
        var foot = P(e, Floor, n);
        return Free(room, key, foot, face, 0f, story) ? Stand(room, key, foot, face, solid) : null;
    }

    // on whatever top is there (a table, a counter, a barrel)
    static Transform On(string room, string key, float e, float n, float face)
    {
        var top = Top(P(e, Floor, n));
        if (top.y - Floor < .3f) { log.AppendLine("  skipped " + room + " - " + key + ": no table at " + e + ", " + n); return null; }
        return Stand(room, key, top + Vector3.up * .003f, face, false);
    }

    // back to the wall that lies `toward` (compass) of the point, its foot `height` above the floor. The wall has to
    // go on past both its sides (a piece on the floor: by half a metre more), so nothing stands in a doorway.
    static Transform W(string room, string key, float e, float n, float toward, float height = 0f, bool solid = true)
    {
        var b = PieceSize(key);
        var dir = Quaternion.Euler(0f, toward, 0f) * Vector3.forward;
        var across = Vector3.Cross(Vector3.up, dir);
        float mid = Mathf.Clamp(height + b.size.y * .5f, .5f, 2.3f);
        var from = P(e, Floor + mid, n);
        // (the outer walls are one-sided: from inside the ray meets their back, its normal along the ray)
        if (!Ray(from, dir, out var h, 6f) || Mathf.Abs(Vector3.Dot(h.normal, dir)) < .9f) { log.AppendLine("  skipped " + room + " - " + key + ": no wall " + toward + " of " + e + ", " + n); return null; }
        bool floor = height < .3f;
        float side = b.extents.x + (floor ? .5f : 0f);
        foreach (float u in new[] { -side, side })
            foreach (float y in floor ? new[] { 1f, 2.1f } : new[] { mid })
                if (!Ray(new Vector3(h.point.x, Floor + y, h.point.z) - dir * .3f + across * u, dir, out _, .6f))
                { log.AppendLine("  skipped " + room + " - " + key + ": a doorway or the wall's end beside " + e + ", " + n); return null; }
        var foot = new Vector3(h.point.x, Floor + height, h.point.z) - dir * (b.extents.z - b.center.z + .02f);
        float face = toward + 180f;
        if (floor && !Free(room, key, foot, face, -.03f)) return null;
        return Stand(room, key, foot, face, solid && floor);
    }

    // four desks back to back (rows along `face`'s right), each with screens, keyboard, tower and a chair
    static void Pod(string room, string desk, float e, float n, float face)
    {
        var c = P(e, Floor, n);
        var rot = Quaternion.Euler(0f, face, 0f);
        Vector3 f = rot * Vector3.forward, u = rot * Vector3.right;
        var db = PieceSize(desk);
        float deep = db.size.z, wide = db.size.x, top = db.max.y;
        bool free = true;   // (the whole pod, chairs included)
        foreach (var hit in Physics.OverlapBox(c + Vector3.up * .6f, new Vector3(wide + .1f, .5f, deep + .9f), rot, ~0, QueryTriggerInteraction.Ignore))
            if (!(hit.bounds.size.y < .12f && hit.bounds.max.y < Floor + .1f)) { free = false; log.AppendLine("  skipped pod " + room + " " + e + ", " + n + ": in " + hit.name); break; }
        if (free)
            foreach (var (at, r) in keepOut)
                if (new Vector2(at.x - c.x, at.z - c.z).magnitude < r + wide) { free = false; log.AppendLine("  skipped pod " + room + " " + e + ", " + n + ": by a story object"); break; }
        if (!free) return;

        foreach (int s in new[] { 1, -1 })
            foreach (int k in new[] { -1, 1 })
            {
                var d = c + f * (s * (deep * .5f + .01f)) + u * (k * (wide * .5f + .02f));
                float facing = s > 0 ? face : face + 180f;   // the sitter's side
                Stand(room, desk, d, facing, true);
                Stand(room, "monitors", d - f * s * .15f + Vector3.up * top, facing, false);
                Stand(room, "keyboard", d + f * s * .17f + Vector3.up * top, facing + (float)(dressRng.NextDouble() * 16 - 8), false);
                Stand(room, "pc tower", d + u * k * (wide * .5f - .2f) - f * s * .1f, facing, true);
                var chair = d + f * s * (deep * .5f + .45f + (float)dressRng.NextDouble() * .25f);
                Stand(room, "swivel chair", chair, facing + 180f + (float)(dressRng.NextDouble() * 70 - 35), true);
            }
    }

    // ---------------------------------------------------------------- the rooms

    static void OpenOffice()
    {
        const string R = "OPEN OFFICE";
        Pod(R, "wooden desk", -17.6f, 17.9f, 0f);
        Pod(R, "small white table", -17.6f, 22.5f, 0f);
        Pod(R, "wooden desk", -17.6f, 27.1f, 0f);
        Pod(R, "small white table", -3.6f, 24.6f, 0f);
        Pod(R, "wooden desk", 3.6f, 24.6f, 0f);
        Pod(R, "wooden desk", 17.6f, 17.9f, 0f);
        Pod(R, "small white table", 17.6f, 21.8f, 0f);

        // the middle: a meeting table, a shelf island with plants
        F(R, "meeting table", 0f, 20.6f, 0f);
        foreach (float x in new[] { -1.1f, 0f, 1.1f }) { F(R, "brown chair", x, 19.1f, 0f); F(R, "brown chair", x, 22.1f, 180f); }
        On(R, "coffee set", -.5f, 20.6f, 90f); On(R, "books", .7f, 20.5f, 0f);
        F(R, "shelving", 0f, 16.85f, 180f); F(R, "shelving", 0f, 17.2f, 0f);
        On(R, "vase", -.4f, 17.0f, 0f); On(R, "books", .5f, 17.0f, 180f);
        F(R, "big plant", -2.1f, 17.0f, 0f); F(R, "big plant", 2.1f, 17.0f, 0f);

        // along the north side
        W(R, "wide filing cabinet", -10f, 28.6f, 0f); W(R, "wide filing cabinet", -1.5f, 28.6f, 0f);
        W(R, "control shelf", 6f, 28.6f, 0f); W(R, "filing cabinet", 12.5f, 28.6f, 0f); W(R, "filing cabinet", 13.1f, 28.6f, 0f);
        W(R, "pinboard", -14f, 28.6f, 0f, 1.3f); W(R, "wall clock", 0f, 28.6f, 0f, 2.3f);
        F(R, "bush planter", -20f, 29.3f, 0f); F(R, "bush planter", 20.3f, 15.8f, 0f);
    }

    static void Hall()
    {
        const string R = "MAIN HALL";
        // (its south corners are slanted: the sofas stand free, backs to the outside wall)
        foreach (float s in new[] { -1f, 1f })
        {
            F(R, "leather sofa", s * 12f, 7.1f, 0f); F(R, "ottoman", s * 12f, 8.9f, 0f);
            F(R, "tub chair", s * 12f - 2.1f, 8.9f, 90f); F(R, "tub chair", s * 12f + 2.1f, 8.9f, 270f);
        }
        F(R, "big plant", -15.2f, 14.2f, 0f); F(R, "big plant", 15.2f, 14.2f, 0f);
        F(R, "big plant", -9f, 7f, 0f); F(R, "big plant", 9f, 7f, 0f);
    }

    static void Lobby()
    {
        const string R = "LOBBY";
        F(R, "big plant", -2.5f, 1.1f, 0f); F(R, "big plant", 2.5f, 1.1f, 0f);
        W(R, "control sofa", -6.5f, 3f, 270f); F(R, "side table", -7f, 1.1f, 0f); On(R, "vase", -7f, 1.1f, 0f);
        W(R, "portrait painting", -6.5f, 3f, 270f, 1.5f);
        W(R, "landscape painting", -4.6f, 4f, 0f, 1.45f); W(R, "landscape painting", 4.6f, 4f, 0f, 1.45f);
        F(R, "bush planter", 7.1f, 1.1f, 0f);
    }

    static void Reception()
    {
        const string R = "RECEPTION";
        F(R, "reception desk", .8f, 11.6f, 180f, true, true);   // (fuse 2 and the papers lie on it)
        On(R, "crt monitor", -.6f, 11.75f, 0f); On(R, "telephone", -1.6f, 11.7f, 0f); On(R, "computer", -.1f, 11.9f, 0f);
        F(R, "office chair", -.6f, 12.9f, 180f);
        W(R, "control shelf", -5.5f, 13.5f, 0f); W(R, "filing cabinet", 4.9f, 13.5f, 0f); W(R, "filing cabinet", 5.4f, 13.5f, 0f);
        W(R, "wall clock", 0f, 13.5f, 0f, 2.3f);
        F(R, "big plant", -6.6f, 14.3f, 0f); F(R, "bush planter", 7.2f, 14.5f, 0f);
    }

    static void Lounge()
    {
        const string R = "LOUNGE";
        F(R, "armchair", -26.4f, 12.9f, 180f); F(R, "armchair", -24f, 12.9f, 180f);
        F(R, "armchair", -20.1f, 12.9f, 180f); F(R, "armchair", -17.7f, 12.9f, 180f);
        W(R, "fireplace", -28.5f, 11f, 270f);
        W(R, "landscape painting", -25.2f, 14f, 0f, 1.5f); W(R, "portrait painting", -28.5f, 8.4f, 270f, 1.5f);
        F(R, "big plant", -28.6f, 14.3f, 0f); F(R, "side table", -27.9f, 8f, 0f); On(R, "vase", -27.9f, 8f, 0f);
    }

    static void Boardroom()
    {
        const string R = "BOARDROOM";
        W(R, "landscape painting", -27.5f, 19f, 270f, 1.5f); W(R, "portrait painting", -27.5f, 26f, 270f, 1.5f);
        W(R, "wall clock", -25.2f, 28.5f, 0f, 2.3f); W(R, "sideboard", -25.2f, 16.6f, 180f);
        W(R, "bookcase", -21.8f, 18.2f, 90f);
        F(R, "big plant", -28.7f, 16.2f, 0f); F(R, "big plant", -21.7f, 29.3f, 0f); F(R, "big plant", -28.6f, 27.6f, 0f);
    }

    static void Pantry()
    {
        const string R = "PANTRY";
        W(R, "control shelf", -11f, 34.5f, 0f);
        W(R, "wood table", -11.6f, 32.4f, 270f); On(R, "coffee set", -11.9f, 32.4f, 90f); On(R, "bean tin", -11.7f, 32.9f, 0f);
        F(R, "bar stool", -.4f, 34.1f, 0f); F(R, "bar stool", 1.5f, 34.1f, 0f);
        On(R, "bean tin", -4.3f, 31.9f, 0f); On(R, "vase", -.5f, 31.8f, 0f);
        W(R, "wall clock", -6f, 34f, 0f, 2.3f);
    }

    static void Loading()
    {
        const string R = "LOADING BAY";
        F(R, "police car", -.2f, 40.7f, 0f); F(R, "passat", -3.7f, 40.8f, 0f); F(R, "vespa rider", 2.6f, 38.4f, 0f);
        F(R, "barrel", -11.8f, 43.3f, 0f); F(R, "barrel", -11.1f, 43.6f, 0f); F(R, "barrel", -11.9f, 42.5f, 0f);
        F(R, "barrel", 3f, 43.4f, 0f); F(R, "barrel", 3.1f, 42.6f, 0f);
        On(R, "crowbar", -11.8f, 43.3f, 30f);
        F(R, "carton pile", -6f, 43.5f, 0f); F(R, "evidence boxes", 1.6f, 43.5f, 0f);
        W(R, "fuse panel", -12f, 40f, 270f, 1f);
    }

    static void Security()
    {
        const string R = "SECURITY";
        var desk = W(R, "control desk", 15.3f, 32.3f, 270f);
        if (desk != null)
        {
            var e = desk.position.x - DoorX; var n = desk.position.z - DoorZ;
            On(R, "crt monitor", e - .1f, n - .45f, 90f); On(R, "tall monitor", e - .1f, n + .3f, 90f); On(R, "telephone", e + .05f, n + .8f, 90f);
            F(R, "office chair", e + 1.15f, n, 270f);
            Battery(desk.position + Vector3.up * 1.3f + Vector3.right * .2f, new Color(.45f, .7f, 1f), .7f, 3f, "Battery security screens");
        }
        W(R, "fuse panel", 17f, 31f, 180f, 1f); W(R, "pinboard", 15.6f, 31f, 180f, 1.3f);
        W(R, "wall cabinet", 20.2f, 31.2f, 180f, 1.2f);
    }

    static void Archive()
    {
        const string R = "ARCHIVE";
        W(R, "filing cabinet", 10f, 32.3f, 270f); W(R, "filing cabinet", 10f, 32.8f, 270f);
        F(R, "evidence boxes", 12.3f, 33.4f, 0f); F(R, "carton", 11.6f, 31.2f, 20f);
    }

    static void Kessler()
    {
        const string R = "KESSLER'S OFFICE";
        W(R, "evidence wall", 26.2f, 19.5f, 0f, .95f);
        W(R, "low bookshelf", 24.4f, 19.5f, 0f);
        W(R, "map wall", 28f, 15.1f, 90f, .9f);
        F(R, "coat stand", 29.2f, 20.5f, 0f);
        W(R, "old safe", 28.6f, 14.2f, 90f);
        W(R, "portrait painting", 22.2f, 17f, 270f, 1.5f); W(R, "wall clock", 26f, 14.6f, 180f, 2.3f);
        On(R, "vase", 22.6f, 20.5f, 0f);
    }

    static void Vane()
    {
        const string R = "VANE'S OFFICE";
        W(R, "bookcase", 28.8f, 22.6f, 90f);
        W(R, "landscape painting", 24f, 27f, 0f, 1.5f); W(R, "wall clock", 27.6f, 22.2f, 180f, 2.3f);
    }

    static void Maintenance()
    {
        const string R = "MAINTENANCE";
        W(R, "control shelf", 24.6f, 37.5f, 0f); F(R, "carton pile", 23f, 37.7f, 0f);
        W(R, "fuse panel", 25f, 32.5f, 180f, 1f); On(R, "crowbar", 28.6f, 37.9f, 30f);
        W(R, "small lockers", 26.6f, 37.6f, 0f);   // (no barrel: the door is by that corner)
    }

    static void Corridors()
    {
        const string R = "SERVICE CORRIDOR";
        W(R, "filing cabinet", 8.4f, 33f, 90f); W(R, "small lockers", 8.4f, 37f, 90f);
        F(R, "carton", 8.3f, 40.5f, 10f);
    }

    // old blood on the floors: (e, n, turn, size, spatter?)
    static void BloodStains()
    {
        var stains = new (float e, float n, float turn, float size, bool spatter)[]
        {
            (-1.4f, 3.4f, 20f, 1.2f, false), (4.8f, 9.2f, 140f, .9f, true), (-10.5f, 24f, 75f, 1.1f, false), (-6.2f, 33.4f, 200f, .8f, true),
            (-8.4f, 40.2f, 300f, 1.3f, false), (25f, 36f, 40f, 1f, true), (24f, 25.9f, 110f, .7f, true), (-25f, 18f, 260f, .9f, false),
            (6.4f, 38f, 10f, 1f, true), (4.4f, 13.7f, 330f, .8f, false), (-26.4f, 33.6f, 90f, 1f, false), (12f, 19.6f, 15f, 1.2f, true),
            (26.5f, 17.8f, 0f, .8f, false), (-17.6f, 20.2f, 160f, .9f, true), (17.4f, 32.8f, 230f, 1f, true),
        };
        int i = 0;
        foreach (var s in stains)
        {
            var t = Stand("BLOOD", s.spatter ? "blood spatter" : "blood pool", P(s.e, Floor + .006f + (i++ % 4) * .0015f, s.n), s.turn, false);
            if (t != null) { t.localScale = Vector3.one * s.size; t.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
        }
    }

    // ---------------------------------------------------------------- light

    // The fuses bring the lamps back: each lamp gets a pendant fitting under the ceiling, warm white, bright enough
    // to see the room by (InteriorDarkness thins its black fog once they are on). In the blackout a red emergency lamp
    // hangs at every one of those places (not one per room: the open office was black between them), and the battery
    // lamps stay lit: exit signs over the ways out, the security screens, a few computers left on in the pods.
    static void Lamps()
    {
        float ceiling = model.Find("Ceiling (generated)").GetComponent<Renderer>().bounds.min.y;
        var fit = PieceSize("ceiling lamp");
        var power = office.Find("PowerLights");
        var spots = new List<Vector3>();
        if (power != null)
            foreach (Transform l in power)
            {
                var light = l.GetComponent<Light>();
                light.color = new Color(1f, .87f, .72f); light.intensity = 5.5f; light.range = 12f; light.shadows = LightShadows.Soft;
                foreach (var old in l.Cast<Transform>().ToList()) Object.DestroyImmediate(old.gameObject);
                var at = new Vector3(l.position.x, ceiling - fit.size.y, l.position.z);
                Stand("LAMPS", "ceiling lamp", at, 0f, false).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                l.position = at - Vector3.up * .12f;
                spots.Add(new Vector3(l.position.x, ceiling, l.position.z));
            }

        var dark = office.GetComponent<InteriorDarkness>();
        if (dark != null)
        {
            Undo.RecordObject(dark, "Office light");
            if (spots.Count > 0) dark.emergencySpots = spots.ToArray();
            dark.emergencyColor = new Color(1f, .07f, .04f);
            dark.emergencyIntensity = 2.6f; dark.emergencyRange = 9f;
            dark.emergencyAmbient = new Color(.05f, .013f, .013f);
            dark.poweredAmbient = new Color(.3f, .25f, .21f);
            EditorUtility.SetDirty(dark);
        }

        // computers left on (on the batteries too): a pale screen and its glow
        var screens = dressing.Cast<Transform>().Where(t => t.name.EndsWith(" - monitors")).ToList();
        var mb = PieceSize("monitors");
        for (int i = 0; i < screens.Count; i += 5)
        {
            var m = screens[i];
            foreach (float x in new[] { -.31f, .31f })
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = "screen glow";
                q.transform.SetParent(m, false);
                q.transform.localPosition = new Vector3(x, mb.center.y + .05f, mb.max.z + .006f);
                q.transform.localScale = new Vector3(.5f, .3f, 1f);
                q.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // (a quad shows its face to -z)
                q.GetComponent<Renderer>().sharedMaterial = GlowMat("Screen glow", new Color(.16f, .27f, .38f));
                q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Battery(m.position + Vector3.up * .3f + m.forward * .45f, new Color(.5f, .72f, 1f), .5f, 2.4f, "Battery computer screen");
        }

        Exit(P(0f, Floor + 2.55f, .45f), Vector3.forward);
        var silo = office.Find("SiloDoor");
        if (silo != null) Exit(new Vector3(silo.position.x, Floor + 2.45f, silo.position.z) + silo.right * 2.15f - silo.forward * .1f, -silo.forward);   // (beside it: the SILO label is over it)
        log.AppendLine("lamps: " + (power != null ? power.childCount : 0) + " fittings");
    }

    // a green EXIT sign on battery over a way out, read from `facing`
    static void Exit(Vector3 at, Vector3 facing)
    {
        var sign = new GameObject("Battery exit sign").transform;
        sign.SetParent(office, false);
        sign.SetPositionAndRotation(at, Quaternion.LookRotation(-facing));
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(box.GetComponent<Collider>());
        box.transform.SetParent(sign, false);
        box.transform.localScale = new Vector3(.62f, .24f, .06f);
        box.GetComponent<Renderer>().sharedMaterial = ExitMat();
        var text = new GameObject("EXIT").AddComponent<TextMeshPro>();
        text.transform.SetParent(sign, false);
        text.transform.localPosition = new Vector3(0f, 0f, -.035f);
        text.text = "EXIT"; text.fontSize = 2.2f; text.alignment = TextAlignmentOptions.Center; text.color = new Color(.85f, 1f, .85f);
        text.rectTransform.sizeDelta = new Vector2(.6f, .24f);
        if (GameFont.Main != null) text.font = GameFont.Main;
        Battery(at + facing * .3f, new Color(.25f, 1f, .4f), .9f, 3.5f, "Battery exit light").SetParent(sign, true);
    }

    static Transform Battery(Vector3 at, Color c, float intensity, float range, string name)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.transform.SetParent(office, false);
        l.transform.position = at;
        l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        return l.transform;
    }

    static Material ExitMat()
    {
        const string path = PerishDir + "/Materials/Exit sign.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        Folder(PerishDir + "/Materials");
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Exit sign" };
        m.SetColor("_BaseColor", new Color(.05f, .35f, .12f));
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(.1f, 1.2f, .3f));
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material GlowMat(string name, Color c)
    {
        string path = PerishDir + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) { m.SetColor("_BaseColor", c); EditorUtility.SetDirty(m); return m; }
        Folder(PerishDir + "/Materials");
        m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
        m.SetColor("_BaseColor", c);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
