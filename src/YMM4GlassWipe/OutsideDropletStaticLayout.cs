// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace YMM4GlassWipe;

/// <summary>
/// 合体有効時に、初期から重なっている静止水滴の可視性と半径倍率を決定的に解決します。
/// 静止水滴の中心は動かさず、既存のHLSLが評価するセル内へ収まる範囲だけ半径を増やします。
/// </summary>
internal sealed class OutsideDropletStaticLayout
{
    private const int MaximumEnumeratedCells = 106496;
    private const int MaximumActiveCells = 106496;
    private const int MaximumResolutionPasses = 8;
    private const int MaximumNeighborCandidates = 512;
    private const int MaximumNeighborChecks = 128000000;
    private const float RadiusCap = 1.35f;
    private const float InitialRadiusCap = 1.075f;
    private const float BoundarySafetyMarginPixels = 0.001f;

    private static readonly LayerDefinition[] Layers =
    {
        new(20f, 1f, 101U),
        new(44f, 0.65f, 307U),
        new(92f, 0.25f, 701U),
    };

    private readonly IReadOnlyList<OutsideDropletStaticChange> changes;
    private readonly Dictionary<CellId, State> states;
    private readonly IReadOnlyList<uint> packedStates;

    private OutsideDropletStaticLayout(
        IReadOnlyList<OutsideDropletStaticChange> changes,
        Dictionary<CellId, State> states,
        IReadOnlyList<uint> packedStates,
        Vector3 columnCounts,
        Vector3 cellCounts)
    {
        this.changes = changes;
        this.states = states;
        this.packedStates = packedStates;
        ColumnCounts = columnCounts;
        CellCounts = cellCounts;
    }

