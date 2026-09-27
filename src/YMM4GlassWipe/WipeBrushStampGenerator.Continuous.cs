// SPDX-License-Identifier: MPL-2.0

using System.Collections;
using System.Numerics;

namespace YMM4GlassWipe;

internal static partial class WipeBrushStampGenerator
{
    private static IReadOnlyList<WipeBrushStamp> GenerateContinuous(
        IReadOnlyList<WipePathSample> samples,
        int firstSampleIndex,
        WipeMaskGeometry geometry,
        WipePathStyle style)
    {
        var stamps = new List<WipeBrushStamp>();
        var state = new ContinuousState(0);
        var start = Math.Clamp(firstSampleIndex, 0, samples.Count);
        // 途中からの列挙でも先頭から姿勢だけを再構築し、再生順へ依存させない。
        for (var index = 0; index < samples.Count; index++)
        {
            state.Append(samples[index], geometry, style,
                index < start ? DiscardStamps.Instance : stamps);
        }
        return stamps;
    }

    /// <summary>
    /// 接触中の最終姿勢を保持し、位置・回転・大きさの変化を同じ規則で細分化します。
    /// GPU履歴や次フレームへ依存せず、先頭から同じ状態を再構築できます。
    /// </summary>
    private sealed class ContinuousState(int strokeIdBase)
    {
        private WipePathSample? _previous;
        private WipePathSample? _previousPrevious;
        private ContinuousPose? _pose;
        private float _followAngle;
        private float _arcLength;
        private int _contactRun = -1;

        public void Append(
            WipePathSample current,
            WipeMaskGeometry geometry,
            WipePathStyle style,
            ICollection<WipeBrushStamp> stamps)
        {
            if (!current.IsContacting || !float.IsFinite(current.Contact) ||
                !float.IsFinite(current.X) || !float.IsFinite(current.Y))
            {
                _previousPrevious = null;
                _previous = current with { Contact = 0 };
                _pose = null;
                _followAngle = 0;
                _arcLength = 0;
                return;
            }

            if (_previous is not { IsContacting: true } previous || _pose is not { } startPose)
            {
                _contactRun++;
                var tangent = _previous is null ? ResolveInitialTangent(style) : null;
                _followAngle = tangent is { } initial
                    ? ResolveFollowAngle(current, initial, geometry, style, 0)
                    : 0;
                var angle = ResolveShapeBaseRotationRadians(style.BrushShape) +
                    SafeManualAngle(current) + _followAngle * style.RotationFollow;
                var first = new ContinuousPose(current, NormalizeAngle(angle));
                Emit(first, geometry, style, stamps);
                _pose = first;
                _previousPrevious = null;
                _previous = current;
                return;
            }

            var curve = CreateCurve(_previousPrevious, previous, current, geometry, style.Smoothing);
            var nextArcLength = _arcLength + geometry.MeasureInShortSideUnits(
                new Vector2(previous.X, previous.Y), new Vector2(current.X, current.Y));
            var segmentLength = nextArcLength - _arcLength;
            var passBoundary = previous.AccumulationGroup != current.AccumulationGroup;
            // 折り返し境界は補正済みの表示方向を引き継ぎ、端点を両片道へ共有する。
            var nextFollowAngle = passBoundary ? _followAngle : ResolveFollowAngle(
                current, curve.EvaluateDerivative(1), geometry, style, _followAngle);
            var rotationDelta = NormalizeAngle(
                NormalizeAngle(SafeManualAngle(current) - SafeManualAngle(previous)) +
                NormalizeAngle(nextFollowAngle - _followAngle) * style.RotationFollow);
            if (passBoundary)
            {
                Emit(startPose with { Sample = startPose.Sample with { AccumulationGroup = current.AccumulationGroup } },
                    geometry, style, stamps);
            }

            var strokeId = Math.Max(0, strokeIdBase) + Math.Max(0, _contactRun);
            Vector2 Position(float amount)
            {
                var position = curve.Evaluate(amount);
                if (style.Jitter > 0)
                {
                    position = ApplyJitter(position, curve.EvaluateDerivative(amount), geometry,
                        style, strokeId, _arcLength + segmentLength * amount);
                }
                return position;
            }

            // 接線が変わる点でも、直前に描いた揺れ位置から途切れずにつなぐ。
            var startCorrection = new Vector2(startPose.Sample.X, startPose.Sample.Y) - Position(0);
            ContinuousPose Evaluate(float amount)
            {
                var sample = Interpolate(previous, current, amount);
                var position = Position(amount) + startCorrection * (1 - amount);
                return new ContinuousPose(sample with { X = position.X, Y = position.Y },
                    startPose.Rotation + rotationDelta * amount);
            }

            var to = Evaluate(1);
            if ((HasDrawableBrushDimensions(previous) ||
                 HasDrawableBrushDimensions(current)) &&
                (previous.Strength > 0 || current.Strength > 0))
            {
                var spacing = style.Quality switch
                {
                    GlassWipeQuality.Low => 2f,
                    GlassWipeQuality.High => 1f,
                    _ => 1.5f,
                };
                var bound = ResolveContinuousMovementBound(curve, previous, current,
                    geometry, style.BrushShape, rotationDelta, startCorrection);
                // 揺れの格子補間による位置変化も初期分割の目安へ含める。
                bound += style.Jitter * WipeMaskRenderer.MaskSize *
                    (3 * MathF.Abs(segmentLength) / NoiseWavelength);
                var steps = ResolveStepCount(bound, spacing);
                var last = startPose;
                var lastAmount = 0f;
                for (long step = 1; step <= steps; step++)
                {
                    var amount = step / (float)steps;
                    var next = step == steps ? to : Evaluate(amount);
                    RefineAndEmit(lastAmount, last, amount, next, 0);
                    last = next;
                    lastAmount = amount;
                }

                void RefineAndEmit(float firstAmount, ContinuousPose first,
                    float lastAmount, ContinuousPose last, int depth)
                {
                    // 接線由来の揺れは解析上限に含めにくいため、実際の輪郭移動も確認する。
                    var middleAmount = (firstAmount + lastAmount) * 0.5f;
                    if (style.Jitter > 0 && depth < 16 &&
                        middleAmount > firstAmount && middleAmount < lastAmount)
                    {
                        var middle = Evaluate(middleAmount);
                        if (MeasurePoseMovement(first, middle, geometry, style.BrushShape) +
                            MeasurePoseMovement(middle, last, geometry, style.BrushShape) > spacing)
                        {
                            RefineAndEmit(firstAmount, first, middleAmount, middle, depth + 1);
                            RefineAndEmit(middleAmount, middle, lastAmount, last, depth + 1);
                            return;
                        }
                    }
                    Emit(last, geometry, style, stamps);
                }
            }

            _followAngle = nextFollowAngle;
            _arcLength = nextArcLength;
            _pose = to with { Rotation = NormalizeAngle(to.Rotation) };
            _previousPrevious = previous;
            _previous = current;
        }

