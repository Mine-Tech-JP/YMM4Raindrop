// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using YMM4GlassWipe;

namespace YMM4GlassWipe.Verification;

internal static class OutsideDropletAbsorptionVerification
{
    public static void VerifyWholeHeadAbsorptionAndStaticGrowth()
    {
        const float speed = 1.892f;
        var constants = CreateConstants();
        var simulation = new OutsideDropletMergeSimulation();
        var initial = simulation.Evaluate(constants);
        Check(initial.IsValid && initial.Heads.All(head => head.Visibility == 0f),
            "出生前に落下滴を表示してはいけません。");
        var shrunk = new Dictionary<int, float>();
        var staticGrowth = false;
        var waiting = 0;
        for (var i = 0; i < 660; i++)
        {
            constants.OutsideDropletLocalTimeSeconds = i / 60f / speed;
            var frame = simulation.Evaluate(constants);
            Check(frame.IsValid, "実使用設定の連続frameは有効である必要があります。");
            foreach (var head in frame.Heads)
            {
                var originalRadius = initial.Heads[head.EmitterIndex].Radius;
                if (head.Visibility > 0.01f && head.Radius < originalRadius * 0.99f)
                    shrunk.TryAdd(head.EmitterIndex, constants.OutsideDropletLocalTimeSeconds);
                if (shrunk.TryGetValue(head.EmitterIndex, out var began) &&
                    constants.OutsideDropletLocalTimeSeconds > began + 0.20f / speed)
                    Check(head.Visibility == 0f, "吸収された小headは同じ区間で再出現してはいけません。");
                if (head.Visibility > 0.01f && head.FallProgress == 0f && head.Radius >= originalRadius)
                {
                    Check(head.InputUv == head.SpawnInputUv, "待機中の成長だけで落下位置が動いてはいけません。");
                    waiting++;
                }
            }
            foreach (var change in frame.StaticChanges)
            {
                frame.StaticLayout!.TryGetState(change.Layer, change.CellX, change.CellY, out _, out var scale);
                staticGrowth |= change.RadiusScale > scale + 0.0001f;
            }
            Check(new GlassCompositeGpuConstants().TryApply(frame),
                "実使用設定の各frameをGPU状態へ転送できる必要があります。");
        }
        Check(shrunk.Count > 0, "小headの全体縮小と消失を実際に検出する必要があります。");
        Check(staticGrowth, "大きい静止滴が小headを吸収した成長を検出する必要があります。");
        Check(waiting > 10, "複数の待機frameを検証する必要があります。");
    }

    public static void VerifyInitialAbsorptionInsideEllipse()
    {
        var constants = CreateConstants();
        constants.RegionShape = 2f;
        var frame = new OutsideDropletMergeSimulation().Evaluate(constants);
        Check(frame.IsValid && frame.StaticLayout is not null, "楕円領域の初期吸収を作成できます。");
        Check(frame.StaticLayout!.Changes.Count > 0, "楕円内にも初期吸収が存在する必要があります。");
        foreach (var change in frame.StaticLayout.Changes)
        {
            var cellSize = (change.Layer == 101u ? 20f : change.Layer == 307u ? 44f : 92f) * constants.OutsideDropletSizeScale;
            var center = (new Vector2(change.CellX, change.CellY) + new Vector2(
                0.25f + 0.5f * OutsideDropletRandom.Sample(change.CellKey, 1u),
                0.25f + 0.5f * OutsideDropletRandom.Sample(change.CellKey, 2u))) * cellSize;
            var uv = center / new Vector2(constants.InputWidth, constants.InputHeight);
            Check(Vector2.DistanceSquared(uv, new Vector2(0.5f)) <= 0.250001f,
                "楕円外の静止滴を吸収の所有先・対象に含めてはいけません。");
        }
    }

    public static void VerifyFallingHeadPairAbsorption()
    {
        var constants = CreateConstants();
        constants.OutsideDropletAmount = 1f;
        constants.OutsideDropletSeed = 33f;
        var simulation = new OutsideDropletMergeSimulation();
        constants.OutsideDropletLocalTimeSeconds = 1.244f;
        var before = simulation.Evaluate(constants);
        constants.OutsideDropletLocalTimeSeconds = 1.352f;
        var after = simulation.Evaluate(constants);
        Check(before.IsValid && after.IsValid, "落下滴同士の固定接触条件は有効である必要があります。");
        var small = before.Heads[6];
        var large = before.Heads[15];
        Check(small.Visibility > 0.9f && large.Visibility > 0.9f &&
            small.Radius * small.Radius * small.VerticalScale < large.Radius * large.Radius * large.VerticalScale,
            "接触前には大きさの異なる2滴が存在する必要があります。");
        var distance = Vector2.Distance(small.InputUv * new Vector2(1920, 1080), large.InputUv * new Vector2(1920, 1080));
        Check(distance < small.Radius * MathF.Max(1f, small.VerticalScale) + large.Radius * MathF.Max(1f, large.VerticalScale) + 1f,
            "固定接触の2滴は互いに接近している必要があります。");
        Check(after.Heads[6].Visibility == 0f && after.Heads[15].Visibility > 0.9f &&
            after.Heads[15].Radius > large.Radius,
            "小さい落下滴全体が消失し、大きい落下滴だけが成長して残る必要があります。");
    }

    private static GlassCompositeConstants CreateConstants() => new()
    {
        InputWidth = 1920f,
        InputHeight = 1080f,
        RegionCenterX = 0.5f,
        RegionCenterY = 0.5f,
        RegionWidth = 1f,
        RegionHeight = 1f,
        RegionRotationCos = 1f,
        QuadValid = 1f,
        OutsideDropletAmount = 0.614f,
        OutsideDropletSizeScale = 2.358f,
        OutsideDropletSeed = 4197f,
        OutsideDropletFallingRatio = 1f,
        OutsideDropletFallSpeedScale = 1.892f,
        OutsideDropletDeformWithSurface = 1f,
    };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
