// SPDX-License-Identifier: MPL-2.0

using System.Globalization;
using System.Numerics;

namespace YMM4GlassWipe;

/// <summary>
/// 標準モードの入力から、現在フレームまでの決定論的な拭き取り軌跡を生成します。
/// </summary>
internal static partial class WipeSimplePathGenerator
{
    private const double CircularArcEpsilon = 1e-6;
    private const double TwoPi = Math.PI * 2;
    internal const double DefaultInvalidRoundTrips = 2;
    internal const double MinimumRoundTrips = 1;
    internal const double MaximumRoundTrips = 5;
    internal const double RoundTripIncrement = 0.5;
    internal const int MinimumRoundTripPassCount = 2;
    internal const int MaximumRoundTripPassCount = 10;
    internal const double DefaultWipeAmountPerPassPercent = 40;
    internal const double MinimumWipeAmountPerPassPercent = 0;
    internal const double MaximumWipeAmountPerPassPercent = 100;
    internal const double DefaultReturnOffsetXPercent = 0;
    internal const double DefaultReturnOffsetPercent = 3.5;
    internal const double MinimumReturnOffsetPercent = -20;
    internal const double MaximumReturnOffsetPercent = 20;
    internal const float MinimumGeneratedPathCoordinate = -1;
    internal const float MaximumGeneratedPathCoordinate = 2;
    private static readonly double[] CircularArcExtremaAngles =
    [
        0,
        Math.PI * 0.5,
        Math.PI,
        Math.PI * 1.5,
    ];
    private static readonly WipeMaskGeometry UnitGeometry =
        WipeMaskGeometry.Create(1, 1);

    /// <summary>
    /// 標準モードで使う固定の描画スタイルです。
    /// </summary>
    public static readonly WipePathStyle FixedStyle = new(
        0,
        0,
        0,
        0,
        0.15f,
        GlassWipeQuality.Standard);

    /// <summary>
    /// 往復プリセットを片道単位で累積するときの固定描画スタイルです。
    /// </summary>
    public static readonly WipePathStyle FixedProgressiveStyle = FixedStyle with
    {
        Quality = GlassWipeQuality.High,
        AccumulationMode = WipeMaskAccumulationMode.PerPassMaximum,
    };

    /// <summary>
    /// 指定した定型、補間、座標から現在フレームまでの軌跡を生成します。
    /// 座標は入力アイテムを基準にした0から100の百分率です。
    /// </summary>
    public static WipeSimplePathGenerationResult Generate(
        GlassWipeSimplePattern pattern,
        GlassWipePathInterpolation interpolation,
        Vector2 startXY,
        Vector2 controlXY,
        Vector2 endXY,
        double roundTrips,
        int currentFrame,
        int itemLength,
        double returnOffsetPercent = DefaultReturnOffsetPercent,
        double wipeAmountPerPassPercent = DefaultWipeAmountPerPassPercent,
        WipeMaskGeometry? maskGeometry = null,
        double returnOffsetXPercent = DefaultReturnOffsetXPercent,
        Func<int, float>? progressProvider = null,
        GlassWipeSimpleGeneratedQuality simpleGeneratedQuality =
            GlassWipeSimpleGeneratedQuality.Auto,
        bool continuousWipe = false)
    {
        var evaluator = CreateStreamEvaluator(
            pattern,
            interpolation,
            startXY,
            controlXY,
            endXY,
            roundTrips,
            itemLength,
            returnOffsetPercent,
            wipeAmountPerPassPercent,
            maskGeometry,
            returnOffsetXPercent,
            progressProvider,
            simpleGeneratedQuality,
            continuousWipe);
        var sanitizedItemLength = evaluator.ItemLength;
        var sanitizedFrame = Math.Clamp(currentFrame, 0, sanitizedItemLength);
        var samples = new WipePathSample[sanitizedFrame + 1];

        for (var frame = 0; frame <= sanitizedFrame; frame++)
        {
            samples[frame] = evaluator.Evaluate(frame);
        }

        return new WipeSimplePathGenerationResult(
            sanitizedFrame,
            sanitizedItemLength,
            samples,
            evaluator.Style,
            evaluator.CreateSignature(sanitizedFrame));
    }

