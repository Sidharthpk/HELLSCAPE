using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// HellScape > Prologue: Street Extras (the Prologue scene must be open). Everything hangs off one root,
// "Street extras" under the Prologue root, and is rebuilt from scratch each time:
//   - two zebra crossings (the delivery street's long straight, and the home street by the office)
//   - the grandma on her Vespa who shoots over the first one in front of the van (GrandmaScare)
//   - more pedestrians, on the footpaths of the delivery streets (copies of the StreetLife walkers)
// It also takes the two StreetLife pedestrians who used to cut across the road by the office and puts them on
// the pavement. StreetLife itself is left alone (PrologueBuilder would undo the PSX people).
public static class PrologueStreetExtras
{
    const string Fbx = "Assets/Perishables/Grandma rides a Vintage Vespa/Grandma rides a Vintage Vespa.fbx";
    const string RootName = "Street extras";
    const string AssetDir = "Assets/Generated/Prologue";
    const float GrandmaHeight = 1.55f;      // the model comes 1 m tall (rider + scooter), centred on its middle
    const float GrandmaNoseYaw = 40f;       // and with its nose 40 degrees off +z

    static readonly Vector3 DeliveryCrossing = new Vector3(108.8f, 0f, -55.2f);   // on the drive line, mid-straight
    static readonly Vector3 DeliveryDir = new Vector3(-0.571f, 0f, 0.821f);       // the way you drive it, to the depot
    static readonly Vector3 HomeCrossing = new Vector3(63f, 0f, 3f);              // clear of the parked cars at x 50-58
    static readonly Vector3 HomeDir = Vector3.left;

    static Scene scene;

    [MenuItem("HellScape/Prologue: Street Extras (grandma, crossings, footpath people)")]
    public static void Build()
    {
        var run = Object.FindFirstObjectByType<DeliveryRun>(FindObjectsInactive.Include);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if (run == null || model == null) { Debug.LogError("Street extras: open the Prologue scene (DeliveryRun) and keep the model at " + Fbx); return; }
        scene = run.gameObject.scene;
        Transform prologue = run.transform.parent;
        Physics.SyncTransforms();

        var oldRoot = prologue.Find(RootName);
        if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot.gameObject);
        var root = new GameObject(RootName).transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Street extras");
        root.SetParent(prologue, false);

