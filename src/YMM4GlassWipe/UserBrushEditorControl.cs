// SPDX-License-Identifier: MPL-2.0

using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YMM4GlassWipe;

internal sealed class UserBrushEditorControl : UserControl, IPropertyEditorControl2
{
    public static readonly DependencyProperty EncodedStateProperty =
        DependencyProperty.Register(
            nameof(EncodedState),
            typeof(string),
            typeof(UserBrushEditorControl),
            new PropertyMetadata(string.Empty));

    private readonly UserBrushLibrary _library;
    private readonly ComboBox _brushSelector = new() { MinWidth = 180 };
    private readonly TextBox _nameEditor = new()
    {
        MinWidth = 140,
        MaxLength = UserBrushLibrary.MaximumNameLength,
    };
    private readonly TextBlock _status = new()
    {
        Margin = new Thickness(0, 4, 0, 0),
        TextWrapping = TextWrapping.Wrap,
    };
    private readonly Button _useButton;
    private readonly Button _replaceButton;
    private readonly Button _deleteButton;
    private string? _loadError;

    public UserBrushEditorControl()
        : this(new UserBrushLibrary())
    {
    }

    internal UserBrushEditorControl(UserBrushLibrary library)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        var root = new StackPanel();
        var selectRow = new WrapPanel();
        selectRow.Children.Add(_brushSelector);
        _useButton = CreateButton("使用", (_, _) => ApplySelectedBrush());
        _replaceButton = CreateButton("置換", (_, _) => ReplaceSelectedBrush());
        _deleteButton = CreateButton("削除", (_, _) => DeleteSelectedBrush());
        selectRow.Children.Add(_useButton);
        selectRow.Children.Add(_replaceButton);
        selectRow.Children.Add(_deleteButton);
        root.Children.Add(selectRow);

        var registerRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        registerRow.Children.Add(new TextBlock
        {
            Text = "登録名",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        });
        registerRow.Children.Add(_nameEditor);
        registerRow.Children.Add(CreateButton("PNGを追加", (_, _) => RegisterBrush()));
        root.Children.Add(registerRow);
        root.Children.Add(new TextBlock
        {
            Text = "登録名は一覧に表示する名前です。入力してから「PNGを追加」で画像を選んでください。",
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });
        root.Children.Add(_status);
        Content = root;
        Focusable = true;

