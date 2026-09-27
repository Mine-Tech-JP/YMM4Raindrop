// SPDX-License-Identifier: MPL-2.0

using System.Text.Json.Serialization;

namespace YMM4GlassWipe;

internal enum DetailedWipePresetScope
{
    All = 0,
    Path = 1,
    Brush = 2,
}

internal static class DetailedWipePresetScopePolicy
{
    public static bool IsValid(DetailedWipePresetScope scope) =>
        scope is DetailedWipePresetScope.All or
            DetailedWipePresetScope.Path or
            DetailedWipePresetScope.Brush;

    public static bool IncludesPath(DetailedWipePresetScope scope) =>
        scope is DetailedWipePresetScope.All or DetailedWipePresetScope.Path;

    public static bool IncludesBrush(DetailedWipePresetScope scope) =>
        scope is DetailedWipePresetScope.All or DetailedWipePresetScope.Brush;

    public static bool IncludesOutsideDroplets(DetailedWipePresetScope scope) =>
        scope == DetailedWipePresetScope.All;

    public static string GetDisplayName(DetailedWipePresetScope scope) =>
        scope switch
        {
            DetailedWipePresetScope.Path => "軌跡",
            DetailedWipePresetScope.Brush => "ブラシ",
            DetailedWipePresetScope.All => "全設定",
            _ => "不明",
        };
}

internal sealed class DetailedWipeAnimationState
{
    public double From { get; set; }

    public double To { get; set; }

    public int AnimationType { get; set; }

    public DetailedWipeAnimationState Sanitize(double minimum, double maximum, double fallback)
    {
        return new DetailedWipeAnimationState
        {
            From = ClampFinite(From, minimum, maximum, fallback),
            To = ClampFinite(To, minimum, maximum, fallback),
            AnimationType = AnimationType,
        };
    }

    private static double ClampFinite(double value, double minimum, double maximum, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}

/// <summary>
/// 詳細モードへ展開できる軌跡とブラシ設定のスナップショットです。
/// 曇りと領域の設定は意図的に含めません。
/// </summary>
internal sealed class DetailedWipePresetState
{
    public const int OutsideDropletAppearanceIntroducedVersion = 9;
    public const int OutsideDropletMergeIntroducedVersion = 10;
    public const int OutsideDropletOutlineOpacityIntroducedVersion = 11;
    public const int OutsideDropletRainIntroducedVersion = 12;
    public const int ContinuousWipeIntroducedVersion = 13;
    public const int BrushPixelDimensionsIntroducedVersion = 14;
    public const int PngBrushSizeScaleIntroducedVersion = 15;
    public const int OutsideDropletPhysicsIntroducedVersion = 16;
    public const int CurrentVersion = 16;

    [JsonRequired]
    public int DataSchemaVersion { get; set; } = CurrentVersion;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OutsideDropletMotionMode? OutsideDropletMotionMode { get; set; } =
        OutsideDropletSettings.DefaultMotionMode;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? OutsideDropletSlip { get; set; } = OutsideDropletSettings.DefaultSlipPercent;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? OutsideDropletSupply { get; set; } = OutsideDropletSettings.DefaultSupplyPercent;

    [JsonRequired]
    public string CustomPathData { get; set; } =
        WipeStrokeDocumentCodec.Encode(WipeStrokeDocument.CreateStarter());

    public GlassWipeBrushShape BrushShape { get; set; } =
        GlassWipeBrushShape.Circle;

    public Guid UserBrushId { get; set; }

    public long UserBrushRevision { get; set; }

    public int UserBrushPixelWidth { get; set; }

    public int UserBrushPixelHeight { get; set; }

    public bool BrushMirror { get; set; }

    public bool ContinuousWipe { get; set; }

    public DetailedWipeAnimationState CircleDiameterPixels { get; set; } =
        DetailedWipePresetFactory.ConstantAnimation(
            GlassWipeCompatibilityDefaults.CircleDiameterPixels);

    public DetailedWipeAnimationState EllipseWidthPixels { get; set; } =
        DetailedWipePresetFactory.ConstantAnimation(
            GlassWipeCompatibilityDefaults.EllipseWidthPixels);

    public DetailedWipeAnimationState EllipseHeightPixels { get; set; } =
        DetailedWipePresetFactory.ConstantAnimation(
            GlassWipeCompatibilityDefaults.EllipseHeightPixels);

