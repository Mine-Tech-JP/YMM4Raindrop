// SPDX-License-Identifier: MPL-2.0

Texture2D OriginalTexture : register(t0);
Texture2D BlurredTexture : register(t1);
Texture2D MaskTexture : register(t2);

SamplerState OriginalSampler : register(s0);
SamplerState BlurredSampler : register(s1);
SamplerState MaskSampler : register(s2);

cbuffer constants : register(b0)
{
    float2 inputSize : packoffset(c0.x);
    float2 inputOrigin : packoffset(c0.z);

    float2 regionCenter : packoffset(c1.x);
    float2 regionSize : packoffset(c1.z);

    float fogAmount : packoffset(c2.x);
    float tintMix : packoffset(c2.y);
    float regionFeather : packoffset(c2.z);
    float debugView : packoffset(c2.w);

    float3 fogTint : packoffset(c3.x);
    float padding : packoffset(c3.w);

    float regionShape : packoffset(c4.x);
    float regionRotationCos : packoffset(c4.y);
    float regionRotationSin : packoffset(c4.z);
    float quadValid : packoffset(c4.w);

    float wipeResidue : packoffset(c5.x);
    float wipeVariation : packoffset(c5.y);
    float fogNoise : packoffset(c5.z);
    float noiseSeed : packoffset(c5.w);

    float3 quadInverseRow0 : packoffset(c6.x);
    float outsideDropletRainEnabled : packoffset(c6.w);

    float3 quadInverseRow1 : packoffset(c7.x);
    float outsideDropletRainStartSeconds : packoffset(c7.w);

    float3 quadInverseRow2 : packoffset(c8.x);
    float outsideDropletRainDurationSeconds : packoffset(c8.w);

    float outsideDropletAmount : packoffset(c9.x);
    float outsideDropletSizeScale : packoffset(c9.y);
    float outsideDropletStrength : packoffset(c9.z);
    float outsideDropletSeed : packoffset(c9.w);

    float outsideDropletLocalTimeSeconds : packoffset(c10.x);
    float outsideDropletFallEnabled : packoffset(c10.y);
    float outsideDropletFallingRatio : packoffset(c10.z);
    float outsideDropletFallSpeedScale : packoffset(c10.w);

    float outsideDropletTrailLength : packoffset(c11.x);
    float outsideDropletDeformWithSurface : packoffset(c11.y);
    float outsideDropletAppearance : packoffset(c11.z);
    float outsideDropletMergeEnabled : packoffset(c11.w);

    float3 quadForwardRow0 : packoffset(c12.x);
    float outsideDropletOutlineOpacity : packoffset(c12.w);

    float3 quadForwardRow1 : packoffset(c13.x);
    float outsideDropletRenderPass : packoffset(c13.w);

    float3 quadForwardRow2 : packoffset(c14.x);
    float outsideDropletFallFrequency : packoffset(c14.w);

    float4 outsideDropletMergeHeadsA[72] : packoffset(c15);
    float4 outsideDropletMergeHeadsB[72] : packoffset(c87);
    float4 outsideDropletMergeStaticSlots[2048] : packoffset(c159);
    float4 outsideDropletInitialColumns : packoffset(c2207);
    float4 outsideDropletInitialCounts : packoffset(c2208);
    uint4 outsideDropletInitialStates[1664] : packoffset(c2209);
};

#include "OutsideDropletSurface.hlsli"
#include "GlassRegion.hlsli"

static const int OutsideDropletBaseEmitterCount = 6;
static const float TwoPi = 6.28318530718f;

float GetDiagnosticCoordinatePhase(float2 coordinate)
{
    float phase17 = frac(
        dot(coordinate, float2(1.0f, 0.61803398875f)) /
        17.0f);
    float phase29 = frac(
        dot(coordinate, float2(0.41421356237f, 1.0f)) /
        29.0f);
    return saturate(phase17 * 0.65f + phase29 * 0.35f);
}



float HashNoise2D(float2 lattice, float seed)
{
    float hashInput = dot(lattice, float2(127.1f, 311.7f)) + seed * 74.7f;
    return frac(sin(hashInput) * 43758.5453f);
}

float GetOutsideDropletCellStateFingerprint(
    float2 patternPosition,
    float cellSizePixels,
    float seed)
{
    float safeCellSize = max(cellSizePixels, 1.0f);
    float2 cell = floor(patternPosition / safeCellSize);
    float activationHash = HashNoise2D(cell, seed + 3.1f);
    float centerXHash = HashNoise2D(cell, seed + 11.7f);
    float centerYHash = HashNoise2D(cell, seed + 29.3f);
    float radiusHash = HashNoise2D(cell, seed + 47.9f);
    float verticalScaleHash = HashNoise2D(cell, seed + 61.1f);
    return saturate(
        activationHash * 0.31f +
        centerXHash * 0.23f +
        centerYHash * 0.19f +
        radiusHash * 0.15f +
        verticalScaleHash * 0.12f);
}

float GetOutsideDropletHashInputWithoutSine(
    float2 lattice,
    float seed,
    float offset)
{
    return
        dot(lattice, float2(127.1f, 311.7f)) +
        (seed + offset) * 74.7f;
}

uint MixOutsideDropletHashInputWithoutSine(
    float2 lattice,
    float seed,
    float offset)
{
    float hashInput = GetOutsideDropletHashInputWithoutSine(
        lattice,
        seed,
        offset);
    uint mixed = asuint(hashInput);
    mixed ^= mixed >> 16;
    mixed *= 0x7feb352du;
    mixed ^= mixed >> 15;
    mixed *= 0x846ca68bu;
    mixed ^= mixed >> 16;
    return mixed;
}

uint GetOutsideDropletCellStatePreAvalancheBitsWithoutSine(
    float2 patternPosition,
    float cellSizePixels,
    float seed)
{
    float safeCellSize = max(cellSizePixels, 1.0f);
    float2 cell = floor(patternPosition / safeCellSize);
    uint fingerprint = MixOutsideDropletHashInputWithoutSine(
        cell,
        seed,
        3.1f);
    fingerprint ^= MixOutsideDropletHashInputWithoutSine(
        cell,
        seed,
        11.7f) * 0x9e3779b9u;
    fingerprint ^= MixOutsideDropletHashInputWithoutSine(
        cell,
        seed,
        29.3f) * 0x85ebca6bu;
    fingerprint ^= MixOutsideDropletHashInputWithoutSine(
        cell,
        seed,
        47.9f) * 0xc2b2ae35u;
    fingerprint ^= MixOutsideDropletHashInputWithoutSine(
        cell,
        seed,
        61.1f) * 0x27d4eb2fu;
    return fingerprint;
}

uint GetOutsideDropletCellStateFingerprintBitsWithoutSine(
    float2 patternPosition,
    float cellSizePixels,
    float seed)
{
    float safeCellSize = max(cellSizePixels, 1.0f);
    float2 cell = floor(patternPosition / safeCellSize);
    uint fingerprint = MixOutsideDropletHashInputWithoutSine(
        cell,
        seed,
        3.1f);
    fingerprint ^= MixOutsideDropletHashInputWithoutSine(
        cell,
        seed,
        11.7f) * 0x9e3779b9u;
    fingerprint ^= MixOutsideDropletHashInputWithoutSine(
        cell,
        seed,
        29.3f) * 0x85ebca6bu;
    fingerprint ^= MixOutsideDropletHashInputWithoutSine(
        cell,
        seed,
        47.9f) * 0xc2b2ae35u;
    fingerprint ^= MixOutsideDropletHashInputWithoutSine(
        cell,
        seed,
        61.1f) * 0x27d4eb2fu;
    fingerprint ^= fingerprint >> 16;
    fingerprint *= 0x7feb352du;
    fingerprint ^= fingerprint >> 15;
    uint folded =
        fingerprint ^
        (fingerprint >> 8) ^
        (fingerprint >> 16) ^
        (fingerprint >> 24);
    return folded;
}

float GetOutsideDropletCellStateFingerprintWithoutSine(
    float2 patternPosition,
    float cellSizePixels,
    float seed)
{
    uint folded = GetOutsideDropletCellStateFingerprintBitsWithoutSine(
        patternPosition,
        cellSizePixels,
        seed);
    return (float)(folded & 255u) / 255.0f;
}

