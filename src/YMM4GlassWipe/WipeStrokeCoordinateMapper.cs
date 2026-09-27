// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace YMM4GlassWipe;

/// <summary>
/// 1920×1080基準のストローク座標を、実際のマスク領域の正規化座標へ変換します。
/// </summary>
internal static class WipeStrokeCoordinateMapper
{
    public static Vector2 ToNormalizedUv(
        Vector2 canvasPosition,
        WipeMaskGeometry geometry)
    {
        var pixelWidth = SanitizeDimension(geometry.PixelWidth);
        var pixelHeight = SanitizeDimension(geometry.PixelHeight);
        var scale = MathF.Min(
            pixelWidth / WipeStrokeDocument.CanvasPixelWidth,
            pixelHeight / WipeStrokeDocument.CanvasPixelHeight);
        if (!float.IsFinite(scale) || scale <= 0)
        {
            return Vector2.Zero;
        }

        var offsetX = (pixelWidth - WipeStrokeDocument.CanvasPixelWidth * scale) / 2;
        var offsetY = (pixelHeight - WipeStrokeDocument.CanvasPixelHeight * scale) / 2;
        var mapped = new Vector2(
            (offsetX + SanitizeCoordinate(canvasPosition.X) * scale) / pixelWidth,
            (offsetY + SanitizeCoordinate(canvasPosition.Y) * scale) / pixelHeight);
        return new Vector2(ClampUnit(mapped.X), ClampUnit(mapped.Y));
    }

    private static float SanitizeDimension(float value) =>
        float.IsFinite(value) && value > 0 ? value : 1;

    private static float SanitizeCoordinate(float value) =>
        float.IsFinite(value) ? value : 0;

    private static float ClampUnit(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
}
