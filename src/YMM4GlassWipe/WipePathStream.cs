// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YMM4GlassWipe;

internal enum WipePathStreamKind
{
    LegacyAnimation,
    SimpleGenerated,
    StrokeCollection,
}

/// <summary>
/// 全履歴を保持せず、指定範囲のサンプルだけを順次評価する軌跡定義です。
/// </summary>
internal sealed class WipePathStream
{
    public const int SampleBatchCapacity = 2048;

    private readonly Action<int, int, Action<int, WipePathSample>> _enumerate;
    private readonly int[] _strokeIdBases;

    internal WipePathStream(
        int frame,
        int itemLength,
        int framesPerSecond,
        int laneCount,
        WipePathStyle style,
        WipePathStreamKind kind,
        WipePathDefinitionFingerprint definitionFingerprint,
        Action<int, int, Action<int, WipePathSample>> enumerate,
        int[]? strokeIdBases = null)
    {
        Frame = frame;
        ItemLength = Math.Max(0, itemLength);
        FramesPerSecond = Math.Max(1, framesPerSecond);
        LaneCount = Math.Max(1, laneCount);
        Style = style.Sanitize();
        Kind = kind;
        DefinitionFingerprint = definitionFingerprint;
        _enumerate = enumerate ?? throw new ArgumentNullException(nameof(enumerate));
        _strokeIdBases = strokeIdBases is { Length: > 0 }
            ? strokeIdBases.ToArray()
            : [0];
    }

    public int Frame { get; }

    public int ItemLength { get; }

    public int FramesPerSecond { get; }

    public int LaneCount { get; }

    public WipePathStyle Style { get; }

    public WipePathStreamKind Kind { get; }

    public WipePathDefinitionFingerprint DefinitionFingerprint { get; }

    public bool HasStarted => Frame >= 0;

    public int GetStrokeIdBase(int laneId) =>
        _strokeIdBases[Math.Clamp(laneId, 0, _strokeIdBases.Length - 1)];

