# Contributing to DupeSweep

Pull requests are welcome — bug fixes, new keep/apply strategies, better docs —
provided they keep the tone and quality of the codebase.

## Ground rules

1. **One concern per pull request.** No drive-by refactors mixed with feature work.
2. **Branch from `master`**, keep the branch short, and squash-merge back.
3. **Conventional commits** (`feat:`, `fix:`, `perf:`, `refactor:`, `test:`, `docs:`, `chore:`).
4. **Green CI is non-negotiable.** `dotnet build` and `dotnet test` must pass before review.
5. **The PR template must be filled.** Empty checkboxes block review.

## Coding standards

- **C# latest, nullable enabled.** No `#nullable disable`; resolve warnings, don't suppress them.
- **Intention-revealing names.** Full descriptive identifiers; `c`, `tmp`, `mgr` are rejected.
- **Comments explain *why*, never *what*.** No filler comments.
- **Async correctness.** Async methods take a `CancellationToken` and propagate it;
  library-level awaits use `ConfigureAwait(false)` (the existing convention here).
- **SOLID / KISS / DRY / YAGNI.** One responsibility per type; the simplest correct solution wins.
- **File I/O is destructive by nature here.** Any change touching `QuarantineService`,
  `RestoreService`, or the `--apply delete` path needs a test that proves data is never
  silently lost (see the existing collision/skip tests for the pattern).

## Build, test, run

```bash
dotnet build DupeSweep.slnx -c Release
dotnet test  DupeSweep.slnx                          # unit + filesystem-backed tests
dotnet run --project src/DupeSweep -- --help         # exercise the CLI
```

## Tests

A pull request that ships behaviour without a test is sent back unless it is purely documentation.

Tests use a real, isolated temp directory per test (see `tests/DupeSweep.Tests/Support/TempDirectory.cs`)
and clean up after themselves. Do not add tests that touch paths outside their own temp directory.

## Reporting bugs and proposing features

Use the issue templates. For security vulnerabilities, **do not open a public issue** — follow
[`SECURITY.md`](SECURITY.md).
