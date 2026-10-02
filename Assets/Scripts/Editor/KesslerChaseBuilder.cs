using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

// HellScape > Office > Kessler Tunnel Chase (user, 2026-10-02): the way from the office to the silo is Kessler's chase.
//   office    no fight: he drops the silo key as he turns, and you run for the silo door (KesslerConfrontation)
//   tunnel    the silo door lets you out at the far end of a service tunnel, laid out here from the user's tunnel
//             piece (scene root "tunnel": a 111 degree bend) - several end to end, bending right then left, the
//             last one's mouth on the north end of the user's metal footbridge ("bridge_Metal"). The black "Tunnel 1" beside it was
//             only the user's sketch of the route: both it and the loose piece are switched off, not deleted.
//             Bodies hang from the roof and swing as you push through (HangingBody); the tunnel's own door is what
//             Kessler breaks down (KesslerChase.DoorBurst), and he runs with the user's RUNKESSLER clip
//   bridge    walled in (a concrete chamber over the blood), so nothing of the rest of the map shows from it.
//             Tomas holds him (BridgeReached); the door at the far end ("door2_SM2 (1)") opens for you and slams
//             behind you; he beats on it until you open the next door on ("Entrance Door"), where Elias is waiting
// Everything it makes sits under OfficeAct/KesslerTunnel and is rebuilt each run - except that the tunnel pieces, the
// plates over their joints, the end wall and the tunnel door stay wherever you've moved them to by hand (the bodies,
// lamps and route marks are laid along the pieces as they stand). MainGameScene open.
public static class KesslerChaseBuilder
{
    const string RootName = "KesslerTunnel";
    const string RunFbx = "Assets/Enemies/RUNKESSLER.fbx";
    const string AnimDir = "Assets/Materials/OfficeAct/Anim";
    const string WalkerController = "Assets/Scenes/Controllers/ZombieWalkerController.controller";
    const string BodyFbx = "Assets/Enemies/body-bag/source/Body.fbx";
    const string BloodMat = "Assets/Materials/OfficeAct/BloodDrip.mat";
    const int Pieces = 4;                   // about 36 m each (user: "make the tunnel longer": it was 2)
    const float HookHeight = 5.7f, BagLength = 1.9f, BagBottom = 0.75f;   // (the duct along the roof is 5.8 m up)

    [MenuItem("HellScape/Office/Kessler Tunnel Chase")]
    public static void Menu() => Debug.Log(Build());

    // one copy of the tunnel piece: where its root goes, and which way round it's walked
    class Piece { public Vector3 root; public float yaw, near, far; public Transform t; }

