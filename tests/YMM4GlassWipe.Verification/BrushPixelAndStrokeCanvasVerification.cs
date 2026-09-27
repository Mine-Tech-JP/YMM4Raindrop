// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using System.Drawing;
using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YMM4GlassWipe.Verification;

internal static class BrushPixelAndStrokeCanvasVerification
{
    public static void VerifyBrushPixelDimensions()
    {
        var effect = new GlassWipeVideoEffect();

        effect.BrushShape = GlassWipeBrushShape.Circle;
        Equal(
            new WipeBrushPixelDimensions(500, 500),
            WipeBrushPixelDimensions.Resolve(effect, 0, 100, 60),
            "円の既定直径");

        effect.BrushShape = GlassWipeBrushShape.Ellipse;
        Equal(
            new WipeBrushPixelDimensions(25, 50),
            WipeBrushPixelDimensions.Resolve(effect, 0, 100, 60),
            "楕円の既定寸法");

        effect.BrushShape = GlassWipeBrushShape.Rectangle;
        Equal(
            new WipeBrushPixelDimensions(100, 620),
            WipeBrushPixelDimensions.Resolve(effect, 0, 100, 60),
            "四角形の既定寸法");

        effect.BrushShape = GlassWipeBrushShape.Hand;
        effect.PngBrushSizeScale.CopyFrom(new Animation(100, 25, 400));
        var handMask = WipeBrushMaskRasterizer.CreateBinaryMask(
            GlassWipeBrushShape.Hand);
        Equal(
            new WipeBrushPixelDimensions(
                handMask.SourcePixelWidth,
                handMask.SourcePixelHeight),
            WipeBrushPixelDimensions.Resolve(effect, 0, 100, 60),
            "手形の100%は埋め込みPNGの元キャンバス寸法を使用する必要があります。");

        effect.PngBrushSizeScale.AnimationType = AnimationType.直線移動;
#pragma warning disable CS0618
        effect.PngBrushSizeScale.From = 50;
        effect.PngBrushSizeScale.To = 200;
#pragma warning restore CS0618
        Equal(
            new WipeBrushPixelDimensions(
                handMask.SourcePixelWidth * 0.5f,
                handMask.SourcePixelHeight * 0.5f),
            WipeBrushPixelDimensions.Resolve(effect, 0, 100, 60),
            "手形の50%倍率");

        effect.BrushShape = GlassWipeBrushShape.ShoePrint;
        var shoeMask = WipeBrushMaskRasterizer.CreateBinaryMask(
            GlassWipeBrushShape.ShoePrint);
        Equal(
            new WipeBrushPixelDimensions(
                shoeMask.SourcePixelWidth * 2f,
                shoeMask.SourcePixelHeight * 2f),
            WipeBrushPixelDimensions.Resolve(effect, 100, 100, 60),
            "靴跡の200%倍率");

        effect.BrushShape = GlassWipeBrushShape.UserImage;
        effect.UserBrushPixelWidth = 100;
        effect.UserBrushPixelHeight = 30;
        Equal(
            new WipeBrushPixelDimensions(125, 37.5f),
            WipeBrushPixelDimensions.Resolve(effect, 50, 100, 60),
            "ユーザー画像へAnimationの125%倍率を適用する必要があります。");

        effect.PngBrushSizeScale.CopyFrom(new Animation(
            100,
            25,
            400));
        Equal(
            new WipeBrushPixelDimensions(100, 30),
            WipeBrushPixelDimensions.Resolve(effect, 0, 100, 60),
            "ユーザー画像の100%は登録PNGの元キャンバス寸法を使用する必要があります。");

        effect.BrushShape = GlassWipeBrushShape.Circle;
        effect.CircleDiameterPixels.AnimationType = AnimationType.直線移動;
#pragma warning disable CS0618
        effect.CircleDiameterPixels.From = 10;
        effect.CircleDiameterPixels.To = 141;
#pragma warning restore CS0618
        var animated = WipeBrushPixelDimensions.Resolve(effect, 50, 100, 60);
        NearlyEqual(75.5f, animated.Width, "円直径Animationの中間値", 0.001f);
        NearlyEqual(75.5f, animated.Height, "円直径Animationの中間値", 0.001f);

        var geometry = WipeMaskGeometry.Create(1920, 1080);
        var circleStamp = GenerateSingleStamp(
            geometry,
            GlassWipeBrushShape.Circle,
            new WipeBrushPixelDimensions(50, 50));
        NearlyEqual(
            50,
            ToInputPixelWidth(circleStamp, geometry),
            "1920×1080上の円直径",
            0.001f);
        NearlyEqual(
            50,
            ToInputPixelHeight(circleStamp, geometry),
            "1920×1080上の円直径",
            0.001f);

        var rectangleStamp = GenerateSingleStamp(
            geometry,
            GlassWipeBrushShape.Rectangle,
            new WipeBrushPixelDimensions(100, 30));
        NearlyEqual(
            100,
            ToInputPixelWidth(rectangleStamp, geometry),
            "四角形の入力上の幅",
            0.001f);
        NearlyEqual(
            30,
            ToInputPixelHeight(rectangleStamp, geometry),
            "四角形の入力上の高さ",
            0.001f);

        var fourKGeometry = WipeMaskGeometry.Create(3840, 2160);
        var fourKStamp = GenerateSingleStamp(
            fourKGeometry,
            GlassWipeBrushShape.Ellipse,
            new WipeBrushPixelDimensions(25, 50));
        NearlyEqual(25, ToInputPixelWidth(fourKStamp, fourKGeometry),
            "4K入力でも指定幅を入力pxとして維持する必要があります。", 0.001f);
        NearlyEqual(50, ToInputPixelHeight(fourKStamp, fourKGeometry),
            "4K入力でも指定高さを入力pxとして維持する必要があります。", 0.001f);

        var zeroSamples = new[]
        {
            CreateSample(new WipeBrushPixelDimensions(0, 50)),
            CreateSample(new WipeBrushPixelDimensions(50, 0)),
        };
        foreach (var sample in zeroSamples)
        {
            Equal(
                0,
                WipeBrushStampGenerator.Generate(
                    [sample],
                    0,
                    geometry,
                    new WipePathStyle(
                        0,
                        0,
                        1,
                        BrushShape: GlassWipeBrushShape.Rectangle))
                    .Count,
                "幅または高さ0は非描画である必要があります。");
        }

        var firstFingerprint = new WipePathSnapshot(
            0,
            100,
            60,
            [CreateSample(new WipeBrushPixelDimensions(50, 50))])
            .Fingerprint;
        var secondFingerprint = new WipePathSnapshot(
            0,
            100,
            60,
            [CreateSample(new WipeBrushPixelDimensions(51, 50))])
            .Fingerprint;
        True(
            firstFingerprint != secondFingerprint,
            "ブラシ幅の変更をSnapshot Fingerprintへ含める必要があります。");

        True(
            GlassWipeSimpleVisibility.IsCircleBrushSizeVisible(
                GlassWipeBrushShape.Circle) &&
            !GlassWipeSimpleVisibility.IsCircleBrushSizeVisible(
                GlassWipeBrushShape.Ellipse) &&
            GlassWipeSimpleVisibility.IsEllipseBrushSizeVisible(
                GlassWipeBrushShape.Ellipse) &&
            !GlassWipeSimpleVisibility.IsEllipseBrushSizeVisible(
                GlassWipeBrushShape.UserImage) &&
            GlassWipeSimpleVisibility.IsRectangleBrushSizeVisible(
                GlassWipeBrushShape.Rectangle) &&
            !GlassWipeSimpleVisibility.IsRectangleBrushSizeVisible(
                GlassWipeBrushShape.Hand) &&
            GlassWipeSimpleVisibility.IsPngBrushSizeVisible(
                GlassWipeBrushShape.Hand) &&
            GlassWipeSimpleVisibility.IsPngBrushSizeVisible(
                GlassWipeBrushShape.ShoePrint) &&
            GlassWipeSimpleVisibility.IsPngBrushSizeVisible(
                GlassWipeBrushShape.UserImage) &&
            !GlassWipeSimpleVisibility.IsPngBrushSizeVisible(
                GlassWipeBrushShape.Circle),
            "生成形状にはピクセル寸法、PNG形状にはサイズ倍率を表示する必要があります。");

        VerifyUserBrushNativeDimensionsAcrossPaths();
    }

