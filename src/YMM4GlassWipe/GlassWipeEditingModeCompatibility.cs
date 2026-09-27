// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

/// <summary>
/// 編集モードの新規作成時と旧保存データ復元時の互換規則を提供します。
/// </summary>
internal static class GlassWipeEditingModeCompatibility
{
    public const GlassWipeEditingMode NewEffectDefault = GlassWipeEditingMode.Simple;

    public static GlassWipeEditingMode ResolveAfterDeserialization(
        bool editingModeWasSet,
        GlassWipeEditingMode editingMode)
    {
        return editingModeWasSet
            ? Normalize(editingMode)
            : GlassWipeEditingMode.Detailed;
    }

    public static GlassWipeEditingMode Normalize(GlassWipeEditingMode editingMode)
    {
        return editingMode is GlassWipeEditingMode.Simple or GlassWipeEditingMode.Detailed
            ? editingMode
            : GlassWipeEditingMode.Detailed;
    }
}
