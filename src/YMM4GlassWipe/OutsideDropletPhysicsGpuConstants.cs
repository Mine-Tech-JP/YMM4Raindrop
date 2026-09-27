// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace YMM4GlassWipe;

[InlineArray(512)]
internal struct OutsideDropletPhysicsVectors { private Vector4 _element; }

[StructLayout(LayoutKind.Sequential)]
internal struct OutsideDropletPhysicsUInt4 { public uint X, Y, Z, W; }

[InlineArray(1024)]
internal struct OutsideDropletPhysicsTileVectors { private OutsideDropletPhysicsUInt4 _element; }

/// <summary>512表示枠と16×16区画の全参照を保持する32,896 bytesの専用定数です。</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct OutsideDropletPhysicsGpuConstants
{
    public Vector4 Frame;
    public Vector4 Style;
    public Vector4 Region;
    public Vector4 Geometry;
    public Vector4 Inverse0;
    public Vector4 Inverse1;
    public Vector4 Inverse2;
    public Vector4 Diagnostic;
    public OutsideDropletPhysicsVectors Heads;
    public OutsideDropletPhysicsVectors Trails;
    public OutsideDropletPhysicsTileVectors Tiles;

    public static OutsideDropletPhysicsGpuConstants Create(
        ReadOnlySpan<OutsideDropletPhysicsDrop> drops,
        GlassWipeParameters parameters,
        float left, float top, float width, float height,
        bool fullScan = false)
    {
        if (drops.Length > OutsideDropletPhysicsFrame.Capacity ||
            !float.IsFinite(left) || !float.IsFinite(top) ||
            !float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0)
            throw new ArgumentException("水滴の描画範囲または表示数が不正です。");
        var mapping = parameters.QuadMapping.InputToLocal;
        var packet = new OutsideDropletPhysicsGpuConstants
        {
            Frame = new(width, height, parameters.OutsideDropletAppearance, fullScan ? 1 : 0),
            Style = new(left, top, parameters.OutsideDropletOutlineOpacity, parameters.OutsideDropletStrength),
            Region = new(parameters.RegionCenterX, parameters.RegionCenterY, parameters.RegionWidth, parameters.RegionHeight),
            Geometry = new((float)parameters.RegionShape, parameters.RegionRotationCos, parameters.RegionRotationSin, parameters.RegionFeather),
            Inverse0 = new(mapping.M11, mapping.M12, mapping.M13, parameters.QuadMapping.IsValid ? 1 : 0),
            Inverse1 = new(mapping.M21, mapping.M22, mapping.M23, 0),
            Inverse2 = new(mapping.M31, mapping.M32, mapping.M33, 0),
            Diagnostic = new((float)parameters.DebugView, drops.Length, 0, 0),
        };
        var scale = height / 1080f;
        var words = MemoryMarshal.Cast<OutsideDropletPhysicsUInt4, uint>(MemoryMarshal.CreateSpan(ref packet.Tiles[0], 1024));
        for (int i = 0; i < drops.Length; i++)
        {
            var drop = drops[i];
            var x = (float)drop.X * scale;
            var y = (float)drop.Y * scale;
            var radius = (float)drop.Radius * scale;
            var morph = drop.Moving ? (float)Math.Clamp(drop.Vy / 240d, 0, 1) : 0;
            var vertical = 1 + .8f * morph;
            var trailTop = Math.Min(y, Math.Max((float)drop.TrailTop * scale, y - 300 * scale));
            trailTop = y + (trailTop - y) * parameters.OutsideDropletTrailLength;
            if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(radius) ||
                !float.IsFinite(trailTop) || radius <= 0)
                throw new ArgumentException("水滴の描画座標が不正です。");
            packet.Heads[i] = new(x, y, radius, vertical);
            packet.Trails[i] = new(trailTop, drop.Moving && parameters.OutsideDropletTrailLength > 0 ? 1 : 0, morph, 1);
            var minX = Tile(x - radius * 1.6f - 2, width);
            var maxX = Tile(x + radius * 1.6f + 2, width);
            var minY = Tile(Math.Min(y - radius * vertical - 2, trailTop - radius * .2f - 2), height);
            var maxY = Tile(y + radius * vertical + 2, height);
            for (int tileY = minY; tileY <= maxY; tileY++)
            for (int tileX = minX; tileX <= maxX; tileX++)
                words[(tileY * 16 + tileX) * 16 + i / 32] |= 1u << (i % 32);
        }
        return packet;
    }

    private static int Tile(float value, float dimension) =>
        (int)Math.Clamp(Math.Floor((double)value / dimension * 16), 0, 15);
}
