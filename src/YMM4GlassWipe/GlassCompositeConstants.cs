// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;

namespace YMM4GlassWipe;

[StructLayout(LayoutKind.Sequential)]
internal struct GlassCompositeConstants
{
    public float InputWidth;
    public float InputHeight;
    public float InputLeft;
    public float InputTop;

    public float RegionCenterX;
    public float RegionCenterY;
    public float RegionWidth;
    public float RegionHeight;

    public float FogAmount;
    public float TintMix;
    public float RegionFeather;
    public float DebugView;

    public float FogTintRed;
    public float FogTintGreen;
    public float FogTintBlue;
    public float Padding0;

    public float RegionShape;
    public float RegionRotationCos;
    public float RegionRotationSin;
    public float QuadValid;

    public float WipeResidue;
    public float WipeVariation;
    public float FogNoise;
    public float NoiseSeed;

    public float QuadInverseM11;
    public float QuadInverseM12;
    public float QuadInverseM13;
    public float OutsideDropletRainEnabled;

    public float QuadInverseM21;
    public float QuadInverseM22;
    public float QuadInverseM23;
    public float OutsideDropletRainStartSeconds;

    public float QuadInverseM31;
    public float QuadInverseM32;
    public float QuadInverseM33;
    public float OutsideDropletRainDurationSeconds;

    public float OutsideDropletAmount;
    public float OutsideDropletSizeScale;
    public float OutsideDropletStrength;
    public float OutsideDropletSeed;

    public float OutsideDropletLocalTimeSeconds;
    public float OutsideDropletFallEnabled;
    public float OutsideDropletFallingRatio;
    public float OutsideDropletFallSpeedScale;

    public float OutsideDropletTrailLength;
    public float OutsideDropletDeformWithSurface;
    public float OutsideDropletAppearance;
    public float OutsideDropletMergeEnabled;

    public float QuadForwardM11;
    public float QuadForwardM12;
    public float QuadForwardM13;
    public float OutsideDropletOutlineOpacity;

    public float QuadForwardM21;
    public float QuadForwardM22;
    public float QuadForwardM23;
    public float OutsideDropletRenderPass;

    public float QuadForwardM31;
    public float QuadForwardM32;
    public float QuadForwardM33;
    public float OutsideDropletFallFrequency;
}
