// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace YMM4GlassWipe;

internal static class PreviewUpdateTrace
{
#if DEBUG
    private const int MaximumRecordCount = 32_768;
    private static readonly string TraceFilePath =
        Path.Combine(Path.GetTempPath(), "YMM4GlassWipe", "diagnostics", "preview-update-trace.tsv");

    private static TraceState? _state;
    private static long _nextRecordIndex;
    private static readonly object InputIdentityGate = new();
    private static readonly Dictionary<nint, int> InputIdentityIds = new();
    private static int _nextInputIdentityId;
    private static int _disabled;
#endif

#if DEBUG
    public static int GetInputIdentityId(
        Vortice.Direct2D1.ID2D1Image? input)
    {
        if (input is null)
        {
            return 0;
        }

        try
        {
            var nativePointer = input.NativePointer;
            if (nativePointer == nint.Zero)
            {
                return -1;
            }

            lock (InputIdentityGate)
            {
                if (InputIdentityIds.TryGetValue(nativePointer, out var inputId))
                {
                    return inputId;
                }

                if (_nextInputIdentityId >= MaximumRecordCount)
                {
                    return -1;
                }

                inputId = ++_nextInputIdentityId;
                InputIdentityIds.Add(nativePointer, inputId);
                return inputId;
            }
        }
        catch
        {
            // 診断用の識別に失敗しても、YMM4の描画を継続する。
            return -1;
        }
    }
#endif

    [Conditional("DEBUG")]
    public static void Record(
        PreviewTraceEvent traceEvent,
        int processorId = 0,
        int resourceId = 0,
        long updateSequence = 0,
        long frame = -1,
        float localTimeSeconds = float.NaN,
        float fallEnabled = float.NaN,
        float seed = float.NaN,
        float inputLeft = float.NaN,
        float inputTop = float.NaN,
        float inputWidth = float.NaN,
        float inputHeight = float.NaN,
        int inputIdentityId = 0,
        int inputSetSequence = 0,
        int detail = 0)
    {
#if DEBUG
        try
        {
            if (Volatile.Read(ref _disabled) != 0)
            {
                return;
            }

            var state = GetOrCreateState();
            var recordIndex = Interlocked.Increment(ref _nextRecordIndex) - 1;
            if ((ulong)recordIndex >= MaximumRecordCount)
            {
                return;
            }

            state.Records[recordIndex] = new TraceRecord(
                Stopwatch.GetTimestamp(),
                Environment.CurrentManagedThreadId,
                processorId,
                resourceId,
                traceEvent,
                updateSequence,
                frame,
                localTimeSeconds,
                fallEnabled,
                seed,
                inputLeft,
                inputTop,
                inputWidth,
                inputHeight,
                inputIdentityId,
                inputSetSequence,
                detail);
            Volatile.Write(ref state.PublishedRecords[recordIndex], 1);
        }
        catch
        {
            Volatile.Write(ref _disabled, 1);
        }
#endif
    }

