// SPDX-License-Identifier: MPL-2.0

using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using YukkuriMovieMaker.Commons;

namespace YMM4GlassWipe;

internal sealed partial class GlassWipeVideoEffect : IJsonOnDeserializing
{
    public GlassWipeVideoEffect()
    {
        _pathTimingMode = GlassWipeDefaultSettings.PathTimingMode;
        _pathStartSeconds = GlassWipeDefaultSettings.PathStartSeconds;
        _pathCompletionSeconds = GlassWipeDefaultSettings.PathCompletionSeconds;
        _pathStart = GlassWipeDefaultSettings.PathStartPercent;
        _pathCompletion = GlassWipeDefaultSettings.PathCompletionPercent;
        _simplePattern = GlassWipeDefaultSettings.SimplePattern;
        _simpleStartX = GlassWipeDefaultSettings.SimpleStartX;
        _simpleStartY = GlassWipeDefaultSettings.SimpleStartY;
        _simpleControlY = GlassWipeDefaultSettings.SimpleControlY;
        _simpleEndX = GlassWipeDefaultSettings.SimpleEndX;
        _simpleEndY = GlassWipeDefaultSettings.SimpleEndY;
        _simpleRoundTrips = GlassWipeDefaultSettings.SimpleRoundTrips;
        _simpleWipeAmountPerPass = GlassWipeDefaultSettings.SimpleWipeAmountPerPassPercent;
        _simpleReturnOffset = GlassWipeDefaultSettings.SimpleReturnOffsetPercent;
        _simpleReturnOffsetX = GlassWipeDefaultSettings.SimpleReturnOffsetXPercent;
        _tintMix = GlassWipeDefaultSettings.TintMixPercent;
        _fogNoise = GlassWipeDefaultSettings.FogNoisePercent;
        _outsideDropletAppearance = GlassWipeDefaultSettings.OutsideDropletAppearance;
        _outsideDropletMotionMode = GlassWipeDefaultSettings.OutsideDropletMotionMode;
        _outsideDropletSupply = GlassWipeDefaultSettings.OutsideDropletSupplyPercent;
        _outsideDropletAmount = GlassWipeDefaultSettings.OutsideDropletAmountPercent;
        _outsideDropletSize = GlassWipeDefaultSettings.OutsideDropletSizePercent;
        _outsideDropletStrength = GlassWipeDefaultSettings.OutsideDropletStrengthPercent;
        _outsideDropletSeed = GlassWipeDefaultSettings.OutsideDropletSeed;
        _outsideDropletOutlineOpacity = GlassWipeDefaultSettings.OutsideDropletOutlineOpacityPercent;
        _outsideDropletFallEnabled = GlassWipeDefaultSettings.OutsideDropletFallEnabled;
        _outsideDropletMergeEnabled = GlassWipeDefaultSettings.OutsideDropletMergeEnabled;
        _outsideDropletRainEnabled = GlassWipeDefaultSettings.OutsideDropletRainEnabled;
        _outsideDropletRainStartSeconds = GlassWipeDefaultSettings.OutsideDropletRainStartSeconds;
        _outsideDropletRainDurationSeconds = GlassWipeDefaultSettings.OutsideDropletRainDurationSeconds;
        _outsideDropletFallingRatio = GlassWipeDefaultSettings.OutsideDropletFallingRatioPercent;
        _outsideDropletFallFrequency = GlassWipeDefaultSettings.OutsideDropletFallFrequencyPercent;
        _outsideDropletFallSpeed = GlassWipeDefaultSettings.OutsideDropletFallSpeedPercent;
        _outsideDropletTrailLength = GlassWipeDefaultSettings.OutsideDropletTrailLengthPercent;
        _regionShape = GlassWipeDefaultSettings.RegionShape;
        _regionFeather = GlassWipeDefaultSettings.RegionFeatherPixels;
        _brushShape = GlassWipeDefaultSettings.BrushShape;
        _brushMirror = GlassWipeDefaultSettings.BrushMirror;
        _continuousWipe = GlassWipeDefaultSettings.ContinuousWipe;
        _simpleGeneratedBrushRotationFollow = GlassWipeDefaultSettings.SimpleGeneratedBrushRotationFollowPercent;
        SubscribeToFogAmountChanges();
    }

    // System.Text.JsonとYMM4のJSON.NETの両方で、保存値を読む前に従来値へ戻す。
    // 明示保存された値は、この後のデシリアライズで上書きされる。
    void IJsonOnDeserializing.OnDeserializing() => RestoreLegacyDefaults();

