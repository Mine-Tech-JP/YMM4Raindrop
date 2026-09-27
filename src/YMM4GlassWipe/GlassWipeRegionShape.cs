// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel.DataAnnotations;

namespace YMM4GlassWipe;

internal enum GlassWipeRegionShape
{
    [Display(Name = "入力領域全体", Description = "YMM4から渡された入力領域全体へ曇りを適用する")]
    FullScreen = 0,

    [Display(Name = "四角形", Description = "中心・幅・高さ・回転で曇りを適用する領域を指定する")]
    Rectangle = 1,

    [Display(Name = "円・楕円", Description = "中心・幅・高さ・回転で円形または楕円形の領域を指定する")]
    Ellipse = 2,

    [Display(Name = "四角形（四隅指定）", Description = "左上・右上・右下・左下の4点で曇りを適用する領域を指定する")]
    Quad = 3,
}
