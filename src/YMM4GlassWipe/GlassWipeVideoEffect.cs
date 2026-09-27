// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using OutsideDropletAppearanceValue = YMM4GlassWipe.OutsideDropletAppearance;

namespace YMM4GlassWipe;

[VideoEffect(DisplayName, ["描画"], ["雫と拭痕", "シズクトフキアト", "しずくとふきあと", "RainDrop & GlassWipe", "くもり", "ホコリ", "曇り", "埃", "拭き取り", "水滴", "雨滴", "雨", "ガラス"])]
internal sealed partial class GlassWipeVideoEffect : VideoEffectBase, IJsonOnDeserialized
{
    internal const string DisplayName = "雫と拭痕";
    private GlassWipeEditingMode _editingMode = GlassWipeEditingModeCompatibility.NewEffectDefault;
    private bool _editingModeWasSet;
    private WipePathTimingMode _pathTimingMode = WipePathTimingModeCompatibility.Default;
    private double _pathStart = WipePathTiming.DefaultStartPercent;
    private double _pathCompletion = WipePathTiming.DefaultCompletionPercent;
    private bool _pathCompletionWasSet;
    private double _pathStartFrame = WipePathTiming.DefaultStartFrame;
    private double _pathCompletionFrame = WipePathTiming.DefaultCompletionFrame;
    private double _pathStartSeconds = WipePathTiming.DefaultStartSeconds;
    private double _pathCompletionSeconds = WipePathTiming.DefaultCompletionSeconds;
    private GlassWipeSimplePattern _simplePattern = GlassWipeSimplePattern.GentleArcOnce;
    private GlassWipePathInterpolation _simpleInterpolation = GlassWipeDefaultSettings.SimpleInterpolation;
    private double _simpleStartX = 15;
    private double _simpleStartY = 65;
    private double _simpleControlX = 50;
    private double _simpleControlY = 45;
    private double _simpleEndX = 85;
    private double _simpleEndY = 65;
    private double _simpleRoundTrips = 2;
    private double _simpleWipeAmountPerPass = WipeSimplePathGenerator.DefaultWipeAmountPerPassPercent;
    private double _simpleReturnOffsetX = WipeSimplePathGenerator.DefaultReturnOffsetXPercent;
    private double _simpleReturnOffset = WipeSimplePathGenerator.DefaultReturnOffsetPercent;
    private Color _fogTint = Colors.White;
    private double _tintMix = 25;
    private GlassWipeRegionShape _regionShape = GlassWipeRegionShape.Rectangle;
    private GlassWipeQuadPreset _quadPreset = GlassWipeQuadPreset.Custom;
    private double _regionFeather = 8;
    private double _pathSmoothing;
    private double _pathJitter;
    private double _jitterSeed = 1;
    private GlassWipeBrushShape _brushShape = GlassWipeBrushShape.Circle;
    private Guid _userBrushId;
    private long _userBrushRevision;
    private int _userBrushPixelWidth;
    private int _userBrushPixelHeight;
    private bool _brushMirror;
    private bool _continuousWipe;
    private double _simpleGeneratedBrushRotationFollow;
    private double _brushRotationFollow;
    private double _brushSoftness;
    private double _wipeResidue;
    private double _wipeVariation;
    private double _fogNoise;
    private readonly List<AnimationValue> _observedFogAmountValues = [];
    private double _outsideDropletAmount = OutsideDropletSettings.DefaultAmountPercent;
    private double _outsideDropletSize = OutsideDropletSettings.DefaultSizePercent;
    private double _outsideDropletStrength = OutsideDropletSettings.DefaultStrengthPercent;
    private double _outsideDropletSeed = OutsideDropletSettings.DefaultSeed;
    private bool _outsideDropletDeformWithSurface = true;
    private bool _outsideDropletFallEnabled;
    private bool _outsideDropletMergeEnabled =
        OutsideDropletSettings.DefaultMergeEnabled;
    private bool _outsideDropletRainEnabled = OutsideDropletSettings.DefaultRainEnabled;
    private double _outsideDropletRainStartSeconds = OutsideDropletSettings.DefaultRainStartSeconds;
    private double _outsideDropletRainDurationSeconds = OutsideDropletSettings.DefaultRainDurationSeconds;
    private double _outsideDropletFallingRatio =
        OutsideDropletSettings.DefaultFallingRatioPercent;
    private double _outsideDropletFallSpeed =
        OutsideDropletSettings.DefaultFallSpeedPercent;
    private double _outsideDropletFallFrequency =
        OutsideDropletSettings.DefaultFallFrequencyPercent;
    private double _outsideDropletTrailLength =
        OutsideDropletSettings.DefaultTrailLengthPercent;
    private OutsideDropletAppearanceValue _outsideDropletAppearance =
        OutsideDropletAppearanceValue.Transparent;
    private bool _outsideDropletAppearanceWasSet;
    private double _outsideDropletOutlineOpacity =
        OutsideDropletSettings.DefaultOutlineOpacityPercent;
    private GlassWipeSimpleGeneratedQuality _simpleGeneratedQuality =
        GlassWipeSimpleGeneratedQuality.Auto;
    private GlassWipeQuality _quality = GlassWipeQuality.Standard;
    private GlassWipeDebugView _debugView = GlassWipeDebugView.Final;
    private WipePathInputMode _pathInputMode = WipePathInputModeCompatibility.NewEffectDefault;
    private bool _pathInputModeWasSet;
    private string _customPathData =
        WipeStrokeDocumentCodec.Encode(WipeStrokeDocument.CreateStarter());

    public override string Label => DisplayName;

    [Display(GroupName = "設定", Name = "設定方法", Description = "標準設定または詳細設定を選択")]
    [EnumComboBox]
    [DefaultValue(GlassWipeEditingMode.Simple)]
    public GlassWipeEditingMode EditingMode
    {
        get => _editingMode;
        set
        {
            _editingModeWasSet = true;
            if (Set(ref _editingMode, GlassWipeEditingModeCompatibility.Normalize(value)))
            {
                NotifySimpleVisibilityChanged();
            }
        }
    }

    [Display(GroupName = "設定", Name = "時間指定方式", Description = "パーセンテージ、固定フレーム、秒数から描画タイミングの指定方法を選択")]
    [EnumComboBox]
    [DefaultValue(GlassWipeDefaultSettings.PathTimingMode)]
    public WipePathTimingMode PathTimingMode
    {
        get => _pathTimingMode;
        set
        {
            if (Set(ref _pathTimingMode, WipePathTimingModeCompatibility.Normalize(value)))
            {
                NotifyTimingVisibilityChanged();
            }
        }
    }

    [JsonIgnore]
    public bool IsPercentTimingVisible =>
        WipePathTimingVisibility.IsPercentVisible(PathTimingMode);

    [JsonIgnore]
    public bool IsFrameTimingVisible =>
        WipePathTimingVisibility.IsFrameVisible(PathTimingMode);

    [JsonIgnore]
    public bool IsSecondsTimingVisible =>
        WipePathTimingVisibility.IsSecondsVisible(PathTimingMode);

