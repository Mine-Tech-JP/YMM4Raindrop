// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace YMM4GlassWipe.Verification;

/// <summary>
/// 外側水滴どうしの重なりで、深い内部だけの輪郭を抑えるCPU数値参照です。
/// GPUのラスタライザー、画面微分、および実機YMM4の画素結果はこの検証の対象外です。
/// </summary>
internal static class OutsideDropletOverlapVerification
{
    private const float EdgeWidth = 0.01f;
    private const float SuppressionWidth = 0.06f;

    /// <summary>
    /// 単独、完全一致、被覆外の滴では、従来のmax合成と同じ陰影を維持することを確認します。
    /// </summary>
    public static void VerifyIsolatedAndCoincident()
    {
        foreach (var style in new[] { DropletStyle.Transparent, DropletStyle.Legacy })
        {
            foreach (var rimDistance in new[] { 0.8f, 0.9f, 0.97f })
            {
                var rim = EvaluateCircle(style, new Vector2(0f, rimDistance), 1f);
                var isolated = Combine(new[] { rim, Vector4.Zero });
                Near(LegacyMax(new[] { rim, Vector4.Zero }), isolated,
                    $"{style}の単独rim r={rimDistance:F2}は従来のmax合成を維持する必要があります。");

                var coincident = Combine(new[] { rim, rim });
                Near(LegacyMax(new[] { rim, rim }), coincident,
                    $"{style}の完全一致rim r={rimDistance:F2}は相互に輪郭を抑制してはいけません。");

                var outside = EvaluateCircle(style, new Vector2(1.20f, 0f), 1f);
                var disjoint = Combine(new[] { rim, outside });
                Near(LegacyMax(new[] { rim, outside }), disjoint,
                    $"{style}の非重複rim r={rimDistance:F2}は従来のmax合成を維持する必要があります。");
            }
        }
    }

    /// <summary>
    /// 同心円と報告設定の静止楕円で、内側輪郭の消失、外周保持、部分重なりを確認します。
    /// </summary>
    public static void VerifyContainmentAndOuterBoundary()
    {
        foreach (var style in new[] { DropletStyle.Transparent, DropletStyle.Legacy })
        {
            foreach (var radiusRatio in new[] { 1.1f, 1.2f })
            {
                foreach (var rimDistance in new[] { 0.8f, 0.9f, 0.97f })
                {
                    var smallRim = EvaluateCircle(style, new Vector2(0f, rimDistance), 1f);
                    var bigInterior = EvaluateCircle(
                        style,
                        new Vector2(0f, rimDistance / radiusRatio),
                        1f);
                    var combined = Combine(new[] { smallRim, bigInterior });

                    Check(smallRim.X + smallRim.Y > 0.01f,
                        $"{style}のsmall rim r={rimDistance:F2}は抑制前に可視の輪郭を持つ必要があります。");
                    Check(SuppressionFactor(bigInterior.W - smallRim.W) <= 0.000001f,
                        $"半径比{radiusRatio:F1}で内包された{style}のsmall rim r={rimDistance:F2}は完全に抑制する必要があります。");
                    Near(new Vector3(bigInterior.X, bigInterior.Y, bigInterior.Z), combined,
                        $"内包された{style}のsmall rim r={rimDistance:F2}はbig滴の内部陰影だけを残す必要があります。");
                }

                var outerRim = EvaluateCircle(style, new Vector2(0.97f, 0f), 1f);
                var smallOutside = EvaluateCircle(style, new Vector2(0.97f * radiusRatio, 0f), 1f);
                var outerCombined = Combine(new[] { outerRim, smallOutside });
                Near(new Vector3(outerRim.X, outerRim.Y, outerRim.Z), outerCombined,
                    $"半径比{radiusRatio:F1}の{style}滴の外周は小滴の被覆外で維持する必要があります。");
            }

            // Seed 4197、量61.4%、サイズ190.1%の静止生成式で確認した包含組。
            // 添付フレームの個体同定やGPU描画の再現ではない。
            var bigCenter = new Vector2(280.50174f, 962.86017f);
            var bigRadii = new Vector2(30.76945f, 29.11009f);
            var smallCenter = new Vector2(286.177f, 963.2712f);
            var smallRadii = new Vector2(4.33769f, 4.05694f);
            var witnessPoint = smallCenter + smallRadii * new Vector2(0f, 0.97f);
            var witnessSmall = EvaluateCircle(style, (witnessPoint - smallCenter) / smallRadii, 1f);
            var witnessBig = EvaluateCircle(style, (witnessPoint - bigCenter) / bigRadii, 1f);
            Check(witnessSmall.Y > 0.1f && witnessBig.Y == 0f,
                "報告設定の包含組には、抑制前に大滴内の小滴輪郭が必要です。");
            Near(new Vector3(witnessBig.X, witnessBig.Y, witnessBig.Z),
                Combine(new[] { witnessSmall, witnessBig }),
                "報告設定の包含組では小滴の内部輪郭を除き、大滴の陰影を残す必要があります。");

            // big半径1.2、small中心x=0.144、評価点x=1.044では、
            // big距離0.87とsmall rim距離0.90の深さ差が0.03となり、抑制率は厳密に0.5です。
            var partialSmall = EvaluateCircle(style, new Vector2(0.9f, 0f), 1f);
            var partialBig = EvaluateCircle(style, new Vector2(0.87f, 0f), 1f);
            var partialFactor = SuppressionFactor(partialBig.W - partialSmall.W);
            Near(0.5f, partialFactor,
                $"{style}の非同心部分重なりは幅{SuppressionWidth:F2}の中間で半分だけ抑制する必要があります。");
            var partial = Combine(new[] { partialSmall, partialBig });
            var partialExpected = LegacyMax(new[]
            {
                ScaleLighting(partialSmall, partialFactor),
                partialBig,
            });
            Near(partialExpected, partial,
                $"{style}の部分重なりはsmall rimを半分だけ残し、big滴の陰影とmax合成する必要があります。");
        }
    }

