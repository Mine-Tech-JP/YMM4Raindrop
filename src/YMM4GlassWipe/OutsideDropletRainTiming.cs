// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

/// <summary>
/// 初回の出現時刻を滴の固定キーから求めます。位置・形状用の乱数チャンネルは使用しません。
/// </summary>
internal static class OutsideDropletRainTiming
{
    public const uint AppearanceChannel = 9U;

    /// <summary>アイテム先頭を0秒とした初回出現時刻を返します。</summary>
    public static float GetAppearanceTimeSeconds(uint key, float startSeconds, float durationSeconds)
    {
        var start = float.IsFinite(startSeconds) ? Math.Clamp(startSeconds, 0f, 36000f) : 0f;
        var duration = float.IsFinite(durationSeconds) ? Math.Clamp(durationSeconds, 0f, 36000f) : 5f;
        var delay = duration * OutsideDropletRandom.Sample(key, AppearanceChannel);
        return start + delay;
    }

    /// <summary>未出現の滴は、描画にも接触判定にも参加しません。</summary>
    public static bool HasAppeared(uint key, float localTimeSeconds, float startSeconds, float durationSeconds) =>
        float.IsFinite(localTimeSeconds) &&
        localTimeSeconds >= GetAppearanceTimeSeconds(key, startSeconds, durationSeconds);
}
