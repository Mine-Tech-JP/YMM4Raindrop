// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using YMM4GlassWipe;
using YukkuriMovieMaker.UndoRedo;
using YmmJson = YukkuriMovieMaker.Json.Json;

namespace YMM4GlassWipe.Verification;

internal static class OutsideDropletPhysicsPersistenceVerification
{
    private static readonly string[] PhysicsProperties =
    [
        nameof(GlassWipeVideoEffect.OutsideDropletMotionMode),
        nameof(GlassWipeVideoEffect.OutsideDropletSlip),
        nameof(GlassWipeVideoEffect.OutsideDropletSupply),
    ];

    public static void Run()
    {
        VerifyEffectPersistence();
        VerifySanitization();
        VerifyPresetMigrationAndScopes();
        VerifyVisibilityAndUndo();
    }

    private static void VerifyEffectPersistence()
    {
        var fresh = new GlassWipeVideoEffect();
        Physics(fresh, OutsideDropletMotionMode.SimplePhysics, 100, 200);
        Equal(50d, fresh.OutsideDropletAmount, "新規水滴量を維持");
        Equal(100d, fresh.OutsideDropletSize, "テンプレートの新規サイズ");
        foreach (var load in new Func<string, GlassWipeVideoEffect?>[]
                 {
                     json => YmmJson.LoadFromText<GlassWipeVideoEffect>(json),
                     json => JsonSerializer.Deserialize<GlassWipeVideoEffect>(json),
                 })
        {
            var missing = load("{}") ?? throw new InvalidOperationException("旧Effectを復元できません。");
            Physics(missing, OutsideDropletMotionMode.Legacy, 100, 100);
            Equal(0d, missing.OutsideDropletAmount, "旧欠落水滴量を維持");
            Equal(100d, missing.OutsideDropletSize, "旧欠落サイズを維持");
        }

        foreach (var mode in Enum.GetValues<OutsideDropletMotionMode>())
        {
            var source = new GlassWipeVideoEffect
            {
                OutsideDropletMotionMode = mode, OutsideDropletSlip = 175,
                OutsideDropletSupply = 0, OutsideDropletDeformWithSurface = true,
                OutsideDropletFallingRatio = 13, OutsideDropletFallFrequency = 275,
                OutsideDropletSize = 225, OutsideDropletFallSpeed = 125,
            };
            var restored = YmmJson.LoadFromText<GlassWipeVideoEffect>(YmmJson.GetJsonText(source))
                ?? throw new InvalidOperationException("物理設定を保存復元できません。");
            Physics(restored, mode, 175, 0);
            Equal(true, restored.OutsideDropletDeformWithSurface, "面変形保存値");
            Equal(13d, restored.OutsideDropletFallingRatio, "旧割合保存値");
            Equal(275d, restored.OutsideDropletFallFrequency, "旧頻度保存値");
            Equal(225d, restored.OutsideDropletSize, "サイズ保存値");
            Equal(125d, restored.OutsideDropletFallSpeed, "速度保存値");
        }
    }

