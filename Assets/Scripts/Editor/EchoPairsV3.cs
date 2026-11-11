using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The Echo shows the office as it was: EchoOfficeMemory swaps every burnt material for its "before" twin listed in
// Resources/EchoOfficeSettings. The pairs there were written for the old KV office, so in the v3 office nothing swapped.
// This finds each burnt_* prop material's original (an embedded material of the same mesh's FBX/glb, by name) and pairs
// the v3 wall/floor with the old office's clean glsl textures. Re-runnable: existing pairs are kept.
public static class EchoPairsV3
{
    static readonly string[] Roots = { "Office Ground Floor", "Fuse", "control room 3D Model Avatar of amogusstrikesback2 amogusstrikes", "double_door" };
    const string Glsl = "Assets/Models/lowpoly-mobile-cartoon-office-rooms/textures/";

    [MenuItem("HellScape/Office/Echo: Pair Burnt Materials With Clean Ones")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        var settings = Resources.Load<EchoOfficeSettings>("EchoOfficeSettings");
        var pairs = settings.materials.Where(p => p.burnt != null && p.before != null).ToList();
        var have = new HashSet<Material>(pairs.Select(p => p.burnt));
        var scene = EditorSceneManager.GetActiveScene();
        var renderers = new List<Renderer>();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (Roots.Contains(root.name) || root.name.StartsWith("Door")) renderers.AddRange(root.GetComponentsInChildren<Renderer>(true));
            else if (root.name == "OfficeAct" && root.transform.Find("Office") != null) renderers.AddRange(root.transform.Find("Office").GetComponentsInChildren<Renderer>(true));
        }
        void Add(Material burnt, Material clean) { if (burnt == null || clean == null || have.Contains(burnt)) return; have.Add(burnt); pairs.Add(new EchoOfficeSettings.MaterialPair { burnt = burnt, before = clean }); }

        int missing = 0;
        var missed = new HashSet<string>();
        foreach (var r in renderers)
        {
            var mats = r.sharedMaterials;
            string src = Source(r);
            foreach (var m in mats)
            {
                if (m == null || have.Contains(m)) continue;
                if (m.name == "Wall (old office, burnt)") { Add(m, AssetDatabase.LoadAssetAtPath<Material>(Glsl + "glsl_wall1.mat")); continue; }
                if (m.name == "Floor (old office, burnt)") { Add(m, AssetDatabase.LoadAssetAtPath<Material>(Glsl + "glsl_floor1.mat")); continue; }
                if (!m.name.StartsWith("burnt_")) continue;
                string want = m.name.Substring(6);
                if (want.EndsWith("_glb")) want = want.Substring(0, want.Length - 4);
                Material clean = null;
                if (src != null)
                    clean = AssetDatabase.LoadAllAssetsAtPath(src).OfType<Material>().FirstOrDefault(x => Safe(x.name) == want);
                if (clean == null)   // a standalone copy somewhere (the supplies pack's extracted materials)
                {
                    var hit = AssetDatabase.FindAssets(want + " t:Material").Select(AssetDatabase.GUIDToAssetPath)
                        .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == want && !p.Contains("/Burnt/"));
                    if (hit != null) clean = AssetDatabase.LoadAssetAtPath<Material>(hit);
                }
                if (clean != null) Add(m, clean); else if (missed.Add(m.name)) missing++;
            }
        }
        settings.materials = pairs.ToArray();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        return $"Echo pairs: {pairs.Count} in all; no clean twin for {missing}: {string.Join(", ", missed)}";
    }

    static string Safe(string n) => string.Join("_", n.Split(Path.GetInvalidFileNameChars()));

    static string Source(Renderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        Mesh mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : mf != null ? mf.sharedMesh : null;
        if (mesh == null) return null;
        string p = AssetDatabase.GetAssetPath(mesh);
        if (string.IsNullOrEmpty(p) || p.EndsWith(".asset")) return null;
        return p;
    }
}