    /// <summary>
    /// 静止水滴の初期吸収状態を作成します。列挙または局所近傍比較の上限を超える場合はfalseです。
    /// </summary>
    public static bool TryCreate(
        Vector2 patternSize,
        float sizeScale,
        uint seed,
        float amount,
        out OutsideDropletStaticLayout layout,
        Func<Vector2, bool>? isInsideRegion = null)
    {
        layout = null!;
        if (!IsFinite(patternSize) || patternSize.X <= 0f || patternSize.Y <= 0f ||
            !float.IsFinite(sizeScale) || sizeScale <= 0f || !float.IsFinite(amount))
        {
            return false;
        }

        var saturatedAmount = Math.Clamp(amount, 0f, 1f);
        var cells = new List<CellState>();
        var layouts = new LayerLayout[Layers.Length];
        var totalCellCount = 0;
        for (var layerIndex = 0; layerIndex < Layers.Length; layerIndex++)
        {
            var layer = Layers[layerIndex];
            var cellSize = MathF.Max(layer.CellSizePixels * sizeScale, 1f);
            if (!float.IsFinite(cellSize) ||
                !TryGetLastCellIndex(patternSize.X, cellSize, out var lastX) ||
                !TryGetLastCellIndex(patternSize.Y, cellSize, out var lastY))
            {
                return false;
            }

            var columns = (long)lastX + 1L;
            var rows = (long)lastY + 1L;
            var cellCount = columns * rows;
            if (columns > int.MaxValue || rows > int.MaxValue || cellCount > int.MaxValue ||
                totalCellCount > MaximumEnumeratedCells - cellCount)
            {
                return false;
            }

            layouts[layerIndex] = new LayerLayout(
                layer, cellSize, (int)columns, totalCellCount, (int)cellCount);
            totalCellCount += (int)cellCount;
        }

        var columnCounts = new Vector3(layouts[0].Columns, layouts[1].Columns, layouts[2].Columns);
        var cellCounts = new Vector3(layouts[0].CellCount, layouts[1].CellCount, layouts[2].CellCount);
        var packedStates = new uint[(totalCellCount + 15) / 16];
        foreach (var layoutEntry in layouts)
        {
            var layer = layoutEntry.Layer;
            // HLSLのfloorと同じく、端で切れる最後のセルも含めます。
            for (var cellY = 0; cellY < layoutEntry.Rows; cellY++)
            {
                for (var cellX = 0; cellX < layoutEntry.Columns; cellX++)
                {
                    var id = new CellId(layer.Layer, cellX, cellY);
                    var key = OutsideDropletRandom.GetCellKey(cellX, cellY, seed, layer.Layer);

                    var activation = OutsideDropletRandom.Sample(key, 0U);
                    if (activation > Math.Clamp(saturatedAmount * layer.ActivationScale, 0f, 1f))
                    {
                        continue;
                    }

                    var center = (new Vector2(cellX, cellY) + new Vector2(
                        Lerp(0.25f, 0.75f, OutsideDropletRandom.Sample(key, 1U)),
                        Lerp(0.25f, 0.75f, OutsideDropletRandom.Sample(key, 2U)))) * layoutEntry.CellSize;
                    if (isInsideRegion is not null && !isInsideRegion(center)) continue;
                    var radius = layoutEntry.CellSize * Lerp(0.11f, 0.21f, OutsideDropletRandom.Sample(key, 3U));
                    var verticalScale = Lerp(0.88f, 1.18f, OutsideDropletRandom.Sample(key, 4U));
                    if (!IsFinite(center) || !float.IsFinite(radius) || !float.IsFinite(verticalScale) ||
                        radius <= 0f || verticalScale <= 0f)
                    {
                        return false;
                    }

                    if (cells.Count >= MaximumActiveCells)
                    {
                        return false;
                    }

                    var packedIndex = layoutEntry.Offset + cellY * layoutEntry.Columns + cellX;
                    cells.Add(new CellState(
                        id, key, layoutEntry.CellSize, center, radius, verticalScale, packedIndex));
                }
            }
        }

        if (cells.Count == 0)
        {
            layout = new OutsideDropletStaticLayout(
                Array.Empty<OutsideDropletStaticChange>(),
                new Dictionary<CellId, State>(),
                packedStates,
                columnCounts,
                cellCounts);
            return true;
        }

        cells.Sort(CellState.CompareRank);
        var neighborhoodSize = MathF.Max(Layers[^1].CellSizePixels * sizeScale, 1f);
        if (!float.IsFinite(neighborhoodSize))
        {
            return false;
        }

        var neighborhoods = CreateNeighborhoods(cells, neighborhoodSize);
        var checkedNeighbors = 0;
        for (var pass = 0; pass < MaximumResolutionPasses; pass++)
        {
            var changed = false;
            foreach (var source in cells)
            {
                if (!source.IsVisible)
                {
                    continue;
                }

                var hasOwner = TryFindOwner(
                    source, neighborhoods, neighborhoodSize, ref checkedNeighbors, out var owner);
                if (checkedNeighbors > MaximumNeighborChecks)
                {
                    return false;
                }

                if (!hasOwner)
                {
                    continue;
                }

                owner.Absorb(source);
                changed = true;
            }

            if (!changed)
            {
                break;
            }
        }

        var changedCells = cells
            .Where(cell => !cell.IsVisible || cell.RadiusScale > 1f)
            .OrderBy(cell => cell.Id)
            .ToArray();
        var changes = new OutsideDropletStaticChange[changedCells.Length];
        var states = new Dictionary<CellId, State>(changedCells.Length);
        for (var index = 0; index < changedCells.Length; index++)
        {
            var cell = changedCells[index];
            var state = new State(cell.IsVisible ? 1f : 0f, cell.IsVisible ? cell.RadiusScale : 1f);
            changes[index] = new OutsideDropletStaticChange(
                cell.Key,
                cell.Id.Layer,
                cell.Id.CellX,
                cell.Id.CellY,
                state.Visibility,
                state.RadiusScale);
            states.Add(cell.Id, state);
            var wordIndex = cell.PackedIndex / 16;
            var bitOffset = (cell.PackedIndex % 16) * 2;
            var packedState = cell.IsVisible ? 2U : 1U;
            packedStates[wordIndex] |= packedState << bitOffset;
        }

        layout = new OutsideDropletStaticLayout(changes, states, packedStates, columnCounts, cellCounts);
        return true;
    }

    public IReadOnlyList<OutsideDropletStaticChange> Changes => changes;

    // 雨の出生予定表から、同じ2bit GPU形式の静止状態を構成します。
    internal static OutsideDropletStaticLayout CreateSnapshot(Vector3 columnCounts, Vector3 cellCounts,
        IEnumerable<OutsideDropletStaticChange> source)
    {
        var changes = source.OrderBy(change => change.Layer).ThenBy(change => change.CellY)
            .ThenBy(change => change.CellX).ToArray();
        var states = new Dictionary<CellId, State>(changes.Length);
        var packed = new uint[((int)(cellCounts.X + cellCounts.Y + cellCounts.Z) + 15) / 16];
        var result = new OutsideDropletStaticLayout(changes, states, packed, columnCounts, cellCounts);
        foreach (var change in changes)
        {
            states.Add(new CellId(change.Layer, change.CellX, change.CellY),
                new State(change.Visibility, change.RadiusScale));
            if (!result.TryGetCellIndex(change.Layer, change.CellX, change.CellY, out var index))
                throw new ArgumentOutOfRangeException(nameof(source));
            var bits = change.Visibility == 0f ? 1U : change.RadiusScale > 1f ? 2U : 0U;
            packed[index / 16] |= bits << ((int)(index % 16) * 2);
        }
        return result;
    }

