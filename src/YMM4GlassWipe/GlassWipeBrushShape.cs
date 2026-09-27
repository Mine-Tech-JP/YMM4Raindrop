// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel.DataAnnotations;

namespace YMM4GlassWipe;

internal enum GlassWipeBrushShape
{
    [Display(Name = "円", Description = "直径をピクセルで指定する正円")]
    Circle = 0,

    [Display(Name = "四角形", Description = "幅と高さをピクセルで指定する四角形")]
    Rectangle = 1,

    [Display(Name = "手形", Description = "指先を進行方向へ向けられる手形で拭き取る")]
    Hand = 2,

    [Display(Name = "靴跡", Description = "回転角0°で爪先が上を向く靴底形状で拭き取る")]
    ShoePrint = 3,

    [Display(Name = "ユーザー画像", Description = "ローカルライブラリへ登録したPNG画像で拭き取る")]
    UserImage = 4,

    [Display(Name = "楕円", Description = "幅と高さをピクセルで指定する楕円")]
    Ellipse = 5,
}
