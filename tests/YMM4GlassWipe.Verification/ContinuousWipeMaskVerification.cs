// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YMM4GlassWipe.Verification;

/// <summary>
/// 内蔵alphaマスクをCPUで双一次補間する限定参照。Direct2D実機の画素一致を保証しません。
/// </summary>
internal static class ContinuousWipeMaskVerification
{
    public static void VerifySweptCoverage()
    {
        const int size = 384;
        var geometry = WipeMaskGeometry.Create(size, size);
        WipePathSample[] samples = [new(0, .25f, .55f, 1, .4f, .4f, 1, 0), new(1, .75f, .55f, 1, .4f, .4f, 1, 0)];
        var style = new WipePathStyle(0, 0, 0, Softness: .15f, Quality: GlassWipeQuality.High,
            AccumulationMode: WipeMaskAccumulationMode.PerPassMaximum, BrushShape: GlassWipeBrushShape.Hand);
        var oldStamps = WipeBrushStampGenerator.Generate(samples, 0, geometry, style);
        var newStamps = WipeBrushStampGenerator.Generate(samples, 0, geometry, style with { ContinuousWipe = true });
        var legacy = Rasterize(oldStamps, size, size, style.AccumulationMode);
        var continuous = Rasterize(newStamps, size, size, style.AccumulationMode);
        var oldGaps = InteriorGaps(legacy, size, .04f);
        var newGaps = InteriorGaps(continuous, size, .04f);
        Check(oldGaps > 10 && newGaps < oldGaps / 4,
            $"手形の通過部分にある反復した隙間を減らす必要があります。旧={oldGaps},連続={newGaps}");
        Check(continuous.Max() <= .40001f && continuous.Max() >= .399f,
            "密度を増やしても片道の拭き取り量40%を超えてはいけません。");
        var twoPasses = newStamps.Concat(newStamps.Select(s => s with { AccumulationGroup = 1 })).ToArray();
        var repeated = Rasterize(twoPasses, size, size, style.AccumulationMode);
        Check(MathF.Abs(repeated.Max() - .64f) < .0001f,
            "40%の二回拭きは最大64%となる必要があります。");
        Console.WriteLine($"[連続拭きCPU参照] 指先の隙間 {oldGaps} → {newGaps}、片道上限 {continuous.Max():F3}");
    }

