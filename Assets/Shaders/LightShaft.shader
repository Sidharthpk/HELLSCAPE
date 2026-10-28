// A fake light shaft: an additive cone under a lamp, fading out with the vertex alpha (apex 1, base 0).
Shader "HorrorShot/LightShaft"
{
    Properties { _Color("Color", Color) = (1,0.45,0.25,0.25) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END
            struct A { float4 p : POSITION; float4 c : COLOR; };
            struct V { float4 p : SV_POSITION; float4 c : COLOR; float3 o : TEXCOORD0; float3 w : TEXCOORD1; };
            V vert(A a) { V v; v.p = TransformObjectToHClip(a.p.xyz); v.c = a.c; v.o = a.p.xyz; v.w = TransformObjectToWorld(a.p.xyz); return v; }
            half4 frag(V v) : SV_Target
            {
                // the cone's surface normal (apex at the origin, radius = depth): fades the beam out at its silhouette, so no hard edge
                float r = length(v.o.xz);
                float3 n = normalize(TransformObjectToWorldDir(float3(v.o.x, r, v.o.z)));
                float3 view = normalize(GetCameraPositionWS() - v.w);
                float edge = pow(saturate(abs(dot(n, view))), 1.5);
                return half4(_Color.rgb, _Color.a * v.c.a * v.c.a * edge);
            }
            ENDHLSL
        }
    }
}
