# SPDX-License-Identifier: MPL-2.0

[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion -lt [Version]'7.2') {
    throw 'このスクリプトにはPowerShell 7.2以降が必要です。pwshで実行してください。'
}

$expectedMplLicenseSha256 = '3F3D9E0024B1921B067D6F7F88DEB4A60CBE7A78E76C64E3F1D7FC3B779B9D04'
$expectedAssetHashes = [ordered]@{
    'src/YMM4GlassWipe/Assets/Brushes/hand-mask.png' =
        '53EABED33965D91E2DA6C5312794F9AFC5F1F60D7A545EC71AEFB07521A4CDEA'
    'src/YMM4GlassWipe/Assets/Brushes/shoe-print-mask.png' =
        'C8AD1632C5F039C3FAFBC0767625AEB6C40848B87155ECC0ECC1DF2265469A97'
}

function Get-Sha256Hex {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
}
else {
    $RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
}

$licensePath = Join-Path $RepositoryRoot 'LICENSE.txt'
if (-not (Test-Path -LiteralPath $licensePath -PathType Leaf)) {
    throw "LICENSE.txtが見つかりません: $licensePath"
}
if ((Get-Sha256Hex -Path $licensePath) -cne $expectedMplLicenseSha256) {
    throw 'LICENSE.txtが監査済みのMozilla公式MPL-2.0本文と一致しません。'
}
$gitAttributesPath = Join-Path $RepositoryRoot '.gitattributes'
if (-not (Test-Path -LiteralPath $gitAttributesPath -PathType Leaf)) {
    throw '.gitattributesが見つかりません。'
}
$gitAttributes = [System.IO.File]::ReadAllText(
    $gitAttributesPath,
    [System.Text.UTF8Encoding]::new($false, $true))
$licenseAttributeCount = ([regex]::Matches(
        $gitAttributes,
        '(?m)^LICENSE\.txt text eol=lf\r?$')).Count
if ($licenseAttributeCount -ne 1) {
    throw '.gitattributesでLICENSE.txtをLFへ固定してください。'
}

$sourceTargets = [System.Collections.Generic.List[string]]::new()
foreach ($sourceRootName in @('src', 'tests', 'tools')) {
    $sourceRoot = Join-Path $RepositoryRoot $sourceRootName
    foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File) {
        $relativePath = [System.IO.Path]::GetRelativePath($RepositoryRoot, $file.FullName).Replace('\', '/')
        if ($relativePath -match '(^|/)(bin|obj|tmp|\.vs)(/|$)') {
            continue
        }
        if ($file.Extension.ToLowerInvariant() -in @('.cs', '.hlsl', '.hlsli', '.csproj', '.props', '.ps1')) {
            $sourceTargets.Add($relativePath)
        }
    }
}
$sourceTargets.Add('Directory.Build.props.sample')
$sourceTargets = @($sourceTargets | Sort-Object -Unique)