    internal static StreamEvaluator CreateStreamEvaluator(
        GlassWipeSimplePattern pattern,
        GlassWipePathInterpolation interpolation,
        Vector2 startXY,
        Vector2 controlXY,
        Vector2 endXY,
        double roundTrips,
        int itemLength,
        double returnOffsetPercent = DefaultReturnOffsetPercent,
        double wipeAmountPerPassPercent = DefaultWipeAmountPerPassPercent,
        WipeMaskGeometry? maskGeometry = null,
        double returnOffsetXPercent = DefaultReturnOffsetXPercent,
        Func<int, float>? progressProvider = null,
        GlassWipeSimpleGeneratedQuality simpleGeneratedQuality =
            GlassWipeSimpleGeneratedQuality.Auto,
        bool continuousWipe = false) =>
        new(
            pattern,
            interpolation,
            startXY,
            controlXY,
            endXY,
            roundTrips,
            itemLength,
            returnOffsetPercent,
            wipeAmountPerPassPercent,
            maskGeometry,
            returnOffsetXPercent,
            progressProvider,
            simpleGeneratedQuality,
            continuousWipe);

    internal sealed class StreamEvaluator
    {
        private readonly GlassWipeSimplePattern _pattern;
        private readonly GlassWipePathInterpolation _interpolation;
        private readonly Vector2 _start;
        private readonly Vector2 _control;
        private readonly Vector2 _end;
        private readonly int _passCount;
        private readonly Vector2 _returnOffset;
        private readonly float _wipeAmountPerPass;
        private readonly WipeMaskGeometry _circularArcGeometry;
        private readonly Vector2[] _points;
        private readonly WipeSimplePathPreset _preset;
        private readonly bool _isRoundTripPattern;
        private readonly Func<int, float>? _progressProvider;

        internal StreamEvaluator(
            GlassWipeSimplePattern pattern,
            GlassWipePathInterpolation interpolation,
            Vector2 startXY,
            Vector2 controlXY,
            Vector2 endXY,
            double roundTrips,
            int itemLength,
            double returnOffsetPercent,
            double wipeAmountPerPassPercent,
            WipeMaskGeometry? maskGeometry,
            double returnOffsetXPercent,
            Func<int, float>? progressProvider,
            GlassWipeSimpleGeneratedQuality simpleGeneratedQuality,
            bool continuousWipe)
        {
            _pattern = SanitizePattern(pattern);
            _interpolation = SanitizeInterpolationForPattern(_pattern, interpolation);
            _start = ToUnit(startXY);
            _control = ToUnit(controlXY);
            _end = ToUnit(endXY);
            _passCount = ToPassCount(roundTrips);
            _isRoundTripPattern = IsRoundTripPattern(_pattern);
            _wipeAmountPerPass = _isRoundTripPattern
                ? SanitizeWipeAmountPerPass(wipeAmountPerPassPercent)
                : SanitizeWipeAmountPerPass(DefaultWipeAmountPerPassPercent);
            _returnOffset = _pattern == GlassWipeSimplePattern.OffsetRoundTrips
                ? new Vector2(
                    SanitizeReturnOffset(returnOffsetXPercent, DefaultReturnOffsetXPercent),
                    SanitizeReturnOffset(returnOffsetPercent, DefaultReturnOffsetPercent))
                : new Vector2(
                    SanitizeReturnOffset(DefaultReturnOffsetXPercent, DefaultReturnOffsetXPercent),
                    SanitizeReturnOffset(DefaultReturnOffsetPercent, DefaultReturnOffsetPercent));
            ItemLength = Math.Max(0, itemLength);
            _circularArcGeometry =
                _interpolation == GlassWipePathInterpolation.CircularArc &&
                UsesControlPoint(_pattern)
                    ? SanitizeCircularArcGeometry(maskGeometry)
                    : UnitGeometry;
            _points = CreatePoints(
                _pattern,
                _start,
                _control,
                _end,
                _passCount,
                _returnOffset);
            _preset = GetPreset(_pattern);
            _progressProvider = progressProvider;

            var (initialAmount, nextAmount) = ResolveInitialProgressAmounts(
                ItemLength,
                progressProvider);
            var initialPosition = EvaluatePosition(initialAmount);
            var nextPosition = EvaluatePosition(nextAmount);
            var initialTangent = nextPosition - initialPosition;
            Style = (_isRoundTripPattern ? FixedProgressiveStyle : FixedStyle) with
            {
                Quality = ResolveSimpleGeneratedQuality(
                    simpleGeneratedQuality,
                    _isRoundTripPattern),
                InitialTangentX = initialTangent.X,
                InitialTangentY = initialTangent.Y,
                ContinuousWipe = continuousWipe,
            };
        }

