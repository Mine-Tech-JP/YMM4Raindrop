// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace YMM4GlassWipe;

internal sealed partial class OutsideDropletMergeSimulation
{
    /// <summary>
    /// 雨の初回だけ、静止滴の出生を時刻順の局所イベントとして構築します。
    /// 構築後の参照は不変の予定表であり、シーク順序や前フレームに依存しません。
    /// </summary>
    private sealed class RainStaticTimeline
    {
        private const int MaximumCells = 106496;
        private const int MaximumNeighbors = 512;
        private const int MaximumNeighborChecks = 128000000;
        private const int MaximumPasses = 8;

        private readonly Dictionary<StaticCellId, RainCell> cells = new();
        private readonly Dictionary<(int X, int Y), List<RainCell>> neighborhoods = new();
        private readonly RainCell[] births;
        private readonly Vector3 columns;
        private readonly Vector3 counts;
        private readonly float neighborhoodSize;
        private readonly Dictionary<StaticCellId, List<RainState>> history = new();
        private int birthIndex;
        private int neighborChecks;
        private OutsideDropletStaticLayout? finalLayout;

        public RainStaticTimeline(SimulationSettings settings)
        {
            columns = Vector3.Zero;
            counts = Vector3.Zero;
            neighborhoodSize = MathF.Max(Layers[^1].CellSizePixels * settings.SizeScale, 1f);
            if (!float.IsFinite(neighborhoodSize) || !float.IsFinite(settings.SizeScale) || settings.SizeScale <= 0f)
                throw new ScheduleFallbackException();
            var bounds = settings.DeformWithSurface ? settings.RegionPixelSize : settings.InputSize;
            var total = 0;
            for (var layerIndex = 0; layerIndex < Layers.Length; layerIndex++)
            {
                var layer = Layers[layerIndex];
                var size = MathF.Max(layer.CellSizePixels * settings.SizeScale, 1f);
                var width = MathF.Floor(bounds.X / size) + 1f;
                var height = MathF.Floor(bounds.Y / size) + 1f;
                if (!float.IsFinite(width) || !float.IsFinite(height) || width < 1f || height < 1f ||
                    width * height > MaximumCells - total) throw new ScheduleFallbackException();
                columns[layerIndex] = width;
                counts[layerIndex] = width * height;
                total += (int)counts[layerIndex];
                for (var y = 0; y < (int)height; y++)
                    for (var x = 0; x < (int)width; x++)
                    {
                        var id = new StaticCellId(layer.Layer, x, y);
                        if (!TryCreateStaticCell(settings, id, out var cell) || !cell.IsActive ||
                            !IsCellInsideRegion(settings, cell)) continue;
                        var state = new RainCell(cell,
                            OutsideDropletRainTiming.GetAppearanceTimeSeconds(cell.Key, settings.RainStartSeconds,
                                settings.RainDurationSeconds) - settings.RainStartSeconds,
                            MathF.Min(1.075f, OutsideDropletStaticLayout.GetRadiusLimit(size,
                                cell.CenterPatternPosition, cell.RadiusPixels, cell.VerticalScale)));
                        cells.Add(id, state);
                        var grid = GetGrid(cell.CenterPatternPosition);
                        if (!neighborhoods.TryGetValue(grid, out var members))
                            neighborhoods.Add(grid, members = new List<RainCell>());
                        members.Add(state);
                    }
            }
            births = cells.Values.OrderBy(cell => cell.Birth).ThenBy(cell => cell,
                Comparer<RainCell>.Create(CompareRank)).ToArray();
        }

        public int BirthCount => births.Length;
        public float NextBirth => birthIndex < births.Length ? births[birthIndex].Birth : float.PositiveInfinity;
        public OutsideDropletStaticLayout FinalLayout => finalLayout ??= GetLayout(float.PositiveInfinity);
        public OutsideDropletStaticLayout EmptyLayout => OutsideDropletStaticLayout.CreateSnapshot(columns, counts,
            Array.Empty<OutsideDropletStaticChange>());

