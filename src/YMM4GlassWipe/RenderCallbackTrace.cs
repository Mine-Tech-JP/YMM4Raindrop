// SPDX-License-Identifier: MPL-2.0

#if DEBUG
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Vortice;

namespace YMM4GlassWipe;

internal static class RenderCallbackTrace
{
    private const int MaximumRecordCount = 16_384;
    private static readonly string TraceFilePath =
        Path.Combine(Path.GetTempPath(), "YMM4GlassWipe", "diagnostics", "render-callback-trace.tsv");

    private static TraceState? _state;
    private static long _nextRecordIndex;
    private static int _nextEffectInstanceId;
    private static int _disabled;

    public static int CreateEffectInstanceId() =>
        Interlocked.Increment(ref _nextEffectInstanceId);

    public static void RecordConstants(
        int effectInstanceId,
        long callbackSequence,
        bool drawInformationAvailable,
        in GlassCompositeConstants constants)
    {
        var record = new TraceRecord
        {
            EffectInstanceId = effectInstanceId,
            CallbackSequence = callbackSequence,
            TraceEvent = RenderCallbackTraceEvent.RenderConstants,
            DrawInformationAvailable = drawInformationAvailable,
            Constants = constants,
        };
        Publish(ref record);
    }

    public static void RecordMapInputRectsToOutputRect(
        int effectInstanceId,
        long callbackSequence,
        RawRect[]? inputRects,
        RawRect[]? inputOpaqueSubRects,
        RawRect outputRect,
        RawRect outputOpaqueSubRect)
    {
        var inputCount = inputRects?.Length ?? 0;
        var opaqueCount = inputOpaqueSubRects?.Length ?? 0;
        var record = new TraceRecord
        {
            EffectInstanceId = effectInstanceId,
            CallbackSequence = callbackSequence,
            TraceEvent = RenderCallbackTraceEvent.MapInputRectsToOutputRect,
            InputCount = inputCount,
            InputUnrecordedCount = Math.Max(inputCount - 3, 0),
            OpaqueCount = opaqueCount,
            OpaqueUnrecordedCount = Math.Max(opaqueCount - 3, 0),
            Input0 = GetRect(inputRects, 0),
            Input1 = GetRect(inputRects, 1),
            Input2 = GetRect(inputRects, 2),
            Opaque0 = GetRect(inputOpaqueSubRects, 0),
            Opaque1 = GetRect(inputOpaqueSubRects, 1),
            Opaque2 = GetRect(inputOpaqueSubRects, 2),
            Output = outputRect,
            OutputOpaque = outputOpaqueSubRect,
        };
        Publish(ref record);
    }

    public static void RecordMapOutputRectToInputRects(
        int effectInstanceId,
        long callbackSequence,
        RawRect outputRect,
        RawRect[]? inputRects)
    {
        var inputCount = inputRects?.Length ?? 0;
        var record = new TraceRecord
        {
            EffectInstanceId = effectInstanceId,
            CallbackSequence = callbackSequence,
            TraceEvent = RenderCallbackTraceEvent.MapOutputRectToInputRects,
            InputCount = inputCount,
            InputUnrecordedCount = Math.Max(inputCount - 3, 0),
            Input0 = GetRect(inputRects, 0),
            Input1 = GetRect(inputRects, 1),
            Input2 = GetRect(inputRects, 2),
            Output = outputRect,
        };
        Publish(ref record);
    }

    [Conditional("DEBUG")]
    public static void Flush()
    {
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
                var constantFields = GetConstantFields();
                var builder = new StringBuilder(Math.Max(recordCount, 1) * 768);
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
                AppendHeader(builder, constantFields);

                for (var i = 0; i < recordCount; i++)
                {
                    if (Volatile.Read(ref state.PublishedRecords[i]) == 0)
                    {
                        continue;
                    }

                    AppendRecord(builder, state.Records[i], constantFields.Length);
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
    }

    private static void Publish(ref TraceRecord record)
    {
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

            record.Timestamp = Stopwatch.GetTimestamp();
            record.ThreadId = Environment.CurrentManagedThreadId;
            state.Records[recordIndex] = record;
            Volatile.Write(ref state.PublishedRecords[recordIndex], 1);
        }
        catch
        {
            Volatile.Write(ref _disabled, 1);
        }
    }

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

    private static RawRect GetRect(RawRect[]? rects, int index) =>
        rects is not null && (uint)index < (uint)rects.Length
            ? rects[index]
            : default;

    private static FieldInfo[] GetConstantFields()
    {
        var fields = typeof(GlassCompositeConstants)
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(field =>
                Marshal.OffsetOf<GlassCompositeConstants>(field.Name).ToInt32())
            .ToArray();
        if (fields.Length * sizeof(float) != Marshal.SizeOf<GlassCompositeConstants>())
        {
            throw new InvalidOperationException(
                "定数バッファをfloat列として記録できません。");
        }

        return fields;
    }