    /// <summary>
    /// layer 101、307、701の順でrow-majorに詰めた、セル16個ごとの2bit状態です。
    /// bit 0は吸収済み、bit 1は初期半径が増えた静止滴を表します。
    /// </summary>
    public IReadOnlyList<uint> PackedStates => packedStates;

    /// <summary>各レイヤーのセル列数です。順序は101、307、701です。</summary>
    public Vector3 ColumnCounts { get; }

    /// <summary>各レイヤーのセル数です。順序は101、307、701です。</summary>
    public Vector3 CellCounts { get; }

    public bool TryGetCellIndex(uint layer, int x, int y, out uint index)
    {
        index = 0;
        var slot = layer == 101U ? 0 : layer == 307U ? 1 : layer == 701U ? 2 : -1;
        if (slot < 0 || x < 0 || y < 0) return false;
        var columns = (int)ColumnCounts[slot];
        var count = (int)CellCounts[slot];
        if (x >= columns || y >= count / columns) return false;
        var offset = slot == 0 ? 0 : slot == 1 ? (int)CellCounts.X : (int)(CellCounts.X + CellCounts.Y);
        index = (uint)(offset + y * columns + x);
        return true;
    }

    /// <summary>
    /// 変更された静止水滴の可視性と半径倍率を返します。未変更セルは1, 1でfalseです。
    /// </summary>
    public bool TryGetState(uint layer, int cellX, int cellY, out float visibility, out float radiusScale)
    {
        if (states.TryGetValue(new CellId(layer, cellX, cellY), out var state))
        {
            visibility = state.Visibility;
            radiusScale = state.RadiusScale;
            return true;
        }

        visibility = 1f;
        radiusScale = 1f;
        return false;
    }

    /// <summary>
    /// HLSLの単一セル評価へ収まる静止水滴の半径倍率上限を返します。
    /// すでに元半径が境界へ近い場合も、元の見た目を縮めないため1未満にはしません。
    /// </summary>
    public static float GetRadiusLimit(
        float cellSize,
        Vector2 centerPatternPosition,
        float baseRadius,
        float verticalScale)
    {
        if (!float.IsFinite(cellSize) || cellSize <= 0f || !IsFinite(centerPatternPosition) ||
            !float.IsFinite(baseRadius) || baseRadius <= 0f ||
            !float.IsFinite(verticalScale) || verticalScale <= 0f)
        {
            return 1f;
        }

        var positionInCell = centerPatternPosition -
            new Vector2(MathF.Floor(centerPatternPosition.X / cellSize),
                MathF.Floor(centerPatternPosition.Y / cellSize)) * cellSize;
        var horizontalDistance = MathF.Max(
            MathF.Min(positionInCell.X, cellSize - positionInCell.X) - BoundarySafetyMarginPixels,
            0f);
        var verticalDistance = MathF.Max(
            MathF.Min(positionInCell.Y, cellSize - positionInCell.Y) - BoundarySafetyMarginPixels,
            0f);
        var horizontalLimit = horizontalDistance / baseRadius;
        var verticalLimit = verticalDistance / (baseRadius * verticalScale);
        return Math.Clamp(MathF.Min(horizontalLimit, verticalLimit), 1f, RadiusCap);
    }

    private static Dictionary<GridCell, List<CellState>> CreateNeighborhoods(
        IEnumerable<CellState> cells,
        float neighborhoodSize)
    {
        var neighborhoods = new Dictionary<GridCell, List<CellState>>();
        foreach (var cell in cells)
        {
            var gridCell = GridCell.FromPosition(cell.Center, neighborhoodSize);
            if (!neighborhoods.TryGetValue(gridCell, out var members))
            {
                members = new List<CellState>();
                neighborhoods.Add(gridCell, members);
            }

            members.Add(cell);
        }

        return neighborhoods;
    }

