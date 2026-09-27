// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace YMM4GlassWipe.Verification;

/// <summary>
/// 外側水滴をGaussianBlurの前に合成する経路を、限定したCPU数値参照とHLSL構造照合で検証します。
/// GPUのラスタライザー、Direct2DのGaussianBlur、または実機YMM4の画素結果を証明するものではありません。
/// </summary>
internal static class OutsideDropletFogRenderingVerification
{
    /// <summary>
    /// 細い黒縁が曇りでぼけ、拭き取りまたは曇り量ゼロでは鮮明な水滴合成へ戻ることを確認します。
    /// </summary>
    public static void VerifyFoggedDropletReference()
    {
        // [1,2,1]/4の3点カーネルによる限定参照。Direct2Dの実装そのものではありません。
        var clearWithDroplets = new[] { 0.8f, 0.0f, 0.8f };
        var blurredWithDroplets = Gaussian3(clearWithDroplets);

        var foggedOutline = CompositeFog(clearWithDroplets[1], blurredWithDroplets[1], fogAmount: 1f, wipeMask: 0f);
        // 直前版は水滴を含まない0.8の背景をぼかし、fog側の固定係数0.06/0.16で黒縁を重ねます。
        var legacySharpOutline = 0.8f * (1f - Saturate(1f * (0.06f / 0.16f) * 1.75f));
        Check(foggedOutline > 0.35f && foggedOutline < 0.45f,
            "ぼかし入力へ入った細い黒縁は近傍色へ広がって薄くなる必要があります。");
        Check(legacySharpOutline > 0.27f && legacySharpOutline < 0.28f,
            "直前版の固定減衰では細い黒縁も輪郭の形を保って残ります。");
        Check(foggedOutline - legacySharpOutline > 0.1f,
            "曇り越しの細い水滴縁は直前版の鮮明な再重ねより薄くなる必要があります。");

        var wipedOutline = CompositeFog(clearWithDroplets[1], blurredWithDroplets[1], fogAmount: 1f, wipeMask: 1f);
        var noFogOutline = CompositeFog(clearWithDroplets[1], blurredWithDroplets[1], fogAmount: 0f, wipeMask: 0f);
        Near(clearWithDroplets[1], wipedOutline,
            "拭き取りマスク1では曇り前の水滴入り映像へ戻る必要があります。");
        Near(clearWithDroplets[1], noFogOutline,
            "曇り量0では水滴入りの元映像をそのまま返す必要があります。");

        var noBlurOutline = CompositeFog(clearWithDroplets[1], clearWithDroplets[1], fogAmount: 1f, wipeMask: 0f);
        Near(clearWithDroplets[1], noBlurOutline,
            "ぼかし半径0の入力では元の水滴合成結果を保持する必要があります。");
    }

