// SPDX-License-Identifier: MPL-2.0

using System.Drawing;
using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Player.Video;
using FrameTime = YukkuriMovieMaker.Player.Video.FrameTime;

namespace YMM4GlassWipe.Verification;

internal static class MaskSuspensionVerification
{
    public static void VerifyDeferredHistory()
    {
        foreach (var shape in new[] { GlassWipeBrushShape.Hand, GlassWipeBrushShape.Rectangle, GlassWipeBrushShape.Circle })
        {
            var effect = CreateEffect(shape);
            var geometry = WipeMaskGeometry.Create(1920, 1080);
            var resumedPath = WipePathStream.Create(effect, Describe(150), geometry);
            var continuous = Generate(resumedPath, geometry, Enumerable.Range(0, 151));
            var deferred = Generate(resumedPath, geometry, [30, 150]);
            Check(continuous.Count > 0 && continuous.SequenceEqual(deferred),
                $"{shape}: 休止前と再開時だけの更新でも、連続更新と同じスタンプ列になる必要があります。");
        }
    }

    public static void VerifyResumeInvalidation()
    {
        var effect = CreateEffect(GlassWipeBrushShape.Hand);
        var geometry = WipeMaskGeometry.Create(1920, 1080);
        var cached = WipePathStream.Create(effect, Describe(30), geometry);
        var resumed = WipePathStream.Create(effect, Describe(150), geometry);
        var append = WipeMaskUpdatePlan.Create(cached, geometry, resumed, geometry);
        Check(append.Kind == WipeMaskUpdateKind.Append && append.FirstSampleIndex == cached.Frame + 1,
            "休止中の未処理区間を飛ばさず、最後に描いたフレームの次から追記する必要があります。");
        Check(WipeMaskUpdatePlan.Create(resumed, geometry, resumed, geometry).Kind == WipeMaskUpdateKind.Reuse,
            "同一フレームでの再表示は描画済み履歴を再利用する必要があります。");
        CheckRebuild(resumed, geometry, WipePathStream.Create(effect, Describe(20), geometry), geometry,
            "休止中に後方シークした場合");

        effect.SimpleEndY = 65;
        var edited = WipePathStream.Create(effect, Describe(150), geometry);
        CheckRebuild(cached, geometry, edited, geometry, "休止中に経路設定を変えた場合");
        CheckRebuild(resumed, geometry, edited, geometry, "同一フレームで経路設定を変えた場合");

        var resized = WipeMaskGeometry.Create(960, 540);
        CheckRebuild(edited, geometry, WipePathStream.Create(effect, Describe(150), resized), resized,
            "休止中に入力寸法を変えた場合");
        effect.EditingMode = GlassWipeEditingMode.Detailed;
        effect.PathInputMode = WipePathInputMode.StrokeCollection;
        effect.CustomPathData = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart).CustomPathData;
        var heart = WipePathStream.Create(effect, Describe(30), geometry);
        effect.CustomPathData = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Smiley).CustomPathData;
        CheckRebuild(heart, geometry, WipePathStream.Create(effect, Describe(150), geometry), geometry,
            "休止中に独自軌跡を差し替えた場合");
    }

    private static List<WipeBrushStamp> Generate(
        WipePathStream path,
        WipeMaskGeometry geometry,
        IEnumerable<int> updateFrames)
    {
        var states = Enumerable.Range(0, path.LaneCount)
            .Select(lane => new WipeBrushStampGenerator.StreamState(path.GetStrokeIdBase(lane))).ToArray();
        var stamps = new List<WipeBrushStamp>();
        var first = 0;
        foreach (var frame in updateFrames)
        {
            path.EnumerateSamples(first, frame,
                (lane, sample) => states[lane].Append(sample, geometry, path.Style, stamps));
            first = frame + 1;
        }
        return stamps;
    }

    private static void CheckRebuild(
        WipePathStream cached,
        WipeMaskGeometry cachedGeometry,
        WipePathStream current,
        WipeMaskGeometry currentGeometry,
        string context)
    {
        var plan = WipeMaskUpdatePlan.Create(cached, cachedGeometry, current, currentGeometry);
        Check(plan.Kind == WipeMaskUpdateKind.Rebuild && plan.FirstSampleIndex == 0,
            $"{context}は古いマスクを再利用せず、先頭から再構築する必要があります。");
    }

    private static GlassWipeVideoEffect CreateEffect(GlassWipeBrushShape shape) => new()
    {
        BrushShape = shape,
        PathTimingMode = WipePathTimingMode.Seconds,
        PathStartSeconds = 0,
        PathCompletionSeconds = 3,
    };

    private static EffectDescription Describe(int frame)
    {
        var timeline = new TimelineSourceDescription(new Size(1920, 1080),
            new FrameTime(frame, 60), new FrameTime(240, 60), 60, default, Guid.Empty, []);
        var item = new TimelineItemSourceDescription(timeline, frame, 240, 0);
        var draw = (DrawDescription)RuntimeHelpers.GetUninitializedObject(typeof(DrawDescription));
        return new EffectDescription(item, draw, 0, 1, 0, 1);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