        private static void Emit(ContinuousPose pose, WipeMaskGeometry geometry,
            WipePathStyle style, ICollection<WipeBrushStamp> stamps) =>
            AddStamp(stamps, pose.Sample, geometry, style, continuousRotation: pose.Rotation);
    }

    private readonly record struct ContinuousPose(WipePathSample Sample, float Rotation);

    private static float ResolveFollowAngle(WipePathSample sample, Vector2 tangent,
        WipeMaskGeometry geometry, WipePathStyle style, float fallback)
    {
        var direction = geometry.ToShortSideVector(
            ApplyReturnTangentCorrection(style.BrushShape, sample.AccumulationGroup, tangent));
        return float.IsFinite(direction.X) && float.IsFinite(direction.Y) &&
            direction.LengthSquared() > DirectionEpsilon * DirectionEpsilon
            ? MathF.Atan2(direction.Y, direction.X)
            : fallback;
    }

    private static float SafeManualAngle(WipePathSample sample) =>
        float.IsFinite(sample.RotationRadians) ? sample.RotationRadians : 0;

    private static float NormalizeAngle(float angle) => MathF.IEEERemainder(angle, MathF.Tau);

    private static float ResolveContinuousMovementBound(BrushCurve curve,
        WipePathSample previous, WipePathSample current, WipeMaskGeometry geometry,
        GlassWipeBrushShape shape, float rotationDelta, Vector2 startCorrection)
    {
        var maskSize = WipeMaskRenderer.MaskSize;
        // 三次Bezierの微分は制御辺の凸結合。最大制御辺の3倍は単位区間の速度上限。
        var centerSpeed = 3 * MathF.Max(Vector2.Distance(curve.Start, curve.Control1),
            MathF.Max(Vector2.Distance(curve.Control1, curve.Control2),
                Vector2.Distance(curve.Control2, curve.End))) * maskSize;
        centerSpeed += startCorrection.Length() * maskSize;
        var firstRadii = ResolveMaskRadii(previous, geometry, shape);
        var lastRadii = ResolveMaskRadii(current, geometry, shape);
        var rotationSpeed = MathF.Max(
                firstRadii.Length(),
                lastRadii.Length()) *
            MathF.Abs(rotationDelta);
        var scaleSpeed = Vector2.Distance(firstRadii, lastRadii);
        return centerSpeed + rotationSpeed + scaleSpeed;
    }

    private static float MeasurePoseMovement(ContinuousPose first, ContinuousPose last,
        WipeMaskGeometry geometry, GlassWipeBrushShape shape)
    {
        var maximum = 0f;
        for (var index = 0; index < 4; index++)
        {
            var corner = new Vector2((index & 1) == 0 ? -1 : 1, (index & 2) == 0 ? -1 : 1);
            maximum = MathF.Max(maximum, Vector2.Distance(
                TransformCorner(first, corner), TransformCorner(last, corner)));
        }
        return maximum;

        Vector2 TransformCorner(ContinuousPose pose, Vector2 corner)
        {
            var sample = pose.Sample;
            var center = new Vector2(
                sample.X,
                sample.Y) * WipeMaskRenderer.MaskSize;
            var local = corner * ResolveMaskRadii(sample, geometry, shape);
            return Vector2.Transform(
                center + local,
                geometry.CreateBrushTransform(center, pose.Rotation));
        }
    }

    private sealed class DiscardStamps : ICollection<WipeBrushStamp>
    {
        public static readonly DiscardStamps Instance = new();
        public int Count => 0;
        public bool IsReadOnly => false;
        public void Add(WipeBrushStamp item) { }
        public void Clear() { }
        public bool Contains(WipeBrushStamp item) => false;
        public bool Remove(WipeBrushStamp item) => false;
        public void CopyTo(WipeBrushStamp[] array, int arrayIndex) { }
        public IEnumerator<WipeBrushStamp> GetEnumerator() => Enumerable.Empty<WipeBrushStamp>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
