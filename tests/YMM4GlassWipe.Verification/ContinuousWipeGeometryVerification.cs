// SPDX-License-Identifier: MPL-2.0

using System.Collections;
using System.Numerics;
using YMM4GlassWipe;

namespace YMM4GlassWipe.Verification;

internal static class ContinuousWipeGeometryVerification
{
    private static readonly WipeMaskGeometry Geometry = WipeMaskGeometry.Create(1024, 1024);

    public static void VerifyInvalidCoordinatesBreakContact()
    {
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            foreach (var invalidSample in new[] { Sample(1, invalid, .5f), Sample(1, .5f, invalid), Sample(1, .5f, .5f, contact: invalid) })
            {
                var samples = new[] { Sample(0, .1f, .5f), invalidSample, Sample(2, .9f, .5f) };
                var stamps = WipeBrushStampGenerator.Generate(samples, 0, Geometry, Style(GlassWipeBrushShape.Hand, true));
                True(stamps.Count == 2, "不正な座標または接触率をまたいで描画してはいけません。");
                True(stamps.All(s => float.IsFinite(s.Center.X) && float.IsFinite(s.Center.Y)), "非有限座標を描画へ渡してはいけません。");
            }
        }
    }
    public static void Run()
    {
        VerifyLegacyCompatibilityAndShapeIsolation();
        VerifyShortestAngleAndRotationFollow();
        VerifyContinuousCornerMovement();
        VerifyPassAndContactBoundaries();
        VerifyStreamingAndBatchBoundary();
        VerifyPartialRebuildAndRandomSeek();
    }

    public static void VerifyLegacyCompatibilityAndShapeIsolation()
    {
        var samples = ReferenceSamples();
        var legacy = Style(GlassWipeBrushShape.Hand, false, 0.8f);
        var omitted = new WipePathStyle(0, 0, 19, RotationFollow: 0.8f,
            Quality: GlassWipeQuality.High, BrushShape: GlassWipeBrushShape.Hand);
        var expected = WipeBrushStampGenerator.Generate(samples, 0, Geometry, legacy);
        Equal(26, expected.Count, "既存の手形参照軌跡のスタンプ数");
        EqualStamps(expected, WipeBrushStampGenerator.Generate(samples, 0, Geometry, omitted),
            "連続拭きの省略値は既存手形列と完全一致する必要があります。");

        foreach (var shape in new[] { GlassWipeBrushShape.Hand, GlassWipeBrushShape.ShoePrint, GlassWipeBrushShape.UserImage })
        {
            var off = Style(shape, false, 0.8f);
            EqualStamps(WipeBrushStampGenerator.Generate(samples, 0, Geometry, off),
                WipeBrushStampGenerator.Generate(samples, 0, Geometry, off with { ContinuousWipe = false }),
                $"{shape} の連続拭きOFFは既存列と完全一致する必要があります。");
        }

        foreach (var shape in new[]
                 { GlassWipeBrushShape.Ellipse, GlassWipeBrushShape.Rectangle })
        {
            var off = Style(shape, false, 0.8f);
            var requested = off with { ContinuousWipe = true };
            False(requested.Sanitize().ContinuousWipe, $"{shape} は連続拭きの対象外です。");
            EqualStamps(WipeBrushStampGenerator.Generate(samples, 0, Geometry, off),
                WipeBrushStampGenerator.Generate(samples, 0, Geometry, requested),
                $"{shape} は連続拭き要求時も既存列を維持する必要があります。");
        }

        var userBrushId = Guid.NewGuid();
        var userStyle = Style(GlassWipeBrushShape.UserImage, true, 0.8f) with
        {
            UserBrushId = userBrushId,
            UserBrushRevision = 2,
            UserBrushPixelWidth = 1254,
            UserBrushPixelHeight = 1254,
        };
        var userContinuous = WipeBrushStampGenerator.Generate(samples, 0, Geometry, userStyle);
        var userLegacy = WipeBrushStampGenerator.Generate(samples, 0, Geometry,
            userStyle with { ContinuousWipe = false });
        True(userContinuous.Count > userLegacy.Count,
            "ユーザー画像の連続拭きONは拭き跡を補間する必要があります。");
        True(userContinuous.All(stamp =>
                stamp.Shape == GlassWipeBrushShape.UserImage &&
                stamp.UserBrushId == userBrushId &&
                stamp.UserBrushRevision == 2 &&
                stamp.UserBrushPixelWidth == 1254 &&
                stamp.UserBrushPixelHeight == 1254),
            "補間したスタンプはユーザー画像の参照と元寸法を保持する必要があります。");
    }

    public static void VerifyShortestAngleAndRotationFollow()
    {
        var pureRotation = new[]
        {
            Sample(0, .5f, .5f, rotation: Degrees(179)),
            Sample(1, .5f, .5f, rotation: Degrees(-179)),
        };
        var pureStamps = WipeBrushStampGenerator.Generate(pureRotation, 0, Geometry,
            Style(GlassWipeBrushShape.Hand, true));
        True(pureStamps.Count > 2, "純回転も指先の移動に応じて補間する必要があります。");
        True(AngularTravel(pureStamps) < Degrees(2.1f), "179°/-179°は最短2°の回転経路を通る必要があります。");

        var direction1 = new Vector2(MathF.Cos(Degrees(179)), MathF.Sin(Degrees(179)));
        var direction2 = new Vector2(MathF.Cos(Degrees(-179)), MathF.Sin(Degrees(-179)));
        var startPoint = new Vector2(.7f, .5f);
        var middlePoint = startPoint + direction1 * .05f;
        var endPoint = middlePoint + direction2 * .05f;
        var followSamples = new[]
        {
            Sample(0, startPoint.X, startPoint.Y),
            Sample(1, middlePoint.X, middlePoint.Y),
            Sample(2, endPoint.X, endPoint.Y),
        };
        var followStyle = Style(GlassWipeBrushShape.Hand, true, .8f) with
        {
            InitialTangentX = direction1.X,
            InitialTangentY = direction1.Y,
        };
        var followStamps = WipeBrushStampGenerator.Generate(followSamples, 0, Geometry, followStyle);
        AssertAngularContinuity(followStamps, .12f, "回転追従80%の接線179°/-179°");
        var travel = AngularTravel(followStamps);
        True(travel > Degrees(1.5f) && travel < Degrees(1.7f),
            $"接線の2°変化へ追従80%を掛けた角度変化は1.6°です。実際={travel}");

        static float AngularTravel(IReadOnlyList<WipeBrushStamp> stamps)
        {
            var result = 0f;
            for (var index = 1; index < stamps.Count; index++)
            {
                var first = Axis(stamps[index - 1]);
                var last = Axis(stamps[index]);
                result += MathF.Abs(MathF.IEEERemainder(
                    MathF.Atan2(last.Y, last.X) - MathF.Atan2(first.Y, first.X), MathF.Tau));
            }
            return result;
        }
    }
    public static void VerifyContinuousCornerMovement()
    {
        var samples = new[]
        {
            Sample(0, 0.18f, 0.32f, size: 0.08f, aspectRatio: 0.5f, rotation: -0.7f),
            Sample(1, 0.82f, 0.68f, size: 0.32f, aspectRatio: 3.0f, rotation: 0.9f),
        };
        var continuous = WipeBrushStampGenerator.Generate(samples, 0, Geometry,
            Style(GlassWipeBrushShape.ShoePrint, true));
        var legacy = WipeBrushStampGenerator.Generate(samples, 0, Geometry,
            Style(GlassWipeBrushShape.ShoePrint, false));

        True(continuous.Count > legacy.Count,
            "位置・回転・サイズ・横倍率が変わる靴跡は従来より細かく分割する必要があります。");
        Equal(samples[0].X * WipeMaskRenderer.MaskSize, continuous[0].Center.X, "連続拭きの始点");
        Equal(samples[^1].X * WipeMaskRenderer.MaskSize, continuous[^1].Center.X, "連続拭きの終点");
        for (var index = 1; index < continuous.Count; index++)
        {
            var movement = MaximumCornerMovement(continuous[index - 1], continuous[index]);
            True(movement <= 1.01f,
                $"高品質の隣接スタンプ四隅の移動量は1 mask pixel以下である必要があります。 Index={index}, Movement={movement}");
        }
    }

    public static void VerifyPassAndContactBoundaries()
    {
        var result = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.OffsetRoundTrips, GlassWipePathInterpolation.CircularArc,
            new Vector2(15, 45), new Vector2(50, 20), new Vector2(85, 65), 2, 487, 487,
            returnOffsetPercent: 20, wipeAmountPerPassPercent: 70, maskGeometry: Geometry, returnOffsetXPercent: 8);
        var style = result.Style with
        {
            RotationFollow = 1,
            BrushShape = GlassWipeBrushShape.Hand,
            ContinuousWipe = true,
        };
        var stamps = WipeBrushStampGenerator.Generate(result.Samples, 0, Geometry, style);
        var outward = stamps.Where(stamp => stamp.AccumulationGroup == 0).Last();
        var returning = stamps.Where(stamp => stamp.AccumulationGroup == 1).First();
        True(Vector2.Dot(Axis(outward), Axis(returning)) > 0,
            "487フレーム・2往復の折返しで手形を上下反転してはいけません。");

        var contact = new[]
        {
            Sample(0, 0.10f, 0.50f), Sample(1, 0.30f, 0.50f),
            Sample(2, 0.75f, 0.50f, contact: 0), Sample(3, 0.90f, 0.50f),
        };
        var contactStamps = WipeBrushStampGenerator.Generate(contact, 0, Geometry,
            Style(GlassWipeBrushShape.Hand, true, 1));
        False(contactStamps.Any(stamp => stamp.Center.X > 0.31f * WipeMaskRenderer.MaskSize &&
                                         stamp.Center.X < 0.89f * WipeMaskRenderer.MaskSize),
            "接触率0をまたいでスタンプを生成してはいけません。");
        Equal(0.90f * WipeMaskRenderer.MaskSize, contactStamps.Last().Center.X,
            "接触再開は新しいストロークの始点から始める必要があります。");
    }

    public static void VerifyStreamingAndBatchBoundary()
    {
        var style = Style(GlassWipeBrushShape.ShoePrint, true, 0.85f);
        var samples = Enumerable.Range(0, 33).Select(index => Sample(index, 0.50f, 0.50f,
            size: 0.20f, aspectRatio: 1.4f, rotation: index % 2 == 0 ? 0 : MathF.PI / 2)).ToArray();
        var expected = WipeBrushStampGenerator.Generate(samples, 0, Geometry, style);
        True(expected.Count > WipeBrushStampGenerator.StampBatchCapacity,
            "連続回転は4,096件のStampバッチ境界を越える検証入力である必要があります。");

        var collector = new BatchCollector(WipeBrushStampGenerator.StampBatchCapacity);
        var stream = new WipeBrushStampGenerator.StreamState(0);
        foreach (var sample in samples) stream.Append(sample, Geometry, style, collector);
        collector.Flush();
        EqualStamps(expected, collector.Items,
            "連続拭きは4,096件バッチ境界をまたいでもGenerateとStreamState.Appendで一致する必要があります。");
        True(collector.MaximumBufferedCount <= WipeBrushStampGenerator.StampBatchCapacity,
            "連続拭きでもStampを4,096件より多く一時保持してはいけません。");
    }

    public static void VerifyPartialRebuildAndRandomSeek()
    {
        var style = Style(GlassWipeBrushShape.ShoePrint, true, 0.85f);
        var samples = Enumerable.Range(0, 320).Select(index =>
        {
            var amount = index / 319f;
            return Sample(index, 0.10f + amount * 0.80f,
                0.50f + MathF.Sin(amount * MathF.PI * 5) * 0.20f,
                contact: index is >= 96 and <= 100 ? 0 : 1,
                size: 0.08f + amount * 0.20f, aspectRatio: 0.6f + amount * 2.4f,
                rotation: -1.4f + amount * 2.8f, accumulationGroup: index < 160 ? 0 : 1);
        }).ToArray();

        var random = new Random(20260903);
        for (var attempt = 0; attempt < 64; attempt++)
        {
            var length = random.Next(2, samples.Length + 1);
            var start = random.Next(1, length);
            var prefix = samples[..length];
            EqualStamps(WipeBrushStampGenerator.Generate(prefix, start, Geometry, style),
                AppendFromIndex(prefix, start, style),
                $"部分再構築 {attempt} はfirstSampleIndexとStreamStateで一致する必要があります。");
            var rebuilt = WipeBrushStampGenerator.Generate(prefix, 0, Geometry, style);
            EqualStamps(rebuilt, WipeBrushStampGenerator.Generate(prefix, 0, Geometry, style),
                $"ランダムシーク再構築 {attempt} は決定的である必要があります。");
        }
    }

    private static IReadOnlyList<WipeBrushStamp> AppendFromIndex(
        IReadOnlyList<WipePathSample> samples, int firstSampleIndex, WipePathStyle style)
    {
        var output = new List<WipeBrushStamp>();
        var discarded = new DiscardCollector();
        var stream = new WipeBrushStampGenerator.StreamState(0);
        for (var index = 0; index < samples.Count; index++)
            stream.Append(samples[index], Geometry, style, index < firstSampleIndex ? discarded : output);
        return output;
    }

    private static WipePathStyle Style(GlassWipeBrushShape shape, bool continuous, float rotationFollow = 0) =>
        new(0, 0, 19, RotationFollow: rotationFollow, Quality: GlassWipeQuality.High,
            BrushShape: shape, ContinuousWipe: continuous);

    private static WipePathSample[] ReferenceSamples() =>
    [
        Sample(0, 0.15f, 0.25f, size: 0.18f, rotation: -0.8f),
        Sample(1, 0.72f, 0.64f, size: 0.27f, aspectRatio: 1.8f, rotation: 1.1f),
    ];

    private static WipePathSample Sample(int frame, float x, float y, float contact = 1, float size = 0.20f,
        float strength = 1, float aspectRatio = 1, float rotation = 0, int accumulationGroup = 0) =>
        new(frame, x, y, contact, size, strength, aspectRatio, rotation, accumulationGroup);

    private static float Degrees(float degrees) => degrees * MathF.PI / 180;

    private static float MaximumCornerMovement(WipeBrushStamp first, WipeBrushStamp last)
    {
        var maximum = 0f;
        foreach (var x in new[] { -1f, 1f })
            foreach (var y in new[] { -1f, 1f })
            {
                var firstCorner = Vector2.Transform(first.Center + new Vector2(x * first.RadiusX, y * first.RadiusY), first.Transform);
                var lastCorner = Vector2.Transform(last.Center + new Vector2(x * last.RadiusX, y * last.RadiusY), last.Transform);
                maximum = MathF.Max(maximum, Vector2.Distance(firstCorner, lastCorner));
            }
        return maximum;
    }

    private static Vector2 Axis(WipeBrushStamp stamp)
    {
        var axis = new Vector2(stamp.Transform.M11, stamp.Transform.M12);
        return axis.LengthSquared() > 0 ? Vector2.Normalize(axis) : Vector2.Zero;
    }

    private static void AssertAngularContinuity(IReadOnlyList<WipeBrushStamp> stamps, float maximumStep, string name)
    {
        for (var index = 1; index < stamps.Count; index++)
        {
            var before = MathF.Atan2(stamps[index - 1].Transform.M12, stamps[index - 1].Transform.M11);
            var after = MathF.Atan2(stamps[index].Transform.M12, stamps[index].Transform.M11);
            var delta = MathF.Abs(MathF.IEEERemainder(after - before, MathF.Tau));
            True(delta <= maximumStep, $"{name} の隣接スタンプ角差が上限を超えています。 Delta={delta}");
        }
    }

    private static void EqualStamps(IReadOnlyList<WipeBrushStamp> expected, IReadOnlyList<WipeBrushStamp> actual, string message)
    {
        if (expected.Count != actual.Count) throw new InvalidOperationException($"{message} Expected={expected.Count}, Actual={actual.Count}");
        for (var index = 0; index < expected.Count; index++)
            if (expected[index] != actual[index]) throw new InvalidOperationException($"{message} Index={index}");
    }

    private static void Equal(int expected, int actual, string message)
    {
        if (expected != actual) throw new InvalidOperationException($"{message} Expected={expected}, Actual={actual}");
    }

    private static void Equal(float expected, float actual, string message)
    {
        if (MathF.Abs(expected - actual) > 0.0001f) throw new InvalidOperationException($"{message} Expected={expected}, Actual={actual}");
    }

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void False(bool condition, string message) => True(!condition, message);

    private sealed class DiscardCollector : ICollection<WipeBrushStamp>
    {
        public int Count => 0;
        public bool IsReadOnly => false;
        public void Add(WipeBrushStamp item) { }
        public void Clear() { }
        public bool Contains(WipeBrushStamp item) => false;
        public void CopyTo(WipeBrushStamp[] array, int arrayIndex) { }
        public IEnumerator<WipeBrushStamp> GetEnumerator() => Enumerable.Empty<WipeBrushStamp>().GetEnumerator();
        public bool Remove(WipeBrushStamp item) => false;
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class BatchCollector : ICollection<WipeBrushStamp>
    {
        private readonly int _capacity;
        private readonly List<WipeBrushStamp> _buffer;
        private readonly List<WipeBrushStamp> _items = [];

        public BatchCollector(int capacity)
        {
            _capacity = capacity;
            _buffer = new List<WipeBrushStamp>(capacity);
        }

        public IReadOnlyList<WipeBrushStamp> Items => _items;
        public int MaximumBufferedCount { get; private set; }
        public int Count => _items.Count + _buffer.Count;
        public bool IsReadOnly => false;

        public void Add(WipeBrushStamp item)
        {
            _buffer.Add(item);
            MaximumBufferedCount = Math.Max(MaximumBufferedCount, _buffer.Count);
            if (_buffer.Count >= _capacity) Flush();
        }

        public void Flush()
        {
            _items.AddRange(_buffer);
            _buffer.Clear();
        }

        public void Clear()
        {
            _items.Clear();
            _buffer.Clear();
            MaximumBufferedCount = 0;
        }

        public bool Contains(WipeBrushStamp item) => _items.Contains(item) || _buffer.Contains(item);

        public void CopyTo(WipeBrushStamp[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
            _buffer.CopyTo(array, arrayIndex + _items.Count);
        }

        public IEnumerator<WipeBrushStamp> GetEnumerator() => _items.Concat(_buffer).GetEnumerator();

        public bool Remove(WipeBrushStamp item) => _buffer.Remove(item) || _items.Remove(item);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
