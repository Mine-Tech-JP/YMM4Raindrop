// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using System.Runtime.InteropServices;
using YMM4GlassWipe;

namespace YMM4GlassWipe.Verification;

/// <summary>
/// 落下頻度のcohort拡張が、従来18本の順序と決定性を維持することを確認します。
/// 実機YMM4上の画素品質・再生負荷は、このCPU/GPU定数転送検証の対象外です。
/// </summary>
internal static class FallFrequencyRenderingVerification
{
    public static void Run()
    {
        VerifyFrequencyHelperBoundsAndCohorts();
        VerifyFrequencyOneBaselineAndFourfoldCandidates();
        VerifyFrequencySeekRainAndMotionInvariants();
        VerifyGpuTransportAndNonFiniteFallback();
        VerifyVisibleFallsIncrease();
    }

    private static void VerifyVisibleFallsIncrease()
    {
        foreach (var rain in new[] { false, true })
        {
            var totals = new int[2];
            for (var setting = 0; setting < 2; setting++)
            {
                var simulation = new OutsideDropletMergeSimulation();
                var constants = CreateConstants(setting == 0 ? 1f : 4f, 0f, 0U, rain);
                constants.OutsideDropletAmount = 0.7f;
                constants.OutsideDropletRainDurationSeconds = rain ? 10f : 0f;
                var gpu = new GlassCompositeGpuConstants();
                for (var frame = 0; frame <= 200; frame++)
                {
                    constants.OutsideDropletLocalTimeSeconds = frame / 10f;
                    var state = simulation.Evaluate(in constants);
                    True(state.IsValid && gpu.TryApply(state), "頻度比較の全フレームは合体描画へ転送できる必要があります。");
                    totals[setting] += state.Heads.Count(head => head.Visibility > 0.05f && head.FallProgress > 0.001f);
                }
            }
            True(totals[0] > 0 && totals[1] > totals[0],
                "頻度400%は候補数だけでなく、この基準条件で見える落下滴も増やす必要があります。");
        }
    }

    private static void VerifyFrequencyHelperBoundsAndCohorts()
    {
        Equal(18, OutsideDropletFrequency.GetHeadCount(1f), "頻度100%は従来どおり18候補です。");
        Equal(36, OutsideDropletFrequency.GetHeadCount(1.01f), "頻度100%超では2 cohortを確保します。");
        Equal(36, OutsideDropletFrequency.GetHeadCount(2f), "頻度200%は36候補です。");
        Equal(54, OutsideDropletFrequency.GetHeadCount(2.5f), "頻度250%は端数cohortを含む54候補です。");
        Equal(72, OutsideDropletFrequency.GetHeadCount(4f), "頻度400%は72候補です。");
        Equal(72, OutsideDropletFrequency.GetHeadCount(40f), "頻度上限を超えて72候補を超過してはいけません。");
        Equal(18, OutsideDropletFrequency.GetHeadCount(float.NaN), "非有限頻度は従来頻度へフォールバックします。");

        const uint seed = 4197;
        var sawEnabledPartial = false;
        var sawDisabledPartial = false;
        for (var index = 0; index < 72; index++)
        {
            var cohort = index / 18;
            var local = index % 18;
            var layerIndex = local / 6;
            var emitter = cohort * 6 + local % 6;
            var layer = new[] { 101U, 307U, 701U }[layerIndex];
            var key = OutsideDropletRandom.GetEmitterKey(seed, layer, (uint)emitter);

            var enabledAtOne = OutsideDropletFrequency.IsEmitterEnabled(key, (uint)emitter, 1f);
            Equal(cohort == 0, enabledAtOne, "頻度100%は旧cohortだけを有効にします。");

            var enabledAtFour = OutsideDropletFrequency.IsEmitterEnabled(key, (uint)emitter, 4f);
            True(enabledAtFour, "頻度400%は確保した全cohortを有効にします。");

            if (cohort != 2)
            {
                continue;
            }

            var expectedPartial = OutsideDropletRandom.Sample(key, 10U) < 0.5f;
            var actualPartial = OutsideDropletFrequency.IsEmitterEnabled(key, (uint)emitter, 2.5f);
            Equal(expectedPartial, actualPartial, "端数cohortは専用channel 10で決定します。");
            sawEnabledPartial |= actualPartial;
            sawDisabledPartial |= !actualPartial;
        }

        True(sawEnabledPartial && sawDisabledPartial,
            "実在するcohort 2で端数頻度の有効・無効を両方確認する必要があります。");
    }