$spdxFailures = [System.Collections.Generic.List[string]]::new()
foreach ($relativePath in $sourceTargets) {
    $fullPath = Join-Path $RepositoryRoot $relativePath
    $text = [System.IO.File]::ReadAllText(
        $fullPath,
        [System.Text.UTF8Encoding]::new($false, $true))
    $occurrenceCount = ([regex]::Matches(
            $text,
            '(?m)^(?:(?://|#) SPDX-License-Identifier: MPL-2\.0|<!-- SPDX-License-Identifier: MPL-2\.0 -->)\r?$')).Count
    $firstLine = $text.Split([string[]]@("`r`n", "`n"), [System.StringSplitOptions]::None)[0]
    $expectedFirstLine = if ($relativePath.EndsWith('.csproj') -or
        $relativePath.EndsWith('.props') -or
        $relativePath -ceq 'Directory.Build.props.sample') {
        '<!-- SPDX-License-Identifier: MPL-2.0 -->'
    }
    elseif ($relativePath.EndsWith('.ps1')) {
        '# SPDX-License-Identifier: MPL-2.0'
    }
    else {
        '// SPDX-License-Identifier: MPL-2.0'
    }

    if ($occurrenceCount -ne 1 -or $firstLine -cne $expectedFirstLine) {
        $spdxFailures.Add("$relativePath (count=$occurrenceCount, first=$firstLine)")
    }
}
if ($spdxFailures.Count -ne 0) {
    throw "SPDX識別子の不備があります:`n$($spdxFailures -join "`n")"
}

$assetProvenancePath = Join-Path $RepositoryRoot 'ASSET_PROVENANCE.md'
$thirdPartyNoticesPath = Join-Path $RepositoryRoot 'THIRD_PARTY_NOTICES.md'
foreach ($requiredPath in @($assetProvenancePath, $thirdPartyNoticesPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "ライセンス関連文書が見つかりません: $requiredPath"
    }
}

$assetProvenance = [System.IO.File]::ReadAllText(
    $assetProvenancePath,
    [System.Text.UTF8Encoding]::new($false, $true))
foreach ($asset in $expectedAssetHashes.GetEnumerator()) {
    $assetPath = Join-Path $RepositoryRoot $asset.Key
    $actualHash = Get-Sha256Hex -Path $assetPath
    if ($actualHash -cne $asset.Value) {
        throw "画像のSHA-256が由来監査時から変化しました: $($asset.Key)"
    }
    if (-not $assetProvenance.Contains($actualHash, [System.StringComparison]::Ordinal)) {
        throw "ASSET_PROVENANCE.mdに画像SHA-256がありません: $($asset.Key)"
    }
}
foreach ($requiredText in @(
        'trainedAlgorithmicMedia',
        'cryptographic validity of the C2PA signature has not been verified',
        'General influence from the AI model''s training data has not been verified')) {
    if (-not $assetProvenance.Contains($requiredText, [System.StringComparison]::Ordinal)) {
        throw "ASSET_PROVENANCE.mdの必須記録がありません: $requiredText"
    }
}

$thirdPartyNotices = [System.IO.File]::ReadAllText(
    $thirdPartyNoticesPath,
    [System.Text.UTF8Encoding]::new($false, $true))
foreach ($requiredText in @(
        'YukkuriMovieMaker 4',
        'Vortice.D3DCompiler',
        '2.1.8-beta',
        'Vortice.Mathematics',
        '1.4.12',
        'SharpGen.Runtime.COM',
        '2.0.0-beta.10',
        'Newtonsoft.Json',
        '13.0.4',
        'D3DCompiler_47_cor3.dll',
        'Windows SDK License',
        'A05F99734F7C4822FEFC12B367AF21FD0976ED6608752FB1E1E80B6ECE7ECBBB',
        '© Microsoft Corporation. All rights reserved.',
        'not included in the `.ymme` package')) {
    if (-not $thirdPartyNotices.Contains($requiredText, [System.StringComparison]::Ordinal)) {
        throw "THIRD_PARTY_NOTICES.mdの必須記録がありません: $requiredText"
    }
}
if ($thirdPartyNotices -match '(?i)\b(TODO|TBD|PLACEHOLDER)\b' -or
    $assetProvenance -match '(?i)\b(TODO|TBD|PLACEHOLDER)\b') {
    throw 'ライセンス関連文書に仮置き表現が残っています。'
}

$requiredDocumentRequirements = [ordered]@{
    'README.md' = @('MPL-2.0', 'THIRD_PARTY_NOTICES.md', 'ASSET_PROVENANCE.md')
    'docs/INSTALLER_README.txt' = @('LICENSE.txt', 'SOURCE_CODE.txt', 'https://www.mine-blog.tech/ymm4-raindrop/')
    'docs/USER_GUIDE.md' = @('MPL-2.0', 'SOURCE_CODE.txt')
    'docs/USER_GUIDE.html' = @('MPL-2.0', 'SOURCE_CODE.txt')
}
$optionalDocumentRequirements = [ordered]@{
    'docs/CURRENT_SPEC.md' = @('MPL-2.0', 'SOURCE_CODE.txt')
    'docs/PRODUCT_SPEC.md' = @('MPL-2.0', 'SOURCE_CODE.txt')
    'docs/TECHNICAL_DESIGN.md' = @('MPL-2.0', 'SOURCE_CODE.txt')
    'docs/TEST_PLAN.md' = @('MPL-2.0', '3F3D9E0024B1921B067D6F7F88DEB4A60CBE7A78E76C64E3F1D7FC3B779B9D04')
    'docs/ROADMAP.md' = @('MPL-2.0')
    'docs/ROADMAP.html' = @('MPL-2.0')
}
foreach ($document in $requiredDocumentRequirements.GetEnumerator()) {
    $documentPath = Join-Path $RepositoryRoot $document.Key
    if (-not (Test-Path -LiteralPath $documentPath -PathType Leaf)) {
        throw "公開に必要な文書が見つかりません: $($document.Key)"
    }
    $documentText = [System.IO.File]::ReadAllText(
        $documentPath,
        [System.Text.UTF8Encoding]::new($false, $true))
    foreach ($requiredText in $document.Value) {
        if (-not $documentText.Contains($requiredText, [System.StringComparison]::Ordinal)) {
            throw "$($document.Key)に必須のライセンス記述がありません: $requiredText"
        }
    }
}
foreach ($document in $optionalDocumentRequirements.GetEnumerator()) {
    $documentPath = Join-Path $RepositoryRoot $document.Key
    if (-not (Test-Path -LiteralPath $documentPath -PathType Leaf)) {
        continue
    }
    $documentText = [System.IO.File]::ReadAllText(
        $documentPath,
        [System.Text.UTF8Encoding]::new($false, $true))
    foreach ($requiredText in $document.Value) {
        if (-not $documentText.Contains($requiredText, [System.StringComparison]::Ordinal)) {
            throw "$($document.Key)に必須のライセンス記述がありません: $requiredText"
        }
    }
}

$sourceNoticePath = Join-Path $RepositoryRoot 'SOURCE_CODE.txt'
if (Test-Path -LiteralPath $sourceNoticePath) {
    throw 'SOURCE_CODE.txtは追跡元へ置かず、公開タグ確定後にリリース処理が生成します。'
}

$releaseScriptPath = Join-Path $RepositoryRoot 'tools/Release-YMM4GlassWipe.ps1'
$releaseScript = [System.IO.File]::ReadAllText(
    $releaseScriptPath,
    [System.Text.UTF8Encoding]::new($false, $true))
foreach ($requiredText in @(
        'Resolve-PublicRepositoryUrl',
        'Assert-ReleaseTagAtHead',
        'THIRD_PARTY_NOTICES.md',
        'ASSET_PROVENANCE.md',
        'SOURCE_CODE.txt',
        'Exact source archive:',
        'docs\USER_GUIDE.html',
        'Assert-HtmlStructure -Path $guidePath',
        $expectedMplLicenseSha256)) {
    if (-not $releaseScript.Contains($requiredText, [System.StringComparison]::Ordinal)) {
        throw "リリーススクリプトに必須処理がありません: $requiredText"
    }
}

$packageEntryBlock = [System.Text.RegularExpressions.Regex]::Match(
    $releaseScript,
    '\$expectedPackageEntries\s*=\s*@\((?<entries>.*?)\)',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $packageEntryBlock.Success) {
    throw 'リリーススクリプトのパッケージ構成定義を取得できません。'
}

$actualPackageEntries = @(
    [System.Text.RegularExpressions.Regex]::Matches(
        $packageEntryBlock.Groups['entries'].Value,
        "'(?<entry>[^']+)'"
    ) | ForEach-Object { $_.Groups['entry'].Value }
)
$expectedPackageEntries = @(
    'README.txt',
    'YMM4GlassWipe.dll',
    'LICENSE.txt',
    'SOURCE_CODE.txt'
)
if (($actualPackageEntries -join "`n") -cne ($expectedPackageEntries -join "`n")) {
    throw "リリースパッケージの構成が4件の定義と一致しません: $($actualPackageEntries -join ', ')"
}

foreach ($forbiddenPattern in @(
        "(?m)^\s*'USER_GUIDE\.html'\s*=",
        "-EntryName\s+'USER_GUIDE\.html'")) {
    if ([System.Text.RegularExpressions.Regex]::IsMatch($releaseScript, $forbiddenPattern)) {
        throw 'ブログ掲載用USER_GUIDE.htmlをリリースパッケージへ含める処理が残っています。'
    }
}

Write-Host "ライセンス検証に合格しました: SPDX $($sourceTargets.Count)件 / assets $($expectedAssetHashes.Count)件"
