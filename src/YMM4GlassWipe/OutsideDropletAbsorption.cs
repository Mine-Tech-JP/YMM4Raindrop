// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace YMM4GlassWipe;

internal sealed partial class OutsideDropletMergeSimulation
{
    private readonly Dictionary<SimulationSettings, OutsideDropletStaticLayout?> staticLayouts = new();
    private readonly Queue<SimulationSettings> staticLayoutOrder = new();

    // 初期静止配置はepochや再生履歴に依存せず、同じ設定から一度だけ構成する。
    private OutsideDropletStaticLayout? GetStaticLayout(SimulationSettings settings)
    {
        var key = settings with
        {
            LocalTimeSeconds = 0f,
            EpochIndex = 0,
            SpeedScale = settings.RainEnabled ? settings.SpeedScale : 1f,
            FallingRatio = settings.RainEnabled ? settings.FallingRatio : 0f,
        };
        if (staticLayouts.TryGetValue(key, out var cached)) return cached;
        var bounds = settings.DeformWithSurface ? settings.RegionPixelSize : settings.InputSize;
        OutsideDropletStaticLayout? layout;
        if (settings.RainEnabled)
        {
            // 後続epochも初回の静止吸収結果を使う。途中のepochを再生する必要はない。
            layout = TryBuildAbsorptionSchedule(settings, 0,
                settings.RainDurationSeconds + EpochBaseSeconds / settings.SpeedScale, out var initial)
                ? initial.FinalStaticLayout : null;
        }
        else
        {
            layout = OutsideDropletStaticLayout.TryCreate(bounds, settings.SizeScale, settings.Seed,
                settings.Amount, out var created, position => IsInitialPositionInside(settings, position)) ? created : null;
        }
        staticLayouts.Add(key, layout);
        staticLayoutOrder.Enqueue(key);
        while (staticLayoutOrder.Count > MaximumCachedSchedules)
            staticLayouts.Remove(staticLayoutOrder.Dequeue());
        return layout;
    }

    private static bool IsInitialPositionInside(SimulationSettings settings, Vector2 position)
    {
        if (settings.DeformWithSurface)
            return IsRegionUvInside(settings, position / settings.RegionPixelSize);
        return TryMapInputUvToRegionUv(settings, position / settings.InputSize, out var regionUv) &&
            IsRegionUvInside(settings, regionUv);
    }

