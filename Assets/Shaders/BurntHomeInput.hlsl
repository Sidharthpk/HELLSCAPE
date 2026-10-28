// Keep URP lighting, normals, shadows, depth and deferred rendering; scorch the existing UV texture.
#define InitializeStandardLitSurfaceData InitializeCleanHomeSurfaceData
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#undef InitializeStandardLitSurfaceData
float BurnHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
float BurnNoise(float2 p) {
    float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
    return lerp(lerp(BurnHash(i),BurnHash(i+float2(1,0)),f.x),lerp(BurnHash(i+float2(0,1)),BurnHash(i+1),f.x),f.y);
}
void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData s) {
    InitializeCleanHomeSurfaceData(uv,s);
    float broad=BurnNoise(uv*9)+0.35*BurnNoise(uv*27)+0.15*BurnNoise(uv*81);
    float soot=smoothstep(0.35,0.95,broad);
    float grain=BurnNoise(uv*420);
    float ash=smoothstep(0.67,0.86,BurnNoise(uv*63))*0.28;
    float cracks=smoothstep(0.58,0.78,BurnNoise(uv*220))*BurnNoise(uv*57);
    float luminance=dot(s.albedo,float3(.2126,.7152,.0722));
    float3 singed=lerp(s.albedo,luminance*float3(.50,.39,.29),.72);
    float3 charcoal=lerp(float3(.018,.014,.012),float3(.105,.085,.065),grain);
    s.albedo=lerp(singed*.62,charcoal,soot*.90);
    s.albedo=lerp(s.albedo,float3(.27,.245,.21),ash*soot);
    s.albedo*=1-cracks*soot*.48;
    s.smoothness=min(s.smoothness,.08);
    s.metallic*=.15;
    s.emission=0;
}
