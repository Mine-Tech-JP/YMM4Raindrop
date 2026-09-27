// SPDX-License-Identifier: MPL-2.0

// 従来方式と簡易物理で共用する描画式。
float2 GetFallingOutsideDropletWidthProfile(
    float normalizedY,
    float fallProgress)
{
    float morph = smoothstep(0.05f, 0.35f, saturate(fallProgress));
    float height = saturate(normalizedY * 0.5f + 0.5f);
    float tipBlend = smoothstep(0.0f, 0.20f, height);
    float taperedWidth =
        0.065f + height * 1.44f + (1.0f - tipBlend) * 0.10f;
    float tipParameter = saturate(height / 0.20f);
    float taperedSlope = 0.72f -
        1.50f * tipParameter * (1.0f - tipParameter);
    float insideShape =
        step(-0.9999f, normalizedY) * (1.0f - step(0.9999f, normalizedY));
    return float2(
        lerp(1.0f, taperedWidth, morph),
        taperedSlope * morph * insideShape);
}

float2 ShapeFallingOutsideDroplet(
    float2 normalizedOffset,
    float fallProgress)
{
    float widthScale = GetFallingOutsideDropletWidthProfile(
        normalizedOffset.y,
        fallProgress).x;
    return float2(
        normalizedOffset.x / max(widthScale, 0.02f),
        normalizedOffset.y);
}

float3 EvaluateTransparentOutsideDropletLighting(
    float2 shapedOffset,
    float dropletDistance,
    float edgeWidth,
    float coverage)
{
    float2 direction = shapedOffset / max(dropletDistance, 0.0001f);
    float lowerSide = saturate(direction.y * 0.5f + 0.5f);
    float rimWidth = lerp(0.08f, 0.22f, lowerSide);
    float innerEdge = 1.0f - rimWidth;
    float rim = smoothstep(
        innerEdge - edgeWidth,
        innerEdge + edgeWidth,
        dropletDistance) * coverage;
    float shadow = rim * lerp(0.62f, 0.96f, lowerSide);
    float highlightSpot = 1.0f - smoothstep(
        0.10f,
        0.25f,
        length((shapedOffset - float2(-0.28f, -0.62f)) * float2(0.8f, 1.4f)));
    float highlight = highlightSpot * coverage * (1.0f - rim) * 0.65f;
    return float3(highlight, shadow, coverage);
}

float3 EvaluateOutsideDropletLighting(
    float2 shapedOffset,
    float dropletDistance,
    float edgeWidth)
{
    float coverage = 1.0f - smoothstep(
        1.0f - edgeWidth,
        1.0f + edgeWidth,
        dropletDistance);
    if (outsideDropletAppearance >= 0.5f)
    {
        return EvaluateTransparentOutsideDropletLighting(
            shapedOffset,
            dropletDistance,
            edgeWidth,
            coverage);
    }

    float rim = smoothstep(0.55f, 0.92f, dropletDistance) * coverage;
    float2 direction = shapedOffset / max(dropletDistance, 0.0001f);
    float lightDirection = saturate(
        dot(direction, normalize(float2(-1.0f, -1.0f))) * 0.5f + 0.5f);
    float highlightSpot = (
        1.0f - smoothstep(
            0.08f,
            0.30f,
            length(shapedOffset - float2(-0.34f, -0.34f)))) *
        coverage;
    float highlight = saturate(rim * lightDirection * 0.55f + highlightSpot * 0.85f);
    float shadow = rim * (1.0f - lightDirection) * 0.75f;
    return float3(highlight, shadow, coverage);
}

float3 EvaluateOutsideDropletAppearance(
    float2 normalizedOffset,
    float fallProgress,
    float edgeWidth,
    out float interiorDepth)
{
    float2 shapedOffset = ShapeFallingOutsideDroplet(
        normalizedOffset,
        fallProgress);
    float dropletDistance = length(shapedOffset);
    interiorDepth = saturate(1.0f - dropletDistance);
    return EvaluateOutsideDropletLighting(
        shapedOffset,
        dropletDistance,
        edgeWidth);
}

float3 EvaluateFallingOutsideDropletAppearance(
    float2 normalizedOffset,
    float fallProgress,
    float dropletRadius,
    float verticalScale,
    float sourcePixelStep,
    out float interiorDepth)
{
    float2 widthProfile = GetFallingOutsideDropletWidthProfile(
        normalizedOffset.y,
        fallProgress);
    float widthScale = max(widthProfile.x, 0.02f);
    float2 shapedOffset = float2(
        normalizedOffset.x / widthScale,
        normalizedOffset.y);
    float dropletDistance = length(shapedOffset);
    float2 direction = shapedOffset / max(dropletDistance, 0.0001f);
    // 境界上の勾配で1画素幅を換算し、細い先端を縦長のぼかしにしない。
    float normalizedGradientX = direction.x / widthScale;
    float normalizedGradientY =
        (direction.y - direction.x * direction.x * widthProfile.y / widthScale) /
        max(verticalScale, 0.001f);
    float edgeWidth = max(
        sourcePixelStep * length(float2(normalizedGradientX, normalizedGradientY)) /
            max(dropletRadius, 0.001f),
        0.001f);
    float verticalEdge = sourcePixelStep /
        max(dropletRadius * verticalScale, 0.001f);
    float axialCoverage = 1.0f - smoothstep(
        1.0f,
        1.0f + max(verticalEdge, 0.001f),
        abs(normalizedOffset.y));
    interiorDepth = saturate(1.0f - dropletDistance) * axialCoverage;
    return EvaluateOutsideDropletLighting(
        shapedOffset,
        dropletDistance,
        edgeWidth) * axialCoverage;
}

