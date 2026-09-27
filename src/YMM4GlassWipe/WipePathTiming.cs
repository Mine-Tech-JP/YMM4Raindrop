// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

internal static class WipePathTiming
{
    public const double MinimumStartPercent = 0;
    public const double MaximumStartPercent = 100;
    public const double DefaultStartPercent = 0;
    public const double MinimumCompletionPercent = 1;
    public const double MaximumCompletionPercent = 100;
    public const double DefaultCompletionPercent = 50;
    public const double LegacyCompletionPercent = 100;
    public const double MinimumFixedFrame = 0;
    public const double MaximumFixedFrame = 1_000_000;
    public const double DefaultStartFrame = 0;
    public const double DefaultCompletionFrame = 60;
    public const double MinimumSeconds = 0;
    public const double MaximumSeconds = 36_000;
    public const double DefaultStartSeconds = 0;
    public const double DefaultCompletionSeconds = 1;

    public static double ResolveAfterDeserialization(
        bool completionWasSet,
        double completionPercent)
    {
        return completionWasSet
            ? SanitizeCompletionPercent(completionPercent)
            : LegacyCompletionPercent;
    }

    public static double SanitizeCompletionPercent(double completionPercent) =>
        double.IsFinite(completionPercent)
            ? Math.Clamp(
                completionPercent,
                MinimumCompletionPercent,
                MaximumCompletionPercent)
            : DefaultCompletionPercent;

    public static double SanitizeStartPercent(double startPercent) =>
        double.IsFinite(startPercent)
            ? Math.Clamp(startPercent, MinimumStartPercent, MaximumStartPercent)
            : DefaultStartPercent;

    public static double SanitizeFixedFrame(
        double frame,
        double fallback = DefaultStartFrame) =>
        double.IsFinite(frame)
            ? Math.Clamp(frame, MinimumFixedFrame, MaximumFixedFrame)
            : Math.Clamp(fallback, MinimumFixedFrame, MaximumFixedFrame);

    public static double SanitizeSeconds(
        double seconds,
        double fallback = DefaultStartSeconds) =>
        double.IsFinite(seconds)
            ? Math.Clamp(seconds, MinimumSeconds, MaximumSeconds)
            : Math.Clamp(fallback, MinimumSeconds, MaximumSeconds);

    public static WipePathTimingWindow ResolveWindow(
        int itemLength,
        int framesPerSecond,
        WipePathTimingMode mode,
        double startPercent,
        double completionPercent,
        double startFrame,
        double completionFrame,
        double startSeconds,
        double completionSeconds)
    {
        itemLength = Math.Max(0, itemLength);
        framesPerSecond = Math.Max(1, framesPerSecond);
        mode = WipePathTimingModeCompatibility.Normalize(mode);

        var resolvedStart = mode switch
        {
            WipePathTimingMode.Frame => ToFrame(SanitizeFixedFrame(startFrame)),
            WipePathTimingMode.Seconds => SecondsToFrame(startSeconds, framesPerSecond),
            _ => PercentToFrame(itemLength, SanitizeStartPercent(startPercent)),
        };
        var resolvedCompletion = mode switch
        {
            WipePathTimingMode.Frame => ToFrame(SanitizeFixedFrame(
                completionFrame,
                DefaultCompletionFrame)),
            WipePathTimingMode.Seconds => SecondsToFrame(
                completionSeconds,
                framesPerSecond,
                DefaultCompletionSeconds),
            _ => GetCompletionFrame(itemLength, completionPercent),
        };
        return new WipePathTimingWindow(
            resolvedStart,
            resolvedCompletion,
            resolvedCompletion <= resolvedStart);
    }

    public static int GetCompletionFrame(
        int itemLength,
        double completionPercent)
    {
        itemLength = Math.Max(0, itemLength);
        if (itemLength == 0)
        {
            return 0;
        }

        var sanitizedPercent = SanitizeCompletionPercent(completionPercent);
        var frame = (int)Math.Round(
            itemLength * sanitizedPercent / 100,
            MidpointRounding.AwayFromZero);
        return Math.Clamp(frame, 1, itemLength);
    }

    private static int PercentToFrame(int itemLength, double percent)
    {
        var frame = (int)Math.Round(
            Math.Max(0, itemLength) * percent / 100,
            MidpointRounding.AwayFromZero);
        return Math.Clamp(frame, 0, Math.Max(0, itemLength));
    }

    private static int SecondsToFrame(
        double seconds,
        int framesPerSecond,
        double fallback = DefaultStartSeconds) =>
        ToFrame(SanitizeSeconds(seconds, fallback) * Math.Max(1, framesPerSecond));

    private static int ToFrame(double frame)
    {
        if (frame >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return Math.Max(
            0,
            (int)Math.Round(frame, MidpointRounding.AwayFromZero));
    }
}

internal readonly record struct WipePathTimingWindow(
    int StartFrame,
    int CompletionFrame,
    bool IsImmediate)
{
    public int EvaluationLength => IsImmediate
        ? 1
        : Math.Max(1, CompletionFrame - StartFrame);

    public int GetEvaluationFrame(int currentFrame)
    {
        currentFrame = Math.Max(0, currentFrame);
        if (currentFrame < StartFrame)
        {
            return -1;
        }

        return IsImmediate
            ? EvaluationLength
            : Math.Clamp(currentFrame - StartFrame, 0, EvaluationLength);
    }
}
