// SPDX-License-Identifier: MPL-2.0

using System.Reflection;

namespace YMM4GlassWipe;

internal static class ShaderResourceLoader
{
    public static byte[] Load(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"埋め込みシェーダーが見つかりません: {resourceName}");

        if (stream.Length > int.MaxValue)
        {
            throw new InvalidOperationException(
                $"埋め込みシェーダーが大きすぎます: {resourceName}");
        }

        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
