[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $BinaryPath,

    [string] $EvidenceDirectory,

    [switch] $KeepFixture
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-TreeSnapshot {
    param([Parameter(Mandatory = $true)][string] $Directory)

    if (-not (Test-Path -LiteralPath $Directory -PathType Container)) {
        return ''
    }

    $rootPath = (Resolve-Path -LiteralPath $Directory).Path
    $rootPath = $rootPath.TrimEnd([char[]]@('\', '/')) + [IO.Path]::DirectorySeparatorChar
    $items = Get-ChildItem -LiteralPath $Directory -File -Recurse | Sort-Object FullName
    $snapshot = foreach ($item in $items) {
        [pscustomobject]@{
            Path = $item.FullName.Substring($rootPath.Length)
            Length = $item.Length
            SHA256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
        }
    }
    if ($null -eq $snapshot) {
        return ''
    }
    return ($snapshot | ConvertTo-Json -Compress)
}

function Assert-SameSnapshot {
    param(
        [Parameter(Mandatory = $true)][string] $Before,
        [Parameter(Mandatory = $true)][string] $After,
        [Parameter(Mandatory = $true)][string] $Description
    )

    if ($Before -cne $After) {
        throw "Fixture changed during $Description."
    }
}

function Write-FixtureBytes {
    param([Parameter(Mandatory = $true)][string] $Path, [Parameter(Mandatory = $true)][byte[]] $Bytes)

    $parent = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    [IO.File]::WriteAllBytes($Path, $Bytes)
}

function Invoke-Dsweep {
    param(
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [Parameter(Mandatory = $true)][string] $StdoutPath,
        [Parameter(Mandatory = $true)][string] $StderrPath
    )

    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $script:resolvedBinary @Arguments 1> $StdoutPath 2> $StderrPath
        return $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
    }
}

$resolvedBinary = (Resolve-Path -LiteralPath $BinaryPath -ErrorAction Stop).Path
$binaryDirectory = Split-Path -Parent $resolvedBinary
if (-not (Test-Path -LiteralPath (Join-Path $binaryDirectory 'LICENSE') -PathType Leaf)) {
    throw "Package is missing LICENSE beside the binary."
}
if (-not (Test-Path -LiteralPath (Join-Path $binaryDirectory 'USAGE.md') -PathType Leaf)) {
    throw "Package is missing USAGE.md beside the binary."
}

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) "dupesweep-smoke-$([guid]::NewGuid().ToString('N'))"
$fixture = Join-Path $fixtureRoot 'fixture'
$quarantine = Join-Path $fixtureRoot 'quarantine'
$logs = if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    Join-Path $fixtureRoot 'logs'
}
else {
    [IO.Path]::GetFullPath($EvidenceDirectory)
}
$created = $false