    /// <summary>
    /// 可視性の0→1→0遷移、ゼロ可視サンプル、輪郭不透明度の既存合成契約を確認します。
    /// </summary>
    public static void VerifyVisibilityTransitions()
    {
        foreach (var style in new[] { DropletStyle.Transparent, DropletStyle.Legacy })
        {
            var smallRim = EvaluateCircle(style, new Vector2(0.9f, 0f), 1f);
            var bigInterior = EvaluateCircle(style, new Vector2(0.75f, 0f), 1f);
            var previous = Vector3.Zero;
            for (var index = 0; index <= 1_000; index++)
            {
                var visibility = index / 1_000f;
                var forward = Combine(new[]
                {
                    ScaleAll(smallRim, visibility),
                    ScaleAll(bigInterior, visibility),
                });
                var reverse = Combine(new[]
                {
                    ScaleAll(bigInterior, visibility),
                    ScaleAll(smallRim, visibility),
                });
                Near(forward, reverse,
                    $"{style}の可視性{visibility:F3}はsample順序で変化してはいけません。");
                if (index > 0)
                {
                    Check(Vector3.Distance(previous, forward) < 0.01f,
                        $"{style}の可視性遷移は幅{SuppressionWidth:F2}でも段差を作ってはいけません。");
                }

                previous = forward;
            }

            // 一方の滴だけが消える/現れる場合も、深さ順位の切替で跳躍させない。
            foreach (var fadeSmall in new[] { false, true })
            {
                var last = Vector3.Zero;
                for (var index = 0; index <= 4_000; index++)
                {
                    var visibility = index <= 2_000 ? index / 2_000f : (4_000 - index) / 2_000f;
                    var small = fadeSmall ? ScaleAll(smallRim, visibility) : smallRim;
                    var big = fadeSmall ? bigInterior : ScaleAll(bigInterior, visibility);
                    var value = Combine(new[] { small, big });
                    if (index > 0)
                    {
                        Check(Vector3.Distance(last, value) < 0.01f,
                            "片側の可視率だけが変化しても輪郭を不連続に切り替えてはいけません。");
                    }
                    last = value;
                }
            }

            var fullyVisible = Combine(new[] { smallRim, bigInterior });
            var invisible = Combine(new[] { ScaleAll(smallRim, 0f), ScaleAll(bigInterior, 0f) });
            Near(Vector3.Zero, invisible,
                $"{style}の可視性0では陰影と被覆を残してはいけません。");
            Near(fullyVisible, Combine(new[] { smallRim, bigInterior, ScaleAll(smallRim, 0f) }),
                $"{style}の透明サンプルは最大深さ、陰影、被覆へ影響してはいけません。");
        }

        var scene = new Vector3(0.8f, 0.4f, 0.2f);
        var opaqueRim = new Vector3(0f, 1f, 1f);
        foreach (var outlineOpacity in new[] { 0f, 0.5f, 1f })
        {
            var composited = CompositeTransparentOutline(scene, opaqueRim, outlineOpacity);
            var expected = scene * (1f - outlineOpacity);
            Near(expected, composited,
                $"黒い輪郭の不透明度{outlineOpacity * 100f:F0}%は既存の飽和後合成を維持する必要があります。");

        }
    }

