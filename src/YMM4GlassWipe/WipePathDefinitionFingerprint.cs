// SPDX-License-Identifier: MPL-2.0

using System.Globalization;
using YukkuriMovieMaker.Commons;

namespace YMM4GlassWipe;

/// <summary>
/// 現在フレームを含まない軌跡定義の構造Fingerprintです。
/// Animationの過去キーフレーム編集も検出するため、毎更新で構造全体を評価します。
/// </summary>
internal readonly record struct WipePathDefinitionFingerprint(ulong Value)
{
    private const int SchemaVersion = 20;
    private const ulong OffsetBasis = 14695981039346656037;
    private const ulong Prime = 1099511628211;

    public static WipePathDefinitionFingerprint Create(
        GlassWipeVideoEffect item,
        WipePathTimingWindow timingWindow,
        int framesPerSecond,
        WipePathStyle style,
        WipeMaskGeometry geometry,
        string pathDataSignature,
        int sourceItemLength)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(pathDataSignature);

        var builder = new Builder();
        builder.Add(SchemaVersion);
        builder.Add(framesPerSecond);
        builder.Add(sourceItemLength);
        builder.Add(timingWindow.StartFrame);
        builder.Add(timingWindow.CompletionFrame);
        builder.Add(timingWindow.IsImmediate);
        builder.Add((int)item.EditingMode);
        builder.Add((int)item.PathInputMode);
        builder.Add((int)item.SimplePattern);
        builder.Add((int)item.SimpleInterpolation);
        builder.Add(item.SimpleStartX);
        builder.Add(item.SimpleStartY);
        builder.Add(item.SimpleControlX);
        builder.Add(item.SimpleControlY);
        builder.Add(item.SimpleEndX);
        builder.Add(item.SimpleEndY);
        builder.Add(WipeSimplePathGenerator.ToPassCount(item.SimpleRoundTrips));
        builder.Add(item.SimpleWipeAmountPerPass);
        builder.Add(item.SimpleReturnOffsetX);
        builder.Add(item.SimpleReturnOffset);
        builder.Add(style);
        builder.Add(geometry.ScaleX);
        builder.Add(geometry.ScaleY);
        builder.Add(geometry.PixelWidth);
        builder.Add(geometry.PixelHeight);
        builder.Add(item.UserBrushPixelWidth);
        builder.Add(item.UserBrushPixelHeight);
        builder.Add(pathDataSignature);

        builder.AddAnimation(item.SimpleProgress);
        builder.AddAnimation(item.BrushX);
        builder.AddAnimation(item.BrushY);
        builder.AddAnimation(item.Contact);
        builder.AddAnimation(item.CircleDiameterPixels);
        builder.AddAnimation(item.EllipseWidthPixels);
        builder.AddAnimation(item.EllipseHeightPixels);
        builder.AddAnimation(item.RectangleWidthPixels);
        builder.AddAnimation(item.RectangleHeightPixels);
        builder.AddAnimation(item.PngBrushSizeScale);
        builder.AddAnimation(item.SimpleGeneratedBrushRotation);
        builder.AddAnimation(item.BrushRotation);
        builder.AddAnimation(item.WipeStrength);
        return new WipePathDefinitionFingerprint(builder.Value);
    }

    private sealed class Builder
    {
        private ulong _hash = OffsetBasis;

        public ulong Value => _hash;

        public void Add(bool value) => Add(value ? 1 : 0);

        public void Add(int value) => Add(unchecked((uint)value));

        public void Add(long value) => Add(unchecked((ulong)value));

        public void Add(float value) => Add(BitConverter.SingleToUInt32Bits(value));

        public void Add(double value) => Add(unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));

        public void Add(Guid value)
        {
            Span<byte> bytes = stackalloc byte[16];
            value.TryWriteBytes(bytes);
            foreach (var item in bytes)
            {
                AddByte(item);
            }
        }

        public void Add(string value)
        {
            Add(value.Length);
            foreach (var character in value)
            {
                Add(character);
            }
        }

        public void Add(WipePathStyle style)
        {
            style = style.Sanitize();
            Add(style.Smoothing);
            Add(style.Jitter);
            Add(style.Seed);
            Add(style.RotationFollow);
            Add(style.Softness);
            Add((int)style.Quality);
            Add((int)style.AccumulationMode);
            Add((int)style.BrushShape);
            Add(style.InitialTangentX);
            Add(style.InitialTangentY);
            Add(style.BrushMirror);
            Add(style.UserBrushId);
            Add(style.UserBrushRevision);
            Add(style.ContinuousWipe);
            Add(style.UserBrushPixelWidth);
            Add(style.UserBrushPixelHeight);
        }

        public void AddAnimation(Animation animation)
        {
            ArgumentNullException.ThrowIfNull(animation);
            var keyFrames = animation.KeyFrames;
            var values = animation.Values;
            var bezier = animation.Bezier;
            Add(WipeAnimationDefinitionFingerprint.Create(
                (int)animation.AnimationType,
                Canonical(animation.Span),
                Canonical(animation.Length),
                Canonical(animation.Loop),
                keyFrames?.Frames.Select(Canonical).ToArray() ?? [],
                values?.Select(value => Canonical(value.Value)).ToArray() ?? [],
                bezier?.IsQuadratic,
                bezier?.Points.Select(point =>
                    new WipeAnimationBezierFingerprintPoint(
                        point.Point.X,
                        point.Point.Y,
                        point.ControlPoint1.X,
                        point.ControlPoint1.Y,
                        point.ControlPoint2.X,
                        point.ControlPoint2.Y)).ToArray() ?? []).Value);
        }

        private static string Canonical<T>(T value) =>
            Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

        private void Add(uint value)
        {
            for (var shift = 0; shift < 32; shift += 8)
            {
                AddByte((byte)(value >> shift));
            }
        }

        private void Add(ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8)
            {
                AddByte((byte)(value >> shift));
            }
        }

        private void AddByte(byte value)
        {
            _hash ^= value;
            _hash *= Prime;
        }
    }
}

