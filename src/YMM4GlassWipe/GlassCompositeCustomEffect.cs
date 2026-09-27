// SPDX-License-Identifier: MPL-2.0

using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YMM4GlassWipe;

internal sealed class GlassCompositeCustomEffect : D2D1CustomShaderEffectBase
{
    private const string ShaderResourceName = "YMM4GlassWipe.Shaders.GlassComposite.cso";
    private readonly bool _outsideDropletPass;

    public GlassCompositeCustomEffect(
        IGraphicsDevicesAndContext devices,
        bool outsideDropletPass = false)
        : base(Create<EffectImpl>(devices))
    {
        _outsideDropletPass = outsideDropletPass;
    }

    public void ApplyParameters(
        GlassWipeParameters parameters,
        float inputLeft,
        float inputTop,
        float inputWidth,
        float inputHeight)
    {
        // 通常表示の最終合成では、水滴時刻と合体ONへの切替を省略する。
        var applyDropletTiming = _outsideDropletPass || parameters.DebugView != GlassWipeDebugView.Final;
        // 設定適用中は合体を止め、必要な描画パスだけ末尾で再評価する。
        SetValue((int)EffectImpl.Properties.OutsideDropletMergeEnabled, 0f);

        var shaderRegionWidth = parameters.RegionWidth;
        var shaderRegionHeight = parameters.RegionHeight;
        if (parameters.RegionShape == GlassWipeRegionShape.Quad &&
            parameters.QuadMapping.IsValid)
        {
            var nominalPixelSize = parameters.Quad.GetNominalPixelSize(
                inputWidth,
                inputHeight);
            shaderRegionWidth = nominalPixelSize.X / MathF.Max(inputWidth, 1);
            shaderRegionHeight = nominalPixelSize.Y / MathF.Max(inputHeight, 1);
        }

        SetValue((int)EffectImpl.Properties.InputWidth, inputWidth);
        SetValue((int)EffectImpl.Properties.InputHeight, inputHeight);
        SetValue((int)EffectImpl.Properties.InputLeft, inputLeft);
        SetValue((int)EffectImpl.Properties.InputTop, inputTop);
        SetValue((int)EffectImpl.Properties.RegionCenterX, parameters.RegionCenterX);
        SetValue((int)EffectImpl.Properties.RegionCenterY, parameters.RegionCenterY);
        SetValue((int)EffectImpl.Properties.RegionWidth, shaderRegionWidth);
        SetValue((int)EffectImpl.Properties.RegionHeight, shaderRegionHeight);
        SetValue((int)EffectImpl.Properties.FogAmount, parameters.FogAmount);
        SetValue((int)EffectImpl.Properties.TintMix, parameters.TintMix);
        SetValue((int)EffectImpl.Properties.FogTintRed, parameters.FogTintRed);
        SetValue((int)EffectImpl.Properties.FogTintGreen, parameters.FogTintGreen);
        SetValue((int)EffectImpl.Properties.FogTintBlue, parameters.FogTintBlue);
        SetValue((int)EffectImpl.Properties.RegionFeather, parameters.RegionFeather);
        SetValue(
            (int)EffectImpl.Properties.DebugView,
            _outsideDropletPass
                ? (float)GlassWipeDebugView.Final
                : (float)parameters.DebugView);
        SetValue((int)EffectImpl.Properties.RegionShape, (float)parameters.RegionShape);
        SetValue((int)EffectImpl.Properties.RegionRotationCos, parameters.RegionRotationCos);
        SetValue((int)EffectImpl.Properties.RegionRotationSin, parameters.RegionRotationSin);
        SetValue(
            (int)EffectImpl.Properties.QuadValid,
            parameters.QuadMapping.IsValid ? 1f : 0f);
        SetValue(
            (int)EffectImpl.Properties.QuadInverseM11,
            parameters.QuadMapping.InputToLocal.M11);
        SetValue(
            (int)EffectImpl.Properties.QuadInverseM12,
            parameters.QuadMapping.InputToLocal.M12);
        SetValue(
            (int)EffectImpl.Properties.QuadInverseM13,
            parameters.QuadMapping.InputToLocal.M13);
        SetValue(
            (int)EffectImpl.Properties.QuadInverseM21,
            parameters.QuadMapping.InputToLocal.M21);
        SetValue(
            (int)EffectImpl.Properties.QuadInverseM22,
            parameters.QuadMapping.InputToLocal.M22);
        SetValue(
            (int)EffectImpl.Properties.QuadInverseM23,
            parameters.QuadMapping.InputToLocal.M23);
        SetValue(
            (int)EffectImpl.Properties.QuadInverseM31,
            parameters.QuadMapping.InputToLocal.M31);
        SetValue(
            (int)EffectImpl.Properties.QuadInverseM32,
            parameters.QuadMapping.InputToLocal.M32);
        SetValue(
            (int)EffectImpl.Properties.QuadInverseM33,
            parameters.QuadMapping.InputToLocal.M33);
        SetValue((int)EffectImpl.Properties.WipeResidue, parameters.WipeResidue);
        SetValue((int)EffectImpl.Properties.WipeVariation, parameters.WipeVariation);
        SetValue((int)EffectImpl.Properties.FogNoise, parameters.FogNoise);
        SetValue((int)EffectImpl.Properties.NoiseSeed, parameters.NoiseSeed);
        SetValue((int)EffectImpl.Properties.OutsideDropletAmount, parameters.OutsideDropletAmount);
        SetValue((int)EffectImpl.Properties.OutsideDropletSizeScale, parameters.OutsideDropletSizeScale);
        SetValue((int)EffectImpl.Properties.OutsideDropletStrength, parameters.OutsideDropletStrength);
        SetValue((int)EffectImpl.Properties.OutsideDropletSeed, parameters.OutsideDropletSeed);
        SetValue((int)EffectImpl.Properties.OutsideDropletRainEnabled, parameters.OutsideDropletRainEnabled);
        SetValue((int)EffectImpl.Properties.OutsideDropletRainStartSeconds, parameters.OutsideDropletRainStartSeconds);
        SetValue((int)EffectImpl.Properties.OutsideDropletRainDurationSeconds, parameters.OutsideDropletRainDurationSeconds);
        SetValue((int)EffectImpl.Properties.OutsideDropletFallFrequency, parameters.OutsideDropletFallFrequency);
        if (applyDropletTiming)
        {
            SetValue(
                (int)EffectImpl.Properties.OutsideDropletLocalTimeSeconds,
                parameters.OutsideDropletLocalTimeSeconds);
        }
        SetValue(
            (int)EffectImpl.Properties.OutsideDropletFallEnabled,
            parameters.OutsideDropletFallEnabled);
        SetValue(
            (int)EffectImpl.Properties.OutsideDropletFallingRatio,
            parameters.OutsideDropletFallingRatio);
        SetValue(
            (int)EffectImpl.Properties.OutsideDropletFallSpeedScale,
            parameters.OutsideDropletFallSpeedScale);
        SetValue(
            (int)EffectImpl.Properties.OutsideDropletTrailLength,
            parameters.OutsideDropletTrailLength);
        SetValue(
            (int)EffectImpl.Properties.OutsideDropletDeformWithSurface,
            parameters.OutsideDropletDeformWithSurface);
        SetValue(
            (int)EffectImpl.Properties.OutsideDropletAppearance,
            parameters.OutsideDropletAppearance);
        SetValue(
            (int)EffectImpl.Properties.QuadForwardM11,
            parameters.QuadMapping.LocalToInput.M11);
        SetValue(
            (int)EffectImpl.Properties.QuadForwardM12,
            parameters.QuadMapping.LocalToInput.M12);
        SetValue(
            (int)EffectImpl.Properties.QuadForwardM13,
            parameters.QuadMapping.LocalToInput.M13);
        SetValue(
            (int)EffectImpl.Properties.QuadForwardM21,
            parameters.QuadMapping.LocalToInput.M21);
        SetValue(
            (int)EffectImpl.Properties.QuadForwardM22,
            parameters.QuadMapping.LocalToInput.M22);
        SetValue(
            (int)EffectImpl.Properties.QuadForwardM23,
            parameters.QuadMapping.LocalToInput.M23);
        SetValue(
            (int)EffectImpl.Properties.QuadForwardM31,
            parameters.QuadMapping.LocalToInput.M31);
        SetValue(
            (int)EffectImpl.Properties.QuadForwardM32,
            parameters.QuadMapping.LocalToInput.M32);
        SetValue(
            (int)EffectImpl.Properties.QuadForwardM33,
            parameters.QuadMapping.LocalToInput.M33);
        SetValue(
            (int)EffectImpl.Properties.OutsideDropletOutlineOpacity,
            parameters.OutsideDropletOutlineOpacity);
        SetValue(
            (int)EffectImpl.Properties.OutsideDropletRenderPass,
            _outsideDropletPass ? 1f : 0f);
        if (applyDropletTiming)
        {
            SetValue(
                (int)EffectImpl.Properties.OutsideDropletMergeEnabled,
                parameters.OutsideDropletMergeEnabled);
        }
    }

