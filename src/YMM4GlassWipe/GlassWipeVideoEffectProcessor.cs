// SPDX-License-Identifier: MPL-2.0

using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YMM4GlassWipe;

internal sealed class GlassWipeVideoEffectProcessor : IVideoEffectProcessor
{
#if DEBUG
    private static int _nextDiagnosticProcessorId;
    private readonly int _diagnosticProcessorId =
        Interlocked.Increment(ref _nextDiagnosticProcessorId);
    private long _diagnosticUpdateSequence;
    private long _diagnosticLastFrame = -1;
    private float _diagnosticLastLocalTimeSeconds = float.NaN;
    private float _diagnosticLastFallEnabled = float.NaN;
    private float _diagnosticLastSeed = float.NaN;
    private readonly object _diagnosticInputStateGate = new();
    private DiagnosticInputState _diagnosticInputState = new(0, 0);
#endif

    private readonly IGraphicsDevicesAndContext _devices;
    private readonly GlassWipeVideoEffect _item;
    private GlassWipeEffectResources? _resources;
    private ID2D1Image? _input;
    private GlassWipeBrushShape _failedBrushShape;
    private Guid _failedUserBrushId;
    private long _failedUserBrushRevision = -1;
    private bool _canRetryUserBrush;
    private bool _disposed;

    public GlassWipeVideoEffectProcessor(
        IGraphicsDevicesAndContext devices,
        GlassWipeVideoEffect item)
    {
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(item);

        _devices = devices;
        _item = item;
        _resources = GlassWipeEffectResources.TryCreate(devices);
#if DEBUG
        PreviewUpdateTrace.Record(
            PreviewTraceEvent.ProcessorCreated,
            _diagnosticProcessorId,
            _resources?.DiagnosticId ?? 0);
#endif
    }

    public ID2D1Image Output
    {
        get
        {
#if DEBUG
            var diagnosticInputState = ReadDiagnosticInputState();
            PreviewUpdateTrace.Record(
                PreviewTraceEvent.OutputRead,
                _diagnosticProcessorId,
                _resources?.DiagnosticId ?? 0,
                Volatile.Read(ref _diagnosticUpdateSequence),
                Volatile.Read(ref _diagnosticLastFrame),
                Volatile.Read(ref _diagnosticLastLocalTimeSeconds),
                Volatile.Read(ref _diagnosticLastFallEnabled),
                Volatile.Read(ref _diagnosticLastSeed),
                inputIdentityId: diagnosticInputState.IdentityId,
                inputSetSequence: diagnosticInputState.SetSequence);
#endif
            return _resources?.Output ??
                _input ??
                throw new InvalidOperationException("入力画像が設定されていません。");
        }
    }

    public void SetInput(ID2D1Image? input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _input = input;
#if DEBUG
        var diagnosticInputIdentityId =
            PreviewUpdateTrace.GetInputIdentityId(input);
        var diagnosticInputState =
            UpdateDiagnosticInputState(diagnosticInputIdentityId);
        PreviewUpdateTrace.Record(
            PreviewTraceEvent.SetInput,
            _diagnosticProcessorId,
            _resources?.DiagnosticId ?? 0,
            Volatile.Read(ref _diagnosticUpdateSequence),
            Volatile.Read(ref _diagnosticLastFrame),
            inputIdentityId: diagnosticInputState.IdentityId,
            inputSetSequence: diagnosticInputState.SetSequence,
            detail: input is null ? 0 : 1);
#endif

        if (_resources is null)
        {
            return;
        }

        try
        {
            _resources.SetInput(input);
        }
        catch
        {
            DisableResources();
        }
    }

    public void ClearInput()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _input = null;
#if DEBUG
        var diagnosticInputState = UpdateDiagnosticInputState(0);
        PreviewUpdateTrace.Record(
            PreviewTraceEvent.ClearInput,
            _diagnosticProcessorId,
            _resources?.DiagnosticId ?? 0,
            Volatile.Read(ref _diagnosticUpdateSequence),
            Volatile.Read(ref _diagnosticLastFrame),
            inputIdentityId: diagnosticInputState.IdentityId,
            inputSetSequence: diagnosticInputState.SetSequence);
#endif