internal readonly record struct WipeAnimationBezierFingerprintPoint(
    float X,
    float Y,
    float Control1X,
    float Control1Y,
    float Control2X,
    float Control2Y);

internal readonly record struct WipeAnimationDefinitionFingerprint(ulong Value)
{
    private const ulong OffsetBasis = 14695981039346656037;
    private const ulong Prime = 1099511628211;

    public static WipeAnimationDefinitionFingerprint Create(
        int animationType,
        string span,
        string length,
        string loop,
        IReadOnlyList<string> frames,
        IReadOnlyList<string> values,
        bool? isQuadratic,
        IReadOnlyList<WipeAnimationBezierFingerprintPoint> bezierPoints)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(length);
        ArgumentNullException.ThrowIfNull(loop);
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(bezierPoints);

        var builder = new Builder();
        builder.Add(animationType);
        builder.Add(span);
        builder.Add(length);
        builder.Add(loop);
        builder.Add("frames");
        foreach (var frame in frames)
        {
            builder.Add(frame);
        }

        builder.Add("values");
        foreach (var value in values)
        {
            builder.Add(value);
        }

        if (!isQuadratic.HasValue)
        {
            builder.Add("bezier-null");
            return new WipeAnimationDefinitionFingerprint(builder.Value);
        }

        builder.Add(isQuadratic.Value);
        builder.Add("bezier-points");
        foreach (var point in bezierPoints)
        {
            builder.Add(point.X);
            builder.Add(point.Y);
            builder.Add(point.Control1X);
            builder.Add(point.Control1Y);
            builder.Add(point.Control2X);
            builder.Add(point.Control2Y);
        }

        return new WipeAnimationDefinitionFingerprint(builder.Value);
    }

    private sealed class Builder
    {
        private ulong _hash = OffsetBasis;

        public ulong Value => _hash;

        public void Add(bool value) => Add(value ? 1 : 0);

        public void Add(int value) => Add(unchecked((uint)value));

        public void Add(float value) => Add(BitConverter.SingleToUInt32Bits(value));

        public void Add(string value)
        {
            Add(value.Length);
            foreach (var character in value)
            {
                Add(character);
            }
        }

        private void Add(uint value)
        {
            for (var shift = 0; shift < 32; shift += 8)
            {
                AddByte((byte)(value >> shift));
            }
        }

        private void AddByte(byte value)
        {
            _hash ^= value;
            _hash *= Prime;
        }
    }
}
