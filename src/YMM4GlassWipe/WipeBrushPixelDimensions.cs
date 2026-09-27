// SPDX-License-Identifier: MPL-2.0

using YukkuriMovieMaker.Commons;

namespace YMM4GlassWipe;

internal readonly record struct WipeBrushPixelDimensions(float Width, float Height)
{
    public const float Maximum = 8192;

    public bool IsDrawable => Width > 0 && Height > 0;

    public WipePathSample ApplyTo(WipePathSample sample) =>
        sample with
        {
            BrushWidthPixels = Width,
            BrushHeightPixels = Height,
        };

    public static WipeBrushPixelDimensions Resolve(
        GlassWipeVideoEffect item,
        int frame,
        int itemLength,
        int framesPerSecond,
        int sourceItemLength = 0)
    {
        ArgumentNullException.ThrowIfNull(item);
        var shape = GlassWipeParameterSanitizer.SanitizeBrushShape(
            item.BrushShape);
        return shape switch
        {
            GlassWipeBrushShape.Circle => Square(Evaluate(
                item.CircleDiameterPixels,
                frame,
                itemLength,
                framesPerSecond,
                sourceItemLength)),
            GlassWipeBrushShape.Ellipse => new(
                Evaluate(
                    item.EllipseWidthPixels,
                    frame,
                    itemLength,
                    framesPerSecond,
                    sourceItemLength),
                Evaluate(
                    item.EllipseHeightPixels,
                    frame,
                    itemLength,
                    framesPerSecond,
                    sourceItemLength)),
            GlassWipeBrushShape.Rectangle => new(
                Evaluate(
                    item.RectangleWidthPixels,
                    frame,
                    itemLength,
                    framesPerSecond,
                    sourceItemLength),
                Evaluate(
                    item.RectangleHeightPixels,
                    frame,
                    itemLength,
                    framesPerSecond,
                    sourceItemLength)),
            GlassWipeBrushShape.Hand or GlassWipeBrushShape.ShoePrint =>
                Scale(
                    WipeBrushMaskRasterizer.GetNativePixelDimensions(shape),
                    EvaluatePngScale(
                        item.PngBrushSizeScale,
                        frame,
                        itemLength,
                        framesPerSecond,
                        sourceItemLength)),
            GlassWipeBrushShape.UserImage => Scale(
                new(
                    Sanitize(item.UserBrushPixelWidth),
                    Sanitize(item.UserBrushPixelHeight)),
                EvaluatePngScale(
                    item.PngBrushSizeScale,
                    frame,
                    itemLength,
                    framesPerSecond,
                    sourceItemLength)),
            _ => Square((float)GlassWipeCompatibilityDefaults.CircleDiameterPixels),
        };
    }

    private static WipeBrushPixelDimensions Square(float size) =>
        new(size, size);

    private static WipeBrushPixelDimensions Scale(
        WipeBrushPixelDimensions dimensions,
        float scale) =>
        new(
            Sanitize(dimensions.Width * scale),
            Sanitize(dimensions.Height * scale));

    private static float Evaluate(
        Animation animation,
        int frame,
        int itemLength,
        int framesPerSecond,
        int sourceItemLength) =>
        Sanitize(WipeAnimationEvaluation.GetValue(animation,
            frame,
            Math.Max(0, itemLength),
            Math.Max(1, framesPerSecond),
            sourceItemLength));

    private static float EvaluatePngScale(
        Animation animation,
        int frame,
        int itemLength,
        int framesPerSecond,
        int sourceItemLength)
    {
        var value = WipeAnimationEvaluation.GetValue(animation,
            frame,
            Math.Max(0, itemLength),
            Math.Max(1, framesPerSecond),
            sourceItemLength);
        var percent = double.IsFinite(value)
            ? Math.Clamp(value, 25, 400)
            : GlassWipeCompatibilityDefaults.PngBrushSizeScalePercent;
        return (float)(percent / 100);
    }

    private static float Sanitize(double value) =>
        double.IsFinite(value)
            ? (float)Math.Clamp(value, 0, Maximum)
            : 0;
}
