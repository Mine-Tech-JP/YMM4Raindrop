// SPDX-License-Identifier: MPL-2.0

using System.Text.Json.Nodes;
using YMM4GlassWipe;

internal static class OutsideDropletRainSettingsVerification
{
    public static void VerifySettingsAndPresetCompatibility()
    {
        Equal(16, DetailedWipePresetState.CurrentVersion, "簡易物理を含む現行Stateはv16である必要があります。");
        var fresh = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
        Equal(false, fresh.OutsideDropletRainEnabled, "内蔵プリセットの雨の既定値はOFFである必要があります。");
        Equal(0d, fresh.OutsideDropletRainStartSeconds, "内蔵プリセットの雨の開始既定値は0秒である必要があります。");
        Equal(5d, fresh.OutsideDropletRainDurationSeconds, "内蔵プリセットの雨の出現時間既定値は5秒である必要があります。");

        fresh.OutsideDropletRainEnabled = true;
        fresh.OutsideDropletRainStartSeconds = 1.25;
        fresh.OutsideDropletRainDurationSeconds = 6.5;
        var encoded = DetailedWipePresetCodec.Encode(fresh);
        True(DetailedWipePresetCodec.TryDecode(encoded, out var restored), "現行の雨設定を復元できる必要があります。");
        Equal(true, restored.OutsideDropletRainEnabled, "雨の有効値を往復保存する必要があります。");
        Equal(1.25d, restored.OutsideDropletRainStartSeconds, "雨の開始秒を往復保存する必要があります。");
        Equal(6.5d, restored.OutsideDropletRainDurationSeconds, "雨の出現秒を往復保存する必要があります。");

        for (var version = 1; version <= 11; version++)
        {
            var legacy = JsonNode.Parse(encoded)!.AsObject();
            legacy["dataSchemaVersion"] = version;
            True(legacy.Remove("outsideDropletRainEnabled"), "旧データ試験では新キーを実際に取り除きます。");
            True(legacy.Remove("outsideDropletRainStartSeconds"), "旧データ試験では新キーを実際に取り除きます。");
            True(legacy.Remove("outsideDropletRainDurationSeconds"), "旧データ試験では新キーを実際に取り除きます。");
            True(DetailedWipePresetCodec.TryDecode(legacy.ToJsonString(), out var migrated), $"v{version}を移行できる必要があります。");
            Equal(false, migrated.OutsideDropletRainEnabled, $"v{version}の雨はOFFへ移行する必要があります。");
            Equal(0d, migrated.OutsideDropletRainStartSeconds, $"v{version}の開始秒は0へ移行する必要があります。");
            Equal(5d, migrated.OutsideDropletRainDurationSeconds, $"v{version}の出現秒は5へ移行する必要があります。");
        }

        var current = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
        current.OutsideDropletRainEnabled = true;
        current.OutsideDropletRainStartSeconds = 2;
        current.OutsideDropletRainDurationSeconds = 7;
        foreach (var scope in new[] { DetailedWipePresetScope.Path, DetailedWipePresetScope.Brush })
        {
            True(fresh.TrySanitize(scope, out var scoped), $"{scope}を正規化できる必要があります。");
            True(scoped.OutsideDropletRainEnabled is null && scoped.OutsideDropletRainStartSeconds is null && scoped.OutsideDropletRainDurationSeconds is null, $"{scope}へ雨設定を保存してはいけません。");
            True(DetailedWipePresetStateMerger.TryMerge(current, fresh, scope, out var merged), $"{scope}を統合できる必要があります。");
            Equal(true, merged.OutsideDropletRainEnabled, $"{scope}適用は有効値を保持する必要があります。");
            Equal(2d, merged.OutsideDropletRainStartSeconds, $"{scope}適用は開始秒を保持する必要があります。");
            Equal(7d, merged.OutsideDropletRainDurationSeconds, $"{scope}適用は出現秒を保持する必要があります。");
        }

        var exchange = DetailedWipePresetExchangeCodec.EncodeSnapshot(WipePathInputMode.StrokeCollection, fresh);
        var node = JsonNode.Parse(exchange)!.AsObject();
        node["state"]!["dataSchemaVersion"] = 11;
        node["state"]!.AsObject().Remove("outsideDropletRainEnabled");
        node["state"]!.AsObject().Remove("outsideDropletRainStartSeconds");
        node["state"]!.AsObject().Remove("outsideDropletRainDurationSeconds");
        True(DetailedWipePresetExchangeCodec.TryDecode(node.ToJsonString(), out var oldExchange), "v11交換Stateを移行できる必要があります。");
        Equal(false, oldExchange.State.OutsideDropletRainEnabled, "v11交換Stateの雨はOFFへ移行する必要があります。");
        Equal(0d, oldExchange.State.OutsideDropletRainStartSeconds, "v11交換Stateの開始秒は0へ移行する必要があります。");
        Equal(5d, oldExchange.State.OutsideDropletRainDurationSeconds, "v11交換Stateの出現秒は5へ移行する必要があります。");

        // 破損軌跡を指定し、All失敗後のBrush経由の補完処理を実際に通す。
        node["state"]!["customPathData"] = "{invalid";
        True(DetailedWipePresetExchangeCodec.TryDecode(node.ToJsonString(), out var fallback),
            "軌跡が不正でもv11交換Stateの雨設定を移行できる必要があります。");
        True(!fallback.CanSavePath, "破損軌跡を保存可能と扱ってはいけません。");
        Equal(false, fallback.State.OutsideDropletRainEnabled, "fallbackの旧雨設定はOFFです。");
        Equal(0d, fallback.State.OutsideDropletRainStartSeconds, "fallbackの旧開始は0秒です。");
        Equal(5d, fallback.State.OutsideDropletRainDurationSeconds, "fallbackの旧期間は5秒です。");
        node["state"]!["dataSchemaVersion"] = 12;
        node["state"]!["outsideDropletRainEnabled"] = true;
        node["state"]!["outsideDropletRainStartSeconds"] = 1.25;
        node["state"]!["outsideDropletRainDurationSeconds"] = 6.5;
        True(DetailedWipePresetExchangeCodec.TryDecode(node.ToJsonString(), out fallback),
            "軌跡不正時も現行の雨設定を保持できる必要があります。");
        Equal(true, fallback.State.OutsideDropletRainEnabled, "fallbackの現行有効値を保持します。");
        Equal(1.25d, fallback.State.OutsideDropletRainStartSeconds, "fallbackの現行開始秒を保持します。");
        Equal(6.5d, fallback.State.OutsideDropletRainDurationSeconds, "fallbackの現行期間秒を保持します。");

        Equal(2f, GlassWipeParameters.ResolveOutsideDropletLocalTimeSeconds(false, true, 120, 60), "雨ONでは落下OFFでもアイテム内時刻を評価する必要があります。");
        Equal(0f, GlassWipeParameters.ResolveOutsideDropletLocalTimeSeconds(false, false, 120, 60), "落下と雨がOFFなら時刻は0である必要があります。");
    }