    public static void WritePreviewArtifacts(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var measurements = new List<object>();
        const int width = 768;
        const int height = 432;
        var geometry = WipeMaskGeometry.Create(1920, 1080);
        var generated = WipeSimplePathGenerator.Generate(GlassWipeSimplePattern.OffsetRoundTrips,
            GlassWipePathInterpolation.CircularArc, new(25, 40), new(50, 30), new(75, 40),
            3, 22, 135, returnOffsetPercent: 20, wipeAmountPerPassPercent: 100, maskGeometry: geometry);
        // 添付画面に近い設定を再構成した比較用入力。実プロジェクトを読み込んだものではありません。
        var samples = generated.Samples.Select(s => s with { Size = s.Size * 2.5f }).ToArray();
        foreach (var shape in new[] { GlassWipeBrushShape.Hand, GlassWipeBrushShape.ShoePrint })
        {
            foreach (var enabled in new[] { false, true })
            {
                var style = generated.Style with { BrushShape = shape, RotationFollow = .8f, ContinuousWipe = enabled };
                var stopwatch = Stopwatch.StartNew();
                var stamps = WipeBrushStampGenerator.Generate(samples, 0, geometry, style);
                stopwatch.Stop();
                var generationMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                var pixels = Rasterize(stamps, width, height, style.AccumulationMode);
                var suffix = enabled ? "continuous" : "legacy";
                var name = shape == GlassWipeBrushShape.Hand ? "hand" : "shoe";
                var path = Path.Combine(outputDirectory, $"{name}-{suffix}.png");
                SavePng(path, pixels, width, height);
                measurements.Add(new
                {
                    shape = name,
                    continuous = enabled,
                    stampCount = stamps.Count,
                    generationMilliseconds,
                    coverageSum = pixels.Sum(v => (double)v),
                    file = Path.GetFileName(path)
                });
            }
        }
        File.WriteAllText(Path.Combine(outputDirectory, "cpu-mask-comparison.json"),
            JsonSerializer.Serialize(new { note = "CPU alpha参照。GPU性能やYMM4実機受け入れを示すものではありません。", measurements },
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private static float[] Rasterize(IReadOnlyList<WipeBrushStamp> stamps, int width, int height,
        WipeMaskAccumulationMode accumulationMode)
    {
        var result = new float[width * height];
        if (stamps.Count == 0) return result;
        var mask = WipeBrushMaskRasterizer.CreateFeatheredAlpha(
            WipeBrushMaskRasterizer.CreateBinaryMask(stamps[0].Shape), stamps[0].Softness);
        foreach (var group in stamps.GroupBy(s => s.AccumulationGroup))
        {
            var pass = group.ToArray();
            var perPass = accumulationMode == WipeMaskAccumulationMode.PerPassMaximum;
            var opacity = perPass ? WipeMaskRenderer.ResolvePassOpacity(pass, 0, pass.Length) : 1;
            if (opacity <= 0) continue;
            var layer = new float[result.Length];
            foreach (var stamp in pass)
            {
                var transform = WipeMaskRenderer.GetStampTransform(stamp) *
                    Matrix3x2.CreateScale(width / 1024f, height / 1024f);
                if (!Matrix3x2.Invert(transform, out var inverse)) continue;
                Vector2[] corners = [new(-1, -1), new(1, -1), new(-1, 1), new(1, 1)];
                var bounds = corners.Select(p => Vector2.Transform(p, transform)).ToArray();
                var left = Math.Clamp((int)MathF.Floor(bounds.Min(p => p.X)), 0, width);
                var right = Math.Clamp((int)MathF.Ceiling(bounds.Max(p => p.X)), 0, width);
                var top = Math.Clamp((int)MathF.Floor(bounds.Min(p => p.Y)), 0, height);
                var bottom = Math.Clamp((int)MathF.Ceiling(bounds.Max(p => p.Y)), 0, height);
                for (var y = top; y < bottom; y++)
                    for (var x = left; x < right; x++)
                    {
                        var uv = Vector2.Transform(new Vector2(x + .5f, y + .5f), inverse);
                        if (MathF.Abs(uv.X) > 1 || MathF.Abs(uv.Y) > 1) continue;
                        var alpha = SampleMask(mask, (uv + Vector2.One) * 128 - new Vector2(.5f)) *
                            Math.Clamp(stamp.Alpha / opacity, 0, 1);
                        var index = y * width + x;
                        layer[index] = alpha + layer[index] * (1 - alpha);
                    }
            }
            for (var index = 0; index < result.Length; index++)
            {
                var value = layer[index] * opacity;
                result[index] = value + result[index] * (1 - value);
            }
        }
        return result;
    }

    private static float SampleMask(byte[] pixels, Vector2 point)
    {
        var x = (int)MathF.Floor(point.X);
        var y = (int)MathF.Floor(point.Y);
        var amount = point - new Vector2(x, y);
        float At(int px, int py) => (uint)px < 256 && (uint)py < 256 ? pixels[py * 256 + px] / 255f : 0;
        return float.Lerp(float.Lerp(At(x, y), At(x + 1, y), amount.X),
            float.Lerp(At(x, y + 1), At(x + 1, y + 1), amount.X), amount.Y);
    }

    private static int InteriorGaps(float[] pixels, int width, float threshold)
    {
        var gaps = 0;
        for (var y = 0; y < width; y++)
        {
            var lit = false;
            var dark = false;
            for (var x = (int)(width * .35f); x < width * .65f; x++)
            {
                if (pixels[y * width + x] >= threshold)
                {
                    if (lit && dark) gaps++;
                    lit = true;
                    dark = false;
                }
                else if (lit) dark = true;
            }
        }
        return gaps;
    }

    private static void SavePng(string path, float[] pixels, int width, int height)
    {
        var bytes = pixels.Select(v => (byte)Math.Clamp(MathF.Round(v * 255), 0, 255)).ToArray();
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, bytes, width);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
