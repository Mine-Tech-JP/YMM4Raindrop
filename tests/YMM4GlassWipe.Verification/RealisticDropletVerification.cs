// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using Vortice;
using YMM4GlassWipe;
using YukkuriMovieMaker.Player.Video;
using YmmJson = YukkuriMovieMaker.Json.Json;

namespace YMM4GlassWipe.Verification;

internal static class RealisticDropletVerification
{
    internal static void VerifyPersistenceAndScopes()
    {
        foreach (var mode in Enum.GetValues<OutsideDropletAppearance>())
        {
            var effect = new GlassWipeVideoEffect { OutsideDropletAppearance = mode, OutsideDropletDeformWithSurface = true };
            var restored = YmmJson.GetClone(effect) ?? throw new InvalidOperationException("水滴を再保存できません。");
            Equal(mode, restored.OutsideDropletAppearance, "YMM4 JSONの見た目往復");
            Equal(true, restored.OutsideDropletDeformWithSurface, "面変形の保存値維持");

            var state = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
            state.OutsideDropletAppearance = mode;
            var encoded = DetailedWipePresetCodec.Encode(state);
            True(DetailedWipePresetCodec.TryDecode(encoded, out var decoded), "プリセット復元");
            Equal(mode, decoded.OutsideDropletAppearance, "プリセットの見た目往復");
            var current = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
            current.OutsideDropletAppearance = OutsideDropletAppearance.Realistic;
            foreach (var scope in Enum.GetValues<DetailedWipePresetScope>())
            {
                True(DetailedWipePresetStateMerger.TryMerge(current, decoded, scope, out var merged), "プリセット適用");
                Equal(scope == DetailedWipePresetScope.All ? mode : OutsideDropletAppearance.Realistic,
                    merged.OutsideDropletAppearance, "プリセット適用範囲");
            }
        }
        Equal(0, (int)OutsideDropletAppearance.Legacy, "旧白輪郭の値");
        Equal(1, (int)OutsideDropletAppearance.Transparent, "旧黒輪郭の値");
        Equal(2, (int)OutsideDropletAppearance.Realistic, "新モードの値");
        Equal(OutsideDropletAppearance.Realistic, new GlassWipeVideoEffect().OutsideDropletAppearance, "テンプレートの新規既定");
        // YMM4の空オブジェクト読込はDefaultValue属性を補う。旧版Stateの移行は既存検証で別に扱う。
        Equal(OutsideDropletAppearance.Transparent,
            YmmJson.LoadFromText<GlassWipeVideoEffect>("{}")!.OutsideDropletAppearance, "YMM4の既定補完維持");
    }

    internal static void VerifyQuadAndUi()
    {
        var effect = new GlassWipeVideoEffect { OutsideDropletAmount = 50, RegionShape = GlassWipeRegionShape.Quad,
            OutsideDropletMotionMode = OutsideDropletMotionMode.Legacy };
        var notifications = new List<string?>();
        ((INotifyPropertyChanged)effect).PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        foreach (var saved in new[] { false, true })
        {
            effect.OutsideDropletDeformWithSurface = saved;
            effect.OutsideDropletAppearance = OutsideDropletAppearance.Realistic;
            var realistic = GlassWipeParameters.Create(effect, CreateDescription());
            Equal(0f, realistic.OutsideDropletDeformWithSurface, "リアルの面変形実効値");
            Equal(2f, realistic.OutsideDropletAppearance, "描画の見た目値");
            Equal(saved, effect.OutsideDropletDeformWithSurface, "保存値を変更しない");
            True(!effect.IsOutsideDropletDeformWithSurfaceVisible && effect.IsOutsideDropletOutlineOpacityVisible,
                "リアルは面変形を隠し輪郭設定を表示");
            effect.OutsideDropletAppearance = OutsideDropletAppearance.Transparent;
            var previous = GlassWipeParameters.Create(effect, CreateDescription());
            Equal(saved ? 1f : 0f, previous.OutsideDropletDeformWithSurface, "従来表示への復帰");
            True(effect.IsOutsideDropletDeformWithSurfaceVisible, "従来表示では面変形を表示");
        }
        True(notifications.Contains(nameof(effect.IsOutsideDropletDeformWithSurfaceVisible)), "UIの変更通知");
        effect.OutsideDropletAmount = 0;
        True(!effect.IsOutsideDropletDeformWithSurfaceVisible && !effect.IsOutsideDropletOutlineOpacityVisible,
            "量0では水滴設定を隠す");
    }

