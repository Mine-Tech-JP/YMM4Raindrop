# SPDX-License-Identifier: MPL-2.0

[CmdletBinding()]
param(
    [string]$DotnetPath,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ReleaseTag,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$PublicRepositoryUrl
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion -lt [Version]'7.2') {
    throw 'このスクリプトにはPowerShell 7.2以降が必要です。pwshで実行してください。'
}

$releaseVersion = '1.0.0'
$expectedMplLicenseSha256 = '3F3D9E0024B1921B067D6F7F88DEB4A60CBE7A78E76C64E3F1D7FC3B779B9D04'
$expectedPackageEntries = @(
    'README.txt',
    'YMM4GlassWipe.dll',
    'LICENSE.txt',
    'SOURCE_CODE.txt'
)
$expectedArchiveTimestamp = [DateTimeOffset]::new(
    1980,
    1,
    1,
    0,
    0,
    0,
    [TimeSpan]::Zero)

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "コマンドが失敗しました（exit code $LASTEXITCODE）: $FilePath $($ArgumentList -join ' ')"
    }
}

function Get-Sha256Hex {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Get-Sha256HexFromStream {
    param(
        [Parameter(Mandatory)]
        [System.IO.Stream]$Stream
    )

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [Convert]::ToHexString($sha256.ComputeHash($Stream))
    }
    finally {
        $sha256.Dispose()
    }
}

function Assert-CleanWorktree {
    param(
        [Parameter(Mandatory)]
        [string]$Phase
    )

    $status = & git status --porcelain=v1
    if ($LASTEXITCODE -ne 0) {
        throw "git status の実行に失敗しました。"
    }

    if ($status) {
        throw "${Phase}: コミット済みHEADからのリリース生成だけを許可します。作業ツリーをクリーンにしてください。`n$status"
    }
}

