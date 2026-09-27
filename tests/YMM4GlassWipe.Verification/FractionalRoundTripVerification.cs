// SPDX-License-Identifier: MPL-2.0

using System.Drawing;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using YMM4GlassWipe;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Player.Video;
using ItemProperty = YukkuriMovieMaker.Commons.ItemProperty;
using PropertiesCache = YukkuriMovieMaker.Commons.PropertiesCache;
using YmmJson = YukkuriMovieMaker.Json.Json;

internal static class FractionalRoundTripVerification
{
    public static void VerifyUiPersistenceAndNormalization()
    {
        var property = typeof(GlassWipeVideoEffect).GetProperty(
            nameof(GlassWipeVideoEffect.SimpleRoundTrips)) ??
            throw new InvalidOperationException("往復回数プロパティが見つかりません。");
        var attribute = property.GetCustomAttribute<RoundTripCountEditorAttribute>() ??
            throw new InvalidOperationException("往復回数へ0.5刻みのEditorが設定されていません。");
        (string Format, string Unit, double Delta, double Minimum, double Maximum)? settings = null;
        Exception? uiException = null;
        var thread = new Thread(() =>
        {
            try
            {
                var slider = attribute.Create() as TextBoxSlider ??
                    throw new InvalidOperationException(
                        "往復回数EditorがTextBoxSliderではありません。");
                settings = (
                    slider.StringFormat,
                    slider.Unit,
                    slider.Delta,
                    slider.DefaultMin,
                    slider.DefaultMax);
            }
            catch (Exception exception)
            {
                uiException = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (uiException is not null)
        {
            throw new InvalidOperationException(
                "往復回数EditorをSTAで生成できません。",
                uiException);
        }

        var actualSettings = settings ??
            throw new InvalidOperationException("往復回数Editorの設定を取得できません。");
        Equal("F1", actualSettings.Format, "往復回数は小数第1位まで表示する必要があります。");
        Equal("回", actualSettings.Unit, "往復回数の単位が不正です。");
        Equal(0.5, actualSettings.Delta, "往復回数は0.5回単位で操作できる必要があります。");
        Equal(1d, actualSettings.Minimum, "往復回数のUI下限が不正です。");
        Equal(5d, actualSettings.Maximum, "往復回数のUI上限が不正です。");

        var effect = new GlassWipeVideoEffect();
        foreach (var (input, expected) in new[]
                 {
                     (0d, 1d),
                     (1d, 1d),
                     (1.2d, 1d),
                     (1.3d, 1.5d),
                     (1.5d, 1.5d),
                     (4.8d, 5d),
                     (6d, 5d),
                     (double.NaN, 2d),
                     (double.PositiveInfinity, 2d),
                 })
        {
            effect.SimpleRoundTrips = input;
            Equal(expected, effect.SimpleRoundTrips, $"往復回数{input}の0.5刻み補正");
        }

        effect.SimpleRoundTrips = 1.5;
        var clone = YmmJson.GetClone(effect) ??
            throw new InvalidOperationException("1.5往復のEffectを保存復元できません。");
        Equal(1.5, clone.SimpleRoundTrips, "1.5往復を保存復元する必要があります。");
    }

    public static void VerifyUiEffectiveRangeAndModelBinding()
    {
        Exception? uiException = null;
        var thread = new Thread(() =>
        {
            try
            {
                var effect = new GlassWipeVideoEffect { SimpleRoundTrips = 1 };
                var property = typeof(GlassWipeVideoEffect).GetProperty(
                    nameof(GlassWipeVideoEffect.SimpleRoundTrips)) ??
                    throw new InvalidOperationException("往復回数プロパティが見つかりません。");
                var attribute = property.GetCustomAttribute<RoundTripCountEditorAttribute>() ??
                    throw new InvalidOperationException("往復回数Editorが設定されていません。");
                var slider = (TextBoxSlider)attribute.Create();
                var itemProperty = new ItemProperty(effect, effect, property, new PropertiesCache());
                try
                {
                    attribute.SetBindings(slider, [itemProperty]);
                    Equal(1d, slider.Min, "往復回数の実入力下限");
                    Equal(5d, slider.Max, "往復回数の実入力上限");
                    Equal(1d, slider.CurrentMin, "往復回数のスライダー左端");
                    Equal(5d, slider.CurrentMax, "往復回数のスライダー右端");
                    Equal(GlassWipeDefaultSettings.SimpleRoundTrips, slider.DefaultValue,
                        "往復回数Editorのリセット既定値");
                    Equal(1d, slider.Value, "保存済み1回のUIへの反映");

                    // YMM4部品の入力補正を通し、表示設定だけでなくモデルへの伝播も確認します。
                    var setFromAutomation = typeof(TextBoxSlider).GetMethod(
                        "SetValueFromAutomation",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                        throw new InvalidOperationException("YMM4スライダーの入力処理が見つかりません。");
                    foreach (var expected in new[] { 1d, 1.5, 2, 2.5, 3, 3.5, 4, 4.5, 5,
                                 4.5, 4, 3.5, 3, 2.5, 2, 1.5, 1 })
                    {
                        setFromAutomation.Invoke(slider, [expected]);
                        Equal(expected, slider.Value, $"{expected}回のUI操作結果");
                        Equal(expected, effect.SimpleRoundTrips, $"{expected}回のモデルへの反映");
                        Equal(1d, slider.CurrentMin, $"{expected}回での左端");
                        Equal(5d, slider.CurrentMax, $"{expected}回での右端");
                    }

                    effect.SimpleRoundTrips = 4.5;
                    Equal(4.5, slider.Value, "モデル変更のUIへの反映");
                    setFromAutomation.Invoke(slider, [slider.DefaultValue]);
                    Equal(GlassWipeDefaultSettings.SimpleRoundTrips, effect.SimpleRoundTrips,
                        "既定値へ戻した場合のモデルへの反映");
                }
                finally
                {
                    attribute.ClearBindings(slider);
                }
            }
            catch (Exception exception)
            {
                uiException = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (uiException is not null)
        {
            throw new InvalidOperationException(
                $"往復回数のUI実入力検証に失敗しました: {uiException.Message}",
                uiException);
        }
    }

    public static void VerifyEndpointsAndAccumulation()
    {
        var start = new Vector2(20, 50);
        var control = new Vector2(45, 20);
        var end = new Vector2(70, 50);
        const double offsetXPercent = 2;
        const double offsetYPercent = 3;
        var offset = new Vector2(
            (float)(offsetXPercent / 100),
            (float)(offsetYPercent / 100));
        var geometry = WipeMaskGeometry.Create(1920, 1080);

        foreach (var (pattern, interpolation) in EnumerateRoundTripCases())
        {
            for (var passCount = 2; passCount <= 10; passCount++)
            {
                var roundTrips = passCount / 2d;
                var itemLength = passCount * 40;
                var result = WipeSimplePathGenerator.Generate(
                    pattern,
                    interpolation,
                    start,
                    control,
                    end,
                    roundTrips,
                    itemLength,
                    itemLength,
                    returnOffsetPercent: offsetYPercent,
                    wipeAmountPerPassPercent: 40,
                    maskGeometry: geometry,
                    returnOffsetXPercent: offsetXPercent);

                True(
                    result.Signature.StartsWith("simple:v9|", StringComparison.Ordinal) &&
                    result.Signature.Contains($"passCount={passCount}|", StringComparison.Ordinal),
                    $"{pattern}/{interpolation}/{roundTrips}回の署名へ片道数を含める必要があります。");

                for (var nodeIndex = 0; nodeIndex <= passCount; nodeIndex++)
                {
                    var frame = nodeIndex * 40;
                    var expected = ExpectedNode(
                        pattern,
                        start / 100,
                        end / 100,
                        offset,
                        nodeIndex);
                    VectorEqual(
                        expected,
                        GetPosition(result.Samples[frame]),
                        $"{pattern}/{interpolation}/{roundTrips}回の片道境界{nodeIndex}");
                }

                Equal(0, result.Samples[0].AccumulationGroup,
                    $"{pattern}/{interpolation}/{roundTrips}回の最初の片道番号");
                Equal(passCount - 1, result.Samples[^1].AccumulationGroup,
                    $"{pattern}/{interpolation}/{roundTrips}回の最後の片道番号");
                True(
                    result.Samples.Zip(result.Samples.Skip(1))
                        .All(pair =>
                            pair.First.AccumulationGroup <= pair.Second.AccumulationGroup),
                    $"{pattern}/{interpolation}/{roundTrips}回の片道番号は単調増加する必要があります。");
            }
        }

        var canonical = GenerateShortRoundTrip(1.5);
        var roundedDown = GenerateShortRoundTrip(1.24);
        var one = GenerateShortRoundTrip(1);
        var roundedUp = GenerateShortRoundTrip(1.26);
        True(roundedDown.Samples.SequenceEqual(one.Samples),
            "1.24往復は1.0往復と同じ軌跡へ補正する必要があります。");
        Equal(roundedDown.Signature, one.Signature,
            "同じ片道数へ補正された往復回数の署名は一致する必要があります。");
        True(roundedUp.Samples.SequenceEqual(canonical.Samples),
            "1.26往復は1.5往復と同じ軌跡へ補正する必要があります。");
        Equal(roundedUp.Signature, canonical.Signature,
            "同じ1.5往復へ補正された署名は一致する必要があります。");
    }

    public static void VerifySnapshotAndStreamConsistency()
    {
        var geometry = WipeMaskGeometry.Create(1280, 720);
        foreach (var (pattern, interpolation) in EnumerateRoundTripCases())
        {
            foreach (var roundTrips in new[] { 1.5, 2.5, 4.5 })
            {
                var effect = new GlassWipeVideoEffect
                {
                    EditingMode = GlassWipeEditingMode.Simple,
                    PathInputMode = WipePathInputMode.SimpleGenerated,
                    PathTimingMode = WipePathTimingMode.Percent,
                    PathStart = 0,
                    PathCompletion = 100,
                    SimplePattern = pattern,
                    SimpleInterpolation = interpolation,
                    SimpleRoundTrips = roundTrips,
                    SimpleReturnOffsetX = 2,
                    SimpleReturnOffset = 3,
                    // 比較する軌跡とブラシを固定し、新規テンプレートの変更から試験条件を分離する。
                    SimpleStartX = 15,
                    SimpleStartY = 50,
                    SimpleControlX = 50,
                    SimpleControlY = 30,
                    SimpleEndX = 75,
                    SimpleEndY = 30,
                    SimpleWipeAmountPerPass = 100,
                    SimpleGeneratedQuality = GlassWipeSimpleGeneratedQuality.Auto,
                    BrushShape = GlassWipeBrushShape.Hand,
                    BrushMirror = true,
                    ContinuousWipe = true,
                    SimpleGeneratedBrushRotationFollow = 100,
                };
                effect.PngBrushSizeScale.CopyFrom(new YukkuriMovieMaker.Commons.Animation(50, 25, 400));
                effect.SimpleGeneratedBrushRotation.CopyFrom(new YukkuriMovieMaker.Commons.Animation(-20, -180, 180));
                var progress = new YukkuriMovieMaker.Commons.Animation(0, 0, 100)
                {
                    AnimationType = YukkuriMovieMaker.Commons.AnimationType.直線移動,
                };
#pragma warning disable CS0618
                progress.From = 0;
                progress.To = 100;
#pragma warning restore CS0618
                effect.SimpleProgress.CopyFrom(progress);
                var description = CreateEffectDescription(120);
                var snapshot = WipePathSnapshot.Create(effect, description, geometry);
                var stream = WipePathStream.Create(effect, description, geometry);
                var streamed = new List<WipePathSample>();
                stream.EnumerateSamples(
                    0,
                    stream.Frame,
                    (laneId, sample) =>
                    {
                        Equal(0, laneId, "定型軌跡のStream laneは0である必要があります。");
                        streamed.Add(sample);
                    });

                True(
                    snapshot.Samples.SequenceEqual(streamed),
                    $"{pattern}/{interpolation}/{roundTrips}回のSnapshotとStreamが一致しません。");

                if (roundTrips == 1.5)
                {
                    foreach (var seekFrame in new[]
                             {
                                 120, 40, 0, 81, 39, 119, 41, 80, 1, 79,
                             })
                    {
                        var seekDescription = CreateEffectDescription(seekFrame);
                        var seekSnapshot = WipePathSnapshot.Create(
                            effect,
                            seekDescription,
                            geometry);
                        var seekStream = WipePathStream.Create(
                            effect,
                            seekDescription,
                            geometry);
                        var seekStreamed = new List<WipePathSample>();
                        seekStream.EnumerateSamples(
                            0,
                            seekStream.Frame,
                            (laneId, sample) =>
                            {
                                Equal(0, laneId,
                                    "定型軌跡の直接移動後もStream laneは0である必要があります。");
                                seekStreamed.Add(sample);
                            });
                        var expectedPrefix = snapshot.Samples
                            .Take(seekFrame + 1)
                            .ToArray();

                        True(
                            seekSnapshot.Samples.SequenceEqual(expectedPrefix),
                            $"{pattern}/{interpolation}/1.5回のフレーム{seekFrame}への直接移動結果が連続評価と一致しません。");
                        True(
                            seekSnapshot.Samples.SequenceEqual(seekStreamed),
                            $"{pattern}/{interpolation}/1.5回のフレーム{seekFrame}でSnapshotとStreamが一致しません。");
                    }
                }

                effect.SimpleRoundTrips = roundTrips + 0.5;
                var changed = WipePathStream.Create(effect, description, geometry);
                True(
                    stream.DefinitionFingerprint != changed.DefinitionFingerprint,
                    $"{pattern}/{interpolation}の片道数変更でFingerprintを更新する必要があります。");
            }
        }
    }

    private static IEnumerable<(GlassWipeSimplePattern Pattern, GlassWipePathInterpolation Interpolation)>
        EnumerateRoundTripCases()
    {
        yield return (
            GlassWipeSimplePattern.ShortRoundTrips,
            GlassWipePathInterpolation.Linear);
        foreach (var interpolation in new[]
                 {
                     GlassWipePathInterpolation.Linear,
                     GlassWipePathInterpolation.Smooth,
                     GlassWipePathInterpolation.CircularArc,
                 })
        {
            yield return (GlassWipeSimplePattern.ArcRoundTrips, interpolation);
            yield return (GlassWipeSimplePattern.OffsetRoundTrips, interpolation);
        }
    }

    private static WipeSimplePathGenerationResult GenerateShortRoundTrip(
        double roundTrips) =>
        WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.ShortRoundTrips,
            GlassWipePathInterpolation.Linear,
            new Vector2(20, 50),
            new Vector2(50, 20),
            new Vector2(80, 50),
            roundTrips,
            120,
            120);

    private static Vector2 ExpectedNode(
        GlassWipeSimplePattern pattern,
        Vector2 start,
        Vector2 end,
        Vector2 offset,
        int nodeIndex)
    {
        var destination = nodeIndex % 2 == 0 ? start : end;
        return pattern == GlassWipeSimplePattern.OffsetRoundTrips
            ? destination + offset * (nodeIndex / 2)
            : destination;
    }

    private static Vector2 GetPosition(WipePathSample sample) =>
        new(sample.X, sample.Y);

    private static EffectDescription CreateEffectDescription(int currentFrame)
    {
        const int framesPerSecond = 60;
        const int duration = 120;
        var timeline = new TimelineSourceDescription(
            new Size(1280, 720),
            new FrameTime(currentFrame, framesPerSecond),
            new FrameTime(duration, framesPerSecond),
            framesPerSecond,
            default,
            Guid.Empty,
            []);
        var item = new TimelineItemSourceDescription(
            timeline,
            currentFrame,
            duration,
            0);
        var draw = (DrawDescription)RuntimeHelpers.GetUninitializedObject(
            typeof(DrawDescription));
        return new EffectDescription(item, draw, 0, 1, 0, 1);
    }

    private static void VectorEqual(
        Vector2 expected,
        Vector2 actual,
        string message,
        float tolerance = 0.00001f)
    {
        NearlyEqual(expected.X, actual.X, message + " X", tolerance);
        NearlyEqual(expected.Y, actual.Y, message + " Y", tolerance);
    }

    private static void NearlyEqual(
        float expected,
        float actual,
        string message,
        float tolerance)
    {
        if (!float.IsFinite(actual) || MathF.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException(
                $"{message} 期待値: {expected}, 実際: {actual}");
        }
    }

    private static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message} 期待値: {expected}, 実際: {actual}");
        }
    }
}