    [OnDeserializing]
    private void OnJsonNetDeserializing(StreamingContext context) => RestoreLegacyDefaults();

    private void RestoreLegacyDefaults()
    {
        _pathTimingMode = WipePathTimingModeCompatibility.Default;
        _pathStartSeconds = WipePathTiming.DefaultStartSeconds;
        _pathCompletionSeconds = WipePathTiming.DefaultCompletionSeconds;
        _simpleReturnOffsetX = WipeSimplePathGenerator.DefaultReturnOffsetXPercent;
        _outsideDropletFallSpeed = OutsideDropletSettings.DefaultFallSpeedPercent;
        _brushMirror = false;
        SimpleGeneratedBrushRotation.CopyFrom(new Animation(0, -180, 180));
        _pathStart = WipePathTiming.DefaultStartPercent;
        _pathCompletion = WipePathTiming.DefaultCompletionPercent;
        _simplePattern = GlassWipeSimplePattern.GentleArcOnce;
        _simpleStartX = 15;
        _simpleStartY = 65;
        _simpleControlY = 45;
        _simpleEndX = 85;
        _simpleEndY = 65;
        _simpleRoundTrips = 2;
        _simpleWipeAmountPerPass = WipeSimplePathGenerator.DefaultWipeAmountPerPassPercent;
        _simpleReturnOffset = WipeSimplePathGenerator.DefaultReturnOffsetPercent;
        _tintMix = 25;
        _fogNoise = 0;
        _outsideDropletAppearance = OutsideDropletAppearanceCompatibility.Default;
        _outsideDropletMotionMode = OutsideDropletSettings.DefaultMotionMode;
        _outsideDropletSupply = OutsideDropletSettings.DefaultSupplyPercent;
        _outsideDropletAmount = OutsideDropletSettings.DefaultAmountPercent;
        _outsideDropletSize = OutsideDropletSettings.DefaultSizePercent;
        _outsideDropletStrength = OutsideDropletSettings.DefaultStrengthPercent;
        _outsideDropletSeed = OutsideDropletSettings.DefaultSeed;
        _outsideDropletOutlineOpacity = OutsideDropletSettings.DefaultOutlineOpacityPercent;
        _outsideDropletFallEnabled = false;
        _outsideDropletMergeEnabled = OutsideDropletSettings.DefaultMergeEnabled;
        _outsideDropletRainEnabled = OutsideDropletSettings.DefaultRainEnabled;
        _outsideDropletRainStartSeconds = OutsideDropletSettings.DefaultRainStartSeconds;
        _outsideDropletRainDurationSeconds = OutsideDropletSettings.DefaultRainDurationSeconds;
        _outsideDropletFallingRatio = OutsideDropletSettings.DefaultFallingRatioPercent;
        _outsideDropletFallFrequency = OutsideDropletSettings.DefaultFallFrequencyPercent;
        _outsideDropletTrailLength = OutsideDropletSettings.DefaultTrailLengthPercent;
        _regionShape = GlassWipeRegionShape.Rectangle;
        _regionFeather = 8;
        _brushShape = GlassWipeBrushShape.Circle;
        _continuousWipe = false;
        _simpleGeneratedBrushRotationFollow = 0;
        _pathCompletionWasSet = false;
        FogAmount.CopyFrom(new Animation(70, 0, 100));
        Blur.CopyFrom(new Animation(GlassWipeCompatibilityDefaults.BlurPixels, 0, 50));
        CircleDiameterPixels.CopyFrom(new Animation(
            GlassWipeCompatibilityDefaults.CircleDiameterPixels,
            0,
            8192));
        EllipseWidthPixels.CopyFrom(new Animation(
            GlassWipeCompatibilityDefaults.EllipseWidthPixels,
            0,
            8192));
        EllipseHeightPixels.CopyFrom(new Animation(
            GlassWipeCompatibilityDefaults.EllipseHeightPixels,
            0,
            8192));
        RectangleWidthPixels.CopyFrom(new Animation(
            GlassWipeCompatibilityDefaults.RectangleWidthPixels,
            0,
            8192));
        RectangleHeightPixels.CopyFrom(new Animation(
            GlassWipeCompatibilityDefaults.RectangleHeightPixels,
            0,
            8192));
        PngBrushSizeScale.CopyFrom(new Animation(
            GlassWipeCompatibilityDefaults.PngBrushSizeScalePercent,
            25,
            400));
    }
}