    [Conditional("DEBUG")]
    public static void Flush()
    {
#if DEBUG
        var state = Volatile.Read(ref _state);
        if (state is null)
        {
            return;
        }

        try
        {
            lock (state.FlushGate)
            {
                var observedCount = Volatile.Read(ref _nextRecordIndex);
                var recordCount = (int)Math.Min(observedCount, MaximumRecordCount);
                var builder = new StringBuilder(Math.Max(recordCount, 1) * 160);
                builder.Append("process_id\tstopwatch_frequency\trecorded\tdropped")
                    .AppendLine();
                builder.Append(Environment.ProcessId)
                    .Append('\t')
                    .Append(Stopwatch.Frequency)
                    .Append('\t')
                    .Append(recordCount)
                    .Append('\t')
                    .Append(Math.Max(observedCount - MaximumRecordCount, 0))
                    .AppendLine();
                builder.Append(
                    "timestamp\tthread_id\tprocessor_id\tresource_id\tevent\tupdate_sequence\tframe\tlocal_time_seconds\tfall_enabled\tseed\tinput_left\tinput_top\tinput_width\tinput_height\tinput_identity_id\tinput_set_sequence\tdetail")
                    .AppendLine();

                for (var i = 0; i < recordCount; i++)
                {
                    if (Volatile.Read(ref state.PublishedRecords[i]) == 0)
                    {
                        continue;
                    }

                    var record = state.Records[i];
                    builder.Append(record.Timestamp)
                        .Append('\t')
                        .Append(record.ThreadId)
                        .Append('\t')
                        .Append(record.ProcessorId)
                        .Append('\t')
                        .Append(record.ResourceId)
                        .Append('\t')
                        .Append(record.TraceEvent)
                        .Append('\t')
                        .Append(record.UpdateSequence)
                        .Append('\t')
                        .Append(record.Frame)
                        .Append('\t');
                    AppendFloat(builder, record.LocalTimeSeconds);
                    builder.Append('\t');
                    AppendFloat(builder, record.FallEnabled);
                    builder.Append('\t');
                    AppendFloat(builder, record.Seed);
                    builder.Append('\t');
                    AppendFloat(builder, record.InputLeft);
                    builder.Append('\t');
                    AppendFloat(builder, record.InputTop);
                    builder.Append('\t');
                    AppendFloat(builder, record.InputWidth);
                    builder.Append('\t');
                    AppendFloat(builder, record.InputHeight);
                    builder.Append('\t')
                        .Append(record.InputIdentityId)
                        .Append('\t')
                        .Append(record.InputSetSequence)
                        .Append('\t')
                        .Append(record.Detail)
                        .AppendLine();
                }

                var traceDirectory = Path.GetDirectoryName(TraceFilePath)!;
                var temporaryPath = TraceFilePath +
                    "." +
                    Environment.ProcessId.ToString(CultureInfo.InvariantCulture) +
                    "." +
                    Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) +
                    ".tmp";
                Directory.CreateDirectory(traceDirectory);
                File.WriteAllText(
                    temporaryPath,
                    builder.ToString(),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(temporaryPath, TraceFilePath, overwrite: true);
            }
        }
        catch
        {
            // 診断ログの失敗でYMM4の描画や終了処理を妨げない。
            Volatile.Write(ref _disabled, 1);
        }
#endif
    }

#if DEBUG
    private static TraceState GetOrCreateState()
    {
        var state = Volatile.Read(ref _state);
        if (state is not null)
        {
            return state;
        }

        var createdState = new TraceState();
        state = Interlocked.CompareExchange(ref _state, createdState, null);
        if (state is not null)
        {
            return state;
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();
        return createdState;
    }

    private static void AppendFloat(StringBuilder builder, float value) =>
        builder.Append(value.ToString("R", CultureInfo.InvariantCulture));

    private sealed class TraceState
    {
        public TraceRecord[] Records { get; } = new TraceRecord[MaximumRecordCount];

        public int[] PublishedRecords { get; } = new int[MaximumRecordCount];

        public object FlushGate { get; } = new();
    }

    private readonly record struct TraceRecord(
        long Timestamp,
        int ThreadId,
        int ProcessorId,
        int ResourceId,
        PreviewTraceEvent TraceEvent,
        long UpdateSequence,
        long Frame,
        float LocalTimeSeconds,
        float FallEnabled,
        float Seed,
        float InputLeft,
        float InputTop,
        float InputWidth,
        float InputHeight,
        int InputIdentityId,
        int InputSetSequence,
        int Detail);
#endif
}

internal enum PreviewTraceEvent
{
    ProcessorCreated,
    SetInput,
    ClearInput,
    OutputRead,
    UpdateEnter,
    ParametersResolved,
    UpdateExit,
    UpdateFailed,
    ResourcesDisabled,
    ResourcesRecovered,
    ProcessorDisposeStart,
    ProcessorDisposeEnd,
    ResourceNoInput,
    ResourceFullApply,
    ResourceTimeApply,
    ResourceNoParameterApply,
#if DEBUG
    ResourceCompositeCacheApply,
    ResourceCompositeCacheRestore,
    ResourceCompositeCacheFailed,
    ResourceCompositeMaskInputBypassApply,
    ResourceCompositeMaskInputBypassRestore,
    ResourceCompositeMaskInputBypassFailed,
#endif
    ResourceDisposeStart,
    ResourceDisposeEnd,
}