    /// <summary>
    /// 透明表示の黒縁だけが不透明度に比例して減衰し、白い反射と入力Alphaを保つことを確認します。
    /// </summary>
    public static void VerifyOutlineOpacityAndAlphaReference()
    {
        const float scene = 0.8f;
        var opacity0 = CompositeTransparentOutline(scene, shadowLighting: 0.8f, highlightLighting: 0f, influence: 1f, outlineOpacity: 0f);
        var opacity50 = CompositeTransparentOutline(scene, shadowLighting: 0.8f, highlightLighting: 0f, influence: 1f, outlineOpacity: 0.5f);
        var opacity100 = CompositeTransparentOutline(scene, shadowLighting: 0.8f, highlightLighting: 0f, influence: 1f, outlineOpacity: 1f);
        Near(0.8f, opacity0, "黒い輪郭の不透明度0%では黒い縁を加えてはいけません。");
        Near(0.4f, opacity50, "飽和後に50%を掛ける黒い輪郭は背景の半分だけを黒へ寄せる必要があります。");
        Near(0f, opacity100, "黒い輪郭の不透明度100%では飽和した黒縁を維持する必要があります。");

        var incorrectPreSaturationOpacity50 = scene * (1f - Saturate(0.8f * 1f * 1.75f * 0.5f));
        Check(MathF.Abs(opacity50 - incorrectPreSaturationOpacity50) > 0.15f,
            "黒い輪郭の不透明度を飽和前へ掛ける回帰を数値例で検出する必要があります。");

        var reflectionOpacity0 = CompositeTransparentOutline(scene, shadowLighting: 0f, highlightLighting: 0.8f, influence: 1f, outlineOpacity: 0f);
        var reflectionOpacity50 = CompositeTransparentOutline(scene, shadowLighting: 0f, highlightLighting: 0.8f, influence: 1f, outlineOpacity: 0.5f);
        var reflectionOpacity100 = CompositeTransparentOutline(scene, shadowLighting: 0f, highlightLighting: 0.8f, influence: 1f, outlineOpacity: 1f);
        Near(reflectionOpacity0, reflectionOpacity50,
            "黒い縁がない白い反射は輪郭不透明度で減衰してはいけません。");
        Near(reflectionOpacity50, reflectionOpacity100,
            "白い反射は輪郭不透明度100%でも不変である必要があります。");

        foreach (var alpha in new[] { 0f, 0.3f, 1f })
        {
            var originalStraight = new Vector3(0.8f, 0.4f, 0.2f);
            var clearStraight = new Vector3(
                CompositeTransparentOutline(originalStraight.X, 0.8f, 0.2f, 1f, 0.5f),
                CompositeTransparentOutline(originalStraight.Y, 0.8f, 0.2f, 1f, 0.5f),
                CompositeTransparentOutline(originalStraight.Z, 0.8f, 0.2f, 1f, 0.5f));
            var original = new Vector4(originalStraight * alpha, alpha);
            var preprocessed = PreprocessTransparentDroplet(original, clearStraight);
            var premultiplied = new Vector3(preprocessed.X, preprocessed.Y, preprocessed.Z);
            Near(alpha, preprocessed.W, "入力Alphaは水滴前処理で変化してはいけません。");
            Near(clearStraight * alpha, premultiplied,
                "水滴入り元映像のRGBは元のAlphaで再度premultiplyする必要があります。");
            Check(IsFinite(premultiplied), "Alpha 0、0.3、1の水滴合成RGBは有限である必要があります。");
            if (alpha == 0f)
            {
                Near(Vector3.Zero, premultiplied,
                    "入力Alpha 0ではpremultiply後のRGBも0である必要があります。");
            }
        }
    }

