// SPDX-License-Identifier: MPL-2.0

using YMM4GlassWipe;

namespace YMM4GlassWipe.Verification;

/// <summary>
/// 実際の連続フレーム評価から、水滴合体の吸収、加速、epoch末の再形成を確認します。
/// 実機YMM4のGPU描画は、このCPU検証の対象外です。
/// </summary>
internal static class OutsideDropletMergeBehaviorVerification
{
    private const float EpochSeconds = 11f;
    private const float DerivativeSeconds = 0.001f;
    private const float RadiusCap = 1.35f;
    private const float SpeedCap = 1.45f;
    private const float CapTolerance = 0.003f;
    private const int EpochFrameCount = 660;

    public static void Run()
    {
        var simulation = new OutsideDropletMergeSimulation();
        VerifyAllNormalFramesAreValid(simulation);
        VerifyFullEpochCaps(simulation);
        var evidence = FindAbsorptionEvidence(simulation);
        VerifyAbsorptionGrowthAndGpuTransport(evidence);
        VerifyEpochReformation(simulation);
    }

    private static void VerifyAllNormalFramesAreValid(
        OutsideDropletMergeSimulation simulation)
    {
        for (var frameIndex = 0; frameIndex <= 660; frameIndex++)
        {
            var time = frameIndex / 60f;
            var frame = Evaluate(simulation, time);
            Check(frame.IsValid,
                $"通常条件のframe {frameIndex}（{time:F3}秒）は有効である必要があります。");
            Check(frame.Heads.Count == OutsideDropletFrequency.BaseHeadCount,
                "通常条件のhead数は18件固定である必要があります。");
        }
    }

    private static AbsorptionEvidence FindAbsorptionEvidence(
        OutsideDropletMergeSimulation simulation)
    {
        for (var millisecond = 250; millisecond < 9_500; millisecond += 10)
        {
            var time = millisecond / 1_000f;
            var current = Evaluate(simulation, time);
            Check(current.IsValid, "吸収探索中のframeは有効である必要があります。");
            if (!current.StaticChanges.Any(change =>
                    change.Visibility is > 0.05f and < 0.95f))
            {
                continue;
            }

            var baseline = Evaluate(simulation, time - 0.12f);
            var accelerated = Evaluate(simulation, time + 0.05f);
            var sustained = Evaluate(simulation, time + 0.10f);
            var baselineNext = Evaluate(simulation, time - 0.12f + DerivativeSeconds);
            var acceleratedNext = Evaluate(simulation, time + 0.05f + DerivativeSeconds);
            Check(
                baseline.IsValid && accelerated.IsValid && sustained.IsValid &&
                baselineNext.IsValid && acceleratedNext.IsValid,
                "吸収前後の連続frameは有効である必要があります。");

            for (var index = 0; index < current.Heads.Count; index++)
            {
                var baseHead = baseline.Heads[index];
                var acceleratedHead = accelerated.Heads[index];
                var sustainedHead = sustained.Heads[index];
                if (baseHead.Visibility < 0.99f ||
                    acceleratedHead.Visibility < 0.99f ||
                    sustainedHead.Visibility < 0.99f ||
                    baseHead.FallProgress is < 0.05f or > 0.85f ||
                    acceleratedHead.FallProgress is < 0.05f or > 0.95f ||
                    baseHead.Radius <= 0f)
                {
                    continue;
                }

                var baseSpeed = GetYSpeed(
                    baseHead,
                    baselineNext.Heads[index]);
                var acceleratedSpeed = GetYSpeed(
                    acceleratedHead,
                    acceleratedNext.Heads[index]);
                var radiusRatio = acceleratedHead.Radius / baseHead.Radius;
                var speedRatio = acceleratedSpeed / baseSpeed;
                if (acceleratedHead.Radius <= baseHead.Radius + 0.001f ||
                    sustainedHead.Radius + 0.001f < acceleratedHead.Radius ||
                    baseSpeed <= 0f ||
                    acceleratedSpeed <= baseSpeed + 0.0001f)
                {
                    continue;
                }

                Check(radiusRatio <= RadiusCap + 0.0001f,
                    "吸収中の半径倍率は1.35を超えてはいけません。");
                Check(speedRatio <= SpeedCap + 0.001f,
                    "吸収中の速度倍率は1.45を超えてはいけません。");
                return new AbsorptionEvidence(current);
            }
        }

        throw new InvalidOperationException(
            "通常条件の連続評価で、半径増加とY方向加速が持続する吸収中headを検出できませんでした。");
    }