    public void ApplyOutsideDropletLocalTime(float localTimeSeconds)
    {
        SetValue(
            (int)EffectImpl.Properties.OutsideDropletLocalTimeSeconds,
            localTimeSeconds);
    }

#if DEBUG
    internal bool GetCachedForDebug() =>
        GetBoolValue((int)Property.Cached);

    internal void SetCachedForDebug(bool cached) =>
        SetValue((int)Property.Cached, cached);
#endif

    [CustomEffect(3)]
    private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
    {
        private GlassCompositeConstants _constantBuffer;
        private GlassCompositeGpuConstants _gpuConstantBuffer;
        private readonly OutsideDropletMergeSimulation _outsideDropletMergeSimulation = new();
        private float _requestedOutsideDropletMergeEnabled;
#if DEBUG
        private readonly int _diagnosticEffectInstanceId =
            RenderCallbackTrace.CreateEffectInstanceId();
        private long _diagnosticCallbackSequence;
        private int _mergeFailureDiagnosticRecorded;
#endif

        public EffectImpl()
            : base(ShaderResourceLoader.Load(ShaderResourceName))
        {
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.InputWidth)]
        public float InputWidth
        {
            get => _constantBuffer.InputWidth;
            set => SetConstant(ref _constantBuffer.InputWidth, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.InputHeight)]
        public float InputHeight
        {
            get => _constantBuffer.InputHeight;
            set => SetConstant(ref _constantBuffer.InputHeight, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.InputLeft)]
        public float InputLeft
        {
            get => _constantBuffer.InputLeft;
            set => SetConstant(ref _constantBuffer.InputLeft, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.InputTop)]
        public float InputTop
        {
            get => _constantBuffer.InputTop;
            set => SetConstant(ref _constantBuffer.InputTop, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.RegionCenterX)]
        public float RegionCenterX
        {
            get => _constantBuffer.RegionCenterX;
            set => SetConstant(ref _constantBuffer.RegionCenterX, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.RegionCenterY)]
        public float RegionCenterY
        {
            get => _constantBuffer.RegionCenterY;
            set => SetConstant(ref _constantBuffer.RegionCenterY, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.RegionWidth)]
        public float RegionWidth
        {
            get => _constantBuffer.RegionWidth;
            set => SetConstant(ref _constantBuffer.RegionWidth, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.RegionHeight)]
        public float RegionHeight
        {
            get => _constantBuffer.RegionHeight;
            set => SetConstant(ref _constantBuffer.RegionHeight, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.FogAmount)]
        public float FogAmount
        {
            get => _constantBuffer.FogAmount;
            set => SetConstant(ref _constantBuffer.FogAmount, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.TintMix)]
        public float TintMix
        {
            get => _constantBuffer.TintMix;
            set => SetConstant(ref _constantBuffer.TintMix, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.FogTintRed)]
        public float FogTintRed
        {
            get => _constantBuffer.FogTintRed;
            set => SetConstant(ref _constantBuffer.FogTintRed, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.FogTintGreen)]
        public float FogTintGreen
        {
            get => _constantBuffer.FogTintGreen;
            set => SetConstant(ref _constantBuffer.FogTintGreen, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.FogTintBlue)]
        public float FogTintBlue
        {
            get => _constantBuffer.FogTintBlue;
            set => SetConstant(ref _constantBuffer.FogTintBlue, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.RegionFeather)]
        public float RegionFeather
        {
            get => _constantBuffer.RegionFeather;
            set => SetConstant(ref _constantBuffer.RegionFeather, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.DebugView)]
        public float DebugView
        {
            get => _constantBuffer.DebugView;
            set => SetConstant(ref _constantBuffer.DebugView, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.RegionShape)]
        public float RegionShape
        {
            get => _constantBuffer.RegionShape;
            set => SetConstant(ref _constantBuffer.RegionShape, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.RegionRotationCos)]
        public float RegionRotationCos
        {
            get => _constantBuffer.RegionRotationCos;
            set => SetConstant(ref _constantBuffer.RegionRotationCos, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.RegionRotationSin)]
        public float RegionRotationSin
        {
            get => _constantBuffer.RegionRotationSin;
            set => SetConstant(ref _constantBuffer.RegionRotationSin, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadValid)]
        public float QuadValid
        {
            get => _constantBuffer.QuadValid;
            set => SetConstant(ref _constantBuffer.QuadValid, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadInverseM11)]
        public float QuadInverseM11
        {
            get => _constantBuffer.QuadInverseM11;
            set => SetConstant(ref _constantBuffer.QuadInverseM11, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadInverseM12)]
        public float QuadInverseM12
        {
            get => _constantBuffer.QuadInverseM12;
            set => SetConstant(ref _constantBuffer.QuadInverseM12, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadInverseM13)]
        public float QuadInverseM13
        {
            get => _constantBuffer.QuadInverseM13;
            set => SetConstant(ref _constantBuffer.QuadInverseM13, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadInverseM21)]
        public float QuadInverseM21
        {
            get => _constantBuffer.QuadInverseM21;
            set => SetConstant(ref _constantBuffer.QuadInverseM21, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadInverseM22)]
        public float QuadInverseM22
        {
            get => _constantBuffer.QuadInverseM22;
            set => SetConstant(ref _constantBuffer.QuadInverseM22, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadInverseM23)]
        public float QuadInverseM23
        {
            get => _constantBuffer.QuadInverseM23;
            set => SetConstant(ref _constantBuffer.QuadInverseM23, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadInverseM31)]
        public float QuadInverseM31
        {
            get => _constantBuffer.QuadInverseM31;
            set => SetConstant(ref _constantBuffer.QuadInverseM31, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadInverseM32)]
        public float QuadInverseM32
        {
            get => _constantBuffer.QuadInverseM32;
            set => SetConstant(ref _constantBuffer.QuadInverseM32, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadInverseM33)]
        public float QuadInverseM33
        {
            get => _constantBuffer.QuadInverseM33;
            set => SetConstant(ref _constantBuffer.QuadInverseM33, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.WipeResidue)]
        public float WipeResidue
        {
            get => _constantBuffer.WipeResidue;
            set => SetConstant(ref _constantBuffer.WipeResidue, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.WipeVariation)]
        public float WipeVariation
        {
            get => _constantBuffer.WipeVariation;
            set => SetConstant(ref _constantBuffer.WipeVariation, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.FogNoise)]
        public float FogNoise
        {
            get => _constantBuffer.FogNoise;
            set => SetConstant(ref _constantBuffer.FogNoise, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.NoiseSeed)]
        public float NoiseSeed
        {
            get => _constantBuffer.NoiseSeed;
            set => SetConstant(ref _constantBuffer.NoiseSeed, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletAmount)]
        public float OutsideDropletAmount
        {
            get => _constantBuffer.OutsideDropletAmount;
            set => SetConstant(ref _constantBuffer.OutsideDropletAmount, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletSizeScale)]
        public float OutsideDropletSizeScale
        {
            get => _constantBuffer.OutsideDropletSizeScale;
            set => SetConstant(ref _constantBuffer.OutsideDropletSizeScale, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletStrength)]
        public float OutsideDropletStrength
        {
            get => _constantBuffer.OutsideDropletStrength;
            set => SetConstant(ref _constantBuffer.OutsideDropletStrength, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletSeed)]
        public float OutsideDropletSeed
        {
            get => _constantBuffer.OutsideDropletSeed;
            set => SetConstant(ref _constantBuffer.OutsideDropletSeed, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletLocalTimeSeconds)]
        public float OutsideDropletLocalTimeSeconds
        {
            get => _constantBuffer.OutsideDropletLocalTimeSeconds;
            set => SetOutsideDropletLocalTimeSeconds(value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletFallEnabled)]
        public float OutsideDropletFallEnabled
        {
            get => _constantBuffer.OutsideDropletFallEnabled;
            set => SetConstant(ref _constantBuffer.OutsideDropletFallEnabled, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletFallingRatio)]
        public float OutsideDropletFallingRatio
        {
            get => _constantBuffer.OutsideDropletFallingRatio;
            set => SetConstant(ref _constantBuffer.OutsideDropletFallingRatio, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletFallSpeedScale)]
        public float OutsideDropletFallSpeedScale
        {
            get => _constantBuffer.OutsideDropletFallSpeedScale;
            set => SetConstant(ref _constantBuffer.OutsideDropletFallSpeedScale, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletTrailLength)]
        public float OutsideDropletTrailLength
        {
            get => _constantBuffer.OutsideDropletTrailLength;
            set => SetConstant(ref _constantBuffer.OutsideDropletTrailLength, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletDeformWithSurface)]
        public float OutsideDropletDeformWithSurface
        {
            get => _constantBuffer.OutsideDropletDeformWithSurface;
            set => SetConstant(ref _constantBuffer.OutsideDropletDeformWithSurface, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletAppearance)]
        public float OutsideDropletAppearance
        {
            get => _constantBuffer.OutsideDropletAppearance;
            set => SetConstant(ref _constantBuffer.OutsideDropletAppearance, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletMergeEnabled)]
        public float OutsideDropletMergeEnabled
        {
            get => _requestedOutsideDropletMergeEnabled;
            set
            {
                _requestedOutsideDropletMergeEnabled =
                    float.IsFinite(value) && value >= 0.5f ? 1f : 0f;
                RebuildMergeConstants();
                UploadConstants();
            }
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletOutlineOpacity)]
        public float OutsideDropletOutlineOpacity
        {
            get => _constantBuffer.OutsideDropletOutlineOpacity;
            set => SetConstant(ref _constantBuffer.OutsideDropletOutlineOpacity, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletRainEnabled)]
        public float OutsideDropletRainEnabled
        {
            get => _constantBuffer.OutsideDropletRainEnabled;
            set => SetConstant(ref _constantBuffer.OutsideDropletRainEnabled, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletRainStartSeconds)]
        public float OutsideDropletRainStartSeconds
        {
            get => _constantBuffer.OutsideDropletRainStartSeconds;
            set => SetConstant(ref _constantBuffer.OutsideDropletRainStartSeconds, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletRainDurationSeconds)]
        public float OutsideDropletRainDurationSeconds
        {
            get => _constantBuffer.OutsideDropletRainDurationSeconds;
            set => SetConstant(ref _constantBuffer.OutsideDropletRainDurationSeconds, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletFallFrequency)]
        public float OutsideDropletFallFrequency
        {
            get => _constantBuffer.OutsideDropletFallFrequency;
            set => SetConstant(ref _constantBuffer.OutsideDropletFallFrequency, OutsideDropletFrequency.Normalize(value));
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.OutsideDropletRenderPass)]
        public float OutsideDropletRenderPass
        {
            get => _constantBuffer.OutsideDropletRenderPass;
            set => SetConstant(ref _constantBuffer.OutsideDropletRenderPass, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadForwardM11)]
        public float QuadForwardM11
        {
            get => _constantBuffer.QuadForwardM11;
            set => SetConstant(ref _constantBuffer.QuadForwardM11, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadForwardM12)]
        public float QuadForwardM12
        {
            get => _constantBuffer.QuadForwardM12;
            set => SetConstant(ref _constantBuffer.QuadForwardM12, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadForwardM13)]
        public float QuadForwardM13
        {
            get => _constantBuffer.QuadForwardM13;
            set => SetConstant(ref _constantBuffer.QuadForwardM13, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadForwardM21)]
        public float QuadForwardM21
        {
            get => _constantBuffer.QuadForwardM21;
            set => SetConstant(ref _constantBuffer.QuadForwardM21, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadForwardM22)]
        public float QuadForwardM22
        {
            get => _constantBuffer.QuadForwardM22;
            set => SetConstant(ref _constantBuffer.QuadForwardM22, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadForwardM23)]
        public float QuadForwardM23
        {
            get => _constantBuffer.QuadForwardM23;
            set => SetConstant(ref _constantBuffer.QuadForwardM23, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadForwardM31)]
        public float QuadForwardM31
        {
            get => _constantBuffer.QuadForwardM31;
            set => SetConstant(ref _constantBuffer.QuadForwardM31, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadForwardM32)]
        public float QuadForwardM32
        {
            get => _constantBuffer.QuadForwardM32;
            set => SetConstant(ref _constantBuffer.QuadForwardM32, value);
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.QuadForwardM33)]
        public float QuadForwardM33
        {
            get => _constantBuffer.QuadForwardM33;
            set => SetConstant(ref _constantBuffer.QuadForwardM33, value);
        }

        protected override void UpdateConstants()
        {
            _gpuConstantBuffer.Core = _constantBuffer;
#if DEBUG
            var drawInformationAvailable = drawInformation is not null;
#endif
            drawInformation?.SetPixelShaderConstantBuffer(_gpuConstantBuffer);
#if DEBUG
            RenderCallbackTrace.RecordConstants(
                _diagnosticEffectInstanceId,
                Interlocked.Increment(ref _diagnosticCallbackSequence),
                drawInformationAvailable,
                _constantBuffer);
#endif
        }

        public override void MapInputRectsToOutputRect(
            RawRect[] inputRects,
            RawRect[] inputOpaqueSubRects,
            out RawRect outputRect,
            out RawRect outputOpaqueSubRect)
        {
            outputRect = inputRects.Length > 0 ? inputRects[0] : default;
            outputOpaqueSubRect = inputOpaqueSubRects.Length > 0
                ? inputOpaqueSubRects[0]
                : default;
#if DEBUG
            RenderCallbackTrace.RecordMapInputRectsToOutputRect(
                _diagnosticEffectInstanceId,
                Interlocked.Increment(ref _diagnosticCallbackSequence),
                inputRects,
                inputOpaqueSubRects,
                outputRect,
                outputOpaqueSubRect);
#endif
        }

        public override void MapOutputRectToInputRects(
            RawRect outputRect,
            RawRect[] inputRects)
        {
            if (inputRects.Length > 0)
            {
                inputRects[0] = OutsideDropletRefraction.Expand(
                    outputRect, OutsideDropletRefraction.GetPadding(in _constantBuffer));
            }

            if (inputRects.Length > 1)
            {
                inputRects[1] = outputRect;
            }

            if (inputRects.Length > 2)
            {
                if (_constantBuffer.OutsideDropletRenderPass >= 0.5f)
                {
                    inputRects[2] = outputRect;
                }
                else
                {
                    inputRects[2] = new RawRect(
                        0,
                        0,
                        WipeMaskRenderer.MaskSize,
                        WipeMaskRenderer.MaskSize);
                }
            }
#if DEBUG
            RenderCallbackTrace.RecordMapOutputRectToInputRects(
                _diagnosticEffectInstanceId,
                Interlocked.Increment(ref _diagnosticCallbackSequence),
                outputRect,
                inputRects);
#endif
        }

        public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect)
        {
            var padding = OutsideDropletRefraction.GetPadding(in _constantBuffer);
            return inputIndex == 0 && padding > 0
                ? OutsideDropletRefraction.Expand(invalidInputRect, padding)
                : base.MapInvalidRect(inputIndex, invalidInputRect);
        }

        private void SetConstant(ref float field, float value)
        {
            field = value;
            UploadConstants();
        }

        private void SetOutsideDropletLocalTimeSeconds(float value)
        {
            _constantBuffer.OutsideDropletLocalTimeSeconds = value;
            RebuildMergeConstants();
            UploadConstants();
        }

        private void RebuildMergeConstants()
        {
            _constantBuffer.OutsideDropletMergeEnabled = 0f;
            if (_constantBuffer.OutsideDropletRenderPass < 0.5f &&
                !IsOutsideDropletCoverageDebugView(_constantBuffer.DebugView))
            {
                return;
            }

            if (_requestedOutsideDropletMergeEnabled < 0.5f ||
                _constantBuffer.OutsideDropletFallEnabled < 0.5f ||
                _constantBuffer.OutsideDropletAmount <= 0f ||
                _constantBuffer.OutsideDropletFallingRatio <= 0f)
            {
                return;
            }

            try
            {
                var frame = _outsideDropletMergeSimulation.Evaluate(in _constantBuffer);
                if (_gpuConstantBuffer.TryApply(frame))
                {
                    _constantBuffer.OutsideDropletMergeEnabled = 1f;
                }

#if DEBUG
                System.Threading.Interlocked.Exchange(
                    ref _mergeFailureDiagnosticRecorded,
                    0);
#endif
            }
            catch (Exception exception)
            {
                // 合体計算の失敗時は既存の水滴描画へフォールバックします。
#if DEBUG
                if (System.Threading.Interlocked.Exchange(
                        ref _mergeFailureDiagnosticRecorded,
                        1) == 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"OutsideDropletMerge:{exception.GetType().Name}");
                }
#else
                _ = exception;
#endif
            }
        }

        private static bool IsOutsideDropletCoverageDebugView(float debugView) =>
            debugView >= 5.5f && debugView < 6.5f;

        private void UploadConstants()
        {
#if DEBUG
            UploadConstantsWithoutRenderCallbackTrace();
#else
            UpdateConstants();
#endif
        }

