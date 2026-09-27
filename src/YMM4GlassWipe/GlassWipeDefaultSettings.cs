// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

/// <summary>
/// 添付テンプレートに合わせた新規エフェクトとUIリセット用の初期値です。
/// 既存データとプリセットの補完には、GlassWipeCompatibilityDefaultsおよび各設定クラスの従来値を使用します。
/// </summary>
internal static class GlassWipeDefaultSettings
{
    public const GlassWipePathInterpolation SimpleInterpolation = GlassWipePathInterpolation.CircularArc;
    public const GlassWipeSimplePattern SimplePattern = GlassWipeSimplePattern.OffsetRoundTrips;
    public const double PathStartPercent = 5;
    public const double PathCompletionPercent = 20;
    public const WipePathTimingMode PathTimingMode = WipePathTimingMode.Seconds;
    public const double PathStartSeconds = 0.5;
    public const double PathCompletionSeconds = 3;
    public const double SimpleStartX = 15;
    public const double SimpleStartY = 50;
    public const double SimpleControlY = 30;
    public const double SimpleEndX = 75;
    public const double SimpleEndY = 30;
    public const double SimpleRoundTrips = 3.5;
    public const double SimpleWipeAmountPerPassPercent = 100;
    public const double SimpleReturnOffsetPercent = 10;
    public const double SimpleReturnOffsetXPercent = 3;

    public const double FogAmountPercent = 90;
    public const double BlurPixels = 10;
    public const double FogNoisePercent = 10;
    public const double TintMixPercent = 10;

    public const double OutsideDropletAmountPercent = 50;
    public const double OutsideDropletSizePercent = 100;
    public const double OutsideDropletStrengthPercent = 100;
    public const OutsideDropletAppearance OutsideDropletAppearance = YMM4GlassWipe.OutsideDropletAppearance.Realistic;
    public const OutsideDropletMotionMode OutsideDropletMotionMode = YMM4GlassWipe.OutsideDropletMotionMode.SimplePhysics;
    public const double OutsideDropletSupplyPercent = 200;
    public const double OutsideDropletSeed = 0;
    public const double OutsideDropletOutlineOpacityPercent = 30;
    public const bool OutsideDropletFallEnabled = true;
    public const bool OutsideDropletMergeEnabled = true;
    public const bool OutsideDropletRainEnabled = true;
    public const double OutsideDropletRainStartSeconds = 1;
    public const double OutsideDropletRainDurationSeconds = 10;
    public const double OutsideDropletFallingRatioPercent = 100;
    public const double OutsideDropletFallFrequencyPercent = 200;
    public const double OutsideDropletFallSpeedPercent = 100;
    public const double OutsideDropletTrailLengthPercent = 100;

    public const GlassWipeRegionShape RegionShape = GlassWipeRegionShape.FullScreen;
    public const double RegionFeatherPixels = 10;
    public const GlassWipeBrushShape BrushShape = GlassWipeBrushShape.Hand;
    public const bool ContinuousWipe = true;
    public const double CircleDiameterPixels = 500;
    public const double EllipseWidthPixels = 25;
    public const double EllipseHeightPixels = 50;
    public const double RectangleWidthPixels = 100;
    public const double RectangleHeightPixels = 620;
    public const double PngBrushSizeScalePercent = 50;
    public const bool BrushMirror = true;
    public const double SimpleGeneratedBrushRotationDegrees = -30;
    public const double SimpleGeneratedBrushRotationFollowPercent = 100;
}
