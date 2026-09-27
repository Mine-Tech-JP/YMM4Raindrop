# 雫と拭痕のビルド手順

リポジトリのルートで以下の操作を行います。

## ソースからビルド

.NET SDK 10.0.400、PowerShell 7.2以降、対応するYMM4本体が必要です。`global.json`がSDKの版を指定します。ビルド時にはYMM4内の参照DLLと`Vortice.D3DCompiler.dll`、`D3DCompiler_47_cor3.dll`を使用します。これらの第三者ファイルは本リポジトリに収録しません。

`Directory.Build.props.sample`を`Directory.Build.props`へコピーし、`YMM4DirPath`を手元のYMM4本体フォルダーへ変更してください。末尾の`\`が必要です。`Directory.Build.props`はGitの追跡対象から除外されています。

```powershell
Copy-Item .\Directory.Build.props.sample .\Directory.Build.props
# Directory.Build.props の YMM4DirPath を編集する

dotnet restore .\YMM4GlassWipe.sln -p:NuGetAudit=false
dotnet build .\YMM4GlassWipe.sln -c Debug --no-restore
dotnet run --project .\tests\YMM4GlassWipe.Verification\YMM4GlassWipe.Verification.csproj -c Debug --no-build
dotnet build .\YMM4GlassWipe.sln -c Release --no-restore
dotnet run --project .\tests\YMM4GlassWipe.Verification\YMM4GlassWipe.Verification.csproj -c Release --no-build
pwsh -NoProfile -File .\tools\Verify-YMM4GlassWipeLicensing.ps1
pwsh -NoProfile -File .\tools\Verify-YMM4GlassWipeReleaseInputs.ps1
```

ビルドは`src/YMM4GlassWipe/Shaders/`のHLSLを再コンパイルします。プラグインDLLの出力先は`src/YMM4GlassWipe/bin/<構成>/net10.0-windows10.0.19041.0/`です。ビルドだけではYMM4へ配備されません。

## 配布物の生成

`tools/Release-YMM4GlassWipe.ps1`は、公開リポジトリで変更をコミットし、ローカルの`v1.0.0`タグがそのHEADを指し、作業ツリーがクリーンな状態になってから使用します。公開URLを指定して実行すると、ビルドと検証を行い、DLL、ライセンス、第三者通知、画像由来、対応ソースの案内、SHA-256一覧を含む`YMM4GlassWipe.ymme`を`tmp/release/v1.0.0/<完全なコミットSHA>/`に新規生成します。既存の出力は上書きしません。

```powershell
$publicRepositoryUrl = 'https://github.com/Mine-Tech-JP/YMM4Raindrop'
pwsh -NoProfile -File .\tools\Release-YMM4GlassWipe.ps1 -ReleaseTag v1.0.0 -PublicRepositoryUrl $publicRepositoryUrl
```

このスクリプトはGitのコミット、タグ作成、push、YMM4への配備を行いません。`SOURCE_CODE.txt`はタグとコミットが確定した後に生成され、リポジトリでは追跡しません。


[READMEへ戻る](../README.md)
