using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The silo fight's floor: a thin layer of blood (the Shader Graph sample WaterLake, coloured blood) and the user's
// severed Meshy parts (Assets/Floating) floating on it. Vane picks them up and throws them (KillerBoss.bodyParts).
// Rerunnable: it replaces its own "Blood pool (generated)" under OfficeAct/Silo.
public static class SiloBloodPool
{
    const string Lake = "Assets/Samples/Shader Graph/17.3.0/Production Ready Shaders/Environment/Water/WaterLake.mat";
    const string MatPath = "Assets/Materials/OfficeAct/SiloBlood.mat";
    const string Name = "Blood pool (generated)";
    const float Depth = 0.06f;              // the layer's surface, above the floor
    const int Copies = 3;                   // of each part

    // longest side of each part, metres (the Meshy exports are all about 1 unit)
    static readonly (string key, float size)[] Sizes =
        { ("arm", 0.8f), ("foot", 0.28f), ("hand", 0.21f), ("head", 0.26f), ("leg", 0.95f), ("torso", 0.75f) };

    [MenuItem("HellScape/Vane: Silo Blood Pool + Body Parts")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var enc = Object.FindObjectsByType<SiloEncounter>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (enc == null || enc.siloCenter == null || enc.killer == null) return "Open MainGameScene (no SiloEncounter).";
        var lake = AssetDatabase.LoadAssetAtPath<Material>(Lake);
        if (lake == null) return "No " + Lake + " (import the Shader Graph Production Ready samples).";

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(lake); AssetDatabase.CreateAsset(mat, MatPath); }
        else mat.CopyPropertiesFromMaterial(lake);
        mat.shader = lake.shader;
        mat.SetColor("_Color", new Color(0.32f, 0.01f, 0.01f, 0.92f));
        mat.SetColor("_DepthColor", new Color(0.08f, 0f, 0f, 1f));
        EditorUtility.SetDirty(mat);

        Transform silo = enc.transform, center = enc.siloCenter;
        var old = silo.Find(Name);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // the floor, a little off the middle (the rifle lies in the middle)
        float floorY = center.position.y;
        if (Physics.Raycast(center.position + new Vector3(1.5f, 2f, 1.5f), Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            floorY = hit.point.y;

        // root at world scale 1 (the parts are its children), the surface a scaled plane under it
        var pool = new GameObject(Name);
        Undo.RegisterCreatedObjectUndo(pool, "Blood pool");
        pool.transform.SetParent(silo, false);
        pool.transform.localScale = new Vector3(1f / silo.lossyScale.x, 1f / silo.lossyScale.y, 1f / silo.lossyScale.z);
        pool.transform.position = new Vector3(center.position.x, floorY + Depth, center.position.z);
        var surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
        surface.name = "Surface";
        Object.DestroyImmediate(surface.GetComponent<Collider>());
        surface.transform.SetParent(pool.transform, false);
        float reach = (enc.killer.arenaRadius + enc.killer.wallBeyond + 0.5f) * 2f;
        surface.transform.localScale = new Vector3(reach / 10f, 1f, reach / 10f);
        var pr = surface.GetComponent<MeshRenderer>();
        pr.sharedMaterial = mat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var parts = pool.AddComponent<SiloBodyParts>();
        parts.surface = pr;
        if (enc.flood != null) parts.flood = enc.flood.surface;

        var objs = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Floating" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
        if (objs.Length == 0) return "No models in Assets/Floating.";
        var rnd = new System.Random(7);
        float ring = enc.killer.arenaRadius;
        int count = objs.Length * Copies;
        for (int i = 0; i < count; i++)
        {
            string path = objs[i % objs.Length];
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, silo.gameObject.scene);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            go.name = "Body part - " + (Sizes.FirstOrDefault(s => file.Contains("_" + s.key + "_")).key ?? file);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            // a URP material from the texture next to it, in case the obj's own came in blank
            string png = System.IO.Path.ChangeExtension(path, ".png");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
            if (tex != null)
            {
                string mp = System.IO.Path.ChangeExtension(path, ".mat");
                var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, mp); }
                m.SetTexture("_BaseMap", tex);
                m.SetFloat("_Smoothness", 0.45f);   // wet
                EditorUtility.SetDirty(m);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
            }

            go.transform.SetParent(pool.transform, false);
            go.transform.localScale = Vector3.one;
            var rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float size = Sizes.FirstOrDefault(s => file.Contains("_" + s.key + "_")).size;
            if (size <= 0f) size = 0.5f;
            float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            go.transform.localScale = Vector3.one * (size * (0.85f + 0.3f * (float)rnd.NextDouble()) / Mathf.Max(0.001f, longest));
            // lying down (long axis flat), spread around the ring he fights on
            go.transform.rotation = Quaternion.Euler(b.size.y >= Mathf.Max(b.size.x, b.size.z) ? 90f : 0f, (float)rnd.NextDouble() * 360f, 0f);
            float ang = (i + (float)rnd.NextDouble() * 0.5f) / count * Mathf.PI * 2f, dist = ring * (0.3f + 0.65f * (float)rnd.NextDouble());
            go.transform.position = new Vector3(center.position.x + Mathf.Cos(ang) * dist, floorY + Depth, center.position.z + Mathf.Sin(ang) * dist);
        }

        Undo.RecordObject(enc.killer, "Body parts");
        enc.killer.bodyParts = parts;
        EditorUtility.SetDirty(enc.killer);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(silo.gameObject.scene);
        return $"Silo blood pool at y {floorY + Depth:F2} ({reach:F1} m across), {objs.Length * Copies} body parts floating on it, wired to {enc.killer.name}.";
    }
}
