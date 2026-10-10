using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

// The office act in the Kessler & Vane office model ("Office Ground Floor", ~100 x 71 m; NewOfficeSupplies.cs puts
// it in the scene). Nothing is rebuilt: every puzzle, witness and prop of OfficeAct/Office is MOVED into the rooms
// with its wiring intact. Safe to run again after editing the tables below: every placement is absolute.
//
// Coordinates in this file are PLAN coordinates: metres east and north of the front door (P() converts), the way
// the floor plan is drawn.
//
//   entrance lobby  e -8..6    n 1..13    arrival, the way out (the front doors)
//   corridor        e 3..6     n 14..26   fuse box (east wall); north to the open-plan door (bolted on the far
//                                         side), east to the store room door (locked: reception has the key)
//   reception       e -16..-6  n 14..30   your desk, Derek's keyboard (fuse 2), the store room key, Priya
//   store room      e 18..29   n 12..24   Tomas's locker (fuse 3), his rota, a body bag
//   open office     e 0..28    n 27.5..45 the hub: Nadia; the guard
//   kitchen         e -6..14   n 45..58   the cake (fuse 1), the calendar, Ben
//   conference      e 19..35   n 54..64   the locker key, the other body bag
//   main hall       the corridor round the open office and east to Vane's door (e 59.6, n 34.5): keypad beside it
//   Vane's office   e 60..77   n 28..42   the safe, the ledger, the security pass
//   records         e 43..60   n 41..52   the incident report
//   security        e 35..48   n 20..30   the CCTV terminal (wants the pass)
//   service         e 49..78   n 14..23   the silo door closes its east end
public static partial class NewOfficeFit
{
    public const string ModelName = "Office Ground Floor";
    const string OldModelName = "Sketchfab_2023_11_11_16_15_44";
    const string DoorModel = "4bfaca4c4a794e9393a413e88dd5611a.fbx";
    const string GenDir = "Assets/Generated/Office";
    const string DamnedPrefab = "Assets/Enemies/Scary Zombie Pack/PrimeZombie.prefab";
    const float Floor = 3.4f;

    static Transform model, act, office;
    static readonly System.Text.StringBuilder log = new System.Text.StringBuilder();

    // one lamp of each kind over every room and stretch of corridor
    static readonly Vector2[] Rooms =
    {
        new Vector2(-4f, 4f), new Vector2(2.5f, 4f), new Vector2(-1f, 10f), new Vector2(0f, 15f),          // entrance lobby
        new Vector2(4.2f, 21f), new Vector2(11f, 21.5f),                                                    // corridor
        new Vector2(-11f, 17.5f), new Vector2(-11f, 26f), new Vector2(-5.5f, 32.2f),                        // reception
        new Vector2(21f, 20f), new Vector2(25.5f, 15f),                                                     // store room
        new Vector2(5f, 30.5f), new Vector2(14f, 30.5f), new Vector2(23f, 30.5f),                           // open office
        new Vector2(5f, 40f), new Vector2(14f, 40f), new Vector2(23f, 40f),
        new Vector2(-1f, 50f), new Vector2(8f, 51f), new Vector2(2f, 55f),                                  // kitchen
        new Vector2(20f, 47.7f), new Vector2(29f, 50f), new Vector2(29f, 42f),                              // main hall, north and east of the open office
        new Vector2(23f, 59f), new Vector2(31f, 59f),                                                       // conference
        new Vector2(33f, 36f), new Vector2(42f, 36f), new Vector2(51f, 36f), new Vector2(57f, 36f),         // main hall, east to Vane's door
        new Vector2(47f, 46f), new Vector2(55f, 46f), new Vector2(40.7f, 45f),                              // records
        new Vector2(39f, 24.5f), new Vector2(45f, 24.5f), new Vector2(51f, 27f),                            // security, the passage beside it
        new Vector2(38f, 13.2f), new Vector2(47f, 16f),                                                     // return hall
        new Vector2(55f, 18.5f), new Vector2(65f, 18.5f), new Vector2(74f, 19f),                            // service corridor
        new Vector2(64f, 32f), new Vector2(70f, 38f), new Vector2(74f, 31f),                                // Vane's office
    };

    // where the Damned turn up, and from which stage of the act (2 = two fuses in, 4 = Vane's door is open)
    static readonly (float x, float z, int stage)[] DamnedPoints =
    {
        (12f, 28.3f, 2), (-12f, 18f, 2),
        (22f, 13.5f, 3), (20f, 48f, 3), (29f, 40f, 3),
        (50f, 45f, 4), (41f, 24f, 4), (62f, 19f, 4),
    };

    [MenuItem("HellScape/Office/Fit To New Office")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Stop Play first."); return; }
        var a = GameObject.Find("OfficeAct");
        if (a == null || a.transform.Find("Office") == null) { Debug.LogError("Needs OfficeAct/Office in the open scene."); return; }
        log.Clear();
        Undo.RegisterFullObjectHierarchyUndo(a, "Fit office");
        model = Install(false); act = a.transform; office = act.Find("Office");
        var m = model.gameObject;

        // the walls are one-sided: without this a probe from behind goes straight through
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        try
        {
            PlaceModel();
            Physics.SyncTransforms();
            Layout();
            Extras();
            Plates();
            BakeNavMesh();
        }
        finally { Physics.queriesHitBackfaces = backfaces; }
        EditorSceneManager.MarkSceneDirty(m.scene);
        Report = log.ToString();
        Debug.Log("New office fitted.\n" + Report);
    }

    // only the walk map again, after furniture has been moved by hand (nothing is re-placed)
    [MenuItem("HellScape/Office/Rebake Office Walk Map")]
    public static void Rebake()
    {
        var m = GameObject.Find(ModelName);
        var a = GameObject.Find("OfficeAct");
        if (EditorApplication.isPlaying || m == null || !IsKv(m.transform) || a == null || a.transform.Find("Office") == null) { Debug.LogError("Needs '" + ModelName + "' and OfficeAct/Office in the open scene, out of Play."); return; }
        model = m.transform; act = a.transform; office = act.Find("Office");
        log.Clear();
        BakeNavMesh();
        EditorSceneManager.MarkSceneDirty(m.scene);
        Report = log.ToString();
        Debug.Log("Office walk map baked. " + Report);
    }

