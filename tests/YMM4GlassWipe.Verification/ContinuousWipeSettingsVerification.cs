// SPDX-License-Identifier: MPL-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Drawing;

using System.Runtime.CompilerServices;
using YMM4GlassWipe;
using YukkuriMovieMaker.Player.Video;
using YmmJson = YukkuriMovieMaker.Json.Json;

internal static class ContinuousWipeSettingsVerification
{
    public static void VerifyDefaultsAndCompatibility()
    {
        Equal(16, DetailedWipePresetState.CurrentVersion, "簡易物理を含むStateはv16である必要があります。");
        Equal(13, DetailedWipePresetState.ContinuousWipeIntroducedVersion, "連続拭きの導入State版はv13である必要があります。");

        var newEffect = new GlassWipeVideoEffect();
        Equal(true, newEffect.ContinuousWipe, "新規Effectの連続拭きはONである必要があります。");

        var legacyEffect = JsonSerializer.Deserialize<GlassWipeVideoEffect>("{}");
        True(legacyEffect is not null, "旧Effectの空JSONを復元できる必要があります。");
        Equal(false, legacyEffect!.ContinuousWipe, "旧Effectで欠落した連続拭きはOFFである必要があります。");

        var clonedNewEffect = YmmJson.GetClone(new GlassWipeVideoEffect());
        True(clonedNewEffect is not null, "YMM4の新規Effectを保存復元できる必要があります。");
        Equal(true, clonedNewEffect!.ContinuousWipe, "YMM4保存cloneは新規Effectの連続拭きONを保持する必要があります。");

        var explicitOffEffect = new GlassWipeVideoEffect { ContinuousWipe = false };
        var clonedExplicitOff = YmmJson.GetClone(explicitOffEffect);
        True(clonedExplicitOff is not null, "YMM4の明示OFF Effectを保存復元できる必要があります。");
        Equal(false, clonedExplicitOff!.ContinuousWipe, "YMM4保存cloneは明示した連続拭きOFFを保持する必要があります。");

        var ymmLegacyEffect = YmmJson.LoadFromText<GlassWipeVideoEffect>("{}") ??
            throw new InvalidOperationException("YMM4の欠落設定Effectを復元できません。");
        Equal(false, ymmLegacyEffect.ContinuousWipe, "YMM4の欠落した連続拭きはOFFである必要があります。");
        var clonedLegacyEffect = YmmJson.GetClone(ymmLegacyEffect);
        True(clonedLegacyEffect is not null, "YMM4の欠落設定Effectを再保存できる必要があります。");
        Equal(false, clonedLegacyEffect!.ContinuousWipe, "YMM4保存cloneは移行後の連続拭きOFFを保持する必要があります。");

        var current = new DetailedWipePresetState
        {
            BrushShape = GlassWipeBrushShape.Hand,
            ContinuousWipe = true,
        };
        var encoded = DetailedWipePresetCodec.Encode(current);
        True(DetailedWipePresetCodec.TryDecode(encoded, out var restored), "現行連続拭きプリセットを復元できる必要があります。");
        Equal(true, restored.ContinuousWipe, "現行連続拭き値を往復保存する必要があります。");

        var userImagePreset = new DetailedWipePresetState
        {
            BrushShape = GlassWipeBrushShape.UserImage,
            ContinuousWipe = true,
        };
        True(DetailedWipePresetCodec.TryDecode(DetailedWipePresetCodec.Encode(userImagePreset),
            out var restoredUserImagePreset), "ユーザー画像のプリセットを復元できる必要があります。");
        Equal(true, restoredUserImagePreset.ContinuousWipe,
            "ユーザー画像の連続拭きONをプリセットへ保存する必要があります。");

        for (var version = 1; version <= 12; version++)
        {
            var legacy = JsonNode.Parse(encoded)!.AsObject();
            legacy["dataSchemaVersion"] = version;
            True(legacy.Remove("continuousWipe"), $"v{version}試験では連続拭きキーを取り除く必要があります。");
            True(DetailedWipePresetCodec.TryDecode(legacy.ToJsonString(), out var migrated), $"v{version}プリセットを移行できる必要があります。");
            Equal(false, migrated.ContinuousWipe, $"v{version}の連続拭きはOFFへ移行する必要があります。");
        }

        var missingCurrent = JsonNode.Parse(encoded)!.AsObject();
        True(missingCurrent.Remove("continuousWipe"), "欠落値試験では連続拭きキーを取り除く必要があります。");
        True(DetailedWipePresetCodec.TryDecode(missingCurrent.ToJsonString(), out var migratedMissing), "現行版で欠落した連続拭きを復元できる必要があります。");
        Equal(false, migratedMissing.ContinuousWipe, "欠落した連続拭きはOFFである必要があります。");

        foreach (var shape in new[]
                 {
                     GlassWipeBrushShape.Ellipse,
                     GlassWipeBrushShape.Rectangle,
                 })
        {
            var unsupported = new DetailedWipePresetState
            {
                BrushShape = shape,
                ContinuousWipe = true,
            };
            True(unsupported.TrySanitize(out var sanitized), $"{shape}を正規化できる必要があります。");
            Equal(false, sanitized.ContinuousWipe, $"{shape}で連続拭きを有効にしてはいけません。");
        }
    }

