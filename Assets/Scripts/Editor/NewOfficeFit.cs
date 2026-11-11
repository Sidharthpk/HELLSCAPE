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

// The office act in the user's v3 office model ("Office Ground Floor", ~60 x 44 m; OfficeV3.cs puts it in the
// scene). Nothing is rebuilt: every puzzle, witness and prop of OfficeAct/Office is MOVED into the rooms with its
// wiring intact. Safe to run again after editing the tables below: every placement is absolute. Things the model
// has (desks, Vane's safe, the pantry tables) are found by name: At("Vane desk").
//
// Coordinates in this file are PLAN coordinates: metres east and north of the front door (P() converts).
//
// (OfficeV3.V3Rooms has every room's rectangle, measured off the model's floors)
//   entrance lobby  e -8..8    n 0..6     arrival, the way out (the front doors), the fuse box (east wall)
//   reception       e -8..8    n 10..15   your name plate, Derek's keyboard (fuse 2), Priya
//   main hall       e -21..21  n 6..15    round reception; the lounge off its west end
//   open office     e -21..21  n 15..30   the hub: your desk (OfficeDocumentBuilder: calendar, report, note), Nadia, Omar, the guard
//   east column     e 21..30   Kessler's armory n 7..13 (vault-dial door off Kessler's office: the pistol; the
//                              colleagues have the numbers), Kessler's office n 13..21, Vane's office n 21..28 (his
//                              door + keypad off the open office: the safe, the ledger, the security pass), maintenance
//                              n 32..38 (Tomas's locker with fuse 3, his rota, a body bag)
//   boardroom       e -30..-21 n 15..30   the locker key
//   pantry          e -13..3   n 30..36   the cake (fuse 1), Ben
//   security        e 14..22   n 30..38   the CCTV terminal (wants the pass)
//   loading         e -13..4   n 36..44   the silo door closes its widest bay
public static partial class NewOfficeFit
{
    public const string ModelName = "Office Ground Floor";
    const string OldModelName = "Sketchfab_2023_11_11_16_15_44";
    const string DoorModel = "4bfaca4c4a794e9393a413e88dd5611a.fbx";
    const string GenDir = "Assets/Generated/Office";
    const float Floor = 3.4f;

    static Transform model, act, office;
    static readonly System.Text.StringBuilder log = new System.Text.StringBuilder();

    // one lamp of each kind over every room and stretch of corridor
    static readonly Vector2[] Rooms =
    {
        new Vector2(0f, 3f), new Vector2(0f, 12.6f), new Vector2(-12f, 8.5f), new Vector2(12f, 8.5f),       // lobby, reception, main hall
        new Vector2(-24f, 11f),                                                                             // lounge
        new Vector2(-10f, 18f), new Vector2(0f, 18f), new Vector2(10f, 18f),                                // open office
        new Vector2(-10f, 26f), new Vector2(0f, 26f), new Vector2(10f, 26f), new Vector2(19f, 22f),
        new Vector2(27f, 10f), new Vector2(27f, 17f), new Vector2(27f, 24.6f), new Vector2(27f, 29.7f),     // armory, Kessler, Vane, west corridor
        new Vector2(27f, 35f),                                                                              // maintenance
        new Vector2(-24f, 19f), new Vector2(-24f, 26f),                                                     // boardroom
        new Vector2(18.8f, 34f), new Vector2(12f, 32.5f),                                                   // security, archive
        new Vector2(-8f, 33f), new Vector2(1f, 33f),                                                        // pantry
        new Vector2(-6f, 40.5f), new Vector2(8f, 39f),                                                      // loading
    };

