// SPDX-License-Identifier: MPL-2.0

using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YMM4GlassWipe;

/// <summary>
/// 埋め込みPNGまたは矩形から、形状の内側距離に沿ったA8ブラシマスクを生成します。
/// </summary>
internal static class WipeBrushMaskRasterizer
{
    internal const int MaskSize = 256;
    internal const int Padding = 4;
    private const byte SourceAlphaThreshold = 128;
    private const int OrthogonalDistance = 3;
    private const int DiagonalDistance = 4;
    private const int InfiniteDistance = 1_000_000;
    private const string HandResourceName =
        "YMM4GlassWipe.Assets.Brushes.hand-mask.png";
    private const string ShoePrintResourceName =
        "YMM4GlassWipe.Assets.Brushes.shoe-print-mask.png";
    private static readonly Lazy<WipeBrushBinaryMask> HandMask =
        new(() => LoadEmbeddedMask(HandResourceName));
    private static readonly Lazy<WipeBrushBinaryMask> ShoePrintMask =
        new(() => LoadEmbeddedMask(ShoePrintResourceName));

    public static WipeBrushBinaryMask CreateBinaryMask(
        GlassWipeBrushShape shape) =>
        shape switch
        {
            GlassWipeBrushShape.Rectangle => CreateRectangleMask(),
            GlassWipeBrushShape.Hand => HandMask.Value,
            GlassWipeBrushShape.ShoePrint => ShoePrintMask.Value,
            _ => throw new ArgumentOutOfRangeException(
                nameof(shape),
                shape,
                "Bitmapを使用しないブラシ形状です。"),
        };

    public static WipeBrushPixelDimensions GetNativePixelDimensions(
        GlassWipeBrushShape shape)
    {
        var mask = shape switch
        {
            GlassWipeBrushShape.Hand => HandMask.Value,
            GlassWipeBrushShape.ShoePrint => ShoePrintMask.Value,
            _ => throw new ArgumentOutOfRangeException(
                nameof(shape),
                shape,
                "ネイティブ寸法を持たないブラシ形状です。"),
        };
        return new WipeBrushPixelDimensions(
            mask.SourcePixelWidth,
            mask.SourcePixelHeight);
    }

