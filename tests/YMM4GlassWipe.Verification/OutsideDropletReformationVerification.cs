// SPDX-License-Identifier: MPL-2.0

using System.Text.Json;
using YMM4GlassWipe;

namespace YMM4GlassWipe.Verification;

internal static class OutsideDropletReformationVerification
{
    public static void VerifyShortWindowConcentration()
    {
        var constants = CreateConstants(true, 2f);
        var simulation = new OutsideDropletMergeSimulation();
        foreach (var offset in new[] { 11f, 16.5f })
        {
            var hidden = new HashSet<uint>();
            var halfway = new Dictionary<uint, float>();
            for (var index = 0; index < 660; index++)
            {
                var time = offset + index / 120f;
                var frame = At(simulation, constants, time);
                foreach (var cell in frame.StaticChanges)
                {
                    if (cell.Visibility == 0f) hidden.Add(cell.CellKey);
                    if (cell.Visibility >= 0.5f && hidden.Contains(cell.CellKey) &&
                        halfway.TryAdd(cell.CellKey, time))
                        Check(frame.Heads.All(head => head.Visibility == 0f),
                            "落下滴が残っている間に静止滴を再形成してはいけません。");
                }
            }
            Check(halfway.Count >= 20, "多数の復活滴を比較できる固定条件である必要があります。");
            var times = halfway.Values.Order().ToArray();
            const float window = 0.25f;
            var peak = times.Max(time => times.Count(candidate => candidate >= time && candidate < time + window));
            // 50%可視率を通過する滴数を測定し、標本1フレームと端点2滴の誤差を許容する。
            var uniformLimit = MathF.Ceiling(times.Length * (window + 1f / 120f) / (times[^1] - times[0])) + 2;
            Check(peak <= uniformLimit,
                $"短時間へ復活が集中しています: 最大{peak}滴、均等分散の許容上限{uniformLimit}滴。");
        }
    }

    public static void VerifyStaggeredReformation()
    {
        // 利用者の新規初期設定を1920x1080で再現する。修正前は16～16.5秒に99滴が一斉復活した。
        foreach (var rain in new[] { true, false })
        {
            var constants = CreateConstants(rain, 2f);
            var simulation = new OutsideDropletMergeSimulation();
            var offset = rain ? 11f : 0f;
            var mixedFrames = 0;
            var firstRecovery = new Dictionary<uint, float>();
            var hidden = new HashSet<uint>();
            for (var index = 0; index <= 330; index++)
            {
                var time = offset + index / 60f;
                var frame = At(simulation, constants, time);
                var recovering = frame.StaticChanges.Where(cell => cell.Visibility > 0f && cell.Visibility < 1f).ToArray();
                foreach (var cell in frame.StaticChanges)
                {
                    if (cell.Visibility == 0f) hidden.Add(cell.CellKey);
                    if (cell.Visibility > 0f && hidden.Contains(cell.CellKey))
                        firstRecovery.TryAdd(cell.CellKey, time);
                }
                if (recovering.Length >= 2 && recovering.Select(cell => cell.Visibility).Distinct().Count() >= 2 &&
                    frame.Heads.All(head => head.Visibility == 0f)) mixedFrames++;
            }
            Check(firstRecovery.Count >= 20, "多数の消費済み水滴が戻る再現条件である必要があります。");
            Check(firstRecovery.Values.Max() - firstRecovery.Values.Min() > 0.5f,
                "復活開始が従来の共通0.5秒区間へ集中してはいけません。");
            Check(mixedFrames >= 10, "複数の水滴が異なる可視率で徐々に戻る必要があります。");
        }
    }

    public static void VerifyBoundariesAndSeek()
    {
        foreach (var rain in new[] { true, false })
        foreach (var speed in new[] { 0.25f, 2f, 4f })
        {
            var constants = CreateConstants(rain, speed);
            var duration = 11f / speed;
            var offset = rain ? 11f : 0f;
            var simulation = new OutsideDropletMergeSimulation();
            var times = new[] { offset + duration * 0.6f, offset + duration * 0.8f,
                offset + duration - 0.00001f, offset + duration,
                offset + duration * 1.8f, offset + 2f * duration - 0.00001f,
                offset + 2f * duration };
            var expected = times.Select(time => Fingerprint(At(simulation, constants, time))).ToArray();
            foreach (var index in new[] { 6, 2, 4, 0, 5, 1, 3 })
            {
                Check(Fingerprint(At(simulation, constants, times[index])) == expected[index],
                    "逆順シークで再形成状態が変わってはいけません。");
                Check(Fingerprint(At(new OutsideDropletMergeSimulation(), constants, times[index])) == expected[index],
                    "直接シークは連続評価と同じ状態を再構築する必要があります。");
            }
            foreach (var boundary in new[] { offset + duration, offset + 2f * duration })
            {
                var before = At(simulation, constants, boundary - 0.00001f);
                var after = At(simulation, constants, boundary);
                Check(before.Heads.All(head => head.Visibility < 0.001f),
                    "区間末で落下滴を残してはいけません。");
                foreach (var cell in before.StaticChanges)
                {
                    var next = after.StaticChanges.FirstOrDefault(candidate => candidate.CellKey == cell.CellKey);
                    var nextVisibility = next.CellKey != 0 ? next.Visibility : LayoutVisibility(after, cell);
                    Check(MathF.Abs(cell.Visibility - nextVisibility) < 0.001f,
                        "区間境界で静止滴を一斉に復活・消失させてはいけません。");
                }
            }
        }
    }

    private static float LayoutVisibility(OutsideDropletMergeFrame frame, OutsideDropletStaticChange cell)
    {
        return frame.StaticLayout!.TryGetState(cell.Layer, cell.CellX, cell.CellY, out var visibility, out _)
            ? visibility : 1f;
    }

    private static OutsideDropletMergeFrame At(OutsideDropletMergeSimulation simulation,
        GlassCompositeConstants constants, float time)
    {
        constants.OutsideDropletLocalTimeSeconds = time;
        var frame = simulation.Evaluate(constants);
        Check(frame.IsValid && new GlassCompositeGpuConstants().TryApply(frame),
            "再形成中と境界の状態はGPUへ転送できる必要があります。");
        Check(frame.StaticChanges.All(cell => float.IsFinite(cell.Visibility) && cell.Visibility >= 0f &&
            cell.Visibility <= 1f && float.IsFinite(cell.RadiusScale)), "可視率・半径は有限の範囲内である必要があります。");
        return frame;
    }

    private static string Fingerprint(OutsideDropletMergeFrame frame) =>
        JsonSerializer.Serialize(frame, new JsonSerializerOptions { IncludeFields = true });

    private static GlassCompositeConstants CreateConstants(bool rain, float speed) => new()
    {
        InputWidth = 1920f, InputHeight = 1080f,
        RegionCenterX = 0.5f, RegionCenterY = 0.5f, RegionWidth = 1f, RegionHeight = 1f,
        RegionRotationCos = 1f, QuadValid = 1f,
        OutsideDropletAmount = 0.7f, OutsideDropletSizeScale = 1f, OutsideDropletSeed = 0f,
        OutsideDropletFallingRatio = 1f, OutsideDropletFallSpeedScale = speed,
        OutsideDropletFallFrequency = 1f, OutsideDropletDeformWithSurface = 1f,
        OutsideDropletRainEnabled = rain ? 1f : 0f,
        OutsideDropletRainStartSeconds = 1f, OutsideDropletRainDurationSeconds = 10f,
    };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