        public bool IsAvailable(StaticCell cell, float time) => cells.TryGetValue(cell.Id, out var state) &&
            time >= state.Birth && GetState(cell.Id, time).Visibility > 0f;
        public float GetScale(StaticCellId id, float time) => GetState(id, time).Scale;

        private RainState GetState(StaticCellId id, float time)
        {
            if (!history.TryGetValue(id, out var events)) return new RainState(0f, 1f, 1f);
            for (var index = events.Count - 1; index >= 0; index--)
                if (events[index].Time <= time) return events[index];
            return new RainState(0f, 1f, 1f);
        }

        // 同時出生はまとめてからサイズ順に解決し、列挙順で勝者が変わらないようにする。
        public HashSet<StaticCellId> ProcessNextBirth(HashSet<StaticCellId> consumed,
            StaticGrowthLedger growth)
        {
            var time = NextBirth;
            var candidates = new HashSet<RainCell>();
            while (birthIndex < births.Length && births[birthIndex].Birth == time)
            {
                var newborn = births[birthIndex++];
                foreach (var neighbor in GetNeighbors(newborn))
                    if (neighbor.Birth <= time && CanTouch(newborn, neighbor)) candidates.Add(neighbor);
            }
            return Resolve(candidates, time, consumed, growth);
        }

        public void CompleteReformation(HashSet<StaticCellId> consumed, StaticGrowthLedger growth, float time)
        {
            // headに消費された滴が戻る前に、その近傍だけを出生済み静止滴と再解決する。
            // 静止同士ですでに吸収された滴はVisible=falseのままなので復活しない。
            var candidates = new HashSet<RainCell>();
            foreach (var id in consumed.OrderBy(id => id))
            {
                if (!cells.TryGetValue(id, out var cell) || !cell.Visible) continue;
                foreach (var neighbor in GetNeighbors(cell))
                    if (neighbor.Birth <= time && CanTouch(cell, neighbor)) candidates.Add(neighbor);
            }
            Resolve(candidates, time, new HashSet<StaticCellId>(), growth);
        }

        private HashSet<StaticCellId> Resolve(HashSet<RainCell> candidates, float time,
            HashSet<StaticCellId> consumed, StaticGrowthLedger growth)
        {
            var changed = new HashSet<StaticCellId>();
            for (var pass = 0; pass < MaximumPasses; pass++)
            {
                var any = false;
                var expanded = new HashSet<RainCell>();
                foreach (var source in candidates.OrderBy(cell => cell, Comparer<RainCell>.Create(CompareRank)))
                {
                    if (!source.Visible || source.Birth > time || consumed.Contains(source.Cell.Id)) continue;
                    RainCell? owner = null;
                    var bestDistance = float.PositiveInfinity;
                    foreach (var candidate in GetNeighbors(source))
                    {
                        if (!candidate.Visible || candidate.Birth > time || consumed.Contains(candidate.Cell.Id) ||
                            CompareRank(source, candidate) >= 0) continue;
                        var distance = ContactDistance(source, candidate, growth, time);
                        if (distance <= 1f && (owner is null || distance < bestDistance ||
                            (distance == bestDistance && CompareRank(candidate, owner) < 0)))
                        {
                            owner = candidate;
                            bestDistance = distance;
                        }
                    }
                    if (owner is null) continue;
                    source.Visible = false;
                    Record(source, time, 0f, 1f);
                    changed.Add(source.Cell.Id);
                    if (!owner.Grown && owner.RadiusLimit > 1f)
                    {
                        owner.Grown = true;
                        Record(owner, time, 1f, owner.RadiusLimit);
                        changed.Add(owner.Cell.Id);
                        foreach (var neighbor in GetNeighbors(owner))
                            if (neighbor.Birth <= time && CanTouch(owner, neighbor)) expanded.Add(neighbor);
                    }
                    any = true;
                }
                if (!any) break;
                candidates.UnionWith(expanded);
            }
            return changed;
        }

