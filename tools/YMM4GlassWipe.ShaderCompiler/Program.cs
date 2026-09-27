// SPDX-License-Identifier: MPL-2.0

using Vortice.D3DCompiler;
using Vortice.Direct3D;
using SharpGen.Runtime;

if (args.Length != 2)
{
    Console.Error.WriteLine("使用方法: YMM4GlassWipe.ShaderCompiler <入力.hlsl> <出力.cso>");
    return 2;
}

var inputPath = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"HLSLファイルが見つかりません: {inputPath}");
    return 2;
}

try
{
#if DEBUG
    var optimizationFlags = ShaderFlags.OptimizationLevel0;
    const string optimizationName = "最適化レベル0（診断）";
#else
    var optimizationFlags = ShaderFlags.OptimizationLevel3;
    const string optimizationName = "最適化レベル3";
#endif
    var compileFlags =
        ShaderFlags.EnableStrictness |
        ShaderFlags.WarningsAreErrors |
        optimizationFlags;
    using var includes = new LocalShaderIncludes(Path.GetDirectoryName(inputPath)!);
    using var shader = Compiler.CompileFromFile(
        inputPath,
        [],
        includes,
        "main",
        "ps_4_0",
        compileFlags,
        EffectFlags.None);

    var outputDirectory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrEmpty(outputDirectory))
    {
        Directory.CreateDirectory(outputDirectory);
    }

    File.WriteAllBytes(outputPath, shader.AsBytes());
    Console.WriteLine(
        $"HLSLをコンパイルしました: {outputPath} [{optimizationName}]");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"HLSLコンパイルに失敗しました: {exception.Message}");
    return 1;
}

// 共通HLSLだけを入力ファイルと同じフォルダーから読む。外部パスは受け付けない。
sealed class LocalShaderIncludes(string directory) : CallbackBase, Include
{
    public Stream Open(IncludeType type, string fileName, Stream? parentStream)
    {
        if (type != IncludeType.Local ||
            fileName is not ("OutsideDropletSurface.hlsli" or "GlassRegion.hlsli"))
            throw new InvalidOperationException("未許可の共通シェーダー参照です。");
        return new MemoryStream(File.ReadAllBytes(Path.Combine(directory, fileName)), writable: false);
    }

    public void Close(Stream stream) => stream.Dispose();
}
