// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel.DataAnnotations;

namespace YMM4GlassWipe;

internal enum OutsideDropletMotionMode
{
    [Display(Name = "周期動作")]
    Legacy = 0,

    [Display(Name = "簡易物理")]
    SimplePhysics = 1,
}
