# SPDX-License-Identifier: MPL-2.0

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion -lt [Version]'7.2') {
    throw 'このスクリプトにはPowerShell 7.2以降が必要です。pwshで実行してください。'
}

function Write-Utf8Text {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Text
    )

    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrEmpty($parent)) {
        $null = New-Item -ItemType Directory -Path $parent -Force
    }
    [System.IO.File]::WriteAllText(
        $Path,
        $Text,
        [System.Text.UTF8Encoding]::new($false))
}

function Invoke-LicensingVerification {
    param(
        [Parameter(Mandatory)]
        [string]$FixtureRoot,

        [Parameter(Mandatory)]
        [bool]$ExpectedSuccess,

        [Parameter(Mandatory)]
        [string]$CaseName,

        [string]$ExpectedErrorText
    )

    $succeeded = $false
    $message = ''
    try {
        & (Join-Path $FixtureRoot 'tools\Verify-YMM4GlassWipeLicensing.ps1') `
            -RepositoryRoot $FixtureRoot
        $succeeded = $true
    }
    catch {
        $message = $_.Exception.Message
    }

    if ($succeeded -ne $ExpectedSuccess) {
        throw "$CaseName の成否が期待値と一致しません。成功=$succeeded / 期待=$ExpectedSuccess / $message"
    }
    if (-not $ExpectedSuccess -and
        -not $message.Contains($ExpectedErrorText, [System.StringComparison]::Ordinal)) {
        throw "$CaseName のエラーが期待値と一致しません。期待=$ExpectedErrorText / 実際=$message"
    }

    return [ordered]@{
        case = $CaseName
        succeeded = $succeeded
        expectedSuccess = $ExpectedSuccess
        message = $message
    }
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runName = [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmssfff')
$runRoot = Join-Path $repositoryRoot "tmp\release-input-regression\$runName"
$fixtureRoot = Join-Path $runRoot 'fixture'
$null = New-Item -ItemType Directory -Path $fixtureRoot -Force

foreach ($directory in @(
        'docs',
        'src\YMM4GlassWipe\Assets\Brushes',
        'tests',
        'tools')) {
    $null = New-Item -ItemType Directory -Path (Join-Path $fixtureRoot $directory)
}

foreach ($relativePath in @(
        'LICENSE.txt',
        'Directory.Build.props.sample',
        'docs\INSTALLER_README.txt',
        'src\YMM4GlassWipe\Assets\Brushes\hand-mask.png',
        'src\YMM4GlassWipe\Assets\Brushes\shoe-print-mask.png',
        'tools\Release-YMM4GlassWipe.ps1',
        'tools\Verify-YMM4GlassWipeLicensing.ps1')) {
    $destination = Join-Path $fixtureRoot $relativePath
    Copy-Item -LiteralPath (Join-Path $repositoryRoot $relativePath) -Destination $destination
}

Write-Utf8Text -Path (Join-Path $fixtureRoot '.gitattributes') -Text "LICENSE.txt text eol=lf`n"
Write-Utf8Text -Path (Join-Path $fixtureRoot 'README.md') -Text @'
MPL-2.0
THIRD_PARTY_NOTICES.md
ASSET_PROVENANCE.md
'@
Write-Utf8Text -Path (Join-Path $fixtureRoot 'docs\USER_GUIDE.md') -Text @'
MPL-2.0
SOURCE_CODE.txt
'@
Write-Utf8Text -Path (Join-Path $fixtureRoot 'docs\USER_GUIDE.html') -Text @'
MPL-2.0
SOURCE_CODE.txt
'@
Write-Utf8Text -Path (Join-Path $fixtureRoot 'ASSET_PROVENANCE.md') -Text @'
53EABED33965D91E2DA6C5312794F9AFC5F1F60D7A545EC71AEFB07521A4CDEA
C8AD1632C5F039C3FAFBC0767625AEB6C40848B87155ECC0ECC1DF2265469A97
trainedAlgorithmicMedia
cryptographic validity of the C2PA signature has not been verified
General influence from the AI model's training data has not been verified
'@
Write-Utf8Text -Path (Join-Path $fixtureRoot 'THIRD_PARTY_NOTICES.md') -Text @'
YukkuriMovieMaker 4
Vortice.D3DCompiler
2.1.8-beta
Vortice.Mathematics
1.4.12
SharpGen.Runtime.COM
2.0.0-beta.10
Newtonsoft.Json
13.0.4
D3DCompiler_47_cor3.dll
Windows SDK License
A05F99734F7C4822FEFC12B367AF21FD0976ED6608752FB1E1E80B6ECE7ECBBB
© Microsoft Corporation. All rights reserved.
not included in the `.ymme` package
'@

$results = [System.Collections.Generic.List[object]]::new()
$releaseScriptText = [System.IO.File]::ReadAllText(
    (Join-Path $fixtureRoot 'tools\Release-YMM4GlassWipe.ps1'),
    [System.Text.UTF8Encoding]::new($false, $true))
$requiredInputBlock = [System.Text.RegularExpressions.Regex]::Match(
    $releaseScriptText,
    'foreach \(\$requiredPath in @\((?<paths>.*?)\)\) \{',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $requiredInputBlock.Success -or
    $requiredInputBlock.Groups['paths'].Value.Contains('$roadmapPath', [System.StringComparison]::Ordinal) -or
    -not $releaseScriptText.Contains(
        'if (Test-Path -LiteralPath $roadmapPath -PathType Leaf)',
        [System.StringComparison]::Ordinal)) {
    throw 'Release処理でROADMAP.htmlが任意入力として扱われていません。'
}
$results.Add([ordered]@{
        case = 'Releaseの内部ロードマップ任意化'
        succeeded = $true
        expectedSuccess = $true
        message = ''
    })
$results.Add((Invoke-LicensingVerification `
            -FixtureRoot $fixtureRoot `
            -ExpectedSuccess $true `
            -CaseName '内部文書なし'))

$requiredGuidePath = Join-Path $fixtureRoot 'docs\USER_GUIDE.md'
$heldGuidePath = Join-Path $runRoot 'USER_GUIDE.md.held'
Move-Item -LiteralPath $requiredGuidePath -Destination $heldGuidePath
try {
    $results.Add((Invoke-LicensingVerification `
                -FixtureRoot $fixtureRoot `
                -ExpectedSuccess $false `
                -CaseName '公開必須文書欠落' `
                -ExpectedErrorText '公開に必要な文書が見つかりません: docs/USER_GUIDE.md'))
}
finally {
    Move-Item -LiteralPath $heldGuidePath -Destination $requiredGuidePath
}

$optionalDocumentPath = Join-Path $fixtureRoot 'docs\CURRENT_SPEC.md'
Write-Utf8Text -Path $optionalDocumentPath -Text "MPL-2.0`n"
$results.Add((Invoke-LicensingVerification `
            -FixtureRoot $fixtureRoot `
            -ExpectedSuccess $false `
            -CaseName '任意内部文書不備' `
            -ExpectedErrorText 'docs/CURRENT_SPEC.mdに必須のライセンス記述がありません: SOURCE_CODE.txt'))

$resultPath = Join-Path $runRoot 'results.json'
[System.IO.File]::WriteAllText(
    $resultPath,
    ($results | ConvertTo-Json -Depth 4),
    [System.Text.UTF8Encoding]::new($false))
Write-Host "公開入力回帰検証に合格しました: $resultPath"
