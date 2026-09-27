// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel.DataAnnotations;

namespace YMM4GlassWipe;

internal enum GlassWipeDebugView
{
    [Display(Name = "最終結果", Description = "曇りと拭き取りを合成した結果")]
    Final = 0,

    [Display(Name = "領域マスク", Description = "曇りを適用する領域")]
    RegionMask = 1,

    [Display(Name = "拭き取りマスク", Description = "現在フレームまでに累積した拭き取り領域")]
    WipeMask = 2,

    [Display(Name = "ぼかし", Description = "GaussianBlurの確認表示")]
    Blurred = 3,

    [Display(Name = "領域ローカルUV", Description = "曇りを適用する領域内の横位置と縦位置を色で確認")]
    RegionLocalUv = 4,

    [Display(Name = "四角形（四隅指定）の妥当性", Description = "有効な四角形（四隅指定）は緑、不正な形は赤で確認")]
    QuadValidity = 5,

#if DEBUG
    [Display(Name = "外側水滴の被覆", Description = "診断専用。水滴の被覆だけを白黒表示し、確認後は最終結果へ戻してください")]
    OutsideDropletCoverage = 6,

    [Display(Name = "座標位相（診断）", Description = "診断専用。赤はSV、緑はSCENE由来の入力座標、青は元画像UVとの差を表示します。確認後は最終結果へ戻してください")]
    CoordinatePhase = 7,

    [Display(Name = "領域座標差（診断）", Description = "診断専用。「入力領域全体」で、赤と緑は水滴用座標と入力座標のX/Y差を512倍し、青は差の大きさをさらに強調して表示します。マゼンタは測定範囲外、黄は無効値です。カーソル移動で色や模様が変わるか確認し、確認後は最終結果へ戻してください")]
    RegionCoordinateDelta = 8,

    [Display(Name = "静止水滴セルHash（診断）", Description = "診断専用。赤は小水滴、緑は中水滴、青は大水滴の静止セルとHash状態です。落下する水滴は対象外です。セル境界で色が切り替わるのは正常です。同じフレームでカーソルを動かしたとき、既存セルの色が変わるか確認し、確認後は最終結果へ戻してください")]
    OutsideDropletCellHash = 9,

    [Display(Name = "水滴Seedビット（診断）", Description = "診断専用。水滴Seedの32ビットを8px幅の白黒縦縞として256pxごとに繰り返し表示します。Hash計算と落下状態は対象外です。同じフレームでカーソルを動かしたとき、縞の境界ではなく中央の白黒が反転するか確認し、確認後は最終結果へ戻してください")]
    OutsideDropletSeedBits = 10,

    [Display(Name = "静止セルHash（sinなし診断）", Description = "診断専用。赤は小水滴、緑は中水滴、青は大水滴の静止セルを、sinを使わない整数Hashで表示します。落下する水滴は対象外です。セル境界で色が切り替わるのは正常です。同じフレームでカーソルを動かしたとき、既存セルの中央色が変わるか確認し、確認後は最終結果へ戻してください")]
    OutsideDropletCellHashWithoutSine = 11,

    [Display(Name = "静止セル座標ビット（診断）", Description = "診断専用。赤は小水滴、緑は中水滴、青は大水滴のセル座標ビットです。512px周期の前半256pxはX、後半256pxはYを、8px幅で32ビット表示します。同じフレームでカーソルを動かし、縞の境界ではなく中央の色が変わるか確認してください。落下する水滴は対象外です。マゼンタは無効な四角形、黄は座標計算不可を示すため、その場合はビット判定を行わないでください。確認後は最終結果へ戻してください")]
    OutsideDropletCellCoordinateBits = 12,

    [Display(Name = "Hash入力ビット（診断）", Description = "診断専用。整数Hashへ渡す直前の32ビットを、赤は小水滴、緑は中水滴、青は大水滴として8px幅の縦縞で表示します。64px高の横帯ごとにHashの5入力を順に割り当て、320pxごとに繰り返します。同じフレームでカーソルを動かし、横帯と縞の境界ではなく中央の色が変わるか確認してください。マゼンタまたは黄の場合は判定せず、確認後は最終結果へ戻してください")]
    OutsideDropletHashInputBits = 13,

    [Display(Name = "最終Hash bit 0（診断）", Description = "診断専用。sinを使わない整数Hashの最下位ビット（bit 0）だけを、赤は小水滴、緑は中水滴、青は大水滴としてセルごとに0/1で表示します。画面X位置による表示ビットの切替は行いません。同じフレームでカーソルを動かし、既存セルの中央色が変わるか確認してください。マゼンタまたは黄の場合は判定せず、確認後は最終結果へ戻してください")]
    OutsideDropletFinalHashBitsWithoutSine = 14,

    [Display(Name = "固定色return（診断）", Description = "診断専用。座標、Hash、画像を参照せず、画面全体を固定色で表示します。同じフレームでカーソルを動かし、全面が一様なままか確認してください。以前の模様が部分的に混ざる場合はスクリーンショットを記録し、確認後は最終結果へ戻してください")]
    FixedReturnColor = 15,

    [Display(Name = "Mix後ビット（診断）", Description = "診断専用。整数Hashの5入力をMixした直後の32ビットを、赤は小水滴、緑は中水滴、青は大水滴として8px幅の縦縞で表示します。64px高の横帯ごとに5入力を順に割り当て、320pxごとに繰り返します。同じフレームでカーソルを動かし、横帯と縞の境界ではなく中央の色が変わるか確認してください。マゼンタまたは黄の場合は判定せず、確認後は最終結果へ戻してください")]
    OutsideDropletMixedHashBitsWithoutSine = 16,

