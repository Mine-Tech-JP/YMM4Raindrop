// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace YMM4GlassWipe.Verification;

internal static class RectangleTurnVerification
{
    private static readonly WipeMaskGeometry Geometry = WipeMaskGeometry.Create(1920, 1080);

    public static void VerifyBoundaryOrientation()
    {
        foreach (var length in new[] { 150, 487 })
        foreach (var follow in new[] { 0f, 0.5f, 1f })
        foreach (var dimensions in new[] { (50f, 600f), (600f, 50f) })
        {
            var (samples, style) = Create(length, dimensions.Item1, dimensions.Item2, follow);
            var boundaries = 0;
            for (var i = 2; i < samples.Length; i++)
            {
                if (samples[i - 1].AccumulationGroup == samples[i].AccumulationGroup) continue;
                boundaries++;
                var before = WipeBrushStampGenerator.Generate(samples[..i], 0, Geometry, style);
                var crossing = WipeBrushStampGenerator.Generate(samples[..(i + 1)], i, Geometry, style);
                Check(before.Count > 0 && crossing.Count > 0, "折り返しの検証用スタンプが必要です。");
                var previousAxis = Axis(before[^1]);
                foreach (var stamp in crossing)
                {
                    Check(Vector2.Dot(previousAxis, Axis(stamp)) > 0.9999f,
                        $"四角形の折り返し区間で余分に傾きました。長さ={length}, frame={i}, 追従={follow}, 寸法={dimensions}");
                    Check(stamp.AccumulationGroup == samples[i].AccumulationGroup,
                        "境界の累積グループを維持する必要があります。");
                }
            }
            Check(boundaries == 6, "3.5往復の全6境界を検証する必要があります。");
        }
    }

    public static void VerifyStreamingAndSeek()
    {
        foreach (var dimensions in new[] { (50f, 600f), (600f, 50f) })
        {
            var (samples, style) = Create(150, dimensions.Item1, dimensions.Item2, 1);
            var state = new WipeBrushStampGenerator.StreamState(0);
            var streaming = new List<WipeBrushStamp>();
            var records = new Dictionary<int, WipeBrushStamp[]>();
            int[] targets = [20, 21, 22, 23, 42, 43, 44, 149, 150];
            for (var i = 0; i < samples.Length; i++)
            {
                state.Append(samples[i], Geometry, style, streaming);
                if (!targets.Contains(i)) continue;
                var rebuilt = WipeBrushStampGenerator.Generate(samples[..(i + 1)], 0, Geometry, style);
                Check(streaming.SequenceEqual(rebuilt), $"連続生成と再構築が一致しません。frame={i}");
                records.Add(i, streaming.ToArray());
            }
            foreach (var frame in new[] { 150, 21, 22, 44, 20, 23, 149, 43, 42, 22 })
            {
                var direct = WipeBrushStampGenerator.Generate(samples[..(frame + 1)], 0, Geometry, style);
                Check(records[frame].SequenceEqual(direct), $"前後シークで拭き跡が変わりました。frame={frame}");
            }
        }
        Check(WipeBrushStampGenerator.RequiresReturnTangentCorrection(GlassWipeBrushShape.Rectangle, 1),
            "四角形の復路は追従率を掛ける前に方向をそろえる必要があります。");
    }

    public static void VerifyPartialFollowAcrossReturn()
    {
        foreach (var length in new[] { 150, 487 })
        foreach (var dimensions in new[] { (50f, 600f), (600f, 50f) })
        foreach (var manualDegrees in new[] { -10f, -20f })
        for (var percent = 0; percent <= 100; percent += 10)
        {
            var (samples, style) = Create(length, dimensions.Item1, dimensions.Item2, percent / 100f);
            samples = samples.Select(sample => sample with
            {
                RotationRadians = manualDegrees * MathF.PI / 180,
            }).ToArray();
            var stamps = WipeBrushStampGenerator.Generate(samples, 0, Geometry, style);
            for (var i = 1; i < stamps.Count; i++)
            {
                var previous = Axis(stamps[i - 1]);
                var current = Axis(stamps[i]);
                var angle = MathF.Acos(Math.Clamp(Vector2.Dot(previous, current), -1, 1)) * 180 / MathF.PI;
                Check(angle < 15,
                    $"復路を含む全区間で姿勢が急変しました。追従={percent}%, 手動角={manualDegrees}, 長さ={length}, 角度差={angle}");
            }
            // 境界だけでなく、その直後も低い追従率のまま連続生成・再構築が一致する。
            var state = new WipeBrushStampGenerator.StreamState(0);
            var streamed = new List<WipeBrushStamp>();
            foreach (var sample in samples) state.Append(sample, Geometry, style, streamed);
            Check(stamps.SequenceEqual(streamed), $"追従{percent}%の全区間で連続生成が一致しません。");
        }
    }

    private static (WipePathSample[] Samples, WipePathStyle Style) Create(
        int length, float width, float height, float follow)
    {
        var generated = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.OffsetRoundTrips, GlassWipePathInterpolation.CircularArc,
            new Vector2(15, 50), new Vector2(50, 30), new Vector2(75, 30),
            3.5, length, length, 10, 100, Geometry, 3);
        var samples = generated.Samples.Select(sample => sample with
        {
            BrushWidthPixels = width, BrushHeightPixels = height,
            RotationRadians = -20 * MathF.PI / 180,
        }).ToArray();
        return (samples, generated.Style with
        {
            BrushShape = GlassWipeBrushShape.Rectangle,
            RotationFollow = follow,
            ContinuousWipe = false,
        });
    }

    private static Vector2 Axis(WipeBrushStamp stamp) => Vector2.Normalize(
        new Vector2(stamp.Transform.M11, stamp.Transform.M12 * Geometry.ScaleX / Geometry.ScaleY));

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
