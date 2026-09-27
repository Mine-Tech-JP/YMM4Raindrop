// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace YMM4GlassWipe;

internal readonly record struct WipeMaskGeometry(
    float ScaleX,
    float ScaleY,
    float PixelWidth,
    float PixelHeight)
{
    public const int SchemaVersion = 2;

    public WipeMaskGeometry(float scaleX, float scaleY)
        : this(
            SanitizeScale(scaleX),
            SanitizeScale(scaleY),
            1 / SanitizeScale(scaleX),
            1 / SanitizeScale(scaleY))
    {
    }

    public static WipeMaskGeometry Create(
        float regionPixelWidth,
        float regionPixelHeight)
    {
        var width = SanitizeDimension(regionPixelWidth);
        var height = SanitizeDimension(regionPixelHeight);
        var shortSide = MathF.Min(width, height);

        return new WipeMaskGeometry(
            shortSide / width,
            shortSide / height,
            width,
            height);
    }

    public float MeasureInShortSideUnits(Vector2 from, Vector2 to)
    {
        return ToShortSideVector(to - from).Length();
    }

    public Vector2 ToShortSideVector(Vector2 vector) =>
        new(
            vector.X / SanitizeScale(ScaleX),
            vector.Y / SanitizeScale(ScaleY));

    public Vector2 FromShortSideVector(Vector2 vector) =>
        new(
            vector.X * SanitizeScale(ScaleX),
            vector.Y * SanitizeScale(ScaleY));

    public Matrix3x2 CreateBrushTransform(
        Vector2 center,
        float rotationRadians)
    {
        if (!float.IsFinite(rotationRadians) ||
            MathF.Abs(rotationRadians) <= 0.000001f)
        {
            return Matrix3x2.Identity;
        }

        var scaleX = SanitizeScale(ScaleX);
        var scaleY = SanitizeScale(ScaleY);
        var rotationCos = MathF.Cos(rotationRadians);
        var rotationSin = MathF.Sin(rotationRadians);
        var physicalRotation = new Matrix3x2(
            rotationCos,
            rotationSin * scaleY / scaleX,
            -rotationSin * scaleX / scaleY,
            rotationCos,
            0,
            0);

        return Matrix3x2.CreateTranslation(-center) *
            physicalRotation *
            Matrix3x2.CreateTranslation(center);
    }

    private static float SanitizeDimension(float value) =>
        float.IsFinite(value) && value > 0 ? value : 1;

    private static float SanitizeScale(float value) =>
        float.IsFinite(value) && value > 0 ? value : 1;
}
