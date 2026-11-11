using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Writes the revised plot's text into the open scene's serialized data (RevisedPlot otherwise only applies it at run time),
// including the string arguments of UnityEvents that RevisedPlot's field pass can't see.
public static class PlotBake
{
    [MenuItem("HellScape/Story/Bake Revised Plot Into Open Scene")]
    public static void Bake()
    {
        var scene = EditorSceneManager.GetActiveScene();
        RevisedPlot.ApplyText(scene);
        int events = 0;
        foreach (var mb in RevisedPlot.All<MonoBehaviour>(scene))
        {
            if (mb == null) continue;
            var so = new SerializedObject(mb);
            var it = so.GetIterator();
            bool changed = false;
            while (it.Next(true))
                if (it.propertyType == SerializedPropertyType.String && it.name == "m_StringArgument")
                {
                    string m = RevisedPlot.Migrate(it.stringValue);
                    if (m != it.stringValue) { it.stringValue = m; changed = true; events++; }
                }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Revised plot baked into {scene.name} ({events} event strings).");
    }
}