    public static WipeBrushBinaryMask CreateBinaryMask(
        byte[] pngBytes,
        string displayName)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        using var stream = new MemoryStream(pngBytes, writable: false);
        return LoadMask(stream, displayName);
    }

    public static byte[] CreateFeatheredAlpha(
        WipeBrushBinaryMask mask,
        float softness)
    {
        ArgumentNullException.ThrowIfNull(mask);
        if (mask.Size != MaskSize || mask.Pixels.Length != MaskSize * MaskSize)
        {
            throw new ArgumentException(
                "ブラシマスクの寸法が不正です。",
                nameof(mask));
        }

        var sanitizedSoftness = float.IsFinite(softness)
            ? Math.Clamp(softness, 0, 1)
            : 0;
        var result = new byte[mask.Pixels.Length];
        if (sanitizedSoftness <= 0)
        {
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = mask.Pixels[index] ? byte.MaxValue : (byte)0;
            }

            return result;
        }

        var distances = CreateInsideDistanceMap(mask.Pixels);
        var featherDistance = MathF.Max(
            OrthogonalDistance,
            sanitizedSoftness * MaskSize * 0.5f * OrthogonalDistance);
        for (var index = 0; index < result.Length; index++)
        {
            if (!mask.Pixels[index])
            {
                continue;
            }

            result[index] = (byte)Math.Clamp(
                MathF.Round(distances[index] / featherDistance * byte.MaxValue),
                0,
                byte.MaxValue);
        }

        return result;
    }

    private static WipeBrushBinaryMask CreateRectangleMask()
    {
        var pixels = new bool[MaskSize * MaskSize];
        for (var y = 0; y < MaskSize; y++)
        {
            for (var x = 0; x < MaskSize; x++)
            {
                pixels[y * MaskSize + x] = true;
            }
        }

        return new WipeBrushBinaryMask(MaskSize, pixels, 1, 1);
    }

    private static WipeBrushBinaryMask LoadEmbeddedMask(string resourceName)
    {
        using var stream = typeof(WipeBrushMaskRasterizer).Assembly
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"埋め込みブラシ画像が見つかりません: {resourceName}");
        return LoadMask(stream, resourceName);
    }

    private static WipeBrushBinaryMask LoadMask(
        Stream stream,
        string resourceName)
    {
        var decoder = new PngBitmapDecoder(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count != 1)
        {
            throw new InvalidOperationException(
                $"ブラシ画像のフレーム数が不正です: {resourceName}");
        }

        var source = decoder.Frames[0];
        var converted = new FormatConvertedBitmap(
            source,
            PixelFormats.Bgra32,
            null,
            0);
        var stride = checked(converted.PixelWidth * 4);
        var bgraPixels = new byte[checked(stride * converted.PixelHeight)];
        converted.CopyPixels(bgraPixels, stride, 0);
        return NormalizeAlpha(
            bgraPixels,
            converted.PixelWidth,
            converted.PixelHeight,
            stride,
            resourceName);
    }

    private static WipeBrushBinaryMask NormalizeAlpha(
        byte[] bgraPixels,
        int width,
        int height,
        int stride,
        string resourceName)
    {
        var hasVisiblePixel = false;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (bgraPixels[y * stride + x * 4 + 3] >= SourceAlphaThreshold)
                {
                    hasVisiblePixel = true;
                    break;
                }
            }

            if (hasVisiblePixel)
            {
                break;
            }
        }

        if (!hasVisiblePixel)
        {
            throw new InvalidOperationException(
                $"ブラシ画像に有効なalphaがありません: {resourceName}");
        }

        var pixels = new bool[MaskSize * MaskSize];
        for (var y = 0; y < MaskSize; y++)
        {
            var sourceY = Math.Clamp(
                (int)MathF.Round(
                    y * (height - 1f) /
                    Math.Max(1, MaskSize - 1)),
                0,
                height - 1);
            for (var x = 0; x < MaskSize; x++)
            {
                var sourceX = Math.Clamp(
                    (int)MathF.Round(
                        x * (width - 1f) /
                        Math.Max(1, MaskSize - 1)),
                    0,
                    width - 1);
                pixels[y * MaskSize + x] =
                    bgraPixels[sourceY * stride + sourceX * 4 + 3] >=
                    SourceAlphaThreshold;
            }
        }

        return new WipeBrushBinaryMask(MaskSize, pixels, width, height);
    }

    private static int[] CreateInsideDistanceMap(bool[] pixels)
    {
        var distances = new int[pixels.Length];
        for (var index = 0; index < pixels.Length; index++)
        {
            var x = index % MaskSize;
            var y = index / MaskSize;
            distances[index] = !pixels[index]
                ? 0
                : x == 0 || x == MaskSize - 1 || y == 0 || y == MaskSize - 1
                    ? OrthogonalDistance
                    : InfiniteDistance;
        }

        for (var y = 0; y < MaskSize; y++)
        {
            for (var x = 0; x < MaskSize; x++)
            {
                var index = y * MaskSize + x;
                if (distances[index] == 0)
                {
                    continue;
                }

                UpdateDistance(distances, index, x - 1, y, OrthogonalDistance);
                UpdateDistance(distances, index, x, y - 1, OrthogonalDistance);
                UpdateDistance(distances, index, x - 1, y - 1, DiagonalDistance);
                UpdateDistance(distances, index, x + 1, y - 1, DiagonalDistance);
            }
        }

        for (var y = MaskSize - 1; y >= 0; y--)
        {
            for (var x = MaskSize - 1; x >= 0; x--)
            {
                var index = y * MaskSize + x;
                if (distances[index] == 0)
                {
                    continue;
                }

                UpdateDistance(distances, index, x + 1, y, OrthogonalDistance);
                UpdateDistance(distances, index, x, y + 1, OrthogonalDistance);
                UpdateDistance(distances, index, x + 1, y + 1, DiagonalDistance);
                UpdateDistance(distances, index, x - 1, y + 1, DiagonalDistance);
            }
        }

        return distances;
    }

    private static void UpdateDistance(
        int[] distances,
        int index,
        int neighborX,
        int neighborY,
        int step)
    {
        if (neighborX < 0 || neighborX >= MaskSize ||
            neighborY < 0 || neighborY >= MaskSize)
        {
            return;
        }

        distances[index] = Math.Min(
            distances[index],
            distances[neighborY * MaskSize + neighborX] + step);
    }
}

internal sealed class WipeBrushBinaryMask
{
    public WipeBrushBinaryMask(
        int size,
        bool[] pixels,
        int sourcePixelWidth = 0,
        int sourcePixelHeight = 0)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        Size = size;
        Pixels = pixels.ToArray();
        SourcePixelWidth = Math.Max(0, sourcePixelWidth);
        SourcePixelHeight = Math.Max(0, sourcePixelHeight);
    }

    public int Size { get; }

    public bool[] Pixels { get; }

    public int SourcePixelWidth { get; }

    public int SourcePixelHeight { get; }
}
