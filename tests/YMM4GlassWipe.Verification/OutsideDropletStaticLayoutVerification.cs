// SPDX-License-Identifier: MPL-2.0

using System.Numerics;
using YMM4GlassWipe;

namespace YMM4GlassWipe.Verification;

/// <summary>
/// 初期重なり静止水滴の決定性、成長上限、探索上限を確認します。
/// GPU描画と呼び出し側への転送はこのCPU検証の対象外です。
/// </summary>
internal static class OutsideDropletStaticLayoutVerification
{
    public static void VerifyDeterminismAndInitialAbsorption()
    {
        var patternSize = new Vector2(1920f, 1080f);
        Check(OutsideDropletStaticLayout.TryCreate(patternSize, 2.358f, 4197U, 0.614f, out var first),
            "実使用設定の静止滴レイアウトを作成できる必要があります。");
        Check(OutsideDropletStaticLayout.TryCreate(patternSize, 2.358f, 4197U, 0.614f, out var second),
            "同一設定の静止滴レイアウトを再作成できる必要があります。");
        Check(first.Changes.SequenceEqual(second.Changes),
            "静止滴の初期吸収は評価順序に依存せず決定的である必要があります。");
        Check(first.PackedStates.SequenceEqual(second.PackedStates) &&
            first.ColumnCounts == second.ColumnCounts && first.CellCounts == second.CellCounts,
            "GPU転送用の初期状態マスクも決定的である必要があります。");
        Check(first.Changes.Any(change => change.Visibility == 0f),
            "初期から重なる小滴は非表示へ変更される必要があります。");
        Check(first.Changes.Any(change => change.Visibility == 1f && change.RadiusScale > 1f),
            "小滴を吸収する大滴は半径を増やす必要があります。");
        foreach (var change in first.Changes)
        {
            Check(float.IsFinite(change.Visibility) && float.IsFinite(change.RadiusScale) &&
                change.Visibility is >= 0f and <= 1f &&
                change.RadiusScale is >= 1f and <= 1.075f,
                "変更後の可視性と半径倍率は有限かつ許容範囲内である必要があります。");
            Check(first.TryGetState(change.Layer, change.CellX, change.CellY, out var visibility, out var radiusScale) &&
                visibility == change.Visibility && radiusScale == change.RadiusScale,
                "変更済みセルIDは同じ状態を返す必要があります。");
        }

        Check(!first.TryGetState(uint.MaxValue, int.MaxValue, int.MaxValue,
                out var defaultVisibility, out var defaultScale) &&
            defaultVisibility == 1f && defaultScale == 1f,
            "未変更セルIDは既定状態とfalseを返す必要があります。");
    }

    public static void VerifyMultiStageGrowthAndCellBoundaryCap()
    {
        OutsideDropletStaticLayout? multiStage = null;
        for (uint seed = 1; seed <= 256; seed++)
        {
            if (OutsideDropletStaticLayout.TryCreate(new Vector2(256f, 144f), 0.25f, seed, 1f, out var layout) &&
                layout.Changes.Count(change => change.Visibility == 0f) >= 2 &&
                layout.Changes.Any(change => change.Visibility == 1f && change.RadiusScale > 1f))
            {
                multiStage = layout;
                break;
            }
        }

        Check(multiStage is not null,
            "固定探索範囲で複数の小滴を消した初期吸収を再現できる必要があります。");
        Check(multiStage!.Changes.Where(change => change.Visibility == 1f)
                .All(change => change.RadiusScale <= 1.075f),
            "初期の静止滴成長は吸収件数にかかわらず1.075倍を超えてはいけません。");
        var limit = OutsideDropletStaticLayout.GetRadiusLimit(10f, new Vector2(2.2f, 5f), 2f, 1f);
        Check(limit >= 1f && limit < 1.1f,
            "セル端に近い水滴は元半径を縮めず、境界を越えない増大量へ制限する必要があります。");
        Check(OutsideDropletStaticLayout.GetRadiusLimit(10f, new Vector2(5f, 5f), 2f, 1f) == 1.35f,
            "セル中央では動的成長用の1.35倍上限を返す必要があります。");
    }

    public static void VerifyBoundedFailure()
    {
        Check(!OutsideDropletStaticLayout.TryCreate(
                new Vector2(100000f, 100000f), 0.25f, 1U, 1f, out _),
            "セル列挙上限を超える入力ではレイアウト作成を中止する必要があります。");
    }

    public static void VerifyCellKeyCollisionDoesNotInvalidateLayout()
    {
        Check(TryFindDenseLayoutCollision(out var seed, out var first, out var second),
            "上限内の固定セル列挙でcellKey衝突を再現できる必要があります。");
        Check(first != second &&
            OutsideDropletRandom.GetCellKey(first.X, first.Y, seed, first.Layer) ==
            OutsideDropletRandom.GetCellKey(second.X, second.Y, seed, second.Layer),
            "再現したセル対は異なるIDかつ同じcellKeyである必要があります。");
        Check(OutsideDropletStaticLayout.TryCreate(
                new Vector2(1920f, 1080f), 0.25f, seed, 0f, out var layout),
            "cellKeyが衝突しても添字マスクの初期レイアウトを無効化してはいけません。");
        Check(!layout.TryGetState(first.Layer, first.X, first.Y, out var firstVisibility, out var firstScale) &&
            !layout.TryGetState(second.Layer, second.X, second.Y, out var secondVisibility, out var secondScale) &&
            firstVisibility == 1f && firstScale == 1f && secondVisibility == 1f && secondScale == 1f,
            "衝突した別セルもIDごとに独立して既定状態を返す必要があります。");
    }

    private static bool TryFindDenseLayoutCollision(out uint seed, out CellAddress first, out CellAddress second)
    {
        foreach (var candidateSeed in Enumerable.Range(0, 64).Select(value => (uint)value))
        {
            var keys = new Dictionary<uint, CellAddress>();
            foreach (var layer in new[] { (20f, 101U), (44f, 307U), (92f, 701U) })
            {
                var cellSize = layer.Item1 * 0.25f;
                var columns = (int)MathF.Floor(1920f / cellSize) + 1;
                var rows = (int)MathF.Floor(1080f / cellSize) + 1;
                for (var y = 0; y < rows; y++)
                {
                    for (var x = 0; x < columns; x++)
                    {
                        var address = new CellAddress(layer.Item2, x, y);
                        var key = OutsideDropletRandom.GetCellKey(x, y, candidateSeed, layer.Item2);
                        if (keys.TryGetValue(key, out first))
                        {
                            seed = candidateSeed;
                            second = address;
                            return true;
                        }

                        keys.Add(key, address);
                    }
                }
            }
        }

        seed = 0U;
        first = default;
        second = default;
        return false;
    }

    private readonly record struct CellAddress(uint Layer, int X, int Y);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
