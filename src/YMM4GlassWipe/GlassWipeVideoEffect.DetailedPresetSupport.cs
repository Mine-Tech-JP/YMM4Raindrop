// SPDX-License-Identifier: MPL-2.0

using YukkuriMovieMaker.Commons;

namespace YMM4GlassWipe;

internal sealed partial class GlassWipeVideoEffect
{
    private DetailedWipePresetState CaptureDetailedPresetState()
    {
        return new DetailedWipePresetState
        {
            CustomPathData = CustomPathData,
            BrushShape = BrushShape,
            UserBrushId = UserBrushId,
            UserBrushRevision = UserBrushRevision,
            UserBrushPixelWidth = UserBrushPixelWidth,
            UserBrushPixelHeight = UserBrushPixelHeight,
            BrushMirror = BrushMirror,
            ContinuousWipe = ContinuousWipe,
            CircleDiameterPixels = CaptureAnimation(CircleDiameterPixels),
            EllipseWidthPixels = CaptureAnimation(EllipseWidthPixels),
            EllipseHeightPixels = CaptureAnimation(EllipseHeightPixels),
            RectangleWidthPixels = CaptureAnimation(RectangleWidthPixels),
            RectangleHeightPixels = CaptureAnimation(RectangleHeightPixels),
            PngBrushSizeScale = CaptureAnimation(PngBrushSizeScale),
            BrushRotation = CaptureAnimation(BrushRotation),
            WipeStrength = CaptureAnimation(WipeStrength),
            SimpleGeneratedBrushRotation =
                CaptureAnimation(SimpleGeneratedBrushRotation),
            SimpleGeneratedBrushRotationFollow =
                SimpleGeneratedBrushRotationFollow,
            PathSmoothing = PathSmoothing,
            PathJitter = PathJitter,
            JitterSeed = JitterSeed,
            BrushRotationFollow = BrushRotationFollow,
            BrushSoftness = BrushSoftness,
            WipeResidue = WipeResidue,
            WipeVariation = WipeVariation,
            OutsideDropletAmount = OutsideDropletAmount,
            OutsideDropletMotionMode = OutsideDropletMotionMode,
            OutsideDropletSlip = OutsideDropletSlip,
            OutsideDropletSupply = OutsideDropletSupply,
            OutsideDropletSize = OutsideDropletSize,
            OutsideDropletStrength = OutsideDropletStrength,
            OutsideDropletSeed = OutsideDropletSeed,
            OutsideDropletDeformWithSurface = OutsideDropletDeformWithSurface,
            OutsideDropletFallEnabled = OutsideDropletFallEnabled,
            OutsideDropletMergeEnabled = OutsideDropletMergeEnabled,
            OutsideDropletRainEnabled = OutsideDropletRainEnabled,
            OutsideDropletRainStartSeconds = OutsideDropletRainStartSeconds,
            OutsideDropletRainDurationSeconds = OutsideDropletRainDurationSeconds,
            OutsideDropletFallingRatio = OutsideDropletFallingRatio,
            OutsideDropletFallSpeed = OutsideDropletFallSpeed,
            OutsideDropletFallFrequency = OutsideDropletFallFrequency,
            OutsideDropletTrailLength = OutsideDropletTrailLength,
            OutsideDropletAppearance = OutsideDropletAppearance,
            OutsideDropletOutlineOpacity = OutsideDropletOutlineOpacity,
            Quality = Quality,
            SimpleGeneratedQuality = SimpleGeneratedQuality,
        };
    }

