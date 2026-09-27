// SPDX-License-Identifier: MPL-2.0

using System.Text.Json;
using YMM4GlassWipe;
using YmmJson = YukkuriMovieMaker.Json.Json;

namespace YMM4GlassWipe.Verification;

internal static class OutsideDropletUiVerification
{
    public static void VerifyValuesAndNotifications()
    {
        foreach (var load in new Func<string, GlassWipeVideoEffect?>[]
                 {
                     json => JsonSerializer.Deserialize<GlassWipeVideoEffect>(json),
                     json => YmmJson.LoadFromText<GlassWipeVideoEffect>(json),
                 })
        {
            var legacy = load("{}") ?? throw new InvalidOperationException("旧設定を復元できません。");
            Check(legacy.OutsideDropletAmount == 0 && legacy.OutsideDropletSize == 100 &&
                legacy.OutsideDropletStrength == 50 && legacy.OutsideDropletSeed == 1 &&
                legacy.OutsideDropletDeformWithSurface && !legacy.OutsideDropletFallEnabled &&
                legacy.OutsideDropletFallingRatio == 30 && legacy.OutsideDropletFallSpeed == 100 &&
                legacy.OutsideDropletTrailLength == 35, "旧保存の水滴欠落値を維持する必要があります。");
        }

        var effect = new GlassWipeVideoEffect
        {
            OutsideDropletAmount = 0,
            OutsideDropletFallEnabled = false,
            OutsideDropletRainEnabled = false,
            OutsideDropletAppearance = OutsideDropletAppearance.Transparent,
        };
        var notifications = new HashSet<string?>();
        effect.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        foreach (var amount in new[] { 40d, 0d, 70d })
        {
            notifications.Clear();
            effect.OutsideDropletAmount = amount;
            var visible = amount > 0;
            Check(effect.AreOutsideDropletSettingsVisible == visible, "水滴量による表示条件が不正です。");
            Check(effect.IsOutsideDropletOutlineOpacityVisible == visible, "黒い輪郭の濃さの表示条件が不正です。");
            Check(!effect.AreOutsideDropletFallSettingsVisible && !effect.AreOutsideDropletRainTimingSettingsVisible,
                "落下と雨がOFFなら従属欄を非表示にする必要があります。");
            foreach (var name in new[]
                     {
                         nameof(GlassWipeVideoEffect.AreOutsideDropletSettingsVisible),
                         nameof(GlassWipeVideoEffect.AreOutsideDropletFallSettingsVisible),
                         nameof(GlassWipeVideoEffect.AreOutsideDropletRainTimingSettingsVisible),
                         nameof(GlassWipeVideoEffect.IsOutsideDropletOutlineOpacityVisible),
                     })
                Check(notifications.Contains(name), "水滴量変更時の通知がありません: " + name);
        }
        foreach (var enabled in new[] { true, false })
        {
            notifications.Clear();
            effect.OutsideDropletFallEnabled = enabled;
            Check(effect.AreOutsideDropletFallSettingsVisible == enabled, "落下ON/OFFの表示条件が不正です。");
            Check(notifications.Contains(nameof(GlassWipeVideoEffect.AreOutsideDropletFallSettingsVisible)),
                "落下ON/OFF変更時に表示を通知する必要があります。");
        }
        effect.OutsideDropletFallEnabled = true;
        effect.OutsideDropletAmount = 0;
        Check(!effect.AreOutsideDropletFallSettingsVisible, "落下ONでも水滴量0なら従属欄を非表示にする必要があります。");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
