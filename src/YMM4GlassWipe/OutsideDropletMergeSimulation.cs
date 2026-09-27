// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace YMM4GlassWipe;

/// <summary>
/// 合体有効時の外側水滴を、フレーム履歴へ依存せず再構築します。
/// GPU側はこの結果を使用し、静止滴の可視性と落下滴の形状を同じepochで評価します。
/// </summary>
internal sealed partial class OutsideDropletMergeSimulation
{
    private const int MaximumContactsPerHead = 64;
    // 頻度を上げても静止滴のGPU table容量に対する接触上限は増やさない。
    private const int MaximumContactsPerEpoch = OutsideDropletFrequency.BaseHeadCount * MaximumContactsPerHead;
    private const int MaximumCandidateCellsPerHead = 8192;
    private const int MaximumCachedSchedules = 8;
    private const float EpochBaseSeconds = 11f;
    private const float TransitionBaseSeconds = 0.18f;
    private const int MaximumContactSearchSamples = 2048;
    private const float RadiusIncreasePerContact = 0.075f;
    private const float SpeedIncreasePerContact = 0.055f;
    private const float RadiusCap = 1.35f;
    private const float SpeedCap = 1.45f;

    private static readonly LayerDefinition[] Layers =
    {
        new(20f, 1f, 101U, 0.75f, 0.90f),
        new(44f, 0.65f, 307U, 1f, 1f),
        new(92f, 0.25f, 701U, 1.25f, 1.15f),
    };

    private readonly Dictionary<ScheduleKey, EpochSchedule?> schedules = new();
    private readonly Queue<ScheduleKey> scheduleOrder = new();

    /// <summary>
    /// 定数バッファから現在時刻の決定的な合体状態を求めます。
    /// 無効な領域・行列・上限超過時は <see cref="OutsideDropletMergeFrame.IsValid"/> がfalseです。
    /// </summary>
    public OutsideDropletMergeFrame Evaluate(in GlassCompositeConstants constants)
    {
        if (!TryCreateSettings(in constants, out var settings))
        {
            return OutsideDropletMergeFrame.Invalid;
        }

        var epochDuration = EpochBaseSeconds / settings.SpeedScale;
        var localTime = MathF.Max(settings.LocalTimeSeconds, 0f);
        var epochIndex = (long)MathF.Floor(localTime / epochDuration);
        var epochStart = epochIndex * epochDuration;
        var epochTime = Math.Clamp(localTime - epochStart, 0f, epochDuration);
        if (settings.RainEnabled)
        {
            // 初回だけ全候補の出生と落下が収まる区間へ延長し、後続で出生をリセットしない。
            var rainTime = localTime - settings.RainStartSeconds;
            var initialDuration = settings.RainDurationSeconds + epochDuration;
            if (rainTime < initialDuration)
            {
                epochIndex = 0;
                epochDuration = initialDuration;
                epochTime = rainTime;
            }
            else
            {
                var repeatedTime = rainTime - initialDuration;
                epochIndex = 1 + (long)MathF.Floor(repeatedTime / epochDuration);
                epochTime = Math.Clamp(repeatedTime - (epochIndex - 1) * epochDuration, 0f, epochDuration);
            }
        }
        var key = new ScheduleKey(settings with { LocalTimeSeconds = 0f }, epochIndex);
        if (schedules.TryGetValue(key, out var cachedSchedule))
        {
            if (cachedSchedule is null)
            {
                return OutsideDropletMergeFrame.Invalid;
            }

            return cachedSchedule.Evaluate(epochTime);
        }

        EpochSchedule? schedule;
        try
        {
            if (!TryBuildAbsorptionSchedule(settings, epochIndex, epochDuration, out var builtSchedule))
            {
                AddSchedule(key, null);
                return OutsideDropletMergeFrame.Invalid;
            }

            schedule = builtSchedule;
        }
        catch (ScheduleFallbackException)
        {
            AddSchedule(key, null);
            return OutsideDropletMergeFrame.Invalid;
        }

        AddSchedule(key, schedule);
        return schedule.Evaluate(epochTime);
    }

    private void AddSchedule(ScheduleKey key, EpochSchedule? schedule)
    {
        schedules.Add(key, schedule);
        scheduleOrder.Enqueue(key);
        while (scheduleOrder.Count > MaximumCachedSchedules)
        {
            schedules.Remove(scheduleOrder.Dequeue());
        }
    }

    private static HeadSchedule CreateHeadSchedule(
        SimulationSettings settings,
        LayerDefinition layer,
        int index,
        uint emitterInLayer)
    {
        var emitterKey = OutsideDropletRandom.GetEmitterKey(
            settings.Seed,
            layer.Layer,
            emitterInLayer);
        var active = OutsideDropletRandom.Sample(emitterKey, 0U) <=
            Saturate(settings.Amount * layer.ActivationScale) &&
            OutsideDropletFrequency.IsEmitterEnabled(emitterKey, emitterInLayer, settings.FallFrequency);
        var selected = OutsideDropletRandom.Sample(emitterKey, 8U) <=
            Saturate(settings.FallingRatio * layer.SelectionScale);
        var cycleKey = OutsideDropletRandom.GetCycleKey(emitterKey, unchecked((uint)settings.EpochIndex));
        var birth = OutsideDropletRandom.Sample(cycleKey, 7U) * (1.5f / settings.SpeedScale);
        if (settings.IsInitialRainEpoch)
            birth = OutsideDropletRainTiming.GetAppearanceTimeSeconds(emitterKey, settings.RainStartSeconds,
                settings.RainDurationSeconds) - settings.RainStartSeconds;
        var fallStart = birth + (0.3f + 0.5f * OutsideDropletRandom.Sample(cycleKey, 5U)) /
            settings.SpeedScale;
        var fallDuration = 6f / (settings.SpeedScale * layer.SizeSpeedScale);
        var spawnRegionUv = GetSpawnRegionUv(settings, cycleKey);
        var spawnValid = TryMapRegionUvToInputUv(settings, spawnRegionUv, out var spawnInputUv);
        return new HeadSchedule(
            index,
            layer,
            active && selected && spawnValid,
            cycleKey,
            spawnRegionUv,
            spawnInputUv,
            birth,
            fallStart,
            fallDuration);
    }