        public int ItemLength { get; }

        public WipePathStyle Style { get; }

        public WipePathSample Evaluate(int frame)
        {
            frame = Math.Clamp(frame, 0, ItemLength);
            var amount = _progressProvider is null
                ? ItemLength == 0
                    ? 0
                    : frame / (float)ItemLength
                : SanitizeProgress(_progressProvider(frame));
            var position = EvaluatePosition(amount);
            return new WipePathSample(
                frame,
                position.X,
                position.Y,
                1,
                _preset.Size,
                _isRoundTripPattern ? _wipeAmountPerPass : _preset.Strength,
                _preset.AspectRatio,
                _preset.RotationRadians,
                _isRoundTripPattern ? GetAccumulationGroup(amount, _passCount) : 0);
        }

        public string CreateSignature(int currentFrame) =>
            WipeSimplePathGenerator.CreateSignature(
            _pattern,
            _interpolation,
            _start,
            _control,
            _end,
            _passCount,
            _returnOffset,
            _wipeAmountPerPass,
            _circularArcGeometry,
            Math.Clamp(currentFrame, 0, ItemLength),
                ItemLength,
                Style);

        private Vector2 EvaluatePosition(float amount) => EvaluatePathPosition(
            _pattern,
            _interpolation,
            _points,
            _start,
            _control,
            _end,
            _passCount,
            _returnOffset,
            amount,
            _circularArcGeometry);
    }

    private static Vector2 EvaluatePathPosition(
        GlassWipeSimplePattern pattern,
        GlassWipePathInterpolation interpolation,
        IReadOnlyList<Vector2> points,
        Vector2 start,
        Vector2 control,
        Vector2 end,
        int passCount,
        Vector2 returnOffset,
        float amount,
        WipeMaskGeometry circularArcGeometry) =>
        interpolation switch
        {
            GlassWipePathInterpolation.CircularArc when UsesControlPoint(pattern) =>
                EvaluateCircularArcPath(
                    pattern,
                    points,
                    start,
                    control,
                    end,
                    passCount,
                    returnOffset,
                    amount,
                    circularArcGeometry),
            GlassWipePathInterpolation.Smooth when
                pattern == GlassWipeSimplePattern.ArcRoundTrips =>
                EvaluateArcRoundTripCurve(
                    start,
                    control,
                    end,
                    passCount,
                    amount),
            GlassWipePathInterpolation.Smooth when
                pattern == GlassWipeSimplePattern.OffsetRoundTrips =>
                EvaluateOffsetRoundTripCurve(points, control, returnOffset, amount),
            _ when pattern == GlassWipeSimplePattern.OffsetRoundTrips =>
                Evaluate(points, interpolation, amount, allowOutside: true),
            _ => Evaluate(points, interpolation, amount),
        };

    private static (float Initial, float Next) ResolveInitialProgressAmounts(
        int itemLength,
        Func<int, float>? progressProvider)
    {
        var initial = progressProvider is null
            ? 0
            : SanitizeProgress(progressProvider(0));
        if (itemLength <= 0)
        {
            return (initial, initial);
        }

        if (progressProvider is null)
        {
            return (initial, 1f / itemLength);
        }

        for (var frame = 1; frame <= itemLength; frame = NextProbeFrame(frame, itemLength))
        {
            var next = SanitizeProgress(progressProvider(frame));
            if (MathF.Abs(next - initial) > 0.000001f)
            {
                return (initial, next);
            }

            if (frame == itemLength)
            {
                break;
            }
        }

        return (initial, initial);
    }

