# Contributing to DupeSweep

Bug fixes, tests, documentation, and focused improvements are welcome. Start with a
small, reviewable change and explain the behavior it adds or corrects.

## Before you start

1. Search existing issues and pull requests, then describe your intended change in an issue when the scope is unclear.
2. Fork the repository and create a branch from `master`.
3. Keep one concern per pull request. Avoid drive-by refactors.
4. Use a Conventional Commit prefix such as `fix:`, `feat:`, `test:`, or `docs:`.

For security vulnerabilities, do not open a public issue. Follow [SECURITY.md](SECURITY.md).

## Local checks

The project targets .NET 10. Run the same build and test commands used by CI:

```bash
dotnet restore DupeSweep.slnx
dotnet build DupeSweep.slnx --configuration Release --no-restore
dotnet test DupeSweep.slnx --configuration Release --no-build
```

To exercise the CLI help and the generated filesystem demo (PowerShell 7+ is required):

```bash
dotnet run --project src/DupeSweep -- --help
pwsh -File ./scripts/demo.ps1
```

The demo creates a unique run directory and fixture below the requested output parent,
writes reports, hashes, logs, and a manifest copy there, and removes only that generated
fixture after the run. Do not point it at a directory containing user files.

## Code and test conventions

- Keep nullable reference types enabled and resolve warnings rather than suppressing them.
- Prefer descriptive names and comments that explain a design reason.
- Preserve async cancellation and the existing `ConfigureAwait(false)` convention in library code.
- Keep file-system tests inside their isolated temporary directory.
- Changes to quarantine, restore, or permanent delete must include a data-preservation test.
- Do not add benchmarks or adoption claims without reproducible input data.

## Pull requests

The description should state the problem, resulting behavior, and checks run. Complete
the pull request template. CI must pass on supported Ubuntu and Windows jobs; platform
specific behavior should include a focused test or explain why one is impractical.

Published executables are currently unsigned, so packaging changes must preserve the
SHA-256 manifest and documented source/build relationship.

The [Code of Conduct](CODE_OF_CONDUCT.md) applies to all project spaces.

## Reproducible validation

For changes that affect filesystem scanning or duplicate grouping, include the exact
`dotnet` commands used and the operating system in the pull request description.
Keep fixtures under the test harness's isolated temporary directory and avoid real
user data.
