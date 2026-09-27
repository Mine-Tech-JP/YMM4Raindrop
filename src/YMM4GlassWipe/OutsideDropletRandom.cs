// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

/// <summary>
/// 外側水滴用のCPU/GPU共通32-bit整数疑似乱数を提供します。
/// </summary>
internal static class OutsideDropletRandom
{
    public static uint Mix(uint value)
    {
        unchecked
        {
            value ^= value >> 16;
            value *= 0x7feb352dU;
            value ^= value >> 15;
            value *= 0x846ca68bU;
            value ^= value >> 16;
            return value;
        }
    }

    public static uint GetCellKey(int cellX, int cellY, uint seed, uint layer)
    {
        unchecked
        {
            var key = Mix(seed ^ 0xa511e9b3U);
            key = Mix(key ^ (uint)cellX);
            key = Mix(key ^ (uint)cellY);
            return Mix(key ^ layer);
        }
    }

    public static uint GetEmitterKey(uint seed, uint layer, uint emitterIndex)
    {
        unchecked
        {
            var key = Mix(seed ^ 0x63d83595U);
            key = Mix(key ^ layer);
            return Mix(key ^ emitterIndex);
        }
    }

    public static uint GetCycleKey(uint emitterKey, uint cycleIndex)
    {
        unchecked
        {
            return Mix(emitterKey ^ Mix(cycleIndex ^ 0xb5297a4dU));
        }
    }

    public static float Sample(uint key, uint channel)
    {
        unchecked
        {
            var bits = Mix(key ^ Mix(channel + 0x9e3779b9U));
            return (bits >> 8) * (1.0f / 16777216.0f);
        }
    }
}
