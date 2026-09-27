// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace YMM4GlassWipe;

internal readonly record struct GlassWipeQuad(
    Vector2 TopLeft,
    Vector2 TopRight,
    Vector2 BottomRight,
    Vector2 BottomLeft)
{
    private const float MinimumEdgeLengthSquared = 0.00000001f;
    private const float MinimumCrossMagnitude = 0.000001f;
    private const float MinimumArea = 0.00001f;

    public static GlassWipeQuad FrontWindshield { get; } = new(
        new Vector2(0.18f, 0.18f),
        new Vector2(0.82f, 0.18f),
        new Vector2(0.92f, 0.85f),
        new Vector2(0.08f, 0.85f));

    public bool IsValid => TryCreateMapping(out _);

    public GlassWipeQuad ResolvePreset(GlassWipeQuadPreset preset) =>
        preset == GlassWipeQuadPreset.FrontWindshield
            ? FrontWindshield
            : this;

    public bool TryCreateMapping(out GlassWipeQuadMapping mapping)
    {
        mapping = GlassWipeQuadMapping.Invalid;
        var points = new[] { TopLeft, TopRight, BottomRight, BottomLeft };
        if (points.Any(point => !IsFinite(point)))
        {
            return false;
        }

        for (var index = 0; index < points.Length; index++)
        {
            var next = points[(index + 1) % points.Length];
            if (Vector2.DistanceSquared(points[index], next) < MinimumEdgeLengthSquared)
            {
                return false;
            }
        }

        for (var index = 0; index < points.Length; index++)
        {
            var current = points[index];
            var next = points[(index + 1) % points.Length];
            var afterNext = points[(index + 2) % points.Length];
            var cross = Cross(next - current, afterNext - next);
            if (cross <= MinimumCrossMagnitude)
            {
                return false;
            }
        }

        var doubledArea = 0f;
        for (var index = 0; index < points.Length; index++)
        {
            doubledArea += Cross(points[index], points[(index + 1) % points.Length]);
        }

        if (MathF.Abs(doubledArea) * 0.5f < MinimumArea)
        {
            return false;
        }

        var dx1 = TopRight.X - BottomRight.X;
        var dx2 = BottomLeft.X - BottomRight.X;
        var dx3 = TopLeft.X - TopRight.X + BottomRight.X - BottomLeft.X;
        var dy1 = TopRight.Y - BottomRight.Y;
        var dy2 = BottomLeft.Y - BottomRight.Y;
        var dy3 = TopLeft.Y - TopRight.Y + BottomRight.Y - BottomLeft.Y;
        var denominator = dx1 * dy2 - dx2 * dy1;
        if (!float.IsFinite(denominator) || MathF.Abs(denominator) < MinimumCrossMagnitude)
        {
            return false;
        }

        var projectiveX = (dx3 * dy2 - dx2 * dy3) / denominator;
        var projectiveY = (dx1 * dy3 - dx3 * dy1) / denominator;
        var localToInput = new GlassWipeHomography(
            TopRight.X - TopLeft.X + projectiveX * TopRight.X,
            BottomLeft.X - TopLeft.X + projectiveY * BottomLeft.X,
            TopLeft.X,
            TopRight.Y - TopLeft.Y + projectiveX * TopRight.Y,
            BottomLeft.Y - TopLeft.Y + projectiveY * BottomLeft.Y,
            TopLeft.Y,
            projectiveX,
            projectiveY,
            1);

        if (!localToInput.TryInvert(out var inputToLocal))
        {
            return false;
        }

        mapping = new GlassWipeQuadMapping(localToInput, inputToLocal, true);
        return true;
    }

    public Vector2 GetNominalPixelSize(float inputWidth, float inputHeight)
    {
        var inputSize = new Vector2(
            SanitizeDimension(inputWidth),
            SanitizeDimension(inputHeight));
        var top = Vector2.Distance(TopLeft * inputSize, TopRight * inputSize);
        var bottom = Vector2.Distance(BottomLeft * inputSize, BottomRight * inputSize);
        var left = Vector2.Distance(TopLeft * inputSize, BottomLeft * inputSize);
        var right = Vector2.Distance(TopRight * inputSize, BottomRight * inputSize);
        return new Vector2(
            SanitizeDimension((top + bottom) * 0.5f),
            SanitizeDimension((left + right) * 0.5f));
    }

    private static bool IsFinite(Vector2 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y);

    private static float Cross(Vector2 left, Vector2 right) =>
        left.X * right.Y - left.Y * right.X;

    private static float SanitizeDimension(float value) =>
        float.IsFinite(value) && value > 0 ? value : 1;
}

internal readonly record struct GlassWipeQuadMapping(
    GlassWipeHomography LocalToInput,
    GlassWipeHomography InputToLocal,
    bool IsValid)
{
    public static GlassWipeQuadMapping Invalid { get; } = new(
        GlassWipeHomography.Identity,
        GlassWipeHomography.Identity,
        false);
}

internal readonly record struct GlassWipeHomography(
    float M11,
    float M12,
    float M13,
    float M21,
    float M22,
    float M23,
    float M31,
    float M32,
    float M33)
{
    private const float MinimumDeterminant = 0.0000001f;

    public static GlassWipeHomography Identity { get; } = new(
        1, 0, 0,
        0, 1, 0,
        0, 0, 1);

    public Vector2 Transform(Vector2 point)
    {
        var denominator = M31 * point.X + M32 * point.Y + M33;
        if (!float.IsFinite(denominator) || MathF.Abs(denominator) < MinimumDeterminant)
        {
            return new Vector2(float.NaN, float.NaN);
        }

        return new Vector2(
            (M11 * point.X + M12 * point.Y + M13) / denominator,
            (M21 * point.X + M22 * point.Y + M23) / denominator);
    }

    public bool TryInvert(out GlassWipeHomography inverse)
    {
        inverse = Identity;
        if (!IsFinite())
        {
            return false;
        }

        var i11 = M22 * M33 - M23 * M32;
        var i12 = M13 * M32 - M12 * M33;
        var i13 = M12 * M23 - M13 * M22;
        var i21 = M23 * M31 - M21 * M33;
        var i22 = M11 * M33 - M13 * M31;
        var i23 = M13 * M21 - M11 * M23;
        var i31 = M21 * M32 - M22 * M31;
        var i32 = M12 * M31 - M11 * M32;
        var i33 = M11 * M22 - M12 * M21;
        var determinant = M11 * i11 + M12 * i21 + M13 * i31;
        if (!float.IsFinite(determinant) || MathF.Abs(determinant) < MinimumDeterminant)
        {
            return false;
        }

        var scale = 1f / determinant;
        inverse = new GlassWipeHomography(
            i11 * scale,
            i12 * scale,
            i13 * scale,
            i21 * scale,
            i22 * scale,
            i23 * scale,
            i31 * scale,
            i32 * scale,
            i33 * scale);
        return inverse.IsFinite();
    }

    private bool IsFinite() =>
        float.IsFinite(M11) &&
        float.IsFinite(M12) &&
        float.IsFinite(M13) &&
        float.IsFinite(M21) &&
        float.IsFinite(M22) &&
        float.IsFinite(M23) &&
        float.IsFinite(M31) &&
        float.IsFinite(M32) &&
        float.IsFinite(M33);
}
