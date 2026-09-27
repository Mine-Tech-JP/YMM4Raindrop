// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using System.Collections;
using System.Runtime.InteropServices;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace YMM4GlassWipe;

internal sealed class WipeMaskRenderer : IDisposable
{
    public const int MaskSize = 1024;

    private readonly ID2D1DeviceContext _drawingContext;
    private readonly ID2D1Bitmap1 _maskBitmap;
    private readonly ID2D1SolidColorBrush _whiteBrush;
    private ID2D1Bitmap1? _passMaskBitmap;
    private ID2D1Bitmap1? _committedMaskBitmap;
    private ID2D1GradientStopCollection? _softGradientStops;
    private ID2D1RadialGradientBrush? _softBrush;
    private ID2D1Bitmap1? _rectangleShapeMask;
    private ID2D1Bitmap1? _handShapeMask;
    private ID2D1Bitmap1? _shoePrintShapeMask;
    private ID2D1Bitmap1? _userShapeMask;
    private WipeBrushBinaryMask? _rectangleBinaryMask;
    private WipeBrushBinaryMask? _handBinaryMask;
    private WipeBrushBinaryMask? _shoePrintBinaryMask;
    private WipeBrushBinaryMask? _userBinaryMask;
    private float _rectangleMaskSoftness = -1;
    private float _handMaskSoftness = -1;
    private float _shoePrintMaskSoftness = -1;
    private float _userMaskSoftness = -1;
    private Guid _userMaskId;
    private long _userMaskRevision;
    private int _userMaskPixelWidth;
    private int _userMaskPixelHeight;
    private readonly UserBrushLibrary _userBrushLibrary = new();
    private float _softBrushSoftness = -1;
    private WipePathSnapshot? _cachedPath;
    private WipePathStream? _cachedStreamPath;
    private WipeMaskGeometry? _cachedGeometry;
    private WipeBrushStampGenerator.StreamState[] _streamStates = [];
    private int _activePassGroup = -1;
    private float _activePassOpacity;
    private bool _hasActivePass;
    private bool _disposed;

    private WipeMaskRenderer(
        ID2D1DeviceContext drawingContext,
        ID2D1Bitmap1 maskBitmap,
        ID2D1SolidColorBrush whiteBrush)
    {
        _drawingContext = drawingContext;
        _maskBitmap = maskBitmap;
        _whiteBrush = whiteBrush;
    }

    public ID2D1Image Image => _maskBitmap;

    public static WipeMaskRenderer Create(IGraphicsDevicesAndContext devices)
    {
        ArgumentNullException.ThrowIfNull(devices);

        ID2D1DeviceContext? drawingContext = null;
        ID2D1Bitmap1? maskBitmap = null;
        ID2D1SolidColorBrush? whiteBrush = null;

        try
        {
            using var device = devices.DeviceContext.Device;
            drawingContext = device.CreateDeviceContext(DeviceContextOptions.None);
            maskBitmap = CreateMaskBitmap(drawingContext);
            whiteBrush = drawingContext.CreateSolidColorBrush(
                new Color4(1, 1, 1, 1));

            var renderer = new WipeMaskRenderer(
                drawingContext,
                maskBitmap,
                whiteBrush);
            drawingContext = null;
            maskBitmap = null;
            whiteBrush = null;
            return renderer;
        }
        finally
        {
            whiteBrush?.Dispose();
            maskBitmap?.Dispose();
            drawingContext?.Dispose();
        }
    }

    public void Update(
        WipePathSnapshot path,
        WipeMaskGeometry geometry)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(path);

        if (path.Style.BrushShape != GlassWipeBrushShape.UserImage)
        {
            ReleaseUserShapeMask();
        }

        var plan = WipeMaskUpdatePlan.Create(
            _cachedPath,
            _cachedGeometry,
            path,
            geometry);

        if (plan.Kind == WipeMaskUpdateKind.Reuse)
        {
            return;
        }