    private bool TryBuildAbsorptionSchedule(SimulationSettings settings, long epochIndex,
        float epochDuration, out EpochSchedule schedule)
    {
        schedule = null!;
        settings = settings with { EpochIndex = epochIndex };
        var rain = settings.IsInitialRainEpoch ? new RainStaticTimeline(settings) : null;
        var layout = rain?.EmptyLayout ?? GetStaticLayout(settings);
        if (layout is null) return false;
        var staticGrowth = new StaticGrowthLedger(settings, layout, rain);
        var headCount = OutsideDropletFrequency.GetHeadCount(settings.FallFrequency);
        var heads = new HeadSchedule[headCount];
        for (var index = 0; index < headCount; index++)
        {
            // 先頭18候補の索引・乱数は従来どおり。追加分は別のemitterとして扱う。
            var layerIndex = index % 18 / 6;
            var emitter = index / 18 * 6 + index % 6;
            heads[index] = CreateHeadSchedule(settings, Layers[layerIndex], index, (uint)emitter);
        }
        var absorbed = new HashSet<StaticCellId>();
        var contacts = new List<MergeContact>(MaximumContactsPerEpoch);
        var pending = new MergeContact?[headCount];
        var pairContacts = new HeadPairContact?[headCount, headCount];
        // 追加候補による組合せ増加を決定的な仕事量で制限する。100%の旧判定は変えない。
        var remainingPairSamples = settings.FallFrequency > 1f ? 1_000_000 : int.MaxValue;
        for (var index = 0; index < heads.Length; index++)
            RefreshStatic(index);
        for (var first = 0; first < headCount; first++)
            for (var second = first + 1; second < headCount; second++)
                RefreshPair(first, second, 0f);

        for (var eventIndex = 0; eventIndex < MaximumContactsPerEpoch + headCount + (rain?.BirthCount ?? 0); eventIndex++)
        {
            var nextIndex = -1;
            for (var index = 0; index < pending.Length; index++)
                if (pending[index] is { } candidate && (nextIndex < 0 ||
                    MergeContact.Compare(candidate, pending[nextIndex]!.Value) < 0))
                    nextIndex = index;
            HeadPairContact? pair = null;
            for (var first = 0; first < headCount; first++)
                for (var second = first + 1; second < headCount; second++)
                    if (pairContacts[first, second] is { } candidate &&
                        (!pair.HasValue || candidate.Time < pair.Value.Time))
                        pair = candidate;
            var nextBirth = rain?.NextBirth ?? float.PositiveInfinity;
            var nextDynamic = MathF.Min(nextIndex >= 0 ? pending[nextIndex]!.Value.Time : float.PositiveInfinity,
                pair?.Time ?? float.PositiveInfinity);
            if (float.IsFinite(nextBirth) && nextBirth <= nextDynamic)
            {
                var changed = rain!.ProcessNextBirth(absorbed, staticGrowth);
                foreach (var candidateHead in heads)
                {
                    if (candidateHead.AbsorbedAt.HasValue || candidateHead.CandidateCells is null ||
                        !changed.Any(candidateHead.CandidateCells.ContainsKey)) continue;
                    candidateHead.SearchStart = MathF.Max(candidateHead.SearchStart, nextBirth);
                    RefreshStatic(candidateHead.Index);
                }
                continue;
            }
            if (nextIndex < 0 && !pair.HasValue)
            {
                rain?.CompleteReformation(absorbed, staticGrowth,
                    GetReformationStart(settings, heads));
                schedule = new EpochSchedule(settings, epochIndex, epochDuration, heads, contacts, staticGrowth);
                return true;
            }
            if (pair is { } nextPair && (nextIndex < 0 || nextPair.Time < pending[nextIndex]!.Value.Time))
            {
                var first = heads[nextPair.First];
                var second = heads[nextPair.Second];
                var firstState = EvaluateHead(settings, first, nextPair.Time);
                var secondState = EvaluateHead(settings, second, nextPair.Time);
                var firstWins = GetHeadArea(firstState) >= GetHeadArea(secondState);
                var winner = firstWins ? first : second;
                var loser = firstWins ? second : first;
                AddHeadGrowth(winner, nextPair.Time, default);
                loser.AbsorbedAt = nextPair.Time;
                loser.AbsorberInputUv = firstWins ? firstState.InputUv : secondState.InputUv;
                winner.SearchStart = nextPair.Time;
                pending[loser.Index] = null;
                RefreshStatic(winner.Index);
                RefreshHeadPairs(winner.Index, nextPair.Time);
                RefreshHeadPairs(loser.Index, nextPair.Time);
                continue;
            }

            var next = pending[nextIndex]!.Value;
            var head = heads[nextIndex];
            if (absorbed.Contains(next.Cell.Id) || !staticGrowth.IsAvailable(next.Cell, next.Time))
            {
                RefreshStatic(nextIndex);
                continue;
            }
            var headState = EvaluateHead(settings, head, next.Time);
            var staticRadius = next.Cell.RadiusPixels * staticGrowth.GetScale(next.Cell, next.Time);
            var staticArea = staticRadius * staticRadius * next.Cell.VerticalScale;
            if (GetHeadArea(headState) >= staticArea)
            {
                var growth = AddHeadGrowth(head, next.Time, next.Cell);
                contacts.Add(growth);
                absorbed.Add(next.Cell.Id);
                RefreshStatic(nextIndex);
            }
            else
            {
                if (!TryGetStaticInputUv(settings, next.Cell, out var targetUv)) return false;
                staticGrowth.Add(next.Cell, next.Time, GetHeadArea(headState));
                head.AbsorbedAt = next.Time;
                head.AbsorberInputUv = targetUv;
                pending[nextIndex] = null;
                // 静止の吸収先が膨らんだ場合だけ、そこへ接近中の候補を更新する。
                foreach (var other in heads)
                {
                    if (other.Index == nextIndex || other.AbsorbedAt.HasValue ||
                        other.CandidateCells?.ContainsKey(next.Cell.Id) != true) continue;
                    other.SearchStart = MathF.Max(other.SearchStart, next.Time);
                    RefreshStatic(other.Index);
                }
            }
            RefreshHeadPairs(nextIndex, next.Time);
        }
        return false;

        void RefreshStatic(int index)
        {
            var head = heads[index];
            pending[index] = head.IsEligible && !head.AbsorbedAt.HasValue &&
                head.Contacts.Count < MaximumContactsPerHead &&
                TryFindHeadContact(settings, head, absorbed, staticGrowth, out var next) ? next : null;
        }
        void RefreshPair(int first, int second, float after)
        {
            pairContacts[first, second] = TryFindHeadPairContact(settings, heads[first], heads[second],
                after, ref remainingPairSamples, out var time) ? new HeadPairContact(first, second, time) : null;
        }
        void RefreshHeadPairs(int index, float after)
        {
            for (var other = 0; other < headCount; other++)
                if (other != index) RefreshPair(Math.Min(index, other), Math.Max(index, other), after);
        }
    }