// 水滴の乱数キーは整数だけで混合し、小数の演算結果をビット再解釈しない。
// 既存のくもり・拭きムラと旧診断用の HashNoise2D は変更しない。
// 旧方式との配置互換は持たず、同じSeedでも水滴の配置と落下周期は変わる。
uint MixOutsideDropletBits(uint value)
{
    value ^= value >> 16;
    value *= 0x7feb352du;
    value ^= value >> 15;
    value *= 0x846ca68bu;
    value ^= value >> 16;
    return value;
}

uint GetOutsideDropletCellKey(int2 cell, uint seed, uint layer)
{
    uint key = MixOutsideDropletBits(seed ^ 0xa511e9b3u);
    key = MixOutsideDropletBits(key ^ (uint)cell.x);
    key = MixOutsideDropletBits(key ^ (uint)cell.y);
    return MixOutsideDropletBits(key ^ layer);
}

uint GetOutsideDropletEmitterKey(uint seed, uint layer, uint emitterIndex)
{
    uint key = MixOutsideDropletBits(seed ^ 0x63d83595u);
    key = MixOutsideDropletBits(key ^ layer);
    return MixOutsideDropletBits(key ^ emitterIndex);
}

uint GetOutsideDropletCycleKey(uint emitterKey, uint cycleIndex)
{
    return MixOutsideDropletBits(
        emitterKey ^ MixOutsideDropletBits(cycleIndex ^ 0xb5297a4du));
}

// 上位24bitだけを [0, 1) へ変換し、float丸めによる1.0への到達を避ける。
// channel: 0=密度選択、1/2=中心、3=半径、4=縦横比、5/6=待機、7=位相、8=落下選択、9=初回出現時刻。
float OutsideDropletRandom(uint key, uint channel)
{
    uint bits = MixOutsideDropletBits(
        key ^ MixOutsideDropletBits(channel + 0x9e3779b9u));
    return (float)(bits >> 8) * (1.0f / 16777216.0f);
}

// CPUと同じ24bit乱数から初回時刻を求める。積和の融合による境界ずれを避ける。
float GetOutsideDropletAppearanceTime(uint key)
{
    precise float delay = outsideDropletRainDurationSeconds * OutsideDropletRandom(key, 9u);
    precise float appearanceTime = outsideDropletRainStartSeconds + delay;
    return appearanceTime;
}

float GetOutsideDropletAppearanceVisibility(uint key)
{
    if (outsideDropletRainEnabled < 0.5f) return 1.0f;
    return step(GetOutsideDropletAppearanceTime(key), outsideDropletLocalTimeSeconds);
}

float ValueNoise2D(float2 position, float seed)
{
    float2 lattice = floor(position);
    float2 blend = frac(position);
    blend = blend * blend * (3.0f - 2.0f * blend);
    float lower = lerp(
        HashNoise2D(lattice, seed),
        HashNoise2D(lattice + float2(1.0f, 0.0f), seed),
        blend.x);
    float upper = lerp(
        HashNoise2D(lattice + float2(0.0f, 1.0f), seed),
        HashNoise2D(lattice + float2(1.0f, 1.0f), seed),
        blend.x);
    return lerp(lower, upper, blend.y);
}

// 円の横幅を高さで変え、先端を丸めた縦長滴へ移行する。幅倍率とY微分を返す。




// 透明表示では中心を着色せず、暗い縁と上側の小さな反射で厚みを表す。








// 初期配置はセル添字の2bit、動的変化は同じ一意添字の固定tableから取得する。
float2 GetOutsideDropletMergeState(uint cellKey, int2 cell, uint layer, float radiusLimit)
{
    float2 result = float2(1.0f, 1.0f);
    if (outsideDropletMergeEnabled < 0.5f) return result;
    if (outsideDropletInitialColumns.w >= 0.5f)
    {
        uint layerIndex = layer == 101u ? 0u : (layer == 307u ? 1u : 2u);
        uint columns = (uint)outsideDropletInitialColumns[layerIndex];
        uint count = (uint)outsideDropletInitialCounts[layerIndex];
        if (cell.x < 0 || cell.y < 0 || columns == 0u ||
            (uint)cell.x >= columns || (uint)cell.y >= count / columns) return result;
        uint layerOffset = layerIndex == 0u ? 0u : (uint)outsideDropletInitialCounts.x;
        if (layerIndex == 2u) layerOffset += (uint)outsideDropletInitialCounts.y;
        cellKey = layerOffset + (uint)cell.y * columns + (uint)cell.x;
        uint wordIndex = cellKey >> 4;
        uint packed = outsideDropletInitialStates[wordIndex >> 2][wordIndex & 3u];
        uint state = (packed >> ((cellKey & 15u) * 2u)) & 3u;
        result.x = (state & 1u) == 0u ? 1.0f : 0.0f;
        result.y = (state & 2u) == 0u ? 1.0f : min(1.075f, radiusLimit);
    }
    uint slotIndex = cellKey & 2047u;
    [loop]
    for (uint probe = 0u; probe < 32u; probe++)
    {
        float4 slot = outsideDropletMergeStaticSlots[slotIndex];
        if (slot.w < 0.5f) return result;
        if ((uint)slot.x == (cellKey & 65535u) && (uint)slot.y == (cellKey >> 16))
            return float2(saturate(slot.z), min(slot.w, radiusLimit));
        slotIndex = (slotIndex + 1u) & 2047u;
    }
    return result;
}



// xyは加重した入力ピクセル変位、zは被覆率、wは重みの合計。
// 深い内部を優先して連続的に混ぜ、滴ごとの追加テクスチャ参照を避ける。


float3 EvaluateOutsideDropletLayer(
    float2 patternPosition,
    float cellSizePixels,
    float amount,
    uint layer,
    float activationScale,
    out float interiorDepth,
    inout float4 refraction)
{
    float safeCellSize = max(cellSizePixels, 1.0f);
    float2 cellPosition = patternPosition / safeCellSize;
    int2 cell = (int2)floor(cellPosition);
    float2 localPosition = frac(cellPosition);
    uint cellKey = GetOutsideDropletCellKey(cell, (uint)outsideDropletSeed, layer);

    float activation = OutsideDropletRandom(cellKey, 0u);
    float active = step(activation, saturate(amount * activationScale));
    active *= GetOutsideDropletAppearanceVisibility(cellKey);
    float2 center = float2(
        lerp(0.25f, 0.75f, OutsideDropletRandom(cellKey, 1u)),
        lerp(0.25f, 0.75f, OutsideDropletRandom(cellKey, 2u)));
    float radius = lerp(
        0.11f,
        0.21f,
        OutsideDropletRandom(cellKey, 3u));
    float verticalScale = lerp(
        0.88f,
        1.18f,
        OutsideDropletRandom(cellKey, 4u));
    float2 clearance = max(min(center, 1.0f - center) - 0.001f / safeCellSize, 0.0f);
    float radiusLimit = clamp(min(clearance.x / radius, clearance.y / (radius * verticalScale)), 1.0f, 1.35f);
    float2 mergeState = GetOutsideDropletMergeState(cellKey, cell, layer, radiusLimit);
    float mergeVisibility = mergeState.x;
    radius *= mergeState.y * lerp(0.08f, 1.0f, mergeVisibility);
    float2 normalizedOffset =
        (localPosition - center) /
        float2(radius, radius * verticalScale);
    // セルごとに不連続な距離を微分すると、境界に水滴外の線が生じる。
    // 連続する座標の1画素幅だけを求め、現在セルの半径へ換算する。
    float sourcePixelStep = max(
        max(length(ddx(patternPosition)), length(ddy(patternPosition))),
        0.001f);
    float minimumRadius = max(
        safeCellSize * radius * min(1.0f, verticalScale),
        0.001f);
    float edgeWidth = max(sourcePixelStep / minimumRadius, 0.001f);
    float3 lighting = EvaluateOutsideDropletAppearance(
        normalizedOffset,
        0.0f,
        edgeWidth,
        interiorDepth) * active * mergeVisibility;
    AccumulateOutsideDropletRefraction(
        (localPosition - center) * safeCellSize,
        interiorDepth, active * mergeVisibility, refraction);
    interiorDepth *= active * mergeVisibility;
    return lighting;
}

