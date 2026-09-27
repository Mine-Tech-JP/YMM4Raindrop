// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

internal sealed record OutsideDropletPhysicsSnapshot(OutsideDropletPhysicsSettings Settings, long Tick,
    OutsideDropletPhysicsDrop[] Drops, long RainEvent, int InitialCursor, long NextId,
    double Accepted, double Rejected, double Outflow, long Merges, long Skipped)
{
    // 物理粒子とは別の表示用履歴。返却配列は計算本体から切り離されている。
    public OutsideDropletPhysicsTransition[] Transitions { get; init; } = [];
}

internal readonly record struct OutsideDropletPhysicsTransition(long Tick,
    OutsideDropletPhysicsDrop BeforeSurvivor, OutsideDropletPhysicsDrop BeforeAbsorbed,
    OutsideDropletPhysicsDrop After);
