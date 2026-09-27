// SPDX-License-Identifier: MPL-2.0

using System.IO;
using System.Numerics;
using System.Security.Cryptography;

namespace YMM4GlassWipe;

// 座標と力は1080基準の仮想単位。120Hzの演算順を全再生経路で共有する。
internal sealed partial class OutsideDropletPhysicsSimulation
{
    public const int Hz = 120;
    public const int Capacity = 256;
    public const int TransitionCapacity = 512;
    public const int TransitionTicks = 15;
    private readonly List<OutsideDropletPhysicsTransition> transitions = new(TransitionCapacity);
    public const double Height = 1080;
    public const double Gravity = 1800;
    public const double CriticalRadius = 12;
    public const double DragCoefficient = 40;
    private const int GridX = 32, GridY = 18;
    private readonly ulong[] grid = new ulong[GridX * GridY * 4];
    private readonly OutsideDropletPhysicsDrop[] initial;
    private readonly OutsideDropletPhysicsSettings settings;
    private readonly bool skipRest;
    private bool stable;
    public readonly OutsideDropletPhysicsDrop[] Bodies = new OutsideDropletPhysicsDrop[Capacity];
    public int Count { get; private set; }
    public long Tick { get; private set; }
    public long IntegratedTicks { get; private set; }
    public long PairTests { get; private set; }
    public long Merges { get; private set; }
    public long Skipped { get; private set; }
    public double Accepted { get; private set; }
    public double Rejected { get; private set; }
    public double Outflow { get; private set; }
    private long rainEvent, nextId = 1;
    private int initialCursor;