float4 EvaluateOutsideDropletLifecycle(uint emitterKey, float sizeSpeedScale)
{
    float safeSpeed = clamp(outsideDropletFallSpeedScale, 0.25f, 4.0f);
    float fallDuration = 6.0f / (safeSpeed * sizeSpeedScale);
    float visibleWaitDuration = fallDuration * lerp(
        0.20f,
        0.45f,
        OutsideDropletRandom(emitterKey, 5u));
    float hiddenWaitDuration = fallDuration * lerp(
        0.15f,
        0.45f,
        OutsideDropletRandom(emitterKey, 6u));
    float cycleDuration = visibleWaitDuration + fallDuration + hiddenWaitDuration;
    float phaseOffset = OutsideDropletRandom(emitterKey, 7u) * cycleDuration;
    float absoluteCycleTime = max(outsideDropletLocalTimeSeconds, 0.0f) + phaseOffset;
    if (outsideDropletRainEnabled >= 0.5f)
    {
        float appearanceTime = GetOutsideDropletAppearanceTime(emitterKey);
        if (outsideDropletLocalTimeSeconds < appearanceTime)
            return float4(0.0f, 0.0f, 0.0f, 2.0f);
        absoluteCycleTime = outsideDropletLocalTimeSeconds - appearanceTime;
    }
    float cycleIndex = floor(absoluteCycleTime / cycleDuration);
    float cycleTime = absoluteCycleTime - cycleIndex * cycleDuration;
    if (cycleTime < visibleWaitDuration)
    {
        float waitProgress = saturate(cycleTime / visibleWaitDuration);
        return float4(
            0.0f,
            smoothstep(0.0f, 0.10f, waitProgress),
            cycleIndex,
            0.0f);
    }

    float fallTime = cycleTime - visibleWaitDuration;
    if (fallTime < fallDuration)
    {
        float progress = saturate(fallTime / fallDuration);
        float visibility = 1.0f - smoothstep(0.92f, 1.0f, progress);
        return float4(progress, visibility, cycleIndex, 1.0f);
    }

    return float4(1.0f, 0.0f, cycleIndex, 2.0f);
}

float2 MapInputUvToRegionUv(
    float2 inputUv,
    float2 safeInputSize,
    bool isQuad,
    out float valid)
{
    valid = 1.0f;
    if (isQuad)
    {
        float3 inputPoint = float3(inputUv, 1.0f);
        float denominator = dot(quadInverseRow2, inputPoint);
        if (abs(denominator) < 0.000001f)
        {
            valid = 0.0f;
            return float2(0.0f, 0.0f);
        }

        return float2(
            dot(quadInverseRow0, inputPoint),
            dot(quadInverseRow1, inputPoint)) / denominator;
    }

    float2 regionPixelSize = max(regionSize * safeInputSize, float2(0.000001f, 0.000001f));
    float2 regionCenterPixels = regionCenter * safeInputSize;
    float2 localPixelPosition = RotateToRegionLocal(
        inputUv * safeInputSize - regionCenterPixels);
    return localPixelPosition / regionPixelSize + 0.5f;
}

float GetOutsideDropletRegionPixelsPerInputPixel(
    float2 inputUv,
    float2 safeInputSize,
    float2 regionPixelSize,
    bool isQuad)
{
    if (!isQuad)
    {
        return 1.0f;
    }

    float3 inputPoint = float3(inputUv, 1.0f);
    float numeratorU = dot(quadInverseRow0, inputPoint);
    float numeratorV = dot(quadInverseRow1, inputPoint);
    float denominator = dot(quadInverseRow2, inputPoint);
    if (abs(denominator) < 0.000001f)
    {
        return 1.0f;
    }

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
    float2 stepX = float2(
        gradientU.x * regionPixelSize.x,
        gradientV.x * regionPixelSize.y);
    float2 stepY = float2(
        gradientU.y * regionPixelSize.x,
        gradientV.y * regionPixelSize.y);
    return max(max(length(stepX), length(stepY)), 0.001f);
}

float IsSourceInsideRegion(float2 regionUv)
{
    float insideBounds =
        step(0.0f, regionUv.x) *
        step(regionUv.x, 1.0f) *
        step(0.0f, regionUv.y) *
        step(regionUv.y, 1.0f);
    if (regionShape >= 1.5f && regionShape < 2.5f)
    {
        float2 ellipsePosition = (regionUv - 0.5f) * 2.0f;
        return insideBounds * step(dot(ellipsePosition, ellipsePosition), 1.0f);
    }

    return insideBounds;
}

float2 MapRegionUvToInputUv(
    float2 regionUv,
    float2 safeInputSize,
    float2 regionPixelSize,
    bool isQuad,
    out float valid)
{
    valid = 1.0f;
    if (isQuad)
    {
        float3 localPoint = float3(regionUv, 1.0f);
        float denominator = dot(quadForwardRow2, localPoint);
        if (abs(denominator) < 0.000001f)
        {
            valid = 0.0f;
            return float2(0.0f, 0.0f);
        }

        return float2(
            dot(quadForwardRow0, localPoint),
            dot(quadForwardRow1, localPoint)) / denominator;
    }

    float2 localPixelPosition = (regionUv - 0.5f) * regionPixelSize;
    float2 rotatedPixelPosition = float2(
        regionRotationCos * localPixelPosition.x -
            regionRotationSin * localPixelPosition.y,
        regionRotationSin * localPixelPosition.x +
            regionRotationCos * localPixelPosition.y);
    return (regionCenter * safeInputSize + rotatedPixelPosition) / safeInputSize;
}

float2 GetOutsideDropletSpawnRegionUv(uint cycleKey)
{
    if (regionShape >= 1.5f && regionShape < 2.5f)
    {
        float angle = OutsideDropletRandom(cycleKey, 1u) * TwoPi;
        float radius = sqrt(OutsideDropletRandom(cycleKey, 2u)) * 0.78f;
        return 0.5f +
            float2(cos(angle), sin(angle)) * radius * 0.5f;
    }

    return float2(
        lerp(0.08f, 0.92f, OutsideDropletRandom(cycleKey, 1u)),
        lerp(0.08f, 0.38f, OutsideDropletRandom(cycleKey, 2u)));
}



// 滴と水筋の内側に入った輪郭を消し、接続部に端の丸い線を残さない。


float3 EvaluateMappedOutsideDropletTrailSegment(
    float2 currentRegionPixelPosition,
    float2 startInputUv,
    float2 endInputUv,
    float2 safeInputSize,
    float2 regionPixelSize,
    float radius,
    float trailProgress,
    bool isQuad,
    out float interiorDepth)
{
    float startValid;
    float2 startRegionUv = MapInputUvToRegionUv(
        startInputUv,
        safeInputSize,
        isQuad,
        startValid);
    float endValid;
    float2 endRegionUv = MapInputUvToRegionUv(
        endInputUv,
        safeInputSize,
        isQuad,
        endValid);
    float sourcePixelStep = GetOutsideDropletRegionPixelsPerInputPixel(
        lerp(startInputUv, endInputUv, trailProgress),
        safeInputSize,
        regionPixelSize,
        isQuad);
    float3 lighting = EvaluateOutsideDropletCapsule(
        currentRegionPixelPosition,
        startRegionUv * regionPixelSize,
        endRegionUv * regionPixelSize,
        radius,
        sourcePixelStep,
        interiorDepth) * startValid * endValid;
    interiorDepth *= startValid * endValid;
    return lighting;
}