    public DetailedWipeAnimationState RectangleWidthPixels { get; set; } =
        DetailedWipePresetFactory.ConstantAnimation(
            GlassWipeCompatibilityDefaults.RectangleWidthPixels);

    public DetailedWipeAnimationState RectangleHeightPixels { get; set; } =
        DetailedWipePresetFactory.ConstantAnimation(
            GlassWipeCompatibilityDefaults.RectangleHeightPixels);

    public DetailedWipeAnimationState PngBrushSizeScale { get; set; } =
        DetailedWipePresetFactory.ConstantAnimation(
            GlassWipeCompatibilityDefaults.PngBrushSizeScalePercent);

    public DetailedWipeAnimationState BrushRotation { get; set; } =
        DetailedWipePresetFactory.ConstantAnimation(0);

    public DetailedWipeAnimationState WipeStrength { get; set; } =
        DetailedWipePresetFactory.ConstantAnimation(100);

    public DetailedWipeAnimationState SimpleGeneratedBrushRotation { get; set; } =
        DetailedWipePresetFactory.ConstantAnimation(0);

    public double SimpleGeneratedBrushRotationFollow { get; set; }

    public double PathSmoothing { get; set; }

    public double PathJitter { get; set; }

    public double JitterSeed { get; set; } = 1;

    public double BrushRotationFollow { get; set; }

    public double BrushSoftness { get; set; }

    public double WipeResidue { get; set; }