    private static bool TryFindHeadContact(
        SimulationSettings settings,
        HeadSchedule head,
        HashSet<StaticCellId> absorbed,
        StaticGrowthLedger staticGrowth,
        out MergeContact contact)
    {
        contact = default;
        if (head.AbsorbedAt.HasValue) return false;
        var candidateIds = head.CandidateIds;
        var candidateCells = head.CandidateCells;
        if (candidateIds is null || candidateCells is null)
        {
            candidateIds = new HashSet<StaticCellId>();
            if (!CollectCandidateCells(settings, head, candidateIds) ||
                candidateIds.Count > MaximumCandidateCellsPerHead)
            {
                throw new ScheduleFallbackException();
            }

            candidateCells = new Dictionary<StaticCellId, StaticCell>(candidateIds.Count);
            foreach (var id in candidateIds)
            {
                if (TryCreateStaticCell(settings, id, out var staticCell))
                {
                    candidateCells.Add(id, staticCell);
                }
            }

            head.CandidateIds = candidateIds;
            head.CandidateCells = candidateCells;
            head.OrderedCandidates = candidateCells.Values
                .Where(staticCell => staticCell.IsActive && IsCellInsideRegion(settings, staticCell) &&
                    !staticGrowth.IsInitiallyAbsorbed(staticCell.Id))
                .Select(staticCell => CreateStaticCandidate(settings, head, staticCell))
                .Where(candidate => candidate.HasValue)
                .Select(candidate => candidate!.Value)
                .OrderBy(candidate => candidate.MinimumInputY)
                .ToArray();
        }

        var found = false;
        var currentInputY = head.SpawnInputUv.Y +
            GetFallDistance(settings, head, head.LastContactTime) / settings.InputSize.Y;
        var bestInputY = float.PositiveInfinity;
        foreach (var candidateCell in head.OrderedCandidates!)
        {
            if (candidateCell.MaximumInputY < currentInputY)
            {
                continue;
            }

            if (found && candidateCell.MinimumInputY > bestInputY)
            {
                break;
            }

            var cell = candidateCell.Cell;
            if (absorbed.Contains(cell.Id))
            {
                continue;
            }

            if (!TryFindContactTime(settings, head, candidateCell, staticGrowth, out var time))
            {
                continue;
            }

            var candidate = new MergeContact(time, head.Index, cell);
            if (!found || MergeContact.Compare(candidate, contact) < 0)
            {
                contact = candidate;
                bestInputY = head.SpawnInputUv.Y +
                    GetFallDistance(settings, head, candidate.Time) / settings.InputSize.Y;
                found = true;
            }
        }

        return found;
    }

    // 射影後も画面Yの軌道は直線。時間標本ではなく連続した線分をセル幅以下で走査する。
    private static bool CollectCandidateCells(
        SimulationSettings settings, HeadSchedule head, HashSet<StaticCellId> candidateIds)
    {
        var origin = settings.DeformWithSurface
            ? head.SpawnRegionUv * settings.RegionPixelSize
            : head.SpawnInputUv * settings.InputSize;
        var direction = GetPatternDerivative(settings, head.SpawnInputUv);
        if (!IsFinite(direction) || direction.LengthSquared() < 1e-12f)
        {
            throw new ScheduleFallbackException();
        }
        direction = Vector2.Normalize(direction);
        var bounds = settings.DeformWithSurface ? settings.RegionPixelSize : settings.InputSize;
        var lower = 0f;
        var upper = float.PositiveInfinity;
        for (var axis = 0; axis < 2; axis++)
        {
            var o = axis == 0 ? origin.X : origin.Y;
            var d = axis == 0 ? direction.X : direction.Y;
            var limit = axis == 0 ? bounds.X : bounds.Y;
            if (MathF.Abs(d) < 1e-7f)
            {
                if (o < 0f || o > limit) return true;
                continue;
            }
            var t0 = -o / d;
            var t1 = (limit - o) / d;
            lower = MathF.Max(lower, MathF.Min(t0, t1));
            upper = MathF.Min(upper, MathF.Max(t0, t1));
        }
        if (upper < lower || !float.IsFinite(upper)) return true;
        var headRadius = head.Layer.CellSizePixels * settings.SizeScale * 0.21f * RadiusCap;
        foreach (var layer in Layers)
        {
            var cellSize = MathF.Max(layer.CellSizePixels * settings.SizeScale, 1f);
            var sampleCount = (int)MathF.Ceiling((upper - lower) / (cellSize * 0.5f));
            if (sampleCount > MaximumCandidateCellsPerHead) throw new ScheduleFallbackException();
            sampleCount = Math.Max(sampleCount, 1);
            var reachX = (int)MathF.Ceiling((headRadius * 1.505f + cellSize * 0.21f * RadiusCap) / cellSize) + 1;
            var reachY = (int)MathF.Ceiling((headRadius * 2.20f * 1.18f + cellSize * 0.21f * 1.18f * RadiusCap) / cellSize) + 1;
            for (var index = 0; index <= sampleCount; index++)
            {
                var position = origin + direction * Lerp(lower, upper, index / (float)sampleCount);
                var cellX = (int)MathF.Floor(position.X / cellSize);
                var cellY = (int)MathF.Floor(position.Y / cellSize);
                for (var y = Math.Max(0, cellY - reachY); y <= Math.Min((int)MathF.Floor(bounds.Y / cellSize), cellY + reachY); y++)
                    for (var x = Math.Max(0, cellX - reachX); x <= Math.Min((int)MathF.Floor(bounds.X / cellSize), cellX + reachX); x++)
                    {
                        candidateIds.Add(new StaticCellId(layer.Layer, x, y));
                        if (candidateIds.Count > MaximumCandidateCellsPerHead) throw new ScheduleFallbackException();
                    }
            }
        }
        return true;
    }

