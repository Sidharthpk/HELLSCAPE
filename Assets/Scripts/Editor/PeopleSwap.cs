using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > People: Use PSX Character Pack
// The street people, the delivery customers and the hell city's lost souls were copies of the same three models.
// This gives each of them their own body from the user's PSX character pack (New Folder/characters-psx, 65 people,
// converted to Humanoid), no two alike in a scene. Only the model changes: the root keeps its scripts and pose, the
// new model gets the old one's Animator controller, root motion, culling, local pose and (for ghosts) the ghost
// material; anything in the scene that pointed at the old model's Animator/transform is pointed at the new one.
public static class PeopleSwap
{
    const string Pack = "Assets/New Folder/characters-psx";

    [MenuItem("HellScape/People: Use PSX Character Pack")]
    public static void Run()
    {
        var all = AssetDatabase.FindAssets("t:Model", new[] { Pack }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.Contains("/Male/") || p.Contains("/Female/")).OrderBy(p => p).ToList();
        bool Uniform(string p) => p.Contains("Police") || p.Contains("Firefighter") || p.Contains("Doctor") || p.Contains("_HM");
        var civilians = all.Where(p => !Uniform(p)).ToList();
        var rng = new System.Random(1304);   // the same cast every time it's run
        List<string> Shuffle(IEnumerable<string> l) => l.OrderBy(_ => rng.Next()).ToList();

        // ---------------------------------------------------------------- the prologue: the morning street
        var pro = SceneManager.GetSceneByName("Prologue");
        bool opened = false;
        if (!pro.isLoaded) { pro = EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity", OpenSceneMode.Additive); opened = true; }
        var proRoot = pro.GetRootGameObjects().First(g => g.name == "Prologue").transform;
        var map = new Dictionary<Object, Object>();
        var deck = Shuffle(civilians);
        string Take(System.Func<string, bool> want)
        {
            var p = deck.FirstOrDefault(want) ?? deck.First();
            deck.Remove(p);
            return p;
        }
        var deliveries = proRoot.Find("Deliveries");
        if (deliveries != null)
        {
            var okafor = deliveries.Find("Mrs Okafor");
            if (okafor != null) Swap(okafor, Take(p => p.Contains("/Female/")), null, map);
            var clinic = deliveries.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Man at the door"));
            if (clinic != null) Swap(clinic, all.First(p => p.Contains("Character_25_Doctor")), null, map);   // the clinic's man: a doctor
        }
        foreach (var w in proRoot.GetComponentsInChildren<PavementWalker>(true))
            Swap(w.transform, Take(p => true), null, map);
        Remap(pro, map);
        Bury();
        EditorSceneManager.MarkSceneDirty(pro);
        EditorSceneManager.SaveScene(pro);
        int proCount = map.Count / 3;
        doomed.Clear();

        // ---------------------------------------------------------------- the hell city: the lost souls
        var main = SceneManager.GetSceneByName("MainGameScene");
        map.Clear();
        var lost = main.GetRootGameObjects().First(g => g.name == "HellWorld").transform.Find("LostSouls (City)");
        var souls = Shuffle(all);   // the dead of the whole city: uniforms too
        int i = 0;
        foreach (Transform soul in lost)
        {
            var old = soul.GetComponentInChildren<Animator>(true);
            if (old == null) continue;
            var ghostMat = old.GetComponentInChildren<Renderer>(true)?.sharedMaterial;
            Swap(soul, souls[i % souls.Count], ghostMat, map);
            i++;
        }
        Remap(main, map);
        Bury();
        EditorSceneManager.MarkSceneDirty(main);
        EditorSceneManager.SaveScene(main);
        if (opened) EditorSceneManager.CloseScene(pro, true);
        Debug.Log($"People swapped: {proCount} in the prologue, {i} lost souls in the hell city.");
    }

    // replace the model under root (the child holding the Animator) with the pack model at modelPath
    static void Swap(Transform root, string modelPath, Material overrideMat, Dictionary<Object, Object> map)
    {
        var oldAnim = root.GetComponentInChildren<Animator>(true);
        if (oldAnim == null) return;
        Transform old = oldAnim.transform;
        while (old.parent != root) old = old.parent;

        var oldRs = old.GetComponentsInChildren<Renderer>(true);
        float height = 1.75f;   // as tall as the one it replaces, if that measures sensibly
        if (oldRs.Length > 0)
        {
            Bounds ob = oldRs[0].bounds; foreach (var r in oldRs) ob.Encapsulate(r.bounds);
            if (ob.size.y > 1.45f && ob.size.y < 2.4f) height = ob.size.y;   // (a child's model gets an adult's height: the pack has no children)
        }
        var shadows = oldRs.Length > 0 ? oldRs[0].shadowCastingMode : UnityEngine.Rendering.ShadowCastingMode.On;
        bool receive = oldRs.Length == 0 || oldRs[0].receiveShadows;

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var g = (GameObject)PrefabUtility.InstantiatePrefab(model, root.gameObject.scene);
        g.transform.SetParent(root, false);
        g.transform.localPosition = old.localPosition;
        g.transform.localRotation = old.localRotation;
        g.transform.SetSiblingIndex(old.GetSiblingIndex());

        // to a person's height (the pack's are ~4.8 units tall), a touch of variety
        var nrs = g.GetComponentsInChildren<Renderer>(true);
        float raw = 4.8f;
        if (nrs.Length > 0) { Bounds nb = nrs[0].bounds; foreach (var r in nrs) nb.Encapsulate(r.bounds); raw = nb.size.y / Mathf.Max(0.0001f, g.transform.lossyScale.y); }
        float want = height * (1f + (Random.value - 0.5f) * 0.08f);
        g.transform.localScale = Vector3.one * (want / Mathf.Max(0.01f, raw));

        var anim = g.GetComponent<Animator>();
        if (anim == null) anim = g.AddComponent<Animator>();
        anim.runtimeAnimatorController = oldAnim.runtimeAnimatorController;
        anim.avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
        anim.applyRootMotion = oldAnim.applyRootMotion;
        anim.cullingMode = oldAnim.cullingMode;
        anim.updateMode = oldAnim.updateMode;

        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
        {
            if (overrideMat != null) r.sharedMaterials = Enumerable.Repeat(overrideMat, r.sharedMaterials.Length).ToArray();
            r.shadowCastingMode = shadows;
            r.receiveShadows = receive;
        }

        map[oldAnim] = anim;
        map[old] = g.transform;
        map[old.gameObject] = g;
        Undo.RegisterCreatedObjectUndo(g, "People");
        doomed.Add(old.gameObject);   // destroyed after the scene's references are moved over
    }

    static readonly List<GameObject> doomed = new List<GameObject>();

    static void Bury()
    {
        foreach (var g in doomed) if (g != null) Undo.DestroyObjectImmediate(g);
        doomed.Clear();
    }

    // everything in the scene that pointed at an old model now points at its replacement
    static void Remap(Scene scene, Dictionary<Object, Object> map)
    {
        if (map.Count == 0) return;
        var byId = map.ToDictionary(kv => kv.Key.GetInstanceID(), kv => kv.Value);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    int id = it.objectReferenceInstanceIDValue;
                    if (id != 0 && byId.TryGetValue(id, out var to)) { it.objectReferenceValue = to; changed = true; }
                }
                if (changed) so.ApplyModifiedProperties();
            }
    }
}
