// SPDX-License-Identifier: MPL-2.0

using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;

namespace YMM4GlassWipe;

internal sealed class GlassWipeEffectResources : IDisposable
{
#if DEBUG
    private const int CompositeCacheApplyExceptionDetail = -1;
    private const int CompositeCacheRestoreExceptionDetail = -2;
    private const int CompositeMaskInputBypassApplyExceptionDetail = -1;
    private const int CompositeMaskInputBypassRestoreExceptionDetail = -2;
    private const int CompositeMaskInputBypassApplyRecoveryExceptionDetail = -4;
    private static int _nextDiagnosticId;
    internal int DiagnosticId { get; } = Interlocked.Increment(ref _nextDiagnosticId);
    private bool _diagnosticCompositeCacheModeActive;
    private bool? _diagnosticOriginalCompositeCached;
    private bool _diagnosticCompositeMaskInputBypassActive;
    private ID2D1Bitmap1? _diagnosticFixedTransparentMaskBitmap;
#endif

    private readonly ID2D1DeviceContext6 _deviceContext;
    private readonly IGraphicsDevicesAndContext _devices;
    private readonly GaussianBlur _blurEffect;
    private readonly ID2D1Image _blurOutput;
    private readonly WipeMaskRenderer _maskRenderer;
    private readonly GlassCompositeCustomEffect _preFogCompositeEffect;
    private readonly ID2D1Image _preFogCompositeOutput;
    private OutsideDropletPhysicsCustomEffect? _physicsEffect;
    private readonly GlassCompositeCustomEffect _compositeEffect;
    private readonly ID2D1Image _compositeOutput;
    private ID2D1Image? _input;
    private GlassWipeParameters _currentParameters;
    private float _currentInputLeft = float.NaN;
    private float _currentInputTop = float.NaN;
    private float _currentInputWidth = float.NaN;
    private float _currentInputHeight = float.NaN;
    private float _currentBlur = float.NaN;
    private bool _hasParameters;
    private bool _preFogPipelineActive;
    private bool _physicsPipelineActive;
    private bool _fogBypassActive;
    private bool _disposed;

    private GlassWipeEffectResources(
        IGraphicsDevicesAndContext devices,
        GaussianBlur blurEffect,
        ID2D1Image blurOutput,
        WipeMaskRenderer maskRenderer,
        GlassCompositeCustomEffect preFogCompositeEffect,
        ID2D1Image preFogCompositeOutput,
        GlassCompositeCustomEffect compositeEffect,
        ID2D1Image compositeOutput)
    {
        _devices = devices;
        _deviceContext = devices.DeviceContext;
        _blurEffect = blurEffect;
        _blurOutput = blurOutput;
        _maskRenderer = maskRenderer;
        _preFogCompositeEffect = preFogCompositeEffect;
        _preFogCompositeOutput = preFogCompositeOutput;
        _compositeEffect = compositeEffect;
        _compositeOutput = compositeOutput;
    }

    public ID2D1Image Output => _compositeOutput;

