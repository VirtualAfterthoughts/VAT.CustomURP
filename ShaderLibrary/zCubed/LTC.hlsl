#ifndef ZCUBED_LTC_INCLUDED
#define ZCUBED_LTC_INCLUDED

#define LTC_LUT_SIZE 64.0 // ltc_texture size
#define LTC_LUT_SCALE ((LTC_LUT_SIZE - 1.0) / LTC_LUT_SIZE)
#define LTC_LUT_BIAS (0.5 / LTC_LUT_SIZE)

Texture2D LTC_LUT1;
Texture2D LTC_LUT2;

SamplerState sampler_LTC_LUT2;

float3 IntegrateEdgefloat(float3 v1, float3 v2)
{
    // Using built-in acos() function will result flaws
    // Using fitting result for calculating acos()
    float x = dot(v1, v2);
    float y = abs(x);

    float a = 0.8543985 + (0.4965155 + 0.0145206*y)*y;
    float b = 3.4175940 + (4.1616724 + y)*y;
    float v = a / b;

    float theta_sintheta = (x > 0.0) ? v : 0.5* rsqrt(max(1.0 - x*x, 1e-7)) - v;

    return cross(v1, v2)*theta_sintheta;
}

// P is fragPos in world space (LTC distribution)
float3 LTC_Integrate(float3 N, float3 V, float3 P, float3x3 Minv, float3 points[4], bool twoSided)
{
    // construct orthonormal basis around N
    float3 T1, T2;
    T1 = normalize(V - N * dot(V, N));
    T2 = cross(N, T1);

    // rotate area light in (T1, T2, N) basis
    Minv = Minv * transpose(float3x3(T1, T2, N));
	//Minv = Minv * transpose(mat3(N, T2, T1));

    // polygon (allocate 4 vertices for clipping)
    float3 L[4];
    // transform polygon from LTC back to origin Do (cosine weighted)
    L[0] = mul(Minv, (points[0] - P));
    L[1] = mul(Minv, (points[1] - P));
    L[2] = mul(Minv, (points[2] - P));
    L[3] = mul(Minv, (points[3] - P));

    // use tabulated horizon-clipped sphere
    // check if the shading point is behind the light
    float3 dir = points[0] - P; // LTC space
    float3 lightNormal = cross(points[1] - points[0], points[3] - points[0]);
    bool behind = (dot(dir, lightNormal) < 0.0);

    // cos weighted space
    L[0] = normalize(L[0]);
    L[1] = normalize(L[1]);
    L[2] = normalize(L[2]);
    L[3] = normalize(L[3]);

	// integrate
    float3 vsum = (0.0).xxx;
    vsum += IntegrateEdgefloat(L[0], L[1]);
    vsum += IntegrateEdgefloat(L[1], L[2]);
    vsum += IntegrateEdgefloat(L[2], L[3]);
    vsum += IntegrateEdgefloat(L[3], L[0]);

    // form factor of the polygon in direction vsum
    float len = length(vsum);

    float z = vsum.z/len;
    if (behind)
        z = -z;

    float2 uv = float2(z * 0.5f + 0.5f, len); // range [0, 1]
    uv = uv * LTC_LUT_SCALE + LTC_LUT_BIAS;

    // Fetch the form factor for horizon clipping
    float scale = LTC_LUT2.Sample(sampler_LTC_LUT2, uv).w;

    float sum = len*scale;
    if (!behind && !twoSided)
        sum = 0.0;

    // Outgoing radiance (solid angle) for the entire polygon
    float3 Lo_i = float3(sum, sum, sum);
    return Lo_i;
}

#endif