    public static void VerifyPresetScopeAndRebuild()
    {
        var current = new DetailedWipePresetState
        {
            BrushShape = GlassWipeBrushShape.Hand,
            ContinuousWipe = true,
        };
        var disabledBrush = new DetailedWipePresetState
        {
            BrushShape = GlassWipeBrushShape.ShoePrint,
            ContinuousWipe = false,
        };

        True(DetailedWipePresetStateMerger.TryMerge(
            current,
            disabledBrush,
            DetailedWipePresetScope.Path,
            out var pathMerged), "軌跡プリセットを統合できる必要があります。");
        Equal(true, pathMerged.ContinuousWipe, "軌跡プリセット適用は現在の連続拭きを保持する必要があります。");

        foreach (var scope in new[] { DetailedWipePresetScope.Brush, DetailedWipePresetScope.All })
        {
            True(DetailedWipePresetStateMerger.TryMerge(current, disabledBrush, scope, out var merged), $"{scope}プリセットを統合できる必要があります。");
            Equal(false, merged.ContinuousWipe, $"{scope}プリセットは連続拭きを適用する必要があります。");
        }

        var baseStyle = new WipePathStyle(0, 0, 1, BrushShape: GlassWipeBrushShape.Hand);
        var continuousStyle = baseStyle with { ContinuousWipe = true };
        Equal(true, continuousStyle.Sanitize().ContinuousWipe, "手形の連続拭きを有効にできる必要があります。");
        Equal(true, (continuousStyle with { BrushShape = GlassWipeBrushShape.UserImage }).Sanitize().ContinuousWipe, "ユーザー画像でも連続拭きを有効にできる必要があります。");

        var baseSnapshot = new WipePathSnapshot(0, 10, 60, [], baseStyle);
        var continuousSnapshot = new WipePathSnapshot(0, 10, 60, [], continuousStyle);
        True(baseSnapshot.Fingerprint.Value != continuousSnapshot.Fingerprint.Value, "連続拭きの変更でSnapshotを再構築する必要があります。");
        True(!baseSnapshot.HasSameContext(continuousSnapshot), "連続拭きの変更でSnapshotの文脈一致を解除する必要があります。");

        var generated = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.StraightOnce,
            GlassWipePathInterpolation.Linear,
            new System.Numerics.Vector2(0, 0),
            new System.Numerics.Vector2(50, 50),
            new System.Numerics.Vector2(100, 100),
            1,
            0,
            10,
            continuousWipe: true);
        True(WipePathSnapshot.ResolveSimpleGeneratedStyle(GlassWipeEditingMode.Simple, generated.Style, GlassWipeBrushShape.Hand, continuousWipe: true).Sanitize().ContinuousWipe, "手形を解決した標準生成Styleへ連続拭きを伝播する必要があります。");
        True(generated.Signature.Contains("continuousWipe=1", StringComparison.Ordinal), "標準生成器の署名へ連続拭きを含める必要があります。");

