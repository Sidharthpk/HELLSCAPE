// Screen-space outline: depth + normal edges, drawn as the surface's own colour pushed into shadow (not a flat black line).
Shader "Hidden/HellScape/OfficeOutline"
{
    Properties
    {
        _Strength ("Strength (0 = off)", Range(0,1)) = 0
        _Thickness ("Thickness (px)", Range(0.5,3)) = 1.2
        _DepthThreshold ("Depth threshold", Range(0.01,2)) = 0.35
        _NormalThreshold ("Normal threshold", Range(0.05,2)) = 0.6
        _Shadow ("Surface darkening", Range(0,1)) = 0.22
        _BorderTint ("Border tint", Color) = (0.05,0.02,0.02,1)
        _FadeStart ("Fade start (m)", Float) = 12
        _FadeEnd ("Fade end (m)", Float) = 32
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "OfficeOutline"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _Strength, _Thickness, _DepthThreshold, _NormalThreshold, _Shadow, _FadeStart, _FadeEnd;
            float4 _BorderTint;

            float Eye(float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                half4 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_Strength <= 0.001) return src;

                float2 px = _Thickness / _ScreenParams.xy;
                float d0 = Eye(uv);
                float dA = Eye(uv + float2(px.x, 0)), dB = Eye(uv - float2(px.x, 0));
                float dC = Eye(uv + float2(0, px.y)), dD = Eye(uv - float2(0, px.y));
                // depth edge relative to distance, so far walls at a grazing angle don't light up
                float depthEdge = (abs(dA - d0) + abs(dB - d0) + abs(dC - d0) + abs(dD - d0)) / max(d0, 0.5);
                depthEdge = saturate((depthEdge - _DepthThreshold * 0.05) / (_DepthThreshold * 0.05));

                float3 n0 = SampleSceneNormals(uv);
                float3 nA = SampleSceneNormals(uv + float2(px.x, 0)), nB = SampleSceneNormals(uv - float2(px.x, 0));
                float3 nC = SampleSceneNormals(uv + float2(0, px.y)), nD = SampleSceneNormals(uv - float2(0, px.y));
                float nd = (1 - dot(n0, nA)) + (1 - dot(n0, nB)) + (1 - dot(n0, nC)) + (1 - dot(n0, nD));
                float normalEdge = saturate((nd - _NormalThreshold) / _NormalThreshold);

                float edge = max(depthEdge, normalEdge) * _Strength;
                edge *= 1 - smoothstep(_FadeStart, _FadeEnd, d0);   // dissolves into the fog
                half3 shadowed = lerp(src.rgb * _Shadow, _BorderTint.rgb, 0.35) * 1.0;
                return half4(lerp(src.rgb, shadowed, saturate(edge)), src.a);
            }
            ENDHLSL
        }
    }
}