#if DEBUG
        private void UploadConstantsWithoutRenderCallbackTrace()
        {
            _gpuConstantBuffer.Core = _constantBuffer;
            drawInformation?.SetPixelShaderConstantBuffer(_gpuConstantBuffer);
        }
#endif

        internal enum Properties
        {
            InputWidth = 0,
            InputHeight,
            InputLeft,
            InputTop,
            RegionCenterX,
            RegionCenterY,
            RegionWidth,
            RegionHeight,
            FogAmount,
            TintMix,
            FogTintRed,
            FogTintGreen,
            FogTintBlue,
            RegionFeather,
            DebugView,
            RegionShape,
            RegionRotationCos,
            RegionRotationSin,
            QuadValid,
            QuadInverseM11,
            QuadInverseM12,
            QuadInverseM13,
            QuadInverseM21,
            QuadInverseM22,
            QuadInverseM23,
            QuadInverseM31,
            QuadInverseM32,
            QuadInverseM33,
            WipeResidue,
            WipeVariation,
            FogNoise,
            NoiseSeed,
            OutsideDropletAmount,
            OutsideDropletSizeScale,
            OutsideDropletStrength,
            OutsideDropletSeed,
            OutsideDropletLocalTimeSeconds,
            OutsideDropletFallEnabled,
            OutsideDropletFallingRatio,
            OutsideDropletFallSpeedScale,
            OutsideDropletTrailLength,
            OutsideDropletDeformWithSurface,
            QuadForwardM11,
            QuadForwardM12,
            QuadForwardM13,
            QuadForwardM21,
            QuadForwardM22,
            QuadForwardM23,
            QuadForwardM31,
            QuadForwardM32,
            QuadForwardM33,
            OutsideDropletAppearance,
            OutsideDropletMergeEnabled,
            OutsideDropletOutlineOpacity,
            OutsideDropletRenderPass,
            OutsideDropletRainEnabled,
            OutsideDropletRainStartSeconds,
            OutsideDropletRainDurationSeconds,
            OutsideDropletFallFrequency,
        }
    }
}