    [MenuItem("HellScape/Office/Fit To New Office")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Stop Play first."); return; }
        var a = GameObject.Find("OfficeAct");
        if (a == null || a.transform.Find("Office") == null) { Debug.LogError("Needs OfficeAct/Office in the open scene."); return; }
        log.Clear();
        Undo.RegisterFullObjectHierarchyUndo(a, "Fit office");
        act = a.transform; office = act.Find("Office");
        model = InstallV3();
        var m = model.gameObject;

        // the walls are one-sided: without this a probe from behind goes straight through
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        try
        {
            PlaceModel();
            EmergencySpots();
            Physics.SyncTransforms();
            BakeNavMesh();   // (ClearWall wants a walk map of this floor)
            Layout();
            Extras();
            if (AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/OfficeDocuments/AprilCalendar.png") != null)
                OfficeDocumentBuilder.Build();
            Physics.SyncTransforms();
            Plates();
            BakeNavMesh();   // (the document builder saves and refreshes: that drops the walk map Props checks against)
            Props();
            BakeNavMesh();
            Reach();
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
        if (EditorApplication.isPlaying || m == null || !IsOffice(m.transform) || a == null || a.transform.Find("Office") == null) { Debug.LogError("Needs '" + ModelName + "' and OfficeAct/Office in the open scene, out of Play."); return; }
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
        if (m == null || !IsOffice(m.transform) || a == null || a.transform.Find("Office") == null) { Debug.LogError("Needs '" + ModelName + "' and OfficeAct/Office in the open scene."); return; }
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
            if (h.collider.transform.IsChildOf(model) && h.collider.name != "Chair" && !h.collider.transform.parent.name.StartsWith("Seals")) { hit = h; return true; }   // (a chair is neither a wall nor a table top; the invisible walls outside are neither)
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
        // ---- ENTRANCE LOBBY: you come in by the front doors and look up the lobby at reception
        var arrival = act.Find("OfficeArrival");
        arrival.SetPositionAndRotation(P(0f, Floor + 1.1f, 2.6f), Quaternion.identity);
        // the way out is the front doors themselves: the old door slab is only what you click on
        var exit = office.Find("ExitDoor");
        float exitWidth = 4.3f;
        var exitWall = Wall(-6f, 1.25f, 2.6f, Vector3.back, "exit door");
        if (Opening(P(-7.5f, Floor + 1.2f, 2.6f), Vector3.back, Vector3.right, 15f, out var fd, out float fw, out _)) { exitWall = fd; exitWidth = fw + .3f; }
        else log.AppendLine("NO DOORWAY at the front: the exit is on the wall");
        Move(exit, new Vector3(exitWall.x, Floor + 1.25f, exitWall.z) + Vector3.forward * .07f, Quaternion.LookRotation(Vector3.back));
        exit.localScale = new Vector3(exitWidth, 2.5f, .1f);
        exit.GetComponent<Renderer>().enabled = false;

        Relight("EmergencyLights", 2.7f, 3.2f, 11f);
        Relight("PowerLights", 2.85f, 7f, 13f);

        // ---- FUSE BOX: on the lobby's east wall, same way round as ever (you face +x to use it)
        var fuseRoot = Root("Fuse").transform;
        var fbNode = office.Find("FuseBox (breaker)");
        var fb = fbNode.GetComponent<FuseBox>();
        var breaker = fbNode.Find("BreakerUse");
        var loose = new[] { fuseRoot.Find("Fuse1"), fuseRoot.Find("Fuse2"), fuseRoot.Find("Fuse3") };
        var picks = new[] { fbNode.Find("FusePickup 1"), fbNode.Find("FusePickup 2"), fbNode.Find("FusePickup 3") };
        var at = loose.Select(f => f.position).ToArray();   // the loose fuses are the box's children, but lie elsewhere
        var box = fuseRoot.Find("PowerBox").GetComponent<Renderer>().bounds;
        var boxWall = Wall(5f, 1.54f, 3.6f, Vector3.right, "fuse box");
        var by = new Vector3(boxWall.x - .005f - box.max.x, 0f, boxWall.z - box.center.z);
        Shove(by, fuseRoot, breaker, Kid("Breaker label", "Label", breaker.position), Kid("Breaker glow", "Light", breaker.position));
        fb.seatedPos += by;
        var boxT = fuseRoot.Find("PowerBox");
        if (boxT.GetComponent<Collider>() == null) boxT.gameObject.AddComponent<BoxCollider>();
        EditorUtility.SetDirty(fb);
        for (int i = 0; i < 3; i++) loose[i].position = at[i];

        // ---- PANTRY (the kitchen): the cake with fuse 1 in it on a table (the calendar is at your desk: OfficeDocumentBuilder)
        var cake = Root("Cake Out Sub").transform;
        var cb = cake.GetComponentInChildren<Renderer>().bounds;
        foreach (var r in cake.GetComponentsInChildren<Renderer>()) cb.Encapsulate(r.bounds);
        var pt = At("Pantry table 1");
        var table = Desk(pt.x, pt.y, "cake", .26f);
        Shove(new Vector3(table.x - cb.center.x, table.y - cb.min.y, table.z - cb.center.z), cake, loose[0]);
        picks[0].position = loose[0].GetComponent<Renderer>().bounds.center;

        // ---- RECEPTION: your name plate on one desk, Derek's keyboard (fuse 2) on the other
        var sign = office.Find("Your desk sign");
        if (sign != null)
        {
            var sb = sign.GetComponent<Renderer>().bounds;
            var rd = At("Reception desk 27.5");
            var on = Desk(rd.x, rd.y, "your name plate", Mathf.Max(sb.extents.x, sb.extents.z) + .03f, sign);
            sign.position += new Vector3(on.x - sb.center.x, on.y + .004f - sb.min.y, on.z - sb.center.z);
            Physics.SyncTransforms();
        }
        var dd = At("Reception desk 31");
        PutFuse(loose[1], picks[1], Desk(dd.x, dd.y, "Derek's desk", .08f));

        // ---- MAINTENANCE (the cleaner's room): a body bag on the floor (his locker and rota are in Extras)
        var crate = office.Find("Crate");
        if (crate != null) crate.gameObject.SetActive(false);   // fuse 3 sat on it; it's in the locker now
        Bag("Body bag (store room)", default, FreeFloor(26f, 34f, .9f, "maintenance bag"));   // (the name is older than the room)
        // ...and the other in the boardroom (OfficeLurkers drops the westmost one off the ceiling)
        Bag("Body bag (boardroom)", default, FreeFloor(-21f, 27f, .9f, "boardroom bag"));
        var pool = office.Find("BloodPool");
        if (pool != null) pool.position = P(3f, pool.position.y, 8f);

        // ---- VANE'S DOOR is the doorway of his office, off the west corridor: the door model stands in it
        Vector3 doorAt = P(22.8f, 0f, 25.5f); float doorway = 1.2f;
        if (Opening(P(19.5f, Floor + 1.2f, 21.6f), Vector3.right, Vector3.forward, 6f, out var dc, out float dw, out _))
        { doorAt = dc + Vector3.right * .12f; doorway = dw; }   // (the middle of the wall: it is .24 thick)
        else log.AppendLine("NO DOORWAY for Vane's door");
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
        // the door model's own wall panels were cut for the last office: what reaches past this wall's ends would
        // stand free in the corridor (this office's wall has its own doorway)
        Physics.SyncTransforms();
        foreach (var r in doorRoot.GetComponentsInChildren<Renderer>(true))
            if (r.transform != frameT && r.bounds.size.y > 2.6f && r.bounds.size.z > doorway + 3f) r.gameObject.SetActive(false);
        log.AppendLine("Vane's door: doorway " + doorway.ToString("F2") + " at " + doorAt);
        var pad = office.Find("Keypad");
        var padWall = Wall(19.5f, 1.65f, doorAt.z - DoorZ + Mathf.Max(1.3f, doorway * .5f + .45f), Vector3.right, "keypad");
        pad.SetPositionAndRotation(padWall + Vector3.left * .03f, doorRot);

        // ---- VANE'S OFFICE, behind the door: the safe where his own stood, and on his desk the second set of books
        var safe = office.Find("Safe");
        var ms = At("Vane safe");
        foreach (Transform c in model) if (c.name.StartsWith("Vane safe")) c.gameObject.SetActive(false);
        Physics.SyncTransforms();
        if (ClearWall(safe, ms.x, ms.y, 3f, 1.05f, .8f, 0f, 1.05f, out var sw0, out var sf0, "safe"))
            Move(safe, sw0 - sf0 * .46f, Quaternion.LookRotation(sf0));
        Physics.SyncTransforms();
        var ledger = office.Find("KesslerLedger");
        var vd = At("Vane desk");
        ledger.position = Desk(vd.x, vd.y, "ledger", .26f, ledger) + Vector3.up * .03f;
        var ledgerLamp = office.Find("Ledger lamp");
        if (ledgerLamp != null) ledgerLamp.position = ledger.position + Vector3.up * .8f;
        Physics.SyncTransforms();

        // ---- SECURITY / SERVER ROOM: the CCTV terminal against a wall
        var cctv = office.Find("CCTV");
        if (ClearWall(cctv, 19f, 35f, 4f, 1.7f, .95f, 0f, 1.95f, out var cw, out var cf, "CCTV terminal", 1.2f))
            Move(cctv, cw - cf * .1f, Quaternion.LookRotation(cf));
        else Move(cctv, P(19f, Floor, 35f), Quaternion.LookRotation(Vector3.right));
        Physics.SyncTransforms();

        // ---- the SILO DOOR closes the widest bay of the loading dock (the north wall: past it the model just ends)
        var silo = office.Find("SiloDoor");
        if (Opening(P(-13f, Floor + 1.2f, 43.3f), Vector3.forward, Vector3.right, 16f, out var so, out float sw, out float sh))
        {
            var label = Kid("Silo label", "Label", silo.position);
            sh = Mathf.Min(sh, 2.8f);
            silo.SetPositionAndRotation(so + Vector3.up * sh * .5f + Vector3.forward * .05f, Quaternion.LookRotation(Vector3.forward));
            silo.localScale = new Vector3(sw + .06f, sh, .1f);
            if (label != null) label.SetPositionAndRotation(so + Vector3.up * (sh + .28f) + Vector3.back * .03f, Quaternion.LookRotation(Vector3.forward));
            log.AppendLine("silo door: doorway " + sw.ToString("F2") + " x " + sh.ToString("F2") + " at " + so);
        }
        else log.AppendLine("NO DOORWAY for the silo door");

        // ---- the dead: where each one worked
        var chair = At("Reception chair 31");
        Ghost("Priya", FreeFloor(chair.x + .9f, chair.y + .4f, .35f, "Priya"));   // front desk: beside her chair
        Ghost("Ben", FreeFloor(-2.5f, 34.5f, .35f, "Ben"));                          // the pantry, with the cake
        Ghost("Nadia", FreeFloor(-7f, 19f, .35f, "Nadia"));                          // the open-plan floor, west bank
        Ghost("Omar", FreeFloor(8.5f, 26.5f, .35f, "Omar"));                         // its east bank
        var vane = Ghost("Vane", FreeFloor(28f, 26.5f, .35f, "Vane"));               // inside his own office
        var tomasGhost = Object.FindObjectsByType<GhostNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(g => g.ghostName == "Tomas");
        var tomas = tomasGhost != null ? tomasGhost.transform : null;
        if (tomas != null)
        {
            var siloRoot = act.Find("SiloComplex");
            if (siloRoot != null) tomas.SetParent(siloRoot, true);
            tomas.SetPositionAndRotation(new Vector3(918f, 90.19f, 1049f), Quaternion.identity);
        }
        if (vane != null) vane.GetComponent<GhostNPC>().driftRadius = 0f;
        if (tomas != null) tomas.GetComponent<GhostNPC>().driftRadius = 0f;
        var damned = office.Find("KesslerConfrontation/Kessler (damned)");
        if (damned != null && vane != null) damned.position = vane.position;

        // the colleagues' name plates (OfficeColleagues starts its scare at them) onto a desk near each one
        foreach (var who in new[] { "Priya", "Ben", "Nadia", "Omar" })
        {
            var plate = office.Find("Desk sign - " + who);
            var g = office.Find("Ghost - " + who);
            if (plate == null || g == null) continue;
            var pb = plate.GetComponent<Renderer>().bounds;
            var on = Desk(g.position.x - DoorX, g.position.z - DoorZ, who + "'s name plate", Mathf.Max(pb.extents.x, pb.extents.z) + .03f, plate, 6f);
            plate.position += new Vector3(on.x - pb.center.x, on.y + .004f - pb.min.y, on.z - pb.center.z);
            Physics.SyncTransforms();
        }
        // the guard starts out on the open-plan floor (OfficeEye takes it from there)
        var eye = Root("EyeBalls");
        if (eye != null) eye.transform.position = P(0f, eye.transform.position.y, 27.5f);
        Physics.SyncTransforms();
    }

    // a bare bit of floor near a point, `half` each way with nothing of the model or the act on it
    static Vector3 FreeFloor(float e, float n, float half, string what, float reach = 3f)
    {
        for (float r = 0f; r <= reach; r += .25f)
        {
            int steps = Mathf.Max(1, Mathf.RoundToInt(r * 25f));
            for (int k = 0; k < steps; k++)
            {
                float a = k * Mathf.PI * 2f / steps;
                var p = P(e + Mathf.Cos(a) * r, Floor, n + Mathf.Sin(a) * r);
                if (Mathf.Abs(Top(p).y - Floor) > .05f) continue;
                if (!Blocked(p + Vector3.up * 1f, new Vector3(half, .9f, half), Quaternion.identity, null)) return p;
            }
        }
        log.AppendLine("NO FLOOR for " + what);
        return P(e, Floor, n);
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

    static Transform Ghost(string who, Vector3 at)
    {
        var g = office.Find("Ghost - " + who);
        if (g == null) { log.AppendLine("MISSING ghost " + who); return null; }
        g.position = new Vector3(at.x, Floor, at.z);
        return g;
    }

    // ---------------------------------------------------------------- the locker, the keys, the pass, the locked door

    static void Extras()
    {
        Material metal = MatOf("Safe/Body"), black = MatOf("Safe/Door"), paper = MatOf("CleaningRota"), gold = MatOf("Safe/SiloKey");
        var dialog = Object.FindFirstObjectByType<DialogueBox>(FindObjectsInactive.Include);
        var fbNode = office.Find("FuseBox (breaker)");
        var fuse3 = Root("Fuse").transform.Find("Fuse3");
        var pick3 = fbNode.Find("FusePickup 3");
        var labelProto = office.Find("Breaker label");

        // ---- TOMAS'S LOCKER (maintenance, the cleaner's room): fuse 3 is padlocked in it; the key is a "souvenir" on
        // the boardroom table
        var locker = Node(office, "Tomas's locker");
        if (ClearWall(locker, 25f, 35f, 3.5f, .78f, .58f, 0f, 1.95f, out var lw, out var lf, "locker"))
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
        if (ClearWall(rota, 27f, 35f, 3.5f, .6f, .08f, 1.25f, 1.95f, out var rw, out var rf, "rota"))
            Move(rota, rw + Vector3.up * 1.6f - rf * .02f, Quaternion.LookRotation(rf), rotaGlow);

        var key = Box(office, "LockerKey", Vector3.zero, new Vector3(.05f, .015f, .12f), gold);
        var ct = At("Extended blue conference table");
        key.transform.position = Desk(ct.x, ct.y, "locker key", .12f, key.transform, 2.5f) + Vector3.up * .01f;
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
        var vd = At("Vane desk");
        pass.transform.position = Desk(vd.x, vd.y, "security pass", .12f, pass.transform) + Vector3.up * .006f;
        pass.GetComponent<BoxCollider>().size = new Vector3(4f, 40f, 6f);
        Inter(pass, "Take the pass", "A security pass on a burnt lanyard.|VANE - ALL AREAS. The photo has melted off.", give: "SecurityPass", hide: true);
        Glow(pass.transform, new Vector3(0f, 40f, 0f), new Color(1f, .7f, .2f));

        foreach (var gone in new[] { "Loading shutter", "Open-plan door (lobby side)", "Open-plan door (bolt)" })   // (the last offices' doors)
        {
            var t = office.Find(gone);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        // ---- KESSLER'S ARMORY, the secure room off his office: a bank-vault dial on the door. He gave each colleague
        // one number so nobody could open it alone (OfficeWitness: Priya 31, Ben 7, Nadia 18; Omar knows the order).
        foreach (var gone in new[] { "StoreKey", "Store room door" })   // (the store room it used to be)
        {
            var t = office.Find(gone);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        if (Opening(P(24.3f, Floor + 1.5f, 14.2f), Vector3.back, Vector3.right, 6f, out var sc, out float sdw, out float sdh))
        {
            sdh = Mathf.Min(sdh, 2.3f);
            var door = Box(office, "Armory door", Vector3.zero, new Vector3(sdw + .04f, sdh, .06f), black);
            door.transform.position = sc + Vector3.up * sdh * .5f + Vector3.back * .1f;
            var dial = door.GetComponent<VaultDial>();
            if (dial == null) dial = door.AddComponent<VaultDial>();
            dial.code = new[] { 18, 7, 31 };
            dial.lockedText = "KESSLER - ARMORY. A vault dial, three numbers.";
            dial.openText = "The bolts slide back.|Kessler's armory. One pistol left on the rack.";
            while (dial.onSolved.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(dial.onSolved, 0);
            UnityEventTools.AddBoolPersistentListener(dial.onSolved, door.SetActive, false);
            var turn = Inter(door, "Turn the dial", once: false);
            UnityEventTools.AddVoidPersistentListener(turn.onUse, dial.Use);
            var carve = door.GetComponent<NavMeshObstacle>();
            if (carve == null) carve = door.AddComponent<NavMeshObstacle>();
            carve.shape = NavMeshObstacleShape.Box; carve.carving = true; carve.center = Vector3.zero; carve.size = Vector3.one;
            log.AppendLine("armory door: " + sdw.ToString("F2") + " x " + sdh.ToString("F2") + " at " + sc);
        }
        else log.AppendLine("NO DOORWAY for the armory door");

        // the pistol, lying on the middle safe: taking it is taking the gun (GunController.Equip)
        var pistol = Node(office, "Armory pistol");
        foreach (var c in pistol.Cast<Transform>().ToList()) Object.DestroyImmediate(c.gameObject);
        var safeTop = Top(Of("Secure safe 1").center);
        pistol.SetPositionAndRotation(safeTop + Vector3.up * .02f, Quaternion.Euler(0f, 90f, 90f));
        PistolBuilder.Prop(pistol, "Beretta");
        var grab = pistol.GetComponent<BoxCollider>();
        if (grab == null) grab = pistol.gameObject.AddComponent<BoxCollider>();
        grab.center = Vector3.zero; grab.size = Vector3.one * .5f;   // easy to aim at
        var take = Inter(pistol.gameObject, "Take the pistol", "A Beretta. Loaded.|[R] reload   [I] inspect", hide: true);
        var gunCtl = Object.FindObjectsByType<GunController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (gunCtl != null) UnityEventTools.AddVoidPersistentListener(take.onUse, gunCtl.Equip);
        else log.AppendLine("no GunController: the armory pistol equips nothing");
        Glow(pistol, new Vector3(0f, .3f, 0f), new Color(1f, .7f, .2f));
        log.AppendLine("armory pistol on the safe at " + pistol.position);

        // ---- Vane's door cuts the navmesh while it is shut (no Damned in the office any more: OfficeDamned is only this)
        var node = Node(office, "Damned spawners");
        var od = node.GetComponent<OfficeDamned>();
        if (od == null) od = node.gameObject.AddComponent<OfficeDamned>();
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
        var settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = Mathf.Min(settings.agentRadius, .3f);   // (the v3 office's doors are 1 m: a 0.5 m agent fits none of them; the player is 0.3)
        var data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings, sources, slab, Vector3.zero, Quaternion.identity);
        if (data == null) { log.AppendLine("NAVMESH bake failed"); return; }
        if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Generated", "Office");
        const string path = GenDir + "/OfficeNavMesh.asset";
        var node = Node(office, "Office navmesh");
        node.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var surface = node.GetComponent<NavMeshSurface>();
        if (surface == null) surface = node.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.RemoveData();   // (before the old asset goes: a surface still holding deleted data adds nothing new)
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(data, path);
        surface.navMeshData = data;
        surface.AddData();
        EditorUtility.SetDirty(surface);
        log.AppendLine("navmesh: " + sources.Count + " sources");
    }
}
