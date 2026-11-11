using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// HellScape > Prologue > Give Pedestrians Umbrellas: Assets/Models/Umbrella.obj (53 loose pieces) is welded into one mesh with
// two slots (cloth, frame), scaled to a 1 m canopy with its pivot at the grip, saved as Assets/Prefabs/Umbrella.prefab with
// eight cloth colours; every PavementWalker in the Prologue then gets an UmbrellaCarrier (right arm up, umbrella overhead).
public static class UmbrellaBuilder
{
    const string Src = "Assets/Models/Umbrella.obj", Dir = "Assets/Generated/Umbrella", PrefabPath = "Assets/Prefabs/Umbrella.prefab";
    const float Scale = .13f, GripY = 4.2f;   // obj units -> metres; the shaft just above the J handle
    static readonly (string name, Color c)[] Colours =
    {
        ("Black", new Color(.05f, .05f, .06f)), ("Black 2", new Color(.08f, .08f, .09f)), ("Yellow", new Color(.92f, .74f, .08f)),
        ("Red", new Color(.62f, .06f, .07f)), ("Grey", new Color(.42f, .43f, .45f)), ("Navy", new Color(.07f, .12f, .32f)),
        ("Green", new Color(.1f, .32f, .16f)), ("Orange", new Color(.85f, .38f, .06f)),
    };

    [MenuItem("HellScape/Prologue/Give Pedestrians Umbrellas")]
    public static void Apply()
    {
        var prefab = BuildPrefab();
        var cloth = Colours.Select(k => Cloth(k.name, k.c)).ToArray();
        var sc = EditorSceneManager.GetSceneByName("Prologue");
        if (!sc.isLoaded) sc = EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity", OpenSceneMode.Additive);
        int n = 0;
        foreach (var w in sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PavementWalker>(true)))
        {
            var c = w.GetComponent<UmbrellaCarrier>() ?? w.gameObject.AddComponent<UmbrellaCarrier>();
            c.umbrellaPrefab = prefab; c.canopies = cloth; c.animator = w.GetComponentInChildren<Animator>(true);
            EditorUtility.SetDirty(c); n++;
        }
        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        Debug.Log("Umbrellas: " + n + " pedestrians carry one, Prologue saved");
    }

    static GameObject BuildPrefab()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Generated", "Umbrella");
        var obj = AssetDatabase.LoadAssetAtPath<GameObject>(Src);
        var inst = (GameObject)Object.Instantiate(obj);
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); inst.transform.localScale = Vector3.one;
        var frame = new CombineInstance[0].ToList(); var cloth = new CombineInstance[0].ToList();
        foreach (var f in inst.GetComponentsInChildren<MeshFilter>())
        {
            var b = f.GetComponent<Renderer>().bounds;
            // the pieces on the axis (shaft, handle, ferrule) are the frame; everything around the rim is cloth and ribs
            bool onAxis = b.size.x < 1f && b.size.z < 1f && new Vector2(b.center.x, b.center.z).magnitude < .5f;
            var ci = new CombineInstance { mesh = f.sharedMesh, transform = Matrix4x4.Translate(new Vector3(0f, -GripY, 0f) * Scale) * Matrix4x4.Scale(Vector3.one * Scale) * f.transform.localToWorldMatrix };
            (onAxis ? frame : cloth).Add(ci);
        }
        Object.DestroyImmediate(inst);
        var m1 = new Mesh(); m1.CombineMeshes(cloth.ToArray(), true, true);
        var m2 = new Mesh(); m2.CombineMeshes(frame.ToArray(), true, true);
        var mesh = new Mesh { name = "Umbrella", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.CombineMeshes(new[] { new CombineInstance { mesh = m1, transform = Matrix4x4.identity }, new CombineInstance { mesh = m2, transform = Matrix4x4.identity } }, false, false);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, Dir + "/Umbrella.asset");

        var go = new GameObject("Umbrella");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { Cloth("Black", Colours[0].c), Frame() };
        var saved = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        return saved;
    }

    static Material Cloth(string name, Color c)
    {
        string path = Dir + "/Umbrella " + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", .35f); m.SetFloat("_Metallic", 0f); m.SetFloat("_Cull", 0f);   // thin cloth: both faces
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Frame()
    {
        string path = Dir + "/Umbrella Frame.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", new Color(.09f, .09f, .1f)); m.SetFloat("_Smoothness", .5f); m.SetFloat("_Metallic", .6f);
        EditorUtility.SetDirty(m);
        return m;
    }
}
