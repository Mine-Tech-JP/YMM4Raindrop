// SPDX-License-Identifier: MPL-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace YMM4GlassWipe;

internal sealed class DetailedWipePresetExchangeState
{
    public const int CurrentVersion = 1;

    [JsonRequired]
    public int DataSchemaVersion { get; set; } = CurrentVersion;

    public WipePathInputMode PathInputMode { get; set; }

    [JsonRequired]
    public DetailedWipePresetState State { get; set; } = new();

    public bool CanSavePath { get; set; }

    public DetailedWipePresetScope? ApplyScope { get; set; }
}

internal static class DetailedWipePresetExchangeCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static string EncodeSnapshot(
        WipePathInputMode pathInputMode,
        DetailedWipePresetState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var canSavePath =
            WipePathInputModeCompatibility.Normalize(pathInputMode) ==
                WipePathInputMode.StrokeCollection &&
            WipeStrokeDocumentCodec.TryDecode(state.CustomPathData, out _);
        if (!TrySanitizeSnapshot(state, out var sanitized))
        {
            throw new ArgumentException("現在のプリセット設定が不正です。", nameof(state));
        }

        return JsonSerializer.Serialize(
            new DetailedWipePresetExchangeState
            {
                PathInputMode = WipePathInputModeCompatibility.Normalize(pathInputMode),
                State = sanitized,
                CanSavePath = canSavePath,
            },
            JsonOptions);
    }

    public static string EncodeApply(
        DetailedWipePresetScope scope,
        DetailedWipePresetState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!DetailedWipePresetScopePolicy.IsValid(scope) ||
            !state.TrySanitize(scope, out var sanitized))
        {
            throw new ArgumentException("適用するプリセット設定が不正です。", nameof(state));
        }

        return JsonSerializer.Serialize(
            new DetailedWipePresetExchangeState
            {
                PathInputMode = WipePathInputMode.StrokeCollection,
                State = sanitized,
                CanSavePath = DetailedWipePresetScopePolicy.IncludesPath(scope),
                ApplyScope = scope,
            },
            JsonOptions);
    }

    public static bool TryDecode(
        string? encoded,
        out DetailedWipePresetExchangeState exchange)
    {
        exchange = new DetailedWipePresetExchangeState();
        if (string.IsNullOrWhiteSpace(encoded) ||
            encoded.Length > DetailedWipePresetCodec.MaximumEncodedLength)
        {
            return false;
        }

        try
        {
            var decoded = JsonSerializer.Deserialize<DetailedWipePresetExchangeState>(
                encoded,
                JsonOptions);
            if (decoded is null ||
                decoded.DataSchemaVersion != DetailedWipePresetExchangeState.CurrentVersion ||
                decoded.State is null ||
                decoded.ApplyScope is { } applyScope &&
                    !DetailedWipePresetScopePolicy.IsValid(applyScope))
            {
                return false;
            }

            var canSavePath = decoded.CanSavePath &&
                WipePathInputModeCompatibility.Normalize(decoded.PathInputMode) ==
                    WipePathInputMode.StrokeCollection &&
                WipeStrokeDocumentCodec.TryDecode(decoded.State.CustomPathData, out _);
            DetailedWipePresetState sanitized;
            var sanitizedSuccessfully = decoded.ApplyScope is { } decodedScope
                ? decoded.State.TrySanitize(decodedScope, out sanitized)
                : TrySanitizeSnapshot(decoded.State, out sanitized);
            if (!sanitizedSuccessfully)
            {
                return false;
            }

            exchange = new DetailedWipePresetExchangeState
            {
                PathInputMode = WipePathInputModeCompatibility.Normalize(decoded.PathInputMode),
                State = sanitized,
                CanSavePath = canSavePath,
                ApplyScope = decoded.ApplyScope,
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool TrySanitizeSnapshot(
        DetailedWipePresetState state,
        out DetailedWipePresetState sanitized)
    {
        if (state.TrySanitize(DetailedWipePresetScope.All, out sanitized))
        {
            return true;
        }

        if (!state.TrySanitize(DetailedWipePresetScope.Brush, out sanitized))
        {
            return false;
        }

        state.CopyOutsideDropletPhysicsTo(sanitized);
        sanitized.OutsideDropletAmount = OutsideDropletSettings.SanitizeAmountPercent(
            state.OutsideDropletAmount ?? OutsideDropletSettings.DefaultAmountPercent);
        sanitized.OutsideDropletSize = OutsideDropletSettings.SanitizeSizePercent(
            state.OutsideDropletSize ?? OutsideDropletSettings.DefaultSizePercent);
        sanitized.OutsideDropletStrength = OutsideDropletSettings.SanitizeStrengthPercent(
            state.OutsideDropletStrength ?? OutsideDropletSettings.DefaultStrengthPercent);
        sanitized.OutsideDropletSeed = OutsideDropletSettings.SanitizeSeedValue(
            state.OutsideDropletSeed ?? OutsideDropletSettings.DefaultSeed);
        sanitized.OutsideDropletDeformWithSurface =
            state.OutsideDropletDeformWithSurface ?? true;
        sanitized.OutsideDropletFallEnabled =
            state.OutsideDropletFallEnabled ?? false;
        sanitized.OutsideDropletMergeEnabled =
            state.DataSchemaVersion <
                DetailedWipePresetState.OutsideDropletMergeIntroducedVersion
                ? OutsideDropletSettings.DefaultMergeEnabled
                : state.OutsideDropletMergeEnabled ??
                    OutsideDropletSettings.DefaultMergeEnabled;
        sanitized.OutsideDropletRainEnabled =
            state.DataSchemaVersion < DetailedWipePresetState.OutsideDropletRainIntroducedVersion
                ? OutsideDropletSettings.DefaultRainEnabled
                : state.OutsideDropletRainEnabled ?? OutsideDropletSettings.DefaultRainEnabled;
        sanitized.OutsideDropletRainStartSeconds =
            state.DataSchemaVersion < DetailedWipePresetState.OutsideDropletRainIntroducedVersion
                ? OutsideDropletSettings.DefaultRainStartSeconds
                : OutsideDropletSettings.SanitizeRainStartSeconds(state.OutsideDropletRainStartSeconds ?? OutsideDropletSettings.DefaultRainStartSeconds);
        sanitized.OutsideDropletRainDurationSeconds =
            state.DataSchemaVersion < DetailedWipePresetState.OutsideDropletRainIntroducedVersion
                ? OutsideDropletSettings.DefaultRainDurationSeconds
                : OutsideDropletSettings.SanitizeRainDurationSeconds(state.OutsideDropletRainDurationSeconds ?? OutsideDropletSettings.DefaultRainDurationSeconds);
        sanitized.OutsideDropletFallingRatio =
            OutsideDropletSettings.SanitizeFallingRatioPercent(
                state.OutsideDropletFallingRatio ??
                OutsideDropletSettings.DefaultFallingRatioPercent);
        sanitized.OutsideDropletFallSpeed =
            OutsideDropletSettings.SanitizeFallSpeedPercent(
                state.OutsideDropletFallSpeed ??
                OutsideDropletSettings.DefaultFallSpeedPercent);
        sanitized.OutsideDropletFallFrequency =
            OutsideDropletSettings.SanitizeFallFrequencyPercent(
                state.OutsideDropletFallFrequency ??
                OutsideDropletSettings.DefaultFallFrequencyPercent);
        sanitized.OutsideDropletTrailLength =
            OutsideDropletSettings.SanitizeTrailLengthPercent(
                state.OutsideDropletTrailLength ??
                OutsideDropletSettings.DefaultTrailLengthPercent);
        sanitized.OutsideDropletAppearance =
            state.DataSchemaVersion <
                DetailedWipePresetState.OutsideDropletAppearanceIntroducedVersion
                ? OutsideDropletAppearanceCompatibility.LegacyMigrationDefault
                : OutsideDropletSettings.SanitizeAppearance(
                    state.OutsideDropletAppearance ??
                    OutsideDropletAppearanceCompatibility.Default);
        sanitized.OutsideDropletOutlineOpacity =
            state.DataSchemaVersion < DetailedWipePresetState.OutsideDropletOutlineOpacityIntroducedVersion
                ? OutsideDropletSettings.DefaultOutlineOpacityPercent
                : OutsideDropletSettings.SanitizeOutlineOpacityPercent(
                    state.OutsideDropletOutlineOpacity ?? OutsideDropletSettings.DefaultOutlineOpacityPercent);
        return true;
    }
}
