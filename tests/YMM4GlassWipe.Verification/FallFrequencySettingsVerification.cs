// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using YMM4GlassWipe;
using YukkuriMovieMaker.Player.Video;
using YmmJson = YukkuriMovieMaker.Json.Json;

namespace YMM4GlassWipe.Verification;

public static class FallFrequencySettingsVerification
{
    public static void VerifyUiPersistenceAndPresetScope()
    {
        const string description =
            "落下滴の候補数を増やします。100%が標準、400%で候補数は最大4倍です。大きさ・垂れる速さは変わりません。合体などにより実際に見える数は変わります。";
        Equal(100d, OutsideDropletSettings.MinimumFallFrequencyPercent, "垂れる頻度の下限");
        Equal(400d, OutsideDropletSettings.MaximumFallFrequencyPercent, "垂れる頻度の上限");
        Equal(100d, OutsideDropletSettings.DefaultFallFrequencyPercent, "垂れる頻度の既定値");
        Equal(1f, OutsideDropletSettings.SanitizeFallFrequencyScale(100), "100%の頻度倍率");
        Equal(4f, OutsideDropletSettings.SanitizeFallFrequencyScale(400), "400%の頻度倍率");
        Equal(100d, OutsideDropletSettings.SanitizeFallFrequencyPercent(double.NaN), "非有限の頻度は既定値へ補正");

        var property = typeof(GlassWipeVideoEffect).GetProperty(
            nameof(GlassWipeVideoEffect.OutsideDropletFallFrequency)) ??
            throw new InvalidOperationException("垂れる頻度プロパティを公開する必要があります。");
        var display = property.GetCustomAttribute<DisplayAttribute>();
        Equal("垂れる頻度", display?.Name ?? string.Empty, "垂れる頻度の表示名");
        Equal(description, display?.Description ?? string.Empty, "垂れる頻度の説明文");
        Equal(
            200d,
            Convert.ToDouble(property.GetCustomAttribute<DefaultValueAttribute>()?.Value),
            "垂れる頻度のリセット既定値");
        var range = property.GetCustomAttribute<RangeAttribute>();
        Equal(100d, Convert.ToDouble(range?.Minimum), "垂れる頻度のUI下限");
        Equal(400d, Convert.ToDouble(range?.Maximum), "垂れる頻度のUI上限");

        var missingEffect = YmmJson.LoadFromText<GlassWipeVideoEffect>("{}") ??
            throw new InvalidOperationException("YMM4の欠落設定Effectを復元できません。");
        Equal(100d, missingEffect.OutsideDropletFallFrequency, "YMM4保存で欠落した垂れる頻度は100%へ補完する必要があります。");
        var savedEffect = new GlassWipeVideoEffect
        {
            OutsideDropletFallFrequency = 250,
        };
        var clonedEffect = YmmJson.GetClone(savedEffect) ??
            throw new InvalidOperationException("YMM4の垂れる頻度Effectを保存復元できません。");
        Equal(250d, clonedEffect.OutsideDropletFallFrequency, "YMM4保存は垂れる頻度250%を往復する必要があります。");

        var parameters = GlassWipeParameters.Create(
            savedEffect,
            CreateEffectDescription());
        Equal(2.5f, parameters.OutsideDropletFallFrequency, "FromItem相当のパラメータ生成は250%を2.5倍へ変換する必要があります。");
        True(
            parameters.HasSameNonTemporalValues(
                parameters with { OutsideDropletLocalTimeSeconds = 1f }),
            "時刻だけの変化は既存の更新経路で扱う必要があります。");
        True(
            !parameters.HasSameNonTemporalValues(
                parameters with { OutsideDropletFallFrequency = 1f }),
            "垂れる頻度の変化は時刻だけの差として扱ってはいけません。");

        var frequencyPreset = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
        frequencyPreset.OutsideDropletFallFrequency = 250;
        var encoded = DetailedWipePresetCodec.Encode(frequencyPreset);
        True(
            DetailedWipePresetCodec.TryDecode(encoded, out var restored),
            "垂れる頻度を含むプリセットを復元できる必要があります。");
        Equal(250d, restored.OutsideDropletFallFrequency ?? double.NaN, "垂れる頻度を往復保存する必要があります。");

        var missingFrequency = JsonNode.Parse(encoded)!.AsObject();
        True(
            missingFrequency.Remove("outsideDropletFallFrequency"),
            "欠落値互換試験では垂れる頻度キーを取り除く必要があります。");
        True(
            DetailedWipePresetCodec.TryDecode(missingFrequency.ToJsonString(), out var restoredMissing),
            "State v15で垂れる頻度が欠落してもプリセットを復元できる必要があります。");
        Equal(100d, restoredMissing.OutsideDropletFallFrequency ?? double.NaN, "欠落した垂れる頻度は100%へ補完する必要があります。");

        True(
            frequencyPreset.TrySanitize(DetailedWipePresetScope.All, out var all),
            "全設定プリセットを正規化できる必要があります。");
        Equal(250d, all.OutsideDropletFallFrequency ?? double.NaN, "全設定プリセットは垂れる頻度を含める必要があります。");
        True(
            frequencyPreset.TrySanitize(DetailedWipePresetScope.Brush, out var brush),
            "ブラシプリセットを正規化できる必要があります。");
        True(brush.OutsideDropletFallFrequency is null, "ブラシプリセットへ垂れる頻度を含めてはいけません。");

        var effectSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "YMM4GlassWipe", "GlassWipeVideoEffect.cs"));
        var propertyEnd = effectSource.IndexOf(
            "public double OutsideDropletFallFrequency",
            StringComparison.Ordinal);
        var propertyStart = propertyEnd >= 0
            ? effectSource.LastIndexOf(
                "[Display(",
                propertyEnd,
                StringComparison.Ordinal)
            : -1;
        True(
            propertyStart >= 0 && propertyEnd >= propertyStart &&
            effectSource[propertyStart..propertyEnd].Contains(
                "[ShowPropertyEditorWhen(nameof(AreOutsideDropletLegacyFallSettingsVisible), true)]",
                StringComparison.Ordinal),
            "垂れる頻度は従来方式専用の落下表示条件へ結び付く必要があります。");

        var parametersSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "YMM4GlassWipe", "GlassWipeParameters.cs"));
        True(
            parametersSource.Contains("float OutsideDropletFallFrequency = 1f", StringComparison.Ordinal) &&
            parametersSource.Contains(
                "OutsideDropletSettings.SanitizeFallFrequencyScale(item.OutsideDropletFallFrequency)",
                StringComparison.Ordinal),
            "FromItem相当のパラメータ生成は垂れる頻度を倍率として渡す必要があります。");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "YMM4GlassWipe.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("リポジトリルートを特定できません。");
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
        var draw = (DrawDescription)RuntimeHelpers.GetUninitializedObject(
            typeof(DrawDescription));
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
