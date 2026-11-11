// PSXShaderKit's console dither (its 4x4 PS1 pattern from PSX-PostProcess-Accurate), ported to a URP fullscreen pass and
// softened: 6-bit instead of 5-bit colour, and blended by _Amount, so it reads as a faint grain, not a pattern.
Shader "Hidden/HellScape/PSXDither"
{
    Properties
    {
        _Amount ("Amount (0 = off)", Range(0,1)) = 0
        _Levels ("Colour levels per channel", Float) = 64
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "PSXDither"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Amount, _Levels;

            static const int Ps1Dither[16] = { -4, 0, -3, 1,  2, -2, 3, -1,  -3, 1, -4, 0,  3, -1, 2, -2 };

            half4 Frag(Varyings i) : SV_Target
            {
                half4 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, i.texcoord);
                if (_Amount <= 0.001) return src;
                uint2 p = (uint2)i.positionCS.xy;
                float offset = Ps1Dither[(p.x & 3) + (p.y & 3) * 4] / 8.0;          // -0.5 .. +0.5 of one level
                float3 q = floor(src.rgb * (_Levels - 1) + 0.5 + offset) / (_Levels - 1);
                return half4(lerp(src.rgb, saturate(q), _Amount), src.a);
            }
            ENDHLSL
        }
    }
}
