// SPDX-License-Identifier: MPL-2.0

using System.Security.Cryptography;
using System.Reflection;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YMM4GlassWipe;

namespace YMM4GlassWipe.Verification;

internal static class UserBrushSafetyVerification
{
    public static void VerifyEditorRecoveryGuidance()
    {
        RunInSta(() =>
        {
            var temporaryDirectory = CreateTemporaryDirectory("editor");
            var invalidRoot = Path.Combine(temporaryDirectory, "invalid-registry");
            Directory.CreateDirectory(invalidRoot);
            var invalidLibrary = new UserBrushLibrary(invalidRoot);
            File.WriteAllBytes(invalidLibrary.RegistryPath, [0xFF]);

            var control = new UserBrushEditorControl(invalidLibrary);
            RefreshOptions(control, Guid.NewGuid());
            var statusText = GetStatusText(control);
            Check(
                statusText.Contains(
                    "一覧を読み込めません",
                    StringComparison.Ordinal) &&
                statusText.Contains(
                    "バックアップ後に一覧を退避",
                    StringComparison.Ordinal),
                "一覧の具体的な読込エラーを選択状態の案内で上書きしてはいけません。");

            File.Delete(invalidLibrary.RegistryPath);
            RefreshOptions(control, Guid.Empty);
            Check(
                string.IsNullOrEmpty(GetStatusText(control)),
                "一覧の読込復旧後に選択肢がなければ古いエラー表示を消す必要があります。");

            var missingLibrary = new UserBrushLibrary(
                Path.Combine(temporaryDirectory, "missing-entry"));
            var missingControl = new UserBrushEditorControl(missingLibrary);
            RefreshOptions(missingControl, Guid.NewGuid());
            var missingStatusText = GetStatusText(missingControl);
            Check(
                missingStatusText.Contains(
                    "PNGを登録",
                    StringComparison.Ordinal) &&
                missingStatusText.Contains(
                    "一覧から新しいブラシを選んで",
                    StringComparison.Ordinal) &&
                missingStatusText.Contains(
                    "使用",
                    StringComparison.Ordinal) &&
                !missingStatusText.Contains(
                    "同名",
                    StringComparison.Ordinal),
                "参照欠落時はPNG登録後に新しいブラシを選び直す手順を案内する必要があります。");
        });
    }

    public static void VerifySharedImageLifetime()
    {
        RunInSta(() =>
        {
            var temporaryDirectory = CreateTemporaryDirectory("shared-image");
            var sourceBytes = CreateTestPng();
            var sourcePath = Path.Combine(temporaryDirectory, "replacement.png");
            File.WriteAllBytes(sourcePath, sourceBytes);

            VerifyDeleteKeepsSharedImage(
                Path.Combine(temporaryDirectory, "delete"),
                sourceBytes);
            VerifyReplaceKeepsSharedImage(
                Path.Combine(temporaryDirectory, "replace"),
                sourcePath,
                sourceBytes);
        });
    }

