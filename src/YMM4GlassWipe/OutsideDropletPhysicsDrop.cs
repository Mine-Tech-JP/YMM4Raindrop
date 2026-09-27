// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;

namespace YMM4GlassWipe;

[StructLayout(LayoutKind.Sequential)]
internal struct OutsideDropletPhysicsDrop
{
    public long Id;
    public double X, Y, PreviousX, PreviousY, Vx, Vy, Mass, Radius, TrailTop;
    public long BirthTick;
    public bool Moving;
    public static OutsideDropletPhysicsDrop Make(long id, double x, double y, double radius, long tick = 0) => new()
    {
        Id = id, X = x, Y = y, PreviousX = x, PreviousY = y, Radius = radius,
        Mass = radius * radius * radius, TrailTop = y, BirthTick = tick,
    };
}
