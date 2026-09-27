// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

internal sealed partial class OutsideDropletPhysicsSimulation
{
    internal bool IsRestingWithoutEvents => stable && initialCursor == initial.Length && NextRainTick() is null;

    private static long InitialBirthTick(OutsideDropletPhysicsSettings settings, int index)
    {
        var delay = Math.Ceiling(Random(settings.Seed, index, 3) * settings.OnsetTicks);
        // doubleからlongへの変換前に上限を検査し、2^63への丸めを避ける。
        var ticks = delay >= settings.OnsetTicks ? settings.OnsetTicks : (long)delay;
        return checked(settings.StartTick + ticks);
    }

    private long? NextRainTick(long? eventIndex = null)
    {
        var index = eventIndex ?? rainEvent;
        if (settings.Supply <= 0 || index == long.MaxValue) return null;
        var eventNumber = index + 1;
        var numerator = eventNumber <= long.MaxValue / Hz ? eventNumber * Hz : (double)eventNumber * Hz;
        var delay = Math.Ceiling(numerator / (24 * settings.Supply));
        if (!double.IsFinite(delay) || delay >= 9223372036854775808d) return null;
        var ticks = (long)delay;
        return ticks > long.MaxValue - settings.StartTick ? null : settings.StartTick + ticks;
    }

    private static void ValidateFixture(OutsideDropletPhysicsDrop[]? fixture)
    {
        if (fixture is null) return;
        if (fixture.Length > Capacity) throw new ArgumentException("初期粒子が上限を超えています。", nameof(fixture));
        long previousBirth = 0;
        foreach (var drop in fixture)
        {
            ValidateDrop(drop);
            if (drop.BirthTick < previousBirth) throw new ArgumentException("初期粒子の出生時刻が昇順ではありません。", nameof(fixture));
            previousBirth = drop.BirthTick;
        }
    }

    private static void ValidateDrop(in OutsideDropletPhysicsDrop drop)
    {
        // 二乗距離と三乗水量の計算にも余裕を残す。通常の粒子・高速接触試験を含む。
        if (!IsBounded(drop.X) || !IsBounded(drop.Y) || !IsBounded(drop.PreviousX) || !IsBounded(drop.PreviousY) ||
            !IsBounded(drop.Vx) || !IsBounded(drop.Vy) || !IsBounded(drop.Radius) || !IsBounded(drop.TrailTop) ||
            !double.IsFinite(drop.Mass) || drop.Mass <= 0 || drop.Radius <= 0 || drop.BirthTick < 0 ||
            Math.Abs(drop.Radius * drop.Radius * drop.Radius - drop.Mass) > drop.Mass * 1e-10)
            throw new ArgumentException("粒子の座標、速度、水量または出生時刻が不正です。");
    }

    private static bool IsBounded(double value) => double.IsFinite(value) && Math.Abs(value) <= 1e50;

    private void RecordTransition(in OutsideDropletPhysicsDrop before, in OutsideDropletPhysicsDrop absorbed,
        in OutsideDropletPhysicsDrop after)
    {
        // 出生直後の未表示滴はID 0。既存滴同士の接触だけを表示履歴へ記録する。
        if (before.Id <= 0 || absorbed.Id <= 0) return;
        if (transitions.Count == TransitionCapacity)
            throw new InvalidOperationException("合体表示履歴が上限512件に達しました。");
        transitions.Add(new(Tick, before, absorbed, after));
    }

    private void ExpireTransitions()
    {
        var expired = 0;
        while (expired < transitions.Count && Tick - transitions[expired].Tick >= TransitionTicks) expired++;
        if (expired > 0) transitions.RemoveRange(0, expired);
    }

    public OutsideDropletPhysicsSnapshot Save() => new(settings, Tick, Bodies.AsSpan(0, Count).ToArray(),
        rainEvent, initialCursor, nextId, Accepted, Rejected, Outflow, Merges, Skipped)
    {
        Transitions = transitions.ToArray(),
    };

    public void Restore(OutsideDropletPhysicsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateSnapshot(snapshot);
        // 検証を完了してから書き換える。不正復元では現在の計算結果を保持する。
        Array.Clear(Bodies);
        snapshot.Drops.CopyTo(Bodies, 0);
        Count = snapshot.Drops.Length;
        Tick = snapshot.Tick;
        rainEvent = snapshot.RainEvent;
        initialCursor = snapshot.InitialCursor;
        nextId = snapshot.NextId;
        Accepted = snapshot.Accepted;
        Rejected = snapshot.Rejected;
        Outflow = snapshot.Outflow;
        Merges = snapshot.Merges;
        Skipped = snapshot.Skipped;
        transitions.Clear();
        transitions.AddRange(snapshot.Transitions);
        stable = false;
    }

