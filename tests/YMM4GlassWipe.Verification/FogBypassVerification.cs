// SPDX-License-Identifier: MPL-2.0

using System.Runtime.CompilerServices;
using System.Drawing;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using FrameTime = YukkuriMovieMaker.Player.Video.FrameTime;

namespace YMM4GlassWipe.Verification;

internal static class FogBypassVerification
{
    public static void VerifyRouting()
    {
        foreach (var view in Enum.GetValues<GlassWipeDebugView>())
        {
            Check(GlassWipeFogPipeline.ShouldBypass(0f, view) == (view == GlassWipeDebugView.Final),
                "曇り量0でも診断表示の描画経路を省略してはいけません。");
            foreach (var amount in new[] { float.Epsilon, 0.00001f, 0.5f, 1f, float.NaN, float.PositiveInfinity })
                Check(!GlassWipeFogPipeline.ShouldBypass(amount, view),
                    "0以外の曇り量を丸めて省略してはいけません。");
        }
        Check(!GlassWipeFogPipeline.ShouldBypass(0f, (GlassWipeDebugView)999),
            "未知の表示モードを通常表示として省略してはいけません。");
        Check(OutsideDropletFogPipeline.ShouldUsePreFog(0.7f, 0.8f, GlassWipeDebugView.Final),
            "曇り省略と独立して水滴専用パスを維持する必要があります。");
        Check(OutsideDropletFogPipeline.ShouldUsePreFog(0.7f, 0.8f, GlassWipeDebugView.Blurred),
            "ぼかし診断は水滴を含む画像を維持する必要があります。");
    }

    public static void VerifyAnimatedTransitions()
    {
        var effect = new GlassWipeVideoEffect();
        foreach (var (from, to) in new[] { (0d, 100d), (100d, 0d) })
        {
            var animation = new Animation(0, 0, 100) { AnimationType = AnimationType.直線移動 };
#pragma warning disable CS0618
            animation.From = from;
            animation.To = to;
#pragma warning restore CS0618
            effect.FogAmount.CopyFrom(animation);
            var amounts = new Dictionary<int, float>();
            for (var frame = 0; frame <= 120; frame++)
            {
                var parameters = GlassWipeParameters.Create(effect, Describe(frame));
                amounts.Add(frame, parameters.FogAmount);
                Check(GlassWipeFogPipeline.ShouldBypass(parameters.FogAmount, parameters.DebugView) ==
                    (parameters.FogAmount == 0f), "省略判定には現在フレームのAnimation評価値が必要です。");
            }
            Check(amounts.Values.Any(amount => amount == 0f) && amounts.Values.Any(amount => amount == 1f) &&
                amounts.Values.Any(amount => amount > 0f && amount < 1f),
                $"検証用Animationは0・中間値・100を実際に通る必要があります。評価範囲={amounts.Values.Min()}～{amounts.Values.Max()}");
            foreach (var frame in new[] { 120, 0, 60, 1, 119, 0 })
            {
                var parameters = GlassWipeParameters.Create(effect, Describe(frame));
                Check(parameters.FogAmount == amounts[frame], "前後シークで曇り量が変わってはいけません。");
            }
        }
    }

    private static EffectDescription Describe(int frame)
    {
        var timeline = new TimelineSourceDescription(new Size(1920, 1080),
            new FrameTime(frame, 60), new FrameTime(120, 60), 60, default, Guid.Empty, []);
        var item = new TimelineItemSourceDescription(timeline, frame, 120, 0);
        var draw = (DrawDescription)RuntimeHelpers.GetUninitializedObject(typeof(DrawDescription));
        return new EffectDescription(item, draw, 0, 1, 0, 1);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
