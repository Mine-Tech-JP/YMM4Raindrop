// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace YMM4GlassWipe.Verification;

/// <summary>
/// 外側水滴のセル境界と水筋について、HLSL数式をCPUで限定的に再現します。
/// この検証はGPUのラスタライザー、画面微分、または実機YMM4の描画結果を証明するものではありません。
/// </summary>
public static class OutsideDropletArtifactVerification
{
    private const float SizeScale149_5Percent = 1.495f;
    private const uint Seed = 1u;

    /// <summary>
    /// 整数セル乱数によりセル境界で旧fwidthが水滴外を被覆する反例を探し、
    /// 連続座標から求めるpixelStep方式では同じ点を被覆しないことを確認します。
    /// </summary>
    public static void VerifyStaticCellBoundaries()
    {
        var layers = new[]
        {
            new StaticLayer(20f * SizeScale149_5Percent, 101u, 1f),
            new StaticLayer(44f * SizeScale149_5Percent, 307u, 0.65f),
            new StaticLayer(92f * SizeScale149_5Percent, 701u, 0.25f),
        };

        foreach (var layer in layers)
        {
            var witness = FindCellBoundaryWitness(layer);
            Console.WriteLine(
                $"[INFO] セル境界 layer={layer.Layer}: 旧被覆={witness.LegacyCoverage:F6}, 新被覆={witness.ContinuousCoverage:F6}, 距離={witness.Distance:F6}");
            Check(witness.Found,
                $"セルサイズ{layer.CellSizePixels:F3}px、レイヤー{layer.Layer}で旧fwidthの境界被覆反例が見つかりません。");
            Check(witness.LegacyCoverage >= 0.15f,
                "旧fwidthの反例は水滴外に可視の被覆を作る必要があります。");
            Check(witness.ContinuousCoverage <= 0.0001f,
                "連続座標pixelStep方式は同じ水滴外のセル境界を被覆してはいけません。");
            Check(witness.Distance > 1.05f,
                "反例点は水滴被覆の外側である必要があります。");
            Check(witness.LegacyEdgeWidth > witness.ContinuousEdgeWidth * 2f,
                "旧fwidthはセル境界の不連続で画素幅より過大になる必要があります。");
        }
    }

    /// <summary>
    /// 水筋の先細り、後端フェード、滴との合成、極小入力、およびライフサイクル倍率を確認します。
    /// </summary>
    public static void VerifyTrailContinuity()
    {
        var dropletRadius = 12f;
        var tailRadius = TrailRadius(dropletRadius, 0f);
        var middleRadius = TrailRadius(dropletRadius, 0.5f);
        var headRadius = TrailRadius(dropletRadius, 1f);
        Check(tailRadius < middleRadius && middleRadius < headRadius,
            "水筋は後端から頭部へ連続して太くなる必要があります。");
        Check(Near(TrailFade(0f, 40f), 0f),
            "水筋の後端はフェードにより被覆ゼロである必要があります。");
        Check(TrailFade(0.5f, 0f) == 0f && TrailFade(0.5f, 0.001f) >= 0f,
            "ゼロ長と極小長の水筋フェードは有限で負になってはいけません。");

        var start = new Vector2(0f, -45f);
        var end = new Vector2(0f, -7f);
        var tail = CapsuleLighting(new Vector2(0f, -45f), start, end, tailRadius, 1f) * TrailFade(0f, 38f);
        Check(IsZero(tail), "後端のカプセル端はフェード後に陰影を残してはいけません。");

        var outside = CapsuleLighting(new Vector2(headRadius + 3f, -25f), start, end, middleRadius, 1f) * TrailFade(0.5f, 38f);
        Check(IsZero(outside), "水筋の被覆外には陰影を残してはいけません。");

        var dropletInterior = new Vector3(0.08f, 0.73f, 1f);
        var trailInterior = new Vector3(0.05f, 0.62f, 1f);
        var mergedInterior = Merge(dropletInterior, trailInterior);
        Check(Near(mergedInterior.X, 0f) && Near(mergedInterior.Y, 0f) && Near(mergedInterior.Z, 1f),
            "水滴の内部では水筋の輪郭陰影を残してはいけません。");

        var previous = Vector3.Zero;
        for (var index = 0; index <= 128; index++)
        {
            var progress = index / 128f;
            var radius = TrailRadius(dropletRadius, progress);
            var fade = TrailFade(progress, 38f);
            var lighting = CapsuleLighting(new Vector2(radius * 0.62f, -45f + 38f * progress), start, end, radius, 1f) * fade;
            Check(IsFiniteUnit(lighting), "水筋の途中は有限な被覆と陰影である必要があります。");
            if (index > 0)
            {
                Check(Vector3.Distance(previous, lighting) < 0.12f,
                    "水筋の先細りとフェードは途中で段差を作ってはいけません。");
            }
            previous = lighting;
        }

        foreach (var radius in new[] { 0f, 0.00001f, 0.5f })
        {
            var value = CapsuleLighting(Vector2.Zero, Vector2.Zero, Vector2.Zero,
                Math.Max(radius * 0.20f, 0.50f), 1f);
            Check(IsFiniteUnit(value), "小半径またはゼロ長の参照式は有限である必要があります。");
        }
        Check(!ShouldDrawTrail(1f, 0f, 20f, 400f) &&
              !ShouldDrawTrail(1f, 0.5f, 0f, 400f) &&
              !ShouldDrawTrail(1f, 0.5f, 20f, 0.0001f),
            "ゼロまたは極小長の水筋はカプセル評価へ進んではいけません。");

        var lifecycleVisibility = 0.5f;
        var mergedBeforeLifecycle = Merge(dropletInterior, trailInterior) * lifecycleVisibility;
        var mergedAfterEarlyLifecycle = Merge(
            dropletInterior * lifecycleVisibility,
            trailInterior * lifecycleVisibility);
        Check(Near(mergedBeforeLifecycle.X, 0f) && Near(mergedBeforeLifecycle.Y, 0f) &&
              Near(mergedBeforeLifecycle.Z, lifecycleVisibility) && mergedAfterEarlyLifecycle.Y > 0.01f,
            "ライフサイクル可視度は滴と水筋の合成後に掛け、半透明の滴内部へ水筋の線を再出現させてはいけません。");
        Check(IsZero(Merge(dropletInterior, trailInterior) * 0f),
            "ライフサイクル可視度がゼロのとき、滴と水筋の合成結果はゼロである必要があります。");
    }