    public static GlassWipeEffectResources? TryCreate(
        IGraphicsDevicesAndContext devices)
    {
        GaussianBlur? blurEffect = null;
        ID2D1Image? blurOutput = null;
        WipeMaskRenderer? maskRenderer = null;
        GlassCompositeCustomEffect? preFogCompositeEffect = null;
        ID2D1Image? preFogCompositeOutput = null;
        GlassCompositeCustomEffect? compositeEffect = null;
        ID2D1Image? compositeOutput = null;

        try
        {
            blurEffect = new GaussianBlur(devices.DeviceContext);
            blurOutput = blurEffect.Output;
            maskRenderer = WipeMaskRenderer.Create(devices);
            preFogCompositeEffect = new GlassCompositeCustomEffect(
                devices,
                outsideDropletPass: true);
            if (!preFogCompositeEffect.IsEnabled)
            {
                return null;
            }

            preFogCompositeOutput = preFogCompositeEffect.Output;
            compositeEffect = new GlassCompositeCustomEffect(devices);

            if (!compositeEffect.IsEnabled)
            {
                return null;
            }

            compositeEffect.SetInput(1, blurOutput, true);
            compositeEffect.SetInput(2, maskRenderer.Image, true);
            compositeOutput = compositeEffect.Output;

            var resources = new GlassWipeEffectResources(
                devices,
                blurEffect,
                blurOutput,
                maskRenderer,
                preFogCompositeEffect,
                preFogCompositeOutput,
                compositeEffect,
                compositeOutput);

            blurEffect = null;
            blurOutput = null;
            maskRenderer = null;
            preFogCompositeEffect = null;
            preFogCompositeOutput = null;
            compositeEffect = null;
            compositeOutput = null;
            return resources;
        }
        catch
        {
            return null;
        }
        finally
        {
            TryRelease(() => compositeEffect?.SetInput(0, null, true));
            TryRelease(() => compositeEffect?.SetInput(1, null, true));
            TryRelease(() => compositeEffect?.SetInput(2, null, true));
            TryRelease(() => preFogCompositeEffect?.SetInput(0, null, true));
            TryRelease(() => preFogCompositeEffect?.SetInput(1, null, true));
            TryRelease(() => preFogCompositeEffect?.SetInput(2, null, true));
            TryRelease(() => compositeOutput?.Dispose());
            TryRelease(() => compositeEffect?.Dispose());
            TryRelease(() => maskRenderer?.Dispose());
            TryRelease(() => blurEffect?.SetInput(0, null, true));
            TryRelease(() => blurOutput?.Dispose());
            TryRelease(() => blurEffect?.Dispose());
            TryRelease(() => preFogCompositeOutput?.Dispose());
            TryRelease(() => preFogCompositeEffect?.Dispose());
        }
    }

    public void SetInput(ID2D1Image? input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _input = input;
        _preFogCompositeEffect.SetInput(0, input, true);
        _preFogCompositeEffect.SetInput(1, input, true);
        _preFogCompositeEffect.SetInput(2, input, true);
        _physicsEffect?.SetInput(input);
        _blurEffect.SetInput(0, input, true);
        _compositeEffect.SetInput(0, input, true);
        // 省略中のslot 1は借用入力を参照するため、入力交換・解除にも追従させる。
        if (_fogBypassActive)
        {
            _compositeEffect.SetInput(1, input, true);
        }
        _preFogPipelineActive = false;
        _physicsPipelineActive = false;
    }

