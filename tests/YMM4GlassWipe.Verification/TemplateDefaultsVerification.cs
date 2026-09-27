// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using YMM4GlassWipe;
using YukkuriMovieMaker.Commons;
using YmmJson = YukkuriMovieMaker.Json.Json;

namespace YMM4GlassWipe.Verification;

internal static class TemplateDefaultsVerification
{
    public static void VerifyTemplateDefaults()
    {
        var fixture = ReadFixture("TemplateDefaultEffect.json");
        Equal(95, fixture.Count, "テンプレートの比較対象は95設定です。");
        var expected = Load(fixture);
        var actual = new GlassWipeVideoEffect();
        Compare(expected, actual, fixture.Select(p => p.Key), "新規追加");
        Equal(90d, actual.FogAmount.DefaultValue, "曇り量のリセット値");
        Equal(10d, actual.Blur.DefaultValue, "ぼかし量のリセット値");
        Equal(500d, actual.CircleDiameterPixels.DefaultValue, "円の直径リセット値");
        Equal(25d, actual.EllipseWidthPixels.DefaultValue, "楕円の幅リセット値");
        Equal(50d, actual.EllipseHeightPixels.DefaultValue, "楕円の高さリセット値");
        Equal(100d, actual.RectangleWidthPixels.DefaultValue, "四角形の幅リセット値");
        Equal(620d, actual.RectangleHeightPixels.DefaultValue, "四角形の高さリセット値");
        Equal(50d, actual.PngBrushSizeScale.DefaultValue, "PNGブラシのサイズ倍率リセット値");
        Equal(-30d, actual.SimpleGeneratedBrushRotation.DefaultValue, "標準ブラシ回転のリセット値");
    }

    public static void VerifyLegacyDefaults()
    {
        // 旧版DLLから別プロセスで取得した85個の既定値を固定する。
        var legacy = ReadFixture("LegacyEffectDefaults.json");
        Equal(85, legacy.Count, "旧DLLとの比較対象は85設定です。");
        var omitted = Load(new JsonObject());
        CompareLegacy(legacy, omitted, []);
        Equal(100d, omitted.PngBrushSizeScale.GetValue(0, 100, 60), "PNG倍率の欠落値は旧100%を維持");

        var partial = Load(CreatePartial());
        CompareLegacy(legacy, partial, ["PathStart", "OutsideDropletAmount", "OutsideDropletFallEnabled", "FogAmount"]);
        Equal(0d, partial.PathStart, "明示した開始位置0");
        Equal(0d, partial.OutsideDropletAmount, "明示した水滴量0");
        Equal(false, partial.OutsideDropletFallEnabled, "明示した落下OFF");
        Equal(0d, partial.FogAmount.GetValue(0, 100, 60), "明示Animationの先頭");
        Equal(100d, partial.FogAmount.GetValue(100, 100, 60), "明示Animationの終端");
    }