    public static void VerifyReplacementNameCollision()
    {
        RunInSta(() =>
        {
            var temporaryDirectory = CreateTemporaryDirectory("replacement-collision");
            var libraryRoot = Path.Combine(temporaryDirectory, "library");
            Directory.CreateDirectory(libraryRoot);
            var library = new UserBrushLibrary(libraryRoot);
            var replacementBytes = CreateTestPng(224);
            var replacementPath = Path.Combine(temporaryDirectory, "replacement.png");
            File.WriteAllBytes(replacementPath, replacementBytes);

            var firstBytes = CreateTestPng(192);
            var protectedBytes = CreateTestPng(64);
            var firstId = Guid.NewGuid();
            var first = CreateEntry(firstId, "衝突元", "shared.png", firstBytes);
            var protectedEntry = CreateEntry(
                Guid.NewGuid(),
                "保護対象",
                $"{firstId:N}-2.png",
                protectedBytes);
            WriteRegistry(library, [first, protectedEntry]);
            var firstPath = library.GetImagePath(first);
            var protectedPath = library.GetImagePath(protectedEntry);
            Directory.CreateDirectory(Path.GetDirectoryName(firstPath)!);
            File.WriteAllBytes(firstPath, firstBytes);
            File.WriteAllBytes(protectedPath, protectedBytes);
            var registryBytesBefore = File.ReadAllBytes(library.RegistryPath);

            Check(
                !library.Replace(first.Id, replacementPath, out _, out var error),
                "別IDが生成予定の画像名を使用している置換は拒否する必要があります。");
            Check(
                error?.Contains("別の登録", StringComparison.Ordinal) == true &&
                error.Contains("変更していません", StringComparison.Ordinal),
                "画像名衝突時は既存データを変更しないことを日本語で案内する必要があります。");
            Check(
                File.ReadAllBytes(library.RegistryPath).SequenceEqual(registryBytesBefore),
                "画像名衝突を拒否したときは一覧のbyte列を変更してはいけません。");
            Check(
                File.ReadAllBytes(protectedPath).SequenceEqual(protectedBytes),
                "画像名衝突を拒否したときは別IDのPNGを変更してはいけません。");
            Check(
                File.ReadAllBytes(firstPath).SequenceEqual(firstBytes),
                "画像名衝突を拒否したときは置換元のPNGも変更してはいけません。");

            var selfCollisionRoot = Path.Combine(temporaryDirectory, "self-collision");
            Directory.CreateDirectory(selfCollisionRoot);
            var selfCollisionLibrary = new UserBrushLibrary(selfCollisionRoot);
            var selfCollisionId = Guid.NewGuid();
            var selfCollisionEntry = CreateEntry(
                selfCollisionId,
                "自己衝突",
                $"{selfCollisionId:N}-2.png",
                firstBytes);
            WriteRegistry(selfCollisionLibrary, [selfCollisionEntry]);
            var selfCollisionPath = selfCollisionLibrary.GetImagePath(selfCollisionEntry);
            Directory.CreateDirectory(Path.GetDirectoryName(selfCollisionPath)!);
            File.WriteAllBytes(selfCollisionPath, firstBytes);
            var selfRegistryBytesBefore = File.ReadAllBytes(
                selfCollisionLibrary.RegistryPath);

            Check(
                !selfCollisionLibrary.Replace(
                    selfCollisionId,
                    replacementPath,
                    out _,
                    out var selfCollisionError),
                "生成予定名を置換元自身が使用している置換は安全に拒否する必要があります。");
            Check(
                selfCollisionError?.Contains("現在の登録画像", StringComparison.Ordinal) == true &&
                selfCollisionError.Contains("変更していません", StringComparison.Ordinal),
                "置換元自身との画像名衝突理由を表示する必要があります。");
            Check(
                File.ReadAllBytes(selfCollisionLibrary.RegistryPath)
                    .SequenceEqual(selfRegistryBytesBefore) &&
                File.ReadAllBytes(selfCollisionPath).SequenceEqual(firstBytes),
                "置換元自身との画像名衝突時は一覧とPNGを変更してはいけません。");
        });
    }

    private static void VerifyDeleteKeepsSharedImage(
        string rootDirectory,
        byte[] imageBytes)
    {
        var library = CreateSharedLibrary(
            rootDirectory,
            imageBytes,
            "shared.png",
            "shared.png");
        var entries = library.Load(out var loadError);
        Check(loadError is null, loadError ?? "共有PNGの一覧を読み込めませんでした。");

        var first = entries.Single(entry => entry.Name == "共有A");
        var second = entries.Single(entry => entry.Name == "共有B");
        var sharedPath = library.GetImagePath(first);
        Check(library.Delete(first.Id, out var firstError), firstError ?? "共有元の削除に失敗しました。");
        Check(
            File.Exists(sharedPath) &&
            library.Load(out loadError).Single().Id == second.Id,
            "別の登録が参照中のPNGは削除してはいけません。");
        Check(loadError is null, loadError ?? "共有元削除後の一覧を読み込めませんでした。");

        Check(library.Delete(second.Id, out var secondError), secondError ?? "最後の共有参照を削除できませんでした。");
        Check(!File.Exists(sharedPath), "最後の参照を削除した時点で共有PNGを削除する必要があります。");
    }

