// SPDX-License-Identifier: MPL-2.0

using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using YukkuriMovieMaker.Commons;

namespace YMM4GlassWipe;

internal sealed class UserBrushEntry
{
    [JsonRequired]
    public Guid Id { get; set; }

    [JsonRequired]
    public string Name { get; set; } = string.Empty;

    [JsonRequired]
    public string FileName { get; set; } = string.Empty;

    [JsonRequired]
    public string ContentHash { get; set; } = string.Empty;

    public long Revision { get; set; } = 1;

    public int PixelWidth { get; set; }

    public int PixelHeight { get; set; }
}

internal sealed class UserBrushLibraryFile
{
    public const int CurrentVersion = 2;

    [JsonRequired]
    public int DataSchemaVersion { get; set; } = CurrentVersion;

    [JsonRequired]
    public List<UserBrushEntry> Brushes { get; set; } = [];
}

/// <summary>
/// ユーザー共通のPNGブラシを検証し、YMM4設定ディレクトリへ原子的に保存します。
/// </summary>
internal sealed class UserBrushLibrary
{
    public const int MaximumBrushCount = 100;
    public const int MaximumNameLength = 64;
    public const int MaximumDimension = 1920;
    public const int MaximumFileBytes = 8 * 1024 * 1024;
    public const int MaximumRegistryFileBytes = 256 * 1024;
    public const string RegistryFileName = "brushes.json";
    private const string ImagesDirectoryName = "images";
    private static readonly string[] ReservedNames = ["手形", "靴跡"];
    private readonly string _rootDirectory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public UserBrushLibrary(string? rootDirectory = null)
    {
        _rootDirectory = rootDirectory ?? GetDefaultRootDirectory();
    }

    public string RootDirectory => _rootDirectory;

    public string RegistryPath => Path.Combine(_rootDirectory, RegistryFileName);

    public IReadOnlyList<UserBrushEntry> Load(out string? error)
    {
        error = null;
        if (!File.Exists(RegistryPath))
        {
            return [];
        }

        if (!BoundedFileReader.TryDeserializeJson<UserBrushLibraryFile>(
                RegistryPath,
                MaximumRegistryFileBytes,
                JsonOptions,
                out var file,
                out var exceededMaximum,
                out _))
        {
            error = exceededMaximum
                ? $"ユーザーブラシ一覧は{MaximumRegistryFileBytes / 1024} KiB以下にしてください。"
                : "ユーザーブラシ一覧を読み込めません。この版と互換性がないか、ファイルが破損しています。既存ファイルと画像は変更していません。バックアップ後に一覧を退避し、PNGを再登録してください。";
            return [];
        }

        if (!TrySanitizeFile(file, out var sanitized))
        {
            error = "ユーザーブラシ一覧はこの版と互換性がないか、形式が不正です。既存ファイルと画像は変更していません。バックアップ後に一覧を退避し、PNGを再登録してください。";
            return [];
        }

        return sanitized.Brushes;
    }

    public bool Register(
        string name,
        string sourcePath,
        out UserBrushEntry entry,
        out bool replaced,
        out string? error)
    {
        entry = new UserBrushEntry();
        replaced = false;
        if (!TryValidateName(name, out var sanitizedName, out error) ||
            !TryReadSource(
                sourcePath,
                out var pngBytes,
                out var contentHash,
                out var pixelWidth,
                out var pixelHeight,
                out error))
        {
            return false;
        }

        var brushes = Load(out error).ToList();
        if (error is not null)
        {
            return false;
        }

        var existing = brushes.FirstOrDefault(brush =>
            string.Equals(brush.Name, sanitizedName, StringComparison.OrdinalIgnoreCase));
        if (existing is null && brushes.Count >= MaximumBrushCount)
        {
            error = $"ユーザーブラシは{MaximumBrushCount}件まで登録できます。";
            return false;
        }

        var id = existing?.Id ?? Guid.NewGuid();
        var revision = NextRevision(existing?.Revision ?? 0);
        entry = CreateEntry(
            id,
            sanitizedName,
            contentHash,
            revision,
            pixelWidth,
            pixelHeight);
        var updated = brushes
            .Where(brush => brush.Id != id)
            .Append(entry)
            .OrderBy(brush => brush.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!WriteImageAndRegistry(pngBytes, entry, existing, updated, out error))
        {
            entry = new UserBrushEntry();
            return false;
        }

        replaced = existing is not null;
        return true;
    }

