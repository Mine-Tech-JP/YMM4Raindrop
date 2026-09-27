// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

internal enum WipeMaskUpdateKind
{
    Rebuild,
    Append,
    Reuse,
}

internal readonly record struct WipeMaskUpdatePlan(
    WipeMaskUpdateKind Kind,
    int FirstSampleIndex)
{
    public static WipeMaskUpdatePlan Create(
        WipePathSnapshot? cachedPath,
        WipeMaskGeometry? cachedGeometry,
        WipePathSnapshot currentPath,
        WipeMaskGeometry currentGeometry)
    {
        ArgumentNullException.ThrowIfNull(currentPath);

        if (cachedPath is null ||
            cachedGeometry is null ||
            cachedGeometry.Value != currentGeometry ||
            currentPath.Frame < cachedPath.Frame)
        {
            return new WipeMaskUpdatePlan(WipeMaskUpdateKind.Rebuild, 0);
        }

        if (currentPath.InputMode == WipePathInputMode.StrokeCollection ||
            cachedPath.InputMode == WipePathInputMode.StrokeCollection)
        {
            return currentPath.Fingerprint == cachedPath.Fingerprint
                ? new WipeMaskUpdatePlan(
                    WipeMaskUpdateKind.Reuse,
                    currentPath.Samples.Length)
                : new WipeMaskUpdatePlan(WipeMaskUpdateKind.Rebuild, 0);
        }

        if (!currentPath.StartsWith(cachedPath))
        {
            return new WipeMaskUpdatePlan(WipeMaskUpdateKind.Rebuild, 0);
        }

        if (currentPath.Samples.Length == cachedPath.Samples.Length)
        {
            return new WipeMaskUpdatePlan(
                WipeMaskUpdateKind.Reuse,
                currentPath.Samples.Length);
        }

        return new WipeMaskUpdatePlan(
            WipeMaskUpdateKind.Append,
            cachedPath.Samples.Length);
    }

    public static WipeMaskUpdatePlan Create(
        WipePathStream? cachedPath,
        WipeMaskGeometry? cachedGeometry,
        WipePathStream currentPath,
        WipeMaskGeometry currentGeometry)
    {
        ArgumentNullException.ThrowIfNull(currentPath);

        if (cachedPath is null ||
            cachedGeometry is null ||
            cachedGeometry.Value != currentGeometry ||
            cachedPath.DefinitionFingerprint != currentPath.DefinitionFingerprint ||
            cachedPath.Kind != currentPath.Kind ||
            cachedPath.LaneCount != currentPath.LaneCount ||
            currentPath.Frame < cachedPath.Frame)
        {
            return new WipeMaskUpdatePlan(WipeMaskUpdateKind.Rebuild, 0);
        }

        if (currentPath.Frame == cachedPath.Frame)
        {
            return new WipeMaskUpdatePlan(
                WipeMaskUpdateKind.Reuse,
                Math.Max(0, currentPath.Frame));
        }

        return new WipeMaskUpdatePlan(
            WipeMaskUpdateKind.Append,
            cachedPath.Frame == int.MaxValue
                ? int.MaxValue
                : Math.Max(0, cachedPath.Frame + 1));
    }
}
