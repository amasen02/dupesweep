# Security policy

DupeSweep is a command-line tool that reads and, when explicitly told to, moves or deletes
files on the local filesystem paths **you** supply. It makes no network calls. The notes below
document the few areas worth understanding before you run `--apply`.

## Security-relevant behaviour

| Area | Behaviour |
|---|---|
| Duplicate confirmation | Files are only ever treated as duplicates after a full SHA-256 hash match — not just matching size or a partial sample. |
| Deletion | `--apply delete` calls `File.Delete` directly and is **permanent**. There is no confirmation prompt; the report printed just before is the only preview you get. |
| Quarantine | `--apply quarantine` (the safer default recommendation) moves duplicates into a folder instead of deleting them, and writes a `manifest.json` so the move can be undone with `dsweep restore`. |
| Restore safety | `dsweep restore` never overwrites an existing file at the destination — if the original path was recreated since quarantine, that entry is skipped and reported, not clobbered. |
| Symlinks | Not followed by default. `--follow-symlinks` opts in with no cycle detection — avoid it on directory trees with symlink loops. |
| Telemetry | None. DupeSweep makes no network calls and reads no files outside the directories you pass it. |

## Reporting a vulnerability

Email `amasen02@gmail.com` with the subject prefix `[SECURITY]`, or open a private
[GitHub security advisory](https://github.com/amasen02/dupesweep/security/advisories/new).
**Do not open a public issue.** Expect acknowledgement within 72 hours.

## Coordinated disclosure window

90 days from acknowledgement, unless mutually extended.
