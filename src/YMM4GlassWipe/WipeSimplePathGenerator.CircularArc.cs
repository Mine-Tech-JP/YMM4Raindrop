// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace YMM4GlassWipe;

internal static partial class WipeSimplePathGenerator
{
    /// <summary>
    /// 始点、円弧上の経由点、終点から、既存の到達時刻を維持した円弧を評価します。
    /// </summary>
    private static Vector2 EvaluateCircularArcPath(
        GlassWipeSimplePattern pattern,
        IReadOnlyList<Vector2> points,
        Vector2 start,
        Vector2 control,
        Vector2 end,
        int passCount,
        Vector2 returnOffset,
        float amount,
        WipeMaskGeometry geometry) =>
        pattern switch
        {
            GlassWipeSimplePattern.GentleArcOnce =>
                EvaluateThreePointCircularArc(
                    start,
                    control,
                    end,
                    amount,
                    geometry),
            GlassWipeSimplePattern.ArcRoundTrips =>
                EvaluateArcRoundTripsCircularArc(
                    start,
                    control,
                    end,
                    passCount,
                    amount,
                    geometry),
            GlassWipeSimplePattern.OffsetRoundTrips =>
                EvaluateOffsetRoundTripsCircularArc(
                    points,
                    control,
                    returnOffset,
                    amount,
                    geometry),
            _ => Evaluate(points, GlassWipePathInterpolation.Linear, amount),
        };

    private static Vector2 EvaluateArcRoundTripsCircularArc(
        Vector2 start,
        Vector2 control,
        Vector2 end,
        int passCount,
        float amount,
        WipeMaskGeometry geometry)
    {
        if (!TryCreateCircularArc(
                start,
                control,
                end,
                geometry,
                allowOutside: false,
                out var arc))
        {
            return EvaluateArcRoundTripCurve(
                start,
                control,
                end,
                passCount,
                amount);
        }

        amount = float.IsFinite(amount) ? Math.Clamp(amount, 0, 1) : 0;
        var scaledAmount = amount * passCount;
        var passIndex = Math.Min((int)scaledAmount, passCount - 1);
        var passAmount = scaledAmount - passIndex;

        return EvaluateCircularArc(
            arc,
            passIndex % 2 == 0 ? passAmount : 1 - passAmount,
            allowOutside: false);
    }

    private static Vector2 EvaluateOffsetRoundTripsCircularArc(
        IReadOnlyList<Vector2> points,
        Vector2 control,
        Vector2 returnOffset,
        float amount,
        WipeMaskGeometry geometry)
    {
        if (points.Count < 2)
        {
            return Evaluate(
                points,
                GlassWipePathInterpolation.Smooth,
                amount,
                allowOutside: true);
        }

        amount = float.IsFinite(amount) ? Math.Clamp(amount, 0, 1) : 0;
        var scaledAmount = amount * (points.Count - 1);
        var segmentIndex = Math.Min(
            (int)scaledAmount,
            points.Count - 2);
        var segmentAmount = scaledAmount - segmentIndex;
        var controlOffset = returnOffset * (segmentIndex * 0.5f);
        var segmentControl = Offset(control, controlOffset);

        return EvaluateThreePointCircularArc(
            points[segmentIndex],
            segmentControl,
            points[segmentIndex + 1],
            segmentAmount,
            geometry,
            allowOutside: true);
    }

    private static Vector2 EvaluateThreePointCircularArc(
        Vector2 start,
        Vector2 control,
        Vector2 end,
        float amount,
        WipeMaskGeometry geometry,
        bool allowOutside = false)
    {
        amount = float.IsFinite(amount) ? Math.Clamp(amount, 0, 1) : 0;
        if (!TryCreateCircularArc(
                start,
                control,
                end,
                geometry,
                allowOutside,
                out var arc))
        {
            return EvaluateThreePointCurve(
                start,
                control,
                end,
                amount,
                allowOutside);
        }

        return EvaluateCircularArc(arc, amount, allowOutside);
    }

    private static Vector2 EvaluateCircularArc(
        CircularArc arc,
        float amount,
        bool allowOutside)
    {
        amount = float.IsFinite(amount) ? Math.Clamp(amount, 0, 1) : 0;
        var angle = amount < 0.5f
            ? arc.StartAngle + arc.FirstSweep * amount * 2
            : arc.StartAngle + arc.FirstSweep +
                arc.SecondSweep * (amount - 0.5f) * 2;
        var physicalPoint = new Vector2(
            arc.Center.X + arc.Radius * (float)Math.Cos(angle),
            arc.Center.Y + arc.Radius * (float)Math.Sin(angle));
        var position = arc.Geometry.FromShortSideVector(physicalPoint);
        return allowOutside ? ClampGeneratedPath(position) : ClampUnit(position);
    }

