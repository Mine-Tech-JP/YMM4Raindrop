// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using YMM4GlassWipe;
using Drop = YMM4GlassWipe.OutsideDropletPhysicsDrop;
using Frame = YMM4GlassWipe.OutsideDropletPhysicsFrame;
using Packet = YMM4GlassWipe.OutsideDropletPhysicsGpuConstants;
using Physics = YMM4GlassWipe.OutsideDropletPhysicsSimulation;
using Replay = YMM4GlassWipe.OutsideDropletPhysicsReplay;
using Settings = YMM4GlassWipe.OutsideDropletPhysicsSettings;
using Snapshot = YMM4GlassWipe.OutsideDropletPhysicsSnapshot;
using Transition = YMM4GlassWipe.OutsideDropletPhysicsTransition;

namespace YMM4GlassWipe.Verification;

#if PHYSICS_RENDERING_STANDALONE
// 親のビルド済みDLLに対して、この検証だけを独立実行するための入口。
internal static class OutsideDropletPhysicsRenderingVerificationEntryPoint
{
    private static int Main(string[] args)
    {
        try
        {
            Ymm4VerificationRuntime.Initialize();
            System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[0]));
            typeof(OutsideDropletPhysicsRenderingVerification).GetMethod(nameof(OutsideDropletPhysicsRenderingVerification.Run))!.Invoke(null, null);
            Console.WriteLine("CPUと表示の接続10群は全件合格しました。");
            return 0;
        }
        catch (Exception error)
        {
            try { Console.Error.WriteLine("CPU接続検証を中止しました。" + Environment.NewLine + error); }
            catch { /* 標準エラー出力も利用できない場合は終了コードで通知する。 */ }
            return 1;
        }
    }
}
#endif