    private static void VerifyReplaceKeepsSharedImage(
        string rootDirectory,
        string sourcePath,
        byte[] imageBytes)
    {
        var library = CreateSharedLibrary(
            rootDirectory,
            imageBytes,
            "shared.png",
            "SHARED.PNG");
        var entries = library.Load(out var loadError);
        Check(loadError is null, loadError ?? "大文字小文字違いの共有PNG一覧を読み込めませんでした。");

        var first = entries.Single(entry => entry.Name == "共有A");
        var second = entries.Single(entry => entry.Name == "共有B");
        var sharedPath = library.GetImagePath(first);
        Check(
            library.Replace(first.Id, sourcePath, out var replacedFirst, out var firstError),
            firstError ?? "最初の共有参照を置換できませんでした。");
        Check(
            File.Exists(sharedPath) && File.Exists(library.GetImagePath(replacedFirst)),
            "Windowsで同じファイル名を参照する登録が残る間は旧PNGを保持する必要があります。");

        Check(
            library.Replace(second.Id, sourcePath, out var replacedSecond, out var secondError),
            secondError ?? "最後の共有参照を置換できませんでした。");
        Check(
            !File.Exists(sharedPath) && File.Exists(library.GetImagePath(replacedSecond)),
            "最後の参照を置換した時点で旧PNGを削除する必要があります。");
    }

    private static UserBrushLibrary CreateSharedLibrary(
        string rootDirectory,
        byte[] imageBytes,
        string firstFileName,
        string secondFileName)
    {
        Directory.CreateDirectory(rootDirectory);
        var library = new UserBrushLibrary(rootDirectory);
        var hash = Convert.ToHexString(SHA256.HashData(imageBytes));
        var file = new UserBrushLibraryFile
        {
            Brushes =
            [
                new UserBrushEntry
                {
                    Id = Guid.NewGuid(),
                    Name = "共有A",
                    FileName = firstFileName,
                    ContentHash = hash,
                    Revision = 1,
                    PixelWidth = 2,
                    PixelHeight = 2,
                },
                new UserBrushEntry
                {
                    Id = Guid.NewGuid(),
                    Name = "共有B",
                    FileName = secondFileName,
                    ContentHash = hash,
                    Revision = 1,
                    PixelWidth = 2,
                    PixelHeight = 2,
                },
            ],
        };
        WriteRegistry(library, file.Brushes);
        var imagePath = library.GetImagePath(file.Brushes[0]);
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
        File.WriteAllBytes(imagePath, imageBytes);
        return library;
    }

    private static UserBrushEntry CreateEntry(
        Guid id,
        string name,
        string fileName,
        byte[] imageBytes) =>
        new()
        {
            Id = id,
            Name = name,
            FileName = fileName,
            ContentHash = Convert.ToHexString(SHA256.HashData(imageBytes)),
            Revision = 1,
            PixelWidth = 2,
            PixelHeight = 2,
        };

    private static void WriteRegistry(
        UserBrushLibrary library,
        IReadOnlyList<UserBrushEntry> entries)
    {
        var file = new UserBrushLibraryFile { Brushes = entries.ToList() };
        File.WriteAllText(
            library.RegistryPath,
            JsonSerializer.Serialize(
                file,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                }));
    }

    private static byte[] CreateTestPng(byte red = 192)
    {
        var pixels = new byte[]
        {
            32, 96, red, 255,
            32, 96, red, 255,
            32, 96, red, 255,
            32, 96, red, 255,
        };
        var bitmap = BitmapSource.Create(
            2,
            2,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            8);
        bitmap.Freeze();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception is not null)
        {
            throw new InvalidOperationException("ユーザーブラシ安全性検証に失敗しました。", exception);
        }
    }

    private static void RefreshOptions(UserBrushEditorControl control, Guid selectedId)
    {
        var method = typeof(UserBrushEditorControl).GetMethod(
            "RefreshOptions",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("ユーザーブラシ一覧の更新処理が見つかりません。");
        method.Invoke(control, [selectedId]);
    }

    private static string GetStatusText(UserBrushEditorControl control)
    {
        var field = typeof(UserBrushEditorControl).GetField(
            "_status",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("ユーザーブラシの状態表示が見つかりません。");
        return ((TextBlock)field.GetValue(control)!).Text;
    }

    private static string CreateTemporaryDirectory(string purpose)
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "tmp",
            "verification",
            $"user-brush-{purpose}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
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

        throw new InvalidOperationException("検証用リポジトリのルートを特定できません。");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