    private static void VerifySanitization()
    {
        Equal(0, (int)OutsideDropletMotionMode.Legacy, "周期動作の保存値を維持");
        Equal(1, (int)OutsideDropletMotionMode.SimplePhysics, "簡易物理の保存値");
        Equal("周期動作", typeof(OutsideDropletMotionMode).GetField(nameof(OutsideDropletMotionMode.Legacy))!
            .GetCustomAttribute<DisplayAttribute>()!.Name!, "周期動作の表示名");
        Equal("簡易物理", typeof(OutsideDropletMotionMode).GetField(nameof(OutsideDropletMotionMode.SimplePhysics))!
            .GetCustomAttribute<DisplayAttribute>()!.Name!, "簡易物理の表示名を維持");
        var motionDescription = typeof(GlassWipeVideoEffect).GetProperty(nameof(GlassWipeVideoEffect.OutsideDropletMotionMode))!
            .GetCustomAttribute<DisplayAttribute>()!.Description!;
        True(motionDescription.Contains("周期動作") && motionDescription.Contains("待ち時間") &&
            motionDescription.Contains("滑りやすさ") && !motionDescription.Contains("従来"), "方式の違いを示す説明");
        foreach (var value in new[] { double.NaN, double.NegativeInfinity, double.PositiveInfinity })
        {
            Equal(1f, OutsideDropletSettings.SanitizeSlipScale(value), "滑りの非有限値");
            Equal(1f, OutsideDropletSettings.SanitizeSupplyScale(value), "補給の非有限値");
            Physics(new GlassWipeVideoEffect
            {
                OutsideDropletMotionMode = (OutsideDropletMotionMode)999,
                OutsideDropletSlip = value, OutsideDropletSupply = value,
            }, OutsideDropletMotionMode.Legacy, 100, 100);
        }
        Equal(.25f, OutsideDropletSettings.SanitizeSlipScale(-1), "滑り下限");
        Equal(4f, OutsideDropletSettings.SanitizeSlipScale(401), "滑り上限");
        Equal(0f, OutsideDropletSettings.SanitizeSupplyScale(-1), "補給下限");
        Equal(4f, OutsideDropletSettings.SanitizeSupplyScale(401), "補給上限");
        var invalid = YmmJson.LoadFromText<GlassWipeVideoEffect>(
            "{\"OutsideDropletMotionMode\":999,\"OutsideDropletSlip\":-1,\"OutsideDropletSupply\":401}")!;
        Physics(invalid, OutsideDropletMotionMode.Legacy, 25, 400);
        foreach (var (name, minimum) in new[] { (PhysicsProperties[1], 25d), (PhysicsProperties[2], 0d) })
        {
            var property = typeof(GlassWipeVideoEffect).GetProperty(name)!;
            var range = property.GetCustomAttribute<RangeAttribute>()!;
            Equal(minimum, Convert.ToDouble(range.Minimum), name + " UI下限");
            Equal(400d, Convert.ToDouble(range.Maximum), name + " UI上限");
            Equal(name == PhysicsProperties[2] ? 200d : 100d, Convert.ToDouble(property.GetCustomAttribute<DefaultValueAttribute>()!.Value), name + " リセット");
        }
    }