    public bool Replace(
        Guid id,
        string sourcePath,
        out UserBrushEntry entry,
        out string? error)
    {
        entry = new UserBrushEntry();
        var brushes = Load(out error).ToList();
        if (error is not null)
        {
            return false;
        }

        var existing = brushes.FirstOrDefault(brush => brush.Id == id);
        if (existing is null)
        {
            error = "置き換えるユーザーブラシが見つかりません。";
            return false;
        }

        if (!TryReadSource(
                sourcePath,
                out var pngBytes,
                out var contentHash,
                out var pixelWidth,
                out var pixelHeight,
                out error))
        {
            return false;
        }

        var replacementEntry = CreateEntry(
            existing.Id,
            existing.Name,
            contentHash,
            NextRevision(existing.Revision),
            pixelWidth,
            pixelHeight);
        var updated = brushes
            .Select(brush => brush.Id == id ? replacementEntry : brush)
            .ToList();
        if (!WriteImageAndRegistry(
                pngBytes,
                replacementEntry,
                existing,
                updated,
                out error))
        {
            entry = new UserBrushEntry();
            return false;
        }

        entry = replacementEntry;
        return true;
    }

    public bool Delete(Guid id, out string? error)
    {
        var brushes = Load(out error).ToList();
        if (error is not null)
        {
            return false;
        }

        var existing = brushes.FirstOrDefault(brush => brush.Id == id);
        if (existing is null)
        {
            error = "削除するユーザーブラシが見つかりません。";
            return false;
        }

        var updated = brushes.Where(brush => brush.Id != id).ToList();
        if (!SaveRegistry(updated, out error))
        {
            return false;
        }

        var shouldDeleteImage = !IsFileNameReferenced(updated, existing.FileName);
        try
        {
            var imagePath = GetImagePath(existing);
            if (shouldDeleteImage && File.Exists(imagePath))
            {
                File.Delete(imagePath);
            }

            TryDeleteFile(RegistryPath + ".bak");

            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            if (!SaveRegistry(brushes, out var rollbackError))
            {
                error = $"ユーザーブラシ画像を削除できず、一覧の復旧にも失敗しました: {rollbackError}";
                return false;
            }

            error = $"ユーザーブラシ画像を削除できませんでした: {exception.Message}";
            return false;
        }
    }

    public bool TryLoadMask(
        Guid id,
        out WipeBrushBinaryMask mask,
        out UserBrushEntry entry,
        out string? error)
    {
        mask = new WipeBrushBinaryMask(WipeBrushMaskRasterizer.MaskSize, []);
        entry = new UserBrushEntry();
        var brushes = Load(out error);
        if (error is not null)
        {
            return false;
        }

        var found = brushes.FirstOrDefault(brush => brush.Id == id);
        if (found is null)
        {
            error = "登録されたユーザーブラシが見つかりません。画像を再登録してください。";
            return false;
        }

        var imagePath = GetImagePath(found);
        if (!File.Exists(imagePath))
        {
            error = "ユーザーブラシ画像がありません。画像を再登録するか、同名のブラシを登録して置き換えてください。";
            return false;
        }

        if (!BoundedFileReader.TryReadBytes(
                imagePath,
                MaximumFileBytes,
                out var bytes,
                out var exceededMaximum,
                out var exception))
        {
            error = exceededMaximum
                ? $"ユーザーブラシ画像は{MaximumFileBytes / 1024 / 1024} MiB以下にしてください。画像を再登録してください。"
                : $"ユーザーブラシ画像を読み込めませんでした: {exception?.Message} 画像を再登録してください。";
            return false;
        }

        if (!UserBrushPngValidator.TryValidate(
                bytes,
                found.Name,
                out mask,
                out var contentHash,
                out error))
        {
            error = $"ユーザーブラシ画像を利用できません。{error} 画像を再登録してください。";
            return false;
        }

        if (!string.Equals(
                found.ContentHash,
                contentHash,
                StringComparison.OrdinalIgnoreCase))
        {
            error = "ユーザーブラシ画像が登録時から変更されています。同名のブラシを登録して置き換えてください。";
            return false;
        }

        if (found.PixelWidth != mask.SourcePixelWidth ||
            found.PixelHeight != mask.SourcePixelHeight)
        {
            error = "ユーザーブラシ画像の寸法が登録情報と一致しません。画像を再登録してください。";
            return false;
        }

        entry = found;
        return true;
    }

    internal string GetImagePath(UserBrushEntry entry) =>
        Path.Combine(_rootDirectory, ImagesDirectoryName, entry.FileName);

