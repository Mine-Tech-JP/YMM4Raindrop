// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YMM4GlassWipe;

internal sealed class WipePathSnapshot
{
    private const int SchemaVersion = 17;

    public WipePathSnapshot(
        int frame,
        int itemLength,
        int framesPerSecond,
        IReadOnlyList<WipePathSample> samples,
        WipePathStyle style = default,
        WipePathInputMode inputMode = WipePathInputMode.LegacyAnimation,
        string? pathDataSignature = null)
    {
        ArgumentNullException.ThrowIfNull(samples);

        Frame = Math.Max(0, frame);
        ItemLength = Math.Max(0, itemLength);
        FramesPerSecond = Math.Max(1, framesPerSecond);
        Samples = samples.ToArray();
        Style = style.Sanitize();
        InputMode = SanitizeInputMode(inputMode);
        PathDataSignature = pathDataSignature ?? string.Empty;
        Fingerprint = WipePathFingerprint.Create(
            SchemaVersion,
            ItemLength,
            FramesPerSecond,
            Style,
            InputMode,
            PathDataSignature,
            Samples);
    }

    public int Frame { get; }

    public int ItemLength { get; }

    public int FramesPerSecond { get; }

    public WipePathSample[] Samples { get; }

    public WipePathStyle Style { get; }

    public WipePathInputMode InputMode { get; }

    public string PathDataSignature { get; }

    public WipePathFingerprint Fingerprint { get; }

