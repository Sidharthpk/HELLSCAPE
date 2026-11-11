using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The office's dark, realistic look: screen-space outline (surface colour pushed into shadow) and a local post
// volume (bloom, DoF, chromatic aberration, vignette, colour adjustments). No lens flares: they looked broken.
public static class OfficeLook
{
    const string Dir = "Assets/Generated/OfficeLook";

    [MenuItem("HellScape/Office Look (outline, flare, post volume)")]
    public static void Apply()
    {
        Directory.CreateDirectory(Dir);
        var (mat, psx) = Passes();

        var office = Object.FindObjectsByType<InteriorDarkness>(FindObjectsSortMode.None).First(d => d.blackoutUntilPowered);

        var gate = office.GetComponent<OfficeOutlineGate>() ?? office.gameObject.AddComponent<OfficeOutlineGate>();
        gate.player = office.player; gate.zone = office.zone; gate.outline = mat; gate.psx = psx;

        var vol = GameObject.Find("Office Look")?.GetComponent<Volume>();
        if (vol == null)
        {
            var go = new GameObject("Office Look");
            vol = go.AddComponent<Volume>();
            go.AddComponent<BoxCollider>().isTrigger = true;
        }
        vol.transform.position = office.zone.center;
        var box = vol.GetComponent<BoxCollider>(); box.size = office.zone.size;
        vol.isGlobal = false; vol.priority = 10f; vol.blendDistance = 4f; vol.weight = 1f;
        var prof = Load<VolumeProfile>(Dir + "/OfficeLook.asset", () => ScriptableObject.CreateInstance<VolumeProfile>());
        Fill(prof);
        Corridor(prof);
        vol.sharedProfile = prof;
        Rooms(vol.transform);
        Ssao();

        var dread = office.GetComponent<OfficeDread>() ?? office.gameObject.AddComponent<OfficeDread>();
        dread.player = office.player; dread.zone = office.zone;
        dread.health = office.player ? office.player.GetComponentInParent<PlayerHealth>() : null;
        EditorUtility.SetDirty(dread);

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();
        Debug.Log("Office look applied");
    }