    private static MergeContact AddHeadGrowth(HeadSchedule head, float time, StaticCell cell)
    {
        var contact = new MergeContact(time, head.Index, cell)
        {
            RadiusIncrease = MathF.Max(0f, MathF.Min(RadiusIncreasePerContact,
                RadiusCap - 1f - head.GrowthContacts.Sum(item => item.RadiusIncrease))),
            SpeedIncrease = MathF.Max(0f, MathF.Min(SpeedIncreasePerContact,
                SpeedCap - 1f - head.GrowthContacts.Sum(item => item.SpeedIncrease))),
        };
        head.Contacts.Add(contact);
        if (contact.RadiusIncrease > 0f || contact.SpeedIncrease > 0f)
            head.GrowthContacts.Add(contact);
        return contact;
    }

    // 頭部の比較用面積。共通のπを省き、透明度や水筋の長さを大きさへ含めない。
    private static float GetHeadArea(OutsideDropletMergeHeadState head) =>
        head.Radius * head.Radius * head.VerticalScale;

    private static bool TryFindHeadPairContact(SimulationSettings settings, HeadSchedule first,
        HeadSchedule second, float after, ref int remainingPairSamples, out float contactTime)
    {
        contactTime = 0f;
        if (!first.IsEligible || !second.IsEligible || first.AbsorbedAt.HasValue || second.AbsorbedAt.HasValue ||
            first.Contacts.Count >= MaximumContactsPerHead || second.Contacts.Count >= MaximumContactsPerHead)
            return false;
        var start = MathF.Max(after, MathF.Max(first.ContactStart, second.ContactStart));
        var end = MathF.Min(first.FallEnd, second.FallEnd);
        if (start >= end ||
            !TryGetHeadPatternPosition(settings, first, start, out _, out _) ||
            !TryGetHeadPatternPosition(settings, second, start, out _, out _)) return false;
        // 領域外の射影微分で探索数が増えないよう、両headが見える区間に制限する。
        end = MathF.Min(FindVisibleContactEnd(settings, first, start, end),
            FindVisibleContactEnd(settings, second, start, end));
        if (start >= end) return false;
        var radiusA = first.Layer.CellSizePixels * settings.SizeScale * 0.21f * RadiusCap;
        var radiusB = second.Layer.CellSizePixels * settings.SizeScale * 0.21f * RadiusCap;
        var maximumWidth = settings.IsQuad && settings.DeformWithSurface
            ? MathF.Max(settings.InputSize.X, settings.InputSize.Y)
            : (radiusA + radiusB) * 2.60f;
        if (MathF.Abs(first.SpawnInputUv.X - second.SpawnInputUv.X) * settings.InputSize.X > maximumWidth)
            return false;
        var minimumRadius = (first.Layer.CellSizePixels + second.Layer.CellSizePixels) * settings.SizeScale * 0.11f;
        var speedA = (settings.RegionPixelSize.Y + first.Layer.CellSizePixels * settings.SizeScale) / first.FallDuration;
        var speedB = (settings.RegionPixelSize.Y + second.Layer.CellSizePixels * settings.SizeScale) / second.FallDuration;
        var maximumRelativeSpeed = MathF.Abs(speedA - speedB) + MathF.Max(speedA, speedB) * (SpeedCap - 1f);
        if (start < first.FallStart || start < second.FallStart)
            maximumRelativeSpeed = MathF.Max(speedA, speedB) * SpeedCap;
        if (settings.IsQuad && settings.DeformWithSurface)
        {
            var aEnd = first.SpawnInputUv + new Vector2(0f, GetFallDistance(settings, first, end) / settings.InputSize.Y);
            var bEnd = second.SpawnInputUv + new Vector2(0f, GetFallDistance(settings, second, end) / settings.InputSize.Y);
            var derivative = MathF.Max(MathF.Max(GetPatternDerivative(settings, first.SpawnInputUv).Length(),
                GetPatternDerivative(settings, aEnd).Length()), MathF.Max(
                GetPatternDerivative(settings, second.SpawnInputUv).Length(), GetPatternDerivative(settings, bEnd).Length()));
            maximumRelativeSpeed = (speedA + speedB) * SpeedCap * derivative;
        }
        var required = (int)MathF.Ceiling((end - start) * maximumRelativeSpeed / MathF.Max(minimumRadius * 0.30f, 0.1f));
        const int maximumPairSamples = 8192;
        if (required > maximumPairSamples) throw new ScheduleFallbackException();
        var count = Math.Max(required, 24);
        if (settings.FallFrequency > 1f)
        {
            // 始点1回と、交差した際の二分探索18回も含めた上界。
            remainingPairSamples -= count + 19;
            if (remainingPairSamples < 0) throw new ScheduleFallbackException();
        }
        var previous = start;
        if (GetHeadPairDistance(settings, first, second, start) <= 0f)
        {
            contactTime = start;
            return true;
        }
        for (var index = 1; index <= count; index++)
        {
            var current = Lerp(start, end, index / (float)count);
            if (GetHeadPairDistance(settings, first, second, current) > 0f)
            {
                previous = current;
                continue;
            }
            var lower = previous;
            var upper = current;
            for (var iteration = 0; iteration < 18; iteration++)
            {
                var middle = (lower + upper) * 0.5f;
                if (GetHeadPairDistance(settings, first, second, middle) <= 0f) upper = middle;
                else lower = middle;
            }
            contactTime = upper;
            return true;
        }
        return false;
    }