    private static void VerifyFrequencyOneBaselineAndFourfoldCandidates()
    {
        var baselineConstants = CreateConstants(frequency: 1f, time: 5f, seed: 33U);
        var baseline = new OutsideDropletMergeSimulation().Evaluate(in baselineConstants);
        True(baseline.IsValid, "頻度100%の実条件は有効なframeを返す必要があります。");
        Equal(18, baseline.Heads.Count, "頻度100%の基準frameは18本です。");
        var baselineSnapshot = baseline.Heads.ToArray();

        var fourfoldConstants = baselineConstants;
        fourfoldConstants.OutsideDropletFallFrequency = 4f;
        var fourfold = new OutsideDropletMergeSimulation().Evaluate(in fourfoldConstants);
        True(fourfold.IsValid, "頻度400%の実条件は有効なframeを返す必要があります。");
        Equal(72, fourfold.Heads.Count, "頻度400%は18本から72候補へ増加します。");
        True(baselineSnapshot.Select(head => head.EmitterIndex).SequenceEqual(Enumerable.Range(0, 18)),
            "頻度100%の基準スナップショットは旧18本の索引順を維持する必要があります。");
        True(fourfold.Heads.Select(head => head.EmitterIndex).SequenceEqual(Enumerable.Range(0, 72)),
            "GPU転送前のhead索引は旧18本を先頭にした0から71の連番である必要があります。");

        for (var index = 0; index < fourfold.Heads.Count; index++)
        {
            var local = index % 18;
            var layer = local / 6;
            var expectedRadiusUpper = (layer switch
            {
                0 => 20f,
                1 => 44f,
                _ => 92f,
            }) * 0.21f * 1.35f;
            var head = fourfold.Heads[index];
            True(float.IsFinite(head.InputUv.X) && float.IsFinite(head.InputUv.Y) &&
                float.IsFinite(head.Radius) && float.IsFinite(head.VerticalScale) &&
                head.Visibility is >= 0f and <= 1f &&
                head.Radius >= 0f && head.Radius <= expectedRadiusUpper + 0.0001f,
                "増加したcohortも既存の半径上限と有限値契約を守る必要があります。");
        }
    }

    private static void VerifyFrequencySeekRainAndMotionInvariants()
    {
        var requests = new[]
        {
            CreateConstants(frequency: 4f, time: 0.5f, seed: 33U, rain: true),
            CreateConstants(frequency: 4f, time: 2.0f, seed: 33U, rain: true),
            CreateConstants(frequency: 2.5f, time: 7.5f, seed: 91U, rain: true),
            CreateConstants(frequency: 4f, time: 14.5f, seed: 91U, rain: true),
            CreateConstants(frequency: 4f, time: 22.0f, seed: 4197U, rain: false),
        };
        var forward = new OutsideDropletMergeSimulation();
        var expected = requests.Select(constants => forward.Evaluate(in constants)).ToArray();
        foreach (var frame in expected)
        {
            True(frame.IsValid && frame.Heads.Count is >= 18 and <= 72,
                "頻度・雨を含む通常条件は有効な有界frameを返す必要があります。");
        }

        var reverse = new OutsideDropletMergeSimulation();
        for (var index = requests.Length - 1; index >= 0; index--)
        {
            var request = requests[index];
            var actual = reverse.Evaluate(in request);
            True(actual.IsValid, "逆順シークでも頻度付きframeを無効化してはいけません。");
            True(expected[index].Heads.SequenceEqual(actual.Heads) &&
                expected[index].StaticChanges.SequenceEqual(actual.StaticChanges) &&
                expected[index].StaticLayout!.PackedStates.SequenceEqual(actual.StaticLayout!.PackedStates),
                "頻度・雨を含むframeは評価順序に依存してはいけません。");
        }

        const float speed = 1.75f;
        const float smallSizeSpeed = 0.9f;
        var expectedDuration = OutsideDropletMotion.SecondsPerRegionHeight / (speed * smallSizeSpeed);
        NearlyEqual(expectedDuration, OutsideDropletMotion.GetFallDurationSeconds(speed, smallSizeSpeed),
            "頻度は既存の速度・サイズ別落下期間式を変更してはいけません。");

        var one = CreateConstants(frequency: 1f, time: 8f, seed: 77U, speed: speed, sizeScale: 1.6f);
        var four = one;
        four.OutsideDropletFallFrequency = 4f;
        var oneFrame = new OutsideDropletMergeSimulation().Evaluate(in one);
        var fourFrame = new OutsideDropletMergeSimulation().Evaluate(in four);
        True(oneFrame.IsValid && fourFrame.IsValid &&
            oneFrame.Heads.All(head => float.IsFinite(head.FallProgress)) &&
            fourFrame.Heads.All(head => float.IsFinite(head.FallProgress)),
            "頻度だけの変更で既存の速度・サイズ別落下評価を非有限値にしてはいけません。");
    }

