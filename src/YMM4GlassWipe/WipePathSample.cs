// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

internal readonly record struct WipePathSample(
    int Frame,
    float X,
    float Y,
    float Contact,
    float Size,
    float Strength,
    float AspectRatio,
    float RotationRadians,
    int AccumulationGroup = 0,
    float? BrushWidthPixels = null,
    float? BrushHeightPixels = null)
{
    public bool IsContacting => Contact > 0;
}