    public static void VerifyRoundTrips()
    {
        var fixture = ReadFixture("TemplateDefaultEffect.json");
        var current = new GlassWipeVideoEffect();
        Compare(current, YmmJson.GetClone(current), fixture.Select(p => p.Key), "新規値の保存往復");

        // 初期値の変更前に明示保存した値は、新しい初期値で置き換えない。
        var previous = Load(new JsonObject
        {
            ["PathTimingMode"] = (int)WipePathTimingMode.Percent,
            ["PathStartSeconds"] = 0d,
            ["PathCompletionSeconds"] = 1d,
            ["OutsideDropletStrength"] = 70d,
            ["OutsideDropletFallSpeed"] = 100d,
            ["BrushMirror"] = false,
            ["FogAmount"] = new JsonObject { ["Values"] = new JsonArray(new JsonObject { ["Value"] = 90d }) },
            ["PngBrushSizeScale"] = new JsonObject { ["Values"] = new JsonArray(new JsonObject { ["Value"] = 100d }) },
        });
        Equal(WipePathTimingMode.Percent, previous.PathTimingMode, "保存済みの割合方式");
        Equal(0d, previous.PathStartSeconds, "保存済みの開始秒");
        Equal(1d, previous.PathCompletionSeconds, "保存済みの完了秒");
        Equal(70d, previous.OutsideDropletStrength, "保存済みの水滴の濃さ");
        Equal(100d, previous.OutsideDropletFallSpeed, "保存済みの垂れる速さ");
        Equal(false, previous.BrushMirror, "保存済みの反転OFF");
        Equal(90d, previous.FogAmount.GetValue(0, 100, 60), "保存済みの曇り量");
        Equal(100d, previous.PngBrushSizeScale.GetValue(0, 100, 60), "保存済みのPNG倍率");
        Compare(previous, YmmJson.GetClone(previous), fixture.Select(p => p.Key), "変更前の明示保存値の往復");

        var partial = Load(CreatePartial());
        Compare(partial, YmmJson.GetClone(partial), fixture.Select(p => p.Key), "省略・明示値の保存往復");
        var zeros = Load(fixture);
        zeros.OutsideDropletFallEnabled = false;
        zeros.OutsideDropletMergeEnabled = false;
        zeros.OutsideDropletRainEnabled = false;
        zeros.OutsideDropletAmount = 0;
        zeros.OutsideDropletStrength = 0;
        zeros.OutsideDropletOutlineOpacity = 0;
        zeros.OutsideDropletRainStartSeconds = 0;
        zeros.OutsideDropletRainDurationSeconds = 0;
        Compare(zeros, YmmJson.GetClone(zeros), fixture.Select(p => p.Key), "OFFと0の保存往復");
    }

    public static void VerifyLabelsAndResetValues()
    {
        var labels = new Dictionary<string, string>
        {
            ["OutsideDropletOutlineOpacity"] = "輪郭の濃さ",
            ["OutsideDropletDeformWithSurface"] = "面に合わせる",
            ["OutsideDropletMergeEnabled"] = "水滴の合体",
            ["OutsideDropletRainEnabled"] = "雨の降り始め",
            ["OutsideDropletRainDurationSeconds"] = "出現期間",
            ["OutsideDropletFallingRatio"] = "垂れる割合",
            ["OutsideDropletFallFrequency"] = "垂れる頻度",
            ["CircleDiameterPixels"] = "直径",
            ["EllipseWidthPixels"] = "幅",
            ["EllipseHeightPixels"] = "高さ",
            ["RectangleWidthPixels"] = "幅",
            ["RectangleHeightPixels"] = "高さ",
            ["PngBrushSizeScale"] = "サイズ倍率",
        };
        foreach (var (name, label) in labels)
        {
            var display = Property(name).GetCustomAttribute<DisplayAttribute>();
            Equal(label, display?.Name, name + "の表示名");
            if (string.IsNullOrWhiteSpace(display?.Description))
                throw new InvalidOperationException(name + "のツールチップがありません。");
        }

        var expected = Load(ReadFixture("TemplateDefaultEffect.json"));
        string[] changed = ["PathTimingMode", "PathStartSeconds", "PathCompletionSeconds", "SimpleReturnOffsetX", "OutsideDropletFallSpeed", "BrushMirror",
            "PathStart", "PathCompletion", "SimplePattern", "SimpleStartX", "SimpleStartY",
            "SimpleControlY", "SimpleEndX", "SimpleEndY", "SimpleRoundTrips", "SimpleWipeAmountPerPass",
            "SimpleReturnOffset", "TintMix", "OutsideDropletAmount", "OutsideDropletSize", "OutsideDropletStrength", "OutsideDropletSeed",
            "OutsideDropletOutlineOpacity", "OutsideDropletFallEnabled", "OutsideDropletMergeEnabled", "OutsideDropletRainEnabled",
            "OutsideDropletRainStartSeconds", "OutsideDropletRainDurationSeconds", "OutsideDropletFallingRatio", "OutsideDropletFallFrequency",
            "OutsideDropletTrailLength", "OutsideDropletAppearance", "OutsideDropletMotionMode", "OutsideDropletSlip", "OutsideDropletSupply", "FogNoise", "RegionShape", "RegionFeather", "BrushShape",
            "SimpleGeneratedBrushRotationFollow"];
        foreach (var name in changed)
        {
            var property = Property(name);
            var attribute = property.GetCustomAttribute<DefaultValueAttribute>() ??
                throw new InvalidOperationException(name + "のDefaultValue属性がありません。");
            Equal(property.GetValue(expected), attribute.Value, name + "のリセット属性");
        }
    }

