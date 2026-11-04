/* 
 * Copyright (C) SAAB AB
 *
 * All rights, including the copyright, to the computer program(s) 
 * herein belong to Saab AB. The program(s) may be used and/or
 * copied only with the written permission of Saab AB, or in
 * accordance with the terms and conditions stipulated in the
 * agreement/contract under which the program(s) have been
 * supplied. 
 * 
 * Information Class:          COMPANY RESTRICTED
 * Defence Secrecy:            UNCLASSIFIED
 * Export Control:             NOT EXPORT CONTROLLED
 */

uint Pack2Half(float a, float b)
{
    uint ha = f32tof16(a) & 0xFFFF;
    uint hb = f32tof16(b) & 0xFFFF;
    return ha | (hb << 16);
}

float UnpackHalfLo(uint p)
{
    return f16tof32(p & 0xFFFF);
}
float UnpackHalfHi(uint p)
{
    return f16tof32(p >> 16);
}

uint PackColor(float4 c)
{
    c = saturate(c); // clamp to [0,1]

    uint r = (uint) round(c.r * 255.0);
    uint g = (uint) round(c.g * 255.0);
    uint b = (uint) round(c.b * 255.0);
    uint a = (uint) round(c.a * 255.0);

    return (r) | (g << 8) | (b << 16) | (a << 24);
}

float4 UnpackColor(uint color)
{
    return float4(
        (color & 255) / 255.0,
        ((color >> 8) & 255) / 255.0,
        ((color >> 16) & 255) / 255.0,
        ((color >> 24) & 255) / 255.0
    );
}

// Convert float3 normal (must be normalized) into uint (SNORM16x2 oct)
uint PackNormalOct(float3 n)
{
    // Ensure unit length
    n = normalize(n);

    // Project onto octahedron
    float invL1 = 1.0 / (abs(n.x) + abs(n.y) + abs(n.z));
    n.xy *= invL1;

    // Fold lower hemisphere
    if (n.z < 0.0)
    {
        float2 folded = (1.0 - abs(n.yx)) * (float2(n.x >= 0 ? 1 : -1, n.y >= 0 ? 1 : -1));
        n.xy = folded;
    }

    // Convert to signed normalized 16-bit
    int2 norm;
    norm.x = (int) round(clamp(n.x, -1.0, 1.0) * 32767.0);
    norm.y = (int) round(clamp(n.y, -1.0, 1.0) * 32767.0);

    // Pack into uint
    return (uint(norm.x) & 0xFFFF) | (uint(norm.y) << 16);
}


float3 UnpackNormalOct(uint packed)
{
    // Extract signed 16-bit values
    int2 norm;
    norm.x = (int) (packed << 16) >> 16; // sign-extend lower 16
    norm.y = (int) packed >> 16; // upper 16 already sign-extended

    float2 f = float2(norm) / 32767.0;

    // Reconstruct z
    float3 n = float3(f.x, f.y, 1.0 - abs(f.x) - abs(f.y));

    // Unfold lower hemisphere
    if (n.z < 0.0)
    {
        float2 unfolded = (1.0 - abs(n.yx)) * (float2(n.x >= 0 ? 1 : -1, n.y >= 0 ? 1 : -1));
        n.xy = unfolded;
    }

    return normalize(n);
}

float3 ReconstructWorldPos(float4 svPos)
{
    // svPos.xy is in pixel coordinates in the fragment shader
    float2 uv01 = svPos.xy / _ScreenParams.xy; // 0..1
    float2 ndcXY = uv01 * 2.0 - 1.0; // -1..1

    // D3D-style depth: ndcZ is 0..1 (Unity built-in on DX/Vulkan/Metal)
    float ndcZ = svPos.z;

    // In PS, svPos.w is typically 1/clip.w => clip.w = 1/svPos.w
    float clipW = 1.0 / svPos.w;

    // Reconstruct clip-space (pre-divide)
    float4 clipPos = float4(ndcXY, ndcZ, 1.0) * clipW;

    // Back to view space
    float4 viewPos = mul(unity_CameraInvProjection, clipPos);
    viewPos /= viewPos.w;

    // Back to world space
    float4 worldPos = mul(unity_CameraToWorld, viewPos);
    return worldPos.xyz;
}

