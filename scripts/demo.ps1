[CmdletBinding()]
param(
    [string]$DsweepPath,
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'dist/demo')
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputParent = [IO.Path]::GetFullPath($OutputDirectory)
$runRoot = Join-Path $outputParent ('run-' + [Guid]::NewGuid().ToString('N'))
$fixtureRoot = Join-Path $runRoot 'fixture'
$fixtureCreated = $false

if ($DsweepPath) {
    $script:DsweepPath = (Resolve-Path -LiteralPath $DsweepPath).Path
}
$script:repoRoot = $repoRoot

function Invoke-DupeSweep {
    param(
        [string[]]$Arguments,
        [string]$StdoutPath,
        [string]$StderrPath
    )

    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        if ($script:DsweepPath) {
            & $script:DsweepPath @Arguments 1> $StdoutPath 2> $StderrPath
        }
        else {
            & dotnet run --project (Join-Path $script:repoRoot 'src/DupeSweep') --configuration Release --no-build -- @Arguments 1> $StdoutPath 2> $StderrPath
        }
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
    }

    if ($exitCode -ne 0) {
        throw "dsweep failed with exit code $exitCode. See $StderrPath"
    }
}

function Get-FileEvidence {
    param([string]$Path)

    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    [ordered]@{ path = $Path; length = (Get-Item -LiteralPath $Path).Length; sha256 = $hash }
}

