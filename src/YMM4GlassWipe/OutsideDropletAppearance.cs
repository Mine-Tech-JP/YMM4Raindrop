// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel.DataAnnotations;

namespace YMM4GlassWipe;

/// <summary>
/// 外側水滴の見た目を表します。
/// </summary>
internal enum OutsideDropletAppearance
{
    [Display(Name = "白い輪郭")]
    Legacy = 0,

    [Display(Name = "黒い輪郭")]
    Transparent = 1,

    [Display(Name = "リアル（屈折）")]
    Realistic = 2,
}

/// <summary>
/// 外側水滴の見た目を保存値から安全に正規化します。
/// </summary>
internal static class OutsideDropletAppearanceCompatibility
{
    public const OutsideDropletAppearance Default = OutsideDropletAppearance.Transparent;

    public const OutsideDropletAppearance LegacyMigrationDefault =
        OutsideDropletAppearance.Legacy;

    public static OutsideDropletAppearance ResolveAfterDeserialization(
        bool appearanceWasSet,
        OutsideDropletAppearance appearance) =>
        appearanceWasSet
            ? Normalize(appearance)
            : LegacyMigrationDefault;

    public static OutsideDropletAppearance Normalize(OutsideDropletAppearance appearance) =>
        appearance is OutsideDropletAppearance.Legacy or
            OutsideDropletAppearance.Transparent or
            OutsideDropletAppearance.Realistic
            ? appearance
            : Default;
}
