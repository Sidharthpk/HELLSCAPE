using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Splits the generic passenger car pack's one FBX (ten bodies and loose wheels laid out in a ring) into ten prefabs:
// each car upright at the origin, nose along +z, wheels as spinnable pivots, its own paint texture on the body and the
// pack's wheel textures on the tyres. Run again any time (the prefabs are rebuilt in place).
public static class CarPack
{
    const string Dir = "Assets/Perishables/generic-passenger-car-pack/";
    public const string OutDir = "Assets/Prefabs/Cars";
    const string MatDir = "Assets/Generated/CarPack";

    // body object in the FBX, prefab name, paint texture
    static readonly (string body, string name, string tex)[] Cars =
    {
        ("Compact Body", "Compact", "CompactBlue"), ("Coupe Body", "Coupe", "CoupeBlue"), ("Hatchback Body", "Hatchback", "HatchbackYellow"),
        ("minivan body", "Minivan", "MinivanRed"), ("Offroad Body", "Offroad", "OffroadGreen"), ("Pickup Body", "Pickup", "PickupGreen"),
        ("Sedan Body", "Sedan", "SedanYellow"), ("Sport body", "Sport", "SportRed"), ("SUV Body", "SUV", "SUVBlack"), ("Wagon Body", "Wagon", "WagonBlue"),
    };
    public static readonly string[] Names = Cars.Select(c => c.name).ToArray();

    // car name -> degrees to turn the model when the nose heuristic got it wrong (set after looking at the result)
    static readonly Dictionary<string, float> NoseFix = new Dictionary<string, float>();

    public static string PrefabPath(string name) => $"{OutDir}/Pack{name}.prefab";

    [MenuItem("HellScape/Cars/Build Car Pack Prefabs")]
    public static void Build()
    {
        Directory.CreateDirectory(OutDir); Directory.CreateDirectory(MatDir);
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "source/fab.fbx");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var kids = inst.transform.Cast<Transform>().ToList();
        var wheels = kids.Where(t => t.name.StartsWith("Wheel")).ToList();
        var used = new HashSet<Transform>();
        foreach (var (bodyName, name, tex) in Cars)
        {
            var body = kids.First(t => t.name == bodyName);
            Vector3 centre = Extent(body.gameObject).center; centre.y = 0f;
            var mine = wheels.Where(w => !used.Contains(w)).OrderBy(w => Flat(Extent(w.gameObject).center - centre).sqrMagnitude).Take(4).ToList();
            foreach (var w in mine) used.Add(w);

            var root = new GameObject("Pack" + name);
            var model = new GameObject("Model").transform; model.SetParent(root.transform, false);
            // every body was laid out in the ring turned about Y by its own angle: undo that with quaternions (the euler y of a
            // z-up import rotation is ambiguous), leaving the mesh in its modelled orientation
            Quaternion un = Quaternion.Euler(270f, 0f, 0f) * Quaternion.Inverse(body.rotation);
            Transform Place(Transform part, string newName)
            {
                var c = Object.Instantiate(part.gameObject).transform;
                c.name = newName;
                c.SetParent(model, false);
                c.SetPositionAndRotation(un * (part.position - centre), un * part.rotation);
                return c;
            }
            var bodyT = Place(body, "Body");
            var pivots = new List<(Transform pivot, string kind)>();
            foreach (var w in mine)
            {
                var mesh = Place(w, w.name);
                var pivot = new GameObject("Wheel").transform; pivot.SetParent(model, false);
                pivot.position = Extent(mesh.gameObject).center;
                mesh.SetParent(pivot, true);
                pivots.Add((pivot, w.name));
            }
            // a modelled-crooked body (the coupe) is straightened by the spread of its vertices
            float crook = Crookedness(bodyT);
            if (Mathf.Abs(crook) > 1.5f) model.rotation = Quaternion.Euler(0f, crook, 0f);
            // length along z
            var b = Extent(model.gameObject);
            if (b.size.x > b.size.z) model.rotation = Quaternion.Euler(0f, 90f, 0f) * model.rotation;
            // the nose end: the roof (tallest vertices) sits behind the middle, so the front is the other way
            if (RoofZ(bodyT) > 0f) model.rotation = Quaternion.Euler(0f, 180f, 0f) * model.rotation;
            if (NoseFix.TryGetValue(name, out float fix)) model.rotation = Quaternion.Euler(0f, fix, 0f) * model.rotation;
            b = Tight(model.gameObject);
            model.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);

            // bake the model's turn into its parts, so every wheel pivot has the car's own axes (spin about x, steer about y):
            // a pivot under a turned model would spin about the wrong axis
            var parts = model.Cast<Transform>().ToList();
            var worldPos = parts.Select(t => t.position).ToList(); var worldRot = parts.Select(t => t.rotation).ToList();
            model.rotation = Quaternion.identity;
            for (int i = 0; i < parts.Count; i++) parts[i].SetPositionAndRotation(worldPos[i], worldRot[i]);
            foreach (var (pivot, _) in pivots)
            {
                var mesh = pivot.GetChild(0);
                mesh.SetParent(null, true); pivot.rotation = Quaternion.identity; mesh.SetParent(pivot, true);
            }