    private static void VerifyUserBrushNativeDimensionsAcrossPaths()
    {
        var brushId = Guid.NewGuid();
        var effect = new GlassWipeVideoEffect
        {
            EditingMode = GlassWipeEditingMode.Detailed,
            PathStart = 0,
            PathCompletion = 100,
            BrushShape = GlassWipeBrushShape.UserImage,
            UserBrushId = brushId,
            UserBrushRevision = 4,
            UserBrushPixelWidth = 100,
            UserBrushPixelHeight = 30,
            CustomPathData = DetailedWipePresetFactory
                .Create(DetailedWipeBuiltInPreset.Heart)
                .CustomPathData,
        };
        effect.PngBrushSizeScale.CopyFrom(new Animation(125, 25, 400));
        var entry = new UserBrushEntry
        {
            Id = brushId,
            Revision = 4,
            PixelWidth = 100,
            PixelHeight = 30,
        };
        var geometry = WipeMaskGeometry.Create(1000, 1000);
        var description = CreateEffectDescription(90);

        foreach (var pathInputMode in new[]
                 {
                     WipePathInputMode.SimpleGenerated,
                     WipePathInputMode.StrokeCollection,
                     WipePathInputMode.LegacyAnimation,
                 })
        {
            effect.PathInputMode = pathInputMode;
            var snapshot = WipePathSnapshot.Create(effect, description, geometry);
            var stream = WipePathStream.Create(effect, description, geometry);

            Equal(100, snapshot.Style.UserBrushPixelWidth,
                $"{pathInputMode} Snapshotの元PNG幅");
            Equal(30, snapshot.Style.UserBrushPixelHeight,
                $"{pathInputMode} Snapshotの元PNG高さ");
            Equal(100, stream.Style.UserBrushPixelWidth,
                $"{pathInputMode} Streamの元PNG幅");
            Equal(30, stream.Style.UserBrushPixelHeight,
                $"{pathInputMode} Streamの元PNG高さ");

            var snapshotSample = snapshot.Samples.First(sample => sample.IsContacting);
            NearlyEqual(125, snapshotSample.BrushWidthPixels ?? -1,
                $"{pathInputMode} Snapshotの125%描画幅", 0.001f);
            NearlyEqual(37.5f, snapshotSample.BrushHeightPixels ?? -1,
                $"{pathInputMode} Snapshotの125%描画高さ", 0.001f);
            var streamSample = Enumerate(stream)
                .Select(value => value.Sample)
                .First(sample => sample.IsContacting);
            NearlyEqual(125, streamSample.BrushWidthPixels ?? -1,
                $"{pathInputMode} Streamの125%描画幅", 0.001f);
            NearlyEqual(37.5f, streamSample.BrushHeightPixels ?? -1,
                $"{pathInputMode} Streamの125%描画高さ", 0.001f);

            var stamp = WipeBrushStampGenerator.Generate(
                    snapshot.Samples,
                    snapshot.Frame,
                    geometry,
                    snapshot.Style)
                .First();
            Equal(100, stamp.UserBrushPixelWidth,
                $"{pathInputMode} Stampの照合用元PNG幅");
            Equal(30, stamp.UserBrushPixelHeight,
                $"{pathInputMode} Stampの照合用元PNG高さ");
            True(
                WipeMaskRenderer.MatchesUserBrushEntry(stamp, entry),
                $"{pathInputMode} の非100%倍率でも登録簿照合に成功する必要があります。");
        }
    }