    private static bool TryCreateCircularArc(
        Vector2 start,
        Vector2 control,
        Vector2 end,
        WipeMaskGeometry geometry,
        bool allowOutside,
        out CircularArc arc)
    {
        arc = default;
        var physicalStart = geometry.ToShortSideVector(start);
        var physicalControl = geometry.ToShortSideVector(control);
        var physicalEnd = geometry.ToShortSideVector(end);
        if (!IsFinite(physicalStart) ||
            !IsFinite(physicalControl) ||
            !IsFinite(physicalEnd))
        {
            return false;
        }

        var maximumEdgeSquared = MathF.Max(
            Vector2.DistanceSquared(physicalStart, physicalControl),
            MathF.Max(
                Vector2.DistanceSquared(physicalControl, physicalEnd),
                Vector2.DistanceSquared(physicalEnd, physicalStart)));
        if (!float.IsFinite(maximumEdgeSquared) ||
            maximumEdgeSquared <= CircularArcEpsilon * CircularArcEpsilon)
        {
            return false;
        }

        var startToControl = physicalControl - physicalStart;
        var startToEnd = physicalEnd - physicalStart;
        var cross = (double)startToControl.X * startToEnd.Y -
            (double)startToControl.Y * startToEnd.X;
        if (!double.IsFinite(cross) ||
            Math.Abs(cross) <= CircularArcEpsilon * maximumEdgeSquared)
        {
            return false;
        }

        var ax = (double)physicalStart.X;
        var ay = physicalStart.Y;
        var bx = (double)physicalControl.X;
        var by = physicalControl.Y;
        var cx = (double)physicalEnd.X;
        var cy = physicalEnd.Y;
        var denominator = 2 *
            (ax * (by - cy) +
             bx * (cy - ay) +
             cx * (ay - by));
        if (!double.IsFinite(denominator) ||
            Math.Abs(denominator) <= CircularArcEpsilon * maximumEdgeSquared)
        {
            return false;
        }

        var startLengthSquared = ax * ax + ay * ay;
        var controlLengthSquared = bx * bx + by * by;
        var endLengthSquared = cx * cx + cy * cy;
        var centerX =
            (startLengthSquared * (by - cy) +
             controlLengthSquared * (cy - ay) +
             endLengthSquared * (ay - by)) /
            denominator;
        var centerY =
            (startLengthSquared * (cx - bx) +
             controlLengthSquared * (ax - cx) +
             endLengthSquared * (bx - ax)) /
            denominator;
        if (!double.IsFinite(centerX) || !double.IsFinite(centerY))
        {
            return false;
        }

        var center = new Vector2((float)centerX, (float)centerY);
        var radius = Vector2.Distance(center, physicalStart);
        if (!IsFinite(center) ||
            !float.IsFinite(radius) ||
            radius <= CircularArcEpsilon)
        {
            return false;
        }

        var startAngle = Math.Atan2(
            physicalStart.Y - center.Y,
            physicalStart.X - center.X);
        var controlAngle = Math.Atan2(
            physicalControl.Y - center.Y,
            physicalControl.X - center.X);
        var endAngle = Math.Atan2(
            physicalEnd.Y - center.Y,
            physicalEnd.X - center.X);
        var counterClockwise =
            NormalizePositiveAngle(controlAngle - startAngle) <
            NormalizePositiveAngle(endAngle - startAngle);
        var firstSweep = counterClockwise
            ? NormalizePositiveAngle(controlAngle - startAngle)
            : -NormalizePositiveAngle(startAngle - controlAngle);
        var secondSweep = counterClockwise
            ? NormalizePositiveAngle(endAngle - controlAngle)
            : -NormalizePositiveAngle(controlAngle - endAngle);
        if (!double.IsFinite(firstSweep) ||
            !double.IsFinite(secondSweep) ||
            Math.Abs(firstSweep) <= CircularArcEpsilon ||
            Math.Abs(secondSweep) <= CircularArcEpsilon)
        {
            return false;
        }

        var candidate = new CircularArc(
            center,
            radius,
            startAngle,
            firstSweep,
            secondSweep,
            geometry);
        if (!IsCircularArcInsideBounds(candidate, allowOutside))
        {
            return false;
        }

        arc = candidate;
        return true;
    }

    private static bool IsCircularArcInsideBounds(
        CircularArc arc,
        bool allowOutside)
    {
        var minimum = allowOutside ? MinimumGeneratedPathCoordinate : 0;
        var maximum = allowOutside ? MaximumGeneratedPathCoordinate : 1;
        var totalSweep = arc.FirstSweep + arc.SecondSweep;
        foreach (var angle in CircularArcExtremaAngles)
        {
            if (!IsAngleOnSweep(arc.StartAngle, totalSweep, angle))
            {
                continue;
            }

            var physicalPoint = new Vector2(
                arc.Center.X + arc.Radius * (float)Math.Cos(angle),
                arc.Center.Y + arc.Radius * (float)Math.Sin(angle));
            if (!IsInsideBounds(
                    arc.Geometry.FromShortSideVector(physicalPoint),
                    minimum,
                    maximum))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAngleOnSweep(
        double startAngle,
        double sweep,
        double angle) =>
        sweep >= 0
            ? NormalizePositiveAngle(angle - startAngle) <=
                sweep + CircularArcEpsilon
            : NormalizePositiveAngle(startAngle - angle) <=
                -sweep + CircularArcEpsilon;

    private static double NormalizePositiveAngle(double angle)
    {
        var result = angle % TwoPi;
        return result < 0 ? result + TwoPi : result;
    }

    private static bool IsInsideBounds(
        Vector2 point,
        float minimum,
        float maximum) =>
        IsFinite(point) &&
        point.X >= minimum - CircularArcEpsilon &&
        point.X <= maximum + CircularArcEpsilon &&
        point.Y >= minimum - CircularArcEpsilon &&
        point.Y <= maximum + CircularArcEpsilon;

    private static bool IsFinite(Vector2 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y);
}
