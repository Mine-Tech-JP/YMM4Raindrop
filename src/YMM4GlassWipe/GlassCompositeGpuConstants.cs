// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace YMM4GlassWipe;

[InlineArray(OutsideDropletFrequency.MaximumHeadCount)]
internal struct OutsideDropletMergeHeadSlots
{
    private Vector4 _element0;
}

[InlineArray(2048)]
internal struct OutsideDropletMergeStaticSlots
{
    private Vector4 _element0;
}

[InlineArray(6656)]
internal struct OutsideDropletInitialStateWords
{
    private uint _element0;
}

[StructLayout(LayoutKind.Sequential)]
internal struct GlassCompositeGpuConstants
{
    internal const int MergeHeadCount = OutsideDropletFrequency.MaximumHeadCount;
    internal const int MergeStaticSlotCount = 2048;
    internal const int MergeStaticProbeLimit = 32;

    public GlassCompositeConstants Core;
    public OutsideDropletMergeHeadSlots MergeHeadsA;
    public OutsideDropletMergeHeadSlots MergeHeadsB;
    public OutsideDropletMergeStaticSlots MergeStaticSlots;
    public Vector4 InitialStaticColumns;
    public Vector4 InitialStaticCounts;
    public OutsideDropletInitialStateWords InitialStaticStates;

    public bool TryApply(OutsideDropletMergeFrame frame)
    {
        if (!frame.IsValid || frame.Heads.Count < OutsideDropletFrequency.BaseHeadCount ||
            frame.Heads.Count > MergeHeadCount || frame.Heads.Count % OutsideDropletFrequency.BaseHeadCount != 0)
        {
            return false;
        }

        Span<bool> assignedHeads = stackalloc bool[MergeHeadCount];
        assignedHeads.Clear();
        foreach (var head in frame.Heads)
        {
            if (!IsValid(head) ||
                (uint)head.EmitterIndex >= frame.Heads.Count)
            {
                return false;
            }

            if (assignedHeads[head.EmitterIndex])
            {
                return false;
            }

            assignedHeads[head.EmitterIndex] = true;
        }

        foreach (var change in frame.StaticChanges)
        {
            if (!float.IsFinite(change.Visibility) ||
                change.Visibility < 0f ||
                change.Visibility > 1f ||
                !float.IsFinite(change.RadiusScale) || change.RadiusScale < 1f || change.RadiusScale > 1.35f)
            {
                return false;
            }
        }

        MergeHeadsA = default;
        MergeHeadsB = default;
        MergeStaticSlots = default;
        InitialStaticColumns = default;
        InitialStaticCounts = default;
        InitialStaticStates = default;
        if (frame.StaticLayout is { } layout)
        {
            if (layout.PackedStates.Count > 6656) return false;
            InitialStaticColumns = new Vector4(layout.ColumnCounts, 1f);
            InitialStaticCounts = new Vector4(layout.CellCounts, 0f);
            for (var index = 0; index < layout.PackedStates.Count; index++)
                InitialStaticStates[index] = layout.PackedStates[index];
        }

        foreach (var head in frame.Heads)
        {
            if (head.Radius <= 0f || head.Visibility <= 0f)
            {
                continue;
            }

            MergeHeadsA[head.EmitterIndex] = new Vector4(
                head.InputUv.X,
                head.InputUv.Y,
                head.Radius,
                head.VerticalScale);
            MergeHeadsB[head.EmitterIndex] = new Vector4(
                head.SpawnInputUv.X,
                head.SpawnInputUv.Y,
                head.Visibility,
                head.FallProgress);
        }

        foreach (var change in frame.StaticChanges)
        {
            var key = change.CellKey;
            if (frame.StaticLayout is { } staticLayout)
            {
                if (!staticLayout.TryGetCellIndex(change.Layer, change.CellX, change.CellY, out key)) return false;
                staticLayout.TryGetState(change.Layer, change.CellX, change.CellY, out var visibility, out var scale);
                if (change.Visibility == visibility && change.RadiusScale == scale) continue;
            }
            if (!TryAddStaticChange(key, change.Visibility, change.RadiusScale))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValid(OutsideDropletMergeHeadState head) =>
        float.IsFinite(head.InputUv.X) &&
        float.IsFinite(head.InputUv.Y) &&
        float.IsFinite(head.SpawnInputUv.X) &&
        float.IsFinite(head.SpawnInputUv.Y) &&
        float.IsFinite(head.Radius) &&
        float.IsFinite(head.VerticalScale) &&
        float.IsFinite(head.Visibility) &&
        float.IsFinite(head.FallProgress) &&
        head.Radius >= 0f &&
        head.VerticalScale >= 0f &&
        (head.Radius <= 0f || head.VerticalScale > 0f) &&
        head.Visibility >= 0f &&
        head.Visibility <= 1f &&
        head.FallProgress >= 0f &&
        head.FallProgress <= 1f;

    private bool TryAddStaticChange(uint cellKey, float visibility, float radiusScale)
    {
        var keyLow = cellKey & 0xffffu;
        var keyHigh = cellKey >> 16;
        var firstSlot = (int)(cellKey & (MergeStaticSlotCount - 1));
        for (var probe = 0; probe < MergeStaticProbeLimit; probe++)
        {
            var slotIndex = (firstSlot + probe) & (MergeStaticSlotCount - 1);
            var slot = MergeStaticSlots[slotIndex];
            if (slot.W < 0.5f)
            {
                MergeStaticSlots[slotIndex] = new Vector4(
                    keyLow,
                    keyHigh,
                    visibility,
                    radiusScale);
                return true;
            }

            if ((uint)slot.X == keyLow && (uint)slot.Y == keyHigh)
            {
                return false;
            }
        }

        return false;
    }
}