        if (!AssetDatabase.IsValidFolder(AssetDir)) AssetDatabase.CreateFolder("Assets/Generated", "Prologue");
        var paint = AssetDatabase.LoadAssetAtPath<Material>(AssetDir + "/ZebraPaint.mat");
        if (paint == null)
        {
            paint = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.8f, 0.78f, 0.72f) };
            paint.SetFloat("_Smoothness", 0f);
            AssetDatabase.CreateAsset(paint, AssetDir + "/ZebraPaint.mat");
        }

        Transform crossing = Zebra(root, "Zebra crossing (delivery street)", DeliveryCrossing, DeliveryDir, paint);
        Zebra(root, "Zebra crossing (home street)", HomeCrossing, HomeDir, paint);
        Grandma(root, crossing, run, model);
        int people = People(root, prologue);
        int moved = OffTheRoad(prologue);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Street extras: 2 zebra crossings, grandma at " + crossing.position.ToString("F1") + ", " + people + " footpath people added, " + moved + " moved off the road.");
    }

    // ---------------------------------------------------------------- ground probing

    static bool Moving(Collider c) =>
        c.GetComponentInParent<PavementWalker>() != null || c.GetComponentInParent<LoopDriver>() != null || c.GetComponentInParent<PrologueVan>() != null;

    // street-level ground height (cars and people don't count; nothing higher than a kerb does either)
    static float Ground(Vector3 p)
    {
        float y = float.MinValue;
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, 3f, p.z), Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider.gameObject.scene == scene && !Moving(h.collider) && h.point.y > y && h.point.y < 0.8f) y = h.point.y;
        return y;
    }

    // metres from a point on the road out to the kerb on that side
    static float Kerb(Vector3 onRoad, Vector3 side)
    {
        float road = Ground(onRoad);
        for (float o = 0.25f; o < 20f; o += 0.25f)
            if (Ground(onRoad + side * o) > road + 0.12f) return o;
        return 20f;
    }

    // ---------------------------------------------------------------- zebra crossing

    static Transform Zebra(Transform root, string name, Vector3 onRoad, Vector3 dir, Material paint)
    {
        dir.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        float r = Kerb(onRoad, right), l = Kerb(onRoad, -right);
        Vector3 mid = onRoad + right * (r - l) * 0.5f;
        mid.y = Ground(mid);
        float half = (r + l) * 0.5f;

        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(mid, Quaternion.LookRotation(dir));

        // stripes run the way the traffic does, side by side across the road
        const float stripe = 0.5f, gap = 0.5f, length = 3.2f, lift = 0.02f;
        var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>();
        for (float x = -half + 0.3f; x + stripe <= half - 0.3f + 0.001f; x += stripe + gap)
        {
            int n = verts.Count;
            verts.Add(new Vector3(x, lift, -length * 0.5f)); verts.Add(new Vector3(x, lift, length * 0.5f));
            verts.Add(new Vector3(x + stripe, lift, length * 0.5f)); verts.Add(new Vector3(x + stripe, lift, -length * 0.5f));
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));
            tris.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path = AssetDir + "/" + name + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = paint;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    // ---------------------------------------------------------------- grandma

    static void Grandma(Transform root, Transform crossing, DeliveryRun run, GameObject model)
    {
        var zone = new GameObject("Grandma scare");
        zone.transform.SetParent(root, false);
        zone.transform.SetPositionAndRotation(crossing.position, crossing.rotation);

        var rider = new GameObject("Grandma");
        rider.transform.SetParent(zone.transform, false);
        var m = (GameObject)PrefabUtility.InstantiatePrefab(model, rider.transform);
        m.transform.localPosition = Vector3.up * (GrandmaHeight * 0.5f);
        m.transform.localRotation = Quaternion.Euler(0f, GrandmaNoseYaw, 0f) * m.transform.localRotation;
        m.transform.localScale *= GrandmaHeight;

        var src = rider.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0.6f;            // mostly in the world, but the horn still lands from across the street
        src.minDistance = 6f;
        src.maxDistance = 70f;
        src.dopplerLevel = 1f;

        var scare = zone.AddComponent<GrandmaScare>();
        scare.van = run.van;
        scare.grandma = rider.transform;
        scare.sound = src;
        rider.SetActive(false);

        // the bonnet camera it cuts to as she crosses: the van camera's settings, drawn over it
        var camGo = new GameObject("Scare camera");
        camGo.transform.SetParent(zone.transform, false);
        var cam = camGo.AddComponent<Camera>();
        var vanCam = run.van.vanCamera;
        if (vanCam != null)
        {
            cam.CopyFrom(vanCam);
            cam.depth = vanCam.depth + 1f;
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = vanCam.GetUniversalAdditionalCameraData().renderPostProcessing;
        }
        cam.fieldOfView = 62f;
        cam.nearClipPlane = 0.1f;
        cam.enabled = false;
        scare.scareCam = cam;
    }

    // ---------------------------------------------------------------- people on the footpaths

    // a clear walking line on the footpath beside the road from p to q, or false
    static bool Lane(Vector3 p, Vector3 q, Vector3 side, float inFromKerb, out Vector3 a, out Vector3 b)
    {
        Vector3 along = (q - p).normalized;
        a = p + along * 2f + side * (Kerb(p, side) + inFromKerb);
        b = q - along * 2f + side * (Kerb(q, side) + inFromKerb);
        float road = Mathf.Max(Ground(p), Ground(q));
        for (int i = 0; i <= 8; i++)
        {
            Vector3 s = Vector3.Lerp(a, b, i / 8f);
            float y = Ground(s);
            if (y < road + 0.06f) return false;                                                     // dips onto the road
            if (Physics.OverlapSphere(new Vector3(s.x, y + 1f, s.z), 0.35f, ~0, QueryTriggerInteraction.Ignore)
                .Any(c => c.gameObject.scene == scene && !Moving(c))) return false;                 // inside a building
        }
        Vector3 a1 = new Vector3(a.x, Ground(a) + 1f, a.z), b1 = new Vector3(b.x, Ground(b) + 1f, b.z);
        foreach (var h in Physics.RaycastAll(a1, b1 - a1, Vector3.Distance(a1, b1), ~0, QueryTriggerInteraction.Ignore))
            if (h.collider.gameObject.scene == scene && !Moving(h.collider)) return false;          // a lamp post, a wall
        a.y = b.y = 0f;   // (the walker finds the ground itself)
        return true;
    }

    static int People(Transform root, Transform prologue)
    {
        var line = MapRoutes.DriveLine(scene, out _);
        var life = prologue.Find("StreetLife");
        var sources = life != null ? life.GetComponentsInChildren<PavementWalker>(true) : new PavementWalker[0];
        if (line.Length < 21 || sources.Length == 0) { Debug.LogWarning("Street extras: no drive line or no StreetLife walkers to copy."); return 0; }

        var holder = new GameObject("Footpath people").transform;
        holder.SetParent(root, false);
        var rnd = new System.Random(712);
        int made = 0;
        // the straight stretches of the delivery round, by drive-line point
        foreach (var (i, j) in new[] { (1, 3), (3, 4), (4, 5), (8, 14), (14, 20) })
            foreach (float sign in new[] { 1f, -1f })
            {
                Vector3 side = Vector3.Cross(Vector3.up, (line[j] - line[i]).normalized) * sign;
                int onThisSide = 0;
                foreach (float inFromKerb in new[] { 0.8f, 2.2f, 1.5f, 0.4f })
                {
                    if (onThisSide >= 2 || !Lane(line[i], line[j], side, inFromKerb, out var a, out var b)) continue;
                    var src = sources[rnd.Next(sources.Length)];
                    var g = Object.Instantiate(src.gameObject, holder);
                    g.name = "Footpath person " + made;
                    var w = g.GetComponent<PavementWalker>();
                    w.a = a; w.b = b;
                    w.speed = 1f + (float)rnd.NextDouble() * 0.5f;
                    w.startAt = (float)rnd.NextDouble();
                    g.transform.position = Vector3.Lerp(a, b, w.startAt);
                    made++; onThisSide++;
                }
            }
        return made;
    }

    // Pedestrians 13 and 14 walked a diagonal across the road by the office: onto the home street's south pavement
    static int OffTheRoad(Transform prologue)
    {
        int moved = 0;
        var life = prologue.Find("StreetLife");
        if (life == null) return 0;
        Vector3 p = new Vector3(32f, 0f, 3f), q = new Vector3(60f, 0f, 3f);
        var lanes = new Queue<float>(new[] { 0.8f, 2.2f, 1.5f, 0.4f });
        foreach (var w in life.GetComponentsInChildren<PavementWalker>(true))
        {
            float road = Ground(p);
            bool onRoad = Enumerable.Range(1, 7).Any(k => Ground(Vector3.Lerp(w.a, w.b, k / 8f)) < road + 0.06f);
            if (!onRoad) continue;
            while (lanes.Count > 0)
            {
                if (!Lane(p, q, Vector3.back, lanes.Dequeue(), out var a, out var b)) continue;
                Undo.RecordObject(w, "Off the road");
                w.a = a; w.b = b;
                Undo.RecordObject(w.transform, "Off the road");
                w.transform.position = Vector3.Lerp(a, b, w.startAt);
                EditorUtility.SetDirty(w);
                moved++;
                break;
            }
        }
        return moved;
    }
}