    public static void VerifyStrokeCanvasMapping()
    {
        var hd = WipeMaskGeometry.Create(1920, 1080);
        VectorEqual(Vector2.Zero, Map(0, 0, hd), "1920×1080の左上");
        VectorEqual(Vector2.One, Map(1920, 1080, hd), "1920×1080の右下");
        VectorEqual(new Vector2(0.5f), Map(960, 540, hd), "1920×1080の中央");

        var square = WipeMaskGeometry.Create(1080, 1080);
        VectorEqual(new Vector2(0, 0.21875f), Map(0, 0, square),
            "正方形領域では上下を中央寄せする必要があります。", 0.00001f);
        VectorEqual(new Vector2(1, 0.78125f), Map(1920, 1080, square),
            "正方形領域のcontain下端", 0.00001f);

        var portrait = WipeMaskGeometry.Create(1080, 1920);
        VectorEqual(new Vector2(0, 0.341796875f), Map(0, 0, portrait),
            "縦長領域では上下を中央寄せする必要があります。", 0.00001f);
        VectorEqual(new Vector2(1, 0.658203125f), Map(1920, 1080, portrait),
            "縦長領域のcontain下端", 0.00001f);

        var starter = WipeStrokeDocument.CreateStarter();
        Equal(2, starter.DataSchemaVersion, "カスタム軌跡のデータ版");
        Equal(192d, starter.Strokes[0].Points[0].X, "スターター始点X");
        Equal(540d, starter.Strokes[0].Points[0].Y, "スターター始点Y");
        Equal(1728d, starter.Strokes[0].Points[1].X, "スターター終点X");
        True(
            WipeStrokeDocumentCodec.Encode(starter).StartsWith("v2:", StringComparison.Ordinal),
            "カスタム軌跡はv2接頭辞で保存する必要があります。");
        True(
            !WipeStrokeDocumentCodec.TryDecode(
                "v1:{\"dataSchemaVersion\":1,\"strokes\":[]}",
                out _),
            "旧v1軌跡はクラッシュせず拒否する必要があります。");

        VerifyLegacyFallbackKeepsV1Data();
        VerifyNonWideStreamDeterminism();

        foreach (var preset in Enum.GetValues<DetailedWipeBuiltInPreset>())
        {
            True(
                WipeStrokeDocumentCodec.TryDecode(
                    DetailedWipePresetFactory.Create(preset).CustomPathData,
                    out var document),
                $"{preset} の軌跡を復元できる必要があります。");
            var hdBounds = GetPhysicalBounds(document, 1920, 1080);
            var squareBounds = GetPhysicalBounds(document, 1080, 1080);
            var portraitBounds = GetPhysicalBounds(document, 1080, 1920);
            var fourKBounds = GetPhysicalBounds(document, 3840, 2160);
            var hdAspect = hdBounds.Width / hdBounds.Height;
            True(
                hdAspect is > 0.9f and < 1.1f,
                $"{preset} の基準図形はほぼ等幅・等高である必要があります。");
            NearlyEqual(hdAspect, squareBounds.Width / squareBounds.Height,
                $"{preset} の正方形領域での縦横比", 0.0001f);
            NearlyEqual(hdAspect, portraitBounds.Width / portraitBounds.Height,
                $"{preset} の縦長領域での縦横比", 0.0001f);
            NearlyEqual(hdBounds.Width * 2, fourKBounds.Width,
                $"{preset} の4K幅", 0.001f);
            NearlyEqual(hdBounds.Height * 2, fourKBounds.Height,
                $"{preset} の4K高さ", 0.001f);
        }
    }

