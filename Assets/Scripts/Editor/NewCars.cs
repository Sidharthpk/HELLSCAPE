using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The two cars added on 2026-10-01 (Assets/Perishables): the PSX estate ("PSX Sedan", three paint textures) and the
// 1978 Passat. HellScape > Cars > Use New Cars puts them to work wherever the game already has cars, next to the
// Fiesta / S2000 / police car rather than instead of all of them:
//   Prologue      some of the street's moving traffic (LoopDriver cars) and parked cars
//   MainGameScene some of the abandoned cars in the hell city (Colldables), and two new wreck types on the escape
//                 road (RoadObstacleSpawner; the estate picks one of its paints each time)
// Only the model under each car is swapped: the holder, its driver script, position and collider stay.
// Run it again any time: the listed slots are refitted (so a change of scale or paint here reaches the scenes).
public static class NewCars
{
    const string Estate = "Assets/Perishables/PSX Sedan - @jonniemadeit/car.fbx";
    const string EstateDir = "Assets/Perishables/PSX Sedan - @jonniemadeit/";
    const string Passat = "Assets/Perishables/psx-passat-ls-1978/source/PassatLS1978.glb";
    const string MatDir = "Assets/Generated/Cars";
    const string ObstacleDir = "Assets/Prefabs/Obstacles";

    // (model, scale to about the other cars' size, nose at the model's -x/-z end)
    static readonly Dictionary<string, (string path, float scale, bool backwards)> Models = new Dictionary<string, (string, float, bool)>
    {
        { "estate", (Estate, 1.15f, true) },   // (a wide car: 4 m long is already 2.1 m across)
        { "passat", (Passat, 0.82f, false) },
    };
    static readonly Color[] PassatTints = { Color.white, new Color(0.62f, 0.72f, 0.86f), new Color(0.86f, 0.78f, 0.6f), new Color(0.6f, 0.2f, 0.18f) };

    // Prologue/StreetLife: which holder gets which car and paint
    static readonly (string holder, string car, int paint)[] Street =
    {
        ("Car 1", "estate", 0), ("Car 3", "passat", 1), ("Car 5", "estate", 1),
        ("Parked car 0", "passat", 2), ("Parked car 2", "estate", 2), ("Parked car 3", "estate", 0),
    };
    // MainGameScene/Colldables: which abandoned car becomes which
    static readonly (string old, string car, int paint)[] City =
    {
        ("Fiesta (1)", "passat", 3), ("Fiesta (2)", "estate", 1), ("S2000 (1)", "estate", 2),
    };

    [MenuItem("HellScape/Cars/Use New Cars")]
    public static void Menu() => Debug.Log(Apply());

