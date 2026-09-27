// SPDX-License-Identifier: MPL-2.0

using YukkuriMovieMaker.Commons;

namespace YMM4GlassWipe;

/// <summary>アイテム基準の中間点を、拭き取り区間内の相対時刻で評価します。</summary>
internal static class WipeAnimationEvaluation
{
    public static double GetValue(
        Animation animation,
        int frame,
        int evaluationLength,
        int framesPerSecond,
        int sourceItemLength)
    {
        // 中間点なしの周期・加減速などは従来の時間指定で評価する。
        if (animation.KeyFrames is not { Count: > 0 } ||
            sourceItemLength <= 0 || evaluationLength <= 0)
        {
            return animation.GetValue(frame, evaluationLength, framesPerSecond);
        }

        // Animationと共有KeyFramesを変更せず、評価時刻を元の座標系へ戻す。
        // 整数フレームの丸めは正の中間値を切り上げ、両端を正確に対応させる。
        var numerator = (long)Math.Clamp(frame, 0, evaluationLength) * sourceItemLength;
        var sourceFrame = (numerator + evaluationLength / 2L) / evaluationLength;
        return animation.GetValue(sourceFrame, sourceItemLength, framesPerSecond);
    }
}