    private static int NextProbeFrame(int frame, int itemLength) =>
        frame >= itemLength / 2
            ? itemLength
            : frame * 2;

    private static Vector2[] CreatePoints(
        GlassWipeSimplePattern pattern,
        Vector2 start,
        Vector2 control,
        Vector2 end,
        int passCount,
        Vector2 returnOffset)
    {
        return pattern switch
        {
            GlassWipeSimplePattern.GentleArcOnce => [start, control, end],
            GlassWipeSimplePattern.ShortRoundTrips => CreateRoundTripPoints(
                start,
                end,
                passCount,
                false,
                control),
            GlassWipeSimplePattern.ArcRoundTrips => CreateRoundTripPoints(
                start,
                end,
                passCount,
                true,
                control),
            GlassWipeSimplePattern.OffsetRoundTrips => CreateOffsetRoundTripPoints(
                start,
                end,
                passCount,
                returnOffset),
            _ => [start, end],
        };
    }

    private static Vector2[] CreateRoundTripPoints(
        Vector2 start,
        Vector2 end,
        int passCount,
        bool isCurved,
        Vector2 control)
    {
        var result = new List<Vector2>(passCount * (isCurved ? 2 : 1) + 1)
        {
            start,
        };

        for (var passIndex = 0; passIndex < passCount; passIndex++)
        {
            if (isCurved)
            {
                result.Add(control);
            }

            result.Add(passIndex % 2 == 0 ? end : start);
        }

        return result.ToArray();
    }

    private static Vector2[] CreateOffsetRoundTripPoints(
        Vector2 start,
        Vector2 end,
        int passCount,
        Vector2 returnOffset)
    {
        var result = new List<Vector2>(passCount + 1)
        {
            start,
        };

        for (var passIndex = 0; passIndex < passCount; passIndex++)
        {
            var completedRoundTrips = (passIndex + 1) / 2;
            var destination = passIndex % 2 == 0 ? end : start;
            result.Add(Offset(destination, returnOffset * completedRoundTrips));
        }

        return result.ToArray();
    }

    /// <summary>
    /// 経由点あり往復の基準曲線を、復路では同じ位置を逆順に評価します。
    /// </summary>
    private static Vector2 EvaluateArcRoundTripCurve(
        Vector2 start,
        Vector2 control,
        Vector2 end,
        int passCount,
        float amount)
    {
        amount = float.IsFinite(amount) ? Math.Clamp(amount, 0, 1) : 0;
        var scaledAmount = amount * passCount;
        var passIndex = Math.Min((int)scaledAmount, passCount - 1);
        var passAmount = scaledAmount - passIndex;

        return EvaluateThreePointCurve(
            start,
            control,
            end,
            passIndex % 2 == 0 ? passAmount : 1 - passAmount);
    }

    /// <summary>
    /// ずらし往復の既存ノード時刻を維持し、各区間内で経由点を通る曲線を評価します。
    /// </summary>
    private static Vector2 EvaluateOffsetRoundTripCurve(
        IReadOnlyList<Vector2> points,
        Vector2 control,
        Vector2 returnOffset,
        float amount)
    {
        if (points.Count < 2)
        {
            return Evaluate(
                points,
                GlassWipePathInterpolation.Smooth,
                amount,
                allowOutside: true);
        }

        amount = float.IsFinite(amount) ? Math.Clamp(amount, 0, 1) : 0;
        var scaledAmount = amount * (points.Count - 1);
        var segmentIndex = Math.Min(
            (int)scaledAmount,
            points.Count - 2);
        var segmentAmount = scaledAmount - segmentIndex;
        var controlOffset = returnOffset * (segmentIndex * 0.5f);
        var segmentControl = Offset(control, controlOffset);

        return EvaluateThreePointCurve(
            points[segmentIndex],
            segmentControl,
            points[segmentIndex + 1],
            segmentAmount,
            allowOutside: true);
    }

