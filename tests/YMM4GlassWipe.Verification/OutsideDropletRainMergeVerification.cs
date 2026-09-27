// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using System.Text;
using YMM4GlassWipe;

namespace YMM4GlassWipe.Verification;

internal static class OutsideDropletRainMergeVerification
{
    public static void VerifyRainHeadBirthAndWaiting()
    {
        foreach (var start in new[] { 20f, 36000f })
        foreach (var speed in new[] { 0.25f, 1f, 4f })
        {
            var constants = CreateConstants();
            constants.OutsideDropletRainStartSeconds = start;
            constants.OutsideDropletRainDurationSeconds = 30f;
            constants.OutsideDropletFallSpeedScale = speed;
            var simulation = new OutsideDropletMergeSimulation();
            constants.OutsideDropletLocalTimeSeconds = start - 1f;
            var before = simulation.Evaluate(constants);
            Check(before.IsValid && before.Heads.All(head => head.Visibility == 0f && head.FallProgress == 0f),
                "開始時刻前にはheadを表示・落下させてはいけません。");
            var verified = 0;
            for (var index = 0; index < before.Heads.Count; index++)
            {
                var layer = Layer(index);
                var emitterKey = OutsideDropletRandom.GetEmitterKey((uint)constants.OutsideDropletSeed,
                    layer, (uint)(index % 6));
                if (!IsEligible(constants, index, emitterKey)) continue;
                var birth = OutsideDropletRainTiming.GetAppearanceTimeSeconds(emitterKey, start, 30f);
                constants.OutsideDropletLocalTimeSeconds = MathF.BitDecrement(birth);
                var unborn = simulation.Evaluate(constants).Heads[index];
                Check(unborn.Visibility == 0f && unborn.FallProgress == 0f &&
                    unborn.Radius == before.Heads[index].Radius,
                    "未出現headは動いたり、他の滴を吸収して成長したりしてはいけません。");
                constants.OutsideDropletLocalTimeSeconds = birth;
                Check(simulation.Evaluate(constants).Heads[index].Visibility == 0f,
                    "大きい開始秒でもHLSLと同じfloat出生境界から待機を開始する必要があります。");
                var cycle = OutsideDropletRandom.GetCycleKey(emitterKey, 0u);
                var wait = (0.3f + 0.5f * OutsideDropletRandom.Sample(cycle, 5u)) / speed;
                constants.OutsideDropletLocalTimeSeconds = birth + wait * 0.25f;
                var waiting = simulation.Evaluate(constants).Heads[index];
                Check(waiting.Visibility > 0f && waiting.FallProgress == 0f &&
                    waiting.InputUv == waiting.SpawnInputUv,
                    "待機区間は自身の出現時刻から始める必要があります。");
                verified++;
            }
            Check(verified >= 5, "速度ごとに複数レイヤーのhead出生を確認する必要があります。");
        }
    }

    public static void VerifyRainStaticBirthCausality()
    {
        var constants = CreateConstants();
        constants.OutsideDropletFallingRatio = 0f;
        constants.OutsideDropletRainStartSeconds = 3f;
        var simulation = new OutsideDropletMergeSimulation();
        var sawAbsorbed = false;
        var sawGrowth = false;
        for (var index = 0; index <= 70; index++)
        {
            constants.OutsideDropletLocalTimeSeconds = index * 0.125f;
            var frame = simulation.Evaluate(constants);
            Check(frame.IsValid && frame.StaticLayout is not null, "出生中の静止予定表を構築できます。");
            foreach (var change in frame.StaticLayout!.Changes)
            {
                var birth = OutsideDropletRainTiming.GetAppearanceTimeSeconds(change.CellKey, 3f, 5f);
                Check(constants.OutsideDropletLocalTimeSeconds >= birth,
                    "未出現の静止滴を吸収側にも、吸収される側にも含めてはいけません。");
                sawAbsorbed |= change.Visibility == 0f;
                sawGrowth |= change.RadiusScale > 1f;
                Check(change.RadiusScale is >= 1f and <= 1.075f,
                    "静止出生時の成長は既存の初期成長上限内である必要があります。");
            }
            Check(new GlassCompositeGpuConstants().TryApply(frame), "静止出生の状態をGPUへ転送できます。");
        }
        Check(sawAbsorbed && sawGrowth, "出生後の静止吸収と成長を実際に確認する必要があります。");
        constants.OutsideDropletRainDurationSeconds = 0f;
        constants.OutsideDropletLocalTimeSeconds = 2.999f;
        var before = new OutsideDropletMergeSimulation().Evaluate(constants);
        Check(before.IsValid && before.StaticLayout!.Changes.Count == 0,
            "期間0でも開始前に静止吸収を実行してはいけません。");
        constants.OutsideDropletLocalTimeSeconds = 3f;
        var simultaneous = new OutsideDropletMergeSimulation().Evaluate(constants);
        Check(simultaneous.IsValid && simultaneous.StaticLayout!.Changes.Count > 0,
            "期間0では開始時刻に同時出生と静止吸収を評価する必要があります。");
    }

