# Contribution opportunities

These are small, source-grounded gaps that can be worked on independently. They are
suggestions, not a request for a fixed number of contributions.

## Add `--version` output ([#18](https://github.com/amasen02/dupesweep/issues/18))

`CommandLine.Parse` currently handles help and scan options but has no version option.
Users of downloaded archives need a stable way to identify the executable before
reporting a result.

Acceptance criteria:

- `dsweep --version` prints the product name and version, then exits successfully
  without scanning a directory.
- The version comes from one maintained project/package value rather than a duplicated
  string in the parser and entry point.
- Parsing and exit behavior are covered by tests, including `--version` before a path.
- Existing help, restore, and scan behavior remains unchanged.

## Surface directory enumeration warnings ([#19](https://github.com/amasen02/dupesweep/issues/19))

`FileScanner.ListEntries` catches `UnauthorizedAccessException` and `IOException` and
currently returns empty lists. As a result, `--verbose` cannot explain why a readable
root produced fewer files than expected.

Acceptance criteria:

- A scan continues past an inaccessible child directory instead of failing the whole
  operation.
- Verbose output includes the affected path and the error category/message, while
  default output remains quiet about skipped entries.
- The warning path is testable without depending on a machine-specific protected
  directory (for example, through a narrow injectable enumeration boundary or a
  deterministic test seam).
- No permission bypass or broader filesystem access is introduced.

## Add follow-symlink cycle protection ([#20](https://github.com/amasen02/dupesweep/issues/20))

`--follow-symlinks` currently walks reparse-point directories recursively and the CLI
documents that it has no cycle detection. A linked directory cycle can therefore make
an opted-in scan revisit the same tree indefinitely.

Acceptance criteria:

- Follow-symlink scans track visited directory identities or canonical paths and stop
  revisiting a directory.
- The default behavior, which skips reparse points, remains unchanged.
- A platform-appropriate test proves a small linked-directory cycle terminates and
  does not duplicate entries in the result.
- The implementation documents platform limitations where directory identity cannot
  be resolved reliably.

For all three tasks, keep the change focused, include tests for changed behavior, and
run the build and test commands in [CONTRIBUTING.md](../CONTRIBUTING.md).