    public void Update(
        GlassWipeParameters parameters,
        Func<WipeMaskGeometry, WipePathStream> pathFactory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(pathFactory);

        if (_input is null)
        {
#if DEBUG
            PreviewUpdateTrace.Record(
                PreviewTraceEvent.ResourceNoInput,
                resourceId: DiagnosticId,
                localTimeSeconds: parameters.OutsideDropletLocalTimeSeconds,
                fallEnabled: parameters.OutsideDropletFallEnabled,
                seed: parameters.OutsideDropletSeed);
#endif
            return;
        }

        var inputBounds = _deviceContext.GetImageLocalBounds(_input);
        var inputLeft = SanitizeCoordinate(inputBounds.Left);
        var inputTop = SanitizeCoordinate(inputBounds.Top);
        var inputWidth = SanitizeDimension(inputBounds.Right - inputBounds.Left);
        var inputHeight = SanitizeDimension(inputBounds.Bottom - inputBounds.Top);

        var physicsCoverage = parameters.OutsideDropletMotionMode == OutsideDropletMotionMode.SimplePhysics &&
            (int)parameters.DebugView == 6;
        var bypassFog = physicsCoverage || GlassWipeFogPipeline.ShouldBypass(parameters.FogAmount, parameters.DebugView);
        // 休止中は最後に適用した値を保持し、再開時に現在のぼかし量を反映する。
        if (!bypassFog && _currentBlur != parameters.Blur)
        {
            _blurEffect.StandardDeviation = parameters.Blur;
            _currentBlur = parameters.Blur;
        }

        var usePreFogPipeline = OutsideDropletFogPipeline.ShouldUsePreFog(
            parameters.OutsideDropletAmount,
            parameters.OutsideDropletStrength,
            parameters.DebugView) || (physicsCoverage && parameters.OutsideDropletAmount > 0 && parameters.OutsideDropletStrength > 0);
        var usePhysics = usePreFogPipeline && parameters.OutsideDropletMotionMode == OutsideDropletMotionMode.SimplePhysics;
        if (usePhysics)
        {
            if (_physicsEffect is null)
            {
                _physicsEffect = new OutsideDropletPhysicsCustomEffect(_devices);
                _physicsEffect.SetInput(_input);
            }
            _physicsEffect.ApplyParameters(parameters, inputLeft, inputTop, inputWidth, inputHeight);
        }
        UpdatePipelineInputs(usePreFogPipeline, bypassFog, usePhysics);
        // 被覆診断でも最終出力の画像IDを変えず、同じ物理前段の結果を通す。
        var compositeParameters = physicsCoverage
            ? parameters with { DebugView = GlassWipeDebugView.Final, FogAmount = 0, OutsideDropletAmount = 0 }
            : parameters.OutsideDropletMotionMode == OutsideDropletMotionMode.SimplePhysics
                ? parameters with { OutsideDropletAmount = 0 }
                : parameters;

        var regionPixelWidth = inputWidth * parameters.RegionWidth;
        var regionPixelHeight = inputHeight * parameters.RegionHeight;
        if (parameters.RegionShape == GlassWipeRegionShape.Quad)
        {
            var quadPixelSize = parameters.Quad.GetNominalPixelSize(
                inputWidth,
                inputHeight);
            regionPixelWidth = quadPixelSize.X;
            regionPixelHeight = quadPixelSize.Y;
        }

        // 曇りが無効な通常表示では、水滴の有無にかかわらず既存マスクと履歴を保持して更新を休止する。
        // 再開時は既存の更新計画により未処理区間を追記し、設定変更や後方シークでは再構築する。
        var skipMaskUpdate = parameters.DebugView == GlassWipeDebugView.Final &&
            parameters.FogAmount == 0f;
        if (!skipMaskUpdate &&
            regionPixelWidth > 0 &&
            regionPixelHeight > 0 &&
            (parameters.RegionShape != GlassWipeRegionShape.Quad ||
             parameters.QuadMapping.IsValid))
        {
            var maskGeometry = WipeMaskGeometry.Create(
                regionPixelWidth,
                regionPixelHeight);
            var path = pathFactory(maskGeometry);
            _maskRenderer.Update(path, maskGeometry);
        }

        var inputBoundsChanged = _currentInputLeft != inputLeft ||
            _currentInputTop != inputTop ||
            _currentInputWidth != inputWidth ||
            _currentInputHeight != inputHeight;
        if (!_hasParameters ||
            !_currentParameters.HasSameCompositeNonTemporalValues(parameters) ||
            inputBoundsChanged)
        {
#if DEBUG
            PreviewUpdateTrace.Record(
                PreviewTraceEvent.ResourceFullApply,
                resourceId: DiagnosticId,
                localTimeSeconds: parameters.OutsideDropletLocalTimeSeconds,
                fallEnabled: parameters.OutsideDropletFallEnabled,
                seed: parameters.OutsideDropletSeed,
                inputLeft: inputLeft,
                inputTop: inputTop,
                inputWidth: inputWidth,
                inputHeight: inputHeight);
#endif
            if (usePreFogPipeline && !usePhysics)
            {
                // 通常表示で曇り専用値・ぼかし量・時刻だけが変わった場合、水滴前段の設定は再利用する。
                var reusePreFogParameters = _hasParameters &&
                    !inputBoundsChanged &&
                    parameters.DebugView == GlassWipeDebugView.Final &&
                    _currentParameters.HasSamePreFogNonTemporalValues(parameters);
                if (reusePreFogParameters)
                {
                    if (_currentParameters.OutsideDropletLocalTimeSeconds !=
                        parameters.OutsideDropletLocalTimeSeconds)
                    {
                        _preFogCompositeEffect.ApplyOutsideDropletLocalTime(
                            parameters.OutsideDropletLocalTimeSeconds);
                    }
                }
                else
                {
                    _preFogCompositeEffect.ApplyParameters(
                        parameters,
                        inputLeft,
                        inputTop,
                        inputWidth,
                        inputHeight);
                }
            }

            _compositeEffect.ApplyParameters(
                compositeParameters,
                inputLeft,
                inputTop,
                inputWidth,
                inputHeight);
        }
        else if (_currentParameters.OutsideDropletLocalTimeSeconds !=
                 parameters.OutsideDropletLocalTimeSeconds)
        {
#if DEBUG
            PreviewUpdateTrace.Record(
                PreviewTraceEvent.ResourceTimeApply,
                resourceId: DiagnosticId,
                localTimeSeconds: parameters.OutsideDropletLocalTimeSeconds,
                fallEnabled: parameters.OutsideDropletFallEnabled,
                seed: parameters.OutsideDropletSeed,
                inputLeft: inputLeft,
                inputTop: inputTop,
                inputWidth: inputWidth,
                inputHeight: inputHeight);
#endif
            if (usePreFogPipeline && !usePhysics)
            {
                _preFogCompositeEffect.ApplyOutsideDropletLocalTime(
                    parameters.OutsideDropletLocalTimeSeconds);
            }

            // 通常表示の水滴時刻は前段だけで使うため、最終合成への時刻転送を省略する。
            // 水滴の再有効化と表示切替では、全パラメータ適用で現在時刻を反映する。
            if (parameters.DebugView != GlassWipeDebugView.Final)
            {
                _compositeEffect.ApplyOutsideDropletLocalTime(
                    parameters.OutsideDropletLocalTimeSeconds);
            }
        }
#if DEBUG
        else
        {
            PreviewUpdateTrace.Record(
                PreviewTraceEvent.ResourceNoParameterApply,
                resourceId: DiagnosticId,
                localTimeSeconds: parameters.OutsideDropletLocalTimeSeconds,
                fallEnabled: parameters.OutsideDropletFallEnabled,
                seed: parameters.OutsideDropletSeed,
                inputLeft: inputLeft,
                inputTop: inputTop,
                inputWidth: inputWidth,
                inputHeight: inputHeight);
        }
#endif

#if DEBUG
        UpdateCompositeMaskInputBypassForDebug(parameters);
        UpdateCompositeOutputCacheForDebug(parameters);
#endif

        _currentParameters = parameters;
        _currentInputLeft = inputLeft;
        _currentInputTop = inputTop;
        _currentInputWidth = inputWidth;
        _currentInputHeight = inputHeight;
        _hasParameters = true;
    }

