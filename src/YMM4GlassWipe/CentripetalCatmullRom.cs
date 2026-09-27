// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace YMM4GlassWipe;

internal static class CentripetalCatmullRom
{
    private const float Alpha = 0.5f;
    private const float Epsilon = 0.000001f;

    public static Vector2 Evaluate(
        Vector2 point0,
        Vector2 point1,
        Vector2 point2,
        Vector2 point3,
        float amount)
    {
        amount = float.IsFinite(amount)
            ? Math.Clamp(amount, 0, 1)
            : 0;

        if (!IsFinite(point0) ||
            !IsFinite(point1) ||
            !IsFinite(point2) ||
            !IsFinite(point3))
        {
            return Linear(point1, point2, amount);
        }

        var t0 = 0f;
        var t1 = t0 + GetInterval(point0, point1);
        var t2 = t1 + GetInterval(point1, point2);
        var t3 = t2 + GetInterval(point2, point3);
        if (t1 - t0 <= Epsilon ||
            t2 - t1 <= Epsilon ||
            t3 - t2 <= Epsilon)
        {
            return Linear(point1, point2, amount);
        }

        var time = t1 + (t2 - t1) * amount;
        var a1 = Interpolate(point0, point1, t0, t1, time);
        var a2 = Interpolate(point1, point2, t1, t2, time);
        var a3 = Interpolate(point2, point3, t2, t3, time);
        var b1 = Interpolate(a1, a2, t0, t2, time);
        var b2 = Interpolate(a2, a3, t1, t3, time);
        var result = Interpolate(b1, b2, t1, t2, time);
        return IsFinite(result)
            ? result
            : Linear(point1, point2, amount);
    }

    private static float GetInterval(Vector2 from, Vector2 to)
    {
        var distance = Vector2.Distance(from, to);
        return float.IsFinite(distance)
            ? MathF.Pow(distance, Alpha)
            : 0;
    }

    private static Vector2 Interpolate(
        Vector2 from,
        Vector2 to,
        float fromTime,
        float toTime,
        float time)
    {
        var duration = toTime - fromTime;
        if (!float.IsFinite(duration) || duration <= Epsilon)
        {
            return from;
        }

        var amount = (time - fromTime) / duration;
        return Linear(from, to, amount);
    }

    private static Vector2 Linear(Vector2 from, Vector2 to, float amount)
    {
        var result = Vector2.Lerp(from, to, amount);
        return IsFinite(result) ? result : Vector2.Zero;
    }

    private static bool IsFinite(Vector2 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y);
}
