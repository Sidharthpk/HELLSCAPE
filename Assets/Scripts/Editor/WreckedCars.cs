using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Makes a pack car look wrecked: each paint texture stays (never a recolour), but is darkened and dirtied with a tiling
// rust/soot layer (URP detail map), flat and matte, the glass grimed and the lamps dead; the body sits crooked, sometimes
// crushed, sometimes with a wheel gone. CityCars calls this for the red city's cars and its road wrecks.
public static class WreckedCars
{
    const string Dir = "Assets/Generated/Wrecks";

    // a tiling grey layer for URP's x2 detail blend: 0.5 leaves the paint alone, darker is rust and soot
    static Texture2D Detail(bool burnt)
    {
        Directory.CreateDirectory(Dir);
        string path = $"{Dir}/wreck_detail_{(burnt ? "soot" : "rust")}.png";
        var have = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (have != null) return have;
        const int N = 512;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var px = new Color[N * N];
        float off = burnt ? 31.7f : 7.3f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float big = Fbm(u, v, 4, off), fine = Fbm(u, v, 24, off + 11f), streak = Fbm(u * 0.4f, v, 6, off + 5f);
                float g = burnt ? Mathf.Lerp(0.36f, 0.05f, Mathf.SmoothStep(0.25f, 0.8f, big * 0.7f + streak * 0.4f))
                                : Mathf.Lerp(0.5f, 0.12f, Mathf.SmoothStep(0.35f, 0.75f, big * 0.8f + fine * 0.25f));
                g *= Mathf.Lerp(0.85f, 1.1f, fine);
                // rust leans orange-brown, soot stays neutral
                px[y * N + x] = burnt ? new Color(g, g * 0.97f, g * 0.94f, 1f) : new Color(g * 1.12f, g * 0.9f, g * 0.7f, 1f);
            }
        t.SetPixels(px); t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.sRGBTexture = true; ti.wrapMode = TextureWrapMode.Repeat; ti.mipmapEnabled = true; ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // tiling value noise (wraps every `freq` cells)
    static float Fbm(float u, float v, int freq, float seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int o = 0; o < 4; o++)
        {
            sum += amp * Noise(u, v, freq << o, seed + o * 13.1f); norm += amp; amp *= 0.5f;
        }
        return sum / norm;
    }

    static float Noise(float u, float v, int f, float seed)
    {
        float x = u * f, y = v * f;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0; fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        float H(int i, int j) { i = ((i % f) + f) % f; j = ((j % f) + f) % f; float s = Mathf.Sin(i * 127.1f + j * 311.7f + seed * 74.7f) * 43758.5453f; return s - Mathf.Floor(s); }
        return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), fx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), fx), fy);
    }

    static Material Wrecked(Material src, bool burnt)
    {
        string path = $"{Dir}/wreck_{src.name}_{(burnt ? "burnt" : "rust")}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        Directory.CreateDirectory(Dir);
        m = new Material(src) { name = Path.GetFileNameWithoutExtension(path) };
        string n = src.name.ToLower();
        bool glass = n.Contains("glass"), lamp = n.Contains("optic"), wheel = n.Contains("wheel");
        Color c = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.white;
        if (glass) { c = Color.Lerp(c, new Color(0.12f, 0.11f, 0.1f, Mathf.Max(c.a, 0.85f)), 0.6f); }
        else if (lamp) c = c * 0.25f;
        else if (wheel) c = c * 0.6f;
        else c = (burnt ? new Color(0.26f, 0.24f, 0.23f) : new Color(0.62f, 0.5f, 0.42f)) * c;
        c.a = glass ? c.a : 1f;
        m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", glass ? 0f : 0.1f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", glass ? 0.35f : 0.1f);
        if (m.HasProperty("_Surface") && glass) { m.SetFloat("_Surface", 1f); }
        if (!glass && !lamp)
        {
            m.SetTexture("_DetailAlbedoMap", Detail(burnt));
            m.SetTextureScale("_DetailAlbedoMap", wheel ? new Vector2(2f, 2f) : new Vector2(3f, 3f));
            m.SetFloat("_DetailAlbedoMapScale", 1f);
            m.EnableKeyword("_DETAIL_MULX2");
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    public static bool IsWrecked(Transform car) => car.GetComponentsInChildren<Renderer>(true).Any(r => r.sharedMaterials.Any(m => m != null && m.name.StartsWith("wreck_")));

    // `car` is the pack car's root (the prefab instance: Model/Body, Model/Wheel xx)
    public static void Apply(Transform car, int seed)
    {
        if (IsWrecked(car)) return;
        var rnd = new System.Random(seed * 7919 + 13);
        bool burnt = rnd.NextDouble() < 0.4;
        foreach (var r in car.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) if (mats[i] != null) mats[i] = Wrecked(mats[i], burnt);
            r.sharedMaterials = mats;
        }
        var model = car.Find("Model");
        if (model == null) return;
        float roll = ((float)rnd.NextDouble() - 0.5f) * 8f, pitch = ((float)rnd.NextDouble() - 0.5f) * 5f;
        var wheels = model.Cast<Transform>().Where(t => t.name.StartsWith("Wheel")).ToList();
        if (wheels.Count > 0 && rnd.NextDouble() < 0.5)
        {
            // a wheel gone: that corner drops
            var w = wheels[rnd.Next(wheels.Count)];
            Vector3 p = model.InverseTransformPoint(w.position);
            roll = p.x < 0f ? -6.5f : 6.5f; pitch = p.z > 0f ? 4f : -4f;
            w.gameObject.SetActive(false);
        }
        model.localRotation = Quaternion.Euler(pitch, 0f, roll);
        if (rnd.NextDouble() < 0.4) model.localScale = new Vector3(1f, 0.9f, 1f);   // crushed roof
        model.localPosition = new Vector3(0f, 0.02f, 0f);
    }
}