    internal static bool TryValidateName(
        string? name,
        out string sanitized,
        out string? error)
    {
        sanitized = name?.Trim() ?? string.Empty;
        if (sanitized.Length is < 1 or > MaximumNameLength ||
            sanitized.Any(char.IsControl))
        {
            error = $"ブラシ名は制御文字を使わず、1～{MaximumNameLength}文字で入力してください。";
            return false;
        }

        var isReserved = false;
        foreach (var reserved in ReservedNames)
        {
            if (string.Equals(reserved, sanitized, StringComparison.OrdinalIgnoreCase))
            {
                isReserved = true;
                break;
            }
        }

        if (isReserved)
        {
            error = $"「{sanitized}」は内蔵ブラシ名のため使用できません。";
            return false;
        }

        error = null;
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string GetDefaultRootDirectory() => Path.Combine(
        AppDirectories.SettingDirectory,
        "YMM4GlassWipe",
        "brushes");

    private bool TryReadSource(
        string sourcePath,
        out byte[] pngBytes,
        out string contentHash,
        out int pixelWidth,
        out int pixelHeight,
        out string? error)
    {
        pngBytes = [];
        contentHash = string.Empty;
        pixelWidth = 0;
        pixelHeight = 0;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            error = "登録するPNGファイルを選択してください。";
            return false;
        }

        if (!BoundedFileReader.TryReadBytes(
                sourcePath,
                MaximumFileBytes,
                out pngBytes,
                out var exceededMaximum,
                out var exception))
        {
            error = exceededMaximum
                ? "PNGファイルは8 MiB以下にしてください。"
                : $"PNGファイルを読み込めませんでした: {exception?.Message}";
            return false;
        }

        if (!UserBrushPngValidator.TryValidate(
                pngBytes,
                Path.GetFileName(sourcePath),
                out var mask,
                out contentHash,
                out error))
        {
            pngBytes = [];
            return false;
        }

        pixelWidth = mask.SourcePixelWidth;
        pixelHeight = mask.SourcePixelHeight;

        return true;
    }

    private bool WriteImageAndRegistry(
        byte[] pngBytes,
        UserBrushEntry entry,
        UserBrushEntry? previous,
        IReadOnlyList<UserBrushEntry> updated,
        out string? error)
    {
        if (previous is not null && string.Equals(
                previous.FileName,
                entry.FileName,
                StringComparison.OrdinalIgnoreCase))
        {
            error = "ユーザーブラシの保存先画像名が現在の登録画像と重複しています。既存の画像と一覧は変更していません。";
            return false;
        }

        if (IsFileNameReferencedByAnotherEntry(updated, entry.FileName, entry.Id))
        {
            error = "ユーザーブラシの保存先画像名が別の登録で使用されています。既存の画像と一覧は変更していません。";
            return false;
        }

        var imagesDirectory = Path.Combine(_rootDirectory, ImagesDirectoryName);
        var destinationPath = GetImagePath(entry);
        var temporaryPath = destinationPath + ".tmp";
        try
        {
            Directory.CreateDirectory(imagesDirectory);
            File.WriteAllBytes(temporaryPath, pngBytes);
            File.Move(temporaryPath, destinationPath, overwrite: true);
            if (!SaveRegistry(updated, out error))
            {
                TryDeleteFile(destinationPath);
                return false;
            }

            if (previous is not null &&
                !IsFileNameReferenced(updated, previous.FileName))
            {
                TryDeleteFile(GetImagePath(previous));
            }

            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            TryDeleteFile(temporaryPath);
            TryDeleteFile(destinationPath);
            error = $"ユーザーブラシを保存できませんでした: {exception.Message}";
            return false;
        }
    }

