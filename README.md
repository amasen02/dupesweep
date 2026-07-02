# DupeSweep (`dsweep`) — a fast duplicate-file finder and reclaimer

[![CI](https://github.com/amasen02/dupesweep/actions/workflows/ci.yml/badge.svg)](https://github.com/amasen02/dupesweep/actions/workflows/ci.yml)
[![CodeQL](https://github.com/amasen02/dupesweep/actions/workflows/codeql.yml/badge.svg)](https://github.com/amasen02/dupesweep/actions/workflows/codeql.yml)
[![.NET](https://img.shields.io/badge/.NET-10-blueviolet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green)](LICENSE)
[![PRs welcome](https://img.shields.io/badge/PRs-welcome-brightgreen)](CONTRIBUTING.md)
[![Contributor Covenant](https://img.shields.io/badge/Contributor%20Covenant-2.1-blue)](CODE_OF_CONDUCT.md)

Find exact duplicate files across one or more directories and reclaim the wasted space —
safely. DupeSweep never deletes anything by default: it reports first, and when you do ask it
to act, `--apply quarantine` moves duplicates into a folder with a manifest so the move can be
undone with `dsweep restore`.

## Why another duplicate finder

Most "find duplicates" scripts either trust file size alone (wrong — two different files can
be the same size) or hash every file up front (slow — hashing a 4 GB video you'll never
compare against anything is wasted work). DupeSweep runs a three-stage funnel so the expensive
work only happens on real candidates:

| Stage | Cost | What it does |
|---|---|---|
| **1. Group by size** | Free (already have it from the directory listing) | Files with a unique size can't have a duplicate — discarded immediately. |
| **2. Quick hash** | Cheap | SHA-256 of just the first 64 KB, computed in parallel across CPU cores. Narrows same-size files down to real candidates. |
| **3. Full hash** | Only for survivors | Full-file SHA-256 confirms a true byte-for-byte match before anything is reported as a duplicate. |

The result: a `~/Downloads` or photo library scan with tens of thousands of files only fully
hashes the handful that are actually worth comparing.

## Install

DupeSweep targets **.NET 10**. Build a single-file executable from source:

```bash
git clone https://github.com/amasen02/dupesweep.git
cd dupesweep

# Framework-dependent single file (uses the installed .NET 10 runtime):
dotnet publish src/DupeSweep -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
#   Linux:  -r linux-x64        macOS (Apple Silicon):  -r osx-arm64

# …or fully self-contained (no runtime needed on the target machine):
dotnet publish src/DupeSweep -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o dist
```

The binary is named **`dsweep`** (`dist/dsweep`, or `dist\dsweep.exe` on Windows). Put `dist` on
your `PATH` to call `dsweep` from anywhere. To run without publishing:

```bash
dotnet run --project src/DupeSweep -- <dir> [options]
```

## Usage

```
dsweep <dir> [dir2 ...] [options]
dsweep restore <manifest.json> [--dry-run]
```

### Scan and report (the default — nothing is ever touched)

```bash
dsweep ~/Downloads
```

```
[1] 4.2 MB x 3 copies  (sha256 74c18fa049de…)
    KEEP      /home/ama/Downloads/report.pdf
    DUPLICATE /home/ama/Downloads/report (1).pdf
    DUPLICATE /home/ama/Downloads/old/report.pdf

dsweep: 1 duplicate group(s), 2 reclaimable file(s), 8.4 MB reclaimable.
```

### Common options

```bash
# Only image files, keep the oldest copy of each duplicate
dsweep ~/Photos ~/Backups/Photos --ext .jpg,.png --keep oldest

# Skip noisy directories, machine-readable output
dsweep . --exclude node_modules --exclude .git --json

# Ignore anything smaller than 1 MB, cap hashing to 8 threads
dsweep /data --min-size 1M --parallel 8
```

### Reclaim the space — safely

```bash
dsweep ~/Downloads --apply quarantine
```

This moves every duplicate (never the file chosen to keep) into
`~/Downloads/.dupesweep-quarantine/`, grouped by duplicate set, and writes a `manifest.json`
recording each file's original location. Review the quarantine folder, then either delete it
once you're confident, or undo the whole thing:

```bash
dsweep restore ~/Downloads/.dupesweep-quarantine/manifest.json --dry-run   # preview
dsweep restore ~/Downloads/.dupesweep-quarantine/manifest.json            # actually restore
```

Restore never overwrites a file that already exists at the destination — if you recreated a
file since quarantining it, that entry is skipped and reported instead of clobbering your work.

For an irreversible cleanup once you've reviewed the report, `--apply delete` permanently
deletes duplicates instead of quarantining them. There is no undo for `delete` — use
`quarantine` unless you are certain.

### All options

| Flag | Meaning |
|---|---|
| `--no-recursive` | Only scan the given directories, not subdirectories. |
| `--min-size <size>` | Ignore files smaller than this (default 1 byte; `0` includes empty files). Accepts `K`/`M`/`G` suffixes. |
| `--ext <list>` | Only consider these extensions, e.g. `.jpg,.png`. |
| `--exclude <glob>` | Exclude files/directories matching a glob (repeatable), e.g. `node_modules`, `*.tmp`. |
| `--follow-symlinks` | Follow symlinked files and directories (off by default; no cycle detection). |
| `--keep <strategy>` | Which copy survives per group: `first` (default, scan order), `oldest`, `newest`, `shortest-path`. |
| `--apply <mode>` | `quarantine` (reversible, recommended) or `delete` (permanent). Omit to just report. |
| `--quarantine-dir <path>` | Destination for `--apply quarantine` (default: `<first-dir>/.dupesweep-quarantine`). |
| `-j, --parallel <n>` | Hashing worker count (default: CPU core count, max 64). |
| `--json` | Emit a machine-readable JSON report instead of text. |
| `-q / -v` | Quiet / verbose (verbose reports files skipped because they couldn't be read). |

## Tests

```bash
dotnet test DupeSweep.slnx
```

The suite is deterministic: every test that touches the filesystem creates its own temp
directory (see `tests/DupeSweep.Tests/Support/TempDirectory.cs`) and cleans up after itself. It
covers the full pipeline — scanning/filtering, the size → quick-hash → full-hash funnel
(including same-size-but-different-content files, which must **not** be grouped), all four keep
strategies, quarantine collision handling, restore's no-overwrite guarantee, CLI parsing, and
both report formats. CI runs the suite on both Ubuntu and Windows.

## Architecture (separation of concerns)

```
src/DupeSweep/
  Program.cs              CLI orchestration: parse -> scan -> find duplicates -> report -> apply
  CommandLine.cs          argv parsing + usage (scan mode and restore mode)
  ScanOptions.cs          parsed configuration + ApplyMode/KeepStrategy enums
  FileScanner.cs          recursive walk with size/extension/exclude/symlink filtering
  Hashing.cs              two-tier SHA-256: 64 KB quick hash, then full-file hash
  DuplicateFinder.cs      the size -> quick-hash -> full-hash funnel, parallelised
  KeepSelector.cs         chooses which file in a group survives, per strategy
  QuarantineService.cs    moves duplicates to a quarantine folder + writes the manifest
  RestoreService.cs       reads a manifest and moves files back, never overwriting
  ReportWriter.cs         text and JSON rendering
  Format.cs / Models.cs   byte/duration formatting + records
tests/DupeSweep.Tests/    xUnit tests against isolated temp directories
```

## Contributing

Contributions are welcome — bug fixes, new keep/apply strategies, better docs. See
[`CONTRIBUTING.md`](CONTRIBUTING.md) for the workflow and coding bar, and please be mindful of
the [Code of Conduct](CODE_OF_CONDUCT.md). Use the issue templates; green CI (`build` + `test`
on Ubuntu and Windows) is required on every pull request. Report security issues privately per
[`SECURITY.md`](SECURITY.md) — never as a public issue.

## Open source commitments

This project is, and will remain, free and open source. As maintainer I commit to:

- **A permissive licence, kept stable.** [MIT](LICENSE) — use it commercially, fork it, build on
  it. No relicensing of accepted contributions.
- **No CLA.** Contributions are accepted under the MIT licence; you keep the copyright to your work.
- **An honest history.** Real, walkable commits — no fabricated activity, no rewritten releases.
- **Best-effort, transparent triage.** Issues and pull requests are read and answered; security
  reports are acknowledged within 72 hours.
- **A welcoming community** governed by the [Code of Conduct](CODE_OF_CONDUCT.md).
- **Reproducible builds.** Green CI — build, tests on two OSes, and CodeQL security analysis —
  on every change.

## License

MIT — see [`LICENSE`](LICENSE). You are free to use, modify, and distribute this software,
including for commercial purposes, provided the copyright notice is retained.

## Author

**Ama Senevirathne** — [GitHub](https://github.com/amasen02)