    private static bool TryFindOwner(
        CellState source,
        IReadOnlyDictionary<GridCell, List<CellState>> neighborhoods,
        float neighborhoodSize,
        ref int checkedNeighbors,
        out CellState owner)
    {
        owner = null!;
        var origin = GridCell.FromPosition(source.Center, neighborhoodSize);
        var candidateCount = 0;
        var found = false;
        var bestDistance = float.PositiveInfinity;
        for (var y = origin.Y - 1; y <= origin.Y + 1; y++)
        {
            for (var x = origin.X - 1; x <= origin.X + 1; x++)
            {
                if (!neighborhoods.TryGetValue(new GridCell(x, y), out var members))
                {
                    continue;
                }

                foreach (var candidate in members)
                {
                    candidateCount++;
                    checkedNeighbors++;
                    if (candidateCount > MaximumNeighborCandidates)
                    {
                        checkedNeighbors = MaximumNeighborChecks + 1;
                        return false;
                    }

                    if (!candidate.IsVisible || CellState.CompareRank(source, candidate) >= 0 ||
                        !Touches(source, candidate, out var distance))
                    {
                        continue;
                    }

                    if (!found || distance < bestDistance ||
                        (distance == bestDistance && CellState.CompareRank(candidate, owner) < 0))
                    {
                        owner = candidate;
                        bestDistance = distance;
                        found = true;
                    }
                }
            }
        }

        return found;
    }

    private static bool Touches(CellState left, CellState right, out float normalizedDistance)
    {
        var horizontalRadius = left.Radius * left.RadiusScale + right.Radius * right.RadiusScale;
        var verticalRadius = left.Radius * left.VerticalScale * left.RadiusScale +
            right.Radius * right.VerticalScale * right.RadiusScale;
        var offset = left.Center - right.Center;
        normalizedDistance = new Vector2(
            offset.X / MathF.Max(horizontalRadius, 0.001f),
            offset.Y / MathF.Max(verticalRadius, 0.001f)).LengthSquared();
        return normalizedDistance <= 1f;
    }

    private static bool TryGetLastCellIndex(float length, float cellSize, out int lastCellIndex)
    {
        lastCellIndex = 0;
        var value = MathF.Floor(length / cellSize);
        if (!float.IsFinite(value) || value < 0f || value > int.MaxValue)
        {
            return false;
        }

        lastCellIndex = (int)value;
        return true;
    }

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    private static float Lerp(float from, float to, float amount) => from + (to - from) * amount;

    private readonly record struct LayerDefinition(float CellSizePixels, float ActivationScale, uint Layer);

    private readonly record struct LayerLayout(
        LayerDefinition Layer,
        float CellSize,
        int Columns,
        int Offset,
        int CellCount)
    {
        public int Rows => CellCount / Columns;
    }

    private readonly record struct CellId(uint Layer, int CellX, int CellY) : IComparable<CellId>
    {
        public int CompareTo(CellId other)
        {
            var layer = Layer.CompareTo(other.Layer);
            if (layer != 0)
            {
                return layer;
            }

            var x = CellX.CompareTo(other.CellX);
            return x != 0 ? x : CellY.CompareTo(other.CellY);
        }
    }

    private readonly record struct GridCell(int X, int Y)
    {
        public static GridCell FromPosition(Vector2 position, float size) => new(
            (int)MathF.Floor(position.X / size),
            (int)MathF.Floor(position.Y / size));
    }

    private readonly record struct State(float Visibility, float RadiusScale);

    private sealed class CellState
    {
        public CellState(
            CellId id,
            uint key,
            float cellSize,
            Vector2 center,
            float radius,
            float verticalScale,
            int packedIndex)
        {
            Id = id;
            Key = key;
            Center = center;
            Radius = radius;
            VerticalScale = verticalScale;
            PackedIndex = packedIndex;
            BaseMass = radius * radius * verticalScale;
            RadiusLimit = MathF.Min(RadiusCap,
                GetRadiusLimit(cellSize, center, radius, verticalScale));
        }

        public CellId Id { get; }
        public uint Key { get; }
        public Vector2 Center { get; }
        public float Radius { get; }
        public float VerticalScale { get; }
        public float BaseMass { get; }
        public int PackedIndex { get; }
        public float RadiusLimit { get; }
        public float RadiusScale { get; private set; } = 1f;
        public bool IsVisible { get; private set; } = true;
        // 初回の成長上限に達しても、重なる小さい水滴は吸収する。

        public void Absorb(CellState source)
        {
            RadiusScale = MathF.Min(RadiusLimit, InitialRadiusCap);
            source.IsVisible = false;
        }

        public static int CompareRank(CellState left, CellState right)
        {
            var mass = left.BaseMass.CompareTo(right.BaseMass);
            if (mass != 0)
            {
                return mass;
            }

            var key = left.Key.CompareTo(right.Key);
            return key != 0 ? key : left.Id.CompareTo(right.Id);
        }
    }
}
