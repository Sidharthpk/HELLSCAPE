using System.IO;
using UnityEditor;
using UnityEngine;

// Turns the office model's four textures into a burnt-out version (soot, char, scorch halos, ash, a few ember cracks) and
// puts them on copies of its materials, so the originals stay as they were. Same size and filtering as the originals
// (they are small pixel textures), and every noise layer wraps, so tiling shows no seams.
public static class BurntOffice
{
    const string Dir = "Assets/Generated/Burnt";
    const string OriginalDir = "Assets/New Folder/lowpoly-mobile-cartoon-office-rooms/textures/";
    const string ModelName = "Sketchfab_2023_11_11_16_15_44";

    // texture name -> how much of it burns (ceilings take the most soot, furniture the least)
    static readonly (string tex, float burn)[] Targets = { ("wall1", 0.78f), ("floor1", 0.85f), ("ceiling1", 0.95f), ("misc", 0.7f) };

    [MenuItem("HellScape/Office/Burn Down The Office")]
    public static void Run()
    {
        var root = GameObject.Find(ModelName);
        if (root == null) { Debug.LogError("Office model not found in the open scene."); return; }
        Directory.CreateDirectory(Dir);
        int swapped = 0;
        var done = new System.Collections.Generic.HashSet<string>();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null || !m.HasProperty("_BaseMap") || m.GetTexture("_BaseMap") == null) continue;
                var src = (Texture2D)m.GetTexture("_BaseMap");
                if (src.name.StartsWith("burnt_"))   // already swapped on an earlier run: regenerate from the original
                    src = AssetDatabase.LoadAssetAtPath<Texture2D>(OriginalDir + src.name.Substring(6) + ".png");
                if (src == null) continue;
                var t = System.Array.Find(Targets, x => x.tex == src.name);
                if (t.tex == null) continue;
                string pngPath = $"{Dir}/burnt_{src.name}.png";
                string matPath = $"{Dir}/{(m.name.StartsWith("burnt_") ? m.name : "burnt_" + m.name)}.mat";
                if (!done.Contains(pngPath)) { Burn(src, t.burn, pngPath); done.Add(pngPath); }   // rewritten in place: meta (GUID) stays, so the materials keep their texture
                var nm = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (nm == null)
                {
                    nm = new Material(m) { name = m.name.StartsWith("burnt_") ? m.name : "burnt_" + m.name };
                    nm.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath));
                    nm.SetColor("_BaseColor", Color.white);
                    if (nm.HasProperty("_Smoothness")) nm.SetFloat("_Smoothness", 0.05f);
                    AssetDatabase.CreateAsset(nm, matPath);
                }
                mats[i] = nm; swapped++;
            }
            r.sharedMaterials = mats;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Office burnt down: {swapped} material slots swapped.");
    }

    static void Burn(Texture2D src, float burn, string pngPath)
    {
        int w0 = src.width, h0 = src.height;
        // read the (possibly non-readable) original at its own size through a render texture
        var rt = RenderTexture.GetTemporary(w0, h0, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var read = new Texture2D(w0, h0, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, w0, h0), 0, 0); read.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        var srcPx = read.GetPixels();
        Object.DestroyImmediate(read);

        // smooth, higher-resolution output: the original is sampled bilinearly, so nothing is blocky
        int w = src.name == "ceiling1" ? 512 : 1024, h = w;
        var px = new Color[w * h];
        int seed = src.name.GetHashCode() & 0xffff;
        bool floor = src.name == "floor1", ceiling = src.name == "ceiling1";
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                Color c = Sample(srcPx, w0, h0, u, v);
                float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;

                float big = Fbm(u, v, 3, seed);                  // where the fire took hold
                float mid = Fbm(u, v, 9, seed + 7);              // smoky mottling
                float grain = Fbm(u, v, 96, seed + 13);          // very fine grime, soft
                float rise = Fbm2(u, v, 10, 2, seed + 21);   // soot rising up the surface: long, soft streaks
                float s = Mathf.SmoothStep(0.1f, 0.62f, big * 0.55f + rise * 0.4f + mid * 0.15f + 0.1f) * (burn + 0.2f);
                s = Mathf.Clamp01(s);

                // what's left of the original: drained of colour and nearly black
                Color old = Color.Lerp(c, new Color(lum, lum, lum), 0.7f) * 0.32f;
                // char: cold, blue-black, never warm and never flat
                Color charCol = new Color(0.028f, 0.027f, 0.03f) * (0.7f + mid * 0.6f);
                Color o = Color.Lerp(old, charCol, Mathf.SmoothStep(0.2f, 0.85f, s));

                // faint grey ash dusting on the least burnt, flat surfaces (soft, not flecks)
                o = Color.Lerp(o, new Color(0.19f, 0.19f, 0.2f), Mathf.SmoothStep(0.55f, 0.9f, mid) * (1f - s) * 0.25f);

                // old heat staining: a dull brown only where the char thins out
                float halo = Mathf.Clamp01(1f - Mathf.Abs(s - 0.4f) / 0.22f);
                o = Color.Lerp(o, new Color(0.13f, 0.085f, 0.06f) * (0.7f + mid * 0.5f), halo * 0.45f);

                // dried blood: dark, wide, soft-edged runs down the walls; broad old stains on the floor. Subtle, never bright.
                if (!ceiling)
                {
                    float stain;
                    if (floor) stain = Mathf.SmoothStep(0.6f, 0.78f, Fbm(u, v, 2, seed + 55) * 0.75f + mid * 0.25f);
                    else
                    {
                        float run = Fbm2(u, v, 7, 2, seed + 77);               // narrow in u, long in v
                        float fall = Mathf.SmoothStep(0.55f, 0.15f, v - (0.35f + 0.5f * Fbm(u, 0.5f, 5, seed + 88)));
                        stain = Mathf.SmoothStep(0.62f, 0.78f, run) * Mathf.Clamp01(fall + 0.35f) * 0.9f;
                    }
                    o = Color.Lerp(o, new Color(0.14f, 0.012f, 0.012f) * (0.7f + grain * 0.5f), stain * 0.8f);
                }

                o *= 0.92f + grain * 0.16f;    // grime, soft
                px[y * w + x] = new Color(o.r, o.g, o.b, 1f);
            }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(pngPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(pngPath);
        var imp = (TextureImporter)AssetImporter.GetAtPath(pngPath);
        imp.filterMode = FilterMode.Trilinear; imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true; imp.anisoLevel = 4;
        imp.maxTextureSize = 1024; imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
    }

    static Color Sample(Color[] px, int w, int h, float u, float v)
    {
        float fx = u * w - 0.5f, fy = v * h - 0.5f;
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0, ty = fy - y0;
        Color a = px[Mod(y0, h) * w + Mod(x0, w)], b = px[Mod(y0, h) * w + Mod(x0 + 1, w)];
        Color c = px[Mod(y0 + 1, h) * w + Mod(x0, w)], d = px[Mod(y0 + 1, h) * w + Mod(x0 + 1, w)];
        return Color.Lerp(Color.Lerp(a, b, tx), Color.Lerp(c, d, tx), ty);
    }

    // value noise that wraps every 'freq' cells, summed over 4 octaves
    static float Fbm(float u, float v, int freq, int seed) => Fbm2(u, v, freq, freq, seed);

    // same, with separate frequencies across and up the surface (long soft streaks stay seamless when tiled)
    static float Fbm2(float u, float v, int fu, int fv, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int o = 0; o < 4; o++)
        {
            sum += amp * Value(u * fu, v * fv, fu, fv, seed + o * 31);
            norm += amp; amp *= 0.5f; fu *= 2; fv *= 2;
        }
        return sum / norm;
    }

    static float Value(float x, float y, int pu, int pv, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        float a = Hash(Mod(x0, pu), Mod(y0, pv), seed), b = Hash(Mod(x0 + 1, pu), Mod(y0, pv), seed);
        float c = Hash(Mod(x0, pu), Mod(y0 + 1, pv), seed), d = Hash(Mod(x0 + 1, pu), Mod(y0 + 1, pv), seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static int Mod(int a, int m) => ((a % m) + m) % m;

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffffff) / (float)0x1000000;
        }
    }
}
