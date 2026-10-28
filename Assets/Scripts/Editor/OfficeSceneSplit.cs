using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Copies MainGameScene to Assets/Scenes/OfficeSilo.unity and strips the city out of the copy, so the office + silo can be
// lit and post-processed on their own. MainGameScene is not touched. Teleporter destinations that lived inside the
// stripped roots (the way back to the city) are kept as bare markers at the scene root so nothing null-refs.
// Safe to re-run: it recopies from MainGameScene every time.
public static class OfficeSceneSplit
{
    const string Main = "Assets/Scenes/MainGameScene.unity", Copy = "Assets/Scenes/OfficeSilo.unity";

    static readonly string[] CityRoots =
    {
        "STATIC_MODELS", "DayWorld", "HellWorld", "Props", "Colldables", "RoadObstacles", "Legacy story references (no scenery)",
        "WaterBlock_50m", "NoZombieZone (Bridge)", "ZombieSpawnPoints", "ZombieSpawner", "BridgeEnding", "Harlow_Clinic", "basement_big",
        "Red Flat (generated)", "Demo city - story connections", "Demo city navigation", "Compact City Boundary", "City night fill",
        "City Atmosphere", "Escape route markers", "Escape Cameras", "Hell mist", "Car pack cars", "NewOfficeBuilding Entrance", "modular_door_8",
    };

    static readonly string[] OfficeRoots =
    {
        "Office Ground Floor", "Office Ground Floor (Kessler & Vane, old)", "Cake Out Sub", "Sketchfab_2023_11_11_16_15_44",
        "4bfaca4c4a794e9393a413e88dd5611a.fbx", "Door", "Door (1)", "Door (2)", "Door (3)", "Door (4)", "Door (5)", "Door (6)", "Door (7)", "Door (8)",
        "EyeBalls", "control room 3D Model Avatar of amogusstrikesback2 amogusstrikes", "Sketchfab_Models", "Reception front glazing", "Fuse",
        "double_door", "Office Look", "BloodPool_low", "WaterBlock_50m (1)", "bridge_Metal", "Tunnel 1", "tunnel",
    };
    static readonly string[] OfficeActParts = { "Office", "Silo", "SiloComplex", "KesslerTunnel", "Flashback (CCTV)" };

    // Run the split first (it copies from MainGameScene), then this: the city scene keeps its street-side door, which now
    // loads OfficeSilo, plus the escape bits (blood wave, horde, checkpoints) that play out in the city.
    [MenuItem("HellScape/Office/Strip Office + Silo From MainGameScene")]
    public static void Strip()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Stop Play first."); return; }
        if (!System.IO.File.Exists(Copy)) { Debug.LogError("Run the split first: " + Copy + " is missing."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(Main, OpenSceneMode.Single);

        var doomed = scene.GetRootGameObjects().Where(r => OfficeRoots.Contains(r.name)).ToList();
        int n = doomed.Count;
        foreach (var r in doomed) Object.DestroyImmediate(r);
        var act = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "OfficeAct");
        if (act != null)
            foreach (var part in OfficeActParts)
            {
                var t = act.transform.Find(part);
                if (t != null) { Object.DestroyImmediate(t.gameObject); n++; }
            }

        foreach (var tp in Object.FindObjectsByType<Teleporter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (tp.name == "OfficeDoor") { tp.loadScene = "OfficeSilo"; tp.arriveAt = "OfficeArrival"; tp.arriveCheckpoint = "Checkpoint - Office entrance"; }

        var inBuild = EditorBuildSettings.scenes.ToList();   // LoadScene("OfficeSilo") needs it listed
        if (!inBuild.Any(s => s.path == Copy)) { inBuild.Add(new EditorBuildSettingsScene(Copy, true)); EditorBuildSettings.scenes = inBuild.ToArray(); }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("MainGameScene: removed " + n + " office/silo objects; OfficeDoor now loads OfficeSilo");
    }

    [MenuItem("HellScape/Office/Split Office + Silo Into Its Own Scene")]
    public static void Split()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Stop Play first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!System.IO.File.ReadAllText(Main).Contains("m_Name: Office Ground Floor")) { Debug.LogError("MainGameScene no longer has the office (already stripped): nothing to copy."); return; }
        AssetDatabase.DeleteAsset(Copy);
        if (!AssetDatabase.CopyAsset(Main, Copy)) { Debug.LogError("Could not copy " + Main); return; }
        var scene = EditorSceneManager.OpenScene(Copy, OpenSceneMode.Single);

        var roots = scene.GetRootGameObjects();
        var doomed = roots.Where(r => CityRoots.Contains(r.name)).ToList();

        // keep the places the doors lead to
        int kept = 0;
        foreach (var tp in Object.FindObjectsByType<Teleporter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var d = tp.destination;
            if (d == null || d.parent == null || !doomed.Any(r => d.IsChildOf(r.transform))) continue;
            d.SetParent(null, true); kept++;
        }
        foreach (var r in doomed) if (r != null) Object.DestroyImmediate(r);

        // no title (START would load the Prologue); SleepSequence sees the scene name and skips the street wake-up
        var title = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "TitleScreen");
        if (title != null) Object.DestroyImmediate(title);

        foreach (var cams in Object.FindObjectsByType<FixedCameras>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(cams);   // its road points were in the city (disabled isn't enough: Awake still runs)

        // start in the office reception
        var arrival = GameObject.Find("OfficeArrival");
        var fpc = Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
        if (arrival != null && fpc != null) fpc.transform.SetPositionAndRotation(arrival.transform.position, arrival.transform.rotation);

        // the ways out lead back to the city scene
        foreach (var tp in Object.FindObjectsByType<Teleporter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tp.name != "ExitDoor" && tp.name != "SiloExit") continue;
            tp.loadScene = "MainGameScene"; tp.arriveAt = "CityReturnPoint";
            tp.arriveCheckpoint = tp.name == "SiloExit" ? "Checkpoint - Escape" : "";
        }

        // dying in the office restarts at its front door, not at the city marker
        var inFront = GameObject.Find("OfficeArrival");
        foreach (var c in Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.name == "Checkpoint - Office entrance" && inFront != null) c.spawn = inFront.transform;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("OfficeSilo.unity: removed " + doomed.Count + " city roots, kept " + kept + " door destinations, " + scene.GetRootGameObjects().Length + " roots left");
    }
}
