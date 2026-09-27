// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

internal enum DetailedWipeBuiltInPreset
{
    Heart,
    LoveUmbrella,
    Smiley,
}

internal static class DetailedWipePresetFactory
{
    public static DetailedWipePresetState Create(DetailedWipeBuiltInPreset preset)
    {
        return preset switch
        {
            DetailedWipeBuiltInPreset.Heart => CreateState(CreateHeart(), 86, 72, 36),
            DetailedWipeBuiltInPreset.LoveUmbrella => CreateState(CreateLoveUmbrella(), 76, 45, 28),
            DetailedWipeBuiltInPreset.Smiley => CreateState(CreateSmiley(), 76, 58, 42),
            _ => CreateState(WipeStrokeDocument.CreateStarter(), 130, 0, 15),
        };
    }

    public static string GetDisplayName(DetailedWipeBuiltInPreset preset)
    {
        return preset switch
        {
            DetailedWipeBuiltInPreset.Heart => "ハート",
            DetailedWipeBuiltInPreset.LoveUmbrella => "相合い傘",
            DetailedWipeBuiltInPreset.Smiley => "スマイル",
            _ => "不明なプリセット",
        };
    }

    internal static DetailedWipeAnimationState ConstantAnimation(double value) =>
        new() { From = value, To = value, AnimationType = 0 };

    private static DetailedWipePresetState CreateState(
        WipeStrokeDocument document,
        double circleDiameterPixels,
        double smoothing,
        double softness)
    {
        return new DetailedWipePresetState
        {
            CustomPathData = WipeStrokeDocumentCodec.Encode(document),
            BrushShape = GlassWipeBrushShape.Circle,
            CircleDiameterPixels = ConstantAnimation(circleDiameterPixels),
            BrushRotation = ConstantAnimation(0),
            WipeStrength = ConstantAnimation(100),
            PathSmoothing = smoothing,
            BrushSoftness = softness,
            Quality = GlassWipeQuality.High,
        };
    }

    private static WipeStrokeDocument CreateHeart() =>
        OneStroke(
            (0, 960, 313),
            (8, 884, 216),
            (16, 766, 184),
            (25, 647, 248),
            (34, 604, 389),
            (43, 658, 540),
            (52, 787, 691),
            (60, 960, 886),
            (68, 1133, 691),
            (77, 1262, 540),
            (86, 1316, 389),
            (92, 1273, 248),
            (95, 1154, 184),
            (98, 1036, 216),
            (100, 960, 313));

    private static WipeStrokeDocument CreateLoveUmbrella() =>
        OneStroke(
            (0, 960, 216),
            (14, 787, 367),
            (28, 614, 616),
            (40, 744, 659),
            (50, 960, 680),
            (60, 1176, 659),
            (72, 1306, 616),
            (86, 1133, 367),
            (92, 960, 216),
            (100, 960, 950));

    private static WipeStrokeDocument CreateSmiley()
    {
        return new WipeStrokeDocument
        {
            Strokes =
            [
                Stroke(
                    (0, 960, 130),
                    (6, 1154, 173),
                    (12, 1306, 313),
                    (18, 1370, 540),
                    (24, 1306, 767),
                    (30, 1154, 907),
                    (36, 960, 950),
                    (42, 766, 907),
                    (48, 614, 767),
                    (54, 550, 540),
                    (60, 614, 313),
                    (66, 766, 173),
                    (70, 960, 130)),
                Stroke((75, 809, 432)),
                Stroke((80, 1111, 432)),
                Stroke(
                    (84, 744, 648),
                    (90, 852, 756),
                    (95, 960, 799),
                    (98, 1068, 756),
                    (100, 1176, 648)),
            ],
        };
    }

    private static WipeStrokeDocument OneStroke(params (double T, double X, double Y)[] points) =>
        new() { Strokes = [Stroke(points)] };

    private static WipeStroke Stroke(params (double T, double X, double Y)[] points) =>
        new()
        {
            Points = points
                .Select(point => new WipeStrokePoint(point.T, point.X, point.Y, 100))
                .ToList(),
        };
}
