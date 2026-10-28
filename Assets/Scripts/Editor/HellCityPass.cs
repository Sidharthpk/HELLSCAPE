using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// HellScape > Hell City > Burning Trees, Lamps And Look (MainGameScene open): the planter trees become dead trees, some burning (TreeFlame crown
// + flickering light), the pavement trees are deleted, some of the street lamps work (orange / yellow / red spot, stuttering), the rest are dark; the
// fires and lamps are Mixed lights so their bounce is baked; softer bloom, brighter ambient. Safe to run again.
// Then HellScape > Hell City > Bake Hell City Lighting (lightmaps + baked reflection probes).
public static class HellCityPass
{
    const string Dir = "Assets/Generated/HellCity";
    const string RootName = "Burning Trees";
    const string DeadPack = "Assets/Perishables/DeadTreePack/DeadTreePack.fbx";
    const double LampChance = 0.30;
    const double BurnChance = 0.3;      // of the planter trees, this share burns; the others are plain dead trees
    static readonly Color[] LampColours = { new Color(1f, 0.5f, 0.12f), new Color(1f, 0.72f, 0.22f), new Color(1f, 0.3f, 0.08f) };

    [MenuItem("HellScape/Hell City/Burning Trees, Lamps And Look")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Generated", "HellCity");
        foreach (var g in AssetDatabase.FindAssets("t:Mesh", new[] { Dir })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));
        var player = GameObject.Find("FirstPersonController");
        var spawn = player != null ? player.transform.position : Vector3.zero;
        var rng = new System.Random(11);
        int trees = BurnTrees(scene, spawn, rng), lamps = LightLamps(scene, spawn, rng);
        CharHedges(scene);
        Look();
        Probes(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Hell city pass: " + trees + " burning trees, " + lamps + " lamp poles lit. Bake the lighting next.");
    }

