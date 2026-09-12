[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PublishDirectory,

    [Parameter(Mandatory = $true)]
    [string] $Rid,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory,

    [string] $RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path $PSScriptRoot '..'
}

if ($Rid -notmatch '^[A-Za-z0-9][A-Za-z0-9-]*$') {
    throw "RID contains unsupported characters: $Rid"
}
if ($Version -notmatch '^[A-Za-z0-9][A-Za-z0-9.+_-]*$') {
    throw "Version contains unsupported characters: $Version"
}

$publishPath = (Resolve-Path -LiteralPath $PublishDirectory -ErrorAction Stop).Path
$repoPath = (Resolve-Path -LiteralPath $RepositoryRoot -ErrorAction Stop).Path
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
$binaryName = if ($Rid -like 'win-*') { 'dsweep.exe' } else { 'dsweep' }
$publishedBinary = Join-Path $publishPath $binaryName
$licensePath = Join-Path $repoPath 'LICENSE'
$archiveName = "dsweep-$Version-$Rid.zip"
$archivePath = Join-Path $outputPath $archiveName
$hashPath = "$archivePath.sha256"
$stagePath = Join-Path $outputPath ".package-$Rid-$([guid]::NewGuid().ToString('N'))"

if (-not (Test-Path -LiteralPath $publishedBinary -PathType Leaf)) {
    throw "Published single-file binary not found: $publishedBinary"
}
if (-not (Test-Path -LiteralPath $licensePath -PathType Leaf)) {
    throw "License file not found: $licensePath"
}
if (Test-Path -LiteralPath $archivePath) {
    throw "Refusing to overwrite existing archive: $archivePath"
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
New-Item -ItemType Directory -Path $stagePath -Force | Out-Null

try {
    Copy-Item -LiteralPath $publishedBinary -Destination (Join-Path $stagePath $binaryName)
    Copy-Item -LiteralPath $licensePath -Destination (Join-Path $stagePath 'LICENSE')

    @"
# DupeSweep $Version ($Rid)

The dsweep executable in this archive is self-contained and targets $Rid.

## Usage

    dsweep <directory> [directory2 ...] [options]
    dsweep restore <manifest.json> [--dry-run]

Preview duplicates as JSON:

    dsweep . --json

Move duplicates to a reversible quarantine:

    dsweep . --apply quarantine --quarantine-dir ./quarantine

Restore a quarantine manifest:

    dsweep restore ./quarantine/manifest.json

Run `dsweep --help` for all options. Read the repository documentation for the
complete release and safety guidance.
"@ | Set-Content -LiteralPath (Join-Path $stagePath 'USAGE.md') -Encoding utf8

    Compress-Archive -Path (Join-Path $stagePath '*') -DestinationPath $archivePath -CompressionLevel Optimal
    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
        throw "Archive was not created: $archivePath"
    }

    $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $archiveName" | Set-Content -LiteralPath $hashPath -Encoding ascii
    Write-Output "Archive: $archivePath"
    Write-Output "SHA256:  $hash"
}
finally {
    if (Test-Path -LiteralPath $stagePath) {
        Remove-Item -LiteralPath $stagePath -Recurse -Force
    }
}
