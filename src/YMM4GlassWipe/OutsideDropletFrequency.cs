// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

/// <summary>既存の落下周期を保ち、独立した落下候補を最大4組へ増やします。</summary>
internal static class OutsideDropletFrequency
{
    public const int BaseHeadCount = 18;
    public const int MaximumHeadCount = 72;
    public const int BaseEmitterCountPerLayer = 6;

    public static float Normalize(float scale) =>
        float.IsFinite(scale) ? Math.Clamp(scale, 1f, 4f) : 1f;

    public static int GetHeadCount(float scale) => BaseHeadCount * (int)MathF.Ceiling(Normalize(scale));

    public static int GetEmitterCountPerLayer(float scale) =>
        BaseEmitterCountPerLayer * (int)MathF.Ceiling(Normalize(scale));

    public static bool IsEmitterEnabled(uint emitterKey, uint emitterIndex, float scale)
    {
        var cohort = emitterIndex / BaseEmitterCountPerLayer;
        if (cohort == 0) return true;
        var fraction = Math.Clamp(Normalize(scale) - cohort, 0f, 1f);
        // channel 0..9の既存乱数を変えず、小数倍率の追加候補だけを選別する。
        return fraction > 0f && OutsideDropletRandom.Sample(emitterKey, 10U) < fraction;
    }
}