        VerifyEffectPresetAndStreamPropagation();
    }

    public static void VerifyUiWiring()
    {
        True(GlassWipeSimpleVisibility.IsContinuousWipeVisible(GlassWipeBrushShape.Hand), "手形では連続拭きを表示する必要があります。");
        True(GlassWipeSimpleVisibility.IsContinuousWipeVisible(GlassWipeBrushShape.ShoePrint), "靴跡では連続拭きを表示する必要があります。");
        True(GlassWipeSimpleVisibility.IsContinuousWipeVisible(GlassWipeBrushShape.UserImage), "ユーザー画像でも連続拭きを表示する必要があります。");
        True(!GlassWipeSimpleVisibility.IsContinuousWipeVisible(GlassWipeBrushShape.Ellipse), "円では連続拭きを表示してはいけません。");
    }

    private static void VerifyEffectPresetAndStreamPropagation()
    {
        var current = new GlassWipeVideoEffect
        {
            EditingMode = GlassWipeEditingMode.Detailed,
            PathInputMode = WipePathInputMode.StrokeCollection,
            BrushShape = GlassWipeBrushShape.Hand,
            ContinuousWipe = true,
        };
        var preset = new DetailedWipePresetState
        {
            BrushShape = GlassWipeBrushShape.ShoePrint,
            ContinuousWipe = false,
        };

        current.DetailedPresetExchange = DetailedWipePresetExchangeCodec.EncodeApply(
            DetailedWipePresetScope.Path,
            preset);
        Equal(true, current.ContinuousWipe, "Effectの軌跡プリセット適用は連続拭きを保持する必要があります。");

        current.DetailedPresetExchange = DetailedWipePresetExchangeCodec.EncodeApply(
            DetailedWipePresetScope.Brush,
            preset);
        Equal(false, current.ContinuousWipe, "Effectのブラシプリセット適用は連続拭きを復元する必要があります。");

        current.ContinuousWipe = true;
        current.DetailedPresetExchange = DetailedWipePresetExchangeCodec.EncodeApply(
            DetailedWipePresetScope.All,
            preset);
        Equal(false, current.ContinuousWipe, "Effectの全設定プリセット適用は連続拭きを復元する必要があります。");

        foreach (var configure in new Action<GlassWipeVideoEffect>[]
                 {
                     item =>
                     {
                         item.EditingMode = GlassWipeEditingMode.Simple;
                         item.PathInputMode = WipePathInputMode.SimpleGenerated;
                     },
                     item =>
                     {
                         item.EditingMode = GlassWipeEditingMode.Detailed;
                         item.PathInputMode = WipePathInputMode.LegacyAnimation;
                     },
                     item =>
                     {
                         item.EditingMode = GlassWipeEditingMode.Detailed;
                         item.PathInputMode = WipePathInputMode.StrokeCollection;
                     },
                 })
        {
            var disabled = new GlassWipeVideoEffect
            {
                BrushShape = GlassWipeBrushShape.Hand,
                ContinuousWipe = false,
            };
            configure(disabled);
            var enabled = YmmJson.GetClone(disabled) ??
                throw new InvalidOperationException("Stream試験用Effectをcloneできません。");
            enabled.ContinuousWipe = true;

            var description = CreateEffectDescription();
            var geometry = WipeMaskGeometry.Create(1280, 720);
            var disabledStream = WipePathStream.Create(disabled, description, geometry);
            var enabledStream = WipePathStream.Create(enabled, description, geometry);
            Equal(false, disabledStream.Style.ContinuousWipe, "OFFのStream Styleは連続拭きOFFである必要があります。");
            Equal(true, enabledStream.Style.ContinuousWipe, "ONのStream Styleは連続拭きONである必要があります。");
            True(disabledStream.DefinitionFingerprint != enabledStream.DefinitionFingerprint, "連続拭き変更でStream DefinitionFingerprintを変更する必要があります。");
            Equal(
                WipeMaskUpdateKind.Rebuild,
                WipeMaskUpdatePlan.Create(disabledStream, geometry, enabledStream, geometry).Kind,
                "連続拭き変更後のStreamはMaskを再構築する必要があります。");
        }
    }

    private static EffectDescription CreateEffectDescription()
    {
        const int framesPerSecond = 60;
        var timeline = new TimelineSourceDescription(
            new Size(1280, 720),
            new FrameTime(0, framesPerSecond),
            new FrameTime(120, framesPerSecond),
            framesPerSecond,
            default,
            Guid.Empty,
            []);
        var item = new TimelineItemSourceDescription(timeline, 0, 120, 0);
        // WipePathStream.CreateはDrawDescriptionの値を読まず、ItemPosition、ItemDuration、FPSだけを使う。
        // 依存を増やさず、Vortice型を要求するDrawDescriptionの現行コンストラクタを呼ばない。
        var draw = (DrawDescription)RuntimeHelpers.GetUninitializedObject(typeof(DrawDescription));
        return new EffectDescription(item, draw, 0, 1, 0, 1);
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
            throw new InvalidOperationException($"{message} 期待値: {expected}, 実際: {actual}");
        }
    }
}