    private static void AppendHeader(
        StringBuilder builder,
        IReadOnlyList<FieldInfo> constantFields)
    {
        builder.Append(
            "timestamp\tthread_id\teffect_instance_id\tcallback_sequence\tevent\tdraw_information_available\tinput_count\tinput_unrecorded_count\topaque_count\topaque_unrecorded_count")
            .Append(
                "\tinput0_left\tinput0_top\tinput0_right\tinput0_bottom\tinput1_left\tinput1_top\tinput1_right\tinput1_bottom\tinput2_left\tinput2_top\tinput2_right\tinput2_bottom")
            .Append(
                "\topaque0_left\topaque0_top\topaque0_right\topaque0_bottom\topaque1_left\topaque1_top\topaque1_right\topaque1_bottom\topaque2_left\topaque2_top\topaque2_right\topaque2_bottom")
            .Append(
                "\toutput_left\toutput_top\toutput_right\toutput_bottom\toutput_opaque_left\toutput_opaque_top\toutput_opaque_right\toutput_opaque_bottom");
        foreach (var field in constantFields)
        {
            builder.Append("\tconstant_")
                .Append(field.Name)
                .Append("_bits");
        }

        builder.AppendLine();
    }

    private static void AppendRecord(
        StringBuilder builder,
        TraceRecord record,
        int constantFieldCount)
    {
        builder.Append(record.Timestamp)
            .Append('\t')
            .Append(record.ThreadId)
            .Append('\t')
            .Append(record.EffectInstanceId)
            .Append('\t')
            .Append(record.CallbackSequence)
            .Append('\t')
            .Append(record.TraceEvent)
            .Append('\t')
            .Append(record.DrawInformationAvailable ? 1 : 0)
            .Append('\t')
            .Append(record.InputCount)
            .Append('\t')
            .Append(record.InputUnrecordedCount)
            .Append('\t')
            .Append(record.OpaqueCount)
            .Append('\t')
            .Append(record.OpaqueUnrecordedCount);
        AppendRect(builder, record.Input0);
        AppendRect(builder, record.Input1);
        AppendRect(builder, record.Input2);
        AppendRect(builder, record.Opaque0);
        AppendRect(builder, record.Opaque1);
        AppendRect(builder, record.Opaque2);
        AppendRect(builder, record.Output);
        AppendRect(builder, record.OutputOpaque);
        AppendConstants(builder, record.Constants, constantFieldCount);
        builder.AppendLine();
    }

    private static void AppendRect(StringBuilder builder, RawRect rect)
    {
        builder.Append('\t')
            .Append(rect.Left)
            .Append('\t')
            .Append(rect.Top)
            .Append('\t')
            .Append(rect.Right)
            .Append('\t')
            .Append(rect.Bottom);
    }

    private static void AppendConstants(
        StringBuilder builder,
        GlassCompositeConstants constants,
        int constantFieldCount)
    {
        var constantsSpan = MemoryMarshal.CreateReadOnlySpan(ref constants, 1);
        var floatValues =
            MemoryMarshal.Cast<GlassCompositeConstants, float>(constantsSpan);
        if (floatValues.Length != constantFieldCount)
        {
            throw new InvalidOperationException(
                "定数バッファの列数がヘッダーと一致しません。");
        }

        foreach (var value in floatValues)
        {
            builder.Append('\t')
                .Append(
                    unchecked((uint)BitConverter.SingleToInt32Bits(value))
                        .ToString("X8", CultureInfo.InvariantCulture));
        }
    }

    private sealed class TraceState
    {
        public TraceRecord[] Records { get; } =
            new TraceRecord[MaximumRecordCount];

        public int[] PublishedRecords { get; } =
            new int[MaximumRecordCount];

        public object FlushGate { get; } = new();
    }

    private struct TraceRecord
    {
        public long Timestamp;
        public int ThreadId;
        public int EffectInstanceId;
        public long CallbackSequence;
        public RenderCallbackTraceEvent TraceEvent;
        public bool DrawInformationAvailable;
        public int InputCount;
        public int InputUnrecordedCount;
        public int OpaqueCount;
        public int OpaqueUnrecordedCount;
        public RawRect Input0;
        public RawRect Input1;
        public RawRect Input2;
        public RawRect Opaque0;
        public RawRect Opaque1;
        public RawRect Opaque2;
        public RawRect Output;
        public RawRect OutputOpaque;
        public GlassCompositeConstants Constants;
    }
}

internal enum RenderCallbackTraceEvent
{
    RenderConstants,
    MapInputRectsToOutputRect,
    MapOutputRectToInputRects,
}
#endif