    private bool SaveRegistry(
        IEnumerable<UserBrushEntry> brushes,
        out string? error)
    {
        error = null;
        var sanitized = brushes
            .Select(TrySanitizeEntry)
            .Where(entry => entry is not null)
            .Cast<UserBrushEntry>()
            .Take(MaximumBrushCount)
            .ToList();
        var file = new UserBrushLibraryFile { Brushes = sanitized };
        var temporaryPath = RegistryPath + ".tmp";
        var backupPath = RegistryPath + ".bak";
        try
        {
            Directory.CreateDirectory(_rootDirectory);
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(file, JsonOptions),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (File.Exists(RegistryPath))
            {
                File.Replace(temporaryPath, RegistryPath, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, RegistryPath);
            }

            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error = $"ユーザーブラシ一覧を保存できませんでした: {exception.Message}";
            return false;
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    private static bool TrySanitizeFile(
        UserBrushLibraryFile? file,
        out UserBrushLibraryFile sanitized)
    {
        sanitized = new UserBrushLibraryFile();
        if (file is null ||
            file.DataSchemaVersion != UserBrushLibraryFile.CurrentVersion ||
            file.Brushes is null ||
            file.Brushes.Count > MaximumBrushCount)
        {
            return false;
        }

        var brushes = new List<UserBrushEntry>(file.Brushes.Count);
        foreach (var brush in file.Brushes)
        {
            var candidate = TrySanitizeEntry(brush);
            if (candidate is null ||
                brushes.Any(existing => existing.Id == candidate.Id) ||
                brushes.Any(existing => string.Equals(
                    existing.Name,
                    candidate.Name,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            brushes.Add(candidate);
        }

        sanitized.Brushes = brushes;
        return true;
    }

    private static UserBrushEntry? TrySanitizeEntry(UserBrushEntry? entry)
    {
        if (entry is null ||
            entry.Id == Guid.Empty ||
            !TryValidateName(entry.Name, out var name, out _) ||
            entry.Revision < 1 ||
            entry.PixelWidth is < 1 or > MaximumDimension ||
            entry.PixelHeight is < 1 or > MaximumDimension ||
            !IsSafeFileName(entry.FileName) ||
            !IsSha256(entry.ContentHash))
        {
            return null;
        }

        return new UserBrushEntry
        {
            Id = entry.Id,
            Name = name,
            FileName = entry.FileName,
            ContentHash = entry.ContentHash.ToUpperInvariant(),
            Revision = entry.Revision,
            PixelWidth = entry.PixelWidth,
            PixelHeight = entry.PixelHeight,
        };
    }

    private static UserBrushEntry CreateEntry(
        Guid id,
        string name,
        string contentHash,
        long revision,
        int pixelWidth,
        int pixelHeight) =>
        new()
        {
            Id = id,
            Name = name,
            FileName = $"{id:N}-{revision}.png",
            ContentHash = contentHash,
            Revision = revision,
            PixelWidth = pixelWidth,
            PixelHeight = pixelHeight,
        };

    private static long NextRevision(long revision) =>
        revision >= long.MaxValue ? 1 : Math.Max(1, revision + 1);

    private static bool IsSafeFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) &&
        string.Equals(Path.GetExtension(fileName), ".png", StringComparison.OrdinalIgnoreCase) &&
        fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static bool IsFileNameReferenced(
        IEnumerable<UserBrushEntry> entries,
        string fileName) =>
        entries.Any(entry => string.Equals(
            entry.FileName,
            fileName,
            StringComparison.OrdinalIgnoreCase));

    private static bool IsFileNameReferencedByAnotherEntry(
        IEnumerable<UserBrushEntry> entries,
        string fileName,
        Guid entryId) =>
        entries.Any(entry =>
            entry.Id != entryId &&
            string.Equals(
                entry.FileName,
                fileName,
                StringComparison.OrdinalIgnoreCase));

    private static bool IsSha256(string? value) =>
        value?.Length == 64 && value.All(Uri.IsHexDigit);

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _ = exception;
        }
    }
}

internal static class UserBrushPngValidator
{
    private static ReadOnlySpan<byte> Signature =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool TryValidate(
        byte[] bytes,
        string displayName,
        out WipeBrushBinaryMask mask,
        out string contentHash,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        mask = new WipeBrushBinaryMask(WipeBrushMaskRasterizer.MaskSize, []);
        contentHash = string.Empty;
        if (bytes.Length is < 33 or > UserBrushLibrary.MaximumFileBytes ||
            !bytes.AsSpan(0, Signature.Length).SequenceEqual(Signature) ||
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)) != 13 ||
            !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            error = "有効なPNGファイルではありません。";
            return false;
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
        if (width is 0 or > UserBrushLibrary.MaximumDimension ||
            height is 0 or > UserBrushLibrary.MaximumDimension)
        {
            error = $"PNGの幅と高さは各{UserBrushLibrary.MaximumDimension}px以下にしてください。";
            return false;
        }

        var colorType = bytes[25];
        if (colorType is not 4 and not 6)
        {
            error = "alphaチャネルを持つPNGを使用してください。";
            return false;
        }

        try
        {
            mask = WipeBrushMaskRasterizer.CreateBinaryMask(bytes, displayName);
            contentHash = Convert.ToHexString(SHA256.HashData(bytes));
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            error = $"PNG画像をデコードできませんでした: {exception.Message}";
            return false;
        }
    }
}
