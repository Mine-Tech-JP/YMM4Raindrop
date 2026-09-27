// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace YMM4GlassWipe.Verification;

// HLSLの数値参照モデル。GPU上の描画確認は別の実機試験として扱う。
internal static class OutsideDropletShaderAppearanceVerification
{
    public static void VerifyTransparentSurface()
    {
        var backgrounds = new[]
        {
            Vector3.Zero,
            Vector3.One,
            new Vector3(0.1f, 0.7f, 0.3f),
            new Vector3(0.5f),
        };
        foreach (var background in backgrounds)
        {
            foreach (var point in new[] { Vector2.Zero, new Vector2(0.25f, 0.1f), new Vector2(1.5f, 0) })
            {
                Near(background, Composite(background, Lighting(point, 0.02f), 0.5f, 1),
                    "透明表示の中心と被覆外は背景色を維持する必要があります。");
            }

            for (var y = -12; y <= 12; y++)
            {
                for (var x = -12; x <= 12; x++)
                {
                    var point = new Vector2(x / 10f, y / 10f);
                    var lighting = Lighting(point, 0.05f);
                    Near(background, Composite(background, lighting, 0, 1),
                        "水滴の濃さ0では背景色を維持する必要があります。");
                    var color = Composite(background, lighting, 1, 1);
                    Check(FiniteUnit(color.X) && FiniteUnit(color.Y) && FiniteUnit(color.Z),
                        "透明表示のRGBは有限な0から1である必要があります。");
                }
            }
        }

        var gray = new Vector3(0.5f);
        var upperRim = Composite(gray, Lighting(new Vector2(0, -0.97f), 0.02f), 0.5f, 1);
        var lowerRim = Composite(gray, Lighting(new Vector2(0, 0.97f), 0.02f), 0.5f, 1);
        Check(upperRim.X < 0.35f && lowerRim.X < upperRim.X,
            "輪郭は暗く、下側により強い影を付ける必要があります。");
        var clear = Composite(gray, Lighting(new Vector2(0, 0.9f), 0.02f), 0.5f, 1);
        var translucent = Composite(gray, Lighting(new Vector2(0, 0.9f), 0.02f), 0.5f, 0.375f);
        Check(translucent.X > clear.X && translucent.X < gray.X,
            "黒い輪郭の不透明度を下げると背景色へ近づく必要があります。");
        var reflection = Composite(gray, Lighting(new Vector2(-0.28f, -0.62f), 0.02f), 0.5f, 1);
        Check(reflection.X > gray.X,
            "上側の小さな反射は背景より明るくなる必要があります。");
    }

    public static void VerifyShaderRouting()
    {
        var root = FindRepositoryRoot();
        var shader = ShaderVerificationSource.ReadAllText(Path.Combine(root, "src", "YMM4GlassWipe", "Shaders", "GlassComposite.hlsl"));
        var lighting = Function(shader, "float3 EvaluateOutsideDropletLighting(");
        var transparent = Function(shader, "float3 EvaluateTransparentOutsideDropletLighting(");
        var composite = Function(shader, "float3 CompositeOutsideDropletSurface(");
        var capsule = Function(shader, "float3 EvaluateOutsideDropletCapsule(");
        Check(shader.Contains("float outsideDropletAppearance : packoffset(c11.z);", StringComparison.Ordinal),
            "表示モードはc11.zで受け取る必要があります。");
        Check(lighting.Contains("if (outsideDropletAppearance >= 0.5f)", StringComparison.Ordinal) &&
              lighting.Contains("EvaluateTransparentOutsideDropletLighting(", StringComparison.Ordinal),
            "静止滴と落下滴で共通の陰影切り替えを使用する必要があります。");
        Check(capsule.Contains("if (outsideDropletAppearance >= 0.5f)", StringComparison.Ordinal),
            "水筋にも表示モードを反映する必要があります。");
        var branchStart = composite.IndexOf("if (outsideDropletAppearance >= 0.5f)", StringComparison.Ordinal);
        var legacyStart = composite.IndexOf("float surface =", StringComparison.Ordinal);
        Check(branchStart >= 0 && legacyStart > branchStart, "透明合成と従来合成の分岐が必要です。");
        var branch = composite[branchStart..legacyStart];
        Check(branch.Contains("return lerp(", StringComparison.Ordinal) &&
              !branch.Contains("stableSurfaceColor", StringComparison.Ordinal) &&
              !branch.Contains("dropletLighting.z", StringComparison.Ordinal),
            "透明合成は中心を着色せず、陰影だけを背景へ合成する必要があります。");
        foreach (var forbidden in new[] { "outsideDropletLocalTimeSeconds", "OutsideDropletRandom(", "HashNoise2D(", "Sample(", "SampleLevel(", "fwidth(" })
        {
            Check(!transparent.Contains(forbidden, StringComparison.Ordinal),
                "透明表示の陰影へ時刻・乱数・背景参照・新たな画面微分を追加してはいけません: " + forbidden);
        }
        Check(!transparent.Contains("sceneColor", StringComparison.Ordinal),
            "陰影と被覆の形は背景の明暗へ依存してはいけません。");
    }

    internal static Vector3 Lighting(Vector2 point, float edgeWidth)
    {
        var distance = point.Length();
        var coverage = 1 - Smooth(1 - edgeWidth, 1 + edgeWidth, distance);
        var direction = point / Math.Max(distance, 0.0001f);
        var lowerSide = Math.Clamp(direction.Y * 0.5f + 0.5f, 0, 1);
        var rimWidth = float.Lerp(0.08f, 0.22f, lowerSide);
        var innerEdge = 1 - rimWidth;
        var rim = Smooth(innerEdge - edgeWidth, innerEdge + edgeWidth, distance) * coverage;
        var shadow = rim * float.Lerp(0.62f, 0.96f, lowerSide);
        var spot = 1 - Smooth(0.10f, 0.25f,
            ((point - new Vector2(-0.28f, -0.62f)) * new Vector2(0.8f, 1.4f)).Length());
        return new Vector3(spot * coverage * (1 - rim) * 0.65f, shadow, coverage);
    }

    internal static Vector3 Composite(Vector3 scene, Vector3 lighting, float influence, float opacity)
    {
        influence = Math.Clamp(influence, 0, 1);
        var result = Vector3.Lerp(scene, Vector3.Zero,
            Math.Clamp(lighting.Y * influence * 1.75f, 0, 1) * Math.Clamp(opacity, 0, 1));
        return Vector3.Lerp(result, Vector3.One,
            Math.Clamp(lighting.X * influence * 0.65f, 0, 1));
    }

    private static float Smooth(float start, float end, float value)
    {
        var amount = Math.Clamp((value - start) / (end - start), 0, 1);
        return amount * amount * (3 - 2 * amount);
    }

    private static bool FiniteUnit(float value) => float.IsFinite(value) && value is >= 0 and <= 1;

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
        var brace = source.IndexOf('{', start);
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
                    return source[start..(index + 1)];
                }
            }
        }
        throw new InvalidOperationException("HLSL関数の終了が見つかりません。");
    }
}