        private void Record(RainCell cell, float time, float visibility, float scale)
        {
            if (!history.TryGetValue(cell.Cell.Id, out var events))
                history.Add(cell.Cell.Id, events = new List<RainState>(2));
            // 各セルには初期成長と静止同士の吸収をそれぞれ最大1件だけ記録する。
            if (events.Count >= 2) throw new ScheduleFallbackException();
            events.Add(new RainState(time, visibility, scale));
        }

        private IEnumerable<RainCell> GetNeighbors(RainCell cell)
        {
            var grid = GetGrid(cell.Cell.CenterPatternPosition);
            var count = 0;
            for (var y = grid.Y - 1; y <= grid.Y + 1; y++)
                for (var x = grid.X - 1; x <= grid.X + 1; x++)
                    if (neighborhoods.TryGetValue((x, y), out var members))
                        foreach (var candidate in members)
                        {
                            if (++count > MaximumNeighbors || ++neighborChecks > MaximumNeighborChecks)
                                throw new ScheduleFallbackException();
                            yield return candidate;
                        }
        }

        private (int X, int Y) GetGrid(Vector2 position) =>
            ((int)MathF.Floor(position.X / neighborhoodSize), (int)MathF.Floor(position.Y / neighborhoodSize));

        private static int CompareRank(RainCell left, RainCell right)
        {
            var mass = (left.Cell.RadiusPixels * left.Cell.RadiusPixels * left.Cell.VerticalScale)
                .CompareTo(right.Cell.RadiusPixels * right.Cell.RadiusPixels * right.Cell.VerticalScale);
            if (mass != 0) return mass;
            var key = left.Cell.Key.CompareTo(right.Cell.Key);
            return key != 0 ? key : left.Cell.Id.CompareTo(right.Cell.Id);
        }

        private static bool CanTouch(RainCell left, RainCell right)
        {
            var a = left.Cell.RadiusPixels * RadiusCap;
            var b = right.Cell.RadiusPixels * RadiusCap;
            var offset = left.Cell.CenterPatternPosition - right.Cell.CenterPatternPosition;
            return new Vector2(offset.X / MathF.Max(a + b, 0.001f),
                offset.Y / MathF.Max(a * left.Cell.VerticalScale + b * right.Cell.VerticalScale, 0.001f))
                .LengthSquared() <= 1f;
        }

        private static float ContactDistance(RainCell left, RainCell right, StaticGrowthLedger growth, float time)
        {
            var a = left.Cell.RadiusPixels * growth.GetScale(left.Cell, time);
            var b = right.Cell.RadiusPixels * growth.GetScale(right.Cell, time);
            var offset = left.Cell.CenterPatternPosition - right.Cell.CenterPatternPosition;
            return new Vector2(offset.X / MathF.Max(a + b, 0.001f),
                offset.Y / MathF.Max(a * left.Cell.VerticalScale + b * right.Cell.VerticalScale, 0.001f))
                .LengthSquared();
        }

        public OutsideDropletStaticLayout GetLayout(float time)
        {
            var changes = new List<OutsideDropletStaticChange>(history.Count);
            foreach (var id in history.Keys)
            {
                var state = GetState(id, time);
                if (state.Visibility == 1f && state.Scale == 1f) continue;
                changes.Add(new OutsideDropletStaticChange(cells[id].Cell.Key, id.Layer, id.CellX, id.CellY,
                    state.Visibility, state.Scale));
            }
            return OutsideDropletStaticLayout.CreateSnapshot(columns, counts, changes);
        }

        private readonly record struct RainState(float Time, float Visibility, float Scale);
        private sealed class RainCell(StaticCell cell, float birth, float radiusLimit)
        {
            public StaticCell Cell { get; } = cell;
            public float Birth { get; } = birth;
            public float RadiusLimit { get; } = radiusLimit;
            public bool Visible { get; set; } = true;
            public bool Grown { get; set; }
        }
    }
}