    private static void VerifyLegacyFallbackKeepsV1Data()
    {
        const string v1Data =
            "v1:{\"dataSchemaVersion\":1,\"strokes\":[{\"points\":[]}]}";
        var effect = new GlassWipeVideoEffect
        {
            EditingMode = GlassWipeEditingMode.Detailed,
            PathInputMode = WipePathInputMode.StrokeCollection,
            CustomPathData = v1Data,
            PathTimingMode = WipePathTimingMode.Percent,
            PathStart = 0,
            PathCompletion = 100,
            BrushShape = GlassWipeBrushShape.Circle,
        };
        var description = CreateEffectDescription(90);
        var geometry = WipeMaskGeometry.Create(900, 1600);

        var snapshot = WipePathSnapshot.Create(effect, description, geometry);
        var stream = WipePathStream.Create(effect, description, geometry);

        Equal(
            WipePathInputMode.LegacyAnimation,
            snapshot.InputMode,
            "v1カスタム軌跡のSnapshotは旧Animationへフォールバックする必要があります。");
        Equal(
            WipePathStreamKind.LegacyAnimation,
            stream.Kind,
            "v1カスタム軌跡のStreamは旧Animationへフォールバックする必要があります。");
        Equal(
            v1Data,
            effect.CustomPathData,
            "v1カスタム軌跡の保存値をフォールバック時に上書きしてはいけません。");
    }

    private static void VerifyNonWideStreamDeterminism()
    {
        var effect = new GlassWipeVideoEffect
        {
            PathTimingMode = WipePathTimingMode.Percent,
            EditingMode = GlassWipeEditingMode.Detailed,
            PathInputMode = WipePathInputMode.StrokeCollection,
            CustomPathData = DetailedWipePresetFactory
                .Create(DetailedWipeBuiltInPreset.Heart)
                .CustomPathData,
            PathStart = 0,
            PathCompletion = 100,
            BrushShape = GlassWipeBrushShape.Circle,
        };
        var portraitGeometry = WipeMaskGeometry.Create(900, 1600);
        var forward = WipePathStream.Create(
            effect,
            CreateEffectDescription(90),
            portraitGeometry);
        var repeated = WipePathStream.Create(
            effect,
            CreateEffectDescription(90),
            portraitGeometry);
        var backward = WipePathStream.Create(
            effect,
            CreateEffectDescription(45),
            portraitGeometry);

        True(forward.Frame >= 90, "前方Streamの評価フレームを設定できる必要があります。");
        True(backward.Frame >= 45 && backward.Frame < forward.Frame,
            "後方シークStreamは前方Streamより短いprefixになる必要があります。");
        Equal(
            WipePathStreamKind.StrokeCollection,
            forward.Kind,
            "v2カスタム軌跡はStrokeCollection Streamを使用する必要があります。");
        Equal(
            forward.DefinitionFingerprint,
            repeated.DefinitionFingerprint,
            "同一フレームのStream Fingerprintは一致する必要があります。");
        Equal(
            forward.DefinitionFingerprint,
            backward.DefinitionFingerprint,
            "前後シークで軌跡定義Fingerprintを変えてはいけません。");

        var forwardSamples = Enumerate(forward);
        var repeatedSamples = Enumerate(repeated);
        var backwardSamples = Enumerate(backward);
        True(
            forwardSamples.SequenceEqual(repeatedSamples),
            "同一フレームのv2 Streamサンプルを再現する必要があります。");
        True(
            forwardSamples
                .Where(sample => sample.Sample.Frame <= backward.Frame)
                .SequenceEqual(backwardSamples),
            "後方シークのv2 Streamは前方履歴の同一prefixを再現する必要があります。");

        var landscapeGeometry = WipeMaskGeometry.Create(1600, 900);
        var differentGeometry = WipePathStream.Create(
            effect,
            CreateEffectDescription(90),
            landscapeGeometry);
        True(
            forward.DefinitionFingerprint != differentGeometry.DefinitionFingerprint,
            "領域代表寸法の変更をv2 Stream Fingerprintへ含める必要があります。");
        Equal(
            WipeMaskUpdateKind.Rebuild,
            WipeMaskUpdatePlan.Create(
                forward,
                portraitGeometry,
                differentGeometry,
                landscapeGeometry).Kind,
            "領域代表寸法の変更後は拭き取りマスクを再構築する必要があります。");
    }

