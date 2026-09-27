// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel.DataAnnotations;

namespace YMM4GlassWipe;

internal enum WipePathInputMode
{
    [Display(Name = "キーフレーム", Description = "従来のX、Y、接触率のキーフレームを使用する")]
    LegacyAnimation = 0,

    [Display(Name = "カスタム軌跡", Description = "ストローク編集で作成した複数の軌跡を使用する")]
    StrokeCollection = 1,

    [Display(Name = "定型軌跡", Description = "標準設定と同じ軌跡をそのまま使用する")]
    SimpleGenerated = 2,
}

internal static class WipePathInputModeCompatibility
{
    public const WipePathInputMode NewEffectDefault = WipePathInputMode.SimpleGenerated;

    public static WipePathInputMode ResolveAfterDeserialization(
        bool pathInputModeWasSet,
        WipePathInputMode pathInputMode,
        GlassWipeEditingMode editingMode)
    {
        if (pathInputModeWasSet)
        {
            return Normalize(pathInputMode);
        }

        return editingMode == GlassWipeEditingMode.Simple
            ? WipePathInputMode.SimpleGenerated
            : WipePathInputMode.LegacyAnimation;
    }

    public static WipePathInputMode Normalize(WipePathInputMode pathInputMode)
    {
        return pathInputMode is
            WipePathInputMode.LegacyAnimation or
            WipePathInputMode.StrokeCollection or
            WipePathInputMode.SimpleGenerated
                ? pathInputMode
                : WipePathInputMode.LegacyAnimation;
    }

    public static bool UsesSimpleGenerator(
        GlassWipeEditingMode editingMode,
        WipePathInputMode pathInputMode) =>
        editingMode == GlassWipeEditingMode.Simple ||
        Normalize(pathInputMode) == WipePathInputMode.SimpleGenerated;
}
