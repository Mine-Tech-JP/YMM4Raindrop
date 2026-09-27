// SPDX-License-Identifier: MPL-2.0

using System.Text.Json;
using YMM4GlassWipe;
using Physics = YMM4GlassWipe.OutsideDropletPhysicsSimulation;
using Settings = YMM4GlassWipe.OutsideDropletPhysicsSettings;
using Drop = YMM4GlassWipe.OutsideDropletPhysicsDrop;
using Snapshot = YMM4GlassWipe.OutsideDropletPhysicsSnapshot;
using Replay = YMM4GlassWipe.OutsideDropletPhysicsReplay;

namespace YMM4GlassWipe.Verification;

internal static class OutsideDropletPhysicsVerification
{
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true };

    public static void Run()
    {
        var settings = new Settings(InitialCount: 256, Supply: 4);
        var direct = new Physics(settings);
        direct.AdvanceTo(7200);
        var expected = Physics.Digest(direct.Save());
        Check(expected == "E47FFB2497498B40ADC0CA3205C0805958C8364CB398E37170E45ECF28C323D0",
            "先行試作の60秒時点の物理状態から変化しました。");
        Verify1(settings, expected);
        Verify2(settings, expected);
        Verify3(settings, expected);
        Verify4(settings, expected);
        Verify5(settings, expected);
        Verify6(settings, expected);
        Verify7(settings, expected);
        Verify8(settings, expected);
        Verify9(settings, expected);
        Verify10(settings, expected);
        Verify11(settings, expected);
        Verify12(settings, expected);
        Verify13(settings, expected);
        Verify14(settings, expected);
        Verify15(settings, expected);
        Verify16(settings, expected);
        Verify17(settings, expected);
        Verify18(settings, expected);
        VerifyInvalidInputs();
        VerifyExtremeTimeAndWidth();
        VerifyTransitionReplayAndCapacity();
        VerifySnapshotRejection();
        VerifyFullCacheBudget();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void ExpectArgument(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("不正入力が拒否されませんでした。");
    }

    // 小滴の静止と大滴の落下
    private static void Verify1(Settings settings, string expected)
    {
        var p = new Physics(new(Supply: 0), [Drop.Make(1, 300, 100, 8), Drop.Make(2, 600, 100, 18)]);
        p.AdvanceTo(60); Check(p.Bodies[0].Y == 100 && p.Bodies[1].Y > 100, "大小の状態が逆です。"); p.CheckMass();
    }

    // 静止滴の合体がしきい値を超える
    private static void Verify2(Settings settings, string expected)
    {
        var p = new Physics(new(Supply: 0), [Drop.Make(1, 300, 100, 10), Drop.Make(2, 318, 100, 10)]);
        Check(p.Count == 1 && p.Bodies[0].Radius > 12, "体積加算が不正です。");
        p.AdvanceTo(30); Check(p.Bodies[0].Moving && p.Bodies[0].Y > 100, "成長後に滑りません。"); p.CheckMass();
    }

    // 保持力とサイズの独立した効果
    private static void Verify3(Settings settings, string expected)
    {
        var fixture = new[] { Drop.Make(1, 300, 100, 10) };
        var low = new Physics(new(Supply: 0, Slip: .25), fixture);
        var high = new Physics(new(Supply: 0, Slip: 4), fixture);
        low.AdvanceTo(30); high.AdvanceTo(30);
        Check(!low.Bodies[0].Moving && high.Bodies[0].Moving, "保持力が反映されません。");
    }

    // 加速と抵抗
    private static void Verify4(Settings settings, string expected)
    {
        var p = new Physics(new(Supply: 0), [Drop.Make(1, 300, -10000, 18)]);
        p.AdvanceTo(10); var early = p.Bodies[0].Vy; p.AdvanceTo(120);
        Check(early > 0 && p.Bodies[0].Vy > early && p.Bodies[0].Vy < 810, "加速または抵抗が不正です。"); p.CheckMass();
    }

    // 固定刻みの高速すり抜け検出
    private static void Verify5(Settings settings, string expected)
    {
        var fast = Drop.Make(1, 300, 50, 2); fast.Vy = 24000; fast.Moving = true;
        var p = new Physics(new(Supply: 0), [fast, Drop.Make(2, 300, 100, 2)]);
        p.AdvanceTo(1); Check(p.Count == 1, "移動区間の接触を見落としました。"); p.CheckMass();
    }

    // 256滴の同時接触と合体OFF
    private static void Verify6(Settings settings, string expected)
    {
        var fixture = Enumerable.Range(0, 256).Select(i => Drop.Make(i + 1, 300, 100, 4)).ToArray();
        var merged = new Physics(new(Supply: 0), fixture);
        var separate = new Physics(new(Supply: 0, Merge: false), fixture);
        Check(merged.Count == 1 && separate.Count == 256, "密集時の粒子数が不正です。"); merged.CheckMass();
    }

    // 落下OFFと補給上限
    private static void Verify7(Settings settings, string expected)
    {
        var p = new Physics(new(InitialCount: 256, Supply: 4, Fall: false, Merge: false));
        var y = p.Bodies[0].Y; p.AdvanceTo(1200);
        Check(p.Count == 256 && p.Skipped > 0 && p.Rejected > 0 && p.Bodies[0].Y == y, "上限や静止が不正です。"); p.CheckMass();
    }

    // 補給と流出の水量収支
    private static void Verify8(Settings settings, string expected)
    {
        var p = new Physics(new(InitialCount: 256, Supply: 4, Speed: 4));
        for (int i = 1; i <= 120; i++) { p.AdvanceTo(i * 120); p.CheckMass(); }
        Check(p.Outflow > 0 && p.Merges > 0, "流出または合体が発生しません。");
    }

    // 出生開始と出現期間
    private static void Verify9(Settings settings, string expected)
    {
        var p = new Physics(new(StartTick: 120, OnsetTicks: 600, Supply: 0, Merge: false, Fall: false));
        p.AdvanceTo(119); Check(p.Count == 0, "出生が開始前です。");
        p.AdvanceTo(720); Check(p.Count == 128, "初期出現が完了しません。");
    }

    // 順再生と直接シーク
    private static void Verify10(Settings settings, string expected)
    {
        var sequential = new Physics(settings);
        for (int f = 0; f <= 3600; f++) sequential.AdvanceTo(Physics.TickForFrame(f, 60));
        Check(expected == Physics.Digest(sequential.Save()), "再生順で状態が変わりました。");
    }

    // 24/30/60/120fpsの共通時刻
    private static void Verify11(Settings settings, string expected)
    {
        foreach (int fps in new[] { 24, 30, 60, 120 }) Check(Physics.TickForFrame(fps * 60, fps) == 7200, "tickが一致しません。");
        Check(Physics.TickForFrame(1, 24) == 5 && Physics.TickForFrame(1, 30) == 4, "境界が不正です。");
    }

    // 後方シークとキャッシュ追放
    private static void Verify12(Settings settings, string expected)
    {
        var replay = new Replay(settings, 32768);
        replay.At(14400); replay.At(1000);
        Check(expected == Physics.Digest(replay.At(7200)) && replay.Evictions > 0 && replay.PeakBytes <= replay.Limit, "キャッシュ復元が不正です。");
        var before = replay.Engine.IntegratedTicks; replay.At(7200);
        Check(before == replay.Engine.IntegratedTicks, "同一時刻で物理計算が進みました。");
    }

    // snapshot保存と読込
    private static void Verify13(Settings settings, string expected)
    {
        var p = new Physics(settings); p.AdvanceTo(1234);
        var json = JsonSerializer.Serialize(p.Save(), JsonOptions);
        var restored = new Physics(settings); restored.Restore(JsonSerializer.Deserialize<Snapshot>(json, JsonOptions)!);
        restored.AdvanceTo(7200); Check(expected == Physics.Digest(restored.Save()), "保存後の再現が不正です。");
    }

    // 設定変更時に新しい初期状態を使う
    private static void Verify14(Settings settings, string expected)
    {
        var changed = new Physics(settings with { Seed = 9 }); changed.AdvanceTo(7200);
        Check(expected != Physics.Digest(changed.Save()), "Seed変更が反映されません。");
        var reset = new Physics(settings); reset.AdvanceTo(7200); Check(expected == Physics.Digest(reset.Save()), "初期復帰が不正です。");
    }

    // 異なる設定と不正snapshotの拒否
    private static void Verify15(Settings settings, string expected)
    {
        var p = new Physics(settings); var before = Physics.Digest(p.Save());
        var badDrops = p.Save().Drops; badDrops[0].Mass = double.NaN;
        foreach (var bad in new[] { p.Save() with { Settings = settings with { Seed = 99 } }, p.Save() with { Drops = badDrops } })
        {
            var rejected = false;
            try { p.Restore(bad); } catch (ArgumentException) { rejected = true; }
            Check(rejected && Physics.Digest(p.Save()) == before, "不正復元が状態を変更しました。");
        }
    }

    // サイズと速度と保持力の端点
    private static void Verify16(Settings settings, string expected)
    {
        foreach (double size in new[] { .25, 4d })
        foreach (double slip in new[] { .25, 4d })
        foreach (double speed in new[] { .25, 4d })
        {
            var p = new Physics(new(256, Size: size, Slip: slip, Speed: speed, Supply: 4));
            for (int second = 1; second <= 30; second++) { p.AdvanceTo(second * 120); p.CheckMass(); }
            Check(p.Count <= 256, "粒子上限を超えました。");
        }
    }

    // 出現途中のsnapshotと複数インスタンス
    private static void Verify17(Settings settings, string expected)
    {
        var rain = settings with { StartTick = 120, OnsetTicks = 2400 };
        var p = new Physics(rain); p.AdvanceTo(1000); var saved = p.Save(); p.AdvanceTo(3600);
        var q = new Physics(rain); q.Restore(saved); q.AdvanceTo(3600);
        Check(Physics.Digest(p.Save()) == Physics.Digest(q.Save()), "残る出生予定が一致しません。");
        q.AdvanceTo(4000); Check(p.Tick == 3600, "可変状態が共有されています。");
    }

    // 静止休止と全tick計算の一致
    private static void Verify18(Settings settings, string expected)
    {
        foreach (double supply in new[] { 0d, 4d })
        {
            var config = new Settings(256, Size: .25, Supply: supply, Fall: false);
            var fast = new Physics(config); var exhaustive = new Physics(config, skipRest: false);
            fast.AdvanceTo(7200); exhaustive.AdvanceTo(7200);
            Check(Physics.Digest(fast.Save()) == Physics.Digest(exhaustive.Save()), "休止省略で状態が変わりました。");
        }
    }

    private static void VerifyInvalidInputs()
    {
        foreach (var invalid in new[]
        {
            new Settings(InitialCount: -1), new Settings(InitialCount: 257),
            new Settings(Size: double.NaN), new Settings(Slip: 0), new Settings(Speed: double.PositiveInfinity),
            new Settings(Supply: -1), new Settings(Width: 0), new Settings(Width: double.PositiveInfinity),
            new Settings(Width: 1e51), new Settings(StartTick: -1), new Settings(OnsetTicks: -1),
            new Settings(StartTick: long.MaxValue, OnsetTicks: 1),
        }) ExpectArgument(() => new Physics(invalid));
        ExpectArgument(() => new Physics(null!));
        var invalidDrop = Drop.Make(1, 0, 0, 1);
        invalidDrop.Vx = double.NaN;
        ExpectArgument(() => new Physics(new(), [invalidDrop]));
        ExpectArgument(() => new Physics(new(), [Drop.Make(1, 0, 0, 1, 2), Drop.Make(2, 0, 0, 1, 1)]));
        ExpectArgument(() => new Replay(new(), -1));
        ExpectArgument(() => new Replay(new(), Replay.Budget + 1));
        ExpectArgument(() => new Replay(new()).At(-1));
        ExpectArgument(() => Physics.TickForFrame(-1, 60));
        ExpectArgument(() => Physics.TickForFrame(1, 0));
        try { Physics.TickForFrame(long.MaxValue, 1); }
        catch (OverflowException) { return; }
        throw new InvalidOperationException("frameからtickへの整数あふれを検出できません。");
    }

    private static void VerifyExtremeTimeAndWidth()
    {
        foreach (var width in new[] { double.Epsilon, 0.001, 1d, 10001d, 1e50 })
        {
            var engine = new Physics(new(256, Width: width, Supply: 4));
            engine.AdvanceTo(120);
            engine.CheckMass();
            var snapshot = engine.Save();
            engine.Restore(snapshot);
            Check(snapshot.Settings.Width == width, "入力の縦横比が変更されました。");
        }
        var empty = new Replay(new(InitialCount: 0, Supply: 0));
        Check(empty.At(long.MaxValue).Tick == long.MaxValue, "最大tickまでの休止区間が再現できません。");
        Check(empty.At(long.MaxValue - 1).Tick == long.MaxValue - 1, "最大tick近傍の後方シークが不正です。");
        var tinyRain = new Physics(new(InitialCount: 0, Supply: double.Epsilon));
        tinyRain.AdvanceTo(long.MaxValue);
        Check(tinyRain.Count == 0, "表現可能時刻より後の出生が巻き戻りました。");
        var late = new Physics(new(InitialCount: 1, StartTick: long.MaxValue, Supply: 4));
        late.AdvanceTo(long.MaxValue);
        Check(late.Count == 1, "最大tickの出生または雨時刻の加算が不正です。");
        var duration = new Physics(new(InitialCount: 1, Supply: 0, OnsetTicks: long.MaxValue));
        duration.AdvanceTo(1);
        Check(duration.Count == 0, "長い出現期間が負の出生時刻になりました。");
    }

    private static void VerifyTransitionReplayAndCapacity()
    {
        var config = new Settings(InitialCount: 0, Supply: 0);
        var drops = new[] { Drop.Make(1, 900, 210, 13), Drop.Make(2, 900, 390, 11) };
        var engine = new Physics(config, drops);
        engine.AdvanceTo(102);
        var merged = engine.Save();
        Check(merged.Transitions.Length == 1, "接近合体の表示履歴が保存されません。");
        var transition = merged.Transitions[0];
        Check(transition.Tick == 102 && transition.BeforeSurvivor.Id == transition.After.Id &&
            transition.BeforeAbsorbed.Id != transition.After.Id, "合体の系譜が不正です。");
        var restored = new Physics(config, drops);
        restored.Restore(JsonSerializer.Deserialize<Snapshot>(JsonSerializer.Serialize(merged, JsonOptions), JsonOptions)!);
        engine.AdvanceTo(110);
        restored.AdvanceTo(110);
        Check(JsonSerializer.Serialize(engine.Save(), JsonOptions) == JsonSerializer.Serialize(restored.Save(), JsonOptions),
            "復元後の表示履歴が一致しません。");
        restored.AdvanceTo(117);
        Check(restored.Save().Transitions.Length == 0, "表示履歴が15tick後に失効しません。");
        // 雨の最大補給と256滴の極端条件でも上限へ到達しない。
        foreach (var size in new[] { .25, 4d })
        {
            var stress = new Physics(new(256, Size: size, Supply: 4, Speed: 4));
            for (var tick = 1; tick <= 3600; tick++)
            {
                stress.AdvanceTo(tick);
                Check(stress.Save().Transitions.Length < Physics.TransitionCapacity, "表示履歴が上限へ達しました。");
            }
            stress.CheckMass();
        }
        // 上限状態を注入して次の合体で明示的に失敗することを確認する。
        var full = merged with { Transitions = Enumerable.Repeat(transition, Physics.TransitionCapacity).ToArray() };
        var overflow = new Physics(config, drops);
        overflow.Restore(full);
        overflow.Bodies[0] = transition.BeforeSurvivor;
        // 実データで現れない上限への追加は private 処理を呼び、黙った削除がないことを確認する。
        var method = typeof(Physics).GetMethod("RecordTransition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        try { method.Invoke(overflow, [transition.BeforeSurvivor, transition.BeforeAbsorbed, transition.After]); }
        catch (System.Reflection.TargetInvocationException error) when (error.InnerException is InvalidOperationException) { return; }
        throw new InvalidOperationException("表示履歴の上限超過が拒否されません。");
    }

    private static void VerifySnapshotRejection()
    {
        var settings = new Settings(InitialCount: 0, Supply: 0);
        var engine = new Physics(settings);
        var valid = engine.Save();
        foreach (var bad in new[]
        {
            valid with { Drops = null! }, valid with { Transitions = null! },
            valid with { NextId = 0 }, valid with { RainEvent = -1 },
            valid with { RainEvent = 1 }, valid with { NextId = long.MaxValue },
            valid with { Merges = long.MaxValue },
            valid with { Accepted = 1 }, valid with { InitialCursor = 1 },
            valid with { Transitions = new OutsideDropletPhysicsTransition[513] },
        })
        {
            ExpectArgument(() => engine.Restore(bad));
            Check(JsonSerializer.Serialize(engine.Save(), JsonOptions) == JsonSerializer.Serialize(valid, JsonOptions),
                "不正snapshotが現在の状態を変更しました。");
        }
        ExpectArgument(() => engine.Restore(null!));
    }

    private static void VerifyFullCacheBudget()
    {
        var replay = new Replay(new(256, Supply: 4));
        replay.At(3600L * Physics.Hz);
        Check(replay.PeakBytes <= Replay.Budget && replay.Evictions > 0, "16MiBのキャッシュ予算が守られていません。");
        replay.Engine.CheckMass();
    }
}
