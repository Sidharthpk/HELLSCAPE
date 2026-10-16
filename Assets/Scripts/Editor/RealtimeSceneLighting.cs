using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Apply to the playable scenes, including inactive lights used by later story beats.
public static class RealtimeSceneLighting
{
    const string SettingsPath = "Assets/Settings/Game Realtime Lighting.lighting";

    [MenuItem("HellScape/Lighting: Realtime in All Game Scenes")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before changing scene lighting.");
        var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
        if (settings == null)
        {
            settings = new LightingSettings { name = "Game Realtime Lighting" };
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        settings.autoGenerate = false;
        settings.bakedGI = false;
        // URP lights render in realtime without Enlighten's precomputed realtime GI.
        settings.realtimeGI = false;
        EditorUtility.SetDirty(settings);

        var active = SceneManager.GetActiveScene();
        try
        {
            foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var scene = SceneManager.GetSceneByPath(entry.path);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    Lightmapping.lightingSettings = settings;
                    Lightmapping.lightingDataAsset = null;
                    var lights = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)).ToArray();
                    foreach (var light in lights)
                    {
                        Undo.RecordObject(light, "Use realtime lighting");
                        light.lightmapBakeType = LightmapBakeType.Realtime;
                        light.bakingOutput = new LightBakingOutput
                        {
                            isBaked = false,
                            lightmapBakeType = LightmapBakeType.Realtime,
                            occlusionMaskChannel = -1,
                            probeOcclusionLightIndex = -1
                        };
                        EditorUtility.SetDirty(light);
                    }
                    foreach (var probe in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ReflectionProbe>(true)))
                    {
                        Undo.RecordObject(probe, "Use realtime reflections");
                        probe.mode = ReflectionProbeMode.Realtime;
                        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
                        probe.bakedTexture = null;
                        EditorUtility.SetDirty(probe);
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"{scene.name}: {lights.Length} realtime lights; baked lighting disabled and data detached.");
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
        }
        finally { if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); }

        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets/Settings" }))
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            var serialized = new SerializedObject(asset);
            var mode = serialized.FindProperty("m_AdditionalLightsRenderingMode");
            if (mode != null && mode.intValue != 1)
            {
                mode.intValue = 1; // PerPixel: consistent point/spot lights on large surfaces.
                serialized.ApplyModifiedProperties();
            }
        }
        AssetDatabase.SaveAssets();
    }
}
