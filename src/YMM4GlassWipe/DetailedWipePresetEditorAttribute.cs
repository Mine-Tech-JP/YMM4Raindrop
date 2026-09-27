// SPDX-License-Identifier: MPL-2.0

using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Views.Converters;

namespace YMM4GlassWipe;

internal sealed class DetailedWipePresetEditorAttribute : PropertyEditorAttribute2
{
    public override FrameworkElement Create() => new DetailedWipePresetEditorControl();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(itemProperties);

        var editor = (DetailedWipePresetEditorControl)control;
        var binding = ItemPropertiesBinding.Create2(
            itemProperties,
            BindingMode.TwoWay,
            UpdateSourceTrigger.PropertyChanged,
            IsMultiEditing,
            delay: 0);
        editor.SetBinding(DetailedWipePresetEditorControl.EncodedStateProperty, binding);
    }

    public override void ClearBindings(FrameworkElement control)
    {
        ArgumentNullException.ThrowIfNull(control);
        BindingOperations.ClearBinding(
            control,
            DetailedWipePresetEditorControl.EncodedStateProperty);
    }
}
