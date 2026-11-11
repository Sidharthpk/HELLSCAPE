using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The brown city (replaces DeadCityLook's and CinematicAtmosphereSetup's colours: don't re-run those).
// Black city lit only by street lamps through building-high volumetric fog, dead trees with no fire, few Damned and Lost.
// Safe to re-run. Touches MainGameScene and Assets/Generated/BrownCity; it does not save the scene.
public static class BrownCityLook
{
    const string Folder = "Assets/Generated/BrownCity", Noise = Folder + "/FogNoise.asset";
    const string TreesRoot = "Burning Trees";
    const int KeepTrees = 56;         // of the 112 that stood; re-run the menu, then Bake Occlusion, to change it
    const float AnchorReach = 3f;     // metres: a Damned anchor further than this from any tree is standing on nothing

    // Pure darkness: no directional light, no ambient, no reflections. Street lamps are the only light in the city.
    static readonly Color Fog = Hex("0A0605"), DenseFog = Hex("0C0706"), DrivingFog = Hex("090504"), Mist = Hex("0A0605");
    const float FogStrength = .6f, Scattering = .045f, ScatterDistance = 90f, DrawDistance = 450f;
    const float FogTop = 50f, FogFalloff = 15f;   // building tops: median 14 m, 90% under 36 m, tallest 66 m
    const int LitLamps = 40, Damned = 4, Lost = 5, TreeDamnedPerTree = 2, TreeDamnedAlive = 10;

    // the only roots that get marked static; anything inside them that moves is skipped (Mover)
    static readonly string[] StillRoots =
    {
        "Burning Trees", "Harlow_Clinic", "NewOfficeBuilding Entrance", "Compact City Boundary", "WaterBlock_50m",
        "Props", "Red Flat (generated)", "BridgeEnding", "OfficeAct",
    };

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    static IEnumerable<T> FindAll<T>() where T : Component =>
        Resources.FindObjectsOfTypeAll<T>().Where(c => c.gameObject.scene.isLoaded && c.gameObject.scene.name == "MainGameScene");

    [MenuItem("HellScape/Hell City/Brown City Look")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play first.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Generated", "BrownCity");
        var look = FindAll<SleepSequence>().First();
        var city = look.hellWorld.GetComponent<DeadCityAtmosphere>();
        var trees = FindAll<HellTrees>().First();
        Air(look, city);
        Renderers();
        NoFire(trees);
        FewerTrees(trees);
        Darkness(trees);
        Statics();
        Unseen();
        Debug.Log("[BrownCity] applied (scene not saved)");
    }

    // ---- SleepSequence.ApplyLook and DeadCityAtmosphere put these in when you wake. The skybox is left alone.
    static void Air(SleepSequence look, DeadCityAtmosphere city)
    {
        look.hellFogColor = Fog;
        look.hellAmbientSky = look.hellAmbientEquator = look.hellAmbientGround = look.bloodAmbient = Color.black;
        look.hellSunIntensity = 0f;
        look.mistColor = Mist;
        city.explorationFog = Fog; city.denseFog = DenseFog; city.drivingFog = DrivingFog;
        city.scattering = Scattering; city.scatteringDistance = ScatterDistance;
        city.volumetricFog = true; city.fogStrength = FogStrength;
        city.fogTop = FogTop; city.fogHeightFalloff = FogFalloff;
        foreach (var cam in new[] { city.playerCamera, city.drivingCamera })
        {
            cam.farClipPlane = DrawDistance;   // the shader has faded to solid sky by here
            cam.useOcclusionCulling = true;
            EditorUtility.SetDirty(cam);
        }
        EditorUtility.SetDirty(look); EditorUtility.SetDirty(city);
        EditorSceneManager.MarkSceneDirty(look.gameObject.scene);
    }

    // ---- The fog pass goes on both renderers: gameplay cameras use the Cinematic one, cutscene cameras the default.
    // It does nothing without a DeadCityAtmosphere, so the prologue and the office are unaffected.
    static void Renderers()
    {
        var noise = FogNoise();
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/DeadCityScattering.shader");
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets/Settings" }))
        {
            var pipeline = new SerializedObject(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid)));
            var list = pipeline.FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
            {
                var renderer = (UniversalRendererData)list.GetArrayElementAtIndex(i).objectReferenceValue;
                var feature = renderer.rendererFeatures.OfType<DeadCityScatteringFeature>().FirstOrDefault();
                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance<DeadCityScatteringFeature>();
                    feature.name = "Dead City Local Scattering";
                    AssetDatabase.AddObjectToAsset(feature, renderer);
                    renderer.rendererFeatures.Add(feature);
                    var map = new SerializedObject(renderer).FindProperty("m_RendererFeatureMap");
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id);
                    map.serializedObject.Update();
                    map.InsertArrayElementAtIndex(map.arraySize);
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                    map.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
                feature.scatteringShader = shader;
                feature.fogNoise = noise;
                feature.Create();
                EditorUtility.SetDirty(feature); EditorUtility.SetDirty(renderer);
            }
        }
        AssetDatabase.SaveAssets();
    }

    // tiling value noise, three octaves: small enough to sample many times a pixel
    static Texture3D FogNoise()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture3D>(Noise);
        if (existing != null) return existing;
        const int size = 64;
        var random = new System.Random(1666);
        float[][] lattices = new[] { 8, 16, 32 }.Select(n =>
        {
            var v = new float[n * n * n];
            for (int i = 0; i < v.Length; i++) v[i] = (float)random.NextDouble();
            return v;
        }).ToArray();
        float Octave(int o, float x, float y, float z)
        {
            int n = 8 << o;
            x *= n; y *= n; z *= n;
            int x0 = (int)x, y0 = (int)y, z0 = (int)z;
            float S(float t) => t * t * (3f - 2f * t);
            float fx = S(x - x0), fy = S(y - y0), fz = S(z - z0);
            float L(int a, int b, int c) => lattices[o][(a % n) + n * ((b % n) + n * (c % n))];
            return Mathf.Lerp(
                Mathf.Lerp(Mathf.Lerp(L(x0, y0, z0), L(x0 + 1, y0, z0), fx), Mathf.Lerp(L(x0, y0 + 1, z0), L(x0 + 1, y0 + 1, z0), fx), fy),
                Mathf.Lerp(Mathf.Lerp(L(x0, y0, z0 + 1), L(x0 + 1, y0, z0 + 1), fx), Mathf.Lerp(L(x0, y0 + 1, z0 + 1), L(x0 + 1, y0 + 1, z0 + 1), fx), fy), fz);
        }
        var bytes = new byte[size * size * size];
        for (int z = 0, i = 0; z < size; z++)
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++, i++)
                {
                    float u = (float)x / size, v = (float)y / size, w = (float)z / size;
                    float value = Octave(0, u, v, w) * .57f + Octave(1, u, v, w) * .29f + Octave(2, u, v, w) * .14f;
                    bytes[i] = (byte)(Mathf.Clamp01((value - .25f) / .5f) * 255f);   // (octaves average towards .5: stretch it back out)
                }
        var texture = new Texture3D(size, size, size, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        texture.SetPixelData(bytes, 0);
        texture.Apply(false, false);
        AssetDatabase.CreateAsset(texture, Noise);
        return texture;
    }

    // ---- No fire anywhere in the city: the flames, the loose fires, the crackle and the trees' dead fire lights.
    static void NoFire(HellTrees trees)
    {
        var roots = FindAll<TreeFireLOD>().Select(t => t.gameObject)
            .Concat(FindAll<Transform>().Where(t => t.parent == null && (t.name.StartsWith("WildFire") || t.name.StartsWith("FireBall"))).Select(t => t.gameObject))
            .Distinct().ToArray();
        foreach (var root in roots) Object.DestroyImmediate(root);
        foreach (var f in FindAll<FireFlicker>().Where(f => f.transform.root.name == TreesRoot).ToArray()) Object.DestroyImmediate(f.gameObject);
        trees.fireClip = null;
        EditorUtility.SetDirty(trees);
        Debug.Log($"[BrownCity] removed {roots.Length} fire objects");
    }

    // ---- Fewer trees, evenly across the city. HellTrees' anchors (where the Damned gather) stay only at trees that stand,
    // or they would circle empty pavement. Re-running keeps KeepTrees: it does not halve them again.
    static void FewerTrees(HellTrees trees)
    {
        var root = FindAll<Transform>().First(t => t.parent == null && t.name == TreesRoot);
        var all = root.Cast<Transform>().Where(t => t.gameObject.activeSelf).ToArray();
        int removed = 0;
        for (int i = 0; i < all.Length && all.Length > KeepTrees; i++)
        {
            if (i * KeepTrees / all.Length != (i + 1) * KeepTrees / all.Length) continue;
            Object.DestroyImmediate(all[i].gameObject);
            removed++;
        }
        var kept = root.Cast<Transform>().Select(t => new Vector2(t.position.x, t.position.z)).ToArray();
        bool AtTree(Transform t) => kept.Any(k => Vector2.Distance(k, new Vector2(t.position.x, t.position.z)) < AnchorReach);
        var anchors = trees.transform.Cast<Transform>().Where(a => !AtTree(a)).ToArray();
        foreach (var a in anchors) Object.DestroyImmediate(a.gameObject);
        trees.lights = trees.lights.Where(l => l != null).ToArray();
        EditorUtility.SetDirty(trees);
        Debug.Log($"[BrownCity] trees removed {removed}, standing {kept.Length}; anchors removed {anchors.Length}, gathering spots left {trees.lights.Length}");
    }

    // ---- Everything in the open city that gives light and isn't a working street lamp goes off for good.
    // The story's own lights (Red Flat, office stage, bridge, your pistol) are left alone.
    static void Darkness(HellTrees trees)
    {
        var lamps = new HashSet<Light>(trees.lamps.Where(l => l != null));
        int off = 0;
        foreach (var l in FindAll<Light>())
        {
            string root = l.transform.root.name;
            bool city = root == "STATIC_MODELS" || root == "Burning Trees" || l.transform.IsChildOf(trees.transform);
            if (!city || lamps.Contains(l) || !l.enabled) continue;
            l.enabled = false; EditorUtility.SetDirty(l); off++;
        }
        trees.mute = new Light[0];   // (HellTrees gives these back when it switches off: there is nothing to give back now)
        trees.maxLit = 0;
        trees.maxLamps = LitLamps;
        EditorUtility.SetDirty(trees);

        foreach (var l in FindAll<Light>().Where(l => l.type == LightType.Directional)) { l.gameObject.SetActive(false); EditorUtility.SetDirty(l.gameObject); }
        RenderSettings.sun = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = RenderSettings.ambientEquatorColor = RenderSettings.ambientGroundColor = Color.black;
        RenderSettings.reflectionIntensity = 0f;
        foreach (var p in FindAll<ReflectionProbe>()) { p.enabled = false; EditorUtility.SetDirty(p); }

        // the clinic's bulbs are painted on: a copy, the prologue keeps its lit clinic
        var clinic = FindAll<MeshRenderer>().First(r => r.name == "Harlow_Clinic");
        var mats = clinic.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] == null || mats[i].name != "lightbulb") continue;
            string path = Folder + "/lightbulb_off.mat";
            var dark = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (dark == null) { dark = new Material(mats[i]); dark.SetColor("_EmissionColor", Color.black); dark.DisableKeyword("_EMISSION"); AssetDatabase.CreateAsset(dark, path); }
            mats[i] = dark;
        }
        clinic.sharedMaterials = mats; EditorUtility.SetDirty(clinic);

        var walkers = FindAll<CityWalkers>().First();
        walkers.damned = Damned; walkers.lost = Lost; EditorUtility.SetDirty(walkers);
        var circles = FindAll<TreeDamned>().First();
        circles.perTree = TreeDamnedPerTree; circles.maxAlive = TreeDamnedAlive; EditorUtility.SetDirty(circles);
        Debug.Log($"[BrownCity] {off} non-street lights off for good");
    }

    // ---- Static: everything that truly never moves, for occlusion culling and batching. No GI flags: lighting is realtime only.
    const StaticEditorFlags Solid = StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic;

    static void Statics()
    {
        var moving = Movers();
        int marked = 0, skipped = 0, seeThrough = 0;
        foreach (var root in FindAll<Transform>().Where(t => t.parent == null && StillRoots.Contains(t.name)))
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name.StartsWith("Shaft") || Moves(r.transform, moving)) { skipped++; continue; }
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, GameObjectUtility.GetStaticEditorFlags(r.gameObject) | Solid);
                marked++;
            }
        // fences, glass and water can't hide what's behind them. ALL materials, not any: a building with one window material is still a wall.
        foreach (var r in FindAll<MeshRenderer>())
        {
            if (!GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.BatchingStatic)) continue;
            var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
            bool through = r.name.StartsWith("fence") || r.sharedMaterials.All(m => m == null || m.renderQueue >= (int)RenderQueue.AlphaTest);
            if (through) seeThrough++;
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, through ? flags & ~StaticEditorFlags.OccluderStatic : flags | StaticEditorFlags.OccluderStatic);
        }
        Debug.Log($"[BrownCity] static: {marked} marked, {skipped} left dynamic, {seeThrough} see-through no longer hide things");
    }

    // ---- Nothing unseen costs anything: animators stop posing bones off-screen (states, timers and root motion still run,
    // so nothing freezes or loses its place), and ghosts, being see-through, stop casting shadows.
    static void Unseen()
    {
        int animators = 0, ghosts = 0;
        foreach (var a in FindAll<Animator>())
        {
            if (a.transform.IsChildOf(FindAll<SleepSequence>().First().playerRoot.transform)) continue;   // (your own arms)
            a.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            EditorUtility.SetDirty(a);
            animators++;
        }
        foreach (var s in FindAll<SkinnedMeshRenderer>())
        {
            if (!s.sharedMaterials.Any(m => m != null && m.renderQueue >= (int)RenderQueue.Transparent)) continue;
            s.shadowCastingMode = ShadowCastingMode.Off;
            EditorUtility.SetDirty(s);
            ghosts++;
        }
        Debug.Log($"[BrownCity] {animators} animators cull when unseen, {ghosts} see-through characters cast no shadow");
    }

    // A transform is moving if physics, animation or a script owns it, or a script holds a reference to it.
    static HashSet<Transform> Movers()
    {
        var set = new HashSet<Transform>();
        void Own<T>() where T : Component { foreach (var c in FindAll<T>()) set.Add(c.transform); }
        Own<Rigidbody>(); Own<Animator>(); Own<Animation>(); Own<NavMeshAgent>(); Own<MonoBehaviour>();
        foreach (var behaviour in FindAll<MonoBehaviour>())
        {
            var p = new SerializedObject(behaviour).GetIterator();
            while (p.NextVisible(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (p.objectReferenceValue is GameObject g) set.Add(g.transform);
                else if (p.objectReferenceValue is Transform t) set.Add(t);
            }
        }
        return set;
    }

    static bool Moves(Transform t, HashSet<Transform> moving)
    {
        // (the roots themselves hold scripts that only switch things on and off; below them, one mover carries all its children)
        for (; t != null && t.parent != null; t = t.parent) if (moving.Contains(t)) return true;
        return false;
    }

    // Rebuilds what the camera may skip behind buildings. Occlusion data only: no lightmaps.
    [MenuItem("HellScape/Hell City/Bake Occlusion")]
    public static void BakeOcclusion()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play first.");
        StaticOcclusionCulling.GenerateInBackground();
    }
}