    /// <summary>
    /// 静止3滴と落下18滴の21枠で、順序に依存しない結果と被覆maxの保持を確認します。
    /// </summary>
    public static void VerifyOrderAndCoverage()
    {
        var samples = new Vector4[21];
        samples[0] = EvaluateCircle(DropletStyle.Transparent, new Vector2(0.9f, 0f), 1f);
        samples[1] = EvaluateCircle(DropletStyle.Legacy, new Vector2(0.75f, 0f), 1f);
        samples[2] = EvaluateCircle(DropletStyle.Transparent, new Vector2(0.87f, 0f), 0.8f);
        samples[3] = EvaluateCircle(DropletStyle.Legacy, new Vector2(0.97f, 0f), 0.7f);
        samples[9] = EvaluateCircle(DropletStyle.Transparent, new Vector2(0.8f, 0f), 0.45f);
        samples[20] = EvaluateCircle(DropletStyle.Legacy, new Vector2(1.2f, 0f), 1f);

        var expectedCoverage = samples.Max(sample => sample.Z);
        var baseline = Combine(samples);
        Near(expectedCoverage, baseline.Z,
            "21枠合成の被覆は抑制前と同じ最大被覆を保持する必要があります。");

        var reversed = samples.Reverse().ToArray();
        Near(baseline, Combine(reversed),
            "21枠合成はsample配列を反転しても決定的である必要があります。");

        var rotated = new Vector4[21];
        for (var index = 0; index < samples.Length; index++)
        {
            rotated[index] = samples[(index * 13) % samples.Length];
        }

        Near(baseline, Combine(rotated),
            "静止3滴と落下18滴の21枠は列挙順で陰影を変えてはいけません。");
        Near(baseline, Combine(samples),
            "同じ21枠を再評価した結果は決定的である必要があります。");
    }

    private static Vector4 EvaluateCircle(DropletStyle style, Vector2 offset, float visibility)
    {
        var distance = offset.Length();
        var coverage = 1f - Smooth(1f - EdgeWidth, 1f + EdgeWidth, distance);
        var lighting = style == DropletStyle.Transparent
            ? EvaluateTransparentLighting(offset, distance, coverage)
            : EvaluateLegacyLighting(offset, distance, coverage);
        visibility = Saturate(visibility);
        return new Vector4(lighting * visibility, Saturate(1f - distance) * visibility);
    }

    private static Vector3 EvaluateTransparentLighting(Vector2 offset, float distance, float coverage)
    {
        var direction = offset / Math.Max(distance, 0.0001f);
        var lowerSide = Saturate(direction.Y * 0.5f + 0.5f);
        var rimWidth = Lerp(0.08f, 0.22f, lowerSide);
        var rim = Smooth(1f - rimWidth - EdgeWidth, 1f - rimWidth + EdgeWidth, distance) * coverage;
        var shadow = rim * Lerp(0.62f, 0.96f, lowerSide);
        var highlightSpot = 1f - Smooth(0.10f, 0.25f,
            ((offset - new Vector2(-0.28f, -0.62f)) * new Vector2(0.8f, 1.4f)).Length());
        return new Vector3(highlightSpot * coverage * (1f - rim) * 0.65f, shadow, coverage);
    }

