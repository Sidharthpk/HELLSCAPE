using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PSX
{
    public class CRTRenderFeature : ScriptableRendererFeature
    {
        CRTPass crtPass;

        public override void Create()
        {
            crtPass = new CRTPass(RenderPassEvent.BeforeRenderingPostProcessing);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(crtPass);
        }

        protected override void Dispose(bool disposing)
        {
            crtPass?.Dispose();
        }
    }


    public class CRTPass : PSXPostPass
    {
        static readonly int ScanLinesWeight = Shader.PropertyToID("_ScanlinesWeight");
        static readonly int NoiseWeight = Shader.PropertyToID("_NoiseWeight");
        static readonly int ScreenBendX = Shader.PropertyToID("_ScreenBendX");
        static readonly int ScreenBendY = Shader.PropertyToID("_ScreenBendY");
        static readonly int VignetteAmount = Shader.PropertyToID("_VignetteAmount");
        static readonly int VignetteSize = Shader.PropertyToID("_VignetteSize");
        static readonly int VignetteRounding = Shader.PropertyToID("_VignetteRounding");
        static readonly int VignetteSmoothing = Shader.PropertyToID("_VignetteSmoothing");
        static readonly int ScanLinesDensity = Shader.PropertyToID("_ScanLinesDensity");
        static readonly int ScanLinesSpeed = Shader.PropertyToID("_ScanLinesSpeed");
        static readonly int NoiseAmount = Shader.PropertyToID("_NoiseAmount");
        static readonly int ChromaticRed = Shader.PropertyToID("_ChromaticRed");
        static readonly int ChromaticGreen = Shader.PropertyToID("_ChromaticGreen");
        static readonly int ChromaticBlue = Shader.PropertyToID("_ChromaticBlue");
        static readonly int GrilleOpacity = Shader.PropertyToID("_GrilleOpacity");
        static readonly int GrilleCounterOpacity = Shader.PropertyToID("_GrilleCounterOpacity");
        static readonly int GrilleResolution = Shader.PropertyToID("_GrilleResolution");
        static readonly int GrilleCounterResolution = Shader.PropertyToID("_GrilleCounterResolution");
        static readonly int GrilleBrightness = Shader.PropertyToID("_GrilleBrightness");
        static readonly int GrilleUvRotation = Shader.PropertyToID("_GrilleUvRotation");
        static readonly int GrilleUvMidPoint = Shader.PropertyToID("_GrilleUvMidPoint");
        static readonly int GrilleShift = Shader.PropertyToID("_GrilleShift");

        public CRTPass(RenderPassEvent evt) : base(evt, "PostEffect/CRTShader", "Render CRT Effects") { }

        protected override bool Prepare(Material mat)
        {
            var m_Crt = VolumeManager.instance.stack.GetComponent<Crt>();
            if (m_Crt == null || !m_Crt.IsActive()) return false;

            mat.SetFloat(ScanLinesWeight, m_Crt.scanlinesWeight.value);
            mat.SetFloat(NoiseWeight, m_Crt.noiseWeight.value);
            mat.SetFloat(ScreenBendX, m_Crt.screenBendX.value);
            mat.SetFloat(ScreenBendY, m_Crt.screenBendY.value);
            mat.SetFloat(VignetteAmount, m_Crt.vignetteAmount.value);
            mat.SetFloat(VignetteSize, m_Crt.vignetteSize.value);
            mat.SetFloat(VignetteRounding, m_Crt.vignetteRounding.value);
            mat.SetFloat(VignetteSmoothing, m_Crt.vignetteSmoothing.value);
            mat.SetFloat(ScanLinesDensity, m_Crt.scanlinesDensity.value);
            mat.SetFloat(ScanLinesSpeed, m_Crt.scanlinesSpeed.value);
            mat.SetFloat(NoiseAmount, m_Crt.noiseAmount.value);
            mat.SetVector(ChromaticRed, m_Crt.chromaticRed.value);
            mat.SetVector(ChromaticGreen, m_Crt.chromaticGreen.value);
            mat.SetVector(ChromaticBlue, m_Crt.chromaticBlue.value);
            mat.SetFloat(GrilleOpacity, m_Crt.grilleOpacity.value);
            mat.SetFloat(GrilleCounterOpacity, m_Crt.grilleCounterOpacity.value);
            mat.SetFloat(GrilleResolution, m_Crt.grilleResolution.value);
            mat.SetFloat(GrilleCounterResolution, m_Crt.grilleCounterResolution.value);
            mat.SetFloat(GrilleBrightness, m_Crt.grilleBrightness.value);
            mat.SetFloat(GrilleUvRotation, m_Crt.grilleUvRotation.value);
            mat.SetFloat(GrilleUvMidPoint, m_Crt.grilleUvMidPoint.value);
            mat.SetVector(GrilleShift, m_Crt.grilleShift.value);
            return true;
        }
    }
}
