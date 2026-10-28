using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// HellScape > Office: Red Haze Look
// The office and the silo in one deep-red haze (the user's reference: red fog, red lamps glowing through it, black
// corners). No directional light: the lamps (spots, with their LightShaft beams) and the zones' red fog and ambient.
// InteriorDarkness sets fog/ambient at runtime, so its colours are what change here; the Echo keeps its blue.
public static class RedHazeLook
{
    static readonly Color Lamp = new Color(1f, .07f, .05f), Fog = new Color(.16f, .012f, .018f);
    static readonly string[] Groups = { "EmergencyLights", "PowerLights", "RedTubeLights", "CorridorLamps", "Lamps", "Silo", "BloodSurface", "StairLamps", "KesslerTunnel", "SiloComplex" };

    [MenuItem("HellScape/Office: Red Haze Look")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var scene = SceneManager.GetSceneByName("OfficeSilo");
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/OfficeSilo.unity", OpenSceneMode.Additive);
        var roots = scene.GetRootGameObjects();

        foreach (var l in roots.SelectMany(r => r.GetComponentsInChildren<Light>(true)).Where(l => l.type == LightType.Directional)) l.enabled = false;
        int lamps = 0;
        foreach (var l in roots.SelectMany(r => r.GetComponentsInChildren<Light>(true)))
        {
            if (l.type == LightType.Directional || l.transform.parent == null || !Groups.Contains(l.transform.parent.name)) continue;
            Undo.RecordObject(l, "Red haze");
            l.color = Lamp;
            l.intensity = Mathf.Max(l.intensity, 6f);
            lamps++;
        }
        foreach (var s in roots.SelectMany(r => r.GetComponentsInChildren<LightShaft>(true))) { Undo.RecordObject(s, "Red haze"); s.strength = .45f; }

        foreach (var z in roots.SelectMany(r => r.GetComponentsInChildren<InteriorDarkness>(true)))
        {
            Undo.RecordObject(z, "Red haze");
            bool office = z.blackoutUntilPowered;
            z.darkAmbient = new Color(.09f, .01f, .012f);
            z.poweredAmbient = new Color(.14f, .016f, .018f);
            z.emergencyAmbient = new Color(.12f, .012f, .014f);
            z.darkFog = z.blackFog = z.poweredFog = Fog;
            z.poweredFogScale = .8f;
            z.emergencyColor = Lamp; z.emergencyIntensity = 4f; z.emergencyRange = 16f;
            z.fogDensity = office ? .06f : .035f;   // (the office's fog of war raises it to .09 at runtime)
        }

        // the room volumes' teal/yellow grade off; more bloom, so each lamp blooms through the haze
        foreach (var p in roots.SelectMany(r => r.GetComponentsInChildren<Volume>(true)).Select(v => v.sharedProfile).Where(p => p != null).Distinct())
        {
            if (p.TryGet(out SplitToning st)) st.active = false;
            if (p.TryGet(out ShadowsMidtonesHighlights smh)) smh.active = false;
            if (p.TryGet(out ColorAdjustments ca)) { ca.colorFilter.Override(Color.white); ca.saturation.Override(10f); }   // (was desaturated and warm: it turned the red to beige)
            if (p.TryGet(out Vignette vg)) vg.color.Override(Color.black);
            if (p.TryGet(out Bloom b)) { b.intensity.Override(1.6f); b.threshold.Override(.6f); b.scatter.Override(.8f); b.tint.Override(new Color(1f, .35f, .3f)); }
            EditorUtility.SetDirty(p);
        }
        var shaft = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/LightShaft/LightShaft.mat");
        if (shaft != null) { shaft.SetColor("_Color", new Color(1f, .1f, .07f, .3f)); EditorUtility.SetDirty(shaft); }   // (LightShaft sets the lamp's colour at runtime)

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Red haze: " + lamps + " lamps red";
    }
}
