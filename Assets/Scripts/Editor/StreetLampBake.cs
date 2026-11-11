using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// HellScape > Hell City > Bake Street Lamps (MainGameScene). The dark city's lamps, baked:
//   - each pole is rolled once (fixed seed) as dead / steady / flickering and saved in HellTrees.lampState
//   - steady lamps -> Baked lights (they light the whole city in the lightmaps, nothing at runtime)
//   - flickering lamps -> Realtime (HellTrees gives the nearest 40 a light, LightFlicker stutters them)
//   - dead poles -> light off, glowing head swapped for LampDead
//   - light probes round every pole so the van, Vane and people still catch the baked lamps
// then starts an async bake. Re-run it to re-roll (change Seed) or after moving lamps.
public static class StreetLampBake
{
    const int Seed = 1337;
    const string Folder = "Assets/Generated/CityLamps";

    [MenuItem("HellScape/Hell City/Bake Street Lamps")]
    public static void Run()
    {
        var trees = Object.FindFirstObjectByType<HellTrees>(FindObjectsInactive.Include);
        if (trees == null) { Debug.LogError("Bake Street Lamps: no HellTrees in " + EditorSceneManager.GetActiveScene().name); return; }
        var states = Roll(trees);
        Probes(trees);
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.ContributeGI) || r.receiveGI != ReceiveGI.Lightmaps || r.scaleInLightmap >= 1f) continue;
            var so = new SerializedObject(r); so.FindProperty("m_ScaleInLightmap").floatValue = 1f; so.ApplyModifiedProperties();
        }
        Settings();
        EditorSceneManager.MarkSceneDirty(trees.gameObject.scene);
        EditorSceneManager.SaveScene(trees.gameObject.scene);
        Debug.Log($"[LampBake] dead {states[0]}, steady/baked {states[1]}, flickering {states[2]}; baking...");
        Lightmapping.BakeAsync();
    }

    static int[] Roll(HellTrees trees)
    {
        var rng = new System.Random(Seed);
        var pole = new Dictionary<Transform, byte>();
        var state = new byte[trees.lamps.Length];
        var count = new int[3];
        Material dead = DeadBulb(trees);
        for (int i = 0; i < trees.lamps.Length; i++)
        {
            var l = trees.lamps[i];
            if (l == null) continue;
            var p = l.transform.parent != null ? l.transform.parent : l.transform;
            if (!pole.TryGetValue(p, out byte s))
            {
                s = rng.NextDouble() >= trees.lampWorking ? (byte)0 : rng.NextDouble() < trees.lampFlickering ? (byte)2 : (byte)1;
                pole[p] = s;
                Bulb(p, s == 0 ? dead : null);
            }
            state[i] = s; count[s]++;
            Undo.RecordObject(l, "Bake lamps");
            l.lightmapBakeType = s == 1 ? LightmapBakeType.Baked : LightmapBakeType.Realtime;
            l.enabled = s == 1;   // (the bake only sees enabled lights; HellTrees switches the flickering ones at runtime)
            l.bounceIntensity = 1f;
            var f = l.GetComponent<LightFlicker>();
            if (s == 2 && f == null) { f = Undo.AddComponent<LightFlicker>(l.gameObject); f.calmTime = new Vector2(0.8f, 9f); f.burstTime = new Vector2(0.15f, 0.7f); f.minBrightness = 0.05f; }
            EditorUtility.SetDirty(l);
        }
        Undo.RecordObject(trees, "Bake lamps");
        trees.lampState = state;
        EditorUtility.SetDirty(trees);
        return count;
    }

    static Material DeadBulb(HellTrees trees)
    {
        string path = Folder + "/LampDead.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Generated", "CityLamps");
        var src = trees.lamps.Where(l => l != null).Select(l => l.transform.parent ? l.transform.parent.GetComponent<MeshRenderer>() : null)
            .Where(r => r != null).SelectMany(r => r.sharedMaterials).FirstOrDefault(x => x != null && (x.name.StartsWith("LampHot") || x.name.EndsWith("_light")));
        m = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.05f));
        m.SetColor("_EmissionColor", Color.black); m.DisableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // dead -> LampDead; working -> back to a hot bulb if an earlier roll killed it
    static void Bulb(Transform pole, Material dead)
    {
        var mr = pole.GetComponent<MeshRenderer>();
        if (mr == null) return;
        var mats = mr.sharedMaterials;
        bool changed = false;
        for (int k = 0; k < mats.Length; k++)
        {
            var m = mats[k];
            if (m == null) continue;
            bool bulb = m.name.StartsWith("LampHot") || m.name.EndsWith("_light") || m.name == "LampDead";
            if (!bulb) continue;
            if (dead != null) { mats[k] = dead; changed = true; }
            else if (m.name == "LampDead") { var hot = AssetDatabase.FindAssets("LampHot0 t:Material").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).FirstOrDefault(); if (hot != null) { mats[k] = hot; changed = true; } }
        }
        if (!changed) return;
        Undo.RecordObject(mr, "Bake lamps");
        mr.sharedMaterials = mats;
    }

    // a ring of probes on the ground and at head/van-roof height round every pole, plus one under the lamp head
    static void Probes(HellTrees trees)
    {
        const string name = "Street lamp probes (generated)";
        var old = GameObject.Find(name);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Bake lamps");
        var pts = new List<Vector3>();
        var seen = new HashSet<Transform>();
        foreach (var l in trees.lamps)
        {
            if (l == null) continue;
            var p = l.transform.parent != null ? l.transform.parent : l.transform;
            if (!seen.Add(p)) continue;
            Vector3 top = l.transform.position;
            float ground = top.y - 6f;
            foreach (var h in Physics.RaycastAll(top, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (!h.collider.transform.IsChildOf(p)) { ground = h.point.y; break; }
            Vector3 g = new Vector3(top.x, ground, top.z);
            pts.Add(new Vector3(top.x, Mathf.Lerp(ground, top.y, 0.7f), top.z));
            foreach (var off in new[] { Vector3.zero, new Vector3(5, 0, 0), new Vector3(-5, 0, 0), new Vector3(0, 0, 5), new Vector3(0, 0, -5) })
            {
                pts.Add(g + off + Vector3.up * 0.6f);
                pts.Add(g + off + Vector3.up * 3f);
            }
        }
        go.AddComponent<LightProbeGroup>().probePositions = pts.ToArray();
        Debug.Log($"[LampBake] {pts.Count} light probes round {seen.Count} poles");
    }

    static void Settings()
    {
        // always this scene's own asset: "Game Realtime Lighting" is shared with OfficeSilo, which must stay realtime
        string path = Folder + "/MainGameScene Lighting.lighting";
        var s = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
        if (s == null) { s = new LightingSettings(); AssetDatabase.CreateAsset(s, path); }
        Lightmapping.lightingSettings = s;
        s.bakedGI = true;
        s.realtimeGI = false;
        s.mixedBakeMode = MixedLightingMode.IndirectOnly;
        s.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
        s.lightmapResolution = 3f;          // texels per metre (with every renderer's scale in lightmap at 1: the city's own 0.1-0.2 left a lamp pool 2 texels wide)
        s.lightmapMaxSize = 2048;
        s.directSampleCount = 16;
        s.indirectSampleCount = 128;
        s.environmentSampleCount = 16;
        s.maxBounces = 2;
        s.ao = false;
        s.lightmapCompression = LightmapCompression.NormalQuality;
        s.filteringMode = LightingSettings.FilterMode.Auto;
        s.autoGenerate = false;
        EditorUtility.SetDirty(s);
    }
}
