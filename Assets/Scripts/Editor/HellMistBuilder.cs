using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > Map > Add fog and mist (main scene): black-red distance fog (the SleepSequence hell values) plus the
// HellMist banks. Re-runnable: it reuses the material and the object.
public static class HellMistBuilder
{
    const string Tex = "Assets/Materials/HellMist.png", Mat = "Assets/Materials/HellMist.mat";

    [MenuItem("HellScape/Map/Add fog and mist (main scene)")]
    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        var mat = Material();

        var sleep = Object.FindFirstObjectByType<SleepSequence>();
        if (sleep != null)
        {
            Undo.RecordObject(sleep, "Hell fog");
            sleep.hellFogColor = new Color(0.02f, 0.008f, 0.008f);   // black with a trace of dried blood
            sleep.hellFogDensity = 0.022f;
            EditorUtility.SetDirty(sleep);
        }
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.02f, 0.008f, 0.008f); RenderSettings.fogDensity = 0.022f;

        var old = GameObject.Find("Hell mist");
        var go = old != null ? old : new GameObject("Hell mist");
        var hm = go.GetComponent<HellMist>() ?? go.AddComponent<HellMist>();
        hm.mist = mat; hm.tint = new Color(0.16f, 0.05f, 0.04f, 0.28f);
        EditorUtility.SetDirty(hm);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Hell fog + mist set in " + scene.name);
    }

    static Material Material()
    {
        if (!System.IO.File.Exists(Tex))
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float noise = Mathf.PerlinNoise(x * 0.07f, y * 0.07f) * 0.5f + 0.5f;
                    float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a) * noise;   // soft puff, lumpy
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            System.IO.File.WriteAllBytes(Tex, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(Tex);
            var imp = (TextureImporter)AssetImporter.GetAtPath(Tex);
            imp.alphaIsTransparency = true; imp.mipmapEnabled = true; imp.SaveAndReimport();
        }
        var m = AssetDatabase.LoadAssetAtPath<Material>(Mat);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(m, Mat);
        }
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f); m.renderQueue = 3000; m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_SoftParticlesEnabled", 1f); m.SetFloat("_SoftParticlesNearFadeDistance", 0f); m.SetFloat("_SoftParticlesFarFadeDistance", 6f);
        m.EnableKeyword("_SOFTPARTICLES_ON");
        m.SetFloat("_CameraFadingEnabled", 1f); m.SetFloat("_CameraNearFadeDistance", 2f); m.SetFloat("_CameraFarFadeDistance", 10f);
        m.EnableKeyword("_FADING_ON");
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }
}
