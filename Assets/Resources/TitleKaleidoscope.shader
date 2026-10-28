Shader "Hellscape/Title Kaleidoscope"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MotionTime ("Motion time", Float) = 0
        _Aspect ("Aspect ratio", Float) = 1.777778
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            float _MotionTime, _Aspect;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float t = _MotionTime * 0.075;
                float2 p = (i.uv - 0.5) * float2(_Aspect, 1.0) * 2.0;
                // Refract the mirrored pattern as if it sits beneath slow-moving water.
                float waterTime = _MotionTime * 0.42;
                float ripple = sin(length(p) * 19.0 - waterTime * 1.8);
                float crossWave = sin(p.x * 11.0 + p.y * 7.0 + waterTime);
                p += p / max(length(p), 0.001) * ripple * 0.022;
                p += float2(sin(p.y * 13.0 - waterTime), cos(p.x * 10.0 + waterTime * 0.8)) * 0.018;
                float radius = length(p);
                // Six mirrored wedges, with slow rotation and soft, flowing folds.
                float angle = atan2(p.y, p.x) + t * 0.18;
                float wedge = 1.04719755;
                angle = abs(frac(angle / wedge + 0.5) - 0.5) * wedge;
                float2 q = float2(cos(angle), sin(angle)) * radius;
                float flow = sin(q.x * 4.8 - t) + cos(q.y * 8.0 + t * 0.7);
                float ribbons = 0.5 + 0.5 * sin(q.x * 6.0 + flow * 1.7 + t * 0.6);
                float warm = smoothstep(-0.55, 0.65, sin(q.x * 3.0 - q.y * 5.0 + t * 0.5));
                float3 blue = float3(0.004, 0.009, 0.025);
                float3 red = float3(0.24, 0.003, 0.009);
                float3 orange = float3(0.32, 0.027, 0.006);
                float3 grey = float3(0.055, 0.045, 0.05);
                float3 color = lerp(blue, red, 0.7 + warm * 0.3);
                color = lerp(color, orange, smoothstep(0.58, 0.98, ribbons) * warm * 0.16);
                float mist = smoothstep(0.35, 0.95, cos(q.x * 4.0 + q.y * 7.0 + t * 0.4));
                color = lerp(color, grey, mist * 0.08);
                color *= lerp(0.07, 1.0, smoothstep(0.08, 0.9, ribbons));
                // Tight moving reflections over broad red folds give a lacquered surface.
                float ridge = 0.5 + 0.5 * cos(q.x * 6.0 + flow * 1.7 + t * 0.6 - 0.55);
                float light = pow(saturate(0.5 + 0.5 * sin(q.x * 2.3 - q.y * 3.5 - t * 0.25)), 3.0);
                color += float3(0.36, 0.065, 0.055) * pow(ridge, 36.0) * light;
                color += float3(0.16, 0.12, 0.115) * pow(ridge, 140.0) * light;
                // Small glints gather at crossing wave crests, like reflected light on water.
                float glint = pow(saturate(ripple * 0.5 + crossWave * 0.5), 12.0);
                color *= 0.92 + 0.08 * crossWave;
                color += float3(0.09, 0.035, 0.03) * glint * (0.25 + 0.75 * light);
                // A dark center and edge falloff frame the title without hard geometry.
                color *= lerp(0.32, 1.0, smoothstep(0.0, 0.85, radius));
                color *= 1.0 - 0.65 * smoothstep(0.75, 2.1, radius);
                return fixed4(color * i.color.rgb, i.color.a);
            }
            ENDCG
        }
    }
}
