using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PSX
{
    public class PixelationRenderFeature : ScriptableRendererFeature
    {
        PixelationPass pixelationPass;

        public override void Create()
        {
            pixelationPass = new PixelationPass(RenderPassEvent.BeforeRenderingPostProcessing);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(pixelationPass);
        }

        protected override void Dispose(bool disposing)
        {
            pixelationPass?.Dispose();
        }
    }


    public class PixelationPass : PSXPostPass
    {
        static readonly int WidthPixelation = Shader.PropertyToID("_WidthPixelation");
        static readonly int HeightPixelation = Shader.PropertyToID("_HeightPixelation");
        static readonly int ColorPrecison = Shader.PropertyToID("_ColorPrecision");

        public PixelationPass(RenderPassEvent evt) : base(evt, "PostEffect/Pixelation", "Render Pixelation Effects") { }

        protected override bool Prepare(Material mat)
        {
            var pixelation = VolumeManager.instance.stack.GetComponent<Pixelation>();
            if (pixelation == null || !pixelation.IsActive()) return false;

            mat.SetFloat(WidthPixelation, pixelation.widthPixelation.value);
            mat.SetFloat(HeightPixelation, pixelation.heightPixelation.value);
            mat.SetFloat(ColorPrecison, pixelation.colorPrecision.value);
            return true;
        }
    }
}