    private static BoundaryWitness FindCellBoundaryWitness(StaticLayer layer)
    {
        var best = BoundaryWitness.None;
        var cellSize = layer.CellSizePixels;
        for (var verticalBoundary = 1; verticalBoundary * cellSize < 1920f; verticalBoundary++)
        {
            var x = verticalBoundary * cellSize - 0.25f;
            for (var y = 0.5f; y < 1080f; y += 1f)
            {
                var candidate = EvaluateBoundary(new Vector2(x, y), Vector2.UnitX, layer);
                best = BoundaryWitness.Prefer(best, candidate);
            }
        }

        for (var horizontalBoundary = 1; horizontalBoundary * cellSize < 1080f; horizontalBoundary++)
        {
            var y = horizontalBoundary * cellSize - 0.25f;
            for (var x = 0.5f; x < 1920f; x += 1f)
            {
                var candidate = EvaluateBoundary(new Vector2(x, y), Vector2.UnitY, layer);
                best = BoundaryWitness.Prefer(best, candidate);
            }
        }
        return best;
    }

    private static BoundaryWitness EvaluateBoundary(Vector2 position, Vector2 boundaryDirection, StaticLayer layer)
    {
        var distance = StaticDistance(position, layer);
        if (distance <= 1.05f)
        {
            return BoundaryWitness.None;
        }

        var state = StaticState(position, layer);
        if (state.Activation > layer.ActivationScale)
        {
            return BoundaryWitness.None;
        }

        var acrossBoundary = StaticDistance(position + boundaryDirection, layer);
        var alongBoundary = StaticDistance(position + new Vector2(boundaryDirection.Y, boundaryDirection.X), layer);
        var legacyEdgeWidth = Math.Max(Math.Abs(acrossBoundary - distance) + Math.Abs(alongBoundary - distance), 0.001f);
        var continuousEdgeWidth = Math.Max(1f /
            Math.Max(layer.CellSizePixels * state.Radius * Math.Min(1f, state.VerticalScale), 0.001f), 0.001f);
        var legacyCoverage = Coverage(distance, legacyEdgeWidth);
        var continuousCoverage = Coverage(distance, continuousEdgeWidth);
        return new BoundaryWitness(distance, legacyEdgeWidth, continuousEdgeWidth, legacyCoverage, continuousCoverage);
    }

    private static float StaticDistance(Vector2 position, StaticLayer layer)
    {
        var state = StaticState(position, layer);
        var localPosition = Frac(position / layer.CellSizePixels);
        return ((localPosition - state.Center) / new Vector2(state.Radius, state.Radius * state.VerticalScale)).Length();
    }

    private static CellState StaticState(Vector2 position, StaticLayer layer)
    {
        var cellPosition = position / layer.CellSizePixels;
        var cellX = (int)MathF.Floor(cellPosition.X);
        var cellY = (int)MathF.Floor(cellPosition.Y);
        var key = GetCellKey(cellX, cellY, Seed, layer.Layer);
        return new CellState(
            new Vector2(Lerp(0.25f, 0.75f, Random(key, 1u)), Lerp(0.25f, 0.75f, Random(key, 2u))),
            Lerp(0.11f, 0.21f, Random(key, 3u)),
            Lerp(0.88f, 1.18f, Random(key, 4u)),
            Random(key, 0u));
    }