            var bodyMat = BodyMaterial(name, tex);
            Paint(bodyT, bodyMat);
            foreach (var (pivot, kind) in pivots)
                foreach (var r in pivot.GetComponentsInChildren<Renderer>())
                {
                    var ms = r.sharedMaterials;
                    for (int i = 0; i < ms.Length; i++)
                        ms[i] = ms[i] != null && ms[i].name == "Body" ? bodyMat : WheelMaterial(kind);
                    r.sharedMaterials = ms;
                }

            var car = root.AddComponent<PackCar>();
            car.body = bodyT;
            foreach (var (p, _) in pivots)
            {
                Vector3 l = root.transform.InverseTransformPoint(p.position);
                bool front = l.z > 0f, right = l.x > 0f;
                p.name = "Wheel " + (front ? "F" : "R") + (right ? "R" : "L");
                if (front) { if (right) car.frontRight = p; else car.frontLeft = p; }
                else { if (right) car.rearRight = p; else car.rearLeft = p; }
            }
            b = Tight(model.gameObject);
            var zs = pivots.Select(p => root.transform.InverseTransformPoint(p.pivot.position).z).ToList();
            float fz = zs.Where(z => z > 0f).DefaultIfEmpty(1f).Average(), rz = zs.Where(z => z <= 0f).DefaultIfEmpty(-1f).Average();
            car.wheelRadius = Extent(pivots[0].pivot.gameObject).extents.y;
            car.rearAxle = rz; car.wheelbase = Mathf.Max(1f, fz - rz);
            car.size = b.size;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(name));
            Object.DestroyImmediate(root);
            Debug.Log($"CarPack {name}: size {b.size.ToString("F2")} wheels {mine.Count} wheelbase {car.wheelbase:F2} radius {car.wheelRadius:F2}");
        }
        Object.DestroyImmediate(inst);
        AssetDatabase.SaveAssets();
    }

    // degrees about Y that turn the body's long axis onto z (-90..90)
    static float Crookedness(Transform body)
    {
        var pts = body.GetComponent<MeshFilter>().sharedMesh.vertices.Select(p => body.TransformPoint(p)).ToList();
        float mx = pts.Average(p => p.x), mz = pts.Average(p => p.z), sxx = 0f, szz = 0f, sxz = 0f;
        foreach (var p in pts) { float dx = p.x - mx, dz = p.z - mz; sxx += dx * dx; szz += dz * dz; sxz += dx * dz; }
        float theta = 0.5f * Mathf.Atan2(2f * sxz, sxx - szz) * Mathf.Rad2Deg;   // major axis, from +x towards +z
        float phi = Mathf.DeltaAngle(0f, theta - 90f);
        if (phi > 90f) phi -= 180f; else if (phi < -90f) phi += 180f;
        return phi;
    }

    // bounds from the actual vertices (a mesh modelled crooked has a renderer box much bigger than the car)
    static Bounds Tight(GameObject g)
    {
        bool first = true; var b = new Bounds();
        foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
            foreach (var v in mf.sharedMesh.vertices)
            {
                var p = mf.transform.TransformPoint(v);
                if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
            }
        return b;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    static Bounds Extent(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // how far (model space, along the long axis) the roof sits from the middle of the car
    static float RoofZ(Transform body)
    {
        var pts = body.GetComponent<MeshFilter>().sharedMesh.vertices.Select(p => body.TransformPoint(p)).ToList();   // world: the car is already turned to run along z
        float top = pts.Max(p => p.y);
        var roof = pts.Where(p => p.y > top - 0.12f).ToList();
        return roof.Average(p => p.z) - pts.Average(p => p.z);
    }

    static Material BodyMaterial(string name, string tex)
    {
        string path = $"{MatDir}/{name}Body.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}textures/{tex}.png"));
        m.SetColor("_BaseColor", new Color(0.62f, 0.62f, 0.6f));
        m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.08f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material WheelMaterial(string meshName)
    {
        // "Wheel_A 1" / "Wheel_A001" -> A
        string letter = meshName.Length > 6 ? meshName.Substring(6, 1) : "A";
        string tex = File.Exists($"{Dir}textures/Wheel_{letter}_Diffuse.png") ? $"Wheel_{letter}_Diffuse"
            : File.Exists($"{Dir}textures/wheel_{letter}_Diffuse.png") ? $"wheel_{letter}_Diffuse" : "wheel_1_Diffuse";
        string path = $"{MatDir}/Wheel{letter}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}textures/{tex}.png"));
        m.SetFloat("_Metallic", 0.1f); m.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Paint(Transform body, Material paint)
    {
        var r = body.GetComponent<Renderer>();
        var ms = r.sharedMaterials;
        for (int i = 0; i < ms.Length; i++) if (ms[i] != null && ms[i].name == "Body") ms[i] = paint;
        r.sharedMaterials = ms;
    }
}