    public static void VerifyRainEpochBoundaryAndSeek()
    {
        var constants = CreateConstants();
        constants.OutsideDropletFallSpeedScale = 4f;
        constants.OutsideDropletRainStartSeconds = 7f;
        constants.OutsideDropletRainDurationSeconds = 30f;
        var initialEnd = 7f + 30f + 11f / 4f;
        var simulation = new OutsideDropletMergeSimulation();
        constants.OutsideDropletLocalTimeSeconds = initialEnd - 0.0001f;
        var end = simulation.Evaluate(constants);
        constants.OutsideDropletLocalTimeSeconds = initialEnd;
        var next = simulation.Evaluate(constants);
        Check(end.IsValid && next.IsValid && end.StaticLayout!.PackedStates.SequenceEqual(next.StaticLayout!.PackedStates),
            "初回静止吸収の結果を後続epochへ引き継ぎ、吸収された小滴を復活させてはいけません。");
        Check(end.Heads.All(head => head.Visibility == 0f) && next.Heads.All(head => head.Visibility == 0f),
            "初回epoch境界は最後の出生headが落下を終えた後である必要があります。");
        constants.OutsideDropletLocalTimeSeconds = initialEnd + 0.5f;
        var repeated = simulation.Evaluate(constants);
        Check(repeated.Heads.Any(head => head.Visibility > 0f),
            "後続epochで開始時刻と出現期間による待機を再実行してはいけません。");
        var times = Enumerable.Range(0, 24).Select(index => index * (initialEnd / 24f))
            .Concat(new[] { initialEnd, initialEnd + 0.25f, initialEnd + 1f, initialEnd + 3.5f }).ToArray();
        var expected = times.Select(time => At(simulation, constants, time)).ToArray();
        for (var index = 0; index < 1000; index++)
        {
            var slot = index * 73 % times.Length;
            Check(At(simulation, constants, times[slot]) == expected[slot],
                "1000回の前後シークで出生・吸収・GPU用静止状態が変わってはいけません。");
        }
        foreach (var slot in new[] { 0, 7, 18, 24, 26, 27 })
            Check(At(new OutsideDropletMergeSimulation(), constants, times[slot]) == expected[slot],
                "キャッシュなしの直接シークも同じ状態を再構築する必要があります。");
    }

