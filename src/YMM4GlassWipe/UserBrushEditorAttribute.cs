// SPDX-License-Identifier: MPL-2.0

using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Views.Converters;

namespace YMM4GlassWipe;

internal sealed class UserBrushEditorAttribute : PropertyEditorAttribute2
{
    public override FrameworkElement Create() => new UserBrushEditorControl();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(itemProperties);

        var editor = (UserBrushEditorControl)control;
        var binding = ItemPropertiesBinding.Create2(
            itemProperties,
            BindingMode.TwoWay,
            UpdateSourceTrigger.PropertyChanged,
            IsMultiEditing,
            delay: 0);
        editor.SetBinding(UserBrushEditorControl.EncodedStateProperty, binding);
    }

    public override void ClearBindings(FrameworkElement control)
    {
        ArgumentNullException.ThrowIfNull(control);
        BindingOperations.ClearBinding(
            control,
            UserBrushEditorControl.EncodedStateProperty);
    }
}