    /// <summary>
    /// HLSLの3入力契約、前処理の入力依存、最終合成の単回水滴処理を照合します。
    /// </summary>
    public static void VerifyHlslRenderPassWiring()
    {
        var shader = ShaderVerificationSource.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "YMM4GlassWipe", "Shaders", "GlassComposite.hlsl"));
        Check(shader.Contains("Texture2D OriginalTexture : register(t0);", StringComparison.Ordinal) &&
              shader.Contains("Texture2D BlurredTexture : register(t1);", StringComparison.Ordinal) &&
              shader.Contains("Texture2D MaskTexture : register(t2);", StringComparison.Ordinal),
            "RenderPassを追加してもシェーダーはOriginal、Blurred、Maskの3入力を維持する必要があります。");
        Check(shader.Contains("float outsideDropletOutlineOpacity : packoffset(c12.w);", StringComparison.Ordinal),
            "黒い輪郭の不透明度はc12.wで受け取る必要があります。");
        Check(shader.Contains("float outsideDropletRenderPass : packoffset(c13.w);", StringComparison.Ordinal),
            "外側水滴の前処理passはc13.wで受け取る必要があります。");

        var composite = Function(shader, "float3 CompositeOutsideDropletSurface(");
        var transparentStart = composite.IndexOf("if (outsideDropletAppearance >= 0.5f)", StringComparison.Ordinal);
        var legacyStart = composite.IndexOf("float surface =", StringComparison.Ordinal);
        Check(transparentStart >= 0 && legacyStart > transparentStart,
            "透明表示と従来の水滴合成は分岐している必要があります。");
        var transparentBranch = composite[transparentStart..legacyStart];
        var legacyBranch = composite[legacyStart..];
        Check(transparentBranch.Contains("saturate(dropletLighting.y * safeInfluence * 1.75f)", StringComparison.Ordinal) &&
              transparentBranch.Contains("saturate(outsideDropletOutlineOpacity)", StringComparison.Ordinal),
            "黒い輪郭は影量を飽和してから不透明度を掛ける必要があります。");
        Check(!legacyBranch.Contains("outsideDropletOutlineOpacity", StringComparison.Ordinal),
            "白い輪郭を含む従来合成へ黒い輪郭の不透明度を使ってはいけません。");

        var renderPassMarker = "if (outsideDropletRenderPass >= 0.5f)";
        var renderPassStart = shader.IndexOf(renderPassMarker, StringComparison.Ordinal);
        Check(renderPassStart >= 0, "水滴前処理RenderPassの分岐が見つかりません。");
        var renderPassBrace = shader.IndexOf('{', renderPassStart);
        var renderPass = Block(shader, renderPassBrace);
        Check(renderPass.Contains("float4 clearWithDroplets = original;", StringComparison.Ordinal) &&
              renderPass.Contains("return clearWithDroplets;", StringComparison.Ordinal),
            "前処理passは元映像へ水滴を合成して返す必要があります。");
        foreach (var forbidden in new[] { "BlurredTexture", "MaskTexture", "sampledWipeMask", "effectiveWipeMask", "wipeMask" })
        {
            Check(!renderPass.Contains(forbidden, StringComparison.Ordinal),
                "前処理passはぼかし・拭き取りマスクを参照してはいけません: " + forbidden);
        }

        var afterRenderPass = shader[(renderPassBrace + renderPass.Length)..];
        Check(afterRenderPass.Contains("float4 blurred = BlurredTexture.Sample(BlurredSampler, blurredUv.xy);", StringComparison.Ordinal) &&
              afterRenderPass.Contains("MaskTexture.SampleLevel(MaskSampler, regionUv, 0).a", StringComparison.Ordinal),
            "最終passは水滴入りぼかし入力と拭き取りマスクを使用する必要があります。");
        Check(afterRenderPass.Contains("float4 result = original;", StringComparison.Ordinal) &&
              afterRenderPass.Contains("result = lerp(original, fogged, fogMask);", StringComparison.Ordinal) &&
              afterRenderPass.Contains("result.a = original.a;", StringComparison.Ordinal),
            "最終passは水滴入りclear入力と水滴入りblur入力を既存のfogMaskでblendし、Alphaを維持する必要があります。");
        Check(!afterRenderPass.Contains("GetOutsideDropletLighting(", StringComparison.Ordinal) &&
              !afterRenderPass.Contains("CompositeOutsideDropletSurface(", StringComparison.Ordinal),
            "最終passで水滴をもう一度合成してはいけません。");
    }

    private static float[] Gaussian3(float[] source)
    {
        Check(source.Length == 3, "参照Gaussianは中央の細い輪郭と両隣の3画素を受け取る必要があります。");
        return new[]
        {
            source[0] * 0.75f + source[1] * 0.25f,
            source[0] * 0.25f + source[1] * 0.50f + source[2] * 0.25f,
            source[1] * 0.25f + source[2] * 0.75f,
        };
    }

    private static float CompositeFog(float clearWithDroplets, float blurredWithDroplets, float fogAmount, float wipeMask) =>
        float.Lerp(clearWithDroplets, blurredWithDroplets, Saturate(fogAmount) * (1f - Saturate(wipeMask)));

    private static float CompositeTransparentOutline(
        float scene,
        float shadowLighting,
        float highlightLighting,
        float influence,
        float outlineOpacity)
    {
        var outlineAlpha = Saturate(shadowLighting * Saturate(influence) * 1.75f) * Saturate(outlineOpacity);
        var darkened = float.Lerp(scene, 0f, outlineAlpha);
        return float.Lerp(darkened, 1f, Saturate(highlightLighting * Saturate(influence) * 0.65f));
    }

    private static Vector4 PreprocessTransparentDroplet(Vector4 original, Vector3 clearStraight) =>
        new(clearStraight * original.W, original.W);

    private static float Saturate(float value) => Math.Clamp(value, 0f, 1f);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static void Near(float expected, float actual, string message) =>
        Check(MathF.Abs(expected - actual) < 0.000001f, message + $" expected={expected:F6}, actual={actual:F6}");

    private static void Near(Vector3 expected, Vector3 actual, string message) =>
        Check(Vector3.Distance(expected, actual) < 0.000001f, message);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "YMM4GlassWipe.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("リポジトリルートが見つかりません。");
    }

    private static string Function(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Check(start >= 0, "HLSL関数が見つかりません: " + signature);
        return Block(source, source.IndexOf('{', start));
    }

    private static string Block(string source, int brace)
    {
        Check(brace >= 0 && source[brace] == '{', "HLSLブロック開始が見つかりません。");
        var depth = 0;
        for (var index = brace; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[brace..(index + 1)];
                }
            }
        }

        throw new InvalidOperationException("HLSLブロックの終了が見つかりません。");
    }
}
