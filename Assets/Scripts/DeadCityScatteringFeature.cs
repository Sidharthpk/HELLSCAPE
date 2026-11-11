using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// The city's volumetric fog and the light it catches. Two Render Graph raster passes: half-size raymarch and depth-aware resolve.
// No history buffer: a stable spatial dither avoids temporal trails on moving cars and Echo cuts.
public sealed class DeadCityScatteringFeature : ScriptableRendererFeature
{
    public static int LastRenderedLightCount { get; private set; }
    public Shader scatteringShader;
    public Texture3D fogNoise;
    Material material;
    ScatteringPass pass;
    public override void Create()
    {
        CoreUtils.Destroy(material);
        if (scatteringShader == null) return;
        material = CoreUtils.CreateEngineMaterial(scatteringShader);
        pass = new ScatteringPass(material, fogNoise) { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
        pass.ConfigureInput(ScriptableRenderPassInput.Depth);
    }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var city = DeadCityAtmosphere.Active;
        if (pass == null || fogNoise == null || city == null || renderingData.cameraData.renderType != CameraRenderType.Base || !city.DrawsFog(renderingData.cameraData.camera)) return;
        renderer.EnqueuePass(pass);
    }
    protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

    sealed class ScatteringPass : ScriptableRenderPass
    {
        const int Slots = 12;
        readonly Material material;
        readonly Texture3D noise;
        readonly Vector4[] spheres = new Vector4[Slots];
        readonly Vector4[] indices = new Vector4[Slots];
        readonly float[] distances = new float[Slots];
        static readonly int ScatterTexture = Shader.PropertyToID("_HSScatterTexture");
        public ScatteringPass(Material material, Texture3D noise) { this.material = material; this.noise = noise; }
        sealed class PassData { public Material material; public TextureHandle source; public MaterialPropertyBlock properties; }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var camera = frameData.Get<UniversalCameraData>();
            var lights = frameData.Get<UniversalLightData>();
            var city = DeadCityAtmosphere.Active;
            if (city == null || !city.DrawsFog(camera.camera) || !resources.cameraDepthTexture.IsValid()) return;
            for (int i = 0; i < Slots; i++) { distances[i] = float.MaxValue; spheres[i] = Vector4.zero; indices[i] = Vector4.zero; }
            int count = 0, lightIndex = 0;
            for (int i = 0; i < lights.visibleLights.Length; i++)
            {
                if (i == lights.mainLightIndex) continue;
                int index = lightIndex++;
                if (index >= UniversalRenderPipeline.maxVisibleAdditionalLights) break;
                if (!city.scatteringEnabled) continue;
                var light = lights.visibleLights[i].light;
                if (light == null || (light.type != LightType.Spot && light.type != LightType.Point) || !city.IsStreetlight(light)) continue;
                float distance = (light.transform.position - camera.camera.transform.position).sqrMagnitude;
                if (distance > city.scatteringDistance * city.scatteringDistance || distance >= distances[Slots - 1]) continue;
                int slot = Slots - 1;
                while (slot > 0 && distance < distances[slot - 1])
                { distances[slot] = distances[slot - 1]; spheres[slot] = spheres[slot - 1]; indices[slot] = indices[slot - 1]; slot--; }
                distances[slot] = distance;
                var position = light.transform.position;
                spheres[slot] = new Vector4(position.x, position.y, position.z, light.range);
                indices[slot] = new Vector4(index, 0, 0, 0);
                count = Mathf.Min(count + 1, Slots);
            }
            LastRenderedLightCount = count;
            float fog = RenderSettings.fogDensity * city.fogStrength * city.FogWeight;
            if (count == 0 && fog <= 0f) return;
            var properties = new MaterialPropertyBlock();
            properties.SetVectorArray("_HSBeamSpheres", spheres);
            properties.SetVectorArray("_HSBeamIndices", indices);
            float densityScale = Mathf.Clamp(RenderSettings.fogDensity / city.explorationDensity, .4f, 2f);
            properties.SetVector("_HSScattering", new Vector4(city.scattering * densityScale * city.FogWeight, city.anisotropy, city.scatteringDistance, count));
            properties.SetVector("_HSFog", new Vector4(fog, city.fogTop, 1f / city.fogHeightFalloff, camera.camera.farClipPlane));
            properties.SetVector("_HSFogBlend", new Vector4(city.skyBlendStart, city.skyBlendEnd, 0f, 0f));
            Color near = RenderSettings.fogColor.linear;
            properties.SetVector("_HSFogNear", new Vector4(near.r, near.g, near.b, 0f));
            properties.SetTexture("_HSFogNoise", noise);
            // the skybox itself, with the same maths as Skybox/Cubemap, so distant buildings dissolve into exactly what is behind them
            var sky = RenderSettings.skybox;
            var cube = sky != null && sky.HasProperty("_Tex") ? sky.GetTexture("_Tex") as Cubemap : null;
            Vector4 skyTint = Vector4.zero;
            if (cube != null)
            {
                Color tint = sky.GetColor("_Tint").linear * 4.594794f * Mathf.GammaToLinearSpace(sky.GetFloat("_Exposure"));   // ([Gamma] on the shader's property)
                skyTint = new Vector4(tint.r, tint.g, tint.b, 1f);
                properties.SetTexture("_HSFogSky", cube);
            }
            properties.SetVector("_HSFogSkyTint", skyTint);
            var desc = graph.GetTextureDesc(resources.activeColorTexture);
            desc.name = "HellScape Volumetric Fog Half";
            desc.width = Mathf.Max(1, camera.cameraTargetDescriptor.width / 2);
            desc.height = Mathf.Max(1, camera.cameraTargetDescriptor.height / 2);
            desc.msaaSamples = MSAASamples.None;
            desc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
            desc.clearBuffer = false;
            var half = graph.CreateTexture(desc);
            using (var builder = graph.AddRasterRenderPass<PassData>("HellScape Volumetric Fog (half)", out var data))
            {
                data.material = material; data.properties = properties;
                builder.UseTexture(resources.cameraDepthTexture);
                if (resources.additionalShadowsTexture.IsValid()) builder.UseTexture(resources.additionalShadowsTexture);
                builder.SetRenderAttachment(half, 0);
                builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                    context.cmd.DrawProcedural(Matrix4x4.identity, d.material, 0, MeshTopology.Triangles, 3, 1, d.properties));
                builder.SetGlobalTextureAfterPass(half, ScatterTexture);
            }
            using (var builder = graph.AddRasterRenderPass<PassData>("HellScape Volumetric Fog Resolve", out var data))
            {
                data.material = material;
                data.properties = new MaterialPropertyBlock();
                data.properties.SetVector("_HSScatterTexture_TexelSize", new Vector4(1f / desc.width, 1f / desc.height, desc.width, desc.height));
                builder.UseTexture(half); builder.UseTexture(resources.cameraDepthTexture);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                    context.cmd.DrawProcedural(Matrix4x4.identity, d.material, 1, MeshTopology.Triangles, 3, 1, d.properties));
            }
        }
    }
}
