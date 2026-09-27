// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel.DataAnnotations;

namespace YMM4GlassWipe;

/// <summary>
/// 定型軌跡のブラシ間隔を表します。
/// </summary>
internal enum GlassWipeSimpleGeneratedQuality
{
    [Display(Name = "自動")]
    Auto = 0,

    [Display(Name = "軽量")]
    Low = 1,

    [Display(Name = "標準")]
    Standard = 2,

    [Display(Name = "高品質")]
    High = 3,
}

/// <summary>
/// 定型軌跡の描画品質を保存時に正規化します。
/// </summary>
internal static class GlassWipeSimpleGeneratedQualityCompatibility
{
    public static GlassWipeSimpleGeneratedQuality Normalize(
        GlassWipeSimpleGeneratedQuality quality)
    {
        return quality is
            GlassWipeSimpleGeneratedQuality.Auto or
            GlassWipeSimpleGeneratedQuality.Low or
            GlassWipeSimpleGeneratedQuality.Standard or
            GlassWipeSimpleGeneratedQuality.High
                ? quality
                : GlassWipeSimpleGeneratedQuality.Auto;
    }
}