        Loaded += (_, _) => RefreshOptions(GetCurrentBrushId());
        _brushSelector.DropDownOpened += (_, _) => RefreshOptions(GetSelectedBrushId());
        _brushSelector.SelectionChanged += (_, _) => UpdateButtonsAndStatus();
        RefreshOptions();
    }

    public event EventHandler? BeginEdit;

    public event EventHandler? EndEdit;

    public string EncodedState
    {
        get => (string)GetValue(EncodedStateProperty);
        set => SetValue(EncodedStateProperty, value);
    }

    public void SetEditorInfo(IEditorInfo? info) => _ = info;

    public void SetFocus()
    {
        if (!Focus())
        {
            Keyboard.Focus(this);
        }
    }

    private Guid GetCurrentBrushId()
    {
        GetBindingExpression(EncodedStateProperty)?.UpdateTarget();
        return UserBrushExchangeCodec.TryDecode(EncodedState, out var state)
            ? state.UserBrushId
            : Guid.Empty;
    }

    private Guid GetSelectedBrushId() =>
        _brushSelector.SelectedItem is BrushOption option
            ? option.Id
            : Guid.Empty;

    private void RefreshOptions(Guid selectedId = default)
    {
        var entries = _library.Load(out var error);
        _loadError = error;
        var options = entries.Select(BrushOption.Available).ToList();
        if (selectedId != Guid.Empty && options.All(option => option.Id != selectedId))
        {
            options.Add(BrushOption.Missing(selectedId));
        }

        _brushSelector.ItemsSource = options;
        _brushSelector.DisplayMemberPath = nameof(BrushOption.DisplayName);
        _brushSelector.SelectedItem = selectedId == Guid.Empty
            ? options.FirstOrDefault()
            : options.FirstOrDefault(option => option.Id == selectedId) ??
              options.FirstOrDefault();
        UpdateButtonsAndStatus();
    }

    private void ApplySelectedBrush()
    {
        if (_brushSelector.SelectedItem is not BrushOption { Entry: { } entry })
        {
            SetStatus("使用するユーザーブラシを選択してください。", isError: true);
            return;
        }

        if (!_library.TryLoadMask(entry.Id, out _, out var current, out var error))
        {
            SetStatus(error, isError: true);
            return;
        }

        ApplyEntry(current);
        SetStatus($"「{current.Name}」を使用します。", isError: false);
    }

    private void RegisterBrush()
    {
        var name = _nameEditor.Text.Trim();
        if (!UserBrushLibrary.TryValidateName(name, out name, out var nameError))
        {
            SetStatus(nameError, isError: true);
            return;
        }

        var existing = _library.Load(out var loadError)
            .FirstOrDefault(entry => string.Equals(
                entry.Name,
                name,
                StringComparison.OrdinalIgnoreCase));
        if (loadError is not null)
        {
            SetStatus(loadError, isError: true);
            return;
        }

        if (existing is not null &&
            !Confirm($"同名のユーザーブラシ「{existing.Name}」を置き換えますか？"))
        {
            return;
        }

        var sourcePath = SelectPngFile();
        if (sourcePath is null)
        {
            return;
        }

        if (!_library.Register(
                name,
                sourcePath,
                out var saved,
                out var replaced,
                out var error))
        {
            SetStatus(error ?? "ユーザーブラシを登録できませんでした。", isError: true);
            return;
        }

        RefreshOptions(saved.Id);
        ApplyEntry(saved);
        SetStatus(
            replaced
                ? $"「{saved.Name}」を置き換えました。"
                : $"「{saved.Name}」を登録しました。",
            isError: false);
    }

    private void ReplaceSelectedBrush()
    {
        if (_brushSelector.SelectedItem is not BrushOption { Entry: { } entry })
        {
            return;
        }

        if (!Confirm($"ユーザーブラシ「{entry.Name}」の画像を置き換えますか？"))
        {
            return;
        }

        var sourcePath = SelectPngFile();
        if (sourcePath is null)
        {
            return;
        }

        if (!_library.Replace(entry.Id, sourcePath, out var saved, out var error))
        {
            SetStatus(error ?? "ユーザーブラシを置き換えられませんでした。", isError: true);
            return;
        }

        RefreshOptions(saved.Id);
        ApplyEntry(saved);
        SetStatus($"「{saved.Name}」を置き換えました。", isError: false);
    }

    private void DeleteSelectedBrush()
    {
        if (_brushSelector.SelectedItem is not BrushOption { Entry: { } entry } ||
            !Confirm($"ユーザーブラシ「{entry.Name}」を完全に削除しますか？"))
        {
            return;
        }

        var currentId = GetCurrentBrushId();
        if (!_library.Delete(entry.Id, out var error))
        {
            SetStatus(error ?? "ユーザーブラシを削除できませんでした。", isError: true);
            return;
        }

        RefreshOptions(currentId);
        SetStatus(
            currentId == entry.Id
                ? $"「{entry.Name}」を削除しました。現在の参照は保持されています。画像を再登録してください。"
                : $"「{entry.Name}」を削除しました。",
            isError: currentId == entry.Id);
    }

    private void ApplyEntry(UserBrushEntry entry)
    {
        BeginEdit?.Invoke(this, EventArgs.Empty);
        try
        {
            SetCurrentValue(
                EncodedStateProperty,
                UserBrushExchangeCodec.EncodeApply(
                    entry.Id,
                    entry.Revision,
                    entry.PixelWidth,
                    entry.PixelHeight));
        }
        finally
        {
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateButtonsAndStatus()
    {
        var option = _brushSelector.SelectedItem as BrushOption;
        var available = option?.Entry is not null;
        _useButton.IsEnabled = available;
        _replaceButton.IsEnabled = available;
        _deleteButton.IsEnabled = available;
        if (_loadError is not null)
        {
            SetStatus(_loadError, isError: true);
            return;
        }

        if (option is null)
        {
            SetStatus(string.Empty, isError: false);
            return;
        }

        if (option.Entry is null)
        {
            SetStatus(
                "参照中のユーザーブラシがありません。PNGを登録し、一覧から新しいブラシを選んで「使用」を押してください。",
                isError: true);
            return;
        }

        if (!_library.TryLoadMask(option.Id, out _, out _, out var error))
        {
            SetStatus(error, isError: true);
            return;
        }

        SetStatus(string.Empty, isError: false);
    }

    private void SetStatus(string? message, bool isError)
    {
        _status.Text = message ?? string.Empty;
        _status.Foreground = isError
            ? System.Windows.Media.Brushes.IndianRed
            : System.Windows.Media.Brushes.Gray;
    }

    private static string? SelectPngFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "ユーザーブラシPNGを選択",
            Filter = "PNG画像 (*.png)|*.png",
            CheckFileExists = true,
            Multiselect = false,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static bool Confirm(string message) =>
        MessageBox.Show(
            message,
            GlassWipeVideoEffect.DisplayName,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

    private static Button CreateButton(string content, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = content,
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(5, 1, 5, 1),
            MinWidth = 48,
        };
        button.Click += handler;
        return button;
    }

    private sealed class BrushOption
    {
        public Guid Id { get; private init; }

        public string DisplayName { get; private init; } = string.Empty;

        public UserBrushEntry? Entry { get; private init; }

        public static BrushOption Available(UserBrushEntry entry) =>
            new()
            {
                Id = entry.Id,
                DisplayName = entry.Name,
                Entry = entry,
            };

        public static BrushOption Missing(Guid id) =>
            new()
            {
                Id = id,
                DisplayName = $"利用不可: {id}",
            };
    }
}