    // 接触し得るhead中心の矩形を入力空間へ写し、同じ単位で時間窓と横方向の除外を求める。
    private static StaticCandidate? CreateStaticCandidate(
        SimulationSettings settings, HeadSchedule head, StaticCell cell)
    {
        var radius = head.Layer.CellSizePixels * settings.SizeScale * 0.21f * RadiusCap;
        var extent = new Vector2(radius * 1.505f + cell.RadiusPixels * RadiusCap,
            radius * 2.20f * 1.18f + cell.RadiusPixels * cell.VerticalScale * RadiusCap);
        var minimum = new Vector2(float.PositiveInfinity);
        var maximum = new Vector2(float.NegativeInfinity);
        var denominatorSign = 0;
        for (var corner = 0; corner < 4; corner++)
        {
            var position = cell.CenterPatternPosition + extent * new Vector2(
                (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f);
            Vector2 inputUv;
            if (settings.DeformWithSurface)
            {
                var regionUv = position / settings.RegionPixelSize;
                if (settings.IsQuad)
                {
                    var denominator = Vector3.Dot(settings.ForwardRow2, new Vector3(regionUv, 1f));
                    if (MathF.Abs(denominator) < 1e-6f ||
                        (denominatorSign != 0 && Math.Sign(denominator) != denominatorSign))
                        throw new ScheduleFallbackException();
                    denominatorSign = Math.Sign(denominator);
                }
                if (!TryMapRegionUvToInputUv(settings, regionUv, out inputUv))
                    throw new ScheduleFallbackException();
            }
            else inputUv = position / settings.InputSize;
            minimum = Vector2.Min(minimum, inputUv);
            maximum = Vector2.Max(maximum, inputUv);
        }
        if (head.SpawnInputUv.X < minimum.X || head.SpawnInputUv.X > maximum.X ||
            maximum.Y < head.SpawnInputUv.Y) return null;
        return new StaticCandidate(cell, minimum.Y, maximum.Y);
    }

    // 入力Yを1px動かしたときのpattern位置の解析微分。透視倍率の過小評価を避ける。
    private static Vector2 GetPatternDerivative(SimulationSettings settings, Vector2 inputUv)
    {
        if (!settings.DeformWithSurface) return Vector2.UnitY;
        if (!settings.IsQuad) return new Vector2(settings.RotationSin, settings.RotationCos);
        var point = new Vector3(inputUv, 1f);
        var denominator = Vector3.Dot(settings.InverseRow2, point);
        if (MathF.Abs(denominator) < 1e-6f) throw new ScheduleFallbackException();
        var u = Vector3.Dot(settings.InverseRow0, point);
        var v = Vector3.Dot(settings.InverseRow1, point);
        return new Vector2(
            settings.InverseRow0.Y * denominator - u * settings.InverseRow2.Y,
            settings.InverseRow1.Y * denominator - v * settings.InverseRow2.Y) *
            settings.RegionPixelSize / (denominator * denominator * settings.InputSize.Y);
    }

    private static bool TryFindContactTime(
        SimulationSettings settings,
        HeadSchedule head,
        StaticCandidate candidate,
        StaticGrowthLedger staticGrowth,
        out float contactTime)
    {
        var cell = candidate.Cell;
        var baseDistance = settings.RegionPixelSize.Y + head.Layer.CellSizePixels * settings.SizeScale;
        var lowerDistance = (candidate.MinimumInputY - head.SpawnInputUv.Y) * settings.InputSize.Y;
        var upperDistance = (candidate.MaximumInputY - head.SpawnInputUv.Y) * settings.InputSize.Y;
        var start = MathF.Max(head.LastContactTime, lowerDistance <= 0f
            ? head.ContactStart
            : FindFallTimeForDistance(settings, head, lowerDistance));
        if (settings.IsInitialRainEpoch)
            start = MathF.Max(start, OutsideDropletRainTiming.GetAppearanceTimeSeconds(cell.Key, settings.RainStartSeconds,
                settings.RainDurationSeconds) - settings.RainStartSeconds);
        var end = FindFallTimeForDistance(settings, head, MathF.Max(upperDistance, 0f));
        if (end <= start)
        {
            contactTime = 0f;
            return false;
        }

        var previousTime = start;
        var previousDistance = GetContactDistance(settings, head, cell, staticGrowth, previousTime);
        if (previousDistance <= 0f)
        {
            contactTime = previousTime;
            return true;
        }

        var smallestContactRadius = MathF.Max(
            head.Layer.CellSizePixels * settings.SizeScale * 0.11f + cell.RadiusPixels,
            0.25f);
        var startInput = head.SpawnInputUv + new Vector2(0f,
            GetFallDistance(settings, head, start) / settings.InputSize.Y);
        var endInput = head.SpawnInputUv + new Vector2(0f,
            GetFallDistance(settings, head, end) / settings.InputSize.Y);
        if (settings.IsQuad && settings.DeformWithSurface &&
            Vector3.Dot(settings.InverseRow2, new Vector3(startInput, 1f)) *
            Vector3.Dot(settings.InverseRow2, new Vector3(endInput, 1f)) <= 0f)
            throw new ScheduleFallbackException();
        var maximumPatternDerivative = MathF.Max(
            GetPatternDerivative(settings, startInput).Length(), GetPatternDerivative(settings, endInput).Length());
        var requiredSamples = (int)MathF.Ceiling(
            (end - start) * baseDistance / head.FallDuration * SpeedCap * maximumPatternDerivative /
            MathF.Max(smallestContactRadius * 0.30f, 0.1f));
        if (requiredSamples > MaximumContactSearchSamples) throw new ScheduleFallbackException();
        var searchSamples = Math.Max(requiredSamples, 12);
        for (var sampleIndex = 1; sampleIndex <= searchSamples; sampleIndex++)
        {
            var currentTime = start + (end - start) * sampleIndex / searchSamples;
            var currentDistance = GetContactDistance(settings, head, cell, staticGrowth, currentTime);
            if (currentDistance > 0f)
            {
                previousTime = currentTime;
                previousDistance = currentDistance;
                continue;
            }

            var lower = previousTime;
            var upper = currentTime;
            for (var iteration = 0; iteration < 18; iteration++)
            {
                var middle = (lower + upper) * 0.5f;
                if (GetContactDistance(settings, head, cell, staticGrowth, middle) <= 0f)
                {
                    upper = middle;
                }
                else
                {
                    lower = middle;
                }
            }

            contactTime = upper;
            return true;
        }

        contactTime = 0f;
        return false;
    }

    private static float FindFallTimeForDistance(
        SimulationSettings settings,
        HeadSchedule head,
        float distance)
    {
        if (distance <= 0f)
        {
            return head.FallStart;
        }

        if (distance >= GetFallDistance(settings, head, head.FallEnd))
        {
            return head.FallEnd;
        }

        var lower = head.FallStart;
        var upper = head.FallEnd;
        for (var iteration = 0; iteration < 20; iteration++)
        {
            var middle = (lower + upper) * 0.5f;
            if (GetFallDistance(settings, head, middle) < distance)
            {
                lower = middle;
            }
            else
            {
                upper = middle;
            }
        }

        return upper;
    }
    private static float GetContactDistance(
        SimulationSettings settings,
        HeadSchedule head,
        StaticCell cell,
        StaticGrowthLedger staticGrowth,
        float time)
    {
        if (!TryGetHeadPatternPosition(settings, head, time, out var headPosition, out var fallProgress))
        {
            return float.PositiveInfinity;
        }

        if (!staticGrowth.IsAvailable(cell, time)) return float.PositiveInfinity;
        var radiusScale = GetRadiusScale(settings, head, time);
        var baseRadius = head.Layer.CellSizePixels * settings.SizeScale * Lerp(
            0.11f,
            0.21f,
            OutsideDropletRandom.Sample(head.CycleKey, 3U));
        var baseVerticalScale = Lerp(
            0.88f,
            1.18f,
            OutsideDropletRandom.Sample(head.CycleKey, 4U));
        var dynamicVerticalScale = baseVerticalScale * Lerp(
            1f,
            2.20f,
            SmoothStep(0.05f, 0.70f, fallProgress));
        var offset = cell.CenterPatternPosition - headPosition;
        var normalizedY = offset.Y / MathF.Max(baseRadius * radiusScale * dynamicVerticalScale, 0.001f);
        var widthScale = GetFallingWidthScale(normalizedY, fallProgress);
        var staticRadius = cell.RadiusPixels * staticGrowth.GetScale(cell, time);
        var horizontalRadius = baseRadius * radiusScale * widthScale + staticRadius;
        var verticalRadius = baseRadius * radiusScale * dynamicVerticalScale +
            staticRadius * cell.VerticalScale;
        var distance = new Vector2(
            offset.X / MathF.Max(horizontalRadius, 0.001f),
            offset.Y / MathF.Max(verticalRadius, 0.001f)).Length() - 1f;
        return distance;
    }

    private static bool TryGetHeadPatternPosition(
        SimulationSettings settings,
        HeadSchedule head,
        float time,
        out Vector2 patternPosition,
        out float fallProgress)
    {
        patternPosition = Vector2.Zero;
        fallProgress = 0f;
        if (time < head.ContactStart || time > head.FallEnd)
        {
            return false;
        }

        var fallDistance = GetFallDistance(settings, head, time);
        fallProgress = Saturate(fallDistance / MathF.Max(
            settings.RegionPixelSize.Y + head.Layer.CellSizePixels * settings.SizeScale,
            0.001f));
        var headInputUv = head.SpawnInputUv + new Vector2(0f, fallDistance / settings.InputSize.Y);
        if (settings.DeformWithSurface)
        {
            if (!TryMapInputUvToRegionUv(settings, headInputUv, out var regionUv) ||
                !IsRegionUvInside(settings, regionUv))
            {
                return false;
            }

            patternPosition = regionUv * settings.RegionPixelSize;
            return true;
        }

        if (!TryMapInputUvToRegionUv(settings, headInputUv, out var inputRegionUv) ||
            !IsRegionUvInside(settings, inputRegionUv))
        {
            return false;
        }

        patternPosition = headInputUv * settings.InputSize;
        return true;
    }

    private static float GetFallDistance(SimulationSettings settings, HeadSchedule head, float time)
    {
        if (time <= head.FallStart) return 0f;
        var elapsed = Math.Clamp(time - head.FallStart, 0f, head.FallDuration);
        var baseDistance = settings.RegionPixelSize.Y + head.Layer.CellSizePixels * settings.SizeScale;
        var baseSpeed = baseDistance / head.FallDuration;
        var multiplierIntegral = elapsed;
        foreach (var existing in head.GrowthContacts)
        {
            if (existing.Time >= time)
            {
                break;
            }

            var increase = existing.SpeedIncrease;
            if (increase <= 0f)
            {
                continue;
            }

            var transition = TransitionBaseSeconds / settings.SpeedScale;
            // 待機中の吸収による加速も、落下開始以降の時間だけを積分する。
            multiplierIntegral += increase * (IntegrateSmoothStep(
                MathF.Min(time, head.FallEnd) - existing.Time, transition) -
                IntegrateSmoothStep(head.FallStart - existing.Time, transition));
        }

        return baseSpeed * multiplierIntegral;
    }

    private static float GetRadiusScale(SimulationSettings settings, HeadSchedule head, float time)
    {
        var result = 1f;
        foreach (var contact in head.GrowthContacts)
        {
            if (contact.Time >= time)
            {
                break;
            }

            var before = result;
            var increase = contact.RadiusIncrease;
            if (increase <= 0f)
            {
                continue;
            }

            result = before + increase * SmoothStep(
                0f,
                TransitionBaseSeconds / settings.SpeedScale,
                time - contact.Time);
        }

        return MathF.Min(result, RadiusCap);
    }

    private static bool TryCreateStaticCell(
        SimulationSettings settings,
        StaticCellId id,
        out StaticCell cell)
    {
        cell = default;
        var layer = Array.Find(Layers, candidate => candidate.Layer == id.Layer);
        if (layer == default)
        {
            return false;
        }

        var cellSize = MathF.Max(layer.CellSizePixels * settings.SizeScale, 1f);
        var key = OutsideDropletRandom.GetCellKey(id.CellX, id.CellY, settings.Seed, id.Layer);
        var activation = OutsideDropletRandom.Sample(key, 0U);
        var center = new Vector2(
            Lerp(0.25f, 0.75f, OutsideDropletRandom.Sample(key, 1U)),
            Lerp(0.25f, 0.75f, OutsideDropletRandom.Sample(key, 2U)));
        cell = new StaticCell(
            id,
            key,
            activation <= Saturate(settings.Amount * layer.ActivationScale),
            (new Vector2(id.CellX, id.CellY) + center) * cellSize,
            cellSize * Lerp(0.11f, 0.21f, OutsideDropletRandom.Sample(key, 3U)),
            Lerp(0.88f, 1.18f, OutsideDropletRandom.Sample(key, 4U)));
        return true;
    }

    private static bool TryGetStaticInputUv(
        SimulationSettings settings,
        StaticCell cell,
        out Vector2 inputUv)
    {
        if (!settings.DeformWithSurface)
        {
            inputUv = cell.CenterPatternPosition / settings.InputSize;
            return IsFinite(inputUv);
        }

        return TryMapRegionUvToInputUv(
            settings,
            cell.CenterPatternPosition / settings.RegionPixelSize,
            out inputUv);
    }
    private static bool IsCellInsideRegion(SimulationSettings settings, StaticCell cell)
    {
        if (settings.DeformWithSurface)
        {
            var regionUv = cell.CenterPatternPosition / settings.RegionPixelSize;
            return IsRegionUvInside(settings, regionUv) &&
                TryMapRegionUvToInputUv(settings, regionUv, out _);
        }

        var inputUv = cell.CenterPatternPosition / settings.InputSize;
        return TryMapInputUvToRegionUv(settings, inputUv, out var mappedRegionUv)
            && IsRegionUvInside(settings, mappedRegionUv);
    }

    private static OutsideDropletMergeHeadState EvaluateHead(
        SimulationSettings settings,
        HeadSchedule head,
        float epochTime)
    {
        var radius = head.Layer.CellSizePixels * settings.SizeScale * Lerp(
            0.11f,
            0.21f,
            OutsideDropletRandom.Sample(head.CycleKey, 3U));
        var vertical = Lerp(0.88f, 1.18f, OutsideDropletRandom.Sample(head.CycleKey, 4U));
        var visibility = 0f;
        var fallProgress = 0f;
        var inputUv = head.SpawnInputUv;
        var motionTime = head.AbsorbedAt.HasValue ? MathF.Min(epochTime, head.AbsorbedAt.Value) : epochTime;
        if (head.IsEligible && epochTime >= head.Birth && epochTime < head.FallStart)
        {
            visibility = SmoothStep(head.Birth, head.FallStart, epochTime);
        }
        else if (head.IsEligible && epochTime >= head.FallStart && epochTime <= head.FallEnd)
        {
            var fallDistance = GetFallDistance(settings, head, motionTime);
            inputUv += new Vector2(0f, fallDistance / settings.InputSize.Y);
            fallProgress = Saturate(fallDistance / MathF.Max(
                settings.RegionPixelSize.Y + head.Layer.CellSizePixels * settings.SizeScale,
                0.001f));
            vertical *= Lerp(1f, 2.20f, SmoothStep(0.05f, 0.70f, fallProgress));
            visibility = 1f - SmoothStep(
                head.FallEnd - TransitionBaseSeconds / settings.SpeedScale,
                head.FallEnd,
                epochTime);
        }

        radius *= GetRadiusScale(settings, head, motionTime);
        if (head.AbsorbedAt is { } absorptionTime)
        {
            var transfer = SmoothStep(absorptionTime,
                absorptionTime + TransitionBaseSeconds / settings.SpeedScale, epochTime);
            inputUv = Vector2.Lerp(inputUv, head.AbsorberInputUv, transfer);
            visibility *= 1f - transfer;
            radius *= Lerp(1f, 0.08f, transfer);
        }
        return new OutsideDropletMergeHeadState(
            head.Index,
            inputUv,
            head.SpawnInputUv,
            radius,
            vertical,
            Saturate(visibility),
            fallProgress);
    }

    private static float GetReformationStart(SimulationSettings settings, HeadSchedule[] heads)
    {
        // 全headの消失と初回出生を待ち、再形成した滴が同じ区間の落下滴と重ならないようにする。
        var start = settings.IsInitialRainEpoch ? settings.RainDurationSeconds : 0f;
        foreach (var head in heads)
        {
            if (!head.IsEligible) continue;
            var end = head.AbsorbedAt is { } time
                ? MathF.Min(head.FallEnd, time + TransitionBaseSeconds / settings.SpeedScale)
                : head.FallEnd;
            start = MathF.Max(start, end);
        }
        return start;
    }

    private static float GetReformationProgress(SimulationSettings settings,
        float time, float start, float end, float phase)
    {
        var available = MathF.Max(end - start, 0f);
        var duration = MathF.Min(available * 0.5f, 1f / settings.SpeedScale);
        var begin = start + (available - duration) * phase;
        return SmoothStep(begin, begin + duration, time);
    }

    private static float GetStaticVisibility(SimulationSettings settings, MergeContact contact, float epochTime,
        float reformationStart, float epochDuration, float phase)
    {
        var disappearance = 1f - SmoothStep(
            contact.Time,
            contact.Time + TransitionBaseSeconds / settings.SpeedScale,
            epochTime);
        if (epochTime >= reformationStart)
        {
            return GetReformationProgress(settings, epochTime, reformationStart, epochDuration, phase);
        }

        return Saturate(disappearance);
    }

    private static Vector2 GetSpawnRegionUv(SimulationSettings settings, uint cycleKey)
    {
        if (settings.IsEllipse)
        {
            var angle = OutsideDropletRandom.Sample(cycleKey, 1U) * MathF.Tau;
            var radius = MathF.Sqrt(OutsideDropletRandom.Sample(cycleKey, 2U)) * 0.78f;
            return new Vector2(0.5f) + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * 0.5f;
        }

        return new Vector2(
            Lerp(0.08f, 0.92f, OutsideDropletRandom.Sample(cycleKey, 1U)),
            Lerp(0.08f, 0.38f, OutsideDropletRandom.Sample(cycleKey, 2U)));
    }

    private static bool TryMapInputUvToRegionUv(SimulationSettings settings, Vector2 inputUv, out Vector2 regionUv)
    {
        if (settings.IsQuad)
        {
            return TryProject(settings.InverseRow0, settings.InverseRow1, settings.InverseRow2, inputUv, out regionUv);
        }

        var offset = inputUv * settings.InputSize - settings.RegionCenterPixels;
        var local = new Vector2(
            settings.RotationCos * offset.X + settings.RotationSin * offset.Y,
            -settings.RotationSin * offset.X + settings.RotationCos * offset.Y);
        regionUv = local / settings.RegionPixelSize + new Vector2(0.5f);
        return IsFinite(regionUv);
    }

    private static bool TryMapRegionUvToInputUv(SimulationSettings settings, Vector2 regionUv, out Vector2 inputUv)
    {
        if (settings.IsQuad)
        {
            return TryProject(settings.ForwardRow0, settings.ForwardRow1, settings.ForwardRow2, regionUv, out inputUv);
        }

        var local = (regionUv - new Vector2(0.5f)) * settings.RegionPixelSize;
        var rotated = new Vector2(
            settings.RotationCos * local.X - settings.RotationSin * local.Y,
            settings.RotationSin * local.X + settings.RotationCos * local.Y);
        inputUv = (settings.RegionCenterPixels + rotated) / settings.InputSize;
        return IsFinite(inputUv);
    }

    private static bool TryProject(Vector3 row0, Vector3 row1, Vector3 row2, Vector2 value, out Vector2 projected)
    {
        var point = new Vector3(value, 1f);
        var denominator = Vector3.Dot(row2, point);
        if (!float.IsFinite(denominator) || MathF.Abs(denominator) < 0.000001f)
        {
            projected = Vector2.Zero;
            return false;
        }

        projected = new Vector2(Vector3.Dot(row0, point), Vector3.Dot(row1, point)) / denominator;
        return IsFinite(projected);
    }

    private static bool IsRegionUvInside(SimulationSettings settings, Vector2 regionUv)
    {
        if (regionUv.X is < 0f or > 1f || regionUv.Y is < 0f or > 1f)
        {
            return false;
        }

        return !settings.IsEllipse || Vector2.DistanceSquared(regionUv, new Vector2(0.5f)) <= 0.25f;
    }

    private static bool TryCreateSettings(in GlassCompositeConstants constants, out SimulationSettings settings)
    {
        settings = default;
        if (!AreFinite(
            constants.InputWidth, constants.InputHeight, constants.RegionCenterX, constants.RegionCenterY,
            constants.RegionWidth, constants.RegionHeight, constants.RegionRotationCos, constants.RegionRotationSin,
            constants.QuadValid, constants.OutsideDropletAmount, constants.OutsideDropletSizeScale,
            constants.OutsideDropletSeed, constants.OutsideDropletLocalTimeSeconds,
            constants.OutsideDropletFallingRatio, constants.OutsideDropletFallSpeedScale,
            constants.OutsideDropletDeformWithSurface,
            constants.QuadInverseM11, constants.QuadInverseM12, constants.QuadInverseM13,
            constants.QuadInverseM21, constants.QuadInverseM22, constants.QuadInverseM23,
            constants.QuadInverseM31, constants.QuadInverseM32, constants.QuadInverseM33,
            constants.QuadForwardM11, constants.QuadForwardM12, constants.QuadForwardM13,
            constants.QuadForwardM21, constants.QuadForwardM22, constants.QuadForwardM23,
            constants.QuadForwardM31, constants.QuadForwardM32, constants.QuadForwardM33))
        {
            return false;
        }

        var inputSize = new Vector2(constants.InputWidth, constants.InputHeight);
        var regionPixelSize = Vector2.Max(
            new Vector2(constants.RegionWidth, constants.RegionHeight) * inputSize,
            new Vector2(0.000001f));
        if (inputSize.X <= 0f || inputSize.Y <= 0f || !IsFinite(regionPixelSize))
        {
            return false;
        }

        var isQuad = constants.RegionShape >= 2.5f;
        if (isQuad && constants.QuadValid < 0.5f)
        {
            return false;
        }

        var speed = Math.Clamp(constants.OutsideDropletFallSpeedScale, 0.25f, 4f);
        var sizeScale = MathF.Max(inputSize.Y / 1080f, 0.25f) *
            MathF.Max(constants.OutsideDropletSizeScale, 0.25f);
        var inverseRow0 = new Vector3(constants.QuadInverseM11, constants.QuadInverseM12, constants.QuadInverseM13);
        var inverseRow1 = new Vector3(constants.QuadInverseM21, constants.QuadInverseM22, constants.QuadInverseM23);
        var inverseRow2 = new Vector3(constants.QuadInverseM31, constants.QuadInverseM32, constants.QuadInverseM33);
        var forwardRow0 = new Vector3(constants.QuadForwardM11, constants.QuadForwardM12, constants.QuadForwardM13);
        var forwardRow1 = new Vector3(constants.QuadForwardM21, constants.QuadForwardM22, constants.QuadForwardM23);
        var forwardRow2 = new Vector3(constants.QuadForwardM31, constants.QuadForwardM32, constants.QuadForwardM33);
        var settingsSeed = (uint)Math.Clamp(constants.OutsideDropletSeed, 0f, uint.MaxValue);
        settings = new SimulationSettings(
            inputSize,
            regionPixelSize,
            new Vector2(constants.RegionCenterX, constants.RegionCenterY) * inputSize,
            constants.RegionRotationCos,
            constants.RegionRotationSin,
            isQuad,
            constants.RegionShape >= 1.5f && constants.RegionShape < 2.5f,
            !isQuad || constants.OutsideDropletDeformWithSurface >= 0.5f,
            inverseRow0,
            inverseRow1,
            inverseRow2,
            forwardRow0,
            forwardRow1,
            forwardRow2,
            Saturate(constants.OutsideDropletAmount),
            Saturate(constants.OutsideDropletFallingRatio),
            speed,
            sizeScale,
            settingsSeed,
            MathF.Max(constants.OutsideDropletLocalTimeSeconds, 0f),
            RainEnabled: constants.OutsideDropletRainEnabled >= 0.5f,
            RainStartSeconds: constants.OutsideDropletRainEnabled >= 0.5f
                ? Math.Clamp(constants.OutsideDropletRainStartSeconds, 0f, 36000f) : 0f,
            RainDurationSeconds: constants.OutsideDropletRainEnabled >= 0.5f
                ? Math.Clamp(constants.OutsideDropletRainDurationSeconds, 0f, 36000f) : 0f,
            FallFrequency: OutsideDropletFrequency.Normalize(constants.OutsideDropletFallFrequency));
        if (settings.RainEnabled && !AreFinite(settings.RainStartSeconds, settings.RainDurationSeconds))
            return false;
        return true;
    }

    private static float GetFallingWidthScale(float normalizedY, float fallProgress)
    {
        var morph = SmoothStep(0.05f, 0.35f, Saturate(fallProgress));
        var height = Saturate(normalizedY * 0.5f + 0.5f);
        var tipBlend = SmoothStep(0f, 0.20f, height);
        var taperedWidth = 0.065f + height * 1.44f + (1f - tipBlend) * 0.10f;
        return Lerp(1f, taperedWidth, morph);
    }

    private static float IntegrateSmoothStep(float value, float duration)
    {
        if (duration <= 0f || value <= 0f)
        {
            return 0f;
        }

        if (value >= duration)
        {
            return value - duration * 0.5f;
        }

        var progress = value / duration;
        return duration * (progress * progress * progress - 0.5f * progress * progress * progress * progress);
    }

    private static float SmoothStep(float minimum, float maximum, float value)
    {
        if (maximum <= minimum)
        {
            return value >= maximum ? 1f : 0f;
        }

        var amount = Saturate((value - minimum) / (maximum - minimum));
        return amount * amount * (3f - 2f * amount);
    }

    private static float Saturate(float value) => Math.Clamp(value, 0f, 1f);

    private static float Lerp(float from, float to, float amount) => from + (to - from) * amount;

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    private static bool AreFinite(params float[] values) => values.All(float.IsFinite);

    private sealed class ScheduleFallbackException : Exception { }

    private readonly record struct LayerDefinition(
        float CellSizePixels,
        float ActivationScale,
        uint Layer,
        float SelectionScale,
        float SizeSpeedScale);

    private readonly record struct StaticCellId(uint Layer, int CellX, int CellY) : IComparable<StaticCellId>
    {
        public int CompareTo(StaticCellId other)
        {
            var layer = Layer.CompareTo(other.Layer);
            if (layer != 0) return layer;
            var x = CellX.CompareTo(other.CellX);
            return x != 0 ? x : CellY.CompareTo(other.CellY);
        }
    }

    private readonly record struct StaticCandidate(StaticCell Cell, float MinimumInputY, float MaximumInputY);

    private readonly record struct StaticCell(
        StaticCellId Id,
        uint Key,
        bool IsActive,
        Vector2 CenterPatternPosition,
        float RadiusPixels,
        float VerticalScale);

    private readonly record struct MergeContact(
        float Time,
        int HeadIndex,
        StaticCell Cell,
        float RadiusIncrease = 0f,
        float SpeedIncrease = 0f)
    {

        public static int Compare(MergeContact left, MergeContact right)
        {
            var time = left.Time.CompareTo(right.Time);
            if (time != 0) return time;
            var emitter = left.HeadIndex.CompareTo(right.HeadIndex);
            if (emitter != 0) return emitter;
            var cellKey = left.Cell.Key.CompareTo(right.Cell.Key);
            return cellKey != 0 ? cellKey : left.Cell.Id.CompareTo(right.Cell.Id);
        }
    }

    private sealed class HeadSchedule
    {
        public HeadSchedule(int index, LayerDefinition layer, bool isEligible, uint cycleKey,
            Vector2 spawnRegionUv, Vector2 spawnInputUv, float birth, float fallStart, float fallDuration)
        {
            Index = index;
            Layer = layer;
            IsEligible = isEligible;
            CycleKey = cycleKey;
            SpawnRegionUv = spawnRegionUv;
            SpawnInputUv = spawnInputUv;
            Birth = birth;
            FallStart = fallStart;
            FallDuration = fallDuration;
        }

        public int Index { get; }
        public LayerDefinition Layer { get; }
        public bool IsEligible { get; }
        public uint CycleKey { get; }
        public Vector2 SpawnRegionUv { get; }
        public Vector2 SpawnInputUv { get; }
        public float Birth { get; }
        public float FallStart { get; }
        public float FallDuration { get; }
        public float FallEnd => FallStart + FallDuration;
        public float ContactStart => Birth + (FallStart - Birth) * 0.5f;
        public float SearchStart { get; set; }
        public float? AbsorbedAt { get; set; }
        public Vector2 AbsorberInputUv { get; set; }
        public float LastContactTime => MathF.Max(SearchStart,
            Contacts.Count == 0 ? ContactStart : Contacts[^1].Time);
        public List<MergeContact> Contacts { get; } = new(MaximumContactsPerHead);
        public List<MergeContact> GrowthContacts { get; } = new(9);
        public HashSet<StaticCellId>? CandidateIds { get; set; }
        public Dictionary<StaticCellId, StaticCell>? CandidateCells { get; set; }
        public StaticCandidate[]? OrderedCandidates { get; set; }
    }

    private sealed class EpochSchedule
    {
        private readonly SimulationSettings settings;
        private readonly float epochDuration;
        private readonly float reformationStart;
        private readonly HeadSchedule[] heads;
        private readonly MergeContact[] contacts;
        private readonly StaticGrowthLedger staticGrowth;

        public EpochSchedule(SimulationSettings settings, long epochIndex, float epochDuration,
            HeadSchedule[] heads, List<MergeContact> contacts, StaticGrowthLedger staticGrowth)
        {
            this.settings = settings with { EpochIndex = epochIndex };
            this.epochDuration = epochDuration;
            reformationStart = GetReformationStart(this.settings, heads);
            this.heads = heads;
            this.staticGrowth = staticGrowth;
            this.contacts = contacts.OrderBy(contact => contact, Comparer<MergeContact>.Create(MergeContact.Compare)).ToArray();
            staticGrowth.AssignReformationPhases(this.contacts.Select(contact => contact.Cell));
        }

        public OutsideDropletStaticLayout FinalStaticLayout => staticGrowth.FinalLayout;

        public OutsideDropletMergeFrame Evaluate(float epochTime)
        {
            var states = new OutsideDropletMergeHeadState[heads.Length];
            for (var index = 0; index < states.Length; index++)
            {
                states[index] = EvaluateHead(settings, heads[index], epochTime);
            }

            var changes = staticGrowth.Evaluate(epochTime, reformationStart, epochDuration);
            for (var index = 0; index < contacts.Length; index++)
            {
                var contact = contacts[index];
                changes[contact.Cell.Id] = new OutsideDropletStaticChange(
                    contact.Cell.Key,
                    contact.Cell.Id.Layer,
                    contact.Cell.Id.CellX,
                    contact.Cell.Id.CellY,
                    staticGrowth.IsAvailable(contact.Cell, epochTime)
                        ? GetStaticVisibility(settings, contact, epochTime, reformationStart, epochDuration,
                            staticGrowth.GetReformationPhase(contact.Cell.Id)) : 0f,
                    staticGrowth.GetScale(contact.Cell, epochTime, epochDuration, reformationStart));
            }

            return new OutsideDropletMergeFrame(true, states,
                changes.Values.OrderBy(change => change.Layer).ThenBy(change => change.CellY)
                    .ThenBy(change => change.CellX).ToArray(), staticGrowth.GetLayout(epochTime));
        }
    }

    private readonly record struct ScheduleKey(SimulationSettings Settings, long EpochIndex);

    private readonly record struct SimulationSettings(
        Vector2 InputSize,
        Vector2 RegionPixelSize,
        Vector2 RegionCenterPixels,
        float RotationCos,
        float RotationSin,
        bool IsQuad,
        bool IsEllipse,
        bool DeformWithSurface,
        Vector3 InverseRow0,
        Vector3 InverseRow1,
        Vector3 InverseRow2,
        Vector3 ForwardRow0,
        Vector3 ForwardRow1,
        Vector3 ForwardRow2,
        float Amount,
        float FallingRatio,
        float SpeedScale,
        float SizeScale,
        uint Seed,
        float LocalTimeSeconds,
        long EpochIndex = 0,
        bool RainEnabled = false,
        float RainStartSeconds = 0f,
        float RainDurationSeconds = 0f,
        float FallFrequency = 1f)
    {
        public bool IsInitialRainEpoch => RainEnabled && EpochIndex == 0;
    }
}

internal readonly record struct OutsideDropletMergeHeadState(
    int EmitterIndex,
    Vector2 InputUv,
    Vector2 SpawnInputUv,
    float Radius,
    float VerticalScale,
    float Visibility,
    float FallProgress);

internal readonly record struct OutsideDropletStaticChange(
    uint CellKey,
    uint Layer,
    int CellX,
    int CellY,
    float Visibility,
    float RadiusScale = 1f);

internal sealed class OutsideDropletMergeFrame
{
    public static OutsideDropletMergeFrame Invalid { get; } = new(false,
        Array.Empty<OutsideDropletMergeHeadState>(), Array.Empty<OutsideDropletStaticChange>());

    public OutsideDropletMergeFrame(bool isValid, IReadOnlyList<OutsideDropletMergeHeadState> heads,
        IReadOnlyList<OutsideDropletStaticChange> staticChanges,
        OutsideDropletStaticLayout? staticLayout = null)
    {
        IsValid = isValid;
        Heads = heads;
        StaticChanges = staticChanges;
        StaticLayout = staticLayout;
    }

    public bool IsValid { get; }
    public IReadOnlyList<OutsideDropletMergeHeadState> Heads { get; }
    public IReadOnlyList<OutsideDropletStaticChange> StaticChanges { get; }
    public OutsideDropletStaticLayout? StaticLayout { get; }
}