    internal static void VerifyRectangles()
    {
        var constants = new GlassCompositeConstants
        {
            OutsideDropletRenderPass = 1, OutsideDropletAppearance = 2,
            OutsideDropletAmount = 1, OutsideDropletStrength = 1, InputHeight = 1080,
        };
        Equal(257, OutsideDropletRefraction.GetPadding(constants), "1080pで変位と補間の範囲を確保");
        constants.InputHeight = 2160;
        Equal(513, OutsideDropletRefraction.GetPadding(constants), "4Kの参照範囲");
        var rect = new RawRect(-400, 25, 20, 80);
        var expanded = OutsideDropletRefraction.Expand(rect, 513);
        Equal(-913, expanded.Left, "非ゼロ原点の左端");
        Equal(593, expanded.Bottom, "非ゼロ原点の下端");
        Equal(rect, OutsideDropletRefraction.Expand(rect, 0), "従来範囲維持");
        var empty = new RawRect(5, 5, 5, 5);
        Equal(empty, OutsideDropletRefraction.Expand(empty, 513), "空矩形維持");
        var infinite = new RawRect(int.MinValue, int.MinValue, int.MaxValue, int.MaxValue);
        Equal(infinite, OutsideDropletRefraction.Expand(infinite, int.MaxValue), "整数溢れ防止");

        var type = typeof(GlassCompositeCustomEffect).GetNestedType("EffectImpl", BindingFlags.NonPublic)!;
        using var instance = (IDisposable)Activator.CreateInstance(type, true)!;
        type.GetField("_constantBuffer", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, constants);
        var inputs = new RawRect[3];
        type.GetMethod("MapOutputRectToInputRects")!.Invoke(instance, [rect, inputs]);
        Equal(expanded, inputs[0], "実コールバックの入力0拡張");
        Equal(rect, inputs[1], "入力1の範囲維持");
        Equal(rect, inputs[2], "水滴前段の入力2範囲維持");
        Equal(expanded, (RawRect)type.GetMethod("MapInvalidRect")!.Invoke(instance, [0, rect])!, "入力変化の出力無効化");
        foreach (var mode in new[] { 0f, 1f })
        {
            constants.OutsideDropletAppearance = mode;
            Equal(0, OutsideDropletRefraction.GetPadding(constants), "従来モードの範囲維持");
        }
        constants.OutsideDropletAppearance = 2;
        constants.OutsideDropletRenderPass = 0;
        Equal(0, OutsideDropletRefraction.GetPadding(constants), "最終合成では拡張しない");
        constants.OutsideDropletRenderPass = 1;
        constants.OutsideDropletAmount = 0;
        Equal(0, OutsideDropletRefraction.GetPadding(constants), "水滴量0は拡張しない");
        constants.OutsideDropletAmount = 1;
        constants.OutsideDropletStrength = 0;
        Equal(0, OutsideDropletRefraction.GetPadding(constants), "濃さ0は拡張しない");
    }

    private static EffectDescription CreateDescription()
    {
        var timeline = new TimelineSourceDescription(new Size(1920, 1080),
            new FrameTime(0, 60), new FrameTime(120, 60), 60, default, Guid.Empty, []);
        var item = new TimelineItemSourceDescription(timeline, 0, 120, 0);
        var draw = (DrawDescription)RuntimeHelpers.GetUninitializedObject(typeof(DrawDescription));
        return new EffectDescription(item, draw, 0, 1, 0, 1);
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: 期待値={expected}, 実際={actual}");
    }
}