float3 RenderOutsideDropletHeadAndTrail(
    float2 currentRegionPixelPosition,
    float2 currentInputPixelPosition,
    float2 safeInputSize,
    float2 regionPixelSize,
    float2 headInputUv,
    float2 headRegionUv,
    float2 spawnInputUv,
    float dropletRadius,
    float verticalScale,
    float4 lifecycle,
    float fallDistance,
    float headInside,
    bool deformWithSurface,
    bool isQuad,
    out float interiorDepth,
    inout float4 refraction)
{
    float2 normalizedOffset;
    if (deformWithSurface)
    {
        normalizedOffset =
            (currentRegionPixelPosition - headRegionUv * regionPixelSize) /
            float2(dropletRadius, dropletRadius * verticalScale);
    }
    else
    {
        normalizedOffset =
            (currentInputPixelPosition - headInputUv * safeInputSize) /
            float2(dropletRadius, dropletRadius * verticalScale);
    }

    float teardropProgress = lifecycle.w >= 0.5f
        ? lifecycle.x
        : 0.0f;
    float sourcePixelStep = deformWithSurface
        ? GetOutsideDropletRegionPixelsPerInputPixel(
            headInputUv,
            safeInputSize,
            regionPixelSize,
            isQuad)
        : 1.0f;
    float dropletDepth;
    float3 dropletLighting = EvaluateFallingOutsideDropletAppearance(
        normalizedOffset,
        teardropProgress,
        dropletRadius,
        verticalScale,
        sourcePixelStep,
        dropletDepth);
    AccumulateOutsideDropletRefraction(
        normalizedOffset * float2(dropletRadius, dropletRadius * verticalScale),
        dropletDepth, lifecycle.y * headInside, refraction);
    if (lifecycle.w < 0.5f ||
        outsideDropletTrailLength <= 0.0f ||
        fallDistance <= 0.0f)
    {
        interiorDepth = dropletDepth * lifecycle.y * headInside;
        return dropletLighting * lifecycle.y * headInside;
    }

    float trailLength = saturate(outsideDropletTrailLength);
    float upperAnchorPixels =
        dropletRadius * verticalScale * smoothstep(
            0.05f,
            0.35f,
            teardropProgress) * 0.65f;
    float2 trailEndInputUv;
    if (deformWithSurface)
    {
        float2 trailEndRegionUv = headRegionUv - float2(
            0.0f,
            upperAnchorPixels / max(regionPixelSize.y, 1.0f));
        float trailEndValid;
        trailEndInputUv = MapRegionUvToInputUv(
            trailEndRegionUv,
            safeInputSize,
            regionPixelSize,
            isQuad,
            trailEndValid);
        headInside *= trailEndValid;
    }
    else
    {
        trailEndInputUv = headInputUv - float2(
            0.0f,
            upperAnchorPixels / safeInputSize.y);
    }
    float2 trailStartInputUv = lerp(
        trailEndInputUv,
        spawnInputUv,
        trailLength);
    float2 trailVector = (trailEndInputUv - trailStartInputUv) * safeInputSize;
    float trailLengthSquared = dot(trailVector, trailVector);
    if (trailLengthSquared <= 0.0001f)
    {
        interiorDepth = dropletDepth * lifecycle.y * headInside;
        return dropletLighting * lifecycle.y * headInside;
    }

    float trailProgress = saturate(dot(
        currentInputPixelPosition - trailStartInputUv * safeInputSize,
        trailVector) / trailLengthSquared);
    float taper = lerp(0.12f, 1.0f, smoothstep(0.0f, 1.0f, trailProgress));
    float trailRadius = max(dropletRadius * 0.20f, 0.50f) * taper;
    float trailFade = smoothstep(0.0f, 0.35f, trailProgress) *
        smoothstep(0.0f, 2.0f, sqrt(trailLengthSquared));
    float3 trailLighting;
    float trailDepth;
    if (deformWithSurface)
    {
        // 射影変換後も直線なので、分割カプセルの端を重ねず1本として描く。
        trailLighting = EvaluateMappedOutsideDropletTrailSegment(
            currentRegionPixelPosition,
            trailStartInputUv,
            trailEndInputUv,
            safeInputSize,
            regionPixelSize,
            trailRadius,
            trailProgress,
            isQuad,
            trailDepth);
    }
    else
    {
        trailLighting = EvaluateOutsideDropletCapsule(
            currentInputPixelPosition,
            trailStartInputUv * safeInputSize,
            trailEndInputUv * safeInputSize,
            trailRadius,
            1.0f,
            trailDepth);
    }

    trailLighting *= trailFade;
    interiorDepth = max(dropletDepth, trailDepth * trailFade * 0.42f) *
        lifecycle.y * headInside;
    return MergeOutsideDropletTrail(dropletLighting, trailLighting) *
        lifecycle.y * headInside;
}

float3 EvaluateFallingOutsideDropletEmitter(
    float2 currentRegionPixelPosition,
    float2 currentInputUv,
    float2 currentInputPixelPosition,
    float2 safeInputSize,
    float2 regionPixelSize,
    float cellSizePixels,
    float activationScale,
    uint layer,
    float selectionScale,
    float sizeSpeedScale,
    uint emitterIndex,
    bool isQuad,
    out float interiorDepth,
    inout float4 refraction)
{
    interiorDepth = 0.0f;
    uint emitterKey = GetOutsideDropletEmitterKey(
        (uint)outsideDropletSeed,
        layer,
        emitterIndex);
    uint cohort = emitterIndex / 6u;
    if (cohort > 0u)
    {
        // 既存channel 0..9を保ち、追加候補の端数倍率だけを選別する。
        float fraction = saturate(clamp(outsideDropletFallFrequency, 1.0f, 4.0f) - cohort);
        if (fraction <= 0.0f || OutsideDropletRandom(emitterKey, 10u) >= fraction)
            return float3(0.0f, 0.0f, 0.0f);
    }
    bool deformWithSurface =
        !isQuad || outsideDropletDeformWithSurface >= 0.5f;
    float activation = OutsideDropletRandom(emitterKey, 0u);
    if (activation > saturate(outsideDropletAmount * activationScale))
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    float selection = OutsideDropletRandom(emitterKey, 8u);
    if (selection > saturate(outsideDropletFallingRatio * selectionScale))
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    float4 lifecycle = EvaluateOutsideDropletLifecycle(
        emitterKey,
        sizeSpeedScale);
    if (lifecycle.w >= 1.5f || lifecycle.y <= 0.0f)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    uint cycleKey = GetOutsideDropletCycleKey(emitterKey, (uint)lifecycle.z);
    float2 spawnRegionUv = GetOutsideDropletSpawnRegionUv(cycleKey);
    float spawnValid;
    float2 spawnInputUv = MapRegionUvToInputUv(
        spawnRegionUv,
        safeInputSize,
        regionPixelSize,
        isQuad,
        spawnValid);
    float fallDistance = lifecycle.x * (regionPixelSize.y + cellSizePixels);
    float2 headInputUv = spawnInputUv +
        float2(0.0f, fallDistance / safeInputSize.y);
    float headValid;
    float2 headRegionUv = MapInputUvToRegionUv(
        headInputUv,
        safeInputSize,
        isQuad,
        headValid);
    float headInside = spawnValid * headValid * IsSourceInsideRegion(headRegionUv);
    if (headInside <= 0.0f)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    float dropletRadius = cellSizePixels * lerp(
        0.11f,
        0.21f,
        OutsideDropletRandom(cycleKey, 3u));
    float verticalScale = lerp(
        0.88f,
        1.18f,
        OutsideDropletRandom(cycleKey, 4u));
    if (lifecycle.w >= 0.5f)
    {
        verticalScale *= lerp(
            1.0f,
            2.20f,
            smoothstep(0.05f, 0.70f, lifecycle.x));
    }

    return RenderOutsideDropletHeadAndTrail(
        currentRegionPixelPosition, currentInputPixelPosition,
        safeInputSize, regionPixelSize, headInputUv, headRegionUv,
        spawnInputUv, dropletRadius, verticalScale, lifecycle,
        fallDistance, headInside, deformWithSurface, isQuad, interiorDepth, refraction);
}

// 合体ONでは、同じフレーム時刻から再現した頭部を共通の輪郭・水筋式へ渡す。
float3 EvaluateMergedOutsideDropletEmitter(
    float2 currentRegionPixelPosition,
    float2 currentInputPixelPosition,
    float2 safeInputSize,
    float2 regionPixelSize,
    uint emitterIndex,
    bool isQuad,
    out float interiorDepth,
    inout float4 refraction)
{
    interiorDepth = 0.0f;
    float4 head = outsideDropletMergeHeadsA[emitterIndex];
    float4 state = outsideDropletMergeHeadsB[emitterIndex];
    if (head.z <= 0.0f || state.z <= 0.0f)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    float headValid;
    float2 headRegionUv = MapInputUvToRegionUv(
        head.xy, safeInputSize, isQuad, headValid);
    float headInside = headValid * IsSourceInsideRegion(headRegionUv);
    if (headInside <= 0.0f)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }
    bool deformWithSurface = !isQuad || outsideDropletDeformWithSurface >= 0.5f;
    float fallDistance = max((head.y - state.y) * safeInputSize.y, 0.0f);
    float4 lifecycle = float4(state.w, state.z, 0.0f, state.w > 0.0f ? 1.0f : 0.0f);
    return RenderOutsideDropletHeadAndTrail(
        currentRegionPixelPosition, currentInputPixelPosition,
        safeInputSize, regionPixelSize, head.xy, headRegionUv,
        state.xy, head.z, head.w, lifecycle,
        fallDistance, headInside, deformWithSurface, isQuad, interiorDepth, refraction);
}

