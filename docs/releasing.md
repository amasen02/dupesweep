# Releasing DupeSweep

DupeSweep releases are OS-specific archives built and exercised by
`.github/workflows/release.yml`. The workflow runs for pull requests targeting
`master` and for a manually dispatched build. It only uploads workflow artifacts;
it does not create a GitHub release or publish a package.

The supported native targets are:

| Runner | Runtime identifier | Archive |
| --- | --- | --- |
| Windows x64 | `win-x64` | `dsweep-<version>-win-x64.zip` |
| Linux x64 | `linux-x64` | `dsweep-<version>-linux-x64.zip` |
| macOS arm64 | `osx-arm64` | `dsweep-<version>-osx-arm64.zip` |
| macOS Intel x64 | `osx-x64` | `dsweep-<version>-osx-x64.zip` |

Each archive contains the self-contained, untrimmed `dsweep` single-file
executable, `LICENSE`, and `USAGE.md`. Native libraries are included for
self-extraction. The workflow extracts each archive into a disposable directory
and runs `scripts/smoke-release.ps1` against that exact extracted executable.

The smoke fixture verifies that JSON reporting does not mutate files, a
same-size nonduplicate is excluded, quarantine moves bytes without changing
their SHA256, restore dry-run does not mutate anything, restore returns the
original bytes, an existing manifest is never overwritten or reused, and an
invalid CLI option returns exit code `2`. A failed platform build or smoke test
fails the workflow; unsupported platforms are not represented by a fabricated
artifact.

## Local verification

From the repository root, build one native package with the .NET 10 SDK:

```powershell
dotnet test DupeSweep.slnx --configuration Release
dotnet publish src/DupeSweep/DupeSweep.csproj --configuration Release `
  --runtime win-x64 --self-contained true --output .tmp_publish `
  -p:PublishSingleFile=true -p:PublishTrimmed=false `
  -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None
./scripts/package-release.ps1 -PublishDirectory .tmp_publish -Rid win-x64 `
  -Version 1.0.1 -OutputDirectory .tmp_artifacts
Expand-Archive .tmp_artifacts/dsweep-1.0.1-win-x64.zip -DestinationPath .tmp_extracted
./scripts/smoke-release.ps1 -BinaryPath .tmp_extracted/dsweep.exe
Get-FileHash .tmp_artifacts/dsweep-1.0.1-win-x64.zip -Algorithm SHA256
```

Use `-KeepFixture` with the smoke script when investigating a failed local run:

```powershell
./scripts/smoke-release.ps1 -BinaryPath .tmp_extracted/dsweep.exe -KeepFixture
```

The script accepts the same `-BinaryPath` contract on Unix; pass the extracted
`dsweep` path without `.exe` there and run `chmod +x` after ZIP extraction. It
creates its own uniquely named temporary fixture and removes that exact directory
after a successful or failed run unless `-KeepFixture` is supplied. CI passes
`-EvidenceDirectory` to retain stdout, stderr, and failure receipts in the
uploaded artifact even when a smoke gate fails.

## Human release procedure

After review, the release owner rebuilds the merged commit through the same
workflow inputs, downloads the four tested archives and `SHA256SUMS`, and checks
that the downloaded hashes match the tested artifacts. The owner also verifies
each per-archive `.sha256` file and the aggregate `SHA256SUMS`. The workflow
stores a `SOURCE-COMMIT-<rid>.txt` receipt beside each archive. The owner then creates
the scoped patch release and attaches those exact archives plus `SHA256SUMS`.
The release process has no code-signing certificates, so archives are not
presented as signed binaries. Release notes should link to `USAGE.md` and state
the supported runtime identifiers accurately.

Quarantine is reversible storage: moving files on the same volume does not
reclaim free disk space. Users should inspect the JSON or text report before
applying an action, keep the manifest, and use `dsweep restore` when they need
to return quarantined files.
