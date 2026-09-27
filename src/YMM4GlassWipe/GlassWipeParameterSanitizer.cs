// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

internal static class GlassWipeParameterSanitizer
{
    internal const double MinimumQuadCoordinatePercent = -100;
    internal const double MaximumQuadCoordinatePercent = 200;

    public static float ToUnit(double percent) =>
        Clamp(percent, 0, 100) / 100f;

    public static float ToQuadUnit(double percent) =>
        Clamp(
            percent,
            MinimumQuadCoordinatePercent,
            MaximumQuadCoordinatePercent) / 100f;

    public static float Clamp(double value, double minimum, double maximum)
    {
        if (!double.IsFinite(value))
        {
            return (float)minimum;
        }

        return (float)Math.Clamp(value, minimum, maximum);
    }

    public static GlassWipeDebugView SanitizeDebugView(
        GlassWipeDebugView debugView) =>
        Enum.IsDefined(debugView)
            ? debugView
            : GlassWipeDebugView.Final;

    public static GlassWipeRegionShape SanitizeRegionShape(
        GlassWipeRegionShape regionShape) =>
        Enum.IsDefined(regionShape)
            ? regionShape
            : GlassWipeRegionShape.Rectangle;

    public static GlassWipeQuality SanitizeQuality(
        GlassWipeQuality quality) =>
        Enum.IsDefined(quality)
            ? quality
            : GlassWipeQuality.Standard;

    public static GlassWipeBrushShape SanitizeBrushShape(
        GlassWipeBrushShape shape) =>
        Enum.IsDefined(shape)
            ? shape
            : GlassWipeBrushShape.Circle;

    public static GlassWipeQuadPreset SanitizeQuadPreset(
        GlassWipeQuadPreset preset) =>
        Enum.IsDefined(preset)
            ? preset
            : GlassWipeQuadPreset.Custom;
}