        if (_resources is null)
        {
            return;
        }

        try
        {
            _resources.SetInput(null);
        }
        catch
        {
            DisableResources();
        }
    }

    public DrawDescription Update(EffectDescription effectDescription)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(effectDescription);

#if DEBUG
        var diagnosticUpdateSequence = Interlocked.Increment(
            ref _diagnosticUpdateSequence);
        var diagnosticFrame = (long)effectDescription.ItemPosition.Frame;
        Volatile.Write(ref _diagnosticLastFrame, diagnosticFrame);
        var diagnosticInputState = ReadDiagnosticInputState();
        PreviewUpdateTrace.Record(
            PreviewTraceEvent.UpdateEnter,
            _diagnosticProcessorId,
            _resources?.DiagnosticId ?? 0,
            diagnosticUpdateSequence,
            diagnosticFrame,
            inputIdentityId: diagnosticInputState.IdentityId,
            inputSetSequence: diagnosticInputState.SetSequence);
#endif

        TryRecoverUserBrushResources();
        if (_resources is not null)
        {
            try
            {
                var parameters = GlassWipeParameters.Create(_item, effectDescription);
#if DEBUG
                Volatile.Write(
                    ref _diagnosticLastLocalTimeSeconds,
                    parameters.OutsideDropletLocalTimeSeconds);
                Volatile.Write(
                    ref _diagnosticLastFallEnabled,
                    parameters.OutsideDropletFallEnabled);
                Volatile.Write(
                    ref _diagnosticLastSeed,
                    parameters.OutsideDropletSeed);
                diagnosticInputState = ReadDiagnosticInputState();
                PreviewUpdateTrace.Record(
                    PreviewTraceEvent.ParametersResolved,
                    _diagnosticProcessorId,
                    _resources.DiagnosticId,
                    diagnosticUpdateSequence,
                    diagnosticFrame,
                    parameters.OutsideDropletLocalTimeSeconds,
                    parameters.OutsideDropletFallEnabled,
                    parameters.OutsideDropletSeed,
                    inputIdentityId: diagnosticInputState.IdentityId,
                    inputSetSequence: diagnosticInputState.SetSequence);
#endif
                _resources.Update(
                    parameters,
                    geometry => WipePathStream.Create(
                        _item,
                        effectDescription,
                        geometry));
            }
#if DEBUG
            catch (Exception exception)
#else
            catch
#endif
            {
#if DEBUG
                diagnosticInputState = ReadDiagnosticInputState();
                PreviewUpdateTrace.Record(
                    PreviewTraceEvent.UpdateFailed,
                    _diagnosticProcessorId,
                    _resources?.DiagnosticId ?? 0,
                    diagnosticUpdateSequence,
                    diagnosticFrame,
                    Volatile.Read(ref _diagnosticLastLocalTimeSeconds),
                    Volatile.Read(ref _diagnosticLastFallEnabled),
                    Volatile.Read(ref _diagnosticLastSeed),
                    inputIdentityId: diagnosticInputState.IdentityId,
                    inputSetSequence: diagnosticInputState.SetSequence,
                    detail: exception.HResult);
#endif
                RecordUserBrushFailure();
                DisableResources();
            }
        }

#if DEBUG
        diagnosticInputState = ReadDiagnosticInputState();
        PreviewUpdateTrace.Record(
            PreviewTraceEvent.UpdateExit,
            _diagnosticProcessorId,
            _resources?.DiagnosticId ?? 0,
            diagnosticUpdateSequence,
            diagnosticFrame,
            Volatile.Read(ref _diagnosticLastLocalTimeSeconds),
            Volatile.Read(ref _diagnosticLastFallEnabled),
            Volatile.Read(ref _diagnosticLastSeed),
            inputIdentityId: diagnosticInputState.IdentityId,
            inputSetSequence: diagnosticInputState.SetSequence);
#endif
        return effectDescription.DrawDescription;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