bool IsRealisticOutsideDroplet()
{
    return outsideDropletAppearance >= 1.5f && outsideDropletAppearance < 2.5f;
}

void AccumulateOutsideDropletRefraction(
    float2 centerOffsetPixels,
    float depth,
    float visibility,
    inout float4 refraction)
{
    if (!IsRealisticOutsideDroplet() || depth <= 0.0f || visibility <= 0.0f)
        return;

    float radialDistance = 1.0f - saturate(depth);
    // 物理定数ではなく、倒立した縮小像を作る近似倍率。
    float magnification = 2.0f + radialDistance * radialDistance;
    float2 offset = -(1.0f + magnification) * centerOffsetPixels;
#ifndef OUTSIDE_DROPLET_PHYSICS
    if (regionShape < 2.5f)
    {
        offset = float2(
            regionRotationCos * offset.x - regionRotationSin * offset.y,
            regionRotationSin * offset.x + regionRotationCos * offset.y);
    }
#endif
    float maximumOffset = 256.0f * max(max(inputSize.y, 1.0f) / 1080.0f, 0.25f);
    offset = clamp(offset, -maximumOffset, maximumOffset);
    float depthSquared = depth * depth;
    float weight = depthSquared * depthSquared * saturate(visibility);
    refraction.xy += offset * weight;
    refraction.z = max(refraction.z, smoothstep(0.0f, 0.16f, depth) * saturate(visibility));
    refraction.w += weight;
}

float3 EvaluateOutsideDropletCapsule(
    float2 samplePosition,
    float2 segmentStart,
    float2 segmentEnd,
    float radius,
    float edgeWidth,
    out float interiorDepth)
{
    float2 segment = segmentEnd - segmentStart;
    float lengthSquared = max(dot(segment, segment), 0.0001f);
    float factor = saturate(
        dot(samplePosition - segmentStart, segment) / lengthSquared);
    float2 nearest = segmentStart + segment * factor;
    float distanceToSegment = length(samplePosition - nearest);
    interiorDepth = saturate(1.0f - distanceToSegment / max(radius, 0.001f));
    edgeWidth = max(edgeWidth, 0.001f);
    float coverage = 1.0f - smoothstep(
        max(radius - edgeWidth, 0.0f),
        radius + edgeWidth,
        distanceToSegment);
    float lightSide = saturate(
        0.5f +
        (nearest.x - samplePosition.x) / max(radius * 2.0f, 0.001f));
    if (outsideDropletAppearance >= 0.5f)
    {
        float innerRadius = radius * 0.62f;
        float rim = smoothstep(
            max(innerRadius - edgeWidth, 0.0f),
            innerRadius + edgeWidth,
            distanceToSegment) * coverage;
        return float3(
            rim * lightSide * 0.08f,
            rim * (0.35f + (1.0f - lightSide) * 0.40f),
            coverage);
    }

    return float3(
        coverage * lightSide * 0.55f,
        coverage * (1.0f - lightSide) * 0.45f,
        coverage);
}

float3 MergeOutsideDropletTrail(float3 dropletLighting, float3 trailLighting)
{
    float dropletCoverage = saturate(dropletLighting.z);
    float trailCoverage = saturate(trailLighting.z);
    float2 visibleDropletEdge = dropletLighting.xy * (1.0f - trailCoverage);
    float2 visibleTrailEdge = trailLighting.xy * (1.0f - dropletCoverage) * 0.42f;
    return float3(
        max(visibleDropletEdge, visibleTrailEdge),
        max(dropletCoverage, trailCoverage * 0.42f));
}

float3 CompositeOutsideDropletSurface(
    float3 sceneColor,
    float3 dropletLighting,
    float influence,
    float surfaceWeight,
    float highlightWeight,
    float shadowWeight)
{
    float safeInfluence = saturate(influence);
    if (outsideDropletAppearance >= 0.5f)
    {
        // 黒い縁の不透明度は飽和後に掛け、0%では白い反射だけを残す。
        float outlineAlpha =
            saturate(dropletLighting.y * safeInfluence * 1.75f) *
            saturate(outsideDropletOutlineOpacity);
        float3 transparentResult = lerp(
            sceneColor,
            float3(0.0f, 0.0f, 0.0f),
            outlineAlpha);
        return lerp(
            transparentResult,
            float3(1.0f, 1.0f, 1.0f),
            saturate(dropletLighting.x * safeInfluence * 0.65f));
    }

    float surface = saturate(dropletLighting.z * safeInfluence);
    float3 stableSurfaceColor = float3(0.62f, 0.68f, 0.72f);
    float3 result = lerp(
        sceneColor,
        stableSurfaceColor,
        surface * surfaceWeight);
    result = lerp(
        result,
        float3(1.0f, 1.0f, 1.0f),
        saturate(dropletLighting.x * safeInfluence * highlightWeight));
    result = lerp(
        result,
        float3(0.0f, 0.0f, 0.0f),
        saturate(dropletLighting.y * safeInfluence * shadowWeight));
    return saturate(result);
}
