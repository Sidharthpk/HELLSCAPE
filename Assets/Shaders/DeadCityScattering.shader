Shader "HellScape/Dead City Local Scattering"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
        Varyings Vert(uint id : SV_VertexID)
        {
            Varyings o; o.positionCS = GetFullScreenTriangleVertexPosition(id);
            o.uv = GetFullScreenTriangleTexCoord(id); return o;
        }
        ENDHLSL
        Pass
        {
            Name "Volumetric Fog"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #define FOG_STEPS 16
            #define LAMP_STEPS 14
            TEXTURE3D(_HSFogNoise); SAMPLER(sampler_HSFogNoise);
            TEXTURECUBE(_HSFogSky); SAMPLER(sampler_HSFogSky);
            float4 _HSBeamSpheres[12], _HSBeamIndices[12], _HSScattering;
            float4 _HSFog;         // density, top height, 1 / height falloff, camera far
            float4 _HSFogBlend;    // sky blend start, end
            float4 _HSFogNear, _HSFogSkyTint;

            // slow rolling banks, 0-1
            float Banks(float3 p)
            {
                float3 wind = float3(.0035, .0008, .0021) * _Time.y;
                float big = SAMPLE_TEXTURE3D_LOD(_HSFogNoise, sampler_HSFogNoise, p * .0085 + wind, 0).r;
                float small = SAMPLE_TEXTURE3D_LOD(_HSFogNoise, sampler_HSFogNoise, p * .041 - wind * 2., 0).r;
                return saturate(big * .75 + small * .25);
            }
            float Density(float3 p)
            {
                float above = max(0., p.y - _HSFog.y);
                return _HSFog.x * 1.9 * exp(-above * _HSFog.z) * lerp(.35, 1.65, Banks(p));
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float rawDepth = SampleSceneDepth(input.uv);
                #if UNITY_REVERSED_Z
                    bool sky = rawDepth <= .000001;
                #else
                    bool sky = rawDepth >= .999999;
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                #endif
                float3 world = ComputeWorldSpacePosition(input.uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 origin = GetCameraPositionWS();
                float3 ray = normalize(world - origin);
                float sceneDistance = length(world - origin);
                // Stable spatial dither, not time jitter: there is no temporal reprojection history to smear.
                float jitter = frac(52.9829189 * frac(dot(floor(input.positionCS.xy), float2(.06711056,.00583715))));

                float3 sum = 0;
                float through = 1;
                if (!sky && _HSFog.x > 0)
                {
                    float3 skyColor = _HSFogNear.rgb;
                    if (_HSFogSkyTint.a > 0)
                        skyColor = SAMPLE_TEXTURECUBE_LOD(_HSFogSky, sampler_HSFogSky, normalize(float3(ray.x, max(ray.y, .03), ray.z)), 3).rgb * _HSFogSkyTint.rgb;
                    float end = min(sceneDistance, _HSFog.w);
                    float previous = 0;
                    [loop] for (int i = 0; i < FOG_STEPS; i++)
                    {
                        float k = (i + jitter) / FOG_STEPS;
                        float d = end * k * k;
                        float slice = 1 - exp(-Density(origin + ray * d) * (d - previous));
                        previous = d;
                        sum += through * slice * lerp(_HSFogNear.rgb, skyColor, smoothstep(_HSFogBlend.x, _HSFogBlend.y, d));
                        through *= 1 - slice;
                    }
                    // solid sky colour by the far clip, whatever the density: the city never pops in or cuts off
                    float gone = smoothstep(_HSFog.w * .6, _HSFog.w * .97, sceneDistance);
                    sum = lerp(sum, skyColor, gone);
                    through *= 1 - gone;
                }

                float endDistance = min(sceneDistance, _HSScattering.z);
                [loop] for (int lamp = 0; lamp < (int)_HSScattering.w; lamp++)
                {
                    float3 delta = origin - _HSBeamSpheres[lamp].xyz;
                    float b = dot(delta, ray);
                    float discriminant = b*b - dot(delta,delta) + _HSBeamSpheres[lamp].w*_HSBeamSpheres[lamp].w;
                    if (discriminant <= 0) continue;
                    float root = sqrt(discriminant);
                    float start = max(.15, -b-root), end = min(endDistance,-b+root);
                    if (end <= start) continue;
                    // equiangular: samples bunch up where the ray passes the bulb, the only place the brightness changes fast
                    float nearest = -b, offset = sqrt(max(dot(delta,delta)-b*b,.01));
                    float angleStart = atan((start-nearest)/offset), angleEnd = atan((end-nearest)/offset);
                    int index = (int)_HSBeamIndices[lamp].x;
                    [loop] for (int sample = 0; sample < LAMP_STEPS; sample++)
                    {
                        float angle = lerp(angleStart,angleEnd,(sample + jitter)/LAMP_STEPS);
                        float cosine2 = max(cos(angle)*cos(angle),.0001);
                        float distance = nearest + offset*tan(angle);
                        float stepSize = offset*(angleEnd-angleStart)/(LAMP_STEPS*cosine2);
                        float3 p = origin + ray*distance;
                        Light light = GetAdditionalPerObjectLight(index,p);
                        if (light.distanceAttenuation < .0001) continue;
                        half shadow = AdditionalLightRealtimeShadow(index,p,light.direction);
                        float g = _HSScattering.y, cosine = dot(ray,-light.direction);
                        float phase = (1-g*g) / (12.56637*pow(max(.05,1+g*g-2*g*cosine),1.5));
                        float extinction = exp(-distance*(_HSFog.x+.002));
                        sum += light.color * min(light.distanceAttenuation, 1.5) * shadow * phase * lerp(.5, 1.5, Banks(p)) * extinction * stepSize * _HSScattering.x;
                    }
                }
                return half4(sum, through);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Depth Aware Resolve"
            Blend One SrcAlpha
            ColorMask RGB
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Resolve
            #pragma target 4.5
            TEXTURE2D(_HSScatterTexture);
            float4 _HSScatterTexture_TexelSize;
            half4 Resolve(Varyings input) : SV_Target
            {
                float depth = LinearEyeDepth(SampleSceneDepth(input.uv),_ZBufferParams);
                float4 sum = 0; float weight = 0;
                // 3x3 tent: the march's dither is a one-texel pattern, so the filter has to span it or it shows as a checker
                [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++)
                {
                    float2 uv = input.uv + float2(x,y)*_HSScatterTexture_TexelSize.xy;
                    uv = (floor(uv*_HSScatterTexture_TexelSize.zw)+.5)*_HSScatterTexture_TexelSize.xy;   // (the half-size texel's own centre, so its depth is the one it was marched with)
                    float tapDepth = LinearEyeDepth(SampleSceneDepth(uv),_ZBufferParams);
                    float w = (2-abs(x))*(2-abs(y))*exp(-abs(tapDepth-depth)/max(.2,depth*.015)) + .00001;
                    sum += SAMPLE_TEXTURE2D(_HSScatterTexture,sampler_PointClamp,uv)*w;
                    weight += w;
                }
                return half4(sum/weight);
            }
            ENDHLSL
        }
    }
}
