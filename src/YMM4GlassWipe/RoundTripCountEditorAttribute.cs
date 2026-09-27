// SPDX-License-Identifier: MPL-2.0

using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Views.Converters;

namespace YMM4GlassWipe;

/// <summary>
/// 往復回数を0.5回単位で編集するスライダーです。
/// </summary>
internal sealed class RoundTripCountEditorAttribute : PropertyEditorAttribute2
{
    public override FrameworkElement Create() => new TextBoxSlider
    {
        StringFormat = "F1",
        Unit = "回",
        // 表示範囲とは別に実入力範囲を指定し、YMM4部品の既定上限1を解除します。
        Min = WipeSimplePathGenerator.MinimumRoundTrips,
        Max = WipeSimplePathGenerator.MaximumRoundTrips,
        DefaultMin = WipeSimplePathGenerator.MinimumRoundTrips,
        DefaultMax = WipeSimplePathGenerator.MaximumRoundTrips,
        DefaultValue = GlassWipeDefaultSettings.SimpleRoundTrips,
        Delta = WipeSimplePathGenerator.RoundTripIncrement,
    };

    public override void SetBindings(
        FrameworkElement control,
        ItemProperty[] itemProperties)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(itemProperties);

        var slider = (TextBoxSlider)control;
        slider.SetBinding(
            TextBoxSlider.ValueProperty,
            ItemPropertiesBinding.Create2(itemProperties));
    }

    public override void ClearBindings(FrameworkElement control)
    {
        ArgumentNullException.ThrowIfNull(control);
        BindingOperations.ClearBinding(control, TextBoxSlider.ValueProperty);
    }
}