try {
    New-Item -ItemType Directory -Path $fixture, $quarantine, $logs -Force | Out-Null
    $created = $true

    $duplicateBytes = [byte[]](0..127)
    $otherBytes = [byte[]](128..255)
    Write-FixtureBytes (Join-Path $fixture 'keep.bin') $duplicateBytes
    Write-FixtureBytes (Join-Path $fixture 'nested\copy.bin') $duplicateBytes
    Write-FixtureBytes (Join-Path $fixture 'same-size-but-unique.bin') $otherBytes

    $initialSnapshot = Get-TreeSnapshot $fixture
    $reportPath = Join-Path $logs 'report.json'
    $reportErrorPath = Join-Path $logs 'report.stderr'
    $exitCode = Invoke-Dsweep @($fixture, '--json', '--quiet') $reportPath $reportErrorPath
    if ($exitCode -ne 0) { throw "JSON report failed with exit code $exitCode." }

    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if ($report.summary.groupCount -ne 1 -or $report.summary.duplicateFileCount -ne 1) {
        throw "Expected one duplicate group with one duplicate file in the report."
    }
    if (-not (Test-Path -LiteralPath (Join-Path $fixture 'same-size-but-unique.bin') -PathType Leaf)) {
        throw 'Same-size nonduplicate fixture is missing before apply.'
    }
    Assert-SameSnapshot $initialSnapshot (Get-TreeSnapshot $fixture) 'report generation'

    $applyOutput = Join-Path $logs 'apply.stdout'
    $applyError = Join-Path $logs 'apply.stderr'
    $exitCode = Invoke-Dsweep @($fixture, '--apply', 'quarantine', '--quarantine-dir', $quarantine, '--quiet') $applyOutput $applyError
    if ($exitCode -ne 0) { throw "Quarantine apply failed with exit code $exitCode." }

    $manifestPath = Join-Path $quarantine 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw 'Quarantine apply did not create a manifest.'
    }
    $manifest = @(Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json)
    if ($manifest.Count -ne 1) { throw "Expected one manifest entry, found $($manifest.Count)." }
    $entry = $manifest[0]
    if (Test-Path -LiteralPath $entry.originalPath -PathType Leaf) {
        throw 'Quarantined duplicate still exists at its original path.'
    }
    if (-not (Test-Path -LiteralPath $entry.quarantinePath -PathType Leaf)) {
        throw 'Manifest quarantine path does not exist.'
    }
    $originalHash = (Get-FileHash -LiteralPath (Join-Path $fixture 'keep.bin') -Algorithm SHA256).Hash
    $quarantineHash = (Get-FileHash -LiteralPath $entry.quarantinePath -Algorithm SHA256).Hash
    if ($originalHash -cne $quarantineHash) { throw 'Quarantine changed duplicate bytes.' }
    if (-not (Test-Path -LiteralPath (Join-Path $fixture 'same-size-but-unique.bin') -PathType Leaf)) {
        throw 'Same-size nonduplicate was moved by quarantine.'
    }

    $beforeDryRun = "$(Get-TreeSnapshot $fixture)`n$(Get-TreeSnapshot $quarantine)"
    $dryOutput = Join-Path $logs 'restore-dry-run.stdout'
    $dryError = Join-Path $logs 'restore-dry-run.stderr'
    $exitCode = Invoke-Dsweep @('restore', $manifestPath, '--dry-run') $dryOutput $dryError
    if ($exitCode -ne 0) { throw "Restore dry-run failed with exit code $exitCode." }
    if (-not ((Get-Content -LiteralPath $dryOutput -Raw) -match 'restore preview')) {
        throw 'Restore dry-run did not report preview mode.'
    }
    $afterDryRun = "$(Get-TreeSnapshot $fixture)`n$(Get-TreeSnapshot $quarantine)"
    Assert-SameSnapshot $beforeDryRun $afterDryRun 'restore dry-run'

    $restoreOutput = Join-Path $logs 'restore.stdout'
    $restoreError = Join-Path $logs 'restore.stderr'
    $exitCode = Invoke-Dsweep @('restore', $manifestPath) $restoreOutput $restoreError
    if ($exitCode -ne 0) { throw "Restore failed with exit code $exitCode." }
    Assert-SameSnapshot $initialSnapshot (Get-TreeSnapshot $fixture) 'restore'
    if (Test-Path -LiteralPath $entry.quarantinePath) { throw 'Restore left a quarantined file behind.' }

    $beforeRefusal = Get-TreeSnapshot $fixture
    Write-FixtureBytes (Join-Path $fixture 'second-copy.bin') $duplicateBytes
    $beforeRefusal = Get-TreeSnapshot $fixture
    $refusalOutput = Join-Path $logs 'overwrite-refusal.stdout'
    $refusalError = Join-Path $logs 'overwrite-refusal.stderr'
    $exitCode = Invoke-Dsweep @($fixture, '--apply', 'quarantine', '--quarantine-dir', $quarantine, '--quiet') $refusalOutput $refusalError
    if ($exitCode -eq 0) { throw 'Quarantine unexpectedly reused an existing manifest.' }
    Assert-SameSnapshot $beforeRefusal (Get-TreeSnapshot $fixture) 'existing-manifest refusal'

    $invalidOutput = Join-Path $logs 'invalid-option.stdout'
    $invalidError = Join-Path $logs 'invalid-option.stderr'
    $exitCode = Invoke-Dsweep @('--definitely-invalid') $invalidOutput $invalidError
    if ($exitCode -ne 2) { throw "Invalid CLI option returned $exitCode instead of 2." }

    $overwriteQuarantine = Join-Path $fixtureRoot 'overwrite-quarantine'
    $overwriteOriginal = Join-Path $fixture 'restore-sentinel.bin'
    $overwritePayload = Join-Path $overwriteQuarantine 'payload.bin'
    $overwriteManifest = Join-Path $overwriteQuarantine 'manifest.json'
    $sentinelBytes = [byte[]](7..18)
    $payloadBytes = [byte[]](19..30)
    Write-FixtureBytes $overwriteOriginal $sentinelBytes
    Write-FixtureBytes $overwritePayload $payloadBytes
    $payloadHash = (Get-FileHash -LiteralPath $overwritePayload -Algorithm SHA256).Hash
    $overwriteManifestJson = ConvertTo-Json -InputObject @([ordered]@{
        originalPath = $overwriteOriginal
        quarantinePath = $overwritePayload
        length = $payloadBytes.Length
        hash = (Get-FileHash -LiteralPath $overwritePayload -Algorithm SHA256).Hash
    })
    $overwriteManifestJson | Set-Content -LiteralPath $overwriteManifest -Encoding utf8
    $sentinelBefore = Get-TreeSnapshot $fixture
    $payloadBefore = Get-TreeSnapshot $overwriteQuarantine
    $overwriteOutput = Join-Path $logs 'restore-overwrite.stdout'
    $overwriteError = Join-Path $logs 'restore-overwrite.stderr'
    $exitCode = Invoke-Dsweep @('restore', $overwriteManifest) $overwriteOutput $overwriteError
    if ($exitCode -eq 0) { throw 'Restore unexpectedly overwrote an existing destination.' }
    Assert-SameSnapshot $sentinelBefore (Get-TreeSnapshot $fixture) 'restore destination refusal'
    Assert-SameSnapshot $payloadBefore (Get-TreeSnapshot $overwriteQuarantine) 'restore destination refusal'

    Remove-Item -LiteralPath $overwriteOriginal -Force
    $overwriteRestoreOutput = Join-Path $logs 'restore-overwrite-retry.stdout'
    $overwriteRestoreError = Join-Path $logs 'restore-overwrite-retry.stderr'
    $exitCode = Invoke-Dsweep @('restore', $overwriteManifest) $overwriteRestoreOutput $overwriteRestoreError
    if ($exitCode -ne 0) { throw "Restore after clearing owned sentinel failed with exit code $exitCode." }
    $restoredHash = (Get-FileHash -LiteralPath $overwriteOriginal -Algorithm SHA256).Hash
    if ($restoredHash -cne $payloadHash) { throw 'Restore destination test changed payload bytes.' }

    Write-Output "Smoke passed: report, same-size exclusion, quarantine, SHA256 preservation, dry-run, restore, manifest refusal, destination refusal, and CLI error exit."
}
catch {
    $failurePath = Join-Path $logs 'smoke-failure.txt'
    $_ | Out-String | Set-Content -LiteralPath $failurePath -Encoding utf8
    Write-Error $_
    exit 1
}
finally {
    if ($created -and -not $KeepFixture -and (Test-Path -LiteralPath $fixtureRoot)) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
    elseif ($created -and $KeepFixture) {
        Write-Output "Smoke fixture retained at: $fixtureRoot"
    }
}