    [Display(Name = "合成後ビット（診断）", Description = "診断専用。5入力のMix結果を合成した直後かつ最終avalanche前の32ビットを、赤は小水滴、緑は中水滴、青は大水滴として8px幅の縦縞で表示します。同じフレームでカーソルを動かし、縞の境界ではなく中央の色が変わるか確認してください。マゼンタまたは黄の場合は判定せず、確認後は最終結果へ戻してください")]
    OutsideDropletPreAvalancheHashBitsWithoutSine = 17,

    [Display(Name = "乗算後ビット（診断）", Description = "診断専用。5入力のMix結果へ合成係数を個別に乗算した直後の32ビットを、赤は小水滴、緑は中水滴、青は大水滴として8px幅の縦縞で表示します。第1項の係数は1で、64px高の横帯ごとに第1項から第5項を順に割り当て、320pxごとに繰り返します。同じフレームでカーソルを動かし、横帯と縞の境界ではなく中央の色が変わるか確認してください。マゼンタまたは黄の場合は判定せず、確認後は最終結果へ戻してください")]
    OutsideDropletWeightedHashBitsWithoutSine = 18,

    [Display(Name = "中セル単独・Hash入力加算段階bit（診断）", Description = "診断専用の一時版。中水滴セルだけを44px基準で計算し、Hash入力の加算段階を64px高×3段階の横帯で表示します。上からdotだけ、(Seed+307)×74.7を加算、offset 3.1込みの完全式の順で、192pxごとに繰り返します。各段階のfloatをasuintで32bitとして読み取り、抽出した同一ビットをR=G=Bのグレースケールとして8px幅の縦縞で画面全体に表示します。この分岐ではHash入力helper、Mix、小水滴・大水滴セルを使いません。同じフレームでカーソルだけを動かした4枚のスクリーンショットを記録してください。判定は画像差分で行い、確認後は最終結果へ戻してください")]
    OutsideDropletAccumulatedHashBitsWithoutSine = 19,

    [Display(Name = "中セル単独・Hash入力bit G単独（診断）", Description = "診断専用。中水滴セルだけを44px基準で計算し、GetOutsideDropletHashInputWithoutSine(cell, Seed+307, 3.1)を1回だけ実行したfloatをasuintで32bitとして読み取ります。scenePosition.x由来の8px幅の縦縞で1ビットを抽出し、緑だけへ表示します。赤と青は0、alphaは1です。この分岐では64px高の段階、RGBグレースケール、Mix、小水滴・大水滴セル、uint3を使いません。同じフレームでカーソルだけを動かした4枚のスクリーンショットを記録してください。判定は画像差分で行い、確認後は最終結果へ戻してください")]
    OutsideDropletMediumOnlyHashInputGreenBitWithoutSine = 20,

    [Display(Name = "中セル単独・cell X bit G単独（診断）", Description = "診断専用の対照表示。中水滴セルだけを44px基準で計算し、セルX座標のfloatをasuintで32bitとして読み取ります。この分岐のセルXからbitへの変換ではHash入力helper、Hash入力用dot、Seed加算を使いません。scenePosition.x由来の8px幅の縦縞で値20と同じビット位置を抽出し、緑だけへ表示します。赤と青は0、alphaは1です。この分岐ではMix、小水滴・大水滴セル、uint3、画像サンプリングを使いません。同じフレームでカーソルだけを動かした4枚のスクリーンショットを記録してください。カーソルがプレビューを横切っても必ず変化するとは限らないため、変化の有無は画像差分で判定し、確認後は最終結果へ戻してください")]
    OutsideDropletMediumOnlyCellXGreenBitWithoutSine = 21,

    [Display(Name = "中セル単独・直書きHash入力bit G単独（診断）", Description = "診断専用のhelper対照表示。値20と同じ中水滴セル、ビット位置、G単独出力を使い、GetOutsideDropletHashInputWithoutSineは呼び出さず、dot(cell, float2(127.1, 311.7)) + ((Seed + 307) + 3.1) * 74.7と同じHash入力式をこの分岐へ直接記述します。赤と青は0、alphaは1です。この分岐では64px高の段階、RGBグレースケール、Mix、小水滴・大水滴セル、uint3、画像サンプリングを使いません。同一ビルドで値20と値22をそれぞれ4枚ずつ記録してください。カーソルがプレビューを横切っても必ず変化するとは限らないため、画像差分で比較し、確認後は最終結果へ戻してください")]
    OutsideDropletMediumOnlyInlineHashInputGreenBitWithoutSine = 22,

    [Display(Name = "中セル単独・cell Y bit G単独（診断）", Description = "診断専用の対照表示。値21と同じ中水滴セルを44px基準で計算し、セルY座標のfloatをasuintで32bitとして読み取ります。この分岐のセルYからbitへの変換ではHash入力helper、Hash入力用dot、Seed加算を使いません。scenePosition.x由来の8px幅の縦縞で値21と同じビット位置を抽出し、緑だけへ表示します。赤と青は0、alphaは1です。この分岐ではMix、小水滴・大水滴セル、uint3、画像サンプリングを使いません。同じフレームでカーソルだけを動かした4枚のスクリーンショットを記録してください。カーソルがプレビューを横切っても必ず変化するとは限らないため、変化の有無は画像差分で判定し、確認後は最終結果へ戻してください")]
    OutsideDropletMediumOnlyCellYGreenBitWithoutSine = 23,
#endif
}
