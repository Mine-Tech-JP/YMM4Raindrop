// SPDX-License-Identifier: MPL-2.0

using System.Text.Json.Serialization;

namespace YMM4GlassWipe;

internal sealed class WipeStrokeDocument
{
    public const int CurrentVersion = 2;
    public const int MaximumStrokeCount = 64;
    public const int MaximumPointCountPerStroke = 512;
    public const int CanvasPixelWidth = 1920;
    public const int CanvasPixelHeight = 1080;

    [JsonPropertyOrder(0)]
    public int DataSchemaVersion { get; set; } = CurrentVersion;

    [JsonPropertyOrder(1)]
    public List<WipeStroke> Strokes { get; set; } = [];

    public static WipeStrokeDocument CreateEmpty() => new();

    public static WipeStrokeDocument CreateStarter() =>
        new()
        {
            Strokes =
            [
                new WipeStroke
                {
                    Points =
                    [
                        new WipeStrokePoint(0, 192, 540, 100),
                        new WipeStrokePoint(100, 1728, 540, 100),
                    ],
                },
            ],
        };

    public WipeStrokeDocument DeepClone() =>
        new()
        {
            DataSchemaVersion = DataSchemaVersion,
            Strokes = (Strokes ?? [])
                .OfType<WipeStroke>()
                .Select(stroke => stroke.DeepClone())
                .ToList(),
        };

    public WipeStrokeDocument Sanitize() =>
        new()
        {
            DataSchemaVersion = CurrentVersion,
            Strokes = (Strokes ?? [])
                .OfType<WipeStroke>()
                .Take(MaximumStrokeCount)
                .Select(stroke => stroke.Sanitize())
                .ToList(),
        };
}

internal sealed class WipeStroke
{
    [JsonPropertyOrder(0)]
    public List<WipeStrokePoint> Points { get; set; } = [];

    public WipeStroke DeepClone() =>
        new()
        {
            Points = (Points ?? [])
                .OfType<WipeStrokePoint>()
                .Select(point => point with { })
                .ToList(),
        };

    public WipeStroke Sanitize() =>
        new()
        {
            Points = (Points ?? [])
                .OfType<WipeStrokePoint>()
                .Take(WipeStrokeDocument.MaximumPointCountPerStroke)
                .Select(point => point.Sanitize())
                .ToList(),
        };
}

internal sealed record WipeStrokePoint
{
    public WipeStrokePoint()
    {
    }

    public WipeStrokePoint(
        double timelinePercent,
        double x,
        double y,
        double contact)
    {
        TimelinePercent = timelinePercent;
        X = x;
        Y = y;
        Contact = contact;
    }

    [JsonPropertyOrder(0)]
    public double TimelinePercent { get; set; }

    [JsonPropertyOrder(1)]
    public double X { get; set; }

    [JsonPropertyOrder(2)]
    public double Y { get; set; }

    [JsonPropertyOrder(3)]
    public double Contact { get; set; } = 100;

    public WipeStrokePoint Sanitize() =>
        new(
            ClampPercent(TimelinePercent),
            ClampPixel(X, WipeStrokeDocument.CanvasPixelWidth),
            ClampPixel(Y, WipeStrokeDocument.CanvasPixelHeight),
            ClampPercent(Contact));

    public bool IsFinite =>
        double.IsFinite(TimelinePercent) &&
        double.IsFinite(X) &&
        double.IsFinite(Y) &&
        double.IsFinite(Contact);

    private static double ClampPercent(double value) =>
        double.IsFinite(value)
            ? Math.Clamp(value, 0, 100)
            : 0;

    private static double ClampPixel(double value, int maximum) =>
        double.IsFinite(value)
            ? Math.Clamp(
                Math.Round(value, MidpointRounding.AwayFromZero),
                0,
                maximum)
            : 0;
}
