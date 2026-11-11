using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// HellScape > Hell City > Build Burning Dead Trees (run with MainGameScene open):
//   every live city tree gets a dead tree from the Perishables pack at its spot, scaled to the same height, with a
//   flame on every branch tip across its whole crown (TreeFlame shader) and a flickering light. Everything sits in
//   one root under HellWorld (HellTrees), so the live trees only go away in the red city, and the red city's only
//   light is the fires: the sun is replaced (and off in hell), the night fill and the city's lamps are muted there.
//   Also: hell sky exposure, fog, post stack (bloom, depth of field, chromatic aberration, vignette, colour
//   adjustments, no flares), the screen-space outline, and the pixel light count. Safe to run again.
public static class HellTreeBuilder
{
    const string Pack = "Assets/Perishables/DeadTreePack/DeadTreePack.fbx";
    const string Dir = "Assets/Generated/HellTrees";
    const string RootName = "Burning Dead Trees";
    const double BurnChance = 0.55;   // the rest stand dead but unlit
    const int FlamesPerTree = 30;

    [MenuItem("HellScape/Hell City/Build Burning Dead Trees")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var hell = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "HellWorld");
        if (hell == null) { Debug.LogError("No HellWorld in " + scene.name); return; }
        bool wasActive = hell.activeSelf;
        hell.SetActive(true);   // (bounds of inactive renderers are not reliable)
        var old = hell.transform.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var originals = Resources.FindObjectsOfTypeAll<Transform>()
            .Where(t => t.gameObject.scene == scene && t.name.StartsWith("tree_") && t.GetComponent<MeshRenderer>() != null)
            .Select(t => t.gameObject).ToList();
        var variants = AssetDatabase.LoadAssetAtPath<GameObject>(Pack).GetComponentsInChildren<MeshRenderer>(true).Select(r => r.gameObject).ToList();

        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Generated", "HellTrees");
        foreach (var g in AssetDatabase.FindAssets("t:Mesh", new[] { Dir })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));   // (old flame meshes)
        var flameMat = FlameMaterial();
        var crowns = new Dictionary<string, Mesh>();

        var root = new GameObject(RootName);
        root.transform.SetParent(hell.transform, false);
        var lights = new List<Light>();
        var rng = new System.Random(7);

        foreach (var orig in originals)
        {
            var ob = orig.GetComponent<MeshRenderer>().bounds;   // (the flat static trees: the world box is the tree's size)
            var tree = new GameObject("Dead tree (" + orig.name + ")");
            tree.transform.SetParent(root.transform, false);

            // trunk: a pack tree stood upright at the origin, scaled to the old tree's height
            var v = variants[rng.Next(variants.Count)];
            var trunk = Object.Instantiate(v, tree.transform);
            trunk.name = v.name;
            var mr = trunk.GetComponent<MeshRenderer>();
            trunk.transform.localPosition = Vector3.zero;
            float s = ob.size.y / mr.bounds.size.y;
            trunk.transform.localScale *= s;
            var b = mr.bounds;
            trunk.transform.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z);
            float h = ob.size.y;

            // the trunk blocks you (the old box colliders were walls)
            var col = tree.AddComponent<CapsuleCollider>();
            col.radius = 0.5f; col.height = Mathf.Min(h, 6f); col.center = new Vector3(0f, col.height / 2f, 0f);

            // a flame on every branch tip: one combined mesh per pack tree and height, in the tree's own space
            string key = v.name + "_" + h.ToString("F1");
            if (!crowns.TryGetValue(key, out var crown))
            {
                crown = Crown(trunk.GetComponent<MeshFilter>().sharedMesh, trunk.transform.localToWorldMatrix, h, rng);   // (the tree sits at the origin: this is its own space)
                crown.name = "Crown " + key;
                AssetDatabase.CreateAsset(crown, Dir + "/" + crown.name + ".asset");
                crowns[key] = crown;
            }
            tree.transform.SetPositionAndRotation(new Vector3(ob.center.x, ob.min.y, ob.center.z), Quaternion.Euler(0f, rng.Next(360), 0f));
            if (rng.NextDouble() > BurnChance) continue;   // a bare dead tree: no flames, no light

            var flames = new GameObject("Flames");
            flames.transform.SetParent(tree.transform, false);
            flames.AddComponent<MeshFilter>().sharedMesh = crown;
            var fr = flames.AddComponent<MeshRenderer>();
            fr.sharedMaterial = flameMat;
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fr.receiveShadows = false;

            var lg = new GameObject("Fire light");
            lg.transform.SetParent(tree.transform, false);
            lg.transform.localPosition = new Vector3(0f, h * 0.85f, 0f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.range = 40f; l.shadows = LightShadows.None; l.enabled = false;
            lights.Add(l);
        }

        var ht = root.AddComponent<HellTrees>();
        ht.originals = originals.ToArray();
        ht.lights = lights.ToArray();
        ht.intensity = 40f;
        ht.fireClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Fire Sound Effect (no copyright) 🔥🔥.mp3");

        var sun = ReplaceSun(scene);
        ht.mute = MuteList(scene, sun, lights);
        Look(scene, hell, sun);

        hell.SetActive(wasActive);
        ApplySettings();
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Burning dead trees: " + originals.Count + " trees, " + lights.Count + " fires, " + ht.mute.Length + " lights muted in hell. Bake the lighting, then save the scene.");
    }

    // flames at the branch tips: the highest points of the crown, spread out (farthest-point sampling over the vertices)
    internal static Mesh Crown(Mesh src, Matrix4x4 m, float h, System.Random rng)   // (m: mesh space -> the tree's own space, origin at its foot)
    {
        var pts = src.vertices.Select(p => m.MultiplyPoint3x4(p)).Where(p => p.y > h * 0.4f).ToList();
        var tips = new List<Vector3> { pts.OrderByDescending(p => p.y).First() };
        while (tips.Count < FlamesPerTree)
        {
            Vector3 best = default; float bestD = 0f;
            foreach (var p in pts)
            {
                float d = tips.Min(t => (t - p).sqrMagnitude);
                if (d > bestD) { bestD = d; best = p; }
            }
            if (bestD < 0.35f * 0.35f) break;
            tips.Add(best);
        }

        var vs = new List<Vector3>(); var uvs = new List<Vector2>(); var seeds = new List<Vector2>(); var tris = new List<int>();
        foreach (var tip in tips)
        {
            float fh = h * (0.2f + 0.12f * (float)rng.NextDouble());
            float fw = fh * 0.9f;
            var seed = new Vector2((float)rng.NextDouble(), 0f);
            float yaw = (float)rng.NextDouble() * 180f;
            for (int k = 0; k < 2; k++)
            {
                var d = Quaternion.Euler(0f, yaw + k * 90f, 0f) * Vector3.right * fw * 0.5f;
                var foot = tip + Vector3.down * fh * 0.3f;
                int o = vs.Count;
                vs.AddRange(new[] { foot - d, foot + d, foot + d + Vector3.up * fh, foot - d + Vector3.up * fh });
                uvs.AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
                seeds.AddRange(new[] { seed, seed, seed, seed });
                tris.AddRange(new[] { o, o + 2, o + 1, o, o + 3, o + 2 });
            }
        }
        var mesh = new Mesh();
        mesh.SetVertices(vs); mesh.SetUVs(0, uvs); mesh.SetUVs(1, seeds); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds();
        return mesh;
    }

    // a new directional light in place of the old sun (same place, same mixed baked mode), no shadow-casting at night:
    // SleepSequence drives it (hell intensity 0)
    static Light ReplaceSun(UnityEngine.SceneManagement.Scene scene)
    {
        var sleep = Object.FindObjectsByType<SleepSequence>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
        var so = new SerializedObject(sleep);
        var oldSun = so.FindProperty("sun").objectReferenceValue as Light;
        Light sun;
        if (oldSun != null && oldSun.name == "Sun (replacement)") sun = oldSun;
        else
        {
            var go = new GameObject("Sun (replacement)");
            sun = go.AddComponent<Light>();
            if (oldSun != null)
            {
                go.transform.SetParent(oldSun.transform.parent, false);
                go.transform.SetPositionAndRotation(oldSun.transform.position, oldSun.transform.rotation);
                sun.type = LightType.Directional; sun.color = oldSun.color; sun.intensity = oldSun.intensity;
                sun.shadows = oldSun.shadows; sun.shadowStrength = oldSun.shadowStrength; sun.cullingMask = oldSun.cullingMask;
                Object.DestroyImmediate(oldSun.gameObject);
            }
            else { sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; go.transform.rotation = Quaternion.Euler(42f, 235f, 0f); }
        }
        sun.lightmapBakeType = LightmapBakeType.Mixed;
        so.FindProperty("sun").objectReferenceValue = sun;
        so.FindProperty("hellSunIntensity").floatValue = 0f;
        so.ApplyModifiedProperties();
        RenderSettings.sun = sun;
        return sun;
    }

    // everything that lights the city but the fires: the night fill, and the city's lamps and windows (kept enabled in
    // the day; HellTrees puts them out only while the red city is on)
    static Light[] MuteList(UnityEngine.SceneManagement.Scene scene, Light sun, List<Light> fires)
    {
        var fireSet = new HashSet<Light>(fires);
        return Resources.FindObjectsOfTypeAll<Light>()
            .Where(l => l.gameObject.scene == scene && l.enabled && l != sun && !fireSet.Contains(l)
                && (l.type != LightType.Directional ? InCity(l.transform) : l.name == "City night fill"))
            .ToArray();
    }

    static bool InCity(Transform t) { for (; t != null; t = t.parent) if (t.name == "demo_city_by_versatile_studio") return true; return false; }

    // fog, post stack, outline
    static void Look(UnityEngine.SceneManagement.Scene scene, GameObject hell, Light sun)
    {
        var sleep = Object.FindObjectsByType<SleepSequence>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
        var so = new SerializedObject(sleep);
        so.FindProperty("hellFogColor").colorValue = new Color(0.07f, 0.01f, 0.008f);
        so.FindProperty("hellFogDensity").floatValue = 0.026f;
        so.ApplyModifiedProperties();

        var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/HellVolumeProfile.asset");
        var bloom = Get<Bloom>(prof); bloom.threshold.Override(0.85f); bloom.intensity.Override(1.1f); bloom.scatter.Override(0.7f); bloom.tint.Override(new Color(1f, 0.6f, 0.35f));
        bloom.dirtIntensity.Override(0f);   // (no lens flare or dirt)
        var dof = Get<DepthOfField>(prof); dof.mode.Override(DepthOfFieldMode.Gaussian); dof.gaussianStart.Override(18f); dof.gaussianEnd.Override(70f); dof.gaussianMaxRadius.Override(1.3f);
        var ca = Get<ChromaticAberration>(prof); ca.intensity.Override(0.25f);
        var vg = Get<Vignette>(prof); vg.intensity.Override(0.4f); vg.smoothness.Override(0.5f); vg.color.Override(new Color(0.08f, 0f, 0f));
        var adj = Get<ColorAdjustments>(prof); adj.postExposure.Override(0.5f); adj.contrast.Override(22f); adj.saturation.Override(8f); adj.colorFilter.Override(new Color(1f, 0.8f, 0.68f));
        EditorUtility.SetDirty(prof);

        var go = hell.transform.Find("Hell Outline");
        if (go == null) { go = new GameObject("Hell Outline").transform; go.SetParent(hell.transform, false); }
        var ho = go.GetComponent<HellOutline>() ?? go.gameObject.AddComponent<HellOutline>();
        ho.outline = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/OfficeLook/OfficeOutline.mat");
    }

    static T Get<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (!p.TryGet(out T c)) { c = p.Add<T>(true); AssetDatabase.AddObjectToAsset(c, p); }
        c.active = true;
        return c;
    }

    // hell sky at 0.3, and 32 pixel lights on every quality level (Forward+ takes them per camera)
    public static void ApplySettings()
    {
        var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Skybox.mat");
        sky.SetFloat("_Exposure", 0.3f);
        EditorUtility.SetDirty(sky);
        int q = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.pixelLightCount = 32; }
        QualitySettings.SetQualityLevel(q, false);
        AssetDatabase.SaveAssets();
    }

    static Material FlameMaterial()
    {
        string path = Dir + "/TreeFlame.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("HellScape/TreeFlame")); AssetDatabase.CreateAsset(mat, path); }
        return mat;
    }
}
