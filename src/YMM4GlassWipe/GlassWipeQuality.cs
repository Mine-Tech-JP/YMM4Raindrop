// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel.DataAnnotations;

namespace YMM4GlassWipe;

internal enum GlassWipeQuality
{
    [Display(Name = "標準", Description = "従来と同じ間隔でブラシを描画する")]
    Standard = 0,

    [Display(Name = "軽量", Description = "ブラシ間隔を広げて描画負荷を抑える")]
    Low = 1,

    [Display(Name = "高品質", Description = "ブラシ間隔を狭めて軌跡を滑らかにする")]
    High = 2,
}