    private static float FindVisibleContactEnd(SimulationSettings settings, HeadSchedule head, float start, float end)
    {
        if (TryGetHeadPatternPosition(settings, head, end, out _, out _)) return end;
        var lower = start;
        var upper = end;
        for (var iteration = 0; iteration < 24; iteration++)
        {
            var middle = (lower + upper) * 0.5f;
            if (TryGetHeadPatternPosition(settings, head, middle, out _, out _)) lower = middle;
            else upper = middle;
        }
        return lower;
    }

    private static float GetHeadPairDistance(SimulationSettings settings, HeadSchedule first,
        HeadSchedule second, float time)
    {
        if (!TryGetHeadPatternPosition(settings, first, time, out var a, out var progressA) ||
            !TryGetHeadPatternPosition(settings, second, time, out var b, out var progressB))
            return float.PositiveInfinity;
        var stateA = EvaluateHead(settings, first, time);
        var stateB = EvaluateHead(settings, second, time);
        var offset = b - a;
        var radiusX = stateA.Radius * GetFallingWidthScale(
            offset.Y / MathF.Max(stateA.Radius * stateA.VerticalScale, 0.001f), progressA) +
            stateB.Radius * GetFallingWidthScale(
                -offset.Y / MathF.Max(stateB.Radius * stateB.VerticalScale, 0.001f), progressB);
        var radiusY = stateA.Radius * stateA.VerticalScale + stateB.Radius * stateB.VerticalScale;
        return new Vector2(offset.X / MathF.Max(radiusX, 0.001f),
            offset.Y / MathF.Max(radiusY, 0.001f)).Length() - 1f;
    }