    private void ValidateSnapshot(OutsideDropletPhysicsSnapshot snapshot)
    {
        if (snapshot.Settings != settings || snapshot.Drops is null || snapshot.Drops.Length > Capacity || snapshot.Tick < 0 ||
            snapshot.Transitions is null || snapshot.Transitions.Length > TransitionCapacity || snapshot.NextId < 1 ||
            snapshot.InitialCursor < 0 || snapshot.InitialCursor > initial.Length || snapshot.RainEvent < 0 ||
            snapshot.Merges < 0 || snapshot.Skipped < 0 || snapshot.Accepted < 0 || snapshot.Rejected < 0 || snapshot.Outflow < 0 ||
            !double.IsFinite(snapshot.Accepted + snapshot.Rejected + snapshot.Outflow))
            throw new ArgumentException("snapshotの設定または範囲が不正です。", nameof(snapshot));
        if ((snapshot.InitialCursor > 0 && initial[snapshot.InitialCursor - 1].BirthTick > snapshot.Tick) ||
            (snapshot.InitialCursor < initial.Length && initial[snapshot.InitialCursor].BirthTick <= snapshot.Tick))
            throw new ArgumentException("snapshotの初期出生カーソルが時刻と一致しません。", nameof(snapshot));
        ValidateRainCursor(snapshot);
        long previousId = 0;
        var sum = 0d;
        foreach (var drop in snapshot.Drops)
        {
            ValidateDrop(drop);
            if (drop.Id <= previousId || drop.Id >= snapshot.NextId || drop.BirthTick > snapshot.Tick)
                throw new ArgumentException("snapshotの粒子IDまたは時刻が不正です。", nameof(snapshot));
            previousId = drop.Id;
            sum += drop.Mass;
        }
        if (!double.IsFinite(sum) || Math.Abs(snapshot.Accepted - snapshot.Outflow - sum) > Math.Max(1e-8, snapshot.Accepted * 1e-10))
            throw new ArgumentException("snapshotの水量収支が不正です。", nameof(snapshot));
        ValidateTransitions(snapshot);
    }

    private void ValidateRainCursor(OutsideDropletPhysicsSnapshot snapshot)
    {
        if ((snapshot.RainEvent > 0 && (NextRainTick(snapshot.RainEvent - 1) is not long previous || previous > snapshot.Tick)) ||
            (NextRainTick(snapshot.RainEvent) is long next && next <= snapshot.Tick))
            throw new ArgumentException("snapshotの補給カーソルが時刻と一致しません。", nameof(snapshot));
        var births = (decimal)snapshot.InitialCursor + snapshot.RainEvent;
        if (snapshot.Merges > births || snapshot.Skipped > births || snapshot.NextId > births + 1)
            throw new ArgumentException("snapshotの粒子カウンターが出生数を超えています。", nameof(snapshot));
    }

    private static void ValidateTransitions(OutsideDropletPhysicsSnapshot snapshot)
    {
        long previousTick = 0;
        foreach (var transition in snapshot.Transitions)
        {
            ValidateDrop(transition.BeforeSurvivor);
            ValidateDrop(transition.BeforeAbsorbed);
            ValidateDrop(transition.After);
            if (transition.Tick < previousTick || transition.Tick > snapshot.Tick || snapshot.Tick - transition.Tick >= TransitionTicks ||
                transition.BeforeSurvivor.Id <= 0 || transition.BeforeAbsorbed.Id <= transition.BeforeSurvivor.Id ||
                transition.BeforeAbsorbed.Id >= snapshot.NextId || transition.After.Id != transition.BeforeSurvivor.Id ||
                transition.BeforeSurvivor.BirthTick > transition.Tick || transition.BeforeAbsorbed.BirthTick > transition.Tick ||
                transition.After.BirthTick != transition.BeforeSurvivor.BirthTick ||
                transition.After.Mass != transition.BeforeSurvivor.Mass + transition.BeforeAbsorbed.Mass)
                throw new ArgumentException("snapshotの合体表示履歴が不正です。", nameof(snapshot));
            previousTick = transition.Tick;
        }
    }
}
