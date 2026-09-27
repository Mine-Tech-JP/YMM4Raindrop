// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe.Verification;

public static class PresetTooltipVerification
{
    public static void VerifyDetailedPresetTooltip()
    {
        const string expectedDescription =
            "定型軌跡では「軌跡」と「全設定」は登録できません。ブラシは登録・適用できます。軌跡または全設定のプリセットを適用すると、軌跡の種類は「カスタム軌跡」に切り替わります。";
        var repositoryRoot = FindRepositoryRoot();
        var tooltipSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "YMM4GlassWipe",
            "DetailedWipePresetTooltipText.cs"));
        var editorSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "YMM4GlassWipe",
            "DetailedWipePresetEditorControl.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);

        True(
            tooltipSource.Contains(expectedDescription, StringComparison.Ordinal),
            "プリセットの注意は、定型軌跡で登録できない種類、ブラシ操作、適用時の切替を説明する必要があります。");
        var tooltipBindingCount = editorSource.Split(
            "ToolTip = DetailedWipePresetTooltipText.Description",
            StringSplitOptions.None).Length - 1;
        True(
            tooltipBindingCount >= 3,
            "プリセット欄のルートと選択欄で注意を確認できる必要があります。");
        True(
            editorSource.Contains(
                "\"適用\",\n            (_, _) => ApplySelectedPreset(),\n            DetailedWipePresetTooltipText.Description",
                StringComparison.Ordinal) &&
            editorSource.Contains(
                "\"現在の設定を登録\",\n            (_, _) => RegisterCurrentState(),\n            DetailedWipePresetTooltipText.Description",
                StringComparison.Ordinal),
            "プリセットの適用と登録の操作で注意を確認できる必要があります。");
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
}
