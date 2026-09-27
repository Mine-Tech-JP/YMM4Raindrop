// SPDX-License-Identifier: MPL-2.0

using System.Text.Json.Nodes;
using YMM4GlassWipe;

internal static class OutsideDropletOutlineOpacityVerification
{
    public static void VerifyCodecAndLegacyCompatibility()
    {
        True(DetailedWipePresetState.CurrentVersion >= DetailedWipePresetState.OutsideDropletOutlineOpacityIntroducedVersion,
            "現行Stateは黒い輪郭の不透明度の導入版以降である必要があります。");
        Equal(11, DetailedWipePresetState.OutsideDropletOutlineOpacityIntroducedVersion,
            "黒い輪郭の不透明度の導入State版はv11である必要があります。");

        foreach (var opacity in new[] { 0d, 50d, 100d })
        {
            var state = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
            state.OutsideDropletOutlineOpacity = opacity;
            var encoded = DetailedWipePresetCodec.Encode(state);
            True(encoded.Contains($"\"outsideDropletOutlineOpacity\":{opacity}", StringComparison.Ordinal),
                $"不透明度={opacity}は実codecで保存する必要があります。");
            True(DetailedWipePresetCodec.TryDecode(encoded, out var restored),
                $"不透明度={opacity}を実codecで復元できる必要があります。");
            Equal(opacity, restored.OutsideDropletOutlineOpacity,
                $"不透明度={opacity}を往復保存する必要があります。");
        }

        var current = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
        current.OutsideDropletMergeEnabled = true;
        current.OutsideDropletOutlineOpacity = 37;
        var latest = DetailedWipePresetCodec.Encode(current);
        for (var version = 1; version <= 10; version++)
        {
            var legacy = JsonNode.Parse(latest)!.AsObject();
            legacy["dataSchemaVersion"] = version;
            True(legacy.Remove("outsideDropletOutlineOpacity"), "移行試験は新しいプロパティを実際に除去します。");
            True(DetailedWipePresetCodec.TryDecode(legacy.ToJsonString(), out var migrated),
                $"v{version}で欠落した黒い輪郭の不透明度を移行できる必要があります。");
            Equal(100d, migrated.OutsideDropletOutlineOpacity,
                $"v{version}で欠落した黒い輪郭の不透明度は100%へ移行する必要があります。");
            if (version == 10)
            {
                Equal(true, migrated.OutsideDropletMergeEnabled,
                    "v10で保存された合体ONは不透明度追加後も維持する必要があります。");
            }
        }

        var preset = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Smiley);
        preset.OutsideDropletOutlineOpacity = 0;
        foreach (var scope in new[] { DetailedWipePresetScope.Path, DetailedWipePresetScope.Brush, DetailedWipePresetScope.All })
        {
            True(preset.TrySanitize(scope, out var scoped), $"{scope}の範囲で正規化できる必要があります。");
            True(DetailedWipePresetStateMerger.TryMerge(current, preset, scope, out var merged),
                $"{scope}の範囲で設定を適用できる必要があります。");
            Equal(scope == DetailedWipePresetScope.All ? 0d : 37d, merged.OutsideDropletOutlineOpacity,
                "不透明度は全設定だけから適用し、軌跡とブラシでは現在値を維持する必要があります。");
            if (scope != DetailedWipePresetScope.All)
            {
                True(scoped.OutsideDropletOutlineOpacity is null,
                    "軌跡・ブラシ保存に不透明度を含めてはいけません。");
            }
        }