    private void UpdatePipelineInputs(bool usePreFogPipeline, bool bypassFog, bool usePhysics)
    {
        if (_preFogPipelineActive != usePreFogPipeline || _physicsPipelineActive != usePhysics)
        {
            var compositeInput = usePreFogPipeline
                ? (usePhysics ? _physicsEffect!.Output : _preFogCompositeOutput)
                : _input;
            _blurEffect.SetInput(0, compositeInput, true);
            _compositeEffect.SetInput(0, compositeInput, true);
            _preFogPipelineActive = usePreFogPipeline;
            _physicsPipelineActive = usePhysics;
        }

        if (_fogBypassActive != bypassFog)
        {
            // シェーダーの早期returnだけでなく、ぼかしの出力依存も外す。
            // 出力画像・マスク・GPUリソースは維持し、再開時の履歴処理を増やさない。
            _compositeEffect.SetInput(1, bypassFog ? _input : _blurOutput, true);
            _fogBypassActive = bypassFog;
        }
    }

#if DEBUG
    private void UpdateCompositeMaskInputBypassForDebug(
        GlassWipeParameters parameters)
    {
        var shouldBypass = parameters.DebugView is
            GlassWipeDebugView.OutsideDropletCellHashWithoutSine or
            GlassWipeDebugView.OutsideDropletFinalHashBitsWithoutSine or
            GlassWipeDebugView.OutsideDropletMixedHashBitsWithoutSine or
            GlassWipeDebugView.OutsideDropletPreAvalancheHashBitsWithoutSine or
            GlassWipeDebugView.OutsideDropletWeightedHashBitsWithoutSine or
            GlassWipeDebugView.OutsideDropletAccumulatedHashBitsWithoutSine or
            GlassWipeDebugView.OutsideDropletMediumOnlyHashInputGreenBitWithoutSine or
            GlassWipeDebugView.OutsideDropletMediumOnlyCellXGreenBitWithoutSine or
            GlassWipeDebugView.OutsideDropletMediumOnlyInlineHashInputGreenBitWithoutSine or
            GlassWipeDebugView.OutsideDropletMediumOnlyCellYGreenBitWithoutSine;
        if (shouldBypass)
        {
            if (_diagnosticCompositeMaskInputBypassActive)
            {
                return;
            }

            try
            {
                // Debug11/14/16/17/18/19/20/21/22/23はMaskTextureをSampleする前にreturnする。
                // 通常マスクと同じ1024x1024範囲の固定透明Bitmapへ差し替え、
                // slot2の可変マスク依存だけを描画グラフから外す。
                var diagnosticMask = _diagnosticFixedTransparentMaskBitmap ??=
                    CreateFixedTransparentMaskBitmapForDebug();
                _compositeEffect.SetInput(2, diagnosticMask, true);
                _diagnosticCompositeMaskInputBypassActive = true;
                GetImageLocalBoundsForDebug(
                    diagnosticMask,
                    out var diagnosticMaskLeft,
                    out var diagnosticMaskTop,
                    out var diagnosticMaskWidth,
                    out var diagnosticMaskHeight);
                PreviewUpdateTrace.Record(
                    PreviewTraceEvent.ResourceCompositeMaskInputBypassApply,
                    resourceId: DiagnosticId,
                    localTimeSeconds: parameters.OutsideDropletLocalTimeSeconds,
                    fallEnabled: parameters.OutsideDropletFallEnabled,
                    seed: parameters.OutsideDropletSeed,
                    inputLeft: diagnosticMaskLeft,
                    inputTop: diagnosticMaskTop,
                    inputWidth: diagnosticMaskWidth,
                    inputHeight: diagnosticMaskHeight,
                    inputIdentityId: PreviewUpdateTrace.GetInputIdentityId(
                        diagnosticMask));
            }
            catch
            {
                var restored = TryRestoreCompositeMaskInputForDebug();
                _diagnosticCompositeMaskInputBypassActive = !restored;
                PreviewUpdateTrace.Record(
                    PreviewTraceEvent.ResourceCompositeMaskInputBypassFailed,
                    resourceId: DiagnosticId,
                    localTimeSeconds: parameters.OutsideDropletLocalTimeSeconds,
                    fallEnabled: parameters.OutsideDropletFallEnabled,
                    seed: parameters.OutsideDropletSeed,
                    detail: restored
                        ? CompositeMaskInputBypassApplyExceptionDetail
                        : CompositeMaskInputBypassApplyRecoveryExceptionDetail);
            }

            return;
        }

        if (!_diagnosticCompositeMaskInputBypassActive)
        {
            return;
        }

        if (!TryRestoreCompositeMaskInputForDebug())
        {
            PreviewUpdateTrace.Record(
                PreviewTraceEvent.ResourceCompositeMaskInputBypassFailed,
                resourceId: DiagnosticId,
                localTimeSeconds: parameters.OutsideDropletLocalTimeSeconds,
                fallEnabled: parameters.OutsideDropletFallEnabled,
                seed: parameters.OutsideDropletSeed,
                detail: CompositeMaskInputBypassRestoreExceptionDetail);
            return;
        }

        _diagnosticCompositeMaskInputBypassActive = false;
        GetImageLocalBoundsForDebug(
            _maskRenderer.Image,
            out var maskLeft,
            out var maskTop,
            out var maskWidth,
            out var maskHeight);
        PreviewUpdateTrace.Record(
            PreviewTraceEvent.ResourceCompositeMaskInputBypassRestore,
            resourceId: DiagnosticId,
            localTimeSeconds: parameters.OutsideDropletLocalTimeSeconds,
            fallEnabled: parameters.OutsideDropletFallEnabled,
            seed: parameters.OutsideDropletSeed,
            inputLeft: maskLeft,
            inputTop: maskTop,
            inputWidth: maskWidth,
            inputHeight: maskHeight,
            inputIdentityId: PreviewUpdateTrace.GetInputIdentityId(
                _maskRenderer.Image));
    }

