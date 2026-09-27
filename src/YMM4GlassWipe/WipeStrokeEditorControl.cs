// SPDX-License-Identifier: MPL-2.0

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace YMM4GlassWipe;

internal sealed class WipeStrokeEditorControl : UserControl, IPropertyEditorControl2
{
    public static readonly DependencyProperty EncodedDocumentProperty =
        DependencyProperty.Register(
            nameof(EncodedDocument),
            typeof(string),
            typeof(WipeStrokeEditorControl),
            new PropertyMetadata(string.Empty, OnEncodedDocumentChanged));

    private readonly StackPanel _content = new();
    private WipeStrokeDocument _document = WipeStrokeDocument.CreateStarter();
    private bool _isRebuilding;
    private bool _suppressDocumentReload;

    public WipeStrokeEditorControl()
    {
        var scrollViewer = new ScrollViewer
        {
            Content = _content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 360,
        };

        Content = scrollViewer;
        Focusable = true;
        Rebuild();
    }

    public event EventHandler? BeginEdit;

    public event EventHandler? EndEdit;

    public string EncodedDocument
    {
        get => (string)GetValue(EncodedDocumentProperty);
        set => SetValue(EncodedDocumentProperty, value);
    }

    public void SetEditorInfo(IEditorInfo? info) => _ = info;

    public void SetFocus()
    {
        if (!Focus())
        {
            Keyboard.Focus(this);
        }
    }

    private static void OnEncodedDocumentChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        var editor = (WipeStrokeEditorControl)dependencyObject;
        if (editor._suppressDocumentReload)
        {
            return;
        }

