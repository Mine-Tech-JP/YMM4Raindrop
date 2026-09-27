// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel.DataAnnotations;

namespace YMM4GlassWipe;

internal enum WipePathTimingMode
{
    [Display(Name = "パーセンテージ", Description = "アイテム全体の長さに対するパーセンテージで開始位置と完了位置を指定します")]
    Percent = 0,

    [Display(Name = "固定フレーム", Description = "アイテム先頭からのフレーム番号で開始位置と完了位置を指定します")]
    Frame = 1,

    [Display(Name = "秒数", Description = "アイテム先頭からの秒数で開始位置と完了位置を指定します")]
    Seconds = 2,
}

internal static class WipePathTimingModeCompatibility
{
    public const WipePathTimingMode Default = WipePathTimingMode.Percent;

    public static WipePathTimingMode Normalize(WipePathTimingMode mode) =>
        mode is WipePathTimingMode.Percent or
            WipePathTimingMode.Frame or
            WipePathTimingMode.Seconds
            ? mode
            : Default;
}

internal static class WipePathTimingVisibility
{
    public static bool IsPercentVisible(WipePathTimingMode mode) =>
        WipePathTimingModeCompatibility.Normalize(mode) == WipePathTimingMode.Percent;

    public static bool IsFrameVisible(WipePathTimingMode mode) =>
        WipePathTimingModeCompatibility.Normalize(mode) == WipePathTimingMode.Frame;

    public static bool IsSecondsVisible(WipePathTimingMode mode) =>
        WipePathTimingModeCompatibility.Normalize(mode) == WipePathTimingMode.Seconds;
}
