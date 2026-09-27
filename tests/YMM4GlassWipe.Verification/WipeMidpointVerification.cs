// SPDX-License-Identifier: MPL-2.0

using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using FrameTime = YukkuriMovieMaker.Player.Video.FrameTime;

namespace YMM4GlassWipe.Verification;

internal static class WipeMidpointVerification
{
    private const int SourceLength = 1200;
    private static readonly WipeMaskGeometry Geometry = WipeMaskGeometry.Create(1920, 1080);

    public static void VerifySimpleCompletionAndSeek()
    {
        foreach (var mode in Enum.GetValues<WipePathTimingMode>())
        {
            var baseline = CreateEffect(mode);
            var keyed = CreateEffect(mode);
            SetLinear(keyed.SimpleProgress, 0, 100, true);
            foreach (var frame in new[] { 0, 29, 30, 75, 105, 179, 180, 300, 600, 1199, 105, 180 })
            {
                var expected = Samples(baseline, frame);
                var actual = Samples(keyed, frame);
                SameSamples(expected, actual, $"{mode} 中間点による往復軌跡の変化 frame={frame}");
                var snapshot = WipePathSnapshot.Create(keyed, Describe(frame), Geometry);
                SameSamples(actual, snapshot.Samples, $"{mode} ストリームと再構築の不一致 frame={frame}");
            }
            Check(Samples(keyed, 180).Length > 1, "拭き取り完了の検証には有効な軌跡が必要です。");
        }
    }

    public static void VerifyDetailedAndBrushAnimations()
    {
        foreach (var inputMode in Enum.GetValues<WipePathInputMode>())
        {
            var baseline = CreateEffect(WipePathTimingMode.Seconds);
            var keyed = CreateEffect(WipePathTimingMode.Seconds);
            foreach (var (effect, middle) in new[] { (baseline, false), (keyed, true) })
            {
                effect.EditingMode = GlassWipeEditingMode.Detailed;
                effect.PathInputMode = inputMode;
                effect.CustomPathData = WipeStrokeDocumentCodec.Encode(WipeStrokeDocument.CreateStarter());
                effect.BrushShape = GlassWipeBrushShape.Circle;
                SetLinear(effect.SimpleProgress, 0, 100, middle);
                SetLinear(effect.BrushX, 10, 90, middle);
                SetLinear(effect.BrushY, 25, 75, middle);
                SetLinear(effect.Contact, 20, 100, middle);
                SetLinear(effect.WipeStrength, 30, 90, middle);
                SetLinear(effect.CircleDiameterPixels, 50, 150, middle);
                SetLinear(effect.BrushRotation, -30, 30, middle);
                SetLinear(effect.SimpleGeneratedBrushRotation, -30, 30, middle);
            }
            foreach (var frame in new[] { 30, 105, 180, 900, 75 })
            {
                var actual = Samples(keyed, frame);
                SameSamples(Samples(baseline, frame), actual, $"{inputMode} ブラシ・座標の評価");
                SameSamples(actual, WipePathSnapshot.Create(keyed, Describe(frame), Geometry).Samples,
                    $"{inputMode} 履歴再構築の評価");
            }
        }
        foreach (var shape in Enum.GetValues<GlassWipeBrushShape>())
        {
            var baseline = CreateEffect(WipePathTimingMode.Seconds);
            var keyed = CreateEffect(WipePathTimingMode.Seconds);
            foreach (var (effect, middle) in new[] { (baseline, false), (keyed, true) })
            {
                effect.BrushShape = shape;
                effect.UserBrushPixelWidth = 200;
                effect.UserBrushPixelHeight = 100;
                foreach (var animation in new[] { effect.CircleDiameterPixels, effect.EllipseWidthPixels,
                    effect.EllipseHeightPixels, effect.RectangleWidthPixels, effect.RectangleHeightPixels,
                    effect.PngBrushSizeScale })
                    SetLinear(animation, 50, 150, middle);
            }
            foreach (var frame in new[] { 0, 75, 150 })
            {
                var expected = WipeBrushPixelDimensions.Resolve(baseline, frame, 150, 60, SourceLength);
                var actual = WipeBrushPixelDimensions.Resolve(keyed, frame, 150, 60, SourceLength);
                Near(actual.Width, expected.Width, $"{shape} 幅");
                Near(actual.Height, expected.Height, $"{shape} 高さ");
            }
        }
    }

