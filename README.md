# 雫と拭痕 – RainDrop & GlassWipe for YMM4

by Mine-Tech / v1.0.0

ゆっくりMovieMaker4（YMM4）向けの映像エフェクトです。映像に曇りを重ね、円・楕円・四角形・手形・靴跡・登録したPNGのブラシで拭き取る演出を作れます。ガラス面の外側に水滴や雨だれを重ねることもできます。外側の水滴はブラシで消えません。

## 動作環境

- ゆっくりMovieMaker4 v4.56.1.0以降
- 64-bit版 Windows 10 Version 2004（build 19041）以降、またはWindows 11
- DirectX 11対応GPU

本プラグインはMine-Techが開発した非公式の第三者製プラグインです。

## インストールと使い方

YMM4を終了し、配布された`YMM4GlassWipe.ymme`をダブルクリックしてYMM4の案内に従ってインストールしてください。DLL形式で導入する場合は、`YMM4GlassWipe.dll`をYMM4の`user\plugin\YMM4GlassWipe`へ配置します。同じDLLを複数のプラグインフォルダーへ残さないでください。

YMM4で映像・画像・エフェクトアイテムに「映像エフェクト」→「描画」→「雫と拭痕」を追加します。操作、ユーザー画像ブラシ、プリセット、バックアップ、制限事項は[ユーザーズマニュアル](docs/USER_GUIDE.md)を参照してください。[HTML版](docs/USER_GUIDE.html)はダウンロードしてブラウザで開けます。

ユーザー画像の登録では「登録名」を入力してから「PNGを追加」を押します。「登録名」はファイルパスの入力欄ではありません。

## ソースコード

ソースのビルド方法は[ビルド手順](docs/BUILDING.md)を参照してください。

## ライセンスと由来

本プラグイン固有のファイルにはMozilla Public License 2.0（MPL-2.0）を適用します。条件は[LICENSE.txt](LICENSE.txt)、第三者要素は[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)、組み込み画像の由来は[ASSET_PROVENANCE.md](ASSET_PROVENANCE.md)を参照してください。実行形式を配布する際は、パッケージ内の`SOURCE_CODE.txt`が対応するソースの入手先を示します。