function Assert-HtmlStructure {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $html = [System.IO.File]::ReadAllText(
        $Path,
        [System.Text.UTF8Encoding]::new($false, $true))
    $html = [System.Text.RegularExpressions.Regex]::Replace(
        $html,
        '<!--.*?-->',
        '',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
    foreach ($rawTextElement in @('script', 'style')) {
        $pattern = "(<$rawTextElement\b[^>]*>).*?(</$rawTextElement\s*>)"
        $html = [System.Text.RegularExpressions.Regex]::Replace(
            $html,
            $pattern,
            '$1$2',
            [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
                [System.Text.RegularExpressions.RegexOptions]::Singleline)
    }

    $voidElements = @(
        'area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input',
        'link', 'meta', 'param', 'source', 'track', 'wbr'
    )
    $stack = [System.Collections.Generic.Stack[string]]::new()
    $tagPattern = '<\s*(/?)\s*([A-Za-z][A-Za-z0-9:-]*)\b[^>]*?>'
    foreach ($match in [System.Text.RegularExpressions.Regex]::Matches(
            $html,
            $tagPattern,
            [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        $isClosing = $match.Groups[1].Value -eq '/'
        $tagName = $match.Groups[2].Value.ToLowerInvariant()
        $isSelfClosing = $match.Value -match '/\s*>$'
        if ($tagName -in $voidElements -or $isSelfClosing) {
            continue
        }

        if (-not $isClosing) {
            $stack.Push($tagName)
            continue
        }

        if ($stack.Count -eq 0) {
            throw "HTMLの終了タグに対応する開始タグがありません: $Path / $tagName"
        }

        $openedTag = $stack.Pop()
        if ($openedTag -cne $tagName) {
            throw "HTMLタグの入れ子が不正です: $Path / 開始=$openedTag / 終了=$tagName"
        }
    }

    if ($stack.Count -ne 0) {
        throw "HTMLの終了タグが不足しています: $Path / $($stack.Peek())"
    }

    foreach ($requiredTag in @('html', 'head', 'body')) {
        if ($html -notmatch "<$requiredTag\b") {
            throw "HTMLの必須タグがありません: $Path / $requiredTag"
        }
    }
}

function Add-ArchiveFile {
    param(
        [Parameter(Mandatory)]
        [System.IO.Compression.ZipArchive]$Archive,

        [Parameter(Mandatory)]
        [string]$SourcePath,

        [Parameter(Mandatory)]
        [string]$EntryName
    )

    $entry = $Archive.CreateEntry(
        $EntryName,
        [System.IO.Compression.CompressionLevel]::Optimal)
    $entry.LastWriteTime = $expectedArchiveTimestamp
    $source = $null
    $destination = $null
    try {
        $source = [System.IO.File]::OpenRead($SourcePath)
        $destination = $entry.Open()
        $source.CopyTo($destination)
    }
    finally {
        try {
            if ($null -ne $destination) {
                $destination.Dispose()
            }
        }
        finally {
            if ($null -ne $source) {
                $source.Dispose()
            }
        }
    }
}

function Add-ArchiveBytes {
    param(
        [Parameter(Mandatory)]
        [System.IO.Compression.ZipArchive]$Archive,

        [Parameter(Mandatory)]
        [byte[]]$Bytes,

        [Parameter(Mandatory)]
        [string]$EntryName
    )

    $entry = $Archive.CreateEntry(
        $EntryName,
        [System.IO.Compression.CompressionLevel]::Optimal)
    $entry.LastWriteTime = $expectedArchiveTimestamp
    $destination = $null
    try {
        $destination = $entry.Open()
        $destination.Write($Bytes, 0, $Bytes.Length)
    }
    finally {
        if ($null -ne $destination) {
            $destination.Dispose()
        }
    }
}

function Resolve-PublicRepositoryUrl {
    param(
        [Parameter(Mandatory)]
        [string]$Value
    )

    $uri = $null
    if (-not [System.Uri]::TryCreate($Value, [System.UriKind]::Absolute, [ref]$uri) -or
        $uri.Scheme -cne 'https' -or
        -not [string]::Equals($uri.Host, 'github.com', [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $uri.IsDefaultPort -or
        -not [string]::IsNullOrEmpty($uri.UserInfo) -or
        -not [string]::IsNullOrEmpty($uri.Query) -or
        -not [string]::IsNullOrEmpty($uri.Fragment) -or
        $uri.AbsolutePath -notmatch '^/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+(?:\.git)?/?$') {
        throw 'PublicRepositoryUrlにはhttps://github.com/<owner>/<repository>形式のURLを指定してください。'
    }

    $segments = $uri.AbsolutePath.Trim('/').Split('/')
    $repositoryName = $segments[1]
    if ($repositoryName.EndsWith('.git', [System.StringComparison]::OrdinalIgnoreCase)) {
        $repositoryName = $repositoryName.Substring(0, $repositoryName.Length - 4)
    }
    if ([string]::IsNullOrWhiteSpace($repositoryName)) {
        throw 'PublicRepositoryUrlのrepository名が空です。'
    }

    return "https://github.com/$($segments[0])/$repositoryName"
}

function Assert-ReleaseTagAtHead {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory)]
        [string]$ReleaseTag,

        [Parameter(Mandatory)]
        [string]$ReleaseVersion,

        [Parameter(Mandatory)]
        [string]$Head
    )

    $expectedTag = "v$ReleaseVersion"
    if ($ReleaseTag -cne $expectedTag) {
        throw "ReleaseTagがリリース版と一致しません。期待値: $expectedTag / 実際: $ReleaseTag"
    }

    $tagReference = "refs/tags/$ReleaseTag"
    $tagOutput = @(& git -C $RepositoryRoot rev-parse --verify "$tagReference^{commit}" 2>$null)
    $tagExitCode = $LASTEXITCODE
    $tagCommit = if ($tagOutput.Count -eq 1) {
        ([string]$tagOutput[0]).Trim()
    }
    else {
        ''
    }
    if ($tagExitCode -ne 0 -or $tagCommit -notmatch '^[0-9a-f]{40}$') {
        throw "リリースタグが存在しないか、コミットへ解決できません: $ReleaseTag"
    }
    if ($tagCommit -cne $Head) {
        throw "リリースタグがHEADを指していません。tag: $tagCommit / HEAD: $Head"
    }
}

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..'))
Push-Location $repositoryRoot
try {
    $gitRoot = (& git rev-parse --show-toplevel).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw 'Gitリポジトリのルートを取得できません。'
    }

    if (-not [string]::Equals(
            [System.IO.Path]::GetFullPath($gitRoot),
            $repositoryRoot,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "スクリプトの対象外リポジトリです: $gitRoot"
    }

    Assert-CleanWorktree -Phase '開始前'

    if ([string]::IsNullOrWhiteSpace($DotnetPath)) {
        $portableDotnet = Join-Path $repositoryRoot 'tmp\tooling\dotnet-10.0.400\dotnet.exe'
        $DotnetPath = if (Test-Path -LiteralPath $portableDotnet -PathType Leaf) {
            $portableDotnet
        }
        else {
            'dotnet'
        }
    }
    $dotnetCommand = (Get-Command `
            -Name $DotnetPath `
            -CommandType Application `
            -ErrorAction Stop | Select-Object -First 1).Source
    $dotnetVersion = (& $dotnetCommand --version).Trim()
    if ($LASTEXITCODE -ne 0 -or $dotnetVersion -cne '10.0.400') {
        throw "リリース生成には.NET SDK 10.0.400が必要です。実際: $dotnetVersion"
    }

    $head = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -notmatch '^[0-9a-f]{40}$') {
        throw '完全なHEADコミットIDを取得できません。'
    }

    $normalizedPublicRepositoryUrl =
        Resolve-PublicRepositoryUrl -Value $PublicRepositoryUrl
    Assert-ReleaseTagAtHead `
        -RepositoryRoot $repositoryRoot `
        -ReleaseTag $ReleaseTag `
        -ReleaseVersion $releaseVersion `
        -Head $head

    $expectedProductVersion = "$releaseVersion+$head"
    $outputDirectory = Join-Path $repositoryRoot "tmp\release\v$releaseVersion\$head"
    if (Test-Path -LiteralPath $outputDirectory) {
        throw "出力先は既に存在します。上書きしません: $outputDirectory"
    }

    $solutionPath = Join-Path $repositoryRoot 'YMM4GlassWipe.sln'
    $verificationProjectPath = Join-Path $repositoryRoot 'tests\YMM4GlassWipe.Verification\YMM4GlassWipe.Verification.csproj'
    $releaseDllPath = Join-Path $repositoryRoot 'src\YMM4GlassWipe\bin\Release\net10.0-windows10.0.19041.0\YMM4GlassWipe.dll'
    $guidePath = Join-Path $repositoryRoot 'docs\USER_GUIDE.html'
    $roadmapPath = Join-Path $repositoryRoot 'docs\ROADMAP.html'
    $licensePath = Join-Path $repositoryRoot 'LICENSE.txt'
    $installerReadmePath = Join-Path $repositoryRoot 'docs\INSTALLER_README.txt'
    $thirdPartyNoticesPath = Join-Path $repositoryRoot 'THIRD_PARTY_NOTICES.md'
    $assetProvenancePath = Join-Path $repositoryRoot 'ASSET_PROVENANCE.md'
    $licensingVerificationScript = Join-Path $repositoryRoot 'tools\Verify-YMM4GlassWipeLicensing.ps1'
    $projectPath = Join-Path $repositoryRoot 'src\YMM4GlassWipe\YMM4GlassWipe.csproj'
    foreach ($requiredPath in @(
            $solutionPath,
            $verificationProjectPath,
            $projectPath,
            $guidePath,
            $licensePath,
            $installerReadmePath,
            $thirdPartyNoticesPath,
            $assetProvenancePath,
            $licensingVerificationScript)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "必要なリリース入力が見つかりません: $requiredPath"
        }
    }

    & $licensingVerificationScript -RepositoryRoot $repositoryRoot
    if ((Get-Sha256Hex -Path $licensePath) -cne $expectedMplLicenseSha256) {
        throw 'LICENSE.txtが監査済みのMozilla公式MPL-2.0本文と一致しません。'
    }

    [xml]$project = [System.IO.File]::ReadAllText($projectPath)
    $projectVersion = @($project.Project.PropertyGroup.Version) |
        Where-Object { $_ } |
        Select-Object -First 1
    $projectAssemblyVersion = @($project.Project.PropertyGroup.AssemblyVersion) |
        Where-Object { $_ } |
        Select-Object -First 1
    $projectFileVersion = @($project.Project.PropertyGroup.FileVersion) |
        Where-Object { $_ } |
        Select-Object -First 1
    if ($projectVersion -cne $releaseVersion -or
        $projectAssemblyVersion -cne '1.0.0.0' -or
        $projectFileVersion -cne '1.0.0.0') {
        throw 'csprojのVersion、互換用AssemblyVersion、FileVersionがv1.0.0のリリース定義と一致しません。'
    }

    Assert-HtmlStructure -Path $guidePath
    if (Test-Path -LiteralPath $roadmapPath -PathType Leaf) {
        Assert-HtmlStructure -Path $roadmapPath
    }

    $versionProperties = @(
        "-p:InformationalVersion=$expectedProductVersion",
        "-p:SourceRevisionId=$head",
        '-p:IncludeSourceRevisionInInformationalVersion=false'
    )
    Invoke-CheckedCommand -FilePath $dotnetCommand -ArgumentList @(
        'restore', $solutionPath, '-p:NuGetAudit=false')
    foreach ($configuration in @('Debug', 'Release')) {
        Invoke-CheckedCommand -FilePath $dotnetCommand -ArgumentList (
            @('build', $solutionPath, '--configuration', $configuration, '--no-restore') +
            $versionProperties)
        Invoke-CheckedCommand -FilePath $dotnetCommand -ArgumentList @(
            'run', '--project', $verificationProjectPath, '--configuration', $configuration,
            '--no-build', '--no-restore')
    }

    Invoke-CheckedCommand -FilePath 'git' -ArgumentList @(
        'show', '--format=', '--check', 'HEAD')
    Invoke-CheckedCommand -FilePath 'git' -ArgumentList @('diff', '--check')
    Assert-CleanWorktree -Phase '検証後'

    $verifiedHead = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $verifiedHead -cne $head) {
        throw "検証中にHEADが変化しました。開始時: $head / 現在: $verifiedHead"
    }

    if (-not (Test-Path -LiteralPath $releaseDllPath -PathType Leaf)) {
        throw "Release DLLが見つかりません: $releaseDllPath"
    }

    $releaseDllVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo(
        $releaseDllPath).ProductVersion
    if ($releaseDllVersion -ne $expectedProductVersion) {
        throw "Release DLLのProductVersionが不正です。期待値: $expectedProductVersion / 実際: $releaseDllVersion"
    }
    $releaseFileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo(
        $releaseDllPath).FileVersion
    if ($releaseFileVersion -cne '1.0.0.0') {
        throw "Release DLLのFileVersionが不正です。実際: $releaseFileVersion"
    }
    $releaseAssemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName(
        $releaseDllPath).Version.ToString()
    if ($releaseAssemblyVersion -cne '1.0.0.0') {
        throw "Release DLLのAssemblyVersionが不正です。実際: $releaseAssemblyVersion"
    }

    $sourceCodeText = (@(
            '雫と拭痕 – RainDrop & GlassWipe for YMM4',
            "Version: $releaseVersion",
            "Release tag: $ReleaseTag",
            "Commit: $head",
            "Repository: $normalizedPublicRepositoryUrl",
            "Source code at release tag: $normalizedPublicRepositoryUrl/tree/$ReleaseTag",
            "Exact source code: $normalizedPublicRepositoryUrl/tree/$head",
            "Exact source archive: $normalizedPublicRepositoryUrl/archive/$head.zip",
            'License: Mozilla Public License 2.0',
            'See LICENSE.txt included in this package.'
        ) -join "`n") + "`n"
    $sourceCodeBytes = [System.Text.UTF8Encoding]::new($false).GetBytes($sourceCodeText)
    $sourceCodeStream = [System.IO.MemoryStream]::new($sourceCodeBytes, $false)
    try {
        $sourceCodeHash = Get-Sha256HexFromStream -Stream $sourceCodeStream
    }
    finally {
        $sourceCodeStream.Dispose()
    }

    $sourceHashes = [ordered]@{
        'README.txt' = Get-Sha256Hex -Path $installerReadmePath
        'YMM4GlassWipe.dll' = Get-Sha256Hex -Path $releaseDllPath
        'LICENSE.txt' = Get-Sha256Hex -Path $licensePath
        'SOURCE_CODE.txt' = $sourceCodeHash
    }

    $null = New-Item -ItemType Directory -Path $outputDirectory
    $packagePath = Join-Path $outputDirectory 'YMM4GlassWipe.ymme'
    $sidecarPath = "$packagePath.sha256"

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $packageFileStream = $null
    $archive = $null
    try {
        $packageFileStream = [System.IO.File]::Open(
            $packagePath,
            [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
        $archive = [System.IO.Compression.ZipArchive]::new(
            $packageFileStream,
            [System.IO.Compression.ZipArchiveMode]::Create,
            $true)
        Add-ArchiveFile -Archive $archive -SourcePath $installerReadmePath -EntryName 'README.txt'
        Add-ArchiveFile -Archive $archive -SourcePath $releaseDllPath -EntryName 'YMM4GlassWipe.dll'
        Add-ArchiveFile -Archive $archive -SourcePath $licensePath -EntryName 'LICENSE.txt'
        Add-ArchiveBytes -Archive $archive -Bytes $sourceCodeBytes -EntryName 'SOURCE_CODE.txt'
    }
    finally {
        try {
            if ($null -ne $archive) {
                $archive.Dispose()
            }
        }
        finally {
            if ($null -ne $packageFileStream) {
                $packageFileStream.Dispose()
            }
        }
    }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $entryNames = @($archive.Entries | ForEach-Object FullName)
        $unexpectedEntries = @($entryNames | Where-Object { $_ -notin $expectedPackageEntries })
        $missingEntries = @($expectedPackageEntries | Where-Object { $_ -notin $entryNames })
        if ($entryNames.Count -ne $expectedPackageEntries.Count -or
            $unexpectedEntries.Count -ne 0 -or
            $missingEntries.Count -ne 0) {
            throw "配布物のroot entriesは指定した$($expectedPackageEntries.Count)件だけでなければなりません。実際: $($entryNames -join ', ')"
        }
        foreach ($entry in $archive.Entries) {
            # ZIP の DOS 日時フィールドはタイムゾーンを保持しないため、
            # 再読込時は UTC ではなく壁時計の日時で固定値を検証する。
            if ($entry.LastWriteTime.DateTime -ne $expectedArchiveTimestamp.DateTime) {
                throw "配布物内entryの時刻が固定値ではありません: $($entry.FullName)"
            }
        }

        $entryHashes = @{}
        foreach ($entry in $archive.Entries) {
            $stream = $entry.Open()
            try {
                $entryHashes[$entry.FullName] = Get-Sha256HexFromStream -Stream $stream
            }
            finally {
                $stream.Dispose()
            }
        }

        foreach ($entryName in $sourceHashes.Keys) {
            if ($entryHashes[$entryName] -ne $sourceHashes[$entryName]) {
                throw "配布物内の$entryNameが検証済み入力と一致しません。"
            }
        }

        if ($entryHashes['YMM4GlassWipe.dll'] -ne $sourceHashes['YMM4GlassWipe.dll']) {
            throw '配布物内DLLはProductVersionを検証したRelease DLLと一致しません。'
        }
    }
    finally {
        $archive.Dispose()
    }

    $packageHash = Get-Sha256Hex -Path $packagePath
    [System.IO.File]::WriteAllText(
        $sidecarPath,
        "$packageHash  $([System.IO.Path]::GetFileName($packagePath))`n",
        [System.Text.UTF8Encoding]::new($false))

    $sidecarLine = [System.IO.File]::ReadAllText($sidecarPath, [System.Text.UTF8Encoding]::new($false))
    $expectedSidecarLine = "$packageHash  $([System.IO.Path]::GetFileName($packagePath))`n"
    if ($sidecarLine -cne $expectedSidecarLine -or (Get-Sha256Hex -Path $packagePath) -ne $packageHash) {
        throw '.ymme.sha256の再読込検証に失敗しました。'
    }

    Write-Host "リリース候補を生成し、再読込検証しました: $outputDirectory"
    Write-Host "HEAD: $head"
    Write-Host "ProductVersion: $expectedProductVersion"
    Write-Host "ymme SHA-256: $packageHash"
}
finally {
    Pop-Location
}