        var invalid = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
        invalid.OutsideDropletOutlineOpacity = double.NaN;
        True(invalid.TrySanitize(out var sanitized), "非有限値を正規化できる必要があります。");
        Equal(100d, sanitized.OutsideDropletOutlineOpacity,
            "非有限の不透明度は100%へ正規化する必要があります。");
        invalid.OutsideDropletOutlineOpacity = -1d;
        True(invalid.TrySanitize(out sanitized), "範囲外の負値を正規化できる必要があります。");
        Equal(0d, sanitized.OutsideDropletOutlineOpacity,
            "負の不透明度は0%へ丸める必要があります。");
        invalid.OutsideDropletOutlineOpacity = 101d;
        True(invalid.TrySanitize(out sanitized), "範囲外の正値を正規化できる必要があります。");
        Equal(100d, sanitized.OutsideDropletOutlineOpacity,
            "100%超の不透明度は100%へ丸める必要があります。");
    }

    public static void VerifySourceWiring()
    {
        var root = FindRepositoryRoot();
        var effectSource = File.ReadAllText(Path.Combine(
            root, "src", "YMM4GlassWipe", "GlassWipeVideoEffect.cs"));
        True(effectSource.Contains(
                "Name = \"輪郭の濃さ\"",
                StringComparison.Ordinal) &&
            effectSource.Contains(
                "[ShowPropertyEditorWhen(nameof(IsOutsideDropletOutlineOpacityVisible), true)]",
                StringComparison.Ordinal),
            "黒い輪郭の不透明度は専用の可視条件を持つ必要があります。");
        True(effectSource.Contains(
                "AreOutsideDropletSettingsVisible &&",
                StringComparison.Ordinal) &&
            effectSource.Contains(
                "OutsideDropletAppearanceValue.Transparent",
                StringComparison.Ordinal),
            "黒い輪郭の不透明度は水滴量と透明な見た目に連動して表示する必要があります。");
        var appearanceStart = effectSource.IndexOf(
            "public OutsideDropletAppearanceValue OutsideDropletAppearance",
            StringComparison.Ordinal);
        var appearanceEnd = effectSource.IndexOf(
            "public double OutsideDropletOutlineOpacity",
            appearanceStart,
            StringComparison.Ordinal);
        True(appearanceStart >= 0 && appearanceEnd > appearanceStart &&
            effectSource[appearanceStart..appearanceEnd].Contains(
                "OnPropertyChanged(nameof(IsOutsideDropletOutlineOpacityVisible));",
                StringComparison.Ordinal),
            "水滴の見た目変更時に黒い輪郭の表示条件を通知する必要があります。");
        True(effectSource.Contains(
                "[DefaultValue(GlassWipeDefaultSettings.OutsideDropletOutlineOpacityPercent)]",
                StringComparison.Ordinal),
            "黒い輪郭の不透明度は新規追加時の既定値40%を明示する必要があります。");

        var opacityStart = effectSource.IndexOf("[Display(GroupName = \"水滴\", Name = \"輪郭の濃さ\"", StringComparison.Ordinal);
        var opacityProperty = effectSource.IndexOf("public double OutsideDropletOutlineOpacity", opacityStart, StringComparison.Ordinal);
        True(effectSource[opacityStart..opacityProperty].Contains("[JsonIgnore(Condition = JsonIgnoreCondition.Never)]", StringComparison.Ordinal),
            "0%を既定値扱いで省略せず、再読込で100%へ戻ることを防ぐ必要があります。");
        var amountStart = effectSource.IndexOf("public double OutsideDropletAmount", StringComparison.Ordinal);
        var amountEnd = effectSource.IndexOf("public double OutsideDropletSize", amountStart, StringComparison.Ordinal);
        True(effectSource[amountStart..amountEnd].Contains("OnPropertyChanged(nameof(IsOutsideDropletOutlineOpacityVisible));", StringComparison.Ordinal),
            "水滴量0との切り替えでも不透明度欄の表示を更新する必要があります。");

        var supportSource = File.ReadAllText(Path.Combine(
            root, "src", "YMM4GlassWipe", "GlassWipeVideoEffect.DetailedPresetSupport.cs"));
        True(supportSource.Contains(
                "OutsideDropletOutlineOpacity = OutsideDropletOutlineOpacity,",
                StringComparison.Ordinal) &&
            supportSource.Contains(
                "OutsideDropletOutlineOpacity = merged.OutsideDropletOutlineOpacity ??",
                StringComparison.Ordinal),
            "全設定プリセットは黒い輪郭の不透明度を保存して復元する必要があります。");
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
