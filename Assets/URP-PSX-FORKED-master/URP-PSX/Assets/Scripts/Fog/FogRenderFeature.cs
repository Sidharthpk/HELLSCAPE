using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PSX
{
    public class FogRenderFeature : ScriptableRendererFeature
    {
        FogPass fogPass;

        public override void Create()
        {
            fogPass = new FogPass(RenderPassEvent.BeforeRenderingPostProcessing);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(fogPass);
        }

        protected override void Dispose(bool disposing)
        {
            fogPass?.Dispose();
        }
    }


    public class FogPass : PSXPostPass
    {
        static readonly int FogDensity = Shader.PropertyToID("_FogDensity");
        static readonly int FogDistance = Shader.PropertyToID("_FogDistance");
        static readonly int FogColor = Shader.PropertyToID("_FogColor");
        static readonly int AmbientColor = Shader.PropertyToID("_AmbientColor");
        static readonly int FogNear = Shader.PropertyToID("_FogNear");
        static readonly int FogFar = Shader.PropertyToID("_FogFar");
        static readonly int FogAltScale = Shader.PropertyToID("_FogAltScale");
        static readonly int FogThinning = Shader.PropertyToID("_FogThinning");
        static readonly int NoiseScale = Shader.PropertyToID("_NoiseScale");
        static readonly int NoiseStrength = Shader.PropertyToID("_NoiseStrength");

        public FogPass(RenderPassEvent evt) : base(evt, "PostEffect/Fog", "Render Fog Effects") { }

        protected override bool Prepare(Material mat)
        {
            var fog = VolumeManager.instance.stack.GetComponent<Fog>();
            if (fog == null || !fog.IsActive()) return false;

            mat.SetFloat(FogDensity, fog.fogDensity.value);
            mat.SetFloat(FogDistance, fog.fogDistance.value);
            mat.SetColor(FogColor, fog.fogColor.value);
            mat.SetColor(AmbientColor, fog.ambientColor.value);
            mat.SetFloat(FogNear, fog.fogNear.value);
            mat.SetFloat(FogFar, fog.fogFar.value);
            mat.SetFloat(FogAltScale, fog.fogAltScale.value);
            mat.SetFloat(FogThinning, fog.fogThinning.value);
            mat.SetFloat(NoiseScale, fog.noiseScale.value);
            mat.SetFloat(NoiseStrength, fog.noiseStrength.value);
            return true;
        }
    }
}
