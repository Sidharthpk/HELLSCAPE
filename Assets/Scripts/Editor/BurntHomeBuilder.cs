using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class BurntHomeBuilder
{
    const string Folder = "Assets/Materials/RedHome";

    public static void Apply(Transform root)
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Materials", "RedHome");
        var appearance = root.GetComponent<BurntHomeAppearance>();
        if (appearance != null) { appearance.ShowClean(false); return; }
        appearance = Undo.AddComponent<BurntHomeAppearance>(root.gameObject);
        var shader = Shader.Find("HellScape/Burnt Home");
        if (shader == null) throw new System.InvalidOperationException("Burnt Home shader is missing.");
        appearance.surfaces = root.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.name != "SM_BG").Select(r =>
            {
                var clean = r.sharedMaterials;
                var burnt = clean.Select(m =>
                {
                    if (m == null) return null;
                    string id = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m));
                    string path = Folder + "/" + m.name.Replace(':', '_').Replace('/', '_') + "_" + id + ".mat";
                    var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (copy == null)
                    {
                        copy = new Material(m) { shader = shader, name = m.name + " (burnt)" };
                        AssetDatabase.CreateAsset(copy, path);
                    }
                    return copy;
                }).ToArray();
                Undo.RecordObject(r, "Burn Red City home");
                r.sharedMaterials = burnt;
                return new BurntHomeAppearance.Surface { renderer = r, clean = clean, burnt = burnt };
            }).ToArray();
        EditorUtility.SetDirty(appearance);
    }

    [MenuItem("HellScape/Red City: Burn Home and Fix Room Height")]
    public static void Upgrade()
    {
        if (EditorApplication.isPlaying) return;
        foreach (string name in new[] { "MainGameScene", "Prologue" })
        {
            var scene = SceneManager.GetSceneByName(name);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Additive);
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach (var t in all)
            {
                if (t.name == "Flat 2B (apartment)" && t.GetComponent<ApartmentEyeHeight>() == null)
                    Undo.AddComponent<ApartmentEyeHeight>(t.gameObject);
                if (name == "MainGameScene" && (t.name == "Red Flat (generated)" || t.name == "Old residential building (home)")) Apply(t);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Red City home scorched; apartment eye height fixed in both scenes.");
    }
}
