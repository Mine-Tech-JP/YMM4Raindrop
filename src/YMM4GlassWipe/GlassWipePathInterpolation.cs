// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel.DataAnnotations;

namespace YMM4GlassWipe;

/// <summary>
/// 定型軌跡の点間を評価する方法を表します。
/// </summary>
internal enum GlassWipePathInterpolation
{
    [Display(Name = "直線", Description = "制御点を区分線形で結ぶ")]
    Linear = 0,

    [Display(Name = "曲線（Catmull-Rom）", Description = "求心性のCatmull-Romスプラインで制御点を滑らかに結ぶ")]
    Smooth = 1,

    [Display(Name = "円弧（3点）", Description = "始点、円弧上の経由点、終点を通る円弧で結ぶ")]
    CircularArc = 2,
}
