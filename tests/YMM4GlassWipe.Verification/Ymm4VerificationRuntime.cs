// SPDX-License-Identifier: MPL-2.0

using System.Reflection;
using System.Runtime.Loader;
using System.Xml.Linq;

namespace YMM4GlassWipe.Verification;

internal static class Ymm4VerificationRuntime
{
    public static void Initialize()
    {
        // 実保存処理の検証だけに、ビルドで参照しているYMM4の既存DLLを使用する。
        var root = FindRepositoryRoot();
        var props = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        var configuredDirectory = props.Descendants("YMM4DirPath").Single().Value;
        var directory = Path.GetFullPath(configuredDirectory, root);
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException("検証用YMM4参照ディレクトリがありません。");
        }

        AssemblyLoadContext.Default.Resolving += (_, name) => Resolve(directory, name);
    }

    private static Assembly? Resolve(string directory, AssemblyName name)
    {
        if (string.IsNullOrEmpty(name.Name) || name.Name != Path.GetFileName(name.Name))
        {
            return null;
        }

        var path = Path.Combine(directory, name.Name + ".dll");
        return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "YMM4GlassWipe.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("検証用リポジトリのルートを特定できません。");
    }
}
