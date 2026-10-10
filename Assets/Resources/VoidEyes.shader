// The eyes in the sky of the soul void (EndingSequence.DeathVision): a flipbook on particles, added onto the black
// sky before anything else is drawn, so it is a sky: no fog, no depth, the floor and the shadow people stand in
// front of it. In Resources so Shader.Find has it in a build.
Shader "HellScape/VoidEyes"
{
    Properties
    {
        _MainTex ("Flipbook", 2D) = "black" {}
        _Dim ("Brightness", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test (Always = a sky; LessEqual = on a wall, OfficeEye)", Float) = 8
    }
    SubShader
    {
        Tags { "Queue" = "Background+1" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float _Dim;

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color; o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                return half4(c * i.color.rgb * (i.color.a * _Dim), 1);
            }
            ENDHLSL
        }
    }
}
