// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel.DataAnnotations;

namespace YMM4GlassWipe;

/// <summary>
/// 軌跡の編集方法を表します。数値0は旧保存データとの互換性のため詳細モードに割り当てます。
/// 新規エフェクトの既定値はGlassWipeEditingModeCompatibilityで標準モードに設定します。
/// </summary>
internal enum GlassWipeEditingMode
{
    [Display(Name = "詳細設定", Description = "制御点を使って軌跡を編集する")]
    Detailed = 0,

    [Display(Name = "標準設定", Description = "始点・経由点・終点から定型軌跡を作成する")]
    Simple = 1,
}
