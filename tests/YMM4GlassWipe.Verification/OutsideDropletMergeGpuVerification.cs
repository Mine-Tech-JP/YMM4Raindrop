// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using System.Runtime.InteropServices;
using YMM4GlassWipe;

internal static class OutsideDropletMergeGpuVerification
{
    public static void VerifyGpuConstantsAndSourceWiring()
    {
        Equal(240, Marshal.SizeOf<GlassCompositeConstants>(),
            "core定数バッファは既存の15レジスタ・240バイトを維持する必要があります。");
        Equal(188, OffsetOf<GlassCompositeConstants>(
                nameof(GlassCompositeConstants.OutsideDropletMergeEnabled)),
            "水滴合体フラグは既存Padding6のc11.wへ置く必要があります。");
        Equal(61968, Marshal.SizeOf<GlassCompositeGpuConstants>(),
            "GPU定数バッファはc0からc3872までの61,968バイトである必要があります。");
        Equal(0, OffsetOf<GlassCompositeGpuConstants>(
                nameof(GlassCompositeGpuConstants.Core)),
            "GPU定数バッファのcoreはc0から始まる必要があります。");
        Equal(240, OffsetOf<GlassCompositeGpuConstants>(
                nameof(GlassCompositeGpuConstants.MergeHeadsA)),
            "落下滴head Aはc15から始まる必要があります。");
        Equal(1392, OffsetOf<GlassCompositeGpuConstants>(
                nameof(GlassCompositeGpuConstants.MergeHeadsB)),
            "落下滴head Bはc87から始まる必要があります。");
        Equal(2544, OffsetOf<GlassCompositeGpuConstants>(
                nameof(GlassCompositeGpuConstants.MergeStaticSlots)),
            "静止滴補正tableはc159から始まる必要があります。");

        Equal(35312, OffsetOf<GlassCompositeGpuConstants>(nameof(GlassCompositeGpuConstants.InitialStaticColumns)), "初期列数はc2207です。");
        Equal(35328, OffsetOf<GlassCompositeGpuConstants>(nameof(GlassCompositeGpuConstants.InitialStaticCounts)), "初期セル数はc2208です。");
        Equal(35344, OffsetOf<GlassCompositeGpuConstants>(nameof(GlassCompositeGpuConstants.InitialStaticStates)), "初期2bit状態はc2209です。");

        var repositoryRoot = FindRepositoryRoot();
        var source = RemoveWhitespace(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "YMM4GlassWipe",
            "GlassCompositeCustomEffect.cs")));
        True(source.Contains(
                "SetValue((int)EffectImpl.Properties.OutsideDropletMergeEnabled,parameters.OutsideDropletMergeEnabled);",
                StringComparison.Ordinal),
            "全パラメータ適用の最後に水滴合体フラグを渡す必要があります。");
        var suspendMerge = source.IndexOf(
            "SetValue((int)EffectImpl.Properties.OutsideDropletMergeEnabled,0f);",
            StringComparison.Ordinal);
        var firstParameter = source.IndexOf(
            "SetValue((int)EffectImpl.Properties.InputWidth,inputWidth);",
            StringComparison.Ordinal);
        var applyTime = source.IndexOf(
            "SetValue((int)EffectImpl.Properties.OutsideDropletLocalTimeSeconds,parameters.OutsideDropletLocalTimeSeconds);",
            StringComparison.Ordinal);
        var resumeMerge = source.IndexOf(
            "SetValue((int)EffectImpl.Properties.OutsideDropletMergeEnabled,parameters.OutsideDropletMergeEnabled);",
            StringComparison.Ordinal);
        True(suspendMerge >= 0 && suspendMerge < firstParameter &&
            firstParameter < applyTime && applyTime < resumeMerge,
            "全設定の適用中は合体を止め、時刻を含む定数が揃ってから一度だけ評価する必要があります。");
        True(source.Contains(
                "set=>SetOutsideDropletLocalTimeSeconds(value);",
                StringComparison.Ordinal) &&
            source.Contains(
                "RebuildMergeConstants();",
                StringComparison.Ordinal),
            "時刻単独更新でも水滴合体scheduleを再評価する必要があります。");
        True(source.Contains(
                "drawInformation?.SetPixelShaderConstantBuffer(_gpuConstantBuffer);",
                StringComparison.Ordinal),
            "GPUにはwrapper定数バッファを一括転送する必要があります。");
        True(source.Contains(
                "catch(Exceptionexception)",
                StringComparison.Ordinal) &&
            source.Contains(
                "exception.GetType().Name",
                StringComparison.Ordinal) &&
            source.Contains(
                "Debug.WriteLine",
                StringComparison.Ordinal) &&
            source.Contains(
                "Interlocked.Exchange(ref_mergeFailureDiagnosticRecorded,0);",
                StringComparison.Ordinal),
            "想定外例外は型名だけを一度DEBUG診断し、成功時に診断状態を戻す必要があります。");

        var shader = RemoveWhitespace(ShaderVerificationSource.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl")));
        True(shader.Contains(
                "float4outsideDropletMergeHeadsA[72]:packoffset(c15);",
                StringComparison.Ordinal) &&
            shader.Contains(
                "float4outsideDropletMergeHeadsB[72]:packoffset(c87);",
                StringComparison.Ordinal) &&
            shader.Contains(
                "float4outsideDropletMergeStaticSlots[2048]:packoffset(c159);",
                StringComparison.Ordinal),
            "HLSLのmerge配列はwrapper定数バッファのc15/c87/c159と一致する必要があります。");
        True(shader.Contains("float4outsideDropletInitialColumns:packoffset(c2207);", StringComparison.Ordinal) &&
            shader.Contains("float4outsideDropletInitialCounts:packoffset(c2208);", StringComparison.Ordinal) &&
            shader.Contains("uint4outsideDropletInitialStates[1664]:packoffset(c2209);", StringComparison.Ordinal),
            "HLSLの初期状態配列もCPU配置と一致する必要があります。");
        True(shader.Contains("cellKey=layerOffset+(uint)cell.y*columns+(uint)cell.x;", StringComparison.Ordinal) &&
            shader.Contains("radius*=mergeState.y*lerp(0.08f,1.0f,mergeVisibility);", StringComparison.Ordinal),
            "一意セル添字と、静止滴の成長・全体縮小を描画へ反映する必要があります。");
        True(shader.Contains(
                "float4head=outsideDropletMergeHeadsA[emitterIndex];",
                StringComparison.Ordinal) &&
            shader.Contains(
                "float4state=outsideDropletMergeHeadsB[emitterIndex];",
                StringComparison.Ordinal) &&
            shader.Contains(
                "state.xy,head.z,head.w,lifecycle",
                StringComparison.Ordinal),
            "HLSLはhead A/BのUV・半径・縦倍率・状態を対応する成分から使用する必要があります。");
        True(shader.Contains(
                "uintslotIndex=cellKey&2047u;",
                StringComparison.Ordinal) &&
            shader.Contains(
                "for(uintprobe=0u;probe<32u;probe++)",
                StringComparison.Ordinal),
            "HLSLの静止滴lookupは2048slot・最大32probeのCPU側契約と一致する必要があります。");
    }

    public static void VerifyStaticSlotTransport()
    {
        VerifyInitialLayoutTransport();
        var highBitKey = 0xf1230000u;
        var frame = new OutsideDropletMergeFrame(
            isValid: true,
            CreateHeads(),
            new[]
            {
                new OutsideDropletStaticChange(0u, 0u, 0, 0, 0.25f),
                new OutsideDropletStaticChange(highBitKey, 2u, 1, -1, 0.75f),
            });
        var constants = new GlassCompositeGpuConstants();
        True(constants.TryApply(frame),
            "key=0と上位bitを含むkeyは同じ固定tableへ格納できる必要があります。");

        True(TryFindStaticSlot(constants, 0u, out var zeroSlot),
            "key=0はoccupiedフラグにより検索可能である必要があります。");
        NearlyEqual(0.25f, zeroSlot.Z,
            "key=0の静止滴可視度を保持する必要があります。");
        True(TryFindStaticSlot(constants, highBitKey, out var highBitSlot),
            "上位16bitを含むkeyを検索できる必要があります。");
        Equal(0xf123u, (uint)highBitSlot.Y,
            "keyの上位16bitをtableへ保持する必要があります。");
        NearlyEqual(0.75f, highBitSlot.Z,
            "上位bitを含むkeyの可視度を保持する必要があります。");

        var tooManyCollisions = Enumerable.Range(0, 33)
            .Select(index => new OutsideDropletStaticChange(
                (uint)(index << 11),
                0u,
                index,
                0,
                1f))
            .ToArray();
        var collisionFrame = new OutsideDropletMergeFrame(
            isValid: true,
            CreateHeads(),
            tooManyCollisions);
        constants = new GlassCompositeGpuConstants();
        True(!constants.TryApply(collisionFrame),
            "32回の固定probeへ収まらない衝突はmerge全体のfallbackにする必要があります。");

        var invalidFrame = new OutsideDropletMergeFrame(
            isValid: false,
            Array.Empty<OutsideDropletMergeHeadState>(),
            Array.Empty<OutsideDropletStaticChange>());
        True(!constants.TryApply(invalidFrame),
            "solverが不正と判定したframeをGPUへ転送してはいけません。");

        var zeroVerticalScaleHeads = CreateHeads();
        zeroVerticalScaleHeads[0] = zeroVerticalScaleHeads[0] with
        {
            VerticalScale = 0f,
        };
        var zeroVerticalScaleFrame = new OutsideDropletMergeFrame(
            isValid: true,
            zeroVerticalScaleHeads,
            Array.Empty<OutsideDropletStaticChange>());
        True(!constants.TryApply(zeroVerticalScaleFrame),
            "半径が正の落下滴で縦倍率がゼロのframeはGPUへ転送してはいけません。");
    }

    private static void VerifyInitialLayoutTransport()
    {
        True(OutsideDropletStaticLayout.TryCreate(new Vector2(1920, 1080), 0.25f, 1u, 1f, out var layout),
            "最小サイズの初期配置を作成できる必要があります。");
        True(layout.Changes.Count > 2048, "固定lookupを超える初期吸収を検証する必要があります。");
        var constants = new GlassCompositeGpuConstants();
        var frame = new OutsideDropletMergeFrame(true, CreateHeads(), layout.Changes, layout);
        True(constants.TryApply(frame), "多数の初期変更は2bitマスクへ転送できる必要があります。");
        True(constants.InitialStaticColumns == new Vector4(layout.ColumnCounts, 1f) &&
            constants.InitialStaticCounts == new Vector4(layout.CellCounts, 0f), "初期配置の寸法を保持します。");
        for (var i = 0; i < layout.PackedStates.Count; i++)
            Equal(layout.PackedStates[i], constants.InitialStaticStates[i], "全初期状態wordを保持します。");
        foreach (var change in layout.Changes)
        {
            True(layout.TryGetCellIndex(change.Layer, change.CellX, change.CellY, out var index), "変更セルの添字を取得します。");
            var state = (constants.InitialStaticStates[(int)(index / 16)] >> (int)((index % 16) * 2)) & 3u;
            Equal(change.Visibility == 0 ? 1u : 2u, state, "個々のセルの可視性と成長bitを保持します。");
            True(!TryFindStaticSlot(constants, index, out _), "初期同値は動的tableを消費しません。");
        }
        var root = layout.Changes.First(change => change.Visibility == 1f);
        var dynamicChange = root with { Visibility = 0.4f, RadiusScale = 1.2f };
        True(constants.TryApply(new OutsideDropletMergeFrame(true, CreateHeads(), new[] { dynamicChange }, layout)),
            "初期配置の上へ動的な可視性と成長を転送できます。");
        layout.TryGetCellIndex(root.Layer, root.CellX, root.CellY, out var key);
        True(TryFindStaticSlot(constants, key, out var slot), "動的キーはRNG値ではなく一意セル添字です。");
        NearlyEqual(0.4f, slot.Z, "動的可視性を保持します。");
        NearlyEqual(1.2f, slot.W, "動的成長倍率を保持します。");
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, 0f, 1.36f })
            True(!constants.TryApply(new OutsideDropletMergeFrame(true, CreateHeads(),
                new[] { dynamicChange with { RadiusScale = invalid } }, layout)), "不正な成長値は拒否します。");
        True(!constants.TryApply(new OutsideDropletMergeFrame(true, CreateHeads(),
            new[] { dynamicChange with { CellX = int.MaxValue } }, layout)), "範囲外セルを拒否します。");
        True(constants.TryApply(new OutsideDropletMergeFrame(true, CreateHeads(), Array.Empty<OutsideDropletStaticChange>())),
            "初期配置を持たない転送へ戻せます。");
        True(constants.InitialStaticColumns == Vector4.Zero && constants.InitialStaticStates[0] == 0u,
            "初期状態の残留を防ぎます。");
    }

    private static OutsideDropletMergeHeadState[] CreateHeads() =>
        Enumerable.Range(0, GlassCompositeGpuConstants.MergeHeadCount)
            .Select(index => new OutsideDropletMergeHeadState(
                index,
                new Vector2(index / (float)GlassCompositeGpuConstants.MergeHeadCount, 0.25f),
                new Vector2(index / (float)GlassCompositeGpuConstants.MergeHeadCount, 0.125f),
                1f,
                1f,
                1f,
                0.5f))
            .ToArray();

    private static bool TryFindStaticSlot(
        GlassCompositeGpuConstants constants,
        uint cellKey,
        out Vector4 result)
    {
        var keyLow = cellKey & 0xffffu;
        var keyHigh = cellKey >> 16;
        var firstSlot = (int)(cellKey &
            (GlassCompositeGpuConstants.MergeStaticSlotCount - 1));
        for (var probe = 0;
             probe < GlassCompositeGpuConstants.MergeStaticProbeLimit;
             probe++)
        {
            var slotIndex = (firstSlot + probe) &
                (GlassCompositeGpuConstants.MergeStaticSlotCount - 1);
            var slot = constants.MergeStaticSlots[slotIndex];
            if (slot.W < 0.5f)
            {
                break;
            }

            if ((uint)slot.X == keyLow && (uint)slot.Y == keyHigh)
            {
                result = slot;
                return true;
            }
        }

        result = default;
        return false;
    }

    private static int OffsetOf<T>(string fieldName) where T : struct =>
        Marshal.OffsetOf<T>(fieldName).ToInt32();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "YMM4GlassWipe.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("リポジトリルートを特定できません。");
    }

    private static string RemoveWhitespace(string value) =>
        new(value.Where(character => !char.IsWhiteSpace(character)).ToArray());

    private static void NearlyEqual(float expected, float actual, string message)
    {
        if (MathF.Abs(expected - actual) > 0.0001f)
        {
            throw new InvalidOperationException(
                $"{message} 期待値: {expected}, 実際: {actual}");
        }
    }

    private static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message} 期待値: {expected}, 実際: {actual}");
        }
    }
}