    public static void VerifyRainReformationKeepsConsumedSmallDropletsHidden()
    {
        var constants = CreateConstants();
        constants.OutsideDropletFallSpeedScale = 4f;
        var simulation = new OutsideDropletMergeSimulation();
        const float reformation = 5f + 10f / 4f;
        const float boundary = 5f + 11f / 4f;
        constants.OutsideDropletLocalTimeSeconds = 5f;
        var before = simulation.Evaluate(constants);
        constants.OutsideDropletLocalTimeSeconds = boundary - 0.001f;
        var after = simulation.Evaluate(constants);
        Check(before.IsValid && after.IsValid, "再形成前後の予定表は有効である必要があります。");
        var newlyAbsorbed = after.StaticLayout!.Changes.Where(change => change.Visibility == 0f &&
            GetVisibility(before, change, includeDynamic: false) > 0f &&
            before.StaticChanges.Any(dynamic => SameCell(dynamic, change) && dynamic.Visibility == 0f)).ToArray();
        Check(newlyAbsorbed.Length > 0,
            "固定seed 4197でhead消費後に静止吸収へ引き継ぐセルを実際に確認する必要があります。");
        var small = newlyAbsorbed.Single(cell => cell.Layer == 101u && cell.CellX == 5 && cell.CellY == 3);
        var owner = after.StaticLayout.Changes.Single(cell => cell.Layer == 307u && cell.CellX == 2 && cell.CellY == 1);
        Check(small.CellKey == 1903198086u && owner.CellKey == 3003300949u,
            "固定回帰ケースの整数キーを変更してはいけません。");
        Check(OutsideDropletRainTiming.GetAppearanceTimeSeconds(owner.CellKey, 0f, 5f) > 4.3f,
            "大滴はheadが小滴を消費した時刻より後に出生する必要があります。");
        constants.OutsideDropletLocalTimeSeconds = 4.10f;
        Check(GetVisibility(simulation.Evaluate(constants), small, includeDynamic: true) == 1f,
            "固定の小滴はhead接触前に出生して存在する必要があります。");
        constants.OutsideDropletLocalTimeSeconds = 4.30f;
        Check(GetVisibility(simulation.Evaluate(constants), small, includeDynamic: true) == 0f,
            "大滴出生前にheadが小滴を全体消費する固定条件を確認します。");
        var a = Geometry(small, 20f * constants.OutsideDropletSizeScale);
        var b = Geometry(owner, 44f * constants.OutsideDropletSizeScale);
        var offset = a.Center - b.Center;
        Check(new Vector2(offset.X / (a.Radius + b.Radius * owner.RadiusScale),
            offset.Y / (a.Radius * a.Vertical + b.Radius * b.Vertical * owner.RadiusScale)).LengthSquared() < 1f,
            "再形成する小滴と後生まれ大滴は幾何的に重なる固定条件である必要があります。");
        foreach (var time in new[] { reformation + 0.01f, boundary - 0.001f, boundary, boundary + 0.01f })
        {
            constants.OutsideDropletLocalTimeSeconds = time;
            var frame = simulation.Evaluate(constants);
            Check(frame.IsValid && new GlassCompositeGpuConstants().TryApply(frame),
                "再形成で静止吸収へ引き継いだframeをGPUへ転送できます。");
            Check(GetVisibility(frame, owner, includeDynamic: true) == 1f,
                "初回末・直後とも、重なる小滴を消し大滴を維持する必要があります。");
            foreach (var cell in newlyAbsorbed)
                Check(GetVisibility(frame, cell, includeDynamic: true) == 0f,
                    "再形成で吸収された小滴を動的tableが再表示してはいけません。");
        }
    }

    private static (Vector2 Center, float Radius, float Vertical) Geometry(OutsideDropletStaticChange cell, float size)
    {
        var center = (new Vector2(cell.CellX, cell.CellY) + new Vector2(
            0.25f + 0.5f * OutsideDropletRandom.Sample(cell.CellKey, 1u),
            0.25f + 0.5f * OutsideDropletRandom.Sample(cell.CellKey, 2u))) * size;
        return (center, size * (0.11f + 0.10f * OutsideDropletRandom.Sample(cell.CellKey, 3u)),
            0.88f + 0.30f * OutsideDropletRandom.Sample(cell.CellKey, 4u));
    }

    private static bool SameCell(OutsideDropletStaticChange left, OutsideDropletStaticChange right) =>
        left.Layer == right.Layer && left.CellX == right.CellX && left.CellY == right.CellY;

    private static float GetVisibility(OutsideDropletMergeFrame frame, OutsideDropletStaticChange cell,
        bool includeDynamic)
    {
        frame.StaticLayout!.TryGetState(cell.Layer, cell.CellX, cell.CellY, out var visibility, out _);
        if (includeDynamic)
            foreach (var change in frame.StaticChanges)
                if (SameCell(change, cell)) return change.Visibility;
        return visibility;
    }

