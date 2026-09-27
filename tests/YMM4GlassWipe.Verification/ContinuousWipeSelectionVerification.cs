// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel;
using System.Text.Json;
using YMM4GlassWipe;
using YukkuriMovieMaker.UndoRedo;
using YmmJson = YukkuriMovieMaker.Json.Json;

namespace YMM4GlassWipe.Verification;

internal static class ContinuousWipeSelectionVerification
{
    public static void VerifyShapeSelection()
    {
        foreach (var source in Enum.GetValues<GlassWipeBrushShape>())
        {
            foreach (var target in new[] { GlassWipeBrushShape.Hand, GlassWipeBrushShape.ShoePrint, GlassWipeBrushShape.UserImage })
            {
                var effect = new GlassWipeVideoEffect { BrushShape = source, ContinuousWipe = false };
                effect.BrushShapeSelection = target;
                Check(effect.ContinuousWipe == (source != target),
                    $"{source}から{target}への切替だけでONとなり、同値再代入はOFFを保持する必要があります。");
                effect.ContinuousWipe = false;
                effect.BrushShapeSelection = target;
                Check(!effect.ContinuousWipe, "選択後の明示OFFは同値再代入で変えてはいけません。");
            }
        }

        foreach (var target in new[]
                 {
                     GlassWipeBrushShape.Ellipse, GlassWipeBrushShape.Rectangle,
                     (GlassWipeBrushShape)int.MaxValue,
                 })
        {
            var effect = new GlassWipeVideoEffect { ContinuousWipe = false };
            effect.BrushShapeSelection = target;
            Check(!effect.ContinuousWipe, "対象外の形状選択で連続拭きをONにしてはいけません。");
        }
    }

    public static void VerifySavedValueRestoration()
    {
        foreach (var shape in new[] { GlassWipeBrushShape.Hand, GlassWipeBrushShape.ShoePrint, GlassWipeBrushShape.UserImage })
        {
            foreach (var savedValue in new bool?[] { null, false, true })
            {
                var shapeProperty = $"\"BrushShape\":{(int)shape}";
                var continuousProperty = savedValue is { } value
                    ? $"\"ContinuousWipe\":{(value ? "true" : "false")}" : null;
                var jsonVariants = continuousProperty is null
                    ? new[] { "{" + shapeProperty + "}" }
                    : new[]
                    {
                        "{" + shapeProperty + "," + continuousProperty + "}",
                        "{" + continuousProperty + "," + shapeProperty + "}",
                    };
                foreach (var json in jsonVariants)
                {
                    foreach (var restore in new Func<string, GlassWipeVideoEffect?>[]
                             {
                                 text => JsonSerializer.Deserialize<GlassWipeVideoEffect>(text),
                                 text => YmmJson.LoadFromText<GlassWipeVideoEffect>(text),
                             })
                    {
                        var effect = restore(json) ?? throw new InvalidOperationException("保存値を復元できません。");
                        Check(effect.BrushShape == shape, "保存された形状を復元する必要があります。");
                        Check(effect.ContinuousWipe == (savedValue ?? false),
                            "プロパティ順に関係なく保存ON/OFFと旧欠落OFFを維持する必要があります。");
                        var clone = YmmJson.GetClone(effect) ?? throw new InvalidOperationException("再保存できません。");
                        Check(clone.BrushShape == shape && clone.ContinuousWipe == (savedValue ?? false),
                            "復元後のYMM4再保存でも形状とON/OFFを保持する必要があります。");
                        effect.ContinuousWipe = false;
                        effect.BrushShapeSelection = OtherShape(shape);
                        Check(effect.ContinuousWipe, "読込終了後の手動切替では再びONにする必要があります。");
                    }
                }
            }
        }
    }

    public static void VerifyPresetRestoration()
    {
        foreach (var target in new[] { GlassWipeBrushShape.Hand, GlassWipeBrushShape.ShoePrint, GlassWipeBrushShape.UserImage })
        {
            foreach (var scope in new[] { DetailedWipePresetScope.Brush, DetailedWipePresetScope.All })
            {
                var effect = new GlassWipeVideoEffect
                {
                    BrushShape = GlassWipeBrushShape.Ellipse,
                    ContinuousWipe = false,
                };
                var observedEnabled = false;
                effect.PropertyChanged += ObserveContinuousWipe;
                effect.DetailedPresetExchange = DetailedWipePresetExchangeCodec.EncodeApply(scope,
                    new DetailedWipePresetState { BrushShape = target, ContinuousWipe = false });
                effect.PropertyChanged -= ObserveContinuousWipe;
                Check(effect.BrushShape == target && !effect.ContinuousWipe,
                    "ブラシ・全設定のプリセットは保存されたOFFを保持する必要があります。");
                Check(!observedEnabled, "プリセット適用途中にも連続拭きONの変更通知を発生させてはいけません。");
                effect.BrushShapeSelection = OtherShape(target);
                Check(effect.ContinuousWipe, "プリセット適用後の手動切替ではONにする必要があります。");

                void ObserveContinuousWipe(object? sender, PropertyChangedEventArgs args)
                {
                    if (args.PropertyName == nameof(GlassWipeVideoEffect.ContinuousWipe))
                    {
                        observedEnabled |= effect.ContinuousWipe;
                    }
                }
            }

            var pathEffect = new GlassWipeVideoEffect { BrushShape = target, ContinuousWipe = false };
            pathEffect.DetailedPresetExchange = DetailedWipePresetExchangeCodec.EncodeApply(
                DetailedWipePresetScope.Path,
                new DetailedWipePresetState { BrushShape = OtherShape(target), ContinuousWipe = true });
            Check(pathEffect.BrushShape == target && !pathEffect.ContinuousWipe,
                "軌跡プリセットでは現在の形状とOFFを保持する必要があります。");
        }
    }

