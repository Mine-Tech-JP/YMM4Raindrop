// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;
using YMM4GlassWipe;

internal static class OutsideDropletRainTimingVerification
{
    public static void VerifyBirthBoundariesAndDistribution()
    {
        var keys = Enumerable.Range(0, 1024)
            .Select(index => OutsideDropletRandom.GetCellKey(index % 32, index / 32, 4197U, 307U))
            .ToArray();
        var births = keys.Select(key => OutsideDropletRainTiming.GetAppearanceTimeSeconds(key, 2f, 5f)).ToArray();
        Check(births.All(time => time is >= 2f and <= 7f), "出現時刻は指定した秒範囲に収まる必要があります。");
        Check(births.Select(time => (int)((time - 2f) * 2f)).Distinct().Count() == 10,
            "固定配置の初回出現が指定期間全体へ分散する必要があります。");
        foreach (var key in keys)
        {
            var birth = OutsideDropletRainTiming.GetAppearanceTimeSeconds(key, 2f, 5f);
            Check(!OutsideDropletRainTiming.HasAppeared(key, MathF.BitDecrement(birth), 2f, 5f),
                "出現時刻の直前は未出現である必要があります。");
            Check(OutsideDropletRainTiming.HasAppeared(key, birth, 2f, 5f),
                "出現時刻で出現対象になる必要があります。");
            Check(OutsideDropletRainTiming.GetAppearanceTimeSeconds(key, 20f, 0f) == 20f,
                "期間0秒は全滴が開始時刻に一斉出現する必要があります。");
            Check(!OutsideDropletRainTiming.HasAppeared(key, float.NaN, 0f, 0f),
                "非有限時刻の滴を出現済みとしてはいけません。");
            var longBirth = OutsideDropletRainTiming.GetAppearanceTimeSeconds(key, 36000f, 36000f);
            Check(float.IsFinite(longBirth) && longBirth is >= 36000f and <= 72000f,
                "長い開始時間と期間でも出現時刻は有限である必要があります。");
        }
        var previous = 0;
        for (var frame = 0; frame <= 480; frame++)
        {
            var time = frame / 60f;
            var count = keys.Count(key => OutsideDropletRainTiming.HasAppeared(key, time, 2f, 5f));
            Check(count >= previous, "初回の出現は途中で巻き戻ってはいけません。");
            previous = count;
        }
        Check(previous == keys.Length, "出現期間終了後は全候補が出現済みになる必要があります。");
    }

    public static void VerifyWaitingAndSeekDeterminism()
    {
        foreach (var speed in new[] { 0.25f, 1f, 4f })
        foreach (var start in new[] { 0f, 23f })
        foreach (var duration in new[] { 0f, 5f, 40f })
        foreach (var layer in new uint[] { 101U, 307U, 701U })
        {
            var key = OutsideDropletRandom.GetEmitterKey(4197U, layer, 2U);
            var birth = OutsideDropletRainTiming.GetAppearanceTimeSeconds(key, start, duration);
            var before = OutsideDropletMotion.EvaluateRainLifecycle(MathF.BitDecrement(birth), key, speed, 1f, start, duration);
            Check(before.Visibility == 0f && before.Progress == 0f && before.Phase == OutsideDropletPhase.Hidden,
                "未出現の落下滴を途中落下状態で描いてはいけません。");
            var atBirth = OutsideDropletMotion.EvaluateRainLifecycle(birth, key, speed, 1f, start, duration);
            Check(atBirth.IsWaitingAtStart && atBirth.Progress == 0f && atBirth.CycleIndex == 0,
                "初回出現は周期途中ではなく待機から始まる必要があります。");
            var wait = OutsideDropletMotion.GetVisibleWaitDurationSeconds(key, speed, 1f);
            var waiting = OutsideDropletMotion.EvaluateRainLifecycle(birth + wait * 0.5f, key, speed, 1f, start, duration);
            Check(waiting.IsWaitingAtStart && waiting.Visibility > 0f && waiting.Progress == 0f,
                "出現した滴は既存の待機時間を経て落下する必要があります。");
            var fall = OutsideDropletMotion.GetFallDurationSeconds(speed, 1f);
            var falling = OutsideDropletMotion.EvaluateRainLifecycle(birth + wait + fall * 0.25f, key, speed, 1f, start, duration);
            Check(falling.IsFalling && MathF.Abs(falling.Progress - 0.25f) < 0.0001f,
                "待機後の落下進行度は出現時刻を原点にする必要があります。");
            var cycle = OutsideDropletMotion.GetCycleDurationSeconds(key, speed, 1f);
            var next = OutsideDropletMotion.EvaluateRainLifecycle(birth + cycle + wait * 0.5f, key, speed, 1f, start, duration);
            Check(next.CycleIndex == 1 && next.IsWaitingAtStart && next.Visibility > 0f,
                "次周期では初回出現を再度待たずに既存の周期を続ける必要があります。");
        }

        var random = new Random(9137);
        var requests = Enumerable.Range(0, 1000).Select(index => (
            Key: OutsideDropletRandom.GetEmitterKey((uint)random.Next(10000), 307U, (uint)(index % 6)),
            Time: random.Next(0, 18000) / 60f,
            Start: index % 3 == 0 ? 12f : 0f,
            Duration: index % 4 == 0 ? 0f : 5f,
            Speed: new[] { 0.25f, 1f, 4f }[index % 3])).ToArray();
        var expected = requests.Select(request => OutsideDropletMotion.EvaluateRainLifecycle(
            request.Time, request.Key, request.Speed, 1f, request.Start, request.Duration)).ToArray();
        for (var index = requests.Length - 1; index >= 0; index--)
        {
            var request = requests[index];
            var actual = OutsideDropletMotion.EvaluateRainLifecycle(
                request.Time, request.Key, request.Speed, 1f, request.Start, request.Duration);
            Check(actual == expected[index], "1000回の逆順シークで出現と運動が変わってはいけません。");
        }
    }

