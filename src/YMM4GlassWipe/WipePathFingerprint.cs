// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

internal readonly record struct WipePathFingerprint(
    ulong Value,
    int SchemaVersion,
    int ItemLength,
    int FramesPerSecond,
    int SampleCount)
{
    private const ulong OffsetBasis = 14695981039346656037;
    private const ulong Prime = 1099511628211;

    public static WipePathFingerprint Create(
        int schemaVersion,
        int itemLength,
        int framesPerSecond,
        WipePathStyle style,
        WipePathInputMode inputMode,
        string pathDataSignature,
        IReadOnlyList<WipePathSample> samples)
    {
        ArgumentNullException.ThrowIfNull(pathDataSignature);
        ArgumentNullException.ThrowIfNull(samples);

        var hash = OffsetBasis;
        Add(ref hash, schemaVersion);
        Add(ref hash, itemLength);
        Add(ref hash, framesPerSecond);
        Add(ref hash, style.Smoothing);
        Add(ref hash, style.Jitter);
        Add(ref hash, style.Seed);
        Add(ref hash, style.RotationFollow);
        Add(ref hash, style.Softness);
        Add(ref hash, (int)style.Quality);
        Add(ref hash, (int)style.AccumulationMode);
        Add(ref hash, (int)style.BrushShape);
        Add(ref hash, style.InitialTangentX);
        Add(ref hash, style.InitialTangentY);
        Add(ref hash, style.BrushMirror ? 1 : 0);
        Add(ref hash, style.UserBrushId);
        Add(ref hash, style.UserBrushRevision);
        Add(ref hash, style.ContinuousWipe ? 1 : 0);
        Add(ref hash, style.UserBrushPixelWidth);
        Add(ref hash, style.UserBrushPixelHeight);
        Add(ref hash, (int)inputMode);
        Add(ref hash, pathDataSignature);
        Add(ref hash, samples.Count);

        foreach (var sample in samples)
        {
            Add(ref hash, sample.Frame);
            Add(ref hash, sample.X);
            Add(ref hash, sample.Y);
            Add(ref hash, sample.Contact);
            Add(ref hash, sample.Size);
            Add(ref hash, sample.Strength);
            Add(ref hash, sample.AspectRatio);
            Add(ref hash, sample.RotationRadians);
            Add(ref hash, sample.AccumulationGroup);
            Add(ref hash, sample.BrushWidthPixels.HasValue ? 1 : 0);
            if (sample.BrushWidthPixels is { } brushWidthPixels)
            {
                Add(ref hash, brushWidthPixels);
            }

            Add(ref hash, sample.BrushHeightPixels.HasValue ? 1 : 0);
            if (sample.BrushHeightPixels is { } brushHeightPixels)
            {
                Add(ref hash, brushHeightPixels);
            }
        }

        return new WipePathFingerprint(
            hash,
            schemaVersion,
            itemLength,
            framesPerSecond,
            samples.Count);
    }

    private static void Add(ref ulong hash, int value) =>
        Add(ref hash, unchecked((uint)value));

    private static void Add(ref ulong hash, float value) =>
        Add(ref hash, BitConverter.SingleToUInt32Bits(value));

    private static void Add(ref ulong hash, string value)
    {
        Add(ref hash, value.Length);
        foreach (var character in value)
        {
            Add(ref hash, (int)character);
        }
    }

    private static void Add(ref ulong hash, Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes);
        foreach (var item in bytes)
        {
            hash ^= item;
            hash *= Prime;
        }
    }

    private static void Add(ref ulong hash, long value)
    {
        var unsigned = unchecked((ulong)value);
        for (var shift = 0; shift < 64; shift += 8)
        {
            hash ^= (byte)(unsigned >> shift);
            hash *= Prime;
        }
    }

    private static void Add(ref ulong hash, uint value)
    {
        for (var shift = 0; shift < 32; shift += 8)
        {
            hash ^= (byte)(value >> shift);
            hash *= Prime;
        }
    }
}