#if DEBUG
        var diagnosticInputState = ReadDiagnosticInputState();
        PreviewUpdateTrace.Record(
            PreviewTraceEvent.ProcessorDisposeStart,
            _diagnosticProcessorId,
            _resources?.DiagnosticId ?? 0,
            Volatile.Read(ref _diagnosticUpdateSequence),
            Volatile.Read(ref _diagnosticLastFrame),
            inputIdentityId: diagnosticInputState.IdentityId,
            inputSetSequence: diagnosticInputState.SetSequence);
#endif
        _disposed = true;
        _input = null;
        DisableResources();
#if DEBUG
        diagnosticInputState = ReadDiagnosticInputState();
        PreviewUpdateTrace.Record(
            PreviewTraceEvent.ProcessorDisposeEnd,
            _diagnosticProcessorId,
            updateSequence: Volatile.Read(ref _diagnosticUpdateSequence),
            frame: Volatile.Read(ref _diagnosticLastFrame),
            inputIdentityId: diagnosticInputState.IdentityId,
            inputSetSequence: diagnosticInputState.SetSequence);
        PreviewUpdateTrace.Flush();
        RenderCallbackTrace.Flush();
#endif
    }

    private void DisableResources()
    {
        var resources = _resources;
#if DEBUG
        var diagnosticInputState = ReadDiagnosticInputState();
        PreviewUpdateTrace.Record(
            PreviewTraceEvent.ResourcesDisabled,
            _diagnosticProcessorId,
            resources?.DiagnosticId ?? 0,
            Volatile.Read(ref _diagnosticUpdateSequence),
            Volatile.Read(ref _diagnosticLastFrame),
            inputIdentityId: diagnosticInputState.IdentityId,
            inputSetSequence: diagnosticInputState.SetSequence);
#endif
        _resources = null;
        resources?.Dispose();
    }

    private void RecordUserBrushFailure()
    {
        _canRetryUserBrush = _item.BrushShape == GlassWipeBrushShape.UserImage;
        _failedBrushShape = _item.BrushShape;
        _failedUserBrushId = _item.UserBrushId;
        _failedUserBrushRevision = _item.UserBrushRevision;
    }

    private void TryRecoverUserBrushResources()
    {
        if (_resources is not null ||
            !_canRetryUserBrush ||
            _item.BrushShape == _failedBrushShape &&
                (_item.BrushShape != GlassWipeBrushShape.UserImage ||
                 _item.UserBrushId == _failedUserBrushId &&
                    _item.UserBrushRevision == _failedUserBrushRevision))
        {
            return;
        }

        _failedBrushShape = _item.BrushShape;
        _failedUserBrushId = _item.UserBrushId;
        _failedUserBrushRevision = _item.UserBrushRevision;
        var resources = GlassWipeEffectResources.TryCreate(_devices);
        if (resources is null)
        {
            return;
        }

        try
        {
            resources.SetInput(_input);
            _resources = resources;
            _canRetryUserBrush = false;
#if DEBUG
            var diagnosticInputState = ReadDiagnosticInputState();
            PreviewUpdateTrace.Record(
                PreviewTraceEvent.ResourcesRecovered,
                _diagnosticProcessorId,
                resources.DiagnosticId,
                Volatile.Read(ref _diagnosticUpdateSequence),
                Volatile.Read(ref _diagnosticLastFrame),
                inputIdentityId: diagnosticInputState.IdentityId,
                inputSetSequence: diagnosticInputState.SetSequence);
#endif
        }
        catch
        {
            resources.Dispose();
        }
    }

#if DEBUG
    private DiagnosticInputState ReadDiagnosticInputState() =>
        Volatile.Read(ref _diagnosticInputState);

    private DiagnosticInputState UpdateDiagnosticInputState(int identityId)
    {
        lock (_diagnosticInputStateGate)
        {
            var currentState = _diagnosticInputState;
            var setSequence = currentState.SetSequence == int.MaxValue
                ? int.MaxValue
                : currentState.SetSequence + 1;
            var updatedState = new DiagnosticInputState(identityId, setSequence);
            Volatile.Write(ref _diagnosticInputState, updatedState);
            return updatedState;
        }
    }

    private sealed record DiagnosticInputState(int IdentityId, int SetSequence);
#endif
}