    private static void VerifyFullEpochCaps(
        OutsideDropletMergeSimulation simulation)
    {
        var baseFrame = Evaluate(simulation, 0f);
        Check(baseFrame.IsValid, "基準frameは有効である必要があります。");
        var baseRadii = baseFrame.Heads.Select(head => head.Radius).ToArray();
        var applicableCount = 0;
        for (var frameIndex = 0; frameIndex < EpochFrameCount; frameIndex++)
        {
            var time = frameIndex / 60f;
            var current = Evaluate(simulation, time);
            var next = Evaluate(simulation, time + DerivativeSeconds);
            Check(current.IsValid && next.IsValid, "上限検証中のframeは有効である必要があります。");
            for (var index = 0; index < current.Heads.Count; index++)
            {
                var head = current.Heads[index];
                var nextHead = next.Heads[index];
                if (!IsStableFallingHead(head, nextHead)) continue;
                // 合体前の仕様上の移動距離と所要秒から、入力UV毎秒の速度を求める。
                var cellSize = index < 6 ? 20f : index < 12 ? 44f : 92f;
                var sizeSpeed = index < 6 ? 0.9f : index < 12 ? 1f : 1.15f;
                var baseSpeed = (1080f + cellSize * 1.495f) / (6f / sizeSpeed) / 1080f;
                Check(head.Radius / baseRadii[index] <= RadiusCap + CapTolerance,
                    "全期間の半径倍率は1.35を超えてはいけません。");
                Check(GetYSpeed(head, nextHead) / baseSpeed <= SpeedCap + CapTolerance,
                    "全期間の実速度倍率は1.45を超えてはいけません。");
                applicableCount++;
            }
        }
        Check(applicableCount > 100, "複数の可視落下headを十分な期間検証する必要があります。");
    }

    private static void VerifyAbsorptionGrowthAndGpuTransport(
        AbsorptionEvidence evidence)
    {
        Check(
            evidence.ContactFrame.StaticChanges.Any(change =>
                change.Visibility is > 0.05f and < 0.95f),
            "吸収中frameでは可視性が遷移中の静止セルを検出する必要があります。");
        Check(
            new GlassCompositeGpuConstants().TryApply(evidence.ContactFrame),
            "吸収中の多数の静止セル変更は32probe制限内でGPU wrapperへ転送できる必要があります。");
    }

    private static void VerifyEpochReformation(
        OutsideDropletMergeSimulation simulation)
    {
        var beforeEpoch = Evaluate(simulation, 10.999f);
        var nextEpoch = Evaluate(simulation, EpochSeconds);
        Check(beforeEpoch.IsValid && nextEpoch.IsValid,
            "epoch境界の前後で有効なframeを返す必要があります。");
        Check(beforeEpoch.StaticChanges.Count > 0,
            "epoch末に再形成対象の静止セルを保持する必要があります。");
        Check(MatchesInitialLayout(beforeEpoch),
            "epoch末では静止セルが初期の統合済み配置へ戻る必要があります。");
        Check(beforeEpoch.Heads.All(head => head.Visibility <= 0.001f),
            "epoch末では全headを非表示にする必要があります。");
        Check(MatchesInitialLayout(nextEpoch),
            "次epochでも初期に吸収された小滴は非表示を維持する必要があります。");
        Check(
            new GlassCompositeGpuConstants().TryApply(beforeEpoch),
            "epoch末の多数の静止セル変更は32probe制限内でGPU wrapperへ転送できる必要があります。");
    }

    private static bool MatchesInitialLayout(OutsideDropletMergeFrame frame)
    {
        if (frame.StaticLayout is null) return false;
        foreach (var change in frame.StaticChanges)
        {
            frame.StaticLayout.TryGetState(change.Layer, change.CellX, change.CellY, out var visibility, out var scale);
            if (MathF.Abs(change.Visibility - visibility) > 0.001f ||
                MathF.Abs(change.RadiusScale - scale) > 0.001f) return false;
        }
        return frame.StaticLayout.Changes.Any(change => change.Visibility == 0f);
    }

    private static OutsideDropletMergeFrame Evaluate(
        OutsideDropletMergeSimulation simulation,
        float time)
    {
        var constants = new GlassCompositeConstants
        {
            InputWidth = 1920f,
            InputHeight = 1080f,
            RegionCenterX = 0.5f,
            RegionCenterY = 0.5f,
            RegionWidth = 1f,
            RegionHeight = 1f,
            RegionRotationCos = 1f,
            RegionRotationSin = 0f,
            RegionShape = 0f,
            QuadValid = 1f,
            OutsideDropletAmount = 1f,
            OutsideDropletSizeScale = 1.495f,
            OutsideDropletSeed = 1f,
            OutsideDropletLocalTimeSeconds = time,
            OutsideDropletFallingRatio = 1f,
            OutsideDropletFallSpeedScale = 1f,
            OutsideDropletDeformWithSurface = 1f,
            QuadInverseM11 = 1f,
            QuadInverseM22 = 1f,
            QuadInverseM33 = 1f,
            QuadForwardM11 = 1f,
            QuadForwardM22 = 1f,
            QuadForwardM33 = 1f,
        };
        return simulation.Evaluate(in constants);
    }

    private static float GetYSpeed(
        OutsideDropletMergeHeadState current,
        OutsideDropletMergeHeadState next) =>
        (next.InputUv.Y - current.InputUv.Y) / DerivativeSeconds;

    private static bool IsStableFallingHead(
        OutsideDropletMergeHeadState current,
        OutsideDropletMergeHeadState next) =>
        // 吸収先への移動は落下の加速ではないため、縮小を始めたheadを除く。
        next.Radius >= current.Radius - 0.000001f &&
        current.Visibility > 0.9f &&
        next.Visibility > 0.9f &&
        current.FallProgress is > 0.01f and < 0.9f &&
        next.FallProgress is > 0.01f and < 0.9f &&
        current.InputUv.Y is > 0.001f and < 0.999f &&
        next.InputUv.Y is > 0.001f and < 0.999f;

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private readonly record struct AbsorptionEvidence(
        OutsideDropletMergeFrame ContactFrame);
}