    // only the colleagues' name plates (nothing else is moved)
    [MenuItem("HellScape/Office/Colleague Name Plates")]
    public static void NamePlates()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Stop Play first."); return; }
        var m = GameObject.Find(ModelName);
        var a = GameObject.Find("OfficeAct");
        if (m == null || !IsKv(m.transform) || a == null || a.transform.Find("Office") == null) { Debug.LogError("Needs '" + ModelName + "' and OfficeAct/Office in the open scene."); return; }
        model = m.transform; act = a.transform; office = act.Find("Office");
        log.Clear();
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        try { Plates(); }
        finally { Physics.queriesHitBackfaces = backfaces; }
        EditorSceneManager.MarkSceneDirty(m.scene);
        Report = log.ToString();
        Debug.Log("Name plates.\n" + Report);
    }

    // A copy of your own desk sign on the desk nearest each of the four colleagues, turned to the side of the desk
    // you can walk up to. OfficeColleagues starts its scare near the sign, so a sign moved by hand takes the scare
    // with it: one that is already there is left alone (delete it to have it placed again).
    static void Plates()
    {
        var mine = office.Find("Your desk sign");
        if (mine == null) { log.AppendLine("MISSING Your desk sign"); return; }
        var ext = mine.GetComponent<Renderer>().bounds.extents;
        foreach (var who in new[] { "Priya", "Ben", "Nadia", "Omar" })
        {
            var g = office.Find("Ghost - " + who);
            if (g == null || office.Find("Desk sign - " + who) != null) continue;
            var on = Desk(g.position.x - DoorX, g.position.z - DoorZ, who + "'s name plate", Mathf.Max(ext.x, ext.z) + .03f, null, 14f);   // (anywhere within reach: Ben's kitchen has only tables)
            if (Mathf.Abs(Top(on).y - on.y) > .05f) continue;   // (no desk near: Desk has logged it)

            // the nearest edge of the desk with room to stand in front of it
            Vector3 face = Vector3.back; float best = float.MaxValue;
            foreach (var d in new[] { Vector3.back, Vector3.forward, Vector3.left, Vector3.right })
                for (float s = .1f; s < 2f && s < best; s += .1f)
                {
                    if (Top(on + d * s).y - Floor > .3f) continue;   // still the desk
                    var stand = on + d * (s + .4f); stand.y = Floor + 1f;
                    if (!Blocked(stand, new Vector3(.2f, .6f, .2f), Quaternion.identity, null)) { best = s; face = d; }
                    break;
                }

            var sign = Object.Instantiate(mine.gameObject, office).transform;
            Undo.RegisterCreatedObjectUndo(sign.gameObject, "Name plate");
            sign.name = "Desk sign - " + who;
            sign.rotation = Quaternion.LookRotation(-face) * Quaternion.Euler(mine.eulerAngles.x, 0f, 0f);   // the label is on its back face
            var b = sign.GetComponent<Renderer>().bounds;
            sign.position += new Vector3(on.x - b.center.x, on.y + .004f - b.min.y, on.z - b.center.z);
            var label = sign.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = who;
            log.AppendLine("name plate " + who + " at " + sign.position + " facing " + face);
        }
    }

    public static string Report = "";

    // ---------------------------------------------------------------- probes (plan coordinates in, world out)

    static Vector3 P(float x, float y, float z) => new Vector3(DoorX + x, y, DoorZ + z);

    // first hit on the office model itself (ignores everything placed in it)
    static bool Ray(Vector3 from, Vector3 dir, out RaycastHit hit, float dist = 15f)
    {
        var hits = Physics.RaycastAll(from, dir, dist, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (p, q) => p.distance.CompareTo(q.distance));
        foreach (var h in hits)
            if (h.collider.transform.IsChildOf(model) && h.collider.name != "Chair") { hit = h; return true; }   // (a chair is neither a wall nor a table top)
        hit = default;
        return false;
    }

    // the wall that `dir` looks at from here, `height` above the floor
    static Vector3 Wall(float x, float height, float z, Vector3 dir, string what)
    {
        var from = P(x, Floor + height, z);
        if (Ray(from, dir, out var h)) return h.point;
        log.AppendLine("NO WALL for " + what);
        return from + dir * 1.5f;
    }

    // top surface (floor or furniture) under a point
    static Vector3 Top(Vector3 world) =>
        Ray(new Vector3(world.x, Floor + 1.5f, world.z), Vector3.down, out var h, 2f) ? h.point : new Vector3(world.x, Floor, world.z);

    // anything solid in this box other than `self`: the model, or another prop of the act
    static bool Blocked(Vector3 centre, Vector3 half, Quaternion rot, Transform self)
    {
        foreach (var c in Physics.OverlapBox(centre, half, rot, ~0, QueryTriggerInteraction.Ignore))
        {
            if (self != null && c.transform.IsChildOf(self)) continue;
            if (c.transform.IsChildOf(model) || (c.transform.IsChildOf(office) && c.GetComponentInParent<GhostNPC>() == null)) return true;
        }
        return false;
    }

    // a stretch of bare wall near a point for something `width` wide that stands `depth` out from it between
    // `bottom` and `top` (above the floor): flat right across, nothing of the model or of the act in that space, and
    // room to stand in front of it. `point` is on the wall at floor height; `facing` looks at the wall.
    static bool ClearWall(Transform self, float x, float z, float radius, float width, float depth, float bottom, float top, out Vector3 point, out Vector3 facing, string what, float recess = .3f)
    {
        point = facing = default; float best = float.MaxValue;
        int nStand = 0, nWall = 0, nFlat = 0;
        Vector3 want = P(x, Floor, z);
        // (walkable from the spot asked for: not a ledge outside a window, not the next room through a wall)
        var path = new NavMeshPath();
        bool nav = NavMesh.SamplePosition(want, out var home, 2f, NavMesh.AllAreas);
        for (float dx = -radius; dx <= radius; dx += .25f)
            for (float dz = -radius; dz <= radius; dz += .25f)
            {
                Vector3 from = want + new Vector3(dx, 0f, dz);
                if (Mathf.Abs(Top(from).y - Floor) > .1f || Blocked(from + Vector3.up * 1.05f, new Vector3(.28f, .6f, .28f), Quaternion.identity, self)) continue;   // somewhere you can stand
                if (nav && (!NavMesh.SamplePosition(from, out var here, .4f, NavMesh.AllAreas) || !NavMesh.CalculatePath(home.position, here.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)) continue;
                nStand++;
                foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                {
                    float mid = Floor + (bottom + top) * .5f;
                    if (!Ray(new Vector3(from.x, mid, from.z), dir, out var h, 2.5f) || Mathf.Abs(h.normal.y) > .2f || Vector3.Dot(h.normal, dir) > -.95f) continue;
                    if (h.distance < depth + .75f) continue;   // (you stand here: the thing must fit between you and the wall)
                    nWall++;
                    float score = (h.point - new Vector3(want.x, mid, want.z)).sqrMagnitude;
                    if (score >= best) continue;
                    Vector3 right = Vector3.Cross(Vector3.up, dir);
                    bool flat = true;
                    foreach (float u in new[] { -.5f, 0f, .5f })
                        foreach (float y in new[] { bottom + .12f, top - .08f })
                            if (!Ray(new Vector3(from.x, Floor + y, from.z) + right * (u * width), dir, out var e, 3f) || e.distance > h.distance + recess) flat = false;   // (a shallow recess is still a wall, a window or a doorway isn't; whatever sticks out of it is caught below)
                    if (!flat) continue;
                    nFlat++;
                    var rot = Quaternion.LookRotation(dir);
                    if (Blocked(h.point - dir * (depth * .5f + .08f), new Vector3(width * .5f, (top - bottom) * .5f - .03f, depth * .5f - .04f), rot, self)) continue;
                    best = score; point = new Vector3(h.point.x, Floor, h.point.z); facing = dir;
                }
            }
        if (best < float.MaxValue) return true;
        log.AppendLine("NO CLEAR WALL for " + what + " (standing spots " + nStand + ", walls in reach " + nWall + ", flat enough " + nFlat + ", none of those empty)");
        return false;
    }

    // the nearest flat desk-height top to a point with nothing standing on it within `clear` (and not in `self`)
    static Vector3 Desk(float x, float z, string what, float clear = .12f, Transform self = null, float reach = 1.6f)
    {
        Vector3 c = P(x, 0f, z), best = default; float bestD = float.MaxValue;
        for (float dx = -reach; dx <= reach; dx += .1f)
            for (float dz = -reach; dz <= reach; dz += .1f)
            {
                float d = dx * dx + dz * dz;
                if (d >= bestD) continue;
                var t = Top(c + new Vector3(dx, 0f, dz));
                float h = t.y - Floor;
                if (h < .6f || h > 1.2f) continue;
                bool flat = true;
                foreach (var o in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                    if (Mathf.Abs(Top(t + o * .14f).y - t.y) > .03f) { flat = false; break; }
                // the desk itself, not a keyboard or a tray lying on it: nothing desk-high round about is lower
                if (flat)
                    for (int k = 0; k < 16 && flat; k++)
                    {
                        float low = Top(t + Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward * (k < 8 ? .3f : .55f)).y;
                        if (low - Floor >= .6f && t.y - low > .008f) flat = false;
                    }
                if (flat && !Blocked(t + Vector3.up * .1f, new Vector3(clear, .09f, clear), Quaternion.identity, self)) { best = t; bestD = d; }
            }
        if (bestD < float.MaxValue) return best;
        log.AppendLine("NO DESK for " + what + ": left at table height (OfficeEvidenceLayout gives it a table)");
        return new Vector3(c.x, Floor + .8f, c.z);
    }

    // a doorway in the wall `dir` looks at, found by sweeping rays `across` it: its centre on the wall face (at floor
    // height), its width and the height of its lintel
    static bool Opening(Vector3 from, Vector3 dir, Vector3 across, float span, out Vector3 centre, out float width, out float height)
    {
        const float step = .05f;
        int n = Mathf.RoundToInt(span / step);
        var dist = new float[n + 1];
        for (int i = 0; i <= n; i++) dist[i] = Ray(from + across * (i * step), dir, out var h, 12f) ? h.distance : 12f;
        // the wall is where most of the rays stop (a lamp or a plant in front of it is only a few of them)
        var near = dist.Where(d => d < 6f).ToArray();
        if (near.Length == 0) { centre = default; width = height = 0f; return false; }
        float wall = near.GroupBy(d => Mathf.Round(d * 10f)).OrderByDescending(g => g.Count()).First().Average();
        int a = -1, bestA = 0, bestB = -1;
        for (int i = 0; i <= n + 1; i++)
        {
            bool open = i <= n && dist[i] > wall + .8f;   // a doorway leads on; a skirting or a frame doesn't
            if (open && a < 0) a = i;
            if (!open && a >= 0) { if (i - 1 - a > bestB - bestA) { bestA = a; bestB = i - 1; } a = -1; }
        }
        centre = default; width = height = 0f;
        log.AppendLine("  sweep " + from + ": wall at " + wall.ToString("F2") + ", open " + (bestA * step).ToString("F2") + ".." + (bestB * step).ToString("F2") + " of " + span);
        if (bestB < bestA) return false;
        centre = from + across * ((bestA + bestB) * .5f * step) + dir * wall;
        centre.y = Floor;
        width = (bestB - bestA + 1) * step;
        height = Ray(centre + dir * .1f + Vector3.up * .3f, Vector3.up, out var lintel, 4f) ? lintel.point.y - Floor : 2.4f;
        return true;
    }

    static Bounds ModelBounds()
    {
        var rs = model.GetComponentsInChildren<Renderer>(true);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // ---------------------------------------------------------------- scene helpers

    static GameObject Root(string name) =>
        model.gameObject.scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);

    // a direct child of Office by its name; the first time, the old unnamed one (a "Light", a "Label", a "Body")
    // nearest `near` is found and given that name
    static Transform Kid(string name, string oldName = null, Vector3 near = default)
    {
        var t = office.Find(name);
        if (t != null || oldName == null) return t;
        float best = 2.5f;
        foreach (Transform c in office)
        {
            float d = Vector3.Distance(c.position, near);
            if (c.name == oldName && d < best) { best = d; t = c; }
        }
        if (t != null) t.name = name; else log.AppendLine("MISSING " + name + " (an old '" + oldName + "' near " + near + ")");
        return t;
    }

    // put `main` at a pose; whatever is passed in `with` keeps its place relative to it
    static void Move(Transform main, Vector3 pos, Quaternion rot, params Transform[] with)
    {
        if (main == null) return;
        var m = Matrix4x4.TRS(pos, rot, Vector3.one) * Matrix4x4.TRS(main.position, main.rotation, Vector3.one).inverse;
        var turn = rot * Quaternion.Inverse(main.rotation);
        foreach (var s in with)
            if (s != null) s.SetPositionAndRotation(m.MultiplyPoint3x4(s.position), turn * s.rotation);
        main.SetPositionAndRotation(pos, rot);
    }

    static void Shove(Vector3 by, params Transform[] what)
    {
        foreach (var t in what) if (t != null) t.position += by;
    }

    static Transform Node(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null) return t;
        t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    // a cube child, made once: position and rotation are in the parent's space
    static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, bool collider = true)
    {
        var t = parent.Find(name);
        var g = t != null ? t.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localRotation = Quaternion.identity;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        var c = g.GetComponent<BoxCollider>();
        if (!collider && c != null) Object.DestroyImmediate(c);
        return g;
    }

    static Interactable Inter(GameObject g, string prompt, string line = null, string give = null, string req = null,
                              bool consume = true, string locked = null, bool once = true, bool hide = false)
    {
        var it = g.GetComponent<Interactable>();
        if (it == null) it = g.AddComponent<Interactable>();   // not ??: Unity fake-null
        it.prompt = prompt; it.line = line; it.giveItem = give; it.requiredItem = req; it.consumeItem = consume;
        if (locked != null) it.lockedLine = locked;
        it.once = once; it.hideOnUse = hide;
        while (it.onUse.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(it.onUse, 0);
        return it;
    }

    static void Glow(Transform parent, Vector3 localPos, Color c)
    {
        var t = Node(parent, "Light");
        t.localPosition = localPos;
        var l = t.GetComponent<Light>();
        if (l == null) l = t.gameObject.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.intensity = .5f; l.range = 1.4f; l.shadows = LightShadows.None;
    }

    static Material MatOf(string path)
    {
        var t = office.Find(path);
        var r = t != null ? t.GetComponent<Renderer>() : null;
        if (r == null) { log.AppendLine("no material at " + path); return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat"); }
        return r.sharedMaterial;
    }

    // ---------------------------------------------------------------- the model

    static void PlaceModel()
    {
        var old = Root(OldModelName);
        if (old != null) old.SetActive(false);   // the first office's rooms: switched off, not deleted

        // darkness, the Echo, the stalker and the Damned all take "inside the office" from this box
        var b = ModelBounds();
        b.Expand(new Vector3(3f, 6f, 3f));
        var dark = office.GetComponent<InteriorDarkness>();
        Bounds was = dark.zone;
        dark.zone = b;
        EditorUtility.SetDirty(dark);
        var spawner = Object.FindFirstObjectByType<ZombieSpawner>();
        if (spawner != null && spawner.interiors != null)
        {
            for (int i = 0; i < spawner.interiors.Length; i++)
                if (spawner.interiors[i].Contains(was.center) || spawner.interiors[i].Contains(b.center))
                { var z = b; z.Expand(new Vector3(14f, 12f, 14f)); spawner.interiors[i] = z; }
            EditorUtility.SetDirty(spawner);
        }
    }

    // ---------------------------------------------------------------- everything that was in the old rooms

    // a door leaf of the model by its number on the floor plan ("D06"), switched off or not
    static Transform Leaf(string id)
    {
        var mf = model.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(x => x.name.StartsWith("DoorLeaf_" + id));
        return mf != null ? mf.transform : null;
    }

    static void Layout()
    {
        // ---- ENTRANCE LOBBY: you come in by the front doors and look up the lobby at the corridor
        var arrival = act.Find("OfficeArrival");
        arrival.SetPositionAndRotation(P(.5f, Floor + 1.1f, 2.6f), Quaternion.identity);
        // the way out is the front doors themselves: the old door slab is only what you click on
        var exit = office.Find("ExitDoor");
        var exitWall = Wall(.5f, 1.25f, 2.6f, Vector3.back, "exit door");
        Move(exit, exitWall + Vector3.forward * .07f, Quaternion.LookRotation(Vector3.back));
        exit.localScale = new Vector3(4.3f, 2.5f, .1f);
        exit.GetComponent<Renderer>().enabled = false;

        // the rooms are big and the lamps far apart: strong, long reach
        Relight("EmergencyLights", 2.7f, 3.2f, 11f);
        Relight("PowerLights", 2.85f, 7f, 13f);

        // ---- FUSE BOX: on the corridor's east wall, same way round as ever (you face +x to use it)
        var fuseRoot = Root("Fuse").transform;
        var fbNode = office.Find("FuseBox (breaker)");
        var fb = fbNode.GetComponent<FuseBox>();
        var breaker = fbNode.Find("BreakerUse");
        var loose = new[] { fuseRoot.Find("Fuse1"), fuseRoot.Find("Fuse2"), fuseRoot.Find("Fuse3") };
        var picks = new[] { fbNode.Find("FusePickup 1"), fbNode.Find("FusePickup 2"), fbNode.Find("FusePickup 3") };
        var at = loose.Select(f => f.position).ToArray();   // the loose fuses are the box's children, but lie elsewhere
        var box = fuseRoot.Find("PowerBox").GetComponent<Renderer>().bounds;
        var boxWall = Wall(4.5f, 1.54f, 18.2f, Vector3.right, "fuse box");
        var by = new Vector3(boxWall.x - .005f - box.max.x, 0f, boxWall.z - box.center.z);
        Shove(by, fuseRoot, breaker, Kid("Breaker label", "Label", breaker.position), Kid("Breaker glow", "Light", breaker.position));
        fb.seatedPos += by;
        var boxT = fuseRoot.Find("PowerBox");
        if (boxT.GetComponent<Collider>() == null) boxT.gameObject.AddComponent<BoxCollider>();
        EditorUtility.SetDirty(fb);
        for (int i = 0; i < 3; i++) loose[i].position = at[i];

        // ---- KITCHEN: the cake with fuse 1 in it on a table, the calendar on the north wall
        var cake = Root("Cake Out Sub").transform;
        var cb = cake.GetComponentInChildren<Renderer>().bounds;
        foreach (var r in cake.GetComponentsInChildren<Renderer>()) cb.Encapsulate(r.bounds);
        var table = Desk(-1.9f, 47.3f, "cake", .26f);
        Shove(new Vector3(table.x - cb.center.x, table.y - cb.min.y, table.z - cb.center.z), cake, loose[0]);
        picks[0].position = loose[0].GetComponent<Renderer>().bounds.center;

        var cal = office.Find("Calendar");
        var calWall = Wall(5f, 1.75f, 54.5f, Vector3.forward, "calendar");
        Move(cal, calWall + Vector3.back * .02f, Quaternion.LookRotation(Vector3.forward),
             Kid("Calendar label", "Label", cal.position), Kid("Calendar glow", "Light", cal.position));

        // ---- RECEPTION: your desk on the counter island, Derek's keyboard (fuse 2) on the side table
        var note = office.Find("YourDesk_Note");
        var sign = Kid("Your desk sign", "Cube", note.position);
        note.position = Desk(-10.3f, 21.4f, "your desk", .1f, note) + Vector3.up * .01f;
        Physics.SyncTransforms();
        if (sign != null)   // the name plate stands on the counter beside it
        {
            var sb = sign.GetComponent<Renderer>().bounds;
            var on = Desk(-10.3f, 22.4f, "your name plate", Mathf.Max(sb.extents.x, sb.extents.z) + .03f, sign);
            sign.position += new Vector3(on.x - sb.center.x, on.y + .004f - sb.min.y, on.z - sb.center.z);
        }
        PutFuse(loose[1], picks[1], Desk(-6.8f, 29.6f, "Derek's desk", .08f));

        // ---- STORE ROOM (the cleaner's): a body bag on the floor (his locker and rota are in Extras)
        var crate = office.Find("Crate");
        if (crate != null) crate.gameObject.SetActive(false);   // fuse 3 sat on it; it's in the locker now
        Bag("Body bag (store room)", default, P(25f, 0f, 19.5f));
        // ...and the other in the conference room: the westmost one, so it's the one OfficeLurkers drops off the ceiling
        Bag("Body bag (boardroom)", default, P(20.4f, 0f, 61.6f));
        var pool = office.Find("BloodPool");
        if (pool != null) pool.position = P(4.2f, pool.position.y, 15.5f);

        // ---- VANE'S DOOR is the door of his office, at the end of the main hall: the model's own leaf comes off
        // and the door model stands in the doorway; its wall panels disappear into the jambs
        var doorAt = P(59.6f, 0f, 34.5f); float doorway = 1.94f;
        var own = Leaf("D21");
        if (own != null)
        {
            var ob = own.GetComponent<Renderer>().bounds;   // (as it hangs shut: the doorway, jamb to jamb)
            doorAt = ob.center; doorway = ob.size.z;
            own.gameObject.SetActive(false);
        }
        Physics.SyncTransforms();
        var doorRoot = Root(DoorModel).transform;
        var kd = office.Find("KesslerDoor");
        var frameT = doorRoot.Find("Plane_002");
        var frame = frameT.GetComponent<Renderer>().bounds;
        var doorRot = Quaternion.Euler(0f, 90f, 0f);
        var local = Quaternion.Inverse(doorRoot.rotation) * (frame.center - doorRoot.position);
        doorAt.y = frame.center.y;
        Move(doorRoot, doorAt - doorRot * local, doorRot, kd);
        kd.SetPositionAndRotation(doorAt, doorRot);
        float leaf = Mathf.Min(frame.size.x, frame.size.z) < .5f ? Mathf.Max(frame.size.x, frame.size.z) : 1.26f;
        float gap = Mathf.Max(0f, (doorway - leaf) * .5f);
        foreach (int side in new[] { -1, 1 })
        {
            var fill = Node(kd, side < 0 ? "Door side fill (south)" : "Door side fill (north)");
            fill.SetPositionAndRotation(doorAt + Vector3.forward * side * (leaf + gap) * .5f, Quaternion.identity);
            fill.position = new Vector3(fill.position.x, Floor + 1.35f, fill.position.z);
            var c = fill.GetComponent<BoxCollider>();
            if (c == null) c = fill.gameObject.AddComponent<BoxCollider>();
            c.size = new Vector3(.3f, 2.7f, gap + .1f);
        }
        log.AppendLine("Vane's door: doorway " + doorway.ToString("F2") + " at " + doorAt);
        var pad = office.Find("Keypad");
        var padWall = Wall(58f, 1.65f, 36.6f, Vector3.right, "keypad");
        pad.SetPositionAndRotation(padWall + Vector3.left * .03f, doorRot);

        // ---- VANE'S OFFICE, behind the door: the safe, and on his desk the second set of books
        var safe = office.Find("Safe");
        if (ClearWall(safe, 69f, 30f, 5f, 1.05f, .8f, 0f, 1.05f, out var sw0, out var sf0, "safe"))
            Move(safe, sw0 - sf0 * .46f, Quaternion.LookRotation(sf0));
        Physics.SyncTransforms();
        var report = office.Find("IncidentReport");
        var ledger = office.Find("KesslerLedger");
        var lamps = office.Cast<Transform>().Where(c => c.name == "Light" && Vector3.Distance(c.position, report.position) < 1.6f)
                          .OrderBy(c => Vector3.Distance(c.position, report.position)).ToArray();
        var reportLamp = office.Find("Report lamp"); var ledgerLamp = office.Find("Ledger lamp");
        if (reportLamp == null && lamps.Length > 0) { reportLamp = lamps[0]; reportLamp.name = "Report lamp"; }
        if (ledgerLamp == null && lamps.Length > 1) { ledgerLamp = lamps[1]; ledgerLamp.name = "Ledger lamp"; }
        ledger.position = Desk(66.4f, 36.2f, "ledger", .26f, ledger) + Vector3.up * .03f;
        // ---- RECORDS: the report he made everyone sign, on the reading table
        report.position = Desk(50.3f, 49.4f, "incident report", .22f, report) + Vector3.up * .01f;
        if (reportLamp != null) reportLamp.position = report.position + Vector3.up * .8f;
        if (ledgerLamp != null) ledgerLamp.position = ledger.position + Vector3.up * .8f;
        Physics.SyncTransforms();

        // ---- SECURITY: the CCTV terminal against a wall of the room
        var cctv = office.Find("CCTV");
        if (ClearWall(cctv, 44f, 23f, 7f, 1.7f, .95f, 0f, 1.95f, out var cw, out var cf, "CCTV terminal", 1.2f))
            Move(cctv, cw - cf * .1f, Quaternion.LookRotation(cf));
        else Move(cctv, P(47.6f, Floor, 21.4f), Quaternion.LookRotation(Vector3.right));
        Physics.SyncTransforms();

        // ---- the SILO DOOR closes the east end of the service corridor: past it the model just ends
        var silo = office.Find("SiloDoor");
        if (Opening(P(76.5f, Floor + 1.2f, 16.3f), Vector3.right, Vector3.forward, 5.6f, out var so, out float sw, out float sh))
        {
            var label = Kid("Silo label", "Label", silo.position);
            sh = Mathf.Min(sh, 2.8f);
            silo.SetPositionAndRotation(so + Vector3.up * sh * .5f + Vector3.right * .05f, Quaternion.LookRotation(Vector3.right));
            silo.localScale = new Vector3(sw + .06f, sh, .1f);
            if (label != null) label.SetPositionAndRotation(so + Vector3.up * (sh + .28f) + Vector3.left * .03f, Quaternion.LookRotation(Vector3.right));
            log.AppendLine("silo door: doorway " + sw.ToString("F2") + " x " + sh.ToString("F2") + " at " + so);
        }
        else log.AppendLine("NO DOORWAY for the silo door");

        // ---- the dead: where each one worked
        Ghost("Priya", -12.6f, 25.3f);      // front desk: reception (beside her chair)
        Ghost("Ben", 3f, 50f);              // the kitchen, with the cake
        Ghost("Nadia", 13.5f, 37.2f);       // the open-plan floor
        Ghost("Omar", 21.5f, 41.2f);        // its north row
        var vane = Ghost("Vane", 57.6f, 34.6f);   // (Echo only) in front of his own door
        Ghost("Tomas", 66.5f, 32f);         // inside Vane's office
        var damned = office.Find("KesslerConfrontation/Kessler (damned)");
        if (damned != null && vane != null) damned.position = vane.position;

        // the colleagues' name plates go where their desks are now
        foreach (var who in new[] { "Priya", "Ben", "Nadia", "Omar" })
        {
            var plate = office.Find("Desk sign - " + who);
            if (plate != null) Object.DestroyImmediate(plate.gameObject);
        }
        // the guard starts out on the open-plan floor (OfficeEye takes it from there)
        var eye = Root("EyeBalls");
        if (eye != null) eye.transform.position = P(14f, eye.transform.position.y, 28.5f);
        Physics.SyncTransforms();
    }

    static void Relight(string node, float height, float intensity, float range)
    {
        var n = office.Find(node);
        var have = n.Cast<Transform>().ToList();
        if (have.Count == 0) return;
        for (int i = 0; i < Rooms.Length; i++)
        {
            var t = i < have.Count ? have[i] : Object.Instantiate(have[i % 14 % have.Count].gameObject, n).transform;   // (the first 14: red/orange, every third flickers)
            t.name = "Light";
            t.position = P(Rooms[i].x, Floor + height, Rooms[i].y);
            var l = t.GetComponent<Light>();
            l.intensity = intensity; l.range = range;
        }
        for (int i = have.Count - 1; i >= Rooms.Length; i--) Object.DestroyImmediate(have[i].gameObject);
    }

    static void PutFuse(Transform fuse, Transform pickup, Vector3 on)
    {
        var b = fuse.GetComponent<Renderer>().bounds;
        fuse.position += new Vector3(on.x - b.center.x, on.y + .004f - b.min.y, on.z - b.center.z);
        pickup.position = fuse.GetComponent<Renderer>().bounds.center;
    }

    static void Bag(string name, Vector3 oldAt, Vector3 to)
    {
        var bag = Kid(name, "Body", oldAt);
        var blood = Kid(name + " blood", "BodyBlood", oldAt);
        if (bag != null) bag.position = new Vector3(to.x, bag.position.y, to.z);
        if (blood != null) blood.position = new Vector3(to.x, blood.position.y, to.z);
        var skin = bag != null ? bag.GetComponentInChildren<Renderer>() : null;
        if (skin == null) return;
        // solid: you step round it, not through it (OfficeLurkers switches it off while its bag falls)
        var solid = Node(bag, "Bag collider");
        solid.SetPositionAndRotation(skin.bounds.center, Quaternion.identity);
        var box = solid.GetComponent<BoxCollider>();
        if (box == null) box = solid.gameObject.AddComponent<BoxCollider>();
        Vector3 k = solid.lossyScale, size = skin.bounds.size * .85f;
        box.size = new Vector3(size.x / k.x, Mathf.Min(size.y, .45f) / k.y, size.z / k.z);
    }

    static Transform Ghost(string who, float x, float z)
    {
        var g = office.Find("Ghost - " + who);
        if (g == null) { log.AppendLine("MISSING ghost " + who); return null; }
        g.position = P(x, Floor, z);
        return g;
    }

    // ---------------------------------------------------------------- what the bigger floor has room for

    static void Extras()
    {
        Material metal = MatOf("Safe/Body"), black = MatOf("Safe/Door"), paper = MatOf("CleaningRota"), gold = MatOf("Safe/SiloKey");
        var dialog = Object.FindFirstObjectByType<DialogueBox>(FindObjectsInactive.Include);
        var fbNode = office.Find("FuseBox (breaker)");
        var fuse3 = Root("Fuse").transform.Find("Fuse3");
        var pick3 = fbNode.Find("FusePickup 3");
        var labelProto = office.Find("Breaker label");

        // ---- TOMAS'S LOCKER (store room): fuse 3 is padlocked in it; the key is a "souvenir" on the conference table
        var locker = Node(office, "Tomas's locker");
        if (ClearWall(locker, 19f, 18.6f, 4f, .78f, .58f, 0f, 1.95f, out var lw, out var lf, "locker"))
            locker.SetPositionAndRotation(lw - lf * .06f, Quaternion.LookRotation(lf));
        Box(locker, "Back", new Vector3(0f, .95f, -.02f), new Vector3(.7f, 1.9f, .03f), metal);
        Box(locker, "Left", new Vector3(-.335f, .95f, -.26f), new Vector3(.03f, 1.9f, .5f), metal);
        Box(locker, "Right", new Vector3(.335f, .95f, -.26f), new Vector3(.03f, 1.9f, .5f), metal);
        Box(locker, "Top", new Vector3(0f, 1.885f, -.26f), new Vector3(.7f, .03f, .5f), metal);
        Box(locker, "Bottom", new Vector3(0f, .015f, -.26f), new Vector3(.7f, .03f, .5f), metal);
        Box(locker, "Shelf", new Vector3(0f, 1.1f, -.26f), new Vector3(.64f, .02f, .46f), metal);
        var lockerDoor = Box(locker, "Door", new Vector3(0f, .95f, -.52f), new Vector3(.7f, 1.9f, .03f), black);
        Box(lockerDoor.transform, "Padlock", new Vector3(.36f, .02f, -1f), new Vector3(.09f, .045f, 1f), gold, false);
        if (labelProto != null)
        {
            var tag = locker.Find("Name");
            if (tag == null) { tag = Object.Instantiate(labelProto.gameObject, locker).transform; tag.name = "Name"; }
            tag.localPosition = new Vector3(0f, 1.62f, -.545f); tag.localRotation = Quaternion.identity; tag.localScale = Vector3.one;
            var tmp = tag.GetComponent<TextMeshPro>();
            tmp.text = "T. ROURKE"; tmp.fontSize = 1.1f; tmp.color = new Color(.85f, .8f, .6f);
        }
        PutFuse(fuse3, pick3, locker.TransformPoint(new Vector3(0f, 1.11f, -.26f)));
        pick3.gameObject.SetActive(false);   // until the padlock is off
        var take3 = pick3.GetComponent<Interactable>();
        if (take3 != null) take3.line = "A fuse, wrapped in a rag in his locker. Still warm.";
        var open = Inter(lockerDoor, "Open the locker", "The padlock drops.|His overalls. His radio. And a fuse, wrapped in a rag.", req: "LockerKey",
            locked: "T. ROURKE. Padlocked.|Through the vent slots: glass and brass. A fuse.|Somebody kept his key. Vane handed out 'souvenirs' in the boardroom.", hide: true);
        UnityEventTools.AddBoolPersistentListener(open.onUse, pick3.gameObject.SetActive, true);

        // his cleaning rota on the wall by it
        Physics.SyncTransforms();
        var rota = office.Find("CleaningRota");
        var rotaGlow = Kid("Rota glow", "Light", rota.position);
        if (ClearWall(rota, 20.5f, 19f, 4f, .6f, .08f, 1.25f, 1.95f, out var rw, out var rf, "rota"))
            Move(rota, rw + Vector3.up * 1.6f - rf * .02f, Quaternion.LookRotation(rf), rotaGlow);

        var key = Box(office, "LockerKey", Vector3.zero, new Vector3(.05f, .015f, .12f), gold);
        key.transform.position = Desk(27.5f, 58f, "locker key", .12f, key.transform) + Vector3.up * .01f;
        key.GetComponent<BoxCollider>().size = new Vector3(6f, 20f, 3f);   // easy to aim at a tiny key
        Inter(key, "Take the key", "A padlock key on a lanyard: T. ROURKE.|Left on the boardroom table like a paperweight.", give: "LockerKey", hide: true);
        Glow(key.transform, new Vector3(0f, 20f, 0f), new Color(1f, .7f, .2f));

        // ---- SECURITY PASS: the CCTV terminal does not wake with the fuses; it wants a pass, and the pass is on
        // Vane's desk, behind his door
        var cctv = office.Find("CCTV");
        var seq = cctv.GetComponent<SequencePuzzle>();
        seq.noPowerLine = "Security terminal. LOGIN REQUIRED.|There's a card reader beside the keys.";
        EditorUtility.SetDirty(seq);
        var counter = fbNode.Find("BreakerUse").GetComponent<PuzzleCounter>();
        for (int i = counter.onSolved.GetPersistentEventCount() - 1; i >= 0; i--)
            if (counter.onSolved.GetPersistentTarget(i) == seq) UnityEventTools.RemovePersistentListener(counter.onSolved, i);
        var reader = Box(cctv, "Card reader", new Vector3(.72f, .84f, -.66f), new Vector3(.1f, .05f, .14f), black);
        reader.GetComponent<BoxCollider>().size = new Vector3(2f, 5f, 2f);
        var swipe = Inter(reader, "Swipe a security pass", "*beep*  ACCESS GRANTED: VANE.", req: "SecurityPass", consume: false,
            locked: "A card reader. The light on it is red.|Vane's pass opened every door in the building. It never left his office.");
        UnityEventTools.AddBoolPersistentListener(swipe.onUse, seq.SetPowered, true);
        Glow(reader.transform, new Vector3(0f, 3f, 0f), new Color(1f, .15f, .1f));

        var pass = Box(office, "SecurityPass", Vector3.zero, new Vector3(.09f, .008f, .06f), paper);
        pass.transform.position = Desk(69.3f, 35.7f, "security pass", .12f, pass.transform) + Vector3.up * .006f;
        pass.GetComponent<BoxCollider>().size = new Vector3(4f, 40f, 6f);
        Inter(pass, "Take the pass", "A security pass on a burnt lanyard.|VANE - ALL AREAS. The photo has melted off.", give: "SecurityPass", hide: true);
        Glow(pass.transform, new Vector3(0f, 40f, 0f), new Color(1f, .7f, .2f));

        var shutter = office.Find("Loading shutter");   // (the last office's loading dock: this one has none)
        if (shutter != null) Object.DestroyImmediate(shutter.gameObject);

        // ---- the floor plan's LOCKED DOORS off the lobby. The store room opens with the key reception keeps...
        var stores = Leaf("D06");
        if (stores != null)
        {
            stores.gameObject.SetActive(true);
            var sk = Box(office, "StoreKey", Vector3.zero, new Vector3(.05f, .015f, .12f), gold);
            sk.transform.position = Desk(-9.6f, 25f, "store room key", .12f, sk.transform) + Vector3.up * .01f;
            sk.GetComponent<BoxCollider>().size = new Vector3(6f, 20f, 3f);
            Inter(sk, "Take the key", "A key on a red tag: STORES.|Reception kept the spares. Nobody signed this one out.", give: "StoreKey", hide: true);
            Glow(sk.transform, new Vector3(0f, 20f, 0f), new Color(1f, .7f, .2f));
            Inter(stores.gameObject, "Unlock the door", "The key turns.|The store room. The cleaner's.", req: "StoreKey",
                locked: "STORES - STAFF ONLY. Locked.|Reception keeps the spare keys.", hide: true);
        }
        // ...the open-plan floor is bolted on its far side: a way back to the lobby once you are through
        var bolted = Leaf("D05");
        if (bolted != null)
        {
            bolted.gameObject.SetActive(true);
            var lb = bolted.GetComponent<Renderer>().bounds;
            var lobbySide = Box(office, "Open-plan door (lobby side)", lb.center + Vector3.back * .14f, new Vector3(lb.size.x, 2.2f, .08f), black);
            var farSide = Box(office, "Open-plan door (bolt)", lb.center + Vector3.forward * .14f, new Vector3(lb.size.x, 2.2f, .08f), black);
            lobbySide.GetComponent<Renderer>().enabled = farSide.GetComponent<Renderer>().enabled = false;
            Inter(lobbySide, "Try the door", "It gives a finger's width and stops.|Bolted on the other side.", once: false);
            var slide = Inter(farSide, "Slide the bolt", "The bolt grinds back.|The lobby, and the way out.", hide: true);
            UnityEventTools.AddBoolPersistentListener(slide.onUse, bolted.gameObject.SetActive, false);
            UnityEventTools.AddBoolPersistentListener(slide.onUse, lobbySide.SetActive, false);
        }
        var closet = Leaf("D13");
        if (closet != null) Inter(closet.gameObject, "Try the door", "PLANT ROOM. Locked, and the lock has run in the heat.", once: false);

        // ---- THE DAMNED
        var node = Node(office, "Damned spawners");
        var od = node.GetComponent<OfficeDamned>();
        if (od == null) od = node.gameObject.AddComponent<OfficeDamned>();
        od.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DamnedPrefab);
        var fpc = Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
        od.player = fpc != null ? fpc.transform : null;
        od.points = new OfficeDamned.Point[DamnedPoints.Length];
        for (int i = 0; i < DamnedPoints.Length; i++)
        {
            var p = Node(node, "Spawn " + (i + 1) + " (stage " + DamnedPoints[i].stage + ")");
            p.position = P(DamnedPoints[i].x, Floor, DamnedPoints[i].z);
            od.points[i] = new OfficeDamned.Point { at = p, stage = DamnedPoints[i].stage };
        }
        var blocker = office.Find("KesslerDoor/DoorBlocker");
        if (blocker != null)
        {
            od.doorBlocker = blocker.GetComponent<Collider>();
            var carve = blocker.GetComponent<NavMeshObstacle>();
            if (carve == null) carve = blocker.gameObject.AddComponent<NavMeshObstacle>();
            carve.shape = NavMeshObstacleShape.Box; carve.carving = true;
            carve.size = new Vector3(2.3f, 2.6f, .5f); carve.center = Vector3.zero;
            od.doorCarve = carve;
        }
        EditorUtility.SetDirty(od);
        if (dialog == null) log.AppendLine("no DialogueBox in the scene");
    }

    // the office's own navmesh (floor level only: not the ceiling tops), for the Damned and the guard. The model
    // is turned on its side as it comes out of the file, so the surface sits on a node of its own. The doors that
    // open in play are left out of the bake and cut their doorway out while they are shut.
    static void BakeNavMesh()
    {
        var gates = new[] { Leaf("D05"), Leaf("D06") }.Where(l => l != null && l.gameObject.activeSelf).ToList();
        foreach (var g in gates) g.gameObject.SetActive(false);
        var sources = new List<NavMeshBuildSource>();
        UnityEngine.AI.NavMeshBuilder.CollectSources(model, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
        foreach (var g in gates)
        {
            g.gameObject.SetActive(true);
            var carve = g.GetComponent<NavMeshObstacle>();
            if (carve == null) carve = g.gameObject.AddComponent<NavMeshObstacle>();
            var mb = g.GetComponent<MeshFilter>().sharedMesh.bounds;
            carve.shape = NavMeshObstacleShape.Box; carve.carving = true;
            carve.center = mb.center; carve.size = Vector3.Max(mb.size, Vector3.one * .5f);
        }
        var b = ModelBounds();
        var slab = new Bounds(new Vector3(b.center.x, Floor + .9f, b.center.z), new Vector3(b.size.x + 2f, 2.6f, b.size.z + 2f));
        var data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources, slab, Vector3.zero, Quaternion.identity);
        if (data == null) { log.AppendLine("NAVMESH bake failed"); return; }
        if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Generated", "Office");
        const string path = GenDir + "/OfficeNavMesh.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(data, path);
        var node = Node(office, "Office navmesh");
        node.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var surface = node.GetComponent<NavMeshSurface>();
        if (surface == null) surface = node.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.RemoveData();
        surface.navMeshData = data;
        surface.AddData();
        EditorUtility.SetDirty(surface);
        log.AppendLine("navmesh: " + sources.Count + " sources");
    }
}
