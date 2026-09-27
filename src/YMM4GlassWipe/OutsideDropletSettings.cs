// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

/// <summary>
/// 外側水滴の保存値と描画値を安全な範囲へ正規化します。
/// </summary>
internal static class OutsideDropletSettings
{
    public const OutsideDropletMotionMode DefaultMotionMode = OutsideDropletMotionMode.Legacy;
    public const double MinimumSlipPercent = 25;
    public const double MaximumSlipPercent = 400;
    public const double DefaultSlipPercent = 100;
    public const double MinimumSupplyPercent = 0;
    public const double MaximumSupplyPercent = 400;
    public const double DefaultSupplyPercent = 100;

    public static OutsideDropletMotionMode SanitizeMotionMode(OutsideDropletMotionMode value) =>
        value is OutsideDropletMotionMode.Legacy or OutsideDropletMotionMode.SimplePhysics
            ? value
            : DefaultMotionMode;

    public static double SanitizeSlipPercent(double value) =>
        Sanitize(value, MinimumSlipPercent, MaximumSlipPercent, DefaultSlipPercent);

    public static double SanitizeSupplyPercent(double value) =>
        Sanitize(value, MinimumSupplyPercent, MaximumSupplyPercent, DefaultSupplyPercent);

    public static float SanitizeSlipScale(double value) =>
        (float)(SanitizeSlipPercent(value) / 100);

    public static float SanitizeSupplyScale(double value) =>
        (float)(SanitizeSupplyPercent(value) / 100);

    public const double MinimumAmountPercent = 0;
    public const double MaximumAmountPercent = 100;
    public const double DefaultAmountPercent = 0;

    public const double MinimumSizePercent = 25;
    public const double MaximumSizePercent = 400;
    public const double DefaultSizePercent = 100;

    public const double MinimumStrengthPercent = 0;
    public const double MaximumStrengthPercent = 100;
    public const double DefaultStrengthPercent = 50;

    public const double MinimumOutlineOpacityPercent = 0;
    public const double MaximumOutlineOpacityPercent = 100;
    public const double DefaultOutlineOpacityPercent = 100;

    public const double MinimumSeed = 0;
    public const double MaximumSeed = 9999;
    public const double DefaultSeed = 1;

    public const double MinimumFallingRatioPercent = 0;
    public const double MaximumFallingRatioPercent = 100;
    public const double DefaultFallingRatioPercent = 30;

    public const double MinimumFallSpeedPercent = 25;
    public const double MaximumFallSpeedPercent = 400;
    public const double DefaultFallSpeedPercent = 100;

    public const double MinimumFallFrequencyPercent = 100;
    public const double MaximumFallFrequencyPercent = 400;
    public const double DefaultFallFrequencyPercent = 100;

    public const double MinimumTrailLengthPercent = 0;
    public const double MaximumTrailLengthPercent = 100;
    public const double DefaultTrailLengthPercent = 35;

    public const bool DefaultMergeEnabled = false;

    public const bool DefaultRainEnabled = false;
    public const double MinimumRainStartSeconds = 0;
    public const double MaximumRainStartSeconds = 36_000;
    public const double DefaultRainStartSeconds = 0;
    public const double MinimumRainDurationSeconds = 0;
    public const double MaximumRainDurationSeconds = 36_000;
    public const double DefaultRainDurationSeconds = 5;

    public static OutsideDropletAppearance SanitizeAppearance(
        OutsideDropletAppearance value) =>
        OutsideDropletAppearanceCompatibility.Normalize(value);

    // 保存値を変更せず、CPUの合体計算とGPUが共有する実効値を解決する。
    public static bool ResolveDeformWithSurface(
        OutsideDropletAppearance appearance,
        bool savedValue) =>
        SanitizeAppearance(appearance) != OutsideDropletAppearance.Realistic && savedValue;

    public static float SanitizeAmount(double value) =>
        ToUnit(value, MinimumAmountPercent, MaximumAmountPercent, DefaultAmountPercent);

