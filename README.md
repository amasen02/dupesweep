# DupeSweep (`dsweep`)

[![CI](https://github.com/amasen02/dupesweep/actions/workflows/ci.yml/badge.svg)](https://github.com/amasen02/dupesweep/actions/workflows/ci.yml)
[![CodeQL](https://github.com/amasen02/dupesweep/actions/workflows/codeql.yml/badge.svg)](https://github.com/amasen02/dupesweep/actions/workflows/codeql.yml)
[![OpenSSF Scorecard](https://api.securityscorecards.dev/projects/github.com/amasen02/dupesweep/badge)](https://securityscorecards.dev/viewer/?uri=github.com/amasen02/dupesweep)
[![Security Policy](https://img.shields.io/badge/Security-Policy-blue.svg)](.github/SECURITY.md)
[![License: MIT](https://img.shields.io/badge/License-MIT-green)](LICENSE)
[![PRs welcome](https://img.shields.io/badge/PRs-welcome-brightgreen)](CONTRIBUTING.md)
[![Contributor Covenant](https://img.shields.io/badge/Contributor%20Covenant-2.1-blue)](CODE_OF_CONDUCT.md)

DupeSweep finds byte-for-byte duplicate files across directories. A scan reports
duplicates without changing files. When requested, `--apply quarantine` moves the
selected duplicates into a quarantine directory and records their original paths in
a manifest that `dsweep restore` can use.

The scanner narrows work in three steps: it groups files by size, hashes the first
64 KiB of same-size candidates, then computes a full SHA-256 only for quick-hash
matches. Files with the same size but different contents are therefore excluded from
duplicate groups.

## Install a release archive

Release archives are self-contained native builds; a .NET runtime is not required.
Download the archive for the operating system and CPU architecture from the
[Releases page](https://github.com/amasen02/dupesweep/releases), and download
`SHA256SUMS` from the same release. The archive contains the `dsweep` executable and
`LICENSE`.

Verify the downloaded archive before extracting it. On Linux or macOS:

```bash
sha256sum -c SHA256SUMS --ignore-missing       # Linux
shasum -a 256 dsweep-1.0.1-osx-arm64.zip      # macOS; compare its line in SHA256SUMS
```

The archive names use the release version, for example `dsweep-1.0.1-win-x64.zip`,
`dsweep-1.0.1-linux-x64.zip`, `dsweep-1.0.1-osx-arm64.zip`, and
`dsweep-1.0.1-osx-x64.zip`. Replace `1.0.1` below with the version you downloaded.

On Windows PowerShell:

```powershell
(Get-FileHash .\dsweep-1.0.1-win-x64.zip -Algorithm SHA256).Hash
# Compare the value with the matching line in SHA256SUMS.
Expand-Archive .\dsweep-1.0.1-win-x64.zip -DestinationPath .\dsweep
```

The published executable is unsigned because no code-signing certificate is part of
this project. Verify the SHA-256 value and release source before running it; Windows
or macOS may show their normal warning for an unsigned downloaded executable.

### Windows x64

```powershell
Expand-Archive .\dsweep-1.0.1-win-x64.zip -DestinationPath "$HOME\bin\dsweep"
& "$HOME\bin\dsweep\dsweep.exe" --help
```

Add the extracted directory to `PATH` if you want to invoke `dsweep` from any shell.

### Linux x64

```bash
mkdir -p "$HOME/.local/bin/dsweep"
unzip dsweep-1.0.1-linux-x64.zip -d "$HOME/.local/bin/dsweep"
chmod +x "$HOME/.local/bin/dsweep/dsweep"
"$HOME/.local/bin/dsweep/dsweep" --help
```

Add `$HOME/.local/bin/dsweep` to `PATH` if desired.

### macOS

Choose `dsweep-1.0.1-osx-arm64.zip` for Apple Silicon or `dsweep-1.0.1-osx-x64.zip` for
Intel Macs:

```bash
mkdir -p "$HOME/.local/bin/dsweep"
unzip dsweep-1.0.1-osx-arm64.zip -d "$HOME/.local/bin/dsweep" # use osx-x64 on Intel
chmod +x "$HOME/.local/bin/dsweep/dsweep"
"$HOME/.local/bin/dsweep/dsweep" --help
```

Distribution uses GitHub Releases; DupeSweep does not require a paid hosting service
or a hosted account. Network access to GitHub is required to download a release.

### Docker

The repository also contains a Dockerfile for local, framework-dependent use:

```bash
docker build -t dupesweep .
docker run --rm -v "$PWD":/scan dupesweep /scan --json
```

The native release archives are the supported path when you want a standalone
executable on the host.

### Build from source

Building from source requires the .NET 10 SDK:

```bash
git clone https://github.com/amasen02/dupesweep.git
cd dupesweep
dotnet run --project src/DupeSweep -- --help
mkdir -p ./target-folder
dotnet run --project src/DupeSweep -- ./target-folder
```

## Usage

```text
dsweep <dir> [dir2 ...] [options]
dsweep restore <manifest.json> [--dry-run]
```

The directory arguments are positional. There is no `scan` subcommand.

Identify the exact build you are running before reporting a result:

```bash
# Prints e.g. "DupeSweep 1.0.1" and exits without scanning.
dsweep --version
```

Scan and report without changing anything:

```bash
dsweep ~/Downloads
dsweep ~/Photos ~/Backups/Photos --ext .jpg,.png --keep oldest
dsweep . --exclude node_modules --exclude .git --json
dsweep /data --min-size 1M --parallel 8
```

Apply reversible quarantine after reviewing the report:

```bash
dsweep ~/Downloads --apply quarantine
dsweep restore ~/Downloads/.dupesweep-quarantine/manifest.json --dry-run
dsweep restore ~/Downloads/.dupesweep-quarantine/manifest.json
```

Quarantine moves files within the selected filesystem. It does not create free disk
space until you review and remove the quarantine directory yourself. Restore skips an
entry when its destination already exists, so it does not overwrite a newer file.
For safety, a quarantine directory containing an existing `manifest.json` is refused;
choose a new quarantine directory after reviewing or archiving the earlier manifest.
For an irreversible operation, `--apply delete` permanently deletes duplicates and
has no restore path.

### Options

| Flag | Meaning |
| --- | --- |
| `--no-recursive` | Scan only the supplied directories. |
| `--min-size <size>` | Ignore files smaller than this (default 1 byte; `0` includes empty files). Accepts `K`, `M`, and `G`. |
| `--ext <list>` | Restrict files by extension, for example `.jpg,.png`. |
| `--exclude <glob>` | Exclude matching files or directories; repeatable. |
| `--follow-symlinks` | Follow symlinked files and directories. Off by default. |
| `--keep <strategy>` | Keep `first` (default), `oldest`, `newest`, or `shortest-path`. |
| `--apply <mode>` | `quarantine` or irreversible `delete`; omit to report only. |
| `--quarantine-dir <path>` | Quarantine destination; defaults to `<first-dir>/.dupesweep-quarantine`. |
| `-j, --parallel <n>` | Hashing worker count, from 1 to 64. |
| `--json` | Emit a machine-readable JSON report. |
| `-q, --quiet` / `-v, --verbose` | Reduce output or include skipped-file warnings. |
| `--version` | Print the DupeSweep version and exit without scanning. |

Run the reproducible filesystem demo with:

```powershell
pwsh -File .\scripts\demo.ps1
```

The demo requires PowerShell 7 or later (`pwsh`).

See [docs/safe-demo.md](docs/safe-demo.md) for the generated evidence and safety
properties.

## Development

```bash
dotnet build DupeSweep.slnx -c Release
dotnet test DupeSweep.slnx
```

The test suite uses isolated temporary directories and covers scanning and filtering,
the size/quick-hash/full-hash funnel, keep strategies, quarantine and restore, CLI
parsing, and text/JSON reports. See [CONTRIBUTING.md](CONTRIBUTING.md) before opening
a pull request, and see [docs/contribution-opportunities.md](docs/contribution-opportunities.md)
for source-grounded starter tasks.

## Project layout

```text
src/DupeSweep/
  Program.cs              CLI orchestration
  CommandLine.cs          argument parsing and usage
  BuildInfo.cs            release version read from assembly metadata
  FileScanner.cs          directory walk and filters
  Hashing.cs              quick and full SHA-256 hashing
  DuplicateFinder.cs      duplicate grouping
  KeepSelector.cs         keep-file selection
  QuarantineService.cs    reversible quarantine and manifest
  RestoreService.cs       manifest restore with no-overwrite behavior
  ReportWriter.cs         text and JSON reports
tests/DupeSweep.Tests/     filesystem-backed unit tests
```

DupeSweep is licensed under the [MIT License](LICENSE). Security reports should follow
the private process in [SECURITY.md](SECURITY.md).

There is no contributor license agreement. Contributions remain under the MIT License.

[![Star History Chart](https://api.star-history.com/svg?repos=amasen02/dupesweep&type=Date)](https://star-history.com/#amasen02/dupesweep&Date)
