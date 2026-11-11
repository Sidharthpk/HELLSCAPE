// Retain URP's lighting, shadows, lightmaps and the original city's texture detail.
#define InitializeStandardLitSurfaceData InitializeCitySurfaceData
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#undef InitializeStandardLitSurfaceData

float CityHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float CityNoise(float2 p)
{
    float2 i = floor(p), f = frac(p); f = f*f*(3-2*f);
    return lerp(lerp(CityHash(i), CityHash(i+float2(1,0)), f.x),
        lerp(CityHash(i+float2(0,1)), CityHash(i+1), f.x), f.y);
}

void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData s)
{
    InitializeCitySurfaceData(uv, s);
    float grey = dot(s.albedo, float3(.2126,.7152,.0722));
#ifdef CITY_ROAD_SURFACE
    // The textures have tyre grime and stains painted in, which repeat along every street: divide out
    // everything broader than ~8 texels of a 2048 map (the 256 mip), leaving the fine grain. Asphalt blurs
    // to .06-.13, kerb concrete to .14+, paint to .3+, so those keep their colour. Asphalt maps are 2048,
    // the pavement's is 512 and keeps its own brightness.
    if (_BaseMap_TexelSize.z > 600)
    {
        float blur = dot(SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, max(log2(_BaseMap_TexelSize.z)-8,0)).rgb, float3(.2126,.7152,.0722));
        float flatten = lerp(.10/clamp(blur,.05,.16), 1, smoothstep(.13,.155,blur));
        s.albedo *= flatten; grey *= flatten;
    }
    float grit = CityNoise(uv*210);
    float3 weathered = lerp(s.albedo, grey*float3(.93,.89,.83), .72);
    s.albedo = weathered * lerp(.60,.87,grit);
    // Roads stay dry and even: no wetness, no wear blotches.
    s.smoothness = .035;
#else
    float patches = CityNoise(uv*8) + .35*CityNoise(uv*29);
    float soot = smoothstep(.25,.86,patches);
    float streaks = smoothstep(.2,.85,CityNoise(uv*float2(92,5)));
    float grain = CityNoise(uv*230);
    float ash = smoothstep(.64,.84,CityNoise(uv*57));
    float3 singed = lerp(s.albedo,grey*float3(.91,.85,.78),.84)*.74;
    float3 charcoal = lerp(float3(.028,.023,.020),float3(.14,.12,.10),grain);
    s.albedo = lerp(singed,charcoal,soot*.88);
    s.albedo *= lerp(.55,1,streaks);
    s.albedo = lerp(s.albedo,float3(.34,.31,.27),ash*soot*.35);
    s.smoothness = min(s.smoothness,.035);
#endif
    s.metallic = 0;
    s.emission = 0;
}