void EvaluateFallingOutsideDropletLayer(
    float2 currentRegionPixelPosition,
    float2 currentInputUv,
    float2 currentInputPixelPosition,
    float2 safeInputSize,
    float2 regionPixelSize,
    float cellSizePixels,
    float activationScale,
    uint layer,
    float selectionScale,
    float sizeSpeedScale,
    bool isQuad,
    inout float4 samples[75],
    uint firstSampleIndex,
    inout float4 refraction)
{
    int emitterCount = OutsideDropletBaseEmitterCount * (int)ceil(clamp(outsideDropletFallFrequency, 1.0f, 4.0f));
    [loop]
    for (int emitterIndex = 0;
         emitterIndex < emitterCount;
         emitterIndex++)
    {
        float interiorDepth;
        float3 lighting = EvaluateFallingOutsideDropletEmitter(
            currentRegionPixelPosition,
            currentInputUv,
            currentInputPixelPosition,
            safeInputSize,
            regionPixelSize,
            cellSizePixels,
            activationScale,
            layer,
            selectionScale,
            sizeSpeedScale,
            (uint)emitterIndex,
            isQuad,
            interiorDepth,
            refraction);
        uint sampleIndex = firstSampleIndex + ((uint)emitterIndex / 6u) * 18u + (uint)emitterIndex % 6u;
        samples[sampleIndex] = float4(lighting, interiorDepth);
    }
}

// 他の滴の深い内部に入った陰影だけを抑える。同じ深さなら外周を維持する。
// 可視性を深さへ反映し、吸収中や再形成中も抑制量を連続的に変える。
float3 CombineOutsideDropletSamples(float4 samples[75], int sampleCount)
{
    float maximumDepth = 0.0f;
    [loop]
    for (int depthIndex = 0; depthIndex < sampleCount; depthIndex++)
    {
        maximumDepth = max(maximumDepth, samples[depthIndex].w);
    }

    float3 lighting = float3(0.0f, 0.0f, 0.0f);
    [loop]
    for (int shadeIndex = 0; shadeIndex < sampleCount; shadeIndex++)
    {
        float4 sample = samples[shadeIndex];
        float visibleEdge = 1.0f - smoothstep(
            0.0f, 0.06f, maximumDepth - sample.w);
        lighting.xy = max(lighting.xy, sample.xy * visibleEdge);
        lighting.z = max(lighting.z, sample.z);
    }
    return lighting;
}

float3 GetOutsideDropletLighting(
    float2 regionPixelPosition,
    float2 inputUv,
    float2 inputPixelPosition,
    float2 safeInputSize,
    float2 regionPixelSize,
    bool isQuad,
    out float4 refraction)
{
    refraction = float4(0.0f, 0.0f, 0.0f, 0.0f);
    float resolutionScale = max(safeInputSize.y / 1080.0f, 0.25f);
    float sizeScale = resolutionScale * max(outsideDropletSizeScale, 0.25f);
    float useScreenSpace =
        isQuad && outsideDropletDeformWithSurface < 0.5f ? 1.0f : 0.0f;
    float2 staticPatternPosition = useScreenSpace >= 0.5f
        ? inputPixelPosition
        : regionPixelPosition;
    int headCount = 18 * (int)ceil(clamp(outsideDropletFallFrequency, 1.0f, 4.0f));
    float4 samples[75];
    [loop]
    for (int initializeIndex = 0; initializeIndex < 3 + headCount; initializeIndex++)
    {
        samples[initializeIndex] = float4(0.0f, 0.0f, 0.0f, 0.0f);
    }
    float interiorDepth;
    float3 smallDroplets = EvaluateOutsideDropletLayer(
        staticPatternPosition,
        20.0f * sizeScale,
        outsideDropletAmount,
        101u,
        1.0f,
        interiorDepth,
        refraction);
    samples[0] = float4(smallDroplets, interiorDepth);
    float3 mediumDroplets = EvaluateOutsideDropletLayer(
        staticPatternPosition,
        44.0f * sizeScale,
        outsideDropletAmount,
        307u,
        0.65f,
        interiorDepth,
        refraction);
    samples[1] = float4(mediumDroplets, interiorDepth);
    float3 largeDroplets = EvaluateOutsideDropletLayer(
        staticPatternPosition,
        92.0f * sizeScale,
        outsideDropletAmount,
        701u,
        0.25f,
        interiorDepth,
        refraction);
    samples[2] = float4(largeDroplets, interiorDepth);
    int sampleCount = 3;
    if (outsideDropletFallEnabled >= 0.5f && outsideDropletFallingRatio > 0.0f)
    {
        sampleCount = 3 + headCount;
        if (outsideDropletMergeEnabled >= 0.5f)
        {
            [loop]
            for (uint emitterIndex = 0u; emitterIndex < (uint)headCount; emitterIndex++)
            {
                float3 lighting = EvaluateMergedOutsideDropletEmitter(
                    regionPixelPosition, inputPixelPosition, safeInputSize,
                    regionPixelSize, emitterIndex, isQuad, interiorDepth, refraction);
                samples[3 + emitterIndex] = float4(lighting, interiorDepth);
            }
        }
        else
        {
            EvaluateFallingOutsideDropletLayer(
                regionPixelPosition,
                inputUv,
                inputPixelPosition,
                safeInputSize,
                regionPixelSize,
                20.0f * sizeScale,
                1.0f,
                101u,
                0.75f,
                0.9f,
                isQuad,
                samples,
                3u,
                refraction);
            EvaluateFallingOutsideDropletLayer(
                regionPixelPosition,
                inputUv,
                inputPixelPosition,
                safeInputSize,
                regionPixelSize,
                44.0f * sizeScale,
                0.65f,
                307u,
                1.0f,
                1.0f,
                isQuad,
                samples,
                9u,
                refraction);
            EvaluateFallingOutsideDropletLayer(
                regionPixelPosition,
                inputUv,
                inputPixelPosition,
                safeInputSize,
                regionPixelSize,
                92.0f * sizeScale,
                0.25f,
                701u,
                1.25f,
                1.15f,
                isQuad,
                samples,
                15u,
                refraction);
        }
    }
    return CombineOutsideDropletSamples(samples, sampleCount);
}