    private readonly record struct HeadPairContact(int First, int Second, float Time);
    private readonly record struct StaticGrowthContact(StaticCell Cell, float Time, float Increase);

    private sealed class StaticGrowthLedger
    {
        private readonly SimulationSettings settings;
        private readonly Dictionary<StaticCellId, List<StaticGrowthContact>> growth = new();
        private readonly Dictionary<StaticCellId, float> reformationPhases = new();
        private readonly RainStaticTimeline? rain;
        public StaticGrowthLedger(SimulationSettings settings, OutsideDropletStaticLayout layout,
            RainStaticTimeline? rain = null)
        {
            this.settings = settings;
            Layout = layout;
            this.rain = rain;
        }
        public OutsideDropletStaticLayout Layout { get; }
        public OutsideDropletStaticLayout FinalLayout => rain?.FinalLayout ?? Layout;
        public OutsideDropletStaticLayout GetLayout(float time) => rain?.GetLayout(time) ?? Layout;
        public bool IsAvailable(StaticCell cell, float time) => rain?.IsAvailable(cell, time) ??
            !IsInitiallyAbsorbed(cell.Id);
        private float GetInitialScale(StaticCellId id, float time)
        {
            if (rain is not null) return rain.GetScale(id, time);
            Layout.TryGetState(id.Layer, id.CellX, id.CellY, out _, out var scale);
            return scale;
        }
        public bool IsInitiallyAbsorbed(StaticCellId id) => rain is null &&
            Layout.TryGetState(id.Layer, id.CellX, id.CellY, out var visibility, out _) && visibility == 0f;

        public void AssignReformationPhases(IEnumerable<StaticCell> consumed)
        {
            // 等分した時間枠を乱数順に割り当て、単純な乱数抽選による短時間への集中を避ける。
            var finalLayout = FinalLayout;
            var cells = consumed.Concat(growth.Values.Select(contacts => contacts[0].Cell))
                .DistinctBy(cell => cell.Id)
                .Where(cell => !finalLayout.TryGetState(cell.Id.Layer, cell.Id.CellX, cell.Id.CellY,
                    out var visibility, out _) || visibility > 0f)
                .OrderBy(cell => OutsideDropletRandom.Sample(
                    OutsideDropletRandom.GetCycleKey(cell.Key, unchecked((uint)settings.EpochIndex)), 10U))
                .ThenBy(cell => cell.Id).ToArray();
            for (var index = 0; index < cells.Length; index++)
            {
                var cell = cells[index];
                var key = OutsideDropletRandom.GetCycleKey(cell.Key, unchecked((uint)settings.EpochIndex));
                reformationPhases.Add(cell.Id,
                    (index + Lerp(0.1f, 0.9f, OutsideDropletRandom.Sample(key, 11U))) / cells.Length);
            }
        }

        public float GetReformationPhase(StaticCellId id) => reformationPhases.GetValueOrDefault(id);