/// <summary>
/// CPU状態から表示配列・GPU定数までを検証する。区画参照と静的配線の監査は、GPU画素比較の代替ではない。
/// </summary>
internal static class OutsideDropletPhysicsRenderingVerification
{
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true };

    public static void Run()
    {
        var failures = new List<Exception>();
        foreach (var (name, test) in new (string, Action)[]
        {
            ("tick間の表示補間", VerifyFraction),
            ("単独接触の開始位置・半径・終了", VerifySingleContact),
            ("同一tickの多段吸収", VerifyMultipleContacts),
            ("吸収中の継続接触", VerifyContinuedContacts),
            ("表示配列の順再生・直接・後方・保存復元", VerifyReplay),
            ("512表示枠と超過拒否", VerifyDisplayCapacity),
            ("GPU定数のサイズとoffset", VerifyGpuLayout),
            ("水筋・端・巨大滴・ghostの区画参照", VerifyTileCoverage),
            ("物理設定と旧frame/fpsの接続", VerifySettingsAndClock),
            ("モード・無効化・GPU走査の静的配線", VerifySourceWiring),
        })
        {
            try { test(); }
            catch (Exception error) { failures.Add(new InvalidOperationException(name + ": " + error.Message, error)); }
        }
        if (failures.Count > 0) throw new AggregateException("CPUと表示の接続検証に失敗しました。", failures);
    }

    private static void VerifyFraction()
    {
        var first = Drop.Make(1, 100, 200, 10);
        first.Moving = true;
        first.Vy = 100;
        var second = first;
        second.X = 104;
        second.Y = 206;
        second.Vy = 120;
        second.TrailTop = 190;
        var current = State(10, [first]);
        var next = State(11, [second]);
        var beforeJson = JsonSerializer.Serialize(current, JsonOptions);
        foreach (var fraction in new[] { 0d, .5, 1 - 1e-9, 1d })
        {
            var display = Frame.Create(current, next, fraction);
            Check(display.Length == 1, "接触なしで表示数が変化しました。");
            Near(100 + 4 * fraction, display[0].X, "Xの補間");
            Near(200 + 6 * fraction, display[0].Y, "Yの補間");
            Near(100 + 20 * fraction, display[0].Vy, "形状に使う速度の補間");
            Near(200 - 10 * fraction, display[0].TrailTop, "水筋の補間");
            Near(first.Radius, display[0].Radius, "接触なしの半径維持");
            CheckMass(current, display);
        }
        Equal(beforeJson, JsonSerializer.Serialize(current, JsonOptions), "表示計算がCPU snapshotを変更しました。");
        AssertGeometry(Frame.Create(current, next, 1 - 1e-9), Frame.Create(next, next with { Tick = 12 }, 1e-9), 1e-7);
    }

    private static void VerifySingleContact()
    {
        var settings = new Settings(InitialCount: 0, Supply: 0);
        var physics = new Physics(settings, [Drop.Make(1, 900, 210, 13), Drop.Make(2, 900, 390, 11)]);
        physics.AdvanceTo(101);
        var before = physics.Save();
        physics.AdvanceTo(102);
        var contact = physics.Save();
        Check(contact.Transitions.Length == 1, "接触用の実物理fixtureで合体が起きません。");
        var transition = contact.Transitions[0];
        physics.AdvanceTo(103);
        var after = physics.Save();
        var start = Frame.Create(contact, after, 0);
        AssertGeometry([transition.BeforeSurvivor, transition.BeforeAbsorbed], start, 1e-9);
        AssertGeometry(Frame.Create(before, contact, 1 - 1e-9), Frame.Create(contact, after, 1e-9), 1e-6);
        foreach (var fraction in new[] { 0d, .5, 1 - 1e-9 }) CheckMass(contact, Frame.Create(contact, after, fraction));
        physics.AdvanceTo(116);
        var lastGhost = physics.Save();
        physics.AdvanceTo(117);
        var expired = physics.Save();
        Check(Frame.Create(lastGhost, expired, 0).Length == 2, "寿命前にghostが消えました。");
        var end = Frame.Create(expired, expired with { Tick = 118 }, 0);
        Check(end.Length == expired.Drops.Length && end.Length == 1, "15tick後もghostが残っています。");
        CheckMass(expired, end);
    }

    private static void VerifyMultipleContacts()
    {
        var a = Drop.Make(1, 100, 200, 2);
        var b = Drop.Make(2, 104, 202, 3);
        var c = Drop.Make(3, 110, 204, 4);
        var first = Contact(100, a, b);
        var second = Contact(100, first.After, c);
        var joined = State(100, [second.After], [first, second]);
        AssertGeometry([a, b, c], Frame.Create(joined, joined with { Tick = 101 }, 0), 1e-9);
        for (var age = 0; age <= 15; age++)
        {
            var current = joined with { Tick = 100 + age };
            foreach (var fraction in new[] { 0d, .5, 1 - 1e-9 })
                CheckMass(current, Frame.Create(current, current with { Tick = current.Tick + 1 }, fraction));
        }
        var expired = joined with { Tick = 115, Transitions = [] };
        Check(Frame.Create(expired, expired with { Tick = 116 }, 0).Length == 1, "多段合体のghostが寿命後も残ります。");
    }

    private static void VerifyContinuedContacts()
    {
        foreach (var existingIsSurvivor in new[] { true, false })
        {
            var a = Drop.Make(existingIsSurvivor ? 1 : 2, 100, 200, 2);
            var b = Drop.Make(3, 104, 202, 3);
            var incoming = Drop.Make(existingIsSurvivor ? 4 : 1, 110, 204, 4);
            var first = Contact(100, a, b);
            var second = existingIsSurvivor ? Contact(104, first.After, incoming) : Contact(104, incoming, first.After);
            var before = State(103, [first.After, incoming], [first]);
            var contact = State(104, [second.After], [first, second]);
            AssertGeometry(Frame.Create(before, contact, 1 - 1e-9),
                Frame.Create(contact, contact with { Tick = 105 }, 1e-9), 1e-6);
            for (var tick = 104; tick <= 119; tick++)
            {
                var state = contact with { Tick = tick };
                foreach (var fraction in new[] { 0d, .5, 1 - 1e-9 })
                    CheckMass(state, Frame.Create(state, state with { Tick = tick + 1 }, fraction));
            }
        }
    }

    private static void VerifyReplay()
    {
        var settings = new Settings(256, Supply: 4);
        var sequential = new Replay(settings, 32768);
        var ticks = new[] { 0L, 1, 100, 239, 240, 241, 400, 720 };
        var expected = new Dictionary<long, string>();
        for (var tick = 0L; tick <= ticks[^1]; tick++)
        {
            var lower = sequential.At(tick);
            if (!ticks.Contains(tick)) continue;
            var upper = sequential.At(tick + 1);
            expected.Add(tick, DisplayDigest(lower, upper));
        }
        var reordered = new Replay(settings, 32768);
        reordered.At(2400);
        foreach (var tick in ticks.Reverse())
        {
            var lower = reordered.At(tick);
            var upper = reordered.At(tick + 1);
            Equal(expected[tick], DisplayDigest(lower, upper), "後方シークの表示配列");
            var direct = new Replay(settings);
            var fresh = direct.At(tick);
            Equal(expected[tick], DisplayDigest(fresh, direct.At(tick + 1)), "空キャッシュの表示配列");
            var saved = JsonSerializer.Deserialize<Snapshot>(JsonSerializer.Serialize(lower, JsonOptions), JsonOptions)!;
            var restored = new Physics(settings);
            restored.Restore(saved);
            restored.AdvanceTo(tick + 1);
            Equal(expected[tick], DisplayDigest(saved, restored.Save()), "snapshot再読込の表示配列");
        }
        Check(reordered.Evictions > 0, "再生fixtureでキャッシュ追放が発生していません。");
    }

    private static string DisplayDigest(Snapshot lower, Snapshot upper) => JsonSerializer.Serialize(
        new[] { 0d, .5, 1 - 1e-9 }.Select(fraction => Frame.Create(lower, upper, fraction)).ToArray(), JsonOptions);

    private static Drop[] CapacityFixture()
    {
        var drops = new List<Drop>();
        var transitions = new List<Transition>();
        for (var i = 0; i < 256; i++)
        {
            var contact = Contact(100, Drop.Make(i + 1, 959, 540, 1), Drop.Make(i + 257, 961, 540, 1));
            drops.Add(contact.After);
            transitions.Add(contact);
        }
        // 転送上限を検証する合成fixture。256物理滴+256ghostを同一区画へ集める。
        var state = State(100, drops.ToArray(), transitions.ToArray());
        var display = Frame.Create(state, state with { Tick = 101 }, 0);
        CheckMass(state, display);
        return display;
    }

    private static void VerifyDisplayCapacity()
    {
        var display = CapacityFixture();
        Check(display.Length == 512 && display[^1].Id == 512, "512表示枠を保持できません。");
        var parameters = Parameters();
        ExpectArgument(() => Packet.Create(display.Concat([Drop.Make(513, 0, 0, 1)]).ToArray(), parameters, 0, 0, 1920, 1080));
    }

    private static void VerifyGpuLayout()
    {
        Equal(32896, Marshal.SizeOf<Packet>(), "GPU定数のサイズ");
        foreach (var (field, offset) in new[]
        {
            (nameof(Packet.Frame), 0), (nameof(Packet.Style), 16), (nameof(Packet.Region), 32),
            (nameof(Packet.Geometry), 48), (nameof(Packet.Inverse0), 64), (nameof(Packet.Inverse1), 80),
            (nameof(Packet.Inverse2), 96), (nameof(Packet.Diagnostic), 112),
            (nameof(Packet.Heads), 128), (nameof(Packet.Trails), 8320), (nameof(Packet.Tiles), 16512),
        }) Equal(offset, Marshal.OffsetOf<Packet>(field).ToInt32(), "GPU定数offset " + field);
    }

    private static void VerifyTileCoverage()
    {
        var dense = CapacityFixture();
        var densePacket = Packet.Create(dense, Parameters(), 31, -17, 1920, 1080);
        Equal(512f, densePacket.Diagnostic.Y, "ghostを含むGPU有効枠数");
        for (var slot = 0; slot < dense.Length; slot++)
            Check(HasReference(densePacket, 8, 8, slot), "密集区画から表示枠が脱落: " + slot);

        var moving = Drop.Make(1, 121, 720, 18);
        moving.Moving = true;
        moving.Vy = 240;
        moving.TrailTop = 100;
        Drop[] cases = [moving, Drop.Make(2, 0, 0, 80), Drop.Make(3, 1919, 1079, 80),
            Drop.Make(4, 960, 540, 2000), Drop.Make(5, -5, 500, 12), Drop.Make(6, 400, 400, .001)];
        foreach (var trailLength in new[] { 0f, 1f })
        foreach (var height in new[] { 360f, 1080f, 2160f })
        {
            var width = height * 16 / 9;
            var parameters = Parameters() with { OutsideDropletTrailLength = trailLength };
            var tiled = Packet.Create(cases, parameters, 31, -17, width, height);
            var full = Packet.Create(cases, parameters, 31, -17, width, height, fullScan: true);
            Equal(0f, tiled.Frame.W, "区画走査フラグ");
            Equal(1f, full.Frame.W, "全走査フラグ");
            full.Frame.W = 0;
            Check(PacketBytes(tiled).SequenceEqual(PacketBytes(full)), "走査方式以外のGPU定数が変化しました。");
            Equal(31f, tiled.Style.X, "非ゼロ原点X");
            Equal(-17f, tiled.Style.Y, "非ゼロ原点Y");
            Equal((float)cases.Length, tiled.Diagnostic.Y, "全走査へ渡す滴数");
            if (height == 1080 && trailLength == 1)
            {
                Equal(new Vector4(121, 720, 18, 1.8f), tiled.Heads[0], "移動滴のCPU座標・半径・縦倍率");
                Equal(420f, tiled.Trails[0].X, "最大300基準pxの水筋始点");
                Equal(1f, tiled.Trails[0].Y, "移動滴の水筋有効フラグ");
            }
            for (var slot = 0; slot < cases.Length; slot++) VerifyHeadAndTrail(tiled, slot);
            for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++) Check(HasReference(tiled, x, y, 3), "巨大滴が区画から脱落しました。");
        }
    }

    private static void VerifyHeadAndTrail(Packet packet, int slot)
    {
        var head = packet.Heads[slot];
        var trail = packet.Trails[slot];
        // 頭の内部点・境界近傍を全走査対象として、区画版の参照包含を確認する。
        for (var step = 0; step < 32; step++)
        {
            var angle = step * Math.PI / 16;
            AssertPixelReference(packet, head.X + head.Z * .95 * Math.Cos(angle),
                head.Y + head.Z * head.W * .95 * Math.Sin(angle), slot);
        }
        AssertPixelReference(packet, head.X, head.Y, slot);
        if (trail.Y <= .5f) return;
        for (var step = 1; step <= 20; step++)
        {
            var amount = step / 20d;
            var y = trail.X + (head.Y - trail.X) * amount;
            AssertPixelReference(packet, head.X, y, slot);
            AssertPixelReference(packet, head.X + head.Z * .1 * amount, y, slot);
        }
    }

    private static void AssertPixelReference(Packet packet, double x, double y, int slot)
    {
        if (x < 0 || y < 0 || x >= packet.Frame.X || y >= packet.Frame.Y) return;
        var tileX = (int)Math.Floor(x / packet.Frame.X * 16);
        var tileY = (int)Math.Floor(y / packet.Frame.Y * 16);
        Check(HasReference(packet, tileX, tileY, slot), $"区画({tileX},{tileY})から枠{slot}の頭または水筋が脱落しました。");
    }

    private static bool HasReference(Packet packet, int x, int y, int slot)
    {
        var words = MemoryMarshal.Cast<OutsideDropletPhysicsUInt4, uint>(MemoryMarshal.CreateSpan(ref packet.Tiles[0], 1024));
        return (words[(y * 16 + x) * 16 + slot / 32] & (1u << (slot % 32))) != 0;
    }

    private static byte[] PacketBytes(Packet packet) => MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref packet, 1)).ToArray();

    private static void VerifySettingsAndClock()
    {
        var parameters = Parameters();
        Equal(0L, parameters.OutsideDropletFrame, "旧呼出の既定frame");
        Equal(60, parameters.OutsideDropletFps, "旧呼出の既定fps");
        var settings = OutsideDropletPhysicsCustomEffect.CreateSettings(parameters, 1920, 1080);
        Equal(128, settings.InitialCount, "水滴量50%の初期滴数");
        Equal(1920d, settings.Width, "1080基準の幅");
        settings.Validate();
        var later = parameters with { OutsideDropletFrame = 600, OutsideDropletFps = 30, OutsideDropletLocalTimeSeconds = 0,
            OutsideDropletAppearance = 2, OutsideDropletTrailLength = 0, OutsideDropletFallFrequency = 4, OutsideDropletFallingRatio = 0 };
        Equal(settings, OutsideDropletPhysicsCustomEffect.CreateSettings(later, 3840, 2160), "時刻・表示設定だけで物理設定が変わりました。");
        foreach (var (width, height) in new[] { (1f, 4096f), (20000f, 1f) })
        {
            var aspect = OutsideDropletPhysicsCustomEffect.CreateSettings(parameters, width, height);
            aspect.Validate();
            Equal((double)width / height * 1080, aspect.Width, "縦横比がClampされました。");
        }
        var rain = parameters with { OutsideDropletRainEnabled = 1, OutsideDropletRainStartSeconds = 1.25f, OutsideDropletRainDurationSeconds = 2.5f };
        var onset = OutsideDropletPhysicsCustomEffect.CreateSettings(rain, 1920, 1080);
        Equal(150L, onset.StartTick, "雨の開始tick");
        Equal(300L, onset.OnsetTicks, "雨の出現期間tick");
        var dry = rain with { OutsideDropletRainEnabled = 0, OutsideDropletFallEnabled = 0, OutsideDropletAmount = 0,
            OutsideDropletSupplyScale = 4, OutsideDropletMergeEnabled = 0, OutsideDropletFrame = 60 };
        var supply = OutsideDropletPhysicsCustomEffect.CreateSettings(dry, 1920, 1080);
        Equal(0L, supply.StartTick, "降り始めOFFで旧開始時刻を使いました。");
        var physics = new Physics(supply);
        physics.AdvanceTo(Physics.TickForFrame(dry.OutsideDropletFrame, dry.OutsideDropletFps));
        Check(physics.Count > 0, "旧時刻0・落下OFFでも進む補給が停止しました。");
    }

    private static GlassWipeParameters Parameters() => new(
        FogAmount: 0, Blur: 0, FogTintRed: 1, FogTintGreen: 1, FogTintBlue: 1, TintMix: 0,
        RegionShape: GlassWipeRegionShape.FullScreen, RegionCenterX: .5f, RegionCenterY: .5f,
        RegionWidth: 1, RegionHeight: 1, RegionRotationCos: 1, RegionRotationSin: 0, RegionFeather: 0,
        Quad: default, QuadMapping: default, WipeResidue: 0, WipeVariation: 0, FogNoise: 0, NoiseSeed: 0,
        OutsideDropletAmount: .5f, OutsideDropletSizeScale: 1, OutsideDropletStrength: 1, OutsideDropletSeed: 7,
        OutsideDropletLocalTimeSeconds: 0, OutsideDropletFallEnabled: 1, OutsideDropletFallingRatio: 1,
        OutsideDropletFallSpeedScale: 1, OutsideDropletTrailLength: 1, OutsideDropletDeformWithSurface: 0,
        OutsideDropletAppearance: 1, DebugView: GlassWipeDebugView.Final);

    private static void VerifySourceWiring()
    {
        var effect = Source("OutsideDropletPhysicsCustomEffect.cs");
        Contains(effect, "OutsideDropletPhysicsSimulation.TickForFrame(frame,fps)", "元のframe/fpsでtickを作る接続");
        Contains(effect, "parameters.OutsideDropletFrame", "元frameの参照");
        Contains(effect, "parameters.OutsideDropletFps", "元fpsの参照");
        Check(!effect.Contains("parameters.OutsideDropletLocalTimeSeconds", StringComparison.Ordinal), "旧float時刻が物理時計へ混入しました。");
        Contains(effect, "OutsideDropletPhysicsFrame.Create(", "表示補間への接続");
        Contains(effect, "OutsideDropletPhysicsGpuConstants.Create(", "GPU定数への接続");
        Contains(effect, "if(_settings!=settings)", "物理設定変更時の再構築");
        Contains(effect, "_previousParameters==parameters&&_previousBounds==bounds", "同一要求の再利用");
        var resources = Source("GlassWipeEffectResources.cs");
        Contains(resources, "usePhysics=usePreFogPipeline&&parameters.OutsideDropletMotionMode==OutsideDropletMotionMode.SimplePhysics", "モードと有効条件の分岐");
        var active = BlockAfter(resources, "if(usePhysics)");
        Contains(active, "_physicsEffect.ApplyParameters(", "有効時だけの物理更新");
        Equal(1, Regex.Matches(resources, Regex.Escape("_physicsEffect.ApplyParameters(")).Count, "物理更新の配線数");
        Contains(resources, "outsideDropletAmount>0&&outsideDropletStrength>0", "水滴無効時の前段省略");
        Contains(resources, "physicsCoverage&&parameters.OutsideDropletAmount>0&&parameters.OutsideDropletStrength>0", "診断表示の無効条件");
        Contains(resources, "parameterswith{OutsideDropletAmount=0}", "最終合成での二重描画抑止");
        Contains(Source("GlassWipeParameters.cs"), "Math.Max(frame,0),Math.Max(fps,1)", "旧呼出から元frame/fpsを保持");
        var shader = Source(Path.Combine("Shaders", "OutsideDropletPhysics.hlsl"));
        Contains(shader, "heads[512]", "HLSL頭部枠");
        Contains(shader, "trails[512]", "HLSL水筋枠");
        Contains(shader, "tiles[1024]", "HLSL区画枠");
        Check(shader.Contains("word<16u", StringComparison.Ordinal) ||
            shader.Contains("word<((uint)diagnostic.y+31u)/32u", StringComparison.Ordinal),
            "512枠時に16wordを走査できる上限がありません。");
        foreach (var (count, expectedWords) in new[] { (0, 0), (1, 1), (32, 1), (33, 2), (256, 8), (511, 16), (512, 16) })
        {
            var words = (count + 31) / 32;
            Equal(expectedWords, words, "有効滴数に必要なword数");
            Check(words <= 16 && words * 32 >= count, "有効滴を収容するword数が不足しています。");
        }
        Contains(shader, "tiles[tile*4u+word/4u][word%4u]", "CPUとHLSLの区画配列対応");
        Contains(shader, "i<(uint)diagnostic.y", "全走査の有効枠数");
        Contains(shader, "texelOrigin=round(uv.xy/uv.zw-scene.xy)+style.xy", "入力原点の小数部分を保持してtexel原点を正規化");
        Contains(shader, "sampleUv=(texelOrigin+samplePixel)*uv.zw", "原点と部分描画で屈折UVの演算順を統一");
        Contains(shader, "pixel=round((scene.xy-style.xy)*1024.0f)/1024.0f", "小数原点でも形状評価座標を1/1024画素へ正規化");
    }

    private static string Source(string file)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "YMM4GlassWipe.sln"))) directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("リポジトリルートが見つかりません。");
        var text = File.ReadAllText(Path.Combine(directory.FullName, "src", "YMM4GlassWipe", file));
        text = Regex.Replace(text, @"//[^\r\n]*|/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(text, @"\s+", string.Empty);
    }

    private static string BlockAfter(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Check(start >= 0, "配線の分岐が見つかりません: " + signature);
        start = source.IndexOf('{', start);
        var depth = 0;
        for (var index = start; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            if (source[index] == '}' && --depth == 0) return source[start..(index + 1)];
        }
        throw new InvalidOperationException("配線の分岐が閉じていません。");
    }

    private static Transition Contact(long tick, Drop survivor, Drop absorbed)
    {
        var after = survivor;
        var mass = survivor.Mass + absorbed.Mass;
        after.X = (survivor.X * survivor.Mass + absorbed.X * absorbed.Mass) / mass;
        after.Y = (survivor.Y * survivor.Mass + absorbed.Y * absorbed.Mass) / mass;
        after.Vy = (survivor.Vy * survivor.Mass + absorbed.Vy * absorbed.Mass) / mass;
        after.Mass = mass;
        after.Radius = Math.Cbrt(mass);
        after.Moving |= absorbed.Moving;
        return new(tick, survivor, absorbed, after);
    }

    private static Snapshot State(long tick, Drop[] drops, Transition[]? transitions = null) => new(
        new Settings(InitialCount: 0, Supply: 0), tick, drops, 0, 0,
        drops.Select(drop => drop.Id).DefaultIfEmpty().Max() + 1, drops.Sum(drop => drop.Mass), 0, 0, 0, 0)
    {
        Transitions = transitions ?? [],
    };

    private static void CheckMass(Snapshot physical, Drop[] display)
    {
        Check(display.Select(drop => drop.Id).Distinct().Count() == display.Length, "ghostを含む表示IDが重複しました。");
        Check(display.Length <= Frame.Capacity, "表示枠が512を超えました。");
        Near(physical.Drops.Sum(drop => drop.Mass), display.Sum(drop => drop.Mass), "表示水量合計と物理水量");
        foreach (var drop in display)
        {
            Check(double.IsFinite(drop.X) && double.IsFinite(drop.Y) && double.IsFinite(drop.Radius) && drop.Mass > 0 && drop.Radius > 0,
                "非有限値または空の表示滴が生成されました。");
            Near(drop.Mass, drop.Radius * drop.Radius * drop.Radius, "表示半径と水量");
        }
    }

    private static void AssertGeometry(Drop[] expected, Drop[] actual, double tolerance)
    {
        Equal(expected.Length, actual.Length, "境界の表示滴数");
        var values = actual.ToDictionary(drop => drop.Id);
        foreach (var drop in expected)
        {
            Check(values.TryGetValue(drop.Id, out var found), "境界で表示IDが消失: " + drop.Id);
            Near(drop.X, found.X, "境界X", tolerance);
            Near(drop.Y, found.Y, "境界Y", tolerance);
            Near(drop.Radius, found.Radius, "境界半径", tolerance);
            Near(drop.Mass, found.Mass, "境界水量", tolerance);
        }
    }

    private static void Contains(string source, string expected, string message) => Check(source.Contains(expected, StringComparison.Ordinal), message);
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Equal<T>(T expected, T actual, string message) => Check(EqualityComparer<T>.Default.Equals(expected, actual), $"{message}: 期待={expected}, 実際={actual}");
    private static void Near(double expected, double actual, string message, double tolerance = 1e-10) =>
        Check(double.IsFinite(actual) && Math.Abs(expected - actual) <= tolerance * Math.Max(1, Math.Abs(expected)), $"{message}: 期待={expected:R}, 実際={actual:R}");
    private static void ExpectArgument(Action action)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new InvalidOperationException("不正な描画入力が拒否されませんでした。");
    }
}