    public static void VerifyGpuLayoutAndRouting()
    {
        Check(Marshal.SizeOf<GlassCompositeConstants>() == 240 && Marshal.SizeOf<GlassCompositeGpuConstants>() == 61968,
            "出現設定はcore 240バイトと最大72 head対応wrapper 61,968バイトを維持する必要があります。");
        var fields = new[]
        {
            (Name: nameof(GlassCompositeConstants.OutsideDropletRainEnabled), Offset: 108, Shader: "outsideDropletRainEnabled", Register: "c6.w"),
            (Name: nameof(GlassCompositeConstants.OutsideDropletRainStartSeconds), Offset: 124, Shader: "outsideDropletRainStartSeconds", Register: "c7.w"),
            (Name: nameof(GlassCompositeConstants.OutsideDropletRainDurationSeconds), Offset: 140, Shader: "outsideDropletRainDurationSeconds", Register: "c8.w"),
        };
        var root = FindRepositoryRoot();
        var shader = ShaderVerificationSource.ReadAllText(Path.Combine(root, "src", "YMM4GlassWipe", "Shaders", "GlassComposite.hlsl"));
        var bridge = File.ReadAllText(Path.Combine(root, "src", "YMM4GlassWipe", "GlassCompositeCustomEffect.cs"));
        foreach (var field in fields)
        {
            Check(Marshal.OffsetOf<GlassCompositeConstants>(field.Name).ToInt32() == field.Offset,
                "出現設定のC# offsetは旧Padding領域と一致する必要があります。");
            Check(shader.Contains($"float {field.Shader} : packoffset({field.Register});", StringComparison.Ordinal),
                "HLSL側も同じ空き領域へ3設定を格納する必要があります。");
            Check(bridge.Contains($"SetValue((int)EffectImpl.Properties.{field.Name}, parameters.{field.Name});", StringComparison.Ordinal),
                "出現設定を両合成passへ渡す必要があります。");
        }
        var birthSource = ExtractFunction(shader, "float GetOutsideDropletAppearanceTime(");
        Check(birthSource.Contains("precise float delay", StringComparison.Ordinal) &&
            birthSource.Contains("precise float appearanceTime", StringComparison.Ordinal) &&
            birthSource.Contains("OutsideDropletRandom(key, 9u)", StringComparison.Ordinal),
            "CPU/GPUの境界時刻は専用整数乱数と同じ積・加算順で求める必要があります。");
        var staticSource = ExtractFunction(shader, "float3 EvaluateOutsideDropletLayer(");
        Check(staticSource.Contains("active *= GetOutsideDropletAppearanceVisibility(cellKey);", StringComparison.Ordinal),
            "静止滴の被覆と輪郭にも初回出現を適用する必要があります。");
        var lifecycle = ExtractFunction(shader, "float4 EvaluateOutsideDropletLifecycle(");
        Check(lifecycle.Contains("outsideDropletRainEnabled >= 0.5f", StringComparison.Ordinal) &&
            lifecycle.Contains("absoluteCycleTime = outsideDropletLocalTimeSeconds - appearanceTime;", StringComparison.Ordinal) &&
            lifecycle.Contains("outsideDropletLocalTimeSeconds < appearanceTime", StringComparison.Ordinal),
            "通常描画と合体失敗時の落下も、出現時刻から待機を始める必要があります。");
    }

    private static string ExtractFunction(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0) throw new InvalidOperationException($"HLSL関数がありません: {signature}");
        var opening = source.IndexOf('{', start);
        var depth = 0;
        for (var index = opening; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            if (source[index] == '}' && --depth == 0) return source[start..(index + 1)];
        }
        throw new InvalidOperationException("HLSL関数の終端がありません。");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "YMM4GlassWipe.sln"))) return directory.FullName;
        throw new InvalidOperationException("リポジトリルートがありません。");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