    [MenuItem("HellScape/Hell City/Dead Trees In Planters")]
    public static void DeadTrees()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var player = GameObject.Find("FirstPersonController");
        int n = BurnTrees(scene, player != null ? player.transform.position : Vector3.zero, new System.Random(11));
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Dead trees in planters: " + n + ". Bake the lighting next.");
    }

    static int BurnTrees(UnityEngine.SceneManagement.Scene scene, Vector3 spawn, System.Random rng)
    {
        var all = Resources.FindObjectsOfTypeAll<MeshRenderer>()
            .Where(r => r.gameObject.scene == scene && r.name.StartsWith("tree_") && r.GetComponent<MeshFilter>() != null).ToList();
        if (all.Count == 0) return 0;   // (already done: the live trees are deleted, the dead ones stay as they are)
        foreach (var o in scene.GetRootGameObjects().Where(g => g.name == RootName).ToList()) Object.DestroyImmediate(o);
        foreach (var r in all) r.gameObject.SetActive(true);   // (inactive bounds are not reliable)
        // only the trees in the raised planters (the basement_* blocks) become dead trees; the ones on the open pavement (TREES) just go
        foreach (var r in all.Where(r => !InPlanter(r)).ToList()) if (r != null) Remove(r.gameObject);
        var trees = all.Where(r => r != null).OrderBy(r => (r.bounds.center - spawn).sqrMagnitude).ToList();   // (nearest first: the first two always burn, so you see it at the start)
        var variants = AssetDatabase.LoadAssetAtPath<GameObject>(DeadPack).GetComponentsInChildren<MeshRenderer>(true).Select(r => r.gameObject).ToList();
        var flameMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/HellTrees/TreeFlame.mat");
        var root = new GameObject(RootName);
        int n = 0;
        var burning = new HashSet<int> { 0, 1 };   // (the two nearest the start always burn)
        for (int i = 2; i < trees.Count; i++) if (rng.NextDouble() < BurnChance) burning.Add(i);
        for (int i = 0; i < trees.Count; i++)
        {
            bool burns = burning.Contains(i);
            var mr = trees[i];
            if (mr == null) continue;   // (went with another tree's prefab)
            var b = mr.bounds;
            var foot = new Vector3(b.center.x, b.min.y, b.center.z);
            float h = b.size.y;
            var fire = new GameObject("Fire (" + mr.name + ")");
            fire.transform.SetParent(root.transform, false);

            // a dead tree from the pack stands where the live one was, scaled to its height (the live one is deleted)
            var v = variants[rng.Next(variants.Count)];
            var trunk = Object.Instantiate(v, fire.transform);
            trunk.name = v.name;
            var tr = trunk.GetComponent<MeshRenderer>();
            trunk.transform.localScale *= h / tr.bounds.size.y;
            var tb = tr.bounds;
            trunk.transform.localPosition -= new Vector3(tb.center.x, tb.min.y, tb.center.z);   // (the pack mesh is off its pivot: stand it on the fire origin)
            FitCollider(fire);   // (the trunk blocks you, as the old tree did)
            Remove(mr.gameObject);

            n++;
            Mesh crown = burns ? HellTreeBuilder.Crown(trunk.GetComponent<MeshFilter>().sharedMesh, trunk.transform.localToWorldMatrix, h, rng) : null;   // (fire is at the origin here: its own space)
            fire.transform.SetPositionAndRotation(foot, Quaternion.Euler(0f, rng.Next(360), 0f));
            if (!burns) continue;   // (a plain dead tree: trunk and collider only)
            crown.name = "Crown " + n;
            AssetDatabase.CreateAsset(crown, Dir + "/" + crown.name + ".asset");

            var flames = new GameObject("Flames");
            flames.transform.SetParent(fire.transform, false);
            flames.AddComponent<MeshFilter>().sharedMesh = crown;
            var fr = flames.AddComponent<MeshRenderer>();
            fr.sharedMaterial = flameMat;
            fr.shadowCastingMode = ShadowCastingMode.Off; fr.receiveShadows = false;

            var lg = new GameObject("Fire light");
            lg.transform.SetParent(fire.transform, false);
            lg.transform.localPosition = new Vector3(0f, h * 0.8f, 0f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.45f, 0.12f); l.range = 45f; l.intensity = 130f;
            l.shadows = LightShadows.None; l.lightmapBakeType = LightmapBakeType.Mixed;
            lg.AddComponent<FireFlicker>();
        }
        return n;
    }

    [MenuItem("HellScape/Hell City/Fit Dead Tree Colliders")]
    public static void FitColliders()
    {
        var root = GameObject.Find(RootName);
        if (root == null) return;
        foreach (Transform fire in root.transform) FitCollider(fire.gameObject);
        EditorSceneManager.MarkSceneDirty(root.scene);
    }

    // a capsule on the trunk where it leaves the ground (a leaning tree's bounds centre is not its trunk), as thick as the trunk there
    static void FitCollider(GameObject fire)
    {
        var mf = fire.GetComponentsInChildren<MeshFilter>().First(f => f.name != "Flames");
        var m = mf.transform.localToWorldMatrix;
        var verts = mf.sharedMesh.vertices.Select(v => fire.transform.InverseTransformPoint(m.MultiplyPoint3x4(v))).ToList();
        float min = verts.Min(v => v.y), top = verts.Max(v => v.y);
        var ring = verts.Where(v => v.y > min + 0.4f && v.y < min + 1.6f).ToList();   // (above the root flare, below the first branches)
        if (ring.Count == 0) ring = verts.Where(v => v.y < min + 1.6f).ToList();
        var c = new Vector3(ring.Average(v => v.x), 0f, ring.Average(v => v.z));
        float r = Mathf.Clamp(ring.Max(v => new Vector2(v.x - c.x, v.z - c.z).magnitude), 0.25f, 0.8f);
        if (!fire.TryGetComponent<CapsuleCollider>(out var col)) col = fire.AddComponent<CapsuleCollider>();
        col.radius = r; col.height = Mathf.Min(top - min, 6f); col.center = new Vector3(c.x, min + col.height / 2f, c.z);
    }

    // a tree is mostly its own prefab (delete the instance); inside a building prefab the building is unpacked so the tree can go
    static void Remove(GameObject g)
    {
        var outer = PrefabUtility.GetOutermostPrefabInstanceRoot(g);
        if (outer != null && outer.name.StartsWith("tree_")) g = outer;
        else if (outer != null) PrefabUtility.UnpackPrefabInstance(outer, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Object.DestroyImmediate(g);
    }

    static bool InPlanter(MeshRenderer r)
    {
        for (var t = r.transform.parent; t != null; t = t.parent) if (t.name.StartsWith("basement_")) return true;
        return false;
    }

    static int LightLamps(UnityEngine.SceneManagement.Scene scene, Vector3 spawn, System.Random rng)
    {
        var poles = Resources.FindObjectsOfTypeAll<Transform>()
            .Where(t => t.gameObject.scene == scene && t.name.StartsWith("lamp_pole") && t.GetComponentInChildren<Light>(true) != null)
            .OrderBy(t => (t.position - spawn).sqrMagnitude).ToList();
        var hot = LampColours.Select((c, k) => LampMaterial("LampHot" + k, c * 6f)).ToArray();
        var off = LampMaterial("LampOff", Color.black);
        int lit = 0;
        for (int i = 0; i < poles.Count; i++)
        {
            bool on = i < 3 || rng.NextDouble() < LampChance;   // (the three nearest always work)
            int colour = rng.Next(LampColours.Length);
            var mr = poles[i].GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var mats = mr.sharedMaterials;
                for (int k = 0; k < mats.Length; k++) if (mats[k] != null && mats[k].name.EndsWith("_light")) mats[k] = on ? hot[colour] : off;
                mr.sharedMaterials = mats;
            }
            foreach (var l in poles[i].GetComponentsInChildren<Light>(true))
            {
                bool small = poles[i].name.StartsWith("lamp_pole_small");
                l.enabled = on;
                if (!on) continue;
                l.type = LightType.Spot; l.spotAngle = 105f; l.innerSpotAngle = 40f;
                l.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // straight down
                l.color = LampColours[colour]; l.range = small ? 22f : 34f; l.intensity = small ? 40f : 120f;
                l.shadows = LightShadows.None; l.lightmapBakeType = LightmapBakeType.Mixed;
                var f = l.GetComponent<LightFlicker>() ?? l.gameObject.AddComponent<LightFlicker>();
                f.calmTime = new Vector2(0.6f, 4f); f.burstTime = new Vector2(0.15f, 0.5f); f.minBrightness = 0.15f;
                EditorUtility.SetDirty(l);
            }
            if (on) lit++;
        }
        return lit;
    }

    // HellScape > Hell City > Path Lamps: every lamp pole within PathReach of the playthrough (spawn -> office door -> van escape road) is lit,
    // a downward spot with a visible beam (LightShaft, the office's cone) and the stutter. Lamps off the path are left alone; fog/look untouched.
    const float PathReach = 14f;

    [MenuItem("HellScape/Hell City/Path Lamps (Beam + Flicker)")]
    public static void PathLamps()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var path = new List<Vector3>();
        var player = GameObject.Find("FirstPersonController"); if (player != null) path.Add(player.transform.position);
        var door = GameObject.Find("OfficeDoor"); if (door != null) path.Add(door.transform.position);
        var road = GameObject.Find("RoadObstacles");
        if (road != null) foreach (Transform t in road.transform) path.Add(t.position);
        if (path.Count < 2) { Debug.LogError("Path Lamps: no FirstPersonController / OfficeDoor / RoadObstacles in " + scene.name); return; }

        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Generated/LightShaft/Cone.asset");
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/LightShaft/LightShaft.mat");
        var hot = LampColours.Select((c, k) => LampMaterial("LampHot" + k, c * 6f)).ToArray();
        var rng = new System.Random(7);
        var poles = Resources.FindObjectsOfTypeAll<Transform>()
            .Where(t => t.gameObject.scene == scene && t.name.StartsWith("lamp_pole") && t.GetComponentInChildren<Light>(true) != null && NearPath(t.position, path)).ToList();
        Physics.queriesHitTriggers = false;
        foreach (var pole in poles)
        {
            int colour = rng.Next(LampColours.Length);
            var mr = pole.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var mats = mr.sharedMaterials;
                for (int k = 0; k < mats.Length; k++) if (mats[k] != null && (mats[k].name.EndsWith("_light") || mats[k].name.StartsWith("LampOff") || mats[k].name.StartsWith("LampHot"))) mats[k] = hot[colour];
                mr.sharedMaterials = mats;
            }
            bool small = pole.name.StartsWith("lamp_pole_small");
            foreach (var l in pole.GetComponentsInChildren<Light>(true))
            {
                Undo.RecordObject(l, "Path lamps");
                l.enabled = true;
                l.type = LightType.Spot; l.spotAngle = 80f; l.innerSpotAngle = 35f;
                l.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var ground = Physics.RaycastAll(l.transform.position, Vector3.down, 25f).Where(x => !x.collider.transform.IsChildOf(pole)).OrderBy(x => x.distance).ToList();   // (not the pole's own collider)
                float h = ground.Count > 0 ? ground[0].distance : (small ? 4f : 8f);
                l.color = LampColours[colour]; l.range = h * 2.2f; l.intensity = small ? 60f : 160f;
                l.shadows = LightShadows.None; l.lightmapBakeType = LightmapBakeType.Mixed;
                var f = l.GetComponent<LightFlicker>() ?? l.gameObject.AddComponent<LightFlicker>();
                f.calmTime = new Vector2(0.6f, 4f); f.burstTime = new Vector2(0.15f, 0.5f); f.minBrightness = 0.05f;

                var old = l.transform.Find("Shaft"); if (old != null) Object.DestroyImmediate(old.gameObject);
                var go = new GameObject("Shaft");
                go.transform.SetParent(l.transform, false);
                go.transform.SetPositionAndRotation(l.transform.position, Quaternion.identity);
                float radius = Mathf.Tan(40f * Mathf.Deg2Rad) * h * .8f;
                var s = l.transform.lossyScale;
                go.transform.localScale = new Vector3(radius / s.x, h / s.y, radius / s.z);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var smr = go.AddComponent<MeshRenderer>();
                smr.sharedMaterial = mat;
                smr.shadowCastingMode = ShadowCastingMode.Off; smr.receiveShadows = false;
                var shaft = go.AddComponent<LightShaft>(); shaft.lamp = l; shaft.strength = .4f;
                EditorUtility.SetDirty(l);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Path lamps: " + poles.Count + " poles along the playthrough lit with a beam + flicker.");
    }

    static bool NearPath(Vector3 p, List<Vector3> path)
    {
        var q = new Vector2(p.x, p.z);
        for (int i = 0; i + 1 < path.Count; i++)
        {
            Vector2 a = new Vector2(path[i].x, path[i].z), b = new Vector2(path[i + 1].x, path[i + 1].z), ab = b - a;
            float t = ab.sqrMagnitude < .01f ? 0f : Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
            if ((a + ab * t - q).magnitude < PathReach) return true;
        }
        return false;
    }

    // the planter hedges burnt black: this scene's hedges get a charred copy of the vegetation material (the shared one stays green for the other scenes)
    static void CharHedges(UnityEngine.SceneManagement.Scene scene)
    {
        var hedges = Resources.FindObjectsOfTypeAll<MeshRenderer>().Where(r => r.gameObject.scene == scene && r.name.StartsWith("hedge")).ToList();
        Material burnt = null;
        foreach (var r in hedges)
        {
            var mats = r.sharedMaterials;
            for (int k = 0; k < mats.Length; k++)
            {
                if (mats[k] == null || !mats[k].name.StartsWith("vegetation")) continue;
                if (burnt == null)
                {
                    string path = Dir + "/HedgeBurnt.mat";
                    burnt = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (burnt == null) { burnt = new Material(mats[k]); AssetDatabase.CreateAsset(burnt, path); }
                    burnt.SetColor("_BaseColor", new Color(.1f, .06f, .045f));
                    EditorUtility.SetDirty(burnt);
                }
                mats[k] = burnt;
            }
            r.sharedMaterials = mats;
        }
    }

    static Material LampMaterial(string name, Color emission)
    {
        string path = Dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", emission.maxColorComponent > 0f ? new Color(emission.r, emission.g, emission.b) / emission.maxColorComponent : Color.black);
        m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        m.SetColor("_EmissionColor", emission);
        EditorUtility.SetDirty(m);
        return m;
    }

    // brighter, redder ambient; softer bloom; fog lifted so the fires have something to light
    static void Look()
    {
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.7f;
        RenderSettings.fogColor = new Color(0.3f, 0.07f, 0.04f);
        RenderSettings.fogDensity = 0.012f;
        var fill = GameObject.Find("City night fill");
        if (fill != null) { var l = fill.GetComponent<Light>(); l.color = new Color(1f, 0.5f, 0.38f); l.intensity = 1.1f; }

        var city = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Generated/CitySurfaces/CityAtmosphere.asset");
        if (city.TryGet(out Bloom bloom)) { bloom.threshold.Override(1.6f); bloom.intensity.Override(0.1f); bloom.scatter.Override(0.4f); bloom.highQualityFiltering.Override(false); EditorUtility.SetDirty(bloom); }
        if (city.TryGet(out ColorAdjustments ca)) { ca.postExposure.Override(0.8f); EditorUtility.SetDirty(ca); }
        EditorUtility.SetDirty(city);
    }

    // the city's reflection probes: baked (they catch the fires and the lamps), sharper
    static void Probes(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var p in Resources.FindObjectsOfTypeAll<ReflectionProbe>().Where(p => p.gameObject.scene == scene))
        {
            p.mode = ReflectionProbeMode.Baked; p.resolution = 256; p.hdr = true; p.intensity = 1.2f;
            EditorUtility.SetDirty(p);
        }
    }

    [MenuItem("HellScape/Hell City/Bake Hell City Lighting")]
    public static void Bake()
    {
        Lightmapping.Clear();
        Lightmapping.BakeAsync();
        Debug.Log("Hell city bake started (lightmaps + reflection probes).");
    }
}