    private static void VerifyPresetMigrationAndScopes()
    {
        Equal(16, DetailedWipePresetState.CurrentVersion, "物理設定Stateの版");
        Equal(1, DetailedWipePresetExchangeState.CurrentVersion, "外側交換schema維持");
        var preset = new DetailedWipePresetState
        {
            OutsideDropletMotionMode = OutsideDropletMotionMode.SimplePhysics,
            OutsideDropletSlip = 250, OutsideDropletSupply = 0,
        };
        var encoded = DetailedWipePresetCodec.Encode(preset);
        True(DetailedWipePresetCodec.TryDecode(encoded, out var restored), "v16往復");
        StatePhysics(restored, OutsideDropletMotionMode.SimplePhysics, 250, 0);
        var missingCurrent = JsonNode.Parse(encoded)!.AsObject();
        foreach (var name in PhysicsProperties)
            missingCurrent.Remove(JsonNamingPolicy.CamelCase.ConvertName(name));
        True(DetailedWipePresetCodec.TryDecode(missingCurrent.ToJsonString(), out var missingState), "v16欠落補完");
        StatePhysics(missingState, OutsideDropletMotionMode.Legacy, 100, 100);
        for (var version = 1; version <= 15; version++)
        {
            var node = JsonNode.Parse(encoded)!.AsObject();
            node["dataSchemaVersion"] = version;
            foreach (var name in PhysicsProperties)
                True(node.Remove(JsonNamingPolicy.CamelCase.ConvertName(name)), "旧データから新キー除去");
            True(DetailedWipePresetCodec.TryDecode(node.ToJsonString(), out var old), $"v{version}読込");
            StatePhysics(old, OutsideDropletMotionMode.Legacy, 100, 100);
        }
        var invalid = new DetailedWipePresetState
        {
            OutsideDropletMotionMode = (OutsideDropletMotionMode)(-1),
            OutsideDropletSlip = double.NaN, OutsideDropletSupply = 999,
        };
        True(invalid.TrySanitize(out var safe), "不正Stateの正規化");
        StatePhysics(safe, OutsideDropletMotionMode.Legacy, 100, 400);

        foreach (var scope in Enum.GetValues<DetailedWipePresetScope>())
        {
            var current = new GlassWipeVideoEffect { OutsideDropletMotionMode = OutsideDropletMotionMode.Legacy, OutsideDropletSlip = 125, OutsideDropletSupply = 300 };
            var apply = DetailedWipePresetExchangeCodec.EncodeApply(scope, preset);
            var node = JsonNode.Parse(apply)!["state"]!.AsObject();
            foreach (var name in PhysicsProperties)
                Equal(scope == DetailedWipePresetScope.All,
                    node.ContainsKey(JsonNamingPolicy.CamelCase.ConvertName(name)), "部分保存は物理項目を含めない");
            current.DetailedPresetExchange = apply;
            Physics(current, scope == DetailedWipePresetScope.All ? OutsideDropletMotionMode.SimplePhysics : OutsideDropletMotionMode.Legacy,
                scope == DetailedWipePresetScope.All ? 250 : 125, scope == DetailedWipePresetScope.All ? 0 : 300);
            var clone = YmmJson.GetClone(current)!;
            Physics(clone, current.OutsideDropletMotionMode, current.OutsideDropletSlip, current.OutsideDropletSupply);
        }

        foreach (var scope in new[] { DetailedWipePresetScope.Path, DetailedWipePresetScope.Brush })
        {
            True(DetailedWipePresetStateMerger.TryMerge(preset, new DetailedWipePresetState(), scope, out var partial),
                "部分適用のState統合");
            StatePhysics(partial, OutsideDropletMotionMode.SimplePhysics, 250, 0);
        }

        // 軌跡不正時のBrush経由snapshotでも物理値と旧版補完を保持する。
        foreach (var version in new[] { 15, 16 })
        {
            preset.DataSchemaVersion = version;
            preset.CustomPathData = "{invalid";
            var snapshot = DetailedWipePresetExchangeCodec.EncodeSnapshot(WipePathInputMode.StrokeCollection, preset);
            True(DetailedWipePresetExchangeCodec.TryDecode(snapshot, out var exchange), "破損軌跡snapshot");
            True(!exchange.CanSavePath, "破損軌跡を保存可能としない");
            StatePhysics(exchange.State, version == 15 ? OutsideDropletMotionMode.Legacy : OutsideDropletMotionMode.SimplePhysics,
                version == 15 ? 100 : 250, version == 15 ? 100 : 0);
        }
    }