    public double WipeVariation { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? OutsideDropletAmount { get; set; } =
        OutsideDropletSettings.DefaultAmountPercent;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? OutsideDropletSize { get; set; } =
        OutsideDropletSettings.DefaultSizePercent;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? OutsideDropletStrength { get; set; } =
        OutsideDropletSettings.DefaultStrengthPercent;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? OutsideDropletSeed { get; set; } =
        OutsideDropletSettings.DefaultSeed;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? OutsideDropletDeformWithSurface { get; set; } = true;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? OutsideDropletFallEnabled { get; set; } = false;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? OutsideDropletMergeEnabled { get; set; } =
        OutsideDropletSettings.DefaultMergeEnabled;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? OutsideDropletRainEnabled { get; set; } = OutsideDropletSettings.DefaultRainEnabled;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? OutsideDropletRainStartSeconds { get; set; } = OutsideDropletSettings.DefaultRainStartSeconds;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? OutsideDropletRainDurationSeconds { get; set; } = OutsideDropletSettings.DefaultRainDurationSeconds;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? OutsideDropletFallingRatio { get; set; } =
        OutsideDropletSettings.DefaultFallingRatioPercent;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? OutsideDropletFallSpeed { get; set; } =
        OutsideDropletSettings.DefaultFallSpeedPercent;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? OutsideDropletFallFrequency { get; set; } =
        OutsideDropletSettings.DefaultFallFrequencyPercent;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? OutsideDropletTrailLength { get; set; } =
        OutsideDropletSettings.DefaultTrailLengthPercent;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OutsideDropletAppearance? OutsideDropletAppearance { get; set; } =
        OutsideDropletAppearanceCompatibility.Default;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? OutsideDropletOutlineOpacity { get; set; } =
        OutsideDropletSettings.DefaultOutlineOpacityPercent;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GlassWipeQuality? Quality { get; set; } = GlassWipeQuality.Standard;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GlassWipeSimpleGeneratedQuality? SimpleGeneratedQuality { get; set; } =
        GlassWipeSimpleGeneratedQuality.Auto;

    public bool TrySanitize(out DetailedWipePresetState sanitized) =>
        TrySanitize(DetailedWipePresetScope.All, out sanitized);

    public bool TrySanitize(
        DetailedWipePresetScope scope,
        out DetailedWipePresetState sanitized)
    {
        sanitized = new DetailedWipePresetState();
        if (DataSchemaVersion is < 1 or > CurrentVersion ||
            !DetailedWipePresetScopePolicy.IsValid(scope))
        {
            return false;
        }

        var hasValidPath = WipeStrokeDocumentCodec.TryDecode(CustomPathData, out var document);
        if (DetailedWipePresetScopePolicy.IncludesPath(scope) && !hasValidPath)
        {
            return false;
        }

        document ??= WipeStrokeDocument.CreateStarter();

        var sanitizedShape = GlassWipeParameterSanitizer.SanitizeBrushShape(
            BrushShape);
        sanitized = new DetailedWipePresetState
        {
            DataSchemaVersion = CurrentVersion,
            CustomPathData = WipeStrokeDocumentCodec.Encode(document),
            BrushShape = sanitizedShape,
            UserBrushId = sanitizedShape == GlassWipeBrushShape.UserImage
                ? UserBrushId
                : Guid.Empty,
            UserBrushRevision = sanitizedShape == GlassWipeBrushShape.UserImage
                ? Math.Max(0, UserBrushRevision)
                : 0,
            UserBrushPixelWidth = sanitizedShape == GlassWipeBrushShape.UserImage
                ? SanitizeUserBrushDimension(UserBrushPixelWidth)
                : 0,
            UserBrushPixelHeight = sanitizedShape == GlassWipeBrushShape.UserImage
                ? SanitizeUserBrushDimension(UserBrushPixelHeight)
                : 0,
            BrushMirror = BrushMirror,
            ContinuousWipe = DataSchemaVersion < ContinuousWipeIntroducedVersion
                ? false
                : ContinuousWipe && sanitizedShape is
                    GlassWipeBrushShape.Hand or
                    GlassWipeBrushShape.ShoePrint or
                    GlassWipeBrushShape.UserImage,
            CircleDiameterPixels = (CircleDiameterPixels ?? new()).Sanitize(
                0,
                WipeBrushPixelDimensions.Maximum,
                GlassWipeCompatibilityDefaults.CircleDiameterPixels),
            EllipseWidthPixels = (EllipseWidthPixels ?? new()).Sanitize(
                0,
                WipeBrushPixelDimensions.Maximum,
                GlassWipeCompatibilityDefaults.EllipseWidthPixels),
            EllipseHeightPixels = (EllipseHeightPixels ?? new()).Sanitize(
                0,
                WipeBrushPixelDimensions.Maximum,
                GlassWipeCompatibilityDefaults.EllipseHeightPixels),
            RectangleWidthPixels = (RectangleWidthPixels ?? new()).Sanitize(
                0,
                WipeBrushPixelDimensions.Maximum,
                GlassWipeCompatibilityDefaults.RectangleWidthPixels),
            RectangleHeightPixels = (RectangleHeightPixels ?? new()).Sanitize(
                0,
                WipeBrushPixelDimensions.Maximum,
                GlassWipeCompatibilityDefaults.RectangleHeightPixels),
            PngBrushSizeScale = DataSchemaVersion < PngBrushSizeScaleIntroducedVersion
                ? DetailedWipePresetFactory.ConstantAnimation(
                    GlassWipeCompatibilityDefaults.PngBrushSizeScalePercent)
                : (PngBrushSizeScale ?? new()).Sanitize(
                    25,
                    400,
                    GlassWipeCompatibilityDefaults.PngBrushSizeScalePercent),
            BrushRotation = (BrushRotation ?? new()).Sanitize(-180, 180, 0),
            WipeStrength = (WipeStrength ?? new()).Sanitize(0, 100, 100),
            SimpleGeneratedBrushRotation =
                (SimpleGeneratedBrushRotation ?? new()).Sanitize(-180, 180, 0),
            SimpleGeneratedBrushRotationFollow =
                ClampFinite(SimpleGeneratedBrushRotationFollow, 0, 100, 0),
            PathSmoothing = ClampFinite(PathSmoothing, 0, 100, 0),
            PathJitter = ClampFinite(PathJitter, 0, 5, 0),
            JitterSeed = ClampFinite(JitterSeed, 0, 9999, 1),
            BrushRotationFollow = ClampFinite(BrushRotationFollow, 0, 100, 0),
            BrushSoftness = ClampFinite(BrushSoftness, 0, 100, 0),
            WipeResidue = ClampFinite(WipeResidue, 0, 100, 0),
            WipeVariation = ClampFinite(WipeVariation, 0, 100, 0),
            OutsideDropletAmount = DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                ? OutsideDropletSettings.SanitizeAmountPercent(
                    OutsideDropletAmount ?? OutsideDropletSettings.DefaultAmountPercent)
                : null,
            OutsideDropletSize = DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                ? OutsideDropletSettings.SanitizeSizePercent(
                    OutsideDropletSize ?? OutsideDropletSettings.DefaultSizePercent)
                : null,
            OutsideDropletStrength = DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                ? OutsideDropletSettings.SanitizeStrengthPercent(
                    OutsideDropletStrength ?? OutsideDropletSettings.DefaultStrengthPercent)
                : null,
            OutsideDropletSeed = DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                ? OutsideDropletSettings.SanitizeSeedValue(
                    OutsideDropletSeed ?? OutsideDropletSettings.DefaultSeed)
                : null,
            OutsideDropletDeformWithSurface =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? OutsideDropletDeformWithSurface ?? true
                    : null,
            OutsideDropletFallEnabled =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? OutsideDropletFallEnabled ?? false
                    : null,
            OutsideDropletMergeEnabled =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? DataSchemaVersion < OutsideDropletMergeIntroducedVersion
                        ? OutsideDropletSettings.DefaultMergeEnabled
                        : OutsideDropletMergeEnabled ??
                            OutsideDropletSettings.DefaultMergeEnabled
                    : null,
            OutsideDropletRainEnabled =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? DataSchemaVersion < OutsideDropletRainIntroducedVersion
                        ? OutsideDropletSettings.DefaultRainEnabled
                        : OutsideDropletRainEnabled ?? OutsideDropletSettings.DefaultRainEnabled
                    : null,
            OutsideDropletRainStartSeconds =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? DataSchemaVersion < OutsideDropletRainIntroducedVersion
                        ? OutsideDropletSettings.DefaultRainStartSeconds
                        : OutsideDropletSettings.SanitizeRainStartSeconds(OutsideDropletRainStartSeconds ?? OutsideDropletSettings.DefaultRainStartSeconds)
                    : null,
            OutsideDropletRainDurationSeconds =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? DataSchemaVersion < OutsideDropletRainIntroducedVersion
                        ? OutsideDropletSettings.DefaultRainDurationSeconds
                        : OutsideDropletSettings.SanitizeRainDurationSeconds(OutsideDropletRainDurationSeconds ?? OutsideDropletSettings.DefaultRainDurationSeconds)
                    : null,
            OutsideDropletFallingRatio =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? OutsideDropletSettings.SanitizeFallingRatioPercent(
                        OutsideDropletFallingRatio ??
                        OutsideDropletSettings.DefaultFallingRatioPercent)
                    : null,
            OutsideDropletFallSpeed =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? OutsideDropletSettings.SanitizeFallSpeedPercent(
                        OutsideDropletFallSpeed ??
                        OutsideDropletSettings.DefaultFallSpeedPercent)
                    : null,
            OutsideDropletFallFrequency =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? OutsideDropletSettings.SanitizeFallFrequencyPercent(
                        OutsideDropletFallFrequency ??
                        OutsideDropletSettings.DefaultFallFrequencyPercent)
                    : null,
            OutsideDropletTrailLength =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? OutsideDropletSettings.SanitizeTrailLengthPercent(
                        OutsideDropletTrailLength ??
                        OutsideDropletSettings.DefaultTrailLengthPercent)
                    : null,
            OutsideDropletAppearance =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? DataSchemaVersion < OutsideDropletAppearanceIntroducedVersion
                        ? OutsideDropletAppearanceCompatibility.LegacyMigrationDefault
                        : OutsideDropletSettings.SanitizeAppearance(
                            OutsideDropletAppearance ??
                            OutsideDropletAppearanceCompatibility.Default)
                    : null,
            OutsideDropletOutlineOpacity =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? DataSchemaVersion < OutsideDropletOutlineOpacityIntroducedVersion
                        ? OutsideDropletSettings.DefaultOutlineOpacityPercent
                        : OutsideDropletSettings.SanitizeOutlineOpacityPercent(
                            OutsideDropletOutlineOpacity ?? OutsideDropletSettings.DefaultOutlineOpacityPercent)
                    : null,
            Quality = DetailedWipePresetScopePolicy.IncludesBrush(scope)
                ? GlassWipeParameterSanitizer.SanitizeQuality(
                    Quality ?? GlassWipeQuality.Standard)
                : null,
            SimpleGeneratedQuality = DetailedWipePresetScopePolicy.IncludesBrush(scope)
                ? GlassWipeSimpleGeneratedQualityCompatibility.Normalize(
                    SimpleGeneratedQuality ?? GlassWipeSimpleGeneratedQuality.Auto)
                : null,
        };
        if (DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope))
        {
            CopyOutsideDropletPhysicsTo(sanitized);
        }
        else
        {
            sanitized.OutsideDropletMotionMode = null;
            sanitized.OutsideDropletSlip = null;
            sanitized.OutsideDropletSupply = null;
        }
        return true;
    }

