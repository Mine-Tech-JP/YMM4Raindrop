// SPDX-License-Identifier: MPL-2.0

using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using FrameTime = YukkuriMovieMaker.Player.Video.FrameTime;

namespace YMM4GlassWipe.Verification;

internal static class UserBrushRecoveryVerification
{
    public static void VerifyBuiltInRecoveryAttempts()
    {
        var devices = DispatchProxy.Create<IGraphicsDevicesAndContext, FailingGraphicsProxy>();
        var proxy = (FailingGraphicsProxy)(object)devices;
        var effect = CreateEffect();
        using var processor = new GlassWipeVideoEffectProcessor(devices, effect);
        Equal(1, proxy.Attempts, "初回の資源生成失敗を注入する必要があります。");
        RecordBrushFailure(processor);

        UpdateRepeatedly(processor);
        Equal(1, proxy.Attempts, "同じ欠落ブラシを毎フレーム再試行してはいけません。");

        effect.BrushShape = GlassWipeBrushShape.Circle;
        UpdateRepeatedly(processor);
        Equal(2, proxy.Attempts, "内蔵ブラシへの変更時は一度だけ資源生成を再試行する必要があります。");

        effect.BrushShape = GlassWipeBrushShape.Hand;
        UpdateRepeatedly(processor);
        Equal(3, proxy.Attempts, "失敗後の別形状への変更も一度だけ再試行する必要があります。");

        effect.BrushShape = GlassWipeBrushShape.UserImage;
        UpdateRepeatedly(processor);
        Equal(4, proxy.Attempts, "ユーザー画像へ戻した場合も一度だけ再試行する必要があります。");
    }

    public static void VerifyUserImageRecoveryAttempts()
    {
        var devices = DispatchProxy.Create<IGraphicsDevicesAndContext, FailingGraphicsProxy>();
        var proxy = (FailingGraphicsProxy)(object)devices;
        var effect = CreateEffect();
        using var processor = new GlassWipeVideoEffectProcessor(devices, effect);

        // GPU生成だけの失敗を、ユーザーブラシ失敗として再試行しない。
        effect.BrushShape = GlassWipeBrushShape.Circle;
        UpdateRepeatedly(processor);
        Equal(1, proxy.Attempts, "初回GPU生成失敗の再試行範囲を広げてはいけません。");

        effect.BrushShape = GlassWipeBrushShape.UserImage;
        RecordBrushFailure(processor);
        effect.UserBrushId = Guid.NewGuid();
        UpdateRepeatedly(processor);
        Equal(2, proxy.Attempts, "別IDへの変更時は一度だけ再試行する必要があります。");

        effect.UserBrushRevision++;
        UpdateRepeatedly(processor);
        Equal(3, proxy.Attempts, "revision更新時は一度だけ再試行する必要があります。");
    }

    private static GlassWipeVideoEffect CreateEffect() => new()
    {
        BrushShape = GlassWipeBrushShape.UserImage,
        UserBrushId = Guid.NewGuid(),
        UserBrushRevision = 1,
    };

    private static void RecordBrushFailure(GlassWipeVideoEffectProcessor processor)
    {
        // 実設定やGPUを壊さず、Updateのcatchが記録する失敗状態だけを注入する。
        var method = typeof(GlassWipeVideoEffectProcessor).GetMethod(
            "RecordUserBrushFailure", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ブラシ失敗の記録処理がありません。");
        method.Invoke(processor, null);
    }

    private static void UpdateRepeatedly(GlassWipeVideoEffectProcessor processor)
    {
        for (var frame = 0; frame < 10; frame++)
        {
            var timeline = new TimelineSourceDescription(new Size(1920, 1080),
                new FrameTime(frame, 60), new FrameTime(120, 60), 60, default, Guid.Empty, []);
            var item = new TimelineItemSourceDescription(timeline, frame, 120, 0);
            var draw = (DrawDescription)RuntimeHelpers.GetUninitializedObject(typeof(DrawDescription));
            var description = new EffectDescription(item, draw, 0, 1, 0, 1);
            if (!ReferenceEquals(processor.Update(description), draw))
                throw new InvalidOperationException("資源生成失敗時も描画記述を返す必要があります。");
        }
    }

    private static void Equal(int expected, int actual, string message)
    {
        if (expected != actual)
            throw new InvalidOperationException($"{message} 期待={expected}、実際={actual}");
    }

    public class FailingGraphicsProxy : DispatchProxy
    {
        public int Attempts { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_DeviceContext")
            {
                Attempts++;
                throw new InvalidOperationException("検証で注入したGPU資源生成失敗です。");
            }

            throw new NotSupportedException("検証対象外のGraphics呼び出しです。");
        }
    }
}