    public static void VerifyBoundsAndUiInvalidation()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Equal(0d, OutsideDropletSettings.SanitizeRainStartSeconds(invalid), "不正な開始秒は既定0へ戻します。");
            Equal(5d, OutsideDropletSettings.SanitizeRainDurationSeconds(invalid), "不正な期間は既定5へ戻します。");
        }
        Equal(0d, OutsideDropletSettings.SanitizeRainStartSeconds(-1), "開始秒の下限");
        Equal(0d, OutsideDropletSettings.SanitizeRainDurationSeconds(-1), "期間の下限");
        Equal(36000d, OutsideDropletSettings.SanitizeRainStartSeconds(36001), "開始秒の上限");
        Equal(36000d, OutsideDropletSettings.SanitizeRainDurationSeconds(36001), "期間の上限");
        var baseline = default(GlassWipeParameters);
        True(baseline.HasSameNonTemporalValues(baseline with { OutsideDropletLocalTimeSeconds = 1f }),
            "時刻だけの変化は既存の更新経路で扱います。");
        True(!baseline.HasSameNonTemporalValues(baseline with { OutsideDropletRainEnabled = 1f }) &&
            !baseline.HasSameNonTemporalValues(baseline with { OutsideDropletRainStartSeconds = 1f }) &&
            !baseline.HasSameNonTemporalValues(baseline with { OutsideDropletRainDurationSeconds = 1f }),
            "雨設定の変更は定数と予定表を再評価する必要があります。");
        var effect = new GlassWipeVideoEffect
        {
            OutsideDropletAmount = 0,
            OutsideDropletRainEnabled = false,
            OutsideDropletFallEnabled = false,
        };
        var notified = false;
        effect.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(GlassWipeVideoEffect.AreOutsideDropletRainTimingSettingsVisible))
                notified = true;
        };
        foreach (var amount in new[] { 70d, 0d })
        {
            notified = false;
            effect.OutsideDropletAmount = amount;
            True(notified, "水滴量の変更で雨の秒数欄も表示更新する必要があります。");
            foreach (var enabled in new[] { true, false })
            {
                notified = false;
                effect.OutsideDropletRainEnabled = enabled;
                True(notified, "雨ON/OFFの変更で秒数欄も表示更新する必要があります。");
                Equal(amount > 0 && enabled, effect.AreOutsideDropletRainTimingSettingsVisible,
                    "雨の秒数欄は水滴量と雨ONに従い、落下ONを必須にしてはいけません。");
            }
        }
    }

    private static void True(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Equal<T>(T expected, T actual, string message) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{message} 期待値: {expected}, 実際: {actual}"); }
}