    public static string Build()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var kc = Object.FindFirstObjectByType<KesslerConfrontation>(FindObjectsInactive.Include);
        var cuts = Object.FindFirstObjectByType<KillerCutscenes>(FindObjectsInactive.Include);
        if (kc == null || cuts == null) return "No office act here (run HellScape > Build Office Act first).";
        var scene = kc.gameObject.scene;
        var roots = scene.GetRootGameObjects();
        var tunnelSrc = roots.FirstOrDefault(g => g.name == "tunnel");
        var blackRef = roots.FirstOrDefault(g => g.name == "Tunnel 1");
        var bridge = roots.FirstOrDefault(g => g.name == "bridge_Metal");
        var complex = roots.FirstOrDefault(g => g.name == "BloodPool_low");
        if (tunnelSrc == null || bridge == null || complex == null) return "Needs the scene roots 'tunnel', 'bridge_Metal' and 'BloodPool_low'.";
        var siloDoorObj = complex.transform.Find("door2_SM2 (1)");
        var entranceDoor = complex.transform.Find("Entrance Door");
        if (siloDoorObj == null || entranceDoor == null) return "Needs BloodPool_low/'door2_SM2 (1)' (the door at the end of the footbridge) and 'Entrance Door'.";
        Transform officeAct = kc.transform.parent.parent, office = kc.transform.parent;
        var teleporter = Object.FindObjectsByType<Teleporter>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "SiloDoor");
        var complexCp = Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(k => k.name.Contains("Silo complex"));
        var eliasChase = Object.FindFirstObjectByType<ChaseKiller>(FindObjectsInactive.Include);
        Transform player = cuts.player;

        // what the user has since moved by hand stays where they put it ("made a few changes to the placement of
        // the tunnel to make it look continuous")
        var kept = new Dictionary<string, (Vector3 pos, Quaternion rot, Vector3 scale)>();
        var old = officeAct.Find(RootName);
        if (old != null)
        {
            if (old.Cast<Transform>().Count(t => t.name.StartsWith("Tunnel piece")) == Pieces)
                foreach (Transform t in old)
                    if (t.name.StartsWith("Tunnel piece") || t.name == "Joint plate" || t.name == "Doorway" || t.name == "Tunnel door") kept[Key(t)] = (t.position, t.rotation, t.localScale);
            Object.DestroyImmediate(old.gameObject);
        }
        Transform root = new GameObject(RootName).transform;
        root.SetParent(officeAct, false);
        void Restore()
        {
            foreach (Transform t in root)
                if (kept.TryGetValue(Key(t), out var k)) { t.SetPositionAndRotation(k.pos, k.rot); t.localScale = k.scale; }
        }

        // ---------------------------------------------------------------- the piece: a bend. Its circle, in its own root's space
        var mf = tunnelSrc.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null).OrderByDescending(m => m.sharedMesh.vertexCount).First();
        var mats = mf.GetComponent<Renderer>().sharedMaterials;
        Vector3[] pts = mf.sharedMesh.vertices.Select(v => tunnelSrc.transform.InverseTransformPoint(mf.transform.TransformPoint(v))).ToArray();
        // the walkway: the flat strip you walk on, down the middle of the tube (faces looking straight up, at the bottom)
        float lowest = pts.Min(p => p.y);
        int[] tris = mf.sharedMesh.triangles;
        var flat = new List<Vector3>();
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 p0 = pts[tris[i]], p1 = pts[tris[i + 1]], p2 = pts[tris[i + 2]];
            if (Mathf.Max(p0.y, p1.y, p2.y) > lowest + 0.2f) continue;
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
            if (Mathf.Abs(n.y) < n.magnitude * 0.95f) continue;
            flat.Add(p0); flat.Add(p1); flat.Add(p2);
        }
        if (flat.Count < 30) return "Couldn't find the tunnel piece's walkway (no flat faces at the bottom of its mesh).";
        FitCircle(flat.ToArray(), out Vector2 c, out _);
        float Rad(Vector3 p) => Vector2.Distance(new Vector2(p.x, p.z), c);
        float Bearing(Vector3 p) => Mathf.Atan2(p.x - c.x, p.z - c.y);
        var radii = flat.Select(Rad).OrderBy(r => r).ToArray();
        float rIn = radii[radii.Length / 20], rOut = radii[radii.Length - 1 - radii.Length / 20], rc = (rIn + rOut) * 0.5f;
        var strip = flat.Where(p => Rad(p) >= rIn - 0.1f && Rad(p) <= rOut + 0.1f).ToArray();
        float floorY = strip.Average(p => p.y);
        float bA = strip.Min(Bearing), bB = strip.Max(Bearing);
        float arc = (bB - bA) * rc;
        Vector3 Local(float b, float r, float up) => new Vector3(c.x + Mathf.Sin(b) * r, floorY + up, c.y + Mathf.Cos(b) * r);
        Vector3 Tangent(float b) => new Vector3(Mathf.Cos(b), 0f, -Mathf.Sin(b));   // the way the bearing grows
        float Heading(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;

        // ---------------------------------------------------------------- laid out from the bridge's north end, alternating right and left
        Bounds bb = bridge.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
        float deckY = bb.min.y + 0.06f;
        Vector3 end = new Vector3(bb.center.x, deckY, bb.max.z - 0.4f);
        float heading = 0f;   // out of the bridge, northwards
        var pieces = new List<Piece>();
        for (int i = 0; i < Pieces; i++)
        {
            bool even = i % 2 == 0;
            var p = new Piece { near = even ? bA : bB, far = even ? bB : bA };
            float sign = even ? 1f : -1f;
            p.yaw = heading - Heading(Tangent(p.near) * sign);
            Quaternion rot = Quaternion.Euler(0f, p.yaw, 0f);
            p.root = end - rot * Local(p.near, rc, 0f);
            end = p.root + rot * Local(p.far, rc, 0f);
            heading = p.yaw + Heading(Tangent(p.far) * sign);

            var copy = Object.Instantiate(tunnelSrc, root);
            copy.name = "Tunnel piece " + (i + 1);
            copy.transform.SetPositionAndRotation(p.root, rot);
            copy.SetActive(true);
            p.t = copy.transform;
            // the tube itself is what you collide with (its faces look inward, which is the side you're on)
            foreach (var m in copy.GetComponentsInChildren<MeshFilter>())
                if (m.sharedMesh != null && m.sharedMesh.vertexCount > 100) m.gameObject.AddComponent<MeshCollider>().sharedMesh = m.sharedMesh;
            pieces.Add(p);
            // where two pieces meet the walkways stop a hand's width short of each other: a plate over the joint
            // (and the next piece starts a little way back inside this one, so no daylight shows through the seam)
            if (i < Pieces - 1)
            {
                Slab(root, "Joint plate", end + Vector3.down * 0.13f, new Vector3(rOut - rIn + 0.6f, 0.3f, 1.6f), mats[0]).rotation = Quaternion.Euler(0f, heading, 0f);
                end -= Quaternion.Euler(0f, heading, 0f) * Vector3.forward * 0.3f;
            }
        }
        Restore();
        tunnelSrc.SetActive(false);
        if (blackRef != null) blackRef.SetActive(false);
        Vector3 farEnd = end, outward = Quaternion.Euler(0f, heading, 0f) * Vector3.forward, inward = -outward;
        float total = arc * Pieces;

        // the route, from the tunnel door (0) to the bridge (total): a point 'aside' metres off the centre line
        Vector3 Route(float s, float aside, float up, out Vector3 forward)
        {
            s = Mathf.Clamp(s, 0f, total - 0.01f);
            int i = Pieces - 1 - Mathf.FloorToInt(s / arc);
            var p = pieces[i];
            float b = Mathf.Lerp(p.far, p.near, (s - (Pieces - 1 - i) * arc) / arc);
            forward = p.t.rotation * (Tangent(b) * (p.near > p.far ? 1f : -1f));
            return p.t.TransformPoint(Local(b, rc + aside, up));   // (where the piece stands now, not where it was laid)
        }

        // the walkway's middle, marked every few metres from the door to the bridge (for the robot playtester, and for you)
        var marks = new GameObject("Route (door to bridge)").transform;
        marks.SetParent(root, false);
        for (float s = 4f; s < total; s += 3f)
        {
            var wp = new GameObject("WP " + marks.childCount).transform;
            wp.SetParent(marks, false);
            wp.SetPositionAndRotation(Route(s, 0f, 0f, out Vector3 fwd), Quaternion.LookRotation(fwd));
        }

        // the mouth: only the bridge is open
        {
            var p = pieces[0];
            float half = bb.size.x * 0.5f;
            const float width = 3.4f;   // from the bridge's rail out to the tube wall
            foreach (float side in new[] { -1f, 1f })
            {
                var wall = Box(p.t, "Mouth barrier", Local(p.near, rc + side * (half + width * 0.5f), 1.6f), new Vector3(0.4f, 3.2f, width));
                wall.localRotation = Quaternion.Euler(0f, p.near * Mathf.Rad2Deg, 0f);   // (z across the tunnel)
            }
        }

        // ---------------------------------------------------------------- the footbridge: something to stand on
        var bridgeCols = new GameObject("Bridge colliders").transform;
        bridgeCols.SetParent(root, false);
        World(bridgeCols, "Deck", new Vector3(bb.center.x, deckY - 0.15f, bb.center.z), new Vector3(bb.size.x - 0.5f, 0.3f, bb.size.z + 0.6f));
        foreach (float side in new[] { -1f, 1f })
            World(bridgeCols, "Rail", new Vector3(bb.center.x + side * (bb.size.x * 0.5f - 0.1f), deckY + 1.4f, bb.center.z), new Vector3(0.2f, 2.8f, bb.size.z));

        // ---------------------------------------------------------------- the far end: a wall, and the door he comes through
        Transform doorway = new GameObject("Doorway").transform;
        doorway.SetParent(root, false);
        doorway.SetPositionAndRotation(farEnd, Quaternion.LookRotation(inward));
        Restore();
        float doorOff = Vector3.Distance(Route(0f, 0f, 0f, out _), doorway.position);
        farEnd = doorway.position; inward = doorway.forward; outward = -inward;
        Material wallMat = mats.FirstOrDefault(m => m != null && m.name.Contains("Wall")) ?? mats[0];
        const float doorScale = 1.2f;
        Bounds srcDoor = siloDoorObj.GetComponent<Renderer>().bounds;

        // ---------------------------------------------------------------- the footbridge's chamber
        // (user: "when im at the foot bridge make sure that i dont see the other models": out on the bridge you could
        // see the outside of the tube, the silo block's walkways and the open ground.) It crosses a walled concrete
        // chamber over the blood now: the tunnel's mouth in one end wall, the silo door in the other. Nothing here is
        // solid: the bridge's rails are what keep you on it.
        {
            var p = pieces[0];
            var ring = pts.Where(q => Mathf.Abs(Bearing(q) - p.near) < 0.03f).ToArray();   // the tube's end, in section
            float u0 = ring.Min(Rad), u1 = ring.Max(Rad), v0 = ring.Min(q => q.y), v1 = ring.Max(q => q.y);
            float tubeR = Mathf.Max(u1 - u0, v1 - v0) * 0.5f, um = (u0 + u1) * 0.5f;
            Vector3 mouth = p.t.TransformPoint(new Vector3(c.x + Mathf.Sin(p.near) * um, (v0 + v1) * 0.5f, c.y + Mathf.Cos(p.near) * um));
            Vector3 north = p.t.rotation * (Tangent(p.near) * (p.far > p.near ? 1f : -1f));   // on into the tunnel
            float half = tubeR + 1.3f, zS = srcDoor.max.z + 0.17f, zN = mouth.z + 0.15f, zMid = (zS + zN) * 0.5f, len = zN - zS + 1f;
            var room = new GameObject("Footbridge chamber").transform;
            room.SetParent(root, false);
            foreach (float side in new[] { -1f, 1f })
                Wall(room, "Side wall", new Vector3(mouth.x + side * (half + 0.25f), mouth.y, zMid), new Vector3(0.5f, half * 2f + 1f, len), wallMat);
            Wall(room, "Roof", new Vector3(mouth.x, mouth.y + half + 0.25f, zMid), new Vector3(half * 2f + 1f, 0.5f, len), wallMat);
            // the tunnel's end wall: a plate with a round hole, just behind the tube's rim
            var plate = new GameObject("Mouth wall");
            plate.transform.SetParent(room, false);
            plate.transform.SetPositionAndRotation(mouth + north * 0.15f, Quaternion.LookRotation(north));
            plate.AddComponent<MeshFilter>().sharedMesh = HoledPlate(tubeR - 0.3f, half + 0.5f);
            plate.AddComponent<MeshRenderer>().sharedMaterial = wallMat;
            // the silo's: all of it but the door (so the block's face, its annex and its walkways are behind it)
            float top = mouth.y + half, bottom = mouth.y - half, gap = 0.03f;
            float w = mouth.x - half - 0.5f, e = mouth.x + half + 0.5f;
            void South(string name, float x0, float x1, float y0, float y1) =>
                Wall(room, name, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, zS), new Vector3(x1 - x0, y1 - y0, 0.3f), wallMat);
            South("Door wall", w, srcDoor.min.x - gap, bottom, top);
            South("Door wall", srcDoor.max.x + gap, e, bottom, top);
            South("Door wall (over the door)", srcDoor.min.x - gap, srcDoor.max.x + gap, srcDoor.max.y + gap, top);
            South("Door wall (sill)", srcDoor.min.x - gap, srcDoor.max.x + gap, bottom, srcDoor.min.y);
        }
        float openW = Mathf.Min(srcDoor.size.x, srcDoor.size.z) < 0.6f ? Mathf.Max(srcDoor.size.x, srcDoor.size.z) * doorScale + 0.1f : 1.5f, openH = srcDoor.size.y * doorScale + 0.05f;
        foreach (float side in new[] { -1f, 1f })
            Slab(doorway, "End wall", new Vector3(side * (openW * 0.5f + 4f), 4.5f, -0.35f), new Vector3(8f, 9f, 0.5f), wallMat);
        Slab(doorway, "End wall (over the door)", new Vector3(0f, openH + 3.2f, -0.35f), new Vector3(openW, 6.4f, 0.5f), wallMat);
        Slab(doorway, "Doorstep", new Vector3(0f, -0.25f, -1.6f), new Vector3(openW + 2f, 0.5f, 3.2f), wallMat);   // (what he stands on, outside)
        var tunnelDoor = Object.Instantiate(siloDoorObj.gameObject, root).transform;
        tunnelDoor.name = "Tunnel door";
        tunnelDoor.gameObject.SetActive(true);
        tunnelDoor.localScale = siloDoorObj.lossyScale * doorScale;
        tunnelDoor.rotation = Quaternion.Euler(0f, Heading(inward) - Heading(DoorThin(srcDoor)), 0f) * siloDoorObj.rotation;
        tunnelDoor.position += farEnd + Vector3.up * (openH * 0.5f) + outward * 0.35f - tunnelDoor.GetComponent<Renderer>().bounds.center;
        Restore();
        var doorLight = new GameObject("Door light").AddComponent<Light>();
        doorLight.transform.SetParent(root, false);
        doorLight.transform.position = farEnd + outward * 1.6f + Vector3.up * 2.1f;
        doorLight.type = LightType.Point; doorLight.color = new Color(1f, 0.12f, 0.05f); doorLight.intensity = 9f; doorLight.range = 14f; doorLight.shadows = LightShadows.None;
        doorLight.enabled = false;

        // where you come in: a few steps inside, the door at your back
        var oldArrival = officeAct.Find("ComplexArrival");
        float eye = 0.93f;
        if (oldArrival != null && Physics.Raycast(oldArrival.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit under, 3f, ~0, QueryTriggerInteraction.Ignore)) eye = Mathf.Clamp(under.distance - 0.2f, 0.6f, 1.4f);
        Transform arrival = new GameObject("TunnelArrival").transform;
        arrival.SetParent(root, false);
        arrival.SetPositionAndRotation(Route(4f, 0f, eye, out Vector3 arriveDir), Quaternion.LookRotation(arriveDir));

        // ---------------------------------------------------------------- red lamps, and the bodies
        var lamps = new GameObject("Lamps").transform;
        lamps.SetParent(root, false);
        for (float s = 5f; s < total; s += 9.5f)
        {
            var l = new GameObject("Lamp").AddComponent<Light>();
            l.transform.SetParent(lamps, false);
            l.transform.position = Route(s, 0f, 4.6f, out _);
            l.type = LightType.Point; l.color = new Color(1f, 0.16f, 0.07f); l.intensity = 7f; l.range = 13f; l.shadows = LightShadows.None;
        }
        // the footbridge, and the room it leads into (which was pitch dark: you have to find your way on through it)
        foreach (float z in new[] { bb.max.z - 2f, bb.min.z + 2f, bb.min.z - 3.5f, bb.min.z - 11f })
        {
            var l = new GameObject(z > bb.min.z ? "Bridge lamp" : "Room lamp").AddComponent<Light>();
            l.transform.SetParent(lamps, false);
            l.transform.position = new Vector3(bb.center.x, deckY + 2.3f, z);
            l.type = LightType.Point; l.color = new Color(1f, 0.2f, 0.08f); l.intensity = 5f; l.range = 9f; l.shadows = LightShadows.None;
        }

        var bagModel = AssetDatabase.LoadAssetAtPath<GameObject>(BodyFbx);
        var officeBag = office.Find("Body");
        Material bagMat = officeBag != null ? officeBag.GetComponentsInChildren<Renderer>(true).Select(r => r.sharedMaterial).FirstOrDefault(m => m != null) : null;
        Material ropeMat = mats.FirstOrDefault(m => m != null && m.name.Contains("Pipe")) ?? wallMat;
        var bodies = new GameObject("Hanging bodies").transform;
        bodies.SetParent(root, false);
        var blood = AssetDatabase.LoadAssetAtPath<Material>(BloodMat);
        if (blood == null)
        {
            blood = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(blood, BloodMat);
        }
        blood.SetColor("_BaseColor", new Color(0.27f, 0.008f, 0.012f));
        EditorUtility.SetDirty(blood);
        var rnd = new System.Random(1304);
        int hung = 0;
        if (bagModel != null)
            for (float s = 14f; s < total - 5f; s += 2.1f + (float)rnd.NextDouble() * 1.3f)   // (none by the door: the cutscenes look back at it)
            {
                float aside = ((float)rnd.NextDouble() * 2f - 1f) * 1.7f;   // over the walkway and the foot of the slopes: you can't not brush them
                Vector3 hook = Route(s, aside, HookHeight, out _);
                Hang(bodies, hook, bagModel, bagMat, ropeMat, (float)rnd.NextDouble() * 360f, new[] { player, kc.damned.transform }, blood, 1.5f + (float)rnd.NextDouble() * 3.5f);
                // and what's dripped out of it so far
                var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(pool.GetComponent<Collider>());
                pool.name = "Blood pool";
                pool.transform.SetParent(bodies, false);
                pool.transform.position = Route(s, aside, 0.012f, out _);
                float wide = 0.5f + (float)rnd.NextDouble() * 0.7f;
                pool.transform.localScale = new Vector3(wide, 0.004f, wide * (0.7f + (float)rnd.NextDouble() * 0.5f));
                pool.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                pool.GetComponent<Renderer>().sharedMaterial = blood;
                hung++;
            }

        // ---------------------------------------------------------------- Kessler: his run, and Tomas
        kc.damnedAnim.runtimeAnimatorController = ChaseController(kc.damnedAnim);
        EditorUtility.SetDirty(kc.damnedAnim);
        Transform tomas = null; Animator tomasAnim = null;
        var ghost = office.Find("Ghost - Tomas");
        if (ghost != null)
        {
            tomas = Object.Instantiate(ghost.gameObject, root).transform;
            tomas.name = "Tomas (footbridge)";
            foreach (var comp in tomas.GetComponents<Component>().Where(k => k is Interactable).ToArray()) Object.DestroyImmediate(comp);
            foreach (var comp in tomas.GetComponents<Component>().Where(k => k is GhostNPC || k is Collider || k is AudioSource).ToArray()) Object.DestroyImmediate(comp);
            tomasAnim = tomas.GetComponentInChildren<Animator>(true);
            tomas.gameObject.SetActive(false);
        }
        Transform bridgeMouth = new GameObject("BridgeMouth (Tomas stands here)").transform;
        bridgeMouth.SetParent(root, false);
        bridgeMouth.SetPositionAndRotation(new Vector3(bb.center.x, deckY, bb.max.z - 1.2f), Quaternion.LookRotation(Vector3.back));

        var chase = root.gameObject.AddComponent<KesslerChase>();
        chase.player = player; chase.playerHead = cuts.playerHead; chase.health = player.GetComponent<PlayerHealth>(); chase.cuts = cuts;
        chase.office = kc; chase.kessler = kc.damned.transform; chase.kesslerAnim = kc.damnedAnim;
        chase.tunnelDoor = tunnelDoor; chase.doorway = doorway; chase.doorLight = doorLight;
        chase.tomas = tomas; chase.tomasAnim = tomasAnim; chase.bridgeMouth = bridgeMouth;
        chase.siloDoor = siloDoorObj.gameObject;
        chase.sfx = root.gameObject.AddComponent<AudioSource>();
        chase.sfx.playOnAwake = false; chase.sfx.spatialBlend = 0f;

        // ---------------------------------------------------------------- the beats
        StoryTrigger Trig(string name, Vector3 pos, Vector3 size, Vector3 facing)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.SetPositionAndRotation(pos, Quaternion.LookRotation(facing));
            var box = t.gameObject.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = size;
            var st = t.gameObject.AddComponent<StoryTrigger>();
            st.playerInCar = false;
            return st;
        }
        // (the door: no trigger. He's at it a few seconds after you arrive, and through it once you've walked on
        // for KesslerChase.walkTime)
        Vector3 waitAt = Route(total - 16f, 0f, 1.5f, out Vector3 waitDir);
        var waits = Trig("TomasTrigger (he's waiting at the mouth)", waitAt, new Vector3(9f, 4f, 1.2f), waitDir);
        UnityEventTools.AddVoidPersistentListener(waits.onEnter, chase.TomasWaits);
        var onBridge = Trig("BridgeTrigger (Tomas holds him)", new Vector3(bb.center.x, deckY + 1.4f, bb.max.z - 3.6f), new Vector3(3f, 3f, 1f), Vector3.back);
        UnityEventTools.AddVoidPersistentListener(onBridge.onEnter, chase.BridgeReached);
        Vector3 doorMid = srcDoor.center;
        var open = Trig("SiloDoorOpen", new Vector3(doorMid.x, deckY + 1.4f, doorMid.z + 2.4f), new Vector3(3f, 3f, 1f), Vector3.back);
        UnityEventTools.AddVoidPersistentListener(open.onEnter, chase.OpenSiloDoor);
        var enter = Trig("SiloEnter (it slams, he beats on it)", new Vector3(doorMid.x, deckY + 1.4f, doorMid.z - 2f), new Vector3(5f, 3f, 1f), Vector3.back);
        UnityEventTools.AddVoidPersistentListener(enter.onEnter, chase.EnterSilo);
        UnityEventTools.AddVoidPersistentListener(enter.onEnter, complexCp.Activate);
        if (eliasChase != null) UnityEventTools.AddVoidPersistentListener(enter.onEnter, eliasChase.Arm);

        // a checkpoint at the tunnel door: dying in the chase starts it again from there
        var cps = complexCp.transform.parent;
        var oldCp = cps.Find("Checkpoint - Kessler tunnel");
        if (oldCp != null) Object.DestroyImmediate(oldCp.gameObject);
        var tunnelCp = Object.Instantiate(complexCp.gameObject, cps).GetComponent<Checkpoint>();
        tunnelCp.name = "Checkpoint - Kessler tunnel";
        tunnelCp.spawn = arrival;
        Clear(tunnelCp.onRespawn);
        UnityEventTools.AddVoidPersistentListener(tunnelCp.onRespawn, chase.ResetChase);
        foreach (var t in new[] { waits, onBridge, open, enter }) UnityEventTools.AddVoidPersistentListener(tunnelCp.onRespawn, t.Rearm);

        // ---------------------------------------------------------------- the office's silo door now leads here
        Purge(teleporter.onArrive);
        Remove(teleporter.onArrive, complexCp, "Activate");
        Remove(teleporter.onArrive, entranceDoor.gameObject, "SetActive");   // (it stays shut until you open it yourself)
        if (eliasChase != null) Remove(teleporter.onArrive, eliasChase, "Arm");
        UnityEventTools.AddVoidPersistentListener(teleporter.onArrive, chase.Arrive);
        UnityEventTools.AddVoidPersistentListener(teleporter.onArrive, tunnelCp.Activate);
        teleporter.destination = arrival;
        teleporter.arriveLine = "A service tunnel, under Silo 2. ...what IS that, hanging from the roof?";
        EditorUtility.SetDirty(teleporter);
        var lockIt = teleporter.GetComponent<Interactable>();
        if (lockIt != null) { lockIt.lockedLine = "SILO 2 - AUTHORISED STAFF ONLY.|Locked. Kessler keeps the only key on him."; EditorUtility.SetDirty(lockIt); }

        // the door on from the corridor: you open it yourself, and that's the end of Kessler's part
        entranceDoor.gameObject.SetActive(true);
        var push = entranceDoor.GetComponent<Interactable>();
        if (push == null) push = entranceDoor.gameObject.AddComponent<Interactable>();
        push.prompt = "Open the door";
        push.once = true;
        push.hideOnUse = true;
        Clear(push.onUse);
        UnityEventTools.AddVoidPersistentListener(push.onUse, chase.StopBanging);
        EditorUtility.SetDirty(push);

        // ---------------------------------------------------------------- the office: he drops the key, you run
        var key = office.GetComponentsInChildren<Interactable>(true).FirstOrDefault(i => i.giveItem == "SiloKey");
        if (key != null)
        {
            kc.keyPickup = key.transform;
            if (key.transform.Find("KeyGlow") == null)
            {
                key.transform.localScale *= 2.2f;   // (it was a thing in a safe: on the floor in the dark it has to be seen)
                var glow = new GameObject("KeyGlow").AddComponent<Light>();
                glow.transform.SetParent(key.transform, false);
                glow.transform.localPosition = Vector3.up * 0.5f;
                glow.type = LightType.Point; glow.color = new Color(1f, 0.85f, 0.5f); glow.intensity = 2.5f; glow.range = 2.6f; glow.shadows = LightShadows.None;
            }
            key.line = "The silo key. GO!";
            EditorUtility.SetDirty(key);
        }
        kc.health = 99999;
        kc.afterTurnLine = "Too slow-- he's TURNED. The silo door. NOW!";
        kc.defeatedLine = "";
        EditorUtility.SetDirty(kc);

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        return $"Kessler's chase: {Pieces} tunnel pieces ({total:0} m, bend radius {rc:0.0} m, {rOut - rIn:0.0} m of floor) from the footbridge's north end {new Vector3(bb.center.x, deckY, bb.max.z):F1} to the tunnel door at {farEnd:F1}; "
             + $"the door stands {doorOff:0.0} m from where the last piece ends; {hung} hanging bodies; footbridge {bb.size.z:0.0} m; silo door at z {doorMid.z:0.0}; arrival {arrival.position:F1}; key pickup {(key != null ? "wired" : "MISSING")}; Tomas {(tomas != null ? "wired" : "MISSING")}.";
    }

    // which way the door's thin side faces as it stands in the scene
    static Vector3 DoorThin(Bounds b) => b.size.x < b.size.z ? Vector3.right : Vector3.forward;

    // ---------------------------------------------------------------- colliders

    static Transform Box(Transform parent, string name, Vector3 localPos, Vector3 size)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        t.gameObject.AddComponent<BoxCollider>().size = size;
        return t;
    }

    static void World(Transform parent, string name, Vector3 pos, Vector3 size)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = pos;
        t.gameObject.AddComponent<BoxCollider>().size = size;
    }

    // a visible, solid block (the end wall, the plates over the joints)
    static Transform Slab(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g.transform;
    }

    // one to look at only (the chamber round the footbridge)
    static void Wall(Transform parent, string name, Vector3 pos, Vector3 size, Material mat) =>
        Object.DestroyImmediate(Slab(parent, name, pos, size, mat).GetComponent<Collider>());

    // a square plate (2 x half across) with a round hole in the middle, seen from its -z side; a tile every 4 m
    static Mesh HoledPlate(float hole, float half)
    {
        const int n = 48;
        var v = new Vector3[n * 2];
        var uv = new Vector2[n * 2];
        var tri = new int[n * 6];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            v[i] = new Vector3(cs, sn, 0f) * hole;
            v[n + i] = new Vector3(cs, sn, 0f) * (half / Mathf.Max(Mathf.Abs(cs), Mathf.Abs(sn)));
            uv[i] = v[i] * 0.25f; uv[n + i] = v[n + i] * 0.25f;
            int j = (i + 1) % n, k = i * 6;
            tri[k] = i; tri[k + 1] = j; tri[k + 2] = n + i;
            tri[k + 3] = j; tri[k + 4] = n + j; tri[k + 5] = n + i;
        }
        var mesh = new Mesh { name = "Mouth wall", vertices = v, uv = uv, triangles = tri };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // a child by its name and which of that name it is ("Joint plate#1")
    static string Key(Transform t)
    {
        int n = 0;
        for (int i = 0; i < t.GetSiblingIndex(); i++) if (t.parent.GetChild(i).name == t.name) n++;
        return t.name + "#" + n;
    }

    // ---------------------------------------------------------------- a body on a rope

    static void Hang(Transform parent, Vector3 hook, GameObject bagModel, Material bagMat, Material ropeMat, float turn, Transform[] pushers, Material blood, float drips)
    {
        var pivot = new GameObject("Hanging body").transform;
        pivot.SetParent(parent, false);
        pivot.position = hook;   // (square to the world while the bag is measured and fitted; turned at the end)
        float rope = HookHeight - BagBottom - BagLength;

        var line = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(line.GetComponent<Collider>());
        line.name = "Rope";
        line.transform.SetParent(pivot, false);
        line.transform.localPosition = Vector3.down * rope * 0.5f;
        line.transform.localScale = new Vector3(0.035f, rope * 0.5f, 0.035f);
        line.GetComponent<Renderer>().sharedMaterial = ropeMat;

        // the bag, head up, its top at the end of the rope (whatever way up the model was made)
        var bag = (GameObject)Object.Instantiate(bagModel, pivot);
        bag.name = "Body bag";
        foreach (var col in bag.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
        var rs = bag.GetComponentsInChildren<Renderer>();
        if (bagMat != null) foreach (var r in rs) r.sharedMaterial = bagMat;
        bag.transform.localPosition = Vector3.zero;
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        Vector3 longAxis = b.size.x >= b.size.y && b.size.x >= b.size.z ? Vector3.right : b.size.z >= b.size.y ? Vector3.forward : Vector3.up;
        bag.transform.rotation = Quaternion.FromToRotation(longAxis, Vector3.down) * bag.transform.rotation;
        bag.transform.localScale *= BagLength / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.y, b.size.z));
        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        bag.transform.position += hook + Vector3.down * (rope + BagLength * 0.5f) - b.center;

        // blood, off the bottom of it ('drips' a second): square drops that fall to the floor
        var drip = new GameObject("Blood drip");
        drip.transform.SetParent(pivot, false);
        drip.transform.position = hook + Vector3.down * (rope + BagLength - 0.05f);
        var ps = drip.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = Mathf.Sqrt(2f * BagBottom / 9.81f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.065f);
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 16;
        var emission = ps.emission;
        emission.rateOverTime = drips;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;
        var pr = drip.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = blood;
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.velocityScale = 0.05f; pr.lengthScale = 1.5f;
        pivot.rotation = Quaternion.Euler(0f, turn, 0f);

        var swing = pivot.gameObject.AddComponent<HangingBody>();
        swing.length = rope + BagLength * 0.5f;
        swing.pushers = pushers;
    }

    // ---------------------------------------------------------------- his run

    // a copy of the walker controller he had, plus "Run": the user's RUNKESSLER clip
    static RuntimeAnimatorController ChaseController(Animator model)
    {
        string path = AnimDir + "/KesslerChase.controller";
        string src = AssetDatabase.GetAssetPath(model.runtimeAnimatorController);
        if (string.IsNullOrEmpty(src) || src == path) src = WalkerController;
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CopyAsset(src, path);
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        var clip = RunClip(model.transform);
        if (clip != null)
        {
            var st = ac.layers[0].stateMachine.AddState("Run");
            st.motion = clip;
            EditorUtility.SetDirty(ac);
        }
        return ac;
    }

    // RUNKESSLER was made for another Mixamo skeleton (its bones are "mixamorig7:..."): the same curves, on this
    // model's bone names, running on the spot (the script moves him), his hips at this model's height
    static AnimationClip RunClip(Transform model)
    {
        var srcModel = AssetDatabase.LoadAssetAtPath<GameObject>(RunFbx);
        var src = AssetDatabase.LoadAllAssetsAtPath(RunFbx).OfType<AnimationClip>().FirstOrDefault(k => !k.name.StartsWith("__preview__"));
        Transform srcHips = srcModel != null ? srcModel.transform.Cast<Transform>().FirstOrDefault(t => t.name.EndsWith("Hips")) : null;
        Transform hips = model.Cast<Transform>().FirstOrDefault(t => t.name.EndsWith("Hips"));
        if (src == null || srcHips == null || hips == null) { Debug.LogWarning("RUNKESSLER: no clip / hips found; he keeps his old run."); return null; }
        string from = srcHips.name.Substring(0, srcHips.name.Length - 4), to = hips.name.Substring(0, hips.name.Length - 4);
        float height = Mathf.Abs(srcHips.localPosition.y) > 0.0001f ? hips.localPosition.y / srcHips.localPosition.y : 1f;

        var clip = new AnimationClip { frameRate = src.frameRate };
        foreach (var b in AnimationUtility.GetCurveBindings(src))
        {
            var curve = AnimationUtility.GetEditorCurve(src, b);
            string path = b.path.Replace(from, to);
            if (path == hips.name && b.propertyName.StartsWith("m_LocalPosition"))
            {
                var keys = curve.keys;
                if (b.propertyName.EndsWith(".y")) for (int i = 0; i < keys.Length; i++) { keys[i].value *= height; keys[i].inTangent *= height; keys[i].outTangent *= height; }
                else for (int i = 0; i < keys.Length; i++) { keys[i].value = hips.localPosition[b.propertyName.EndsWith(".x") ? 0 : 2]; keys[i].inTangent = keys[i].outTangent = 0f; }
                curve = new AnimationCurve(keys);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, b.type, b.propertyName), curve);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        string asset = AnimDir + "/KesslerRun.anim";
        AssetDatabase.DeleteAsset(asset);
        AssetDatabase.CreateAsset(clip, asset);
        return clip;
    }

    // ---------------------------------------------------------------- helpers

    static void Clear(UnityEvent e) { while (e.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(e, 0); }

    static void Remove(UnityEvent e, Object target, string method)
    {
        for (int i = e.GetPersistentEventCount() - 1; i >= 0; i--)
            if (e.GetPersistentTarget(i) == target && e.GetPersistentMethodName(i) == method) UnityEventTools.RemovePersistentListener(e, i);
    }

    // listeners left pointing at things an earlier run of this made and has since destroyed
    static void Purge(UnityEvent e)
    {
        for (int i = e.GetPersistentEventCount() - 1; i >= 0; i--)
            if (e.GetPersistentTarget(i) == null) UnityEventTools.RemovePersistentListener(e, i);
    }

    // least-squares circle through points in the XZ plane
    static void FitCircle(Vector3[] p, out Vector2 centre, out float radius)
    {
        double mx = p.Average(v => (double)v.x), mz = p.Average(v => (double)v.z);
        double sxx = 0, szz = 0, sxz = 0, sxxx = 0, szzz = 0, sxzz = 0, sxxz = 0;
        foreach (var v in p)
        {
            double x = v.x - mx, z = v.z - mz;
            sxx += x * x; szz += z * z; sxz += x * z; sxxx += x * x * x; szzz += z * z * z; sxzz += x * z * z; sxxz += x * x * z;
        }
        double e = 0.5 * (sxxx + sxzz), f = 0.5 * (szzz + sxxz), det = sxx * szz - sxz * sxz;
        double cx = (e * szz - sxz * f) / det, cz = (sxx * f - sxz * e) / det;
        centre = new Vector2((float)(cx + mx), (float)(cz + mz));
        radius = (float)System.Math.Sqrt(cx * cx + cz * cz + (sxx + szz) / p.Length);
    }
}
