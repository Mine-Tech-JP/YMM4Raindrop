// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe;

internal sealed record OutsideDropletPhysicsSettings(int InitialCount = 128, uint Seed = 7, double Size = 1,
    double Supply = 1, double Slip = 1, double Speed = 1, bool Fall = true, bool Merge = true,
    double Width = 1920, long StartTick = 0, long OnsetTicks = 0)
{
    public const double MinimumWidth = double.Epsilon;
    public const double MaximumWidth = 1e50;

    // 入力幅/高さ*1080を無補正で使う。距離の二乗計算を守る上限だけを設ける。
    public void Validate()
    {
        if (InitialCount is < 0 or > 256 || !double.IsFinite(Size) || Size is < .25 or > 4 ||
            !double.IsFinite(Supply) || Supply is < 0 or > 4 || !double.IsFinite(Slip) || Slip is < .25 or > 4 ||
            !double.IsFinite(Speed) || Speed is < .25 or > 4 || !double.IsFinite(Width) || Width <= 0 || Width > MaximumWidth ||
            StartTick < 0 || OnsetTicks < 0 || StartTick > long.MaxValue - OnsetTicks) throw new ArgumentOutOfRangeException(nameof(OutsideDropletPhysicsSettings), "物理設定が範囲外です。");
    }
}
