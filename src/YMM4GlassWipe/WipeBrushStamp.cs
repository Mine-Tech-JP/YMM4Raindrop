// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace YMM4GlassWipe;

internal readonly record struct WipeBrushStamp(
    Vector2 Center,
    float RadiusX,
    float RadiusY,
    Matrix3x2 Transform,
    float Alpha,
    float Softness,
    int AccumulationGroup,
    GlassWipeBrushShape Shape = GlassWipeBrushShape.Circle,
    bool Mirror = false,
    Guid UserBrushId = default,
    long UserBrushRevision = 0,
    int UserBrushPixelWidth = 0,
    int UserBrushPixelHeight = 0);