    public static float SanitizeSizeScale(double value) =>
        (float)(Sanitize(value, MinimumSizePercent, MaximumSizePercent, DefaultSizePercent) /
            100);

    public static float SanitizeStrength(double value) =>
        ToUnit(value, MinimumStrengthPercent, MaximumStrengthPercent, DefaultStrengthPercent);

    public static float SanitizeOutlineOpacity(double value) =>
        ToUnit(value, MinimumOutlineOpacityPercent, MaximumOutlineOpacityPercent, DefaultOutlineOpacityPercent);

    public static float SanitizeSeed(double value) =>
        (float)Math.Round(Sanitize(value, MinimumSeed, MaximumSeed, DefaultSeed));

    public static float SanitizeFallingRatio(double value) =>
        ToUnit(
            value,
            MinimumFallingRatioPercent,
            MaximumFallingRatioPercent,
            DefaultFallingRatioPercent);

    public static float SanitizeFallSpeedScale(double value) =>
        (float)(Sanitize(
            value,
            MinimumFallSpeedPercent,
            MaximumFallSpeedPercent,
            DefaultFallSpeedPercent) / 100);

    public static float SanitizeFallFrequencyScale(double value) =>
        (float)(Sanitize(
            value,
            MinimumFallFrequencyPercent,
            MaximumFallFrequencyPercent,
            DefaultFallFrequencyPercent) / 100);

    public static float SanitizeTrailLength(double value) =>
        ToUnit(
            value,
            MinimumTrailLengthPercent,
            MaximumTrailLengthPercent,
            DefaultTrailLengthPercent);

    public static double SanitizeAmountPercent(double value) =>
        Sanitize(value, MinimumAmountPercent, MaximumAmountPercent, DefaultAmountPercent);

    public static double SanitizeSizePercent(double value) =>
        Sanitize(value, MinimumSizePercent, MaximumSizePercent, DefaultSizePercent);

    public static double SanitizeStrengthPercent(double value) =>
        Sanitize(value, MinimumStrengthPercent, MaximumStrengthPercent, DefaultStrengthPercent);

    public static double SanitizeOutlineOpacityPercent(double value) =>
        Sanitize(value, MinimumOutlineOpacityPercent, MaximumOutlineOpacityPercent, DefaultOutlineOpacityPercent);

    public static double SanitizeSeedValue(double value) =>
        Math.Round(Sanitize(value, MinimumSeed, MaximumSeed, DefaultSeed));

    public static double SanitizeFallingRatioPercent(double value) =>
        Sanitize(
            value,
            MinimumFallingRatioPercent,
            MaximumFallingRatioPercent,
            DefaultFallingRatioPercent);

    public static double SanitizeFallSpeedPercent(double value) =>
        Sanitize(
            value,
            MinimumFallSpeedPercent,
            MaximumFallSpeedPercent,
            DefaultFallSpeedPercent);

    public static double SanitizeFallFrequencyPercent(double value) =>
        Sanitize(
            value,
            MinimumFallFrequencyPercent,
            MaximumFallFrequencyPercent,
            DefaultFallFrequencyPercent);

    public static double SanitizeTrailLengthPercent(double value) =>
        Sanitize(
            value,
            MinimumTrailLengthPercent,
            MaximumTrailLengthPercent,
            DefaultTrailLengthPercent);

    public static double SanitizeRainStartSeconds(double value) =>
        Sanitize(value, MinimumRainStartSeconds, MaximumRainStartSeconds, DefaultRainStartSeconds);

    public static double SanitizeRainDurationSeconds(double value) =>
        Sanitize(value, MinimumRainDurationSeconds, MaximumRainDurationSeconds, DefaultRainDurationSeconds);

    private static float ToUnit(
        double value,
        double minimum,
        double maximum,
        double fallback) =>
        (float)(Sanitize(value, minimum, maximum, fallback) / 100);

    private static double Sanitize(
        double value,
        double minimum,
        double maximum,
        double fallback) =>
        double.IsFinite(value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;
}
