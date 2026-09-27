// SPDX-License-Identifier: MPL-2.0

using System.Text.Json.Nodes;
using YMM4GlassWipe;

internal static class OutsideDropletMergeSettingsVerification
{
    public static void VerifySourceWiring()
    {
        Equal(
            false,
            OutsideDropletSettings.DefaultMergeEnabled,
            "水滴合体の旧データ補完値はオフである必要があります。");
        True(
            DetailedWipePresetState.CurrentVersion >= DetailedWipePresetState.OutsideDropletMergeIntroducedVersion,
            "現行Stateは水滴合体の導入版以降である必要があります。");
        Equal(
            10,
            DetailedWipePresetState.OutsideDropletMergeIntroducedVersion,
            "水滴合体の導入State版はv10である必要があります。");

        var fresh = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
        Equal(
            false,
            fresh.OutsideDropletMergeEnabled,
            "内蔵の全設定プリセットは水滴合体をオフで作成する必要があります。");

        var repositoryRoot = FindRepositoryRoot();
        var effectSource = File.ReadAllText(Path.Combine(
            repositoryRoot, "src", "YMM4GlassWipe", "GlassWipeVideoEffect.cs"));
        var propertyStart = effectSource.IndexOf(
            "[Display(GroupName = \"水滴\", Name = \"水滴の合体\"",
            StringComparison.Ordinal);
        var propertyEnd = effectSource.IndexOf(
            "public bool OutsideDropletMergeEnabled",
            propertyStart,
            StringComparison.Ordinal);
        True(propertyStart >= 0 && propertyEnd >= propertyStart,
            "水滴合体プロパティを公開する必要があります。");
        var propertyAttributes = effectSource[propertyStart..propertyEnd];
        True(
            propertyAttributes.Contains("[ToggleSlider]", StringComparison.Ordinal) &&
            propertyAttributes.Contains(
                "[ShowPropertyEditorWhen(nameof(IsOutsideDropletMergeVisible), true)]",
                StringComparison.Ordinal) &&
            propertyAttributes.Contains(
                "[DefaultValue(GlassWipeDefaultSettings.OutsideDropletMergeEnabled)]",
                StringComparison.Ordinal),
            "水滴合体はモード別の表示条件へ接続し、新規追加時の既定値をオンにする必要があります。");
        True(
            effectSource.Contains(
                "AreOutsideDropletSettingsVisible && OutsideDropletFallEnabled",
                StringComparison.Ordinal),
            "落下設定の表示条件は水滴量と落下ONを両方確認する必要があります。");
        True(
            effectSource[propertyEnd..].Contains(
                "OnPropertyChanged(nameof(DetailedPresetExchange));",
                StringComparison.Ordinal),
            "水滴合体の変更時は詳細プリセット交換値を通知する必要があります。");

        var supportSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "YMM4GlassWipe",
            "GlassWipeVideoEffect.DetailedPresetSupport.cs"));
        True(
            supportSource.Contains(
                "OutsideDropletMergeEnabled = OutsideDropletMergeEnabled,",
                StringComparison.Ordinal) &&
            supportSource.Contains(
                "OutsideDropletMergeEnabled = merged.OutsideDropletMergeEnabled ??",
                StringComparison.Ordinal),
            "水滴合体は詳細プリセットの取得と適用に接続する必要があります。");
    }

    public static void VerifyCodecAndPresetCompatibility()
    {
        foreach (var enabled in new[] { false, true })
        {
            var state = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Smiley);
            state.OutsideDropletMergeEnabled = enabled;
            var encoded = DetailedWipePresetCodec.Encode(state);
            True(
                encoded.Contains(
                    $"\"outsideDropletMergeEnabled\":{enabled.ToString().ToLowerInvariant()}",
                    StringComparison.Ordinal),
                $"現行プリセットは水滴合体={enabled}を保存する必要があります。");
            True(
                DetailedWipePresetCodec.TryDecode(encoded, out var restored),
                $"水滴合体={enabled}を含む現行プリセットを復元できる必要があります。");
            Equal(
                enabled,
                restored.OutsideDropletMergeEnabled,
                $"現行プリセットの水滴合体={enabled}を往復保存する必要があります。");

            for (var legacyVersion = 1; legacyVersion <= 9; legacyVersion++)
            {
                var legacyState = JsonNode.Parse(encoded)!.AsObject();
                legacyState["dataSchemaVersion"] = legacyVersion;
                var legacy = legacyState.ToJsonString();
                True(
                    DetailedWipePresetCodec.TryDecode(legacy, out var migrated),
                    $"v{legacyVersion}プリセットを移行できる必要があります。");
                Equal(
                    false,
                    migrated.OutsideDropletMergeEnabled,
                    $"v{legacyVersion}の水滴合体は保存値にかかわらずオフへ移行する必要があります。");
            }
        }

        var current = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
        current.OutsideDropletMergeEnabled = true;
        var preset = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Smiley);
        preset.OutsideDropletMergeEnabled = false;
        foreach (var scope in new[]
                 {
                     DetailedWipePresetScope.Path,
                     DetailedWipePresetScope.Brush,
                 })
        {
            True(preset.TrySanitize(scope, out var scoped),
                $"{scope}プリセットを正規化できる必要があります。");
            True(scoped.OutsideDropletMergeEnabled is null,
                $"{scope}プリセットへ水滴合体を保存してはいけません。");
            True(DetailedWipePresetStateMerger.TryMerge(
                    current, preset, scope, out var merged),
                $"{scope}プリセットを統合できる必要があります。");
            Equal(
                true,
                merged.OutsideDropletMergeEnabled,
                $"{scope}の適用では現在の水滴合体値を保持する必要があります。");
        }

        True(preset.TrySanitize(DetailedWipePresetScope.All, out var allScoped),
            "全設定プリセットを正規化できる必要があります。");
        Equal(
            false,
            allScoped.OutsideDropletMergeEnabled,
            "全設定プリセットは水滴合体を保存する必要があります。");
        True(DetailedWipePresetStateMerger.TryMerge(
                current, preset, DetailedWipePresetScope.All, out var allMerged),
            "全設定プリセットを統合できる必要があります。");
        Equal(
            false,
            allMerged.OutsideDropletMergeEnabled,
            "全設定の適用では水滴合体を復元する必要があります。");

        var exchange = DetailedWipePresetExchangeCodec.EncodeSnapshot(
            WipePathInputMode.StrokeCollection,
            current);
        var legacyExchangeState = JsonNode.Parse(exchange)!.AsObject();
        legacyExchangeState["state"]!["dataSchemaVersion"] = 9;
        var legacyExchange = legacyExchangeState.ToJsonString();
        True(
            DetailedWipePresetExchangeCodec.TryDecode(legacyExchange, out var migratedExchange),
            "v9の交換用Stateを移行できる必要があります。");
        Equal(
            false,
            migratedExchange.State.OutsideDropletMergeEnabled,
            "v9の交換用Stateの水滴合体はオフへ移行する必要があります。");
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