    private static Vector2 EvaluateThreePointCurve(
        Vector2 start,
        Vector2 control,
        Vector2 end,
        float amount,
        bool allowOutside = false)
    {
        amount = float.IsFinite(amount) ? Math.Clamp(amount, 0, 1) : 0;
        Vector2 result;

        if (amount < 0.5f)
        {
            result = CentripetalCatmullRom.Evaluate(
                start * 2 - control,
                start,
                control,
                end,
                amount * 2);
        }
        else
        {
            result = CentripetalCatmullRom.Evaluate(
                start,
                control,
                end,
                end * 2 - control,
                (amount - 0.5f) * 2);
        }

        return allowOutside ? ClampGeneratedPath(result) : ClampUnit(result);
    }

    private static Vector2 Evaluate(
        IReadOnlyList<Vector2> points,
        GlassWipePathInterpolation interpolation,
        float amount,
        bool allowOutside = false)
    {
        if (points.Count == 0)
        {
            return Vector2.Zero;
        }

        if (points.Count == 1)
        {
            return points[0];
        }

        amount = float.IsFinite(amount) ? Math.Clamp(amount, 0, 1) : 0;
        var scaledAmount = amount * (points.Count - 1);
        var segmentIndex = Math.Min(
            (int)scaledAmount,
            points.Count - 2);
        var segmentAmount = scaledAmount - segmentIndex;
        var point1 = points[segmentIndex];
        var point2 = points[segmentIndex + 1];
        Vector2 result;

        if (interpolation == GlassWipePathInterpolation.Smooth)
        {
            var point0 = segmentIndex > 0
                ? points[segmentIndex - 1]
                : point1 * 2 - point2;
            var point3 = segmentIndex + 2 < points.Count
                ? points[segmentIndex + 2]
                : point2 * 2 - point1;
            result = CentripetalCatmullRom.Evaluate(
                point0,
                point1,
                point2,
                point3,
                segmentAmount);
        }
        else
        {
            result = Vector2.Lerp(point1, point2, segmentAmount);
        }

        return allowOutside ? ClampGeneratedPath(result) : ClampUnit(result);
    }

    private static WipeSimplePathPreset GetPreset(
        GlassWipeSimplePattern pattern) =>
        pattern switch
        {
            GlassWipeSimplePattern.GentleArcOnce => new(0.15f, 0.82f, 1.25f, 0.12f),
            GlassWipeSimplePattern.ShortRoundTrips => new(0.12f, 0.74f, 1.10f, 0),
            GlassWipeSimplePattern.ArcRoundTrips => new(0.14f, 0.80f, 1.25f, 0.12f),
            GlassWipeSimplePattern.OffsetRoundTrips => new(0.13f, 0.76f, 1.40f, 0),
            _ => new(0.16f, 0.86f, 1.35f, 0),
        };

    internal static GlassWipeQuality ResolveSimpleGeneratedQuality(
        GlassWipeSimpleGeneratedQuality simpleGeneratedQuality,
        bool isRoundTripPattern) =>
        GlassWipeSimpleGeneratedQualityCompatibility.Normalize(
            simpleGeneratedQuality) switch
        {
            GlassWipeSimpleGeneratedQuality.Low => GlassWipeQuality.Low,
            GlassWipeSimpleGeneratedQuality.Standard => GlassWipeQuality.Standard,
            GlassWipeSimpleGeneratedQuality.High => GlassWipeQuality.High,
            _ => isRoundTripPattern
                ? GlassWipeQuality.High
                : GlassWipeQuality.Standard,
        };

