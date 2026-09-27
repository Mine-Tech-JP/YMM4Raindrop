// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;

namespace YMM4GlassWipe;

internal sealed class OutsideDropletPhysicsReplay
{
    public const int Budget = 16 * 1024 * 1024;
    private readonly List<OutsideDropletPhysicsSnapshot> checkpoints = new();
    private readonly OutsideDropletPhysicsSnapshot initial;
    public OutsideDropletPhysicsSimulation Engine { get; }
    public int Bytes { get; private set; }
    public int PeakBytes { get; private set; }
    public int Evictions { get; private set; }
    public int Limit { get; }
    public OutsideDropletPhysicsReplay(OutsideDropletPhysicsSettings settings, int limit = Budget)
    {
        if (limit < 0 || limit > Budget) throw new ArgumentOutOfRangeException(nameof(limit));
        Engine = new(settings); initial = Engine.Save(); Limit = limit;
    }
    // 配列ヘッダーとリスト参照も含めた保守的な見積り。初期状態と作業配列は別枠。
    private static int SizeOf(OutsideDropletPhysicsSnapshot snapshot) => 320 + snapshot.Drops.Length * Marshal.SizeOf<OutsideDropletPhysicsDrop>() + snapshot.Transitions.Length * 320;
    public OutsideDropletPhysicsSnapshot At(long tick)
    {
        if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
        if (tick < Engine.Tick)
        {
            var nearest = initial;
            foreach (var checkpoint in checkpoints) if (checkpoint.Tick <= tick && checkpoint.Tick > nearest.Tick) nearest = checkpoint;
            Engine.Restore(nearest);
        }
        while (Engine.Tick < tick)
        {
            var distance = Math.Min(tick - Engine.Tick, 240 - Engine.Tick % 240);
            var next = Engine.IsRestingWithoutEvents ? tick : Engine.Tick + distance;
            Engine.AdvanceTo(next);
            if (next % 240 != 0 || checkpoints.Any(s => s.Tick == next)) continue;
            var saved = Engine.Save(); var bytes = SizeOf(saved);
            while (bytes > Limit - Bytes && checkpoints.Count > 0)
            {
                Bytes -= SizeOf(checkpoints[0]); checkpoints.RemoveAt(0); Evictions++;
            }
            if (bytes <= Limit) { checkpoints.Add(saved); Bytes += bytes; PeakBytes = Math.Max(PeakBytes, Bytes); }
        }
        return Engine.Save();
    }
}