        public float GetScale(StaticCell cell, float time, float epochDuration = float.PositiveInfinity,
            float reformationStart = float.PositiveInfinity)
        {
            var scale = GetInitialScale(cell.Id, time);
            if (!growth.TryGetValue(cell.Id, out var contacts)) return scale;
            var strength = float.IsFinite(epochDuration)
                ? 1f - GetReformationProgress(settings, time, reformationStart, epochDuration,
                    GetReformationPhase(cell.Id))
                : 1f;
            foreach (var contact in contacts)
                scale += contact.Increase * SmoothStep(contact.Time,
                    contact.Time + TransitionBaseSeconds / settings.SpeedScale, time) * strength;
            if (rain is not null)
            {
                var cellSize = Layers.First(layer => layer.Layer == cell.Id.Layer).CellSizePixels * settings.SizeScale;
                scale = MathF.Min(scale, OutsideDropletStaticLayout.GetRadiusLimit(cellSize,
                    cell.CenterPatternPosition, cell.RadiusPixels, cell.VerticalScale));
            }
            return scale;
        }
        public void Add(StaticCell cell, float time, float incomingArea)
        {
            var initialScale = GetInitialScale(cell.Id, time);
            if (!growth.TryGetValue(cell.Id, out var contacts))
                growth.Add(cell.Id, contacts = new List<StaticGrowthContact>());
            var committedScale = initialScale + contacts.Sum(contact => contact.Increase);
            var cellSize = Layers.First(layer => layer.Layer == cell.Id.Layer).CellSizePixels * settings.SizeScale;
            var limit = GetSafeStaticRadiusLimit(cell, cellSize, time);
            var area = MathF.Max(cell.RadiusPixels * cell.RadiusPixels * cell.VerticalScale, 0.001f);
            var increase = MathF.Sqrt(committedScale * committedScale + incomingArea / area) - committedScale;
            increase = MathF.Max(0f, MathF.Min(MathF.Min(increase, RadiusIncreasePerContact), limit - committedScale));
            contacts.Add(new StaticGrowthContact(cell, time, increase));
        }
        // 大きい静止滴へ吸収した後も、別の静止滴へ新しい突起を作らない範囲だけ成長させる。
        private float GetSafeStaticRadiusLimit(StaticCell cell, float cellSize, float time)
        {
            var limit = OutsideDropletStaticLayout.GetRadiusLimit(cellSize,
                cell.CenterPatternPosition, cell.RadiusPixels, cell.VerticalScale);
            var ownExtent = cell.RadiusPixels * MathF.Max(1f, cell.VerticalScale);
            var bounds = settings.DeformWithSurface ? settings.RegionPixelSize : settings.InputSize;
            foreach (var layer in Layers)
            {
                var neighborCellSize = MathF.Max(layer.CellSizePixels * settings.SizeScale, 1f);
                var reach = ownExtent * RadiusCap + neighborCellSize * 0.21f * 1.18f * RadiusCap;
                var minimum = Vector2.Max(Vector2.Zero, cell.CenterPatternPosition - new Vector2(reach));
                var maximum = Vector2.Min(bounds, cell.CenterPatternPosition + new Vector2(reach));
                for (var y = (int)MathF.Floor(minimum.Y / neighborCellSize);
                     y <= (int)MathF.Floor(maximum.Y / neighborCellSize); y++)
                    for (var x = (int)MathF.Floor(minimum.X / neighborCellSize);
                         x <= (int)MathF.Floor(maximum.X / neighborCellSize); x++)
                    {
                        var id = new StaticCellId(layer.Layer, x, y);
                        if (id == cell.Id || IsInitiallyAbsorbed(id) ||
                            !TryCreateStaticCell(settings, id, out var other) || !other.IsActive || !IsAvailable(other, time)) continue;
                        var otherScale = GetScale(other, time);
                        if (growth.TryGetValue(id, out var scheduled))
                        {
                            var initial = GetInitialScale(id, time);
                            otherScale = MathF.Max(otherScale, initial + scheduled.Sum(contact => contact.Increase));
                        }
                        var otherExtent = other.RadiusPixels * MathF.Max(1f, other.VerticalScale) * otherScale;
                        var clearance = Vector2.Distance(cell.CenterPatternPosition, other.CenterPatternPosition) - otherExtent;
                        limit = MathF.Min(limit, MathF.Max(1f, clearance / ownExtent));
                    }
            }
            return limit;
        }
        public Dictionary<StaticCellId, OutsideDropletStaticChange> Evaluate(float time, float reformationStart,
            float epochDuration)
        {
            var states = new Dictionary<StaticCellId, OutsideDropletStaticChange>();
            foreach (var contacts in growth.Values)
            {
                var cell = contacts[0].Cell;
                states[cell.Id] = new OutsideDropletStaticChange(cell.Key, cell.Id.Layer,
                    cell.Id.CellX, cell.Id.CellY, IsAvailable(cell, time) ? 1f : 0f,
                    GetScale(cell, time, epochDuration, reformationStart));
            }
            return states;
        }
    }
}