    private static string CreateSignature(
        GlassWipeSimplePattern pattern,
        GlassWipePathInterpolation interpolation,
        Vector2 start,
        Vector2 control,
        Vector2 end,
        int passCount,
        Vector2 returnOffset,
        float wipeAmountPerPass,
        WipeMaskGeometry circularArcGeometry,
        int currentFrame,
        int itemLength,
        WipePathStyle style) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"simple:v9|pattern={(int)pattern}|interpolation={(int)interpolation}|start={ToCanonical(start.X)},{ToCanonical(start.Y)}|control={ToCanonical(control.X)},{ToCanonical(control.Y)}|end={ToCanonical(end.X)},{ToCanonical(end.Y)}|passCount={passCount}|returnOffset={ToCanonical(returnOffset.X)},{ToCanonical(returnOffset.Y)}|wipeAmountPerPass={ToCanonical(wipeAmountPerPass)}|arcGeometry={ToCanonical(circularArcGeometry.ScaleX)},{ToCanonical(circularArcGeometry.ScaleY)}|frame={currentFrame}|itemLength={itemLength}|style={ToCanonical(FixedStyle.Smoothing)},{ToCanonical(FixedStyle.Jitter)},{FixedStyle.Seed},{ToCanonical(FixedStyle.RotationFollow)},{ToCanonical(FixedStyle.Softness)},{(int)style.Sanitize().Quality}|continuousWipe={(style.ContinuousWipe ? 1 : 0)}");

    private static string ToCanonical(float value) =>
        BitConverter.SingleToUInt32Bits(value)
            .ToString("X8", CultureInfo.InvariantCulture);

    private static GlassWipeSimplePattern SanitizePattern(
        GlassWipeSimplePattern pattern) =>
        pattern is >= GlassWipeSimplePattern.StraightOnce and <= GlassWipeSimplePattern.OffsetRoundTrips
            ? pattern
            : GlassWipeSimplePattern.StraightOnce;

    private static GlassWipePathInterpolation SanitizeInterpolation(
        GlassWipePathInterpolation interpolation) =>
        interpolation is >= GlassWipePathInterpolation.Linear and
            <= GlassWipePathInterpolation.CircularArc
                ? interpolation
                : GlassWipePathInterpolation.Linear;

    private static GlassWipePathInterpolation SanitizeInterpolationForPattern(
        GlassWipeSimplePattern pattern,
        GlassWipePathInterpolation interpolation) =>
        pattern is
            GlassWipeSimplePattern.StraightOnce or
            GlassWipeSimplePattern.ShortRoundTrips
                ? GlassWipePathInterpolation.Linear
                : SanitizeInterpolation(interpolation);

    private static WipeMaskGeometry SanitizeCircularArcGeometry(
        WipeMaskGeometry? geometry)
    {
        if (geometry is not { } value ||
            !float.IsFinite(value.ScaleX) ||
            !float.IsFinite(value.ScaleY) ||
            value.ScaleX <= 0 ||
            value.ScaleY <= 0)
        {
            return UnitGeometry;
        }

        return new WipeMaskGeometry(
            Math.Clamp(value.ScaleX, 0.000001f, 1),
            Math.Clamp(value.ScaleY, 0.000001f, 1));
    }

    private static bool UsesControlPoint(GlassWipeSimplePattern pattern) =>
        pattern is GlassWipeSimplePattern.GentleArcOnce or
            GlassWipeSimplePattern.ArcRoundTrips or
            GlassWipeSimplePattern.OffsetRoundTrips;

    private static bool IsRoundTripPattern(GlassWipeSimplePattern pattern) =>
        pattern is GlassWipeSimplePattern.ShortRoundTrips or
            GlassWipeSimplePattern.ArcRoundTrips or
            GlassWipeSimplePattern.OffsetRoundTrips;

    private static int GetAccumulationGroup(float amount, int passCount)
    {
        var sanitizedAmount = float.IsFinite(amount)
            ? Math.Clamp(amount, 0, 1)
            : 0;
        if (sanitizedAmount <= 0)
        {
            return 0;
        }

        return Math.Clamp(
            (int)MathF.Ceiling(sanitizedAmount * passCount) - 1,
            0,
            passCount - 1);
    }

    /// <summary>
    /// 往復回数を0.5刻みへ補正し、軌跡評価に使う片道数へ変換します。
    /// </summary>
    internal static int ToPassCount(double roundTrips)
    {
        var sanitized = double.IsFinite(roundTrips)
            ? Math.Clamp(roundTrips, MinimumRoundTrips, MaximumRoundTrips)
            : DefaultInvalidRoundTrips;
        return Math.Clamp(
            (int)Math.Round(
                sanitized / RoundTripIncrement,
                MidpointRounding.AwayFromZero),
            MinimumRoundTripPassCount,
            MaximumRoundTripPassCount);
    }

    internal static double SanitizeRoundTrips(double roundTrips) =>
        ToPassCount(roundTrips) * RoundTripIncrement;

    private static float SanitizeWipeAmountPerPass(double percent)
    {
        var sanitizedPercent = double.IsFinite(percent)
            ? Math.Clamp(
                percent,
                MinimumWipeAmountPerPassPercent,
                MaximumWipeAmountPerPassPercent)
            : DefaultWipeAmountPerPassPercent;
        return (float)(sanitizedPercent / 100);
    }

    private static float SanitizeReturnOffset(
        double percent,
        double defaultPercent)
    {
        var sanitizedPercent = double.IsFinite(percent)
            ? Math.Clamp(
                percent,
                MinimumReturnOffsetPercent,
                MaximumReturnOffsetPercent)
            : defaultPercent;
        return (float)(sanitizedPercent / 100);
    }

    private static float SanitizeProgress(float progress) =>
        float.IsFinite(progress) ? Math.Clamp(progress, 0, 1) : 0;

    private static Vector2 ToUnit(Vector2 percent) =>
        new(ToUnit(percent.X), ToUnit(percent.Y));

    private static float ToUnit(float percent) =>
        float.IsFinite(percent)
            ? Math.Clamp(percent, 0, 100) / 100
            : 0;

    private static Vector2 Offset(Vector2 value, Vector2 offset) =>
        ClampGeneratedPath(value + offset);

    private static Vector2 ClampGeneratedPath(Vector2 value) =>
        new(ClampGeneratedPath(value.X), ClampGeneratedPath(value.Y));

    private static float ClampGeneratedPath(float value) =>
        float.IsFinite(value)
            ? Math.Clamp(
                value,
                MinimumGeneratedPathCoordinate,
                MaximumGeneratedPathCoordinate)
            : 0;

    private static Vector2 ClampUnit(Vector2 value) =>
        new(ClampUnit(value.X), ClampUnit(value.Y));

    private static float ClampUnit(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;

    private readonly record struct CircularArc(
        Vector2 Center,
        float Radius,
        double StartAngle,
        double FirstSweep,
        double SecondSweep,
        WipeMaskGeometry Geometry);

    private readonly record struct WipeSimplePathPreset(
        float Size,
        float Strength,
        float AspectRatio,
        float RotationRadians);
}

