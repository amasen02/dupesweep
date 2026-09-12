# Safe, reproducible demo

`scripts/demo.ps1` (run with PowerShell 7+, the `pwsh` command) demonstrates the complete
reversible workflow against generated files. It never accepts an input directory to scan or delete. Instead, it creates one
unique fixture below a unique run directory, records evidence, and removes that exact
fixture after the run. Reports, hashes, the manifest copy, and command logs remain for
inspection. A failed run also leaves its evidence and removes only the generated
fixture.

The fixture contains:

- two byte-identical files, which should produce one duplicate group;
- two files with the same length but different bytes, which must not be grouped.

The script then performs these checks:

1. A JSON report is written and the fixture hashes are recorded. The report-only scan
   must leave all files and hashes unchanged.
2. Quarantine is applied to the duplicate set. The generated manifest and quarantine
   file are checked before continuing.
3. Restore dry-run is executed. The quarantine file must still exist, proving that the
   preview does not move anything.
4. Restore is executed. The restored duplicate's SHA-256 must match the recorded
   pre-quarantine hash, and the same-size nonduplicate files must still be present.

By default, evidence is kept in a unique `dist/demo/run-<id>/` directory. Pass a
different output parent only when it is dedicated to this demo:

```powershell
pwsh -File .\scripts\demo.ps1 -OutputDirectory .\artifacts\dupesweep-demo
```

To test a published binary rather than `dotnet run`, pass its path:

```powershell
pwsh -File .\scripts\demo.ps1 -DsweepPath .\dsweep\dsweep.exe
```

The evidence files are:

| File | Contents |
| --- | --- |
| `build.log` | Release build output when using source mode. |
| `report.json` | Initial machine-readable duplicate report. |
| `scan.stderr.txt` | Initial scan diagnostics. |
| `hashes-before.json` | SHA-256 and lengths before applying quarantine. |
| `apply.stderr.txt` | Quarantine diagnostics, including the manifest path. |
| `manifest.json` | Copy of the quarantine manifest retained after fixture cleanup. |
| `restore-dry-run.txt` | Restore preview output. |
| `restore.txt` | Restore output. |
| `hashes-after.json` | SHA-256 and lengths after roundtrip. |
| `demo-summary.json` | Assertions and paths used by the run. |

The demo does not claim a throughput number or a guarantee against hostile file-system
changes. It is a small, deterministic adoption check for report immutability,
same-size filtering, quarantine, dry-run behavior, and byte-preserving restore.
