// SPDX-License-Identifier: MPL-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace YMM4GlassWipe;

internal static class DetailedWipePresetCodec
{
    public const int MaximumEncodedLength = 1_048_576;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static string Encode(DetailedWipePresetState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.TrySanitize(out var sanitized))
        {
            throw new ArgumentException("詳細軌跡プリセットが不正です。", nameof(state));
        }

        return JsonSerializer.Serialize(sanitized, JsonOptions);
    }

    public static bool TryDecode(string? encoded, out DetailedWipePresetState state)
    {
        state = new DetailedWipePresetState();
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > MaximumEncodedLength)
        {
            return false;
        }

        try
        {
            var decoded = JsonSerializer.Deserialize<DetailedWipePresetState>(encoded, JsonOptions);
            return decoded is not null && decoded.TrySanitize(out state);
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
}
