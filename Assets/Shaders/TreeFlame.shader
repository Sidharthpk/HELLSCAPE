// Fire on top of a burning tree: procedural (no texture), red at the edges, orange in the body, yellow at the core,
// licking upward. Drawn on a few crossed quads (see HellTreeBuilder); each tree gets its own phase from its position.
Shader "HellScape/TreeFlame"
{
    Properties
    {
        _Core ("Core", Color) = (1, 0.9, 0.35, 1)
        _Body ("Body", Color) = (1, 0.42, 0.05, 1)
        _Edge ("Edge", Color) = (0.75, 0.06, 0.02, 1)
        _Speed ("Rise speed", Float) = 1.7
        _Intensity ("Intensity", Float) = 2.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Flame"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Core, _Body, _Edge;
                float _Speed, _Intensity;
            CBUFFER_END

            struct Attr { float4 pos : POSITION; float2 uv : TEXCOORD0; float2 seed : TEXCOORD1; };
            struct V2F { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float phase : TEXCOORD1; float flick : TEXCOORD2; };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }

            V2F vert(Attr a)
            {
                V2F o;
                float3 centre = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                o.phase = frac(dot(centre, float3(0.137, 0.291, 0.173))) * 40 + a.seed.x * 40;   // each flame on the tree has its own
                float3 p = a.pos.xyz;
                // restless: the flame gutters (brightness and height jump about) and the tip whips from side to side
                o.flick = 0.45 + 1.1 * Noise(float2(o.phase, _Time.y * 11)) * (0.6 + 0.4 * Noise(float2(o.phase * 2, _Time.y * 29)));
                p.x += (sin(_Time.y * 5.3 + o.phase) + 0.6 * sin(_Time.y * 13 + o.phase * 2)) * 0.14 * a.uv.y * a.uv.y;
                p.y += (o.flick - 0.8) * 0.25 * a.uv.y;
                o.pos = TransformObjectToHClip(p);
                o.uv = a.uv;
                return o;
            }

            half4 frag(V2F i) : SV_Target
            {
                // a soft teardrop: wide rounded foot, tapering to a tip, swaying with slow smooth noise, feathered edges
                float2 uv = i.uv;
                float y = uv.y;
                float x = uv.x * 2 - 1;
                float t = _Time.y * _Speed;
                float n1 = Noise(float2(uv.x * 1.5 + i.phase, y * 1.6 - t));
                float n2 = Noise(float2(uv.x * 3 - i.phase, y * 3.2 - t * 1.6));
                float sway = ((n1 - 0.5) * 0.7 + (n2 - 0.5) * 0.3) * y;
                float w = pow(saturate(1 - y), 0.75) * smoothstep(0.0, 0.25, y + 0.12) * 0.95 + 0.001;
                float d = abs(x - sway) / w;
                float shape = 1 - smoothstep(0.15, 1.0, d);
                float heat = shape * (1 - y * 0.55) * (0.8 + 0.4 * n2);
                float alpha = saturate(heat * 1.25) * smoothstep(0.0, 0.18, y) * smoothstep(0.95, 0.25, y);   // (the tip dissolves instead of ending in a point)
                half3 col = lerp(_Edge.rgb, _Body.rgb, smoothstep(0.1, 0.6, heat));
                col = lerp(col, _Core.rgb, smoothstep(0.6, 1.1, heat) * (1 - y));
                return half4(col * _Intensity * i.flick, alpha);
            }
            ENDHLSL
        }
    }
}