/// <summary>
/// 標準モードの評価結果です。<see cref="CreateSnapshot"/> は既存の複数Stroke入力と同じ経路で使用できます。
/// </summary>
internal sealed class WipeSimplePathGenerationResult
{
    public WipeSimplePathGenerationResult(
        int frame,
        int itemLength,
        WipePathSample[] samples,
        WipePathStyle style,
        string signature)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(signature);

        Frame = Math.Max(0, frame);
        ItemLength = Math.Max(0, itemLength);
        Samples = samples.ToArray();
        Style = style.Sanitize();
        Signature = signature;
    }

    public int Frame { get; }

    public int ItemLength { get; }

    public WipePathSample[] Samples { get; }

    public WipePathStyle Style { get; }

    /// <summary>
    /// 生成に使った全入力を固定書式で表す、キャッシュ無効化用の署名です。
    /// </summary>
    public string Signature { get; }

    /// <summary>
    /// 既存の複数Stroke入力と同じキャッシュ規則で使えるスナップショットへ変換します。
    /// </summary>
    public WipePathSnapshot CreateSnapshot(int framesPerSecond) =>
        CreateSnapshot(framesPerSecond, Style);

    /// <summary>
    /// 生成した軌跡へ指定の描画スタイルを適用してスナップショットへ変換します。
    /// </summary>
    public WipePathSnapshot CreateSnapshot(
        int framesPerSecond,
        WipePathStyle style) =>
        new(
            Frame,
            ItemLength,
            Math.Max(1, framesPerSecond),
            Samples,
            style,
            WipePathInputMode.StrokeCollection,
            Signature);
}
