// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using YMM4GlassWipe;

namespace YMM4GlassWipe.Verification;

/// <summary>
/// CPU合体スケジュールの決定性、上限、時間連続性を確認します。
/// 実機YMM4のGPU描画は、このCPU検証の対象外です。
/// </summary>
internal static class OutsideDropletMergeSimulationVerification
{
    public static void Run()
    {
        VerifyNoContact();
        VerifyContactsAndOrder();
        VerifyPositionContinuityAndCaps();
        VerifyEpochBoundary();
        VerifyBoundsAndQuadMappings();
        VerifyRandomSeekDeterminism();
    }

    private static void VerifyNoContact()
    {
        var simulation = new OutsideDropletMergeSimulation();
        var constants = CreateConstants(amount: 0f, time: 4f);
        var frame = simulation.Evaluate(in constants);
        Check(frame.IsValid, "Amount 0では有効な空スケジュールを返す必要があります。");
        Check(frame.Heads.Count == 18, "Head配列は無効化しても18件固定である必要があります。");
        Check(frame.StaticChanges.Count == 0, "静止滴が無効なら吸収変更を作成してはいけません。");
    }

    private static void VerifyContactsAndOrder()
    {
        var frame = FindContactFrame();
        Check(frame.StaticChanges.Count >= 2, "十分な密度では複数の静止滴を吸収する必要があります。");
        var distinct = new HashSet<(uint Layer, int X, int Y)>();
        foreach (var change in frame.StaticChanges)
        {
            Check(distinct.Add((change.Layer, change.CellX, change.CellY)),
                "同じ静止セルを1epochで2回吸収してはいけません。");
            Check(change.Visibility is >= 0f and <= 1f, "静止滴可視性は[0, 1]である必要があります。");
        }
    }

    private static void VerifyPositionContinuityAndCaps()
    {
        var simulation = new OutsideDropletMergeSimulation();
        var before = CreateConstants(time: 5.000f);
        var after = CreateConstants(time: 5.001f);
        var first = simulation.Evaluate(in before);
        var second = simulation.Evaluate(in after);
        Check(first.IsValid && second.IsValid, "連続時刻の評価は有効である必要があります。");
        for (var index = 0; index < first.Heads.Count; index++)
        {
            var a = first.Heads[index];
            var b = second.Heads[index];
            Check(Vector2.Distance(a.InputUv, b.InputUv) < 0.01f,
                "合体前後を含む微小時間差で位置が跳んではいけません。");
            var layerCellSize = index < 6 ? 20f : index < 12 ? 44f : 92f;
            var maximumRadius = layerCellSize * MathF.Max(360f / 1080f, 0.25f) * 0.21f * 1.35f;
            Check(a.Radius <= maximumRadius + 0.0001f,
                "合体後の半径は1.35倍を超えてはいけません。");
        }
    }

    private static void VerifyEpochBoundary()
    {
        var simulation = new OutsideDropletMergeSimulation();
        var before = CreateConstants(time: 10.999f);
        var after = CreateConstants(time: 11.001f);
        var prior = simulation.Evaluate(in before);
        var next = simulation.Evaluate(in after);
        Check(prior.IsValid && next.IsValid, "epoch境界の前後で旧描画へのフォールバックを起こしてはいけません。");
        Check(next.Heads.All(head => head.Visibility is >= 0f and <= 1f),
            "epoch境界でhead可視性が範囲外になってはいけません。");
    }

