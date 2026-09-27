// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

/// <summary>整数tickの状態から、再生順に依存しない表示用の滴を作ります。</summary>
internal static class OutsideDropletPhysicsFrame
{
    public const int Capacity = 512;

    public static OutsideDropletPhysicsDrop[] Create(
        OutsideDropletPhysicsSnapshot current,
        OutsideDropletPhysicsSnapshot next,
        double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        var following = next.Drops.ToDictionary(drop => drop.Id);
        var beforeContact = new Dictionary<long, OutsideDropletPhysicsDrop>();
        foreach (var transition in next.Transitions)
        {
            if (transition.Tick <= current.Tick) continue;
            beforeContact.TryAdd(transition.BeforeSurvivor.Id, transition.BeforeSurvivor);
            beforeContact.TryAdd(transition.BeforeAbsorbed.Id, transition.BeforeAbsorbed);
        }

        var visible = new Dictionary<long, OutsideDropletPhysicsDrop>(Capacity);
        foreach (var value in current.Drops)
        {
            var drop = value;
            if (beforeContact.TryGetValue(drop.Id, out var target) || following.TryGetValue(drop.Id, out target))
            {
                drop.X = Lerp(drop.X, target.X, fraction);
                drop.Y = Lerp(drop.Y, target.Y, fraction);
                drop.Vy = Lerp(drop.Vy, target.Vy, fraction);
                drop.TrailTop = Lerp(drop.TrailTop, target.TrailTop, fraction);
            }
            visible.Add(drop.Id, drop);
        }

        // 新しい合体から逆にほどく。物理水量は変更せず、表示用の水量だけを分配する。
        // 多段合体で一度吸収された滴も、その表示上の子として分配し二重計上を防ぐ。
        for (int i = current.Transitions.Length - 1; i >= 0; i--)
        {
            var transition = current.Transitions[i];
            if (!visible.TryGetValue(transition.After.Id, out var survivor)) continue;
            var progress = Math.Clamp((current.Tick + fraction - transition.Tick) / 15d, 0, 1);
            progress = progress * progress * (3 - 2 * progress);
            if (progress >= 1) continue;
            var absorbed = transition.BeforeAbsorbed;
            var scale = Math.Min(1, survivor.Mass / transition.After.Mass);
            absorbed.Mass *= scale * (1 - progress);
            if (absorbed.Mass <= 1e-12) continue;
            var remaining = survivor.Mass - absorbed.Mass;
            if (remaining <= 0 || visible.ContainsKey(absorbed.Id))
                throw new InvalidOperationException("水滴の吸収表示で水量またはIDが重複しています。");
            absorbed.X = survivor.X + (absorbed.X - transition.After.X) * (1 - progress);
            absorbed.Y = survivor.Y + (absorbed.Y - transition.After.Y) * (1 - progress);
            absorbed.Radius = Math.Cbrt(absorbed.Mass);
            survivor.X = (survivor.X * survivor.Mass - absorbed.X * absorbed.Mass) / remaining;
            survivor.Y = (survivor.Y * survivor.Mass - absorbed.Y * absorbed.Mass) / remaining;
            survivor.Mass = remaining;
            survivor.Radius = Math.Cbrt(remaining);
            visible[survivor.Id] = survivor;
            visible.Add(absorbed.Id, absorbed);
            if (visible.Count > Capacity)
                throw new InvalidOperationException("水滴の吸収表示が固定容量を超えています。");
        }

        return visible.Values.OrderBy(drop => drop.Id).ToArray();
    }

    private static double Lerp(double start, double end, double amount) => start + (end - start) * amount;
}
