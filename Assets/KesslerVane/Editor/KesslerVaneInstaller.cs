using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using KesslerVane;

public static class KesslerVaneInstaller
{
    const string Root = "Assets/KesslerVane";
    [Serializable] public sealed class Entry { public string name, state, key, albedo, normal; public float roughness; public float[] uvTileMeters; }
    [Serializable] public sealed class Manifest { public Entry[] materials; }
    [Serializable] public sealed class Audit {
        public string unityVersion, shader;
        public int meshRenderers, missingMaterials, missingAlbedoTextures, triangles, colliders;
        public bool stateSwapPassed, noMarkers, cleanAndBurnedSameBounds;
        public Vector3 boundsCenter, boundsSize;
    }
    static Dictionary<string, Material> mats;
    static Manifest manifest;
    static Shader shader;

    [MenuItem("Tools/Kessler & Vane/Build or refresh materials and prefabs")]
    public static void Build()
    {
        manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/materials.json"));
        var pipeline = GraphicsSettings.defaultRenderPipeline;
        string shaderName = pipeline == null ? "Standard" : pipeline.GetType().Name.Contains("HD") ? "HDRP/Lit" : "Universal Render Pipeline/Lit";
        shader = Shader.Find(shaderName);
        if (shader == null) throw new Exception("Shader is unavailable: " + shaderName);
        mats = new Dictionary<string, Material>();
        Directory.CreateDirectory(Root + "/Materials/Clean"); Directory.CreateDirectory(Root + "/Materials/Burned"); Directory.CreateDirectory(Root + "/Prefabs"); Directory.CreateDirectory(Root + "/Scenes");
        AssetDatabase.Refresh();
        foreach (var e in manifest.materials)
        {
            ConfigureTexture(Root + "/" + e.albedo, false);
            if (!string.IsNullOrEmpty(e.normal)) ConfigureTexture(Root + "/" + e.normal, true);
            string path = Root + "/Materials/" + e.state + "/" + e.name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.name = e.name;
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/" + e.albedo);
            foreach (string prop in new[] { "_MainTex", "_BaseMap", "_BaseColorMap" }) if (material.HasProperty(prop)) material.SetTexture(prop, albedo);
            foreach (string prop in new[] { "_Color", "_BaseColor" }) if (material.HasProperty(prop)) material.SetColor(prop, Color.white);
            foreach (string prop in new[] { "_Glossiness", "_Smoothness" }) if (material.HasProperty(prop)) material.SetFloat(prop, 1f - e.roughness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (!string.IsNullOrEmpty(e.normal))
            {
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/" + e.normal);
                foreach (string prop in new[] { "_BumpMap", "_NormalMap" }) if (material.HasProperty(prop)) material.SetTexture(prop, normal);
                material.EnableKeyword("_NORMALMAP"); if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", .65f);
            }
            EditorUtility.SetDirty(material); mats[e.name] = material;
        }
        AssetDatabase.SaveAssets();
        foreach (string state in new[] { "Clean", "Burned" })
        {
            string path = Root + "/Models/KesslerVane_" + state + ".fbx";
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new Exception("FBX missing: " + path);
            importer.globalScale = 1f; importer.importAnimation = false; importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importNormals = ModelImporterNormals.Import;
            importer.generateSecondaryUV = true;
            foreach (var e in manifest.materials.Where(m => m.state == state))
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), e.name), mats[e.name]);
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model); go.name = "Office_" + state;
            var bindings = new List<OfficeStoryState.Binding>();
            foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var keys = renderer.sharedMaterials.Select(m => m == null ? "" : m.name.Replace("KV_Clean_", "").Replace("KV_Burned_", "")).ToArray();
                if (keys.Any(k => !mats.ContainsKey("KV_Clean_" + k))) throw new Exception("Unmapped material on " + renderer.name + ": " + string.Join(",", keys));
                bindings.Add(new OfficeStoryState.Binding { renderer = renderer, clean = keys.Select(k => mats["KV_Clean_" + k]).ToArray(), burned = keys.Select(k => mats["KV_Burned_" + k]).ToArray() });
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && renderer.GetComponent<Collider>() == null)
                {
                    var collider = renderer.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = filter.sharedMesh;
                }
            }
            var story = go.AddComponent<OfficeStoryState>(); story.bindings = bindings.ToArray(); story.SetBurned(state == "Burned");
            PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/Office_" + state + ".prefab"); UnityEngine.Object.DestroyImmediate(go);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Kessler & Vane: clean and burned materials and prefabs ready.");
    }
    static void ConfigureTexture(string path, bool normal)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new Exception("Texture missing: " + path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normal; importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true; importer.anisoLevel = 4; importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
    }
    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true); Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds); return bounds;
    }
    static void Capture(Camera camera, string path)
    {
        var rt = new RenderTexture(1440, 900, 24, RenderTextureFormat.ARGB32); camera.targetTexture = rt; camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
    }
    public static void ValidateAndBuild()
    {
        try
        {
            Build(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var clean = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Office_Clean.prefab");
            var burned = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Office_Burned.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(clean); var other = (GameObject)PrefabUtility.InstantiatePrefab(burned);
            Bounds bounds = BoundsOf(go), burnedBounds = BoundsOf(other); UnityEngine.Object.DestroyImmediate(other);
            var audit = new Audit { unityVersion = Application.unityVersion, shader = shader.name, boundsCenter = bounds.center, boundsSize = bounds.size,
                cleanAndBurnedSameBounds = (bounds.size - burnedBounds.size).sqrMagnitude < .0001f,
                noMarkers = !go.GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains("label") || t.name.Contains("marker") || t.name.Contains("route") || t.name.Contains("Legend")) };
            foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                audit.meshRenderers++;
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null) { audit.missingMaterials++; continue; }
                    if (mat.mainTexture == null) audit.missingAlbedoTextures++;
                }
                audit.triangles += renderer.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
            }
            audit.colliders = go.GetComponentsInChildren<Collider>(true).Length;
            var state = go.GetComponent<OfficeStoryState>(); state.SetBurned(true);
            audit.stateSwapPassed = go.GetComponentsInChildren<Renderer>(true).All(r => r.sharedMaterials.All(m => m.name.StartsWith("KV_Burned_")));
            state.SetBurned(false);
            if (audit.missingMaterials != 0 || audit.missingAlbedoTextures != 0 || !audit.stateSwapPassed || !audit.noMarkers || !audit.cleanAndBurnedSameBounds)
                throw new Exception("Validation failed: " + JsonUtility.ToJson(audit));
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.48f, .52f, .43f); RenderSettings.fog = false;
            var sunlight = new GameObject("Preview directional light").AddComponent<Light>(); sunlight.type = LightType.Directional; sunlight.intensity = .55f; sunlight.transform.rotation = Quaternion.Euler(60, -30, 0);
            foreach (var floor in go.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("Floor_") && r.bounds.size.x > 3f && r.bounds.size.z > 3f))
            {
                var lamp = new GameObject("Preview light " + floor.name).AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 24; lamp.intensity = 1.5f; lamp.color = new Color(.84f, .91f, .72f); lamp.transform.position = new Vector3(floor.bounds.center.x, 2.85f, floor.bounds.center.z);
            }
            var cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; cam.fieldOfView = 72; cam.nearClipPlane = .08f; cam.farClipPlane = 250; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.045f,.05f,.043f);
            var office = go.GetComponentsInChildren<Renderer>().First(r => r.name.StartsWith("Floor_open-office")).bounds;
            cam.transform.position = new Vector3(office.center.x + office.extents.x * .65f, 1.7f, office.center.z - office.extents.z * .05f);
            cam.transform.LookAt(new Vector3(office.center.x - office.extents.x * .4f, 1.5f, office.center.z + office.extents.z * .55f));
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../kessler-vane-unity/Previews")); Directory.CreateDirectory(output);
            Capture(cam, Path.Combine(output, "Unity_Clean_Interior.png")); state.SetBurned(true); Capture(cam, Path.Combine(output, "Unity_Burned_Interior.png")); state.SetBurned(false);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Root + "/Scenes/Office_Demo.unity");
            File.WriteAllText(Path.Combine(output, "unity-validation.json"), JsonUtility.ToJson(audit, true));
            string package = Path.GetFullPath(Path.Combine(Application.dataPath, "../../kessler-vane-unity/KesslerVane_Unity.unitypackage"));
            AssetDatabase.ExportPackage(Root, package, ExportPackageOptions.Recurse);
            Debug.Log("KV_VALIDATION_SUCCESS " + JsonUtility.ToJson(audit));
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
}