    public static void VerifyRainMergeLimitsAndOffCompatibility()
    {
        var constants = CreateConstants();
        constants.OutsideDropletRainEnabled = 0f;
        constants.OutsideDropletLocalTimeSeconds = 1.25f;
        var expected = At(new OutsideDropletMergeSimulation(), constants, 1.25f);
        constants.OutsideDropletRainStartSeconds = float.NaN;
        constants.OutsideDropletRainDurationSeconds = float.PositiveInfinity;
        Check(At(new OutsideDropletMergeSimulation(), constants, 1.25f) == expected,
            "OFFでは雨設定の値を合体計算へ混入させてはいけません。");
        constants = CreateConstants();
        constants.InputWidth = 1000000f;
        constants.InputHeight = 1000f;
        constants.OutsideDropletSizeScale = 0.25f;
        Check(!new OutsideDropletMergeSimulation().Evaluate(constants).IsValid,
            "雨ONでも既存の最大セル列挙数を超えたら安全に通常描画へ戻す必要があります。");
        constants = CreateConstants();
        constants.OutsideDropletRainStartSeconds = 36000f;
        constants.OutsideDropletRainDurationSeconds = 36000f;
        constants.OutsideDropletFallSpeedScale = 4f;
        constants.OutsideDropletLocalTimeSeconds = 70000f;
        var longFrame = new OutsideDropletMergeSimulation().Evaluate(constants);
        Check(longFrame.IsValid && new GlassCompositeGpuConstants().TryApply(longFrame),
            "遅い開始・長い期間・高速でも全履歴配列を作らず有限の初回予定表を構築できます。");
    }

    private static string At(OutsideDropletMergeSimulation simulation, GlassCompositeConstants constants, float time)
    {
        constants.OutsideDropletLocalTimeSeconds = time;
        var frame = simulation.Evaluate(constants);
        Check(frame.IsValid && new GlassCompositeGpuConstants().TryApply(frame), "検証対象frameはGPU転送可能である必要があります。");
        var output = new StringBuilder();
        foreach (var head in frame.Heads)
        {
            output.Append(head.EmitterIndex).Append(':');
            Append(head.InputUv.X); Append(head.InputUv.Y); Append(head.SpawnInputUv.X); Append(head.SpawnInputUv.Y);
            Append(head.Radius); Append(head.VerticalScale); Append(head.Visibility); Append(head.FallProgress);
        }
        foreach (var change in frame.StaticChanges)
        {
            output.Append(change.CellKey).Append(':').Append(change.Layer).Append(':')
                .Append(change.CellX).Append(':').Append(change.CellY).Append(':');
            Append(change.Visibility); Append(change.RadiusScale);
        }
        foreach (var word in frame.StaticLayout!.PackedStates) output.Append(word).Append(',');
        return output.ToString();
        void Append(float value) => output.Append(BitConverter.SingleToInt32Bits(value)).Append(',');
    }

    private static uint Layer(int index) => index < 6 ? 101u : index < 12 ? 307u : 701u;
    private static bool IsEligible(GlassCompositeConstants constants, int index, uint key)
    {
        var activation = index < 6 ? 1f : index < 12 ? 0.65f : 0.25f;
        var selection = index < 6 ? 0.75f : index < 12 ? 1f : 1.25f;
        return OutsideDropletRandom.Sample(key, 0u) <= constants.OutsideDropletAmount * activation &&
            OutsideDropletRandom.Sample(key, 8u) <= MathF.Min(1f, constants.OutsideDropletFallingRatio * selection);
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
        OutsideDropletAmount = 1f,
        OutsideDropletSizeScale = 2.358f,
        OutsideDropletSeed = 4197f,
        OutsideDropletFallingRatio = 1f,
        OutsideDropletFallSpeedScale = 1.892f,
        OutsideDropletDeformWithSurface = 1f,
        OutsideDropletRainEnabled = 1f,
        OutsideDropletRainStartSeconds = 0f,
        OutsideDropletRainDurationSeconds = 5f,
    };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
