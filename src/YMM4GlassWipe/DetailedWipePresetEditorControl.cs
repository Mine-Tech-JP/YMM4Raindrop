// SPDX-License-Identifier: MPL-2.0

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YMM4GlassWipe;

internal sealed class DetailedWipePresetEditorControl : UserControl, IPropertyEditorControl2
{
    public static readonly DependencyProperty EncodedStateProperty =
        DependencyProperty.Register(
            nameof(EncodedState),
            typeof(string),
            typeof(DetailedWipePresetEditorControl),
            new PropertyMetadata(string.Empty));

    private readonly DetailedWipeUserPresetStore _store = new();
    private readonly ComboBox _scopeSelector = new()
    {
        MinWidth = 88,
        ToolTip = DetailedWipePresetTooltipText.Description,
    };
    private readonly ComboBox _presetSelector = new()
    {
        MinWidth = 180,
        ToolTip = DetailedWipePresetTooltipText.Description,
    };
    private readonly TextBox _nameEditor = new()
    {
        MinWidth = 140,
        MaxLength = DetailedWipeUserPresetStore.MaximumNameLength,
    };
    private readonly TextBlock _status = new()
    {
        Margin = new Thickness(0, 4, 0, 0),
        TextWrapping = TextWrapping.Wrap,
    };
    private readonly Button _deleteButton;
    private readonly Button _emptyButton;

