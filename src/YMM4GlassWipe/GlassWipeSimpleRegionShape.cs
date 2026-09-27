// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel.DataAnnotations;

namespace YMM4GlassWipe;

/// <summary>
/// 標準モードで選択できる領域形状を表します。
/// </summary>
public enum GlassWipeSimpleRegionShape
{
    [Display(Name = "入力領域全体", Description = "YMM4から渡された入力領域全体へ曇りを適用します")]
    FullScreen = 0,

    [Display(Name = "四角形", Description = "中心、幅、高さ、回転角で四角形の領域を指定します")]
    Rectangle = 1,

    [Display(Name = "円・楕円", Description = "中心、幅、高さ、回転角で円または楕円の領域を指定します")]
    Ellipse = 2,

    [Display(Name = "四角形（四隅指定）", Description = "左上、右上、右下、左下の4点で領域を指定します")]
    Quad = 3,
}

/// <summary>
/// 標準モードの領域形状と保存対象の領域形状の互換規則を提供します。
/// </summary>
internal static class GlassWipeSimpleRegionShapeCompatibility
{
    public static GlassWipeSimpleRegionShape FromRegionShape(GlassWipeRegionShape regionShape)
    {
        return regionShape switch
        {
            GlassWipeRegionShape.FullScreen => GlassWipeSimpleRegionShape.FullScreen,
            GlassWipeRegionShape.Ellipse => GlassWipeSimpleRegionShape.Ellipse,
            GlassWipeRegionShape.Quad => GlassWipeSimpleRegionShape.Quad,
            _ => GlassWipeSimpleRegionShape.Rectangle,
        };
    }

    public static GlassWipeRegionShape ToRegionShape(GlassWipeSimpleRegionShape simpleRegionShape)
    {
        return simpleRegionShape switch
        {
            GlassWipeSimpleRegionShape.FullScreen => GlassWipeRegionShape.FullScreen,
            GlassWipeSimpleRegionShape.Ellipse => GlassWipeRegionShape.Ellipse,
            GlassWipeSimpleRegionShape.Quad => GlassWipeRegionShape.Quad,
            _ => GlassWipeRegionShape.Rectangle,
        };
    }
}
