using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Bakes the city's indirect light (sky, bounce, soft contact shadow) into lightmaps so the streets aren't black, with the
// sun left realtime-Mixed (indirect baked, direct + shadows live) and every lamp still realtime. The office and the silo
// are left on light probes only: InteriorDarkness blacks them out at runtime and a baked sun leak would defeat it.
public static class CityBake
{
    const string SettingsPath = "Assets/Settings/City Baked Lighting.lighting";

    [MenuItem("HellScape/City Lightmap: Set up")]
    public static void Setup()
    {
        var s = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
        if (s == null) { s = new LightingSettings { name = "City Baked Lighting" }; AssetDatabase.CreateAsset(s, SettingsPath); }
        s.bakedGI = true; s.realtimeGI = false;
        s.mixedBakeMode = MixedLightingMode.IndirectOnly;
        s.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
        s.lightmapResolution = 8f;   // x each renderer's scale in lightmap (0.02-0.2): about 1 texel per metre
        s.lightmapMaxSize = 2048; s.lightmapPadding = 2;
        s.directSampleCount = 32; s.indirectSampleCount = 128; s.environmentSampleCount = 128; s.maxBounces = 2;
        s.lightmapCompression = LightmapCompression.NormalQuality;
        s.ao = false; s.directionalityMode = LightmapsMode.NonDirectional;
        s.filteringMode = LightingSettings.FilterMode.Auto;
        EditorUtility.SetDirty(s);
        Lightmapping.lightingSettings = s;

        // the city's one directional light: indirect baked, shadows live but softer, a little warmer and brighter
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional && l.name == "Directional Light"))
        {
            l.lightmapBakeType = LightmapBakeType.Mixed;
            l.shadows = LightShadows.Soft; l.shadowStrength = 0.5f; l.shadowBias = 0.5f;
            l.intensity = 0.9f; l.color = new Color(1f, 0.8f, 0.66f);
            EditorUtility.SetDirty(l);
        }
        // a lifted gradient ambient: this is what the bake bounces around the streets (and what characters stand in)
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.46f, 0.48f, 0.56f);
        RenderSettings.ambientEquatorColor = new Color(0.36f, 0.35f, 0.38f);
        RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.18f);

        // the office and the silo stay probe-lit
        foreach (var d in Object.FindObjectsByType<InteriorDarkness>(FindObjectsSortMode.None))
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (d.zone.Contains(r.bounds.center) && r.receiveGI == ReceiveGI.Lightmaps) { r.receiveGI = ReceiveGI.LightProbes; EditorUtility.SetDirty(r); }

        // softer pipeline shadows (High-quality soft filter)
        foreach (var g in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets/Settings" }))
        {
            var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(g)));
            var q = so.FindProperty("m_SoftShadowQuality");
            if (q != null && q.intValue < 3) { q.intValue = 3; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();
    }

    [MenuItem("HellScape/City Lightmap: Bake")]
    public static void Bake() { Setup(); Lightmapping.BakeAsync(); }
}
