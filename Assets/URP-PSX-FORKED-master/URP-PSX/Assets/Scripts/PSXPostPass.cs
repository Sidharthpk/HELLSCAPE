using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace PSX
{
    // Shared Render Graph pass for the PSX post effects (Unity 6 / URP 17).
    // The shaders are legacy CG that read _MainTex from a fullscreen quad, so we
    // run cmd.Blit inside an unsafe pass instead of rewriting them for Blitter.
    public abstract class PSXPostPass : ScriptableRenderPass
    {
        protected Material material;
        readonly string effectName;

        class PassData
        {
            public TextureHandle source;
            public TextureHandle temp;
            public Material material;
        }

        protected PSXPostPass(RenderPassEvent evt, string shaderPath, string passName)
        {
            renderPassEvent = evt;
            effectName = passName;
            requiresIntermediateTexture = true;          // we read and write the camera color
            ConfigureInput(ScriptableRenderPassInput.Depth); // Fog and CRT sample _CameraDepthTexture

            var shader = Shader.Find(shaderPath);
            if (shader == null)
            {
                Debug.LogError($"Shader not found: {shaderPath}");
                return;
            }
            material = CoreUtils.CreateEngineMaterial(shader);
        }

        // Read the volume component and push its values into the material.
        // Return false to skip the pass this frame.
        protected abstract bool Prepare(Material mat);

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            if (!cameraData.postProcessEnabled) return;
            if (!Prepare(material)) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle source = resourceData.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(source);
            desc.name = effectName + "_Temp";
            desc.clearBuffer = false;
            desc.filterMode = FilterMode.Point;
            TextureHandle temp = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddUnsafePass<PassData>(effectName, out var data))
            {
                data.source = source;
                data.temp = temp;
                data.material = material;

                builder.UseTexture(source, AccessFlags.ReadWrite);
                builder.UseTexture(temp, AccessFlags.ReadWrite);
                if (resourceData.cameraDepthTexture.IsValid())
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData d, UnsafeGraphContext ctx) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                    RenderTargetIdentifier src = d.source; // explicit: TextureHandle converts to several types
                    RenderTargetIdentifier tmp = d.temp;
                    cmd.Blit(src, tmp);
                    cmd.Blit(tmp, src, d.material, 0);
                });
            }
        }

        public void Dispose() => CoreUtils.Destroy(material);
    }
}
