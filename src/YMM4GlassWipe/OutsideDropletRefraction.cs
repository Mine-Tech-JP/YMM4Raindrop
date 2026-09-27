// SPDX-License-Identifier: MPL-2.0

using Vortice;

namespace YMM4GlassWipe;

/// <summary>
/// 屈折による近傍参照の要求範囲と無効化範囲を同じ上限で拡張します。
/// </summary>
internal static class OutsideDropletRefraction
{
    // GlassComposite.hlslの屈折変位上限と一致させる。
    internal const float MaximumOffsetAt1080 = 256f;

    internal static int GetPadding(in GlassCompositeConstants constants)
    {
        if (constants.OutsideDropletRenderPass < 0.5f ||
            constants.OutsideDropletAppearance != (float)OutsideDropletAppearance.Realistic ||
            constants.OutsideDropletAmount <= 0f || constants.OutsideDropletStrength <= 0f)
            return 0;

        var height = float.IsFinite(constants.InputHeight) ? Math.Max(constants.InputHeight, 1f) : 1080f;
        var offset = MaximumOffsetAt1080 * Math.Max(height / 1080d, 0.25d);
        // 双線形補間の隣接画素分を含める。極端な入力でも整数の加算を溢れさせない。
        return (int)Math.Min(Math.Ceiling(offset) + 1d, int.MaxValue);
    }

    internal static RawRect Expand(RawRect rectangle, int padding)
    {
        if (padding <= 0 || rectangle.Right <= rectangle.Left || rectangle.Bottom <= rectangle.Top)
            return rectangle;

        return new RawRect(
            (int)Math.Max((long)rectangle.Left - padding, int.MinValue),
            (int)Math.Max((long)rectangle.Top - padding, int.MinValue),
            (int)Math.Min((long)rectangle.Right + padding, int.MaxValue),
            (int)Math.Min((long)rectangle.Bottom + padding, int.MaxValue));
    }
}
