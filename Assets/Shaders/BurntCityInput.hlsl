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

#ifdef CITY_ROAD_SURFACE
float CityFracture(float2 p)
{
    float2 cell = floor(p), f = frac(p);
    float first = 10, second = 10;
    [unroll] for (int y = -1; y <= 1; y++)
        [unroll] for (int x = -1; x <= 1; x++)
        {
            float2 n = float2(x,y);
            float2 offset = n + float2(CityHash(cell+n), CityHash(cell+n+37)) - f;
            float d = dot(offset, offset);
            if (d < first) { second = first; first = d; } else second = min(second,d);
        }
    return sqrt(second) - sqrt(first);
}
#endif

void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData s)
{
    InitializeCitySurfaceData(uv, s);
    float grey = dot(s.albedo, float3(.2126,.7152,.0722));
#ifdef CITY_ROAD_SURFACE
    float patch = CityNoise(uv*6.5 + 19);
    float grit = CityNoise(uv*210);
    float wear = smoothstep(.42,.76,patch);
    float2 warp = float2(CityNoise(uv*17+13), CityNoise(uv*17+67))*2.4;
    float fracture = 1-smoothstep(.010,.042,CityFracture(uv*32 + warp));
    fracture *= smoothstep(.27,.70,CityNoise(uv*8+44)) * (.55+.45*CityNoise(uv*41));
    float3 weathered = lerp(s.albedo, grey*float3(.93,.89,.83), .72);
    s.albedo = weathered * lerp(.60,.87,grit);
    s.albedo = lerp(s.albedo, float3(.25,.235,.215)*( .78+.22*grit), wear*.46);
    s.albedo *= 1-fracture*.70;
    s.smoothness = .035;
#else
    float patches = CityNoise(uv*8) + .35*CityNoise(uv*29);
    float soot = smoothstep(.36,.98,patches);
    float streaks = CityNoise(uv*float2(74,7));
    float grain = CityNoise(uv*230);
    float ash = smoothstep(.64,.84,CityNoise(uv*57));
    float3 singed = lerp(s.albedo,grey*float3(.80,.71,.61),.72)*.70;
    float3 charcoal = lerp(float3(.065,.055,.047),float3(.21,.185,.15),grain);
    s.albedo = lerp(singed,charcoal,soot*.78);
    s.albedo *= lerp(.63,1,streaks);
    s.albedo = lerp(s.albedo,float3(.29,.265,.225),ash*soot*.18);
    s.smoothness = min(s.smoothness,.06);
#endif
    s.metallic = 0;
    s.emission = 0;
}
