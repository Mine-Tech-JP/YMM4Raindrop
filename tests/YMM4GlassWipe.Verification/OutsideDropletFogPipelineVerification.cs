// SPDX-License-Identifier: MPL-2.0

namespace YMM4GlassWipe.Verification;

internal static class OutsideDropletFogPipelineVerification
{
    internal static void VerifyRouting()
    {
        Verify(false, 0f, 1f, GlassWipeDebugView.Final, "水滴量0");
        Verify(false, 1f, 0f, GlassWipeDebugView.Final, "水滴強さ0");
        Verify(true, 1f, 1f, GlassWipeDebugView.Final, "最終結果");
        Verify(true, 1f, 1f, GlassWipeDebugView.Blurred, "ぼかし表示");
        Verify(false, 1f, 1f, GlassWipeDebugView.RegionMask, "既存Debug View");
    }

    private static void Verify(
        bool expected,
        float amount,
        float strength,
        GlassWipeDebugView debugView,
        string scenario)
    {
        var actual = OutsideDropletFogPipeline.ShouldUsePreFog(
            amount,
            strength,
            debugView);
        if (actual == expected)
        {
            return;
        }

        throw new InvalidOperationException($"水滴先行合成の経路判定が不正です: {scenario}");
    }
}
