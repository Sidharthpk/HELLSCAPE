using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Uses the authored demo scene itself, including its prefab overrides and lighting.
// MainGameScene retains its path/GUID so scene loads and story hooks keep working.
public static class DemoCityMigration
{
    const string DemoPath = "Assets/Versatile Studio Assets/Demo City By Versatile Studio/Scenes/demo_city_night.unity";
    const string MainPath = "Assets/Scenes/MainGameScene.unity";
    static Scene main;
    static Transform[] original;
    static Transform Find(string name) => original.FirstOrDefault(t => t != null && t.name == name);
    static T Component<T>() where T : Component => original.Select(t => t.GetComponent<T>()).First(t => t != null);
    static void Move(string name, Vector3 delta) { var t = Find(name); if (t != null) t.position += delta; }
    static void Off(string name) { var t = Find(name); if (t != null) t.gameObject.SetActive(false); }
    static GameObject Group(string name) { var g = new GameObject(name); SceneManager.MoveGameObjectToScene(g, main); return g; }
    static void Slab(Transform parent, string name, Vector3 at, Vector3 size, Material material)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name; g.transform.SetParent(parent, false);
        g.transform.position = at; g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = material;
        GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
    }
    static void BridgePose(Transform t, Quaternion turn, Vector3 from, Vector3 to)
    {
        t.SetPositionAndRotation(to + turn * (t.position - from), turn * t.rotation);
    }

    // The demo collision mesh includes perimeter fences. Cut only vertical barrier
    // triangles at the story access points; retain its original road surface.
    public static void FinishNavigation()
    {
        var scene = SceneManager.GetSceneByName("MainGameScene");
        var ts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        var city = ts.First(t => t.name == "STATIC_MODELS");
        foreach (string name in new[] { "fence_part64", "fence_part65", "fence_part67", "fence_part70", "fence_part81", "fence_part82" })
            foreach (var t in city.GetComponentsInChildren<Transform>(true).Where(t => t.name == name)) t.gameObject.SetActive(false);
        var collider = city.GetComponentsInChildren<MeshCollider>(true).First(c => c.name == "city_part_collider");
        const string meshPath = "Assets/Generated/DemoCity/CityCollisionWithEntrances.asset";
        var storedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        Mesh mesh;
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Versatile Studio Assets/Demo City By Versatile Studio/Prefabs/demo_city_by_versatile_studio.prefab").GetComponentInChildren<MeshCollider>().sharedMesh;
            mesh = Object.Instantiate(source);
            mesh.name = "CityCollisionWithEntrances";
            var openings = new[] {
                new Bounds(new Vector3(-38, 5, 40), new Vector3(12, 10, 14)),
                new Bounds(new Vector3(126.5f, 7, 34), new Vector3(8, 10, 14)),
                new Bounds(new Vector3(126.5f, 8, 106), new Vector3(8, 12, 18)),
                new Bounds(new Vector3(245, 10, 75), new Vector3(12, 10, 110)) };
            var vertices = mesh.vertices;
            int removed = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var triangles = mesh.GetTriangles(sub); var kept = new List<int>();
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var a = collider.transform.TransformPoint(vertices[triangles[i]]);
                    var b = collider.transform.TransformPoint(vertices[triangles[i + 1]]);
                    var c = collider.transform.TransformPoint(vertices[triangles[i + 2]]);
                    var bounds = new Bounds(a, Vector3.zero); bounds.Encapsulate(b); bounds.Encapsulate(c);
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    if (Mathf.Abs(normal.y) < .35f && openings.Any(o => o.Intersects(bounds))) { removed++; continue; }
                    kept.Add(triangles[i]); kept.Add(triangles[i + 1]); kept.Add(triangles[i + 2]);
                }
                mesh.SetTriangles(kept, sub);
            }
            if (GameObject.Find("Compact City Boundary") != null) SharedCompactCity.TrimCollision(mesh);
            if (storedMesh == null) AssetDatabase.CreateAsset(mesh, meshPath);
            else { EditorUtility.CopySerialized(mesh, storedMesh); Object.DestroyImmediate(mesh); mesh = storedMesh; EditorUtility.SetDirty(mesh); }
            Debug.Log("Opened " + removed + " barrier triangles for story connections.");
        }
        collider.sharedMesh = mesh;
        foreach (string name in new[] { "FirstPersonController", "Prometheus" })
        {
            var go = ts.First(t => t.name == name).gameObject;
            var modifier = go.GetComponent<NavMeshModifier>();
            if (modifier == null) modifier = go.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
        }
        Physics.SyncTransforms();
        var surface = ts.First(t => t.name == "Demo city navigation").GetComponent<NavMeshSurface>();
        var stored = AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Generated/DemoCity/CityNavigation.asset");
        surface.BuildNavMesh();
        var built = surface.navMeshData;
        EditorUtility.CopySerialized(built, stored);
        stored.name = "CityNavigation";
        surface.RemoveData(); surface.navMeshData = stored; surface.AddData();
        Object.DestroyImmediate(built);
        EditorUtility.SetDirty(stored);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("HellScape/City/Migrate main game to authored demo scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        main = SceneManager.GetSceneByName("MainGameScene");
        if (!main.isLoaded) main = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Additive);
        if (main.GetRootGameObjects().Any(g => g.name == "STATIC_MODELS")) throw new InvalidOperationException("Main scene already migrated.");
        original = main.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        var demo = SceneManager.GetSceneByName("demo_city_night");
        if (!demo.isLoaded) demo = EditorSceneManager.OpenScene(DemoPath, OpenSceneMode.Additive);
        foreach (var c in demo.GetRootGameObjects().Where(g => g.name == "Demo inspection").ToArray()) Object.DestroyImmediate(c);
        var staticRoot = demo.GetRootGameObjects().First(g => g.name == "STATIC_MODELS");
        var authored = staticRoot.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => (t.position, t.rotation, t.lossyScale));
        var demoSun = demo.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)).First(l => l.type == LightType.Directional);
        var roadCollider = staticRoot.GetComponentInChildren<MeshCollider>();
        var roadMaterial = staticRoot.GetComponentsInChildren<Renderer>(true).First(r => r.name == "city_part_demo_main1").sharedMaterials[0];

        // Disable the old streets as a whole. Keep their transforms for legacy references.
        var props = Find("Props");
        foreach (Transform t in props)
            if (t.name.StartsWith("сити исчерп") || t.name == "Street west (generated)" || t.name == "Structure_02" ||
                t.name == "конт на скетч" || t.name == "Tunnel 1" || t.name == "NavMesh Surface") t.gameObject.SetActive(false);
        foreach (var n in new[] { "Colldables", "Street barriers (generated)", "StreetLights", "Blood", "NoObstacleZones (Tunnel)" }) Off(n);
        var blockers = Find("RoadObstacles");
        foreach (Transform t in blockers) if (!t.name.StartsWith("RoadPoint")) t.gameObject.SetActive(false);
        foreach (var n in new[] { "Body", "Body (1)", "bus_namahoi", "bus_namahoi (1)", "copzombie_l_actisdato" })
        { var t = Find("HellWorld").Find(n); if (t != null) t.gameObject.SetActive(false); }

        var connections = Group("Demo city - story connections");
        // The home is in the gap north of the central avenue. The office is beyond
        // the north perimeter, connected by a service lane between authored plots.
        var homeDelta = new Vector3(-55.32f, 4f, 41.51f);
        Move("New Home (generated)", homeDelta);
        Move("Outside Flat 2B (street)", homeDelta);
        var officeDelta = new Vector3(115.65f, 6.79f, 202.32f);
        foreach (var n in new[] { "NewOfficeBuilding Entrance", "modular_door_8", "OfficeEntrance (City)", "CityReturnPoint" }) Move(n, officeDelta);
        Slab(connections.transform, "Home foundation", new Vector3(-31, -.1f, 63), new Vector3(23, 1.5f, 38), roadMaterial);
        Slab(connections.transform, "Home access", new Vector3(-38, .48f, 41), new Vector3(9, .5f, 32), roadMaterial);
        Slab(connections.transform, "Office forecourt", new Vector3(140, 2.7f, 144), new Vector3(47, .6f, 45), roadMaterial);
        Slab(connections.transform, "Office service lane", new Vector3(126.5f, 2.7f, 82), new Vector3(5, .6f, 112), roadMaterial);

        // Only open fence sections for the two access lanes; retain all demo buildings.
        foreach (var t in staticRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name == "fence_part70" || t.name == "fence_part82" || t.name == "fence_part67")) t.gameObject.SetActive(false);

        var sleep = Component<SleepSequence>();
        sleep.streetAnchor = null; // Different map: use the explicit arrival, not prologue-local coordinates.
        var wake = new Vector3(-38, 1.8f, 49);
        sleep.fallWakePoint.SetPositionAndRotation(wake, Quaternion.Euler(0, 180, 0));
        if (sleep.wakePoint != null) sleep.wakePoint.SetPositionAndRotation(wake, Quaternion.Euler(0, 180, 0));
        sleep.playerRoot.transform.SetPositionAndRotation(wake, Quaternion.Euler(0, 180, 0));
        sleep.sun.gameObject.SetActive(false);
        sleep.sun = demoSun;
        Find("Deer Zombie Trigger").position = new Vector3(-38, 2, 17);
        var prime = Find("HellWorld").Find("PrimeZombie"); if (prime != null) prime.position = new Vector3(-38, 1, -20);
        var civilians = Find("Civilians"); if (civilians != null) civilians.gameObject.SetActive(false);

        // Retain the original ending bridge and its encounter as a rigid assembly.
        var from = new Vector3(191.45f, -3.26f, -40.81f);
        var to = new Vector3(325, 5.7f, 200);
        var turn = Quaternion.Euler(0, 37.7f, 0);
        foreach (var n in new[] { "TEXTURES_FOR_SKETCH", "TEXTURES_FOR_SKETCH (1)", "BridgeEnding", "NoZombieZone (Bridge)", "HordeStop (Bridge)", "Horde line (bridge mouth)", "WaterBlock_50m" })
        { var t = Find(n); if (t != null) BridgePose(t, turn, from, to); }
        // This marker belongs to the office despite living under BridgeEnding.
        Find("Kessler bursts out (office door)").SetPositionAndRotation(new Vector3(12.4f, -3.85f, -47.3f) + officeDelta, Quaternion.identity);
        Slab(connections.transform, "Bridge approach", new Vector3(285, 5.45f, 200), new Vector3(90, .5f, 12), roadMaterial);
        Slab(connections.transform, "East road to bridge", new Vector3(245, 5.45f, 112.5f), new Vector3(8, .5f, 187), roadMaterial);
        foreach (var t in staticRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name == "tree_1 (12)" && Mathf.Abs(t.position.x - 248) < 12 && t.position.z > 25 && t.position.z < 55)) t.gameObject.SetActive(false);

        var van = Find("Prometheus");
        van.SetPositionAndRotation(new Vector3(118.5f, 3.15f, 145), Quaternion.Euler(0, 180, 0));
        var escape = original.Select(t => t.GetComponent<Checkpoint>()).First(c => c != null && c.name.Contains("Escape"));
        escape.spawn.SetPositionAndRotation(new Vector3(118.5f, 4.1f, 151), Quaternion.Euler(0, 180, 0));
        // Route follows the demo's avenues, then the retained ending bridge.
        var route = new[] {
            new Vector3(118.5f,3,145), new Vector3(126.5f,3,115), new Vector3(126.5f,3,90), new Vector3(126.5f,3,65),
            new Vector3(126.5f,3,40), new Vector3(120,3,25), new Vector3(90,0,25), new Vector3(50,0,25),
            new Vector3(10,0,25), new Vector3(-38,0,25), new Vector3(-38,0,0), new Vector3(-38,0,-40),
            new Vector3(-38,0,-82), new Vector3(0,0,-82), new Vector3(50,0,-82), new Vector3(90,0,-82),
            new Vector3(140,0,-82), new Vector3(190,0,-82), new Vector3(245,0,-82), new Vector3(245,0,-35),
            new Vector3(245,0,25), new Vector3(245,5.7f,200), new Vector3(325,5.7f,200), new Vector3(405,5.2f,200) };
        for (int i = 6; i <= 20; i++)
        { RaycastHit h; if (roadCollider.Raycast(new Ray(route[i] + Vector3.up * 100, Vector3.down), out h, 200)) route[i].y = h.point.y; }
        var horde = Component<HordeChase>();
        for (int i = 0; i < horde.road.Length; i++)
        {
            var p = route[Mathf.Min(i, route.Length - 1)]; horde.road[i].position = p;
            if (i + 1 < route.Length) horde.road[i].rotation = Quaternion.LookRotation(route[i + 1] - p);
        }
        var spawner = Component<ZombieSpawner>();
        for (int i = 0; i < spawner.spawnPoints.Length; i++) spawner.spawnPoints[i].position = route[6 + i % 15];
        int spawnIndex = 0;
        foreach (Transform t in Find("ZombieSpawnPoints")) t.position = route[6 + spawnIndex++ % 15];
        Find("RoadPoint Start (garage)").position = van.position;
        Find("Hell Wake (after the fall)").position = wake;
        var cams = Find("Escape Cameras");
        int shot = 0; foreach (Transform t in cams) { int k = Mathf.Min(5 + shot++ * 2, 20); t.position = route[k] + new Vector3(5, 5, 5); t.LookAt(route[Mathf.Min(k + 1, 23)]); }

        // Add collision to the demo's decorative buildings without changing their placement.
        int collisionCount = 0;
        foreach (var mf in staticRoot.GetComponentsInChildren<MeshFilter>(true))
        {
            string n = mf.name.ToLowerInvariant();
            if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null ||
                !(n.Contains("house") || n.StartsWith("office_building") || n.StartsWith("factory_building") || n.StartsWith("basement") || n.StartsWith("fence_part"))) continue;
            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh; collisionCount++;
        }
        // Merge INTO the demo, preserving its scene lighting and all original object poses.
        SceneManager.SetActiveScene(demo);
        EditorSceneManager.MergeScenes(main, demo);
        main = demo;
        var navGo = Group("Demo city navigation");
        var surface = navGo.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Volume;
        surface.center = new Vector3(100, 35, -45);
        surface.size = new Vector3(1200, 140, 650);
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.overrideVoxelSize = true; surface.voxelSize = .2f;
        surface.BuildNavMesh();
        if (!AssetDatabase.IsValidFolder("Assets/Generated/DemoCity"))
        { if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated"); AssetDatabase.CreateFolder("Assets/Generated", "DemoCity"); }
        AssetDatabase.CreateAsset(surface.navMeshData, "Assets/Generated/DemoCity/CityNavigation.asset");
        AssetDatabase.SaveAssets();
        int moved = authored.Count(p => p.Key.position != p.Value.Item1 || p.Key.rotation != p.Value.Item2 || p.Key.lossyScale != p.Value.Item3);
        if (moved != 0) throw new InvalidOperationException("Authored demo transforms changed: " + moved);
        EditorSceneManager.SaveScene(demo, MainPath);
        FinishNavigation();
        Debug.Log("Demo scene is now MainGameScene: " + authored.Count + " authored transforms unchanged; " + collisionCount + " collision meshes added. Original office, home, silo, footbridge/tunnel and ending bridge retained.");
    }
}
