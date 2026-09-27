// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using MotionMode = YMM4GlassWipe.OutsideDropletMotionMode;

namespace YMM4GlassWipe;

internal sealed partial class GlassWipeVideoEffect
{
    // 互換補完値。新規追加の値はコンストラクターでテンプレート既定値へ切り替える。
    private MotionMode _outsideDropletMotionMode = OutsideDropletSettings.DefaultMotionMode;
    private double _outsideDropletSlip = OutsideDropletSettings.DefaultSlipPercent;
    private double _outsideDropletSupply = OutsideDropletSettings.DefaultSupplyPercent;

    [Display(GroupName = "水滴", Name = "水滴の動き", Description = "周期動作は設定した待ち時間や周期に沿って水滴が落下します。簡易物理は水滴の大きさや滑りやすさに応じて動き、接触すると合体します。水滴を垂らす設定と合体のON/OFFに従います。切り替えても他の設定値は維持します。")]
    [EnumComboBox]
    [ShowPropertyEditorWhen(nameof(AreOutsideDropletSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.OutsideDropletMotionMode)]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public MotionMode OutsideDropletMotionMode
    {
        get => _outsideDropletMotionMode;
        set
        {
            if (Set(ref _outsideDropletMotionMode, OutsideDropletSettings.SanitizeMotionMode(value)))
            {
                NotifyOutsideDropletPhysicsVisibilityChanged();
                OnPropertyChanged(nameof(DetailedPresetExchange));
            }
        }
    }

    [Display(GroupName = "水滴", Name = "滑りやすさ", Description = "簡易物理の水滴の滑りやすさです。大きくすると小さい滴も滑り始めやすくなります。")]
    [TextBoxSlider("F1", "%", OutsideDropletSettings.MinimumSlipPercent, OutsideDropletSettings.MaximumSlipPercent)]
    [ShowPropertyEditorWhen(nameof(AreOutsideDropletPhysicsSettingsVisible), true)]
    [DefaultValue(OutsideDropletSettings.DefaultSlipPercent)]
    [Range(OutsideDropletSettings.MinimumSlipPercent, OutsideDropletSettings.MaximumSlipPercent)]
    public double OutsideDropletSlip
    {
        get => _outsideDropletSlip;
        set
        {
            if (Set(ref _outsideDropletSlip, OutsideDropletSettings.SanitizeSlipPercent(value)))
                OnPropertyChanged(nameof(DetailedPresetExchange));
        }
    }

    [Display(GroupName = "水滴", Name = "雨の補給", Description = "簡易物理で追加する雨の量です。0%で補給を止めます。水滴を垂らす設定とは独立し、粒子上限では追加を見送ります。")]
    [TextBoxSlider("F1", "%", OutsideDropletSettings.MinimumSupplyPercent, OutsideDropletSettings.MaximumSupplyPercent)]
    [ShowPropertyEditorWhen(nameof(AreOutsideDropletPhysicsSettingsVisible), true)]
    [DefaultValue(GlassWipeDefaultSettings.OutsideDropletSupplyPercent)]
    [Range(OutsideDropletSettings.MinimumSupplyPercent, OutsideDropletSettings.MaximumSupplyPercent)]
    public double OutsideDropletSupply
    {
        get => _outsideDropletSupply;
        set
        {
            if (Set(ref _outsideDropletSupply, OutsideDropletSettings.SanitizeSupplyPercent(value)))
                OnPropertyChanged(nameof(DetailedPresetExchange));
        }
    }

    [JsonIgnore]
    public bool AreOutsideDropletPhysicsSettingsVisible =>
        AreOutsideDropletSettingsVisible && OutsideDropletMotionMode == MotionMode.SimplePhysics;

    [JsonIgnore]
    public bool AreOutsideDropletLegacyFallSettingsVisible =>
        AreOutsideDropletFallSettingsVisible && OutsideDropletMotionMode == MotionMode.Legacy;

    [JsonIgnore]
    public bool IsOutsideDropletMergeVisible =>
        AreOutsideDropletFallSettingsVisible || AreOutsideDropletPhysicsSettingsVisible;

    private void NotifyOutsideDropletPhysicsVisibilityChanged()
    {
        OnPropertyChanged(nameof(AreOutsideDropletPhysicsSettingsVisible));
        OnPropertyChanged(nameof(AreOutsideDropletLegacyFallSettingsVisible));
        OnPropertyChanged(nameof(IsOutsideDropletMergeVisible));
        OnPropertyChanged(nameof(IsOutsideDropletDeformWithSurfaceVisible));
    }
}