    private static Vector3 CapsuleLighting(Vector2 sample, Vector2 start, Vector2 end, float radius, float edgeWidth)
    {
        var segment = end - start;
        var lengthSquared = Math.Max(Vector2.Dot(segment, segment), 0.0001f);
        var factor = Math.Clamp(Vector2.Dot(sample - start, segment) / lengthSquared, 0f, 1f);
        var nearest = start + segment * factor;
        var distance = Vector2.Distance(sample, nearest);
        edgeWidth = Math.Max(edgeWidth, 0.001f);
        var coverage = 1f - Smooth(Math.Max(radius - edgeWidth, 0f), radius + edgeWidth, distance);
        var lightSide = Math.Clamp(0.5f + (nearest.X - sample.X) / Math.Max(radius * 2f, 0.001f), 0f, 1f);
        var innerRadius = radius * 0.62f;
        var rim = Smooth(Math.Max(innerRadius - edgeWidth, 0f), innerRadius + edgeWidth, distance) * coverage;
        return new Vector3(rim * lightSide * 0.08f, rim * (0.35f + (1f - lightSide) * 0.40f), coverage);
    }

    private static Vector3 Merge(Vector3 droplet, Vector3 trail)
    {
        var dropletCoverage = Math.Clamp(droplet.Z, 0f, 1f);
        var trailCoverage = Math.Clamp(trail.Z, 0f, 1f);
        var visibleDroplet = new Vector2(droplet.X, droplet.Y) * (1f - trailCoverage);
        var visibleTrail = new Vector2(trail.X, trail.Y) * (1f - dropletCoverage) * 0.42f;
        return new Vector3(Math.Max(visibleDroplet.X, visibleTrail.X), Math.Max(visibleDroplet.Y, visibleTrail.Y),
            Math.Max(dropletCoverage, trailCoverage * 0.42f));
    }

    private static float TrailRadius(float dropletRadius, float progress) =>
        Math.Max(dropletRadius * 0.20f, 0.50f) * Lerp(0.12f, 1f, Smooth(0f, 1f, Math.Clamp(progress, 0f, 1f)));

    private static float TrailFade(float progress, float lengthPixels) =>
        Smooth(0f, 0.35f, Math.Clamp(progress, 0f, 1f)) * Smooth(0f, 2f, MathF.Sqrt(Math.Max(lengthPixels * lengthPixels, 0f)));

    private static bool ShouldDrawTrail(float lifecycleState, float trailLength, float fallDistance, float trailLengthSquared) =>
        lifecycleState >= 0.5f && trailLength > 0f && fallDistance > 0f && trailLengthSquared > 0.0001f;

    private static float Coverage(float distance, float edgeWidth) =>
        1f - Smooth(1f - edgeWidth, 1f + edgeWidth, distance);

    private static uint GetCellKey(int x, int y, uint seed, uint layer)
    {
        var key = Mix(seed ^ 0xa511e9b3u);
        key = Mix(key ^ (uint)x);
        key = Mix(key ^ (uint)y);
        return Mix(key ^ layer);
    }

    private static uint Mix(uint value)
    {
        value ^= value >> 16;
        value *= 0x7feb352du;
        value ^= value >> 15;
        value *= 0x846ca68bu;
        return value ^ (value >> 16);
    }

    private static float Random(uint key, uint channel) =>
        (Mix(key ^ Mix(channel + 0x9e3779b9u)) >> 8) * (1f / 16777216f);

    private static float Smooth(float from, float to, float value)
    {
        var amount = Math.Clamp((value - from) / (to - from), 0f, 1f);
        return amount * amount * (3f - 2f * amount);
    }

    private static float Lerp(float from, float to, float amount) => from + (to - from) * amount;

    private static Vector2 Frac(Vector2 value) => value - new Vector2(MathF.Floor(value.X), MathF.Floor(value.Y));

    private static bool IsFiniteUnit(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) &&
        value.X is >= 0f and <= 1f && value.Y is >= 0f and <= 1f && value.Z is >= 0f and <= 1f;

    private static bool IsZero(Vector3 value) => Vector3.DistanceSquared(value, Vector3.Zero) < 0.00000001f;

    private static bool Near(float left, float right) => Math.Abs(left - right) < 0.00001f;

    private static bool Near(Vector3 left, Vector3 right) => Vector3.DistanceSquared(left, right) < 0.00000001f;

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private readonly record struct StaticLayer(float CellSizePixels, uint Layer, float ActivationScale);

    private readonly record struct CellState(Vector2 Center, float Radius, float VerticalScale, float Activation);

    private readonly record struct BoundaryWitness(
        float Distance,
        float LegacyEdgeWidth,
        float ContinuousEdgeWidth,
        float LegacyCoverage,
        float ContinuousCoverage)
    {
        public static BoundaryWitness None => new(0f, 0f, 0f, 0f, 1f);

        public bool Found => LegacyCoverage >= 0.15f && ContinuousCoverage <= 0.0001f && Distance > 1.05f;

        public static BoundaryWitness Prefer(BoundaryWitness current, BoundaryWitness candidate) =>
            candidate.Found && (!current.Found || candidate.LegacyCoverage > current.LegacyCoverage) ? candidate : current;
    }
}
