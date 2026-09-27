// SPDX-License-Identifier: MPL-2.0

using System.Text.Json;
using YMM4GlassWipe;

internal static class OutsideDropletAppearanceVerification
{
    public static void VerifySettingsAndPersistence()
    {
        Equal(
            global::YMM4GlassWipe.OutsideDropletAppearance.Transparent,
            OutsideDropletAppearanceCompatibility.Default,
            "水滴の見た目の既定値は黒い輪郭である必要があります。");
        Equal(
            global::YMM4GlassWipe.OutsideDropletAppearance.Transparent,
            OutsideDropletAppearanceCompatibility.Normalize(
                global::YMM4GlassWipe.OutsideDropletAppearance.Transparent),
            "透明表示を選択できる必要があります。");
        Equal(
            global::YMM4GlassWipe.OutsideDropletAppearance.Transparent,
            OutsideDropletAppearanceCompatibility.Normalize(
                (global::YMM4GlassWipe.OutsideDropletAppearance)99),
            "不正な水滴の見た目は新規既定値へ補正する必要があります。");
        var fresh = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
        Equal(
            global::YMM4GlassWipe.OutsideDropletAppearance.Transparent,
            fresh.OutsideDropletAppearance,
            "新規の全設定プリセットは黒い輪郭を既定値にする必要があります。");

        var transparent = DetailedWipePresetFactory.Create(
            DetailedWipeBuiltInPreset.Heart);
        transparent.OutsideDropletAppearance =
            global::YMM4GlassWipe.OutsideDropletAppearance.Transparent;
        var encoded = DetailedWipePresetCodec.Encode(transparent);
        True(
            encoded.Contains("\"outsideDropletAppearance\":1", StringComparison.Ordinal),
            "全設定プリセットは透明表示の選択値を保存する必要があります。");
        True(
            DetailedWipePresetCodec.TryDecode(encoded, out var restored),
            "透明表示を含むプリセットを復元できる必要があります。");
        Equal(
            global::YMM4GlassWipe.OutsideDropletAppearance.Transparent,
            restored.OutsideDropletAppearance,
            "透明表示の選択値を往復保存する必要があります。");

        var legacy = encoded.Replace(
            "\"outsideDropletAppearance\":1",
            "\"outsideDropletAppearance\":0",
            StringComparison.Ordinal);
        True(
            DetailedWipePresetCodec.TryDecode(legacy, out var legacyRestored),
            "白い輪郭を含む既存プリセットを復元できる必要があります。");
        Equal(
            global::YMM4GlassWipe.OutsideDropletAppearance.Legacy,
            legacyRestored.OutsideDropletAppearance,
            "既存プリセットの明示的な白い輪郭を保持する必要があります。");

        var missing = RemoveJsonProperty(encoded, "outsideDropletAppearance");
        True(
            DetailedWipePresetCodec.TryDecode(missing, out var missingRestored),
            "水滴の見た目が欠落したプリセットを復元できる必要があります。");
        Equal(
            global::YMM4GlassWipe.OutsideDropletAppearance.Transparent,
            missingRestored.OutsideDropletAppearance,
            "v9の欠落した水滴の見た目は新規既定値にする必要があります。");

        var invalid = encoded.Replace(
            "\"outsideDropletAppearance\":1",
            "\"outsideDropletAppearance\":99",
            StringComparison.Ordinal);
        True(
            DetailedWipePresetCodec.TryDecode(invalid, out var invalidRestored),
            "不正な水滴の見た目を含むプリセットを安全に復元できる必要があります。");
        Equal(
            global::YMM4GlassWipe.OutsideDropletAppearance.Transparent,
            invalidRestored.OutsideDropletAppearance,
            "不正な保存値は新規既定値へ補正する必要があります。");


        Equal(
            global::YMM4GlassWipe.OutsideDropletAppearance.Legacy,
            OutsideDropletAppearanceCompatibility.ResolveAfterDeserialization(
                false,
                global::YMM4GlassWipe.OutsideDropletAppearance.Transparent),
            "水滴見た目を持たない旧Effectは白い輪郭へ移行する必要があります。");
        Equal(
            global::YMM4GlassWipe.OutsideDropletAppearance.Legacy,
            OutsideDropletAppearanceCompatibility.ResolveAfterDeserialization(
                true,
                global::YMM4GlassWipe.OutsideDropletAppearance.Legacy),
            "明示保存された白い輪郭はEffect復元後も保持する必要があります。");
        Equal(
            global::YMM4GlassWipe.OutsideDropletAppearance.Transparent,
            OutsideDropletAppearanceCompatibility.ResolveAfterDeserialization(
                true,
                global::YMM4GlassWipe.OutsideDropletAppearance.Transparent),
            "明示保存された黒い輪郭はEffect復元後も保持する必要があります。");

        var effectSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "YMM4GlassWipe", "GlassWipeVideoEffect.cs"));
        var propertyStart = effectSource.IndexOf(
            "[Display(GroupName = \"水滴\", Name = \"水滴の見た目\"",
            StringComparison.Ordinal);
        var propertyEnd = effectSource.IndexOf(
            "public OutsideDropletAppearanceValue OutsideDropletAppearance",
            propertyStart,
            StringComparison.Ordinal);
        True(propertyStart >= 0 && propertyEnd >= propertyStart,
            "水滴の見た目プロパティを公開する必要があります。");
        var propertyAttributes = effectSource[propertyStart..propertyEnd];
        True(
            propertyAttributes.Contains("[DefaultValue(GlassWipeDefaultSettings.OutsideDropletAppearance)]", StringComparison.Ordinal) &&
            propertyAttributes.Contains("[JsonIgnore(Condition = JsonIgnoreCondition.Never)]", StringComparison.Ordinal) &&
            propertyAttributes.Contains("白い輪郭、黒い輪郭、リアル（屈折）", StringComparison.Ordinal),
            "水滴の見た目はテンプレートの既定値を使い、白い輪郭、黒い輪郭、リアルを表示して保存する必要があります。");
        True(
            effectSource.Contains("_outsideDropletAppearanceWasSet = true;", StringComparison.Ordinal) &&
            effectSource.Contains("OutsideDropletAppearanceCompatibility.ResolveAfterDeserialization(", StringComparison.Ordinal),
            "Effect本体は設定有無で旧欠落値と明示保存値を区別する必要があります。");
        True(
            propertyAttributes.Contains("[EnumComboBox]", StringComparison.Ordinal) &&
            propertyAttributes.Contains(
                "[ShowPropertyEditorWhen(nameof(AreOutsideDropletSettingsVisible), true)]",
                StringComparison.Ordinal),
            "水滴量に連動して水滴の見た目をEnumComboBoxで表示する必要があります。");
    }
    public static void VerifyPresetCompatibility()
    {
        var transparent = DetailedWipePresetFactory.Create(
            DetailedWipeBuiltInPreset.Smiley);
        transparent.OutsideDropletAppearance =
            global::YMM4GlassWipe.OutsideDropletAppearance.Transparent;
        transparent.OutsideDropletAmount = 77;
        transparent.OutsideDropletSize = 175;
        transparent.OutsideDropletStrength = 45;
        transparent.OutsideDropletSeed = 17;
        transparent.OutsideDropletDeformWithSurface = false;
        transparent.OutsideDropletFallEnabled = true;
        transparent.OutsideDropletFallingRatio = 55;
        transparent.OutsideDropletFallSpeed = 150;
        transparent.OutsideDropletTrailLength = 65;
        var encoded = DetailedWipePresetCodec.Encode(transparent);

        for (var legacyVersion = 1; legacyVersion <= 8; legacyVersion++)
        {
            var legacy = encoded.Replace(
                $"\"dataSchemaVersion\":{DetailedWipePresetState.CurrentVersion}",
                $"\"dataSchemaVersion\":{legacyVersion}",
                StringComparison.Ordinal);
            True(
                DetailedWipePresetCodec.TryDecode(legacy, out var migrated),
                $"v{legacyVersion}プリセットを移行できる必要があります。");
            Equal(
                global::YMM4GlassWipe.OutsideDropletAppearance.Legacy,
                migrated.OutsideDropletAppearance,
                $"v{legacyVersion}の水滴の見た目は選択値があっても従来表示へ移行する必要があります。");
            if (legacyVersion == 8)
            {
                Equal(77d, migrated.OutsideDropletAmount, "v8の水滴量を保持する必要があります。");
                Equal(175d, migrated.OutsideDropletSize, "v8の水滴サイズを保持する必要があります。");
                Equal(45d, migrated.OutsideDropletStrength, "v8の水滴濃さを保持する必要があります。");
                Equal(17d, migrated.OutsideDropletSeed, "v8の水滴Seedを保持する必要があります。");
                Equal(false, migrated.OutsideDropletDeformWithSurface, "v8の面変形を保持する必要があります。");
                Equal(true, migrated.OutsideDropletFallEnabled, "v8の落下ON/OFFを保持する必要があります。");
                Equal(55d, migrated.OutsideDropletFallingRatio, "v8の落下割合を保持する必要があります。");
                Equal(150d, migrated.OutsideDropletFallSpeed, "v8の落下速度を保持する必要があります。");
                Equal(65d, migrated.OutsideDropletTrailLength, "v8の水筋長さを保持する必要があります。");
            }
        }

        var exchange = DetailedWipePresetExchangeCodec.EncodeSnapshot(
            WipePathInputMode.StrokeCollection,
            transparent);
        var legacyExchange = exchange.Replace(
            $"\"dataSchemaVersion\":{DetailedWipePresetState.CurrentVersion}",
            "\"dataSchemaVersion\":8",
            StringComparison.Ordinal);
        True(
            DetailedWipePresetExchangeCodec.TryDecode(legacyExchange, out var migratedExchange),
            "v8の交換用Stateを移行できる必要があります。");
        Equal(
            global::YMM4GlassWipe.OutsideDropletAppearance.Legacy,
            migratedExchange.State.OutsideDropletAppearance,
            "v8の交換用Stateの水滴見た目は白い輪郭へ移行する必要があります。");

        var future = encoded.Replace(
            $"\"dataSchemaVersion\":{DetailedWipePresetState.CurrentVersion}",
            $"\"dataSchemaVersion\":{DetailedWipePresetState.CurrentVersion + 1}",
            StringComparison.Ordinal);
        True(
            !DetailedWipePresetCodec.TryDecode(future, out _),
            "未来のプリセットStateは拒否する必要があります。");

        var current = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
        current.OutsideDropletAppearance =
            global::YMM4GlassWipe.OutsideDropletAppearance.Legacy;
        foreach (var scope in new[]
                 {
                     DetailedWipePresetScope.Path,
                     DetailedWipePresetScope.Brush,
                 })
        {
            True(transparent.TrySanitize(scope, out var scoped),
                $"{scope}プリセットを正規化できる必要があります。");
            True(scoped.OutsideDropletAppearance is null,
                $"{scope}プリセットへ水滴の見た目を保存してはいけません。");
            True(DetailedWipePresetStateMerger.TryMerge(
                    current, transparent, scope, out var merged),
                $"{scope}プリセットを統合できる必要があります。");
            Equal(
                global::YMM4GlassWipe.OutsideDropletAppearance.Legacy,
                merged.OutsideDropletAppearance,
                $"{scope}の適用では現在の水滴の見た目を保持する必要があります。");
        }

        True(transparent.TrySanitize(DetailedWipePresetScope.All, out var allScoped),
            "全設定プリセットを正規化できる必要があります。");
        Equal(
            global::YMM4GlassWipe.OutsideDropletAppearance.Transparent,
            allScoped.OutsideDropletAppearance,
            "全設定プリセットは水滴の見た目を保存する必要があります。");
        True(DetailedWipePresetStateMerger.TryMerge(
                current, transparent, DetailedWipePresetScope.All, out var allMerged),
            "全設定プリセットを統合できる必要があります。");
        Equal(
            global::YMM4GlassWipe.OutsideDropletAppearance.Transparent,
            allMerged.OutsideDropletAppearance,
            "全設定の適用では水滴の見た目を復元する必要があります。");
        Equal(
            global::YMM4GlassWipe.OutsideDropletAppearance.Legacy,
            OutsideDropletAppearanceCompatibility.LegacyMigrationDefault,
            "v1からv8の移行既定値は白い輪郭である必要があります。");
    }

    public static void VerifyTemporalInvalidation()
    {
        var firstFrame = default(GlassWipeParameters) with
        {
            OutsideDropletAppearance = 0,
            OutsideDropletLocalTimeSeconds = 1,
        };
        var nextFrame = firstFrame with { OutsideDropletLocalTimeSeconds = 2 };
        True(
            firstFrame.HasSameNonTemporalValues(nextFrame),
            "時刻だけが異なる場合は水滴の見た目を含む定数を再適用してはいけません。");
        True(
            !firstFrame.HasSameNonTemporalValues(nextFrame with
            {
                OutsideDropletAppearance = 1,
            }),
            "水滴の見た目が異なる場合は全定数を再適用する必要があります。");
    }

    private static string RemoveJsonProperty(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, propertyName, StringComparison.Ordinal))
                {
                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
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
