// SPDX-License-Identifier: MPL-2.0

using System.Text.RegularExpressions;

internal static class ShaderVerificationSource
{
    // 検証対象もコンパイラと同じ共通関数を読む。その他のソースはそのまま返す。
    public static string ReadAllText(string path)
    {
        var source = File.ReadAllText(path);
        if (!string.Equals(Path.GetExtension(path), ".hlsl", StringComparison.OrdinalIgnoreCase)) return source;
        return Regex.Replace(source, "(?m)^#include \\\"(OutsideDropletSurface\\.hlsli|GlassRegion\\.hlsli)\\\"\\r?$",
            match => File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, match.Groups[1].Value)));
    }
}
