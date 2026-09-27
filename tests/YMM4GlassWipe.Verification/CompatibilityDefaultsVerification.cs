// SPDX-License-Identifier: MPL-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using YMM4GlassWipe;
using YukkuriMovieMaker.Commons;
using YmmJson = YukkuriMovieMaker.Json.Json;

namespace YMM4GlassWipe.Verification;

internal static class CompatibilityDefaultsVerification
{
    // 本体の現在の既定値定数を参照せず、旧版の欠落補完値を固定する。
    private static readonly (string Name, double Value)[] BrushDefaults =
    [
        (nameof(GlassWipeVideoEffect.CircleDiameterPixels), 50),
        (nameof(GlassWipeVideoEffect.EllipseWidthPixels), 25),
        (nameof(GlassWipeVideoEffect.EllipseHeightPixels), 50),
        (nameof(GlassWipeVideoEffect.RectangleWidthPixels), 50),
        (nameof(GlassWipeVideoEffect.RectangleHeightPixels), 50),
        (nameof(GlassWipeVideoEffect.PngBrushSizeScale), 100),
    ];

    public static void VerifyEffectFallbacks()
    {
        foreach (var load in new Func<string, GlassWipeVideoEffect?>[]
                 {
                     json => JsonSerializer.Deserialize<GlassWipeVideoEffect>(json),
                     json => YmmJson.LoadFromText<GlassWipeVideoEffect>(json),
                 })
        {
            var omitted = load("{}") ?? throw new InvalidOperationException("欠落設定を復元できません。");
            foreach (var (name, value) in BrushDefaults)
                Equal(value, EffectAnimation(omitted, name).GetValue(0, 100, 60), name + "の欠落補完");
            Equal(15d, omitted.Blur.GetValue(0, 100, 60), "旧ぼかしの欠落補完");
        }

        // get-only Animationの明示値は、製品で使うYMM4の読込経路で検証する。
        foreach (var explicitValue in new[] { 0d, 125d })
        {
            var json = new JsonObject();
            foreach (var name in BrushDefaults.Select(item => item.Name).Append(nameof(GlassWipeVideoEffect.Blur)))
                json[name] = new JsonObject
                {
                    ["Values"] = new JsonArray(new JsonObject { ["Value"] = explicitValue }),
                };
            var restored = YmmJson.LoadFromText<GlassWipeVideoEffect>(json.ToJsonString()) ?? throw new InvalidOperationException("明示設定を復元できません。");
            var clone = YmmJson.GetClone(restored) ?? throw new InvalidOperationException("明示設定を再保存できません。");
            foreach (var name in BrushDefaults.Select(item => item.Name).Append(nameof(GlassWipeVideoEffect.Blur)))
            {
                // 保存値の保持を確認する。描画用Clampとは分ける。
                Equal(explicitValue, EffectAnimation(restored, name).Values[0].Value, name + "の明示値");
                Equal(explicitValue, EffectAnimation(clone, name).Values[0].Value, name + "の再保存値");
            }
        }
    }

    public static void VerifyPresetFallbacks()
    {
        for (var version = 1; version <= 15; version++)
        {
            var json = JsonNode.Parse(DetailedWipePresetCodec.Encode(new DetailedWipePresetState()))!.AsObject();
            json["dataSchemaVersion"] = version;
            foreach (var (name, _) in BrushDefaults)
                json.Remove(JsonNamingPolicy.CamelCase.ConvertName(name));
            Check(DetailedWipePresetCodec.TryDecode(json.ToJsonString(), out var omitted), $"v{version}の欠落設定を復元");
            foreach (var (name, value) in BrushDefaults)
                CheckAnimation(omitted, name, value, $"v{version}の欠落補完");

            foreach (var (name, _) in BrushDefaults)
                json[JsonNamingPolicy.CamelCase.ConvertName(name)] = new JsonObject
                {
                    ["from"] = 125d, ["to"] = 125d, ["animationType"] = 0,
                };
            Check(DetailedWipePresetCodec.TryDecode(json.ToJsonString(), out var explicitState), $"v{version}の明示値を復元");
            foreach (var (name, _) in BrushDefaults)
                CheckAnimation(explicitState, name,
                    name == nameof(GlassWipeVideoEffect.PngBrushSizeScale) && version < 15 ? 100 : 125,
                    $"v{version}の明示値と版移行");
        }

        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var state = new DetailedWipePresetState();
            foreach (var (name, _) in BrushDefaults)
            {
                var animation = PresetAnimation(state, name);
                animation.From = animation.To = invalid;
            }
            Check(state.TrySanitize(out var sanitized), "非有限の寸法を補正");
            foreach (var (name, value) in BrushDefaults)
                CheckAnimation(sanitized, name, value, "非有限値の補完");
        }

        // nullは欠落と異なり、空Animationの0をClampする既存動作を維持する。
        var nullJson = JsonNode.Parse(DetailedWipePresetCodec.Encode(new DetailedWipePresetState()))!.AsObject();
        foreach (var (name, _) in BrushDefaults)
            nullJson[JsonNamingPolicy.CamelCase.ConvertName(name)] = null;
        Check(DetailedWipePresetCodec.TryDecode(nullJson.ToJsonString(), out var nullState), "nullの寸法を補正");
        foreach (var (name, _) in BrushDefaults)
            CheckAnimation(nullState, name, name == nameof(GlassWipeVideoEffect.PngBrushSizeScale) ? 25 : 0, "nullの補正");

        foreach (var (preset, diameter) in new[]
                 {
                     (DetailedWipeBuiltInPreset.Heart, 86d),
                     (DetailedWipeBuiltInPreset.LoveUmbrella, 76d),
                     (DetailedWipeBuiltInPreset.Smiley, 76d),
                 })
        {
            var state = DetailedWipePresetFactory.Create(preset);
            Check(DetailedWipePresetCodec.TryDecode(DetailedWipePresetCodec.Encode(state), out var restored), "内蔵プリセットを往復保存");
            foreach (var (name, value) in BrushDefaults)
            {
                var expected = name == nameof(GlassWipeVideoEffect.CircleDiameterPixels) ? diameter : value;
                CheckAnimation(state, name, expected, preset + "の初期値");
                CheckAnimation(restored, name, expected, preset + "の保存値");
            }
        }
    }

    private static Animation EffectAnimation(GlassWipeVideoEffect effect, string name) =>
        (Animation)typeof(GlassWipeVideoEffect).GetProperty(name)!.GetValue(effect)!;

    private static DetailedWipeAnimationState PresetAnimation(DetailedWipePresetState state, string name) =>
        (DetailedWipeAnimationState)typeof(DetailedWipePresetState).GetProperty(name)!.GetValue(state)!;

    private static void CheckAnimation(DetailedWipePresetState state, string name, double value, string context)
    {
        var animation = PresetAnimation(state, name);
        Equal(value, animation.From, context + ": " + name + ".From");
        Equal(value, animation.To, context + ": " + name + ".To");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal(double expected, double actual, string message)
    {
        if (expected != actual) throw new InvalidOperationException($"{message}: 期待値={expected}, 実際={actual}");
    }
}