    private static void VerifyGpuTransportAndNonFiniteFallback()
    {
        var fourfoldConstants = CreateConstants(frequency: 4f, time: 5f, seed: 33U);
        var fourfold = new OutsideDropletMergeSimulation().Evaluate(in fourfoldConstants);
        var gpu = new GlassCompositeGpuConstants();
        True(fourfold.IsValid && fourfold.Heads.Count == 72 && gpu.TryApply(fourfold),
            "頻度400%の72 headはGPU wrapperへ一括転送できる必要があります。");

        Equal(240, Marshal.SizeOf<GlassCompositeConstants>(),
            "落下頻度はPadding9を置換してcore定数バッファを拡張してはいけません。");
        Equal(236, Marshal.OffsetOf<GlassCompositeConstants>(nameof(GlassCompositeConstants.OutsideDropletFallFrequency)).ToInt32(),
            "落下頻度は旧Padding9のc14.wへ置く必要があります。");

        var finite = CreateConstants(frequency: 1f, time: 5f, seed: 33U);
        var nonFinite = finite;
        nonFinite.OutsideDropletFallFrequency = float.NaN;
        var expected = new OutsideDropletMergeSimulation().Evaluate(in finite);
        var actual = new OutsideDropletMergeSimulation().Evaluate(in nonFinite);
        True(expected.IsValid && actual.IsValid && expected.Heads.SequenceEqual(actual.Heads) &&
            expected.StaticChanges.SequenceEqual(actual.StaticChanges) &&
            expected.StaticLayout!.PackedStates.SequenceEqual(actual.StaticLayout!.PackedStates),
            "非有限頻度は旧頻度1.0として安全に再構築する必要があります。");
    }

    private static GlassCompositeConstants CreateConstants(
        float frequency,
        float time,
        uint seed,
        bool rain = false,
        float speed = 1f,
        float sizeScale = 1f) => new()
        {
            InputWidth = 1280f,
            InputHeight = 720f,
            RegionCenterX = 0.5f,
            RegionCenterY = 0.5f,
            RegionWidth = 1f,
            RegionHeight = 1f,
            RegionRotationCos = 1f,
            RegionRotationSin = 0f,
            RegionShape = 0f,
            QuadValid = 1f,
            OutsideDropletAmount = 1f,
            OutsideDropletSizeScale = sizeScale,
            OutsideDropletSeed = seed,
            OutsideDropletLocalTimeSeconds = time,
            OutsideDropletFallEnabled = 1f,
            OutsideDropletFallingRatio = 1f,
            OutsideDropletFallSpeedScale = speed,
            OutsideDropletDeformWithSurface = 1f,
            OutsideDropletRainEnabled = rain ? 1f : 0f,
            OutsideDropletRainStartSeconds = rain ? 1f : 0f,
            OutsideDropletRainDurationSeconds = rain ? 5f : 0f,
            OutsideDropletFallFrequency = frequency,
            QuadInverseM11 = 1f,
            QuadInverseM22 = 1f,
            QuadInverseM33 = 1f,
            QuadForwardM11 = 1f,
            QuadForwardM22 = 1f,
            QuadForwardM33 = 1f,
        };

    private static void NearlyEqual(float expected, float actual, string message)
    {
        if (MathF.Abs(expected - actual) > 0.0001f)
        {
            throw new InvalidOperationException($"{message} 期待値: {expected}, 実際: {actual}");
        }
    }

    private static void True(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} 期待値: {expected}, 実際: {actual}");
        }
    }
}