        editor.LoadDocument(eventArgs.NewValue as string);
    }

    private void LoadDocument(string? encoded)
    {
        _document = WipeStrokeDocumentCodec.TryDecode(encoded, out var document)
            ? document
            : WipeStrokeDocument.CreateStarter();
        Rebuild();
    }

    private void Rebuild()
    {
        _isRebuilding = true;
        try
        {
            _content.Children.Clear();

            if (!WipeStrokeDocumentCodec.TryDecode(EncodedDocument, out _))
            {
                _content.Children.Add(new TextBlock
                {
                    Foreground = Brushes.DarkGoldenrod,
                    Margin = new Thickness(0, 0, 0, 6),
                    Text = "保存値が未設定または不正です。編集を開始するとスターター軌跡を保存します。",
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            var addStroke = CreateButton("ストロークを追加", (_, _) =>
            {
                if (_document.Strokes.Count >= WipeStrokeDocument.MaximumStrokeCount)
                {
                    return;
                }

                ApplyEdit(() => _document.Strokes.Add(CreateStarterStroke()));
            });
            addStroke.IsEnabled = _document.Strokes.Count < WipeStrokeDocument.MaximumStrokeCount;
            _content.Children.Add(addStroke);

            for (var strokeIndex = 0; strokeIndex < _document.Strokes.Count; strokeIndex++)
            {
                _content.Children.Add(CreateStrokePanel(strokeIndex));
            }
        }
        finally
        {
            _isRebuilding = false;
        }
    }

    private FrameworkElement CreateStrokePanel(int strokeIndex)
    {
        var stroke = _document.Strokes[strokeIndex];
        var panel = new StackPanel
        {
            Margin = new Thickness(0, 8, 0, 0),
        };
        panel.Children.Add(new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            Text = $"ストローク {strokeIndex + 1}",
        });

        var commands = new WrapPanel { Margin = new Thickness(0, 3, 0, 3) };
        var moveUp = CreateButton("上へ", (_, _) => MoveStroke(strokeIndex, -1));
        moveUp.IsEnabled = strokeIndex > 0;
        commands.Children.Add(moveUp);
        var moveDown = CreateButton("下へ", (_, _) => MoveStroke(strokeIndex, 1));
        moveDown.IsEnabled = strokeIndex + 1 < _document.Strokes.Count;
        commands.Children.Add(moveDown);
        commands.Children.Add(CreateButton("削除", (_, _) =>
            ApplyEdit(() => _document.Strokes.RemoveAt(strokeIndex))));

        var addPoint = CreateButton("制御点を追加", (_, _) => AddPoint(strokeIndex));
        addPoint.IsEnabled = stroke.Points.Count < WipeStrokeDocument.MaximumPointCountPerStroke;
        commands.Children.Add(addPoint);
        panel.Children.Add(commands);

        for (var pointIndex = 0; pointIndex < stroke.Points.Count; pointIndex++)
        {
            panel.Children.Add(CreatePointPanel(strokeIndex, pointIndex));
        }

        return new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Child = panel,
            Padding = new Thickness(6),
        };
    }

    private FrameworkElement CreatePointPanel(int strokeIndex, int pointIndex)
    {
        var point = _document.Strokes[strokeIndex].Points[pointIndex];
        var panel = new StackPanel { Margin = new Thickness(8, 3, 0, 3) };
        panel.Children.Add(new TextBlock { Text = $"制御点 {pointIndex + 1}" });

        var values = new WrapPanel();
        values.Children.Add(CreateNumberEditor(
            "時刻%",
            point.TimelinePercent,
            0,
            100,
            integerOnly: false,
            applyValue: value => UpdatePoint(
                strokeIndex,
                pointIndex,
                current => current with { TimelinePercent = value })));
        values.Children.Add(CreateNumberEditor(
            "X px",
            point.X,
            0,
            WipeStrokeDocument.CanvasPixelWidth,
            integerOnly: true,
            applyValue: value => UpdatePoint(
                strokeIndex,
                pointIndex,
                current => current with { X = value })));
        values.Children.Add(CreateNumberEditor(
            "Y px",
            point.Y,
            0,
            WipeStrokeDocument.CanvasPixelHeight,
            integerOnly: true,
            applyValue: value => UpdatePoint(
                strokeIndex,
                pointIndex,
                current => current with { Y = value })));
        values.Children.Add(CreateNumberEditor(
            "接触%",
            point.Contact,
            0,
            100,
            integerOnly: false,
            applyValue: value => UpdatePoint(
                strokeIndex,
                pointIndex,
                current => current with { Contact = value })));
        panel.Children.Add(values);

        var commands = new WrapPanel();
        var moveUp = CreateButton("上へ", (_, _) => MovePoint(strokeIndex, pointIndex, -1));
        moveUp.IsEnabled = pointIndex > 0;
        commands.Children.Add(moveUp);
        var moveDown = CreateButton("下へ", (_, _) => MovePoint(strokeIndex, pointIndex, 1));
        moveDown.IsEnabled = pointIndex + 1 < _document.Strokes[strokeIndex].Points.Count;
        commands.Children.Add(moveDown);
        commands.Children.Add(CreateButton("削除", (_, _) =>
            ApplyEdit(() => _document.Strokes[strokeIndex].Points.RemoveAt(pointIndex))));
        panel.Children.Add(commands);

        return panel;
    }

    private FrameworkElement CreateNumberEditor(
        string label,
        double value,
        double minimum,
        double maximum,
        bool integerOnly,
        Action<double> applyValue)
    {
        var committedValue = NormalizeValue(value, minimum, maximum, integerOnly);
        var panel = new StackPanel
        {
            Margin = new Thickness(0, 0, 6, 0),
            Width = 76,
        };
        panel.Children.Add(new TextBlock { Text = label });

        var textBox = new TextBox
        {
            Text = FormatValue(committedValue, integerOnly),
        };
        textBox.LostKeyboardFocus += (_, _) =>
        {
            if (_isRebuilding)
            {
                return;
            }

            if (!TryParseFiniteDouble(textBox.Text, out var editedValue))
            {
                textBox.Text = FormatValue(committedValue, integerOnly);
                return;
            }

            editedValue = NormalizeValue(editedValue, minimum, maximum, integerOnly);
            textBox.Text = FormatValue(editedValue, integerOnly);
            if (editedValue.Equals(committedValue))
            {
                return;
            }

            applyValue(editedValue);
            committedValue = editedValue;
        };
        panel.Children.Add(textBox);
        return panel;
    }

    private void AddPoint(int strokeIndex)
    {
        var points = _document.Strokes[strokeIndex].Points;
        if (points.Count >= WipeStrokeDocument.MaximumPointCountPerStroke)
        {
            return;
        }

        ApplyEdit(() =>
        {
            var point = points.Count == 0
                ? new WipeStrokePoint(
                    0,
                    WipeStrokeDocument.CanvasPixelWidth / 2,
                    WipeStrokeDocument.CanvasPixelHeight / 2,
                    100)
                : points[^1] with
                {
                    TimelinePercent = Math.Min(100, points[^1].TimelinePercent + 10),
                };
            points.Add(point);
        });
    }

    private void MoveStroke(int strokeIndex, int offset)
    {
        var destination = strokeIndex + offset;
        if (destination < 0 || destination >= _document.Strokes.Count)
        {
            return;
        }

        ApplyEdit(() => (_document.Strokes[strokeIndex], _document.Strokes[destination]) =
            (_document.Strokes[destination], _document.Strokes[strokeIndex]));
    }

    private void MovePoint(int strokeIndex, int pointIndex, int offset)
    {
        var points = _document.Strokes[strokeIndex].Points;
        var destination = pointIndex + offset;
        if (destination < 0 || destination >= points.Count)
        {
            return;
        }

        ApplyEdit(() => (points[pointIndex], points[destination]) =
            (points[destination], points[pointIndex]));
    }

    private void UpdatePoint(
        int strokeIndex,
        int pointIndex,
        Func<WipeStrokePoint, WipeStrokePoint> update)
    {
        ApplyEdit(() =>
        {
            var current = _document.Strokes[strokeIndex].Points[pointIndex];
            _document.Strokes[strokeIndex].Points[pointIndex] = update(current);
        }, rebuild: false);
    }

    private void ApplyEdit(Action edit, bool rebuild = true)
    {
        BeginEdit?.Invoke(this, EventArgs.Empty);
        try
        {
            edit();
            var encoded = WipeStrokeDocumentCodec.Encode(_document);
            if (string.Equals(encoded, EncodedDocument, StringComparison.Ordinal))
            {
                if (rebuild)
                {
                    LoadDocument(encoded);
                }
            }
            else
            {
                var wasSuppressingDocumentReload = _suppressDocumentReload;
                _suppressDocumentReload = !rebuild;
                try
                {
                    SetCurrentValue(EncodedDocumentProperty, encoded);
                }
                finally
                {
                    _suppressDocumentReload = wasSuppressingDocumentReload;
                }
            }
        }
        finally
        {
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
    }

    private static WipeStroke CreateStarterStroke() =>
        new()
        {
            Points =
            [
                new WipeStrokePoint(0, 192, 540, 100),
                new WipeStrokePoint(100, 1728, 540, 100),
            ],
        };

    private static Button CreateButton(string content, RoutedEventHandler clickHandler)
    {
        var button = new Button
        {
            Content = content,
            Margin = new Thickness(0, 0, 4, 0),
            Padding = new Thickness(5, 1, 5, 1),
            MinWidth = 48,
            VerticalAlignment = VerticalAlignment.Center,
        };
        button.Click += clickHandler;
        return button;
    }

    private static bool TryParseFiniteDouble(string text, out double value) =>
        (double.TryParse(
             text,
             NumberStyles.Float,
             CultureInfo.CurrentCulture,
             out value) ||
         double.TryParse(
             text,
             NumberStyles.Float,
             CultureInfo.InvariantCulture,
             out value)) &&
        double.IsFinite(value);

    private static double NormalizeValue(
        double value,
        double minimum,
        double maximum,
        bool integerOnly)
    {
        var clamped = double.IsFinite(value)
            ? Math.Clamp(value, minimum, maximum)
            : minimum;
        return integerOnly
            ? Math.Round(clamped, MidpointRounding.AwayFromZero)
            : clamped;
    }

    private static string FormatValue(double value, bool integerOnly) =>
        value.ToString(
            integerOnly ? "0" : "0.###",
            CultureInfo.CurrentCulture);
}
