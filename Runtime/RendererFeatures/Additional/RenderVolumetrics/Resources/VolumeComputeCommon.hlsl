#define VOXEL_THREADS [numthreads(16, 16, 2)]

//
// Inputs
//
CBUFFER_START(VOLUMETRICS_CAMERA_DATA)

float4x4 _CameraToWorld[2];
float4x4 _WorldToCamera[2];
float4x4 _Projection[2];
float4x4 _InverseProjection[2];

CBUFFER_END

float4 _PassData; // xyz = Texture Dimensions
float4 _FogParams; // x = Steps, y = Far, z = Density, w = Scattering

//
// Basic ray type
//  
struct Ray
{
    float3 origin;
    float3 direction;
};

Ray RayFromViewpoint(float2 vPoint, float4x4 cam2World, float4x4 invProj)
{
    Ray ray;
    
    ray.origin = mul(cam2World, float4(0, 0, 0, 1)).xyz;

    ray.direction = mul(invProj, float4(vPoint, 0, 1)).xyz;
    ray.direction = mul(cam2World, float4(ray.direction, 0)).xyz;
    ray.direction = normalize(ray.direction);

    return ray;
}

//
// Functions
//
#define VOLUME_PI 3.141592654

float MieScattering(float lightDotView, float gScattering)
{
    float result = 1.0f - gScattering * gScattering;
    result /= (4.0f * VOLUME_PI * pow(abs(1.0f + gScattering * gScattering - (2.0f * gScattering) * lightDotView), 1.5f));
    return result;
}

float3 GetSamplePoint(uint3 id)
{
    float2 uv = id.xy / _PassData.xy;
    float slice = id.z / _PassData.z;
    
    float2 coords = (uv - 0.5) * 2.0;
    // Texel compensation
    coords += 1 / _PassData.xy;

    Ray sRay = RayFromViewpoint(coords, _CameraToWorld[_PassData.w], _InverseProjection[_PassData.w]);

    float3 sStart = sRay.origin;
    float3 sEnd = sStart + sRay.direction * _FogParams.y;
    float3 sDir = sRay.direction;

    return lerp(sStart, sEnd, pow(slice, 2.0));
}