    public static WipePathStream Create(
        GlassWipeVideoEffect item,
        EffectDescription effectDescription,
        WipeMaskGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(effectDescription);

        var itemLength = Math.Max(0, effectDescription.ItemDuration.Frame);
        var framesPerSecond = Math.Max(1, effectDescription.FPS);
        var currentFrame = WipePathSnapshot.GetLastHistoryFrame(
            effectDescription.ItemPosition.Frame,
            itemLength);
        var timingWindow = WipePathTiming.ResolveWindow(
            itemLength,
            framesPerSecond,
            item.PathTimingMode,
            item.PathStart,
            item.PathCompletion,
            item.PathStartFrame,
            item.PathCompletionFrame,
            item.PathStartSeconds,
            item.PathCompletionSeconds);
        var evaluationFrame = timingWindow.GetEvaluationFrame(currentFrame);
        var evaluationLength = timingWindow.EvaluationLength;

        if (WipePathInputModeCompatibility.UsesSimpleGenerator(
                item.EditingMode,
                item.PathInputMode))
        {
            return CreateSimple(
                item,
                timingWindow,
                evaluationFrame,
                evaluationLength,
                framesPerSecond,
                itemLength,
                geometry);
        }

        var style = CreateDetailedStyle(item);
        if (item.PathInputMode == WipePathInputMode.StrokeCollection &&
            WipeStrokeDocumentCodec.TryDecode(item.CustomPathData, out var document))
        {
            var sanitizedDocument = document.Sanitize();
            var signature = WipeStrokeDocumentCodec.Encode(sanitizedDocument);
            var fingerprint = WipePathDefinitionFingerprint.Create(
                item,
                timingWindow,
                framesPerSecond,
                style,
                geometry,
                signature,
                itemLength);
            return new WipePathStream(
                evaluationFrame,
                evaluationLength,
                framesPerSecond,
                Math.Max(1, sanitizedDocument.Strokes.Count),
                style,
                WipePathStreamKind.StrokeCollection,
                fingerprint,
                (first, last, sink) =>
                {
                    foreach (var (laneId, frameSample) in WipeStrokeSampler.EnumerateMergedSanitized(
                                 sanitizedDocument,
                                 first,
                                 last,
                                 evaluationLength,
                                 geometry))
                    {
                        var brushDimensions = WipeBrushPixelDimensions.Resolve(
                            item,
                            frameSample.Frame,
                            evaluationLength,
                            framesPerSecond,
                            itemLength);
                        sink(
                            laneId,
                            brushDimensions.ApplyTo(new WipePathSample(
                                frameSample.Frame,
                                frameSample.X,
                                frameSample.Y,
                                frameSample.Contact,
                                0,
                                EvaluateUnit(
                                    item.WipeStrength,
                                    frameSample.Frame,
                                    evaluationLength,
                                    framesPerSecond,
                                    itemLength),
                                1,
                                EvaluateRange(
                                    item.BrushRotation,
                                    frameSample.Frame,
                                    evaluationLength,
                                    framesPerSecond,
                                    itemLength,
                                    -180,
                                    180) * MathF.PI / 180)));
                    }
                },
                WipeStrokeSampler.GetStrokeIdBasesSanitized(sanitizedDocument));
        }

        var legacyFingerprint = WipePathDefinitionFingerprint.Create(
            item,
            timingWindow,
            framesPerSecond,
            style,
            geometry,
            string.Empty,
            itemLength);
        return new WipePathStream(
            evaluationFrame,
            evaluationLength,
            framesPerSecond,
            1,
            style,
            WipePathStreamKind.LegacyAnimation,
            legacyFingerprint,
            (first, last, sink) =>
            {
                for (var frame = first; ; frame++)
                {
                    var brushDimensions = WipeBrushPixelDimensions.Resolve(
                        item,
                        frame,
                        evaluationLength,
                        framesPerSecond,
                        itemLength);
                    sink(
                        0,
                        brushDimensions.ApplyTo(new WipePathSample(
                            frame,
                            EvaluateUnit(item.BrushX, frame, evaluationLength, framesPerSecond, itemLength),
                            EvaluateUnit(item.BrushY, frame, evaluationLength, framesPerSecond, itemLength),
                            EvaluateUnit(item.Contact, frame, evaluationLength, framesPerSecond, itemLength),
                            0,
                            EvaluateUnit(item.WipeStrength, frame, evaluationLength, framesPerSecond, itemLength),
                            1,
                            EvaluateRange(
                                item.BrushRotation,
                                frame,
                                evaluationLength,
                                framesPerSecond,
                                itemLength,
                                -180,
                                180) * MathF.PI / 180)));
                    if (frame == last)
                    {
                        break;
                    }
                }
            });
    }

    public void EnumerateSamples(
        int firstFrame,
        int lastFrame,
        Action<int, WipePathSample> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (!HasStarted)
        {
            return;
        }

        var first = Math.Clamp(firstFrame, 0, ItemLength);
        var last = Math.Clamp(lastFrame, 0, Math.Min(Frame, ItemLength));
        if (last < first)
        {
            return;
        }

        var batchFirst = first;
        while (batchFirst <= last)
        {
            var batchLastLong = Math.Min(
                (long)last,
                (long)batchFirst + SampleBatchCapacity - 1);
            var batchLast = (int)batchLastLong;
            _enumerate(batchFirst, batchLast, sink);
            if (batchLast == last)
            {
                break;
            }

            batchFirst = batchLast + 1;
        }
    }

