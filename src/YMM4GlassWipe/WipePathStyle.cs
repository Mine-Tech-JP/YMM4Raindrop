// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

internal readonly record struct WipePathStyle(
    float Smoothing,
    float Jitter,
    int Seed,
    float RotationFollow = 0,
    float Softness = 0,
    GlassWipeQuality Quality = GlassWipeQuality.Standard,
    WipeMaskAccumulationMode AccumulationMode = WipeMaskAccumulationMode.SourceOver,
    GlassWipeBrushShape BrushShape = GlassWipeBrushShape.Circle,
    float InitialTangentX = 0,
    float InitialTangentY = 0,
    bool BrushMirror = false,
    Guid UserBrushId = default,
    long UserBrushRevision = 0,
    bool ContinuousWipe = false,
    int UserBrushPixelWidth = 0,
    int UserBrushPixelHeight = 0)
{
    public WipePathStyle Sanitize()
    {
        var shape = GlassWipeParameterSanitizer.SanitizeBrushShape(BrushShape);
        return new(
            ClampFinite(Smoothing, 0, 1),
            ClampFinite(Jitter, 0, 0.05f),
            Math.Clamp(Seed, 0, 9999),
            ClampFinite(RotationFollow, 0, 1),
            ClampFinite(Softness, 0, 1),
            GlassWipeParameterSanitizer.SanitizeQuality(Quality),
            AccumulationMode == WipeMaskAccumulationMode.PerPassMaximum
                ? WipeMaskAccumulationMode.PerPassMaximum
                : WipeMaskAccumulationMode.SourceOver,
            shape,
            SanitizeTangent(InitialTangentX),
            SanitizeTangent(InitialTangentY),
            BrushMirror,
            shape == GlassWipeBrushShape.UserImage
                ? UserBrushId
                : Guid.Empty,
            shape == GlassWipeBrushShape.UserImage
                ? Math.Max(0, UserBrushRevision)
                : 0,
            ContinuousWipe && shape is
                GlassWipeBrushShape.Hand or
                GlassWipeBrushShape.ShoePrint or
                GlassWipeBrushShape.UserImage,
            shape == GlassWipeBrushShape.UserImage
                ? Math.Clamp(UserBrushPixelWidth, 0, UserBrushLibrary.MaximumDimension)
                : 0,
            shape == GlassWipeBrushShape.UserImage
                ? Math.Clamp(UserBrushPixelHeight, 0, UserBrushLibrary.MaximumDimension)
                : 0);
    }

    public bool IsDisabled =>
        Smoothing <= 0 &&
        Jitter <= 0 &&
        RotationFollow <= 0 &&
        Softness <= 0 &&
        Quality == GlassWipeQuality.Standard &&
        AccumulationMode == WipeMaskAccumulationMode.SourceOver &&
        BrushShape == GlassWipeBrushShape.Circle &&
        !ContinuousWipe;

    public float SpacingRatio => Quality switch
    {
        GlassWipeQuality.Low => 0.35f,
        GlassWipeQuality.High => 0.125f,
        _ => WipeBrushStampGenerator.SpacingRatio,
    };

    private static float ClampFinite(
        float value,
        float minimum,
        float maximum) =>
        float.IsFinite(value)
            ? Math.Clamp(value, minimum, maximum)
            : minimum;

    private static float SanitizeTangent(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, -3, 3) : 0;
}

internal enum WipeMaskAccumulationMode
{
    SourceOver = 0,
    PerPassMaximum = 1,
}