    // 部分適用と交換用snapshotでも同じ版別補完を使用する。
    internal void CopyOutsideDropletPhysicsTo(DetailedWipePresetState target)
    {
        var legacy = DataSchemaVersion < OutsideDropletPhysicsIntroducedVersion;
        target.OutsideDropletMotionMode = legacy
            ? OutsideDropletSettings.DefaultMotionMode
            : OutsideDropletSettings.SanitizeMotionMode(
                OutsideDropletMotionMode ?? OutsideDropletSettings.DefaultMotionMode);
        target.OutsideDropletSlip = legacy
            ? OutsideDropletSettings.DefaultSlipPercent
            : OutsideDropletSettings.SanitizeSlipPercent(
                OutsideDropletSlip ?? OutsideDropletSettings.DefaultSlipPercent);
        target.OutsideDropletSupply = legacy
            ? OutsideDropletSettings.DefaultSupplyPercent
            : OutsideDropletSettings.SanitizeSupplyPercent(
                OutsideDropletSupply ?? OutsideDropletSettings.DefaultSupplyPercent);
    }

    private static double ClampFinite(double value, double minimum, double maximum, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;

    private static int SanitizeUserBrushDimension(int value) =>
        Math.Clamp(value, 0, UserBrushLibrary.MaximumDimension);
}

internal static class DetailedWipePresetStateMerger
{
    public static bool TryMerge(
        DetailedWipePresetState current,
        DetailedWipePresetState preset,
        DetailedWipePresetScope scope,
        out DetailedWipePresetState merged)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(preset);
        merged = new DetailedWipePresetState();
        if (!current.TrySanitize(DetailedWipePresetScope.Brush, out var sanitizedCurrent) ||
            !preset.TrySanitize(scope, out var sanitizedPreset))
        {
            return false;
        }

