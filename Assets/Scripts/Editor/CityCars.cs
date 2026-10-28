using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Puts the generic passenger car pack (HellScape > Cars > Build Car Pack Prefabs) to work in the open scene and takes
// every other car out; the van stays. Run it again any time: the cars it made are rebuilt, nothing is stacked.
//   Prologue      the moving traffic (LoopDriver, now with the new driving) and the parked cars, plus more of both
//   MainGameScene the abandoned cars of the hell city (Colldables), the wrecks on the escape road, the cars in the
//                 office loading bay, plus more abandoned cars on the streets
// Buses are left alone: they aren't cars.
public static class CityCars
{
    const string MadeRoot = "Car pack cars";
    static bool wrecking;   // the red city's cars come out wrecked (WreckedCars)
    static int wreckSeed;   // the extra cars added by this tool live under this

    [MenuItem("HellScape/Cars/Swap In Car Pack (open scene)")]
    public static void Menu() => Debug.Log(Apply());

    public static string Apply()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CarPack.PrefabPath(CarPack.Names[0])) == null) CarPack.Build();
        var scene = SceneManager.GetActiveScene();
        string log = scene.name == "Prologue" ? Prologue(scene) : scene.name == "MainGameScene" ? Main(scene) : "Open the Prologue or MainGameScene.";
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        return log;
    }

    // ---------------------------------------------------------------- one car

    static GameObject Prefab(int i) => AssetDatabase.LoadAssetAtPath<GameObject>(CarPack.PrefabPath(CarPack.Names[((i % CarPack.Names.Length) + CarPack.Names.Length) % CarPack.Names.Length]));

    // the pack car under a holder (origin on the ground, nose along the holder's +z), the holder's box collider fitted round it
    static void Dress(Transform holder, GameObject prefab, bool kinematic)
    {
        foreach (var c in holder.Cast<Transform>().ToArray()) Object.DestroyImmediate(c.gameObject);
        foreach (var t in holder.GetComponents<MonoBehaviour>().Where(m => m != null && m.GetType().Name == "RandomTint").ToArray()) Object.DestroyImmediate(t);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
        model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity; model.transform.localScale = Vector3.one;
        if (wrecking && !kinematic) WreckedCars.Apply(model.transform, wreckSeed++);
        var car = model.GetComponent<PackCar>();
        var box = holder.GetComponent<BoxCollider>() ?? holder.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, car.size.y * 0.5f + 0.05f, 0f);
        box.size = new Vector3(car.size.x * 0.95f, car.size.y - 0.1f, car.size.z * 0.98f);
        var rb = holder.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = kinematic;
    }

    static string Describe(GameObject prefab) => prefab.name.Replace("Pack", "");

    // ---------------------------------------------------------------- the prologue

    static string Prologue(Scene scene)
    {
        Transform life = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "StreetLife");
        if (life == null) return "No StreetLife in this scene.";
        int n = 0, kind = 0;

        // the moving traffic: the existing drivers first, then more on the same route
        var drivers = life.GetComponentsInChildren<LoopDriver>(true).Where(d => d.transform.parent == life).OrderBy(d => d.name).ToList();
        var made = life.Find(MadeRoot);
        if (made != null) Object.DestroyImmediate(made.gameObject);
        Vector3[] path = drivers.Select(d => d.path).FirstOrDefault(p => p != null && p.Length > 4);
        foreach (var d in drivers) { Dress(d.transform, Prefab(kind++), true); d.modelYaw = 0f; n++; }
        made = new GameObject(MadeRoot).transform; made.SetParent(life, false);
        if (path != null && drivers.Count > 0)
        {
            const int total = 14;
            float length = 0f; for (int i = 1; i < path.Length; i++) length += Vector3.Distance(path[i - 1], path[i]);
            var like = drivers[0];
            var rnd = new System.Random(11);
            for (int i = drivers.Count; i < total; i++)
            {
                var go = new GameObject("Traffic car " + i); go.transform.SetParent(made, false);
                go.layer = like.gameObject.layer; go.tag = like.gameObject.tag;
                var rb = go.AddComponent<Rigidbody>();
                var lrb = like.GetComponent<Rigidbody>();
                if (lrb != null) { rb.mass = lrb.mass; rb.interpolation = lrb.interpolation; rb.collisionDetectionMode = lrb.collisionDetectionMode; }
                go.AddComponent<BoxCollider>();
                Dress(go.transform, Prefab(kind++), true);
                var d = go.AddComponent<LoopDriver>();
                d.path = path; d.player = like.player; d.speed = 7f + (float)rnd.NextDouble() * 3.5f;
                d.startAt = length * i / total + (float)rnd.NextDouble() * 6f;
                n++;
            }
            // the originals keep their own spacing; spread them among the new ones too
            for (int i = 0; i < drivers.Count; i++) drivers[i].startAt = length * i / total * 1f + 3f;
        }

        // the parked cars: the old ones stood on a plaza that is no longer a street; park a fresh set along the real kerbs
        foreach (var t in life.Cast<Transform>().Where(t => t.name.StartsWith("Parked car")).ToArray()) Object.DestroyImmediate(t.gameObject);
        int parked = path != null ? ParkMore(made, path, ref kind, 16, false) : 0;
        n += parked;

        // any other old car left in the scene (outside StreetLife) other than the van
        return $"Prologue: {drivers.Count} driving cars swapped, {n - drivers.Count - parked} more driving, {parked} parked along the kerbs; total {n}.";
    }

    // parked cars hugging the kerb: from the lane, step sideways until the ground steps up (the kerb) and park a car's width short of it
    static int ParkMore(Transform parent, Vector3[] path, ref int kind, int want, bool abandoned, int salt = 0)
    {
        var keepClear = new List<Vector3>();
        var van = Object.FindObjectsByType<PrologueVan>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (van != null) keepClear.Add(van.transform.position);
        foreach (var run in Object.FindObjectsByType<DeliveryRun>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var st in run.stops) if (st.bay != null) keepClear.Add(st.bay.position);
        var spawn = GameObject.FindGameObjectWithTag("Player");
        if (spawn != null) keepClear.Add(spawn.transform.position);
        int added = 0;
        var rnd = new System.Random((abandoned ? 23 : 5) + salt * 101);
        for (int i = 14; i < path.Length - 14 && added < want; i += 4 + rnd.Next(0, 5))
        {
            Vector3 tan = Flat(path[Mathf.Min(i + 2, path.Length - 1)] - path[i]);
            if (tan.sqrMagnitude < 0.01f) continue;
            tan.Normalize();
            Vector3 right = new Vector3(tan.z, 0f, -tan.x);
            float side = rnd.Next(0, 2) == 0 ? 1f : -1f;
            if (abandoned && rnd.NextDouble() < 0.35)
            {
                // stopped dead in the road, at any angle
                Vector3 mid = path[i] + right * ((float)rnd.NextDouble() - 0.5f) * 5f;
                if (keepClear.Any(k => Flat(k - mid).sqrMagnitude < 14f * 14f)) continue;
                if (!Physics.Raycast(mid + Vector3.up * 3f, Vector3.down, out var rh, 8f, ~0, QueryTriggerInteraction.Ignore)) continue;
                var rrot = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                if (Physics.OverlapBox(rh.point + Vector3.up * 1.15f, new Vector3(1.2f, 0.55f, 2.9f), rrot, ~0, QueryTriggerInteraction.Ignore).Length > 0) continue;
                MakeCar(parent, "Abandoned car " + added, rh.point, rrot, ref kind);
                added++;
                continue;
            }
            // the kerb on that side
            if (!Physics.Raycast(path[i] + Vector3.up * 3f, Vector3.down, out var lane, 8f, ~0, QueryTriggerInteraction.Ignore)) continue;
            float baseY = lane.point.y, kerb = -1f;
            for (float d = 3f; d < 26f; d += 0.25f)
            {
                Vector3 q = path[i] + right * side * d;
                if (!Physics.Raycast(q + Vector3.up * 3f, Vector3.down, out var h, 8f, ~0, QueryTriggerInteraction.Ignore)) break;
                if (h.point.y > baseY + 0.03f) { kerb = d; break; }   // (the kerbs here are only a few centimetres)
            }
            if (kerb < 5f) continue;
            Vector3 pos = path[i] + right * side * (kerb - 1.3f);
            if (keepClear.Any(k => Flat(k - pos).sqrMagnitude < 14f * 14f)) continue;
            if (!Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (path.Any(p => Flat(p - pos).sqrMagnitude < 3.3f * 3.3f)) continue;   // never in a lane
            Quaternion rot = Quaternion.LookRotation(tan * side);                    // facing the way its own side's traffic goes
            var hits = Physics.OverlapBox(hit.point + Vector3.up * 1.15f, new Vector3(1.1f, 0.55f, 2.7f), rot, ~0, QueryTriggerInteraction.Ignore);
            if (hits.Any(c => !c.transform.IsChildOf(parent) || c.transform.parent == parent)) continue;
            if (abandoned) rot *= Quaternion.Euler(0f, (float)(rnd.NextDouble() - 0.5) * 24f, 0f);
            MakeCar(parent, (abandoned ? "Abandoned car " : "Parked car ") + added, hit.point, rot, ref kind);
            added++;
        }
        return added;
    }

    static void MakeCar(Transform parent, string name, Vector3 at, Quaternion rot, ref int kind)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(at, rot);
        go.AddComponent<BoxCollider>();
        var rb = go.AddComponent<Rigidbody>(); rb.mass = 1200f;
        Dress(go.transform, Prefab(kind++), false);
        Physics.SyncTransforms();
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // ---------------------------------------------------------------- the hell city

    static readonly string[] OldCars = { "caprice", "police", "estate", "fiesta", "s2000", "passat", "sedan", "car" };

    static string Main(Scene scene)
    {
        wrecking = true; wreckSeed = 0;
        try { return MainWrecked(scene); } finally { wrecking = false; }
    }

    static string MainWrecked(Scene scene)
    {
        int swapped = 0, kind = 3;
        // the abandoned cars of the hell city: same place, same way round, a new model
        var lot = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Colldables");
        if (lot != null)
            foreach (Transform holder in lot.transform.Cast<Transform>().ToArray())
            {
                string n = holder.name.ToLower();
                if (!OldCars.Any(k => n.Contains(k)) || n.Contains("bus")) continue;
                var box = holder.GetComponent<BoxCollider>();
                Quaternion rot = holder.rotation;
                Vector3 at = holder.position;
                if (box != null)
                {
                    Physics.SyncTransforms();
                    at = new Vector3(box.bounds.center.x, box.bounds.min.y, box.bounds.center.z);
                    if (box.size.x > box.size.z) rot *= Quaternion.Euler(0f, 90f, 0f);   // its long axis was x: the new car's is z
                }
                holder.SetPositionAndRotation(at, Quaternion.Euler(0f, rot.eulerAngles.y, 0f));
                foreach (var c in holder.GetComponents<BoxCollider>().Skip(1).ToArray()) Object.DestroyImmediate(c);
                Dress(holder, Prefab(kind++), false);
                holder.name = "Car (" + Describe(Prefab(kind - 1)) + ")";
                swapped++;
            }

        // the wrecks on the escape road (RoadObstacleSpawner): every old car wreck out, the ten new cars in (buses stay)
        int wrecks = 0;
        var spawner = Object.FindObjectsByType<RoadObstacleSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(sp => sp.gameObject.scene == scene);
        if (spawner != null && spawner.obstacles != null)
        {
            var old = spawner.obstacles.Where(o => o.prefab != null && o.prefab.name.StartsWith("Wreck") && !o.prefab.name.StartsWith("WreckPack") && !o.prefab.name.Contains("Bus")).ToList();
            var like = old.FirstOrDefault() ?? spawner.obstacles.FirstOrDefault();
            if (like != null)
            {
                float weight = Mathf.Max(0.1f, old.Sum(o => o.weight)) / CarPack.Names.Length;
                var list = spawner.obstacles.Where(o => !old.Contains(o) && !(o.prefab != null && o.prefab.name.StartsWith("WreckPack"))).ToList();
                foreach (var name in CarPack.Names)
                {
                    list.Add(new RoadObstacleSpawner.ObstacleType { prefab = Wreck(name), damage = like.damage, pushable = like.pushable, mass = like.mass, weight = weight });
                    wrecks++;
                }
                Undo.RecordObject(spawner, "Wrecks");
                spawner.obstacles = list.ToArray();
                EditorUtility.SetDirty(spawner);
            }
        }

        // the cars in the office loading bay (low-poly props): real ones, where they stood
        int bay = 0;
        foreach (var name in new[] { "LOADING BAY - police car", "LOADING BAY - passat" })
        {
            var t = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(x => x.name == name);
            if (t == null) continue;
            var go = new GameObject("LOADING BAY - car (" + Describe(Prefab(kind)) + ")"); go.transform.SetParent(t.parent, false);
            var r0 = t.GetComponent<Renderer>();
            bool wide = r0 != null && r0.bounds.size.x > r0.bounds.size.z;
            go.transform.SetPositionAndRotation(new Vector3(t.position.x, r0 != null ? r0.bounds.min.y : t.position.y, t.position.z), Quaternion.Euler(0f, t.eulerAngles.y + (wide ? 90f : 0f), 0f));
            go.AddComponent<BoxCollider>();
            Dress(go.transform, Prefab(kind++), true);
            Object.DestroyImmediate(t.gameObject);
            bay++;
        }

        // more abandoned cars on the streets of the old route: some at the kerb, some stopped dead in the road
        var oldRoot = scene.GetRootGameObjects().FirstOrDefault(r => r.name == MadeRoot);
        if (oldRoot != null) Object.DestroyImmediate(oldRoot);
        var made = new GameObject(MadeRoot).transform;
        int more = ParkMore(made, SharedCompactCity.TrafficRoute(), ref kind, 70, true);
        for (int pass = 1; pass <= 3; pass++) more += ParkMore(made, SharedCompactCity.TrafficRoute(), ref kind, 70, true, pass);   // (other spacings fill the gaps)
        return $"MainGameScene: {swapped} abandoned cars swapped, {wrecks} road wrecks, {bay} loading-bay cars, {more} more abandoned cars.";
    }

    // a wreck prefab for the road spawner: the pack car with a box collider (the spawner would fit one anyway)
    static GameObject Wreck(string name)
    {
        string path = $"Assets/Prefabs/Obstacles/WreckPack{name}.prefab";
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CarPack.PrefabPath(name)));
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "WreckPack" + name;
        WreckedCars.Apply(go.transform, System.Array.IndexOf(CarPack.Names, name) + 100);
        var car = go.GetComponent<PackCar>();
        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, car.size.y * 0.5f, 0f); box.size = new Vector3(car.size.x * 0.95f, car.size.y, car.size.z * 0.98f);
        Object.DestroyImmediate(car);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }
}
