// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

internal static class GlassWipeSimpleVisibility
{
    internal const string WipeAmountDisplayName = "拭き取り量";
    internal const string ReturnOffsetXDisplayName = "ずれX";
    internal const string ReturnOffsetYDisplayName = "ずれY";

    public static bool IsSimpleMode(GlassWipeEditingMode editingMode) =>
        editingMode == GlassWipeEditingMode.Simple;

    public static bool IsDetailedMode(GlassWipeEditingMode editingMode) =>
        editingMode == GlassWipeEditingMode.Detailed;

    public static bool AreSimpleGeneratedBrushSettingsVisible(
        GlassWipeEditingMode editingMode,
        WipePathInputMode pathInputMode) =>
        AreSimpleGeneratedPathSettingsVisible(editingMode, pathInputMode);

    public static bool AreSimpleGeneratedPathSettingsVisible(
        GlassWipeEditingMode editingMode,
        WipePathInputMode pathInputMode) =>
        editingMode == GlassWipeEditingMode.Simple ||
        editingMode == GlassWipeEditingMode.Detailed &&
        pathInputMode == WipePathInputMode.SimpleGenerated;

    public static bool IsBrushMirrorVisible(GlassWipeBrushShape shape) =>
        shape is GlassWipeBrushShape.Hand or
            GlassWipeBrushShape.ShoePrint or
            GlassWipeBrushShape.UserImage;

    public static bool IsContinuousWipeVisible(GlassWipeBrushShape shape) =>
        shape is GlassWipeBrushShape.Hand or
            GlassWipeBrushShape.ShoePrint or
            GlassWipeBrushShape.UserImage;

    public static bool IsUserBrushVisible(GlassWipeBrushShape shape) =>
        shape == GlassWipeBrushShape.UserImage;

    public static bool IsPngBrushSizeVisible(GlassWipeBrushShape shape) =>
        shape is GlassWipeBrushShape.Hand or
            GlassWipeBrushShape.ShoePrint or
            GlassWipeBrushShape.UserImage;

    public static bool IsCircleBrushSizeVisible(GlassWipeBrushShape shape) =>
        shape == GlassWipeBrushShape.Circle;

    public static bool IsEllipseBrushSizeVisible(GlassWipeBrushShape shape) =>
        shape == GlassWipeBrushShape.Ellipse;

    public static bool IsRectangleBrushSizeVisible(GlassWipeBrushShape shape) =>
        shape == GlassWipeBrushShape.Rectangle;

    public static bool IsInterpolationVisible(
        GlassWipeEditingMode editingMode,
        WipePathInputMode pathInputMode,
        GlassWipeSimplePattern pattern)
    {
        return AreSimpleGeneratedPathSettingsVisible(editingMode, pathInputMode) &&
            pattern is
                GlassWipeSimplePattern.GentleArcOnce or
                GlassWipeSimplePattern.ArcRoundTrips or
                GlassWipeSimplePattern.OffsetRoundTrips;
    }

    public static bool IsControlPointVisible(
        GlassWipeEditingMode editingMode,
        WipePathInputMode pathInputMode,
        GlassWipeSimplePattern pattern,
        GlassWipePathInterpolation interpolation)
    {
        if (!AreSimpleGeneratedPathSettingsVisible(editingMode, pathInputMode))
        {
            return false;
        }

        return pattern is
            GlassWipeSimplePattern.GentleArcOnce or
            GlassWipeSimplePattern.ArcRoundTrips ||
            pattern == GlassWipeSimplePattern.OffsetRoundTrips &&
            interpolation is
                GlassWipePathInterpolation.Smooth or
                GlassWipePathInterpolation.CircularArc;
    }

    public static bool AreRoundTripSettingsVisible(
        GlassWipeEditingMode editingMode,
        WipePathInputMode pathInputMode,
        GlassWipeSimplePattern pattern)
    {
        return AreSimpleGeneratedPathSettingsVisible(editingMode, pathInputMode) &&
            pattern is
                GlassWipeSimplePattern.ShortRoundTrips or
                GlassWipeSimplePattern.ArcRoundTrips or
                GlassWipeSimplePattern.OffsetRoundTrips;
    }

    public static bool IsReturnOffsetVisible(
        GlassWipeEditingMode editingMode,
        WipePathInputMode pathInputMode,
        GlassWipeSimplePattern pattern)
    {
        return AreSimpleGeneratedPathSettingsVisible(editingMode, pathInputMode) &&
            pattern == GlassWipeSimplePattern.OffsetRoundTrips;
    }

    public static bool IsRegionTransformVisible(
        GlassWipeEditingMode editingMode,
        GlassWipeRegionShape regionShape)
    {
        if (!IsSupportedEditingMode(editingMode))
        {
            return false;
        }

        return GlassWipeParameterSanitizer.SanitizeRegionShape(regionShape) is
                GlassWipeRegionShape.Rectangle or
                GlassWipeRegionShape.Ellipse;
    }

    public static bool IsQuadConfigurationVisible(
        GlassWipeEditingMode editingMode,
        GlassWipeRegionShape regionShape)
    {
        return IsSupportedEditingMode(editingMode) &&
            GlassWipeParameterSanitizer.SanitizeRegionShape(regionShape) ==
                GlassWipeRegionShape.Quad;
    }

    public static bool AreQuadPointsVisible(
        GlassWipeEditingMode editingMode,
        GlassWipeRegionShape regionShape,
        GlassWipeQuadPreset quadPreset)
    {
        return IsQuadConfigurationVisible(editingMode, regionShape) &&
            GlassWipeParameterSanitizer.SanitizeQuadPreset(quadPreset) ==
                GlassWipeQuadPreset.Custom;
    }

    private static bool IsSupportedEditingMode(GlassWipeEditingMode editingMode) =>
        editingMode is GlassWipeEditingMode.Simple or GlassWipeEditingMode.Detailed;
}