        var pathSource = DetailedWipePresetScopePolicy.IncludesPath(scope)
            ? sanitizedPreset
            : sanitizedCurrent;
        var brushSource = DetailedWipePresetScopePolicy.IncludesBrush(scope)
            ? sanitizedPreset
            : sanitizedCurrent;
        merged = new DetailedWipePresetState
        {
            CustomPathData = pathSource.CustomPathData,
            PathSmoothing = pathSource.PathSmoothing,
            PathJitter = pathSource.PathJitter,
            JitterSeed = pathSource.JitterSeed,
            BrushShape = brushSource.BrushShape,
            UserBrushId = brushSource.UserBrushId,
            UserBrushRevision = brushSource.UserBrushRevision,
            UserBrushPixelWidth = brushSource.UserBrushPixelWidth,
            UserBrushPixelHeight = brushSource.UserBrushPixelHeight,
            BrushMirror = brushSource.BrushMirror,
            ContinuousWipe = brushSource.ContinuousWipe,
            CircleDiameterPixels = brushSource.CircleDiameterPixels,
            EllipseWidthPixels = brushSource.EllipseWidthPixels,
            EllipseHeightPixels = brushSource.EllipseHeightPixels,
            RectangleWidthPixels = brushSource.RectangleWidthPixels,
            RectangleHeightPixels = brushSource.RectangleHeightPixels,
            PngBrushSizeScale = brushSource.PngBrushSizeScale,
            BrushRotation = brushSource.BrushRotation,
            WipeStrength = brushSource.WipeStrength,
            SimpleGeneratedBrushRotation = brushSource.SimpleGeneratedBrushRotation,
            SimpleGeneratedBrushRotationFollow =
                brushSource.SimpleGeneratedBrushRotationFollow,
            BrushRotationFollow = brushSource.BrushRotationFollow,
            BrushSoftness = brushSource.BrushSoftness,
            WipeResidue = brushSource.WipeResidue,
            WipeVariation = brushSource.WipeVariation,
            OutsideDropletAmount = DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                ? sanitizedPreset.OutsideDropletAmount
                : OutsideDropletSettings.SanitizeAmountPercent(
                    current.OutsideDropletAmount ??
                    OutsideDropletSettings.DefaultAmountPercent),
            OutsideDropletSize = DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                ? sanitizedPreset.OutsideDropletSize
                : OutsideDropletSettings.SanitizeSizePercent(
                    current.OutsideDropletSize ??
                    OutsideDropletSettings.DefaultSizePercent),
            OutsideDropletStrength = DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                ? sanitizedPreset.OutsideDropletStrength
                : OutsideDropletSettings.SanitizeStrengthPercent(
                    current.OutsideDropletStrength ??
                    OutsideDropletSettings.DefaultStrengthPercent),
            OutsideDropletSeed = DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                ? sanitizedPreset.OutsideDropletSeed
                : OutsideDropletSettings.SanitizeSeedValue(
                    current.OutsideDropletSeed ??
                    OutsideDropletSettings.DefaultSeed),
            OutsideDropletDeformWithSurface =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? sanitizedPreset.OutsideDropletDeformWithSurface
                    : current.OutsideDropletDeformWithSurface ?? true,
            OutsideDropletFallEnabled =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? sanitizedPreset.OutsideDropletFallEnabled
                    : current.OutsideDropletFallEnabled ?? false,
            OutsideDropletMergeEnabled =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? sanitizedPreset.OutsideDropletMergeEnabled
                    : current.OutsideDropletMergeEnabled ??
                        OutsideDropletSettings.DefaultMergeEnabled,
            OutsideDropletRainEnabled =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? sanitizedPreset.OutsideDropletRainEnabled
                    : current.OutsideDropletRainEnabled ?? OutsideDropletSettings.DefaultRainEnabled,
            OutsideDropletRainStartSeconds =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? sanitizedPreset.OutsideDropletRainStartSeconds
                    : OutsideDropletSettings.SanitizeRainStartSeconds(current.OutsideDropletRainStartSeconds ?? OutsideDropletSettings.DefaultRainStartSeconds),
            OutsideDropletRainDurationSeconds =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? sanitizedPreset.OutsideDropletRainDurationSeconds
                    : OutsideDropletSettings.SanitizeRainDurationSeconds(current.OutsideDropletRainDurationSeconds ?? OutsideDropletSettings.DefaultRainDurationSeconds),
            OutsideDropletFallingRatio =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? sanitizedPreset.OutsideDropletFallingRatio
                    : OutsideDropletSettings.SanitizeFallingRatioPercent(
                        current.OutsideDropletFallingRatio ??
                        OutsideDropletSettings.DefaultFallingRatioPercent),
            OutsideDropletFallSpeed =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? sanitizedPreset.OutsideDropletFallSpeed
                    : OutsideDropletSettings.SanitizeFallSpeedPercent(
                        current.OutsideDropletFallSpeed ??
                        OutsideDropletSettings.DefaultFallSpeedPercent),
            OutsideDropletFallFrequency =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? sanitizedPreset.OutsideDropletFallFrequency
                    : OutsideDropletSettings.SanitizeFallFrequencyPercent(
                        current.OutsideDropletFallFrequency ??
                        OutsideDropletSettings.DefaultFallFrequencyPercent),
            OutsideDropletTrailLength =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? sanitizedPreset.OutsideDropletTrailLength
                    : OutsideDropletSettings.SanitizeTrailLengthPercent(
                        current.OutsideDropletTrailLength ??
                        OutsideDropletSettings.DefaultTrailLengthPercent),
            OutsideDropletAppearance =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? sanitizedPreset.OutsideDropletAppearance
                    : OutsideDropletSettings.SanitizeAppearance(
                        current.OutsideDropletAppearance ??
                        OutsideDropletAppearanceCompatibility.Default),
            OutsideDropletOutlineOpacity =
                DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
                    ? sanitizedPreset.OutsideDropletOutlineOpacity
                    : OutsideDropletSettings.SanitizeOutlineOpacityPercent(
                        current.OutsideDropletOutlineOpacity ?? OutsideDropletSettings.DefaultOutlineOpacityPercent),
            Quality = brushSource.Quality,
            SimpleGeneratedQuality = brushSource.SimpleGeneratedQuality,
        };
        (DetailedWipePresetScopePolicy.IncludesOutsideDroplets(scope)
            ? sanitizedPreset
            : current).CopyOutsideDropletPhysicsTo(merged);
        return true;
    }
}
