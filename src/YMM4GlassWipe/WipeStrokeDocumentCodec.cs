// SPDX-License-Identifier: MPL-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace YMM4GlassWipe;

internal static class WipeStrokeDocumentCodec
{
    public const string Prefix = "v2:";
    public const int MaximumEncodedLength = 1_048_576;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static string Encode(WipeStrokeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var sanitized = document.Sanitize();
        return Prefix + JsonSerializer.Serialize(sanitized, JsonOptions);
    }

    public static bool TryDecode(
        string? encoded,
        out WipeStrokeDocument document)
    {
        document = WipeStrokeDocument.CreateEmpty();
        if (string.IsNullOrWhiteSpace(encoded) ||
            encoded.Length > MaximumEncodedLength ||
            !encoded.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var decoded = JsonSerializer.Deserialize<WipeStrokeDocument>(
                encoded.AsSpan(Prefix.Length),
                JsonOptions);
            if (!IsValid(decoded))
            {
                return false;
            }

            document = decoded!.Sanitize();
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

    private static bool IsValid(WipeStrokeDocument? document)
    {
        if (document is null ||
            document.DataSchemaVersion != WipeStrokeDocument.CurrentVersion ||
            document.Strokes is null ||
            document.Strokes.Count > WipeStrokeDocument.MaximumStrokeCount)
        {
            return false;
        }

        foreach (var stroke in document.Strokes)
        {
            if (stroke is null ||
                stroke.Points is null ||
                stroke.Points.Count > WipeStrokeDocument.MaximumPointCountPerStroke ||
                stroke.Points.Any(point => point is null || !point.IsFinite))
            {
                return false;
            }
        }

        return true;
    }
}