    public static WipePathSnapshot Create(
        GlassWipeVideoEffect item,
        EffectDescription effectDescription,
        WipeMaskGeometry? maskGeometry = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(effectDescription);

        var itemLength = Math.Max(0, effectDescription.ItemDuration.Frame);
        var framesPerSecond = Math.Max(1, effectDescription.FPS);
        var frame = GetLastHistoryFrame(
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
        var evaluationFrame = timingWindow.GetEvaluationFrame(frame);
        var hasStarted = evaluationFrame >= 0;
        var historyFrame = Math.Max(0, evaluationFrame);
        var evaluationLength = timingWindow.EvaluationLength;
        var geometry = maskGeometry ?? WipeMaskGeometry.Create(1920, 1080);
        if (WipePathInputModeCompatibility.UsesSimpleGenerator(
            item.EditingMode,
            item.PathInputMode))
        {
            var simpleResult = WipeSimplePathGenerator.Generate(
                item.SimplePattern,
                item.SimpleInterpolation,
                new Vector2((float)item.SimpleStartX, (float)item.SimpleStartY),
                new Vector2((float)item.SimpleControlX, (float)item.SimpleControlY),
                new Vector2((float)item.SimpleEndX, (float)item.SimpleEndY),
                item.SimpleRoundTrips,
                historyFrame,
                evaluationLength,
                item.SimpleReturnOffset,
                item.SimpleWipeAmountPerPass,
                geometry,
                item.SimpleReturnOffsetX,
                historyFrame => GlassWipeParameterSanitizer.ToUnit(
                    WipeAnimationEvaluation.GetValue(item.SimpleProgress,
                        historyFrame,
                        evaluationLength,
                        framesPerSecond,
                        itemLength)),
                item.SimpleGeneratedQuality,
                item.ContinuousWipe);
            var simpleStyle = ResolveSimpleGeneratedStyle(
                item.EditingMode,
                simpleResult.Style,
                item.BrushShape,
                item.SimpleGeneratedBrushRotationFollow,
                item.BrushMirror,
                item.UserBrushId,
                item.UserBrushRevision,
                item.ContinuousWipe,
                item.UserBrushPixelWidth,
                item.UserBrushPixelHeight);
            var simpleSamples = ResolveSimpleGeneratedSamples(
                item.EditingMode,
                simpleResult.Samples,
                historyFrame => WipeBrushPixelDimensions.Resolve(
                    item,
                    historyFrame,
                    evaluationLength,
                    framesPerSecond,
                    itemLength),
                historyFrame => EvaluateRange(
                    item.SimpleGeneratedBrushRotation,
                    historyFrame,
                    evaluationLength,
                    framesPerSecond,
                    itemLength,
                    -180,
                    180));
            var simpleSnapshot = new WipePathSnapshot(
                simpleResult.Frame,
                simpleResult.ItemLength,
                framesPerSecond,
                simpleSamples,
                simpleStyle,
                WipePathInputMode.StrokeCollection,
                simpleResult.Signature);
            return ApplyStartGate(simpleSnapshot, hasStarted);
        }

        var style = new WipePathStyle(
            ClampSetting(item.PathSmoothing, 0, 100) / 100,
            ClampSetting(item.PathJitter, 0, 5) / 100,
            (int)MathF.Round(ClampSetting(item.JitterSeed, 0, 9999)),
            ClampSetting(item.BrushRotationFollow, 0, 100) / 100,
            ClampSetting(item.BrushSoftness, 0, 100) / 100,
            GlassWipeParameterSanitizer.SanitizeQuality(item.Quality),
            BrushShape: GlassWipeParameterSanitizer.SanitizeBrushShape(
                item.BrushShape),
            BrushMirror: item.BrushMirror,
            UserBrushId: item.UserBrushId,
            UserBrushRevision: item.UserBrushRevision,
            ContinuousWipe: item.ContinuousWipe,
            UserBrushPixelWidth: item.UserBrushPixelWidth,
            UserBrushPixelHeight: item.UserBrushPixelHeight);

        if (TryResolveCustomDocument(
                item.PathInputMode,
                item.CustomPathData,
                out var customDocument))
        {
            var customSamples = WipeStrokeSampler.Expand(
                    customDocument,
                    historyFrame,
                    evaluationLength,
                    geometry)
                .Select(sample => WipeBrushPixelDimensions.Resolve(
                        item,
                        sample.Frame,
                        evaluationLength,
                        framesPerSecond,
                        itemLength)
                    .ApplyTo(new WipePathSample(
                        sample.Frame,
                        sample.X,
                        sample.Y,
                        sample.Contact,
                        0,
                        EvaluateUnit(
                            item.WipeStrength,
                            sample.Frame,
                            evaluationLength,
                            framesPerSecond,
                            itemLength),
                        1,
                        EvaluateRange(
                            item.BrushRotation,
                            sample.Frame,
                            evaluationLength,
                            framesPerSecond,
                            itemLength,
                            -180,
                            180) * MathF.PI / 180)))
                .ToArray();

            var customSnapshot = new WipePathSnapshot(
                historyFrame,
                evaluationLength,
                framesPerSecond,
                customSamples,
                style,
                WipePathInputMode.StrokeCollection,
                WipeStrokeDocumentCodec.Encode(customDocument));
            return ApplyStartGate(customSnapshot, hasStarted);
        }

        var legacyBaseSnapshot = CreateLegacy(
            historyFrame,
            evaluationLength,
            framesPerSecond,
            historyFrame => new WipeLegacyAnimationValues(
                WipeAnimationEvaluation.GetValue(item.BrushX, historyFrame, evaluationLength, framesPerSecond, itemLength),
                WipeAnimationEvaluation.GetValue(item.BrushY, historyFrame, evaluationLength, framesPerSecond, itemLength),
                WipeAnimationEvaluation.GetValue(item.Contact, historyFrame, evaluationLength, framesPerSecond, itemLength),
                0,
                WipeAnimationEvaluation.GetValue(item.WipeStrength, historyFrame, evaluationLength, framesPerSecond, itemLength),
                100,
                WipeAnimationEvaluation.GetValue(item.BrushRotation, historyFrame, evaluationLength, framesPerSecond, itemLength)),
            style);
        var legacySnapshot = new WipePathSnapshot(
            legacyBaseSnapshot.Frame,
            legacyBaseSnapshot.ItemLength,
            legacyBaseSnapshot.FramesPerSecond,
            legacyBaseSnapshot.Samples
                .Select(sample => WipeBrushPixelDimensions.Resolve(
                        item,
                        sample.Frame,
                        evaluationLength,
                        framesPerSecond,
                        itemLength)
                    .ApplyTo(sample))
                .ToArray(),
            legacyBaseSnapshot.Style,
            legacyBaseSnapshot.InputMode,
            legacyBaseSnapshot.PathDataSignature);
        return ApplyStartGate(legacySnapshot, hasStarted);
    }

    internal static WipePathSnapshot CreateLegacy(
        int currentFrame,
        int itemLength,
        int framesPerSecond,
        Func<int, WipeLegacyAnimationValues> valueProvider,
        WipePathStyle style = default)
    {
        ArgumentNullException.ThrowIfNull(valueProvider);

        itemLength = Math.Max(0, itemLength);
        framesPerSecond = Math.Max(1, framesPerSecond);
        var frame = GetLastHistoryFrame(currentFrame, itemLength);
        var samples = new WipePathSample[frame + 1];

        for (var historyFrame = 0; historyFrame <= frame; historyFrame++)
        {
            var values = valueProvider(historyFrame);
            samples[historyFrame] = new WipePathSample(
                historyFrame,
                GlassWipeParameterSanitizer.ToUnit(values.XPercent),
                GlassWipeParameterSanitizer.ToUnit(values.YPercent),
                GlassWipeParameterSanitizer.ToUnit(values.ContactPercent),
                GlassWipeParameterSanitizer.ToUnit(values.SizePercent),
                GlassWipeParameterSanitizer.ToUnit(values.StrengthPercent),
                GlassWipeParameterSanitizer.Clamp(
                    values.AspectRatioPercent,
                    100,
                    400) / 100,
                GlassWipeParameterSanitizer.Clamp(
                    values.RotationDegrees,
                    -180,
                    180) * MathF.PI / 180);
        }

        return new WipePathSnapshot(
            frame,
            itemLength,
            framesPerSecond,
            samples,
            style,
            WipePathInputMode.LegacyAnimation);
    }

    internal static bool TryResolveCustomDocument(
        WipePathInputMode inputMode,
        string? customPathData,
        out WipeStrokeDocument document)
    {
        if (SanitizeInputMode(inputMode) == WipePathInputMode.StrokeCollection &&
            WipeStrokeDocumentCodec.TryDecode(customPathData, out document))
        {
            return true;
        }

        document = WipeStrokeDocument.CreateEmpty();
        return false;
    }

    public static int GetLastHistoryFrame(int currentFrame, int itemLength) =>
        Math.Clamp(currentFrame, 0, Math.Max(0, itemLength));

    internal static WipePathSnapshot ApplyStartGate(
        WipePathSnapshot snapshot,
        bool hasStarted)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return hasStarted
            ? snapshot
            : new WipePathSnapshot(
                0,
                snapshot.ItemLength,
                snapshot.FramesPerSecond,
                [],
                snapshot.Style,
                snapshot.InputMode,
                snapshot.PathDataSignature);
    }

