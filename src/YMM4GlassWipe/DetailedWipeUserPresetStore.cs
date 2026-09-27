// SPDX-License-Identifier: MPL-2.0

using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using YukkuriMovieMaker.Commons;

namespace YMM4GlassWipe;

internal sealed class DetailedWipeUserPreset
{
    [JsonRequired]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonRequired]
    public string Name { get; set; } = string.Empty;

    public DetailedWipePresetScope Scope { get; set; } = DetailedWipePresetScope.All;

    [JsonRequired]
    public DetailedWipePresetState State { get; set; } = new();
}

internal sealed class DetailedWipeUserPresetFile
{
    public const int CurrentVersion = 3;

    [JsonRequired]
    public int DataSchemaVersion { get; set; } = CurrentVersion;

    [JsonRequired]
    public List<DetailedWipeUserPreset> Presets { get; set; } = [];
}

/// <summary>
/// ユーザー共通の詳細プリセットを、破損時にYMM4へ例外を伝播させず保存します。
/// </summary>
internal sealed class DetailedWipeUserPresetStore
{
    public const int MaximumPresetCount = 100;
    public const int MaximumNameLength = 64;
    public const int MaximumFileBytes = 128 * 1024 * 1024;
    private readonly string _filePath;
    private readonly string? _legacyFilePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public DetailedWipeUserPresetStore(
        string? filePath = null,
        string? legacyFilePath = null)
    {
        if (filePath is null)
        {
            _filePath = GetDefaultFilePath();
            _legacyFilePath = legacyFilePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YMM4GlassWipe",
                "presets.json");
        }
        else
        {
            _filePath = filePath;
            _legacyFilePath = legacyFilePath;
        }
    }

    public string FilePath => _filePath;

    public string? LegacyFilePath => _legacyFilePath;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string GetDefaultFilePath() => Path.Combine(
        AppDirectories.SettingDirectory,
        "YMM4GlassWipe",
        "presets.json");

    public IReadOnlyList<DetailedWipeUserPreset> Load(out string? error)
    {
        error = null;
        var sourcePath = ResolveSourcePath();
        if (sourcePath is null)
        {
            return [];
        }

        if (!BoundedFileReader.TryDeserializeJson<DetailedWipeUserPresetFile>(
                sourcePath,
                MaximumFileBytes,
                JsonOptions,
                out var file,
                out var exceededMaximum,
                out _))
        {
            error = exceededMaximum
                ? $"ユーザープリセットは{MaximumFileBytes / 1024 / 1024} MiB以下にしてください。"
                : "ユーザープリセットを読み込めません。この版と互換性がないか、ファイルが破損しています。既存ファイルは変更していません。バックアップ後に退避し、必要な設定を現在値から登録し直してください。";
            return [];
        }

        if (!TrySanitizeFile(file, out var sanitized))
        {
            error = "ユーザープリセットはこの版と互換性がないか、形式が不正です。既存ファイルは変更していません。バックアップ後に退避し、必要な設定を現在値から登録し直してください。";
            return [];
        }

        return sanitized.Presets;
    }

    public bool Upsert(
        string name,
        DetailedWipePresetScope scope,
        DetailedWipePresetState state,
        out bool replaced,
        out string? error)
    {
        replaced = false;
        error = null;
        name = name.Trim();
        if (name.Length is < 1 or > MaximumNameLength || name.Any(char.IsControl))
        {
            error = $"プリセット名は制御文字を使わず、1～{MaximumNameLength}文字で入力してください。";
            return false;
        }

        if (!DetailedWipePresetScopePolicy.IsValid(scope) ||
            !state.TrySanitize(scope, out var sanitizedState))
        {
            error = "登録する詳細設定が不正です。";
            return false;
        }

        var presets = Load(out var loadError).ToList();
        if (loadError is not null)
        {
            error = loadError;
            return false;
        }

        var existing = presets.FirstOrDefault(
            preset => preset.Scope == scope &&
                string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            if (presets.Count >= MaximumPresetCount)
            {
                error = $"ユーザープリセットは{MaximumPresetCount}件まで登録できます。";
                return false;
            }

            presets.Add(new DetailedWipeUserPreset
            {
                Name = name,
                Scope = scope,
                State = sanitizedState,
            });
        }
        else
        {
            existing.Name = name;
            existing.Scope = scope;
            existing.State = sanitizedState;
            replaced = true;
        }

        return Save(presets, out error);
    }

    public bool Delete(Guid id, out string? error)
    {
        var presets = Load(out error).ToList();
        if (error is not null)
        {
            return false;
        }

        if (presets.RemoveAll(preset => preset.Id == id) == 0)
        {
            error = "削除するユーザープリセットが見つかりません。";
            return false;
        }

        return Save(presets, out error);
    }

    private bool Save(IEnumerable<DetailedWipeUserPreset> presets, out string? error)
    {
        error = null;
        var sanitizedPresets = presets
            .Take(MaximumPresetCount)
            .Select(preset => TrySanitizePreset(preset))
            .Where(preset => preset is not null)
            .Cast<DetailedWipeUserPreset>()
            .ToList();
        var file = new DetailedWipeUserPresetFile { Presets = sanitizedPresets };
        var directory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            error = "ユーザープリセットの保存先が不正です。";
            return false;
        }

        var temporaryPath = _filePath + ".tmp";
        var backupPath = _filePath + ".bak";
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(file, JsonOptions),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (File.Exists(_filePath))
            {
                File.Replace(temporaryPath, _filePath, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, _filePath);
            }

            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error = $"ユーザープリセットを保存できませんでした: {exception.Message}";
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _ = exception;
            }
        }
    }

    private string? ResolveSourcePath()
    {
        if (File.Exists(_filePath))
        {
            return _filePath;
        }

        return !string.IsNullOrWhiteSpace(_legacyFilePath) &&
            File.Exists(_legacyFilePath)
                ? _legacyFilePath
                : null;
    }

    private static bool TrySanitizeFile(
        DetailedWipeUserPresetFile? file,
        out DetailedWipeUserPresetFile sanitized)
    {
        sanitized = new DetailedWipeUserPresetFile();
        if (file is null ||
            file.DataSchemaVersion != DetailedWipeUserPresetFile.CurrentVersion ||
            file.Presets is null ||
            file.Presets.Count > MaximumPresetCount)
        {
            return false;
        }

        var presets = new List<DetailedWipeUserPreset>(file.Presets.Count);
        foreach (var preset in file.Presets)
        {
            var sanitizedPreset = TrySanitizePreset(preset);
            if (sanitizedPreset is null || presets.Any(existing => existing.Id == sanitizedPreset.Id))
            {
                return false;
            }

            presets.Add(sanitizedPreset);
        }

        sanitized.Presets = presets;
        return true;
    }

    private static DetailedWipeUserPreset? TrySanitizePreset(
        DetailedWipeUserPreset? preset)
    {
        if (preset is null ||
            preset.Id == Guid.Empty ||
            preset.State is null ||
            preset.State.DataSchemaVersion != DetailedWipePresetState.CurrentVersion)
        {
            return null;
        }

        var name = preset.Name?.Trim() ?? string.Empty;
        var scope = preset.Scope;
        if (name.Length is < 1 or > MaximumNameLength ||
            name.Any(char.IsControl) ||
            !DetailedWipePresetScopePolicy.IsValid(scope) ||
            !preset.State.TrySanitize(scope, out var state))
        {
            return null;
        }

        return new DetailedWipeUserPreset
        {
            Id = preset.Id,
            Name = name,
            Scope = scope,
            State = state,
        };
    }
}