    private static WipePathStream CreateSimple(
        GlassWipeVideoEffect item,
        WipePathTimingWindow timingWindow,
        int evaluationFrame,
        int evaluationLength,
        int framesPerSecond,
        int itemLength,
        WipeMaskGeometry geometry)
    {
        var evaluator = WipeSimplePathGenerator.CreateStreamEvaluator(
            item.SimplePattern,
            item.SimpleInterpolation,
            new Vector2((float)item.SimpleStartX, (float)item.SimpleStartY),
            new Vector2((float)item.SimpleControlX, (float)item.SimpleControlY),
            new Vector2((float)item.SimpleEndX, (float)item.SimpleEndY),
            item.SimpleRoundTrips,
            evaluationLength,
            item.SimpleReturnOffset,
            item.SimpleWipeAmountPerPass,
            geometry,
            item.SimpleReturnOffsetX,
            frame => GlassWipeParameterSanitizer.ToUnit(
                WipeAnimationEvaluation.GetValue(item.SimpleProgress, frame, evaluationLength, framesPerSecond, itemLength)),
            item.SimpleGeneratedQuality,
            item.ContinuousWipe);
        var style = WipePathSnapshot.ResolveSimpleGeneratedStyle(
            item.EditingMode,
            evaluator.Style,
            item.BrushShape,
            item.SimpleGeneratedBrushRotationFollow,
            item.BrushMirror,
            item.UserBrushId,
            item.UserBrushRevision,
            item.ContinuousWipe,
            item.UserBrushPixelWidth,
            item.UserBrushPixelHeight);
        var signature = evaluator.CreateSignature(0);
        var fingerprint = WipePathDefinitionFingerprint.Create(
            item,
            timingWindow,
            framesPerSecond,
            style,
            geometry,
            signature,
            itemLength);
        return new WipePathStream(
            evaluationFrame,
            evaluationLength,
            framesPerSecond,
            1,
            style,
            WipePathStreamKind.SimpleGenerated,
            fingerprint,
            (first, last, sink) =>
            {
                for (var frame = first; ; frame++)
                {
                    var sample = evaluator.Evaluate(frame);
                    var resolved = item.EditingMode is
                        GlassWipeEditingMode.Simple or GlassWipeEditingMode.Detailed
                            ? WipePathSnapshot.ResolveSimpleGeneratedSample(
                                sample,
                                WipeBrushPixelDimensions.Resolve(
                                    item,
                                    frame,
                                    evaluationLength,
                                    framesPerSecond,
                                    itemLength),
                                EvaluateRange(
                            item.SimpleGeneratedBrushRotation,
                            frame,
                            evaluationLength,
                            framesPerSecond,
                            itemLength,
                            -180,
                            180))
                            : sample;
                    sink(0, resolved);
                    if (frame == last)
                    {
                        break;
                    }
                }
            });
    }

    private static WipePathStyle CreateDetailedStyle(GlassWipeVideoEffect item) =>
        new WipePathStyle(
            ClampSetting(item.PathSmoothing, 0, 100) / 100,
            ClampSetting(item.PathJitter, 0, 5) / 100,
            (int)MathF.Round(ClampSetting(item.JitterSeed, 0, 9999)),
            ClampSetting(item.BrushRotationFollow, 0, 100) / 100,
            ClampSetting(item.BrushSoftness, 0, 100) / 100,
            GlassWipeParameterSanitizer.SanitizeQuality(item.Quality),
            BrushShape: GlassWipeParameterSanitizer.SanitizeBrushShape(item.BrushShape),
            BrushMirror: item.BrushMirror,
            UserBrushId: item.UserBrushId,
            UserBrushRevision: item.UserBrushRevision,
            ContinuousWipe: item.ContinuousWipe,
            UserBrushPixelWidth: item.UserBrushPixelWidth,
            UserBrushPixelHeight: item.UserBrushPixelHeight);

    private static float EvaluateUnit(
        Animation animation,
        int frame,
        int itemLength,
        int framesPerSecond,
        int sourceItemLength) =>
        GlassWipeParameterSanitizer.ToUnit(
            WipeAnimationEvaluation.GetValue(animation, frame, itemLength, framesPerSecond, sourceItemLength));

    private static float EvaluateRange(
        Animation animation,
        int frame,
        int itemLength,
        int framesPerSecond,
        int sourceItemLength,
        float minimum,
        float maximum) =>
        GlassWipeParameterSanitizer.Clamp(
            WipeAnimationEvaluation.GetValue(animation, frame, itemLength, framesPerSecond, sourceItemLength),
            minimum,
            maximum);

    private static float ClampSetting(
        double value,
        float minimum,
        float maximum) =>
        GlassWipeParameterSanitizer.Clamp((float)value, minimum, maximum);
}