    internal static WipePathStyle ResolveSimpleGeneratedStyle(
        GlassWipeEditingMode editingMode,
        WipePathStyle generatedStyle,
        GlassWipeBrushShape brushShape,
        double rotationFollowPercent = 0,
        bool brushMirror = false,
        Guid userBrushId = default,
        long userBrushRevision = 0,
        bool continuousWipe = false,
        int userBrushPixelWidth = 0,
        int userBrushPixelHeight = 0) =>
        editingMode is
            GlassWipeEditingMode.Simple or
            GlassWipeEditingMode.Detailed
            ? generatedStyle with
            {
                RotationFollow = ClampSetting(
                    rotationFollowPercent,
                    0,
                    100) / 100,
                BrushShape = GlassWipeParameterSanitizer.SanitizeBrushShape(
                    brushShape),
                BrushMirror = brushMirror,
                UserBrushId = userBrushId,
                UserBrushRevision = userBrushRevision,
                ContinuousWipe = continuousWipe,
                UserBrushPixelWidth = userBrushPixelWidth,
                UserBrushPixelHeight = userBrushPixelHeight,
            }
            : generatedStyle;

    internal static WipePathSample[] ResolveSimpleGeneratedSamples(
        GlassWipeEditingMode editingMode,
        IReadOnlyList<WipePathSample> generatedSamples,
        Func<int, WipeBrushPixelDimensions> dimensionsProvider,
        Func<int, float> rotationDegreesProvider)
    {
        ArgumentNullException.ThrowIfNull(generatedSamples);
        ArgumentNullException.ThrowIfNull(dimensionsProvider);
        ArgumentNullException.ThrowIfNull(rotationDegreesProvider);

        if (editingMode is not
            (GlassWipeEditingMode.Simple or GlassWipeEditingMode.Detailed))
        {
            return generatedSamples.ToArray();
        }

        return generatedSamples
            .Select(sample => ResolveSimpleGeneratedSample(
                sample,
                dimensionsProvider(sample.Frame),
                rotationDegreesProvider(sample.Frame)))
            .ToArray();
    }

    internal static WipePathSample ResolveSimpleGeneratedSample(
        WipePathSample sample,
        WipeBrushPixelDimensions dimensions,
        float rotationDegrees) =>
        dimensions.ApplyTo(sample with
        {
            RotationRadians = sample.RotationRadians +
                ClampSetting(rotationDegrees, -180, 180) * MathF.PI / 180,
        });

    public bool HasSameContext(WipePathSnapshot other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return ItemLength == other.ItemLength &&
               FramesPerSecond == other.FramesPerSecond &&
               Style == other.Style &&
               InputMode == other.InputMode &&
               string.Equals(
                   PathDataSignature,
                   other.PathDataSignature,
                   StringComparison.Ordinal);
    }

    public bool StartsWith(WipePathSnapshot prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        if (!HasSameContext(prefix) || Samples.Length < prefix.Samples.Length)
        {
            return false;
        }

        return Samples.AsSpan(0, prefix.Samples.Length)
            .SequenceEqual(prefix.Samples);
    }

    private static float EvaluateUnit(
        Animation animation,
        int frame,
        int itemLength,
        int framesPerSecond,
        int sourceItemLength)
    {
        var value = WipeAnimationEvaluation.GetValue(animation, frame, itemLength, framesPerSecond, sourceItemLength);
        return GlassWipeParameterSanitizer.ToUnit(value);
    }

    private static float EvaluateRange(
        Animation animation,
        int frame,
        int itemLength,
        int framesPerSecond,
        int sourceItemLength,
        float minimum,
        float maximum)
    {
        var value = WipeAnimationEvaluation.GetValue(animation, frame, itemLength, framesPerSecond, sourceItemLength);
        return GlassWipeParameterSanitizer.Clamp(value, minimum, maximum);
    }

    private static float ClampSetting(
        double value,
        float minimum,
        float maximum) =>
        GlassWipeParameterSanitizer.Clamp(
            (float)value,
            minimum,
            maximum);

    private static WipePathInputMode SanitizeInputMode(
        WipePathInputMode inputMode) =>
        inputMode == WipePathInputMode.StrokeCollection
            ? WipePathInputMode.StrokeCollection
            : WipePathInputMode.LegacyAnimation;
}