float4 main(
    float4 position : SV_POSITION,
    float4 scenePosition : SCENE_POSITION,
    float4 originalUv : TEXCOORD0,
    float4 blurredUv : TEXCOORD1,
    float4 maskUv : TEXCOORD2) : SV_Target
{
    if (debugView >= 14.5f && debugView < 15.5f)
    {
        return float4(0.25f, 0.5f, 0.75f, 1.0f);
    }

    float2 safeInputSize = max(inputSize, float2(1.0f, 1.0f));
    float2 inputPixelPosition = scenePosition.xy - inputOrigin;
    float2 inputUv = inputPixelPosition / safeInputSize;
    if (debugView >= 6.5f && debugView < 7.5f)
    {
        float3 diagnosticPhase = float3(
            GetDiagnosticCoordinatePhase(position.xy),
            GetDiagnosticCoordinatePhase(inputPixelPosition),
            GetDiagnosticCoordinatePhase(
                originalUv.xy * safeInputSize -
                inputPixelPosition));
        return float4(0.20f + diagnosticPhase * 0.80f, 1.0f);
    }

    if (debugView >= 10.5f && debugView < 23.5f)
    {
        bool diagnosticIsQuad = regionShape >= 2.5f;
        if (diagnosticIsQuad && quadValid < 0.5f)
        {
            return float4(1.0f, 0.0f, 1.0f, 1.0f);
        }

        float2 diagnosticRegionUv;
        if (diagnosticIsQuad)
        {
            float3 diagnosticInputPoint = float3(inputUv, 1.0f);
            float diagnosticNumeratorU =
                dot(quadInverseRow0, diagnosticInputPoint);
            float diagnosticNumeratorV =
                dot(quadInverseRow1, diagnosticInputPoint);
            float diagnosticDenominator =
                dot(quadInverseRow2, diagnosticInputPoint);
            if (abs(diagnosticDenominator) < 0.000001f)
            {
                return float4(1.0f, 1.0f, 0.0f, 1.0f);
            }

            diagnosticRegionUv = float2(
                diagnosticNumeratorU,
                diagnosticNumeratorV) / diagnosticDenominator;
        }
        else
        {
            float2 diagnosticSafeRegionSize = max(
                regionSize,
                float2(0.000001f, 0.000001f));
            float2 diagnosticRegionSizePixels =
                diagnosticSafeRegionSize * safeInputSize;
            float2 diagnosticRegionCenterPixels =
                regionCenter * safeInputSize;
            float2 diagnosticLocalPixelPosition = RotateToRegionLocal(
                inputPixelPosition - diagnosticRegionCenterPixels);
            diagnosticRegionUv =
                diagnosticLocalPixelPosition /
                diagnosticRegionSizePixels +
                0.5f;
        }

        float2 diagnosticRegionPixelSize = max(
            regionSize * safeInputSize,
            float2(1.0f, 1.0f));
        float2 diagnosticRegionPixelPosition =
            diagnosticRegionUv * diagnosticRegionPixelSize;
        float resolutionScale = max(
            safeInputSize.y / 1080.0f,
            0.25f);
        float sizeScale =
            resolutionScale *
            max(outsideDropletSizeScale, 0.25f);
        float useScreenSpace =
            diagnosticIsQuad &&
            outsideDropletDeformWithSurface < 0.5f
                ? 1.0f
                : 0.0f;
        float2 staticPatternPosition = useScreenSpace >= 0.5f
            ? inputPixelPosition
            : diagnosticRegionPixelPosition;
        if (debugView >= 18.5f && debugView < 19.5f)
        {
            float2 mediumOnlyCell = floor(
                staticPatternPosition /
                max(44.0f * sizeScale, 1.0f));
            uint mediumOnlyStripeIndex =
                (uint)floor(max(scenePosition.x, 0.0f) / 8.0f);
            uint mediumOnlyBitIndex = mediumOnlyStripeIndex & 31u;
            uint mediumOnlyHashInputStage =
                (uint)floor(max(scenePosition.y, 0.0f) / 64.0f) % 3u;
            float mediumOnlyLatticeDot =
                dot(mediumOnlyCell, float2(127.1f, 311.7f));
            float mediumOnlyHashInputStageValue =
                mediumOnlyHashInputStage == 0u
                    ? mediumOnlyLatticeDot
                    : mediumOnlyHashInputStage == 1u
                        ? mediumOnlyLatticeDot +
                            (outsideDropletSeed + 307.0f) * 74.7f
                        : mediumOnlyLatticeDot +
                            ((outsideDropletSeed + 307.0f) + 3.1f) * 74.7f;
            uint mediumOnlyHashInputStageBits =
                asuint(mediumOnlyHashInputStageValue);
            float mediumOnlyHashInputStageBitValue =
                (float)((mediumOnlyHashInputStageBits >> mediumOnlyBitIndex) & 1u);
            return float4(
                mediumOnlyHashInputStageBitValue,
                mediumOnlyHashInputStageBitValue,
                mediumOnlyHashInputStageBitValue,
                1.0f);
        }

        if (debugView >= 19.5f && debugView < 20.5f)
        {
            float2 mediumOnlyCell = floor(
                staticPatternPosition /
                max(44.0f * sizeScale, 1.0f));
            uint mediumOnlyStripeIndex =
                (uint)floor(max(scenePosition.x, 0.0f) / 8.0f);
            uint mediumOnlyBitIndex = mediumOnlyStripeIndex & 31u;
            float mediumOnlyHashInput =
                GetOutsideDropletHashInputWithoutSine(
                    mediumOnlyCell,
                    outsideDropletSeed + 307.0f,
                    3.1f);
            uint mediumOnlyHashInputBits = asuint(mediumOnlyHashInput);
            float mediumOnlyHashInputBitValue =
                (float)((mediumOnlyHashInputBits >> mediumOnlyBitIndex) & 1u);
            return float4(0.0f, mediumOnlyHashInputBitValue, 0.0f, 1.0f);
        }

        if (debugView >= 20.5f && debugView < 21.5f)
        {
            float2 mediumOnlyCell = floor(
                staticPatternPosition /
                max(44.0f * sizeScale, 1.0f));
            uint mediumOnlyCellXStripeIndex =
                (uint)floor(max(scenePosition.x, 0.0f) / 8.0f);
            uint mediumOnlyCellXBitIndex = mediumOnlyCellXStripeIndex & 31u;
            uint mediumOnlyCellXBits = asuint(mediumOnlyCell.x);
            float mediumOnlyCellXBitValue =
                (float)((mediumOnlyCellXBits >> mediumOnlyCellXBitIndex) & 1u);
            return float4(0.0f, mediumOnlyCellXBitValue, 0.0f, 1.0f);
        }

        if (debugView >= 21.5f && debugView < 22.5f)
        {
            float2 mediumOnlyCell = floor(
                staticPatternPosition /
                max(44.0f * sizeScale, 1.0f));
            uint mediumOnlyInlineStripeIndex =
                (uint)floor(max(scenePosition.x, 0.0f) / 8.0f);
            uint mediumOnlyInlineBitIndex = mediumOnlyInlineStripeIndex & 31u;
            float mediumOnlyInlineHashInput =
                dot(mediumOnlyCell, float2(127.1f, 311.7f)) +
                ((outsideDropletSeed + 307.0f) + 3.1f) * 74.7f;
            uint mediumOnlyInlineHashInputBits =
                asuint(mediumOnlyInlineHashInput);
            float mediumOnlyInlineHashInputBitValue =
                (float)((mediumOnlyInlineHashInputBits >> mediumOnlyInlineBitIndex) & 1u);
            return float4(0.0f, mediumOnlyInlineHashInputBitValue, 0.0f, 1.0f);
        }

        if (debugView >= 22.5f && debugView < 23.5f)
        {
            float2 mediumOnlyCell = floor(
                staticPatternPosition /
                max(44.0f * sizeScale, 1.0f));
            uint mediumOnlyCellYStripeIndex =
                (uint)floor(max(scenePosition.x, 0.0f) / 8.0f);
            uint mediumOnlyCellYBitIndex = mediumOnlyCellYStripeIndex & 31u;
            uint mediumOnlyCellYBits = asuint(mediumOnlyCell.y);
            float mediumOnlyCellYBitValue =
                (float)((mediumOnlyCellYBits >> mediumOnlyCellYBitIndex) & 1u);
            return float4(0.0f, mediumOnlyCellYBitValue, 0.0f, 1.0f);
        }

        if (debugView < 11.5f)
        {
            float3 noSineCellState = float3(
                GetOutsideDropletCellStateFingerprintWithoutSine(
                    staticPatternPosition,
                    20.0f * sizeScale,
                    outsideDropletSeed + 101.0f),
                GetOutsideDropletCellStateFingerprintWithoutSine(
                    staticPatternPosition,
                    44.0f * sizeScale,
                    outsideDropletSeed + 307.0f),
                GetOutsideDropletCellStateFingerprintWithoutSine(
                    staticPatternPosition,
                    92.0f * sizeScale,
                    outsideDropletSeed + 701.0f));
            return float4(noSineCellState, 1.0f);
        }

        float2 smallCell = floor(
            staticPatternPosition /
            max(20.0f * sizeScale, 1.0f));
        float2 mediumCell = floor(
            staticPatternPosition /
            max(44.0f * sizeScale, 1.0f));
        float2 largeCell = floor(
            staticPatternPosition /
            max(92.0f * sizeScale, 1.0f));
        uint stripeIndex =
            (uint)floor(max(scenePosition.x, 0.0f) / 8.0f);
        uint bitIndex = stripeIndex & 31u;
        if (debugView < 12.5f)
        {
            uint componentIndex = (stripeIndex >> 5) & 1u;
            uint3 cellBits = componentIndex == 0u
                ? uint3(
                    asuint(smallCell.x),
                    asuint(mediumCell.x),
                    asuint(largeCell.x))
                : uint3(
                    asuint(smallCell.y),
                    asuint(mediumCell.y),
                    asuint(largeCell.y));
            float3 bitValues = float3(
                (float)((cellBits.x >> bitIndex) & 1u),
                (float)((cellBits.y >> bitIndex) & 1u),
                (float)((cellBits.z >> bitIndex) & 1u));
            return float4(bitValues, 1.0f);
        }

        if (debugView < 13.5f)
        {
            uint hashOffsetIndex =
                (uint)floor(max(position.y, 0.0f) / 64.0f) % 5u;
            float hashOffset = hashOffsetIndex == 0u
                ? 3.1f
                : hashOffsetIndex == 1u
                    ? 11.7f
                    : hashOffsetIndex == 2u
                        ? 29.3f
                        : hashOffsetIndex == 3u
                            ? 47.9f
                            : 61.1f;
            float smallLayerSeed = outsideDropletSeed + 101.0f;
            float mediumLayerSeed = outsideDropletSeed + 307.0f;
            float largeLayerSeed = outsideDropletSeed + 701.0f;
            uint3 hashInputBits = asuint(float3(
                GetOutsideDropletHashInputWithoutSine(
                    smallCell,
                    smallLayerSeed,
                    hashOffset),
                GetOutsideDropletHashInputWithoutSine(
                    mediumCell,
                    mediumLayerSeed,
                    hashOffset),
                GetOutsideDropletHashInputWithoutSine(
                    largeCell,
                    largeLayerSeed,
                    hashOffset)));
            float3 hashInputBitValues = float3(
                (float)((hashInputBits.x >> bitIndex) & 1u),
                (float)((hashInputBits.y >> bitIndex) & 1u),
                (float)((hashInputBits.z >> bitIndex) & 1u));
            return float4(hashInputBitValues, 1.0f);
        }

        if (debugView < 14.5f)
        {
            uint3 finalFingerprintBits = uint3(
                GetOutsideDropletCellStateFingerprintBitsWithoutSine(
                    staticPatternPosition,
                    20.0f * sizeScale,
                    outsideDropletSeed + 101.0f),
                GetOutsideDropletCellStateFingerprintBitsWithoutSine(
                    staticPatternPosition,
                    44.0f * sizeScale,
                    outsideDropletSeed + 307.0f),
                GetOutsideDropletCellStateFingerprintBitsWithoutSine(
                    staticPatternPosition,
                    92.0f * sizeScale,
                    outsideDropletSeed + 701.0f));
            const uint finalFingerprintBitIndex = 0u;
            float3 finalFingerprintBitValues = float3(
                (float)((finalFingerprintBits.x >> finalFingerprintBitIndex) & 1u),
                (float)((finalFingerprintBits.y >> finalFingerprintBitIndex) & 1u),
                (float)((finalFingerprintBits.z >> finalFingerprintBitIndex) & 1u));
            return float4(finalFingerprintBitValues, 1.0f);
        }

        if (debugView < 16.5f)
        {
            uint mixedHashOffsetIndex =
                (uint)floor(max(scenePosition.y, 0.0f) / 64.0f) % 5u;
            float mixedHashOffset = mixedHashOffsetIndex == 0u
                ? 3.1f
                : mixedHashOffsetIndex == 1u
                    ? 11.7f
                    : mixedHashOffsetIndex == 2u
                        ? 29.3f
                        : mixedHashOffsetIndex == 3u
                            ? 47.9f
                            : 61.1f;
            uint3 mixedHashBits = uint3(
                MixOutsideDropletHashInputWithoutSine(
                    smallCell,
                    outsideDropletSeed + 101.0f,
                    mixedHashOffset),
                MixOutsideDropletHashInputWithoutSine(
                    mediumCell,
                    outsideDropletSeed + 307.0f,
                    mixedHashOffset),
                MixOutsideDropletHashInputWithoutSine(
                    largeCell,
                    outsideDropletSeed + 701.0f,
                    mixedHashOffset));
            float3 mixedHashBitValues = float3(
                (float)((mixedHashBits.x >> bitIndex) & 1u),
                (float)((mixedHashBits.y >> bitIndex) & 1u),
                (float)((mixedHashBits.z >> bitIndex) & 1u));
            return float4(mixedHashBitValues, 1.0f);
        }

        if (debugView < 17.5f)
        {
            uint3 preAvalancheHashBits = uint3(
                GetOutsideDropletCellStatePreAvalancheBitsWithoutSine(
                    staticPatternPosition,
                    20.0f * sizeScale,
                    outsideDropletSeed + 101.0f),
                GetOutsideDropletCellStatePreAvalancheBitsWithoutSine(
                    staticPatternPosition,
                    44.0f * sizeScale,
                    outsideDropletSeed + 307.0f),
                GetOutsideDropletCellStatePreAvalancheBitsWithoutSine(
                    staticPatternPosition,
                    92.0f * sizeScale,
                    outsideDropletSeed + 701.0f));
            float3 preAvalancheHashBitValues = float3(
                (float)((preAvalancheHashBits.x >> bitIndex) & 1u),
                (float)((preAvalancheHashBits.y >> bitIndex) & 1u),
                (float)((preAvalancheHashBits.z >> bitIndex) & 1u));
            return float4(preAvalancheHashBitValues, 1.0f);
        }

        if (debugView < 18.5f)
        {
            uint weightedHashTermIndex =
                (uint)floor(max(scenePosition.y, 0.0f) / 64.0f) % 5u;
            float weightedHashOffset = weightedHashTermIndex == 0u
                ? 3.1f
                : weightedHashTermIndex == 1u
                    ? 11.7f
                    : weightedHashTermIndex == 2u
                        ? 29.3f
                        : weightedHashTermIndex == 3u
                            ? 47.9f
                            : 61.1f;
            uint weightedHashMultiplier = weightedHashTermIndex == 0u
                ? 1u
                : weightedHashTermIndex == 1u
                    ? 0x9e3779b9u
                    : weightedHashTermIndex == 2u
                        ? 0x85ebca6bu
                        : weightedHashTermIndex == 3u
                            ? 0xc2b2ae35u
                            : 0x27d4eb2fu;
            uint3 weightedHashBits = uint3(
                MixOutsideDropletHashInputWithoutSine(
                    smallCell,
                    outsideDropletSeed + 101.0f,
                    weightedHashOffset),
                MixOutsideDropletHashInputWithoutSine(
                    mediumCell,
                    outsideDropletSeed + 307.0f,
                    weightedHashOffset),
                MixOutsideDropletHashInputWithoutSine(
                    largeCell,
                    outsideDropletSeed + 701.0f,
                    weightedHashOffset)) * weightedHashMultiplier;
            float3 weightedHashBitValues = float3(
                (float)((weightedHashBits.x >> bitIndex) & 1u),
                (float)((weightedHashBits.y >> bitIndex) & 1u),
                (float)((weightedHashBits.z >> bitIndex) & 1u));
            return float4(weightedHashBitValues, 1.0f);
        }

    }

    float4 original = OriginalTexture.Sample(OriginalSampler, originalUv.xy);

    bool isQuad = regionShape >= 2.5f;
    if (isQuad && quadValid < 0.5f)
    {
        if (debugView >= 5.5f && debugView < 6.5f)
        {
            return float4(0.0f, 0.0f, 0.0f, 1.0f);
        }

        if (debugView >= 4.5f)
        {
            return float4(original.a, 0.0f, 0.0f, original.a);
        }

        return original;
    }

    float2 regionUv;
    float regionMask;
    if (isQuad)
    {
        float3 inputPoint = float3(inputUv, 1.0f);
        float numeratorU = dot(quadInverseRow0, inputPoint);
        float numeratorV = dot(quadInverseRow1, inputPoint);
        float denominator = dot(quadInverseRow2, inputPoint);
        if (abs(denominator) < 0.000001f)
        {
            if (debugView >= 5.5f && debugView < 6.5f)
            {
                return float4(0.0f, 0.0f, 0.0f, 1.0f);
            }

            if (debugView >= 4.5f)
            {
                return float4(original.a, 0.0f, 0.0f, original.a);
            }

            return original;
        }

        regionUv = float2(numeratorU, numeratorV) / denominator;
        regionMask = GetQuadRegionMask(
            regionUv,
            numeratorU,
            numeratorV,
            denominator,
            safeInputSize);
    }
    else
    {
        float2 safeRegionSize = max(
            regionSize,
            float2(0.000001f, 0.000001f));
        float2 regionSizePixels = safeRegionSize * safeInputSize;
        float2 halfSizePixels = regionSizePixels * 0.5f;
        float2 regionCenterPixels = regionCenter * safeInputSize;
        float2 localPixelPosition = RotateToRegionLocal(
            inputPixelPosition - regionCenterPixels);
        regionUv = localPixelPosition / regionSizePixels + 0.5f;
        regionMask = GetRegionMask(localPixelPosition, halfSizePixels);
    }

    if (debugView >= 7.5f && debugView < 8.5f)
    {
        float2 diagnosticRegionPixelSize = max(
            regionSize * safeInputSize,
            float2(1.0f, 1.0f));
        float2 regionPixelDelta =
            regionUv * diagnosticRegionPixelSize -
            inputPixelPosition;
        if (any(regionPixelDelta != regionPixelDelta))
        {
            return float4(1.0f, 1.0f, 0.0f, 1.0f);
        }

        const float diagnosticAmplification = 512.0f;
        float2 amplifiedDelta =
            regionPixelDelta * diagnosticAmplification;
        float deltaMagnitude = max(
            abs(amplifiedDelta.x),
            abs(amplifiedDelta.y));
        if (deltaMagnitude >= 0.49f)
        {
            return float4(1.0f, 0.0f, 1.0f, 1.0f);
        }

        float2 signedDelta = 0.5f + amplifiedDelta;
        return float4(
            signedDelta,
            saturate(deltaMagnitude * 2.0f),
            1.0f);
    }

    if (debugView >= 9.5f && debugView < 10.5f)
    {
        uint seedBits = asuint(outsideDropletSeed);
        uint stripeIndex =
            (uint)floor(max(scenePosition.x, 0.0f) / 8.0f);
        uint bitIndex = stripeIndex & 31u;
        float bitValue = (float)((seedBits >> bitIndex) & 1u);
        return float4(bitValue, bitValue, bitValue, 1.0f);
    }

    if (debugView >= 8.5f && debugView < 9.5f)
    {
        float2 diagnosticRegionPixelSize = max(
            regionSize * safeInputSize,
            float2(1.0f, 1.0f));
        float2 diagnosticRegionPixelPosition =
            regionUv * diagnosticRegionPixelSize;
        float resolutionScale = max(
            safeInputSize.y / 1080.0f,
            0.25f);
        float sizeScale =
            resolutionScale *
            max(outsideDropletSizeScale, 0.25f);
        float useScreenSpace =
            isQuad && outsideDropletDeformWithSurface < 0.5f
                ? 1.0f
                : 0.0f;
        float2 staticPatternPosition = useScreenSpace >= 0.5f
            ? inputPixelPosition
            : diagnosticRegionPixelPosition;
        float3 cellStateFingerprint = float3(
            GetOutsideDropletCellStateFingerprint(
                staticPatternPosition,
                20.0f * sizeScale,
                outsideDropletSeed + 101.0f),
            GetOutsideDropletCellStateFingerprint(
                staticPatternPosition,
                44.0f * sizeScale,
                outsideDropletSeed + 307.0f),
            GetOutsideDropletCellStateFingerprint(
                staticPatternPosition,
                92.0f * sizeScale,
                outsideDropletSeed + 701.0f));
        return float4(cellStateFingerprint, 1.0f);
    }

    if (debugView >= 5.5f && debugView < 6.5f)
    {
        float coverage = 0.0f;
        if (outsideDropletAmount > 0.0f &&
            outsideDropletStrength > 0.0f &&
            regionMask > 0.0f)
        {
            float2 dropletRegionPixelSize = max(
                regionSize * safeInputSize,
                float2(1.0f, 1.0f));
            float4 refraction;
            float3 dropletLighting = GetOutsideDropletLighting(
                regionUv * dropletRegionPixelSize,
                inputUv,
                inputPixelPosition,
                safeInputSize,
                dropletRegionPixelSize,
                isQuad,
                refraction);
            coverage = saturate(
                dropletLighting.z *
                regionMask *
                outsideDropletStrength);
        }

        return float4(coverage, coverage, coverage, 1.0f);
    }

    // 外側水滴は元映像へ先に合成し、この出力をGaussianBlurへ渡す。
    // 拭き取りマスクは最終合成だけへ適用するため、水滴自体は拭かれない。
    if (outsideDropletRenderPass >= 0.5f)
    {
        float4 clearWithDroplets = original;
        if (outsideDropletAmount > 0.0f &&
            outsideDropletStrength > 0.0f &&
            regionMask > 0.0f)
        {
            float2 dropletRegionPixelSize = max(
                regionSize * safeInputSize,
                float2(1.0f, 1.0f));
            float4 refraction;
            float3 dropletLighting = GetOutsideDropletLighting(
                regionUv * dropletRegionPixelSize,
                inputUv,
                inputPixelPosition,
                safeInputSize,
                dropletRegionPixelSize,
                isQuad,
                refraction);
            float dropletInfluence = regionMask * saturate(outsideDropletStrength);
            float3 clearStraight = original.a > 0.000001f
                ? original.rgb / original.a
                : float3(0.0f, 0.0f, 0.0f);
            if (IsRealisticOutsideDroplet() && refraction.w > 0.00000001f && original.a > 0.000001f)
            {
                float2 offset = refraction.xy / refraction.w;
                float2 samplePosition = clamp(
                    inputPixelPosition + offset,
                    float2(0.5f, 0.5f), safeInputSize - 0.5f);
                // Direct2Dのzwは入力1ピクセルあたりのUV変位。部分描画や非ゼロ原点にも対応する。
                float2 sampleUv = originalUv.xy +
                    (samplePosition - inputPixelPosition) * originalUv.zw;
                float4 refracted = OriginalTexture.SampleLevel(OriginalSampler, sampleUv, 0);
                // 透明な参照色は混ぜず、出力のAlphaは常に元画素を維持する。
                float3 refractedStraight = refracted.a > 0.000001f
                    ? refracted.rgb / refracted.a
                    : clearStraight;
                clearStraight = lerp(clearStraight, refractedStraight,
                    saturate(refraction.z * dropletInfluence) * saturate(refracted.a));
            }
            clearStraight = CompositeOutsideDropletSurface(
                clearStraight,
                dropletLighting,
                dropletInfluence,
                0.16f,
                0.28f,
                0.22f);
            clearWithDroplets.rgb = clearStraight * original.a;
        }
        return clearWithDroplets;
    }

    // 水滴専用パスの終了後に判定し、その処理と診断表示を維持する。
    // originalは水滴有効時には水滴合成済み。Alphaもそのまま返す。
    if (debugView == 0.0f && fogAmount == 0.0f)
    {
        return original;
    }

    float4 blurred = BlurredTexture.Sample(BlurredSampler, blurredUv.xy);

    float sampledWipeMask = 0.0f;
    if (regionMask > 0.0f &&
        all(regionUv >= float2(0.0f, 0.0f)) &&
        all(regionUv <= float2(1.0f, 1.0f)))
    {
        sampledWipeMask = MaskTexture.SampleLevel(MaskSampler, regionUv, 0).a;
    }

    float effectiveWipeMask = sampledWipeMask;
    if (wipeResidue > 0.0f || wipeVariation > 0.0f)
    {
        float variationScale = 1.0f;
        if (wipeVariation > 0.0f)
        {
            float wipeNoiseValue = ValueNoise2D(regionUv * 48.0f, noiseSeed);
            variationScale = lerp(
                1.0f,
                0.65f + 0.35f * wipeNoiseValue,
                saturate(wipeVariation));
        }

        effectiveWipeMask = saturate(
            sampledWipeMask *
            (1.0f - saturate(wipeResidue)) *
            variationScale);
    }

    float wipeMask = effectiveWipeMask * regionMask;
    float3 blurStraight = blurred.a > 0.000001f
        ? blurred.rgb / blurred.a
        : float3(0.0f, 0.0f, 0.0f);
    float3 fogStraight = lerp(blurStraight, fogTint, saturate(tintMix));
    if (fogNoise > 0.0f)
    {
        float fogNoiseValue =
            ValueNoise2D(regionUv * 24.0f, noiseSeed + 17.0f) * 2.0f - 1.0f;
        fogStraight = saturate(
            fogStraight + fogNoiseValue * 0.12f * saturate(fogNoise));
    }

    float4 fogged = float4(fogStraight * original.a, original.a);
    if (debugView >= 0.5f && debugView < 1.5f)
    {
        return float4(
            float3(regionMask, regionMask, regionMask) * original.a,
            original.a);
    }

    if (debugView >= 1.5f && debugView < 2.5f)
    {
        return float4(
            float3(wipeMask, wipeMask, wipeMask) * original.a,
            original.a);
    }

    if (debugView >= 2.5f && debugView < 3.5f)
    {
        return float4(blurStraight * original.a, original.a);
    }

    if (debugView >= 3.5f && debugView < 4.5f)
    {
        float3 localUvColor = float3(
            saturate(regionUv.x),
            saturate(regionUv.y),
            0.0f);
        return float4(
            localUvColor * regionMask * original.a,
            original.a);
    }

    if (debugView >= 4.5f)
    {
        return isQuad
            ? float4(0.0f, original.a, 0.0f, original.a)
            : float4(0.0f, 0.0f, 0.0f, original.a);
    }

    float4 result = original;
    if (fogAmount > 0.0f)
    {
        float fogMask = saturate(
            regionMask * fogAmount * (1.0f - effectiveWipeMask));
        result = lerp(original, fogged, fogMask);
    }

    result.a = original.a;
    return result;
}
