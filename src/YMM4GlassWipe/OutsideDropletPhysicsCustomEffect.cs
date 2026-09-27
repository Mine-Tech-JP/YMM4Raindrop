// SPDX-License-Identifier: MPL-2.0

using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YMM4GlassWipe;

/// <summary>インスタンス専用のCPU再生状態とGPU定数を所有します。</summary>
internal sealed class OutsideDropletPhysicsCustomEffect : IDisposable
{
    private readonly ID2D1Factory1 _factory;
    private readonly Guid _registration = Guid.NewGuid();
    private readonly ID2D1Effect _effect;
    private readonly EffectImpl _implementation;
    private readonly ID2D1Image _output;
    private OutsideDropletPhysicsSettings? _settings;
    private OutsideDropletPhysicsReplay? _replay;
    private OutsideDropletPhysicsSnapshot? _lower;
    private OutsideDropletPhysicsSnapshot? _upper;
    private GlassWipeParameters? _previousParameters;
    private (float Left, float Top, float Width, float Height) _previousBounds;
    private bool _disposed;
    private int _revision;

    public OutsideDropletPhysicsCustomEffect(IGraphicsDevicesAndContext devices)
    {
        _factory = devices.DeviceContext.Factory.QueryInterface<ID2D1Factory1>();
        ID2D1Effect? effect = null;
        ID2D1Image? output = null;
        bool registered = false;
        try
        {
            var shader = ShaderResourceLoader.Load("YMM4GlassWipe.Shaders.OutsideDropletPhysics.cso");
            EffectImpl? implementation = null;
            _factory.RegisterEffect(() => implementation = new EffectImpl(shader), _registration);
            registered = true;
            effect = new ID2D1Effect(devices.DeviceContext.CreateEffect(_registration));
            _implementation = implementation ?? throw new InvalidOperationException("簡易物理用シェーダーを生成できません。");
            output = effect.Output;
            _effect = effect;
            _output = output;
        }
        catch
        {
            output?.Dispose();
            effect?.Dispose();
            if (registered) _factory.UnregisterEffect(_registration);
            _factory.Dispose();
            throw;
        }
    }

    public ID2D1Image Output => _output;
    internal long IntegratedTicks => _replay?.Engine.IntegratedTicks ?? 0;
    internal int CacheBytes => _replay?.Bytes ?? 0;

    public void SetInput(ID2D1Image? input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _effect.SetInput(0, input, true);
    }

    public void ApplyParameters(GlassWipeParameters parameters, float left, float top, float width, float height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var bounds = (left, top, width, height);
        if (_previousParameters == parameters && _previousBounds == bounds) return;
        var settings = CreateSettings(parameters, width, height);
        if (_settings != settings)
        {
            _replay = new OutsideDropletPhysicsReplay(settings);
            _settings = settings;
            _lower = _upper = null;
        }

        var frame = Math.Max(0, parameters.OutsideDropletFrame);
        var fps = Math.Max(1, parameters.OutsideDropletFps);
        var tick = OutsideDropletPhysicsSimulation.TickForFrame(frame, fps);
        var fraction = ((frame % fps) * 120L % fps) / (double)fps;
        if (_lower?.Tick != tick)
        {
            _lower = _replay!.At(tick);
            _upper = _replay.At(checked(tick + 1));
        }
        var drops = OutsideDropletPhysicsFrame.Create(_lower!, _upper!, fraction);
        ApplyPacket(OutsideDropletPhysicsGpuConstants.Create(drops, parameters, left, top, width, height));
        _previousParameters = parameters;
        _previousBounds = bounds;
    }

    internal void ApplyPacket(OutsideDropletPhysicsGpuConstants packet)
    {
        _implementation.Packet = packet;
        _revision = (_revision + 1) & 0x7fffff;
        _effect.SetValue(0, (float)_revision);
    }

    internal static OutsideDropletPhysicsSettings CreateSettings(GlassWipeParameters parameters, float width, float height) => new(
        InitialCount: (int)Math.Round(Math.Clamp(parameters.OutsideDropletAmount, 0, 1) * 256),
        Seed: (uint)parameters.OutsideDropletSeed,
        Size: parameters.OutsideDropletSizeScale,
        Supply: parameters.OutsideDropletSupplyScale,
        Slip: parameters.OutsideDropletSlipScale,
        Speed: parameters.OutsideDropletFallSpeedScale,
        Fall: parameters.OutsideDropletFallEnabled >= .5f,
        Merge: parameters.OutsideDropletMergeEnabled >= .5f,
        Width: (double)width / height * 1080,
        StartTick: parameters.OutsideDropletRainEnabled >= .5f ? (long)Math.Ceiling(parameters.OutsideDropletRainStartSeconds * 120d) : 0,
        OnsetTicks: parameters.OutsideDropletRainEnabled >= .5f ? (long)Math.Ceiling(parameters.OutsideDropletRainDurationSeconds * 120d) : 0);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _effect.SetInput(0, null, true); }
        finally
        {
            try { _output.Dispose(); }
            finally
            {
                try { _effect.Dispose(); }
                finally
                {
                    try { _factory.UnregisterEffect(_registration); }
                    finally { _factory.Dispose(); }
                }
            }
        }
    }

    [CustomEffect(1)]
    private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
    {
        public OutsideDropletPhysicsGpuConstants Packet;
        private float _revision;
        public EffectImpl(byte[] shader) : base(shader) { }

        [CustomEffectProperty(PropertyType.Float, 0)]
        public float Revision { get => _revision; set { _revision = value; UpdateConstants(); } }

        protected override void UpdateConstants() => drawInformation?.SetPixelShaderConstantBuffer(Packet);
        public override void MapInputRectsToOutputRect(RawRect[] inputs, RawRect[] opaque, out RawRect output, out RawRect outputOpaque)
        {
            output = inputs[0];
            outputOpaque = opaque[0];
        }

        private int Padding => Packet.Frame.Z >= 1.5f && Packet.Frame.Z < 2.5f
            ? (int)Math.Clamp(Math.Ceiling(256d * Math.Max(Packet.Frame.Y / 1080d, .25d)) + 1, 0, int.MaxValue / 2)
            : 0;
        private RawRect Expand(RawRect rect) => new(
            (int)Math.Clamp((long)rect.Left - Padding, int.MinValue, int.MaxValue),
            (int)Math.Clamp((long)rect.Top - Padding, int.MinValue, int.MaxValue),
            (int)Math.Clamp((long)rect.Right + Padding, int.MinValue, int.MaxValue),
            (int)Math.Clamp((long)rect.Bottom + Padding, int.MinValue, int.MaxValue));
        public override void MapOutputRectToInputRects(RawRect output, RawRect[] inputs) => inputs[0] = Expand(output);
        public override RawRect MapInvalidRect(int inputIndex, RawRect rect) => Padding > 0 ? Expand(rect) : base.MapInvalidRect(inputIndex, rect);
    }
}