    private static void VerifyVisibilityAndUndo()
    {
        var effect = new GlassWipeVideoEffect
        {
            OutsideDropletAmount = 50, OutsideDropletFallEnabled = true,
            OutsideDropletDeformWithSurface = true, OutsideDropletFallingRatio = 17,
            OutsideDropletFallFrequency = 325,
            OutsideDropletMotionMode = OutsideDropletMotionMode.Legacy,
        };
        var notifications = new HashSet<string?>();
        effect.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        True(effect.AreOutsideDropletLegacyFallSettingsVisible, "従来項目表示");
        effect.OutsideDropletMotionMode = OutsideDropletMotionMode.SimplePhysics;
        True(effect.AreOutsideDropletPhysicsSettingsVisible && !effect.AreOutsideDropletLegacyFallSettingsVisible &&
            !effect.IsOutsideDropletDeformWithSurfaceVisible, "物理項目の表示切替");
        effect.OutsideDropletFallEnabled = false;
        True(effect.IsOutsideDropletMergeVisible && effect.AreOutsideDropletPhysicsSettingsVisible, "落下OFFの合体と補給");
        effect.OutsideDropletAmount = 0;
        True(!effect.AreOutsideDropletPhysicsSettingsVisible && !effect.IsOutsideDropletMergeVisible, "量0で隠す");
        True(notifications.Contains(nameof(effect.AreOutsideDropletPhysicsSettingsVisible)) &&
            notifications.Contains(nameof(effect.AreOutsideDropletLegacyFallSettingsVisible)) &&
            notifications.Contains(nameof(effect.IsOutsideDropletDeformWithSurfaceVisible)), "表示通知");
        Equal(true, effect.OutsideDropletDeformWithSurface, "面変形を自動変更しない");
        Equal(17d, effect.OutsideDropletFallingRatio, "割合を自動変更しない");
        Equal(325d, effect.OutsideDropletFallFrequency, "頻度を自動変更しない");

        // 各保存プロパティを列挙し、実際のYMM4 Undoコマンドで個別編集を往復する。
        var editable = TypeDescriptor.GetProperties(typeof(GlassWipeVideoEffect));
        object[] editedValues = [OutsideDropletMotionMode.Legacy, 225d, 0d];
        for (var index = 0; index < PhysicsProperties.Length; index++)
        {
            var target = new GlassWipeVideoEffect();
            var property = editable[PhysicsProperties[index]]!;
            True(!property.IsReadOnly, "Undo対象の保存プロパティ");
            var before = property.GetValue(target);
            var commands = CaptureCommands(target, () => property.SetValue(target, editedValues[index]));
            Replay(commands, false);
            Equal(before, property.GetValue(target), "個別Undo");
            Replay(commands, true);
            Equal(editedValues[index], property.GetValue(target), "個別Redo");
        }

        var allTarget = new GlassWipeVideoEffect();
        var allCommands = CaptureCommands(allTarget, () => allTarget.DetailedPresetExchange =
            DetailedWipePresetExchangeCodec.EncodeApply(DetailedWipePresetScope.All, new DetailedWipePresetState
            {
                OutsideDropletMotionMode = OutsideDropletMotionMode.SimplePhysics,
                OutsideDropletSlip = 175, OutsideDropletSupply = 0,
            }));
        Replay(allCommands, false);
        Physics(allTarget, OutsideDropletMotionMode.SimplePhysics, 100, 200);
        Replay(allCommands, true);
        Physics(allTarget, OutsideDropletMotionMode.SimplePhysics, 175, 0);
    }

    private static List<IUndoRedoCommand> CaptureCommands(GlassWipeVideoEffect effect, Action edit)
    {
        var commands = new List<IUndoRedoCommand>();
        effect.UndoRedoCommandCreated += Capture;
        try { edit(); }
        finally { effect.UndoRedoCommandCreated -= Capture; }
        True(commands.Count > 0, "Undoコマンド取得");
        return commands;

        void Capture(object? sender, UndoRedoEventArgs args)
        {
            if (args.Command.IsEmpty) return;
            if (args.Command is not IUndoRedoCommand command)
                throw new InvalidOperationException("同期Undoコマンドを取得できません。");
            commands.Add(command);
        }
    }

    private static void Replay(List<IUndoRedoCommand> commands, bool redo)
    {
        if (redo) foreach (var command in commands) command.Redo();
        else for (var index = commands.Count - 1; index >= 0; index--) commands[index].Undo();
    }

    private static void Physics(GlassWipeVideoEffect effect, OutsideDropletMotionMode mode, double slip, double supply)
    {
        Equal(mode, effect.OutsideDropletMotionMode, "Effectの動き");
        Equal(slip, effect.OutsideDropletSlip, "Effectの滑り");
        Equal(supply, effect.OutsideDropletSupply, "Effectの補給");
    }

    private static void StatePhysics(DetailedWipePresetState state, OutsideDropletMotionMode mode, double slip, double supply)
    {
        Equal<OutsideDropletMotionMode?>(mode, state.OutsideDropletMotionMode, "Stateの動き");
        Equal<double?>(slip, state.OutsideDropletSlip, "Stateの滑り");
        Equal<double?>(supply, state.OutsideDropletSupply, "Stateの補給");
    }

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: 期待={expected}, 実際={actual}");
    }
}