    private static JsonObject CreatePartial()
    {
        var animation = ReadFixture("TemplateDefaultEffect.json")["SimpleProgress"]!.DeepClone().AsObject();
        animation["Span"] = 2.5;
        animation["Bezier"]!["Points"]![0]!["ControlPoint1"]!["X"] = -0.2;
        return new JsonObject
        {
            ["PathStart"] = 0d,
            ["OutsideDropletAmount"] = 0d,
            ["OutsideDropletFallEnabled"] = false,
            ["FogAmount"] = animation,
        };
    }

    private static GlassWipeVideoEffect Load(JsonObject json) =>
        YmmJson.LoadFromText<GlassWipeVideoEffect>(json.ToJsonString()) ??
            throw new InvalidOperationException("YMM4の実保存処理で設定を復元できません。");

    private static void CompareLegacy(JsonObject expected, GlassWipeVideoEffect actual, string[] exceptions)
    {
        foreach (var (name, value) in expected)
        {
            if (exceptions.Contains(name) || IsRetiredLegacyProperty(name)) continue;
            var current = Property(name).GetValue(actual);
            var canonical = current is Animation animation
                ? "Animation:" + animation.GetValue(0, 100, 60).ToString("R", CultureInfo.InvariantCulture)
                : Scalar(current);
            Equal(value!.GetValue<string>(), canonical, "旧欠落値: " + name);
        }
    }

    private static void Compare(GlassWipeVideoEffect expected, GlassWipeVideoEffect? actual, IEnumerable<string> names, string context)
    {
        ArgumentNullException.ThrowIfNull(actual);
        foreach (var name in names)
        {
            var property = Property(name);
            var before = property.GetValue(expected);
            var after = property.GetValue(actual);
            if (before is Animation first && after is Animation second)
            {
                // 全Values、Span、種別、Bezierを比較し、時刻0だけの一致に限定しない。
                var a = JsonNode.Parse(JsonConvert.SerializeObject(first))!.AsObject();
                var b = JsonNode.Parse(JsonConvert.SerializeObject(second))!.AsObject();
                foreach (var key in new[] { "Values", "Span", "AnimationType", "Bezier" })
                {
                    if (!a.ContainsKey(key) || !b.ContainsKey(key) || !JsonNode.DeepEquals(a[key], b[key]))
                        throw new InvalidOperationException(context + ": " + name + "." + key + "が一致しません。");
                }
            }
            else Equal(Scalar(before), Scalar(after), context + ": " + name);
        }
    }

    private static string Scalar(object? value) => value switch
    {
        null => "<null>",
        IFormattable item => item.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static PropertyInfo Property(string name) => typeof(GlassWipeVideoEffect).GetProperty(name) ??
        throw new InvalidOperationException("保存プロパティがありません: " + name);

    private static bool IsRetiredLegacyProperty(string name) => name is
        "SimpleGeneratedBrushSizeScale" or "SimpleGeneratedBrushAspectRatioScale" or
        "BrushSize" or "BrushAspectRatio" or "BrushShape" or "CustomPathData";

    private static JsonObject ReadFixture(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "YMM4GlassWipe.sln")))
                return JsonNode.Parse(File.ReadAllText(Path.Combine(directory.FullName, "tests", "YMM4GlassWipe.Verification", "Fixtures", name)))!.AsObject();
        }
        throw new DirectoryNotFoundException("検証リポジトリが見つかりません。");
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: Expected={expected}, Actual={actual}");
    }
}
