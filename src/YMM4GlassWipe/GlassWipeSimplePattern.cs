// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel.DataAnnotations;

namespace YMM4GlassWipe;

/// <summary>
/// 標準モードで使用する拭き取り軌跡の定型を表します。
/// </summary>
internal enum GlassWipeSimplePattern
{
    [Display(Name = "一回拭き（直線）", Description = "開始位置から終了位置へ一回だけ直線で拭く")]
    StraightOnce = 0,

    [Display(Name = "一回拭き（経由点あり）", Description = "開始位置、経由位置、終了位置を通って一回だけ拭く")]
    GentleArcOnce = 1,

    [Display(Name = "往復拭き（直線）", Description = "開始位置と終了位置の間を直線で往復する")]
    ShortRoundTrips = 2,

    [Display(Name = "往復拭き（経由点あり）", Description = "開始位置、経由位置、終了位置を通って往復する")]
    ArcRoundTrips = 3,

    [Display(Name = "往復拭き（ずれ）", Description = "往復ごとに位置をずらしながら拭く")]
    OffsetRoundTrips = 4,
}