    [Display(GroupName = "設定", Name = "描画開始位置", Description = "アイテム全体の何%から軌跡を描き始めるかを指定")]
    [TextBoxSlider("F1", "%", WipePathTiming.MinimumStartPercent, WipePathTiming.MaximumStartPercent)]
    [ShowPropertyEditorWhen(nameof(IsPercentTimingVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.PathStartPercent)]
    [Range(WipePathTiming.MinimumStartPercent, WipePathTiming.MaximumStartPercent)]
    public double PathStart
    {
        get => _pathStart;
        set => Set(ref _pathStart, value);
    }

    [Display(GroupName = "設定", Name = "描画完了位置", Description = "アイテム全体の何%で軌跡を描き終え、残りを完成状態で維持するかを指定")]
    [TextBoxSlider("F1", "%", WipePathTiming.MinimumCompletionPercent, WipePathTiming.MaximumCompletionPercent)]
    [ShowPropertyEditorWhen(nameof(IsPercentTimingVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.PathCompletionPercent)]
    [Range(WipePathTiming.MinimumCompletionPercent, WipePathTiming.MaximumCompletionPercent)]
    public double PathCompletion
    {
        get => _pathCompletion;
        set
        {
            _pathCompletionWasSet = true;
            Set(ref _pathCompletion, value);
        }
    }

    [Display(GroupName = "設定", Name = "描画開始フレーム", Description = "アイテム先頭を0として軌跡を描き始める固定フレームを指定")]
    [TextBoxSlider("F0", "f", WipePathTiming.MinimumFixedFrame, WipePathTiming.MaximumFixedFrame)]
    [ShowPropertyEditorWhen(nameof(IsFrameTimingVisible), true)]
    [DefaultValue(WipePathTiming.DefaultStartFrame)]
    [Range(WipePathTiming.MinimumFixedFrame, WipePathTiming.MaximumFixedFrame)]
    public double PathStartFrame
    {
        get => _pathStartFrame;
        set => Set(ref _pathStartFrame, value);
    }

    [Display(GroupName = "設定", Name = "描画完了フレーム", Description = "アイテム先頭を0として軌跡を描き終える固定フレームを指定")]
    [TextBoxSlider("F0", "f", WipePathTiming.MinimumFixedFrame, WipePathTiming.MaximumFixedFrame)]
    [ShowPropertyEditorWhen(nameof(IsFrameTimingVisible), true)]
    [DefaultValue(WipePathTiming.DefaultCompletionFrame)]
    [Range(WipePathTiming.MinimumFixedFrame, WipePathTiming.MaximumFixedFrame)]
    public double PathCompletionFrame
    {
        get => _pathCompletionFrame;
        set => Set(ref _pathCompletionFrame, value);
    }

    [Display(GroupName = "設定", Name = "描画開始秒", Description = "アイテム先頭から軌跡を描き始める秒数を指定")]
    [TextBoxSlider("F2", "秒", WipePathTiming.MinimumSeconds, WipePathTiming.MaximumSeconds)]
    [ShowPropertyEditorWhen(nameof(IsSecondsTimingVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.PathStartSeconds)]
    [Range(WipePathTiming.MinimumSeconds, WipePathTiming.MaximumSeconds)]
    public double PathStartSeconds
    {
        get => _pathStartSeconds;
        set => Set(ref _pathStartSeconds, value);
    }

    [Display(GroupName = "設定", Name = "描画完了秒", Description = "アイテム先頭から軌跡を描き終える秒数を指定")]
    [TextBoxSlider("F2", "秒", WipePathTiming.MinimumSeconds, WipePathTiming.MaximumSeconds)]
    [ShowPropertyEditorWhen(nameof(IsSecondsTimingVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.PathCompletionSeconds)]
    [Range(WipePathTiming.MinimumSeconds, WipePathTiming.MaximumSeconds)]
    public double PathCompletionSeconds
    {
        get => _pathCompletionSeconds;
        set => Set(ref _pathCompletionSeconds, value);
    }

    [Display(GroupName = "かんたん設定", Name = "拭き方", Description = "よく使う拭き取りパターンを選択")]
    [EnumComboBox]
    [ShowPropertyEditorWhen(nameof(AreSimpleGeneratedPathSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.SimplePattern)]
    public GlassWipeSimplePattern SimplePattern
    {
        get => _simplePattern;
        set
        {
            if (Set(ref _simplePattern, value))
            {
                NotifySimpleVisibilityChanged();
            }
        }
    }

    [Display(GroupName = "かんたん設定", Name = "点のつなぎ方", Description = "直線、求心性のCatmull-Romスプライン、3点を通る円弧から選択")]
    [EnumComboBox]
    [ShowPropertyEditorWhen(nameof(IsSimpleInterpolationVisible), true)]
    public GlassWipePathInterpolation SimpleInterpolation
    {
        get => _simpleInterpolation;
        set
        {
            if (Set(ref _simpleInterpolation, value))
            {
                OnPropertyChanged(nameof(IsSimpleControlPointVisible));
            }
        }
    }

    [Display(GroupName = "かんたん設定", Name = "進行度", Description = "0%から100%までの拭き取りの進み方をYMM4標準のアニメーションで設定")]
    [AnimationSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(AreSimpleGeneratedPathSettingsVisible), true)]
    public Animation SimpleProgress { get; } = CreateSimpleProgressAnimation();

    [JsonIgnore]
    public bool IsSimpleMode =>
        GlassWipeSimpleVisibility.IsSimpleMode(EditingMode);

    [JsonIgnore]
    public bool IsDetailedMode =>
        GlassWipeSimpleVisibility.IsDetailedMode(EditingMode);

    [JsonIgnore]
    public bool AreSimpleGeneratedPathSettingsVisible =>
        GlassWipeSimpleVisibility.AreSimpleGeneratedPathSettingsVisible(
            EditingMode,
            PathInputMode);

    [JsonIgnore]
    public bool AreFogSettingsVisible =>
        FogAmount.ActiveValues.Any(value =>
            GlassWipeParameterSanitizer.ToUnit(value.Value) > 0);

    [JsonIgnore]
    public bool AreFogNoiseSettingsVisible =>
        AreFogSettingsVisible;

    [JsonIgnore]
    public bool AreOutsideDropletSettingsVisible =>
        OutsideDropletSettings.SanitizeAmount(OutsideDropletAmount) > 0;

    [JsonIgnore]
    public bool AreOutsideDropletFallSettingsVisible =>
        AreOutsideDropletSettingsVisible && OutsideDropletFallEnabled;

    [JsonIgnore]
    public bool AreOutsideDropletRainTimingSettingsVisible =>
        AreOutsideDropletSettingsVisible && OutsideDropletRainEnabled;

    [JsonIgnore]
    public bool IsOutsideDropletOutlineOpacityVisible =>
        AreOutsideDropletSettingsVisible &&
        OutsideDropletSettings.SanitizeAppearance(OutsideDropletAppearance) is
            OutsideDropletAppearanceValue.Transparent or OutsideDropletAppearanceValue.Realistic;

    [JsonIgnore]
    public bool IsOutsideDropletDeformWithSurfaceVisible =>
        AreOutsideDropletSettingsVisible &&
        OutsideDropletMotionMode == OutsideDropletSettings.DefaultMotionMode &&
        OutsideDropletSettings.SanitizeAppearance(OutsideDropletAppearance) !=
            OutsideDropletAppearanceValue.Realistic;

    [JsonIgnore]
    public bool IsSimpleInterpolationVisible =>
        GlassWipeSimpleVisibility.IsInterpolationVisible(
            EditingMode,
            PathInputMode,
            SimplePattern);

    [JsonIgnore]
    public bool IsSimpleControlPointVisible =>
        GlassWipeSimpleVisibility.IsControlPointVisible(
            EditingMode,
            PathInputMode,
            SimplePattern,
            SimpleInterpolation);

    [JsonIgnore]
    public bool AreSimpleRoundTripSettingsVisible =>
        GlassWipeSimpleVisibility.AreRoundTripSettingsVisible(
            EditingMode,
            PathInputMode,
            SimplePattern);

    [JsonIgnore]
    public bool IsSimpleReturnOffsetVisible =>
        GlassWipeSimpleVisibility.IsReturnOffsetVisible(
            EditingMode,
            PathInputMode,
            SimplePattern);

    [JsonIgnore]
    public bool IsRegionTransformVisible =>
        GlassWipeSimpleVisibility.IsRegionTransformVisible(
            EditingMode,
            RegionShape);

    [JsonIgnore]
    public bool IsQuadConfigurationVisible =>
        GlassWipeSimpleVisibility.IsQuadConfigurationVisible(
            EditingMode,
            RegionShape);

    [JsonIgnore]
    public bool AreQuadPointsVisible =>
        GlassWipeSimpleVisibility.AreQuadPointsVisible(
            EditingMode,
            RegionShape,
            QuadPreset);

    [JsonIgnore]
    public bool AreDetailedPathSettingsVisible =>
        EditingMode == GlassWipeEditingMode.Detailed &&
        PathInputMode != WipePathInputMode.SimpleGenerated;

    [JsonIgnore]
    public bool AreSimpleGeneratedBrushSettingsVisible =>
        GlassWipeSimpleVisibility.AreSimpleGeneratedBrushSettingsVisible(
            EditingMode,
            PathInputMode);

    [JsonIgnore]
    public bool IsBrushMirrorVisible =>
        GlassWipeSimpleVisibility.IsBrushMirrorVisible(BrushShape);

    [JsonIgnore]
    public bool IsContinuousWipeVisible =>
        GlassWipeSimpleVisibility.IsContinuousWipeVisible(BrushShape);

    [JsonIgnore]
    public bool IsUserBrushVisible =>
        GlassWipeSimpleVisibility.IsUserBrushVisible(BrushShape);

    [JsonIgnore]
    public bool IsPngBrushSizeVisible =>
        GlassWipeSimpleVisibility.IsPngBrushSizeVisible(BrushShape);

    [JsonIgnore]
    public bool IsCircleBrushSizeVisible =>
        GlassWipeSimpleVisibility.IsCircleBrushSizeVisible(BrushShape);

    [JsonIgnore]
    public bool IsEllipseBrushSizeVisible =>
        GlassWipeSimpleVisibility.IsEllipseBrushSizeVisible(BrushShape);

    [JsonIgnore]
    public bool IsRectangleBrushSizeVisible =>
        GlassWipeSimpleVisibility.IsRectangleBrushSizeVisible(BrushShape);

    [JsonIgnore]
    public bool IsDetailedCustomPathVisible =>
        EditingMode == GlassWipeEditingMode.Detailed &&
        PathInputMode == WipePathInputMode.StrokeCollection;

    [JsonIgnore]
    public bool IsDetailedLegacyPathVisible =>
        EditingMode == GlassWipeEditingMode.Detailed &&
        PathInputMode == WipePathInputMode.LegacyAnimation;

    [Display(GroupName = "かんたん設定", Name = "始点X", Description = "曇りを適用する領域内における拭き始めの横位置")]
    [TextBoxSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(AreSimpleGeneratedPathSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.SimpleStartX)]
    [Range(0, 100)]
    public double SimpleStartX
    {
        get => _simpleStartX;
        set => Set(ref _simpleStartX, value);
    }

    [Display(GroupName = "かんたん設定", Name = "始点Y", Description = "曇りを適用する領域内における拭き始めの縦位置")]
    [TextBoxSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(AreSimpleGeneratedPathSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.SimpleStartY)]
    [Range(0, 100)]
    public double SimpleStartY
    {
        get => _simpleStartY;
        set => Set(ref _simpleStartY, value);
    }

    [Display(GroupName = "かんたん設定", Name = "経由点X", Description = "曇りを適用する領域内における曲線の経由位置")]
    [TextBoxSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(IsSimpleControlPointVisible), true)]
    [DefaultValue(50d)]
    [Range(0, 100)]
    public double SimpleControlX
    {
        get => _simpleControlX;
        set => Set(ref _simpleControlX, value);
    }

    [Display(GroupName = "かんたん設定", Name = "経由点Y", Description = "曇りを適用する領域内における曲線の経由位置")]
    [TextBoxSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(IsSimpleControlPointVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.SimpleControlY)]
    [Range(0, 100)]
    public double SimpleControlY
    {
        get => _simpleControlY;
        set => Set(ref _simpleControlY, value);
    }

    [Display(GroupName = "かんたん設定", Name = "終点X", Description = "曇りを適用する領域内における拭き終わりの横位置")]
    [TextBoxSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(AreSimpleGeneratedPathSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.SimpleEndX)]
    [Range(0, 100)]
    public double SimpleEndX
    {
        get => _simpleEndX;
        set => Set(ref _simpleEndX, value);
    }

    [Display(GroupName = "かんたん設定", Name = "終点Y", Description = "曇りを適用する領域内における拭き終わりの縦位置")]
    [TextBoxSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(AreSimpleGeneratedPathSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.SimpleEndY)]
    [Range(0, 100)]
    public double SimpleEndY
    {
        get => _simpleEndY;
        set => Set(ref _simpleEndY, value);
    }

    [Display(GroupName = "かんたん設定", Name = "往復回数", Description = "往復パターンの繰り返し回数。0.5回単位で指定し、1.5回では終点側で拭き終わります。")]
    [RoundTripCountEditor]
    [ShowPropertyEditorWhen(nameof(AreSimpleRoundTripSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.SimpleRoundTrips)]
    [Range(WipeSimplePathGenerator.MinimumRoundTrips, WipeSimplePathGenerator.MaximumRoundTrips)]
    public double SimpleRoundTrips
    {
        get => _simpleRoundTrips;
        set => Set(
            ref _simpleRoundTrips,
            WipeSimplePathGenerator.SanitizeRoundTrips(value));
    }

    [Display(GroupName = "かんたん設定", Name = GlassWipeSimpleVisibility.WipeAmountDisplayName, Description = "往復パターンで、片道の通過ごとに残っている曇りから取り除く割合")]
    [TextBoxSlider("F1", "%", WipeSimplePathGenerator.MinimumWipeAmountPerPassPercent, WipeSimplePathGenerator.MaximumWipeAmountPerPassPercent)]
    [ShowPropertyEditorWhen(nameof(AreSimpleRoundTripSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.SimpleWipeAmountPerPassPercent)]
    [Range(WipeSimplePathGenerator.MinimumWipeAmountPerPassPercent, WipeSimplePathGenerator.MaximumWipeAmountPerPassPercent)]
    public double SimpleWipeAmountPerPass
    {
        get => _simpleWipeAmountPerPass;
        set => Set(ref _simpleWipeAmountPerPass, value);
    }

    [Display(GroupName = "かんたん設定", Name = GlassWipeSimpleVisibility.ReturnOffsetXDisplayName, Description = "往復ごとの横方向のずれ。マイナスで左、プラスで右")]
    [TextBoxSlider("F1", "%", WipeSimplePathGenerator.MinimumReturnOffsetPercent, WipeSimplePathGenerator.MaximumReturnOffsetPercent)]
    [ShowPropertyEditorWhen(nameof(IsSimpleReturnOffsetVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.SimpleReturnOffsetXPercent)]
    [Range(WipeSimplePathGenerator.MinimumReturnOffsetPercent, WipeSimplePathGenerator.MaximumReturnOffsetPercent)]
    public double SimpleReturnOffsetX
    {
        get => _simpleReturnOffsetX;
        set => Set(ref _simpleReturnOffsetX, value);
    }

    [Display(GroupName = "かんたん設定", Name = GlassWipeSimpleVisibility.ReturnOffsetYDisplayName, Description = "往復ごとの縦方向のずれ。マイナスで上、プラスで下")]
    [TextBoxSlider("F1", "%", WipeSimplePathGenerator.MinimumReturnOffsetPercent, WipeSimplePathGenerator.MaximumReturnOffsetPercent)]
    [ShowPropertyEditorWhen(nameof(IsSimpleReturnOffsetVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.SimpleReturnOffsetPercent)]
    [Range(WipeSimplePathGenerator.MinimumReturnOffsetPercent, WipeSimplePathGenerator.MaximumReturnOffsetPercent)]
    public double SimpleReturnOffset
    {
        get => _simpleReturnOffset;
        set => Set(ref _simpleReturnOffset, value);
    }

    [Display(GroupName = "曇り", Name = "曇り量", Description = "領域へ適用する曇りの強さ")]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation FogAmount { get; } = new(GlassWipeDefaultSettings.FogAmountPercent, 0, 100);

    [Display(GroupName = "曇り", Name = "ぼかし量", Description = "曇り部分へ適用するぼかしの強さ")]
    [AnimationSlider("F1", "px", 0, 50)]
    [ShowPropertyEditorWhen(nameof(AreFogSettingsVisible), true)]
    public Animation Blur { get; } = new(GlassWipeDefaultSettings.BlurPixels, 0, 50);

    [Display(GroupName = "曇り", Name = "曇り色", Description = "ぼかし映像へ混ぜる色")]
    [ColorPicker]
    [ShowPropertyEditorWhen(nameof(AreFogSettingsVisible), true)]
    public Color FogTint
    {
        get => _fogTint;
        set => Set(ref _fogTint, value);
    }

    [Display(GroupName = "曇り", Name = "色の混合量", Description = "曇り色を混ぜる割合")]
    [TextBoxSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(AreFogSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.TintMixPercent)]
    [Range(0, 100)]
    public double TintMix
    {
        get => _tintMix;
        set => Set(ref _tintMix, value);
    }

    [Display(GroupName = "水滴", Name = "外側の水滴量", Description = "窓の外側にある拭けない水滴の個数を調整します。簡易物理では初期候補が100%で256滴です。拭き取りでは水滴が残り、くもりだけが取れます。")]
    [TextBoxSlider("F1", "%", OutsideDropletSettings.MinimumAmountPercent, OutsideDropletSettings.MaximumAmountPercent)]
    [DefaultValue(GlassWipeDefaultSettings.OutsideDropletAmountPercent)]
    [Range(OutsideDropletSettings.MinimumAmountPercent, OutsideDropletSettings.MaximumAmountPercent)]
    public double OutsideDropletAmount
    {
        get => _outsideDropletAmount;
        set
        {
            if (Set(ref _outsideDropletAmount, value))
            {
                OnPropertyChanged(nameof(AreOutsideDropletSettingsVisible));
                OnPropertyChanged(nameof(IsOutsideDropletDeformWithSurfaceVisible));
                OnPropertyChanged(nameof(IsOutsideDropletOutlineOpacityVisible));
                OnPropertyChanged(nameof(AreOutsideDropletFallSettingsVisible));
                OnPropertyChanged(nameof(AreOutsideDropletRainTimingSettingsVisible));
                NotifyOutsideDropletPhysicsVisibilityChanged();
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [Display(GroupName = "水滴", Name = "水滴サイズ", Description = "外側の水滴全体の基準サイズを調整します。個々の水滴には大小の差が付きます。簡易物理では大きさが滑り始める条件にも影響します。")]
    [TextBoxSlider("F1", "%", OutsideDropletSettings.MinimumSizePercent, OutsideDropletSettings.MaximumSizePercent)]
    [ShowPropertyEditorWhen(nameof(AreOutsideDropletSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.OutsideDropletSizePercent)]
    [Range(OutsideDropletSettings.MinimumSizePercent, OutsideDropletSettings.MaximumSizePercent)]
    public double OutsideDropletSize
    {
        get => _outsideDropletSize;
        set
        {
            if (Set(ref _outsideDropletSize, value))
            {
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [Display(GroupName = "水滴", Name = "水滴の濃さ", Description = "外側の水滴の輪郭、ハイライト、影をまとめて調整します。リアル（屈折）では背景を歪める表現の強さにも反映します。")]
    [TextBoxSlider("F1", "%", OutsideDropletSettings.MinimumStrengthPercent, OutsideDropletSettings.MaximumStrengthPercent)]
    [ShowPropertyEditorWhen(nameof(AreOutsideDropletSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.OutsideDropletStrengthPercent)]
    [Range(OutsideDropletSettings.MinimumStrengthPercent, OutsideDropletSettings.MaximumStrengthPercent)]
    public double OutsideDropletStrength
    {
        get => _outsideDropletStrength;
        set
        {
            if (Set(ref _outsideDropletStrength, value))
            {
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [Display(GroupName = "水滴", Name = "Seed", Description = "外側の水滴配置を固定する整数です。同じ値ではシークや再読込後も同じ配置になります。")]
    [TextBoxSlider("F0", "", OutsideDropletSettings.MinimumSeed, OutsideDropletSettings.MaximumSeed)]
    [ShowPropertyEditorWhen(nameof(AreOutsideDropletSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.OutsideDropletSeed)]
    [Range(OutsideDropletSettings.MinimumSeed, OutsideDropletSettings.MaximumSeed)]
    public double OutsideDropletSeed
    {
        get => _outsideDropletSeed;
        set
        {
            if (Set(ref _outsideDropletSeed, value))
            {
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [Display(GroupName = "水滴", Name = "水滴の見た目", Description = "白い輪郭、黒い輪郭、リアル（屈折）を選択します。リアルは入力映像を水滴内で上下左右反転して歪める近似表現です。水筋は従来表現です。四隅指定でも水滴の形を画面上で保ち、面に沿った変形は行いません。")]
    [EnumComboBox]
    [ShowPropertyEditorWhen(nameof(AreOutsideDropletSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.OutsideDropletAppearance)]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public OutsideDropletAppearanceValue OutsideDropletAppearance
    {
        get => _outsideDropletAppearance;
        set
        {
            _outsideDropletAppearanceWasSet = true;
            if (Set(
                    ref _outsideDropletAppearance,
                    OutsideDropletSettings.SanitizeAppearance(value)))
            {
                OnPropertyChanged(nameof(IsOutsideDropletOutlineOpacityVisible));
                OnPropertyChanged(nameof(IsOutsideDropletDeformWithSurfaceVisible));
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [Display(GroupName = "水滴", Name = "輪郭の濃さ", Description = "黒い輪郭の不透明度です。0%で透明、100%で元の濃さにします。水滴全体の濃さや白い反射は維持します。")]
    [TextBoxSlider("F1", "%", OutsideDropletSettings.MinimumOutlineOpacityPercent, OutsideDropletSettings.MaximumOutlineOpacityPercent)]
    [ShowPropertyEditorWhen(nameof(IsOutsideDropletOutlineOpacityVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.OutsideDropletOutlineOpacityPercent)]
    [Range(OutsideDropletSettings.MinimumOutlineOpacityPercent, OutsideDropletSettings.MaximumOutlineOpacityPercent)]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public double OutsideDropletOutlineOpacity
    {
        get => _outsideDropletOutlineOpacity;
        set
        {
            if (Set(ref _outsideDropletOutlineOpacity, value))
            {
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [Display(GroupName = "水滴", Name = "面に合わせる", Description = "四角形（四隅指定）で水滴形状を面の遠近変形へ追従させます。オフでは水滴の丸みと画面下方向への落下を優先し、領域の内側だけへ表示します。")]
    [ToggleSlider]
    [ShowPropertyEditorWhen(nameof(IsOutsideDropletDeformWithSurfaceVisible), true)]
    [DefaultValue(true)]
    public bool OutsideDropletDeformWithSurface
    {
        get => _outsideDropletDeformWithSurface;
        set
        {
            if (Set(ref _outsideDropletDeformWithSurface, value))
            {
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [Display(GroupName = "水滴", Name = "水滴を垂らす", Description = "外側の水滴を画面下へ落下させます。簡易物理では大きさと滑りやすさで滑り始めます。OFFでも雨の補給は続きます。同じ設定と時刻ではシーク後も同じ状態を再現します。拭き取りでは消えません。")]
    [ToggleSlider]
    [ShowPropertyEditorWhen(nameof(AreOutsideDropletSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.OutsideDropletFallEnabled)]
    public bool OutsideDropletFallEnabled
    {
        get => _outsideDropletFallEnabled;
        set
        {
            if (Set(ref _outsideDropletFallEnabled, value))
            {
                OnPropertyChanged(nameof(AreOutsideDropletFallSettingsVisible));
                NotifyOutsideDropletPhysicsVisibilityChanged();
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [Display(GroupName = "水滴", Name = "水滴の合体", Description = "接触した水滴を合体させます。周期動作では小さい滴を大きい滴へ吸収します。簡易物理では水量を足し合わせ、大きくなった滴が滑り始めることがあります。")]
    [ToggleSlider]
    [ShowPropertyEditorWhen(nameof(IsOutsideDropletMergeVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.OutsideDropletMergeEnabled)]
    public bool OutsideDropletMergeEnabled
    {
        get => _outsideDropletMergeEnabled;
        set
        {
            if (Set(ref _outsideDropletMergeEnabled, value))
            {
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [Display(GroupName = "水滴", Name = "雨の降り始め", Description = "アイテム先頭から指定した時刻に、水滴を徐々に出現させます。")]
    [ToggleSlider]
    [ShowPropertyEditorWhen(nameof(AreOutsideDropletSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.OutsideDropletRainEnabled)]
    public bool OutsideDropletRainEnabled
    {
        get => _outsideDropletRainEnabled;
        set
        {
            if (Set(ref _outsideDropletRainEnabled, value))
            {
                OnPropertyChanged(nameof(AreOutsideDropletRainTimingSettingsVisible));
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [Display(GroupName = "水滴", Name = "出現開始時間", Description = "アイテム先頭から水滴の出現を始める時刻です。")]
    [TextBoxSlider("F2", "秒", OutsideDropletSettings.MinimumRainStartSeconds, OutsideDropletSettings.MaximumRainStartSeconds)]
    [ShowPropertyEditorWhen(nameof(AreOutsideDropletRainTimingSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.OutsideDropletRainStartSeconds)]
    [Range(OutsideDropletSettings.MinimumRainStartSeconds, OutsideDropletSettings.MaximumRainStartSeconds)]
    public double OutsideDropletRainStartSeconds
    {
        get => _outsideDropletRainStartSeconds;
        set
        {
            if (Set(ref _outsideDropletRainStartSeconds, value))
            {
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [Display(GroupName = "水滴", Name = "出現期間", Description = "各水滴が初めて出現する時刻を分散させる期間です。0秒なら一斉に出現します。")]
    [TextBoxSlider("F2", "秒", OutsideDropletSettings.MinimumRainDurationSeconds, OutsideDropletSettings.MaximumRainDurationSeconds)]
    [ShowPropertyEditorWhen(nameof(AreOutsideDropletRainTimingSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.OutsideDropletRainDurationSeconds)]
    [Range(OutsideDropletSettings.MinimumRainDurationSeconds, OutsideDropletSettings.MaximumRainDurationSeconds)]
    public double OutsideDropletRainDurationSeconds
    {
        get => _outsideDropletRainDurationSeconds;
        set
        {
            if (Set(ref _outsideDropletRainDurationSeconds, value))
            {
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [Display(GroupName = "水滴", Name = "垂れる割合", Description = "落下させる水滴の割合です。大きな水滴ほど選ばれやすくなります。")]
    [TextBoxSlider("F1", "%", OutsideDropletSettings.MinimumFallingRatioPercent, OutsideDropletSettings.MaximumFallingRatioPercent)]
    [ShowPropertyEditorWhen(nameof(AreOutsideDropletLegacyFallSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.OutsideDropletFallingRatioPercent)]
    [Range(OutsideDropletSettings.MinimumFallingRatioPercent, OutsideDropletSettings.MaximumFallingRatioPercent)]
    public double OutsideDropletFallingRatio
    {
        get => _outsideDropletFallingRatio;
        set
        {
            if (Set(ref _outsideDropletFallingRatio, value))
            {
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [Display(GroupName = "水滴", Name = "垂れる速さ", Description = "水滴が画面下方向へ垂れる速さです。周期動作の基準は100%で領域の高さを約6秒、簡易物理では運動計算の時間倍率です。簡易物理の雨の補給と出現時刻は変わりません。")]
    [TextBoxSlider("F1", "%", OutsideDropletSettings.MinimumFallSpeedPercent, OutsideDropletSettings.MaximumFallSpeedPercent)]
    [ShowPropertyEditorWhen(nameof(AreOutsideDropletFallSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.OutsideDropletFallSpeedPercent)]
    [Range(OutsideDropletSettings.MinimumFallSpeedPercent, OutsideDropletSettings.MaximumFallSpeedPercent)]
    public double OutsideDropletFallSpeed
    {
        get => _outsideDropletFallSpeed;
        set
        {
            if (Set(ref _outsideDropletFallSpeed, value))
            {
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [Display(GroupName = "水滴", Name = "垂れる頻度", Description = "落下滴の候補数を増やします。100%が標準、400%で候補数は最大4倍です。大きさ・垂れる速さは変わりません。合体などにより実際に見える数は変わります。")]
    [TextBoxSlider("F1", "%", OutsideDropletSettings.MinimumFallFrequencyPercent, OutsideDropletSettings.MaximumFallFrequencyPercent)]
    [ShowPropertyEditorWhen(nameof(AreOutsideDropletLegacyFallSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.OutsideDropletFallFrequencyPercent)]
    [Range(OutsideDropletSettings.MinimumFallFrequencyPercent, OutsideDropletSettings.MaximumFallFrequencyPercent)]
    public double OutsideDropletFallFrequency
    {
        get => _outsideDropletFallFrequency;
        set
        {
            if (Set(ref _outsideDropletFallFrequency, value))
            {
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [Display(GroupName = "水滴", Name = "水筋の長さ", Description = "落下した滴の後ろに見せる水筋の長さです。0%で非表示。周期動作では100%で落下開始位置まで、簡易物理では画面高さに対して上限のある長さまで表示します。長い水筋ほど描画負荷が増えます。")]
    [TextBoxSlider("F1", "%", OutsideDropletSettings.MinimumTrailLengthPercent, OutsideDropletSettings.MaximumTrailLengthPercent)]
    [ShowPropertyEditorWhen(nameof(AreOutsideDropletFallSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.OutsideDropletTrailLengthPercent)]
    [Range(OutsideDropletSettings.MinimumTrailLengthPercent, OutsideDropletSettings.MaximumTrailLengthPercent)]
    public double OutsideDropletTrailLength
    {
        get => _outsideDropletTrailLength;
        set
        {
            if (Set(ref _outsideDropletTrailLength, value))
            {
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [Display(GroupName = "領域", Name = "形状", Description = "入力領域全体、四角形、円・楕円、四角形（四隅指定）から曇りを適用する領域を選択")]
    [EnumComboBox]
    [DefaultValue(GlassWipeDefaultSettings.RegionShape)]
    public GlassWipeRegionShape RegionShape
    {
        get => _regionShape;
        set
        {
            if (Set(ref _regionShape, value))
            {
                NotifySimpleVisibilityChanged();
            }
        }
    }

    [Display(GroupName = "四角形（四隅指定）", Name = "プリセット", Description = "カスタムでは下の4点を使用し、フロントガラスでは対称台形を使用する")]
    [EnumComboBox]
    [ShowPropertyEditorWhen(nameof(IsQuadConfigurationVisible), true)]
    public GlassWipeQuadPreset QuadPreset
    {
        get => _quadPreset;
        set
        {
            if (Set(ref _quadPreset, value))
            {
                OnPropertyChanged(nameof(AreQuadPointsVisible));
            }
        }
    }

    [Display(GroupName = "四角形（四隅指定）", Name = "左上X", Description = "入力画像左上を原点とする左上頂点の横位置")]
    [AnimationSlider("F1", "%", GlassWipeParameterSanitizer.MinimumQuadCoordinatePercent, GlassWipeParameterSanitizer.MaximumQuadCoordinatePercent)]
    [ShowPropertyEditorWhen(nameof(AreQuadPointsVisible), true)]
    public Animation QuadTopLeftX { get; } = new(10, GlassWipeParameterSanitizer.MinimumQuadCoordinatePercent, GlassWipeParameterSanitizer.MaximumQuadCoordinatePercent);

    [Display(GroupName = "四角形（四隅指定）", Name = "左上Y", Description = "入力画像左上を原点とする左上頂点の縦位置")]
    [AnimationSlider("F1", "%", GlassWipeParameterSanitizer.MinimumQuadCoordinatePercent, GlassWipeParameterSanitizer.MaximumQuadCoordinatePercent)]
    [ShowPropertyEditorWhen(nameof(AreQuadPointsVisible), true)]
    public Animation QuadTopLeftY { get; } = new(20, GlassWipeParameterSanitizer.MinimumQuadCoordinatePercent, GlassWipeParameterSanitizer.MaximumQuadCoordinatePercent);

    [Display(GroupName = "四角形（四隅指定）", Name = "右上X", Description = "入力画像左上を原点とする右上頂点の横位置")]
    [AnimationSlider("F1", "%", GlassWipeParameterSanitizer.MinimumQuadCoordinatePercent, GlassWipeParameterSanitizer.MaximumQuadCoordinatePercent)]
    [ShowPropertyEditorWhen(nameof(AreQuadPointsVisible), true)]
    public Animation QuadTopRightX { get; } = new(90, GlassWipeParameterSanitizer.MinimumQuadCoordinatePercent, GlassWipeParameterSanitizer.MaximumQuadCoordinatePercent);

    [Display(GroupName = "四角形（四隅指定）", Name = "右上Y", Description = "入力画像左上を原点とする右上頂点の縦位置")]
    [AnimationSlider("F1", "%", GlassWipeParameterSanitizer.MinimumQuadCoordinatePercent, GlassWipeParameterSanitizer.MaximumQuadCoordinatePercent)]
    [ShowPropertyEditorWhen(nameof(AreQuadPointsVisible), true)]
    public Animation QuadTopRightY { get; } = new(20, GlassWipeParameterSanitizer.MinimumQuadCoordinatePercent, GlassWipeParameterSanitizer.MaximumQuadCoordinatePercent);

    [Display(GroupName = "四角形（四隅指定）", Name = "右下X", Description = "入力画像左上を原点とする右下頂点の横位置")]
    [AnimationSlider("F1", "%", GlassWipeParameterSanitizer.MinimumQuadCoordinatePercent, GlassWipeParameterSanitizer.MaximumQuadCoordinatePercent)]
    [ShowPropertyEditorWhen(nameof(AreQuadPointsVisible), true)]
    public Animation QuadBottomRightX { get; } = new(90, GlassWipeParameterSanitizer.MinimumQuadCoordinatePercent, GlassWipeParameterSanitizer.MaximumQuadCoordinatePercent);

    [Display(GroupName = "四角形（四隅指定）", Name = "右下Y", Description = "入力画像左上を原点とする右下頂点の縦位置")]
    [AnimationSlider("F1", "%", GlassWipeParameterSanitizer.MinimumQuadCoordinatePercent, GlassWipeParameterSanitizer.MaximumQuadCoordinatePercent)]
    [ShowPropertyEditorWhen(nameof(AreQuadPointsVisible), true)]
    public Animation QuadBottomRightY { get; } = new(80, GlassWipeParameterSanitizer.MinimumQuadCoordinatePercent, GlassWipeParameterSanitizer.MaximumQuadCoordinatePercent);

    [Display(GroupName = "四角形（四隅指定）", Name = "左下X", Description = "入力画像左上を原点とする左下頂点の横位置")]
    [AnimationSlider("F1", "%", GlassWipeParameterSanitizer.MinimumQuadCoordinatePercent, GlassWipeParameterSanitizer.MaximumQuadCoordinatePercent)]
    [ShowPropertyEditorWhen(nameof(AreQuadPointsVisible), true)]
    public Animation QuadBottomLeftX { get; } = new(10, GlassWipeParameterSanitizer.MinimumQuadCoordinatePercent, GlassWipeParameterSanitizer.MaximumQuadCoordinatePercent);

    [Display(GroupName = "四角形（四隅指定）", Name = "左下Y", Description = "入力画像左上を原点とする左下頂点の縦位置")]
    [AnimationSlider("F1", "%", GlassWipeParameterSanitizer.MinimumQuadCoordinatePercent, GlassWipeParameterSanitizer.MaximumQuadCoordinatePercent)]
    [ShowPropertyEditorWhen(nameof(AreQuadPointsVisible), true)]
    public Animation QuadBottomLeftY { get; } = new(80, GlassWipeParameterSanitizer.MinimumQuadCoordinatePercent, GlassWipeParameterSanitizer.MaximumQuadCoordinatePercent);

    [Display(GroupName = "領域", Name = "中心X", Description = "入力画像に対する領域中心の横位置")]
    [AnimationSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(IsRegionTransformVisible), true)]
    public Animation RegionCenterX { get; } = new(50, 0, 100);

    [Display(GroupName = "領域", Name = "中心Y", Description = "入力画像に対する領域中心の縦位置")]
    [AnimationSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(IsRegionTransformVisible), true)]
    public Animation RegionCenterY { get; } = new(50, 0, 100);

    [Display(GroupName = "領域", Name = "幅", Description = "入力画像に対する領域の幅")]
    [AnimationSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(IsRegionTransformVisible), true)]
    public Animation RegionWidth { get; } = new(80, 0, 100);

    [Display(GroupName = "領域", Name = "高さ", Description = "入力画像に対する領域の高さ")]
    [AnimationSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(IsRegionTransformVisible), true)]
    public Animation RegionHeight { get; } = new(60, 0, 100);

    [Display(GroupName = "領域", Name = "回転角", Description = "曇りを適用する領域を中心の周りに回転する角度")]
    [AnimationSlider("F1", "°", -180, 180)]
    [ShowPropertyEditorWhen(nameof(IsRegionTransformVisible), true)]
    public Animation RegionRotation { get; } = new(0, -180, 180);

    [Display(GroupName = "領域", Name = "境界ぼかし", Description = "領域の内側へ適用する境界のぼかし幅")]
    [TextBoxSlider("F1", "px", 0, 100)]
    [DefaultValue(GlassWipeDefaultSettings.RegionFeatherPixels)]
    [Range(0, 1000)]
    public double RegionFeather
    {
        get => _regionFeather;
        set => Set(ref _regionFeather, value);
    }

    [Display(GroupName = "軌跡", Name = "軌跡の種類", Description = "標準モード、従来のキーフレーム、複数ストロークのカスタム軌跡から選択")]
    [EnumComboBox]
    [ShowPropertyEditorWhen(nameof(IsDetailedMode), true)]
    public WipePathInputMode PathInputMode
    {
        get => _pathInputMode;
        set
        {
            _pathInputModeWasSet = true;
            if (Set(ref _pathInputMode, WipePathInputModeCompatibility.Normalize(value)))
            {
                NotifyDetailedVisibilityChanged();
            }
        }
    }

    [JsonIgnore]
    [Display(GroupName = "軌跡", Name = "プリセット", Description = DetailedWipePresetTooltipText.Description)]
    [DetailedWipePresetEditor]
    [ShowPropertyEditorWhen(nameof(IsDetailedMode), true)]
    public string DetailedPresetExchange
    {
        get => DetailedWipePresetExchangeCodec.EncodeSnapshot(
            PathInputMode,
            CaptureDetailedPresetState());
        set
        {
            if (DetailedWipePresetExchangeCodec.TryDecode(value, out var exchange) &&
                exchange.ApplyScope is { } scope)
            {
                ApplyDetailedPresetState(exchange.State, scope);
            }
        }
    }

    [Display(GroupName = "軌跡", Name = "カスタム軌跡", Description = "ストロークと制御点を追加、削除、並べ替え")]
    [WipeStrokeEditor]
    [ShowPropertyEditorWhen(nameof(IsDetailedCustomPathVisible), true)]
    public string CustomPathData
    {
        get => _customPathData;
        set => Set(ref _customPathData, value ?? string.Empty);
    }

    [Display(GroupName = "軌跡", Name = "X", Description = "曇りを適用する領域内におけるブラシ中心の横位置")]
    [AnimationSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(IsDetailedLegacyPathVisible), true)]
    public Animation BrushX { get; } = new(50, 0, 100);

    [Display(GroupName = "軌跡", Name = "Y", Description = "曇りを適用する領域内におけるブラシ中心の縦位置")]
    [AnimationSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(IsDetailedLegacyPathVisible), true)]
    public Animation BrushY { get; } = new(50, 0, 100);

    [Display(GroupName = "軌跡", Name = "接触率", Description = "0%ではブラシを持ち上げ、前後の軌跡を接続しない")]
    [AnimationSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(IsDetailedLegacyPathVisible), true)]
    public Animation Contact { get; } = new(100, 0, 100);

    // 保存とUndo／Redoはこのプロパティを使い、選択時の既定値を再適用しない。
    [Browsable(false)]
    [DefaultValue(GlassWipeDefaultSettings.BrushShape)]
    public GlassWipeBrushShape BrushShape
    {
        get => _brushShape;
        set => SetBrushShape(value, applySelectionDefault: false);
    }

    [JsonIgnore]
    [System.Runtime.Serialization.IgnoreDataMember]
    [Display(GroupName = "ブラシ", Name = "形状", Description = "円、楕円、四角形、手形、靴跡、ユーザー画像から拭き取り形状を選択")]
    [EnumComboBox]
    [DefaultValue(GlassWipeDefaultSettings.BrushShape)]
    public GlassWipeBrushShape BrushShapeSelection
    {
        get => BrushShape;
        set => SetBrushShape(value, applySelectionDefault: true);
    }

    /// <summary>
    /// 手動の形状切り替え時だけ連続拭きを既定ONにし、保存値の復元とは区別します。
    /// </summary>
    private void SetBrushShape(GlassWipeBrushShape value, bool applySelectionDefault)
    {
        var shape = GlassWipeParameterSanitizer.SanitizeBrushShape(value);
        if (!Set(ref _brushShape, shape, nameof(BrushShape)))
        {
            return;
        }

        if (applySelectionDefault && GlassWipeSimpleVisibility.IsContinuousWipeVisible(shape))
        {
            ContinuousWipe = true;
        }
        OnPropertyChanged(nameof(BrushShapeSelection));
        OnPropertyChanged(nameof(IsBrushMirrorVisible));
        OnPropertyChanged(nameof(IsContinuousWipeVisible));
        OnPropertyChanged(nameof(IsUserBrushVisible));
        OnPropertyChanged(nameof(IsPngBrushSizeVisible));
        OnPropertyChanged(nameof(IsCircleBrushSizeVisible));
        OnPropertyChanged(nameof(IsEllipseBrushSizeVisible));
        OnPropertyChanged(nameof(IsRectangleBrushSizeVisible));
    }

    [Browsable(false)]
    public Guid UserBrushId
    {
        get => _userBrushId;
        set
        {
            if (Set(ref _userBrushId, value))
            {
                _userBrushRevision = 0;
                _userBrushPixelWidth = 0;
                _userBrushPixelHeight = 0;
                OnPropertyChanged(nameof(UserBrushExchange));
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [JsonIgnore]
    [Display(GroupName = "ブラシ", Name = "ユーザー画像", Description = "ローカルライブラリへ登録したPNGブラシを選択、追加、置換、削除")]
    [UserBrushEditor]
    [ShowPropertyEditorWhen(nameof(IsUserBrushVisible), true)]
    public string UserBrushExchange
    {
        get => UserBrushExchangeCodec.EncodeSnapshot(
            UserBrushId,
            _userBrushRevision,
            UserBrushPixelWidth,
            UserBrushPixelHeight);
        set
        {
            if (!UserBrushExchangeCodec.TryDecode(value, out var exchange) ||
                !exchange.Apply)
            {
                return;
            }

            UserBrushId = exchange.UserBrushId;
            UserBrushRevision = exchange.Revision;
            UserBrushPixelWidth = exchange.PixelWidth;
            UserBrushPixelHeight = exchange.PixelHeight;
        }
    }

    [Browsable(false)]
    [DefaultValue(0L)]
    public long UserBrushRevision
    {
        get => _userBrushRevision;
        set => Set(
            ref _userBrushRevision,
            Math.Max(0, value),
            nameof(UserBrushRevision),
            nameof(UserBrushExchange),
            nameof(DetailedPresetExchange));
    }

    [Browsable(false)]
    public int UserBrushPixelWidth
    {
        get => _userBrushPixelWidth;
        set => Set(
            ref _userBrushPixelWidth,
            Math.Clamp(value, 0, UserBrushLibrary.MaximumDimension),
            nameof(UserBrushPixelWidth),
            nameof(UserBrushExchange),
            nameof(DetailedPresetExchange));
    }

    [Browsable(false)]
    public int UserBrushPixelHeight
    {
        get => _userBrushPixelHeight;
        set => Set(
            ref _userBrushPixelHeight,
            Math.Clamp(value, 0, UserBrushLibrary.MaximumDimension),
            nameof(UserBrushPixelHeight),
            nameof(UserBrushExchange),
            nameof(DetailedPresetExchange));
    }

    [Display(GroupName = "ブラシ", Name = "左右反転", Description = "手形は左右の手、靴跡は左右の足を切り替えます")]
    [ToggleSlider]
    [ShowPropertyEditorWhen(nameof(IsBrushMirrorVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.BrushMirror)]
    public bool BrushMirror
    {
        get => _brushMirror;
        set => Set(ref _brushMirror, value);
    }

    [Display(GroupName = "ブラシ", Name = "連続拭き", Description = "手形、靴跡、ユーザー画像の拭き跡を連続してつなげる")]
    [ToggleSlider]
    [ShowPropertyEditorWhen(nameof(IsContinuousWipeVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.ContinuousWipe)]
    public bool ContinuousWipe
    {
        get => _continuousWipe;
        set => Set(ref _continuousWipe, value);
    }

    [Display(GroupName = "ブラシ", Name = GlassWipeBrushUiText.CircleDiameterName, Description = GlassWipeBrushUiText.CircleDiameterDescription)]
    [AnimationSlider("F0", "px", 0, 8192)]
    [ShowPropertyEditorWhen(nameof(IsCircleBrushSizeVisible), true)]
    public Animation CircleDiameterPixels { get; } = new(
        GlassWipeDefaultSettings.CircleDiameterPixels,
        0,
        8192);

    [Display(GroupName = "ブラシ", Name = GlassWipeBrushUiText.WidthName, Description = GlassWipeBrushUiText.EllipseWidthDescription)]
    [AnimationSlider("F0", "px", 0, 8192)]
    [ShowPropertyEditorWhen(nameof(IsEllipseBrushSizeVisible), true)]
    public Animation EllipseWidthPixels { get; } = new(
        GlassWipeDefaultSettings.EllipseWidthPixels,
        0,
        8192);

    [Display(GroupName = "ブラシ", Name = GlassWipeBrushUiText.HeightName, Description = GlassWipeBrushUiText.EllipseHeightDescription)]
    [AnimationSlider("F0", "px", 0, 8192)]
    [ShowPropertyEditorWhen(nameof(IsEllipseBrushSizeVisible), true)]
    public Animation EllipseHeightPixels { get; } = new(
        GlassWipeDefaultSettings.EllipseHeightPixels,
        0,
        8192);

    [Display(GroupName = "ブラシ", Name = GlassWipeBrushUiText.WidthName, Description = GlassWipeBrushUiText.RectangleWidthDescription)]
    [AnimationSlider("F0", "px", 0, 8192)]
    [ShowPropertyEditorWhen(nameof(IsRectangleBrushSizeVisible), true)]
    public Animation RectangleWidthPixels { get; } = new(
        GlassWipeDefaultSettings.RectangleWidthPixels,
        0,
        8192);

    [Display(GroupName = "ブラシ", Name = GlassWipeBrushUiText.HeightName, Description = GlassWipeBrushUiText.RectangleHeightDescription)]
    [AnimationSlider("F0", "px", 0, 8192)]
    [ShowPropertyEditorWhen(nameof(IsRectangleBrushSizeVisible), true)]
    public Animation RectangleHeightPixels { get; } = new(
        GlassWipeDefaultSettings.RectangleHeightPixels,
        0,
        8192);

    [Display(GroupName = "ブラシ", Name = "サイズ倍率", Description = "手形、靴跡、ユーザー画像のPNG元キャンバス寸法に対する倍率")]
    [AnimationSlider("F1", "%", 25, 400)]
    [ShowPropertyEditorWhen(nameof(IsPngBrushSizeVisible), true)]
    public Animation PngBrushSizeScale { get; } = new(
        GlassWipeDefaultSettings.PngBrushSizeScalePercent,
        25,
        400);

    [Display(GroupName = "ブラシ", Name = "回転角", Description = "標準モードの既定角度へ加算するブラシ回転角")]
    [AnimationSlider("F1", "°", -180, 180)]
    [ShowPropertyEditorWhen(nameof(AreSimpleGeneratedBrushSettingsVisible), true)]
    public Animation SimpleGeneratedBrushRotation { get; } = new(GlassWipeDefaultSettings.SimpleGeneratedBrushRotationDegrees, -180, 180);

    [Display(GroupName = "ブラシ", Name = "回転追従", Description = "標準モードのブラシを移動方向へ向ける割合。回転角は追従方向への加算値")]
    [TextBoxSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(AreSimpleGeneratedBrushSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.SimpleGeneratedBrushRotationFollowPercent)]
    [Range(0, 100)]
    public double SimpleGeneratedBrushRotationFollow
    {
        get => _simpleGeneratedBrushRotationFollow;
        set => Set(ref _simpleGeneratedBrushRotationFollow, value);
    }

    [Display(GroupName = "ブラシ", Name = "回転角", Description = "曇りを適用する領域内でブラシを回転する角度")]
    [AnimationSlider("F1", "°", -180, 180)]
    [ShowPropertyEditorWhen(nameof(AreDetailedPathSettingsVisible), true)]
    public Animation BrushRotation { get; } = new(0, -180, 180);

    [Display(GroupName = "ブラシ", Name = "軌跡平滑化", Description = "軌跡の角を滑らかにする強さ。0%で元の折れ線を維持")]
    [TextBoxSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(AreDetailedPathSettingsVisible), true)]
    [DefaultValue(0d)]
    [Range(0, 100)]
    public double PathSmoothing
    {
        get => _pathSmoothing;
        set => Set(ref _pathSmoothing, value);
    }

    [Display(GroupName = "ブラシ", Name = "微小揺れ", Description = "曇りを適用する領域の短辺に対する軌跡の揺れ幅。0%で無効")]
    [TextBoxSlider("F2", "%", 0, 5)]
    [ShowPropertyEditorWhen(nameof(AreDetailedPathSettingsVisible), true)]
    [DefaultValue(0d)]
    [Range(0, 5)]
    public double PathJitter
    {
        get => _pathJitter;
        set => Set(ref _pathJitter, value);
    }

    [Display(GroupName = "ブラシ", Name = "回転追従", Description = "移動方向へブラシを向ける割合。回転角は追従方向への加算値として扱う")]
    [TextBoxSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(AreDetailedPathSettingsVisible), true)]
    [DefaultValue(0d)]
    [Range(0, 100)]
    public double BrushRotationFollow
    {
        get => _brushRotationFollow;
        set => Set(ref _brushRotationFollow, value);
    }

    [Display(GroupName = "ブラシ", Name = "柔らかさ", Description = "ブラシ中心から外周へ拭き取り強度を滑らかに減衰する割合")]
    [TextBoxSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(AreDetailedPathSettingsVisible), true)]
    [DefaultValue(0d)]
    [Range(0, 100)]
    public double BrushSoftness
    {
        get => _brushSoftness;
        set => Set(ref _brushSoftness, value);
    }

    [Display(GroupName = "ブラシ", Name = "Seed", Description = "微小揺れ、拭きムラ、曇りノイズの形を固定する整数")]
    [TextBoxSlider("F0", "", 0, 9999)]
    [ShowPropertyEditorWhen(nameof(AreDetailedPathSettingsVisible), true)]
    [DefaultValue(1d)]
    [Range(0, 9999)]
    public double JitterSeed
    {
        get => _jitterSeed;
        set => Set(ref _jitterSeed, value);
    }

    [Display(GroupName = "ブラシ", Name = "拭き残し", Description = "完全には拭き取らず、通過範囲へ残す曇りの割合")]
    [TextBoxSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(AreDetailedPathSettingsVisible), true)]
    [DefaultValue(0d)]
    [Range(0, 100)]
    public double WipeResidue
    {
        get => _wipeResidue;
        set => Set(ref _wipeResidue, value);
    }

    [Display(GroupName = "ブラシ", Name = "拭きムラ", Description = "Seedで固定した拭き取り強度のムラを加える")]
    [TextBoxSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(AreDetailedPathSettingsVisible), true)]
    [DefaultValue(0d)]
    [Range(0, 100)]
    public double WipeVariation
    {
        get => _wipeVariation;
        set => Set(ref _wipeVariation, value);
    }

    [Display(GroupName = "曇り", Name = "曇りノイズ", Description = "Seedで固定した曇り色の濃淡を加える")]
    [TextBoxSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(AreFogNoiseSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.FogNoisePercent)]
    [Range(0, 100)]
    public double FogNoise
    {
        get => _fogNoise;
        set => Set(ref _fogNoise, value);
    }

    [Display(GroupName = "ブラシ", Name = "描画品質", Description = "軌跡を構成するブラシの間隔を選択します。\n軽量は間隔を広げ、標準は通常の間隔、高品質は間隔を狭めます。\n自動は通常の定型軌跡で標準、往復系で高品質を使用します。\n形状や拭き取り強度など、ほかのブラシ設定は変更しません。\n高品質は、4Kや長尺の素材で描画負荷が増える場合があります。\n動作が重い場合は、自動、標準、または軽量を選択してください。")]
    [EnumComboBox]
    [ShowPropertyEditorWhen(nameof(AreSimpleGeneratedBrushSettingsVisible), true)]
    [DefaultValue(GlassWipeSimpleGeneratedQuality.Auto)]
    public GlassWipeSimpleGeneratedQuality SimpleGeneratedQuality
    {
        get => _simpleGeneratedQuality;
        set => Set(
            ref _simpleGeneratedQuality,
            GlassWipeSimpleGeneratedQualityCompatibility.Normalize(value));
    }

    [Display(GroupName = "ブラシ", Name = "描画品質", Description = "軌跡を構成するブラシの間隔を選択します。\n軽量は間隔を広げ、標準は通常の間隔、高品質は間隔を狭めます。\n形状や拭き取り強度など、ほかのブラシ設定は変更しません。\n高品質は、4Kや長尺の素材で描画負荷が増える場合があります。\n動作が重い場合は、標準、または軽量を選択してください。")]
    [EnumComboBox]
    [ShowPropertyEditorWhen(nameof(AreDetailedPathSettingsVisible), true)]
    public GlassWipeQuality Quality
    {
        get => _quality;
        set => Set(ref _quality, value);
    }

    [Display(GroupName = "ブラシ", Name = "拭き取り強度", Description = "ブラシが通過した範囲の曇りを除去する強さ")]
    [AnimationSlider("F1", "%", 0, 100)]
    [ShowPropertyEditorWhen(nameof(AreDetailedPathSettingsVisible), true)]
    public Animation WipeStrength { get; } = new(100, 0, 100);

    [Display(GroupName = "デバッグ", Name = "表示", Description = "領域と累積拭き取りマスクを切り替えて確認")]
    [EnumComboBox]
    [ShowPropertyEditorWhen(nameof(IsDetailedMode), true)]
    public GlassWipeDebugView DebugView
    {
        get => _debugView;
        set => Set(ref _debugView, value);
    }

    public void OnDeserialized()
    {
        _outsideDropletMotionMode = OutsideDropletSettings.SanitizeMotionMode(_outsideDropletMotionMode);
        _outsideDropletSlip = OutsideDropletSettings.SanitizeSlipPercent(_outsideDropletSlip);
        _outsideDropletSupply = OutsideDropletSettings.SanitizeSupplyPercent(_outsideDropletSupply);
        _simpleGeneratedQuality = GlassWipeSimpleGeneratedQualityCompatibility.Normalize(
            _simpleGeneratedQuality);
        _simpleRoundTrips = WipeSimplePathGenerator.SanitizeRoundTrips(
            _simpleRoundTrips);
        _pathTimingMode = WipePathTimingModeCompatibility.Normalize(_pathTimingMode);
        _pathStart = WipePathTiming.SanitizeStartPercent(_pathStart);
        _pathCompletion = WipePathTiming.ResolveAfterDeserialization(
            _pathCompletionWasSet,
            _pathCompletion);
        _pathStartFrame = WipePathTiming.SanitizeFixedFrame(_pathStartFrame);
        _pathCompletionFrame = WipePathTiming.SanitizeFixedFrame(
            _pathCompletionFrame,
            WipePathTiming.DefaultCompletionFrame);
        _pathStartSeconds = WipePathTiming.SanitizeSeconds(_pathStartSeconds);
        _pathCompletionSeconds = WipePathTiming.SanitizeSeconds(
            _pathCompletionSeconds,
            WipePathTiming.DefaultCompletionSeconds);
        _outsideDropletAmount = OutsideDropletSettings.SanitizeAmountPercent(
            _outsideDropletAmount);
        _outsideDropletSize = OutsideDropletSettings.SanitizeSizePercent(
            _outsideDropletSize);
        _outsideDropletStrength = OutsideDropletSettings.SanitizeStrengthPercent(
            _outsideDropletStrength);
        _outsideDropletSeed = OutsideDropletSettings.SanitizeSeedValue(
            _outsideDropletSeed);
        _outsideDropletFallingRatio = OutsideDropletSettings.SanitizeFallingRatioPercent(
            _outsideDropletFallingRatio);
        _outsideDropletFallSpeed = OutsideDropletSettings.SanitizeFallSpeedPercent(
            _outsideDropletFallSpeed);
        _outsideDropletFallFrequency = OutsideDropletSettings.SanitizeFallFrequencyPercent(
            _outsideDropletFallFrequency);
        _outsideDropletTrailLength = OutsideDropletSettings.SanitizeTrailLengthPercent(
            _outsideDropletTrailLength);
        _outsideDropletRainStartSeconds = OutsideDropletSettings.SanitizeRainStartSeconds(_outsideDropletRainStartSeconds);
        _outsideDropletRainDurationSeconds = OutsideDropletSettings.SanitizeRainDurationSeconds(_outsideDropletRainDurationSeconds);
        _outsideDropletOutlineOpacity =
            OutsideDropletSettings.SanitizeOutlineOpacityPercent(_outsideDropletOutlineOpacity);
        _outsideDropletAppearance =
            OutsideDropletAppearanceCompatibility.ResolveAfterDeserialization(
                _outsideDropletAppearanceWasSet,
                _outsideDropletAppearance);
        _editingMode = GlassWipeEditingModeCompatibility.ResolveAfterDeserialization(
            _editingModeWasSet,
            _editingMode);
        _pathInputMode = WipePathInputModeCompatibility.ResolveAfterDeserialization(
            _pathInputModeWasSet,
            _pathInputMode,
            _editingMode);
        _brushShape = GlassWipeParameterSanitizer.SanitizeBrushShape(
            _brushShape);
        if (_brushShape != GlassWipeBrushShape.UserImage)
        {
            _userBrushId = Guid.Empty;
            _userBrushRevision = 0;
            _userBrushPixelWidth = 0;
            _userBrushPixelHeight = 0;
        }
        else
        {
            _userBrushRevision = Math.Max(0, _userBrushRevision);
            _userBrushPixelWidth = Math.Clamp(
                _userBrushPixelWidth,
                0,
                UserBrushLibrary.MaximumDimension);
            _userBrushPixelHeight = Math.Clamp(
                _userBrushPixelHeight,
                0,
                UserBrushLibrary.MaximumDimension);
        }
        RefreshFogAmountValueSubscriptions();
        NotifyFogVisibilityChanged();
    }

    public override IEnumerable<string> CreateExoVideoFilters(
        int keyFrameIndex,
        ExoOutputDescription exoOutputDescription) => [];

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
    {
        ArgumentNullException.ThrowIfNull(devices);
        return new GlassWipeVideoEffectProcessor(devices, this);
    }

    protected override IEnumerable<IAnimatable> GetAnimatables() =>
    [
        SimpleProgress,
        FogAmount,
        Blur,
        RegionCenterX,
        RegionCenterY,
        RegionWidth,
        RegionHeight,
        RegionRotation,
        QuadTopLeftX,
        QuadTopLeftY,
        QuadTopRightX,
        QuadTopRightY,
        QuadBottomRightX,
        QuadBottomRightY,
        QuadBottomLeftX,
        QuadBottomLeftY,
        BrushX,
        BrushY,
        Contact,
        CircleDiameterPixels,
        EllipseWidthPixels,
        EllipseHeightPixels,
        RectangleWidthPixels,
        RectangleHeightPixels,
        PngBrushSizeScale,
        SimpleGeneratedBrushRotation,
        BrushRotation,
        WipeStrength,
    ];

    private void NotifySimpleVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsSimpleMode));
        OnPropertyChanged(nameof(IsDetailedMode));
        OnPropertyChanged(nameof(AreSimpleGeneratedPathSettingsVisible));
        OnPropertyChanged(nameof(IsSimpleInterpolationVisible));
        OnPropertyChanged(nameof(IsSimpleControlPointVisible));
        OnPropertyChanged(nameof(AreSimpleRoundTripSettingsVisible));
        OnPropertyChanged(nameof(IsSimpleReturnOffsetVisible));
        OnPropertyChanged(nameof(IsRegionTransformVisible));
        OnPropertyChanged(nameof(IsQuadConfigurationVisible));
        OnPropertyChanged(nameof(AreQuadPointsVisible));
        NotifyDetailedVisibilityChanged();
    }

    private void NotifyDetailedVisibilityChanged()
    {
        OnPropertyChanged(nameof(AreDetailedPathSettingsVisible));
        OnPropertyChanged(nameof(AreFogNoiseSettingsVisible));
        OnPropertyChanged(nameof(AreSimpleGeneratedPathSettingsVisible));
        OnPropertyChanged(nameof(IsSimpleInterpolationVisible));
        OnPropertyChanged(nameof(IsSimpleControlPointVisible));
        OnPropertyChanged(nameof(AreSimpleRoundTripSettingsVisible));
        OnPropertyChanged(nameof(IsSimpleReturnOffsetVisible));
        OnPropertyChanged(nameof(AreSimpleGeneratedBrushSettingsVisible));
        OnPropertyChanged(nameof(IsUserBrushVisible));
        OnPropertyChanged(nameof(IsContinuousWipeVisible));
        OnPropertyChanged(nameof(IsDetailedCustomPathVisible));
        OnPropertyChanged(nameof(IsDetailedLegacyPathVisible));
    }

    private void SubscribeToFogAmountChanges()
    {
        FogAmount.PropertyChanged += OnFogAmountPropertyChanged;
        RefreshFogAmountValueSubscriptions();
    }

    private void OnFogAmountPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (string.IsNullOrEmpty(eventArgs.PropertyName) ||
            eventArgs.PropertyName == nameof(Animation.Values))
        {
            RefreshFogAmountValueSubscriptions();
        }

        NotifyFogVisibilityChanged();
    }

    private void RefreshFogAmountValueSubscriptions()
    {
        foreach (var value in _observedFogAmountValues)
        {
            value.PropertyChanged -= OnFogAmountValuePropertyChanged;
        }

        _observedFogAmountValues.Clear();
        foreach (var value in FogAmount.Values)
        {
            value.PropertyChanged += OnFogAmountValuePropertyChanged;
            _observedFogAmountValues.Add(value);
        }
    }

    private void OnFogAmountValuePropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs) =>
        NotifyFogVisibilityChanged();

    private void NotifyFogVisibilityChanged()
    {
        OnPropertyChanged(nameof(AreFogSettingsVisible));
        OnPropertyChanged(nameof(AreFogNoiseSettingsVisible));
    }

    private void NotifyTimingVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsPercentTimingVisible));
        OnPropertyChanged(nameof(IsFrameTimingVisible));
        OnPropertyChanged(nameof(IsSecondsTimingVisible));
    }

    private static Animation CreateSimpleProgressAnimation()
    {
        var animation = new Animation(0, 0, 100)
        {
            AnimationType = AnimationType.直線移動,
        };

        // YMM4 4.55.1.1には開始値と終了値が異なるAnimationを生成する公開ファクトリがないため、
        // 初期化時だけ保存互換用セッターを使用します。通常の評価はGetValue()で行います。
#pragma warning disable CS0618
        animation.From = 0;
        animation.To = 100;
#pragma warning restore CS0618
        return animation;
    }
}