    private bool TryRestoreCompositeMaskInputForDebug()
    {
        try
        {
            _compositeEffect.SetInput(2, _maskRenderer.Image, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private ID2D1Bitmap1 CreateFixedTransparentMaskBitmapForDebug()
    {
        const int bytesPerPixel = 4;
        var rowPitch = checked(WipeMaskRenderer.MaskSize * bytesPerPixel);
        var transparentPixels = new byte[checked(
            rowPitch * WipeMaskRenderer.MaskSize)];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(
            transparentPixels,
            System.Runtime.InteropServices.GCHandleType.Pinned);

        try
        {
            var properties = new BitmapProperties1(
                new Vortice.DCommon.PixelFormat(
                    Vortice.DXGI.Format.B8G8R8A8_UNorm,
                    Vortice.DCommon.AlphaMode.Premultiplied),
                96,
                96,
                BitmapOptions.None);
            return _deviceContext.CreateBitmap(
                new Vortice.Mathematics.SizeI(
                    WipeMaskRenderer.MaskSize,
                    WipeMaskRenderer.MaskSize),
                handle.AddrOfPinnedObject(),
                rowPitch,
                properties);
        }
        finally
        {
            handle.Free();
        }
    }

    private void GetImageLocalBoundsForDebug(
        ID2D1Image image,
        out float left,
        out float top,
        out float width,
        out float height)
    {
        try
        {
            var bounds = _deviceContext.GetImageLocalBounds(image);
            left = SanitizeCoordinate(bounds.Left);
            top = SanitizeCoordinate(bounds.Top);
            width = SanitizeDimension(bounds.Right - bounds.Left);
            height = SanitizeDimension(bounds.Bottom - bounds.Top);
        }
        catch
        {
            left = float.NaN;
            top = float.NaN;
            width = float.NaN;
            height = float.NaN;
        }
    }

    private void UpdateCompositeOutputCacheForDebug(
        GlassWipeParameters parameters)
    {
        var shouldEnable = parameters.DebugView ==
            GlassWipeDebugView.OutsideDropletCoverage;
        if (shouldEnable)
        {
            if (_diagnosticCompositeCacheModeActive)
            {
                return;
            }

            try
            {
                var cachedBefore = _compositeEffect.GetCachedForDebug();
                _diagnosticOriginalCompositeCached ??= cachedBefore;
                if (!cachedBefore)
                {
                    _compositeEffect.SetCachedForDebug(true);
                }

                var cachedAfter = _compositeEffect.GetCachedForDebug();
                var originalCached = _diagnosticOriginalCompositeCached.Value;
                var detail = EncodeCompositeCacheTraceDetail(
                    originalCached,
                    cachedBefore,
                    cachedAfter);
                if (!cachedAfter)
                {
                    PreviewUpdateTrace.Record(
                        PreviewTraceEvent.ResourceCompositeCacheFailed,
                        resourceId: DiagnosticId,
                        localTimeSeconds: parameters.OutsideDropletLocalTimeSeconds,
                        fallEnabled: parameters.OutsideDropletFallEnabled,
                        seed: parameters.OutsideDropletSeed,
                        detail: detail);
                    return;
                }

                _diagnosticCompositeCacheModeActive = true;
                PreviewUpdateTrace.Record(
                    PreviewTraceEvent.ResourceCompositeCacheApply,
                    resourceId: DiagnosticId,
                    localTimeSeconds: parameters.OutsideDropletLocalTimeSeconds,
                    fallEnabled: parameters.OutsideDropletFallEnabled,
                    seed: parameters.OutsideDropletSeed,
                    detail: detail);
            }
            catch
            {
                PreviewUpdateTrace.Record(
                    PreviewTraceEvent.ResourceCompositeCacheFailed,
                    resourceId: DiagnosticId,
                    localTimeSeconds: parameters.OutsideDropletLocalTimeSeconds,
                    fallEnabled: parameters.OutsideDropletFallEnabled,
                    seed: parameters.OutsideDropletSeed,
                    detail: CompositeCacheApplyExceptionDetail);
            }

            return;
        }

        if (!_diagnosticCompositeCacheModeActive &&
            !_diagnosticOriginalCompositeCached.HasValue)
        {
            return;
        }

        try
        {
            var cachedBefore = _compositeEffect.GetCachedForDebug();
            var originalCached = _diagnosticOriginalCompositeCached ?? cachedBefore;
            if (cachedBefore != originalCached)
            {
                _compositeEffect.SetCachedForDebug(originalCached);
            }

            var cachedAfter = _compositeEffect.GetCachedForDebug();
            var detail = EncodeCompositeCacheTraceDetail(
                originalCached,
                cachedBefore,
                cachedAfter);
            if (cachedAfter != originalCached)
            {
                PreviewUpdateTrace.Record(
                    PreviewTraceEvent.ResourceCompositeCacheFailed,
                    resourceId: DiagnosticId,
                    localTimeSeconds: parameters.OutsideDropletLocalTimeSeconds,
                    fallEnabled: parameters.OutsideDropletFallEnabled,
                    seed: parameters.OutsideDropletSeed,
                    detail: detail);
                return;
            }

            PreviewUpdateTrace.Record(
                PreviewTraceEvent.ResourceCompositeCacheRestore,
                resourceId: DiagnosticId,
                localTimeSeconds: parameters.OutsideDropletLocalTimeSeconds,
                fallEnabled: parameters.OutsideDropletFallEnabled,
                seed: parameters.OutsideDropletSeed,
                detail: detail);
            _diagnosticOriginalCompositeCached = null;
            _diagnosticCompositeCacheModeActive = false;
        }
        catch
        {
            PreviewUpdateTrace.Record(
                PreviewTraceEvent.ResourceCompositeCacheFailed,
                resourceId: DiagnosticId,
                localTimeSeconds: parameters.OutsideDropletLocalTimeSeconds,
                fallEnabled: parameters.OutsideDropletFallEnabled,
                seed: parameters.OutsideDropletSeed,
                detail: CompositeCacheRestoreExceptionDetail);
        }
    }

    // detail: bit 0 = 元の値、bit 1 = 操作前、bit 2 = 読戻し後。
    // 例外時はApply=-1、Restore=-2を記録する。
    private static int EncodeCompositeCacheTraceDetail(
        bool originalCached,
        bool cachedBefore,
        bool cachedAfter) =>
        (originalCached ? 1 : 0) |
        (cachedBefore ? 2 : 0) |
        (cachedAfter ? 4 : 0);
#endif

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

#if DEBUG
        PreviewUpdateTrace.Record(
            PreviewTraceEvent.ResourceDisposeStart,
            resourceId: DiagnosticId);
#endif
        _disposed = true;
        _input = null;

        TryRelease(() => _compositeEffect.SetInput(0, null, true));
        TryRelease(() => _compositeEffect.SetInput(1, null, true));
        TryRelease(() => _compositeEffect.SetInput(2, null, true));
        TryRelease(() => _blurEffect.SetInput(0, null, true));
        TryRelease(() => _preFogCompositeEffect.SetInput(0, null, true));
        TryRelease(() => _preFogCompositeEffect.SetInput(1, null, true));
        TryRelease(() => _preFogCompositeEffect.SetInput(2, null, true));
        TryRelease(() => _physicsEffect?.SetInput(null));
#if DEBUG
        TryRelease(() => _diagnosticFixedTransparentMaskBitmap?.Dispose());
        _diagnosticFixedTransparentMaskBitmap = null;
#endif
        TryRelease(_compositeOutput.Dispose);
        TryRelease(_compositeEffect.Dispose);
        TryRelease(_maskRenderer.Dispose);
        TryRelease(_blurOutput.Dispose);
        TryRelease(_blurEffect.Dispose);
        TryRelease(_preFogCompositeOutput.Dispose);
        TryRelease(_preFogCompositeEffect.Dispose);
        TryRelease(() => _physicsEffect?.Dispose());
#if DEBUG
        PreviewUpdateTrace.Record(
            PreviewTraceEvent.ResourceDisposeEnd,
            resourceId: DiagnosticId);
#endif
    }

    private static float SanitizeDimension(float value) =>
        float.IsFinite(value) && value > 0 ? value : 1;

    private static float SanitizeCoordinate(float value) =>
        float.IsFinite(value) ? value : 0;

    private static void TryRelease(Action release)
    {
        try
        {
            release();
        }
        catch
        {
            // フォールバックと終了処理を継続するため、解放時の例外は外へ出さない。
        }
    }
}

internal static class OutsideDropletFogPipeline
{
    internal static bool ShouldUsePreFog(
        float outsideDropletAmount,
        float outsideDropletStrength,
        GlassWipeDebugView debugView) =>
        outsideDropletAmount > 0 &&
        outsideDropletStrength > 0 &&
        debugView is GlassWipeDebugView.Final or GlassWipeDebugView.Blurred;
}

internal static class GlassWipeFogPipeline
{
    internal static bool ShouldBypass(float evaluatedFogAmount, GlassWipeDebugView debugView) =>
        evaluatedFogAmount == 0f && debugView == GlassWipeDebugView.Final;
}
