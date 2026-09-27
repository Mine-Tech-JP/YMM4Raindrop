// SPDX-License-Identifier: MPL-2.0

// 従来方式と簡易物理で共用する描画式。
float2 RotateToRegionLocal(float2 offsetPixels)
{
    return float2(
        regionRotationCos * offsetPixels.x + regionRotationSin * offsetPixels.y,
        -regionRotationSin * offsetPixels.x + regionRotationCos * offsetPixels.y);
}

float GetRegionMask(float2 localPixelPosition, float2 halfSizePixels)
{
    if (regionSize.x <= 0.0f || regionSize.y <= 0.0f)
    {
        return 0.0f;
    }

    if (regionShape < 0.5f)
    {
        return 1.0f;
    }

    float distanceInsidePixels;
    if (regionShape < 1.5f)
    {
        float2 distanceFromCenterPixels = abs(localPixelPosition);
        distanceInsidePixels = min(
            halfSizePixels.x - distanceFromCenterPixels.x,
            halfSizePixels.y - distanceFromCenterPixels.y);
    }
    else
    {
        float2 safeHalfSizePixels = max(
            halfSizePixels,
            float2(0.000001f, 0.000001f));
        float2 normalizedPosition = localPixelPosition / safeHalfSizePixels;
        float ellipseDistanceSquared = dot(
            normalizedPosition,
            normalizedPosition);
        float2 ellipseGradient = 2.0f * localPixelPosition / max(
            safeHalfSizePixels * safeHalfSizePixels,
            float2(0.000001f, 0.000001f));
        float gradientLength = length(ellipseGradient);
        distanceInsidePixels = gradientLength > 0.000001f
            ? (1.0f - ellipseDistanceSquared) / gradientLength
            : min(safeHalfSizePixels.x, safeHalfSizePixels.y);
    }

    if (regionFeather <= 0.0f)
    {
        return distanceInsidePixels >= 0.0f ? 1.0f : 0.0f;
    }

    return saturate(distanceInsidePixels / regionFeather);
}

float GetQuadRegionMask(
    float2 regionUv,
    float numeratorU,
    float numeratorV,
    float denominator,
    float2 safeInputSize)
{
    float2 localDistance = min(regionUv, 1.0f - regionUv);
    float denominatorSquared = max(
        denominator * denominator,
        0.000000000001f);
    float2 gradientU = (
        quadInverseRow0.xy * denominator -
        quadInverseRow2.xy * numeratorU) /
        denominatorSquared /
        safeInputSize;
    float2 gradientV = (
        quadInverseRow1.xy * denominator -
        quadInverseRow2.xy * numeratorV) /
        denominatorSquared /
        safeInputSize;
    float distanceInsidePixels = min(
        localDistance.x / max(length(gradientU), 0.000001f),
        localDistance.y / max(length(gradientV), 0.000001f));

    if (regionFeather <= 0.0f)
    {
        return distanceInsidePixels >= 0.0f ? 1.0f : 0.0f;
    }

    return saturate(distanceInsidePixels / regionFeather);
}
