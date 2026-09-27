// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using YukkuriMovieMaker.Player.Video;

namespace YMM4GlassWipe;

internal readonly record struct GlassWipeParameters(
    float FogAmount,
    float Blur,
    float FogTintRed,
    float FogTintGreen,
    float FogTintBlue,
    float TintMix,
    GlassWipeRegionShape RegionShape,
    float RegionCenterX,
    float RegionCenterY,
    float RegionWidth,
    float RegionHeight,
    float RegionRotationCos,
    float RegionRotationSin,
    float RegionFeather,
    GlassWipeQuad Quad,
    GlassWipeQuadMapping QuadMapping,
    float WipeResidue,
    float WipeVariation,
    float FogNoise,
    float NoiseSeed,
    float OutsideDropletAmount,
    float OutsideDropletSizeScale,
    float OutsideDropletStrength,
    float OutsideDropletSeed,
    float OutsideDropletLocalTimeSeconds,
    float OutsideDropletFallEnabled,
    float OutsideDropletFallingRatio,
    float OutsideDropletFallSpeedScale,
    float OutsideDropletTrailLength,
    float OutsideDropletDeformWithSurface,
    float OutsideDropletAppearance,
    GlassWipeDebugView DebugView,
    float OutsideDropletMergeEnabled = 0,
    float OutsideDropletOutlineOpacity = 1f,
    float OutsideDropletRainEnabled = 0,
    float OutsideDropletRainStartSeconds = 0,
    float OutsideDropletRainDurationSeconds = 5,
    float OutsideDropletFallFrequency = 1f,
    OutsideDropletMotionMode OutsideDropletMotionMode = OutsideDropletMotionMode.Legacy,
    float OutsideDropletSlipScale = 1f,
    float OutsideDropletSupplyScale = 1f,
    long OutsideDropletFrame = 0,
    int OutsideDropletFps = 60)
{
    internal bool HasSameNonTemporalValues(GlassWipeParameters other) =>
        this with
        {
            OutsideDropletLocalTimeSeconds =
                other.OutsideDropletLocalTimeSeconds,
            OutsideDropletFrame = other.OutsideDropletFrame,
        } == other;

    // ぼかし量はGaussianBlur側で反映し、合成定数の更新判定から除く。
    internal bool HasSameCompositeNonTemporalValues(GlassWipeParameters other) =>
        (this with { Blur = other.Blur }).HasSameNonTemporalValues(other);

    // 水滴前段で参照しない曇り専用値を除き、領域と水滴の変更は検出する。
    internal bool HasSamePreFogNonTemporalValues(GlassWipeParameters other) =>
        (this with
        {
            FogAmount = other.FogAmount,
            FogTintRed = other.FogTintRed,
            FogTintGreen = other.FogTintGreen,
            FogTintBlue = other.FogTintBlue,
            TintMix = other.TintMix,
            WipeResidue = other.WipeResidue,
            WipeVariation = other.WipeVariation,
            FogNoise = other.FogNoise,
            NoiseSeed = other.NoiseSeed,
        }).HasSameCompositeNonTemporalValues(other);

    internal static float ResolveOutsideDropletLocalTimeSeconds(
        bool fallEnabled,
        int frame,
        int fps) =>
        fallEnabled
            ? OutsideDropletMotion.GetLocalTimeSeconds(frame, fps)
            : 0;

    internal static float ResolveOutsideDropletLocalTimeSeconds(bool fallEnabled, bool rainEnabled, int frame, int fps) =>
        fallEnabled || rainEnabled ? OutsideDropletMotion.GetLocalTimeSeconds(frame, fps) : 0;

    public static GlassWipeParameters Create(
        GlassWipeVideoEffect item,
        EffectDescription effectDescription)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(effectDescription);

        var frame = effectDescription.ItemPosition.Frame;
        var length = effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS;
        var tint = item.FogTint;
        var debugView = GlassWipeParameterSanitizer.SanitizeDebugView(
            item.DebugView);
        var regionShape = GlassWipeParameterSanitizer.SanitizeRegionShape(
            item.RegionShape);
        var regionCenterX = GlassWipeParameterSanitizer.ToUnit(
            Evaluate(item.RegionCenterX, frame, length, fps));
        var regionCenterY = GlassWipeParameterSanitizer.ToUnit(
            Evaluate(item.RegionCenterY, frame, length, fps));
        var regionWidth = GlassWipeParameterSanitizer.ToUnit(
            Evaluate(item.RegionWidth, frame, length, fps));
        var regionHeight = GlassWipeParameterSanitizer.ToUnit(
            Evaluate(item.RegionHeight, frame, length, fps));
        var regionRotationDegrees = GlassWipeParameterSanitizer.Clamp(
            Evaluate(item.RegionRotation, frame, length, fps),
            -180,
            180);
        var regionRotationRadians = regionRotationDegrees * MathF.PI / 180;
        var regionRotationCos = MathF.Cos(regionRotationRadians);
        var regionRotationSin = MathF.Sin(regionRotationRadians);
        var regionFeather = GlassWipeParameterSanitizer.Clamp(
            item.RegionFeather,
            0,
            1000);

        var quadPreset = GlassWipeParameterSanitizer.SanitizeQuadPreset(
            item.QuadPreset);
        var quad = new GlassWipeQuad(
            new Vector2(
                GlassWipeParameterSanitizer.ToQuadUnit(
                    Evaluate(item.QuadTopLeftX, frame, length, fps)),
                GlassWipeParameterSanitizer.ToQuadUnit(
                    Evaluate(item.QuadTopLeftY, frame, length, fps))),
            new Vector2(
                GlassWipeParameterSanitizer.ToQuadUnit(
                    Evaluate(item.QuadTopRightX, frame, length, fps)),
                GlassWipeParameterSanitizer.ToQuadUnit(
                    Evaluate(item.QuadTopRightY, frame, length, fps))),
            new Vector2(
                GlassWipeParameterSanitizer.ToQuadUnit(
                    Evaluate(item.QuadBottomRightX, frame, length, fps)),
                GlassWipeParameterSanitizer.ToQuadUnit(
                    Evaluate(item.QuadBottomRightY, frame, length, fps))),
            new Vector2(
                GlassWipeParameterSanitizer.ToQuadUnit(
                    Evaluate(item.QuadBottomLeftX, frame, length, fps)),
                GlassWipeParameterSanitizer.ToQuadUnit(
                    Evaluate(item.QuadBottomLeftY, frame, length, fps))))
            .ResolvePreset(quadPreset);
        var quadMapping = quad.TryCreateMapping(out var validQuadMapping)
            ? validQuadMapping
            : GlassWipeQuadMapping.Invalid;
        if (regionShape == GlassWipeRegionShape.FullScreen)
        {
            regionCenterX = 0.5f;
            regionCenterY = 0.5f;
            regionWidth = 1;
            regionHeight = 1;
            regionRotationCos = 1;
            regionRotationSin = 0;
            regionFeather = 0;
        }

        return new GlassWipeParameters(
            GlassWipeParameterSanitizer.ToUnit(
                Evaluate(item.FogAmount, frame, length, fps)),
            GlassWipeParameterSanitizer.Clamp(
                Evaluate(item.Blur, frame, length, fps),
                0,
                50),
            tint.R / 255f,
            tint.G / 255f,
            tint.B / 255f,
            GlassWipeParameterSanitizer.ToUnit(item.TintMix),
            regionShape,
            regionCenterX,
            regionCenterY,
            regionWidth,
            regionHeight,
            regionRotationCos,
            regionRotationSin,
            regionFeather,
            quad,
            quadMapping,
            GlassWipeParameterSanitizer.ToUnit(item.WipeResidue),
            GlassWipeParameterSanitizer.ToUnit(item.WipeVariation),
            GlassWipeParameterSanitizer.ToUnit(item.FogNoise),
            MathF.Round(GlassWipeParameterSanitizer.Clamp(item.JitterSeed, 0, 9999)),
            OutsideDropletSettings.SanitizeAmount(item.OutsideDropletAmount),
            OutsideDropletSettings.SanitizeSizeScale(item.OutsideDropletSize),
            OutsideDropletSettings.SanitizeStrength(item.OutsideDropletStrength),
            OutsideDropletSettings.SanitizeSeed(item.OutsideDropletSeed),
            ResolveOutsideDropletLocalTimeSeconds(
                item.OutsideDropletFallEnabled,
                item.OutsideDropletRainEnabled,
                frame,
                fps),
            item.OutsideDropletFallEnabled ? 1 : 0,
            OutsideDropletSettings.SanitizeFallingRatio(
                item.OutsideDropletFallingRatio),
            OutsideDropletSettings.SanitizeFallSpeedScale(
                item.OutsideDropletFallSpeed),
            OutsideDropletSettings.SanitizeTrailLength(
                item.OutsideDropletTrailLength),
            OutsideDropletSettings.SanitizeMotionMode(item.OutsideDropletMotionMode) == OutsideDropletMotionMode.Legacy &&
            OutsideDropletSettings.ResolveDeformWithSurface(
                item.OutsideDropletAppearance, item.OutsideDropletDeformWithSurface) ? 1 : 0,
            (float)OutsideDropletSettings.SanitizeAppearance(
                item.OutsideDropletAppearance),
            debugView,
            item.OutsideDropletMergeEnabled ? 1 : 0,
            OutsideDropletSettings.SanitizeOutlineOpacity(item.OutsideDropletOutlineOpacity),
            item.OutsideDropletRainEnabled ? 1 : 0,
            (float)OutsideDropletSettings.SanitizeRainStartSeconds(item.OutsideDropletRainStartSeconds),
            (float)OutsideDropletSettings.SanitizeRainDurationSeconds(item.OutsideDropletRainDurationSeconds),
            OutsideDropletSettings.SanitizeFallFrequencyScale(item.OutsideDropletFallFrequency),
            OutsideDropletSettings.SanitizeMotionMode(item.OutsideDropletMotionMode),
            OutsideDropletSettings.SanitizeSlipScale(item.OutsideDropletSlip),
            OutsideDropletSettings.SanitizeSupplyScale(item.OutsideDropletSupply),
            Math.Max(frame, 0),
            Math.Max(fps, 1));
    }

    private static double Evaluate(
        YukkuriMovieMaker.Commons.Animation animation,
        int frame,
        int length,
        int fps)
    {
        var value = animation.GetValue(frame, length, fps);
        return double.IsFinite(value) ? value : 0;
    }
}
