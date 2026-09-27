// SPDX-License-Identifier: MPL-2.0

#define OUTSIDE_DROPLET_PHYSICS 1
Texture2D OriginalTexture : register(t0);
SamplerState OriginalSampler : register(s0);
cbuffer constants : register(b0)
{
    float4 frame;
    float4 style;
    float4 region;
    float4 geometry;
    float4 inverse0;
    float4 inverse1;
    float4 inverse2;
    float4 diagnostic;
    float4 heads[512];
    float4 trails[512];
    uint4 tiles[1024];
};
#define inputSize frame.xy
#define outsideDropletAppearance frame.z
#define outsideDropletOutlineOpacity style.z
#define regionCenter region.xy
#define regionSize region.zw
#define regionShape geometry.x
#define regionRotationCos geometry.y
#define regionRotationSin geometry.z
#define regionFeather geometry.w
#define quadInverseRow0 inverse0.xyz
#define quadInverseRow1 inverse1.xyz
#define quadInverseRow2 inverse2.xyz
#include "OutsideDropletSurface.hlsli"
#include "GlassRegion.hlsli"

uint BitIndex(uint value)
{
    uint result = 0;
    if ((value & 65535u) == 0u) { result += 16u; value >>= 16; }
    if ((value & 255u) == 0u) { result += 8u; value >>= 8; }
    if ((value & 15u) == 0u) { result += 4u; value >>= 4; }
    if ((value & 3u) == 0u) { result += 2u; value >>= 2; }
    if ((value & 1u) == 0u) result += 1u;
    return result;
}

float RegionCoverage(float2 pixel)
{
    float coverage = 0.0f;
    if (regionShape >= 2.5f)
    {
        float3 inputPoint = float3(pixel / inputSize, 1.0f);
        float u = dot(quadInverseRow0, inputPoint);
        float v = dot(quadInverseRow1, inputPoint);
        float denominator = dot(quadInverseRow2, inputPoint);
        if (inverse0.w >= .5f && abs(denominator) >= .000001f)
            coverage = GetQuadRegionMask(float2(u, v) / denominator, u, v, denominator, inputSize);
    }
    else
    {
        coverage = GetRegionMask(RotateToRegionLocal(pixel - regionCenter * inputSize), regionSize * inputSize * .5f);
    }
    return coverage;
}

void Evaluate(uint index, float2 pixel, inout float3 lighting, inout float deepest, inout float4 refraction)
{
    float4 head = heads[index];
    if (head.z <= 0.0f) return;
    float4 trail = trails[index];
    float2 centerOffset = pixel - head.xy;
    float2 offset = centerOffset / (head.z * float2(1.0f, head.w));
    float depth;
    float3 evaluated = EvaluateFallingOutsideDropletAppearance(offset, trail.z, head.z, head.w, 1.0f, depth);
    AccumulateOutsideDropletRefraction(centerOffset, depth, trail.w, refraction);
    float2 end = head.xy - float2(0.0f, head.z * head.w * smoothstep(.05f, .35f, trail.z) * .65f);
    float2 start = float2(head.x, min(trail.x, end.y));
    float lengthSquared = dot(end - start, end - start);
    if (trail.y > .5f && lengthSquared > .0001f)
    {
        float progress = saturate(dot(pixel - start, end - start) / lengthSquared);
        float taper = lerp(.12f, 1.0f, smoothstep(0.0f, 1.0f, progress));
        float radius = max(head.z * .20f, .50f) * taper;
        float fade = smoothstep(0.0f, .35f, progress) * smoothstep(0.0f, 2.0f, sqrt(lengthSquared));
        float trailDepth;
        float3 trailLighting = EvaluateOutsideDropletCapsule(pixel, start, end, radius, 1.0f, trailDepth) * fade;
        evaluated = MergeOutsideDropletTrail(evaluated, trailLighting);
        depth = max(depth, trailDepth * fade * .42f);
    }
    evaluated *= trail.w;
    depth *= trail.w;
    // 深さの交差を短い範囲で連続化し、内側の輪郭だけを抑える。
    if (depth > deepest)
    {
        lighting.xy *= 1.0f - smoothstep(0.0f, .06f, depth - deepest);
        lighting.xy = max(lighting.xy, evaluated.xy);
        deepest = depth;
    }
    else
    {
        lighting.xy = max(lighting.xy, evaluated.xy * (1.0f - smoothstep(0.0f, .06f, deepest - depth)));
    }
    lighting.z = max(lighting.z, evaluated.z);
}

float4 main(float4 position : SV_POSITION, float4 scene : SCENE_POSITION, float4 uv : TEXCOORD0) : SV_Target
{
    // 分割描画で補間座標が微小に変わっても、同じ1/1024画素の位置で形状を評価する。
    float2 pixel = round((scene.xy - style.xy) * 1024.0f) / 1024.0f;
    float4 original = OriginalTexture.SampleLevel(OriginalSampler, uv.xy, 0);
    float regionMask = RegionCoverage(pixel);
    if (regionMask <= 0.0f || style.w <= 0.0f) return original;
    float3 lighting = 0.0f;
    float deepest = -1.0f;
    float4 refraction = 0.0f;
    if (frame.w > .5f)
    {
        [loop] for (uint i = 0u; i < (uint)diagnostic.y; i++) Evaluate(i, pixel, lighting, deepest, refraction);
    }
    else
    {
        uint2 cell = (uint2)clamp(floor(pixel / frame.xy * 16.0f), 0.0f, 15.0f);
        uint tile = cell.y * 16u + cell.x;
        [loop] for (uint word = 0u; word < ((uint)diagnostic.y + 31u) / 32u; word++)
        {
            uint mask = tiles[tile * 4u + word / 4u][word % 4u];
            [loop] while (mask != 0u)
            {
                uint bit = BitIndex(mask);
                Evaluate(word * 32u + bit, pixel, lighting, deepest, refraction);
                mask &= mask - 1u;
            }
        }
    }
    if (diagnostic.x == 6.0f)
    {
        float coverage = saturate(lighting.z * regionMask);
        return float4(coverage.xxx * original.a, original.a);
    }
    float3 color = original.a > .000001f ? original.rgb / original.a : 0.0f;
    float influence = regionMask * style.w;
    if (IsRealisticOutsideDroplet() && refraction.w > .00000001f && original.a > .000001f)
    {
        float2 samplePixel = clamp(pixel + refraction.xy / refraction.w, .5f, frame.xy - .5f);
        // 部分入力の割当原点をscene座標で揃え、入力原点の小数部分は保持する。
        float2 texelOrigin = round(uv.xy / uv.zw - scene.xy) + style.xy;
        float2 sampleUv = (texelOrigin + samplePixel) * uv.zw;
        float4 sampleColor = OriginalTexture.SampleLevel(OriginalSampler, sampleUv, 0);
        color = lerp(color, sampleColor.a > .000001f ? sampleColor.rgb / sampleColor.a : color,
            saturate(refraction.z * influence) * saturate(sampleColor.a));
    }
    color = CompositeOutsideDropletSurface(color, lighting, influence, .16f, .28f, .22f);
    return float4(saturate(color) * original.a, original.a);
}