    // HLSLの白い輪郭（outsideDropletAppearance < 0.5）の数式参照です。
    private static Vector3 EvaluateLegacyLighting(Vector2 offset, float distance, float coverage)
    {
        var rim = Smooth(0.55f, 0.92f, distance) * coverage;
        var direction = offset / Math.Max(distance, 0.0001f);
        var lightDirection = Saturate(Vector2.Dot(direction, Vector2.Normalize(new Vector2(-1f, -1f))) * 0.5f + 0.5f);
        var highlightSpot = (1f - Smooth(0.08f, 0.30f,
            Vector2.Distance(offset, new Vector2(-0.34f, -0.34f)))) * coverage;
        var highlight = Saturate(rim * lightDirection * 0.55f + highlightSpot * 0.85f);
        var shadow = rim * (1f - lightDirection) * 0.75f;
        return new Vector3(highlight, shadow, coverage);
    }

    private static Vector3 Combine(IReadOnlyList<Vector4> samples)
    {
        var maximumDepth = 0f;
        for (var index = 0; index < samples.Count; index++)
        {
            maximumDepth = Math.Max(maximumDepth, samples[index].W);
        }

        var lighting = Vector3.Zero;
        for (var index = 0; index < samples.Count; index++)
        {
            var sample = samples[index];
            var visibleEdge = SuppressionFactor(maximumDepth - sample.W);
            lighting.X = Math.Max(lighting.X, sample.X * visibleEdge);
            lighting.Y = Math.Max(lighting.Y, sample.Y * visibleEdge);
            lighting.Z = Math.Max(lighting.Z, sample.Z);
        }

        return lighting;
    }

    private static Vector3 LegacyMax(IReadOnlyList<Vector4> samples)
    {
        var lighting = Vector3.Zero;
        for (var index = 0; index < samples.Count; index++)
        {
            lighting.X = Math.Max(lighting.X, samples[index].X);
            lighting.Y = Math.Max(lighting.Y, samples[index].Y);
            lighting.Z = Math.Max(lighting.Z, samples[index].Z);
        }

        return lighting;
    }

    private static Vector4 ScaleAll(Vector4 sample, float scale) => sample * Saturate(scale);

    private static Vector4 ScaleLighting(Vector4 sample, float scale) => new(sample.X * scale, sample.Y * scale, sample.Z, sample.W);

    private static float SuppressionFactor(float depthDifference) =>
        1f - Smooth(0f, SuppressionWidth, depthDifference);

    private static Vector3 CompositeTransparentOutline(Vector3 scene, Vector3 lighting, float outlineOpacity)
    {
        var outlineAlpha = Saturate(lighting.Y * 1.75f) * Saturate(outlineOpacity);
        var darkened = Vector3.Lerp(scene, Vector3.Zero, outlineAlpha);
        return Vector3.Lerp(darkened, Vector3.One, Saturate(lighting.X * 0.65f));
    }

    private static float Smooth(float start, float end, float value)
    {
        var amount = Saturate((value - start) / (end - start));
        return amount * amount * (3f - 2f * amount);
    }

    private static float Saturate(float value) => Math.Clamp(value, 0f, 1f);

    private static float Lerp(float from, float to, float amount) => from + (to - from) * amount;

    private static void Near(float expected, float actual, string message) =>
        Check(MathF.Abs(expected - actual) < 0.000001f, message + $" expected={expected:F6}, actual={actual:F6}");

    private static void Near(Vector3 expected, Vector3 actual, string message) =>
        Check(Vector3.Distance(expected, actual) < 0.000001f, message);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private enum DropletStyle
    {
        Transparent,
        Legacy,
    }
}