    public DetailedWipePresetEditorControl()
    {
        var root = new StackPanel
        {
            ToolTip = DetailedWipePresetTooltipText.Description,
        };
        var scopeRow = new WrapPanel();
        scopeRow.Children.Add(new TextBlock
        {
            Text = "種類",
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        scopeRow.Children.Add(_scopeSelector);
        root.Children.Add(scopeRow);

        var applyRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        applyRow.Children.Add(_presetSelector);
        applyRow.Children.Add(CreateButton(
            "適用",
            (_, _) => ApplySelectedPreset(),
            DetailedWipePresetTooltipText.Description));
        _deleteButton = CreateButton("削除", (_, _) => DeleteSelectedPreset());
        applyRow.Children.Add(_deleteButton);
        _emptyButton = CreateButton("空から作成", (_, _) => ApplyEmptyPreset());
        applyRow.Children.Add(_emptyButton);
        root.Children.Add(applyRow);

        var registerRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        registerRow.Children.Add(_nameEditor);
        registerRow.Children.Add(CreateButton(
            "現在の設定を登録",
            (_, _) => RegisterCurrentState(),
            DetailedWipePresetTooltipText.Description));
        root.Children.Add(registerRow);
        root.Children.Add(_status);
        Content = root;
        Focusable = true;

        _scopeSelector.ItemsSource = new[]
        {
            ScopeOption.Create(DetailedWipePresetScope.Path),
            ScopeOption.Create(DetailedWipePresetScope.Brush),
            ScopeOption.Create(DetailedWipePresetScope.All),
        };
        _scopeSelector.DisplayMemberPath = nameof(ScopeOption.DisplayName);
        _scopeSelector.SelectedIndex = 0;
        Loaded += (_, _) => RefreshPresetOptions(GetSelectedUserPresetId());
        _scopeSelector.SelectionChanged += (_, _) => RefreshPresetOptions();
        _presetSelector.DropDownOpened +=
            (_, _) => RefreshPresetOptions(GetSelectedUserPresetId());
        _presetSelector.SelectionChanged += (_, _) => UpdateButtons();
        RefreshPresetOptions();
    }

    public event EventHandler? BeginEdit;

    public event EventHandler? EndEdit;

    public string EncodedState
    {
        get => (string)GetValue(EncodedStateProperty);
        set => SetValue(EncodedStateProperty, value);
    }

    private DetailedWipePresetScope SelectedScope =>
        _scopeSelector.SelectedItem is ScopeOption option
            ? option.Scope
            : DetailedWipePresetScope.Path;

    private Guid? GetSelectedUserPresetId() =>
        _presetSelector.SelectedItem is PresetOption { UserPreset: { } preset }
            ? preset.Id
            : null;

    public void SetEditorInfo(IEditorInfo? info) => _ = info;

    public void SetFocus()
    {
        if (!Focus())
        {
            Keyboard.Focus(this);
        }
    }

    private void RefreshPresetOptions(Guid? selectedUserPresetId = null)
    {
        var scope = SelectedScope;
        var options = new List<PresetOption>();
        if (scope == DetailedWipePresetScope.Path)
        {
            options.Add(PresetOption.BuiltIn(DetailedWipeBuiltInPreset.Heart));
            options.Add(PresetOption.BuiltIn(DetailedWipeBuiltInPreset.LoveUmbrella));
            options.Add(PresetOption.BuiltIn(DetailedWipeBuiltInPreset.Smiley));
        }

        var userPresets = _store.Load(out var error);
        var matchingPresets = userPresets
            .Where(preset => preset.Scope == scope)
            .ToArray();
        options.AddRange(matchingPresets.Select(PresetOption.User));
        _presetSelector.ItemsSource = options;
        _presetSelector.DisplayMemberPath = nameof(PresetOption.DisplayName);
        _presetSelector.SelectedItem = selectedUserPresetId is null
            ? options.FirstOrDefault()
            : options.FirstOrDefault(option => option.UserPreset?.Id == selectedUserPresetId) ??
              options.FirstOrDefault();
        SetStatus(error, isError: error is not null);
        UpdateButtons();
    }

    private void ApplySelectedPreset()
    {
        if (_presetSelector.SelectedItem is not PresetOption option)
        {
            return;
        }

        var state = option.BuiltInPreset is { } builtIn
            ? DetailedWipePresetFactory.Create(builtIn)
            : option.UserPreset?.State;
        if (state is null || !state.TrySanitize(option.Scope, out var sanitized))
        {
            SetStatus("選択したプリセットが不正です。", isError: true);
            return;
        }

        var scopeName = DetailedWipePresetScopePolicy.GetDisplayName(option.Scope);
        if (!ConfirmOverwrite(
                $"「{option.DisplayName}」の{scopeName}だけを現在の設定へ適用しますか？"))
        {
            return;
        }

        ApplyState(option.Scope, sanitized);
        SetStatus($"「{option.DisplayName}」の{scopeName}を適用しました。", isError: false);
    }

    private void ApplyEmptyPreset()
    {
        if (SelectedScope != DetailedWipePresetScope.Path ||
            !ConfirmOverwrite("現在のブラシを保ったまま、空のカスタム軌跡を作成しますか？"))
        {
            return;
        }

        var state = new DetailedWipePresetState
        {
            CustomPathData = WipeStrokeDocumentCodec.Encode(WipeStrokeDocument.CreateEmpty()),
        };
        ApplyState(DetailedWipePresetScope.Path, state);
        SetStatus("ブラシを保ったまま空のカスタム軌跡を作成しました。", isError: false);
    }

    private void RegisterCurrentState()
    {
        GetBindingExpression(EncodedStateProperty)?.UpdateTarget();
        if (!DetailedWipePresetExchangeCodec.TryDecode(EncodedState, out var exchange) ||
            exchange.ApplyScope is not null)
        {
            SetStatus("現在の設定を取得できませんでした。", isError: true);
            return;
        }

        var scope = SelectedScope;
        if (DetailedWipePresetScopePolicy.IncludesPath(scope) && !exchange.CanSavePath)
        {
            SetStatus(
                scope == DetailedWipePresetScope.Path
                    ? "軌跡を登録するには、軌跡の種類を「カスタム軌跡」にしてください。"
                    : "全設定を登録するには、軌跡の種類を「カスタム軌跡」にしてください。",
                isError: true);
            return;
        }

        if (!exchange.State.TrySanitize(scope, out var state))
        {
            SetStatus("現在の設定を登録できません。設定値を確認してください。", isError: true);
            return;
        }

        var name = _nameEditor.Text.Trim();
        var existing = _store.Load(out var loadError)
            .FirstOrDefault(preset =>
                preset.Scope == scope &&
                string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));
        if (loadError is not null)
        {
            SetStatus(loadError, isError: true);
            return;
        }

        var scopeName = DetailedWipePresetScopePolicy.GetDisplayName(scope);
        if (existing is not null &&
            !ConfirmOverwrite(
                $"同名の{scopeName}プリセット「{existing.Name}」を上書きしますか？"))
        {
            return;
        }

        if (!_store.Upsert(name, scope, state, out var replaced, out var error))
        {
            SetStatus(error ?? "ユーザープリセットを登録できませんでした。", isError: true);
            return;
        }

        var saved = _store.Load(out _)
            .FirstOrDefault(preset =>
                preset.Scope == scope &&
                string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));
        RefreshPresetOptions(saved?.Id);
        SetStatus(
            replaced
                ? $"{scopeName}「{name}」を上書きしました。"
                : $"{scopeName}「{name}」を登録しました。",
            isError: false);
    }

