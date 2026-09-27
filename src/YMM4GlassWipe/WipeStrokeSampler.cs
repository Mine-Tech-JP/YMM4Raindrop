// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace YMM4GlassWipe;

internal readonly record struct WipeStrokeFrameSample(
    int Frame,
    float X,
    float Y,
    float Contact);

internal static class WipeStrokeSampler
{
    internal static int[] GetStrokeIdBases(WipeStrokeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return GetStrokeIdBasesSanitized(document.Sanitize());
    }

    internal static int[] GetStrokeIdBasesSanitized(WipeStrokeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var result = new int[Math.Max(1, document.Strokes.Count)];
        var nextBase = 0;
        for (var lane = 0; lane < document.Strokes.Count; lane++)
        {
            result[lane] = nextBase;
            var wasContacting = false;
            foreach (var point in document.Strokes[lane].Points)
            {
                var isContacting = point.Contact > 0;
                if (isContacting && !wasContacting)
                {
                    nextBase++;
                }

                wasContacting = isContacting;
            }
        }

        return result;
    }

    public static WipeStrokeFrameSample[] Expand(
        WipeStrokeDocument document,
        int currentFrame,
        int itemLength) =>
        Expand(
            document,
            currentFrame,
            itemLength,
            WipeMaskGeometry.Create(
                WipeStrokeDocument.CanvasPixelWidth,
                WipeStrokeDocument.CanvasPixelHeight));

    public static WipeStrokeFrameSample[] Expand(
        WipeStrokeDocument document,
        int currentFrame,
        int itemLength,
        WipeMaskGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.DataSchemaVersion != WipeStrokeDocument.CurrentVersion)
        {
            return [];
        }

        var sanitized = document.Sanitize();
        var clampedLength = Math.Max(0, itemLength);
        var clampedCurrentFrame = Math.Clamp(
            currentFrame,
            0,
            clampedLength);
        var result = new List<WipeStrokeFrameSample>();
        var hasPreviousStroke = false;

        foreach (var stroke in sanitized.Strokes)
        {
            var strokeSamples = ExpandStroke(
                stroke,
                clampedCurrentFrame,
                clampedLength,
                geometry);
            if (strokeSamples.Count == 0)
            {
                continue;
            }

            if (hasPreviousStroke)
            {
                var first = strokeSamples[0];
                result.Add(first with { Contact = 0 });
            }

            result.AddRange(strokeSamples);
            hasPreviousStroke = true;
        }

