// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace YMM4GlassWipe;

internal static partial class WipeBrushStampGenerator
{
    public const float SpacingRatio = 0.25f;
    public const int StampBatchCapacity = 4096;
    private const float MinimumSpacing = 1f / WipeMaskRenderer.MaskSize;
    private const float NoiseWavelength = 0.12f;
    private const int CurveLengthSamples = 8;
    private const float DirectionEpsilon = 0.000001f;

    public static IReadOnlyList<WipeBrushStamp> Generate(
        IReadOnlyList<WipePathSample> samples,
        int firstSampleIndex,
        WipeMaskGeometry geometry,
        WipePathStyle style = default)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0)
        {
            return [];
        }

        var sanitizedStyle = style.Sanitize();
        if (sanitizedStyle.ContinuousWipe)
        {
            return GenerateContinuous(samples, firstSampleIndex, geometry, sanitizedStyle);
        }

        return sanitizedStyle.IsDisabled
            ? GenerateLinear(samples, firstSampleIndex, geometry)
            : GenerateShaped(
                samples,
                firstSampleIndex,
                geometry,
                sanitizedStyle);
    }

    private static IReadOnlyList<WipeBrushStamp> GenerateLinear(
        IReadOnlyList<WipePathSample> samples,
        int firstSampleIndex,
        WipeMaskGeometry geometry)
    {
        var stamps = new List<WipeBrushStamp>();
        var startIndex = Math.Clamp(firstSampleIndex, 0, samples.Count);

        if (startIndex == 0)
        {
            AddStamp(stamps, samples[0], geometry);
            startIndex = 1;
        }

        for (var index = startIndex; index < samples.Count; index++)
        {
            var previous = samples[index - 1];
            var current = samples[index];

            if (!current.IsContacting)
            {
                continue;
            }

            if (!previous.IsContacting)
            {
                AddStamp(stamps, current, geometry);
                continue;
            }

            AddLinearSegment(stamps, previous, current, geometry);
        }

        return stamps;
    }

    private static IReadOnlyList<WipeBrushStamp> GenerateShaped(
        IReadOnlyList<WipePathSample> samples,
        int firstSampleIndex,
        WipeMaskGeometry geometry,
        WipePathStyle style)
    {
        var (strokeIds, strokeArcLengths) = BuildStrokeMetadata(
            samples,
            geometry);

        var stamps = new List<WipeBrushStamp>();
        var startIndex = Math.Clamp(firstSampleIndex, 0, samples.Count);

        if (startIndex == 0)
        {
            AddStamp(
                stamps,
                samples[0],
                geometry,
                style,
                ResolveInitialTangent(style));
            startIndex = 1;
        }

        for (var index = startIndex; index < samples.Count; index++)
        {
            var previous = samples[index - 1];
            var current = samples[index];

            if (!current.IsContacting)
            {
                continue;
            }

            if (!previous.IsContacting)
            {
                AddStamp(stamps, current, geometry, style);
                continue;
            }

            // 後続フレームへ依存させず、前進追加済みの軌跡を変化させない。
            WipePathSample? previousPrevious =
                index >= 2 && samples[index - 2].IsContacting
                    ? samples[index - 2]
                    : null;
            AddShapedSegment(
                stamps,
                previousPrevious,
                previous,
                current,
                geometry,
                style,
                strokeIds[index],
                strokeArcLengths[index - 1],
                strokeArcLengths[index] - strokeArcLengths[index - 1]);
        }

        return stamps;
    }

    private static void AddLinearSegment(
        ICollection<WipeBrushStamp> stamps,
        WipePathSample previous,
        WipePathSample current,
        WipeMaskGeometry geometry)
    {
        if ((!HasDrawableBrushDimensions(previous) &&
             !HasDrawableBrushDimensions(current)) ||
            (previous.Strength <= 0 && current.Strength <= 0))
        {
            return;
        }

        var previousPosition = new Vector2(previous.X, previous.Y);
        var currentPosition = new Vector2(current.X, current.Y);
        var distance = geometry.MeasureInShortSideUnits(
            previousPosition,
            currentPosition);
        var representativeSize = MathF.Max(
            (ResolveSpacingSize(previous, geometry) +
             ResolveSpacingSize(current, geometry)) * 0.5f,
            MinimumSpacing);
        var maximumSpacing = MathF.Max(
            representativeSize * SpacingRatio,
            MinimumSpacing);
        var stepCount = ResolveStepCount(distance, maximumSpacing);

        for (long step = 1; step <= stepCount; step++)
        {
            var amount = step / (float)stepCount;
            AddStamp(
                stamps,
                Interpolate(previous, current, amount),
                geometry);
        }
    }

    private static void AddShapedSegment(
        ICollection<WipeBrushStamp> stamps,
        WipePathSample? previousPrevious,
        WipePathSample previous,
        WipePathSample current,
        WipeMaskGeometry geometry,
        WipePathStyle style,
        int strokeId,
        float startArcLength,
        float segmentArcLength)
    {
        if ((!HasDrawableBrushDimensions(previous) &&
             !HasDrawableBrushDimensions(current)) ||
            (previous.Strength <= 0 && current.Strength <= 0))
        {
            return;
        }

        var curve = CreateCurve(
            previousPrevious,
            previous,
            current,
            geometry,
            style.Smoothing);
        var distance = EstimateCurveLength(curve, geometry);
        var representativeSize = MathF.Max(
            (ResolveSpacingSize(previous, geometry) +
             ResolveSpacingSize(current, geometry)) * 0.5f,
            MinimumSpacing);
        var maximumSpacing = MathF.Max(
            representativeSize * style.SpacingRatio,
            MinimumSpacing);
        var stepCount = ResolveStepCount(distance, maximumSpacing);

        if (previous.AccumulationGroup != current.AccumulationGroup)
        {
            var boundaryTangent = ResolveRotationTangentAtPassBoundary(
                previousPrevious,
                previous,
                current,
                style,
                curve.EvaluateDerivative(0));
            AddStamp(
                stamps,
                previous with
                {
                    AccumulationGroup = current.AccumulationGroup,
                },
                geometry,
                style,
                boundaryTangent);
        }

        for (long step = 1; step <= stepCount; step++)
        {
            var amount = step / (float)stepCount;
            var position = curve.Evaluate(amount);
            var tangent = curve.EvaluateDerivative(amount);
            if (style.Jitter > 0)
            {
                position = ApplyJitter(
                    position,
                    tangent,
                    geometry,
                    style,
                    strokeId,
                    startArcLength + segmentArcLength * amount);
            }

            var rotationTangent = ResolveRotationTangentAtPassBoundary(
                previousPrevious,
                previous,
                current,
                style,
                tangent);

            var sample = Interpolate(previous, current, amount) with
            {
                X = position.X,
                Y = position.Y,
            };
            AddStamp(stamps, sample, geometry, style, rotationTangent);
        }
    }

    private static Vector2 ResolveRotationTangentAtPassBoundary(
        WipePathSample? previousPrevious,
        WipePathSample previous,
        WipePathSample current,
        WipePathStyle style,
        Vector2 tangent)
    {
        if (previous.AccumulationGroup == current.AccumulationGroup ||
            style.BrushShape is not
                (GlassWipeBrushShape.Rectangle or
                    GlassWipeBrushShape.Hand or
                    GlassWipeBrushShape.ShoePrint or
                    GlassWipeBrushShape.UserImage))
        {
            return tangent;
        }

        var previousTangent = previousPrevious is { } prior &&
            prior.AccumulationGroup == previous.AccumulationGroup
                ? new Vector2(previous.X - prior.X, previous.Y - prior.Y)
                : new Vector2(style.InitialTangentX, style.InitialTangentY);
        if (!float.IsFinite(previousTangent.X) ||
            !float.IsFinite(previousTangent.Y) ||
            previousTangent.LengthSquared() <= DirectionEpsilon * DirectionEpsilon)
        {
            return tangent;
        }

        var previousDisplayTangent = ApplyReturnTangentCorrection(
            style.BrushShape,
            previous.AccumulationGroup,
            previousTangent);
        return ApplyReturnTangentCorrection(
            style.BrushShape,
            current.AccumulationGroup,
            previousDisplayTangent);
    }

    private static BrushCurve CreateCurve(
        WipePathSample? previousPrevious,
        WipePathSample previous,
        WipePathSample current,
        WipeMaskGeometry geometry,
        float smoothing)
    {
        var start = new Vector2(previous.X, previous.Y);
        var end = new Vector2(current.X, current.Y);
        var outgoing = end - start;
        var linearControl1 = start + outgoing / 3;
        var linearControl2 = start + outgoing * (2f / 3f);

        if (previousPrevious is null || smoothing <= 0)
        {
            return new BrushCurve(
                start,
                linearControl1,
                linearControl2,
                end);
        }

        var previousPosition = new Vector2(
            previousPrevious.Value.X,
            previousPrevious.Value.Y);
        var incomingShort = geometry.ToShortSideVector(start - previousPosition);
        var outgoingShort = geometry.ToShortSideVector(outgoing);
        var incomingLength = incomingShort.Length();
        var outgoingLength = outgoingShort.Length();
        if (incomingLength <= DirectionEpsilon ||
            outgoingLength <= DirectionEpsilon)
        {
            return new BrushCurve(
                start,
                linearControl1,
                linearControl2,
                end);
        }

        var directionAgreement = Vector2.Dot(
            incomingShort / incomingLength,
            outgoingShort / outgoingLength);
        if (directionAgreement <= -0.5f)
        {
            return new BrushCurve(
                start,
                linearControl1,
                linearControl2,
                end);
        }

        var smoothHandleShort =
            incomingShort / incomingLength *
            (MathF.Min(incomingLength, outgoingLength) / 3);
        var smoothControl1 = start +
            geometry.FromShortSideVector(smoothHandleShort);
        return new BrushCurve(
            start,
            Vector2.Lerp(linearControl1, smoothControl1, smoothing),
            linearControl2,
            end);
    }

    private static float EstimateCurveLength(
        BrushCurve curve,
        WipeMaskGeometry geometry)
    {
        var length = 0f;
        var previous = curve.Start;
        for (var index = 1; index <= CurveLengthSamples; index++)
        {
            var current = curve.Evaluate(index / (float)CurveLengthSamples);
            length += geometry.MeasureInShortSideUnits(previous, current);
            previous = current;
        }

        return length;
    }

    private static long ResolveStepCount(float distance, float maximumSpacing)
    {
        if (!float.IsFinite(distance) || distance <= 0 ||
            !float.IsFinite(maximumSpacing) || maximumSpacing <= 0)
        {
            return 1;
        }

        var required = Math.Ceiling((double)distance / maximumSpacing);
        return required >= long.MaxValue
            ? long.MaxValue
            : Math.Max(1, (long)required);
    }

    private static Vector2 ApplyJitter(
        Vector2 position,
        Vector2 tangent,
        WipeMaskGeometry geometry,
        WipePathStyle style,
        int strokeId,
        float arcLength)
    {
        var tangentShort = geometry.ToShortSideVector(tangent);
        var tangentLength = tangentShort.Length();
        if (tangentLength <= DirectionEpsilon)
        {
            return position;
        }

        var perpendicular = new Vector2(
            -tangentShort.Y,
            tangentShort.X) / tangentLength;
        // ストローク開始からの固定弧長座標により、AppendとRebuildを一致させる。
        var noisePosition = MathF.Max(0, arcLength) / NoiseWavelength;
        var lattice = (int)MathF.Floor(noisePosition);
        var blend = noisePosition - lattice;
        blend = blend * blend * (3 - 2 * blend);
        var noise = Lerp(
            HashNoise(style.Seed, strokeId, lattice),
            HashNoise(style.Seed, strokeId, lattice + 1),
            blend);
        return position + geometry.FromShortSideVector(
            perpendicular * (noise * style.Jitter));
    }

    private static (int[] StrokeIds, float[] ArcLengths) BuildStrokeMetadata(
        IReadOnlyList<WipePathSample> samples,
        WipeMaskGeometry geometry)
    {
        var strokeIds = new int[samples.Count];
        var arcLengths = new float[samples.Count];
        var strokeId = -1;

        for (var index = 0; index < samples.Count; index++)
        {
            var current = samples[index];
            if (!current.IsContacting)
            {
                strokeIds[index] = strokeId;
                continue;
            }

            if (index == 0 || !samples[index - 1].IsContacting)
            {
                strokeId++;
                arcLengths[index] = 0;
            }
            else
            {
                var previous = samples[index - 1];
                arcLengths[index] = arcLengths[index - 1] +
                    geometry.MeasureInShortSideUnits(
                        new Vector2(previous.X, previous.Y),
                        new Vector2(current.X, current.Y));
            }

            strokeIds[index] = strokeId;
        }

        return (strokeIds, arcLengths);
    }

    private static float HashNoise(int seed, int strokeId, int lattice)
    {
        unchecked
        {
            var hash = 2166136261u;
            hash = (hash ^ (uint)seed) * 16777619u;
            hash = (hash ^ (uint)strokeId) * 16777619u;
            hash = (hash ^ (uint)lattice) * 16777619u;
            hash ^= hash >> 16;
            hash *= 0x7feb352du;
            hash ^= hash >> 15;
            hash *= 0x846ca68bu;
            hash ^= hash >> 16;
            return hash / (float)uint.MaxValue * 2 - 1;
        }
    }

    private static void AddStamp(
        ICollection<WipeBrushStamp> stamps,
        WipePathSample sample,
        WipeMaskGeometry geometry,
        WipePathStyle style = default,
        Vector2? tangent = null,
        float? continuousRotation = null)
    {
        var alpha = ClampFinite(sample.Contact * sample.Strength, 0, 1, 0);
        var radii = ResolveMaskRadii(sample, geometry, style.BrushShape);
        if (alpha <= 0 || radii.X <= 0 || radii.Y <= 0)
        {
            return;
        }

        var rotationRadians = continuousRotation ?? (ResolveShapeBaseRotationRadians(style.BrushShape) +
            (float.IsFinite(sample.RotationRadians)
            ? sample.RotationRadians
            : 0));
        if (continuousRotation is null && style.RotationFollow > 0 && tangent is not null)
        {
            var correctedTangent = ApplyReturnTangentCorrection(
                style.BrushShape,
                sample.AccumulationGroup,
                tangent.Value);
            var tangentShort = geometry.ToShortSideVector(correctedTangent);

            if (tangentShort.LengthSquared() > DirectionEpsilon * DirectionEpsilon)
            {
                rotationRadians +=
                    MathF.Atan2(tangentShort.Y, tangentShort.X) *
                    style.RotationFollow;
            }
        }

        var center = new Vector2(
            sample.X * WipeMaskRenderer.MaskSize,
            sample.Y * WipeMaskRenderer.MaskSize);
        stamps.Add(new WipeBrushStamp(
            center,
            radii.X,
            radii.Y,
            geometry.CreateBrushTransform(center, rotationRadians),
            alpha,
            style.Softness,
            Math.Max(0, sample.AccumulationGroup),
            style.BrushShape,
            style.BrushMirror,
            style.UserBrushId,
            style.UserBrushRevision,
            ResolveUserBrushDimension(
                style.BrushShape,
                style.UserBrushPixelWidth),
            ResolveUserBrushDimension(
                style.BrushShape,
                style.UserBrushPixelHeight)));
    }

    private static int ResolveUserBrushDimension(
        GlassWipeBrushShape shape,
        int dimension)
    {
        if (shape != GlassWipeBrushShape.UserImage ||
            dimension < 1)
        {
            return 0;
        }

        return Math.Clamp(
            dimension,
            0,
            UserBrushLibrary.MaximumDimension);
    }

    internal static bool RequiresReturnTangentCorrection(
        GlassWipeBrushShape shape,
        int accumulationGroup) =>
        accumulationGroup >= 0 &&
        (accumulationGroup & 1) == 1 &&
        shape is GlassWipeBrushShape.Rectangle or
            GlassWipeBrushShape.Hand or
            GlassWipeBrushShape.ShoePrint or
            GlassWipeBrushShape.UserImage;

    private static Vector2 ApplyReturnTangentCorrection(
        GlassWipeBrushShape shape,
        int accumulationGroup,
        Vector2 tangent) =>
        RequiresReturnTangentCorrection(shape, accumulationGroup)
            ? -tangent
            : tangent;

    internal static float ResolveShapeBaseRotationRadians(
        GlassWipeBrushShape shape) =>
        shape == GlassWipeBrushShape.ShoePrint
            ? -MathF.PI / 2
            : 0;

    private static Vector2? ResolveInitialTangent(WipePathStyle style)
    {
        if (style.RotationFollow <= 0)
        {
            return null;
        }

        var tangent = new Vector2(
            style.InitialTangentX,
            style.InitialTangentY);
        return float.IsFinite(tangent.X) &&
            float.IsFinite(tangent.Y) &&
            tangent.LengthSquared() > DirectionEpsilon * DirectionEpsilon
                ? tangent
                : null;
    }

    private static WipePathSample Interpolate(
        WipePathSample from,
        WipePathSample to,
        float amount) =>
        new(
            to.Frame,
            Lerp(from.X, to.X, amount),
            Lerp(from.Y, to.Y, amount),
            Lerp(from.Contact, to.Contact, amount),
            Lerp(from.Size, to.Size, amount),
            Lerp(from.Strength, to.Strength, amount),
            Lerp(from.AspectRatio, to.AspectRatio, amount),
            LerpAngle(
                from.RotationRadians,
                to.RotationRadians,
                amount),
            Math.Max(0, to.AccumulationGroup),
            LerpNullable(
                from.BrushWidthPixels,
                to.BrushWidthPixels,
                amount),
            LerpNullable(
                from.BrushHeightPixels,
                to.BrushHeightPixels,
                amount));

    private static float Lerp(float from, float to, float amount) =>
        from + (to - from) * amount;

    private static float? LerpNullable(
        float? from,
        float? to,
        float amount) =>
        from is { } fromValue && to is { } toValue
            ? Lerp(fromValue, toValue, amount)
            : null;

    private static bool HasDrawableBrushDimensions(WipePathSample sample)
    {
        if (sample.BrushWidthPixels.HasValue ||
            sample.BrushHeightPixels.HasValue)
        {
            return sample.BrushWidthPixels is > 0 &&
                sample.BrushHeightPixels is > 0;
        }

        return sample.Size > 0;
    }

    private static float ResolveSpacingSize(
        WipePathSample sample,
        WipeMaskGeometry geometry)
    {
        if (sample.BrushWidthPixels is { } width &&
            sample.BrushHeightPixels is { } height)
        {
            var pixelWidth = SanitizeDimension(geometry.PixelWidth);
            var pixelHeight = SanitizeDimension(geometry.PixelHeight);
            var normalizedSize = geometry.ToShortSideVector(
                new Vector2(
                    ClampFinite(
                        width,
                        0,
                        WipeBrushPixelDimensions.Maximum,
                        0) / pixelWidth,
                    ClampFinite(
                        height,
                        0,
                        WipeBrushPixelDimensions.Maximum,
                        0) / pixelHeight));
            return MathF.Min(
                MathF.Abs(normalizedSize.X),
                MathF.Abs(normalizedSize.Y));
        }

        return ClampFinite(sample.Size, 0, 1, 0);
    }

    private static Vector2 ResolveMaskRadii(
        WipePathSample sample,
        WipeMaskGeometry geometry,
        GlassWipeBrushShape shape)
    {
        if (sample.BrushWidthPixels is { } width &&
            sample.BrushHeightPixels is { } height)
        {
            var pixelWidth = SanitizeDimension(geometry.PixelWidth);
            var pixelHeight = SanitizeDimension(geometry.PixelHeight);
            return new Vector2(
                WipeMaskRenderer.MaskSize *
                    ClampFinite(
                        width,
                        0,
                        WipeBrushPixelDimensions.Maximum,
                        0) /
                    pixelWidth *
                    0.5f,
                WipeMaskRenderer.MaskSize *
                    ClampFinite(
                        height,
                        0,
                        WipeBrushPixelDimensions.Maximum,
                        0) /
                    pixelHeight *
                    0.5f);
        }

        var size = ClampFinite(sample.Size, 0, 1, 0);
        var aspectRatio = ClampFinite(sample.AspectRatio, 0.25f, 4, 1);
        var canonicalHorizontalScale =
            shape == GlassWipeBrushShape.ShoePrint
                ? new Vector2(1, aspectRatio)
                : new Vector2(aspectRatio, 1);
        var radius = WipeMaskRenderer.MaskSize * size * 0.5f;
        return new Vector2(
            radius * geometry.ScaleX * canonicalHorizontalScale.X,
            radius * geometry.ScaleY * canonicalHorizontalScale.Y);
    }

    private static float SanitizeDimension(float value) =>
        float.IsFinite(value) && value > 0 ? value : 1;

    private static float LerpAngle(float from, float to, float amount)
    {
        var difference = MathF.IEEERemainder(to - from, MathF.Tau);
        return from + difference * amount;
    }

    private static float ClampFinite(
        float value,
        float minimum,
        float maximum,
        float fallback) =>
        float.IsFinite(value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;

    private readonly record struct BrushCurve(
        Vector2 Start,
        Vector2 Control1,
        Vector2 Control2,
        Vector2 End)
    {
        public Vector2 Evaluate(float amount)
        {
            var inverse = 1 - amount;
            return inverse * inverse * inverse * Start +
                   3 * inverse * inverse * amount * Control1 +
                   3 * inverse * amount * amount * Control2 +
                   amount * amount * amount * End;
        }

        public Vector2 EvaluateDerivative(float amount)
        {
            var inverse = 1 - amount;
            return 3 * inverse * inverse * (Control1 - Start) +
                   6 * inverse * amount * (Control2 - Control1) +
                   3 * amount * amount * (End - Control2);
        }
    }

    /// <summary>
    /// バッチ境界をまたいで直前点、弧長、接線を維持するスタンプ生成状態です。
    /// </summary>
    internal sealed class StreamState
    {
        private readonly int _strokeIdBase;
        private WipePathSample? _previousPrevious;
        private WipePathSample? _previous;
        private float _arcLength;
        private int _contactRun = -1;
        private ContinuousState? _continuousState;

        public StreamState(int strokeIdBase)
        {
            _strokeIdBase = Math.Max(0, strokeIdBase);
        }

        public void Append(
            WipePathSample current,
            WipeMaskGeometry geometry,
            WipePathStyle style,
            ICollection<WipeBrushStamp> stamps)
        {
            ArgumentNullException.ThrowIfNull(stamps);
            style = style.Sanitize();
            if (style.ContinuousWipe)
            {
                (_continuousState ??= new ContinuousState(_strokeIdBase))
                    .Append(current, geometry, style, stamps);
                return;
            }


            if (_previous is null)
            {
                if (current.IsContacting)
                {
                    _contactRun = 0;
                    AddStamp(
                        stamps,
                        current,
                        geometry,
                        style,
                        ResolveInitialTangent(style));
                }

                _previous = current;
                return;
            }

            var previous = _previous.Value;
            if (!current.IsContacting)
            {
                _previousPrevious = previous;
                _previous = current;
                return;
            }

            if (!previous.IsContacting)
            {
                _contactRun++;
                _arcLength = 0;
                AddStamp(stamps, current, geometry, style);
                _previousPrevious = previous;
                _previous = current;
                return;
            }

            var measuredSegmentArcLength = geometry.MeasureInShortSideUnits(
                new Vector2(previous.X, previous.Y),
                new Vector2(current.X, current.Y));
            // 従来の全履歴方式は累積値を一度floatへ丸めた後、その差分を
            // Jitter座標へ渡していた。同じ丸め順を保ち、短尺結果を一致させる。
            var nextArcLength = _arcLength + measuredSegmentArcLength;
            var segmentArcLength = nextArcLength - _arcLength;
            if (style.IsDisabled)
            {
                AddLinearSegment(stamps, previous, current, geometry);
            }
            else
            {
                WipePathSample? previousPrevious =
                    _previousPrevious is { } prior && prior.IsContacting
                    ? prior
                    : null;
                AddShapedSegment(
                    stamps,
                    previousPrevious,
                    previous,
                    current,
                    geometry,
                    style,
                    _strokeIdBase + Math.Max(0, _contactRun),
                    _arcLength,
                    segmentArcLength);
            }

            _arcLength = nextArcLength;
            _previousPrevious = previous;
            _previous = current;
        }
    }
}
