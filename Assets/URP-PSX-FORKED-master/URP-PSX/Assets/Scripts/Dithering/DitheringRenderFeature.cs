using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PSX
{
    public class DitheringRenderFeature : ScriptableRendererFeature
    {
        DitheringPass ditheringPass;

        public override void Create()
        {
            ditheringPass = new DitheringPass(RenderPassEvent.BeforeRenderingPostProcessing);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(ditheringPass);
        }

        protected override void Dispose(bool disposing)
        {
            ditheringPass?.Dispose();
        }
    }


    public class DitheringPass : PSXPostPass
    {
        static readonly int PatternIndex = Shader.PropertyToID("_PatternIndex");
        static readonly int DitherThreshold = Shader.PropertyToID("_DitherThreshold");
        static readonly int DitherStrength = Shader.PropertyToID("_DitherStrength");
        static readonly int DitherScale = Shader.PropertyToID("_DitherScale");

        public DitheringPass(RenderPassEvent evt) : base(evt, "PostEffect/Dithering", "Render Dithering Effects") { }

        protected override bool Prepare(Material mat)
        {
            var dithering = VolumeManager.instance.stack.GetComponent<Dithering>();
            if (dithering == null || !dithering.IsActive()) return false;

            mat.SetInt(PatternIndex, dithering.patternIndex.value);
            mat.SetFloat(DitherThreshold, dithering.ditherThreshold.value);
            mat.SetFloat(DitherStrength, dithering.ditherStrength.value);
            mat.SetFloat(DitherScale, dithering.ditherScale.value);
            return true;
        }
    }
}