        return result.ToArray();
    }

    /// <summary>
    /// 複数Strokeをフレーム順、同一フレームでは保存順で列挙します。
    /// 全履歴配列を作らず、各Strokeの制御点と列挙カーソルだけを保持します。
    /// </summary>
    internal static IEnumerable<(int LaneId, WipeStrokeFrameSample Sample)> EnumerateMerged(
        WipeStrokeDocument document,
        int firstFrame,
        int lastFrame,
        int itemLength) =>
        EnumerateMerged(
            document,
            firstFrame,
            lastFrame,
            itemLength,
            WipeMaskGeometry.Create(
                WipeStrokeDocument.CanvasPixelWidth,
                WipeStrokeDocument.CanvasPixelHeight));

    internal static IEnumerable<(int LaneId, WipeStrokeFrameSample Sample)> EnumerateMerged(
        WipeStrokeDocument document,
        int firstFrame,
        int lastFrame,
        int itemLength,
        WipeMaskGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(document);
        return EnumerateMergedSanitized(
            document.Sanitize(),
            firstFrame,
            lastFrame,
            itemLength,
            geometry);
    }

    internal static IEnumerable<(int LaneId, WipeStrokeFrameSample Sample)>
        EnumerateMergedSanitized(
            WipeStrokeDocument document,
            int firstFrame,
            int lastFrame,
            int itemLength,
            WipeMaskGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(document);
        var clampedLength = Math.Max(0, itemLength);
        var first = Math.Clamp(firstFrame, 0, clampedLength);
        var last = Math.Clamp(lastFrame, 0, clampedLength);
        if (last < first)
        {
            yield break;
        }

        var queue = new PriorityQueue<StrokeEnumeratorNode, (int Frame, int Lane, int Sequence)>();
        for (var lane = 0; lane < document.Strokes.Count; lane++)
        {
            var enumerator = EnumerateStrokeRange(
                    document.Strokes[lane],
                    first,
                    last,
                    clampedLength,
                    geometry)
                .GetEnumerator();
            if (!enumerator.MoveNext())
            {
                enumerator.Dispose();
                continue;
            }

            var node = new StrokeEnumeratorNode(lane, enumerator);
            queue.Enqueue(node, (node.Current.Frame, lane, node.Sequence));
        }

        try
        {
            while (queue.TryDequeue(out var node, out _))
            {
                yield return (node.LaneId, node.Current);
                if (node.MoveNext())
                {
                    queue.Enqueue(
                        node,
                        (node.Current.Frame, node.LaneId, node.Sequence));
                }
                else
                {
                    node.Dispose();
                }
            }
        }
        finally
        {
            while (queue.TryDequeue(out var remaining, out _))
            {
                remaining.Dispose();
            }
        }
    }

    private static IEnumerable<WipeStrokeFrameSample> EnumerateStrokeRange(
        WipeStroke stroke,
        int firstFrame,
        int lastFrame,
        int itemLength,
        WipeMaskGeometry geometry)
    {
        if (stroke.Points.Count == 0)
        {
            yield break;
        }

        var frames = new int[stroke.Points.Count];
        for (var index = 0; index < stroke.Points.Count; index++)
        {
            var frame = PercentToFrame(stroke.Points[index].TimelinePercent, itemLength);
            frames[index] = index == 0
                ? frame
                : Math.Max(frames[index - 1], frame);
        }

        if (stroke.Points.Count == 1)
        {
            if (frames[0] >= firstFrame && frames[0] <= lastFrame)
            {
                yield return CreateSample(frames[0], stroke.Points[0], geometry);
            }

            yield break;
        }

        var hasAnySample = false;
        for (var segmentIndex = 0;
             segmentIndex < stroke.Points.Count - 1;
             segmentIndex++)
        {
            var startFrame = frames[segmentIndex];
            var endFrame = frames[segmentIndex + 1];
            if (startFrame > lastFrame)
            {
                yield break;
            }

            if (endFrame <= startFrame)
            {
                if (!hasAnySample)
                {
                    if (startFrame >= firstFrame && startFrame <= lastFrame)
                    {
                        yield return CreateSample(
                            startFrame,
                            stroke.Points[segmentIndex],
                            geometry);
                    }

                    hasAnySample = true;
                }

                if (startFrame >= firstFrame && startFrame <= lastFrame)
                {
                    yield return CreateSample(
                        startFrame,
                        stroke.Points[segmentIndex + 1],
                        geometry);
                }

                hasAnySample = true;
                continue;
            }

            var logicalFirstFrame = segmentIndex == 0
                ? startFrame
                : startFrame + 1;
            var rangeFirstFrame = Math.Max(firstFrame, logicalFirstFrame);
            var rangeLastFrame = Math.Min(lastFrame, endFrame);
            if (rangeFirstFrame <= rangeLastFrame)
            {
                for (var frame = rangeFirstFrame; ; frame++)
                {
                    var amount = (frame - startFrame) / (float)(endFrame - startFrame);
                    yield return EvaluateSegment(
                        stroke.Points,
                        segmentIndex,
                        frame,
                        amount,
                        geometry);
                    if (frame == rangeLastFrame)
                    {
                        break;
                    }
                }
            }

            hasAnySample = true;
        }
    }

    private sealed class StrokeEnumeratorNode : IDisposable
    {
        private readonly IEnumerator<WipeStrokeFrameSample> _enumerator;

        public StrokeEnumeratorNode(
            int laneId,
            IEnumerator<WipeStrokeFrameSample> enumerator)
        {
            LaneId = laneId;
            _enumerator = enumerator;
        }

        public int LaneId { get; }

        public int Sequence { get; private set; }

        public WipeStrokeFrameSample Current => _enumerator.Current;

        public bool MoveNext()
        {
            Sequence++;
            return _enumerator.MoveNext();
        }

        public void Dispose() => _enumerator.Dispose();
    }

    private static List<WipeStrokeFrameSample> ExpandStroke(
        WipeStroke stroke,
        int currentFrame,
        int itemLength,
        WipeMaskGeometry geometry)
    {
        var result = new List<WipeStrokeFrameSample>();
        if (stroke.Points.Count == 0)
        {
            return result;
        }

        var frames = new int[stroke.Points.Count];
        for (var index = 0; index < stroke.Points.Count; index++)
        {
            var frame = PercentToFrame(
                stroke.Points[index].TimelinePercent,
                itemLength);
            frames[index] = index == 0
                ? frame
                : Math.Max(frames[index - 1], frame);
        }

        if (stroke.Points.Count == 1)
        {
            if (frames[0] <= currentFrame)
            {
                result.Add(CreateSample(frames[0], stroke.Points[0], geometry));
            }

            return result;
        }

        for (var segmentIndex = 0;
             segmentIndex < stroke.Points.Count - 1;
             segmentIndex++)
        {
            var startFrame = frames[segmentIndex];
            var endFrame = frames[segmentIndex + 1];
            if (startFrame > currentFrame)
            {
                break;
            }

            if (endFrame <= startFrame)
            {
                if (result.Count == 0)
                {
                    result.Add(CreateSample(
                        startFrame,
                        stroke.Points[segmentIndex],
                        geometry));
                }

                result.Add(CreateSample(
                    startFrame,
                    stroke.Points[segmentIndex + 1],
                    geometry));
                continue;
            }

            var lastFrame = Math.Min(currentFrame, endFrame);
            var firstFrame = segmentIndex == 0
                ? startFrame
                : startFrame + 1;
            for (var frame = firstFrame; frame <= lastFrame; frame++)
            {
                var amount = (frame - startFrame) /
                    (float)(endFrame - startFrame);
                result.Add(EvaluateSegment(
                    stroke.Points,
                    segmentIndex,
                    frame,
                    amount,
                    geometry));
            }

            if (currentFrame < endFrame)
            {
                break;
            }
        }

        return result;
    }

    private static WipeStrokeFrameSample EvaluateSegment(
        IReadOnlyList<WipeStrokePoint> points,
        int segmentIndex,
        int frame,
        float amount,
        WipeMaskGeometry geometry)
    {
        var point1 = ToVector(points[segmentIndex]);
        var point2 = ToVector(points[segmentIndex + 1]);
        var point0 = segmentIndex > 0
            ? ToVector(points[segmentIndex - 1])
            : point1 * 2 - point2;
        var point3 = segmentIndex + 2 < points.Count
            ? ToVector(points[segmentIndex + 2])
            : point2 * 2 - point1;
        var position = CentripetalCatmullRom.Evaluate(
            point0,
            point1,
            point2,
            point3,
            amount);
        var contact = Lerp(
            (float)points[segmentIndex].Contact / 100,
            (float)points[segmentIndex + 1].Contact / 100,
            amount);
        var normalizedPosition = WipeStrokeCoordinateMapper.ToNormalizedUv(
            position,
            geometry);
        return new WipeStrokeFrameSample(
            frame,
            normalizedPosition.X,
            normalizedPosition.Y,
            ClampUnit(contact));
    }

    private static WipeStrokeFrameSample CreateSample(
        int frame,
        WipeStrokePoint point,
        WipeMaskGeometry geometry)
    {
        var position = WipeStrokeCoordinateMapper.ToNormalizedUv(
            ToVector(point),
            geometry);
        return new(
            frame,
            position.X,
            position.Y,
            ClampUnit((float)point.Contact / 100));
    }

    private static int PercentToFrame(double percent, int itemLength) =>
        Math.Clamp(
            (int)Math.Round(
                percent / 100 * itemLength,
                MidpointRounding.AwayFromZero),
            0,
            itemLength);

    private static Vector2 ToVector(WipeStrokePoint point) =>
        new(
            ClampCanvasCoordinate(point.X, WipeStrokeDocument.CanvasPixelWidth),
            ClampCanvasCoordinate(point.Y, WipeStrokeDocument.CanvasPixelHeight));

    private static float ClampCanvasCoordinate(double value, int maximum) =>
        double.IsFinite(value)
            ? (float)Math.Clamp(value, 0, maximum)
            : 0;

    private static float Lerp(float from, float to, float amount) =>
        from + (to - from) * Math.Clamp(amount, 0, 1);

    private static float ClampUnit(float value) =>
        float.IsFinite(value)
            ? Math.Clamp(value, 0, 1)
            : 0;
}