    public static string Apply()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Estate) == null || AssetDatabase.LoadAssetAtPath<GameObject>(Passat) == null) return "The new car models aren't imported.";
        var log = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            int n = scene.name == "Prologue" ? Prologue(scene) : scene.name == "MainGameScene" ? Main(scene, log) : -1;
            if (n < 0) continue;
            if (n > 0) EditorSceneManager.MarkSceneDirty(scene);
            log.Add($"{scene.name}: {n} car(s) swapped");
        }
        AssetDatabase.SaveAssets();
        return string.Join("; ", log);
    }

    // ---------------------------------------------------------------- paint

    static Material Paint(string car, int i)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Generated", "Cars");
        if (car == "estate")
        {
            // one material per paint texture (the model comes with five identical ones, all on the first texture)
            string[] tex = { "cartexture.png", "cartexture 1.png", "cartexture 2.png" };
            i = Mathf.Abs(i) % tex.Length;
            string path = $"{MatDir}/PSXEstate_{i}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(Estate).GetComponentInChildren<Renderer>().sharedMaterial;
                m = new Material(src) { name = "PSXEstate_" + i };
                AssetDatabase.CreateAsset(m, path);
            }
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(EstateDir + tex[i]);
            var imp = AssetImporter.GetAtPath(EstateDir + tex[i]) as TextureImporter;
            if (imp != null && imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; imp.SaveAndReimport(); }   // crisp PS1 texels
            m.mainTexture = t;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(m);
            return m;
        }
        // the Passat has one (white) texture: copies of it with only the paintwork recoloured, so the tyres,
        // glass and bumpers stay as they are
        i = Mathf.Abs(i) % PassatTints.Length;
        if (i == 0) return null;   // as it comes
        var body = AssetDatabase.LoadAssetAtPath<GameObject>(Passat).GetComponentsInChildren<Renderer>().Select(r => r.sharedMaterial).First(x => x != null && x.name.Contains("Passat"));
        string texPath = $"{MatDir}/Passat_paint_{i}.png";
        var src0 = body.mainTexture as Texture2D;
        if (src0 != null)
        {
            // (the imported texture isn't readable: copy it through a render texture)
            var rt = RenderTexture.GetTemporary(src0.width, src0.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src0, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var copy = new Texture2D(src0.width, src0.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            var px = copy.GetPixels();
            Color tint = PassatTints[i];
            for (int k = 0; k < px.Length; k++)
            {
                Color c = px[k];
                float lo = Mathf.Min(c.r, Mathf.Min(c.g, c.b)), hi = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                float paintness = Mathf.InverseLerp(0.62f, 0.8f, lo) * (1f - Mathf.InverseLerp(0.08f, 0.2f, hi - lo));   // bright and grey = paint
                px[k] = Color.Lerp(c, new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a), paintness);
            }
            copy.SetPixels(px); copy.Apply();
            System.IO.File.WriteAllBytes(texPath, copy.EncodeToPNG());
            Object.DestroyImmediate(copy);
            AssetDatabase.ImportAsset(texPath);
            var timp = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (timp != null && (timp.filterMode != src0.filterMode || timp.mipmapEnabled)) { timp.filterMode = src0.filterMode; timp.mipmapEnabled = false; timp.SaveAndReimport(); }
        }
        string ppath = $"{MatDir}/Passat_{i}.mat";
        var pm = AssetDatabase.LoadAssetAtPath<Material>(ppath);
        if (pm == null)
        {
            pm = new Material(body) { name = "Passat_" + i };
            AssetDatabase.CreateAsset(pm, ppath);
        }
        var painted = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        foreach (var prop in new[] { "baseColorFactor", "_BaseColor", "_Color" })
            if (pm.HasProperty(prop)) pm.SetColor(prop, Color.white);
        if (painted != null)
            foreach (var prop in pm.GetTexturePropertyNames())
                if (pm.GetTexture(prop) == src0) pm.SetTexture(prop, painted);
        EditorUtility.SetDirty(pm);
        return pm;
    }

    static void Repaint(GameObject model, string car, int paint)
    {
        var m = Paint(car, paint);
        if (m == null) return;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (car == "estate" || (mats[i] != null && mats[i].name.Contains("Passat"))) mats[i] = m;   // (the Passat's props and underbody keep theirs)
            r.sharedMaterials = mats;
        }
    }

    // ---------------------------------------------------------------- one car

    // the model under a holder: scaled, nose along the holder's +z, centred, wheels on the holder's origin; the
    // holder's box collider refitted round it
    static GameObject Fit(Transform holder, string car, int paint)
    {
        var spec = Models[car];
        Quaternion rot = holder.rotation;
        holder.rotation = Quaternion.identity;
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(spec.path), holder);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * spec.scale / Mathf.Max(0.0001f, holder.lossyScale.x);
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        Bounds b = Extent(model);
        model.transform.localRotation = Quaternion.Euler(0f, (b.size.x > b.size.z ? -90f : 0f) + (spec.backwards ? 180f : 0f), 0f);
        b = Extent(model);
        model.transform.position += holder.position - new Vector3(b.center.x, b.min.y, b.center.z);
        b = Extent(model);
        var box = holder.GetComponent<BoxCollider>();
        if (box == null) box = holder.gameObject.AddComponent<BoxCollider>();
        box.center = holder.InverseTransformPoint(b.center);
        box.size = b.size / Mathf.Max(0.0001f, holder.lossyScale.x);
        holder.rotation = rot;
        Repaint(model, car, paint);
        return model;
    }

    static Bounds Extent(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void Strip(Transform holder)
    {
        foreach (var child in holder.Cast<Transform>().Where(c => c.GetComponentInChildren<Renderer>(true) != null).ToArray())
            Undo.DestroyObjectImmediate(child.gameObject);
    }

    // which of the new cars a holder already carries, and in which paint ("PSXEstate_2" -> 2)
    static bool IsNew(Transform holder, out string car, out int paint)
    {
        car = null; paint = 0;
        foreach (Transform c in holder)
        {
            string p = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(c.gameObject);
            if (p != Estate && p != Passat) continue;
            car = p == Estate ? "estate" : "passat";
            foreach (var r in c.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null && (m.name.StartsWith("PSXEstate_") || m.name.StartsWith("Passat_")) && int.TryParse(m.name.Substring(m.name.LastIndexOf('_') + 1), out int k)) paint = k;
            return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- the prologue street

    static int Prologue(Scene scene)
    {
        Transform life = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            life = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "StreetLife");
            if (life != null) break;
        }
        if (life == null) return 0;
        int n = 0;
        foreach (var (name, car, paint) in Street)
        {
            Transform holder = life.Find(name);
            if (holder == null) continue;
            Strip(holder);
            Undo.RegisterCreatedObjectUndo(Fit(holder, car, paint), "New car");
            n++;
        }
        return n;
    }

    // ---------------------------------------------------------------- the hell city and the escape road

    static int Main(Scene scene, List<string> log)
    {
        int n = 0;
        var lot = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Colldables");
        if (lot != null)
        {
            // the ones already swapped: refit (scale / paint may have changed here)
            foreach (Transform holder in lot.transform)
                if (IsNew(holder, out string was, out int wasPaint)) { Strip(holder); Fit(holder, was, wasPaint); n++; }
            foreach (var (oldName, car, paint) in City)
            {
                Transform old = lot.transform.Find(oldName);
                if (old == null) continue;
                var oldBox = old.GetComponent<BoxCollider>();
                float ground = oldBox != null ? oldBox.bounds.min.y : old.position.y;
                var holder = new GameObject(car == "estate" ? "PSX Estate" : "Passat 1978").transform;
                holder.SetParent(lot.transform, false);
                holder.SetSiblingIndex(old.GetSiblingIndex());
                holder.SetPositionAndRotation(new Vector3(old.position.x, ground, old.position.z), Quaternion.Euler(0f, old.eulerAngles.y, 0f));
                holder.gameObject.layer = old.gameObject.layer;
                holder.gameObject.tag = old.gameObject.tag;
                Fit(holder, car, paint);
                var rb = old.GetComponent<Rigidbody>();
                if (rb != null) EditorUtility.CopySerialized(rb, holder.gameObject.AddComponent<Rigidbody>());
                Undo.RegisterCreatedObjectUndo(holder.gameObject, "New car");
                Undo.DestroyObjectImmediate(old.gameObject);
                n++;
            }
        }

        // two more kinds of wreck on the road out
        var spawner = Object.FindObjectsByType<RoadObstacleSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(s => s.gameObject.scene == scene);
        if (spawner != null && spawner.obstacles != null)
        {
            var like = spawner.obstacles.FirstOrDefault(o => o.prefab != null && o.prefab.name.StartsWith("Wreck")) ?? spawner.obstacles.FirstOrDefault();
            var list = spawner.obstacles.ToList();
            foreach (var (name, car) in new[] { ("WreckEstate", "estate"), ("WreckPassat", "passat") })
            {
                var prefab = Wreck(name, car);
                if (list.Any(o => o.prefab == prefab)) continue;
                list.Add(new RoadObstacleSpawner.ObstacleType
                {
                    prefab = prefab,
                    damage = like != null ? like.damage : 25f,
                    pushable = like != null && like.pushable,
                    mass = like != null ? like.mass : 900f,
                    weight = like != null ? like.weight : 0.5f,
                });
                n++;
                log.Add("road wreck added: " + name);
            }
            Undo.RecordObject(spawner, "New wrecks");
            spawner.obstacles = list.ToArray();
            EditorUtility.SetDirty(spawner);
        }
        return n;
    }

    static GameObject Wreck(string name, string car)
    {
        string path = $"{ObstacleDir}/{name}.prefab";   // (rebuilt every time, same asset)
        var holder = new GameObject(name);
        Fit(holder.transform, car, 0);
        var paint = holder.AddComponent<CarPaint>();
        paint.paints = car == "estate" ? Enumerable.Range(0, 3).Select(i => Paint(car, i)).ToArray() : new Material[0];
        var prefab = PrefabUtility.SaveAsPrefabAsset(holder, path);
        Object.DestroyImmediate(holder);
        return prefab;
    }
}
