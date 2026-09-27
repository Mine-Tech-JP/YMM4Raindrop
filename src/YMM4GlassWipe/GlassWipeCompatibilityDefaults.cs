// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

/// <summary>
/// 旧保存の欠落値とプリセットの寸法補完に使用する互換値です。
/// 新規追加・UIリセット用のGlassWipeDefaultSettingsとは独立して維持します。
/// </summary>
internal static class GlassWipeCompatibilityDefaults
{
    public const double BlurPixels = 15;
    public const double CircleDiameterPixels = 50;
    public const double EllipseWidthPixels = 25;
    public const double EllipseHeightPixels = 50;
    public const double RectangleWidthPixels = 50;
    public const double RectangleHeightPixels = 50;
    public const double PngBrushSizeScalePercent = 100;
}