    private static void VerifyBoundsAndQuadMappings()
    {
        foreach (var condition in new[]
        {
            (Width: 1920f, Height: 1080f, Size: 1f, Speed: 1f, Quad: false, Deform: 1f),
            (Width: 3840f, Height: 2160f, Size: 0.25f, Speed: 1f, Quad: false, Deform: 1f),
            (Width: 1920f, Height: 1080f, Size: 0.25f, Speed: 4f, Quad: false, Deform: 1f),
            (Width: 1920f, Height: 1080f, Size: 1f, Speed: 1f, Quad: true, Deform: 1f),
            (Width: 1920f, Height: 1080f, Size: 1f, Speed: 1f, Quad: true, Deform: 0f),
        })
        {
            var simulation = new OutsideDropletMergeSimulation();
            var constants = CreateConstants(
                time: 5f,
                width: condition.Width,
                height: condition.Height,
                sizeScale: condition.Size,
                speed: condition.Speed,
                isQuad: condition.Quad,
                deformWithSurface: condition.Deform);
            var frame = simulation.Evaluate(in constants);
            Check(frame.IsValid, "解像度・サイズ・速度・Quad条件で探索上限を黙って欠落扱いしてはいけません。");
            Check(frame.Heads.Count == 18, "全bounds条件でhead配列は18件固定である必要があります。");
            Check(frame.StaticChanges.Count > 0, "透視変形を含む密な通常領域で吸収候補を失ってはいけません。");
            Check(frame.Heads.All(head =>
                float.IsFinite(head.InputUv.X) &&
                float.IsFinite(head.InputUv.Y) &&
                float.IsFinite(head.Radius) &&
                float.IsFinite(head.VerticalScale) &&
                head.Visibility is >= 0f and <= 1f),
                "全bounds条件のhead状態は有限値かつ可視性範囲内である必要があります。");
        }
    }
    private static void VerifyRandomSeekDeterminism()
    {
        var random = new Random(12345);
        var requests = Enumerable.Range(0, 1000)
            .Select(index => (Time: (float)(random.NextDouble() * 88d), Seed: (uint)(index % 9 + 1)))
            .ToArray();
        var first = new OutsideDropletMergeSimulation();
        var expected = new OutsideDropletMergeFrame[requests.Length];
        for (var index = 0; index < requests.Length; index++)
        {
            var constants = CreateConstants(time: requests[index].Time, seed: requests[index].Seed);
            expected[index] = first.Evaluate(in constants);
            Check(expected[index].IsValid, "通常条件の1000 random seekで無効frameを返してはいけません。");
        }

        var order = Enumerable.Range(0, requests.Length).OrderBy(_ => random.Next()).ToArray();
        var replay = new OutsideDropletMergeSimulation();
        foreach (var index in order)
        {
            var constants = CreateConstants(time: requests[index].Time, seed: requests[index].Seed);
            var actual = replay.Evaluate(in constants);
            Check(actual.IsValid, "順不同random seekで無効frameを返してはいけません。");
            Check(expected[index].Heads.SequenceEqual(actual.Heads),
                "random seekでhead状態が評価順序に依存してはいけません。");
            Check(expected[index].StaticChanges.SequenceEqual(actual.StaticChanges) &&
                expected[index].StaticLayout!.PackedStates.SequenceEqual(actual.StaticLayout!.PackedStates),
                "random seekで吸収状態が評価順序に依存してはいけません。");
        }

        foreach (var index in Enumerable.Range(0, 32))
        {
            var constants = CreateConstants(time: requests[index].Time, seed: requests[index].Seed);
            var cold = new OutsideDropletMergeSimulation().Evaluate(in constants);
            Check(cold.IsValid, "通常条件のcold評価で無効frameを返してはいけません。");
            Check(expected[index].Heads.SequenceEqual(cold.Heads) &&
                expected[index].StaticChanges.SequenceEqual(cold.StaticChanges) &&
                expected[index].StaticLayout!.PackedStates.SequenceEqual(cold.StaticLayout!.PackedStates),
                "代表cold評価はcache有無で同じ結果を返す必要があります。");
        }
    }
    private static OutsideDropletMergeFrame FindContactFrame()
    {
        for (uint seed = 1; seed <= 24; seed++)
        {
            var simulation = new OutsideDropletMergeSimulation();
            var constants = CreateConstants(time: 5f, seed: seed);
            var frame = simulation.Evaluate(in constants);
            if (frame.IsValid && frame.StaticChanges.Count >= 2)
            {
                return frame;
            }
        }

        throw new InvalidOperationException("固定探索範囲で合体候補を再現できませんでした。");
    }

    private static GlassCompositeConstants CreateConstants(
        float amount = 1f,
        float time = 5f,
        uint seed = 1,
        float width = 640f,
        float height = 360f,
        float sizeScale = 1f,
        float speed = 1f,
        bool isQuad = false,
        float deformWithSurface = 1f) => new()
        {
            InputWidth = width,
            InputHeight = height,
            RegionCenterX = 0.5f,
            RegionCenterY = 0.5f,
            RegionWidth = 1f,
            RegionHeight = 1f,
            RegionRotationCos = 1f,
            RegionRotationSin = 0f,
            RegionShape = isQuad ? 3f : 0f,
            QuadValid = 1f,
            OutsideDropletAmount = amount,
            OutsideDropletSizeScale = sizeScale,
            OutsideDropletSeed = seed,
            OutsideDropletLocalTimeSeconds = time,
            OutsideDropletFallingRatio = 1f,
            OutsideDropletFallSpeedScale = speed,
            OutsideDropletDeformWithSurface = deformWithSurface,
            // 上辺と下辺の縮尺が異なる透視Quad。順逆行列を対にする。
            QuadInverseM11 = isQuad ? 0.6125f : 1f,
            QuadInverseM12 = isQuad ? 0.025f : 0f,
            QuadInverseM13 = isQuad ? -0.065f : 0f,
            QuadInverseM22 = isQuad ? 0.75f : 1f,
            QuadInverseM23 = isQuad ? -0.1125f : 0f,
            QuadInverseM32 = isQuad ? -0.1875f : 0f,
            QuadInverseM33 = isQuad ? 0.4875f : 1f,
            QuadForwardM11 = isQuad ? 0.75f : 1f,
            QuadForwardM13 = isQuad ? 0.10f : 0f,
            QuadForwardM22 = isQuad ? 0.65f : 1f,
            QuadForwardM23 = isQuad ? 0.15f : 0f,
            QuadForwardM32 = isQuad ? 0.25f : 0f,
            QuadForwardM33 = 1f,
        };

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}