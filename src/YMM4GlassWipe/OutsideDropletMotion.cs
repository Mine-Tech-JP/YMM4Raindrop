// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

/// <summary>
/// 外側水滴の時間基準と決定的な落下周期を計算します。
/// </summary>
internal static class OutsideDropletMotion
{
    public const float SecondsPerRegionHeight = 6;

    public static float GetLocalTimeSeconds(int frame, int fps)
    {
        var safeFrame = Math.Max(frame, 0);
        var safeFps = Math.Max(fps, 1);
        return safeFrame / (float)safeFps;
    }

    public static OutsideDropletLifecycle EvaluateLifecycle(
        float localTimeSeconds,
        uint emitterKey,
        float speedScale,
        float sizeSpeedScale,
        bool beginAtStart = false)
    {
        var safeTime = float.IsFinite(localTimeSeconds)
            ? MathF.Max(localTimeSeconds, 0)
            : 0;
        var safeSpeed = float.IsFinite(speedScale)
            ? Math.Clamp(speedScale, 0.25f, 4)
            : 1;
        var safeSizeSpeed = float.IsFinite(sizeSpeedScale)
            ? Math.Clamp(sizeSpeedScale, 0.8f, 1.25f)
            : 1;
        var visibleWaitDuration = GetVisibleWaitDurationSeconds(
            emitterKey,
            safeSpeed,
            safeSizeSpeed);
        var fallDuration = GetFallDurationSeconds(safeSpeed, safeSizeSpeed);
        var cycleDuration = GetCycleDurationSeconds(emitterKey, safeSpeed, safeSizeSpeed);
        var phaseOffset = beginAtStart ? 0f : GetPhaseOffsetSeconds(emitterKey, cycleDuration);
        var absoluteCycleTime = safeTime + phaseOffset;
        var cycleIndex = (long)MathF.Floor(absoluteCycleTime / cycleDuration);
        var cycleTime = absoluteCycleTime - cycleIndex * cycleDuration;
        if (cycleTime < visibleWaitDuration)
        {
            var waitProgress = Math.Clamp(cycleTime / visibleWaitDuration, 0, 1);
            return new OutsideDropletLifecycle(
                cycleIndex,
                0,
                SmoothStep(0, 0.1f, waitProgress),
                OutsideDropletPhase.WaitingAtStart);
        }

        var fallTime = cycleTime - visibleWaitDuration;
        if (fallTime >= fallDuration)
        {
            return new OutsideDropletLifecycle(
                cycleIndex,
                1,
                0,
                OutsideDropletPhase.Hidden);
        }

        var progress = Math.Clamp(fallTime / fallDuration, 0, 1);
        var visibility = 1 - SmoothStep(0.92f, 1, progress);
        return new OutsideDropletLifecycle(
            cycleIndex,
            progress,
            visibility,
            OutsideDropletPhase.Falling);
    }

    /// <summary>
    /// 雨の初回出現を待ち、その滴の出現時刻から待機・落下周期を開始します。
    /// </summary>
    public static OutsideDropletLifecycle EvaluateRainLifecycle(
        float localTimeSeconds,
        uint emitterKey,
        float speedScale,
        float sizeSpeedScale,
        float startSeconds,
        float durationSeconds)
    {
        if (!OutsideDropletRainTiming.HasAppeared(emitterKey, localTimeSeconds, startSeconds, durationSeconds))
        {
            return new OutsideDropletLifecycle(0, 0f, 0f, OutsideDropletPhase.Hidden);
        }

        var birth = OutsideDropletRainTiming.GetAppearanceTimeSeconds(emitterKey, startSeconds, durationSeconds);
        return EvaluateLifecycle(localTimeSeconds - birth, emitterKey, speedScale, sizeSpeedScale, beginAtStart: true);
    }

    public static float GetFallDurationSeconds(float speedScale, float sizeSpeedScale)
    {
        var safeSpeed = float.IsFinite(speedScale)
            ? Math.Clamp(speedScale, 0.25f, 4)
            : 1;
        var safeSizeSpeed = float.IsFinite(sizeSpeedScale)
            ? Math.Clamp(sizeSpeedScale, 0.8f, 1.25f)
            : 1;
        return SecondsPerRegionHeight / (safeSpeed * safeSizeSpeed);
    }

    public static float GetCycleDurationSeconds(
        uint emitterKey,
        float speedScale,
        float sizeSpeedScale)
    {
        var fallDuration = GetFallDurationSeconds(speedScale, sizeSpeedScale);
        return GetVisibleWaitDurationSeconds(emitterKey, speedScale, sizeSpeedScale) +
            fallDuration +
            GetHiddenWaitDurationSeconds(emitterKey, speedScale, sizeSpeedScale);
    }

    public static float GetVisibleWaitDurationSeconds(
        uint emitterKey,
        float speedScale,
        float sizeSpeedScale)
    {
        var fallDuration = GetFallDurationSeconds(speedScale, sizeSpeedScale);
        return fallDuration * Lerp(
            0.2f,
            0.45f,
            OutsideDropletRandom.Sample(emitterKey, 5));
    }

    public static float GetHiddenWaitDurationSeconds(
        uint emitterKey,
        float speedScale,
        float sizeSpeedScale)
    {
        var fallDuration = GetFallDurationSeconds(speedScale, sizeSpeedScale);
        return fallDuration * Lerp(
            0.15f,
            0.45f,
            OutsideDropletRandom.Sample(emitterKey, 6));
    }

    public static float GetPhaseOffsetSeconds(uint emitterKey, float cycleDuration)
    {
        var safeDuration = float.IsFinite(cycleDuration)
            ? MathF.Max(cycleDuration, 0.0001f)
            : SecondsPerRegionHeight;
        return OutsideDropletRandom.Sample(emitterKey, 7) * safeDuration;
    }

    private static float SmoothStep(float minimum, float maximum, float value)
    {
        var amount = Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);
        return amount * amount * (3 - 2 * amount);
    }

    private static float Lerp(float from, float to, float amount) =>
        from + (to - from) * amount;
}

internal enum OutsideDropletPhase
{
    WaitingAtStart,
    Falling,
    Hidden,
}

internal readonly record struct OutsideDropletLifecycle(
    long CycleIndex,
    float Progress,
    float Visibility,
    OutsideDropletPhase Phase)
{
    public bool IsFalling => Phase == OutsideDropletPhase.Falling;

    public bool IsWaitingAtStart => Phase == OutsideDropletPhase.WaitingAtStart;
}