    private void DeleteSelectedPreset()
    {
        if (_presetSelector.SelectedItem is not PresetOption { UserPreset: { } preset })
        {
            return;
        }

        var scopeName = DetailedWipePresetScopePolicy.GetDisplayName(preset.Scope);
        if (!ConfirmOverwrite($"{scopeName}プリセット「{preset.Name}」を削除しますか？"))
        {
            return;
        }

        if (!_store.Delete(preset.Id, out var error))
        {
            SetStatus(error ?? "ユーザープリセットを削除できませんでした。", isError: true);
            return;
        }

        RefreshPresetOptions();
        SetStatus($"{scopeName}「{preset.Name}」を削除しました。", isError: false);
    }

    private void ApplyState(DetailedWipePresetScope scope, DetailedWipePresetState state)
    {
        BeginEdit?.Invoke(this, EventArgs.Empty);
        try
        {
            SetCurrentValue(
                EncodedStateProperty,
                DetailedWipePresetExchangeCodec.EncodeApply(scope, state));
        }
        finally
        {
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateButtons()
    {
        _deleteButton.IsEnabled =
            _presetSelector.SelectedItem is PresetOption { UserPreset: not null };
        _emptyButton.IsEnabled = SelectedScope == DetailedWipePresetScope.Path;
    }

    private void SetStatus(string? message, bool isError)
    {
        _status.Text = message ?? string.Empty;
        _status.Foreground = isError
            ? System.Windows.Media.Brushes.IndianRed
            : System.Windows.Media.Brushes.Gray;
    }

    private static bool ConfirmOverwrite(string message) =>
        MessageBox.Show(
            message,
            GlassWipeVideoEffect.DisplayName,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

    private static Button CreateButton(
        string content,
        RoutedEventHandler handler,
        string? toolTip = null)
    {
        var button = new Button
        {
            Content = content,
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(5, 1, 5, 1),
            MinWidth = 48,
            ToolTip = toolTip,
        };
        button.Click += handler;
        return button;
    }

    private sealed class ScopeOption
    {
        public DetailedWipePresetScope Scope { get; private init; }

        public string DisplayName { get; private init; } = string.Empty;

        public static ScopeOption Create(DetailedWipePresetScope scope) =>
            new()
            {
                Scope = scope,
                DisplayName = DetailedWipePresetScopePolicy.GetDisplayName(scope),
            };
    }

    private sealed class PresetOption
    {
        public string DisplayName { get; private init; } = string.Empty;

        public DetailedWipePresetScope Scope { get; private init; }

        public DetailedWipeBuiltInPreset? BuiltInPreset { get; private init; }

        public DetailedWipeUserPreset? UserPreset { get; private init; }

        public static PresetOption BuiltIn(DetailedWipeBuiltInPreset preset) =>
            new()
            {
                DisplayName = $"内蔵: {DetailedWipePresetFactory.GetDisplayName(preset)}",
                Scope = DetailedWipePresetScope.Path,
                BuiltInPreset = preset,
            };

        public static PresetOption User(DetailedWipeUserPreset preset) =>
            new()
            {
                DisplayName = $"ユーザー: {preset.Name}",
                Scope = preset.Scope,
                UserPreset = preset,
            };
    }
}