    // RE Engine grade, the corridor half (the whole office's base): ACES, crushed blacks, dark red shadows,
    // dull warm highlights, a tight lens. Film grain stays with RetroLook (the pause menu switches it).
    static void Corridor(VolumeProfile p)
    {
        Effect<Tonemapping>(p).mode.Override(TonemappingMode.ACES);
        var col = Effect<ColorAdjustments>(p);
        col.postExposure.Override(0.9f); col.contrast.Override(20f); col.saturation.Override(-38f);
        col.colorFilter.Override(new Color(1f, 0.88f, 0.86f));   // (no teal: the dark red is the only tint)
        var smh = Effect<ShadowsMidtonesHighlights>(p);
        smh.shadows.Override(new Vector4(1f, 0.82f, 0.82f, -0.22f));
        smh.midtones.Override(new Vector4(1f, 0.97f, 0.96f, 0f));
        smh.highlights.Override(new Vector4(1f, 0.98f, 0.94f, 0f));
        var split = Effect<SplitToning>(p);
        split.shadows.Override(Hex("#4A1210")); split.highlights.Override(Hex("#C9A890")); split.balance.Override(-10f);
        var vig = Effect<Vignette>(p);
        vig.intensity.Override(0.42f); vig.smoothness.Override(0.4f); vig.color.Override(Hex("#050807")); vig.active = true;
        var lens = Effect<LensDistortion>(p);
        lens.intensity.Override(0.08f); lens.scale.Override(1f);
        var bloom = Effect<Bloom>(p);
        bloom.threshold.Override(0.9f); bloom.intensity.Override(0.7f); bloom.scatter.Override(0.75f);
        bloom.tint.Override(new Color(1f, 0.9f, 0.85f));
        bloom.dirtTexture.Override(AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/LensDirt.png") ?? MakeDirt()); bloom.dirtIntensity.Override(2.5f);
        EditorUtility.SetDirty(p);
    }

    // The rooms off the corridors: a darker, dirty-yellow grade on top (RPD-style: cold halls, warm pits).
    static readonly string[] Halls = { "MAIN HALL", "OPEN OFFICE", "WEST CORRIDOR", "ENTRANCE LOBBY" };

    static void Rooms(Transform look)
    {
        var prof = Load<VolumeProfile>(Dir + "/OfficeRoom.asset", () => ScriptableObject.CreateInstance<VolumeProfile>());
        var col = Effect<ColorAdjustments>(prof);
        col.postExposure.Override(1.75f); col.saturation.Override(-30f); col.colorFilter.Override(new Color(1f, 0.9f, 0.72f));
        var smh = Effect<ShadowsMidtonesHighlights>(prof);
        smh.shadows.Override(new Vector4(0.95f, 0.9f, 0.8f, -0.05f));
        var split = Effect<SplitToning>(prof);
        split.shadows.Override(Hex("#1A1410")); split.highlights.Override(Hex("#B89A5A")); split.balance.Override(-15f);
        var vig = Effect<Vignette>(prof);
        vig.intensity.Override(0.4f); vig.color.Override(Color.black);
        EditorUtility.SetDirty(prof);

        var holder = look.Find("Rooms");
        if (holder != null) Object.DestroyImmediate(holder.gameObject);
        holder = new GameObject("Rooms").transform;
        holder.SetParent(look, false);
        foreach (var r in NewOfficeFit.V3Rooms)
        {
            if (Halls.Contains(r.room)) continue;
            var go = new GameObject(r.room);
            go.transform.SetParent(holder, false);
            go.transform.position = new Vector3(NewOfficeFit.DoorX + (r.e0 + r.e1) / 2f, 5.4f, NewOfficeFit.DoorZ + (r.n0 + r.n1) / 2f);
            var box = go.AddComponent<BoxCollider>(); box.isTrigger = true;
            box.size = new Vector3(r.e1 - r.e0 - 0.6f, 4f, r.n1 - r.n0 - 0.6f);   // inside the walls: the 1 m blend fades at the doorway
            var v = go.AddComponent<Volume>();
            v.isGlobal = false; v.priority = 11f; v.blendDistance = 1f; v.sharedProfile = prof;
        }
    }

    // Deeper contact shadows under desks and fixtures. Renderer-wide (URP has no per-volume SSAO): the city gets it too.
    static void Ssao()
    {
        foreach (var g in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/Settings" }))
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(g)).Where(o => o is ScreenSpaceAmbientOcclusion))
        {
            var so = new SerializedObject(o);
            void F(string n, float v) => so.FindProperty("m_Settings." + n).floatValue = v;
            F("Intensity", 1.4f); F("Radius", 0.4f); F("DirectLightingStrength", 0.3f); F("Falloff", 40f);
            so.ApplyModifiedProperties();
        }
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    // smudges and specks for bloom's lens dirt
    static Texture2D MakeDirt()
    {
        const int N = 512;
        var tex = new Texture2D(N, N, TextureFormat.RGB24, false);
        var rng = new System.Random(3);
        var px = new Color[N * N];
        for (int s = 0; s < 140; s++)
        {
            float cx = rng.Next(N), cy = rng.Next(N), r = s < 20 ? 30 + rng.Next(70) : 2 + rng.Next(10), a = 0.15f + (float)rng.NextDouble() * 0.35f;
            for (int y = (int)Mathf.Max(0, cy - r); y < Mathf.Min(N, cy + r); y++)
            for (int x = (int)Mathf.Max(0, cx - r); x < Mathf.Min(N, cx + r); x++)
            {
                float d = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) / r);
                px[y * N + x] += Color.white * a * d * d;
            }
        }
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(Dir + "/LensDirt.png", tex.EncodeToPNG());
        AssetDatabase.ImportAsset(Dir + "/LensDirt.png");
        return AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/LensDirt.png");
    }

    // The prologue (open street, afternoon): same stack, global, and much less light and colour.
    [MenuItem("HellScape/Prologue Look (darker, desaturated)")]
    public static void ApplyPrologue()
    {
        Directory.CreateDirectory(Dir);
        var (mat, psx) = Passes();

        var go = GameObject.Find("Prologue Look") ?? new GameObject("Prologue Look");
        var vol = go.GetComponent<Volume>() ?? go.AddComponent<Volume>();
        vol.isGlobal = true; vol.priority = 7f; vol.weight = 1f;   // above the afternoon atmosphere (5), under the scripted Cctv/Hell looks (10)
        var prof = Load<VolumeProfile>(Dir + "/PrologueLook.asset", () => ScriptableObject.CreateInstance<VolumeProfile>());
        Fill(prof, -0.1f, 4f, -35f, 0f, 18f, 70f);
        vol.sharedProfile = prof;

        var player = GameObject.FindWithTag("Player");
        var gate = go.GetComponent<OfficeOutlineGate>() ?? go.AddComponent<OfficeOutlineGate>();
        gate.player = player ? player.transform : null; gate.zone = new Bounds(Vector3.zero, Vector3.one * 100000f); gate.outline = mat; gate.psx = psx; gate.strength = 0.5f;

        // less light: sun, ambient, a denser and duller haze
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.name == "Sun")
            {
                l.intensity = 1.0f; l.color = new Color(0.92f, 0.85f, 0.76f);
            }
        RenderSettings.ambientIntensity = 0.55f; RenderSettings.reflectionIntensity = 0.3f;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.006f; RenderSettings.fogColor = new Color(0.36f, 0.38f, 0.4f);

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();
        Debug.Log("Prologue look applied");
    }

    // The two fullscreen passes on every renderer: the outline (before post) and the PSX dither (after post, so it sees the final picture).
    static (Material outline, Material psx) Passes()
    {
        Directory.CreateDirectory(Dir);
        var outline = Load<Material>(Dir + "/OfficeOutline.mat", () => new Material(Shader.Find("Hidden/HellScape/OfficeOutline")));
        var psx = Load<Material>(Dir + "/PSXDither.mat", () => new Material(Shader.Find("Hidden/HellScape/PSXDither")));
        outline.SetFloat("_Strength", 0f); psx.SetFloat("_Amount", 0f);
        EditorUtility.SetDirty(outline); EditorUtility.SetDirty(psx);
        foreach (var g in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/Settings" }))
        {
            var r = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(g));
            AddFeature(r, outline, "Office Outline", FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing, ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            AddFeature(r, psx, "PSX Dither", FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing, ScriptableRenderPassInput.None);
        }
        return (outline, psx);
    }

    static void AddFeature(UniversalRendererData r, Material m, string name, FullScreenPassRendererFeature.InjectionPoint at, ScriptableRenderPassInput needs)
    {
        if (r.rendererFeatures.OfType<FullScreenPassRendererFeature>().Any(f => f.passMaterial == m)) return;
        var f = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
        f.name = name; f.passMaterial = m; f.injectionPoint = at; f.requirements = needs; f.fetchColorBuffer = true;
        AssetDatabase.AddObjectToAsset(f, r);
        r.rendererFeatures.Add(f);
        EditorUtility.SetDirty(r);
    }

    static T Effect<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (!p.TryGet<T>(out var c)) { c = p.Add<T>(true); AssetDatabase.AddObjectToAsset(c, p); }
        return c;
    }

    static void Fill(VolumeProfile p, float exposure = -0.7f, float contrast = 22f, float saturation = -24f, float vignette = 0.5f, float dofStart = 9f, float dofEnd = 28f)
    {
        var bloom = Effect<Bloom>(p);
        bloom.threshold.Override(0.85f); bloom.intensity.Override(0.9f); bloom.scatter.Override(0.72f);
        bloom.tint.Override(new Color(1f, 0.82f, 0.75f));
        var dof = Effect<DepthOfField>(p);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(dofStart); dof.gaussianEnd.Override(dofEnd); dof.gaussianMaxRadius.Override(1.2f); dof.highQualitySampling.Override(true);
        var ca = Effect<ChromaticAberration>(p);
        ca.intensity.Override(0.14f);
        var vig = Effect<Vignette>(p);
        vig.intensity.Override(vignette); vig.active = vignette > 0f; vig.smoothness.Override(0.55f); vig.rounded.Override(true);
        var col = Effect<ColorAdjustments>(p);
        col.postExposure.Override(exposure); col.contrast.Override(contrast); col.saturation.Override(saturation);
        col.colorFilter.Override(new Color(0.93f, 0.9f, 0.92f));
        var shadows = Effect<ShadowsMidtonesHighlights>(p);
        shadows.shadows.Override(new Vector4(0.9f, 0.95f, 1.1f, contrast > 10f ? -0.25f : -0.05f));
        EditorUtility.SetDirty(p);
    }

    static T Load<T>(string path, System.Func<T> make) where T : Object
    {
        var o = AssetDatabase.LoadAssetAtPath<T>(path);
        if (o != null) return o;
        o = make();
        AssetDatabase.CreateAsset(o, path);
        return o;
    }
}