    private void ApplyDetailedPresetState(
        DetailedWipePresetState state,
        DetailedWipePresetScope scope)
    {
        if (!DetailedWipePresetStateMerger.TryMerge(
                CaptureDetailedPresetState(),
                state,
                scope,
                out var merged))
        {
            return;
        }

        if (DetailedWipePresetScopePolicy.IncludesPath(scope))
        {
            PathInputMode = WipePathInputMode.StrokeCollection;
            CustomPathData = merged.CustomPathData;
            PathSmoothing = merged.PathSmoothing;
            PathJitter = merged.PathJitter;
            JitterSeed = merged.JitterSeed;
        }

        if (DetailedWipePresetScopePolicy.IncludesBrush(scope))
        {
            BrushShape = merged.BrushShape;
            UserBrushId = merged.UserBrushId;
            UserBrushRevision = merged.UserBrushRevision;
            UserBrushPixelWidth = merged.UserBrushPixelWidth;
            UserBrushPixelHeight = merged.UserBrushPixelHeight;
            BrushMirror = merged.BrushMirror;
            ContinuousWipe = merged.ContinuousWipe;
            ApplyAnimation(
                CircleDiameterPixels,
                merged.CircleDiameterPixels);
            ApplyAnimation(
                EllipseWidthPixels,
                merged.EllipseWidthPixels);
            ApplyAnimation(
                EllipseHeightPixels,
                merged.EllipseHeightPixels);
            ApplyAnimation(
                RectangleWidthPixels,
                merged.RectangleWidthPixels);
            ApplyAnimation(
                RectangleHeightPixels,
                merged.RectangleHeightPixels);
            ApplyAnimation(PngBrushSizeScale, merged.PngBrushSizeScale);
            ApplyAnimation(BrushRotation, merged.BrushRotation);
            ApplyAnimation(WipeStrength, merged.WipeStrength);
            ApplyAnimation(
                SimpleGeneratedBrushRotation,
                merged.SimpleGeneratedBrushRotation);
            SimpleGeneratedBrushRotationFollow =
                merged.SimpleGeneratedBrushRotationFollow;
            BrushRotationFollow = merged.BrushRotationFollow;
            BrushSoftness = merged.BrushSoftness;
            WipeResidue = merged.WipeResidue;
            WipeVariation = merged.WipeVariation;
            Quality = merged.Quality ?? GlassWipeQuality.Standard;
            SimpleGeneratedQuality = merged.SimpleGeneratedQuality ??
                GlassWipeSimpleGeneratedQuality.Auto;
        }

        if (DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope))
        {
            OutsideDropletMotionMode = merged.OutsideDropletMotionMode ??
                OutsideDropletSettings.DefaultMotionMode;
            OutsideDropletSlip = merged.OutsideDropletSlip ?? OutsideDropletSettings.DefaultSlipPercent;
            OutsideDropletSupply = merged.OutsideDropletSupply ?? OutsideDropletSettings.DefaultSupplyPercent;
            OutsideDropletAmount = merged.OutsideDropletAmount ??
                OutsideDropletSettings.DefaultAmountPercent;
            OutsideDropletSize = merged.OutsideDropletSize ??
                OutsideDropletSettings.DefaultSizePercent;
            OutsideDropletStrength = merged.OutsideDropletStrength ??
                OutsideDropletSettings.DefaultStrengthPercent;
            OutsideDropletSeed = merged.OutsideDropletSeed ??
                OutsideDropletSettings.DefaultSeed;
            OutsideDropletDeformWithSurface =
                merged.OutsideDropletDeformWithSurface ?? true;
            OutsideDropletFallEnabled = merged.OutsideDropletFallEnabled ?? false;
            OutsideDropletMergeEnabled = merged.OutsideDropletMergeEnabled ??
                OutsideDropletSettings.DefaultMergeEnabled;
            OutsideDropletRainEnabled = merged.OutsideDropletRainEnabled ?? OutsideDropletSettings.DefaultRainEnabled;
            OutsideDropletRainStartSeconds = merged.OutsideDropletRainStartSeconds ?? OutsideDropletSettings.DefaultRainStartSeconds;
            OutsideDropletRainDurationSeconds = merged.OutsideDropletRainDurationSeconds ?? OutsideDropletSettings.DefaultRainDurationSeconds;
            OutsideDropletFallingRatio = merged.OutsideDropletFallingRatio ??
                OutsideDropletSettings.DefaultFallingRatioPercent;
            OutsideDropletFallSpeed = merged.OutsideDropletFallSpeed ??
                OutsideDropletSettings.DefaultFallSpeedPercent;
            OutsideDropletFallFrequency = merged.OutsideDropletFallFrequency ??
                OutsideDropletSettings.DefaultFallFrequencyPercent;
            OutsideDropletTrailLength = merged.OutsideDropletTrailLength ??
                OutsideDropletSettings.DefaultTrailLengthPercent;
            OutsideDropletAppearance = merged.OutsideDropletAppearance ??
                OutsideDropletAppearanceCompatibility.Default;
            OutsideDropletOutlineOpacity = merged.OutsideDropletOutlineOpacity ??
                OutsideDropletSettings.DefaultOutlineOpacityPercent;
        }

        OnPropertyChanged(nameof(DetailedPresetExchange));
        OnPropertyChanged(nameof(UserBrushExchange));
    }

    private static DetailedWipeAnimationState CaptureAnimation(Animation animation)
    {
        return new DetailedWipeAnimationState
        {
            From = animation.GetValue(0, 100, 60),
            To = animation.GetValue(100, 100, 60),
            AnimationType = (int)animation.AnimationType,
        };
    }

    private static void ApplyAnimation(Animation animation, DetailedWipeAnimationState state)
    {
#pragma warning disable CS0618
        animation.From = state.From;
        animation.To = state.To;
#pragma warning restore CS0618
        animation.AnimationType = Enum.IsDefined(typeof(AnimationType), state.AnimationType)
            ? (AnimationType)state.AnimationType
            : default;
    }
}
