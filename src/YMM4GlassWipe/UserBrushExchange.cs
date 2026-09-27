// SPDX-License-Identifier: MPL-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace YMM4GlassWipe;

internal sealed class UserBrushExchangeState
{
    public const int CurrentVersion = 2;

    [JsonRequired]
    public int DataSchemaVersion { get; set; } = CurrentVersion;

    public Guid UserBrushId { get; set; }

    public long Revision { get; set; }

    public int PixelWidth { get; set; }

    public int PixelHeight { get; set; }

    public bool Apply { get; set; }
}

internal static class UserBrushExchangeCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static string EncodeSnapshot(
        Guid userBrushId,
        long revision,
        int pixelWidth,
        int pixelHeight) =>
        Encode(
            userBrushId,
            revision,
            pixelWidth,
            pixelHeight,
            apply: false);

    public static string EncodeApply(
        Guid userBrushId,
        long revision,
        int pixelWidth,
        int pixelHeight)
    {
        if (userBrushId == Guid.Empty ||
            revision < 1 ||
            !IsValidDimension(pixelWidth) ||
            !IsValidDimension(pixelHeight))
        {
            throw new ArgumentException("適用するユーザーブラシが不正です。", nameof(userBrushId));
        }

        return Encode(
            userBrushId,
            revision,
            pixelWidth,
            pixelHeight,
            apply: true);
    }

    public static bool TryDecode(
        string? encoded,
        out UserBrushExchangeState state)
    {
        state = new UserBrushExchangeState();
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > 4096)
        {
            return false;
        }

        try
        {
            var decoded = JsonSerializer.Deserialize<UserBrushExchangeState>(
                encoded,
                JsonOptions);
            if (decoded is null ||
                decoded.DataSchemaVersion != UserBrushExchangeState.CurrentVersion ||
                decoded.Revision < 0 ||
                decoded.UserBrushId != Guid.Empty &&
                    (!IsValidDimension(decoded.PixelWidth) ||
                     !IsValidDimension(decoded.PixelHeight)) ||
                decoded.Apply &&
                    (decoded.UserBrushId == Guid.Empty || decoded.Revision < 1))
            {
                return false;
            }

            state = decoded;
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

    private static string Encode(
        Guid userBrushId,
        long revision,
        int pixelWidth,
        int pixelHeight,
        bool apply) =>
        JsonSerializer.Serialize(
            new UserBrushExchangeState
            {
                UserBrushId = userBrushId,
                Revision = Math.Max(0, revision),
                PixelWidth = userBrushId == Guid.Empty ? 0 : pixelWidth,
                PixelHeight = userBrushId == Guid.Empty ? 0 : pixelHeight,
                Apply = apply,
            },
            JsonOptions);

    private static bool IsValidDimension(int value) =>
        value is >= 1 and <= UserBrushLibrary.MaximumDimension;
}