    public static void VerifySelectionSerialization()
    {
        var effect = new GlassWipeVideoEffect { BrushShape = GlassWipeBrushShape.ShoePrint, ContinuousWipe = false };
        foreach (var json in new[] { JsonSerializer.Serialize(effect), YmmJson.GetJsonText(effect) })
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Check(root.TryGetProperty(nameof(GlassWipeVideoEffect.BrushShape), out var savedShape),
                "従来のBrushShape保存名を維持する必要があります。");
            // System.Text.Jsonは数値、YMM4のJSON.NETはenum名で保存する。
            var savedShapeMatches = savedShape.ValueKind == JsonValueKind.Number
                ? savedShape.GetInt32() == (int)GlassWipeBrushShape.ShoePrint
                : savedShape.ValueKind == JsonValueKind.String &&
                  savedShape.GetString() == nameof(GlassWipeBrushShape.ShoePrint);
            Check(savedShapeMatches, "保存された形状が靴跡と一致する必要があります。");
            Check(!root.TryGetProperty(nameof(GlassWipeVideoEffect.BrushShapeSelection), out _),
                "画面専用の形状選択プロパティを保存してはいけません。");
        }

        var properties = TypeDescriptor.GetProperties(typeof(GlassWipeVideoEffect));
        Check(!properties[nameof(GlassWipeVideoEffect.BrushShape)]!.IsBrowsable,
            "保存用形状を画面へ重複表示してはいけません。");
        var selection = properties[nameof(GlassWipeVideoEffect.BrushShapeSelection)]!;
        Check(selection.IsBrowsable, "画面専用の形状選択を表示する必要があります。");
        var display = (System.ComponentModel.DataAnnotations.DisplayAttribute?)selection.Attributes[
            typeof(System.ComponentModel.DataAnnotations.DisplayAttribute)];
        Check(display?.Name == "形状" && display.GroupName == "ブラシ",
            "形状の項目名とグループを維持する必要があります。");
        Check(selection.Attributes.Cast<Attribute>().Any(attribute => attribute.GetType().Name == "EnumComboBoxAttribute"),
            "形状選択は既存のEnumComboBoxを使う必要があります。");
    }

    public static void VerifyUndoRedo()
    {
        foreach (var source in Enum.GetValues<GlassWipeBrushShape>())
        {
            foreach (var target in new[] { GlassWipeBrushShape.Hand, GlassWipeBrushShape.ShoePrint, GlassWipeBrushShape.UserImage })
            {
                if (source == target)
                {
                    continue;
                }
                var effect = new GlassWipeVideoEffect { BrushShape = source, ContinuousWipe = false };
                var commands = CaptureCommands(effect, () => effect.BrushShapeSelection = target);
                Check(effect.BrushShape == target && effect.ContinuousWipe, "選択後は対象形状とONになる必要があります。");
                for (var repeat = 0; repeat < 3; repeat++)
                {
                    UndoCommands(commands);
                    Check(effect.BrushShape == source && !effect.ContinuousWipe,
                        "形状選択のUndoで元の形状と明示OFFを復元する必要があります。");
                    RedoCommands(commands);
                    Check(effect.BrushShape == target && effect.ContinuousWipe,
                        "形状選択のRedoで対象形状とONを復元する必要があります。");
                }
            }
        }

        var presetEffect = new GlassWipeVideoEffect { BrushShape = GlassWipeBrushShape.Hand, ContinuousWipe = false };
        var presetCommands = CaptureCommands(presetEffect, () =>
            presetEffect.DetailedPresetExchange = DetailedWipePresetExchangeCodec.EncodeApply(
                DetailedWipePresetScope.Brush,
                new DetailedWipePresetState { BrushShape = GlassWipeBrushShape.ShoePrint, ContinuousWipe = false }));
        UndoCommands(presetCommands);
        Check(presetEffect.BrushShape == GlassWipeBrushShape.Hand && !presetEffect.ContinuousWipe,
            "プリセット適用のUndoでも元の明示OFFを維持する必要があります。");
        RedoCommands(presetCommands);
        Check(presetEffect.BrushShape == GlassWipeBrushShape.ShoePrint && !presetEffect.ContinuousWipe,
            "プリセット適用のRedoでも保存OFFを維持する必要があります。");
    }

    private static IReadOnlyList<IUndoRedoCommand> CaptureCommands(GlassWipeVideoEffect effect, Action edit)
    {
        var commands = new List<IUndoRedoCommand>();
        effect.UndoRedoCommandCreated += Capture;
        try
        {
            edit();
        }
        finally
        {
            effect.UndoRedoCommandCreated -= Capture;
        }
        Check(commands.Count > 0, "編集操作のUndo／Redoコマンドを取得できる必要があります。");
        return commands;

        void Capture(object? sender, UndoRedoEventArgs args)
        {
            if (args.Command.IsEmpty)
            {
                return;
            }
            if (args.Command is not IUndoRedoCommand command)
            {
                throw new InvalidOperationException("設定変更の同期Undoコマンドを取得できません。");
            }
            commands.Add(command);
        }
    }
    private static void UndoCommands(IReadOnlyList<IUndoRedoCommand> commands)
    {
        for (var index = commands.Count - 1; index >= 0; index--)
        {
            commands[index].Undo();
        }
    }

    private static void RedoCommands(IReadOnlyList<IUndoRedoCommand> commands)
    {
        foreach (var command in commands)
        {
            command.Redo();
        }
    }
    private static GlassWipeBrushShape OtherShape(GlassWipeBrushShape shape) =>
        shape == GlassWipeBrushShape.Hand ? GlassWipeBrushShape.ShoePrint : GlassWipeBrushShape.Hand;

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
