// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel.DataAnnotations;

namespace YMM4GlassWipe;

internal enum GlassWipeQuadPreset
{
    [Display(Name = "カスタム", Description = "4点のAnimation値を使用する")]
    Custom = 0,

    [Display(Name = "フロントガラス", Description = "左右対称の台形を使用する")]
    FrontWindshield = 1,
}
