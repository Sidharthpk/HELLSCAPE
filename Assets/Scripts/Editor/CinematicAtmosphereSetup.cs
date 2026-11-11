using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Deliberate, repeatable installation. Originals remain untouched; no geometry is generated or moved.
public static class CinematicAtmosphereSetup
{
    public const string Folder = "Assets/Generated/CinematicAtmosphere";
    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    static T[] All<T>() where T : Component => Resources.FindObjectsOfTypeAll<T>().Where(c => c.gameObject.scene.isLoaded && c.gameObject.scene.name == "MainGameScene").ToArray();
    [MenuItem("HellScape/Lighting/Cinematic atmosphere — prototype street")]
    public static void Prototype() => Apply(false);
    [MenuItem("HellScape/Lighting/Cinematic atmosphere — city wide")]
    public static void City() => Apply(true);

    public static void Apply(bool cityWide)
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().name != "MainGameScene")
            throw new InvalidOperationException("Open MainGameScene and stop Play Mode first.");
        if (!File.Exists("Backups/CinematicAtmosphere-20261030/pre-overhaul.zip")) throw new InvalidOperationException("Create the pre-overhaul backup first.");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var look = All<SleepSequence>().Single(); var trees = All<HellTrees>().Single();
        Undo.RecordObject(look, "Cinematic exterior lighting");
        look.hellFogColor = Hex("344F51"); look.hellFogDensity = .008f;
        look.hellAmbientSky = Hex("3A565B"); look.hellAmbientEquator = Hex("263D41"); look.hellAmbientGround = Hex("10191C");
        look.hellSunColor = Hex("819FA6"); look.hellSunIntensity = .15f;
        // Keep key light orientation: no existing transform changes.
        look.sun.color = look.hellSunColor; look.sun.intensity = look.hellSunIntensity;
        look.sun.shadows = LightShadows.Soft; look.sun.shadowStrength = .85f;
        look.hellSkybox = Sky();
        RenderSettings.skybox = look.hellSkybox;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = look.hellAmbientSky; RenderSettings.ambientEquatorColor = look.hellAmbientEquator; RenderSettings.ambientGroundColor = look.hellAmbientGround;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = look.hellFogColor; RenderSettings.fogDensity = look.hellFogDensity;
        Grade();
        var city = look.hellWorld.GetComponent<DeadCityAtmosphere>();
        if (city == null) city = Undo.AddComponent<DeadCityAtmosphere>(look.hellWorld);
        city.look = look; city.trees = trees; city.vehicle = All<CarInteract>().First();
        city.playerCamera = look.playerRoot.GetComponentInChildren<Camera>(true); city.drivingCamera = city.vehicle.carCamera;
        city.explorationFog = look.hellFogColor; city.drivingFog = Hex("29494C"); city.denseFog = Hex("485B59");
        city.explorationDensity = .008f; city.drivingDensity = .0035f; city.denseDensity = .016f;
        city.scattering = .055f; city.scatteringDistance = 80f; city.anisotropy = .35f;
        // Selected back streets, in existing city space. No trigger objects or colliders.
        city.denseStreets = new[] { new Bounds(new Vector3(-90f, 3f, 65f), new Vector3(12f, 8f, 24f)) };
        Renderers(city);
        Lamps(trees, cityWide);
        Roads(cityWide);
        if (cityWide) Environment(look, trees);
        EditorUtility.SetDirty(look); EditorUtility.SetDirty(look.sun); EditorUtility.SetDirty(city); EditorUtility.SetDirty(trees);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(look.gameObject.scene); EditorSceneManager.SaveScene(look.gameObject.scene);
        Debug.Log("[CinematicAtmosphere] Applied " + (cityWide ? "city-wide" : "street prototype") + "; original assets retained.");
    }

    static T Copy<T>(T source, string name) where T : UnityEngine.Object
    {
        string path = Folder + "/" + name;
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path)) throw new IOException("Cannot copy " + source + " to " + path);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
    static T Effect<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet<T>(out var effect)) { effect = profile.Add<T>(); AssetDatabase.AddObjectToAsset(effect, profile); }
        effect.active = true; EditorUtility.SetDirty(effect); return effect;
    }
    static void Grade()
    {
        var source = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Generated/DeadCity/VP_HS_DeadCity_Base.asset");
        var profile = Copy(source, "VP_HS_CinematicDeadCity.asset");
        Effect<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
        var c = Effect<ColorAdjustments>(profile); c.postExposure.Override(-.2f); c.contrast.Override(16); c.saturation.Override(-16); c.colorFilter.Override(Hex("E5EDE9"));
        var white = Effect<WhiteBalance>(profile); white.temperature.Override(-7); white.tint.Override(0);
        var bloom = Effect<Bloom>(profile); bloom.threshold.Override(1.15f); bloom.intensity.Override(.22f); bloom.scatter.Override(.6f); bloom.highQualityFiltering.Override(false);
        Effect<Vignette>(profile).intensity.Override(.18f);
        Effect<ChromaticAberration>(profile).intensity.Override(.01f);
        Effect<MotionBlur>(profile).intensity.Override(0);
        Effect<DepthOfField>(profile).mode.Override(DepthOfFieldMode.Off);
        // Grain remains owned by RetroLook so the existing pause accessibility toggle works.
        if (profile.TryGet<FilmGrain>(out var grain)) { grain.active = false; EditorUtility.SetDirty(grain); }
        var volume = All<Volume>().Single(v => v.name == "Hell Global Volume");
        volume.sharedProfile = profile; EditorUtility.SetDirty(volume); EditorUtility.SetDirty(profile);
    }
    static Material Sky()
    {
        string source = "Assets/Generated/DeadCity/StormSkyCube.png", path = Folder + "/StormSkyCinematic.png";
        if (!File.Exists(path))
        {
            AssetDatabase.CopyAsset(source, path);
            var texture = new Texture2D(2, 2); texture.LoadImage(File.ReadAllBytes(source));
            int face = texture.width / 4;
            var pixels = texture.GetPixels();
            for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
            {
                int row = (texture.height - 1 - y) / face, column = x / face;
                if (row != 1 && column != 1) continue;
                float u = (x % face + .5f) / face * 2f - 1f;
                float v = 1f - ((texture.height - 1 - y) % face + .5f) / face * 2f;
                float elevation = row == 1 ? v / Mathf.Sqrt(1 + u * u + v * v) : (row == 0 ? 1f : -1f);
                // Preserve seamless clouds overhead; match the new distance fog at the horizon.
                float blend = 1f - Mathf.SmoothStep(0, 1, Mathf.Max(0, elevation) / .3f);
                pixels[y * texture.width + x] = Color.Lerp(pixels[y * texture.width + x], Hex("344F51"), blend * .94f);
            }
            texture.SetPixels(pixels); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }
        var sky = Copy(AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/DeadCity/MAT_HS_DeadCity_StormSky.mat"), "MAT_HS_CinematicStormSky.mat");
        sky.SetTexture("_Tex", AssetDatabase.LoadAssetAtPath<Texture>(path)); EditorUtility.SetDirty(sky); return sky;
    }
    static void Renderers(DeadCityAtmosphere city)
    {
        int selectedIndex = -1;
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets/Settings" }))
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
            var serial = new SerializedObject(pipeline); var list = serial.FindProperty("m_RendererDataList");
            var source = (UniversalRendererData)list.GetArrayElementAtIndex(0).objectReferenceValue;
            var renderer = Copy(source, source.name + "_Cinematic.asset");
            var ssao = renderer.rendererFeatures.FirstOrDefault(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion");
            if (ssao != null)
            {
                var ao = new SerializedObject(ssao); var settings = ao.FindProperty("m_Settings");
                settings.FindPropertyRelative("Intensity").floatValue = .65f;
                settings.FindPropertyRelative("Radius").floatValue = .3f;
                settings.FindPropertyRelative("Downsample").boolValue = true;
                settings.FindPropertyRelative("Source").intValue = 0; // Depth normals in URP 17.3
                settings.FindPropertyRelative("Samples").intValue = 1;
                settings.FindPropertyRelative("BlurQuality").intValue = 1;
                ao.ApplyModifiedPropertiesWithoutUndo();
            }
            var feature = renderer.rendererFeatures.OfType<DeadCityScatteringFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<DeadCityScatteringFeature>(); feature.name = "Dead City Local Scattering";
                AssetDatabase.AddObjectToAsset(feature, renderer); renderer.rendererFeatures.Add(feature);
            }
            feature.scatteringShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/DeadCityScattering.shader"); feature.Create();
            EditorUtility.SetDirty(feature); EditorUtility.SetDirty(renderer);
            int index = -1;
            for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer) index = i;
            if (index < 0) { index = list.arraySize; list.InsertArrayElementAtIndex(index); list.GetArrayElementAtIndex(index).objectReferenceValue = renderer; serial.ApplyModifiedPropertiesWithoutUndo(); }
            if (selectedIndex >= 0 && selectedIndex != index) throw new InvalidOperationException("Quality renderer indices differ; resolve explicitly.");
            selectedIndex = index;
        }
        foreach (var cam in new[] { city.playerCamera, city.drivingCamera }) { var data = cam.GetUniversalAdditionalCameraData(); data.SetRenderer(selectedIndex); EditorUtility.SetDirty(data); }
    }
    static Transform Pole(Transform t) { for (; t != null; t = t.parent) if (t.name.StartsWith("lamp_pole")) return t; return null; }
    static Texture2D Cookie()
    {
        string path = Folder + "/LampCookie.png";
        if (!File.Exists(path))
        {
            const int size = 128; var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size * 2 - 1, v = (y + .5f) / size * 2 - 1;
                float r = Mathf.Sqrt(u*u + v*v);
                float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.48f, 1f, r));
                float irregular = .76f + .24f * Mathf.PerlinNoise(u * 3 + 13.1f, v * 3 + 9.4f);
                float value = edge * irregular;
                pixels[y * size + x] = new Color(value, value, value, value);
            }
            tex.SetPixels(pixels); File.WriteAllBytes(path, tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex); AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.sRGBTexture = false; importer.wrapMode = TextureWrapMode.Clamp; importer.mipmapEnabled = true; importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static void Lamps(HellTrees trees, bool cityWide)
    {
        var cookie = Cookie(); int tuned = 0, shadowed = 0;
        var nearest = trees.lamps.Where(l => l != null).OrderBy(l => (l.transform.position - CinematicLightingReview.Street).sqrMagnitude).Take(2).ToArray();
        foreach (var light in trees.lamps)
        {
            if (light == null || (!cityWide && (light.transform.position - CinematicLightingReview.Street).sqrMagnitude > 90 * 90)) continue;
            var pole = Pole(light.transform); if (pole == null) continue;
            float height = 8;
            foreach (var hit in Physics.RaycastAll(light.transform.position + Vector3.up * .1f, Vector3.down, 30, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (hit.collider.transform.IsChildOf(pole) || hit.normal.y < .65f || hit.distance < 2.5f) continue;
                height = Mathf.Clamp(hit.distance - .1f, 4f, 14f); break;
            }
            bool green = light.color.g > light.color.r * 1.08f;
            light.type = LightType.Spot; light.color = Hex(green ? "B5DBAA" : "D8AA72");
            light.intensity = (green ? 1.5f : 2.5f) * height * height;
            light.range = Mathf.Clamp(height * 2.2f, 12f, 28f); light.spotAngle = 105; light.innerSpotAngle = 65;
            light.cookie = cookie;
            int hash = Mathf.Abs(Mathf.RoundToInt(pole.position.x) * 31 + Mathf.RoundToInt(pole.position.z));
            bool shadow = nearest.Contains(light) || hash % 11 == 0;
            light.shadows = shadow ? LightShadows.Soft : LightShadows.None; light.shadowStrength = .8f;
            light.shadowBias = .08f; light.shadowNormalBias = .25f;
            var extra = light.GetUniversalAdditionalLightData();
            var serializedLight = new SerializedObject(extra);
            serializedLight.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = 1;
            serializedLight.ApplyModifiedPropertiesWithoutUndo();
            if (shadow) shadowed++;
            EditorUtility.SetDirty(light); EditorUtility.SetDirty(extra); tuned++;
        }
        trees.maxLamps = 28;
        Debug.Log($"[CinematicAtmosphere] Tuned {tuned} existing working lamps; {shadowed} shadow-capable, nearest 28 active; broken lamps retained.");
    }
    static void Roads(bool cityWide)
    {
        int count=0;
        // HellWorld replaces the saved shared materials on enable. Update its BURNT mapping,
        // preserving clean/day materials, otherwise the runtime swap discards the matte variants.
        foreach(var appearance in All<BurntCityAppearance>())
        {
            foreach(var surface in appearance.surfaces)
            {
                if(surface.renderer==null || (!cityWide && surface.renderer.bounds.SqrDistance(CinematicLightingReview.Street)>90*90))continue;
                for(int i=0;i<surface.burnt.Length;i++)
                {
                    var source=surface.burnt[i];if(source==null || !source.name.StartsWith("asphalt_"))continue;
                    string name=source.name.Replace(" (worn red city)","").Replace("_WornWet","")+"_WornWet.mat";
                    var road=Copy(source,name);
                    ConfigureMatte(road);surface.burnt[i]=road;count++;
                }
            }
            EditorUtility.SetDirty(appearance);appearance.ShowBurnt(appearance.gameObject.activeSelf);
        }
        foreach(var renderer in All<MeshRenderer>())
        {
            if (!cityWide && renderer.bounds.SqrDistance(CinematicLightingReview.Street)>90*90) continue;
            var materials=renderer.sharedMaterials; bool changed=false;
            for(int i=0;i<materials.Length;i++)
            {
                var source=materials[i]; if(source==null || !source.name.StartsWith("asphalt_"))continue;
                if(AssetDatabase.GetAssetPath(source).StartsWith(Folder+"/",StringComparison.Ordinal))continue;
                var road=Copy(source,source.name+"_Cinematic.mat");
                if(!road.HasProperty("_Smoothness") || !road.HasProperty("_MetallicGlossMap"))continue;
                ConfigureMatte(road);
                // Keep the original shader, albedo, markings, normal and UVs.
                materials[i]=road; changed=true; count++; EditorUtility.SetDirty(road);
            }
            if(changed){renderer.sharedMaterials=materials; EditorUtility.SetDirty(renderer);}
        }
        Debug.Log("[CinematicAtmosphere] Asphalt slots set matte: "+count);
    }
    static void ConfigureMatte(Material road)
    {
        road.SetTexture("_MetallicGlossMap",null);road.SetFloat("_Smoothness",.035f);road.SetFloat("_Metallic",0);
        road.SetFloat("_SmoothnessTextureChannel",0);road.SetFloat("_EnvironmentReflections",0);
        road.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");road.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");road.DisableKeyword("_METALLICSPECGLOSSMAP");
        EditorUtility.SetDirty(road);
    }
    static void Environment(SleepSequence look,HellTrees trees)
    {
        trees.intensity=22f; trees.maxLit=8; trees.flickerFloor=.88f; trees.flickerSpan=.28f;
        foreach(var light in trees.lights) if(light!=null){light.range=10;light.shadows=LightShadows.None;EditorUtility.SetDirty(light);}
        var water=All<MeshRenderer>().First(r=>r.name=="WaterBlock_50m");
        var material=Copy(AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/DeadCity/Water_DeadCity.mat"),"Water_CinematicDeadCity.mat");
        material.SetColor("Color_36218622185947c6a5ae36366d8e21d8",Hex("071719"));
        var surface=Hex("102D32"); surface.a=.863f;material.SetColor("Color_93e06cd551a5449091bcde90b46765a0",surface);
        material.SetFloat("Vector1_6269b1025b26473ca8bc61634f34b537",.15f); water.sharedMaterial=material;
        EditorUtility.SetDirty(material);EditorUtility.SetDirty(water);
        var mist=Copy(AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/DeadCity/GroundFogTeal.mat"),"GroundFog_Cinematic.mat");
        mist.SetFloat("_SoftParticlesEnabled",1);mist.SetFloat("_SoftParticlesNearFadeDistance",0);mist.SetFloat("_SoftParticlesFarFadeDistance",2);
        mist.SetVector("_SoftParticleFadeParams",new Vector4(0,.5f,0,0));mist.EnableKeyword("_SOFTPARTICLES_ON");EditorUtility.SetDirty(mist);
        foreach(var renderer in look.mist)
        {
            if(renderer==null)continue;renderer.sharedMaterial=mist;EditorUtility.SetDirty(renderer);
            var ps=renderer.GetComponent<ParticleSystem>();if(ps==null)continue;
            var main=ps.main;main.startColor=new Color(1,1,1,.08f);main.maxParticles=Mathf.Min(main.maxParticles,100);
            main.startSize3D=true;main.startSizeX=new ParticleSystem.MinMaxCurve(12f,22f);
            main.startSizeY=new ParticleSystem.MinMaxCurve(.65f,1.3f);main.startSizeZ=1f;
            main.startRotation=new ParticleSystem.MinMaxCurve(-.06f,.06f);
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
            velocity.x=new ParticleSystem.MinMaxCurve(-.12f,.12f);velocity.y=new ParticleSystem.MinMaxCurve(0f,0f);velocity.z=new ParticleSystem.MinMaxCurve(-.08f,.08f);
            var emission=ps.emission;emission.rateOverTime=ps.name=="Ground Fog (Particle Pack)"?12f:2f;
            EditorUtility.SetDirty(ps);
        }
        foreach(var probe in All<ReflectionProbe>())
        {
            // Reuse existing volumes, bounds and positions; refresh across frames, never every frame.
            probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution=Mathf.Min(probe.resolution,128);probe.boxProjection=true;EditorUtility.SetDirty(probe);
        }
    }
}