    private static List<(int LaneId, WipePathSample Sample)> Enumerate(
        WipePathStream stream)
    {
        var samples = new List<(int LaneId, WipePathSample Sample)>();
        stream.EnumerateSamples(
            0,
            stream.Frame,
            (laneId, sample) => samples.Add((laneId, sample)));
        return samples;
    }

    private static EffectDescription CreateEffectDescription(int currentFrame)
    {
        const int framesPerSecond = 60;
        const int duration = 120;
        var timeline = new TimelineSourceDescription(
            new Size(1280, 720),
            new YukkuriMovieMaker.Player.Video.FrameTime(currentFrame, framesPerSecond),
            new YukkuriMovieMaker.Player.Video.FrameTime(duration, framesPerSecond),
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

    private static WipeBrushStamp GenerateSingleStamp(
        WipeMaskGeometry geometry,
        GlassWipeBrushShape shape,
        WipeBrushPixelDimensions dimensions) =>
        WipeBrushStampGenerator.Generate(
                [CreateSample(dimensions)],
                0,
                geometry,
                new WipePathStyle(0, 0, 1, BrushShape: shape))
            .Single();

    private static WipePathSample CreateSample(
        WipeBrushPixelDimensions dimensions) =>
        new(
            0,
            0.5f,
            0.5f,
            1,
            0,
            1,
            1,
            0,
            BrushWidthPixels: dimensions.Width,
            BrushHeightPixels: dimensions.Height);

    private static float ToInputPixelWidth(
        WipeBrushStamp stamp,
        WipeMaskGeometry geometry) =>
        stamp.RadiusX * 2 / WipeMaskRenderer.MaskSize * geometry.PixelWidth;

    private static float ToInputPixelHeight(
        WipeBrushStamp stamp,
        WipeMaskGeometry geometry) =>
        stamp.RadiusY * 2 / WipeMaskRenderer.MaskSize * geometry.PixelHeight;

    private static Vector2 Map(float x, float y, WipeMaskGeometry geometry) =>
        WipeStrokeCoordinateMapper.ToNormalizedUv(new Vector2(x, y), geometry);

    private static (float Width, float Height) GetPhysicalBounds(
        WipeStrokeDocument document,
        float width,
        float height)
    {
        var geometry = WipeMaskGeometry.Create(width, height);
        var points = document.Strokes
            .SelectMany(stroke => stroke.Points)
            .Select(point => Map((float)point.X, (float)point.Y, geometry))
            .Select(point => new Vector2(point.X * width, point.Y * height))
            .ToArray();
        var minX = points.Min(point => point.X);
        var maxX = points.Max(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxY = points.Max(point => point.Y);
        return (maxX - minX, maxY - minY);
    }

    private static void VectorEqual(
        Vector2 expected,
        Vector2 actual,
        string message,
        float tolerance = 0.000001f)
    {
        NearlyEqual(expected.X, actual.X, message + " X", tolerance);
        NearlyEqual(expected.Y, actual.Y, message + " Y", tolerance);
    }

    private static void NearlyEqual(
        float expected,
        float actual,
        string message,
        float tolerance = 0.000001f)
    {
        if (MathF.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException(
                $"{message}: Expected={expected}, Actual={actual}");
        }
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message}: Expected={expected}, Actual={actual}");
        }
    }

    private static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