try {
    New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
    $fixtureCreated = $true

    if (-not $script:DsweepPath) {
        $buildLog = Join-Path $runRoot 'build.log'
        & dotnet build (Join-Path $script:repoRoot 'DupeSweep.slnx') --configuration Release 1> $buildLog 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed with exit code $LASTEXITCODE. See $buildLog"
        }
    }

    # Exact byte arrays make equal lengths and different content independent of text encoding.
    $duplicateBytes = [byte[]](0..255 | ForEach-Object { $_ })
    $differentBytes = [byte[]](255..0 | ForEach-Object { $_ })
    $duplicateA = Join-Path $fixtureRoot 'duplicate-a.bin'
    $duplicateB = Join-Path $fixtureRoot 'duplicate-b.bin'
    $differentA = Join-Path $fixtureRoot 'same-size-a.bin'
    $differentB = Join-Path $fixtureRoot 'same-size-b.bin'
    [IO.File]::WriteAllBytes($duplicateA, $duplicateBytes)
    [IO.File]::WriteAllBytes($duplicateB, $duplicateBytes)
    [IO.File]::WriteAllBytes($differentA, $differentBytes)
    # Keep the same length while making the second file's bytes different.
    $differentBytes[0] = 254
    [IO.File]::WriteAllBytes($differentB, $differentBytes)

    $fixtureFiles = @($duplicateA, $duplicateB, $differentA, $differentB)
    $before = @($fixtureFiles | ForEach-Object { Get-FileEvidence $_ })
    $before | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $runRoot 'hashes-before.json')

    $reportPath = Join-Path $runRoot 'report.json'
    $scanStderr = Join-Path $runRoot 'scan.stderr.txt'
    Invoke-DupeSweep @($fixtureRoot, '--json') $reportPath $scanStderr
    $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
    if ($report.summary.groupCount -ne 1 -or $report.summary.duplicateFileCount -ne 1) {
        throw "expected one duplicate group and one duplicate file; got $($report.summary.groupCount) groups and $($report.summary.duplicateFileCount) duplicates"
    }
    $reportedDuplicate = @($report.groups[0].duplicates | Where-Object { $_ -match 'duplicate-[ab]\.bin' })
    if ($reportedDuplicate.Count -ne 1) {
        throw 'the report did not identify exactly one generated duplicate file'
    }
    if (($report.groups | ConvertTo-Json -Depth 5) -match 'same-size-[ab]\.bin') {
        throw 'same-size files with different bytes were incorrectly reported as duplicates'
    }
    foreach ($entry in $before) {
        if (-not (Test-Path -LiteralPath $entry.path)) { throw "report-only scan moved or deleted $($entry.path)" }
        if ((Get-FileEvidence $entry.path).sha256 -ne $entry.sha256) { throw "report-only scan changed $($entry.path)" }
    }

    $quarantineRoot = Join-Path $fixtureRoot '.dupesweep-quarantine'
    $applyStdout = Join-Path $runRoot 'apply.stdout.txt'
    $applyStderr = Join-Path $runRoot 'apply.stderr.txt'
    Invoke-DupeSweep @($fixtureRoot, '--apply', 'quarantine', '--quarantine-dir', $quarantineRoot) $applyStdout $applyStderr
    $manifestPath = Join-Path $quarantineRoot 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'quarantine did not produce manifest.json' }
    $manifest = @(Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json)
    if ($manifest.Count -ne 1 -or -not (Test-Path -LiteralPath $manifest[0].quarantinePath)) {
        throw 'quarantine manifest does not describe the moved duplicate'
    }
    $manifestEvidencePath = Join-Path $runRoot 'manifest.json'
    Copy-Item -LiteralPath $manifestPath -Destination $manifestEvidencePath
    $quarantineBeforeDryRun = Get-FileEvidence $manifest[0].quarantinePath
    if (Test-Path -LiteralPath $manifest[0].originalPath) {
        throw 'quarantine did not move the duplicate away from its original path'
    }
    foreach ($entry in $before) {
        if ($entry.path -eq $manifest[0].originalPath) { continue }
        if (-not (Test-Path -LiteralPath $entry.path)) { throw "quarantine changed nonduplicate file $($entry.path)" }
        if ((Get-FileEvidence $entry.path).sha256 -ne $entry.sha256) { throw "quarantine changed $($entry.path)" }
    }

    $dryRunPath = Join-Path $runRoot 'restore-dry-run.txt'
    $dryRunErr = Join-Path $runRoot 'restore-dry-run.stderr.txt'
    Invoke-DupeSweep @('restore', $manifestPath, '--dry-run') $dryRunPath $dryRunErr
    if (Test-Path -LiteralPath $manifest[0].originalPath) { throw 'restore dry-run recreated the original file' }
    if (-not (Test-Path -LiteralPath $manifest[0].quarantinePath)) { throw 'restore dry-run moved the quarantined file' }
    if ((Get-FileEvidence $manifest[0].quarantinePath).sha256 -ne $quarantineBeforeDryRun.sha256) {
        throw 'restore dry-run changed the quarantined file'
    }

    $restorePath = Join-Path $runRoot 'restore.txt'
    $restoreErr = Join-Path $runRoot 'restore.stderr.txt'
    Invoke-DupeSweep @('restore', $manifestPath) $restorePath $restoreErr
    if (-not (Test-Path -LiteralPath $manifest[0].originalPath)) { throw 'restore did not recreate the original duplicate path' }
    $after = @($fixtureFiles | ForEach-Object { Get-FileEvidence $_ })
    $after | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $runRoot 'hashes-after.json')
    foreach ($entry in $before) {
        $restored = $after | Where-Object { $_.path -eq $entry.path }
        if (-not $restored -or $restored.sha256 -ne $entry.sha256 -or $restored.length -ne $entry.length) {
            throw "roundtrip changed $($entry.path)"
        }
    }

    [ordered]@{
        status = 'passed'
        fixture = $fixtureRoot
        report = $reportPath
        manifest = $manifestEvidencePath
        duplicateGroups = $report.summary.groupCount
        duplicateFiles = $report.summary.duplicateFileCount
        sameSizeNonduplicatesExcluded = $true
        roundtripHashesMatch = $true
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot 'demo-summary.json')
    Write-Output "DupeSweep safe demo passed. Evidence: $runRoot"
}
finally {
    if ($fixtureCreated -and (Test-Path -LiteralPath $fixtureRoot)) {
        # This path was generated above; never remove a caller-supplied input path.
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