    public static void VerifyEvaluationBoundariesAndPreservation()
    {
        var effect = CreateEffect(WipePathTimingMode.Seconds);
        SetLinear(effect.SimpleProgress, 0, 100, true);
        SetLinear(effect.FogAmount, 0, 100, true);
        var progress = effect.SimpleProgress;
        var keys = progress.KeyFrames ?? throw new InvalidOperationException("検証用中間点がありません。");
        var frames = keys.Frames.ToArray();
        var values = progress.Values.Select(value => value.Value).ToArray();
        var originalLength = progress.Length;
        Near(WipeAnimationEvaluation.GetValue(progress, 0, 150, 60, SourceLength), 0, "開始値");
        Near(WipeAnimationEvaluation.GetValue(progress, 75, 150, 60, SourceLength), 50, "中間値");
        Near(WipeAnimationEvaluation.GetValue(progress, 150, 150, 60, SourceLength), 100, "完了値");
        Near(WipeAnimationEvaluation.GetValue(progress, 1, 1, 60, SourceLength), 100, "即時完了");
        Near(WipeAnimationEvaluation.GetValue(progress, int.MaxValue, int.MaxValue, 60, SourceLength), 100,
            "長い区間の整数演算");
        Near(GlassWipeParameters.Create(effect, Describe(180)).FogAmount, 0.15, "曇り量はアイテム時刻のまま");
        var before = WipePathStream.Create(effect, Describe(180), Geometry).DefinitionFingerprint;
        var after = WipePathStream.Create(effect, Describe(180, 2400), Geometry).DefinitionFingerprint;
        Check(before != after, "元のアイテム長の変更は拭き取りキャッシュへ反映する必要があります。");
        _ = Samples(effect, 105);
        Check(ReferenceEquals(keys, progress.KeyFrames) && frames.SequenceEqual(keys.Frames) &&
            values.SequenceEqual(progress.Values.Select(value => value.Value)) && originalLength == progress.Length,
            "描画評価が保存用Animationや共有中間点を変更してはいけません。");
        var unkeyed = new Animation(0, 0, 100);
        SetLinear(unkeyed, 0, 100, false);
        foreach (var frame in new[] { 0, 37, 75, 150 })
            Near(WipeAnimationEvaluation.GetValue(unkeyed, frame, 150, 60, SourceLength),
                unkeyed.GetValue(frame, 150, 60), "中間点なしの評価維持");
        var uneven = new Animation(0, 0, 100);
        SetLinear(uneven, 0, 100, false);
        var unevenKeys = new KeyFrames();
        uneven.SetKeyFrames(unevenKeys);
        unevenKeys.Insert(300);
        unevenKeys.Insert(900);
        double[] unevenValues = [0, 80, 20, 100];
        for (var i = 0; i < unevenValues.Length; i++) uneven.Values[i].Value = unevenValues[i];
        Near(WipeAnimationEvaluation.GetValue(uneven, 30, 120, 60, SourceLength), 80, "複数中間点の山");
        Near(WipeAnimationEvaluation.GetValue(uneven, 90, 120, 60, SourceLength), 20, "複数中間点の谷");
        Near(WipeAnimationEvaluation.GetValue(uneven, 120, 120, 60, SourceLength), 100, "複数中間点の終端");
    }

    private static GlassWipeVideoEffect CreateEffect(WipePathTimingMode mode)
    {
        var effect = new GlassWipeVideoEffect
        {
            PathTimingMode = mode,
            PathStartSeconds = 0.5, PathCompletionSeconds = 3,
            PathStartFrame = 30, PathCompletionFrame = 180,
            PathStart = 2.5, PathCompletion = 15,
        };
        SetLinear(effect.SimpleProgress, 0, 100, false);
        return effect;
    }

    private static void SetLinear(Animation animation, double from, double to, bool middle)
    {
        var definition = new Animation(from, -8192, 8192) { AnimationType = AnimationType.直線移動 };
#pragma warning disable CS0618
        definition.From = from;
        definition.To = to;
#pragma warning restore CS0618
        animation.CopyFrom(definition);
        animation.SetAnimationParameters(SourceLength, 60);
        if (middle)
        {
            var keys = new KeyFrames();
            animation.SetKeyFrames(keys);
            keys.Insert(600);
        }
        for (var index = 0; index < animation.Values.Count; index++)
            animation.Values[index].Value = from + (to - from) * index / (animation.Values.Count - 1);
    }

    private static WipePathSample[] Samples(GlassWipeVideoEffect effect, int frame)
    {
        var stream = WipePathStream.Create(effect, Describe(frame), Geometry);
        var samples = new List<WipePathSample>();
        stream.EnumerateSamples(0, stream.Frame, (_, sample) => samples.Add(sample));
        return samples.ToArray();
    }

    private static EffectDescription Describe(int frame, int length = SourceLength)
    {
        var timeline = new TimelineSourceDescription(new System.Drawing.Size(1920, 1080),
            new FrameTime(frame, 60), new FrameTime(length, 60), 60, default, Guid.Empty, []);
        var item = new TimelineItemSourceDescription(timeline, frame, length, 0);
        var draw = (DrawDescription)RuntimeHelpers.GetUninitializedObject(typeof(DrawDescription));
        return new EffectDescription(item, draw, 0, 1, 0, 1);
    }

    private static void SameSamples(WipePathSample[] expected, WipePathSample[] actual, string context)
    {
        Check(expected.Length == actual.Length, $"{context}: サンプル数");
        for (var i = 0; i < expected.Length; i++)
        {
            var a = expected[i]; var b = actual[i];
            Check(a.Frame == b.Frame && a.AccumulationGroup == b.AccumulationGroup, $"{context}: 履歴番号");
            Near(a.X, b.X, context); Near(a.Y, b.Y, context);
            Near(a.Contact, b.Contact, context); Near(a.Strength, b.Strength, context);
            Near(a.RotationRadians, b.RotationRadians, context);
            Check(a.BrushWidthPixels.HasValue == b.BrushWidthPixels.HasValue &&
                a.BrushHeightPixels.HasValue == b.BrushHeightPixels.HasValue, $"{context}: ブラシ寸法の有無");
            Near(a.BrushWidthPixels ?? 0, b.BrushWidthPixels ?? 0, context);
            Near(a.BrushHeightPixels ?? 0, b.BrushHeightPixels ?? 0, context);
        }
    }

    private static void Near(double actual, double expected, string message) =>
        Check(double.IsFinite(actual) && Math.Abs(actual - expected) < 0.00001,
            $"{message}: actual={actual}, expected={expected}");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
