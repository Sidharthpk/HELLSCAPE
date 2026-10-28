// A flat colour with no fog and no lighting: the dream's stalkers are pure black however thick the mist (EndingDream).
Shader "HellScape/DreamSilhouette"
{
    Properties { [HDR] _Color("Colour", Color) = (0,0,0,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            struct A { float4 p : POSITION; };
            struct V { float4 p : SV_POSITION; };
            V vert(A i) { V o; o.p = TransformObjectToHClip(i.p.xyz); return o; }
            half4 frag(V i) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