    public OutsideDropletPhysicsSimulation(OutsideDropletPhysicsSettings settings, OutsideDropletPhysicsDrop[]? fixture = null, bool skipRest = true)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        ValidateFixture(fixture);
        this.settings = settings;
        this.skipRest = skipRest;
        initial = fixture is not null ? fixture.ToArray() : Enumerable.Range(0, settings.InitialCount)
            .Select(i => OutsideDropletPhysicsDrop.Make(0, Random(settings.Seed, i, 0) * settings.Width,
                Random(settings.Seed, i, 1) * Height,
                (4 + 14 * Math.Pow(Random(settings.Seed, i, 2), 2)) * settings.Size,
                InitialBirthTick(settings, i)))
            .OrderBy(p => p.BirthTick).ToArray();
        if (initial.Length > Capacity) throw new ArgumentException("初期粒子が上限を超えています。");
        EmitDue();
        if (settings.Merge) ResolveContacts();
        Compact();
    }

    public static double Random(uint seed, long index, int channel)
    {
        unchecked
        {
            ulong value = (ulong)index * 0x9E3779B97F4A7C15UL + seed + (ulong)channel * 0xD1B54A32D192ED03UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return ((value ^ (value >> 31)) >> 11) * (1d / (1UL << 53));
        }
    }

    public static long TickForFrame(long frame, int fps)
    {
        if (frame < 0 || fps <= 0) throw new ArgumentOutOfRangeException(nameof(frame));
        return checked((frame / fps) * Hz + (frame % fps) * Hz / fps);
    }

    private void EmitDue()
    {
        while (initialCursor < initial.Length && initial[initialCursor].BirthTick <= Tick)
            Emit(initial[initialCursor++]);
        if (settings.Supply <= 0) return;
        while (NextRainTick() is long due && due <= Tick)
        {
            var i = rainEvent++;
            Emit(OutsideDropletPhysicsDrop.Make(0, Random(settings.Seed, i, 11) * settings.Width,
                Random(settings.Seed, i, 12) * Height,
                (3 + 8 * Random(settings.Seed, i, 13)) * settings.Size, Tick));
        }
    }

    private void Emit(OutsideDropletPhysicsDrop drop)
    {
        if (settings.Merge)
        {
            for (int i = 0; i < Count; i++)
            {
                ref var other = ref Bodies[i];
                if (other.Mass <= 0) continue;
                var dx = drop.X - other.X; var dy = drop.Y - other.Y;
                if (dx * dx + dy * dy <= Math.Pow(drop.Radius + other.Radius, 2))
                {
                    Accepted += drop.Mass;
                    MergeInto(ref other, in drop);
                    return;
                }
            }
        }
        if (Count >= Capacity) { Skipped++; Rejected += drop.Mass; return; }
        if (nextId == long.MaxValue) throw new InvalidOperationException("粒子IDの上限に達しました。");
        drop.Id = nextId++;
        Bodies[Count++] = drop;
        Accepted += drop.Mass;
    }

    public void AdvanceTo(long target)
    {
        if (target < Tick) throw new ArgumentOutOfRangeException(nameof(target), "逆向きにはsnapshotが必要です。");
        var dt = settings.Speed / Hz;
        while (Tick < target)
        {
            if (skipRest && stable)
            {
                long? nextInitial = initialCursor < initial.Length ? initial[initialCursor].BirthTick : null;
                var nextRain = NextRainTick();
                var skipUntil = target;
                if (nextInitial.HasValue) skipUntil = Math.Min(skipUntil, nextInitial.Value - 1);
                if (nextRain.HasValue) skipUntil = Math.Min(skipUntil, nextRain.Value - 1);
                if (skipUntil > Tick) Tick = skipUntil;
                ExpireTransitions();
                if (Tick == target) break;
            }
            Tick++; IntegratedTicks++;
            ExpireTransitions();
            EmitDue();
            for (int i = 0; i < Count; i++)
            {
                ref var p = ref Bodies[i];
                p.PreviousX = p.X; p.PreviousY = p.Y;
                if (!settings.Fall) { p.Vx = p.Vy = 0; p.Moving = false; continue; }
                var hold = Gravity * CriticalRadius * CriticalRadius / settings.Slip * p.Radius;
                if (!p.Moving && p.Mass * Gravity > hold) { p.Moving = true; p.TrailTop = p.Y; }
                if (!p.Moving) continue;
                var acceleration = Gravity - .6 * hold / p.Mass;
                var damping = DragCoefficient / p.Radius;
                // 抵抗だけを陰的に扱う。刻みと演算順は全要求経路で共通。
                p.Vy = Math.Max(0, (p.Vy + acceleration * dt) / (1 + damping * dt));
                p.Vx /= 1 + damping * dt;
                p.X += p.Vx * dt; p.Y += p.Vy * dt;
                if (p.Vy < .01 && p.Mass * Gravity <= hold) { p.Vx = p.Vy = 0; p.Moving = false; }
            }
            if (settings.Merge) ResolveContacts();
            for (int i = 0; i < Count; i++)
            {
                ref var p = ref Bodies[i];
                if (p.Mass > 0 && p.Y - p.Radius > Height) { Outflow += p.Mass; p.Mass = 0; }
            }
            Compact();
            stable = true;
            for (int i = 0; i < Count; i++)
            {
                var p = Bodies[i];
                var canStart = settings.Fall && p.Radius * p.Radius * settings.Slip > CriticalRadius * CriticalRadius;
                if (p.Moving || p.Vx != 0 || p.Vy != 0 || p.X != p.PreviousX || p.Y != p.PreviousY || canStart)
                { stable = false; break; }
            }
        }
    }

    private void MergeInto(ref OutsideDropletPhysicsDrop a, in OutsideDropletPhysicsDrop b)
    {
        var before = a;
        var sum = a.Mass + b.Mass;
        var part = b.Mass / sum;
        a.X += (b.X - a.X) * part; a.Y += (b.Y - a.Y) * part;
        a.Vx += (b.Vx - a.Vx) * part; a.Vy += (b.Vy - a.Vy) * part;
        a.Mass = sum; a.Radius = Math.Cbrt(sum);
        a.Moving |= b.Moving;
        Merges++;
        RecordTransition(before, b, a);
    }

    private int GX(double x) => (int)Math.Clamp(Math.Floor(x / settings.Width * GridX), 0, GridX - 1);
    private static int GY(double y) => (int)Math.Clamp(Math.Floor(y / Height * GridY), 0, GridY - 1);
    private (int X0, int Y0, int X1, int Y1) Bounds(in OutsideDropletPhysicsDrop p) => (
        GX(Math.Min(p.X, p.PreviousX) - p.Radius), GY(Math.Min(p.Y, p.PreviousY) - p.Radius),
        GX(Math.Max(p.X, p.PreviousX) + p.Radius), GY(Math.Max(p.Y, p.PreviousY) + p.Radius));

    private void BuildGrid()
    {
        Array.Clear(grid);
        for (int i = 0; i < Count; i++)
        {
            if (Bodies[i].Mass <= 0) continue;
            var b = Bounds(Bodies[i]);
            for (int y = b.Y0; y <= b.Y1; y++)
            for (int x = b.X0; x <= b.X1; x++) grid[(y * GridX + x) * 4 + i / 64] |= 1UL << (i % 64);
        }
    }

    private void ResolveContacts()
    {
        Span<ulong> candidates = stackalloc ulong[4];
        // 合体で範囲が広がる場合は索引を再構築する。各回で粒子が減るため有限。
        for (int pass = 0; pass < Capacity; pass++)
        {
            BuildGrid();
            var merged = false;
            for (int i = 0; i < Count; i++)
            {
                ref var a = ref Bodies[i];
                if (a.Mass <= 0) continue;
                candidates.Clear();
                var bounds = Bounds(a);
                for (int y = bounds.Y0; y <= bounds.Y1; y++)
                for (int x = bounds.X0; x <= bounds.X1; x++)
                for (int w = 0; w < 4; w++) candidates[w] |= grid[(y * GridX + x) * 4 + w];
                for (int w = i / 64; w < 4; w++)
                {
                    var mask = candidates[w];
                    while (mask != 0)
                    {
                        int j = w * 64 + BitOperations.TrailingZeroCount(mask);
                        mask &= mask - 1;
                        if (j <= i || Bodies[j].Mass <= 0) continue;
                        ref var b = ref Bodies[j];
                        PairTests++;
                        var dx = a.PreviousX - b.PreviousX; var dy = a.PreviousY - b.PreviousY;
                        var vx = a.X - a.PreviousX - b.X + b.PreviousX;
                        var vy = a.Y - a.PreviousY - b.Y + b.PreviousY;
                        var lengthSquared = vx * vx + vy * vy;
                        var t = lengthSquared > 0 ? Math.Clamp(-(dx * vx + dy * vy) / lengthSquared, 0, 1) : 0;
                        var nearX = dx + t * vx; var nearY = dy + t * vy;
                        var radius = a.Radius + b.Radius;
                        // 最短接近と合体後の終点を評価する。TOIに沿う厳密な多体解法ではない。
                        var endX = a.X - b.X; var endY = a.Y - b.Y;
                        if (nearX * nearX + nearY * nearY > radius * radius && endX * endX + endY * endY > radius * radius) continue;
                        MergeInto(ref a, in b); b.Mass = 0; merged = true;
                    }
                }
            }
            if (!merged) return;
        }
        throw new InvalidOperationException("合体走査が収束しません。");
    }

    private void Compact()
    {
        var write = 0;
        for (int i = 0; i < Count; i++) if (Bodies[i].Mass > 0) Bodies[write++] = Bodies[i];
        Array.Clear(Bodies, write, Count - write);
        Count = write;
    }

    public void CheckMass()
    {
        var mass = 0d;
        for (int i = 0; i < Count; i++)
        {
            var p = Bodies[i];
            if (!double.IsFinite(p.X + p.Y + p.Mass + p.Vy) || p.Mass <= 0 || p.Radius <= 0)
                throw new InvalidOperationException("粒子が非有限値です。");
            mass += p.Mass;
        }
        if (Math.Abs(Accepted - Outflow - mass) > Math.Max(1e-8, Accepted * 1e-10))
            throw new InvalidOperationException("水量収支が一致しません。");
    }

    public static string Digest(OutsideDropletPhysicsSnapshot state)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(state.Tick); writer.Write(state.RainEvent); writer.Write(state.InitialCursor); writer.Write(state.NextId);
        writer.Write(state.Accepted); writer.Write(state.Rejected); writer.Write(state.Outflow); writer.Write(state.Merges); writer.Write(state.Skipped);
        foreach (var p in state.Drops)
        {
            writer.Write(p.Id); writer.Write(p.X); writer.Write(p.Y); writer.Write(p.PreviousX); writer.Write(p.PreviousY);
            writer.Write(p.Vx); writer.Write(p.Vy); writer.Write(p.Mass); writer.Write(p.Radius); writer.Write(p.TrailTop);
            writer.Write(p.BirthTick); writer.Write(p.Moving);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