        var accumulationMode = path.Style.AccumulationMode;
        var forceRebuild =
            accumulationMode == WipeMaskAccumulationMode.PerPassMaximum;
        var stamps = WipeBrushStampGenerator.Generate(
            path.Samples,
            forceRebuild ? 0 : plan.FirstSampleIndex,
            geometry,
            path.Style);
        DrawMask(
            forceRebuild || plan.Kind == WipeMaskUpdateKind.Rebuild,
            stamps,
            accumulationMode);
        _cachedPath = path;
        _cachedGeometry = geometry;
        _cachedStreamPath = null;
        _streamStates = [];
    }

    /// <summary>
    /// 固定容量バッチで履歴を評価し、最新マスクへ差分追加します。
    /// </summary>
    public void Update(
        WipePathStream path,
        WipeMaskGeometry geometry)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(path);

        if (path.Style.BrushShape != GlassWipeBrushShape.UserImage)
        {
            ReleaseUserShapeMask();
        }

        var plan = WipeMaskUpdatePlan.Create(
            _cachedStreamPath,
            _cachedGeometry,
            path,
            geometry);
        if (plan.Kind == WipeMaskUpdateKind.Reuse)
        {
            return;
        }

        // 以降はBitmapと列挙カーソルを変更する。途中で失敗した状態を
        // 次回Reuse／Appendしないよう、成功時にだけ新しいCacheを確定する。
        _cachedStreamPath = null;
        _cachedPath = null;
        _cachedGeometry = null;

        var rebuild = plan.Kind == WipeMaskUpdateKind.Rebuild;
        if (rebuild)
        {
            ResetStreamState(path);
        }

        if (!path.HasStarted)
        {
            _cachedStreamPath = path;
            _cachedGeometry = geometry;
            _cachedPath = null;
            return;
        }

        var firstFrame = rebuild ? 0 : plan.FirstSampleIndex;
        var stamps = new BufferedStampCollection(
            WipeBrushStampGenerator.StampBatchCapacity,
            path.Style.AccumulationMode == WipeMaskAccumulationMode.PerPassMaximum
                ? batch => DrawPerPassStreamBatch(batch)
                : batch => DrawStampBatch(
                    _maskBitmap,
                    PrimitiveBlend.SourceOver,
                    batch,
                    0,
                    batch.Count));
        path.EnumerateSamples(
            firstFrame,
            path.Frame,
            (laneId, sample) =>
            {
                var stateIndex = Math.Clamp(laneId, 0, _streamStates.Length - 1);
                _streamStates[stateIndex].Append(
                    sample,
                    geometry,
                    path.Style,
                    stamps);
            });
        stamps.Flush();

        if (path.Style.AccumulationMode == WipeMaskAccumulationMode.PerPassMaximum)
        {
            ComposePerPassDisplay();
        }

        _cachedStreamPath = path;
        _cachedGeometry = geometry;
        _cachedPath = null;
    }

    private void ResetStreamState(WipePathStream path)
    {
        _streamStates = new WipeBrushStampGenerator.StreamState[path.LaneCount];
        for (var lane = 0; lane < _streamStates.Length; lane++)
        {
            _streamStates[lane] = new WipeBrushStampGenerator.StreamState(
                path.GetStrokeIdBase(lane));
        }

        _activePassGroup = -1;
        _activePassOpacity = 0;
        _hasActivePass = false;
        ClearBitmap(_maskBitmap);
        if (path.Style.AccumulationMode == WipeMaskAccumulationMode.PerPassMaximum)
        {
            EnsurePassMaskBitmap();
            EnsureCommittedMaskBitmap();
            ClearBitmap(_passMaskBitmap!);
            ClearBitmap(_committedMaskBitmap!);
        }
    }

    private void DrawPerPassStreamBatch(IReadOnlyList<WipeBrushStamp> stamps)
    {
        EnsurePassMaskBitmap();
        EnsureCommittedMaskBitmap();

        var firstStampIndex = 0;
        while (firstStampIndex < stamps.Count)
        {
            var group = stamps[firstStampIndex].AccumulationGroup;
            var nextGroupIndex = firstStampIndex + 1;
            while (nextGroupIndex < stamps.Count &&
                   stamps[nextGroupIndex].AccumulationGroup == group)
            {
                nextGroupIndex++;
            }

            var stampCount = nextGroupIndex - firstStampIndex;
            var passOpacity = ResolvePassOpacity(stamps, firstStampIndex, stampCount);
            if (passOpacity > 0)
            {
                BeginStreamPass(group, passOpacity);
                DrawStampBatch(
                    _passMaskBitmap!,
                    ResolvePassPrimitiveBlend(stamps[firstStampIndex].Shape),
                    stamps,
                    firstStampIndex,
                    stampCount,
                    1 / _activePassOpacity);
            }

            firstStampIndex = nextGroupIndex;
        }
    }

    private void BeginStreamPass(int group, float opacity)
    {
        opacity = Math.Clamp(opacity, 0, 1);
        if (_hasActivePass && group != _activePassGroup)
        {
            DrawBitmapToTarget(
                _committedMaskBitmap!,
                _passMaskBitmap!,
                false,
                _activePassOpacity);
            ClearBitmap(_passMaskBitmap!);
            _hasActivePass = false;
        }

        if (!_hasActivePass)
        {
            _activePassGroup = group;
            _activePassOpacity = opacity;
            _hasActivePass = true;
            return;
        }

        // Simple往復の片道強度は固定である。異常値では過大適用を避けて最大値を使う。
        _activePassOpacity = MathF.Max(_activePassOpacity, opacity);
    }

    private void ComposePerPassDisplay()
    {
        EnsurePassMaskBitmap();
        EnsureCommittedMaskBitmap();
        DrawBitmapToTarget(_maskBitmap, _committedMaskBitmap!, true, 1);
        if (_hasActivePass && _activePassOpacity > 0)
        {
            DrawBitmapToTarget(
                _maskBitmap,
                _passMaskBitmap!,
                false,
                _activePassOpacity);
        }
    }

    private void DrawStampBatch(
        ID2D1Image target,
        PrimitiveBlend primitiveBlend,
        IReadOnlyList<WipeBrushStamp> stamps,
        int firstStampIndex,
        int stampCount,
        float opacityScale = 1)
    {
        EnsureSoftBrush(stamps);
        ID2D1Image? previousTarget = _drawingContext.Target;
        var previousTransform = _drawingContext.Transform;
        var previousAntialiasMode = _drawingContext.AntialiasMode;
        var previousPrimitiveBlend = _drawingContext.PrimitiveBlend;
        try
        {
            DrawStampsToTarget(
                target,
                false,
                primitiveBlend,
                stamps,
                firstStampIndex,
                stampCount,
                opacityScale);
        }
        finally
        {
            RestoreDrawingState(
                previousTarget,
                previousTransform,
                previousAntialiasMode,
                previousPrimitiveBlend);
        }
    }

    private void ClearBitmap(ID2D1Image target)
    {
        ID2D1Image? previousTarget = _drawingContext.Target;
        var previousTransform = _drawingContext.Transform;
        var previousAntialiasMode = _drawingContext.AntialiasMode;
        var previousPrimitiveBlend = _drawingContext.PrimitiveBlend;
        try
        {
            DrawStampsToTarget(
                target,
                true,
                PrimitiveBlend.SourceOver,
                [],
                0,
                0);
        }
        finally
        {
            RestoreDrawingState(
                previousTarget,
                previousTransform,
                previousAntialiasMode,
                previousPrimitiveBlend);
        }
    }

    private void DrawBitmapToTarget(
        ID2D1Image target,
        ID2D1Bitmap1 source,
        bool clearTarget,
        float opacity)
    {
        ID2D1Image? previousTarget = _drawingContext.Target;
        var previousTransform = _drawingContext.Transform;
        var previousAntialiasMode = _drawingContext.AntialiasMode;
        var previousPrimitiveBlend = _drawingContext.PrimitiveBlend;
        try
        {
            _drawingContext.Target = target;
            _drawingContext.Transform = Matrix3x2.Identity;
            _drawingContext.AntialiasMode = AntialiasMode.PerPrimitive;
            _drawingContext.PrimitiveBlend = PrimitiveBlend.SourceOver;
            _drawingContext.BeginDraw();
            try
            {
                if (clearTarget)
                {
                    _drawingContext.Clear(new Color4(0, 0, 0, 0));
                }

                _drawingContext.DrawBitmap(
                    source,
                    Math.Clamp(opacity, 0, 1),
                    BitmapInterpolationMode.NearestNeighbor);
            }
            finally
            {
                _drawingContext.EndDraw();
            }
        }
        finally
        {
            RestoreDrawingState(
                previousTarget,
                previousTransform,
                previousAntialiasMode,
                previousPrimitiveBlend);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cachedPath = null;
        _cachedStreamPath = null;
        _cachedGeometry = null;
        _streamStates = [];

        try
        {
            _drawingContext.Target = null;
        }
        catch
        {
            // 終了時は残りの所有リソース解放を優先する。
        }

        TryDispose(_whiteBrush);
        TryDispose(_softBrush);
        TryDispose(_softGradientStops);
        TryDispose(_rectangleShapeMask);
        TryDispose(_handShapeMask);
        TryDispose(_shoePrintShapeMask);
        ReleaseUserShapeMask();
        TryDispose(_passMaskBitmap);
        TryDispose(_committedMaskBitmap);
        TryDispose(_maskBitmap);
        TryDispose(_drawingContext);
    }

    private void DrawMask(
        bool clearMask,
        IReadOnlyList<WipeBrushStamp> stamps,
        WipeMaskAccumulationMode accumulationMode)
    {
        if (accumulationMode == WipeMaskAccumulationMode.PerPassMaximum &&
            stamps.Count > 0)
        {
            DrawPerPassMask(clearMask, stamps);
            return;
        }

        DrawSourceOverMask(clearMask, stamps);
    }

    private void DrawSourceOverMask(
        bool clearMask,
        IReadOnlyList<WipeBrushStamp> stamps)
    {
        if (!clearMask && stamps.Count == 0)
        {
            return;
        }
        EnsureSoftBrush(stamps);

        ID2D1Image? previousTarget = _drawingContext.Target;
        var previousTransform = _drawingContext.Transform;
        var previousAntialiasMode = _drawingContext.AntialiasMode;
        var previousPrimitiveBlend = _drawingContext.PrimitiveBlend;

        try
        {
            DrawStampsToTarget(
                _maskBitmap,
                clearMask,
                PrimitiveBlend.SourceOver,
                stamps,
                0,
                stamps.Count);
        }
        finally
        {
            RestoreDrawingState(
                previousTarget,
                previousTransform,
                previousAntialiasMode,
                previousPrimitiveBlend);
        }
    }

    private void DrawPerPassMask(
        bool clearMask,
        IReadOnlyList<WipeBrushStamp> stamps)
    {
        EnsureSoftBrush(stamps);
        EnsurePassMaskBitmap();

        ID2D1Image? previousTarget = _drawingContext.Target;
        var previousTransform = _drawingContext.Transform;
        var previousAntialiasMode = _drawingContext.AntialiasMode;
        var previousPrimitiveBlend = _drawingContext.PrimitiveBlend;

        try
        {
            if (clearMask)
            {
                DrawStampsToTarget(
                    _maskBitmap,
                    true,
                    PrimitiveBlend.SourceOver,
                    stamps,
                    0,
                    0);
            }

            var firstStampIndex = 0;
            while (firstStampIndex < stamps.Count)
            {
                var group = stamps[firstStampIndex].AccumulationGroup;
                var nextGroupIndex = firstStampIndex + 1;
                while (nextGroupIndex < stamps.Count &&
                       stamps[nextGroupIndex].AccumulationGroup == group)
                {
                    nextGroupIndex++;
                }

                var stampCount = nextGroupIndex - firstStampIndex;
                var passOpacity = ResolvePassOpacity(
                    stamps,
                    firstStampIndex,
                    stampCount);
                if (passOpacity <= 0)
                {
                    firstStampIndex = nextGroupIndex;
                    continue;
                }

                DrawStampsToTarget(
                    _passMaskBitmap!,
                    true,
                    ResolvePassPrimitiveBlend(
                        stamps[firstStampIndex].Shape),
                    stamps,
                    firstStampIndex,
                    stampCount,
                    1 / passOpacity);
                CompositePassMask(passOpacity);
                firstStampIndex = nextGroupIndex;
            }
        }
        finally
        {
            RestoreDrawingState(
                previousTarget,
                previousTransform,
                previousAntialiasMode,
                previousPrimitiveBlend);
        }
    }

    private void DrawStampsToTarget(
        ID2D1Image target,
        bool clearTarget,
        PrimitiveBlend primitiveBlend,
        IReadOnlyList<WipeBrushStamp> stamps,
        int firstStampIndex,
        int stampCount,
        float opacityScale = 1)
    {
        EnsureShapeMasks(stamps, firstStampIndex, stampCount);
        _drawingContext.Target = target;
        _drawingContext.Transform = Matrix3x2.Identity;
        _drawingContext.AntialiasMode = AntialiasMode.PerPrimitive;
        _drawingContext.PrimitiveBlend = primitiveBlend;
        _drawingContext.BeginDraw();
        try
        {
            if (clearTarget)
            {
                _drawingContext.Clear(new Color4(0, 0, 0, 0));
            }

            var activeTransform = Matrix3x2.Identity;
            var lastStampIndex = firstStampIndex + stampCount;
            for (var stampIndex = firstStampIndex;
                 stampIndex < lastStampIndex;
                 stampIndex++)
            {
                var stamp = stamps[stampIndex];
                var stampOpacity = ScaleStampOpacity(
                    stamp.Alpha,
                    opacityScale);
                var antialiasMode = RequiresAliasedOpacityMask(stamp.Shape)
                    ? AntialiasMode.Aliased
                    : AntialiasMode.PerPrimitive;
                if (_drawingContext.AntialiasMode != antialiasMode)
                {
                    // Direct2DのFillOpacityMaskはAliasedでなければ描画されない。
                    _drawingContext.AntialiasMode = antialiasMode;
                }

                var stampTransform = GetStampTransform(stamp);
                if (stampTransform != activeTransform)
                {
                    _drawingContext.Transform = stampTransform;
                    activeTransform = stampTransform;
                }

                if (stamp.Shape is not
                    (GlassWipeBrushShape.Circle or GlassWipeBrushShape.Ellipse))
                {
                    _whiteBrush.Opacity = stampOpacity;
                    _drawingContext.FillOpacityMask(
                        GetShapeMask(stamp.Shape),
                        _whiteBrush,
                        new Vortice.RawRectF(-1, -1, 1, 1),
                        null);
                    continue;
                }

                if (stamp.Softness > 0 && _softBrush is not null)
                {
                    _softBrush.Center = stamp.Center;
                    _softBrush.RadiusX = stamp.RadiusX;
                    _softBrush.RadiusY = stamp.RadiusY;
                    _softBrush.Opacity = stampOpacity;
                    _drawingContext.FillEllipse(
                        new Ellipse(
                            stamp.Center,
                            stamp.RadiusX,
                            stamp.RadiusY),
                        _softBrush);
                    continue;
                }

                _whiteBrush.Opacity = stampOpacity;
                _drawingContext.FillEllipse(
                    new Ellipse(
                        stamp.Center,
                        stamp.RadiusX,
                        stamp.RadiusY),
                    _whiteBrush);
            }
        }
        finally
        {
            _drawingContext.EndDraw();
        }
    }

    private void EnsureShapeMasks(
        IReadOnlyList<WipeBrushStamp> stamps,
        int firstStampIndex,
        int stampCount)
    {
        var lastStampIndex = firstStampIndex + stampCount;
        for (var stampIndex = firstStampIndex;
             stampIndex < lastStampIndex;
             stampIndex++)
        {
            var stamp = stamps[stampIndex];
            switch (stamp.Shape)
            {
                case GlassWipeBrushShape.Rectangle:
                    EnsureShapeMask(
                        GlassWipeBrushShape.Rectangle,
                        stamp.Softness,
                        ref _rectangleBinaryMask,
                        ref _rectangleShapeMask,
                        ref _rectangleMaskSoftness);
                    break;
                case GlassWipeBrushShape.Hand:
                    EnsureShapeMask(
                        GlassWipeBrushShape.Hand,
                        stamp.Softness,
                        ref _handBinaryMask,
                        ref _handShapeMask,
                        ref _handMaskSoftness);
                    break;
                case GlassWipeBrushShape.ShoePrint:
                    EnsureShapeMask(
                        GlassWipeBrushShape.ShoePrint,
                        stamp.Softness,
                        ref _shoePrintBinaryMask,
                        ref _shoePrintShapeMask,
                        ref _shoePrintMaskSoftness);
                    break;
                case GlassWipeBrushShape.UserImage:
                    EnsureUserShapeMask(stamp);
                    break;
            }
        }
    }

    private void EnsureUserShapeMask(WipeBrushStamp stamp)
    {
        if (stamp.UserBrushId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "使用するユーザーブラシが選択されていません。");
        }

        var requiresReload = _userBinaryMask is null ||
            _userMaskId != stamp.UserBrushId ||
            stamp.UserBrushRevision != _userMaskRevision ||
            stamp.UserBrushPixelWidth != _userMaskPixelWidth ||
            stamp.UserBrushPixelHeight != _userMaskPixelHeight;
        if (requiresReload)
        {
            if (!_userBrushLibrary.TryLoadMask(
                    stamp.UserBrushId,
                    out var loadedMask,
                    out var entry,
                    out var error))
            {
                throw new InvalidOperationException(
                    error ?? "ユーザーブラシ画像を利用できません。");
            }

            if (!MatchesUserBrushEntry(stamp, entry))
            {
                throw new InvalidOperationException(
                    "ユーザーブラシ画像が保存時から置き換えられたか、寸法情報が一致しません。ブラシを選び直してください。");
            }

            var oldBitmap = _userShapeMask;
            _userShapeMask = null;
            _userBinaryMask = loadedMask;
            _userMaskId = entry.Id;
            _userMaskRevision = entry.Revision;
            _userMaskPixelWidth = entry.PixelWidth;
            _userMaskPixelHeight = entry.PixelHeight;
            _userMaskSoftness = -1;
            TryDispose(oldBitmap);
        }

        EnsureShapeMask(
            GlassWipeBrushShape.UserImage,
            stamp.Softness,
            ref _userBinaryMask,
            ref _userShapeMask,
            ref _userMaskSoftness);
    }

    private void ReleaseUserShapeMask()
    {
        var bitmap = _userShapeMask;
        _userShapeMask = null;
        _userBinaryMask = null;
        _userMaskId = Guid.Empty;
        _userMaskRevision = 0;
        _userMaskPixelWidth = 0;
        _userMaskPixelHeight = 0;
        _userMaskSoftness = -1;
        TryDispose(bitmap);
    }

    internal static bool MatchesUserBrushEntry(
        WipeBrushStamp stamp,
        UserBrushEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return stamp.UserBrushId != Guid.Empty &&
            stamp.UserBrushRevision >= 1 &&
            stamp.UserBrushPixelWidth >= 1 &&
            stamp.UserBrushPixelHeight >= 1 &&
            stamp.UserBrushId == entry.Id &&
            stamp.UserBrushRevision == entry.Revision &&
            stamp.UserBrushPixelWidth == entry.PixelWidth &&
            stamp.UserBrushPixelHeight == entry.PixelHeight;
    }

    private void EnsureShapeMask(
        GlassWipeBrushShape shape,
        float softness,
        ref WipeBrushBinaryMask? binaryMask,
        ref ID2D1Bitmap1? bitmap,
        ref float cachedSoftness)
    {
        var sanitizedSoftness = float.IsFinite(softness)
            ? Math.Clamp(softness, 0, 1)
            : 0;
        if (bitmap is not null &&
            MathF.Abs(sanitizedSoftness - cachedSoftness) <= 0.000001f)
        {
            return;
        }

        binaryMask ??= WipeBrushMaskRasterizer.CreateBinaryMask(shape);
        var alpha = WipeBrushMaskRasterizer.CreateFeatheredAlpha(
            binaryMask,
            sanitizedSoftness);
        ID2D1Bitmap1? newBitmap = null;
        var handle = GCHandle.Alloc(alpha, GCHandleType.Pinned);
        try
        {
            var properties = new BitmapProperties1(
                new PixelFormat(
                    Format.A8_UNorm,
                    Vortice.DCommon.AlphaMode.Straight),
                96,
                96,
                BitmapOptions.None);
            newBitmap = _drawingContext.CreateBitmap(
                new SizeI(
                    WipeBrushMaskRasterizer.MaskSize,
                    WipeBrushMaskRasterizer.MaskSize),
                handle.AddrOfPinnedObject(),
                WipeBrushMaskRasterizer.MaskSize,
                properties);

            var oldBitmap = bitmap;
            bitmap = newBitmap;
            newBitmap = null;
            cachedSoftness = sanitizedSoftness;
            TryDispose(oldBitmap);
        }
        finally
        {
            handle.Free();
            TryDispose(newBitmap);
        }
    }

    private ID2D1Bitmap1 GetShapeMask(GlassWipeBrushShape shape) =>
        shape switch
        {
            GlassWipeBrushShape.Rectangle => _rectangleShapeMask!,
            GlassWipeBrushShape.Hand => _handShapeMask!,
            GlassWipeBrushShape.ShoePrint => _shoePrintShapeMask!,
            GlassWipeBrushShape.UserImage => _userShapeMask!,
            _ => throw new ArgumentOutOfRangeException(
                nameof(shape),
                shape,
                "画像マスクを使用しないブラシ形状です。"),
        };

    internal static bool RequiresAliasedOpacityMask(
        GlassWipeBrushShape shape) =>
        shape is not
            (GlassWipeBrushShape.Circle or GlassWipeBrushShape.Ellipse);

    internal static Matrix3x2 GetStampTransform(WipeBrushStamp stamp)
    {
        if (stamp.Shape is
            GlassWipeBrushShape.Circle or GlassWipeBrushShape.Ellipse)
        {
            return stamp.Transform;
        }

        var mirrorScale = stamp.Mirror
            ? stamp.Shape switch
            {
                GlassWipeBrushShape.Hand => new Vector2(-1, 1),
                GlassWipeBrushShape.ShoePrint => new Vector2(1, -1),
                GlassWipeBrushShape.UserImage => new Vector2(-1, 1),
                _ => Vector2.One,
            }
            : Vector2.One;
        return Matrix3x2.CreateScale(
                stamp.RadiusX * mirrorScale.X,
                stamp.RadiusY * mirrorScale.Y) *
            Matrix3x2.CreateTranslation(stamp.Center) *
            stamp.Transform;
    }

    /// <summary>
    /// 片道内の形状カバー率から分離して一度だけ適用する強度を取得します。
    /// </summary>
    internal static float ResolvePassOpacity(
        IReadOnlyList<WipeBrushStamp> stamps,
        int firstStampIndex,
        int stampCount)
    {
        ArgumentNullException.ThrowIfNull(stamps);
        var first = Math.Clamp(firstStampIndex, 0, stamps.Count);
        var boundedCount = Math.Min(
            stamps.Count - first,
            Math.Max(0, stampCount));
        var last = first + boundedCount;
        var opacity = 0f;
        for (var stampIndex = first; stampIndex < last; stampIndex++)
        {
            var stampOpacity = stamps[stampIndex].Alpha;
            if (float.IsFinite(stampOpacity))
            {
                opacity = MathF.Max(
                    opacity,
                    Math.Clamp(stampOpacity, 0, 1));
            }
        }

        return opacity;
    }

    /// <summary>
    /// 片道強度を除いたスタンプ不透明度へ安全に変換します。
    /// </summary>
    internal static float ScaleStampOpacity(float opacity, float scale)
    {
        if (!float.IsFinite(opacity) || !float.IsFinite(scale))
        {
            return 0;
        }

        return Math.Clamp(opacity * scale, 0, 1);
    }

    /// <summary>
    /// 片道内のスタンプ密度で端部と中央の強度が変わらない合成方式を取得します。
    /// 画像マスクは半透明の輪郭を重ねて一回拭きと同じ太さを保ちます。
    /// </summary>
    private static PrimitiveBlend ResolvePassPrimitiveBlend(
        GlassWipeBrushShape shape) =>
        UsesMaximumPassCoverage(shape)
            ? PrimitiveBlend.Max
            : PrimitiveBlend.SourceOver;

    internal static bool UsesMaximumPassCoverage(
        GlassWipeBrushShape shape) =>
        shape is
            GlassWipeBrushShape.Circle or
            GlassWipeBrushShape.Ellipse or
            GlassWipeBrushShape.Rectangle;

    private void CompositePassMask(float opacity)
    {
        _drawingContext.Target = _maskBitmap;
        _drawingContext.Transform = Matrix3x2.Identity;
        _drawingContext.AntialiasMode = AntialiasMode.PerPrimitive;
        _drawingContext.PrimitiveBlend = PrimitiveBlend.SourceOver;
        _drawingContext.BeginDraw();
        try
        {
            _drawingContext.DrawBitmap(
                _passMaskBitmap!,
                Math.Clamp(opacity, 0, 1),
                BitmapInterpolationMode.NearestNeighbor);
        }
        finally
        {
            _drawingContext.EndDraw();
        }
    }

    private void EnsurePassMaskBitmap()
    {
        _passMaskBitmap ??= CreateMaskBitmap(_drawingContext);
    }

    private void EnsureCommittedMaskBitmap()
    {
        _committedMaskBitmap ??= CreateMaskBitmap(_drawingContext);
    }

    private static ID2D1Bitmap1 CreateMaskBitmap(
        ID2D1DeviceContext drawingContext)
    {
        var bitmapProperties = new BitmapProperties1(
            new PixelFormat(
                Format.B8G8R8A8_UNorm,
                Vortice.DCommon.AlphaMode.Premultiplied),
            96,
            96,
            BitmapOptions.Target);
        return drawingContext.CreateBitmap(
            new SizeI(MaskSize, MaskSize),
            bitmapProperties);
    }

    private sealed class BufferedStampCollection : ICollection<WipeBrushStamp>
    {
        private readonly List<WipeBrushStamp> _items;
        private readonly Action<IReadOnlyList<WipeBrushStamp>> _flush;

        public BufferedStampCollection(
            int capacity,
            Action<IReadOnlyList<WipeBrushStamp>> flush)
        {
            _items = new List<WipeBrushStamp>(Math.Max(1, capacity));
            _flush = flush ?? throw new ArgumentNullException(nameof(flush));
        }

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        public void Add(WipeBrushStamp item)
        {
            _items.Add(item);
            if (_items.Count >= _items.Capacity)
            {
                Flush();
            }
        }

        public void Flush()
        {
            if (_items.Count == 0)
            {
                return;
            }

            _flush(_items);
            _items.Clear();
        }

        public void Clear() => _items.Clear();

        public bool Contains(WipeBrushStamp item) => _items.Contains(item);

        public void CopyTo(WipeBrushStamp[] array, int arrayIndex) =>
            _items.CopyTo(array, arrayIndex);

        public bool Remove(WipeBrushStamp item) => _items.Remove(item);

        public IEnumerator<WipeBrushStamp> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private void RestoreDrawingState(
        ID2D1Image? previousTarget,
        Matrix3x2 previousTransform,
        AntialiasMode previousAntialiasMode,
        PrimitiveBlend previousPrimitiveBlend)
    {
        try
        {
            _drawingContext.Transform = previousTransform;
        }
        finally
        {
            try
            {
                _drawingContext.PrimitiveBlend = previousPrimitiveBlend;
            }
            finally
            {
                try
                {
                    _drawingContext.AntialiasMode = previousAntialiasMode;
                }
                finally
                {
                    try
                    {
                        _drawingContext.Target = previousTarget;
                    }
                    finally
                    {
                        previousTarget?.Dispose();
                    }
                }
            }
        }
    }

    private void EnsureSoftBrush(IReadOnlyList<WipeBrushStamp> stamps)
    {
        var softness = 0f;
        foreach (var stamp in stamps)
        {
            if (stamp.Softness > 0)
            {
                softness = Math.Clamp(stamp.Softness, 0, 1);
                break;
            }
        }

        if (MathF.Abs(softness - _softBrushSoftness) <= 0.000001f)
        {
            return;
        }

        ID2D1GradientStopCollection? newGradientStops = null;
        ID2D1RadialGradientBrush? newSoftBrush = null;
        try
        {
            if (softness > 0)
            {
                var opaque = new Color4(1, 1, 1, 1);
                var transparent = new Color4(1, 1, 1, 0);
                var solidEnd = Math.Clamp(1 - softness, 0, 1);
                GradientStop[] gradientStops = solidEnd > 0.000001f
                    ?
                    [
                        new GradientStop(0, opaque),
                        new GradientStop(solidEnd, opaque),
                        new GradientStop(1, transparent),
                    ]
                    :
                    [
                        new GradientStop(0, opaque),
                        new GradientStop(1, transparent),
                    ];
                newGradientStops = _drawingContext.CreateGradientStopCollection(
                    gradientStops,
                    Gamma.StandardRgb,
                    ExtendMode.Clamp);
                newSoftBrush = _drawingContext.CreateRadialGradientBrush(
                    new RadialGradientBrushProperties(
                        Vector2.Zero,
                        Vector2.Zero,
                        1,
                        1),
                    newGradientStops);
            }

            TryDispose(_softBrush);
            TryDispose(_softGradientStops);
            _softBrush = newSoftBrush;
            _softGradientStops = newGradientStops;
            _softBrushSoftness = softness;
            newSoftBrush = null;
            newGradientStops = null;
        }
        finally
        {
            TryDispose(newSoftBrush);
            TryDispose(newGradientStops);
        }
    }

    private static void TryDispose(IDisposable? resource)
    {
        try
        {
            resource?.Dispose();
        }
        catch
        {
            // 終了時は他の所有リソース解放を継続する。
        }
    }
}
